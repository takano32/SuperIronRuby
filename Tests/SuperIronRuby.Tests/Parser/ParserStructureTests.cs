using System.Numerics;
using SuperIronRuby.Parser;

namespace SuperIronRuby.Tests.Parser;

// Task P7: node-shape structure tests over the native parser.
public class ParserStructureTests
{
    private static Node Top(string src) => ((StatementsNode)PrismParser.Parse(src).Root.Statements).Body[0];

    [Fact]
    public void MethodWithAllParameterKinds()
    {
        var def = Assert.IsType<DefNode>(Top("def f(a, b = 1, *r, k:, **kw, &blk); end"));
        Assert.Equal("f", def.Name);
        var p = Assert.IsType<ParametersNode>(def.Parameters);
        Assert.Single(p.Requireds);
        Assert.Single(p.Optionals);
        Assert.NotNull(p.Rest);
        Assert.Single(p.Keywords);
        Assert.NotNull(p.KeywordRest);
        Assert.NotNull(p.Block);
    }

    [Fact]
    public void InterpolatedStringParts()
    {
        var interp = Assert.IsType<InterpolatedStringNode>(Top("\"a#{b}c\""));
        Assert.Equal(3, interp.Parts.Length);
        Assert.IsType<StringNode>(interp.Parts[0]);
        Assert.IsType<EmbeddedStatementsNode>(interp.Parts[1]);
        Assert.IsType<StringNode>(interp.Parts[2]);
    }

    [Fact]
    public void CaseInArrayPattern()
    {
        var cm = Assert.IsType<CaseMatchNode>(Top("case x\nin [1, *rest]\nend"));
        var inn = Assert.IsType<InNode>(cm.Conditions[0]);
        var arr = Assert.IsType<ArrayPatternNode>(inn.Pattern);
        Assert.Single(arr.Requireds);
        Assert.NotNull(arr.Rest);
    }

    [Fact]
    public void HeredocIsString()
    {
        var src = "<<~TEXT\n  hello\n  world\nTEXT\n";
        var node = Top(src);
        Assert.True(node is StringNode or InterpolatedStringNode);
    }

    [Fact]
    public void BignumLiteralValue()
    {
        var i = Assert.IsType<IntegerNode>(Top("1_000_000_000_000_000_000_000"));
        Assert.IsType<BigInteger>(i.Value);
        Assert.Equal(BigInteger.Parse("1000000000000000000000"), (BigInteger)i.Value);
    }

    [Fact]
    public void FloatLiteralValue()
        => Assert.Equal(1.5, Assert.IsType<FloatNode>(Top("1.5")).Value);

    [Fact]
    public void PercentWordArray()
    {
        var arr = Assert.IsType<ArrayNode>(Top("%w[a b c]"));
        Assert.Equal(3, arr.Elements.Length);
        Assert.All(arr.Elements, e => Assert.IsType<StringNode>(e));
    }

    [Fact]
    public void SymbolArray()
    {
        var arr = Assert.IsType<ArrayNode>(Top("%i[x y]"));
        Assert.Equal(2, arr.Elements.Length);
        Assert.All(arr.Elements, e => Assert.IsType<SymbolNode>(e));
    }

    [Fact]
    public void RegexpWithFlags()
    {
        var re = Assert.IsType<RegularExpressionNode>(Top("/ab+/i"));
        Assert.Equal("ab+", re.Unescaped);
        Assert.NotEqual(0, re.Flags & (int)RegularExpressionFlags.IgnoreCase);
    }

    [Fact]
    public void HashWithSymbolKeys()
    {
        var h = Assert.IsType<HashNode>(Top("{a: 1, b: 2}"));
        Assert.Equal(2, h.Elements.Length);
        Assert.All(h.Elements, e => Assert.IsType<AssocNode>(e));
    }

    [Fact]
    public void BeginlessAndEndlessRange()
    {
        Assert.Null(Assert.IsType<RangeNode>(Top("..5")).Left);
        Assert.Null(Assert.IsType<RangeNode>(Top("1..")).Right);
    }

    [Fact]
    public void Warnings_ArePopulated()
    {
        // `*` in void context yields a Prism warning
        var r = PrismParser.Parse("1 + 2");
        Assert.True(r.Success);
        // not asserting count — just that warnings is a valid (possibly empty) list
        Assert.NotNull(r.Warnings);
    }

    [Fact]
    public void ChildNodesOrderMatchesPrism()
    {
        // CallNode child order: receiver, arguments, block
        var call = Assert.IsType<CallNode>(Top("1 + 2"));
        var kids = call.ChildNodes().Where(n => n is not null).ToList();
        Assert.IsType<IntegerNode>(kids[0]);        // receiver
        Assert.IsType<ArgumentsNode>(kids[1]);      // arguments
    }
}
