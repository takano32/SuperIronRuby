namespace SuperIronRuby.Runtime;

/// <summary>
/// The Ruby-level object for an exception (an instance of <c>Exception</c> or a
/// subclass). Carries the message and backtrace. This is the Ruby value; the
/// C# control-flow wrapper that actually unwinds the stack is
/// <see cref="RubyRaiseException"/>.
/// </summary>
public class RubyExceptionObject : RubyObject
{
    /// <summary>The exception message (Ruby's <c>message</c>).</summary>
    public string Message { get; set; }

    /// <summary>The backtrace lines (Ruby's <c>backtrace</c>); empty until raised.</summary>
    public List<string> Backtrace { get; } = new();

    public RubyExceptionObject(RubyClass rubyClass, string message = "")
        : base(rubyClass)
        => Message = message ?? string.Empty;
}
