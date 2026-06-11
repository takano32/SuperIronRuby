using System.Text.RegularExpressions;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>Regexp matching + String regexp methods (task B4 core).</summary>
[RubyClass("Regexp")]
public static class RegexpOps
{
    private static RubyRegexp R(object? s) => (RubyRegexp)s!;

    [RubyMethod("source")]
    public static object? Source(RubyContext c, object? s, object?[] a, RubyProc? b) => new MutableString(R(s).Source);

    [RubyMethod("match?")]
    public static object? MatchQ(RubyContext c, object? s, object?[] a, RubyProc? b)
        => R(s).Regex.IsMatch(Str(a[0]));

    [RubyMethod("match")]
    public static object? Match(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var m = R(s).Regex.Match(Str(a[0]));
        return m.Success ? new RubyMatchData(m, Str(a[0])) : null;
    }

    [RubyMethod("=~")]
    public static object? Tilde(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        if (a[0] is not MutableString) return null;
        var m = R(s).Regex.Match(Str(a[0]));
        return m.Success ? (long)m.Index : (object?)null;
    }

    [RubyMethod("===")]
    public static object? CaseEq(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a[0] is MutableString str && R(s).Regex.IsMatch(str.Value);

    [RubyMethod("inspect")]
    public static object? Inspect(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString("/" + R(s).Source + "/");

    [RubyMethod("to_s")]
    public static object? ToS(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString("(?-mix:" + R(s).Source + ")");

    internal static string Str(object? o) => o is MutableString m ? m.Value : o?.ToString() ?? "";
}

/// <summary>A subset of MatchData for the common capture-access operations.
/// Routed to the Ruby MatchData class via the Extends mapping below.</summary>
public sealed class RubyMatchData
{
    public Match Match { get; }
    public string Input { get; }
    public RubyMatchData(Match match, string input) { Match = match; Input = input; }
}

[RubyClass("MatchData", Extends = typeof(RubyMatchData))]
public static class MatchDataOps
{
    private static Match M(object? s) => ((RubyMatchData)s!).Match;

    [RubyMethod("[]")]
    public static object? Index(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var m = M(s);
        if (a[0] is long i)
            return i < m.Groups.Count && m.Groups[(int)i].Success ? new MutableString(m.Groups[(int)i].Value) : null;
        var name = KernelOps.NameOf(a[0]);
        var g = m.Groups[name];
        return g.Success ? new MutableString(g.Value) : null;
    }

    [RubyMethod("to_a")]
    public static object? ToA(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = new RubyArray();
        foreach (Group g in M(s).Groups) arr.Add(g.Success ? new MutableString(g.Value) : null);
        return arr;
    }

    [RubyMethod("captures")]
    public static object? Captures(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = new RubyArray();
        var m = M(s);
        for (int i = 1; i < m.Groups.Count; i++) arr.Add(m.Groups[i].Success ? new MutableString(m.Groups[i].Value) : null);
        return arr;
    }

    [RubyMethod("pre_match")]
    public static object? Pre(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(((RubyMatchData)s!).Input[..M(s).Index]);

    [RubyMethod("post_match")]
    public static object? Post(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(((RubyMatchData)s!).Input[(M(s).Index + M(s).Length)..]);

    [RubyMethod("to_s")]
    public static object? ToS(RubyContext c, object? s, object?[] a, RubyProc? b) => new MutableString(M(s).Value);
}

[RubyClass("String")]
public static class StringRegexpOps
{
    private static string V(object? s) => ((MutableString)s!).Value;

    [RubyMethod("=~")]
    public static object? Tilde(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        if (a[0] is not RubyRegexp re) return null;
        var m = re.Regex.Match(V(s));
        return m.Success ? (long)m.Index : (object?)null;
    }

    [RubyMethod("match?")]
    public static object? MatchQ(RubyContext c, object? s, object?[] a, RubyProc? b)
        => ToRegex(a[0]).IsMatch(V(s));

    [RubyMethod("match")]
    public static object? Match(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var m = ToRegex(a[0]).Match(V(s));
        return m.Success ? new RubyMatchData(m, V(s)) : null;
    }

    [RubyMethod("scan")]
    public static object? Scan(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var re = ToRegex(a[0]);
        var arr = new RubyArray();
        foreach (Match m in re.Matches(V(s)))
        {
            object? item;
            if (m.Groups.Count > 1)
            {
                var g = new RubyArray();
                for (int i = 1; i < m.Groups.Count; i++) g.Add(new MutableString(m.Groups[i].Value));
                item = g;
            }
            else item = new MutableString(m.Value);
            if (blk is not null) blk.Call(item); else arr.Add(item);
        }
        return blk is not null ? s : arr;
    }

    [RubyMethod("sub")]
    public static object? Sub(RubyContext c, object? s, object?[] a, RubyProc? blk)
        => new MutableString(Replace(c, V(s), a, blk, all: false));

    [RubyMethod("gsub")]
    public static object? Gsub(RubyContext c, object? s, object?[] a, RubyProc? blk)
        => new MutableString(Replace(c, V(s), a, blk, all: true));

    private static string Replace(RubyContext c, string input, object?[] a, RubyProc? blk, bool all)
    {
        var re = ToRegex(a[0]);
        MatchEvaluator eval = m =>
        {
            if (blk is not null) return c.ToStr(blk.Call(new MutableString(m.Value)));
            if (a.Length > 1 && a[1] is MutableString rep)
                return Regex.Replace(rep.Value, @"\\(\d)", mm => m.Groups[int.Parse(mm.Groups[1].Value)].Value);
            return m.Value;
        };
        return all ? re.Replace(input, eval) : re.Replace(input, eval, 1);
    }

    private static Regex ToRegex(object? o)
        => o switch
        {
            RubyRegexp re => re.Regex,
            MutableString str => new Regex(Regex.Escape(str.Value)),
            _ => throw new InvalidOperationException("expected a Regexp or String pattern"),
        };
}
