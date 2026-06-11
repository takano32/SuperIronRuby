using System.Numerics;
using SuperIronRuby.Interpreter;
using SuperIronRuby.Parser;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Interpreter;

// Task I1: top-level execution of literals, statement sequences, and locals.
public class SkeletonTests
{
    private static object? Run(string source)
    {
        var ctx = new RubyContext();
        var interp = new SuperIronRuby.Interpreter.Interpreter(ctx);
        return interp.Run(PrismParser.Parse(source));
    }

    [Fact]
    public void IntegerLiteral() => Assert.Equal(42L, Run("42"));

    [Fact]
    public void BignumLiteral()
        => Assert.Equal(BigInteger.Parse("100000000000000000000"), Run("100000000000000000000"));

    [Fact]
    public void FloatLiteral() => Assert.Equal(3.5, Run("3.5"));

    [Fact]
    public void TrueFalseNil()
    {
        Assert.Equal(true, Run("true"));
        Assert.Equal(false, Run("false"));
        Assert.Null(Run("nil"));
    }

    [Fact]
    public void StringLiteral()
    {
        var v = Assert.IsType<MutableString>(Run("\"ab\""));
        Assert.Equal("ab", v.Value);
    }

    [Fact]
    public void SymbolLiteral()
    {
        var v = Assert.IsType<RubySymbol>(Run(":sym"));
        Assert.Equal("sym", v.Name);
    }

    [Fact]
    public void LastStatementWins() => Assert.Equal(2L, Run("1; 2"));

    [Fact]
    public void EmptyProgramIsNil() => Assert.Null(Run(""));

    [Fact]
    public void LocalAssignmentAndRead() => Assert.Equal(5L, Run("x = 5; x"));

    [Fact]
    public void LocalReassignment() => Assert.Equal(7L, Run("x = 1; x = 7; x"));

    [Fact]
    public void Parentheses() => Assert.Equal(3L, Run("(1; 3)"));

    [Fact]
    public void SelfAtTopLevelIsMainObject()
    {
        var ctx = new RubyContext();
        var interp = new SuperIronRuby.Interpreter.Interpreter(ctx);
        Assert.Same(ctx.MainObject, interp.Run(PrismParser.Parse("self")));
    }

    [Fact]
    public void FrozenStringLiteralIsFrozen()
    {
        var v = Assert.IsType<MutableString>(Run("# frozen_string_literal: true\n\"hi\""));
        Assert.True(v.IsFrozen);
    }
}
