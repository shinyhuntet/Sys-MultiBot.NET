namespace SysBot.Pokemon.SWSH.RNG;

public class Xoroshiro : IRNG
{
    private ulong state0;
    private ulong state1;
    public Xoroshiro(ulong seed)
    {
        state0 = seed; 
        state1 = 0x82A2B175229D6A5B;
    }
    public ulong NextUlong()
    {
        ulong s0 = state0; 
        ulong s1 = state1;
        ulong result = s0 + s1; 
        s1 ^= s0; 
        state0 = RotateLeft(s0, 24) ^ s1 ^ (s1 << 16); 
        state1 = RotateLeft(s1, 37); 
        return result;
    }
    public override uint Next()
    {
        return (uint)NextUlong();
    }
    private static ulong RotateLeft(ulong num, int shift)
    {
        return (num << shift) | (num >> (64 - shift));
    }
}
