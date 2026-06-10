namespace SuperIronRuby.Runtime;

/// <summary>
/// Minimal placeholder for a Ruby class. Fleshed out by task R2 (superclass
/// chain, allocator, method/constant tables, singleton classes). For task R1
/// this exists only so RubyObject can carry a class reference and the assembly
/// compiles self-contained.
/// </summary>
public sealed class RubyClass : RubyModule
{
}
