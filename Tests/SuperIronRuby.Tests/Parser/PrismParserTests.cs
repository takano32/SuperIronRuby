using SuperIronRuby.Parser;

namespace SuperIronRuby.Tests.Parser;

// Exercises the native libprism P/Invoke path end-to-end, plus the MRI fallback.
public class PrismParserTests
{
    private static StatementsNode Body(ParseResult r) => (StatementsNode)r.Root.Statements;

    [Fact]
    public void Native_ParsesSimpleCall()
    {
        var r = PrismParser.Parse("puts 1");
        Assert.True(r.Success);
        var call = Assert.IsType<CallNode>(Body(r).Body[0]);
        Assert.Equal("puts", call.Name);
        var args = Assert.IsType<ArgumentsNode>(call.Arguments);
        Assert.Equal(1L, Assert.IsType<IntegerNode>(args.Arguments[0]).Value);
    }

    [Fact]
    public void Native_ParsesClassAndMethod()
    {
        var r = PrismParser.Parse("class Foo\n  def bar; 42; end\nend");
        Assert.True(r.Success);
        var cls = Assert.IsType<ClassNode>(Body(r).Body[0]);
        Assert.Equal("Foo", cls.Name);
    }

    [Fact]
    public void SyntaxError_IsReportedNotThrown()
    {
        // unterminated def — Prism is error-tolerant and reports diagnostics
        var r = PrismParser.Parse("def ");
        Assert.False(r.Success);
        Assert.NotEmpty(r.Errors);
        Assert.All(r.Errors, e => Assert.False(string.IsNullOrEmpty(e.Message)));
    }

    [Fact]
    public void Source_AndFilePathArePreserved()
    {
        var r = PrismParser.Parse("1 + 2", "demo.rb");
        Assert.Equal("1 + 2", r.Source);
        Assert.Equal("demo.rb", r.FilePath);
    }

    [Fact]
    public void LineForOffset_MultiLine()
    {
        var src = "a = 1\nb = 2\nc = 3";
        var r = PrismParser.Parse(src);
        Assert.Equal(1, r.LineForOffset(0));   // 'a'
        Assert.Equal(2, r.LineForOffset(6));   // 'b'
        Assert.Equal(3, r.LineForOffset(12));  // 'c'
    }

    [Fact]
    public void NativeAndMriBackendsAgree()
    {
        // Skip gracefully if `ruby` is not on PATH.
        if (!RubyAvailable()) return;

        var sourceBytes = System.Text.Encoding.UTF8.GetBytes("def f(x) = x * 2");
        var native = InvokeSerialize(sourceBytes, "native");
        var mri = InvokeSerialize(sourceBytes, "mri");
        Assert.Equal(mri, native);
    }

    private static byte[] InvokeSerialize(byte[] src, string backend)
    {
        var prev = Environment.GetEnvironmentVariable("SIR_PARSER_BACKEND");
        Environment.SetEnvironmentVariable("SIR_PARSER_BACKEND", backend);
        try
        {
            // Round-trip through Parse and re-serialize is awkward; instead parse
            // and compare a stable projection (node count via a walk).
            var r = PrismParser.Parse(System.Text.Encoding.UTF8.GetString(src));
            return System.Text.Encoding.UTF8.GetBytes(Walk(r.Root));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SIR_PARSER_BACKEND", prev);
        }
    }

    private static string Walk(Node n)
    {
        var sb = new System.Text.StringBuilder();
        void Rec(Node? node)
        {
            if (node is null) return;
            sb.Append(node.Type).Append(';');
            foreach (var c in node.ChildNodes()) Rec(c);
        }
        Rec(n);
        return sb.ToString();
    }

    private static bool RubyAvailable()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("ruby", "--version")
            { RedirectStandardOutput = true, UseShellExecute = false };
            using var p = System.Diagnostics.Process.Start(psi);
            p!.WaitForExit();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}
