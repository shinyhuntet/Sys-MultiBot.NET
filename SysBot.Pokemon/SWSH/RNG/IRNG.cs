using System;

namespace SysBot.Pokemon.SWSH.RNG;

public abstract class IRNG
{
    public abstract uint Next();
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
        } 
        while (inclusiveMax <= rand); 
        return min + rand;
    }
    public uint Next(uint max, uint mask)
    {
        return Next(0, max, mask);
    }
    public uint Next(uint max)
    {
        uint mask = (uint)(Math.Pow(2, 32 - LeadingZeroCount(max)) - 1); 
        return Next(0, max, mask);
    }
    private static int LeadingZeroCount(uint value)
    {
        if (value == 0) 
            return 32; 
        int count = 0; 
        if (value <= 0x0000FFFF) { count += 16; value <<= 16; }
        if (value <= 0x00FFFFFF) { count += 8; value <<= 8; }
        if (value <= 0x0FFFFFFF) { count += 4; value <<= 4; }
        if (value <= 0x3FFFFFFF) { count += 2; value <<= 2; }
        if (value <= 0x7FFFFFFF) { count += 1; }
        return count;
    }
}

