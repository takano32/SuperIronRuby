using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>
/// Foundation methods shared by all objects (Ruby's <c>Kernel</c> module, mixed
/// into Object). Higher-level Kernel methods (puts/p/format/...) live in the
/// B-layer (task B1); this is the minimum everything else relies on.
/// </summary>
[RubyModule("Kernel")]
public static class KernelOps
{
    [RubyMethod("class")]
    public static object? ClassOf(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => ctx.GetClassOf(self);

    [RubyMethod("frozen?")]
    public static object? Frozen(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => self switch
        {
            null or bool or long or double or System.Numerics.BigInteger or RubySymbol => true,
            MutableString s => s.IsFrozen,
            RubyArray a => a.IsFrozen,
            RubyHash h => h.IsFrozen,
            RubyObject o => o.IsFrozen,
            _ => false,
        };

    [RubyMethod("freeze")]
    public static object? Freeze(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        switch (self)
        {
            case MutableString s: s.Freeze(); break;
            case RubyArray a: a.IsFrozen = true; break;
            case RubyHash h: h.IsFrozen = true; break;
            case RubyObject o: o.IsFrozen = true; break;
        }
        return self;
    }

    [RubyMethod("nil?")]
    public static object? IsNil(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => self is null;

    [RubyMethod("is_a?")]
    [RubyMethod("kind_of?")]
    public static object? IsA(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => args[0] is RubyModule m && ctx.IsKindOf(self, m);

    [RubyMethod("instance_of?")]
    public static object? InstanceOf(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => args[0] is RubyClass c && ReferenceEquals(ctx.GetClassOf(self), c);

    [RubyMethod("respond_to?")]
    public static object? RespondTo(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        var name = NameOf(args[0]);
        var includeAll = args.Length > 1 && RubyContext.Truthy(args[1]);
        return ctx.RespondTo(self, name, includeAll);
    }

    [RubyMethod("send")]
    [RubyMethod("__send__")]
    [RubyMethod("public_send")]
    public static object? SendMethod(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        var name = NameOf(args[0]);
        var rest = args.Skip(1).ToArray();
        return ctx.Send(self, name, rest, block, RubyCallFlags.ImplicitSelf);
    }

    [RubyMethod("object_id")]
    public static object? ObjectId(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => ctx.ObjectIdOf(self);

    [RubyMethod("equal?")]
    public static object? Equal(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => ReferenceEquals(self, args[0]) || RubyValueComparer.Instance.RubyEql(self, args[0]) && IsImmediate(self);

    [RubyMethod("==")]
    public static object? Eq(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => RubyValueComparer.Instance.RubyEql(self, args[0]) || ReferenceEquals(self, args[0]);

    [RubyMethod("!=")]
    public static object? Ne(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => !RubyContext.Truthy(ctx.Send(self, "==", args));

    [RubyMethod("!")]
    public static object? Not(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => !RubyContext.Truthy(self);

    [RubyMethod("hash")]
    public static object? Hash(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => (long)RubyValueComparer.Instance.RubyHashCode(self);

    [RubyMethod("to_s")]
    public static object? ToS(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => new MutableString(ctx.DefaultToS(self));

    [RubyMethod("inspect")]
    public static object? InspectM(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => new MutableString(ctx.DefaultInspect(self));

    [RubyMethod("itself")]
    public static object? Itself(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => self;

    [RubyMethod("tap")]
    public static object? Tap(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        block?.Call(self);
        return self;
    }

    [RubyMethod("then")]
    [RubyMethod("yield_self")]
    public static object? Then(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => block is null ? self : block.Call(self);

    [RubyMethod("instance_variable_get")]
    public static object? IvarGet(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => self is RubyObject o ? o.GetIvar(NameOf(args[0])) : null;

    [RubyMethod("instance_variable_set")]
    public static object? IvarSet(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        if (self is RubyObject o) o.SetIvar(NameOf(args[0]), args[1]);
        return args[1];
    }

    [RubyMethod("instance_variable_defined?")]
    public static object? IvarDefined(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => self is RubyObject o && o.TryGetIvar(NameOf(args[0]), out _);

    [RubyMethod("instance_variables")]
    public static object? Ivars(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        var arr = new RubyArray();
        if (self is RubyObject o)
            foreach (var n in o.IvarNames) arr.Add(ctx.Intern(n));
        return arr;
    }

    [RubyMethod("dup")]
    [RubyMethod("clone")]
    public static object? Dup(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => self switch
        {
            MutableString s => s.Duplicate(),
            RubyArray a => new RubyArray(a),
            RubyObject o => DupObject(o),
            _ => self, // immediates dup to themselves
        };

    private static RubyObject DupObject(RubyObject o)
    {
        var copy = new RubyObject(o.RubyClass);
        foreach (var n in o.IvarNames) copy.SetIvar(n, o.GetIvar(n));
        return copy;
    }

    // ---- helpers -----------------------------------------------------------

    public static string NameOf(object? value) => value switch
    {
        RubySymbol sym => sym.Name,
        MutableString s => s.Value,
        _ => value?.ToString() ?? "",
    };

    private static bool IsImmediate(object? value)
        => value is null or bool or long or double or System.Numerics.BigInteger or RubySymbol;
}
