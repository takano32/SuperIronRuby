using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>Struct and Data (task B9 core). Struct.new(*members) and Data.define
/// produce a new class with accessors, value semantics, and pattern-match support.</summary>
[RubyClass("Struct")]
public static class StructOps
{
    // Members are stored on the generated class as a constant-ish list.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RubyClass, List<string>> Members = new();

    [RubyMethod("new", Kind = RubyMethodKind.Static)]
    public static object? New(RubyContext c, object? self, object?[] args, RubyProc? block)
        => Build(c, args, c.ObjectClass.TryGetConstant("Struct", out var sc) ? (RubyClass)sc! : c.ObjectClass, "struct");

    internal static RubyClass Build(RubyContext c, object?[] args, RubyClass super, string kind)
    {
        var members = args.Where(a => a is RubySymbol or MutableString).Select(KernelOps.NameOf).ToList();
        var cls = new RubyClass(null, super);
        Members.Add(cls, members);

        // accessors
        foreach (var m in members)
        {
            var ivar = "@" + m;
            cls.DefineMethod(m, RubyMethodInfo.FromBuiltin(m, cls,
                (_, s, _, _) => ((RubyObject)s!).GetIvar(ivar), arityMin: 0, arityMax: 0));
            cls.DefineMethod(m + "=", RubyMethodInfo.FromBuiltin(m + "=", cls,
                (_, s, a, _) => { ((RubyObject)s!).SetIvar(ivar, a[0]); return a[0]; }, arityMin: 1, arityMax: 1));
        }

        // initialize (positional)
        cls.DefineMethod("initialize", RubyMethodInfo.FromBuiltin("initialize", cls, (ctx, s, a, _) =>
        {
            var o = (RubyObject)s!;
            for (int i = 0; i < members.Count; i++)
                o.SetIvar("@" + members[i], i < a.Length ? a[i] : null);
            return null;
        }));

        DefineCommon(c, cls, members, kind);
        cls.Allocator = (_, k) => new RubyObject(k);
        return cls;
    }

    private static void DefineCommon(RubyContext c, RubyClass cls, List<string> members, string kind)
    {
        object? GetAll(object? s) { var arr = new RubyArray(); foreach (var m in members) arr.Add(((RubyObject)s!).GetIvar("@" + m)); return arr; }

        cls.DefineMethod("to_a", RubyMethodInfo.FromBuiltin("to_a", cls, (_, s, _, _) => GetAll(s)));
        cls.DefineMethod("deconstruct", RubyMethodInfo.FromBuiltin("deconstruct", cls, (_, s, _, _) => GetAll(s)));
        cls.DefineMethod("members", RubyMethodInfo.FromBuiltin("members", cls,
            (ctx, _, _, _) => { var a = new RubyArray(); foreach (var m in members) a.Add(ctx.Intern(m)); return a; }));

        cls.DefineMethod("to_h", RubyMethodInfo.FromBuiltin("to_h", cls, (ctx, s, _, _) =>
        {
            var h = new RubyHash();
            foreach (var m in members) h.Store(ctx.Intern(m), ((RubyObject)s!).GetIvar("@" + m));
            return h;
        }));
        cls.DefineMethod("deconstruct_keys", RubyMethodInfo.FromBuiltin("deconstruct_keys", cls, (ctx, s, _, _) =>
        {
            var h = new RubyHash();
            foreach (var m in members) h.Store(ctx.Intern(m), ((RubyObject)s!).GetIvar("@" + m));
            return h;
        }));

        cls.DefineMethod("[]", RubyMethodInfo.FromBuiltin("[]", cls, (_, s, a, _) =>
        {
            var o = (RubyObject)s!;
            if (a[0] is long i) return o.GetIvar("@" + members[(int)i]);
            return o.GetIvar("@" + KernelOps.NameOf(a[0]));
        }));

        cls.DefineMethod("==", RubyMethodInfo.FromBuiltin("==", cls, (ctx, s, a, _) =>
        {
            if (a[0] is not RubyObject o || !ReferenceEquals(o.RubyClass, ((RubyObject)s!).RubyClass)) return false;
            foreach (var m in members)
                if (!RubyValueComparer.Instance.RubyEql(((RubyObject)s!).GetIvar("@" + m), o.GetIvar("@" + m))) return false;
            return true;
        }));

        cls.DefineMethod("inspect", RubyMethodInfo.FromBuiltin("inspect", cls, (ctx, s, _, _) =>
        {
            var o = (RubyObject)s!;
            var parts = members.Select(m => $"{m}={ctx.Inspect(o.GetIvar("@" + m))}");
            var name = o.RubyClass.Name is { } n ? " " + n : "";
            return new MutableString($"#<{kind}{name} {string.Join(", ", parts)}>");
        }));
        cls.DefineMethod("to_s", cls.GetOwnMethod("inspect")!.CloneAs("to_s", cls));

        // with(**changes) for Data immutability
        cls.DefineMethod("with", RubyMethodInfo.FromBuiltin("with", cls, (ctx, s, a, _) =>
        {
            var copy = (RubyObject)cls.Allocate(ctx);
            var src = (RubyObject)s!;
            foreach (var m in members) copy.SetIvar("@" + m, src.GetIvar("@" + m));
            if (a.Length > 0 && a[0] is RubyHash h)
                foreach (var e in h.Entries()) copy.SetIvar("@" + KernelOps.NameOf(e.Key), e.Value);
            return copy;
        }));
    }
}

[RubyClass("Data")]
public static class DataOps
{
    [RubyMethod("define", Kind = RubyMethodKind.Static)]
    public static object? Define(RubyContext c, object? self, object?[] args, RubyProc? block)
    {
        var super = c.ObjectClass.TryGetConstant("Data", out var dc) ? (RubyClass)dc! : c.ObjectClass;
        var cls = StructOps.Build(c, args, super, "data");

        // Data supports keyword construction too; reuse positional initialize but
        // also accept a single keyword hash.
        var members = args.Where(a => a is RubySymbol or MutableString).Select(KernelOps.NameOf).ToList();
        cls.DefineMethod("initialize", RubyMethodInfo.FromBuiltin("initialize", cls, (ctx, s, a, _) =>
        {
            var o = (RubyObject)s!;
            if (a.Length == 1 && a[0] is RubyHash h)
                foreach (var m in members) o.SetIvar("@" + m, h.GetOrNull(ctx.Intern(m)));
            else
                for (int i = 0; i < members.Count; i++) o.SetIvar("@" + members[i], i < a.Length ? a[i] : null);
            return null;
        }));
        return cls;
    }
}
