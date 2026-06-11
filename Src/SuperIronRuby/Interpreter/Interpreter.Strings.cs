using System.Text;
using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Interpreter;

// Composite literals: interpolation, arrays, hashes, ranges, regexps (task I7).
public sealed partial class Interpreter
{
    private object? EvalInterpolatedString(InterpolatedStringNode node, RubyScope scope)
        => new MutableString(ConcatParts(node.Parts, scope));

    private object? EvalInterpolatedSymbol(InterpolatedSymbolNode node, RubyScope scope)
        => _context.Intern(ConcatParts(node.Parts, scope));

    private string ConcatParts(Node[] parts, RubyScope scope)
    {
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            switch (part)
            {
                case StringNode s:
                    sb.Append(s.Unescaped);
                    break;
                case EmbeddedStatementsNode emb:
                    sb.Append(_context.ToStr(Eval(emb.Statements, scope)));
                    break;
                case EmbeddedVariableNode ev:
                    sb.Append(_context.ToStr(Eval(ev.Variable, scope)));
                    break;
                default:
                    sb.Append(_context.ToStr(Eval(part, scope)));
                    break;
            }
        }
        return sb.ToString();
    }

    private object? EvalEmbeddedStatements(EmbeddedStatementsNode node, RubyScope scope)
        => Eval(node.Statements, scope);

    private object? EvalArray(ArrayNode node, RubyScope scope)
    {
        var arr = new RubyArray();
        foreach (var element in node.Elements)
        {
            if (element is SplatNode splat)
            {
                var list = new List<object?>();
                ExpandSplat(splat, scope, list);
                arr.AddRange(list);
            }
            else
            {
                arr.Add(Eval(element, scope));
            }
        }
        return arr;
    }

    private object? EvalHash(HashNode node, RubyScope scope)
    {
        var hash = new RubyHash();
        BuildHashElements(node.Elements, hash, scope);
        return hash;
    }

    private void BuildHashElements(Node[] elements, RubyHash hash, RubyScope scope)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case AssocNode assoc:
                    hash.Store(Eval(assoc.Key, scope), EvalAssocValue(assoc, scope));
                    break;
                case AssocSplatNode splat when splat.Value is not null:
                    if (Eval(splat.Value, scope) is RubyHash h)
                        foreach (var e in h.Entries()) hash.Store(e.Key, e.Value);
                    break;
            }
        }
    }

    // `{ x: }` shorthand: the value is an ImplicitNode wrapping the local/method read.
    private object? EvalAssocValue(AssocNode assoc, RubyScope scope)
        => assoc.Value is ImplicitNode impl ? Eval(impl.Value, scope) : Eval(assoc.Value, scope);

    private object? EvalRange(RangeNode node, RubyScope scope)
    {
        var begin = node.Left is null ? null : Eval(node.Left, scope);
        var end = node.Right is null ? null : Eval(node.Right, scope);
        bool excludeEnd = (node.Flags & (int)RangeFlags.ExcludeEnd) != 0;
        return new RubyRange(begin, end, excludeEnd);
    }

    private object? EvalRegexp(RegularExpressionNode node, RubyScope scope)
        => new RubyRegexp(node.Unescaped, RegexpOptions(node.Flags));

    private object? EvalInterpolatedRegexp(InterpolatedRegularExpressionNode node, RubyScope scope)
        => new RubyRegexp(ConcatParts(node.Parts, scope), RegexpOptions(node.Flags));

    private static RubyRegexpOptions RegexpOptions(int flags)
    {
        var opts = RubyRegexpOptions.None;
        if ((flags & (int)RegularExpressionFlags.IgnoreCase) != 0) opts |= RubyRegexpOptions.IgnoreCase;
        if ((flags & (int)RegularExpressionFlags.MultiLine) != 0) opts |= RubyRegexpOptions.Multiline;
        if ((flags & (int)RegularExpressionFlags.Extended) != 0) opts |= RubyRegexpOptions.Extended;
        return opts;
    }
}
