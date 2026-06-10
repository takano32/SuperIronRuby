using SuperIronRuby.Runtime;
using SuperIronRuby.Builtins;

namespace SuperIronRuby.Tests.Runtime;

// End-to-end runtime exercise WITHOUT the parser/interpreter: build a class
// graph through the runtime API and drive it via Send. Proves R1-R7 compose.
public class SendIntegrationTests
{
    private static RubyContext Loaded()
    {
        var ctx = new RubyContext();
        BuiltinLoader.LoadAssembly(ctx, typeof(KernelOps).Assembly);
        return ctx;
    }

    private static object?[] A(params object?[] xs) => xs;

    [Fact]
    public void BuildClass_IncludeModule_DispatchVisibilityMethodMissingExceptions()
    {
        var ctx = Loaded();

        // module Greeting; def hello; "hi"; end; end
        var greeting = ctx.DefineModule("Greeting");
        greeting.DefineMethod("hello", RubyMethodInfo.FromBuiltin("hello", greeting,
            (_, _, _, _) => new MutableString("hi")));

        // class Animal; attr_accessor :name; include Greeting
        //   def to_s; "Animal(#{@name})"; end
        //   private def secret; 42; end
        //   def method_missing(n, *a); "mm:#{n}"; end
        // end
        var animal = ctx.DefineClass("Animal", ctx.ObjectClass);
        ctx.Send(animal, "attr_accessor", A(ctx.Intern("name")));
        ctx.Send(animal, "include", A(greeting));
        animal.DefineMethod("secret", RubyMethodInfo.FromBuiltin("secret", animal,
            (_, _, _, _) => 42L, RubyMethodVisibility.Private));
        animal.DefineMethod("method_missing", RubyMethodInfo.FromBuiltin("method_missing", animal,
            (c, _, a, _) => new MutableString("mm:" + KernelOps.NameOf(a[0]))));

        var obj = ctx.Send(animal, "new", A());

        // attr roundtrip + included module method
        ctx.Send(obj, "name=", A(new MutableString("Rex")));
        Assert.Equal("Rex", ((MutableString)ctx.Send(obj, "name", A())!).Value);
        Assert.Equal("hi", ((MutableString)ctx.Send(obj, "hello", A())!).Value);

        // ancestors include the module
        var anc = (RubyArray)ctx.Send(animal, "ancestors", A())!;
        var names = anc.Select(m => ((RubyModule)m!).Name).ToArray();
        Assert.Equal(new[] { "Animal", "Greeting", "Object", "Kernel", "BasicObject" }, names);

        // private method: implicit-self allowed, explicit receiver rejected
        Assert.Equal(42L, ctx.Send(obj, "secret", A(), flags: RubyCallFlags.ImplicitSelf));
        Assert.Throws<RubyRaiseException>(() => ctx.SendPublic(obj, "secret", A()));

        // method_missing catches the unknown call
        Assert.Equal("mm:wat", ((MutableString)ctx.Send(obj, "wat", A())!).Value);

        // exception raise + kind_of
        var raised = ctx.RaiseTypeError("bad");
        Assert.True(ctx.IsKindOf(raised.RubyException, ctx.StandardErrorClass));
    }

    [Fact]
    public void Subclass_OverridesAndSuperChain()
    {
        var ctx = Loaded();
        var baseC = ctx.DefineClass("Base", ctx.ObjectClass);
        baseC.DefineMethod("kind", RubyMethodInfo.FromBuiltin("kind", baseC, (_, _, _, _) => new MutableString("base")));
        var derived = ctx.DefineClass("Derived", baseC);
        derived.DefineMethod("kind", RubyMethodInfo.FromBuiltin("kind", derived, (_, _, _, _) => new MutableString("derived")));

        Assert.Equal("base", ((MutableString)ctx.Send(ctx.Send(baseC, "new", A()), "kind", A())!).Value);
        Assert.Equal("derived", ((MutableString)ctx.Send(ctx.Send(derived, "new", A()), "kind", A())!).Value);
        Assert.Same(baseC, ctx.Send(derived, "superclass", A()));
    }
}
