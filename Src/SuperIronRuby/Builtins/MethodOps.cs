using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>A bound method object (Object#method). Routed via Extends.</summary>
public sealed class RubyBoundMethod
{
    public object? Receiver { get; }
    public RubyMethodInfo Method { get; }
    public RubyBoundMethod(object? receiver, RubyMethodInfo method) { Receiver = receiver; Method = method; }
}

[RubyModule("Kernel")]
public static class MethodObjectKernelOps
{
    [RubyMethod("method")]
    public static object? Method(RubyContext c, object? self, object?[] a, RubyProc? b)
    {
        var name = KernelOps.NameOf(a[0]);
        var m = c.ResolveMethod(self, name) ?? throw c.RaiseNameError($"undefined method '{name}' for class '{c.GetClassOf(self).Name}'");
        return new RubyBoundMethod(self, m);
    }
}

[RubyClass("Method", Extends = typeof(RubyBoundMethod))]
public static class MethodOps
{
    private static RubyBoundMethod B(object? s) => (RubyBoundMethod)s!;

    [RubyMethod("call")]
    [RubyMethod("()")]
    [RubyMethod("[]")]
    public static object? Call(RubyContext c, object? s, object?[] a, RubyProc? blk)
        => c.Invoke(B(s).Method, B(s).Receiver, a, blk);

    [RubyMethod("to_proc")]
    public static object? ToProc(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var bm = B(s);
        return new RubyProc(args => c.Invoke(bm.Method, bm.Receiver, args, null));
    }

    [RubyMethod("name")]
    public static object? Name(RubyContext c, object? s, object?[] a, RubyProc? b) => c.Intern(B(s).Method.Name);

    [RubyMethod("owner")]
    public static object? Owner(RubyContext c, object? s, object?[] a, RubyProc? b) => B(s).Method.Owner;

    [RubyMethod("receiver")]
    public static object? Receiver(RubyContext c, object? s, object?[] a, RubyProc? b) => B(s).Receiver;

    [RubyMethod("arity")]
    public static object? Arity(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var m = B(s).Method;
        return m.ArityMax == m.ArityMin ? (long)m.ArityMin : (long)(-(m.ArityMin + 1));
    }
}
