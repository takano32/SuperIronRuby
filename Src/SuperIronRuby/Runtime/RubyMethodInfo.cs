namespace SuperIronRuby.Runtime;

/// <summary>Ruby method visibility.</summary>
public enum RubyMethodVisibility
{
    Public,
    Private,
    Protected,
}

/// <summary>
/// The uniform signature for a builtin method implemented in C#.
/// <paramref name="self"/> is the receiver, <paramref name="args"/> the
/// evaluated positional arguments, and <paramref name="block"/> the passed
/// block (or null).
/// </summary>
public delegate object? BuiltinMethodBody(RubyContext context, object? self, object?[] args, RubyProc? block);

/// <summary>
/// A method entry in a module's method table. Exactly one of
/// <see cref="Builtin"/> (C# implementation) or <see cref="InterpretedDef"/>
/// (an interpreter-owned method body) is set.
/// </summary>
public sealed class RubyMethodInfo
{
    public string Name;
    public RubyModule Owner;
    public RubyMethodVisibility Visibility;

    /// <summary>Minimum required positional arity.</summary>
    public int ArityMin;

    /// <summary>Maximum positional arity, or -1 for unbounded (splat).</summary>
    public int ArityMax;

    /// <summary>C# implementation, when this is a builtin method.</summary>
    public BuiltinMethodBody? Builtin;

    /// <summary>Interpreter-owned method body (e.g. a DefNode + closure), when
    /// this is a Ruby-defined method. Opaque to the runtime.</summary>
    public object? InterpretedDef;

    public RubyMethodInfo(string name, RubyModule owner)
    {
        Name = name;
        Owner = owner;
        Visibility = RubyMethodVisibility.Public;
        ArityMin = 0;
        ArityMax = -1;
    }

    /// <summary>Creates a builtin method entry.</summary>
    public static RubyMethodInfo FromBuiltin(string name, RubyModule owner, BuiltinMethodBody body,
        RubyMethodVisibility visibility = RubyMethodVisibility.Public, int arityMin = 0, int arityMax = -1)
        => new(name, owner)
        {
            Builtin = body,
            Visibility = visibility,
            ArityMin = arityMin,
            ArityMax = arityMax,
        };

    /// <summary>Returns a copy bound to a new name/owner (used by alias).</summary>
    public RubyMethodInfo CloneAs(string newName, RubyModule newOwner)
        => new(newName, newOwner)
        {
            Visibility = Visibility,
            ArityMin = ArityMin,
            ArityMax = ArityMax,
            Builtin = Builtin,
            InterpretedDef = InterpretedDef,
        };
}
