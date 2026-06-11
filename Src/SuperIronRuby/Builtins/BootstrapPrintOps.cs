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
    [RubyMethod("new", Kind = RubyMethodKind.Static)]
    public static object? New(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(a.Length > 0 && a[0] is MutableString m ? m.Value : "");

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
    [RubyMethod("===")]
    [RubyMethod("eql?")]
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
        => c.Intern(V(s));

    private static string V(object? s) => ((MutableString)s!).Value;

    [RubyMethod("strip")]
    public static object? Strip(RubyContext c, object? s, object?[] a, RubyProc? b) => new MutableString(V(s).Trim());
    [RubyMethod("lstrip")]
    public static object? Lstrip(RubyContext c, object? s, object?[] a, RubyProc? b) => new MutableString(V(s).TrimStart());
    [RubyMethod("rstrip")]
    public static object? Rstrip(RubyContext c, object? s, object?[] a, RubyProc? b) => new MutableString(V(s).TrimEnd());

    [RubyMethod("chomp")]
    public static object? Chomp(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var v = V(s);
        if (v.EndsWith('\n')) v = v[..^1];
        if (v.EndsWith('\r')) v = v[..^1];
        return new MutableString(v);
    }

    [RubyMethod("reverse")]
    public static object? Reverse(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var chars = V(s).ToCharArray();
        System.Array.Reverse(chars);
        return new MutableString(new string(chars));
    }

    [RubyMethod("split")]
    public static object? Split(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var v = V(s);
        var arr = new RubyArray();
        string[] parts;
        if (a.Length == 0 || a[0] is null)
            parts = v.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        else
            parts = v.Split(((MutableString)a[0]!).Value);
        // drop trailing empty strings (Ruby default behavior)
        int end = parts.Length;
        while (end > 0 && parts[end - 1] == "") end--;
        for (int i = 0; i < end; i++) arr.Add(new MutableString(parts[i]));
        return arr;
    }

    [RubyMethod("chars")]
    public static object? Chars(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = new RubyArray();
        foreach (var rune in V(s).EnumerateRunes()) arr.Add(new MutableString(rune.ToString()));
        return arr;
    }

    [RubyMethod("each_char")]
    public static object? EachChar(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        if (blk is not null)
            foreach (var rune in V(s).EnumerateRunes()) blk.Call(new MutableString(rune.ToString()));
        return s;
    }

    [RubyMethod("%")]
    public static object? Format(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var fmtArgs = a[0] is RubyArray arr ? arr.ToArray() : new[] { a[0] };
        return new MutableString(FormatHelper.Sprintf(c, V(s), fmtArgs));
    }

    [RubyMethod("include?")]
    public static object? Include(RubyContext c, object? s, object?[] a, RubyProc? b)
        => V(s).Contains(((MutableString)a[0]!).Value);

    [RubyMethod("start_with?")]
    public static object? StartWith(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a.Any(x => x is MutableString m && V(s).StartsWith(m.Value));

    [RubyMethod("end_with?")]
    public static object? EndWith(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a.Any(x => x is MutableString m && V(s).EndsWith(m.Value));

    [RubyMethod("empty?")]
    public static object? Empty(RubyContext c, object? s, object?[] a, RubyProc? b) => V(s).Length == 0;

    [RubyMethod("<=>")]
    public static object? Cmp(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a[0] is MutableString o ? (long)Math.Sign(string.CompareOrdinal(V(s), o.Value)) : (object?)null;

    [RubyMethod("capitalize")]
    public static object? Capitalize(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var v = V(s);
        if (v.Length == 0) return new MutableString("");
        return new MutableString(char.ToUpperInvariant(v[0]) + v[1..].ToLowerInvariant());
    }

    [RubyMethod("<<")]
    [RubyMethod("concat")]
    public static object? Concat(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var str = (MutableString)s!;
        foreach (var x in a) str.Append(x is MutableString m ? m.Value : c.ToStr(x));
        return s;
    }

    [RubyMethod("succ")]
    [RubyMethod("next")]
    public static object? Succ(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString(RangeOps.StringSucc(V(s)));

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
    private static RubyArray A(object? s) => (RubyArray)s!;

    [RubyMethod("new", Kind = RubyMethodKind.Static)]
    public static object? New(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var arr = new RubyArray();
        if (a.Length >= 1 && a[0] is long n)
        {
            var fill = a.Length >= 2 ? a[1] : null;
            for (long i = 0; i < n; i++) arr.Add(blk is not null ? blk.Call(i) : fill);
        }
        else if (a.Length >= 1 && a[0] is RubyArray src)
        {
            arr.AddRange(src);
        }
        return arr;
    }

    [RubyMethod("inspect")]
    [RubyMethod("to_s")]
    public static object? Inspect(RubyContext c, object? s, object?[] a, RubyProc? b)
        => new MutableString("[" + string.Join(", ", A(s).Select(c.Inspect)) + "]");

    [RubyMethod("to_a")]
    [RubyMethod("to_ary")]
    [RubyMethod("entries")]
    public static object? ToA(RubyContext c, object? s, object?[] a, RubyProc? b) => s;

    [RubyMethod("length")]
    [RubyMethod("size")]
    public static object? Length(RubyContext c, object? s, object?[] a, RubyProc? b) => (long)A(s).Count;

    [RubyMethod("empty?")]
    public static object? Empty(RubyContext c, object? s, object?[] a, RubyProc? b) => A(s).Count == 0;

    [RubyMethod("each")]
    public static object? Each(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        if (blk is not null) foreach (var item in A(s).ToArray()) blk.Call(item);
        return s;
    }

    [RubyMethod("each_index")]
    public static object? EachIndex(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        if (blk is not null) for (int i = 0; i < A(s).Count; i++) blk.Call((long)i);
        return s;
    }

    [RubyMethod("[]")]
    [RubyMethod("at")]
    public static object? Index(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = A(s);
        if (a[0] is RubyRange r) return Slice(arr, r);
        int i = (int)(long)a[0]!;
        if (i < 0) i += arr.Count;
        if (a.Length == 2)
        {
            int len = (int)(long)a[1]!;
            var res = new RubyArray();
            for (int k = 0; k < len && i + k < arr.Count; k++) res.Add(arr[i + k]);
            return res;
        }
        return i >= 0 && i < arr.Count ? arr[i] : null;
    }

    private static object? Slice(RubyArray arr, RubyRange r)
    {
        int lo = (int)Convert.ToInt64(r.Begin ?? 0L);
        if (lo < 0) lo += arr.Count;
        int hi = r.End is null ? arr.Count - 1 : (int)Convert.ToInt64(r.End);
        if (hi < 0) hi += arr.Count;
        if (r.ExcludeEnd) hi--;
        var res = new RubyArray();
        for (int i = lo; i <= hi && i < arr.Count; i++) if (i >= 0) res.Add(arr[i]);
        return res;
    }

    [RubyMethod("[]=")]
    public static object? IndexSet(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = A(s);
        int i = (int)(long)a[0]!;
        if (i < 0) i += arr.Count;
        while (arr.Count <= i) arr.Add(null);
        arr[i] = a[1];
        return a[1];
    }

    private static void CheckFrozen(RubyContext c, RubyArray arr)
    {
        if (arr.IsFrozen) throw c.RaiseFrozenError("Array", c.Inspect(arr));
    }

    [RubyMethod("<<")]
    public static object? Append(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        CheckFrozen(c, A(s));
        A(s).Add(a[0]);
        return s;
    }

    [RubyMethod("push")]
    [RubyMethod("append")]
    public static object? Push(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        CheckFrozen(c, A(s));
        foreach (var x in a) A(s).Add(x);
        return s;
    }

    [RubyMethod("pop")]
    public static object? Pop(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = A(s);
        if (arr.Count == 0) return null;
        var v = arr[^1];
        arr.RemoveAt(arr.Count - 1);
        return v;
    }

    [RubyMethod("shift")]
    public static object? Shift(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = A(s);
        if (arr.Count == 0) return null;
        var v = arr[0];
        arr.RemoveAt(0);
        return v;
    }

    [RubyMethod("unshift")]
    [RubyMethod("prepend")]
    public static object? Unshift(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        A(s).InsertRange(0, a);
        return s;
    }

    [RubyMethod("concat")]
    public static object? Concat(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        foreach (var x in a) if (x is RubyArray o) A(s).AddRange(o);
        return s;
    }

    [RubyMethod("+")]
    public static object? Plus(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var res = new RubyArray(A(s));
        if (a[0] is RubyArray o) res.AddRange(o);
        return res;
    }

    [RubyMethod("first")]
    public static object? First(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = A(s);
        if (a.Length == 1)
        {
            int n = (int)(long)a[0]!;
            var res = new RubyArray();
            for (int i = 0; i < n && i < arr.Count; i++) res.Add(arr[i]);
            return res;
        }
        return arr.Count > 0 ? arr[0] : null;
    }

    [RubyMethod("last")]
    public static object? Last(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = A(s);
        return arr.Count > 0 ? arr[^1] : null;
    }

    [RubyMethod("reverse")]
    public static object? Reverse(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var res = new RubyArray(A(s));
        res.Reverse();
        return res;
    }

    [RubyMethod("sort")]
    public static object? Sort(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var res = new RubyArray(A(s));
        res.Sort((x, y) =>
        {
            var r = blk is not null ? blk.Call(x, y) : c.Send(x, "<=>", new[] { y });
            if (r is null) throw c.RaiseArgumentError($"comparison of {c.GetClassOf(x).Name} with {c.GetClassOf(y).Name} failed");
            return (int)Convert.ToInt64(r);
        });
        return res;
    }

    [RubyMethod("join")]
    public static object? Join(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var sep = a.Length > 0 && a[0] is MutableString m ? m.Value : "";
        return new MutableString(string.Join(sep, A(s).Select(x => x is null ? "" : c.ToStr(x))));
    }

    [RubyMethod("include?")]
    public static object? Include(RubyContext c, object? s, object?[] a, RubyProc? b)
        => A(s).Any(x => RubyValueComparer.Instance.RubyEql(x, a[0]));

    [RubyMethod("index")]
    [RubyMethod("find_index")]
    public static object? IndexOf(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = A(s);
        for (int i = 0; i < arr.Count; i++)
            if (RubyValueComparer.Instance.RubyEql(arr[i], a[0])) return (long)i;
        return null;
    }

    [RubyMethod("uniq")]
    public static object? Uniq(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var seen = new HashSet<object>(RubyValueComparer.Instance);
        var res = new RubyArray();
        foreach (var x in A(s)) if (seen.Add(x ?? NilBox)) res.Add(x);
        return res;
    }
    private static readonly object NilBox = new();

    [RubyMethod("flatten")]
    public static object? Flatten(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var res = new RubyArray();
        void Rec(RubyArray arr) { foreach (var x in arr) { if (x is RubyArray sub) Rec(sub); else res.Add(x); } }
        Rec(A(s));
        return res;
    }

    [RubyMethod("compact")]
    public static object? Compact(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var res = new RubyArray();
        foreach (var x in A(s)) if (x is not null) res.Add(x);
        return res;
    }

    [RubyMethod("map!")]
    [RubyMethod("collect!")]
    public static object? MapBang(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var arr = A(s);
        if (blk is not null) for (int i = 0; i < arr.Count; i++) arr[i] = blk.Call(arr[i]);
        return s;
    }

    [RubyMethod("<=>")]
    public static object? Cmp(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        if (a[0] is not RubyArray o) return null;
        var arr = A(s);
        int n = Math.Min(arr.Count, o.Count);
        for (int i = 0; i < n; i++)
        {
            var r = c.Send(arr[i], "<=>", new[] { o[i] });
            if (r is null) return null;
            int cmp = (int)Convert.ToInt64(r);
            if (cmp != 0) return (long)cmp;
        }
        return (long)arr.Count.CompareTo(o.Count);
    }

    [RubyMethod("==")]
    public static object? Eq(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a[0] is RubyArray o && RubyValueComparer.Instance.RubyEql(s, o);

    [RubyMethod("dup")]
    public static object? Dup(RubyContext c, object? s, object?[] a, RubyProc? b) => new RubyArray(A(s));
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

    private static RubyHash H(object? s) => (RubyHash)s!;

    [RubyMethod("new", Kind = RubyMethodKind.Static)]
    public static object? New(RubyContext c, object? s, object?[] a, RubyProc? blk)
        => new RubyHash { DefaultValue = a.Length > 0 ? a[0] : null, DefaultProc = blk };

    [RubyMethod("[]")]
    public static object? Index(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var h = H(s);
        if (h.TryGetValue(a[0], out var v)) return v;
        if (h.DefaultProc is not null) return h.DefaultProc.Call(s, a[0]);
        return h.DefaultValue;
    }

    [RubyMethod("[]=")]
    [RubyMethod("store")]
    public static object? Store(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        H(s).Store(a[0], a[1]);
        return a[1];
    }

    [RubyMethod("size")]
    [RubyMethod("length")]
    public static object? Size(RubyContext c, object? s, object?[] a, RubyProc? b) => (long)H(s).Count;

    [RubyMethod("empty?")]
    public static object? Empty(RubyContext c, object? s, object?[] a, RubyProc? b) => H(s).Count == 0;

    [RubyMethod("each")]
    [RubyMethod("each_pair")]
    public static object? Each(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        // Yields a single [key, value] pair; a |k, v| block auto-splats it.
        if (blk is not null)
            foreach (var e in H(s).Entries().ToArray()) blk.Call(new RubyArray { e.Key, e.Value });
        return s;
    }

    [RubyMethod("each_key")]
    public static object? EachKey(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        if (blk is not null) foreach (var k in H(s).Keys().ToArray()) blk.Call(k);
        return s;
    }

    [RubyMethod("each_value")]
    public static object? EachValue(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        if (blk is not null) foreach (var v in H(s).Values().ToArray()) blk.Call(v);
        return s;
    }

    [RubyMethod("keys")]
    public static object? Keys(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = new RubyArray();
        arr.AddRange(H(s).Keys());
        return arr;
    }

    [RubyMethod("values")]
    public static object? Values(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = new RubyArray();
        arr.AddRange(H(s).Values());
        return arr;
    }

    [RubyMethod("fetch")]
    public static object? Fetch(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        if (H(s).TryGetValue(a[0], out var v)) return v;
        if (a.Length > 1) return a[1];
        if (blk is not null) return blk.Call(a[0]);
        throw c.RaiseError(c.KeyErrorClass, $"key not found: {c.Inspect(a[0])}");
    }

    [RubyMethod("key?")]
    [RubyMethod("has_key?")]
    [RubyMethod("include?")]
    [RubyMethod("member?")]
    public static object? KeyQ(RubyContext c, object? s, object?[] a, RubyProc? b) => H(s).ContainsKey(a[0]);

    [RubyMethod("value?")]
    [RubyMethod("has_value?")]
    public static object? ValueQ(RubyContext c, object? s, object?[] a, RubyProc? b)
        => H(s).Values().Any(v => RubyValueComparer.Instance.RubyEql(v, a[0]));

    [RubyMethod("merge")]
    public static object? Merge(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var res = new RubyHash();
        foreach (var e in H(s).Entries()) res.Store(e.Key, e.Value);
        foreach (var arg in a)
            if (arg is RubyHash o)
                foreach (var e in o.Entries())
                {
                    if (blk is not null && res.TryGetValue(e.Key, out var existing))
                        res.Store(e.Key, blk.Call(e.Key, existing, e.Value));
                    else
                        res.Store(e.Key, e.Value);
                }
        return res;
    }

    [RubyMethod("merge!")]
    [RubyMethod("update")]
    public static object? MergeBang(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        foreach (var arg in a)
            if (arg is RubyHash o)
                foreach (var e in o.Entries()) H(s).Store(e.Key, e.Value);
        return s;
    }

    [RubyMethod("delete")]
    public static object? Delete(RubyContext c, object? s, object?[] a, RubyProc? b)
        => H(s).Remove(a[0], out var v) ? v : null;

    [RubyMethod("select")]
    [RubyMethod("filter")]
    public static object? Select(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var res = new RubyHash();
        if (blk is not null)
            foreach (var e in H(s).Entries())
                if (RubyContext.Truthy(blk.Call(e.Key, e.Value))) res.Store(e.Key, e.Value);
        return res;
    }

    [RubyMethod("reject")]
    public static object? Reject(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var res = new RubyHash();
        if (blk is not null)
            foreach (var e in H(s).Entries())
                if (!RubyContext.Truthy(blk.Call(e.Key, e.Value))) res.Store(e.Key, e.Value);
        return res;
    }

    [RubyMethod("transform_values")]
    public static object? TransformValues(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var res = new RubyHash();
        if (blk is not null) foreach (var e in H(s).Entries()) res.Store(e.Key, blk.Call(e.Value));
        return res;
    }

    [RubyMethod("transform_keys")]
    public static object? TransformKeys(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var res = new RubyHash();
        if (blk is not null) foreach (var e in H(s).Entries()) res.Store(blk.Call(e.Key), e.Value);
        return res;
    }

    [RubyMethod("map")]
    public static object? Map(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var res = new RubyArray();
        if (blk is not null) foreach (var e in H(s).Entries()) res.Add(blk.Call(e.Key, e.Value));
        return res;
    }

    [RubyMethod("any?")]
    public static object? Any(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        foreach (var e in H(s).Entries())
            if (blk is null || RubyContext.Truthy(blk.Call(e.Key, e.Value))) return true;
        return false;
    }

    [RubyMethod("to_h")]
    public static object? ToH(RubyContext c, object? s, object?[] a, RubyProc? b) => s;

    [RubyMethod("to_a")]
    public static object? ToA(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = new RubyArray();
        foreach (var e in H(s).Entries())
        {
            var pair = new RubyArray { e.Key, e.Value };
            arr.Add(pair);
        }
        return arr;
    }

    [RubyMethod("==")]
    public static object? Eq(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        if (a[0] is not RubyHash o || o.Count != H(s).Count) return false;
        foreach (var e in H(s).Entries())
            if (!o.TryGetValue(e.Key, out var v) || !RubyValueComparer.Instance.RubyEql(v, e.Value)) return false;
        return true;
    }
}
