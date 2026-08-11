namespace SysBot.Pokemon.SWSH.RNG;

public class MersenneTwister : IRNG
{
    private readonly uint[] mt = new uint[624]; 
    private ushort index;
    public MersenneTwister(uint seed = 0)
    {
        mt[0] = seed;
        for (index = 1; index < 624; index++)
        {
            seed = 0x6C078965 * (seed ^ (seed >> 30)) + (uint)index; 
            mt[index] = seed;
        }
    }
    public override uint Next()
    {
        if (index >= 624)
        {
            Shuffle();
        }
        uint y = mt[index++]; 
        y ^= (y >> 11); 
        y ^= (y << 7) & 0x9D2C5680; 
        y ^= (y << 15) & 0xEFC60000; 
        y ^= (y >> 18); 
        return y;
    }
    private void Shuffle()
    {
        uint mt1 = mt[0], mt2;
        for (ushort i = 0; i < 227; i++)
        {
            mt2 = mt[i + 1]; 
            uint y = (mt1 & 0x80000000) | (mt2 & 0x7fffffff); 
            uint y1 = y >> 1;
            if ((y & 1) != 0)
            {
                y1 ^= 0x9908B0DF;
            }
            mt[i] = y1 ^ mt[i + 397]; mt1 = mt2;
        }
        for (ushort i = 227; i < 623; i++)
        {
            mt2 = mt[i + 1]; 
            uint y = (mt1 & 0x80000000) | (mt2 & 0x7fffffff); 
            uint y1 = y >> 1;
            if ((y & 1) != 0)
            {
                y1 ^= 0x9908B0DF;
            }
            mt[i] = y1 ^ mt[i - 227]; mt1 = mt2;
        }
        uint finalY = (mt1 & 0x80000000) | (mt[0] & 0x7fffffff); 
        uint finalY1 = finalY >> 1;
        if ((finalY & 1) != 0)
        {
            finalY1 ^= 0x9908B0DF;
        }
        mt[623] = finalY1 ^ mt[396]; index -= 624;
    }
}
