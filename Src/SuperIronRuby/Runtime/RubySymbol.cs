namespace SuperIronRuby.Runtime;

/// <summary>
/// Ruby's <c>Symbol</c>. A symbol carries an immutable name and is compared by
/// reference: interning (guaranteeing one instance per name) is the
/// <c>RubyContext</c>'s job (task R3). Because intern guarantees uniqueness,
/// reference equality is the correct identity here, and the default
/// (reference-based) <see cref="GetHashCode"/> is used — do NOT override it.
/// </summary>
public sealed class RubySymbol
{
    /// <summary>The symbol's name (without the leading colon).</summary>
    public string Name { get; }

    /// <summary>
    /// Constructs a symbol. Call sites outside the interner should be rare;
    /// equality/hash semantics rely on the interner handing out one instance
    /// per name (task R3).
    /// </summary>
    public RubySymbol(string name) => Name = name ?? string.Empty;

    public override string ToString() => ":" + Name;

    // Equals/GetHashCode intentionally NOT overridden: reference identity.
}
