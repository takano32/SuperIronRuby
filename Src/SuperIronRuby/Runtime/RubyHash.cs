namespace SuperIronRuby.Runtime;

/// <summary>
/// Ruby's <c>Hash</c>: an insertion-ordered map using Ruby <c>eql?</c>/<c>hash</c>
/// key semantics (see <see cref="RubyValueComparer"/>). Insertion order is
/// preserved for iteration, matching Ruby (verified against ruby 4.0.2:
/// <c>{b:1, a:2}.keys == [:b, :a]</c>).
/// </summary>
/// <remarks>
/// Storage strategy for task R1 is simple and correct: a <see cref="Dictionary{TKey,TValue}"/>
/// keyed by the Ruby key with <see cref="RubyValueComparer"/>, mapping to a node
/// in a doubly-linked insertion-order list. Reassigning an existing key keeps its
/// original position (Ruby behavior). Performance tuning is left for later.
/// </remarks>
public sealed class RubyHash
{
    /// <summary>One key/value entry, also a node in the insertion-order list.</summary>
    public sealed class Entry
    {
        public object? Key;
        public object? Value;
        internal Entry? Prev;
        internal Entry? Next;

        internal Entry(object? key, object? value)
        {
            Key = key;
            Value = value;
        }
    }

    // Comparer wraps Ruby keys; null (Ruby nil) is a legal key, but Dictionary
    // disallows a null TKey, so nil is stored under this sentinel.
    private static readonly object NilKeySentinel = new();

    private readonly Dictionary<object, Entry> _map;
    private Entry? _head;
    private Entry? _tail;

    /// <summary>The default value returned for a missing key (Ruby <c>Hash.new(default)</c>).</summary>
    public object? DefaultValue { get; set; }

    /// <summary>The default-value block (Ruby <c>Hash.new { |h, k| ... }</c>); when set,
    /// takes precedence over <see cref="DefaultValue"/>. Invoked by the Hash builtins,
    /// not here.</summary>
    public RubyProc? DefaultProc { get; set; }

    /// <summary>True once frozen; mutation then raises (enforced by builtins).</summary>
    public bool IsFrozen { get; set; }

    public RubyHash() => _map = new Dictionary<object, Entry>(RubyValueComparer.Instance);

    public RubyHash(int capacity)
        => _map = new Dictionary<object, Entry>(capacity, RubyValueComparer.Instance);

    private static object NormalizeKey(object? key) => key ?? NilKeySentinel;

    /// <summary>Number of entries.</summary>
    public int Count => _map.Count;

    /// <summary>True when there are no entries.</summary>
    public bool IsEmpty => _map.Count == 0;

    /// <summary>True if the key is present (Ruby <c>key?</c>).</summary>
    public bool ContainsKey(object? key) => _map.ContainsKey(NormalizeKey(key));

    /// <summary>Gets the stored value for a present key, else <c>false</c> (and
    /// <paramref name="value"/> = null). Does NOT apply defaults — that is the
    /// builtin's job.</summary>
    public bool TryGetValue(object? key, out object? value)
    {
        if (_map.TryGetValue(NormalizeKey(key), out var entry))
        {
            value = entry.Value;
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>Reads a key's value, or <c>null</c> (nil) if absent, without
    /// applying the hash's default. Use the builtin <c>[]</c> for default semantics.</summary>
    public object? GetOrNull(object? key)
        => _map.TryGetValue(NormalizeKey(key), out var entry) ? entry.Value : null;

    /// <summary>Stores a key/value pair. Existing keys keep their position; new
    /// keys are appended (Ruby insertion-order semantics).</summary>
    public void Store(object? key, object? value)
    {
        var k = NormalizeKey(key);
        if (_map.TryGetValue(k, out var entry))
        {
            entry.Value = value;
            return;
        }

        var node = new Entry(key, value);
        _map[k] = node;
        if (_tail is null)
        {
            _head = _tail = node;
        }
        else
        {
            node.Prev = _tail;
            _tail.Next = node;
            _tail = node;
        }
    }

    /// <summary>Removes a key, returning true and its value if it was present.</summary>
    public bool Remove(object? key, out object? value)
    {
        var k = NormalizeKey(key);
        if (!_map.TryGetValue(k, out var node))
        {
            value = null;
            return false;
        }

        _map.Remove(k);
        if (node.Prev is not null) node.Prev.Next = node.Next;
        else _head = node.Next;
        if (node.Next is not null) node.Next.Prev = node.Prev;
        else _tail = node.Prev;

        value = node.Value;
        return true;
    }

    /// <summary>Removes all entries.</summary>
    public void Clear()
    {
        _map.Clear();
        _head = _tail = null;
    }

    /// <summary>Entries in insertion order.</summary>
    public IEnumerable<Entry> Entries()
    {
        for (var node = _head; node is not null; node = node.Next)
            yield return node;
    }

    /// <summary>Keys in insertion order.</summary>
    public IEnumerable<object?> Keys()
    {
        for (var node = _head; node is not null; node = node.Next)
            yield return node.Key;
    }

    /// <summary>Values in insertion order.</summary>
    public IEnumerable<object?> Values()
    {
        for (var node = _head; node is not null; node = node.Next)
            yield return node.Value;
    }
}
