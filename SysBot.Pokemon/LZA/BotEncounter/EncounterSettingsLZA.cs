using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Threading;
using SysBot.Base;

namespace SysBot.Pokemon;

public class EncounterSettingsLZA : IBotStateSettings, ICountSettings
{
    private const string Counts = nameof(Counts);
    private const string Encounter = nameof(Encounter);
    private const string Settings = nameof(Settings);
    public override string ToString() => "Encounter Bot ZA Settings";

    [Category(Encounter), Description("When enabled, the bot will continue after finding a suitable match.")]
    public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.StopExit;

    [Category(Settings)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FossilSettingsLZA Fossil { get; set; } = new();

    [Category(Settings)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public OverworldEncounterLZA Overworld { get; set; } = new();

    [Category(Encounter)]
    public class OverworldEncounterLZA
    {
        public override string ToString() => "Overworld Bot Settings";

        [Category(Encounter), Description("Which mode is used to find the target in the overworld.")]
        public OverworldModeLZA Mode { get; set; }

        [Category(Encounter), Description("Stop when maximum (10) shinies are stored (applicable when ONLY searching for shinies)")]
        public bool StopOnMaxShiniesStored { get; set; } = true;

        [Category(Encounter), Description("When enabled, th bot will read StashedShinies from RAM instead of Save Block")]
        public bool FastScan { get; set; } = true;

        [Category(Encounter), Description("Check overworld after amount of bench sitting (applicable when ONLY searching for shinies), use '0' to disable")]
        public int OverworldSpawnCheck { get; set; } = 1;

        [Category(Encounter), Description("Duration in milliseconds to walk forward, then back, after a bench sit or when passing a Wild Zone entrance.")]
        public int WalkDurationMs { get; set; }

        [Category(Encounter), Description("Teleport Coords used, when Mode is Teleport.")]
        public List<Vector3> TeleportCoords { get; set; } = [];

        [Category(Encounter), Description("Duration in milliseconds to teleport.")]
        public int WaitInterval { get; set; }

        [Category(Encounter), Description("Duration in milliseconds to teleport.")]
        public Weather SetWeather { get; set; }

        [Category(Encounter), Description("Duration in milliseconds to teleport.")]
        public TimeOfDay SetTime { get; set; }
    }
    
    [Category(Encounter), Description("When enabled, the 100% catch cheat will be enabled.")]
    public bool EnableCatchCheat { get; set; }

    [Category(Encounter), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
    public bool ScreenOff { get; set; }

    private int _completedWild;
    private int _completedLegend;
    private int _completedFossils;

    [Category(Counts), Description("Encountered Wild Pokémon")]
    public int CompletedEncounters
    {
        get => _completedWild;
        set => _completedWild = value;
    }

    [Category(Counts), Description("Encountered Legendary Pokémon")]
    public int CompletedLegends
    {
        get => _completedLegend;
        set => _completedLegend = value;
    }

    [Category(Counts), Description("Fossil Pokémon Revived")]
    public int CompletedFossils
    {
        get => _completedFossils;
        set => _completedFossils = value;
    }

    [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
    public bool EmitCountsOnStatusCheck { get; set; }

    public int AddCompletedEncounters() => Interlocked.Increment(ref _completedWild);
    public int AddCompletedLegends() => Interlocked.Increment(ref _completedLegend);
    public int AddCompletedFossils() => Interlocked.Increment(ref _completedFossils);

    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedEncounters != 0)
            yield return $"Wild Encounters: {CompletedEncounters}";
        if (CompletedLegends != 0)
            yield return $"Legendary Encounters: {CompletedLegends}";
        if (CompletedFossils != 0)
            yield return $"Completed Fossils: {CompletedFossils}";
    }

    public enum OverworldModeLZA
    {
        BenchSit,
        WildZoneEntrance,
        Teleport
    }
    public enum Weather
    {
        None = -1,
        Clear = 0,
        Overcast = 1,
        Rain = 2,
        StrongWinds = 3,
        Windy = 5,
        MildWinds = 7,
        Fog = 8,
        IntenseSun = 9,
    }

    public enum TimeOfDay
    {
        None = -1,         // Unchanged
        Morning = 14400,   // Beginning of Morning
        Midday = 43200,    // Mid-day
        Night = 72000,     // Beggining of night
        LateNight = 86400  // Mid-Night
    }

    public enum MenuState : byte
    {
        Overworld = 0,
        XMenu = 1,
        LinkPlay = 2,
        LinkTrade = 3,
        InBox = 4,
    }
}
