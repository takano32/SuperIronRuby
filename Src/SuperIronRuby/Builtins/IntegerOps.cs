using System.Numerics;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>
/// Integer arithmetic and conversions. A focused subset to make the engine
/// usable; task B2 expands it (full bit ops, divmod, gcd, bases, Math, ...).
/// </summary>
[RubyClass("Integer")]
public static class IntegerOps
{
    private static BigInteger Big(object? o) => o switch
    {
        long l => l, BigInteger b => b, _ => throw new InvalidOperationException(),
    };
    private static bool IsInt(object? o) => o is long or BigInteger;
    private static double Dbl(object? o) => o switch
    {
        long l => l, BigInteger b => (double)b, double d => d, _ => 0,
    };

    // Normalize a BigInteger back to long when it fits. The (object) cast is
    // essential: without it the conditional's common type is BigInteger and the
    // long branch is implicitly widened back, so it would never return a long.
    internal static object Norm(BigInteger v)
        => v >= long.MinValue && v <= long.MaxValue ? (object)(long)v : v;

    [RubyMethod("+")]
    public static object? Add(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a[0] is double d ? Dbl(s) + d : Norm(Big(s) + Big(a[0]));

    [RubyMethod("-")]
    public static object? Sub(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a[0] is double d ? Dbl(s) - d : Norm(Big(s) - Big(a[0]));

    [RubyMethod("*")]
    public static object? Mul(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a[0] is double d ? Dbl(s) * d : Norm(Big(s) * Big(a[0]));

    [RubyMethod("/")]
    public static object? Div(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        if (a[0] is double d) return Dbl(s) / d;
        var divisor = Big(a[0]);
        if (divisor.IsZero) throw c.RaiseZeroDivisionError();
        // Ruby floor division.
        var n = Big(s);
        var q = BigInteger.DivRem(n, divisor, out var r);
        if (!r.IsZero && ((r < 0) != (divisor < 0))) q -= 1;
        return Norm(q);
    }

    [RubyMethod("%")]
    public static object? Mod(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var divisor = Big(a[0]);
        if (divisor.IsZero) throw c.RaiseZeroDivisionError();
        var n = Big(s);
        var r = n % divisor;
        if (!r.IsZero && ((r < 0) != (divisor < 0))) r += divisor;  // Ruby modulo sign
        return Norm(r);
    }

    [RubyMethod("**")]
    public static object? Pow(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        if (a[0] is long e && e >= 0) return Norm(BigInteger.Pow(Big(s), (int)e));
        return Math.Pow(Dbl(s), Dbl(a[0]));
    }

    [RubyMethod("-@")]
    public static object? Neg(RubyContext c, object? s, object?[] a, RubyProc? b) => Norm(-Big(s));

    [RubyMethod("abs")]
    public static object? Abs(RubyContext c, object? s, object?[] a, RubyProc? b) => Norm(BigInteger.Abs(Big(s)));

    [RubyMethod("<")]
    public static object? Lt(RubyContext c, object? s, object?[] a, RubyProc? b) => Compare(s, a[0]) < 0;
    [RubyMethod(">")]
    public static object? Gt(RubyContext c, object? s, object?[] a, RubyProc? b) => Compare(s, a[0]) > 0;
    [RubyMethod("<=")]
    public static object? Le(RubyContext c, object? s, object?[] a, RubyProc? b) => Compare(s, a[0]) <= 0;
    [RubyMethod(">=")]
    public static object? Ge(RubyContext c, object? s, object?[] a, RubyProc? b) => Compare(s, a[0]) >= 0;

    [RubyMethod("==")]
    public static object? Eq(RubyContext c, object? s, object?[] a, RubyProc? b)
        => (IsInt(a[0]) || a[0] is double) && Compare(s, a[0]) == 0;

    [RubyMethod("<=>")]
    public static object? Cmp(RubyContext c, object? s, object?[] a, RubyProc? b)
        => (IsInt(a[0]) || a[0] is double) ? (long)Compare(s, a[0]) : null;

    private static int Compare(object? x, object? y)
        => y is double || x is double ? Dbl(x).CompareTo(Dbl(y)) : Big(x).CompareTo(Big(y));

    [RubyMethod("===")]
    public static object? CaseEq(RubyContext c, object? s, object?[] a, RubyProc? b)
        => Eq(c, s, a, b);

    [RubyMethod("zero?")]
    public static object? Zero(RubyContext c, object? s, object?[] a, RubyProc? b) => Big(s).IsZero;
    [RubyMethod("positive?")]
    public static object? Pos(RubyContext c, object? s, object?[] a, RubyProc? b) => Big(s) > 0;
    [RubyMethod("negative?")]
    public static object? NegQ(RubyContext c, object? s, object?[] a, RubyProc? b) => Big(s) < 0;
    [RubyMethod("even?")]
    public static object? Even(RubyContext c, object? s, object?[] a, RubyProc? b) => Big(s).IsEven;
    [RubyMethod("odd?")]
    public static object? Odd(RubyContext c, object? s, object?[] a, RubyProc? b) => !Big(s).IsEven;

    [RubyMethod("succ")]
    [RubyMethod("next")]
    public static object? Succ(RubyContext c, object? s, object?[] a, RubyProc? b) => Norm(Big(s) + 1);
    [RubyMethod("pred")]
    public static object? Pred(RubyContext c, object? s, object?[] a, RubyProc? b) => Norm(Big(s) - 1);

    [RubyMethod("to_s")]
    [RubyMethod("inspect")]
    [RubyMethod("to_i")]
    [RubyMethod("to_int")]
    public static object? ToS(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        if (a.Length == 1 && (a[0] is long || a[0] is BigInteger))
            return new MutableString(ToBase(Big(s), (int)Big(a[0])));
        return s is long or BigInteger && (a.Length == 0)
            ? new MutableString(s.ToString()!)
            : s;
    }

    [RubyMethod("to_f")]
    public static object? ToF(RubyContext c, object? s, object?[] a, RubyProc? b) => Dbl(s);

    [RubyMethod("times")]
    public static object? Times(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var n = Big(s);
        if (blk is null) throw c.RaiseNotImplementedError("Integer#times without a block (Enumerator) not yet supported");
        for (BigInteger i = 0; i < n; i++) blk.Call(Norm(i));
        return s;
    }

    [RubyMethod("upto")]
    public static object? Upto(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        if (blk is null) throw c.RaiseNotImplementedError("Integer#upto without a block not yet supported");
        for (var i = Big(s); i <= Big(a[0]); i++) blk.Call(Norm(i));
        return s;
    }

    private static string ToBase(BigInteger v, int radix)
    {
        if (radix == 10) return v.ToString();
        bool neg = v < 0;
        v = BigInteger.Abs(v);
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (v.IsZero) return "0";
        var sb = new System.Text.StringBuilder();
        while (v > 0) { sb.Insert(0, digits[(int)(v % radix)]); v /= radix; }
        return (neg ? "-" : "") + sb;
    }
}
