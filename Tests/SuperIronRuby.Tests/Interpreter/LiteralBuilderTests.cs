using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Interpreter;

// Task I7: interpolation, array/hash/range/regexp literals.
public class LiteralBuilderTests
{
    private static object? Run(string src) => TestRuntime.Run(src);

    [Fact]
    public void StringInterpolation()
    {
        var v = Assert.IsType<MutableString>(Run("x = 5; \"v=#{x}\""));
        Assert.Equal("v=5", v.Value);
    }

    [Fact]
    public void InterpolationWithExpression()
    {
        var v = Assert.IsType<MutableString>(Run("\"a#{1 + 1}b\""));
        Assert.Equal("a2b", v.Value);
    }

    [Fact]
    public void SymbolInterpolation()
    {
        var v = Assert.IsType<RubySymbol>(Run("x = 1; :\"s#{x}\""));
        Assert.Equal("s1", v.Name);
    }

    [Fact]
    public void ArrayLiteral()
    {
        var v = Assert.IsType<RubyArray>(Run("[1, 2, 3]"));
        Assert.Equal(new object?[] { 1L, 2L, 3L }, v);
    }

    [Fact]
    public void ArrayWithSplat()
    {
        var v = Assert.IsType<RubyArray>(Run("a = [2, 3]; [1, *a, 4]"));
        Assert.Equal(new object?[] { 1L, 2L, 3L, 4L }, v);
    }

    [Fact]
    public void EmptyArray() => Assert.Empty(Assert.IsType<RubyArray>(Run("[]")));

    [Fact]
    public void HashLiteralSymbolKeys()
    {
        var h = Assert.IsType<RubyHash>(Run("{a: 1, b: 2}"));
        Assert.Equal(2, h.Count);
        var ctx = new RubyContext();
        Assert.Equal(new object?[] { ctx.Intern("a"), ctx.Intern("b") }.Select(s => ((RubySymbol)s!).Name),
                     h.Keys().Select(k => ((RubySymbol)k!).Name));
    }

    [Fact]
    public void HashLiteralRocketKeys()
    {
        var h = Assert.IsType<RubyHash>(Run("{1 => 10, 2 => 20}"));
        Assert.Equal(10L, h.GetOrNull(1L));
        Assert.Equal(20L, h.GetOrNull(2L));
    }

    [Fact]
    public void HashWithDoubleSplat()
    {
        var h = Assert.IsType<RubyHash>(Run("base = {a: 1}; {**base, b: 2}"));
        Assert.Equal(2, h.Count);
    }

    [Fact]
    public void RangeLiteral()
    {
        var r = Assert.IsType<RubyRange>(Run("1..5"));
        Assert.Equal(1L, r.Begin);
        Assert.Equal(5L, r.End);
        Assert.False(r.ExcludeEnd);
    }

    [Fact]
    public void ExclusiveRange()
    {
        var r = Assert.IsType<RubyRange>(Run("1...5"));
        Assert.True(r.ExcludeEnd);
    }

    [Fact]
    public void EndlessRange()
    {
        var r = Assert.IsType<RubyRange>(Run("1.."));
        Assert.Equal(1L, r.Begin);
        Assert.Null(r.End);
    }

    [Fact]
    public void RegexpLiteral()
    {
        var re = Assert.IsType<RubyRegexp>(Run("/ab+/"));
        Assert.Equal("ab+", re.Source);
    }

    [Fact]
    public void ForLoopOverRange()
    {
        var src = "sum = 0; for x in (1..3); sum = sum + x; end; sum";
        Assert.Equal(6L, Run(src));
    }

    [Fact]
    public void ArrayWithMixedTypes()
    {
        var v = Assert.IsType<RubyArray>(Run("[1, \"a\", :b, nil, true]"));
        Assert.Equal(5, v.Count);
        Assert.Equal(1L, v[0]);
        Assert.IsType<MutableString>(v[1]);
        Assert.IsType<RubySymbol>(v[2]);
        Assert.Null(v[3]);
        Assert.Equal(true, v[4]);
    }
}
