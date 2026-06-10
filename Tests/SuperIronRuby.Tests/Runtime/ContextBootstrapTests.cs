using System.Numerics;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Runtime;

// Core-hierarchy bootstrap, verified against ruby 4.0.2 (see comments).
public class ContextBootstrapTests
{
    private static string[] Names(IReadOnlyList<RubyModule> mods)
        => mods.Select(m => m.Name!).ToArray();

    [Fact]
    public void CoreHierarchyShape()
    {
        var ctx = new RubyContext();
        Assert.Null(ctx.BasicObjectClass.Superclass);                 // ruby: BasicObject.superclass == nil
        Assert.Same(ctx.BasicObjectClass, ctx.ObjectClass.Superclass);
        Assert.Same(ctx.ObjectClass, ctx.ModuleClass.Superclass);     // ruby: Module.superclass == Object
        Assert.Same(ctx.ModuleClass, ctx.ClassClass.Superclass);      // ruby: Class.superclass == Module
    }

    [Fact]
    public void ObjectAncestorsIncludeKernel()
    {
        // ruby: Object.ancestors == [Object, Kernel, BasicObject]
        var ctx = new RubyContext();
        Assert.Equal(new[] { "Object", "Kernel", "BasicObject" }, Names(ctx.ObjectClass.Ancestors));
    }

    [Fact]
    public void ClassAndModuleAncestors()
    {
        // ruby: Class.ancestors == [Class, Module, Object, Kernel, BasicObject]
        var ctx = new RubyContext();
        Assert.Equal(new[] { "Class", "Module", "Object", "Kernel", "BasicObject" },
            Names(ctx.ClassClass.Ancestors));
    }

    [Fact]
    public void IntegerAncestors()
    {
        // ruby: Integer.ancestors == [Integer, Numeric, Comparable, Object, Kernel, BasicObject]
        var ctx = new RubyContext();
        Assert.Equal(new[] { "Integer", "Numeric", "Comparable", "Object", "Kernel", "BasicObject" },
            Names(ctx.IntegerClass.Ancestors));
    }

    [Theory]
    [InlineData(null, "NilClass")]
    [InlineData(true, "TrueClass")]
    [InlineData(false, "FalseClass")]
    public void GetClassOf_Singletons(object? value, string expected)
    {
        var ctx = new RubyContext();
        Assert.Equal(expected, ctx.GetClassOf(value).Name);
    }

    [Fact]
    public void GetClassOf_ValueKinds()
    {
        var ctx = new RubyContext();
        Assert.Same(ctx.IntegerClass, ctx.GetClassOf(42L));
        Assert.Same(ctx.IntegerClass, ctx.GetClassOf(new BigInteger(1) << 100));
        Assert.Same(ctx.FloatClass, ctx.GetClassOf(1.5));
        Assert.Same(ctx.StringClass, ctx.GetClassOf(new MutableString("x")));
        Assert.Same(ctx.SymbolClass, ctx.GetClassOf(ctx.Intern("s")));
        Assert.Same(ctx.ArrayClass, ctx.GetClassOf(new RubyArray()));
        Assert.Same(ctx.HashClass, ctx.GetClassOf(new RubyHash()));
        Assert.Same(ctx.RangeClass, ctx.GetClassOf(new RubyRange(1L, 2L, false)));
        Assert.Same(ctx.ClassClass, ctx.GetClassOf(ctx.ObjectClass));
        Assert.Same(ctx.ModuleClass, ctx.GetClassOf(ctx.KernelModule));
    }

    [Fact]
    public void Intern_ReturnsIdenticalSymbols()
    {
        var ctx = new RubyContext();
        Assert.Same(ctx.Intern("foo"), ctx.Intern("foo"));
        Assert.NotSame(ctx.Intern("foo"), ctx.Intern("bar"));
    }

    [Fact]
    public void Globals_DefaultNilThenSettable()
    {
        var ctx = new RubyContext();
        Assert.Null(ctx.GetGlobal("$undefined"));
        ctx.SetGlobal("$x", 7L);
        Assert.Equal(7L, ctx.GetGlobal("$x"));
    }

    [Fact]
    public void DefineClass_ReopenReturnsSameClass()
    {
        var ctx = new RubyContext();
        var a = ctx.DefineClass("Reopen", ctx.ObjectClass);
        var b = ctx.DefineClass("Reopen", ctx.ObjectClass);
        Assert.Same(a, b);
    }

    [Fact]
    public void SingletonClassOf_Object_IsLazyAndStable()
    {
        var ctx = new RubyContext();
        var obj = new RubyObject(ctx.ObjectClass);
        Assert.Null(obj.SingletonClass);
        var s1 = ctx.SingletonClassOf(obj);
        var s2 = ctx.SingletonClassOf(obj);
        Assert.Same(s1, s2);
        Assert.True(s1.IsSingletonClass);
        Assert.Same(obj, s1.AttachedObject);
        // method lookup uses the singleton class once it exists
        Assert.Same(s1, ctx.GetImmediateClassOf(obj));
    }

    [Fact]
    public void IsKindOf_WalksAncestors()
    {
        var ctx = new RubyContext();
        Assert.True(ctx.IsKindOf(42L, ctx.IntegerClass));
        Assert.True(ctx.IsKindOf(42L, ctx.NumericClass));
        Assert.True(ctx.IsKindOf(42L, ctx.ComparableModule));
        Assert.True(ctx.IsKindOf(42L, ctx.ObjectClass));
        Assert.False(ctx.IsKindOf(42L, ctx.StringClass));
    }
}
