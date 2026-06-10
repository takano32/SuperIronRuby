using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Runtime;

// Exception hierarchy + raise helpers, verified against ruby 4.0.2.
public class ExceptionsTests
{
    private static string[] Names(IReadOnlyList<RubyModule> mods)
        => mods.Select(m => m.Name!).ToArray();

    [Theory]
    [InlineData("NoMethodError", "NameError")]
    [InlineData("NameError", "StandardError")]
    [InlineData("ArgumentError", "StandardError")]
    [InlineData("TypeError", "StandardError")]
    [InlineData("RuntimeError", "StandardError")]
    [InlineData("FrozenError", "RuntimeError")]
    [InlineData("ZeroDivisionError", "StandardError")]
    [InlineData("KeyError", "IndexError")]
    [InlineData("StopIteration", "IndexError")]
    [InlineData("FloatDomainError", "RangeError")]
    [InlineData("NotImplementedError", "ScriptError")]
    [InlineData("LoadError", "ScriptError")]
    [InlineData("ScriptError", "Exception")]
    [InlineData("StandardError", "Exception")]
    [InlineData("SystemExit", "Exception")]
    [InlineData("EOFError", "IOError")]
    [InlineData("NoMatchingPatternKeyError", "NoMatchingPatternError")]
    public void HierarchyParents(string child, string expectedParent)
    {
        var ctx = new RubyContext();
        Assert.True(ctx.ObjectClass.TryGetConstant(child, out var c));
        var cls = Assert.IsType<RubyClass>(c);
        Assert.Equal(expectedParent, cls.Superclass!.Name);
    }

    [Fact]
    public void StandardErrorAncestors()
    {
        // ruby: ArgumentError.ancestors.first(3) == [ArgumentError, StandardError, Exception]
        var ctx = new RubyContext();
        Assert.Equal(new[] { "ArgumentError", "StandardError", "Exception", "Object" },
            Names(ctx.ArgumentErrorClass.Ancestors).Take(4).ToArray());
    }

    [Fact]
    public void CreateException_SetsClassAndMessage()
    {
        var ctx = new RubyContext();
        var ex = ctx.CreateException(ctx.RuntimeErrorClass, "boom");
        Assert.Same(ctx.RuntimeErrorClass, ex.RubyClass);
        Assert.Equal("boom", ex.Message);
    }

    // The Raise* helpers build (and return) the RubyRaiseException so callers
    // write `throw ctx.RaiseX(...)`. Tests inspect the returned object directly.

    [Fact]
    public void RaiseError_CarriesRubyException()
    {
        var ctx = new RubyContext();
        var thrown = ctx.RaiseTypeError("nope");
        Assert.Same(ctx.TypeErrorClass, thrown.RubyException.RubyClass);
        Assert.Equal("nope", thrown.RubyException.Message);
    }

    [Fact]
    public void NoMethodError_MessageFormat()
    {
        // ruby: 1.foo => undefined method 'foo' for an instance of Integer
        var ctx = new RubyContext();
        Assert.Equal("undefined method 'foo' for an instance of Integer",
            ctx.RaiseNoMethodError(1L, "foo").RubyException.Message);
    }

    [Fact]
    public void NoMethodError_NilReceiver()
    {
        // ruby: nil.foo => undefined method 'foo' for nil
        var ctx = new RubyContext();
        Assert.Equal("undefined method 'foo' for nil",
            ctx.RaiseNoMethodError(null, "foo").RubyException.Message);
    }

    [Fact]
    public void PrivateMethodError_MessageFormat()
    {
        // ruby: private method 's' called for an instance of C
        var ctx = new RubyContext();
        var c = ctx.DefineClass("C", ctx.ObjectClass);
        var obj = new RubyObject(c);
        Assert.Equal("private method 's' called for an instance of C",
            ctx.RaisePrivateMethodError(obj, "s").RubyException.Message);
    }

    [Fact]
    public void ArityError_MessageFormat()
    {
        // ruby: wrong number of arguments (given 2, expected 1)
        var ctx = new RubyContext();
        Assert.Equal("wrong number of arguments (given 2, expected 1)",
            ctx.RaiseArityError(2, 1, 1).RubyException.Message);
    }

    [Fact]
    public void FrozenError_MessageFormat()
    {
        var ctx = new RubyContext();
        var ex = ctx.RaiseFrozenError("String", "\"x\"");
        Assert.Equal("can't modify frozen String: \"x\"", ex.RubyException.Message);
        Assert.Same(ctx.FrozenErrorClass, ex.RubyException.RubyClass);
    }

    [Fact]
    public void RaisedExceptionIsKindOfStandardError()
    {
        var ctx = new RubyContext();
        var ex = ctx.CreateException(ctx.ArgumentErrorClass, "x");
        Assert.True(ctx.IsKindOf(ex, ctx.StandardErrorClass));
        Assert.True(ctx.IsKindOf(ex, ctx.ExceptionClass));
    }
}
