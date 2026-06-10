using SuperIronRuby.Runtime;

namespace SuperIronRuby.Builtins;

/// <summary>The handful of methods Ruby defines directly on BasicObject.</summary>
[RubyClass("BasicObject", Inherits = "BasicObject")]
public static class BasicObjectOps
{
    // initialize is a private no-op; Class#new calls it after allocation.
    [RubyMethod("initialize", Visibility = RubyMethodVisibility.Private)]
    public static object? Initialize(RubyContext ctx, object? self, object?[] args, RubyProc? block)
        => null;

    [RubyMethod("method_missing", Visibility = RubyMethodVisibility.Private)]
    public static object? MethodMissing(RubyContext ctx, object? self, object?[] args, RubyProc? block)
    {
        // Default: raise NoMethodError. args[0] is the symbol name.
        var name = args.Length > 0 ? KernelOps.NameOf(args[0]) : "?";
        throw ctx.RaiseNoMethodError(self, name);
    }
}
