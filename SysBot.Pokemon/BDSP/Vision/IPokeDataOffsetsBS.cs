using System.Collections.Generic;

namespace SysBot.Pokemon;

public interface IPokeDataOffsetsBS
{
    public ulong PlayerPrefsProviderInstance { get; }    

    public IReadOnlyList<long> MainRNGState { get; }
    public IReadOnlyList<long> R1_SpeciesPointer { get; }
    public IReadOnlyList<long> R2_SpeciesPointer { get; }
    public IReadOnlyList<long> R1_SeedPointer { get; }
    public IReadOnlyList<long> R2_SeedPointer { get; }
    public IReadOnlyList<long> EggFlagPointer { get; }
    public IReadOnlyList<long> EggSeedPointer { get; }
    public IReadOnlyList<long> EggStepPointer { get; }
    public IReadOnlyList<long> LocationPointer { get; }
    public IReadOnlyList<long> PartyStartPokemonPointer { get; }
    public IReadOnlyList<long> PartySlot2PokemonPointer { get; }
    public IReadOnlyList<long> BoxStartPokemonPointer { get; }
    public IReadOnlyList<long> OpponentPokemonPointer { get; }
    public IReadOnlyList<long> DayCareParent1PokemonPointer { get; }
    public IReadOnlyList<long> DayCareParent2PokemonPointer { get; }
    public IReadOnlyList<long> LinkTradePartnerPokemonPointer { get; }
    public IReadOnlyList<long> LinkTradePartnerNamePointer { get; }
    public IReadOnlyList<long> LinkTradePartnerIDPointer { get; }
    public IReadOnlyList<long> LinkTradePartnerParamPointer { get; }
    public IReadOnlyList<long> LinkTradePartnerNIDPointer { get; }
    public IReadOnlyList<long> SceneIDPointer { get; }
    public IReadOnlyList<long> DayTimePointer { get; }
    public IReadOnlyList<long> UnionWorkIsGamingPointer { get; }
    public IReadOnlyList<long> UnionWorkIsTalkingPointer { get; }
    public IReadOnlyList<long> UnionWorkPenaltyPointer { get; }
    public IReadOnlyList<long> MyStatusTrainerPointer { get; }
    public IReadOnlyList<long> MyStatusTIDPointer { get; }
    public IReadOnlyList<long> ConfigTextSpeedPointer { get; }
    public IReadOnlyList<long> ConfigLanguagePointer { get; }
    public IReadOnlyList<long> ItemBlockPointer { get; }
}
