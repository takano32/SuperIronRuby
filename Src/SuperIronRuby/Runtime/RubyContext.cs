namespace SuperIronRuby.Runtime;

/// <summary>
/// The per-engine Ruby world (class hierarchy, globals, method dispatch).
/// Minimal placeholder for task R2 — fleshed out by task R3 (bootstrap of the
/// core class hierarchy, GetClassOf, intern, globals) and later runtime tasks.
/// It exists here so that <see cref="BuiltinMethodBody"/> and
/// <see cref="RubyClass.Allocator"/> have a concrete type to reference.
/// </summary>
public sealed partial class RubyContext
{
}
