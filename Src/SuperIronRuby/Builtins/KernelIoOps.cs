using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>
/// Kernel I/O and the raise family. This is the minimal set the engine needs to
/// be useful; task B1 expands it (format/Integer()/gets/catch-throw/...).
/// </summary>
[RubyModule("Kernel")]
public static class KernelIoOps
{
    [RubyMethod("puts", Kind = RubyMethodKind.ModuleFunction)]
    public static object? Puts(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        if (args.Length == 0)
        {
            ctx.Stdout.Write('\n');
            return null;
        }
        foreach (var arg in args) PutsOne(ctx, arg);
        return null;
    }

    private static void PutsOne(RubyContext ctx, object? arg)
    {
        if (arg is RubyArray arr)
        {
            if (arr.Count == 0) { ctx.Stdout.Write('\n'); return; }
            foreach (var item in arr) PutsOne(ctx, item);
            return;
        }
        var s = arg is null ? "" : ctx.ToStr(arg);
        ctx.Stdout.Write(s);
        if (!s.EndsWith('\n')) ctx.Stdout.Write('\n');
    }

    [RubyMethod("print", Kind = RubyMethodKind.ModuleFunction)]
    public static object? Print(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        foreach (var arg in args) ctx.Stdout.Write(arg is null ? "" : ctx.ToStr(arg));
        return null;
    }

    [RubyMethod("p", Kind = RubyMethodKind.ModuleFunction)]
    public static object? P(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        foreach (var arg in args)
        {
            ctx.Stdout.Write(ctx.Inspect(arg));
            ctx.Stdout.Write('\n');
        }
        return args.Length switch { 0 => null, 1 => args[0], _ => Pack(args) };
    }

    [RubyMethod("pp", Kind = RubyMethodKind.ModuleFunction)]
    public static object? Pp(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => P(ctx, self, args, block);

    [RubyMethod("warn", Kind = RubyMethodKind.ModuleFunction)]
    public static object? Warn(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        foreach (var arg in args)
        {
            ctx.Stderr.Write(arg is null ? "" : ctx.ToStr(arg));
            ctx.Stderr.Write('\n');
        }
        return null;
    }

    [RubyMethod("raise", Kind = RubyMethodKind.ModuleFunction)]
    [RubyMethod("fail", Kind = RubyMethodKind.ModuleFunction)]
    public static object? Raise(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        switch (args.Length)
        {
            case 0:
                var cur = ctx.CurrentException ?? ctx.CreateException(ctx.RuntimeErrorClass, "unhandled exception");
                throw new RubyRaiseException(cur);
            default:
                switch (args[0])
                {
                    case MutableString msg:
                        throw ctx.RaiseError(ctx.RuntimeErrorClass, msg.Value);
                    case RubyClass cls:
                        var m = args.Length > 1 && args[1] is MutableString m2 ? m2.Value : (cls.Name ?? "");
                        throw ctx.RaiseError(cls, m);
                    case RubyExceptionObject ex:
                        throw new RubyRaiseException(ex);
                    default:
                        throw ctx.RaiseTypeError("exception class/object expected");
                }
        }
    }

    private static RubyArray Pack(object?[] args)
    {
        var arr = new RubyArray();
        arr.AddRange(args);
        return arr;
    }
}
