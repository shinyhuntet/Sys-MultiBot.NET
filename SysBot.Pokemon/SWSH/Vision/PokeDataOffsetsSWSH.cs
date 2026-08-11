using System;
using System.Collections.Generic;

namespace SysBot.Pokemon;

/// <summary>
/// Sword &amp; Shield RAM offsets
/// </summary>
public class PokeDataOffsetsSWSH
{
    public const string SWSHGameVersion = "1.3.2";
    public const string SwordID = "0100ABF008968000";
    public const string ShieldID = "01008DB008C2C000";

    public const uint BoxStartOffset = 0x45075880;
    public const uint CurrentBoxOffset = 0x450C680E;
    public const uint TrainerDataOffset = 0x45068F18;
    public const uint SoftBanUnixTimespanOffset = 0x450C89E8;
    public const uint IsConnectedOffset = 0x30c7cca8;
    public const uint TextSpeedOffset = 0x450690A0;
    public const uint ItemTreasureAddress = 0x45068970;
    public const uint CalyrexFusionSlotOffset = 0x450CAE28;
    public const uint PokeBallOffset = 0x45067B88; // 0x74 size
    public const int PokeBallPouchInventoryLength = 29;

    public const uint DenVanillaOffset = 0x450C8A70;
    public const uint DenIslandOfArmorOffset = 0x450C94D8;
    public const uint DenCrownTundraOffset = 0x450C9F40;
    public const uint DenEventStartOffset = 0x2F9EB320;

    public const uint DexRecMon = 0x45072B18;
    public const uint DexRecMonGender = 0x45072B20;
    public const uint DexRecLocation = 0x45072B98;

    public const uint OtherItemAddress = 0x45067D90;
    public const int OtherItemPouchInventoryLength = 546;
    public const uint BerryPouchOffset = 0x45067C50;// 212 bytes
    public const int BerryPouchInventoryLength = 53;
    public const uint IngredientPouchOffset = 0x45068B00;// 100 bytes
    public const int IngredientPouchInventoryLength = 25;
    public const uint KeyItemPouchOffset = 0x45068C90; // 256 bytes


    // Raid Offsets
    // The dex number of the Pokémon the host currently has chosen. 
    // Details for each player span 0x30, so add 0x30 to get to the next offset.
    public const uint RaidP0PokemonOffset = 0x8398A294;
    // Add to each Pokémon offset.  AltForm used.
    public const uint RaidAltFormInc = 0x4;
    // Add to each Pokémon offset.  0 = male, 1 = female, 2 = genderless.
    public const uint RaidGenderIncr = 0x8;
    // Add to each Pokémon offset.  Bool for whether the Pokémon is shiny.
    public const uint RaidShinyIncr = 0xC;
    // Add to each Pokémon offset.  Bool for whether they have locked in their Pokémon.
    public const uint RaidLockedInIncr = 0x1C;
    public const uint RaidBossOffset = 0x8398A25C;

    // 0 when not in a battle or raid, 0x40 or 0x41 otherwise.
    public const uint InBattleRaidOffsetSW = 0x3F128624;
    public const uint InBattleRaidOffsetSH = 0x3F128626;

    // Pokémon Encounter Offsets
    public const uint WildPokemonOffset = 0x8FEA3648;
    public const uint RaidPokemonOffset = 0x886A95B8;
    public const uint LegendaryPokemonOffset = 0x886BC348;

    // Link Trade Offsets
    public const uint LinkTradePartnerPokemonOffset = 0xAF286078;
    public const uint LinkTradePartnerNameOffset = 0xAF28384C;
    public const uint LinkTradePartnerTIDSIDOffset = LinkTradePartnerNameOffset - 0x8;
    public const uint LinkTradePartnerNIDOffset = 0xAF2846B0;
    public const uint LinkTradeSearchingOffset = 0x2F76C3C8;

    // Surprise Trade Offsets
    public const uint SurpriseTradePartnerPokemonOffset = 0x450675a0;
    public const uint SurpriseTradePartnerNameOffset = 0x45067708;
    public const uint SurpriseTradePartnerTIDSIDOffset = SurpriseTradePartnerNameOffset - 0x8;

    public const uint SurpriseTradeSearchOffset = 0x45067704;
    public const uint SurpriseTradeSearch_Empty = 0x00000000;
    public const uint SurpriseTradeSearch_Searching = 0x01000000;
    public const uint SurpriseTradeSearch_Found = 0x0200012C;

    public const uint SurpriseTradeLockSlot = 0x450676fc;
    public const uint SurpriseTradeLockBox = 0x450676f8;

    /* Wild Area Daycare */
    public const uint DayCare_Wildarea_Step_Counter = 0x4511FC54;
    public const uint DayCare_Wildarea_Egg_Seed = 0x4511FC58;
    public const uint DayCare_Wildarea_Egg_Is_Ready = 0x4511FC60;
    public const uint DayCare_Wildarea_Egg_Parent = 0x4511F9C0;

    /* Route 5 Daycare */
    public const uint DayCare_Route5_Step_Counter = 0x4511F99C;
    public const uint DayCare_Route5_Egg_Seed = 0x4511F9A0;
    public const uint DayCare_Route5_Egg_Is_Ready = 0x4511F9A8;
    public const uint DayCare_Route5_Egg_Parent = 0x4511F708;

    public const int BoxFormatSlotSize = 0x158;
    public const int TrainerDataLength = 0x110;
    public const int DayCareSize = 2 + (0x148 * 2);

    /* Lair offsets */
    public IReadOnlyList<long> MaxLairPokemonRNGPointer { get; } = new long[] { 0x28F4060, 0x238, 0x2AB8 };
    public IReadOnlyList<long> MaxLairLegendaryPointer { get; } = [ 0x26365B8, 0x68, 0x78, 0x88, 0xD08, 0x950, 0xD0 ];

    public const uint OpponentRaidPokemonHPOffset = 0x886A90D4;
    public const uint OpponetRaidPokemonAsleepOffset = 0x886A9100;
    public const uint OpponetRaidPokemonParalizedOffset = 0x886A90F8;
    public const uint MaxLairPenaltyWarnOffset = 0x50B06FC0;
    public const uint MaxLairPenaltyCountOffset = 0x50B12710;
    public const uint CurrentScreenLairOffset = 0x16E498;
    public const uint LairMiscScreenOffset = 0x2955BA0; // Main
    public const uint ResetLegendFlagOffset = 0x50AD76F0;
    public const uint UnlocksUBInMaxLairOffset = 0x50AD84B8;
    public const uint LairMove1Offset = 0x840A5B10;

    public const uint AdventureSeedOffset = 0x4514A4B0;
    public const uint LairRewardsOffset = 0x2977BC0;
    public const uint DamageOutputOffset = 0x007E37F0;

    public const uint LairPartyP1Offset = 0x886B67C8;
    public const uint LairPartyP2Offset = 0x886BC348;
    public const uint LairPartyP3Offset = 0x886B9588;
    public const uint LairPartyP4Offset = 0x886BF108;

    public const uint RentalMon1 = 0x83E93070;
    public const uint RentalMon2 = 0x83E93300;
    public const uint RentalMon3 = 0x83E93590;

    public const uint LairSpeciesNote1 = 0x50B12278;
    public const uint LairSpeciesNote2 = 0x50B122B0;
    public const uint LairSpeciesNote3 = 0x50B122E8;
    public const uint LairSpeciesNote4 = 0x50B12320;

    public const uint LairSpeciesHint = 0x50B12968;
    public const uint LairSpeciesNote1Pre = 0x50B129A0;
    public const uint LairSpeciesNote2Pre = 0x50B129D8;
    public const uint LairSpeciesNote3Pre = 0x50B12A10;
    public const uint LairSpeciesNote4Pre = 0x50B12A48;

    public const uint LairSpeciesNoteKeyStart = 0x50B12270;
    public const uint LairSpeciesNoteKey2Start = 0x50B12998;
    public const uint KMaxLairSpeciesID1Noted = 0x6F669A35; // U32 Max Lair Species 1 Noted
    public const uint KMaxLairSpeciesID2Noted = 0x6F66951C; // U32 Max Lair Species 2 Noted
    public const uint KMaxLairSpeciesID3Noted = 0x6F6696CF; // U32 Max Lair Species 3 Noted
    public const uint SpeciesNoteLength = 0x38; // U32 Max Lair Species 4 Noted

    #region ScreenDetection
    // Stable overworld detection. Value is 1 on overworld and 0 otherwise.
    public IReadOnlyList<long> OverworldPointer { get; } = new long[] { 0x2636678, 0xC0, 0x80 };
    public IReadOnlyList<long> PlayerCoordsPointer { get; } = [0x26365B8, 0x88, 0x1F8, 0xE0, 0x10, 0xE0, 0x60];
    public IReadOnlyList<long> CurrentBallIndexPointer { get; } = [ 0x2951270, 0x1D8, 0x818, 0x2B0, 0x2E0, 0x200, 0x0 ];

    // For detecting when we're on the in-battle menu, so we can flee.
    public const uint BattleMenuOffset = 0x8398A470;

    // Original screen detection offset.
    public const uint CurrentScreenOffset = 0x6B30FA00;
    // Used for checking if we're in a box. It can be either value for different users.
    public const uint CurrentScreen_Box1 = 0xFF00D59B;
    public const uint CurrentScreen_Box2 = 0xFF000000;
    // Value when user is softbanned.
    public const uint CurrentScreen_Softban = 0xFF000000;
    #endregion

    public static uint GetTrainerNameOffset(TradeMethod tradeMethod) => tradeMethod switch
    {
        TradeMethod.LinkTrade => LinkTradePartnerNameOffset,
        TradeMethod.SurpriseTrade => SurpriseTradePartnerNameOffset,
        _ => throw new ArgumentException("Trainer name offset is not available for this trade method.", nameof(tradeMethod)),
    };

    public static uint GetTrainerTIDSIDOffset(TradeMethod tradeMethod) => tradeMethod switch
    {
        TradeMethod.LinkTrade => LinkTradePartnerTIDSIDOffset,
        TradeMethod.SurpriseTrade => SurpriseTradePartnerTIDSIDOffset,
        _ => throw new ArgumentException("Trainer TID/SID offset is not available for this trade method.", nameof(tradeMethod)),
    };

    public static uint GetDaycareStepCounterOffset(SwordShieldDaycare daycare) => daycare switch
    {
        SwordShieldDaycare.WildArea => DayCare_Wildarea_Step_Counter,
        SwordShieldDaycare.Route5 => DayCare_Route5_Step_Counter,
        _ => throw new ArgumentException(nameof(daycare)),
    };

    public static uint GetDaycareEggIsReadyOffset(SwordShieldDaycare daycare) => daycare switch
    {
        SwordShieldDaycare.WildArea => DayCare_Wildarea_Egg_Is_Ready,
        SwordShieldDaycare.Route5 => DayCare_Route5_Egg_Is_Ready,
        _ => throw new ArgumentException(nameof(daycare)),
    };

    public static uint GetDaycareEggParentsOffset(SwordShieldDaycare daycare) => daycare switch
    {
        SwordShieldDaycare.WildArea => DayCare_Wildarea_Egg_Parent,
        SwordShieldDaycare.Route5 => DayCare_Route5_Egg_Parent,
        _ => throw new ArgumentException(nameof(daycare)),
    };
    public static uint GetDaycareEggSeedOffset(SwordShieldDaycare daycare) => daycare switch
    {
        SwordShieldDaycare.WildArea => DayCare_Wildarea_Egg_Seed,
        SwordShieldDaycare.Route5 => DayCare_Route5_Egg_Seed,
        _ => throw new ArgumentException(nameof(daycare)),
    };

}
