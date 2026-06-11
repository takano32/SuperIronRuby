using System.Numerics;
using SuperIronRuby.Interpreter;
using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;
using SuperIronRuby.Builtins;

namespace SuperIronRuby.Tests.Interpreter;

/// <summary>
/// Shared helper for interpreter tests: builds a context with the foundation
/// builtins loaded plus a few numeric/string operators that the full builtin
/// library (tasks B2/B3) will provide later, so interpreter behavior can be
/// tested before those land.
/// </summary>
internal static class TestRuntime
{
    public static (RubyContext ctx, SuperIronRuby.Interpreter.Interpreter interp) Create()
    {
        var ctx = new RubyContext();
        BuiltinLoader.LoadAssembly(ctx, typeof(KernelOps).Assembly);
        InstallNumericOps(ctx);
        var interp = new SuperIronRuby.Interpreter.Interpreter(ctx);
        return (ctx, interp);
    }

    public static object? Run(string source)
    {
        var (_, interp) = Create();
        return interp.Run(PrismParser.Parse(source));
    }

    private static void InstallNumericOps(RubyContext ctx)
    {
        void Def(RubyClass cls, string name, BuiltinMethodBody body, int min = 1, int max = 1)
            => cls.DefineMethod(name, RubyMethodInfo.FromBuiltin(name, cls, body, arityMin: min, arityMax: max));

        static double ToD(object? o) => o switch
        {
            long l => l, double d => d, BigInteger b => (double)b, _ => 0,
        };
        static bool IsInt(object? o) => o is long or BigInteger;

        Def(ctx.IntegerClass, "+", (_, s, a, _) => Add(s, a[0]));
        Def(ctx.IntegerClass, "-", (_, s, a, _) => (long)(long)s! - (long)a[0]!);
        Def(ctx.IntegerClass, "*", (_, s, a, _) => (long)(long)s! * (long)a[0]!);
        Def(ctx.IntegerClass, "<", (_, s, a, _) => ToD(s) < ToD(a[0]));
        Def(ctx.IntegerClass, ">", (_, s, a, _) => ToD(s) > ToD(a[0]));
        Def(ctx.IntegerClass, "<=", (_, s, a, _) => ToD(s) <= ToD(a[0]));
        Def(ctx.IntegerClass, ">=", (_, s, a, _) => ToD(s) >= ToD(a[0]));
        Def(ctx.IntegerClass, "==", (_, s, a, _) => IsInt(a[0]) && ToD(s) == ToD(a[0]));
        Def(ctx.IntegerClass, "to_s", (_, s, _, _) => new MutableString(s!.ToString()!), 0, 0);
        Def(ctx.FloatClass, "+", (_, s, a, _) => (double)s! + ToD(a[0]));
        Def(ctx.FloatClass, "to_s", (_, s, _, _) => new MutableString(((double)s!).ToString()), 0, 0);

        // String#+ and #length for interpolation/call tests.
        Def(ctx.StringClass, "+", (_, s, a, _) =>
            new MutableString(((MutableString)s!).Value + ((MutableString)a[0]!).Value));
        Def(ctx.StringClass, "length", (_, s, _, _) => (long)((MutableString)s!).Value.Length, 0, 0);
        Def(ctx.StringClass, "upcase", (_, s, _, _) =>
            new MutableString(((MutableString)s!).Value.ToUpperInvariant()), 0, 0);
    }

    private static object Add(object? a, object? b)
    {
        if (a is long la && b is long lb)
        {
            long r = unchecked(la + lb);
            // overflow check
            if (((la ^ r) & (lb ^ r)) < 0) return (BigInteger)la + lb;
            return r;
        }
        return (BigInteger)Convert.ToInt64(a) + Convert.ToInt64(b);
    }
}
