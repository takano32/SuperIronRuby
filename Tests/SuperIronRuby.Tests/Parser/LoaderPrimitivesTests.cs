using System.Numerics;
using SuperIronRuby.Parser;

namespace SuperIronRuby.Tests.Parser;

// Hand-computed byte sequences for Prism's LEB128 primitives (serialize.rb
// load_varuint/load_varsint/load_integer).
public class LoaderPrimitivesTests
{
    private static Loader OnBytes(params byte[] data) => new(data, System.Array.Empty<byte>());

    [Theory]
    [InlineData(0UL, new byte[] { 0x00 })]
    [InlineData(1UL, new byte[] { 0x01 })]
    [InlineData(127UL, new byte[] { 0x7F })]
    [InlineData(128UL, new byte[] { 0x80, 0x01 })]
    [InlineData(300UL, new byte[] { 0xAC, 0x02 })]
    [InlineData(16384UL, new byte[] { 0x80, 0x80, 0x01 })]
    public void VarUInt(ulong expected, byte[] bytes)
    {
        Assert.Equal(expected, OnBytes(bytes).LoadVarUInt());
    }

    [Theory]
    [InlineData(0, new byte[] { 0x00 })]   // zigzag(0)=0
    [InlineData(-1, new byte[] { 0x01 })]  // zigzag(1)=-1
    [InlineData(1, new byte[] { 0x02 })]   // zigzag(2)=1
    [InlineData(-2, new byte[] { 0x03 })]
    [InlineData(2, new byte[] { 0x04 })]
    [InlineData(64, new byte[] { 0x80, 0x01 })] // zigzag(128)=64
    public void VarSInt(int expected, byte[] bytes)
    {
        Assert.Equal(expected, OnBytes(bytes).LoadVarSInt());
    }

    [Fact]
    public void Integer_SmallPositive()
    {
        // negative=0, length=1, word=42
        var v = OnBytes(0x00, 0x01, 0x2A).LoadInteger();
        Assert.Equal(42L, v);
    }

    [Fact]
    public void Integer_Negative()
    {
        // negative=1, length=1, word=7  => -7
        var v = OnBytes(0x01, 0x01, 0x07).LoadInteger();
        Assert.Equal(-7L, v);
    }

    [Fact]
    public void Integer_TwoWordsPromotesToBigInteger()
    {
        // negative=0, length=2, word0=0 (varuint 0x00), word1=1 (varuint 0x01)
        // => 0 | (1 << 32) == 4294967296
        var v = OnBytes(0x00, 0x02, 0x00, 0x01).LoadInteger();
        Assert.Equal(4294967296L, v);
    }

    [Fact]
    public void Integer_VeryLargeIsBigInteger()
    {
        // negative=0, length=3, words all 0xFFFFFFFF -> (2^96 - 1), exceeds long
        // word value 4294967295 as varuint: 0xFF 0xFF 0xFF 0xFF 0x0F
        var bytes = new List<byte> { 0x00, 0x03 };
        for (int i = 0; i < 3; i++) bytes.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F });
        var v = OnBytes(bytes.ToArray()).LoadInteger();
        Assert.IsType<BigInteger>(v);
        Assert.Equal((BigInteger.One << 96) - 1, (BigInteger)v);
    }

    [Fact]
    public void SequentialReadsAdvanceCursor()
    {
        var loader = OnBytes(0x01, 0x80, 0x01, 0x7F); // 1, 128, 127
        Assert.Equal(1UL, loader.LoadVarUInt());
        Assert.Equal(128UL, loader.LoadVarUInt());
        Assert.Equal(127UL, loader.LoadVarUInt());
        Assert.Equal(4, loader.Position);
    }
}
