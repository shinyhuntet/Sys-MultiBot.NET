using PKHeX.Core;
using System;

namespace SysBot.Pokemon;

public class EggRNG
{
    public static PK8 GenerateEgg(ulong seed, PK8 parent1, PK8 parent2, bool shinycharm, SAV8SWSH TrainerData)
    {
        var r = new Xoroshiro128Plus(seed);
        PK8 pk = new PK8();

        var Male = new PK8();
        var female = new PK8();
        if (parent1.Gender == 0 || parent2.Gender == 1 || (parent1.Species == 132 && parent2.Gender == 1) || (parent2.Species == 132 && parent1.Gender != 1))
        {
            Male = parent1;
            female = parent2;
        }
        else
        {
            female = parent1;
            Male = parent2;
        }
        var poke = female.Species == 132 ? Male : female;
        var parentpi = PersonalTable.SWSH.GetFormEntry(poke.Species, poke.Form);
        pk.Species = parentpi.HatchSpecies;
        if (poke.Species == 29 || poke.Species == 32)        
            pk.Species = (ushort)(r.NextInt(0x2) == 1 ? 29 : 32);
        
        else if (poke.Species == 313 || poke.Species == 314)        
            pk.Species = (ushort)(r.NextInt(0x2) == 1 ? 314 : 313);
        
        else if (poke.Species == 490)
            pk.Species = 489;

        pk.Form = parentpi.LocalFormIndex;
        var childpi = PersonalTable.SWSH.GetFormEntry(pk.Species, pk.Form);
        var GenderRatio = childpi.Gender;
        if (GenderRatio == 255)        
            pk.Gender = (int)Gender.Genderless;        
        else if (GenderRatio == 254)        
            pk.Gender = (int)Gender.Female;        
        else if (GenderRatio == 0)        
            pk.Gender = (int)Gender.Male;        
        else        
            pk.Gender = (byte)(r.NextInt(252) + 1 < GenderRatio ? 1 : 0);        

        int nature = (int)r.NextInt(25);
        if (Male.HeldItem == 229 && female.HeldItem == 229)
            nature = (int)(r.NextInt(2) == 1 ? female.Nature : Male.Nature);
        else if (Male.HeldItem == 229)
            nature = (int)Male.Nature;
        else if (female.HeldItem == 229)
            nature = (int)female.Nature;

        pk.SetNature((Nature)nature);

        int baseAbility = poke.AbilityNumber;
        int ability = GetAbilityNum(baseAbility, (int)r.NextInt(100));
        pk.SetAbilityIndex(ability);

        int InheritIVsCnt = 3;
        if (Male.HeldItem == 280 || female.HeldItem == 280)
            InheritIVsCnt = 5;

        int[] InheritIVs = { -1, -1, -1, -1, -1, -1 };
        var FEMALE_POWER = GetPowerItem(female.HeldItem);
        var MALE_POWER = GetPowerItem(Male.HeldItem);
        bool BOTH_POWER = MALE_POWER >= 0 && FEMALE_POWER >= 0;
        if (BOTH_POWER)
        {
            if (r.NextInt(2) == 0)
                InheritIVs[FEMALE_POWER] = 1;
            else
                InheritIVs[MALE_POWER] = 0;
        }
        else if (MALE_POWER >= 0)
        {
            InheritIVs[MALE_POWER] = 0;
        }
        else if (FEMALE_POWER >= 0)
        {
            InheritIVs[FEMALE_POWER] = 1;
        }

        if (MALE_POWER >= 0 || FEMALE_POWER >= 0)
            InheritIVsCnt -= 1;

        for (int i = 0; i < InheritIVsCnt; i++)
        {
            var tmp = (int)r.NextInt(6);
            while (InheritIVs[tmp] > -1)
                tmp = (int)r.NextInt(6);
            InheritIVs[tmp] = (int)r.NextInt(2);
        }

        Span<int> alloc = stackalloc int[6];
        Male.GetIVs(alloc);
        var MaleIVs = ToSpeedLast(alloc);
        female.GetIVs(alloc);
        var FemaleIVs = ToSpeedLast(alloc);
        for (int i = 0; i < 6; i++)
        {
            int iv = (int)r.NextInt(32);

            if (InheritIVs[i] == 0)            
                iv = MaleIVs[i];                
            
            if (InheritIVs[i] == 1)            
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

        pk.EncryptionConstant = (uint)r.NextInt(0xFFFFFFFF);

        pk.TrainerTID7 = TrainerData.TrainerTID7;
        pk.TrainerSID7 = TrainerData.TrainerSID7;
        var txor = pk.TrainerTID7 ^ pk.TrainerSID7;
        int reroll = Male.Language != female.Language ? 6 : 0;
        if (shinycharm)
            reroll += 2;
        for (int i = 0; i < reroll; i++)
        {
            pk.PID = (uint)r.NextInt(0xFFFFFFFF);
            var ShinyXor = (uint)((pk.PID >> 16) ^ (pk.PID & 0xFFFF) ^ txor);
            if (ShinyXor < 16)
                break;
        }

        pk.Ball = poke.Ball;
        if (Male.Species == female.Species)
        {
            byte BASE_BALL = poke.Ball;
            byte MALE_BALL = Male.Ball;
            if (r.NextInt(100) >= 50)
                pk.Ball = MALE_BALL;
        }
        if (pk.Ball == 16 || pk.Ball == 1)
            pk.Ball = 4;

        return pk;
    }

    const int POWERITEM = 289;

    private static int GetAbilityNum(int baseAbility, int randroll)
    {
        if (baseAbility == 4)
        {
            if (randroll < 20)
                return 0;
            else if (randroll < 40)
                return 1;
            return 2;
        }
        else if (baseAbility == 2)
        {
            return randroll < 20 ? 0 : 1;
        }
        else
        {
            return randroll < 80 ? 0 : 1;
        }
    }
    private static int GetPowerItem(int itemID)
    {
        if (POWERITEM <= itemID && itemID <= POWERITEM + 5)
            return itemID - POWERITEM;
        return -1;
    }

    public static int[] ToSpeedLast(ReadOnlySpan<int> ivs) => [ivs[0], ivs[1], ivs[2], ivs[4], ivs[5], ivs[3]];

}
