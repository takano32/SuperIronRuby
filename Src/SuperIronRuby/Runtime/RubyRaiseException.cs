namespace SuperIronRuby.Runtime;

/// <summary>
/// The CLR exception that carries a raised Ruby exception up the call stack.
/// Ruby <c>raise</c> throws one of these; <c>rescue</c> in the interpreter
/// catches it and inspects <see cref="RubyException"/>. Builtins must only catch
/// this (never the unwind control-flow exceptions) and only when they genuinely
/// handle the error.
/// </summary>
public sealed class RubyRaiseException : Exception
{
    /// <summary>The Ruby-level exception object being raised.</summary>
    public RubyExceptionObject RubyException { get; }

    public RubyRaiseException(RubyExceptionObject rubyException)
        : base(rubyException?.Message ?? string.Empty)
        => RubyException = rubyException!;
}
