using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Runtime;

// A custom CLR type routed to a Ruby class via [RubyClass(Extends=...)].
public sealed class Widget
{
    public int Size;
    public Widget(int size) => Size = size;
}

[RubyClass("Widget", Extends = typeof(Widget))]
public static class WidgetOps
{
    [RubyMethod("size")]
    public static object? Size(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => (long)((Widget)self!).Size;

    [RubyMethod("double_size")]
    [RubyMethod("dbl")] // second [RubyMethod] -> same body bound under two names
    public static object? DoubleSize(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => (long)((Widget)self!).Size * 2;

    [RubyMethod("make", Kind = RubyMethodKind.Static)]
    public static object? Make(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => new Widget((int)(long)args[0]!);
}

[RubyModule("Gadgetry")]
public static class GadgetryOps
{
    [RubyMethod("version", Kind = RubyMethodKind.ModuleFunction)]
    public static object? Version(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => new MutableString("1.0");
}

public class BuiltinLoaderTests
{
    private static RubyContext Loaded()
    {
        var ctx = new RubyContext();
        BuiltinLoader.LoadAssembly(ctx, typeof(WidgetOps).Assembly);
        return ctx;
    }

    [Fact]
    public void InstanceMethod_DispatchesOnExtendedClrType()
    {
        var ctx = Loaded();
        var w = new Widget(21);
        Assert.Equal(21L, ctx.Send(w, "size", System.Array.Empty<object?>()));
        Assert.Equal(42L, ctx.Send(w, "dbl", System.Array.Empty<object?>()));      // second [RubyMethod] name
        Assert.Equal(42L, ctx.Send(w, "double_size", System.Array.Empty<object?>()));
    }

    [Fact]
    public void Extends_RoutesGetClassOf()
    {
        var ctx = Loaded();
        Assert.True(ctx.ObjectClass.TryGetConstant("Widget", out var wc));
        Assert.Same(wc, ctx.GetClassOf(new Widget(1)));
    }

    [Fact]
    public void StaticMethod_DefinedOnSingleton()
    {
        var ctx = Loaded();
        Assert.True(ctx.ObjectClass.TryGetConstant("Widget", out var wc));
        var made = ctx.Send(wc, "make", new object?[] { 7L });
        Assert.IsType<Widget>(made);
        Assert.Equal(7, ((Widget)made!).Size);
    }

    [Fact]
    public void ModuleFunction_PublicSingletonAndPrivateInstance()
    {
        var ctx = Loaded();
        Assert.True(ctx.ObjectClass.TryGetConstant("Gadgetry", out var g));
        var mod = Assert.IsType<RubyModule>(g);

        // public on the module's singleton (Gadgetry.version)
        var v = ctx.Send(mod, "version", System.Array.Empty<object?>());
        Assert.Equal("1.0", ((MutableString)v!).Value);

        // private as an instance method
        var instance = mod.LookupMethod("version");
        Assert.NotNull(instance);
        Assert.Equal(RubyMethodVisibility.Private, instance!.Visibility);
    }
}
