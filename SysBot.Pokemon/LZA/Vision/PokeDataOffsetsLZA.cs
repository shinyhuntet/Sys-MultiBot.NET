using System.Collections.Generic;

namespace SysBot.Pokemon;

/// <summary>
/// Pokémon Legends: Z-A RAM offsets
/// </summary>
public class PokeDataOffsetsLZA
{
    public const string ZAGameVersion = "2.0.2";
    public const string LegendsZAID = "0100F43008C44000";

    public IReadOnlyList<long> BoxStartPokemonPointer { get; } = [0x610A710, 0xB0, 0x978, 0x0];
    public IReadOnlyList<long> MyStatusPointer { get; } = [0x610A710, 0xA0, 0x40];
    public IReadOnlyList<long> KItemPointer { get; } = [0x610A710, 0xD0, 0x40];
    public IReadOnlyList<long> SaveBlockPointer { get; } = [0x610A670, 0x30, 0x00];
    public IReadOnlyList<long> KLastSavedPointer { get; } = [0x610A670, 0x30, 0x08, 0x2E0];
    public IReadOnlyList<long> KOverworldPointer { get; } = [0x610A670, 0x30, 0x08, 0xA00];
    public IReadOnlyList<long> KStoredShinyEntityBlockPointer { get; } = [0x610A670, 0x30, 0x08, 0x1680];
    public IReadOnlyList<long> KStoredShinyEntityPointer { get; } = [0x610A710, 0x120, 0x168, 0x00];
    public IReadOnlyList<long> PlayerCoordPointer { get; } = [0x47D71A0, 0x248, 0x00, 0x138, 0x90]; // [[[[main+47D71A0]+248]+00]+138]+90
    // Array start
    public IReadOnlyList<long> ArrayStartPointer { get; } = [0x40FE500, 0xB8, 0x378, 0x00];
    // Invalid start
    public IReadOnlyList<long> InvalidStartPointer { get; } = [0x40FE500, 0xB8, 0x380, 0x00];
    // Weather pointer
    public IReadOnlyList<long> WeatherPointer { get; } = [0x612FC30, 0xB0, 0x28, 0x00];
    // Time pointer
    public IReadOnlyList<long> TimePointer { get; } = [0x40FE500, 0x20, 0x40, 0x30];

    //Main Offsets
    public const uint OverworldOffset = 0x610C858;
    public const uint MenuOffset = 0x612DA80;

    public const uint KItemKey = 0x21C9BD44;
    public const int KItemSize = 0xBC00;
    public const uint KOverworldKey = 0x5E8E1711;
    public const uint KStoredShinyEntityKey = 0xF3A8569D;
    public const uint KLastSaved = 0x1522C79C;

    public const int FormatSlotSize = 0x158; // Party format size
    public const int BoxSlotSize = 0x198; // Size between box entries
}
