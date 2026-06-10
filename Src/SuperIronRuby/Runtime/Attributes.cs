namespace SuperIronRuby.Runtime;

/// <summary>How a builtin method attaches to its class.</summary>
public enum RubyMethodKind
{
    /// <summary>An ordinary instance method.</summary>
    Instance,
    /// <summary>A class/singleton method (defined on the class's singleton).</summary>
    Static,
    /// <summary>A module function: a private instance method AND a singleton
    /// method (Ruby <c>module_function</c>).</summary>
    ModuleFunction,
}

/// <summary>
/// Marks a static C# class as the implementation of a Ruby class. The methods
/// it contains are bound by <see cref="BuiltinLoader"/>. (IronRuby-style.)
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RubyClassAttribute : Attribute
{
    /// <summary>The Ruby class name (e.g. "String").</summary>
    public string Name { get; }

    /// <summary>When set, instances of this CLR type are routed to this Ruby
    /// class by <c>GetClassOf</c> (e.g. <c>MutableString</c> -> String).</summary>
    public Type? Extends { get; set; }

    /// <summary>The superclass constant name; defaults to "Object".</summary>
    public string Inherits { get; set; } = "Object";

    public RubyClassAttribute(string name) => Name = name;
}

/// <summary>Marks a static C# class as the implementation of a Ruby module.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RubyModuleAttribute : Attribute
{
    public string Name { get; }
    public RubyModuleAttribute(string name) => Name = name;
}

/// <summary>
/// Marks a static method (signature
/// <c>(RubyContext, object?, object?[], RubyProc?) -> object?</c>) as a Ruby
/// method on the enclosing <see cref="RubyClassAttribute"/>/<see cref="RubyModuleAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RubyMethodAttribute : Attribute
{
    public string Name { get; }
    public RubyMethodKind Kind { get; set; } = RubyMethodKind.Instance;
    public RubyMethodVisibility Visibility { get; set; } = RubyMethodVisibility.Public;
    public int ArityMin { get; set; }
    public int ArityMax { get; set; } = -1;

    public RubyMethodAttribute(string name) => Name = name;
}
