using System;
using System.Numerics;

namespace SysBot.Pokemon.SWSH.RNG;

public class Xoroshiro128PlusSWSH
{
    public const ulong XOROSHIRO_CONST0 = 0x0F4B17A579F18960;
    public const ulong XOROSHIRO_CONST = 0x82A2B175229D6A5B;

    private ulong s0;
    private ulong s1;

    public Xoroshiro128PlusSWSH(ulong s0 = XOROSHIRO_CONST0, ulong s1 = XOROSHIRO_CONST) => (this.s0, this.s1) = (s0, s1);
    public (ulong s0, ulong s1) GetState() => (s0, s1);
    public UInt128 FullState() => new(s1, s0);

    public ulong Nextulong()
    {
        var _s0 = s0;
        var _s1 = s1;
        ulong result = _s0 + _s1;

        _s1 ^= _s0;
        // Final calculations and store back to fields
        s0 = RotateLeft(_s0, 24) ^ _s1 ^ (_s1 << 16);
        s1 = RotateLeft(_s1, 37);

        return result;
    }

    public uint Next() => (uint)Nextulong();

    private static ulong RotateLeft(ulong num, int shift) => (num << shift) | (num >> (64 - shift));
    public uint Next(uint min, uint max, uint mask)
    {
        uint diff = max - min;
        if (diff == 0)
            return min;

        uint rand = 0;
        uint inclusiveMax = diff + 1;
        
        do
        {
            rand = Next() & mask;
        } while (inclusiveMax <= rand);

        return min + rand;            
    }

    public uint Next(uint max, uint mask) => Next(0, max, mask);

    public uint Next(uint max)// Something wrong
    {
        uint mask = GetBitmaskBeta(max);// Something wrong ?
        return Next(0, max, mask);
    }
    /// <summary>
    /// Gets the inclusive range bitmask for the specified <see cref="exclusiveMax"/> value.
    /// </summary>
    private static uint GetBitmask(uint exclusiveMax) => (uint)(1 << (32 - BitOperations.LeadingZeroCount(exclusiveMax))) - 1;

    private static uint GetBitmaskBeta(uint max) => (uint)(Math.Pow(2, 32 - LeadingZeroCount(max)) - 1);

    private static int LeadingZeroCount(uint value)
    {
        if (value == 0) return 32; 
        int count = 0; 
        if (value <= 0x0000FFFF) { count += 16; value <<= 16; }
        if (value <= 0x00FFFFFF) { count += 8; value <<= 8; }
        if (value <= 0x0FFFFFFF) { count += 4; value <<= 4; }
        if (value <= 0x3FFFFFFF) { count += 2; value <<= 2; }
        if (value <= 0x7FFFFFFF) { count += 1; }
        return count;
    }

}
