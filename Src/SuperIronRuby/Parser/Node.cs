namespace SuperIronRuby.Parser;

/// <summary>
/// A byte range into the original source: <see cref="StartOffset"/> is a 0-based
/// byte offset and <see cref="Length"/> is a byte count (Prism locations are
/// byte-oriented, not character-oriented).
/// </summary>
public readonly struct Location
{
    public readonly int StartOffset;
    public readonly int Length;

    public Location(int startOffset, int length)
    {
        StartOffset = startOffset;
        Length = length;
    }

    /// <summary>One past the last byte (exclusive end offset).</summary>
    public int EndOffset => StartOffset + Length;

    public override string ToString() => $"@{StartOffset},{Length}";
}

/// <summary>
/// Base class for every Prism AST node. Concrete node classes and the
/// <see cref="NodeType"/> enum are generated from <c>Vendor/prism/config.yml</c>
/// into <c>Nodes.g.cs</c> by <c>Tools/generate_nodes.rb</c>.
/// </summary>
public abstract class Node
{
    /// <summary>The node's kind (serialization type, 1-based).</summary>
    public abstract NodeType Type { get; }

    /// <summary>Stable per-node identifier assigned by Prism.</summary>
    public uint NodeId;

    /// <summary>Source location (byte offset + length).</summary>
    public Location Location;

    /// <summary>
    /// Common + node-specific flags as a raw integer. Bits 0-1 are reserved
    /// common flags (newline, static); node-specific flags start at bit 2.
    /// </summary>
    public int Flags;

    /// <summary>
    /// Yields the immediate child nodes in Prism field order. Optional children
    /// that are absent are yielded as <c>null</c>; array children are flattened.
    /// Callers that want only present nodes should filter out nulls.
    /// </summary>
    public abstract IEnumerable<Node?> ChildNodes();

    /// <summary>True when the newline flag (common bit 0) is set.</summary>
    public bool IsNewLine => (Flags & 0x1) != 0;

    /// <summary>True when the static-literal flag (common bit 1) is set.</summary>
    public bool IsStaticLiteral => (Flags & 0x2) != 0;
}
