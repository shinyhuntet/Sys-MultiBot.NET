using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.SWSH.RNG;
using System;
using System.Collections.Generic;
using System.Linq;
using static SysBot.Pokemon.DmaxPokemonData;
using MersenneTwister = SysBot.Pokemon.SWSH.RNG.MersenneTwister;

namespace SysBot.Pokemon;

public static class LairRNG
{
    public static List<PK8> DmaxLegendaries = [];
    private static List<uint> GenerateSpeciesTable(uint seed)
    {
        int poolSize = dmaxAdventureTemplates.Count;
        List<uint> PoolList = new List<uint>(poolSize);
        PoolList.AddRange(Enumerable.Range(0, poolSize).Select(x => (uint)x));

        var mt = new MersenneTwister(seed);
        uint maxRand = (uint)poolSize;

        for (uint i = 0; i < poolSize; i++)
        {
            maxRand = maxRand - 1;

            uint rand = mt.Next(maxRand, 0x1ff) + i;

            if (rand != 0)
            {
                var tmp = PoolList[(int)i];
                PoolList[(int)i] = PoolList[(int)rand];
                PoolList[(int)rand] = tmp;
            }
        }
        return PoolList;
    }

    private static DmaxAdventureTemplate GetTemplateFromPool(List<uint> pool, List<DmaxAdventureTemplate> previousResults)
    {
        DmaxAdventureTemplate result = dmaxAdventureTemplates[0];

        for (int i = 0; i < pool.Count; i++)
        {
            var tableIndex = pool[i];
            result = dmaxAdventureTemplates[(int)tableIndex];

            var foundResult = previousResults.FirstOrDefault(x => x.species == result.species && result.altForm == x.altForm);

            bool previouslySelected = foundResult.species != 0;

            if (!result.isBoss && !previouslySelected)
            {
                break;
            }
        }
        return result;
    }

    private static void DetermineSpeciesTable(ref Xoroshiro rng, ref List<DmaxAdventureTemplate> results)
    {
        var poolList = GenerateSpeciesTable(rng.Next());
        var result = GetTemplateFromPool(poolList, results);
        results.Add(result);
    }

    private static void DetermineEncounterTable(ref Xoroshiro rng, ref List<DmaxAdventureTemplate> results)
    {
        DetermineSpeciesTable(ref rng, ref results);
        var result = results.Last();
        var Type1 = PersonalTable.SWSH.GetFormEntry(result.species, result.altForm).Type1;
        var Type2 = PersonalTable.SWSH.GetFormEntry(result.species, result.altForm).Type2;
        bool HasTwoType = Type1 != Type2;
        if (HasTwoType)
            rng.Next();
    }

    public static (List<PK8>, List<PK8>, PK8) GenerateLairSpeciesList(uint rand, ulong seed, int NPCCount = 3)
    {
        List<PK8> RentalpkList = new();
        List<PK8> EncounterpkList = new();
        var rng = new Xoroshiro(seed);
        List<DmaxAdventureTemplate> results = new();
        PK8 Boss = new();

        if (NPCCount > 2)
        {
            var BossIndex = rng.Next(rand);
            Boss = DmaxLegendaries[(int)BossIndex];
        }

        for (int i = 0; i < 4; i++)
            DetermineSpeciesTable(ref rng, ref results);

        if (NPCCount > 2)
            rng.Next(2);

        DetermineSpeciesTable(ref rng, ref results);

        if (NPCCount > 1)
            rng.Next(2);

        DetermineSpeciesTable(ref rng, ref results);

        if (NPCCount > 0)
            rng.Next(2);

        rng.Next(2);
        rng.Next(9);

        for (int i = 0; i < 10; i++)
            DetermineEncounterTable(ref rng, ref results);

        var Rentalpk = results[..6];
        var EncFinal = results[6..];

        foreach (var rental in Rentalpk)
        {
            var pk = new PK8
            {
                Species = rental.species,
                Form = rental.altForm,
            };
            pk.SetAbilityIndex(rental.abilityIndex);
            RentalpkList.Add(pk);
        }

        foreach (var enc in EncFinal)
        {
            var pk = new PK8
            {
                Species = enc.species,
                Form = enc.altForm,
            };
            pk.SetAbilityIndex(enc.abilityIndex);
            EncounterpkList.Add(pk);
        }

        return (RentalpkList, EncounterpkList, Boss);

    }

    private static PK8 GenerateLairPokemon(PK8 pk, ulong s0, ulong s1, int slotValue, int NPCCount = 3)
    {
        var rng_upper = new Xoroshiro128Plus(s0, s1);
        for (var i = 0; i < NPCCount; i++)
            rng_upper.Next();

        for (var j = 0; j < slotValue; j++)
            rng_upper.Next();

        // Generate the seed for the Lair Pokémon.
        var init = rng_upper.Next();
        var rng = new Xoroshiro128Plus(init);

        pk.EncryptionConstant = (uint)rng.NextInt(); // EC
        rng.NextInt(); // TID
        rng.NextInt(); // PID

        // Max Lair always has 4 fixed IVs.
        Span<int> ivs = [-1, -1, -1, -1, -1, -1];
        for (var i = 0; i < 4; i++)
        {
            int slot;
            do
            {
                slot = (int)rng.NextInt(6);
            } while (ivs[slot] != -1);

            ivs[slot] = 31;
        }
        for (var i = 0; i < 6; i++)
        {
            if (ivs[i] != -1)
                continue;

            var iv = (int)rng.NextInt(32);
            ivs[i] = iv;
        }

        var GenderRatio = PersonalTable.SWSH.GetFormEntry(pk.Species, pk.Form).Gender;
        if (GenderRatio == 255)
            pk.Gender = (int)Gender.Genderless;
        else if (GenderRatio == 254)
            pk.Gender = (int)Gender.Female;
        else if (GenderRatio == 0)
            pk.Gender = (int)Gender.Male;
        else
            pk.Gender = (byte)((int)(rng.NextInt(252) + 1) < GenderRatio ? 1 : 0);


        // Skip Gender -- all fixed.

        int nature = (int)rng.NextInt(25);
        pk.SetNature((Nature)nature);

        pk.IV_HP = ivs[0];
        pk.IV_ATK = ivs[1];
        pk.IV_DEF = ivs[2];
        pk.IV_SPA = ivs[3];
        pk.IV_SPD = ivs[4];
        pk.IV_SPE = ivs[5];

        pk.SetIsShiny(false);
        pk.DynamaxLevel = 8;
        pk.CurrentFriendship = PersonalTable.SWSH.GetFormEntry(pk.Species, pk.Form).BaseFriendship;

        return pk;
    }
    public static void GenerateLairLegendayList(bool isUBUnlocked, GameVersion Version)
    {
        DmaxLegendaries = dmaxAdventureTemplates.Where(x => x.isBoss && !Enum.IsDefined(Version == GameVersion.SW ? typeof(LairSpeciesSH) : typeof(LairSpeciesSW), x.species) && (isUBUnlocked || !isUBUnlocked && x.species < (ushort)Species.Necrozma)).Select(x => new PK8(){ Species = x.species, Form = x.altForm }).ToList();        
    }
    public static PK8 GetMaxLairNormalPokemon(ulong s0, ulong s1, List<PK8> pkList, int slot, int NPCCount = 3)
    {
        var index = pkList.Count > 6 ? slot : NPCCount;
        var pk = pkList[index];
        pk.CurrentLevel = 65;
        pk = GenerateLairPokemon(pk, s0, s1, slot, NPCCount);
        return pk;
    }

    public static PK8 GetMaxLairLegendary(LairSpecies LairSpecies, ulong s0, ulong s1, int NPCCount = 3)
    {
        // This should be the RNG state after generating 3 rental Pokémon.
        // Advance it npccount more times for replacement rentals, then 10 times for the Pokémon on the field.
        var pk = new PK8();
        pk.Species = (ushort)LairSpecies;
        pk.CurrentLevel = 70;
        pk.SetAbilityIndex(1);
        pk = GenerateLairPokemon(pk, s0, s1, 10, NPCCount);
        return pk;
    }

}
