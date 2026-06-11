using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>Proc/lambda methods (task I8 core; task B10 adds curry/compose/params).</summary>
[RubyClass("Proc")]
public static class ProcOps
{
    [RubyMethod("call")]
    [RubyMethod("()")]
    [RubyMethod("[]")]
    [RubyMethod("yield")]
    [RubyMethod("===")]
    public static object? Call(RubyContext c, object? self, object?[] args, RubyProc? block)
        => ((RubyProc)self!).Call(args);

    [RubyMethod("lambda?")]
    public static object? IsLambda(RubyContext c, object? self, object?[] a, RubyProc? b)
        => ((RubyProc)self!).IsLambda;

    [RubyMethod("to_proc")]
    public static object? ToProc(RubyContext c, object? self, object?[] a, RubyProc? b) => self;

    [RubyMethod("arity")]
    public static object? Arity(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        var p = (RubyProc)self!;
        // Best-effort: exact min when no optional/splat, else -(min+1).
        if (p.ArityMax == p.ArityMin && p.ArityMax >= 0) return (long)p.ArityMin;
        return (long)(-(p.ArityMin + 1));
    }
}

[RubyModule("Kernel")]
public static class ProcKernelOps
{
    [RubyMethod("proc", Kind = RubyMethodKind.ModuleFunction)]
    public static object? Proc(RubyContext c, object? self, object?[] a, RubyProc? block)
        => block ?? throw c.RaiseArgumentError("tried to create Proc object without a block");

    [RubyMethod("lambda", Kind = RubyMethodKind.ModuleFunction)]
    public static object? Lambda(RubyContext c, object? self, object?[] a, RubyProc? block)
    {
        if (block is null) throw c.RaiseArgumentError("tried to create Proc object without a block");
        block.IsLambda = true;   // mark the given block as a lambda (semantics caveat)
        return block;
    }
    // block_given? is handled specially by the interpreter (it needs the
    // enclosing method's block, not a block passed to block_given? itself).
}
