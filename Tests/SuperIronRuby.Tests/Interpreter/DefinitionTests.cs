using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Interpreter;

// Task I3: method definition, parameter binding, return, yield, super.
public class DefinitionTests
{
    private static object? Run(string src) => TestRuntime.Run(src);

    [Fact]
    public void DefAndCallSimple() => Assert.Equal(3L, Run("def f(a, b); a + b; end; f(1, 2)"));

    [Fact]
    public void DefReturnsNameSymbol()
    {
        var v = Assert.IsType<RubySymbol>(Run("def foo; end"));
        Assert.Equal("foo", v.Name);
    }

    [Fact]
    public void DefaultArgument() => Assert.Equal(3L, Run("def f(a, b = 2); a + b; end; f(1)"));

    [Fact]
    public void DefaultArgumentOverridden() => Assert.Equal(5L, Run("def f(a, b = 2); a + b; end; f(1, 4)"));

    [Fact]
    public void RestParameter()
    {
        var v = Assert.IsType<RubyArray>(Run("def f(*xs); xs; end; f(1, 2, 3)"));
        Assert.Equal(new object?[] { 1L, 2L, 3L }, v);
    }

    [Fact]
    public void RequiredKeyword() => Assert.Equal(7L, Run("def f(a:, b:); a + b; end; f(a: 3, b: 4)"));

    [Fact]
    public void OptionalKeyword() => Assert.Equal(5L, Run("def f(a, k: 4); a + k; end; f(1)"));

    [Fact]
    public void MissingRequiredKeywordRaises()
    {
        var ex = Assert.Throws<RubyRaiseException>(() => Run("def f(a:); a; end; f"));
        Assert.Equal("missing keyword: :a", ex.RubyException.Message);
    }

    [Fact]
    public void ArityErrorTooFew()
    {
        var ex = Assert.Throws<RubyRaiseException>(() => Run("def f(a, b); a; end; f(1)"));
        Assert.Equal("wrong number of arguments (given 1, expected 2)", ex.RubyException.Message);
    }

    [Fact]
    public void ExplicitReturn() => Assert.Equal(9L, Run("def f; return 9; 10; end; f"));

    [Fact]
    public void EndlessDef() => Assert.Equal(8L, Run("def double(x) = x + x; double(4)"));

    [Fact]
    public void Yield() => Assert.Equal(10L, Run("def f; yield 5; end; f { |x| x + x }"));

    [Fact]
    public void YieldWithoutBlockRaises()
    {
        var ex = Assert.Throws<RubyRaiseException>(() => Run("def f; yield; end; f"));
        Assert.Equal("no block given (yield)", ex.RubyException.Message);
    }

    [Fact]
    public void NestedYieldAndReturn() => Assert.Equal(2L, Run("def f; yield; end; f { return 2 }; 99"));

    // Class/constant-dependent def tests (instance methods, super, singleton via
    // a constant receiver) are covered in task I4 once ConstantReadNode/ClassNode
    // evaluation exists.
}
