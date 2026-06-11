using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Interpreter;

// Task I4: classes/modules, constants, ivars/cvars/gvars, defined?.
public class ClassesTests
{
    private static object? Run(string src) => TestRuntime.Run(src);

    [Fact]
    public void DefineClassAndReadConstant()
    {
        var v = Run("class Foo; end; Foo");
        var cls = Assert.IsType<RubyClass>(v);
        Assert.Equal("Foo", cls.Name);
    }

    [Fact]
    public void InstanceMethodOnUserClass()
        => Assert.Equal(42L, Run("class C; def greet(n); n + 1; end; end; C.new.greet(41)"));

    [Fact]
    public void IvarRoundTripViaMethods()
    {
        var src = "class C; def set(v); @x = v; end; def get; @x; end; end; " +
                  "c = C.new; c.set(7); c.get";
        Assert.Equal(7L, Run(src));
    }

    [Fact]
    public void SuperChainAcrossClasses()
    {
        var src = @"
class A; def kind; 1; end; end
class B < A; def kind; super + 10; end; end
B.new.kind
";
        Assert.Equal(11L, Run(src));
    }

    [Fact]
    public void NestedModuleAndConstantPath()
    {
        var src = "module M; class Inner; end; end; M::Inner";
        var cls = Assert.IsType<RubyClass>(Run(src));
        Assert.Equal("M::Inner", cls.Name);
    }

    [Fact]
    public void ReopenClassAddsMethods()
    {
        var src = @"
class C; def a; 1; end; end
class C; def b; 2; end; end
c = C.new
c.a + c.b
";
        Assert.Equal(3L, Run(src));
    }

    [Fact]
    public void ConstantAssignment()
        => Assert.Equal(99L, Run("FOO = 99; FOO"));

    [Fact]
    public void ClassConstant()
    {
        var src = "class C; X = 5; def getx; X; end; end; C.new.getx";
        Assert.Equal(5L, Run(src));
    }

    [Fact]
    public void ClassVariableSharedAcrossInstances()
    {
        var src = @"
class Counter
  @@count = 0
  def add; @@count = @@count + 1; end
  def count; @@count; end
end
c1 = Counter.new
c2 = Counter.new
c1.add
c2.add
c2.count
";
        Assert.Equal(2L, Run(src));
    }

    [Fact]
    public void GlobalVariable()
        => Assert.Equal(5L, Run("$g = 5; $g"));

    [Fact]
    public void UninitializedConstantRaises()
    {
        var ex = Assert.Throws<RubyRaiseException>(() => Run("NoSuchConstant"));
        Assert.Equal("uninitialized constant NoSuchConstant", ex.RubyException.Message);
    }

    [Fact]
    public void SingletonClassReopen()
    {
        var src = @"
class C; end
class << C
  def klass_method; 7; end
end
C.klass_method
";
        Assert.Equal(7L, Run(src));
    }

    [Theory]
    [InlineData("x = 1; defined?(x)", "local-variable")]
    [InlineData("defined?(nil)", "expression")]
    [InlineData("defined?(self)", "self")]
    [InlineData("defined?(String)", "constant")]
    [InlineData("@iv = 1; defined?(@iv)", "instance-variable")]
    [InlineData("$g = 1; defined?($g)", "global-variable")]
    public void DefinedReturnsKind(string src, string expected)
    {
        var v = Assert.IsType<MutableString>(Run(src));
        Assert.Equal(expected, v.Value);
    }

    [Fact]
    public void DefinedReturnsNilForUndefined()
        => Assert.Null(Run("defined?(no_such_thing_xyz)"));
}
