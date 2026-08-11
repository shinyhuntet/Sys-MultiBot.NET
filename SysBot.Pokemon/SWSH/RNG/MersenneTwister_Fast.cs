namespace SysBot.Pokemon.SWSH.RNG;
public class MersenneTwister_Fast // Little faster version, Modified by wwwwwwzx
{
    /* Period parameters */
    private const int N = 624;
    private const int M = 397;
    private const uint MatrixA = 0x9908b0df; /* constant vector a */
    private const uint UpperMask = 0x80000000; /* most significant w-r bits */
    private const uint LowerMask = 0x7fffffff; /* least significant r bits */

    /* Tempering parameters */
    private const uint TemperingMaskB = 0x9d2c5680;
    private const uint TemperingMaskC = 0xefc60000;
    private static readonly uint[] _mag01 = { 0x0, MatrixA };
    private readonly uint[] _mt = new uint[N]; /* the array for the state vector  */
    private short _mti;

    public MersenneTwister_Fast(uint seed)
    {
        init(seed);
    }

    public uint Nextuint()
    {
        uint y;
        if (_mti >= N)
        {
            short kk = 0;
            for (; kk < N - M; ++kk)
            {
                y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                _mt[kk] = _mt[kk + M] ^ (y >> 1) ^ _mag01[y & 0x1];
            }

            for (; kk < N - 1; ++kk)
            {
                y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                _mt[kk] = _mt[kk + (M - N)] ^ (y >> 1) ^ _mag01[y & 0x1];
            }

            y = (_mt[N - 1] & UpperMask) | (_mt[0] & LowerMask);
            _mt[N - 1] = _mt[M - 1] ^ (y >> 1) ^ _mag01[y & 0x1];

            _mti = 0;
        }
        y = _mt[_mti++];
        y ^= temperingShiftU(y);
        y ^= temperingShiftS(y) & TemperingMaskB;
        y ^= temperingShiftT(y) & TemperingMaskC;
        y ^= temperingShiftL(y);
        return y;
    }

    public uint Next(uint min, uint max, uint mask)
    {
        uint diff = max- min;

        if (diff == 0)
            return min;

        uint rand = 0;
        uint inclusiveMax = diff + 1;

        do
        {
            rand = Nextuint() & mask;
        } while (inclusiveMax <= rand);

        return min + rand;
    }

    public uint Next(uint max, uint mask) => Next(0, max, mask);
    public void Next(int n)
    {
        _mti += (short)n;
        while (_mti >= N)
        {
            short kk = 0;
            uint y;
            for (; kk < N - M; ++kk)
            {
                y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                _mt[kk] = _mt[kk + M] ^ (y >> 1) ^ _mag01[y & 0x1];
            }

            for (; kk < N - 1; ++kk)
            {
                y = (_mt[kk] & UpperMask) | (_mt[kk + 1] & LowerMask);
                _mt[kk] = _mt[kk + (M - N)] ^ (y >> 1) ^ _mag01[y & 0x1];
            }

            y = (_mt[N - 1] & UpperMask) | (_mt[0] & LowerMask);
            _mt[N - 1] = _mt[M - 1] ^ (y >> 1) ^ _mag01[y & 0x1];

            _mti -= N;
        }
    }

    private static uint temperingShiftU(uint y) => (y >> 11);

    private static uint temperingShiftS(uint y) => (y << 7);

    private static uint temperingShiftT(uint y) => (y << 15);

    private static uint temperingShiftL(uint y) => (y >> 18);

    private void init(uint seed)
    {
        _mt[0] = seed;
        for (_mti = 1; _mti < N; _mti++)
            _mt[_mti] = (uint)(1812433253U * (_mt[_mti - 1] ^ (_mt[_mti - 1] >> 30)) + _mti);
    }
}

