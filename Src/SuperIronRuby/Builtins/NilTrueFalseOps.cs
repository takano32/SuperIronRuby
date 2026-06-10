using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

[RubyClass("NilClass")]
public static class NilOps
{
    [RubyMethod("to_s")]
    public static object? ToS(RubyContext ctx, object? self, object?[] a, RubyProc? b) => new MutableString("");

    [RubyMethod("inspect")]
    public static object? Inspect(RubyContext ctx, object? self, object?[] a, RubyProc? b) => new MutableString("nil");

    [RubyMethod("to_a")]
    public static object? ToA(RubyContext ctx, object? self, object?[] a, RubyProc? b) => new RubyArray();

    [RubyMethod("to_i")]
    public static object? ToI(RubyContext ctx, object? self, object?[] a, RubyProc? b) => 0L;

    [RubyMethod("nil?")]
    public static object? Nil(RubyContext ctx, object? self, object?[] a, RubyProc? b) => true;

    [RubyMethod("&")]
    public static object? And(RubyContext ctx, object? self, object?[] a, RubyProc? b) => false;

    [RubyMethod("|")]
    public static object? Or(RubyContext ctx, object? self, object?[] a, RubyProc? b) => RubyContext.Truthy(a[0]);

    [RubyMethod("^")]
    public static object? Xor(RubyContext ctx, object? self, object?[] a, RubyProc? b) => RubyContext.Truthy(a[0]);
}

[RubyClass("TrueClass")]
public static class TrueOps
{
    [RubyMethod("to_s")]
    [RubyMethod("inspect")]
    public static object? ToS(RubyContext ctx, object? self, object?[] a, RubyProc? b) => new MutableString("true");

    [RubyMethod("&")]
    public static object? And(RubyContext ctx, object? self, object?[] a, RubyProc? b) => RubyContext.Truthy(a[0]);

    [RubyMethod("|")]
    public static object? Or(RubyContext ctx, object? self, object?[] a, RubyProc? b) => true;

    [RubyMethod("^")]
    public static object? Xor(RubyContext ctx, object? self, object?[] a, RubyProc? b) => !RubyContext.Truthy(a[0]);
}

[RubyClass("FalseClass")]
public static class FalseOps
{
    [RubyMethod("to_s")]
    [RubyMethod("inspect")]
    public static object? ToS(RubyContext ctx, object? self, object?[] a, RubyProc? b) => new MutableString("false");

    [RubyMethod("&")]
    public static object? And(RubyContext ctx, object? self, object?[] a, RubyProc? b) => false;

    [RubyMethod("|")]
    public static object? Or(RubyContext ctx, object? self, object?[] a, RubyProc? b) => RubyContext.Truthy(a[0]);

    [RubyMethod("^")]
    public static object? Xor(RubyContext ctx, object? self, object?[] a, RubyProc? b) => RubyContext.Truthy(a[0]);
}
