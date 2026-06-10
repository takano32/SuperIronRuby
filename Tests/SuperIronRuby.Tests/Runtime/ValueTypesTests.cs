using System.Numerics;
using SuperIronRuby.Runtime;

namespace SuperIronRuby.Tests.Runtime;

// Ground truth for the Ruby semantics asserted here was checked against
// ruby 4.0.2 (see comments). These cover task R1's value representation.
public class ValueTypesTests
{
    // -- MutableString -------------------------------------------------------

    [Fact]
    public void MutableString_EqualityIsByContent()
    {
        var a = new MutableString("hello");
        var b = new MutableString("hello");
        Assert.NotSame(a, b);
        Assert.Equal(a, b);                       // content equality
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void MutableString_AppendMutatesContent()
    {
        var s = new MutableString("ab");
        s.Append("cd");
        Assert.Equal("abcd", s.Value);
    }

    [Fact]
    public void MutableString_FrozenStringRejectsMutation()
    {
        var s = new MutableString("x").Freeze();
        Assert.True(s.IsFrozen);
        Assert.Throws<InvalidOperationException>(() => s.Append("y"));
    }

    [Fact]
    public void MutableString_DuplicateIsUnfrozenCopy()
    {
        var s = new MutableString("x").Freeze();
        var d = s.Duplicate();
        Assert.False(d.IsFrozen);
        Assert.Equal("x", d.Value);
        d.Append("y");                            // does not throw
        Assert.Equal("xy", d.Value);
    }

    // -- RubyArray -----------------------------------------------------------

    [Fact]
    public void RubyArray_BehavesAsList()
    {
        var a = new RubyArray { 1L, "two", null };
        Assert.Equal(3, a.Count);
        Assert.Equal(1L, a[0]);
        Assert.Null(a[2]);
        a.Add(4L);
        Assert.Equal(4L, a[3]);
    }

    [Fact]
    public void RubyArray_FromSequence()
    {
        var a = new RubyArray(new object?[] { 1L, 2L, 3L });
        Assert.Equal(new object?[] { 1L, 2L, 3L }, a);
    }

    // -- RubyHash: eql? key semantics ---------------------------------------

    [Fact]
    public void RubyHash_IntegerAndFloatAreDistinctKeys()
    {
        // ruby: {}.tap{|h| h[1]=:a; h[1.0]=:b}.size == 2
        var h = new RubyHash();
        h.Store(1L, "a");
        h.Store(1.0, "b");
        Assert.Equal(2, h.Count);
        Assert.Equal("a", h.GetOrNull(1L));
        Assert.Equal("b", h.GetOrNull(1.0));
    }

    [Fact]
    public void RubyHash_StringKeysCompareByContent()
    {
        // ruby: {}.tap{|h| h["x"]=1; h["x".dup]=2}.size == 1
        var h = new RubyHash();
        h.Store(new MutableString("x"), 1L);
        h.Store(new MutableString("x"), 2L);
        Assert.Equal(1, h.Count);
        Assert.Equal(2L, h.GetOrNull(new MutableString("x")));
    }

    [Fact]
    public void RubyHash_LongAndEqualBigIntegerAreSameKey()
    {
        // Both are Ruby Integer -> eql?; must collide to one entry.
        var h = new RubyHash();
        h.Store(5L, "a");
        h.Store(new BigInteger(5), "b");
        Assert.Equal(1, h.Count);
        Assert.Equal("b", h.GetOrNull(5L));
    }

    [Fact]
    public void RubyHash_PreservesInsertionOrder()
    {
        // ruby: {b:1, a:2}.keys == [:b, :a]
        var h = new RubyHash();
        var b = new RubySymbol("b");
        var a = new RubySymbol("a");
        h.Store(b, 1L);
        h.Store(a, 2L);
        Assert.Equal(new object?[] { b, a }, h.Keys().ToArray());
    }

    [Fact]
    public void RubyHash_ReassignKeepsPosition()
    {
        var h = new RubyHash();
        h.Store(1L, "a");
        h.Store(2L, "b");
        h.Store(1L, "A");                         // update, not move
        Assert.Equal(new object?[] { 1L, 2L }, h.Keys().ToArray());
        Assert.Equal("A", h.GetOrNull(1L));
    }

    [Fact]
    public void RubyHash_NilIsALegalKey()
    {
        var h = new RubyHash();
        h.Store(null, "nilval");
        Assert.True(h.ContainsKey(null));
        Assert.Equal("nilval", h.GetOrNull(null));
        Assert.Equal(1, h.Count);
    }

    [Fact]
    public void RubyHash_RemoveUnlinksEntry()
    {
        var h = new RubyHash();
        h.Store(1L, "a");
        h.Store(2L, "b");
        h.Store(3L, "c");
        Assert.True(h.Remove(2L, out var removed));
        Assert.Equal("b", removed);
        Assert.Equal(new object?[] { 1L, 3L }, h.Keys().ToArray());
        Assert.False(h.Remove(99L, out _));
    }

    [Fact]
    public void RubyHash_ArrayKeysCompareByValue()
    {
        // ruby: {[1,2]=>:x}[[1,2]] == :x
        var h = new RubyHash();
        h.Store(new RubyArray { 1L, 2L }, "x");
        Assert.Equal("x", h.GetOrNull(new RubyArray { 1L, 2L }));
    }

    // -- Unwind exceptions ---------------------------------------------------

    [Fact]
    public void BreakUnwind_CarriesValueAndSourceId()
    {
        var ex = new BreakUnwind(42L, 7);
        Assert.Equal(42L, ex.Value);
        Assert.Equal(7, ex.SourceId);
    }

    [Fact]
    public void RubyRange_HoldsBounds()
    {
        var r = new RubyRange(1L, 5L, excludeEnd: true);
        Assert.Equal(1L, r.Begin);
        Assert.Equal(5L, r.End);
        Assert.True(r.ExcludeEnd);
    }
}
