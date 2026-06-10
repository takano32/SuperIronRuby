namespace SuperIronRuby.Runtime;

/// <summary>
/// Ruby's <c>Proc</c> (and <c>lambda</c>). Wraps a C# delegate body plus the
/// metadata needed for block/lambda control-flow semantics.
/// </summary>
/// <remarks>
/// <see cref="SourceId"/> gives each proc literal a stable identity so a
/// <c>break</c> raised inside a block (carried by <c>BreakUnwind</c>) can be
/// matched to the call frame that owns the block. Lambdas treat <c>return</c>
/// and arity differently from non-lambda procs; <see cref="IsLambda"/> records
/// which one this is.
/// </remarks>
public sealed class RubyProc
{
    /// <summary>The proc's executable body.</summary>
    public Func<object?[], object?> Body { get; }

    /// <summary>True for lambdas (strict arity, local <c>return</c>).</summary>
    public bool IsLambda { get; set; }

    /// <summary>Minimum required positional arguments.</summary>
    public int ArityMin { get; set; }

    /// <summary>Maximum positional arguments, or -1 for a splat (unbounded).</summary>
    public int ArityMax { get; set; }

    /// <summary>Stable identity of the proc literal, used to match <c>break</c>.</summary>
    public long SourceId { get; set; }

    public RubyProc(Func<object?[], object?> body) => Body = body;

    /// <summary>
    /// Invokes the proc. May throw <see cref="BreakUnwind"/>, <see cref="NextUnwind"/>,
    /// etc.; callers (yielding builtins) must let those propagate.
    /// </summary>
    public object? Call(params object?[] args) => Body(args);
}
