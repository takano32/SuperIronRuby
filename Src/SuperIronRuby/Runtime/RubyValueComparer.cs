using System.Numerics;

namespace SuperIronRuby.Runtime;

/// <summary>
/// Implements Ruby's <c>eql?</c>/<c>hash</c> equality used for Hash keys. Unlike
/// <c>==</c>, <c>eql?</c> is type-sensitive for numerics: verified against
/// ruby 4.0.2, <c>1.eql?(1.0) == false</c>, so <c>{1=&gt;.., 1.0=&gt;..}.size == 2</c>.
/// String keys compare by content (<c>"a".eql?("a".dup)</c>), symbols by
/// reference (interned), arrays element-wise recursively, and unknown objects
/// fall back to reference identity.
/// </summary>
public sealed class RubyValueComparer : IEqualityComparer<object>
{
    /// <summary>Shared instance — the comparer is stateless.</summary>
    public static readonly RubyValueComparer Instance = new();

    /// <summary>
    /// Pluggable hook for context-dispatched <c>hash</c> of user objects
    /// (a user-defined <c>hash</c> method). Unused in task R1; wired by a later
    /// task. When set it is consulted for objects with no built-in handling.
    /// </summary>
    public Func<object, int>? CustomHash { get; set; }

    bool IEqualityComparer<object>.Equals(object? x, object? y) => RubyEql(x, y);

    int IEqualityComparer<object>.GetHashCode(object obj) => RubyHashCode(obj);

    /// <summary>Ruby <c>eql?</c> for two values (null == Ruby nil).</summary>
    public bool RubyEql(object? x, object? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        switch (x)
        {
            case bool bx:
                return y is bool by && bx == by;

            // Integers: long and BigInteger are the SAME Ruby class (Integer),
            // so they must be eql? to each other when numerically equal, but NOT
            // eql? to a Float (different class).
            case long lx:
                return y switch
                {
                    long ly => lx == ly,
                    BigInteger byi => byi == lx,
                    _ => false,
                };
            case BigInteger bix:
                return y switch
                {
                    long ly => bix == ly,
                    BigInteger byi => bix == byi,
                    _ => false,
                };

            // Float is a distinct class from Integer.
            case double dx:
                return y is double dy && dx.Equals(dy);

            case MutableString sx:
                return y is MutableString sy && sx.Equals(sy);

            case RubySymbol:
                // Symbols are interned -> reference equality (already handled above).
                return false;

            case RubyArray ax:
                return y is RubyArray ay && ArrayEql(ax, ay);

            default:
                // User objects: reference identity (handled by ReferenceEquals above).
                // Context-dispatched user eql? comes in a later task.
                return false;
        }
    }

    private bool ArrayEql(RubyArray a, RubyArray b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!RubyEql(a[i], b[i])) return false;
        }
        return true;
    }

    /// <summary>Ruby <c>hash</c> for a value, consistent with <see cref="RubyEql"/>.</summary>
    public int RubyHashCode(object? obj)
    {
        switch (obj)
        {
            case null:
                return 0;
            case bool b:
                return b ? 1 : 2;
            case long l:
                return NumericHash(l);
            case BigInteger bi:
                // Normalize: a BigInteger holding a long-range value must hash
                // the same as that long (they are eql?).
                return bi >= long.MinValue && bi <= long.MaxValue
                    ? NumericHash((long)bi)
                    : bi.GetHashCode();
            case double d:
                return d.GetHashCode();
            case MutableString s:
                return s.GetHashCode();
            case RubySymbol sym:
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(sym);
            case RubyArray a:
                return ArrayHash(a);
            default:
                if (CustomHash is not null) return CustomHash(obj);
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    // Long and an equal BigInteger must share a hash so they collide and are
    // then resolved as eql?.
    private static int NumericHash(long value) => value.GetHashCode();

    private int ArrayHash(RubyArray a)
    {
        var hc = new HashCode();
        hc.Add(a.Count);
        foreach (var item in a)
            hc.Add(RubyHashCode(item));
        return hc.ToHashCode();
    }
}
