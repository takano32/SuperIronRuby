namespace SuperIronRuby.Runtime;

/// <summary>
/// Ruby's <c>Range</c>. Endpoints are arbitrary Ruby values (most commonly
/// Integer/Float/String). Either end may be <c>null</c> to model beginless
/// (<c>(..5)</c>) and endless (<c>(1..)</c>) ranges; verified against
/// ruby 4.0.2: <c>(1..).end == nil</c>, <c>(..5).begin == nil</c>.
/// </summary>
public sealed class RubyRange
{
    /// <summary>Lower bound, or <c>null</c> for a beginless range.</summary>
    public object? Begin { get; }

    /// <summary>Upper bound, or <c>null</c> for an endless range.</summary>
    public object? End { get; }

    /// <summary>True for <c>begin...end</c> (upper bound excluded).</summary>
    public bool ExcludeEnd { get; }

    public RubyRange(object? begin, object? end, bool excludeEnd)
    {
        Begin = begin;
        End = end;
        ExcludeEnd = excludeEnd;
    }
}
