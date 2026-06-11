using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Interpreter;

// Task I9: case/in pattern matching.
public class PatternTests
{
    private static object? Run(string src) => TestRuntime.Run(src);

    [Fact]
    public void ValuePattern()
        => Assert.Equal("two", Assert.IsType<RubySymbol>(Run("case 2; in 1; :one; in 2; :two; end")).Name);

    [Fact]
    public void ClassPattern()
        => Assert.Equal("int", Assert.IsType<RubySymbol>(Run("case 5; in String; :str; in Integer; :int; end")).Name);

    [Fact]
    public void ArrayPatternBindsElements()
        => Assert.Equal(3L, Run("case [1, 2]; in [a, b]; a + b; end"));

    [Fact]
    public void ArrayPatternWithRest()
    {
        var v = Assert.IsType<RubyArray>(Run("case [1, 2, 3]; in [1, *rest]; rest; end"));
        Assert.Equal(new object?[] { 2L, 3L }, v);
    }

    [Fact]
    public void ArrayPatternLengthMismatchFallsThrough()
        => Assert.Equal("no", Assert.IsType<RubySymbol>(Run("case [1, 2, 3]; in [a, b]; :yes; else; :no; end")).Name);

    [Fact]
    public void HashPatternShorthandBinding()
    {
        var v = Assert.IsType<MutableString>(Run("case {name: \"Alice\"}; in {name: String => n}; n; end"));
        Assert.Equal("Alice", v.Value);
    }

    [Fact]
    public void HashPatternKeyShorthand()
        => Assert.Equal(30L, Run("case {age: 30}; in {age:}; age; end"));

    [Fact]
    public void HashPatternMissingKeyFallsThrough()
        => Assert.Equal("no", Assert.IsType<RubySymbol>(Run("case {a: 1}; in {b:}; :yes; else; :no; end")).Name);

    [Fact]
    public void GuardCondition()
        => Assert.Equal("big", Assert.IsType<RubySymbol>(Run("case 5; in Integer => x if x > 3; :big; in Integer; :small; end")).Name);

    [Fact]
    public void GuardFailsFallsToNext()
        => Assert.Equal("small", Assert.IsType<RubySymbol>(Run("case 2; in Integer => x if x > 3; :big; in Integer; :small; end")).Name);

    [Fact]
    public void AlternationPattern()
        => Assert.Equal("yes", Assert.IsType<RubySymbol>(Run("case 2; in 1 | 2 | 3; :yes; else; :no; end")).Name);

    [Fact]
    public void PinnedValue()
        => Assert.Equal("pinned", Assert.IsType<RubySymbol>(Run("case 1.0; in ^(1); :pinned; else; :no; end")).Name);

    [Fact]
    public void CaptureBinding()
        => Assert.Equal(7L, Run("case 7; in Integer => n; n; end"));

    [Fact]
    public void MatchPredicateBoolean()
    {
        Assert.Equal(true, Run("[1, 2] in [a, b]"));
        Assert.Equal(false, Run("[1, 2, 3] in [a, b]"));
    }

    [Fact]
    public void NoMatchRaises()
    {
        var ex = Assert.Throws<RubyRaiseException>(() => Run("case 5; in String; :s; end"));
        Assert.Equal("NoMatchingPatternError", ex.RubyException.RubyClass.Name);
    }

    [Fact]
    public void NestedArrayPattern()
        => Assert.Equal(true, Run("case [1, [2, 3]]; in [a, [b, c]]; a == 1 && b == 2 && c == 3; end"));

    [Fact]
    public void FindPattern()
    {
        var v = Run("case [1, 2, 3, 4]; in [*, 2, 3, *]; :found; else; :no; end");
        Assert.Equal("found", Assert.IsType<RubySymbol>(v).Name);
    }
}
