using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>Foundation methods on Module (and inherited by Class).</summary>
[RubyClass("Module")]
public static class ModuleOps
{
    [RubyMethod("name")]
    [RubyMethod("to_s")]
    [RubyMethod("inspect")]
    public static object? Name(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        var mod = (RubyModule)self!;
        return mod.Name is null ? (object?)null : new MutableString(mod.Name);
    }

    [RubyMethod("ancestors")]
    public static object? Ancestors(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        var arr = new RubyArray();
        foreach (var m in ((RubyModule)self!).Ancestors) arr.Add(m);
        return arr;
    }

    [RubyMethod("include")]
    public static object? Include(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        var mod = (RubyModule)self!;
        // Ruby applies includes right-to-left so the leftmost ends up closest.
        for (int i = args.Length - 1; i >= 0; i--)
            if (args[i] is RubyModule m) mod.Include(m);
        return self;
    }

    [RubyMethod("prepend")]
    public static object? Prepend(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        var mod = (RubyModule)self!;
        for (int i = args.Length - 1; i >= 0; i--)
            if (args[i] is RubyModule m) mod.Prepend(m);
        return self;
    }

    [RubyMethod("include?")]
    public static object? IncludeQ(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => args[0] is RubyModule m && ((RubyModule)self!).Ancestors.Contains(m) && !ReferenceEquals(m, self);

    [RubyMethod("===")]
    public static object? CaseEq(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => ctx.IsKindOf(args[0], (RubyModule)self!);

    [RubyMethod("==")]
    public static object? Eq(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => ReferenceEquals(self, args[0]);

    [RubyMethod("instance_methods")]
    public static object? InstanceMethods(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        var mod = (RubyModule)self!;
        bool inherited = args.Length == 0 || RubyContext.Truthy(args[0]);
        var names = new HashSet<string>();
        var source = inherited ? mod.Ancestors : new[] { mod };
        foreach (var m in source)
            foreach (var n in m.OwnMethodNames)
                names.Add(n);
        var arr = new RubyArray();
        foreach (var n in names) arr.Add(ctx.Intern(n));
        return arr;
    }

    [RubyMethod("method_defined?")]
    public static object? MethodDefined(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => ((RubyModule)self!).LookupMethod(KernelOps.NameOf(args[0])) is not null;

    [RubyMethod("const_get")]
    public static object? ConstGet(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        var name = KernelOps.NameOf(args[0]);
        if (((RubyModule)self!).TryGetConstant(name, out var v)) return v;
        throw ctx.RaiseNameError($"uninitialized constant {name}");
    }

    [RubyMethod("const_set")]
    public static object? ConstSet(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        ((RubyModule)self!).SetConstant(KernelOps.NameOf(args[0]), args[1]);
        return args[1];
    }

    [RubyMethod("const_defined?")]
    public static object? ConstDefined(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => ((RubyModule)self!).TryGetConstant(KernelOps.NameOf(args[0]), out _);

    [RubyMethod("attr_reader")]
    public static object? AttrReader(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        DefineAccessors(ctx, (RubyModule)self!, args, read: true, write: false);
        return null;
    }

    [RubyMethod("attr_writer")]
    public static object? AttrWriter(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        DefineAccessors(ctx, (RubyModule)self!, args, read: false, write: true);
        return null;
    }

    [RubyMethod("attr_accessor")]
    public static object? AttrAccessor(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        DefineAccessors(ctx, (RubyModule)self!, args, read: true, write: true);
        return null;
    }

    private static void DefineAccessors(RubyContext ctx, RubyModule mod, object?[] args, bool read, bool write)
    {
        foreach (var a in args)
        {
            var name = KernelOps.NameOf(a);
            var ivar = "@" + name;
            if (read)
            {
                mod.DefineMethod(name, RubyMethodInfo.FromBuiltin(name, mod,
                    (_, self, _, _) => self is RubyObject o ? o.GetIvar(ivar) : null, arityMin: 0, arityMax: 0));
            }
            if (write)
            {
                var setter = name + "=";
                mod.DefineMethod(setter, RubyMethodInfo.FromBuiltin(setter, mod,
                    (_, self, a2, _) => { if (self is RubyObject o) o.SetIvar(ivar, a2[0]); return a2[0]; },
                    arityMin: 1, arityMax: 1));
            }
        }
    }
}

/// <summary>Foundation methods on Class.</summary>
[RubyClass("Class", Inherits = "Module")]
public static class ClassOps
{
    [RubyMethod("allocate")]
    public static object? Allocate(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => ((RubyClass)self!).Allocate(ctx);

    [RubyMethod("new")]
    public static object? New(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        var instance = ((RubyClass)self!).Allocate(ctx);
        ctx.Send(instance, "initialize", args, block, RubyCallFlags.ImplicitSelf);
        return instance;
    }

    [RubyMethod("superclass")]
    public static object? Superclass(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => ((RubyClass)self!).Superclass;
}
