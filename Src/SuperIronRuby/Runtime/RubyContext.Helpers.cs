using System.Numerics;
using System.Runtime.CompilerServices;

namespace SuperIronRuby.Runtime;

// Shared value helpers used by the builtin library (object_id, inspect/to_s
// dispatch, truthiness). Behavior verified against ruby 4.0.2.
public sealed partial class RubyContext
{
    private readonly ConditionalWeakTable<object, object> _objectIds = new();
    private long _nextObjectId = 8; // reference-object ids grow from here

    /// <summary>Ruby <c>nil ? false : value != false</c> truthiness.</summary>
    public static bool Truthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        _ => true,
    };

    /// <summary>A stable per-value object id (matches MRI for the immediates that
    /// have fixed ids; reference objects get a stable growing counter).</summary>
    public long ObjectIdOf(object? value)
    {
        switch (value)
        {
            case null: return 4L;       // ruby 4.0: nil.object_id == 4
            case false: return 0L;
            case true: return 20L;
            case long l: return 2 * l + 1;   // Integer n -> 2n+1
            case BigInteger b: return (long)(2 * b + 1);
            default:
                if (_objectIds.TryGetValue(value, out var id)) return (long)id;
                var next = _nextObjectId;
                _nextObjectId += 8;
                _objectIds.Add(value, next);
                return next;
        }
    }

    /// <summary>Dispatches <c>inspect</c> and returns the resulting C# string.</summary>
    public string Inspect(object? value)
    {
        var result = Send(value, "inspect", System.Array.Empty<object?>());
        return result is MutableString s ? s.Value : DefaultInspect(value);
    }

    /// <summary>Dispatches <c>to_s</c> and returns the resulting C# string.</summary>
    public string ToStr(object? value)
    {
        var result = Send(value, "to_s", System.Array.Empty<object?>());
        return result is MutableString s ? s.Value : DefaultToS(value);
    }

    /// <summary>The default <c>to_s</c> for an ordinary object: "#&lt;Class:0x...&gt;".</summary>
    public string DefaultToS(object? value)
    {
        var cls = GetClassOf(value);
        return $"#<{cls.Name}:0x{ObjectIdOf(value):x16}>";
    }

    /// <summary>The default <c>inspect</c>: like to_s but listing instance
    /// variables, e.g. "#&lt;Point:0x... @x=1, @y=2&gt;".</summary>
    public string DefaultInspect(object? value)
    {
        var cls = GetClassOf(value);
        if (value is RubyObject obj)
        {
            var names = obj.IvarNames.ToList();
            if (names.Count == 0)
                return $"#<{cls.Name}:0x{ObjectIdOf(value):x16}>";
            var parts = names.Select(n => $"{n}={Inspect(obj.GetIvar(n))}");
            return $"#<{cls.Name}:0x{ObjectIdOf(value):x16} {string.Join(", ", parts)}>";
        }
        return $"#<{cls.Name}:0x{ObjectIdOf(value):x16}>";
    }
}
