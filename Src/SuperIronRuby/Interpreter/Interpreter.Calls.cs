using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

// Method calls and short-circuit boolean operators (task I2).
public sealed partial class Interpreter
{
    private object? EvalCall(CallNode node, RubyScope scope)
    {
        bool implicitSelf = node.Receiver is null;
        object? receiver = implicitSelf ? scope.Self : Eval(node.Receiver, scope);

        // Safe navigation: `recv&.foo` short-circuits to nil on a nil receiver.
        if (node.IsSafeNavigation() && receiver is null)
            return null;

        var (args, block) = EvalArguments(node, scope);

        var flags = implicitSelf ? RubyCallFlags.ImplicitSelf : RubyCallFlags.None;

        object? result;
        // A break out of a block literal unwinds to the call that received it.
        if (node.Block is BlockNode blockLit)
        {
            try
            {
                result = _context.Send(receiver, node.Name, args, block, flags, scope.Self);
            }
            catch (BreakUnwind br) when (br.SourceId == blockLit.NodeId)
            {
                return br.Value;
            }
        }
        else
        {
            result = _context.Send(receiver, node.Name, args, block, flags, scope.Self);
        }

        // Attribute writes (`recv.foo = v`) evaluate to the assigned value, not
        // the setter's return.
        if (node.IsAttributeWrite() && args.Length > 0)
            return args[^1];

        return result;
    }

    /// <summary>Evaluates a call's arguments and block into a flat positional
    /// array plus an optional block. Splats are expanded; a trailing keyword hash
    /// is appended as a <see cref="RubyHash"/>; block literals are built by I8.</summary>
    private (object?[] args, RubyProc? block) EvalArguments(CallNode node, RubyScope scope)
    {
        var list = new List<object?>();

        if (node.Arguments is ArgumentsNode argsNode)
        {
            foreach (var argNode in argsNode.Arguments)
            {
                switch (argNode)
                {
                    case SplatNode splat:
                        ExpandSplat(splat, scope, list);
                        break;
                    case KeywordHashNode kw:
                        list.Add(BuildKeywordHash(kw, scope));
                        break;
                    default:
                        list.Add(Eval(argNode, scope));
                        break;
                }
            }
        }

        RubyProc? block = EvalBlockArgument(node, scope);
        return (list.ToArray(), block);
    }

    private void ExpandSplat(SplatNode splat, RubyScope scope, List<object?> into)
    {
        // `*nil` expands to nothing in Ruby 4; otherwise to_a/the array's elements.
        var value = Eval(splat.Expression, scope);
        switch (value)
        {
            case null:
                break;
            case RubyArray arr:
                into.AddRange(arr);
                break;
            default:
                if (_context.Send(value, "to_a", System.Array.Empty<object?>()) is RubyArray a)
                    into.AddRange(a);
                else
                    into.Add(value);
                break;
        }
    }

    // A trailing `key: value` argument hash. Marked with KwArgsHash so the
    // parameter binder (I3) can recognize it when matching keyword parameters.
    private RubyHash BuildKeywordHash(KeywordHashNode node, RubyScope scope)
    {
        var hash = new KwArgsHash();
        foreach (var element in node.Elements)
        {
            switch (element)
            {
                case AssocNode assoc:
                    hash.Store(Eval(assoc.Key, scope), Eval(assoc.Value, scope));
                    break;
                case AssocSplatNode splat when splat.Value is not null:
                    if (Eval(splat.Value, scope) is RubyHash h)
                        foreach (var e in h.Entries()) hash.Store(e.Key, e.Value);
                    break;
            }
        }
        return hash;
    }

    private RubyProc? EvalBlockArgument(CallNode node, RubyScope scope)
    {
        switch (node.Block)
        {
            case null:
                return null;
            case BlockNode blockNode:
                return MakeBlockProc(blockNode, scope);     // implemented in I8
            case BlockArgumentNode blockArg:
                return CoerceToProc(blockArg.Expression is null ? null : Eval(blockArg.Expression, scope));
            default:
                return null;
        }
    }

    private RubyProc? CoerceToProc(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case RubyProc proc:
                return proc;
            case RubySymbol sym:
                // &:name -> a proc that sends `name` to its first argument.
                return new RubyProc(args => _context.Send(args[0], sym.Name, args.Skip(1).ToArray()));
            default:
                return _context.Send(value, "to_proc", System.Array.Empty<object?>()) as RubyProc;
        }
    }

    // Block literals (`foo { ... }` / `foo do ... end`) become RubyProcs that
    // close over `scope`. Full semantics (numbered params, `it`, break/next)
    // arrive in task I8; this minimal version supports a single block parameter
    // and body evaluation so calls-with-blocks work end to end.
    private RubyProc MakeBlockProc(BlockNode node, RubyScope outer)
    {
        return new RubyProc(args =>
        {
            var blockScope = new RubyScope(ScopeKind.Block, outer)
            {
                CurrentBreakId = node.NodeId,   // break in this block targets the call
            };
            BindBlockParametersMinimal(node, args, blockScope);
            while (true)
            {
                try
                {
                    return Eval(node.Body, blockScope);
                }
                catch (NextUnwind n) { return n.Value; }   // next -> this iteration's value
                catch (RedoUnwind) { continue; }            // redo -> re-run the body
            }
        })
        {
            SourceId = node.NodeId,
        };
    }

    // Minimal parameter binding for block literals (I8 generalizes this).
    private void BindBlockParametersMinimal(BlockNode node, object?[] args, RubyScope scope)
    {
        if (node.Parameters is BlockParametersNode { Parameters: ParametersNode pnode })
        {
            var requireds = pnode.Requireds;
            for (int i = 0; i < requireds.Length; i++)
            {
                if (requireds[i] is RequiredParameterNode req)
                    scope.DeclareLocal(req.Name, i < args.Length ? args[i] : null);
            }
        }
    }

    private object? EvalAnd(AndNode node, RubyScope scope)
    {
        var left = Eval(node.Left, scope);
        return Truthy(left) ? Eval(node.Right, scope) : left;
    }

    private object? EvalOr(OrNode node, RubyScope scope)
    {
        var left = Eval(node.Left, scope);
        return Truthy(left) ? left : Eval(node.Right, scope);
    }
}

/// <summary>A keyword-argument hash, distinguished so the parameter binder can
/// route it to keyword parameters (Ruby 3+ keyword separation).</summary>
public sealed class KwArgsHash : RubyHash { }
