using System.Numerics;
using System.Text;

namespace SuperIronRuby.Parser;

/// <summary>
/// Deserializes Prism's binary AST format (prism v1.8.1). This file holds the
/// hand-written half: header, primitives (LEB128 varints, integers, doubles,
/// strings, locations), the constant pool, and diagnostics. The per-node-type
/// reader (<c>LoadNode</c>) is generated into <c>Loader.g.cs</c> by
/// <c>Tools/generate_nodes.rb</c> (task P3).
/// </summary>
/// <remarks>
/// Ground truth: the rendered <c>serialize.rb</c> for v1.8.1. Each method names
/// its Ruby counterpart. The serialized layout (load_parse order): header,
/// encoding, start_line (varsint), line offsets, comments, magic comments,
/// optional data location, errors, warnings, constant-pool base (uint32) and
/// size (varuint), then the root node, then the constant-pool table trailer.
/// </remarks>
public sealed partial class Loader
{
    private const int ExpectedMajor = 1;
    private const int ExpectedMinor = 8;
    private const int ExpectedPatch = 1;

    private readonly byte[] _data;       // the serialized buffer
    private readonly byte[] _source;     // the original source bytes (for embedded strings/constants)
    private int _pos;                    // read cursor into _data

    // Constant pool (resolved lazily; 1-based indices in the stream map to 0-based here).
    private int _constantPoolBase;
    private int _constantPoolSize;
    private string?[] _constantPool = System.Array.Empty<string?>();

    // Populated during LoadProgram, exposed via ParseResult.
    private int _startLine;
    private int[] _lineOffsets = System.Array.Empty<int>();
    private readonly List<ParseDiagnostic> _errors = new();
    private readonly List<ParseDiagnostic> _warnings = new();

    public Loader(byte[] serialized, byte[] sourceBytes)
    {
        _data = serialized;
        _source = sourceBytes;
    }

    public IReadOnlyList<ParseDiagnostic> Errors => _errors;
    public IReadOnlyList<ParseDiagnostic> Warnings => _warnings;
    public int StartLine => _startLine;

    /// <summary>Deserializes a full parse result (serialize.rb <c>load_parse</c>).</summary>
    public ProgramNode LoadProgram()
    {
        LoadHeader();
        LoadEncoding();                  // we assume UTF-8; the name is consumed
        _startLine = LoadVarSInt();
        LoadLineOffsets();
        LoadComments();
        LoadMagicComments();
        LoadOptionalLocation();          // data location
        LoadErrors();
        LoadWarnings();

        _constantPoolBase = (int)LoadUInt32();
        _constantPoolSize = (int)LoadVarUInt();
        _constantPool = new string?[_constantPoolSize];

        var node = LoadNode();
        // The constant-pool table + owned-constant trailer follow the root node
        // in the stream; we've already resolved constants by absolute offset, so
        // there is nothing more we need from the trailer.
        if (node is not ProgramNode program)
            throw new InvalidOperationException($"expected ProgramNode at root, got {node.Type}");
        return program;
    }

    // ---- header & metadata -------------------------------------------------

    private void LoadHeader()
    {
        // serialize.rb load_header
        if (_data.Length < 9 ||
            _data[0] != (byte)'P' || _data[1] != (byte)'R' || _data[2] != (byte)'I' ||
            _data[3] != (byte)'S' || _data[4] != (byte)'M')
            throw new InvalidOperationException("Invalid Prism serialization (bad magic)");

        int major = _data[5], minor = _data[6], patch = _data[7];
        if (major != ExpectedMajor || minor != ExpectedMinor || patch != ExpectedPatch)
            throw new InvalidOperationException(
                $"Prism serialization version mismatch: got {major}.{minor}.{patch}, " +
                $"expected {ExpectedMajor}.{ExpectedMinor}.{ExpectedPatch}");

        byte locationFields = _data[8];
        if (locationFields != 0)
            throw new InvalidOperationException("Prism serialization without location fields is not supported");

        _pos = 9;
    }

    // serialize.rb load_encoding: a length-prefixed name. We consume it; the
    // implementation assumes UTF-8 sources.
    private void LoadEncoding()
    {
        int len = (int)LoadVarUInt();
        _pos += len;
    }

    // serialize.rb load_line_offsets
    private void LoadLineOffsets()
    {
        int count = (int)LoadVarUInt();
        _lineOffsets = new int[count];
        for (int i = 0; i < count; i++)
            _lineOffsets[i] = (int)LoadVarUInt();
    }

    // serialize.rb load_comments (0 = inline, 1 = embdoc; each has one location)
    private void LoadComments()
    {
        int count = (int)LoadVarUInt();
        for (int i = 0; i < count; i++)
        {
            LoadVarUInt();          // comment kind
            LoadLocationObject();   // location
        }
    }

    // serialize.rb load_magic_comments (two locations each)
    private void LoadMagicComments()
    {
        int count = (int)LoadVarUInt();
        for (int i = 0; i < count; i++)
        {
            LoadLocationObject();
            LoadLocationObject();
        }
    }

    private void LoadErrors()
    {
        int count = (int)LoadVarUInt();
        for (int i = 0; i < count; i++)
        {
            int typeId = (int)LoadVarUInt();
            string message = LoadEmbeddedString();
            var loc = LoadLocationObject();
            int level = ReadByte();         // load_error_level
            _errors.Add(new ParseDiagnostic(DiagnosticSeverity.Error, typeId, message, loc, level));
        }
    }

    private void LoadWarnings()
    {
        int count = (int)LoadVarUInt();
        for (int i = 0; i < count; i++)
        {
            int typeId = (int)LoadVarUInt();
            string message = LoadEmbeddedString();
            var loc = LoadLocationObject();
            int level = ReadByte();         // load_warning_level
            _warnings.Add(new ParseDiagnostic(DiagnosticSeverity.Warning, typeId, message, loc, level));
        }
    }

    // ---- primitives --------------------------------------------------------

    private byte ReadByte() => _data[_pos++];

    /// <summary>Test hook: current read cursor into the serialized buffer.</summary>
    internal int Position { get => _pos; set => _pos = value; }

    /// <summary>serialize.rb load_varuint — unsigned LEB128.</summary>
    internal ulong LoadVarUInt()
    {
        ulong n = ReadByte();
        if (n < 128) return n;

        n -= 128;
        int shift = 0;
        byte b;
        while ((b = ReadByte()) >= 128)
            n += (ulong)(b - 128) << (shift += 7);
        return n + ((ulong)b << (shift + 7));
    }

    /// <summary>serialize.rb load_varsint — zigzag-decoded signed LEB128.</summary>
    internal int LoadVarSInt()
    {
        ulong n = LoadVarUInt();
        return (int)((long)(n >> 1) ^ -(long)(n & 1));
    }

    /// <summary>serialize.rb load_integer — sign byte + word count + 32-bit
    /// little-endian words (each a varuint). Returns long when it fits, else
    /// BigInteger.</summary>
    internal object LoadInteger()
    {
        bool negative = ReadByte() != 0;
        int length = (int)LoadVarUInt();

        BigInteger value = BigInteger.Zero;
        for (int i = 0; i < length; i++)
            value |= (BigInteger)LoadVarUInt() << (i * 32);

        if (negative) value = -value;
        if (value >= long.MinValue && value <= long.MaxValue)
            return (long)value;
        return value;
    }

    /// <summary>serialize.rb load_double — 8-byte little-endian IEEE-754.</summary>
    private double LoadDouble()
    {
        double d = BitConverter.ToDouble(_data, _pos);
        _pos += 8;
        return d;
    }

    /// <summary>serialize.rb load_uint32 — 4-byte little-endian.</summary>
    private uint LoadUInt32()
    {
        uint v = BitConverter.ToUInt32(_data, _pos);
        _pos += 4;
        return v;
    }

    // serialize.rb load_embedded_string — length-prefixed bytes in the stream.
    private string LoadEmbeddedString()
    {
        int len = (int)LoadVarUInt();
        string s = Encoding.UTF8.GetString(_data, _pos, len);
        _pos += len;
        return s;
    }

    /// <summary>serialize.rb load_string — type 1: slice of the source; type 2:
    /// embedded in the stream.</summary>
    private string LoadString()
    {
        byte type = ReadByte();
        switch (type)
        {
            case 1:
            {
                int offset = (int)LoadVarUInt();
                int length = (int)LoadVarUInt();
                return Encoding.UTF8.GetString(_source, offset, length);
            }
            case 2:
                return LoadEmbeddedString();
            default:
                throw new InvalidOperationException($"Unknown serialized string type: {type}");
        }
    }

    // serialize.rb load_location_object / load_location (non-freeze path reads
    // the same two varuints, just packs them differently).
    private Location LoadLocationObject()
    {
        int start = (int)LoadVarUInt();
        int length = (int)LoadVarUInt();
        return new Location(start, length);
    }

    private Location LoadLocation() => LoadLocationObject();

    // serialize.rb load_optional_location — flag byte then a location.
    private Location? LoadOptionalLocation()
        => ReadByte() != 0 ? LoadLocation() : null;

    // serialize.rb load_optional_node — peek the flag byte; 0 => nil.
    private Node? LoadOptionalNode()
    {
        if (_data[_pos] != 0)
            return LoadNode();
        _pos++;             // consume the 0 flag
        return null;
    }

    // serialize.rb load_constant — 1-based index into the pool.
    private string LoadConstant()
    {
        int index = (int)LoadVarUInt();
        return GetConstant(index - 1);
    }

    // serialize.rb load_optional_constant — 0 => nil.
    private string? LoadOptionalConstant()
    {
        int index = (int)LoadVarUInt();
        return index != 0 ? GetConstant(index - 1) : null;
    }

    // ConstantPool#get — entry at base + index*8: uint32 start, uint32 length.
    // bit 31 of start clear => slice of the source; set => slice of the
    // serialized buffer (owned constant).
    private string GetConstant(int index)
    {
        if (_constantPool[index] is { } cached) return cached;

        int offset = _constantPoolBase + index * 8;
        uint start = BitConverter.ToUInt32(_data, offset);
        uint length = BitConverter.ToUInt32(_data, offset + 4);

        string value;
        if ((start & (1u << 31)) == 0)
            value = Encoding.UTF8.GetString(_source, (int)start, (int)length);
        else
            value = Encoding.UTF8.GetString(_data, (int)(start & ((1u << 31) - 1)), (int)length);

        _constantPool[index] = value;
        return value;
    }

    // Reads a constant[] field: a varuint count followed by that many constants.
    private string[] LoadConstantArray()
    {
        int count = (int)LoadVarUInt();
        var arr = new string[count];
        for (int i = 0; i < count; i++) arr[i] = LoadConstant();
        return arr;
    }

    // Reads a node[] field: a varuint count followed by that many nodes.
    private Node[] LoadNodeArray()
    {
        int count = (int)LoadVarUInt();
        var arr = new Node[count];
        for (int i = 0; i < count; i++) arr[i] = LoadNode();
        return arr;
    }

    /// <summary>Maps a 0-based byte offset to a 1-based line number using the
    /// line-offset table (honoring the configured start line).</summary>
    public int LineForOffset(int byteOffset)
    {
        // binary search for the greatest line offset <= byteOffset
        int lo = 0, hi = _lineOffsets.Length - 1, line = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (_lineOffsets[mid] <= byteOffset) { line = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return _startLine + line;
    }

    // Temporary stub: the generated per-node reader (Loader.g.cs) REPLACES this
    // in task P3. Kept here only so the primitives compile and self-test before
    // P3 lands. Primitive tests do not call LoadNode.
    private Node LoadNode()
        => throw new NotImplementedException("per-node loader is generated by task P3 (Loader.g.cs)");
}
