using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysBot.Pokemon;

public class ArceusRNG
{
    private static uint GetShinyXor(in uint pid, in uint oid)
    {
        var xor = pid ^ oid;
        return (xor ^ (xor >> 16)) & 0xFFFF;
    }

    public static uint GetMask(uint maximum)
    {
        maximum -= 1;
        for (int i = 0; i < 6; i++)
        {
            maximum |= maximum >> (1 << i);
        }
        return maximum;
    }

    public static (bool shiny, uint shinyXor, uint EC, uint PID, int[] IVs, ulong ability, byte gender, Nature nature, ulong newseed) GenerateFromSeed(ulong seed, int rolls, int guranteedivs, in int genderRatio)
    {
        bool shiny = false;
        uint EC;
        uint pid = 0;
        ulong ability;
        byte gender;
        Nature nature;
        ulong newseed = 0;
        uint shinyXor = 17;
        var rng = new Xoroshiro128Plus(seed);
        EC = (uint)rng.NextInt();
        var sidtid = (uint)rng.NextInt();
        for (int i = 0; i < rolls; i++)
        {
            pid = (uint)rng.NextInt();
            shinyXor = GetShinyXor(pid, sidtid);
            shiny = shinyXor < 16;
            if (shiny)
            {
                newseed = rng.GetState().s0;
                break;
            }
        }

        const int UNSET = -1;
        int[] ivs = { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };
        const int MAX = 31;
        for (int i = 0; i < guranteedivs; i++)
        {
            int index;
            do { index = (int)rng.NextInt(6); }
            while (ivs[index] != UNSET);

            ivs[index] = MAX;
        }

        for (int i = 0; i < ivs.Length; i++)
        {
            if (ivs[i] == UNSET)
                ivs[i] = (int)rng.NextInt(32);
        }
        ability = rng.Next() & GetMask(2);
        gender = genderRatio switch
        {
            PersonalInfo.RatioMagicGenderless => 2,
            PersonalInfo.RatioMagicFemale => 1,
            PersonalInfo.RatioMagicMale => 0,
            _ => (int)rng.NextInt(252) + 1 < genderRatio ? (byte)1 : (byte)0,
        };
        nature = (Nature)(rng.NextInt(25));
        return (shiny, shinyXor, EC, pid, ivs, ability, gender, nature, newseed);
    }

    public static (PA8, ulong) GeneratePKMFromSeed(ulong seed, int rolls, int guranteedivs, PA8 pkm)
    {
        var rng = new Xoroshiro128Plus(seed);
        pkm.EncryptionConstant = (uint)rng.NextInt();        
        pkm.ID32 = (uint)rng.NextInt();
        pkm.TID16 = (ushort)(pkm.ID32 >> 16);
        pkm.SID16 = (ushort)(pkm.ID32 & 0xFFFF);
        ulong newseed = 0;
        for (int i = 0; i < rolls; i++)
        {
            pkm.PID = (uint)rng.NextInt();
            if (pkm.IsShiny)
            {
                newseed = rng.GetState().s0;
                break;
            }
        }

        const int UNSET = -1;
        int[] ivs = { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };
        const int MAX = 31;
        for (int i = 0; i < guranteedivs; i++)
        {
            int index;
            do { index = (int)rng.NextInt(6); }
            while (ivs[index] != UNSET);

            ivs[index] = MAX;
        }

        for (int i = 0; i < ivs.Length; i++)
        {
            if (ivs[i] == UNSET)
                ivs[i] = (int)rng.NextInt(32);
        }
        pkm.IV_HP = ivs[0];
        pkm.IV_ATK = ivs[1];
        pkm.IV_DEF = ivs[2];
        pkm.IV_SPA = ivs[3];
        pkm.IV_SPD = ivs[4];
        pkm.IV_SPE = ivs[5];

        var ability = rng.Next() & GetMask(2);
        pkm.SetAbility((int)ability);
        var genderRatio = PersonalTable.LA.GetFormEntry(pkm.Species, pkm.Form).Gender;
        pkm.Gender = genderRatio switch
        {
            PersonalInfo.RatioMagicGenderless => 2,
            PersonalInfo.RatioMagicFemale => 1,
            PersonalInfo.RatioMagicMale => 0,
            _ => (int)rng.NextInt(252) + 1 < genderRatio ? (byte)1 : (byte)0,
        };
        pkm.Nature = (Nature)(rng.NextInt(25));
        return (pkm, newseed);
    }
}
