using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

// Multiple assignment and operator-assignment (task I6).
public sealed partial class Interpreter
{
    // ---- multiple assignment ---------------------------------------------

    private object? EvalMultiWrite(MultiWriteNode node, RubyScope scope)
    {
        var value = Eval(node.Value, scope);
        var array = ToAssignArray(value);
        DistributeMulti(node.Lefts, node.Rest, node.Rights, array, scope);
        return value;
    }

    // Coerces an RHS value into the array used for destructuring.
    private RubyArray ToAssignArray(object? value)
    {
        if (value is RubyArray a) return a;
        // a, b = 1  -> [1]; respond to to_ary? keep simple.
        var arr = new RubyArray();
        if (value is not null) arr.Add(value);
        return arr;
    }

    private void DistributeMulti(Node[] lefts, Node? rest, Node[] rights, RubyArray values, RubyScope scope)
    {
        int n = values.Count;
        // assign leading lefts
        for (int i = 0; i < lefts.Length; i++)
            AssignTarget(lefts[i], i < n ? values[i] : null, scope);

        if (rest is not null)
        {
            int restCount = Math.Max(0, n - lefts.Length - rights.Length);
            var restArr = new RubyArray();
            for (int i = 0; i < restCount; i++) restArr.Add(values[lefts.Length + i]);
            if (rest is SplatNode { Expression: { } target }) AssignTarget(target, restArr, scope);
            // ImplicitRestNode / nameless splat: discard.

            // assign trailing rights from the end
            for (int i = 0; i < rights.Length; i++)
            {
                int idx = n - rights.Length + i;
                AssignTarget(rights[i], idx >= 0 && idx < n ? values[idx] : null, scope);
            }
        }
    }

    /// <summary>Assigns to any assignment target (variable, call setter, index
    /// setter, or nested multi-target). Generalizes <see cref="AssignSimpleTarget"/>.</summary>
    internal void AssignTarget(Node target, object? value, RubyScope scope)
    {
        switch (target)
        {
            case MultiTargetNode multi:
                DistributeMulti(multi.Lefts, multi.Rest, multi.Rights, ToAssignArray(value), scope);
                break;
            case CallTargetNode call:
                _context.Send(Eval(call.Receiver, scope), call.Name, new[] { value }, null, RubyCallFlags.None, scope.Self);
                break;
            case IndexTargetNode idx:
            {
                var receiver = Eval(idx.Receiver, scope);
                var (args, _) = EvalArgumentList(idx.Arguments, scope);
                var all = args.Append(value).ToArray();
                _context.Send(receiver, "[]=", all, null, RubyCallFlags.None, scope.Self);
                break;
            }
            case SplatNode { Expression: { } inner }:
                AssignTarget(inner, value, scope);
                break;
            default:
                AssignSimpleTarget(target, value, scope);
                break;
        }
    }

    // ---- operator assignment ---------------------------------------------
    // Each variable kind exposes a (read, write) pair; the three op-assign forms
    // (&&=, ||=, op=) are then uniform.

    private object? EvalOrWrite(Func<object?> read, Action<object?> write, Func<object?> rhs, bool definedAware)
    {
        var current = read();
        if (Truthy(current)) return current;
        var value = rhs();
        write(value);
        return value;
    }

    private object? EvalAndWrite(Func<object?> read, Action<object?> write, Func<object?> rhs)
    {
        var current = read();
        if (!Truthy(current)) return current;
        var value = rhs();
        write(value);
        return value;
    }

    private object? EvalOpWrite(Func<object?> read, Action<object?> write, string op, Func<object?> rhs)
    {
        var current = read();
        var value = _context.Send(current, op, new[] { rhs() });
        write(value);
        return value;
    }

    // Local variables.
    private object? EvalLocalOrWrite(LocalVariableOrWriteNode n, RubyScope s)
        => EvalOrWrite(() => s.TryGetLocal(n.Name, out var v) ? v : null, v => s.SetLocal(n.Name, v),
            () => Eval(n.Value, s), definedAware: true);
    private object? EvalLocalAndWrite(LocalVariableAndWriteNode n, RubyScope s)
        => EvalAndWrite(() => s.TryGetLocal(n.Name, out var v) ? v : null, v => s.SetLocal(n.Name, v),
            () => Eval(n.Value, s));
    private object? EvalLocalOpWrite(LocalVariableOperatorWriteNode n, RubyScope s)
        => EvalOpWrite(() => s.TryGetLocal(n.Name, out var v) ? v : null, v => s.SetLocal(n.Name, v),
            n.BinaryOperator, () => Eval(n.Value, s));

    // Instance variables.
    private object? EvalIvarOrWrite(InstanceVariableOrWriteNode n, RubyScope s)
        => EvalOrWrite(() => GetIvar(s.Self, n.Name), v => SetIvar(s.Self, n.Name, v), () => Eval(n.Value, s), true);
    private object? EvalIvarAndWrite(InstanceVariableAndWriteNode n, RubyScope s)
        => EvalAndWrite(() => GetIvar(s.Self, n.Name), v => SetIvar(s.Self, n.Name, v), () => Eval(n.Value, s));
    private object? EvalIvarOpWrite(InstanceVariableOperatorWriteNode n, RubyScope s)
        => EvalOpWrite(() => GetIvar(s.Self, n.Name), v => SetIvar(s.Self, n.Name, v), n.BinaryOperator, () => Eval(n.Value, s));

    // Global variables.
    private object? EvalGvarOrWrite(GlobalVariableOrWriteNode n, RubyScope s)
        => EvalOrWrite(() => _context.GetGlobal(n.Name), v => _context.SetGlobal(n.Name, v), () => Eval(n.Value, s), true);
    private object? EvalGvarAndWrite(GlobalVariableAndWriteNode n, RubyScope s)
        => EvalAndWrite(() => _context.GetGlobal(n.Name), v => _context.SetGlobal(n.Name, v), () => Eval(n.Value, s));
    private object? EvalGvarOpWrite(GlobalVariableOperatorWriteNode n, RubyScope s)
        => EvalOpWrite(() => _context.GetGlobal(n.Name), v => _context.SetGlobal(n.Name, v), n.BinaryOperator, () => Eval(n.Value, s));

    // Class variables.
    private object? EvalCvarOrWrite(ClassVariableOrWriteNode n, RubyScope s)
        => EvalOrWrite(() => SafeClassVar(s, n.Name), v => ClassVarOwner(s).ClassVariables[n.Name] = v, () => Eval(n.Value, s), true);
    private object? EvalCvarAndWrite(ClassVariableAndWriteNode n, RubyScope s)
        => EvalAndWrite(() => SafeClassVar(s, n.Name), v => ClassVarOwner(s).ClassVariables[n.Name] = v, () => Eval(n.Value, s));
    private object? EvalCvarOpWrite(ClassVariableOperatorWriteNode n, RubyScope s)
        => EvalOpWrite(() => SafeClassVar(s, n.Name), v => ClassVarOwner(s).ClassVariables[n.Name] = v, n.BinaryOperator, () => Eval(n.Value, s));

    private object? SafeClassVar(RubyScope s, string name)
    {
        foreach (var mod in ClassVarOwner(s).Ancestors)
            if (mod.ClassVariables.TryGetValue(name, out var v)) return v;
        return null;
    }

    // Constants.
    private object? EvalConstOrWrite(ConstantOrWriteNode n, RubyScope s)
        => EvalOrWrite(() => SafeConst(s, n.Name), v => (s.DefinitionTarget ?? _context.ObjectClass).SetConstant(n.Name, v), () => Eval(n.Value, s), true);
    private object? EvalConstOpWrite(ConstantOperatorWriteNode n, RubyScope s)
        => EvalOpWrite(() => SafeConst(s, n.Name), v => (s.DefinitionTarget ?? _context.ObjectClass).SetConstant(n.Name, v), n.BinaryOperator, () => Eval(n.Value, s));

    private object? SafeConst(RubyScope s, string name)
    {
        var target = s.DefinitionTarget ?? _context.ObjectClass;
        if (target.TryGetConstant(name, out var v)) return v;
        if (_context.ObjectClass.TryGetConstant(name, out var ov)) return ov;
        return null;
    }

    // Call op-writes: `recv.attr ||= v` / `recv.attr += v` (receiver eval'd once).
    private object? EvalCallOrWrite(CallOrWriteNode n, RubyScope s)
    {
        var recv = n.Receiver is null ? s.Self : Eval(n.Receiver, s);
        var current = _context.Send(recv, n.ReadName, System.Array.Empty<object?>(), null, RubyCallFlags.None, s.Self);
        if (Truthy(current)) return current;
        var value = Eval(n.Value, s);
        _context.Send(recv, n.WriteName, new[] { value }, null, RubyCallFlags.None, s.Self);
        return value;
    }

    private object? EvalCallAndWrite(CallAndWriteNode n, RubyScope s)
    {
        var recv = n.Receiver is null ? s.Self : Eval(n.Receiver, s);
        var current = _context.Send(recv, n.ReadName, System.Array.Empty<object?>(), null, RubyCallFlags.None, s.Self);
        if (!Truthy(current)) return current;
        var value = Eval(n.Value, s);
        _context.Send(recv, n.WriteName, new[] { value }, null, RubyCallFlags.None, s.Self);
        return value;
    }

    private object? EvalCallOpWrite(CallOperatorWriteNode n, RubyScope s)
    {
        var recv = n.Receiver is null ? s.Self : Eval(n.Receiver, s);
        var current = _context.Send(recv, n.ReadName, System.Array.Empty<object?>(), null, RubyCallFlags.None, s.Self);
        var value = _context.Send(current, n.BinaryOperator, new[] { Eval(n.Value, s) });
        _context.Send(recv, n.WriteName, new[] { value }, null, RubyCallFlags.None, s.Self);
        return value;
    }

    // Index op-writes: `a[i] ||= v` / `a[i] += v` (receiver + index eval'd once).
    private object? EvalIndexOrWrite(IndexOrWriteNode n, RubyScope s)
    {
        var (recv, idx) = IndexParts(n.Receiver, n.Arguments, s);
        var current = _context.Send(recv, "[]", idx, null, RubyCallFlags.None, s.Self);
        if (Truthy(current)) return current;
        var value = Eval(n.Value, s);
        _context.Send(recv, "[]=", idx.Append(value).ToArray(), null, RubyCallFlags.None, s.Self);
        return value;
    }

    private object? EvalIndexAndWrite(IndexAndWriteNode n, RubyScope s)
    {
        var (recv, idx) = IndexParts(n.Receiver, n.Arguments, s);
        var current = _context.Send(recv, "[]", idx, null, RubyCallFlags.None, s.Self);
        if (!Truthy(current)) return current;
        var value = Eval(n.Value, s);
        _context.Send(recv, "[]=", idx.Append(value).ToArray(), null, RubyCallFlags.None, s.Self);
        return value;
    }

    private object? EvalIndexOpWrite(IndexOperatorWriteNode n, RubyScope s)
    {
        var (recv, idx) = IndexParts(n.Receiver, n.Arguments, s);
        var current = _context.Send(recv, "[]", idx, null, RubyCallFlags.None, s.Self);
        var value = _context.Send(current, n.BinaryOperator, new[] { Eval(n.Value, s) });
        _context.Send(recv, "[]=", idx.Append(value).ToArray(), null, RubyCallFlags.None, s.Self);
        return value;
    }

    private (object? recv, object?[] idx) IndexParts(Node? receiver, Node? arguments, RubyScope s)
    {
        var recv = receiver is null ? s.Self : Eval(receiver, s);
        var (idx, _) = EvalArgumentList(arguments, s);
        return (recv, idx);
    }
}
