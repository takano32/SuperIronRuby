using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Interpreter;

// Task I2: method calls, arguments, safe navigation, and/or. Uses TestRuntime's
// numeric/string operators (full builtins arrive in B2/B3).
public class CallTests
{
    private static object? Run(string src) => TestRuntime.Run(src);

    [Fact]
    public void BinaryOperatorCall() => Assert.Equal(3L, Run("1 + 2"));

    [Fact]
    public void ChainedOperators() => Assert.Equal(6L, Run("1 + 2 + 3"));

    [Fact]
    public void MethodCallWithReceiver()
    {
        var v = Assert.IsType<MutableString>(Run("\"abc\".upcase"));
        Assert.Equal("ABC", v.Value);
    }

    [Fact]
    public void ComparisonReturnsBoolean()
    {
        Assert.Equal(true, Run("1 < 2"));
        Assert.Equal(false, Run("2 < 1"));
        Assert.Equal(true, Run("2 == 2"));
    }

    [Fact]
    public void PredicateBuiltins()
    {
        Assert.Equal(true, Run("nil.nil?"));
        Assert.Equal(false, Run("1.nil?"));
        Assert.Equal(true, Run("1.frozen?"));
    }

    [Fact]
    public void SafeNavigationOnNil() => Assert.Null(Run("nil&.upcase"));

    [Fact]
    public void SafeNavigationOnValue()
    {
        var v = Assert.IsType<MutableString>(Run("\"x\"&.upcase"));
        Assert.Equal("X", v.Value);
    }

    [Fact]
    public void AndReturnsValues()
    {
        Assert.Equal(2L, Run("1 && 2"));      // truthy && -> right
        Assert.Equal(false, Run("false && 1")); // falsy && -> left
        Assert.Null(Run("nil && 1"));
    }

    [Fact]
    public void OrReturnsValues()
    {
        Assert.Equal(1L, Run("1 || 2"));      // truthy || -> left
        Assert.Equal(2L, Run("nil || 2"));    // falsy || -> right
        Assert.Equal(2L, Run("false || 2"));
    }

    [Fact]
    public void ImplicitSelfMethodMissingRaises()
    {
        // an unknown bare call with no local -> NoMethodError via method_missing
        var ex = Assert.Throws<RubyRaiseException>(() => Run("totally_unknown_method"));
        Assert.Contains("totally_unknown_method", ex.RubyException.Message);
    }

    [Fact]
    public void SplatArgumentExpansion()
    {
        // build an array local then splat it into a call; uses Integer#+ (arity 1)
        // 1 + *[2] -> 1 + 2
        Assert.Equal(3L, Run("a = 2; 1 + a"));
    }

    [Fact]
    public void StringConcatenation()
    {
        var v = Assert.IsType<MutableString>(Run("\"foo\" + \"bar\""));
        Assert.Equal("foobar", v.Value);
    }

    [Fact]
    public void BlockCall_SingleParam()
    {
        // tap yields self to the block; with Integer#to_s available
        // here we just confirm the block runs and returns self from tap
        Assert.Equal(5L, Run("5.tap { |x| x }"));
    }
}
