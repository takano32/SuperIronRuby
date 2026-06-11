using SuperIronRuby.Hosting;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Hosting;

// Task W1: end-to-end execution through the ScriptEngine.
public class EngineTests
{
    [Fact]
    public void ExecuteReturnsLastValue()
    {
        var engine = Ruby.CreateEngine();
        Assert.Equal(3L, engine.Execute("1 + 2"));
    }

    [Fact]
    public void PutsWritesToStdout()
    {
        var engine = Ruby.CreateEngine();
        Assert.Equal("hello\n", engine.ExecuteToString("puts \"hello\""));
    }

    [Fact]
    public void PutsArithmetic()
    {
        var engine = Ruby.CreateEngine();
        Assert.Equal("2\n", engine.ExecuteToString("puts 1 + 1"));
    }

    [Fact]
    public void PInspectsValues()
    {
        var engine = Ruby.CreateEngine();
        Assert.Equal("[1, \"a\", :b]\n", engine.ExecuteToString("p [1, \"a\", :b]"));
    }

    [Fact]
    public void ConstantsPersistAcrossExecute()
    {
        var engine = Ruby.CreateEngine();
        engine.Execute("class Foo; def v; 7; end; end");
        Assert.Equal(7L, engine.Execute("Foo.new.v"));
    }

    [Fact]
    public void InstanceVariablesPersistAcrossExecute()
    {
        // Top-level locals do NOT persist across separate Execute calls because
        // Prism parses each snippet independently (a bare `x` with no prior
        // assignment in that snippet parses as a method call). Instance variables
        // on the main object do persist and parse unambiguously.
        var engine = Ruby.CreateEngine();
        engine.Execute("@x = 10");
        Assert.Equal(10L, engine.Execute("@x"));
    }

    [Fact]
    public void SyntaxErrorThrows()
    {
        var engine = Ruby.CreateEngine();
        var ex = Assert.Throws<RubySyntaxError>(() => engine.Execute("def "));
        Assert.Contains(":", ex.Message);
    }

    [Fact]
    public void MethodDefinitionAndCall()
    {
        var engine = Ruby.CreateEngine();
        Assert.Equal("9\n", engine.ExecuteToString("def sq(x) = x * x\nputs sq(3)"));
    }

    [Fact]
    public void IntegerToSWithBase()
    {
        var engine = Ruby.CreateEngine();
        Assert.Equal("ff\n", engine.ExecuteToString("puts 255.to_s(16)"));
    }

    [Fact]
    public void WhileLoopProgram()
    {
        var engine = Ruby.CreateEngine();
        var src = "i = 0\nsum = 0\nwhile i < 5\n  i = i + 1\n  sum = sum + i\nend\nputs sum";
        Assert.Equal("15\n", engine.ExecuteToString(src));
    }

    [Fact]
    public void ArrayEachWithBlock()
    {
        var engine = Ruby.CreateEngine();
        var src = "total = 0\n[1, 2, 3, 4].each { |n| total = total + n }\nputs total";
        Assert.Equal("10\n", engine.ExecuteToString(src));
    }

    [Fact]
    public void FloatFormatting()
    {
        var engine = Ruby.CreateEngine();
        Assert.Equal("1.5\n", engine.ExecuteToString("puts 3.0 / 2"));
    }

    [Fact]
    public void BignumArithmetic()
    {
        var engine = Ruby.CreateEngine();
        Assert.Equal("1208925819614629174706176\n", engine.ExecuteToString("puts 2 ** 80"));
    }

    [Fact]
    public void ExecuteFileRuns()
    {
        var engine = Ruby.CreateEngine();
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "x = 21\nputs x * 2\n");
            Assert.Equal("42\n", CaptureFile(engine, tmp));
        }
        finally { File.Delete(tmp); }
    }

    private static string CaptureFile(ScriptEngine engine, string path)
    {
        var prev = engine.Context.Stdout;
        var sw = new StringWriter();
        engine.Context.Stdout = sw;
        try { engine.ExecuteFile(path); }
        finally { engine.Context.Stdout = prev; }
        return sw.ToString();
    }
}
