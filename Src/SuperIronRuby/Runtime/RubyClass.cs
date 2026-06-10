namespace SuperIronRuby.Runtime;

/// <summary>
/// A Ruby class: a module with a superclass and the ability to allocate
/// instances. Singleton classes are represented as ordinary
/// <see cref="RubyClass"/> instances with <see cref="IsSingletonClass"/> set.
/// </summary>
public class RubyClass : RubyModule
{
    private RubyClass? _superclass;

    /// <summary>The superclass, or null for BasicObject.</summary>
    public RubyClass? Superclass
    {
        get => _superclass;
        set
        {
            _superclass = value;
            InvalidateAllAncestors();
        }
    }

    /// <summary>
    /// Allocates a bare instance of this class. Defaults to a plain
    /// <see cref="RubyObject"/>; builtin classes (String, Array, ...) and user
    /// classes can override how instances are created.
    /// </summary>
    public Func<RubyContext, RubyClass, object>? Allocator;

    /// <summary>True if this is a singleton (eigen) class.</summary>
    public bool IsSingletonClass;

    /// <summary>For a singleton class, the object it is attached to.</summary>
    public object? AttachedObject;

    public RubyClass(string? name, RubyClass? superclass)
    {
        Name = name;
        _superclass = superclass;
    }

    /// <summary>Allocates an instance via <see cref="Allocator"/>, falling back
    /// to a plain <see cref="RubyObject"/>.</summary>
    public object Allocate(RubyContext context)
        => Allocator is not null ? Allocator(context, this) : new RubyObject(this);

    // A class's linearization base is its superclass's full ancestor list.
    private protected override IEnumerable<RubyModule> SuperclassAncestors()
        => _superclass is null ? Enumerable.Empty<RubyModule>() : _superclass.Ancestors;

    public override string ToString() => Name ?? "#<Class>";
}
