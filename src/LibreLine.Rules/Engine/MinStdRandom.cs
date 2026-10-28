// SPDX-License-Identifier: GPL-2.0-only
namespace LibreLine.Rules.Engine;

/// <summary>
/// The game's random numbers: <c>minstd_rand</c> (multiplier 48271, modulus 2^31 − 1) and its way of picking a
/// number in a range. Every client and the server rebuild the same decks from <c>pb.Settings.seed</c>.
/// </summary>
public sealed class MinStdRandom
{
    private const uint Modulus = 0x7fffffff;
    private const ulong Multiplier = 48271;

    private uint state;

    public MinStdRandom(int seed)
    {
        state = unchecked((uint)seed) % Modulus;
        if (state == 0)
        {
            state = 1;
        }
    }

    /// <summary>A copy that continues the same sequence.</summary>
    public MinStdRandom Clone() => (MinStdRandom)MemberwiseClone();

    /// <summary>How many numbers have been drawn so far.</summary>
    public long Draws { get; private set; }

    /// <summary>The next raw value, in [1, 2^31 − 2].</summary>
    public uint Next()
    {
        state = (uint)(state * Multiplier % Modulus);
        Draws++;
        return state;
    }

    /// <summary>A number in [lo, hi]. Draws nothing when the range holds a single number.</summary>
    public int Uniform(int lo, int hi)
    {
        if (hi < lo)
        {
            throw new ArgumentOutOfRangeException(nameof(hi), "empty range");
        }

        var range = (uint)(hi - lo);
        if (range == 0)
        {
            return lo;
        }

        var bucket = 0x7ffffffeu / (range + 1);
        uint value;
        do
        {
            value = (Next() - 1) / bucket;
        }
        while (value > range);

        return lo + (int)value;
    }
}
