using System.Numerics;
using System.Text;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

// Minimal to_s/inspect for the common value types so puts/p/string interpolation
// print correctly. Tasks B3 (String), B4 (Symbol), B5 (Array), B2 (Float),
// B13 (Exception) flesh these classes out fully.

[RubyClass("String")]
public static class StringPrintOps
{
    [RubyMethod("to_s")]
    [RubyMethod("to_str")]
    public static object? ToS(RubyContext c, object? s, object?[] a, RubyProc? b) => s;

    [RubyMethod("inspect")]
    public static object? Inspect(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(InspectString(((MutableString)s!).Value));

    [RubyMethod("+")]
    public static object? Add(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        if (a[0] is not MutableString other)
            throw c.RaiseTypeError("no implicit conversion into String");
        return new MutableString(((MutableString)s!).Value + other.Value);
    }

    [RubyMethod("*")]
    public static object? Mul(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(string.Concat(Enumerable.Repeat(((MutableString)s!).Value, (int)(long)a[0]!)));

    [RubyMethod("==")]
    public static object? Eq(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a[0] is MutableString o && ((MutableString)s!).Value == o.Value;

    [RubyMethod("length")]
    [RubyMethod("size")]
    public static object? Length(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        int count = 0;
        var e = System.Globalization.StringInfo.GetTextElementEnumerator(((MutableString)s!).Value);
        var en = ((MutableString)s!).Value.EnumerateRunes();
        foreach (var _ in en) count++;
        return (long)count;
    }

    [RubyMethod("upcase")]
    public static object? Upcase(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(((MutableString)s!).Value.ToUpperInvariant());

    [RubyMethod("downcase")]
    public static object? Downcase(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(((MutableString)s!).Value.ToLowerInvariant());

    [RubyMethod("to_sym")]
    [RubyMethod("intern")]
    public static object? ToSym(RubyContext c, object? s, object?[] a, RubyProc? b)
        => c.Intern(((MutableString)s!).Value);

    internal static string InspectString(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': sb.Append("\\r"); break;
                case '\e': sb.Append("\\e"); break;
                default:
                    if (ch < 0x20) sb.Append($"\\x{(int)ch:X2}");
                    else sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}

[RubyClass("Symbol")]
public static class SymbolPrintOps
{
    [RubyMethod("to_s")]
    [RubyMethod("id2name")]
    [RubyMethod("name")]
    public static object? ToS(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(((RubySymbol)s!).Name);

    [RubyMethod("inspect")]
    public static object? Inspect(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(":" + ((RubySymbol)s!).Name);

    [RubyMethod("to_sym")]
    public static object? ToSym(RubyContext c, object? s, object?[] a, RubyProc? b) => s;

    [RubyMethod("to_proc")]
    public static object? ToProc(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var name = ((RubySymbol)s!).Name;
        return new RubyProc(args => c.Send(args[0], name, args.Skip(1).ToArray()));
    }
}

[RubyClass("Float")]
public static class FloatPrintOps
{
    [RubyMethod("to_s")]
    [RubyMethod("inspect")]
    public static object? ToS(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(FormatFloat((double)s!));

    [RubyMethod("+")]
    public static object? Add(RubyContext c, object? s, object?[] a, RubyProc? b) => (double)s! + Num(a[0]);
    [RubyMethod("-")]
    public static object? Sub(RubyContext c, object? s, object?[] a, RubyProc? b) => (double)s! - Num(a[0]);
    [RubyMethod("*")]
    public static object? Mul(RubyContext c, object? s, object?[] a, RubyProc? b) => (double)s! * Num(a[0]);
    [RubyMethod("/")]
    public static object? Div(RubyContext c, object? s, object?[] a, RubyProc? b) => (double)s! / Num(a[0]);
    [RubyMethod("to_f")]
    public static object? ToF(RubyContext c, object? s, object?[] a, RubyProc? b) => s;
    [RubyMethod("to_i")]
    public static object? ToI(RubyContext c, object? s, object?[] a, RubyProc? b)
        => IntegerOps.Norm(new BigInteger(Math.Truncate((double)s!)));

    private static double Num(object? o) => o switch { long l => l, double d => d, BigInteger b => (double)b, _ => 0 };

    // MRI-ish shortest round-trip formatting with a decimal point.
    internal static string FormatFloat(double d)
    {
        if (double.IsNaN(d)) return "NaN";
        if (double.IsPositiveInfinity(d)) return "Infinity";
        if (double.IsNegativeInfinity(d)) return "-Infinity";
        var s = d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (!s.Contains('.') && !s.Contains('e') && !s.Contains('E')) s += ".0";
        return s;
    }
}

[RubyClass("Array")]
public static class ArrayPrintOps
{
    [RubyMethod("inspect")]
    [RubyMethod("to_s")]
    public static object? Inspect(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = (RubyArray)s!;
        return new MutableString("[" + string.Join(", ", arr.Select(c.Inspect)) + "]");
    }

    [RubyMethod("to_a")]
    [RubyMethod("to_ary")]
    public static object? ToA(RubyContext c, object? s, object?[] a, RubyProc? b) => s;

    [RubyMethod("length")]
    [RubyMethod("size")]
    public static object? Length(RubyContext c, object? s, object?[] a, RubyProc? b) => (long)((RubyArray)s!).Count;

    [RubyMethod("each")]
    public static object? Each(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        if (blk is not null) foreach (var item in (RubyArray)s!) blk.Call(item);
        return s;
    }

    [RubyMethod("[]")]
    public static object? Index(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = (RubyArray)s!;
        int i = (int)(long)a[0]!;
        if (i < 0) i += arr.Count;
        return i >= 0 && i < arr.Count ? arr[i] : null;
    }

    [RubyMethod("<<")]
    [RubyMethod("push")]
    public static object? Push(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = (RubyArray)s!;
        foreach (var x in a) arr.Add(x);
        return arr;
    }

    [RubyMethod("first")]
    public static object? First(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = (RubyArray)s!;
        return arr.Count > 0 ? arr[0] : null;
    }

    [RubyMethod("==")]
    public static object? Eq(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a[0] is RubyArray o && RubyValueComparer.Instance.RubyEql(s, o);
}

[RubyClass("Hash")]
public static class HashPrintOps
{
    [RubyMethod("inspect")]
    [RubyMethod("to_s")]
    public static object? Inspect(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var h = (RubyHash)s!;
        var parts = h.Entries().Select(e =>
            e.Key is RubySymbol sym
                ? $"{sym.Name}: {c.Inspect(e.Value)}"
                : $"{c.Inspect(e.Key)} => {c.Inspect(e.Value)}");
        return new MutableString("{" + string.Join(", ", parts) + "}");
    }

    [RubyMethod("[]")]
    public static object? Index(RubyContext c, object? s, object?[] a, RubyProc? b)
        => ((RubyHash)s!).GetOrNull(a[0]);

    [RubyMethod("[]=")]
    [RubyMethod("store")]
    public static object? Store(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        ((RubyHash)s!).Store(a[0], a[1]);
        return a[1];
    }

    [RubyMethod("size")]
    [RubyMethod("length")]
    public static object? Size(RubyContext c, object? s, object?[] a, RubyProc? b) => (long)((RubyHash)s!).Count;
}
