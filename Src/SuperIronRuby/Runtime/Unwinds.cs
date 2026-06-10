namespace SuperIronRuby.Runtime;

// Non-local control-flow signals. These are CLR exceptions used purely to unwind
// the interpreter/builtin call stack for Ruby's break/next/redo/retry/return/throw.
// They are NOT Ruby errors: builtins must let them propagate and must not catch
// them (catch RubyRaiseException only). All are sealed.

/// <summary>Ruby <c>break</c>: exits the block's calling method, yielding a value.
/// <see cref="SourceId"/> identifies the owning proc literal so the right frame
/// catches it.</summary>
public sealed class BreakUnwind : Exception
{
    public object? Value { get; }
    public long SourceId { get; }

    public BreakUnwind(object? value, long sourceId)
    {
        Value = value;
        SourceId = sourceId;
    }
}

/// <summary>Ruby <c>next</c>: returns a value from the current block iteration.</summary>
public sealed class NextUnwind : Exception
{
    public object? Value { get; }

    public NextUnwind(object? value) => Value = value;
}

/// <summary>Ruby <c>redo</c>: re-runs the current block iteration without re-fetching.</summary>
public sealed class RedoUnwind : Exception
{
}

/// <summary>Ruby <c>retry</c>: restarts a <c>begin</c>/<c>rescue</c> body (or generator).</summary>
public sealed class RetryUnwind : Exception
{
}

/// <summary>Ruby <c>return</c>: returns from the enclosing method.
/// <see cref="FrameId"/> identifies the method frame to unwind to.</summary>
public sealed class ReturnUnwind : Exception
{
    public object? Value { get; }
    public long FrameId { get; }

    public ReturnUnwind(object? value, long frameId)
    {
        Value = value;
        FrameId = frameId;
    }
}

/// <summary>Ruby <c>throw</c>: unwinds to the matching <c>catch</c> tag.</summary>
public sealed class ThrowUnwind : Exception
{
    public object? Tag { get; }
    public object? Value { get; }

    public ThrowUnwind(object? tag, object? value)
    {
        Tag = tag;
        Value = value;
    }
}
