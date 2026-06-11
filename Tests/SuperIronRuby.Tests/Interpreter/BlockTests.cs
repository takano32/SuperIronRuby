using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Interpreter;

// Task I8: blocks, lambdas, numbered params, `it`, closures.
public class BlockTests
{
    private static object? Run(string src) => TestRuntime.Run(src);

    [Fact]
    public void ClosureCapturesAndMutatesOuterLocal()
    {
        var src = "c = 0; p = proc { c = c + 1 }; p.call; p.call; c";
        Assert.Equal(2L, Run(src));
    }

    [Fact]
    public void LambdaCallReturnsValue()
        => Assert.Equal(3L, Run("add = ->(a, b) { a + b }; add.call(1, 2)"));

    [Fact]
    public void LambdaWithBrackets()
        => Assert.Equal(6L, Run("dbl = ->(x) { x + x }; dbl[3]"));

    [Fact]
    public void ProcAutoSplatsArray()
    {
        var src = "p = proc { |a, b| a + b }; p.call([3, 4])";
        Assert.Equal(7L, Run(src));
    }

    [Fact]
    public void LambdaStrictArityRaises()
    {
        var ex = Assert.Throws<RubyRaiseException>(() => Run("l = ->(a, b) { a }; l.call(1)"));
        Assert.Contains("wrong number of arguments", ex.RubyException.Message);
    }

    [Fact]
    public void NumberedParameter()
        => Assert.Equal(6L, Run("m = proc { _1 * 2 }; m.call(3)"));

    [Fact]
    public void TwoNumberedParameters()
        => Assert.Equal(7L, Run("m = proc { _1 + _2 }; m.call(3, 4)"));

    [Fact]
    public void ItParameter()
        => Assert.Equal(9L, Run("p2 = proc { it }; p2.call(9)"));

    [Fact]
    public void BlockGivenTrue()
        => Assert.Equal(true, Run("def f; block_given?; end; f { }"));

    [Fact]
    public void BlockGivenFalse()
        => Assert.Equal(false, Run("def f; block_given?; end; f"));

    [Fact]
    public void BreakFromBlockReturnsToCall()
        => Assert.Equal(7L, Run("def f; yield; 99; end; f { break 7 }"));

    [Fact]
    public void LambdaReturnReturnsFromLambda()
        => Assert.Equal(5L, Run("l = -> { return 5 }; l.call"));

    [Fact]
    public void NextInBlockYieldsValue()
    {
        // collect via each-like: sum using a proc that nexts
        var src = "p = proc { |x| next 0 if x == 2; x }; p.call(2)";
        Assert.Equal(0L, Run(src));
    }

    [Fact]
    public void ProcArity()
    {
        Assert.Equal(2L, Run("->(a, b) { }.arity"));
    }

    [Fact]
    public void LambdaPredicate()
    {
        Assert.Equal(true, Run("(-> { }).lambda?"));
        Assert.Equal(false, Run("proc { }.lambda?"));
    }

    [Fact]
    public void SymbolToProc()
    {
        var src = "up = :upcase.to_proc; up.call(\"hi\")";
        var v = Assert.IsType<MutableString>(Run(src));
        Assert.Equal("HI", v.Value);
    }
}
