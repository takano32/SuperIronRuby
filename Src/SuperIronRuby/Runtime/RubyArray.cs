namespace SuperIronRuby.Runtime;

/// <summary>
/// Ruby's <c>Array</c>. Directly subclasses <see cref="List{T}"/> of
/// <c>object?</c> so all the standard list operations are available to builtins
/// and the interpreter without wrapping. Ruby-specific methods (push/pop/&lt;&lt;,
/// element-reference with negative indices, etc.) are added by builtins later.
/// </summary>
public sealed class RubyArray : List<object?>
{
    /// <summary>Creates an empty array.</summary>
    public RubyArray() { }

    /// <summary>Creates an empty array with the given initial capacity.</summary>
    public RubyArray(int capacity) : base(capacity) { }

    /// <summary>Creates an array populated from an existing sequence.</summary>
    public RubyArray(IEnumerable<object?> items) : base(items) { }
}
