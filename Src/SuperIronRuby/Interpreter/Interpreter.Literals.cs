using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

// Literals, statement sequences, and local variables (task I1).
public sealed partial class Interpreter
{
    private object? EvalStatements(StatementsNode node, RubyScope scope)
    {
        object? result = null;
        foreach (var stmt in node.Body)
            result = Eval(stmt, scope);
        return result;          // value of the last statement; nil if empty
    }

    private object? EvalParentheses(ParenthesesNode node, RubyScope scope)
        => Eval(node.Body, scope);

    private object? EvalString(StringNode node, RubyScope scope)
    {
        var str = new MutableString(node.Unescaped);
        if (node.IsStaticLiteral && node.IsFrozenStringLiteral())
            str.Freeze();
        return str;
    }

    private object? EvalLocalRead(LocalVariableReadNode node, RubyScope scope)
    {
        // A bare identifier that is not a known local is parsed by Prism as a
        // CallNode, so a LocalVariableReadNode is always a real local; default
        // to nil if somehow unset.
        return scope.TryGetLocal(node.Name, out var v) ? v : null;
    }

    private object? EvalLocalWrite(LocalVariableWriteNode node, RubyScope scope)
    {
        var value = Eval(node.Value, scope);
        scope.SetLocal(node.Name, value);
        return value;           // assignment evaluates to the assigned value
    }
}

// Small node helpers kept close to their use.
internal static class StringNodeExtensions
{
    // Prism sets the frozen flag on string literals via StringFlags. The
    // generated StringNode exposes typed flags; bit for FROZEN is checked here.
    public static bool IsFrozenStringLiteral(this StringNode node)
        => (node.Flags & (int)StringFlags.Frozen) != 0;
}
