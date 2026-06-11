using System.Numerics;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>Range methods (task B7 core: each/inspect/cover/to_a; Enumerable
/// supplies map/select/etc.).</summary>
[RubyClass("Range")]
public static class RangeOps
{
    private static RubyRange R(object? s) => (RubyRange)s!;

    [RubyMethod("begin")]
    [RubyMethod("first")]
    public static object? Begin(RubyContext c, object? s, object?[] a, RubyProc? b) => R(s).Begin;

    [RubyMethod("end")]
    [RubyMethod("last")]
    public static object? End(RubyContext c, object? s, object?[] a, RubyProc? b) => R(s).End;

    [RubyMethod("exclude_end?")]
    public static object? ExcludeEnd(RubyContext c, object? s, object?[] a, RubyProc? b) => R(s).ExcludeEnd;

    [RubyMethod("each")]
    public static object? Each(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var r = R(s);
        if (blk is null) return s;
        if (r.Begin is long or BigInteger)
        {
            if (r.End is null) throw c.RaiseRuntimeError("cannot iterate an endless range with #each here");
            var lo = (BigInteger)Convert.ToInt64(r.Begin);
            var hi = (BigInteger)Convert.ToInt64(r.End);
            var end = r.ExcludeEnd ? hi - 1 : hi;
            for (var i = lo; i <= end; i++) blk.Call(IntegerOps.Norm(i));
        }
        else if (r.Begin is MutableString lo2 && r.End is MutableString hi2)
        {
            // string range via succ
            var cur = lo2.Value;
            while (true)
            {
                int cmp = string.CompareOrdinal(cur, hi2.Value);
                if (r.ExcludeEnd ? cmp >= 0 : cmp > 0) break;
                blk.Call(new MutableString(cur));
                if (cur == hi2.Value) break;
                cur = StringSucc(cur);
            }
        }
        return s;
    }

    [RubyMethod("to_a")]
    [RubyMethod("to_ary")]
    [RubyMethod("entries")]
    public static object? ToA(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = new RubyArray();
        Each(c, s, a, new RubyProc(args => { arr.Add(args[0]); return null; }));
        return arr;
    }

    [RubyMethod("include?")]
    [RubyMethod("member?")]
    [RubyMethod("cover?")]
    [RubyMethod("===")]
    public static object? Cover(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var r = R(s);
        var v = a[0];
        if (r.Begin is not null && Cmp(c, v, r.Begin) < 0) return false;
        if (r.End is not null)
        {
            int cmp = Cmp(c, v, r.End);
            if (r.ExcludeEnd ? cmp >= 0 : cmp > 0) return false;
        }
        return true;
    }

    [RubyMethod("size")]
    [RubyMethod("count")]
    public static object? Size(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var r = R(s);
        if (r.Begin is long or BigInteger && r.End is long or BigInteger)
        {
            var lo = (BigInteger)Convert.ToInt64(r.Begin);
            var hi = (BigInteger)Convert.ToInt64(r.End);
            var n = (r.ExcludeEnd ? hi : hi + 1) - lo;
            return IntegerOps.Norm(n < 0 ? 0 : n);
        }
        return null;
    }

    [RubyMethod("sum")]
    public static object? Sum(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var r = R(s);
        if (blk is null && r.Begin is long or BigInteger && r.End is long or BigInteger)
        {
            var lo = (BigInteger)Convert.ToInt64(r.Begin);
            var hi = (BigInteger)Convert.ToInt64(r.End);
            if (r.ExcludeEnd) hi -= 1;
            if (hi < lo) return a.Length > 0 ? a[0] : 0L;
            var gauss = (hi - lo + 1) * (lo + hi) / 2;
            var init = a.Length > 0 ? (BigInteger)Convert.ToInt64(a[0]) : 0;
            return IntegerOps.Norm(gauss + init);
        }
        // fall back to Enumerable#sum
        return c.Send(ToA(c, s, System.Array.Empty<object?>(), null), "sum", a, blk);
    }

    [RubyMethod("inspect")]
    public static object? Inspect(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var r = R(s);
        var op = r.ExcludeEnd ? "..." : "..";
        return new MutableString($"{InspectEnd(c, r.Begin)}{op}{InspectEnd(c, r.End)}");
    }

    [RubyMethod("to_s")]
    public static object? ToS(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var r = R(s);
        var op = r.ExcludeEnd ? "..." : "..";
        return new MutableString($"{ToStr(c, r.Begin)}{op}{ToStr(c, r.End)}");
    }

    private static string InspectEnd(RubyContext c, object? v) => v is null ? "" : c.Inspect(v);
    private static string ToStr(RubyContext c, object? v) => v is null ? "" : c.ToStr(v);

    private static int Cmp(RubyContext c, object? x, object? y)
    {
        var r = c.Send(x, "<=>", new[] { y });
        return r is null ? 1 : (int)Convert.ToInt64(r);
    }

    internal static string StringSucc(string s)
    {
        if (s.Length == 0) return s;
        var chars = s.ToCharArray();
        int i = chars.Length - 1;
        while (i >= 0)
        {
            char ch = chars[i];
            if (ch == 'z') { chars[i] = 'a'; i--; }
            else if (ch == 'Z') { chars[i] = 'A'; i--; }
            else if (ch == '9') { chars[i] = '0'; i--; }
            else { chars[i]++; return new string(chars); }
        }
        // carried past the front
        char first = s[0];
        string prefix = char.IsDigit(first) ? "1" : char.IsUpper(first) ? "A" : "a";
        return prefix + new string(chars);
    }
}
