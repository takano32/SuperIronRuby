using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

/// <summary>An interpreter-owned method body: the def node plus the unit it came
/// from (for line info). Stored in <see cref="RubyMethodInfo.InterpretedDef"/>.</summary>
internal sealed class InterpretedMethod
{
    public DefNode Node { get; }
    public ParseResult Unit { get; }
    public InterpretedMethod(DefNode node, ParseResult unit) { Node = node; Unit = unit; }
}

// Method definition + invocation, return, yield, super (task I3).
public sealed partial class Interpreter
{
    // Installs the real Ruby-method invoker (replacing the I1 stub).
    private void EnsureInvokerInstalled()
    {
        _context.InterpretedInvoker = InvokeInterpreted;
    }

    private object? EvalDef(DefNode node, RubyScope scope)
    {
        RubyModule target;
        if (node.Receiver is null)
        {
            target = scope.DefinitionTarget ?? _context.ObjectClass;
        }
        else
        {
            // def self.foo / def obj.foo -> singleton method
            var receiver = Eval(node.Receiver, scope);
            target = _context.SingletonClassOf(receiver);
        }

        var method = new RubyMethodInfo(node.Name, target)
        {
            InterpretedDef = new InterpretedMethod(node, _unit!),
            Visibility = RubyMethodVisibility.Public,
        };
        target.DefineMethod(node.Name, method);
        EnsureInvokerInstalled();
        return _context.Intern(node.Name);   // def returns the method name symbol
    }

    private object? InvokeInterpreted(RubyMethodInfo method, object? self, object?[] args, RubyProc? block)
    {
        if (method.InterpretedDef is not InterpretedMethod im)
            throw new InvalidOperationException($"method '{method.Name}' has no interpreted body");

        var scope = new RubyScope(ScopeKind.Method, null)
        {
            Self = self,
            DefinitionTarget = method.Owner,
            Block = block,
            CurrentMethod = method,
        };
        // Constant lookup inside a method uses the lexical nesting captured at
        // definition; approximate with the owner chain (refined in I4).
        scope.LexicalModules.Add(method.Owner);

        var def = im.Node;
        if (def.Parameters is ParametersNode pnode)
            BindParameters(pnode, args, block, scope, BindMode.Method);

        try
        {
            return Eval(def.Body, scope);
        }
        catch (ReturnUnwind ret) when (ret.FrameId == scope.FrameId)
        {
            return ret.Value;
        }
    }

    private object? EvalReturn(ReturnNode node, RubyScope scope)
    {
        object? value = ReturnValue(node.Arguments, scope);
        long target = ReturnTargetFrame(scope);
        throw new ReturnUnwind(value, target);
    }

    // The frame a `return` unwinds to: the nearest enclosing method (blocks
    // return from their defining method), else the top-level frame.
    private static long ReturnTargetFrame(RubyScope scope)
    {
        var s = scope;
        while (s is not null)
        {
            if (s.Kind is ScopeKind.Method or ScopeKind.TopLevel) return s.FrameId;
            s = s.Parent;
        }
        return scope.FrameId;
    }

    private object? ReturnValue(Node? argsNode, RubyScope scope)
    {
        if (argsNode is not ArgumentsNode a || a.Arguments.Length == 0) return null;
        if (a.Arguments.Length == 1) return Eval(a.Arguments[0], scope);
        var arr = new RubyArray();
        foreach (var n in a.Arguments) arr.Add(Eval(n, scope));
        return arr;
    }

    private object? EvalYield(YieldNode node, RubyScope scope)
    {
        var block = FindMethodBlock(scope);
        if (block is null)
            throw _context.RaiseError(_context.LocalJumpErrorClass, "no block given (yield)");
        var (args, _) = EvalArgumentList(node.Arguments, scope);
        return block.Call(args);
    }

    // Walks to the enclosing method scope and returns its block.
    private static RubyProc? FindMethodBlock(RubyScope scope)
    {
        var s = scope;
        while (s is not null)
        {
            if (s.Block is not null) return s.Block;
            if (s.Kind is ScopeKind.Method or ScopeKind.TopLevel) return s.Block;
            s = s.Parent;
        }
        return null;
    }

    private object? EvalSuper(SuperNode node, RubyScope scope)
    {
        var (args, block) = EvalArgumentList(node.Arguments, scope);
        return InvokeSuper(scope, args, block ?? scope.Block);
    }

    private object? EvalForwardingSuper(ForwardingSuperNode node, RubyScope scope)
    {
        // zsuper: re-pass the current method's arguments. We approximate by
        // forwarding no explicit args (the common case for parameterless supers);
        // full argument forwarding is refined alongside parameter capture.
        return InvokeSuper(scope, System.Array.Empty<object?>(), scope.Block);
    }

    private object? InvokeSuper(RubyScope scope, object?[] args, RubyProc? block)
    {
        var method = scope.CurrentMethod
            ?? throw _context.RaiseError(_context.RuntimeErrorClass, "super called outside of method");
        var self = scope.Self;
        var ancestors = _context.GetImmediateClassOf(self).Ancestors;

        // Find the method of the same name AFTER the current method's owner.
        int ownerIndex = -1;
        for (int i = 0; i < ancestors.Count; i++)
        {
            if (ReferenceEquals(ancestors[i], method.Owner)) { ownerIndex = i; break; }
        }
        for (int i = ownerIndex + 1; i < ancestors.Count; i++)
        {
            var m = ancestors[i].GetOwnMethod(method.Name);
            if (m is not null) return _context.Invoke(m, self, args, block);
        }
        throw _context.RaiseError(_context.NoMethodErrorClass,
            $"super: no superclass method '{method.Name}'");
    }

    // Evaluates an ArgumentsNode (used by yield/super) into positional args.
    private (object?[] args, RubyProc? block) EvalArgumentList(Node? argsNode, RubyScope scope)
    {
        if (argsNode is not ArgumentsNode an) return (System.Array.Empty<object?>(), null);
        var list = new List<object?>();
        foreach (var arg in an.Arguments)
        {
            if (arg is SplatNode splat) ExpandSplat(splat, scope, list);
            else list.Add(Eval(arg, scope));
        }
        return (list.ToArray(), null);
    }
}
