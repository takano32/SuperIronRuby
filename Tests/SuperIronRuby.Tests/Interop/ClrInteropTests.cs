using SuperIronRuby.Hosting;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Interop;

// Task W3: CLR interop via System:: namespaces.
public class ClrInteropTests
{
    private static object? Run(string src) => Ruby.CreateEngine().Execute(src);

    [Fact]
    public void NamespaceResolvesToModule()
    {
        Assert.IsType<ClrNamespaceModule>(Run("System"));
        Assert.IsType<ClrNamespaceModule>(Run("System::Text"));
    }

    [Fact]
    public void TypeResolvesToClass()
    {
        Assert.IsType<RubyClass>(Run("System::Text::StringBuilder"));
    }

    [Fact]
    public void ConstructAndCallInstanceMethod()
    {
        var src = "sb = System::Text::StringBuilder.new; sb.append(\"a\"); sb.append(\"b\"); sb.to_string";
        var v = Assert.IsType<MutableString>(Run(src));
        Assert.Equal("ab", v.Value);
    }

    [Fact]
    public void StaticMethodCall()
    {
        Assert.Equal(2.0, Run("System::Math.sqrt(4.0)"));
    }

    [Fact]
    public void StaticPropertyAccess()
    {
        var v = Run("System::Environment.processor_count");
        Assert.IsType<long>(v);
        Assert.True((long)v! > 0);
    }

    [Fact]
    public void StringConversionRoundTrip()
    {
        var src = "System::String.concat(\"foo\", \"bar\")";
        var v = Assert.IsType<MutableString>(Run(src));
        Assert.Equal("foobar", v.Value);
    }
}
