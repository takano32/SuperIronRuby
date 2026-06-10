using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Runtime;

// MRI-verified ancestor linearization. Each case is annotated with the ruby
// 4.0.2 one-liner whose output it reproduces. A small hierarchy is built by
// hand (no RubyContext needed) rooted at a BasicObject/Object/Kernel triad that
// mirrors MRI's bottom of the chain.
public class AncestorsTests
{
    private static (RubyClass basic, RubyClass obj, RubyModule kernel) Roots()
    {
        var basic = new RubyClass("BasicObject", null);
        var kernel = new RubyModule { Name = "Kernel" };
        var obj = new RubyClass("Object", basic);
        obj.Include(kernel);
        return (basic, obj, kernel);
    }

    private static string[] Names(IReadOnlyList<RubyModule> mods)
        => mods.Select(m => m.Name!).ToArray();

    [Fact]
    public void Object_Ancestors()
    {
        // ruby: p Object.ancestors  => [Object, Kernel, BasicObject]
        var (_, obj, _) = Roots();
        Assert.Equal(new[] { "Object", "Kernel", "BasicObject" }, Names(obj.Ancestors));
    }

    [Fact]
    public void Include_PlacesModuleAfterSelf()
    {
        // ruby: module M;end; class A;end; class B<A; include M; end
        //       p B.ancestors => [B, M, A, Object, Kernel, BasicObject]
        var (_, obj, _) = Roots();
        var m = new RubyModule { Name = "M" };
        var a = new RubyClass("A", obj);
        var b = new RubyClass("B", a);
        b.Include(m);
        Assert.Equal(new[] { "B", "M", "A", "Object", "Kernel", "BasicObject" }, Names(b.Ancestors));
    }

    [Fact]
    public void Prepend_PlacesModuleBeforeSelf_AndIncludeDedupsViaSuper()
    {
        // ruby: module M;end; module N;end; class A;end
        //       class B<A; include M; end
        //       class C<B; prepend N; include M; end
        //       p C.ancestors => [N, C, B, M, A, Object, Kernel, BasicObject]
        var (_, obj, _) = Roots();
        var m = new RubyModule { Name = "M" };
        var n = new RubyModule { Name = "N" };
        var a = new RubyClass("A", obj);
        var b = new RubyClass("B", a);
        b.Include(m);
        var c = new RubyClass("C", b);
        c.Prepend(n);
        c.Include(m); // already present via B -> no separate entry
        Assert.Equal(new[] { "N", "C", "B", "M", "A", "Object", "Kernel", "BasicObject" },
            Names(c.Ancestors));
    }

    [Fact]
    public void NestedInclude_SplicesIncludedModulesOwnAncestors()
    {
        // ruby: module P1;end; module P2; include P1; end; class D; include P2; end
        //       p D.ancestors => [D, P2, P1, Object, Kernel, BasicObject]
        var (_, obj, _) = Roots();
        var p1 = new RubyModule { Name = "P1" };
        var p2 = new RubyModule { Name = "P2" };
        p2.Include(p1);
        var d = new RubyClass("D", obj);
        d.Include(p2);
        Assert.Equal(new[] { "D", "P2", "P1", "Object", "Kernel", "BasicObject" },
            Names(d.Ancestors));
    }

    [Fact]
    public void RepeatedInclude_IsNoOp_NewerIncludesComeFirst()
    {
        // ruby: class E; include M; include N; include M; end
        //       p E.ancestors => [E, N, M, Object, Kernel, BasicObject]
        var (_, obj, _) = Roots();
        var m = new RubyModule { Name = "M" };
        var n = new RubyModule { Name = "N" };
        var e = new RubyClass("E", obj);
        e.Include(m);
        e.Include(n);
        e.Include(m);
        Assert.Equal(new[] { "E", "N", "M", "Object", "Kernel", "BasicObject" }, Names(e.Ancestors));
    }

    [Fact]
    public void MultiplePrepends_NewestIsFrontmost()
    {
        // ruby: module X;end; module Y;end; class F; prepend X; prepend Y; end
        //       p F.ancestors => [Y, X, F, Object, Kernel, BasicObject]
        var (_, obj, _) = Roots();
        var x = new RubyModule { Name = "X" };
        var y = new RubyModule { Name = "Y" };
        var f = new RubyClass("F", obj);
        f.Prepend(x);
        f.Prepend(y);
        Assert.Equal(new[] { "Y", "X", "F", "Object", "Kernel", "BasicObject" }, Names(f.Ancestors));
    }

    // ---- method resolution over ancestors ---------------------------------

    [Fact]
    public void LookupMethod_WalksAncestorsAndPrependWins()
    {
        var (_, obj, _) = Roots();
        var m = new RubyModule { Name = "M" };
        var g = new RubyClass("G", obj);
        g.DefineMethod("foo", RubyMethodInfo.FromBuiltin("foo", g, (_, _, _, _) => "from G"));
        m.DefineMethod("foo", RubyMethodInfo.FromBuiltin("foo", m, (_, _, _, _) => "from M"));
        g.Prepend(m); // prepended module resolves before the class's own method

        var resolved = g.LookupMethod("foo");
        Assert.NotNull(resolved);
        Assert.Same(m, resolved!.Owner);
    }

    [Fact]
    public void AliasMethod_CopiesResolvedMethodUnderNewName()
    {
        var (_, obj, _) = Roots();
        var h = new RubyClass("H", obj);
        h.DefineMethod("orig", RubyMethodInfo.FromBuiltin("orig", h, (_, _, _, _) => 42L));
        Assert.True(h.AliasMethod("aka", "orig"));
        var aka = h.GetOwnMethod("aka");
        Assert.NotNull(aka);
        Assert.Equal(42L, aka!.Builtin!(null!, null, System.Array.Empty<object?>(), null));
    }

    [Fact]
    public void Constants_WalkAncestors()
    {
        var (_, obj, _) = Roots();
        var k = new RubyClass("K", obj);
        obj.SetConstant("FOO", 1L);
        Assert.True(k.TryGetConstant("FOO", out var v));
        Assert.Equal(1L, v);
        Assert.False(k.TryGetConstant("MISSING", out _));
    }

    [Fact]
    public void AncestorCache_InvalidatesOnInclude()
    {
        var (_, obj, _) = Roots();
        var c = new RubyClass("C2", obj);
        Assert.Equal(new[] { "C2", "Object", "Kernel", "BasicObject" }, Names(c.Ancestors));
        var late = new RubyModule { Name = "Late" };
        c.Include(late);
        Assert.Equal(new[] { "C2", "Late", "Object", "Kernel", "BasicObject" }, Names(c.Ancestors));
    }
}
