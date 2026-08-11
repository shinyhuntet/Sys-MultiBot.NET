/* Special thanks to the RNG Researchers in the PokémonRNG community.
 * Particular credits for the RNG researches to
 * Zaksabeast, Real.96, EzPzStreamz, ShinySylveon, AdmiralFish, Lincoln-LM, Kaphotics.
 * Thanks to SciresM that allowed us to make these researches in the
 * first place with his awesome tools and knowledge.
 */

using Newtonsoft.Json;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Runtime;

namespace SysBot.Pokemon
{
    public class Xorshift
    {
        private readonly uint[] state;

        public Xorshift(ulong seed0, ulong seed1)
        {
            uint s0 = (uint)(seed0 >> 32);
            uint s1 = (uint)(seed0 & 0xFFFFFFFF);
            uint s2 = (uint)(seed1 >> 32);
            uint s3 = (uint)(seed1 & 0xFFFFFFFF);
            this.state = new uint[] { s0, s1, s2, s3 };
        }
        public Xorshift(uint s0, uint s1, uint s2, uint s3)
		{
            this.state = new uint[] { s0, s1, s2, s3 };
        }
        public ulong[] GetU64State()
        {
            ulong s_0 = ((this.state[0] | 0xFFFFFFFF00000000) << 32) | this.state[1];
            ulong s_1 = ((this.state[2] | 0xFFFFFFFF00000000) << 32) | this.state[3];
            return new ulong[] { s_0, s_1 };
        }
        public uint[] GetU32State()
		{
            return new uint[] { this.state[0], this.state[1], this.state[2], this.state[3] };
		}
        public uint Advance(int advances)
		{
            uint seed = 0x0;
            for (int i = 0; i < advances; i++)
                seed = this.Next();
            return seed;
		}
        public uint Next()
        {
            uint t = this.state[0];
            uint s = this.state[3];

            t ^= t << 11;
            t ^= t >> 8;
            t ^= s ^ (s >> 19);

            this.state[0] = this.state[1];
            this.state[1] = this.state[2];
            this.state[2] = this.state[3];
            this.state[3] = t;

            return (t % 0xffffffff) + 0x80000000;
        }

        public uint NextUInt()
        {
            uint t = this.state[0];
            uint s = this.state[3];

            t ^= t << 11;
            t ^= t >> 8;
            t ^= s ^ (s >> 19);

            this.state[0] = this.state[1];
            this.state[1] = this.state[2];
            this.state[2] = this.state[3];
            this.state[3] = t;

            return t;
        }

        public float NextFloat()
        {
            float t = (float)((NextUInt() & 0x7fffff) / 8388607.0);
            return (float)1.0 - t;
        }


        public void Prev()
        {
            uint t = this.state[2] >> 19 ^ this.state[2] ^ this.state[3];
            t ^= t >> 8;
            t ^= t << 11 & 0xFFFFFFFF;
            t ^= t << 22 & 0xFFFFFFFF;

            this.state[0] = t;
            this.state[1] = this.state[0];
            this.state[2] = this.state[1];
            this.state[3] = this.state[2];
        }

        public byte Range(byte min = 0, byte max = 100)
		{
            var s0 = this.state[0];
            this.state[0] = this.state[1];
            this.state[1] = this.state[2];
            this.state[2] = this.state[3];

            uint tmp = s0 ^ s0 << 11;
            tmp = tmp ^ tmp >> 8 ^ this.state[2] ^ this.state[2] >> 19;

            this.state[3] = tmp;

            var diff = (byte)(max - min);

            return (byte)((tmp % diff) + min);

        }
    }
    class RNGList
    {
        ushort size = 2;
        ushort shift = 0;
        ushort head = 0;
        ushort pointer = 0;
        uint[] list;
        Xorshift rng;
        public RNGList(Xorshift rng)
        {
            this.rng = rng;
            list = new uint[size];
            foreach (uint x in list)
            {
                list[x] = this.rng.Next() >> shift;
            }
        }
        public void advanceStates(uint advances)
        {
            for (uint i = 0; i < advances; i++)
            {
                advanceState();
            }
        }
        public void advanceState()
        {
            list[head++] = rng.Next() >> shift;
            head = (ushort)(head & (size - 1));

            pointer = head;
        }
        public uint next()
        {
            uint result = list[pointer++];
            pointer %= size;

            return result;
        }
        public uint nextrand()
        {
            return (next() % 0xffffffff) + 0x80000000;
        }
        public void advance(uint advances)
        {
            pointer = (ushort)((pointer + advances) & (size - 1));
        }

        public uint getValue()
        {
            uint result = list[pointer++];
            pointer = (ushort)(pointer & (size - 1));
            return result;
        }

        public void resetState()
        {
            pointer = head;
        }
    }
    
    class XoroshiroBDSP
    {
        public XoroshiroBDSP(ulong seed)
        {
            ulong splitmix(ulong seed, ulong state)
            {
                seed += state;
                seed = 0xBF58476D1CE4E5B9 * (seed ^ (seed >> 30));
                seed = 0x94D049BB133111EB * (seed ^ (seed >> 27));
                return seed ^ (seed >> 31);
            }

            state[0] = splitmix(seed, 0x9E3779B97F4A7C15);
            state[1] = splitmix(seed, 0x3C6EF372FE94F82A);
        }
        public void advance(uint advances)
        {
            for (uint advance = 0; advance < advances; advance++)
            {
                next();
            }
        }
        public uint next()
        {
            ulong s0 = state[0];
            ulong s1 = state[1];
            ulong result = s0 + s1;

            s1 ^= s0;
            state[0] = rotl(s0, 24) ^ s1 ^ (s1 << 16);
            state[1] = rotl(s1, 37);

            return (uint)(result >> 32);
        }

        public uint next(uint max)
        {
            return next() % max;
        }

        ulong rotl(ulong x, int k)
        {
            return (x << k) | (x >> (64 - k));
        }
        private ulong[] state = new ulong[2];
    }
    public class RNG8b
    {
        private const int UNSET = -1;
        private const int MAX = 31;
        private const int N_IV = 6;
        private const int N_ABILITY = 1;
        private const int N_GENDER = 253;
        private const int N_NATURE = 25;
        bool isshinycharm;
        public struct EggMoveList
        {
            public uint specie;
            public uint count;
            public uint[] moves;

            public EggMoveList(uint val2, uint val3, uint[] val1)
            {
                this.moves = val1;
                this.specie = val2;
                this.count = val3;
            }
        };

        public EggMoveList[] eggMoveList =
        {
            new EggMoveList(12, 1, new uint[]{ 130, 80, 174, 275, 267, 204, 345, 133, 437, 438, 124, 580, 0, 0, 0, 0 }),
    new EggMoveList(13, 4, new uint[]{ 187, 246, 44, 407, 232, 68, 17, 525, 200, 251, 349, 242, 314, 0, 0, 0 }),
    new EggMoveList(12, 7, new uint[]{ 243, 114, 54, 175, 281, 252, 392, 453, 323, 791, 330, 396, 0, 0, 0, 0 }),
new EggMoveList(9, 19,new uint[]{ 103, 172, 154, 68, 179, 253, 387, 279, 515, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 23,new uint[]{ 21, 180, 251, 305, 184, 342, 50, 415, 389, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 27,new uint[]{ 68, 175, 189, 400, 232, 468, 306, 341, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(11, 29,new uint[]{ 68, 50, 342, 130, 48, 36, 305, 116, 204, 251, 599, 0, 0, 0, 0, 0 }),
new EggMoveList(13, 32,new uint[]{ 93, 68, 50, 457, 342, 389, 48, 36, 32, 37, 133, 251, 599, 0, 0, 0 }),
new EggMoveList(10, 37,new uint[]{ 95, 175, 336, 262, 608, 488, 257, 394, 384, 506, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 41,new uint[]{ 174, 16, 95, 98, 18, 17, 428, 413, 599, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(11, 43,new uint[]{ 75, 175, 235, 275, 321, 298, 267, 495, 668, 73, 204, 0, 0, 0, 0, 0 }),
new EggMoveList(13, 46,new uint[]{ 103, 68, 60, 175, 230, 232, 450, 440, 97, 73, 469, 565, 580, 0, 0, 0 }),
new EggMoveList(7, 48,new uint[]{ 226, 103, 97, 234, 390, 450, 476, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 54,new uint[]{ 499, 109, 238, 95, 60, 493, 281, 248, 227, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(6, 56,new uint[]{ 68, 179, 251, 279, 227, 400, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 58,new uint[]{ 37, 38, 234, 343, 24, 34, 83, 257, 370, 682, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(6, 60,new uint[]{ 283, 114, 54, 170, 150, 227, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 63,new uint[]{ 93, 470, 277, 227, 282, 7, 9, 8, 379, 385, 375, 502, 0, 0, 0, 0 }),
new EggMoveList(12, 66,new uint[]{ 68, 418, 321, 501, 66, 227, 370, 7, 9, 8, 379, 484, 0, 0, 0, 0 }),
new EggMoveList(13, 69,new uint[]{ 227, 235, 141, 275, 345, 388, 321, 311, 499, 438, 491, 562, 668, 0, 0, 0 }),
new EggMoveList(10, 72,new uint[]{ 367, 392, 62, 109, 114, 282, 243, 229, 321, 330, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 74,new uint[]{ 5, 335, 359, 175, 174, 475, 469, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 77,new uint[]{ 37, 24, 95, 38, 32, 234, 204, 67, 502, 667, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 79,new uint[]{ 562, 187, 335, 23, 248, 173, 472, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(13, 83,new uint[]{ 16, 98, 175, 297, 174, 343, 400, 493, 515, 364, 143, 189, 279, 0, 0, 0 }),
new EggMoveList(5, 84,new uint[]{ 48, 114, 175, 413, 372, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 86,new uint[]{ 122, 195, 50, 32, 21, 252, 333, 254, 256, 255, 562, 494, 0, 0, 0, 0 }),
new EggMoveList(13, 88,new uint[]{ 114, 212, 122, 286, 174, 325, 425, 254, 256, 255, 184, 491, 612, 0, 0, 0 }),
new EggMoveList(9, 90,new uint[]{ 61, 392, 791, 36, 229, 103, 333, 341, 350, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 92,new uint[]{ 195, 114, 310, 288, 50, 499, 123, 513, 7, 8, 9, 184, 0, 0, 0, 0 }),
new EggMoveList(9, 95,new uint[]{ 175, 335, 111, 469, 205, 525, 457, 350, 484, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 96,new uint[]{ 272, 7, 9, 8, 260, 427, 385, 471, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 98,new uint[]{ 282, 246, 359, 163, 400, 114, 133, 321, 97, 502, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 104,new uint[]{ 246, 187, 197, 24, 195, 130, 174, 43, 103, 442, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 108,new uint[]{ 562, 174, 359, 37, 34, 173, 133, 330, 428, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(11, 109,new uint[]{ 60, 220, 288, 180, 174, 254, 256, 255, 103, 390, 599, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 111,new uint[]{ 68, 174, 407, 130, 368, 470, 242, 179, 306, 423, 424, 422, 0, 0, 0, 0 }),
new EggMoveList(9, 114,new uint[]{ 93, 283, 175, 73, 267, 476, 133, 437, 384, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 116,new uint[]{ 62, 499, 50, 175, 190, 150, 330, 200, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 122,new uint[]{ 95, 252, 109, 471, 321, 248, 271, 478, 196, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(6, 123,new uint[]{ 68, 400, 364, 501, 226, 179, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(6, 127,new uint[]{ 364, 175, 37, 31, 98, 370, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(6, 147,new uint[]{ 453, 225, 245, 54, 48, 114, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 152,new uint[]{ 22, 73, 68, 246, 175, 275, 437, 505, 580, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 155,new uint[]{ 154, 179, 37, 343, 336, 306, 24, 394, 326, 267, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 158,new uint[]{ 246, 8, 232, 349, 453, 313, 335, 260, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 161,new uint[]{ 38, 163, 116, 271, 387, 204, 343, 608, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 163,new uint[]{ 48, 17, 18, 297, 101, 143, 97, 212, 542, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 165,new uint[]{ 60, 103, 227, 282, 450, 366, 68, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 167,new uint[]{ 60, 50, 226, 390, 476, 400, 224, 679, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 170,new uint[]{ 60, 54, 487, 103, 133, 250, 97, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 175,new uint[]{ 217, 64, 375, 326, 234, 248, 500, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 177,new uint[]{ 65, 98, 297, 389, 493, 114, 428, 502, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 179,new uint[]{ 34, 103, 260, 28, 495, 97, 598, 604, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 187,new uint[]{ 93, 227, 38, 133, 270, 312, 538, 402, 580, 668, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 190,new uint[]{ 68, 180, 21, 251, 252, 343, 340, 279, 415, 501, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 191,new uint[]{ 227, 267, 174, 270, 230, 234, 580, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(15, 194,new uint[]{ 246, 174, 254, 256, 255, 68, 24, 105, 495, 491, 612, 34, 227, 385, 598, 0 }),
new EggMoveList(10, 198,new uint[]{ 18, 65, 143, 109, 297, 195, 375, 103, 413, 260, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 200,new uint[]{ 103, 194, 286, 262, 389, 425, 174, 472, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 203,new uint[]{ 36, 133, 248, 251, 273, 277, 24, 243, 212, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 204,new uint[]{ 42, 175, 129, 68, 328, 279, 390, 379, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 207,new uint[]{ 232, 17, 68, 328, 97, 226, 38, 364, 400, 440, 379, 342, 0, 0, 0, 0 }),
new EggMoveList(8, 209,new uint[]{ 118, 217, 215, 173, 370, 38, 102, 313, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 211,new uint[]{ 175, 114, 61, 48, 310, 453, 491, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 213,new uint[]{ 230, 282, 367, 51, 515, 111, 611, 343, 270, 328, 189, 350, 0, 0, 0, 0 }),
new EggMoveList(11, 215,new uint[]{ 68, 180, 44, 252, 458, 420, 364, 556, 306, 8, 675, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 216,new uint[]{ 242, 36, 69, 68, 232, 281, 238, 38, 370, 400, 187, 583, 0, 0, 0, 0 }),
new EggMoveList(11, 218,new uint[]{ 151, 257, 174, 108, 262, 254, 255, 256, 205, 517, 385, 0, 0, 0, 0, 0 }),
new EggMoveList(11, 220,new uint[]{ 44, 246, 38, 90, 174, 556, 573, 34, 341, 333, 341, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 222,new uint[]{ 54, 109, 267, 174, 457, 103, 133, 275, 333, 710, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 223,new uint[]{ 190, 48, 114, 175, 323, 491, 103, 350, 173, 341, 129, 494, 0, 0, 0, 0 }),
new EggMoveList(15, 225,new uint[]{ 62, 98, 150, 229, 420, 252, 573, 194, 68, 694, 262, 248, 8, 196, 191, 0 }),
new EggMoveList(8, 228,new uint[]{ 83, 68, 180, 179, 422, 364, 389, 194, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(13, 231,new uint[]{ 116, 34, 246, 173, 68, 90, 283, 420, 457, 189, 484, 583, 667, 0, 0, 0 }),
new EggMoveList(8, 236,new uint[]{ 229, 136, 183, 170, 68, 410, 418, 364, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(3, 238,new uint[]{ 252, 273, 272, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(6, 239,new uint[]{ 238, 223, 359, 364, 7, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(11, 240,new uint[]{ 562, 223, 183, 238, 5, 103, 9, 394, 187, 116, 384, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 241,new uint[]{ 562, 174, 38, 359, 217, 69, 179, 270, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 246,new uint[]{ 23, 246, 174, 200, 116, 349, 334, 372, 442, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 252,new uint[]{ 71, 163, 400, 24, 225, 73, 388, 235, 242, 306, 345, 580, 0, 0, 0, 0 }),
new EggMoveList(10, 255,new uint[]{ 64, 306, 174, 68, 364, 387, 400, 226, 97, 67, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(13, 258,new uint[]{ 189, 44, 246, 68, 174, 38, 243, 124, 23, 469, 281, 253, 250, 0, 0, 0 }),
new EggMoveList(7, 261,new uint[]{ 310, 305, 343, 43, 423, 424, 422, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 263,new uint[]{ 321, 493, 245, 204, 271, 270, 189, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 270,new uint[]{ 235, 75, 230, 73, 321, 68, 298, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 273,new uint[]{ 73, 98, 36, 388, 400, 133, 384, 492, 251, 580, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 278,new uint[]{ 239, 16, 392, 282, 487, 469, 314, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(11, 280,new uint[]{ 50, 212, 262, 194, 288, 425, 109, 282, 227, 581, 502, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 283,new uint[]{ 341, 60, 56, 170, 450, 565, 471, 679, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(5, 290,new uint[]{ 16, 400, 450, 515, 175, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(11, 300,new uint[]{ 270, 253, 313, 273, 226, 321, 387, 428, 389, 493, 322, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 302,new uint[]{ 105, 260, 364, 389, 368, 236, 271, 286, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 303,new uint[]{ 246, 321, 21, 69, 612, 305, 423, 424, 422, 385, 368, 581, 0, 0, 0, 0 }),
new EggMoveList(10, 304,new uint[]{ 174, 407, 283, 457, 23, 189, 34, 103, 276, 179, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(11, 307,new uint[]{ 7, 9, 8, 252, 226, 223, 384, 385, 427, 418, 501, 0, 0, 0, 0, 0 }),
new EggMoveList(4, 313,new uint[]{ 226, 271, 69, 679, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(5, 314,new uint[]{ 226, 74, 313, 109, 312, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(11, 315,new uint[]{ 178, 79, 75, 326, 791, 191, 42, 170, 437, 402, 438, 0, 0, 0, 0, 0 }),
new EggMoveList(6, 318,new uint[]{ 246, 194, 38, 37, 56, 129, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(14, 322,new uint[]{ 336, 184, 34, 205, 111, 23, 246, 257, 254, 256, 255, 442, 74, 484, 0, 0 }),
new EggMoveList(6, 324,new uint[]{ 281, 90, 130, 175, 246, 276, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 328,new uint[]{ 98, 16, 175, 210, 450, 364, 116, 341, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(14, 331,new uint[]{ 51, 298, 223, 68, 67, 345, 402, 50, 335, 388, 415, 565, 562, 612, 0, 0 }),
new EggMoveList(9, 333,new uint[]{ 407, 297, 114, 366, 310, 97, 384, 304, 583, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(13, 335,new uint[]{ 175, 24, 68, 174, 154, 400, 232, 458, 50, 515, 364, 501, 187, 0, 0, 0 }),
new EggMoveList(8, 336,new uint[]{ 254, 256, 255, 34, 184, 372, 415, 515, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 339,new uint[]{ 37, 209, 175, 36, 250, 56, 349, 414, 341, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 341,new uint[]{ 246, 232, 38, 453, 415, 163, 34, 276, 349, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(5, 352,new uint[]{ 277, 271, 252, 105, 612, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(6, 353,new uint[]{ 50, 194, 310, 286, 109, 441, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(6, 355,new uint[]{ 220, 288, 262, 114, 286, 194, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(6, 357,new uint[]{ 29, 21, 73, 267, 174, 348, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(12, 359,new uint[]{ 174, 38, 277, 212, 44, 364, 226, 428, 372, 224, 506, 583, 0, 0, 0, 0 }),
new EggMoveList(9, 361,new uint[]{ 335, 50, 415, 205, 556, 191, 311, 506, 313, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 363,new uint[]{ 174, 90, 254, 256, 255, 281, 392, 187, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(5, 366,new uint[]{ 34, 48, 109, 392, 330, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 371,new uint[]{ 111, 407, 37, 239, 56, 349, 424, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(16, 387,new uint[]{ 388, 321, 34, 38, 328, 402, 37, 133, 276, 254, 256, 255, 414, 469, 580, 484 }),
new EggMoveList(12, 390,new uint[]{ 7, 9, 24, 227, 257, 116, 270, 252, 299, 68, 501, 66, 0, 0, 0, 0 }),
new EggMoveList(10, 393,new uint[]{ 458, 48, 281, 189, 173, 175, 97, 392, 297, 196, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(5, 399,new uint[]{ 98, 38, 154, 401, 130, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(13, 403,new uint[]{ 608, 36, 24, 336, 400, 98, 423, 424, 422, 129, 270, 598, 313, 0, 0, 0 }),
new EggMoveList(11, 406,new uint[]{ 235, 178, 79, 75, 326, 791, 191, 42, 170, 437, 402, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 417,new uint[]{ 343, 44, 313, 111, 205, 260, 175, 39, 266, 268, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 418,new uint[]{ 189, 29, 154, 163, 210, 226, 392, 415, 487, 270, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(13, 420,new uint[]{ 75, 230, 321, 267, 312, 505, 361, 111, 579, 205, 311, 402, 580, 0, 0, 0 }),
new EggMoveList(13, 422,new uint[]{ 68, 243, 254, 256, 255, 281, 174, 124, 499, 54, 151, 133, 90, 0, 0, 0 }),
new EggMoveList(16, 427,new uint[]{ 509, 383, 458, 252, 175, 186, 415, 298, 313, 227, 67, 9, 8, 7, 322, 612 }),
new EggMoveList(8, 431,new uint[]{ 44, 39, 98, 28, 313, 372, 175, 387, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 433,new uint[]{ 50, 174, 95, 273, 248, 105, 500, 322, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 434,new uint[]{ 43, 123, 38, 310, 114, 163, 242, 184, 492, 583, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(9, 443,new uint[]{ 37, 232, 38, 239, 200, 184, 34, 442, 341, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 446,new uint[]{ 38, 174, 90, 68, 562, 204, 18, 428, 495, 120, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 449,new uint[]{ 254, 256, 255, 174, 18, 34, 279, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 451,new uint[]{ 28, 163, 109, 18, 103, 97, 342, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(10, 453,new uint[]{ 364, 223, 29, 410, 252, 238, 418, 68, 501, 367, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(8, 458,new uint[]{ 239, 114, 21, 243, 150, 366, 109, 133, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(7, 459,new uint[]{ 73, 74, 38, 23, 130, 345, 402, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
new EggMoveList(13, 423,new uint[]{ 68, 243, 254, 256, 255, 281, 174, 124, 499, 54, 151, 133, 90, 0, 0, 0 }),
        };

        public struct LevelInfo
        {
            public uint max;
            public uint min;

            public LevelInfo(uint max, uint min)
            {
                this.max = max;
                this.min = min;
            }
        };

        public LevelInfo[] levelInfoList =
        {
            new LevelInfo(16, 20),
            new LevelInfo(25, 29),
            new LevelInfo(29, 33),
            new LevelInfo(33, 37),
            new LevelInfo(36, 40),
            new LevelInfo(39, 43),
            new LevelInfo(42, 46),
            new LevelInfo(50, 55),
            new LevelInfo(58, 63)
        };

        public int getItem(uint rand, Lead lead, PB8 pk)
        {
            var ItemTableRange = new uint[] { 50, 60 };

            uint thresh1 = ItemTableRange[lead == Lead.CompoundEyes ? 1 : 0];
            uint thresh2 = 20;

            if (rand >= thresh1)
            {
                if (rand >= (thresh1 + thresh2))
                {
                    return (PersonalTable.BDSP.GetFormEntry(pk.Species, pk.Form)).Item3;
                }
                else
                {
                    return (PersonalTable.BDSP.GetFormEntry(pk.Species, pk.Form)).Item2;
                }
            }
            else
            {
                return (PersonalTable.BDSP.GetFormEntry(pk.Species, pk.Form)).Item1;
            }
        }
        public PB8 UndergroundGenerator(PB8 pk, ulong seed0, ulong seed1, bool diglett, uint levelflag, PokeTradeHub<PK8> Hub)
        {
            int pidRolls = diglett ? 2 : 1;

            uint level;
            var levelinfo = levelInfoList[levelflag];
            var gen = new Xorshift(seed0, seed1);
            if (Hub.Config.BDSP_RNG.AutoRNGSettings.Lead == Lead.Pressure)
            {
                level = levelinfo.max;
            }
            else
            {
                uint range = levelinfo.max - levelinfo.min + 1;
                level = levelinfo.min + (gen.NextUInt() % range);
            }

            pk.EncryptionConstant = gen.Next();
            uint sidtid = gen.Next();
            uint pid = 0;
            for (int i = 0; i < pidRolls; i++)
            {
                pid = gen.Next();
                uint psv = (pid >> 16) ^ (pid & 0xffff);
                uint xor = (sidtid >> 16) ^ (sidtid & 0xffff) ^ psv;
                pid = GetRevisedPID(sidtid, pid, pk);
                if (xor < 16)
                    break;
            }
            pk.PID = pid;

            int[] ivs = { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };

            for (var i = 0; i < ivs.Length; i++)
                ivs[i] = (int)(gen.Next() % 32);

            pk.IV_HP = ivs[0];
            pk.IV_ATK = ivs[1];
            pk.IV_DEF = ivs[2];
            pk.IV_SPA = ivs[3];
            pk.IV_SPD = ivs[4];
            pk.IV_SPE = ivs[5];

            int ability = (int)gen.Next() % 2;
            pk.SetAbilityIndex(ability);

            var genderRatio = PersonalTable.BDSP.GetFormEntry(pk.Species, pk.Form).Gender;
            if (genderRatio == PersonalInfo.RatioMagicGenderless)
                pk.Gender = (int)Gender.Genderless;
            else if (genderRatio == PersonalInfo.RatioMagicMale)
                pk.Gender = (int)Gender.Male;
            else if (genderRatio == PersonalInfo.RatioMagicFemale)
                pk.Gender = (int)Gender.Female;
            else
            {
                if((Hub.Config.BDSP_RNG.AutoRNGSettings.Lead == Lead.CuteCharmF || Hub.Config.BDSP_RNG.AutoRNGSettings.Lead == Lead.CuteCharmM) && (gen.Next() % 100) < 67)
                    pk.Gender = (byte)(Hub.Config.BDSP_RNG.AutoRNGSettings.Lead == Lead.CuteCharmF ? 1 : 0);
                else
                    pk.Gender = (byte)(((int)((gen.Next() % 253) + 1) < genderRatio) ? 1 : 0);
            }

            if (Hub.Config.BDSP_RNG.AutoRNGSettings.SyncNature != Nature.Random)
                pk.Nature = Hub.Config.BDSP_RNG.AutoRNGSettings.SyncNature;
            else
                pk.Nature = (Nature)(gen.Next() % 25);

            gen.Advance(4);

            pk.HeldItem = getItem(gen.NextUInt() % 100, Hub.Config.BDSP_RNG.AutoRNGSettings.Lead, pk);
            EggMoveList eggMoves = eggMoveList.Last();

            for(int i = 0; i < eggMoveList.Length; i++)
            {
                if (eggMoveList[i].specie >= (PersonalTable.BDSP.GetFormEntry(pk.Species, pk.Form)).HatchSpecies && eggMoveList[i].specie < pk.Species)
                    eggMoves = eggMoveList[i];
            }

            uint eggMove = 0;
            if(eggMoves.specie != eggMoveList.Last().specie && eggMoves.specie == (PersonalTable.BDSP.GetFormEntry(pk.Species, pk.Form)).HatchSpecies)
                eggMove = eggMoves.moves[gen.NextUInt() % eggMoves.count];

            pk.RelearnMove1 = (ushort)eggMove;

            return pk;
        }

        public PB8 EggGenerator(PB8 pk, ulong seed, bool shinycharm, PB8 parent1, PB8 parent2)
        {
            isshinycharm = shinycharm;
            var Male = new PB8();
            var Female = new PB8();
            if (parent1.Gender == 0 || parent2.Gender == 1 || (parent1.Species == 132 && parent2.Gender == 1) || (parent2.Species == 132 && parent1.Gender != 1))
            {
                Male = parent1;
                Female = parent2;
            }
            else
            {
                Female = parent1;
                Male = parent2;
            }

            ushort pidRolls = 0;
            if (Male.Language != Female.Language)
                pidRolls += 6;
            if (isshinycharm)
                pidRolls += 2;

            if ((seed & 0x80000000) > 0)
                seed |= 0xffffffff00000000;

            var gen = new Xoroshiro128Plus8b(seed);

            var poke = Female.Species == 132 ? Male : Female;
            pk.Species = (PersonalTable.BDSP.GetFormEntry(poke.Species, poke.Form)).HatchSpecies;
            if (poke.Species == 29 || poke.Species == 32)
            {
                pk.Species = (ushort)(gen.NextUInt(0x2) == 1 ? 29 : 32);
            }
            else if (poke.Species == 313 || poke.Species== 314)
            {
                pk.Species = (ushort)(gen.NextUInt(0x2) == 1 ? 314 : 313);
            }
            else if (poke.Species == 490)
                pk.Species = 489;

            var genderRatio = PersonalTable.BDSP.GetFormEntry(pk.Species, pk.Form).Gender;
            if (genderRatio == PersonalInfo.RatioMagicGenderless)
                pk.Gender = (int)Gender.Genderless;
            else if (genderRatio == PersonalInfo.RatioMagicMale)
                pk.Gender = (int)Gender.Male;
            else if (genderRatio == PersonalInfo.RatioMagicFemale)
                pk.Gender = (int)Gender.Female;
            else
                pk.Gender = (byte)(((int)gen.NextUInt(252) + 1 < genderRatio) ? 1 : 0);

            pk.Nature = (Nature)gen.NextUInt(25);
            if (Male.HeldItem == 229 && Female.HeldItem == 229)
                pk.Nature = gen.NextUInt(2) == 0 ? Female.Nature : Male.Nature;
            else if (Male.HeldItem == 229)
                pk.Nature = Male.Nature;
            else if (Female.HeldItem == 229)
                pk.Nature = Female.Nature;

            int Parenrability = Female.Species == 132 ? Male.AbilityNumber : Female.AbilityNumber;
            int ability = (int)gen.NextUInt(100);
            if (Parenrability == 4)
                ability = ability < 20 ? 0 : ability < 40 ? 1 : 2;
            else if (Parenrability == 2)
                ability = ability < 20 ? 0 : 1;
            else
                ability = ability < 80 ? 0 : 1;
            pk.SetAbilityIndex(ability);

            int inheritance = 3;
            if (Male.HeldItem == 280 || Female.HeldItem == 280)
                inheritance = 5;

            int[] inherit = { 0, 0, 0, 0, 0, 0 };
            for (int i = 0; i < inheritance;)
            {
                int index = (int)gen.NextUInt(6);
                if (inherit[index] == 0)
                {
                    inherit[index]  = (int)gen.NextUInt(2) + 1;
                    i++;
                }
            }

            Span<int> alloc = stackalloc int[6];
            Male.GetIVs(alloc);
            var MaleIVs = ToSpeedLast(alloc);
            Female.GetIVs(alloc);
            var FemaleIVs = ToSpeedLast(alloc);
            for (int i = 0; i < 6; i++)
            {
                int iv = (int)gen.NextUInt(32);
                if (inherit[i] == 1)
                    iv = MaleIVs[i];

                if (inherit[i] == 2)
                    iv = FemaleIVs[i];

                switch (i)
                {
                    case 0: pk.IV_HP = iv; break;
                    case 1: pk.IV_ATK = iv; break;
                    case 2: pk.IV_DEF = iv; break;
                    case 3: pk.IV_SPA = iv; break;
                    case 4: pk.IV_SPD = iv; break;
                    case 5: pk.IV_SPE = iv; break;
                }
            }

            pk.EncryptionConstant = gen.NextUInt();

            uint pid = 0;
            uint psv = 0;
            uint tsv = (uint)(pk.TrainerTID7 ^ pk.TrainerSID7);
            for (ushort roll = 0; roll < pidRolls; roll++)
            {
                pid = gen.NextUInt(0xffffffff);
                psv = ((pid >> 16) ^ (pid & 0xffff));
                if ((psv ^ tsv) < 16)
                {
                    break;
                }
            }
            pk.PID = pid;
            return pk;
        }
        public PB8 CalculateFromSeed(PB8 pk, Shiny shiny, RNGType type, uint seed)
        {
            var xoro = new Xoroshiro128Plus8b(seed);

            var flawless = GetFlawless(type, PokeEvents.None);

            pk.EncryptionConstant = seed;

            var fakeTID = xoro.NextUInt(); // fakeTID
            var pid = xoro.NextUInt();
            pid = GetRevisedPID(fakeTID, pid, pk);
            if (shiny == Shiny.Never)
            {
                if (GetIsShiny(pk.TrainerTID7, pk.TrainerSID7, pid))
                    pid ^= 0x1000_0000;
            }
            pk.PID = pid;

            int[] ivs = { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };
            var determined = 0;
            while (determined < flawless)
            {
                var idx = xoro.NextUInt(N_IV);
                if (ivs[idx] != UNSET)
                    continue;
                ivs[idx] = MAX;
                determined++;
            }

            for (var i = 0; i < ivs.Length; i++)
            {
                if (ivs[i] == UNSET)
                    ivs[i] = (int)xoro.NextUInt(MAX + 1);
            }

            pk.IV_HP = ivs[0];
            pk.IV_ATK = ivs[1];
            pk.IV_DEF = ivs[2];
            pk.IV_SPA = ivs[3];
            pk.IV_SPD = ivs[4];
            pk.IV_SPE = ivs[5];

            pk.SetAbilityIndex((int)xoro.NextUInt(2));

            pk.Nature = (Nature)xoro.NextUInt(N_NATURE);

            return pk;
        }

        public PB8 CalculateFromStates(PB8 pk, Shiny shiny, RNGType type, Xorshift seed, Nature SyncNature, WildMode mode = WildMode.None, List<int>? slots = null, PokeEvents events = PokeEvents.None, int[]? unownForms = null)
        {
            var xoro = new Xorshift(seed.GetU64State()[0], seed.GetU64State()[1]);
            
            if(type is RNGType.Wild && mode != WildMode.None && slots != null)
			{
                var calc = xoro.Range(0, 100);
                var slot = CalcSlot(mode, calc);
                pk.Species = (ushort)slots[slot];

                if (unownForms != null && unownForms.Length > 0)
                    pk.Form = (byte)unownForms[xoro.Range(0, (byte)unownForms.Length)];

                //Save the slot in some available structure space
                pk.Move1 = slot;
                if (mode is WildMode.GoodRod or WildMode.GoodRod or WildMode.SuperRod)
                    xoro.Advance(82);
                else
                    xoro.Advance(84);

                if (mode is not WildMode.Grass or WildMode.Swarm)
                    xoro.Next(); //Lvl Range(0,1000)
			}

            if (type is RNGType.MysteryGift)
                xoro.Next();

            pk.EncryptionConstant = xoro.Next();

            var fakeTID = type switch
            {
                RNGType.MysteryGift => (uint)0x0,
                _ => xoro.Next(),
            };

            var pid = xoro.Next();

            if (type is not RNGType.MysteryGift)
            {
                pid = GetRevisedPID(fakeTID, pid, pk);
            }
            else
            {
                pk.TrainerTID7 = GetGiftFTID(events, pk)[0];
                pk.TrainerSID7 = GetGiftFTID(events, pk)[1];
            }

            if (shiny == Shiny.Never)
            {
                if (GetIsShiny(pk.TrainerTID7, pk.TrainerSID7, pid))
                    pid ^= 0x1000_0000;
            }
            pk.PID = pid;

            int[] ivs = { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };

            if (IsFixedIV(type, events))
            {
                var flawless = GetFlawless(type, events);
                var determined = 0;
                while (determined < flawless)
                {
                    var idx = xoro.Next() % N_IV;
                    if (ivs[idx] != UNSET)
                        continue;
                    ivs[idx] = MAX;
                    determined++;
                }

                for (var i = 0; i < ivs.Length; i++)
                {
                    if (ivs[i] == UNSET)
                        ivs[i] = (int)(xoro.Next() & MAX);
                }
            }
            else
            {
                ivs = GetFixedIVs(events);
            }

            pk.IV_HP = ivs[0];
            pk.IV_ATK = ivs[1];
            pk.IV_DEF = ivs[2];
            pk.IV_SPA = ivs[3];
            pk.IV_SPD = ivs[4];
            pk.IV_SPE = ivs[5];

            if (type is not RNGType.MysteryGift)
            {

                pk.SetAbilityIndex((int)(xoro.Next()&N_ABILITY));
                
                var genderRatio = PersonalTable.BDSP.GetFormEntry(pk.Species, pk.Form).Gender;
                if (genderRatio == PersonalInfo.RatioMagicGenderless)
                    pk.Gender = (int)Gender.Genderless;
                else if (genderRatio == PersonalInfo.RatioMagicMale)
                    pk.Gender = (int)Gender.Male;
                else if (genderRatio == PersonalInfo.RatioMagicFemale)
                    pk.Gender = (int)Gender.Female;
                else
                    pk.Gender = (byte)((((xoro.Next() % N_GENDER)) + 1 < genderRatio) ? 1 : 0);
            }
            else
            {
                pk.Gender = (byte)GetEventGender(events);
            }

            if (type is not RNGType.MysteryGift || AllowRandomNature(events))
            {
                bool IsSync = SyncNature == Nature.Random || type is RNGType.MysteryGift ? false : true;
                if (!IsSync)
                    pk.Nature = (Nature)(xoro.Next() % N_NATURE);
                else
                    pk.Nature = SyncNature;
            }
            else
            {
                pk.Nature = GetEventNature(events);
            }

            return pk;
        }
        private static uint GetRevisedPID(uint fakeTID, uint pid, ITrainerID32 tr)
        {
            var xor = GetShinyXor(pid, fakeTID);
            var newXor = GetShinyXor(pid, (uint)(tr.TID16 | (tr.SID16 << 16)));

            var fakeRare = GetRareType(xor);
            var newRare = GetRareType(newXor);

            if (fakeRare == newRare)
                return pid;

            var isShiny = xor < 16;
            if (isShiny)
                return (((uint)(tr.TID16 ^ tr.SID16) ^ (pid & 0xFFFF) ^ (xor == 0 ? 0u : 1u)) << 16) | (pid & 0xFFFF);
            return pid ^ 0x1000_0000;
        }

        private static Shiny GetRareType(uint xor) => xor switch
        {
            0 => Shiny.AlwaysSquare,
            < 16 => Shiny.AlwaysStar,
            _ => Shiny.Never,
        };

        private static bool IsFixedIV(RNGType type, PokeEvents events) => type is RNGType.MysteryGift && events switch
        {
            PokeEvents.PokeCenterPiplup => true,
            _ => false,
        };

        private static int GetFlawless(RNGType type, PokeEvents events)
		{
            return type switch
            {
                RNGType.Legendary or RNGType.Shamin or RNGType.Roamer or RNGType.Gift_3IV  => 3,
                RNGType.MysteryGift => events switch
                {
                    PokeEvents.ManaphyEgg => 3,
                    PokeEvents.BirthDayHappiny => 0,
                    PokeEvents.PokeCenterPiplup => 0,
                    PokeEvents.KorDawnPiplup => 0,
                    PokeEvents.KorRegigigas => 3,
                    PokeEvents.OtsukimiClefairy => 0,
                    _ => 0,
                },
                _ => 0,
            };  
		}
        private static int[] GetFixedIVs(PokeEvents events) => events switch
        {
            PokeEvents.PokeCenterPiplup => new int[6] { 20, 20, 20, 20, 28, 20 },
            _ => throw new System.ArgumentException("No fixed IVs for this event."),
        };

        private static uint[] GetGiftFTID(PokeEvents events, ITrainerID32 tr) => events switch
        {
            PokeEvents.ManaphyEgg => [tr.TID16, tr.SID16],
            PokeEvents.BirthDayHappiny => [61213, 2108],
            PokeEvents.PokeCenterPiplup => [58605, 03100],
            PokeEvents.KorDawnPiplup => [28217, 18344],
            PokeEvents.KorRegigigas => [11257, 18329],
            PokeEvents.OtsukimiClefairy => [29358, 02307],
            _ => [tr.TID16, tr.SID16],
        };

        private static Gender GetEventGender(PokeEvents events) => events switch
        {
            PokeEvents.BirthDayHappiny => Gender.Female,
            PokeEvents.PokeCenterPiplup => Gender.Male,
            PokeEvents.KorDawnPiplup => Gender.Male,
            PokeEvents.KorRegigigas => Gender.Genderless,
            PokeEvents.OtsukimiClefairy => Gender.Male,
            _ => Gender.Genderless,
        };

        private static Nature GetEventNature(PokeEvents events) => events switch
        {
            PokeEvents.BirthDayHappiny => Nature.Hardy,
            PokeEvents.PokeCenterPiplup => Nature.Hardy,
            PokeEvents.KorDawnPiplup => Nature.Hardy,
            PokeEvents.OtsukimiClefairy => Nature.Modest,
            _ => Nature.Hardy,
        };

        private static bool AllowRandomNature(PokeEvents events) => events switch
        {
            PokeEvents.ManaphyEgg => true,
            PokeEvents.BirthDayHappiny => false,
            PokeEvents.PokeCenterPiplup => false,
            PokeEvents.KorDawnPiplup => false,
            PokeEvents.KorRegigigas => true,
            PokeEvents.OtsukimiClefairy => false,
            _ => true,
        };

        private static bool GetIsShiny(uint tid, uint sid, uint pid) =>
            GetIsShiny(pid, (sid << 16) | tid);        

        private static bool GetIsShiny(uint pid, uint oid) => 
            GetShinyXor(pid, oid) < 16;

        private static uint GetShinyXor(uint pid, uint oid)
        {
            var xor = pid ^ oid;
            return (xor ^ (xor >> 16)) & 0xFFFF;
        }

        private static int[] ToSpeedLast(ReadOnlySpan<int> ivs) => [ivs[0], ivs[1], ivs[2], ivs[4], ivs[5], ivs[3]];
        //Thanks to Admiral Fish and his Pokéfinder
        private byte CalcSlot(WildMode mode, byte rand)
		{
            var calc = mode switch
            {
                WildMode.GoodRod or WildMode.SuperRod => new byte[5] { 40, 80, 95, 99, 100 },
                WildMode.OldRod or WildMode.Surf => new byte[5] { 60, 90, 95, 99, 100 },
                _ => new byte[12] { 20, 40, 50, 60, 70, 80, 85, 90, 94, 98, 99, 100 },
            };
            byte i;
            for (i = 0; i < calc.Length; i++)
                if (rand < calc[i])
                    return i;
            return 255;
        }
    }
}
