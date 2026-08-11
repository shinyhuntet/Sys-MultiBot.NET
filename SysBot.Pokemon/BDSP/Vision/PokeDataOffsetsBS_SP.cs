using System.Collections.Generic;

namespace SysBot.Pokemon;

public class PokeDataOffsetsBS_SP : BasePokeDataOffsetsBS
{
    public override ulong PlayerPrefsProviderInstance => 0x4EA7408;

    public override IReadOnlyList<long> MainRNGState { get; } = [0x4FD43D0, 0x0];
    public override IReadOnlyList<long> R1_SpeciesPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x2A0, 0x2C];
    public override IReadOnlyList<long> R2_SpeciesPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x2A0, 0x4C];
    public override IReadOnlyList<long> R1_SeedPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x2A0, 0x24];
    public override IReadOnlyList<long> R2_SeedPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x2A0, 0x44];
    public override IReadOnlyList<long> EggFlagPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x458];
    public override IReadOnlyList<long> EggSeedPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x460];
    public override IReadOnlyList<long> EggStepPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x468];
    public override IReadOnlyList<long> BoxStartPokemonPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0xA0, 0x20, 0x20, 0x20];
    public override IReadOnlyList<long> PartyStartPokemonPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x808, 0x10, 0x20, 0x20, 0x18, 0x20];
    public override IReadOnlyList<long> PartySlot2PokemonPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x808, 0x10, 0x28, 0x20, 0x18, 0x20];
    public override IReadOnlyList<long> DayCareParent1PokemonPointer { get; } = [0x4E7BE98, 0x40, 0xB8, 0x10, 0x450, 0x20, 0x20];
    public override IReadOnlyList<long> DayCareParent2PokemonPointer { get; } = [0x4E7BE98, 0x40, 0xB8, 0x10, 0x450, 0x28, 0x20];
    public override IReadOnlyList<long> LocationPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x40];

    public override IReadOnlyList<long> LinkTradePartnerPokemonPointer { get; } = [0x4E77488, 0xB8, 0x8, 0x20];
    public override IReadOnlyList<long> LinkTradePartnerNamePointer { get; } = [0x4E7C9A8, 0xB8, 0x30, 0x110, 0x28, 0x90, 0x20, 0x0];
    public override IReadOnlyList<long> LinkTradePartnerIDPointer { get; } = [0x4E7C9A8, 0xB8, 0x30, 0x110, 0x28, 0x90, 0x10];
    public override IReadOnlyList<long> LinkTradePartnerParamPointer { get; } = [0x4E7C9A8, 0xB8, 0x30, 0x110, 0x28, 0x90];
    public override IReadOnlyList<long> LinkTradePartnerNIDPointer { get; } = [0x4FFE810, 0x70, 0x168, 0x40]; // todo for multi-user Union Room; limited penalties available.

    public override IReadOnlyList<long> OpponentPokemonPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x800, 0x58, 0x28, 0x10, 0x20, 0x20, 0x18, 0x20];
    public override IReadOnlyList<long> SceneIDPointer { get; } = [0x4E70C28, 0xB8, 0x18];
    public override IReadOnlyList<long> DayTimePointer { get; } = [0x4E70C28, 0xB8, 0x0, 0x60, 0x100];

    // Union Work - Detects states in the Union Room
    public override IReadOnlyList<long> UnionWorkIsGamingPointer { get; } = [0x4E70D70, 0xB8, 0x3C]; // 1 when loaded into Union Room, 0 otherwise
    public override IReadOnlyList<long> UnionWorkIsTalkingPointer { get; } = [0x4E70D70, 0xB8, 0x85];  // 1 when talking to another player or in box, 0 otherwise
    public override IReadOnlyList<long> UnionWorkPenaltyPointer { get; } = [0x4E70D70, 0xB8, 0x90]; // 0 when no penalty, float value otherwise.

    public override IReadOnlyList<long> MyStatusTrainerPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0xE0, 0x0];
    public override IReadOnlyList<long> MyStatusTIDPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0xE8];
    public override IReadOnlyList<long> ConfigTextSpeedPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0xA8];
    public override IReadOnlyList<long> ConfigLanguagePointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0xAC];
    public override IReadOnlyList<long> ItemBlockPointer { get; } = [0x4E7BE98, 0xB8, 0x10, 0x48, 0x20];
}
