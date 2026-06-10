namespace SuperIronRuby.Runtime;

/// <summary>
/// The base representation for an ordinary Ruby object: a reference to its class
/// plus a lazily-allocated instance-variable table. User-defined Ruby classes
/// are modeled by instances (or subclasses) of this type.
/// </summary>
public class RubyObject
{
    private Dictionary<string, object?>? _ivars;

    /// <summary>
    /// The object's class. (For task R1 <see cref="RubyClass"/> is a placeholder;
    /// task R2 fleshes out singleton-class / logical-class lookup.)
    /// </summary>
    public RubyClass RubyClass { get; set; }

    /// <summary>True once frozen; Ruby mutation of a frozen object raises.</summary>
    public bool IsFrozen { get; set; }

    public RubyObject(RubyClass rubyClass) => RubyClass = rubyClass;

    /// <summary>
    /// Reads an instance variable (name including the leading <c>@</c>), or
    /// returns <c>null</c> (Ruby's <c>nil</c>) if unset.
    /// </summary>
    public object? GetIvar(string name)
        => _ivars is not null && _ivars.TryGetValue(name, out var v) ? v : null;

    /// <summary>Returns true and the value if the ivar is set.</summary>
    public bool TryGetIvar(string name, out object? value)
    {
        if (_ivars is not null)
            return _ivars.TryGetValue(name, out value);
        value = null;
        return false;
    }

    /// <summary>Sets an instance variable, allocating the table on first use.</summary>
    public void SetIvar(string name, object? value)
    {
        _ivars ??= new Dictionary<string, object?>();
        _ivars[name] = value;
    }

    /// <summary>The instance-variable names currently set (Ruby's <c>instance_variables</c>).</summary>
    public IEnumerable<string> IvarNames => _ivars?.Keys ?? Enumerable.Empty<string>();
}
