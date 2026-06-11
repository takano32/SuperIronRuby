using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>Exception object methods (task B13 core).</summary>
[RubyClass("Exception")]
public static class ExceptionOps
{
    private static RubyExceptionObject E(object? s) => (RubyExceptionObject)s!;

    [RubyMethod("initialize", Visibility = RubyMethodVisibility.Private)]
    public static object? Init(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        if (s is RubyExceptionObject ex)
            ex.Message = a.Length > 0 && a[0] is not null ? c.ToStr(a[0]) : (ex.RubyClass.Name ?? "Exception");
        return s;
    }

    [RubyMethod("message")]
    [RubyMethod("to_s")]
    public static object? Message(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var ex = E(s);
        return new MutableString(string.IsNullOrEmpty(ex.Message) ? (ex.RubyClass.Name ?? "") : ex.Message);
    }

    [RubyMethod("inspect")]
    public static object? Inspect(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var ex = E(s);
        var cls = ex.RubyClass.Name ?? "Exception";
        return new MutableString(string.IsNullOrEmpty(ex.Message) || ex.Message == cls
            ? cls
            : $"#<{cls}: {ex.Message}>");
    }

    [RubyMethod("full_message")]
    public static object? FullMessage(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var ex = E(s);
        return new MutableString($"{ex.Message} ({ex.RubyClass.Name})");
    }

    [RubyMethod("backtrace")]
    public static object? Backtrace(RubyContext c, object? s, object?[] a, RubyProc? b)
    {
        var arr = new RubyArray();
        foreach (var line in E(s).Backtrace) arr.Add(new MutableString(line));
        return arr;
    }

    [RubyMethod("exception")]
    public static object? Exception(RubyContext c, object? s, object?[] a, RubyProc? b) => s;

    [RubyMethod("==")]
    public static object? Eq(RubyContext c, object? s, object?[] a, RubyProc? b)
        => a[0] is RubyExceptionObject o && ReferenceEquals(o.RubyClass, E(s).RubyClass) && o.Message == E(s).Message;
}
