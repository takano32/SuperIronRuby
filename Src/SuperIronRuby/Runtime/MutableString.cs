namespace SuperIronRuby.Runtime;

/// <summary>
/// Ruby's <c>String</c>: a mutable, content-addressed string with UTF-8
/// semantics. The CLR <see cref="string"/> type is immutable and is never used
/// directly as a Ruby value — all Ruby strings are <see cref="MutableString"/>.
/// </summary>
/// <remarks>
/// Storage strategy for task R1 is deliberately simple and correct: an internal
/// CLR string field that is replaced wholesale on every mutation. A byte-buffer
/// representation for true byte-level operations and performance is left for a
/// later task. Equality and hashing are by content so that two distinct
/// instances holding the same text are equal (matching Ruby's <c>eql?</c>/hash
/// for strings; verified against ruby 4.0.2:
/// <c>{}.tap{|h| h["a"]=1; h["a".dup]=2}.size == 1</c>).
/// </remarks>
public sealed class MutableString : IEquatable<MutableString>
{
    private string _value;

    public MutableString() => _value = string.Empty;

    public MutableString(string value) => _value = value ?? string.Empty;

    /// <summary>The current text content.</summary>
    public string Value => _value;

    /// <summary>Number of UTF-16 code units (not Ruby character count yet).</summary>
    public int Length => _value.Length;

    /// <summary>True once <see cref="Freeze"/> has been called; mutation then throws.</summary>
    public bool IsFrozen { get; private set; }

    /// <summary>Marks this string immutable and returns it (Ruby's <c>freeze</c>).</summary>
    public MutableString Freeze()
    {
        IsFrozen = true;
        return this;
    }

    private void EnsureMutable()
    {
        if (IsFrozen)
            throw new InvalidOperationException("can't modify frozen String");
    }

    /// <summary>Replaces the entire content (Ruby's <c>replace</c>).</summary>
    public MutableString SetValue(string value)
    {
        EnsureMutable();
        _value = value ?? string.Empty;
        return this;
    }

    /// <summary>Appends text (Ruby's <c>&lt;&lt;</c>/<c>concat</c>); replaces the storage.</summary>
    public MutableString Append(string value)
    {
        EnsureMutable();
        _value += value ?? string.Empty;
        return this;
    }

    /// <summary>Appends another MutableString's content.</summary>
    public MutableString Append(MutableString other)
        => Append(other?._value ?? string.Empty);

    /// <summary>Returns a new, unfrozen copy of this string (Ruby's <c>dup</c>).</summary>
    public MutableString Duplicate() => new MutableString(_value);

    public bool Equals(MutableString? other)
        => other is not null && string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as MutableString);

    public override int GetHashCode() => _value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => _value;
}
