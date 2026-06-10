using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Runtime;

// Method dispatch: resolution, cache invalidation, method_missing, visibility,
// arity. Error messages verified against ruby 4.0.2.
public class DispatchTests
{
    private static RubyMethodInfo Builtin(RubyModule owner, string name, BuiltinMethodBody body,
        RubyMethodVisibility vis = RubyMethodVisibility.Public, int min = 0, int max = -1)
        => RubyMethodInfo.FromBuiltin(name, owner, body, vis, min, max);

    [Fact]
    public void Send_DispatchesBuiltinOnClass()
    {
        var ctx = new RubyContext();
        var c = ctx.DefineClass("C", ctx.ObjectClass);
        c.DefineMethod("greet", Builtin(c, "greet", (_, self, _, _) => new MutableString("hi")));
        var obj = new RubyObject(c);
        Assert.Equal("hi", ((MutableString)ctx.Send(obj, "greet", System.Array.Empty<object?>())!).Value);
    }

    [Fact]
    public void Send_InheritsFromSuperclass()
    {
        var ctx = new RubyContext();
        var a = ctx.DefineClass("A", ctx.ObjectClass);
        a.DefineMethod("v", Builtin(a, "v", (_, _, _, _) => 1L));
        var b = ctx.DefineClass("B", a);
        var obj = new RubyObject(b);
        Assert.Equal(1L, ctx.Send(obj, "v", System.Array.Empty<object?>()));
    }

    [Fact]
    public void Send_PrependOverridesOwnMethod()
    {
        var ctx = new RubyContext();
        var c = ctx.DefineClass("C2", ctx.ObjectClass);
        var m = new RubyModule { Name = "M" };
        c.DefineMethod("x", Builtin(c, "x", (_, _, _, _) => "class"));
        m.DefineMethod("x", Builtin(m, "x", (_, _, _, _) => "module"));
        c.Prepend(m);
        var obj = new RubyObject(c);
        Assert.Equal("module", ctx.Send(obj, "x", System.Array.Empty<object?>()));
    }

    [Fact]
    public void Send_MethodMissingReceivesSymbolAndArgs()
    {
        var ctx = new RubyContext();
        var c = ctx.DefineClass("Ghost", ctx.ObjectClass);
        object? seenName = null;
        object?[]? seenArgs = null;
        c.DefineMethod("method_missing", Builtin(c, "method_missing", (_, _, args, _) =>
        {
            seenName = args[0];
            seenArgs = args.Skip(1).ToArray();
            return "handled";
        }));
        var obj = new RubyObject(c);
        var result = ctx.Send(obj, "anything", new object?[] { 1L, 2L });
        Assert.Equal("handled", result);
        Assert.Same(ctx.Intern("anything"), seenName);
        Assert.Equal(new object?[] { 1L, 2L }, seenArgs);
    }

    [Fact]
    public void Send_UndefinedRaisesNoMethodError()
    {
        var ctx = new RubyContext();
        var ex = Assert.Throws<RubyRaiseException>(
            () => ctx.Send(1L, "nope", System.Array.Empty<object?>()));
        Assert.Same(ctx.NoMethodErrorClass, ex.RubyException.RubyClass);
        Assert.Equal("undefined method 'nope' for an instance of Integer", ex.RubyException.Message);
    }

    [Fact]
    public void Cache_InvalidatesAfterRedefinition()
    {
        var ctx = new RubyContext();
        var c = ctx.DefineClass("Redef", ctx.ObjectClass);
        c.DefineMethod("v", Builtin(c, "v", (_, _, _, _) => 1L));
        var obj = new RubyObject(c);
        Assert.Equal(1L, ctx.Send(obj, "v", System.Array.Empty<object?>())); // populates cache
        c.DefineMethod("v", Builtin(c, "v", (_, _, _, _) => 2L));            // bumps version
        Assert.Equal(2L, ctx.Send(obj, "v", System.Array.Empty<object?>())); // recomputed
    }

    [Fact]
    public void Visibility_PrivateRejectsExplicitReceiver()
    {
        var ctx = new RubyContext();
        var c = ctx.DefineClass("Priv", ctx.ObjectClass);
        c.DefineMethod("secret", Builtin(c, "secret", (_, _, _, _) => 1L, RubyMethodVisibility.Private));
        var obj = new RubyObject(c);

        // explicit-receiver (public) call is rejected
        var ex = Assert.Throws<RubyRaiseException>(
            () => ctx.SendPublic(obj, "secret", System.Array.Empty<object?>()));
        Assert.Equal("private method 'secret' called for an instance of Priv", ex.RubyException.Message);

        // implicit-self call is allowed
        Assert.Equal(1L, ctx.Send(obj, "secret", System.Array.Empty<object?>(), flags: RubyCallFlags.ImplicitSelf));
    }

    [Fact]
    public void Visibility_ProtectedAllowsSameKindCaller()
    {
        var ctx = new RubyContext();
        var c = ctx.DefineClass("Prot", ctx.ObjectClass);
        c.DefineMethod("cmp", Builtin(c, "cmp", (_, _, _, _) => true, RubyMethodVisibility.Protected));
        var a = new RubyObject(c);
        var b = new RubyObject(c);
        // caller is kind_of the owner -> allowed
        Assert.Equal(true, ctx.SendPublic(a, "cmp", System.Array.Empty<object?>(), callerSelf: b));
        // caller is not kind_of the owner -> rejected
        Assert.Throws<RubyRaiseException>(
            () => ctx.SendPublic(a, "cmp", System.Array.Empty<object?>(), callerSelf: 5L));
    }

    [Fact]
    public void Arity_BuiltinWrongCountRaisesArgumentError()
    {
        var ctx = new RubyContext();
        var c = ctx.DefineClass("Ar", ctx.ObjectClass);
        c.DefineMethod("one", Builtin(c, "one", (_, _, _, _) => null, min: 1, max: 1));
        var obj = new RubyObject(c);
        var ex = Assert.Throws<RubyRaiseException>(
            () => ctx.Send(obj, "one", new object?[] { 1L, 2L }));
        Assert.Same(ctx.ArgumentErrorClass, ex.RubyException.RubyClass);
        Assert.Equal("wrong number of arguments (given 2, expected 1)", ex.RubyException.Message);
    }

    [Fact]
    public void RespondTo_PublicOnlyByDefault()
    {
        var ctx = new RubyContext();
        var c = ctx.DefineClass("Rt", ctx.ObjectClass);
        c.DefineMethod("pub", Builtin(c, "pub", (_, _, _, _) => null));
        c.DefineMethod("priv", Builtin(c, "priv", (_, _, _, _) => null, RubyMethodVisibility.Private));
        var obj = new RubyObject(c);
        Assert.True(ctx.RespondTo(obj, "pub"));
        Assert.False(ctx.RespondTo(obj, "priv"));
        Assert.True(ctx.RespondTo(obj, "priv", includeAll: true));
        Assert.False(ctx.RespondTo(obj, "absent"));
    }
}
