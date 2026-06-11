using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Interpreter;

// Task I6: multiple assignment and operator assignment.
public class AssignmentTests
{
    private static object? Run(string src) => TestRuntime.Run(src);

    [Fact]
    public void SimpleMultiAssign() => Assert.Equal(3L, Run("a, b = 1, 2; a + b"));

    [Fact]
    public void Swap() => Assert.Equal(true, Run("a = 1; b = 2; a, b = b, a; a == 2 && b == 1"));

    [Fact]
    public void SplatInMiddle()
    {
        var v = Assert.IsType<RubyArray>(Run("a, *r, z = 1, 2, 3, 4; r"));
        Assert.Equal(new object?[] { 2L, 3L }, v);
    }

    [Fact]
    public void SplatGetsRemainder()
    {
        Assert.Equal(1L, Run("a, *r = 1, 2, 3; a"));
        var r = Assert.IsType<RubyArray>(Run("a, *r = 1, 2, 3; r"));
        Assert.Equal(new object?[] { 2L, 3L }, r);
    }

    [Fact]
    public void MultiAssignFromArray() => Assert.Equal(2L, Run("a, b = [1, 2]; b"));

    [Fact]
    public void NestedMultiTarget() => Assert.Equal(true, Run("(a, b), c = [1, 2], 3; a == 1 && b == 2 && c == 3"));

    [Fact]
    public void OrAssignWhenNil() => Assert.Equal(5L, Run("x = nil; x ||= 5; x"));

    [Fact]
    public void OrAssignKeepsTruthy() => Assert.Equal(1L, Run("x = 1; x ||= 5; x"));

    [Fact]
    public void AndAssignWhenTruthy() => Assert.Equal(9L, Run("x = 1; x &&= 9; x"));

    [Fact]
    public void AndAssignKeepsFalsy() => Assert.Null(Run("x = nil; x &&= 9; x"));

    [Fact]
    public void OpAssignAdd() => Assert.Equal(7L, Run("x = 5; x += 2; x"));

    [Fact]
    public void OpAssignMultiply() => Assert.Equal(12L, Run("x = 3; x *= 4; x"));

    [Fact]
    public void IvarOrAssignOnFreshObject() => Assert.Equal(3L, Run("@x ||= 3; @x"));

    [Fact]
    public void IvarOpAssign() => Assert.Equal(11L, Run("@x = 10; @x += 1; @x"));

    [Fact]
    public void GlobalOpAssign() => Assert.Equal(2L, Run("$g = 1; $g += 1; $g"));

    [Fact]
    public void ConstOrAssignWhenMissing() => Assert.Equal(42L, Run("FOO ||= 42; FOO"));

    [Fact]
    public void IndexOpAssign()
    {
        var src = "h = {}; h[:k] = 1; h[:k] += 5; h[:k]";
        Assert.Equal(6L, Run(src));
    }

    [Fact]
    public void IndexOrAssign()
    {
        var src = "h = {}; h[:k] ||= 10; h[:k] ||= 20; h[:k]";
        Assert.Equal(10L, Run(src));
    }

    [Fact]
    public void AttrOpAssignEvaluatesReceiverOnce()
    {
        // a counter method bumped each time it's called; attr += should call the
        // receiver expression once.
        var src = @"
class Box
  def initialize; @v = 0; end
  def v; @v; end
  def v=(x); @v = x; end
end
b = Box.new
b.v += 5
b.v";
        Assert.Equal(5L, Run(src));
    }
}
