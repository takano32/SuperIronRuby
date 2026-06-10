namespace SuperIronRuby.Runtime;

/// <summary>
/// Minimal placeholder for a Ruby module. Fleshed out by task R2 (class
/// hierarchy, ancestors, method tables, constants). For task R1 this exists
/// only so the value types (RubyObject, RubyClass) can reference it and the
/// assembly compiles self-contained.
/// </summary>
public class RubyModule
{
    /// <summary>The module's name (may be null for anonymous modules).</summary>
    public string? Name;
}
