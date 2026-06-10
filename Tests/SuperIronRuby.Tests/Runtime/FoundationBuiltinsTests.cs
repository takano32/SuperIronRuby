using SuperIronRuby.Runtime;
using SuperIronRuby.Builtins;

namespace SuperIronRuby.Tests.Runtime;

// Foundation builtins (R7), exercised through ctx.Send. Output formats checked
// against ruby 4.0.2.
public class FoundationBuiltinsTests
{
    private static RubyContext Loaded()
    {
        var ctx = new RubyContext();
        BuiltinLoader.LoadAssembly(ctx, typeof(KernelOps).Assembly);
        return ctx;
    }

    private static object?[] A(params object?[] xs) => xs;

    [Fact]
    public void IsA_AcrossHierarchy()
    {
        var ctx = Loaded();
        Assert.Equal(true, ctx.Send(1L, "is_a?", A(ctx.IntegerClass)));
        Assert.Equal(true, ctx.Send(1L, "kind_of?", A(ctx.NumericClass)));
        Assert.Equal(false, ctx.Send(1L, "is_a?", A(ctx.StringClass)));
        Assert.Equal(true, ctx.Send(1L, "instance_of?", A(ctx.IntegerClass)));
        Assert.Equal(false, ctx.Send(1L, "instance_of?", A(ctx.NumericClass)));
    }

    [Fact]
    public void FrozenAndNil()
    {
        var ctx = Loaded();
        Assert.Equal(true, ctx.Send(1L, "frozen?", A()));          // ruby: 1.frozen? == true
        Assert.Equal(true, ctx.Send(ctx.Intern("s"), "frozen?", A()));
        Assert.Equal(false, ctx.Send(new MutableString("x"), "frozen?", A()));
        Assert.Equal(true, ctx.Send(null, "nil?", A()));
        Assert.Equal(false, ctx.Send(1L, "nil?", A()));
    }

    [Fact]
    public void NilToSAndInspect()
    {
        var ctx = Loaded();
        Assert.Equal("", ((MutableString)ctx.Send(null, "to_s", A())!).Value);
        Assert.Equal("nil", ((MutableString)ctx.Send(null, "inspect", A())!).Value);
        Assert.Empty((RubyArray)ctx.Send(null, "to_a", A())!);
        Assert.Equal(0L, ctx.Send(null, "to_i", A()));
    }

    [Fact]
    public void BooleanLogicOps()
    {
        var ctx = Loaded();
        Assert.Equal(false, ctx.Send(true, "&", A(false))); // true & false
        Assert.Equal(true, ctx.Send(true, "|", A(false)));  // true | false
        Assert.Equal(false, ctx.Send(true, "^", A(true)));  // true ^ true
        Assert.Equal(false, ctx.Send(null, "&", A(true)));  // nil & true
    }

    [Fact]
    public void AttrAccessor_RoundTrip()
    {
        var ctx = Loaded();
        var c = ctx.DefineClass("Point", ctx.ObjectClass);
        ctx.Send(c, "attr_accessor", A(ctx.Intern("x"), ctx.Intern("y")));
        var obj = ctx.Send(c, "new", A());
        ctx.Send(obj, "x=", A(10L));
        ctx.Send(obj, "y=", A(20L));
        Assert.Equal(10L, ctx.Send(obj, "x", A()));
        Assert.Equal(20L, ctx.Send(obj, "y", A()));
    }

    [Fact]
    public void ConstGetSet()
    {
        var ctx = Loaded();
        var c = ctx.DefineClass("Holder", ctx.ObjectClass);
        ctx.Send(c, "const_set", A(ctx.Intern("FOO"), 99L));
        Assert.Equal(true, ctx.Send(c, "const_defined?", A(ctx.Intern("FOO"))));
        Assert.Equal(99L, ctx.Send(c, "const_get", A(ctx.Intern("FOO"))));
        var ex = Assert.Throws<RubyRaiseException>(() => ctx.Send(c, "const_get", A(ctx.Intern("BAR"))));
        Assert.Equal("uninitialized constant BAR", ex.RubyException.Message);
    }

    [Fact]
    public void ClassNew_CallsInitialize()
    {
        var ctx = Loaded();
        var c = ctx.DefineClass("Counter", ctx.ObjectClass);
        c.DefineMethod("initialize", RubyMethodInfo.FromBuiltin("initialize", c,
            (_, self, a, _) => { ((RubyObject)self!).SetIvar("@n", a[0]); return null; }));
        ctx.Send(c, "attr_reader", A(ctx.Intern("n")));
        var obj = ctx.Send(c, "new", A(5L));
        Assert.Equal(5L, ctx.Send(obj, "n", A()));
    }

    [Fact]
    public void DefaultInspect_ListsInstanceVariables()
    {
        var ctx = Loaded();
        var c = ctx.DefineClass("Vec", ctx.ObjectClass);
        var obj = (RubyObject)((RubyClass)c).Allocate(ctx);
        // Use values whose #inspect exists at the R7 layer (Integer#inspect is B2).
        obj.SetIvar("@x", null);
        obj.SetIvar("@y", true);
        var inspected = ((MutableString)ctx.Send(obj, "inspect", A())!).Value;
        Assert.Matches(@"^#<Vec:0x[0-9a-f]+ @x=nil, @y=true>$", inspected);
        var s = ((MutableString)ctx.Send(obj, "to_s", A())!).Value;
        Assert.Matches(@"^#<Vec:0x[0-9a-f]+>$", s);
    }

    [Fact]
    public void DupArrayAndObject()
    {
        var ctx = Loaded();
        var arr = new RubyArray { 1L, 2L };
        var dup = (RubyArray)ctx.Send(arr, "dup", A())!;
        Assert.NotSame(arr, dup);
        Assert.Equal(new object?[] { 1L, 2L }, dup);

        var c = ctx.DefineClass("Dupable", ctx.ObjectClass);
        var o = (RubyObject)((RubyClass)c).Allocate(ctx);
        o.SetIvar("@v", 7L);
        var od = (RubyObject)ctx.Send(o, "dup", A())!;
        Assert.NotSame(o, od);
        Assert.Equal(7L, od.GetIvar("@v"));
    }

    [Fact]
    public void RespondToAndSend()
    {
        var ctx = Loaded();
        Assert.Equal(true, ctx.Send(1L, "respond_to?", A(ctx.Intern("class"))));
        Assert.Equal(false, ctx.Send(1L, "respond_to?", A(ctx.Intern("nonexistent_xyz"))));
        Assert.Same(ctx.IntegerClass, ctx.Send(1L, "send", A(ctx.Intern("class"))));
    }

    [Fact]
    public void AncestorsReturnsModules()
    {
        var ctx = Loaded();
        var anc = (RubyArray)ctx.Send(ctx.IntegerClass, "ancestors", A())!;
        var names = anc.Select(m => ((RubyModule)m!).Name).ToArray();
        Assert.Equal(new[] { "Integer", "Numeric", "Comparable", "Object", "Kernel", "BasicObject" }, names);
    }
}
