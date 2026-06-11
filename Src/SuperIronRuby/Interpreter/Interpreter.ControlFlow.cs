using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

// Conditionals, loops, case/when, begin/rescue/ensure, loop jumps (task I5).
public sealed partial class Interpreter
{
    private static long _loopCounter = 1_000_000; // kept distinct from block node ids

    private object? EvalIf(IfNode node, RubyScope scope)
        => Truthy(Eval(node.Predicate, scope))
            ? Eval(node.Statements, scope)
            : Eval(node.Subsequent, scope);   // elsif chain / ElseNode / null

    private object? EvalUnless(UnlessNode node, RubyScope scope)
        => !Truthy(Eval(node.Predicate, scope))
            ? Eval(node.Statements, scope)
            : Eval(node.ElseClause, scope);

    private object? EvalElse(ElseNode node, RubyScope scope) => Eval(node.Statements, scope);

    private object? EvalWhile(WhileNode node, RubyScope scope)
        => RunLoop(node.Predicate, node.Statements, scope, until: false, beginModifier: IsBeginModifier(node.Flags));

    private object? EvalUntil(UntilNode node, RubyScope scope)
        => RunLoop(node.Predicate, node.Statements, scope, until: true, beginModifier: IsBeginModifier(node.Flags));

    private static bool IsBeginModifier(int flags) => (flags & (int)LoopFlags.BeginModifier) != 0;

    private object? RunLoop(Node predicate, Node? body, RubyScope scope, bool until, bool beginModifier)
    {
        long loopId = ++_loopCounter;
        long savedBreakId = scope.CurrentBreakId;
        scope.CurrentBreakId = loopId;
        try
        {
            bool first = true;
            while (true)
            {
                if (!(beginModifier && first))
                {
                    bool cond = Truthy(Eval(predicate, scope));
                    if (until) cond = !cond;
                    if (!cond) break;
                }
                first = false;
                try
                {
                    Eval(body, scope);
                }
                catch (NextUnwind) { /* continue */ }
                catch (RedoUnwind) { /* re-run body without re-checking predicate */ continue; }
            }
            return null;   // a while/until with no break yields nil
        }
        catch (BreakUnwind br) when (br.SourceId == loopId)
        {
            return br.Value;
        }
        finally
        {
            scope.CurrentBreakId = savedBreakId;
        }
    }

    private object? EvalBreak(BreakNode node, RubyScope scope)
        => throw new BreakUnwind(JumpValue(node.Arguments, scope), scope.CurrentBreakId);

    private object? EvalNext(NextNode node, RubyScope scope)
        => throw new NextUnwind(JumpValue(node.Arguments, scope));

    private object? EvalRedo(RedoNode node, RubyScope scope) => throw new RedoUnwind();

    private object? JumpValue(Node? argsNode, RubyScope scope)
    {
        if (argsNode is not ArgumentsNode a || a.Arguments.Length == 0) return null;
        if (a.Arguments.Length == 1) return Eval(a.Arguments[0], scope);
        var arr = new RubyArray();
        foreach (var n in a.Arguments) arr.Add(Eval(n, scope));
        return arr;
    }

    // ---- case / when -----------------------------------------------------

    private object? EvalCase(CaseNode node, RubyScope scope)
    {
        object? subject = node.Predicate is null ? null : Eval(node.Predicate, scope);

        foreach (var condNode in node.Conditions)
        {
            if (condNode is not WhenNode when) continue;
            foreach (var test in when.Conditions)
            {
                if (test is SplatNode splat)
                {
                    var list = new List<object?>();
                    ExpandSplat(splat, scope, list);
                    foreach (var item in list)
                        if (WhenMatches(item, subject, node.Predicate is not null))
                            return Eval(when.Statements, scope);
                    continue;
                }

                var testValue = Eval(test, scope);
                if (WhenMatches(testValue, subject, node.Predicate is not null))
                    return Eval(when.Statements, scope);
            }
        }
        return Eval(node.ElseClause, scope);   // else clause or nil
    }

    // With a subject: `test === subject`. Without (case with no predicate): the
    // when condition is itself the truthiness test.
    private bool WhenMatches(object? testValue, object? subject, bool hasSubject)
        => hasSubject
            ? Truthy(_context.Send(testValue, "===", new[] { subject }))
            : Truthy(testValue);

    // ---- begin / rescue / ensure -----------------------------------------

    private object? EvalBegin(BeginNode node, RubyScope scope)
        => EvalBeginBody(node.Statements, node.RescueClause, node.ElseClause, node.EnsureClause, scope);

    private object? EvalBeginBody(Node? body, Node? rescueClause, Node? elseClause, Node? ensureClause, RubyScope scope)
    {
        try
        {
            while (true)   // loop only to support `retry`
            {
                try
                {
                    var result = Eval(body, scope);
                    if (elseClause is ElseNode els) result = Eval(els.Statements, scope);
                    return result;
                }
                catch (RubyRaiseException ex)
                {
                    var handled = TryRescue(rescueClause as RescueNode, ex, scope, out var value, out var retry);
                    if (retry) continue;
                    if (handled) return value;
                    throw;
                }
            }
        }
        finally
        {
            if (ensureClause is EnsureNode ens) Eval(ens.Statements, scope);
        }
    }

    private bool TryRescue(RescueNode? rescueNode, RubyRaiseException ex, RubyScope scope,
        out object? value, out bool retry)
    {
        value = null;
        retry = false;

        for (var r = rescueNode; r is not null; r = r.Subsequent as RescueNode)
        {
            if (!RescueMatches(r, ex, scope)) continue;

            // bind the exception to the reference target, if any
            if (r.Reference is not null) AssignSimpleTarget(r.Reference, ex.RubyException, scope);

            var savedCurrent = _context.CurrentException;
            _context.CurrentException = ex.RubyException;
            try
            {
                try
                {
                    value = Eval(r.Statements, scope);
                    return true;
                }
                catch (RetryUnwind)
                {
                    retry = true;
                    return false;
                }
            }
            finally
            {
                _context.CurrentException = savedCurrent;
            }
        }
        return false;
    }

    private bool RescueMatches(RescueNode rescueNode, RubyRaiseException ex, RubyScope scope)
    {
        if (rescueNode.Exceptions.Length == 0)
            return _context.IsKindOf(ex.RubyException, _context.StandardErrorClass);

        foreach (var exNode in rescueNode.Exceptions)
        {
            if (Eval(exNode, scope) is RubyModule cls && _context.IsKindOf(ex.RubyException, cls))
                return true;
        }
        return false;
    }

    private object? EvalRescueModifier(RescueModifierNode node, RubyScope scope)
    {
        try
        {
            return Eval(node.Expression, scope);
        }
        catch (RubyRaiseException ex) when (_context.IsKindOf(ex.RubyException, _context.StandardErrorClass))
        {
            return Eval(node.RescueExpression, scope);
        }
    }

    // ---- for ... in ... --------------------------------------------------

    private object? EvalFor(ForNode node, RubyScope scope)
    {
        var collection = Eval(node.Collection, scope);
        long loopId = ++_loopCounter;
        long saved = scope.CurrentBreakId;
        scope.CurrentBreakId = loopId;
        try
        {
            var block = new RubyProc(args =>
            {
                // for-loop index assigns to the ENCLOSING scope (no new scope)
                AssignSimpleTarget(node.Index, args.Length == 1 ? args[0] : Pack(args), scope);
                try { return Eval(node.Statements, scope); }
                catch (NextUnwind n) { return n.Value; }
            });
            _context.Send(collection, "each", System.Array.Empty<object?>(), block);
            return collection;
        }
        catch (BreakUnwind br) when (br.SourceId == loopId)
        {
            return br.Value;
        }
        finally
        {
            scope.CurrentBreakId = saved;
        }
    }

    private static RubyArray Pack(object?[] args)
    {
        var arr = new RubyArray();
        arr.AddRange(args);
        return arr;
    }

    private object? EvalRetry(RetryNode node, RubyScope scope) => throw new RetryUnwind();

    /// <summary>Assigns a value to a single (non-destructuring) target node. The
    /// full multiple-assignment target handling lives in task I6; this covers the
    /// common variable kinds needed by rescue references and for-loops.</summary>
    internal void AssignSimpleTarget(Node target, object? value, RubyScope scope)
    {
        switch (target)
        {
            case LocalVariableTargetNode t: scope.SetLocal(t.Name, value); break;
            case InstanceVariableTargetNode t: SetIvar(scope.Self, t.Name, value); break;
            case GlobalVariableTargetNode t: _context.SetGlobal(t.Name, value); break;
            case ClassVariableTargetNode t: ClassVarOwner(scope).ClassVariables[t.Name] = value; break;
            case ConstantTargetNode t: (scope.DefinitionTarget ?? _context.ObjectClass).SetConstant(t.Name, value); break;
            case LocalVariableWriteNode t: scope.SetLocal(t.Name, value); break;
            default: throw NotImplemented(target);
        }
    }
}
