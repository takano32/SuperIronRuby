using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Interpreter;

// Task I5: if/unless/ternary, while/until, case/when, begin/rescue/ensure, jumps.
public class ControlFlowTests
{
    private static object? Run(string src) => TestRuntime.Run(src);

    [Fact]
    public void IfTrueBranch() => Assert.Equal(1L, Run("if true; 1; else; 2; end"));

    [Fact]
    public void IfFalseBranch() => Assert.Equal(2L, Run("if false; 1; else; 2; end"));

    [Fact]
    public void Elsif() => Assert.Equal(2L, Run("x = 2; if x == 1; 10; elsif x == 2; 2; else; 3; end"));

    [Fact]
    public void Ternary() => Assert.Equal(5L, Run("true ? 5 : 6"));

    [Fact]
    public void Unless() => Assert.Equal(1L, Run("unless false; 1; else; 2; end"));

    [Fact]
    public void IfModifier() => Assert.Equal(7L, Run("x = 7 if true; x"));

    [Fact]
    public void WhileLoopAccumulates()
        => Assert.Equal(5L, Run("i = 0; while i < 5; i = i + 1; end; i"));

    [Fact]
    public void WhileBreakWithValue()
        => Assert.Equal(3L, Run("i = 0; r = while true; i = i + 1; break i if i == 3; end; r"));

    [Fact]
    public void UntilLoop()
        => Assert.Equal(3L, Run("i = 0; until i >= 3; i = i + 1; end; i"));

    [Fact]
    public void NextSkips()
    {
        // sum only even-ish via next; count iterations that increment sum
        var src = "sum = 0; i = 0; while i < 5; i = i + 1; next if i == 3; sum = sum + 1; end; sum";
        Assert.Equal(4L, Run(src));   // skipped one iteration
    }

    [Fact]
    public void CaseWhenMatch()
    {
        var src = "x = 2; case x; when 1; 10; when 2; 20; else; 30; end";
        Assert.Equal(20L, Run(src));
    }

    [Fact]
    public void CaseElse()
    {
        var src = "x = 9; case x; when 1; 10; else; 30; end";
        Assert.Equal(30L, Run(src));
    }

    [Fact]
    public void CaseWithClassPattern()
    {
        var src = "case 5; when String; :str; when Integer; :int; end";
        var v = Assert.IsType<RubySymbol>(Run(src));
        Assert.Equal("int", v.Name);
    }

    [Fact]
    public void BeginRescue()
    {
        var src = "begin; raise 'boom'; rescue => e; e.message; end";
        var v = Assert.IsType<MutableString>(Run(src));
        Assert.Equal("boom", v.Value);
    }

    [Fact]
    public void RescueSpecificClass()
    {
        var src = @"
begin
  raise TypeError, 'bad'
rescue ArgumentError
  :arg
rescue TypeError
  :type
end";
        Assert.Equal("type", Assert.IsType<RubySymbol>(Run(src)).Name);
    }

    [Fact]
    public void EnsureRuns()
    {
        var src = @"
$log = 0
begin
  $log = 1
rescue
  $log = 2
ensure
  $log = $log + 10
end
$log";
        Assert.Equal(11L, Run(src));
    }

    [Fact]
    public void RescueModifier()
        => Assert.Equal(42L, Run("(raise 'x' rescue 42)"));

    [Fact]
    public void RetryEventuallySucceeds()
    {
        var src = @"
$tries = 0
begin
  $tries = $tries + 1
  raise 'again' if $tries < 3
  $tries
rescue
  retry
end";
        Assert.Equal(3L, Run(src));
    }

    // for-loop evaluation is implemented; its test needs range/array literals
    // (task I7) to supply a collection, so it lives in the I7 test suite.

    [Fact]
    public void EnsureRunsOnException()
    {
        var src = @"
$ran = false
begin
  begin
    raise 'e'
  ensure
    $ran = true
  end
rescue
end
$ran";
        Assert.Equal(true, Run(src));
    }
}
