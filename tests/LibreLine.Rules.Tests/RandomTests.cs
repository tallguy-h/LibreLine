// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Engine;

namespace LibreLine.Rules.Tests;

public sealed class RandomTests
{
    [Fact]
    public void MatchesMinstdRand()
    {
        // The C++ standard: the 10000th value of a default-constructed minstd_rand (seed 1) is 399268537.
        var random = new MinStdRandom(1);
        uint value = 0;
        for (var i = 0; i < 10000; i++)
        {
            value = random.Next();
        }

        Assert.Equal(399268537u, value);
    }

    [Fact]
    public void FirstValueFromSeedOne()
    {
        Assert.Equal(48271u, new MinStdRandom(1).Next());
    }

    [Fact]
    public void SeedZeroBehavesLikeOne()
    {
        Assert.Equal(new MinStdRandom(1).Next(), new MinStdRandom(0).Next());
    }

    [Fact]
    public void SeedIsTakenModuloTheModulus()
    {
        // 2^31 − 1 is the modulus itself, so it becomes 0 and then 1; a negative seed is read as unsigned.
        Assert.Equal(new MinStdRandom(1).Next(), new MinStdRandom(int.MaxValue).Next());
        Assert.Equal(new MinStdRandom(5).Next(), new MinStdRandom(unchecked((int)(0x7fffffffu + 5u))).Next());
    }

    [Fact]
    public void ASingleNumberRangeDrawsNothing()
    {
        var random = new MinStdRandom(42);
        Assert.Equal(7, random.Uniform(7, 7));
        Assert.Equal(0, random.Draws);
    }

    [Fact]
    public void UniformUsesBuckets()
    {
        // r = 9: bucket = 0x7ffffffe / 10; the first raw value from seed 1 is 48271.
        var random = new MinStdRandom(1);
        Assert.Equal((48271 - 1) / (0x7ffffffe / 10), random.Uniform(0, 9));
    }

    [Fact]
    public void UniformStaysInRange()
    {
        var random = new MinStdRandom(12345);
        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(random.Uniform(3, 8), 3, 8);
        }
    }
}
