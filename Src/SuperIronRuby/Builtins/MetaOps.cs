using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>Metaprogramming on Module/Class (task B12 core).</summary>
[RubyClass("Module")]
public static class ModuleMetaOps
{
    private static RubyModule M(object? s) => (RubyModule)s!;

    [RubyMethod("private")]
    public static object? Private(RubyContext c, object? s, object?[] a, RubyProc? b)
        => SetVisibility(M(s), a, RubyMethodVisibility.Private);

    [RubyMethod("public")]
    public static object? Public(RubyContext c, object? s, object?[] a, RubyProc? b)
        => SetVisibility(M(s), a, RubyMethodVisibility.Public);

    [RubyMethod("protected")]
    public static object? Protected(RubyContext c, object? s, object?[] a, RubyProc? b)
        => SetVisibility(M(s), a, RubyMethodVisibility.Protected);

    private static object? SetVisibility(RubyModule mod, object?[] args, RubyMethodVisibility vis)
    {
        if (args.Length == 0)
        {
            mod.DefaultVisibility = vis;     // no-arg: change the default for later defs
            return null;
        }
        foreach (var a in args)
        {
            var name = KernelOps.NameOf(a);
            var m = mod.GetOwnMethod(name);
            if (m is not null) m.Visibility = vis;
        }
        return args.Length == 1 ? args[0] : null;
    }

    [RubyMethod("module_function")]
    public static object? ModuleFunction(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var mod = M(s);
        foreach (var arg in a)
        {
            var name = KernelOps.NameOf(arg);
            var m = mod.GetOwnMethod(name);
            if (m is not null)
                c.SingletonClassOf(mod).DefineMethod(name, m.CloneAs(name, c.SingletonClassOf(mod)));
        }
        return s;
    }

    [RubyMethod("define_method")]
    public static object? DefineMethod(RubyContext c, object? s, object?[] a, RubyProc? blk)
    {
        var mod = M(s);
        var name = KernelOps.NameOf(a[0]);
        var body = blk ?? (a.Length > 1 ? a[1] as RubyProc : null)
            ?? throw c.RaiseArgumentError("tried to create a Method with no block");
        mod.DefineMethod(name, RubyMethodInfo.FromBuiltin(name, mod,
            (ctx, self, args, b2) => InvokeProcAsMethod(body, self, args, b2)));
        return c.Intern(name);
    }

    private static object? InvokeProcAsMethod(RubyProc proc, object? self, object?[] args, RubyProc? block)
        // The block runs with the call's args; self-rebinding for define_method
        // procs is approximate (the proc keeps its captured self). Good enough for
        // the common define_method usage.
        => proc.Call(args);

    [RubyMethod("alias_method")]
    public static object? AliasMethod(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        M(s).AliasMethod(KernelOps.NameOf(a[0]), KernelOps.NameOf(a[1]));
        return c.Intern(KernelOps.NameOf(a[0]));
    }

    [RubyMethod("remove_method")]
    [RubyMethod("undef_method")]
    public static object? RemoveMethod(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        foreach (var arg in a) M(s).RemoveMethod(KernelOps.NameOf(arg));
        return s;
    }

    [RubyMethod("attr")]
    public static object? Attr(RubyContext c, object? s, object?[] a, RubyProc? b)
        => ModuleOps.AttrReader(c, s, a, b);
}

[RubyModule("Kernel")]
public static class KernelMetaOps
{
    [RubyMethod("define_singleton_method")]
    public static object? DefineSingletonMethod(RubyContext c, object? self, object?[] a, RubyProc? blk)
    {
        var singleton = c.SingletonClassOf(self);
        return ModuleMetaOps.DefineMethod(c, singleton, a, blk);
    }

    [RubyMethod("singleton_class")]
    public static object? SingletonClass(RubyContext c, object? self, object?[] a, RubyProc? b)
        => c.SingletonClassOf(self);

    [RubyMethod("methods")]
    public static object? Methods(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        var names = new HashSet<string>();
        foreach (var m in c.GetImmediateClassOf(self).Ancestors)
            foreach (var n in m.OwnMethodNames) names.Add(n);
        var arr = new RubyArray();
        foreach (var n in names) arr.Add(c.Intern(n));
        return arr;
    }

    [RubyMethod("respond_to_missing?", Visibility = RubyMethodVisibility.Private)]
    public static object? RespondToMissing(RubyContext c, object? self, object?[] a, RubyProc? b) => false;
}
