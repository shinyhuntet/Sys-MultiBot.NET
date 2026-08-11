using Discord;
using Discord.Rest;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;
using static System.Buffers.Binary.BinaryPrimitives;

namespace SysBot.Pokemon;

// Thanks to Anubis and Zyro for providing offsets and ideas for LairBot, and Elvis for endless testing with PinkBot!
public sealed class LairBotSWSH : EncounterBotSWSH
{
    private new readonly LairBotSettings Settings;
    private readonly LairBotUtil.PokeMoveInfo.MoveInfoRoot MoveInfo;
    private readonly LairBotUtil LairUtils;
    private readonly IDumper DumpSetting;
    private byte[] OtherItemsPouch = { 0 };
    private byte[] BallPouch = { 0 };
    private LairBall PreviousBall = 0;
    private ulong MainNsoBase;
    private ulong LairMiscScreenCalc;
    private bool StopBot;
    private bool Lost;
    private int LegendFound = 0;
    private int HackyNoteCheck = -1;
    private int Caught;
    private int OldMoveIndex = 0;
    private int LairEncounterCount;
    private int LairCatchCount = 0;
    private int CatchCount = -1;
    private int ResetCount;
    private readonly LairCount AdventureCounts = new();
    private readonly KeepPathTotals KeepPathCounts = new();
    private readonly LairOffsetValues OffsetValues;
    private List<PK8> Rental = new();
    private List<PK8> Encounter = new();
    private PK8 Boss = new();
    private PK8 LairBoss = new();
    private PK8 PlayerPk = new();
    private const uint StandardDamage = 0x7900E808;
    private const uint AlteredDamage = 0x7900E81F;
    private const uint LairLegendariesCount = 29;
    private const uint LairUBCount = 10;
    private uint LairLegendayRand = 0;


    private sealed class LairCount
    {
        public double AdventureCount { get; set; }
        public double WinCount { get; set; }
    }

    private sealed class KeepPathTotals
    {
        public int KeepPathAdventures { get; set; }
        public int KeepPathWins { get; set; }
    }

    private sealed class LairOffsetValues
    {
        public ushort LairLobby { get; set; }
        public ushort LairAdventurePath { get; set; }
        public ushort LairDmax { get; set; }
        public ushort LairBattleMenu { get; set; }
        public ushort LairMovesMenu { get; set; }
        public ushort LairOnCatchScreen { get; set; }
        public ushort LairChoosePokemonScreen { get; set; }
        public ushort LairCatchScreen { get; set; }
        public ushort LairRewardsScreen { get; set; }
    }

    public LairBotSWSH(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg, hub)
    {
        Settings = Hub.Config.EncounterSWSH.LairSWSH;
        DumpSetting = Hub.Config.Folder;
        OffsetValues = ValueParse();
        LairUtils = new LairUtil();
        MoveInfo = LairBotUtil.LoadMoves();
    }

    private class LairUtil : LairBotUtil { }

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        if (Settings.ClearPenaltyOnStart)
            await ClearMaxLairPenalty(token).ConfigureAwait(false);
        if (Settings.LairBotMode == LairBotModes.LairBot)
            await LairBotLoop(token).ConfigureAwait(false);
        else
            await OffsetLogLoop(token).ConfigureAwait(false);

        if (Settings.EnableOHKO)
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(StandardDamage), MainNsoBase + DamageOutputOffset, token).ConfigureAwait(false);
    }

    // For pointer offsets that don't change per session are accessed frequently, so set these each time we start.
    private async Task InitializeSessionOffsets(CancellationToken token)
    {
        Log("Caching session offsets...");
        OverworldOffset = await SwitchConnection.PointerAll(Offsets.OverworldPointer, token).ConfigureAwait(false);
    }
    private async Task LairBotLoop(CancellationToken token)
    {
        //var LostFlag = false;
        int raidCount = 1;
        AdventureCounts.WinCount = 0;
        AdventureCounts.AdventureCount = 0;
        LairEncounterCount = 0;
        LairCatchCount = 0;
        while (!token.IsCancellationRequested)
        {
            //retry:
            Lost = false;
            OldMoveIndex = 0;
            LairUtils.TerrainDur = -1;

            if (raidCount == 1)
            {
                MainNsoBase = await SwitchConnection.GetMainNsoBaseAsync(token).ConfigureAwait(false);
                LairMiscScreenCalc = MainNsoBase + LairMiscScreenOffset;
                Caught = 0;

                while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    await Click(A, 0_500, token).ConfigureAwait(false);

                Log($"{(StopBot ? "Waiting for next Legendary Adventure... Use \"$hunt (Species)\" to select a new Legendary!" : $"Starting a new Adventure...")}");
                if (StopBot)
                {
                    StopBot = false;
                    break;
                }

                if (!await SettingsCheck(token).ConfigureAwait(false))
                    break;

                var species = await SetHuntedLairLegendary(token).ConfigureAwait(false);
                if(species == Species.None)
                {
                    Log("No valid Legendary species selected. Stopping the bot.");
                    break;
                }

                LairLegendayRand = await GetLairLegendaryRand(token).ConfigureAwait(false);

                if (Settings.InjectSeed && Settings.SeedToInject != string.Empty && !await LairSeedInjector(token).ConfigureAwait(false))
                    break;

                ulong seed = BitConverter.ToUInt64(await Connection.ReadBytesAsync(AdventureSeedOffset, 8, token).ConfigureAwait(false), 0);
                Log($"Here is your current Lair Seed: {seed:X16}");
                if (Rental.Count <= 0 || Encounter.Count <= 0)
                    (Rental, Encounter, Boss) = LairRNG.GenerateLairSpeciesList(LairLegendayRand, seed);

                if (Boss.Species == 0)
                {
                    Log("Lair Boss data could not be generated properly.");
                    break;
                }

                Log($"LairHintBoss is {SpeciesName.GetSpeciesNameGeneration(Boss.Species, 2, 8)}{TradeExtensions<PK8>.FormOutput(Boss.Species, Boss.Form, out _)}");

                var winRate = AdventureCounts.AdventureCount > 0 ? $" {AdventureCounts.WinCount}/{AdventureCounts.AdventureCount} adventures won so far." : "";
                Log($"Starting a Solo Adventure for {(species == 0 ? "a random Legendary" : (Species)species)}!{winRate}");
                if (!await RentalRoutine((ushort)species, token).ConfigureAwait(false)) // Enter rental selection.
                    continue;
            }

            while (!await LairStatusCheck(OffsetValues.LairAdventurePath, CurrentScreenLairOffset, token).ConfigureAwait(false)) // Delay until in path select screen.            
                await Task.Delay(2_000, token).ConfigureAwait(false);//Bug Ocurrs!!!


            await Task.Delay(raidCount == 1 ? 11_000 : Settings.SelectPath == SelectPath.GoRight ? 3_500 : 1_500, token).ConfigureAwait(false); // Because map scroll is slow and random dialogue is annoying.

            if (Settings.EnableOHKO) // Enable dirty OHKO.
                await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(AlteredDamage), MainNsoBase + DamageOutputOffset, token).ConfigureAwait(false);

            if (raidCount != 4)
            {
                switch (Settings.SelectPath) // Choose a path to take.
                {
                    case SelectPath.GoLeft: await Click(A, 3_000, token).ConfigureAwait(false); break;
                    case SelectPath.GoRight: await Click(DRIGHT, 1_000, token).ConfigureAwait(false); await Click(A, 3_000, token).ConfigureAwait(false); break;
                }
                ;
            }
            while (!await IsInBattle(token).ConfigureAwait(false)) // Will also deal with possible Scientists and Backpackers.
                await Click(A, 2_000, token).ConfigureAwait(false);

            /*if ((LairBoss is null || LairBoss.Species == 0) && (raidCount == 1))
            {
                await Task.Delay(Config.Connection.Protocol == SwitchProtocol.USB ? 6_000 : 7_000).ConfigureAwait(false);
                Log("Reading legendary pokemon data...");
            }
            var stopwatch = new Stopwatch();
            stopwatch.Restart();
            while ((LairBoss is null || LairBoss.Species == 0) && raidCount == 1)
            {
                LairBoss = await ReadLairLegendary(token).ConfigureAwait(false) ?? new();
                if (stopwatch.Elapsed >= TimeSpan.FromSeconds(10))
                {
                    Log("Not in Battle, Pointer may be incorrect. End Reading Lair Legendary...");
                    if (Settings.UseStopConditionsPathReset || LostFlag)
                    {
                        await GameRestart(token).ConfigureAwait(false);
                        if (await GetDyniteCount(token).ConfigureAwait(false) < 10)
                        {
                            Log("Restoring Dynite Ore...");
                            await SetDyniteCount(token).ConfigureAwait(false);
                        }
                        await ClearMaxLairPenalty(token).ConfigureAwait(false);
                        LairBoss = new();
                        goto retry;
                    }
                    break;
                }
            }
            if (raidCount == 1 && Settings.UseStopConditionsPathReset && LairBoss != null)
            {
                if (!await LegendReset(token).ConfigureAwait(false))
                    continue;
            }
            else if (raidCount == 1 && LairBoss != null)
            {
                Log("Reading legendary Pokémon offset...");
                TradeExtensions<PK8>.EncounterLogs(LairBoss);
                Log($"AdventureCount {AdventureCounts.AdventureCount + 1} {Environment.NewLine}{ShowdownParsing.GetShowdownText(LairBoss)}{Environment.NewLine}");
            }*/
            var lairPk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, 344, token).ConfigureAwait(false);
            lairPk ??= new();

#pragma warning disable CS8601 // Possible null reference assignment.
            var party = new PK8[3]
            {
                await ReadUntilPresent(LairPartyP2Offset, 0_500, 0_200, 344, token).ConfigureAwait(false),
                await ReadUntilPresent(LairPartyP3Offset, 0_500, 0_200, 344, token).ConfigureAwait(false),
                await ReadUntilPresent(LairPartyP4Offset, 0_500, 0_200, 344, token).ConfigureAwait(false),
            };
            PlayerPk = await ReadUntilPresent(LairPartyP1Offset, 0_500, 0_200, 344, token).ConfigureAwait(false);
#pragma warning restore CS8601 // Possible null reference assignment.

            foreach (var species in party)
            {
                LogUtil.LogText($"NPC Pokemon{party.ToList().IndexOf(species) + 1}: {SpeciesName.GetSpeciesNameGeneration(species.Species, 2, 8)}{TradeExtensions<PK8>.FormOutput(species.Species, species.Form, out _)}");
            }
            LairEncounterCount++;
            Log($"Raid Battle {raidCount}. Encounter {LairEncounterCount}: {SpeciesName.GetSpeciesNameGeneration(lairPk.Species, 2, 8)}{TradeExtensions<PK8>.FormOutput(lairPk.Species, lairPk.Form, out _)}{(raidCount == 4 ? $".{Environment.NewLine}More details{Environment.NewLine}{ShowdownParsing.GetShowdownText(lairPk)}" : ".")}");
            PlayerPk ??= new();

            Log($"Sending out: {SpeciesName.GetSpeciesNameGeneration(PlayerPk.Species, 2, 8)}{TradeExtensions<PK8>.FormOutput(PlayerPk.Species, PlayerPk.Form, out _)}.");
            await BattleRoutine(party, lairPk, token).ConfigureAwait(false);
            //LostFlag = Lost;

            if (raidCount == 4 || Lost)
            {
                AdventureCounts.AdventureCount++;
                if (!Settings.InjectSeed && !Settings.EnableOHKO && !Settings.FastMode && !Settings.CatchLairPokémon && Settings.KeepPath)
                    KeepPathCounts.KeepPathAdventures++;
            }

            if (Lost) // We've lost the battle, exit back to main loop.
            {
                Log($"Lost Adventure {AdventureCounts.AdventureCount}.");
                if (Caught > 0)
                    await Results(token).ConfigureAwait(false);
                raidCount = 1;
                LairBoss = new();
                Rental = new();
                Encounter = new();
                continue;
            }

            await CatchRoutine(raidCount, party, lairPk, token).ConfigureAwait(false);
            if (raidCount == 4) // Final raid complete.
            {
                if (!Settings.InjectSeed && !Settings.EnableOHKO && !Settings.FastMode && !Settings.CatchLairPokémon && Settings.KeepPath)
                    KeepPathCounts.KeepPathWins++;

                AdventureCounts.WinCount++;
                Log($"Adventure {AdventureCounts.AdventureCount} completed.");
                await Results(token).ConfigureAwait(false);
                raidCount = 1;
                LairBoss = new();
                Rental = new();
                Encounter = new();
                continue;
            }
            raidCount++;
        }
    }
    private async Task<bool> IsMatchMaxLairLegendary(CancellationToken token)
    {
        ResetCount++;
        List<TargetShinyType> OriginalSettings = new();
        foreach (var setting in Hub.Config.StopConditions.SearchConditions)
        {
            if (!setting.IsEnabled)
                continue;
            OriginalSettings.Add(setting.ShinyTarget);
            setting.ShinyTarget = TargetShinyType.DisableOption;
        }
        var valid = false;
        ulong ofs = 0;

        while (!valid)
            (valid, ofs) = await ValidatePointerAll(Offsets.MaxLairPokemonRNGPointer, token).ConfigureAwait(false);

        var (s0, s1) = await GetMaxLairRNGState(ofs, token).ConfigureAwait(false);

        LairBoss = LairRNG.GetMaxLairLegendary(Settings.LairSpeciesQueue[0], s0, s1);
        Log($"{(Settings.UseStopConditionsPathReset ? $"Reset {ResetCount}" : $"AdventureCount {AdventureCounts.AdventureCount + 1}")} {Environment.NewLine}{ShowdownParsing.GetShowdownText(LairBoss)}{Environment.NewLine}");

        if (Settings.UseStopConditionsPathReset && !StopConditionSettings.EncounterFound(LairBoss, Hub.Config.StopConditions))
        {
            Log("No match found, restarting the game...");
            await GameRestart(token).ConfigureAwait(false);

            if (await GetDyniteCount(token).ConfigureAwait(false) < 10)
            {
                Log("Restoring Dynite Ore...");
                await SetDyniteCount(token).ConfigureAwait(false);
            }
            await ClearMaxLairPenalty(token).ConfigureAwait(false);
            LairBoss = new();
            return false;
        }

        Log("Stats match conditions, now let's continue the adventure and check if it's shiny...");
        var OriginalIndex = 0;
        for (int i = 0; i < Hub.Config.StopConditions.SearchConditions.Count; i++)
        {
            if (!Hub.Config.StopConditions.SearchConditions[i].IsEnabled)
                continue;
            Hub.Config.StopConditions.SearchConditions[i].ShinyTarget = OriginalSettings[OriginalIndex];
            OriginalIndex++;
        }
        foreach (var rental in Rental)
        {
            Log($"RentalMon {Rental.IndexOf(rental) + 1}: {SpeciesName.GetSpeciesNameGeneration(rental.Species, 2, 8)}{TradeExtensions<PK8>.FormOutput(rental.Species, PlayerPk.Form, out _)}.");
        }
        for (int i = 0; i < 4; i++)
        {
            var pk = LairRNG.GetMaxLairNormalPokemon(s0, s1, Rental, 0, i);
            Log($"Rental Pokemon(NPC) {i + 1}{Environment.NewLine}{ShowdownParsing.GetShowdownText(pk)}{Environment.NewLine}");
        }
        for (int i = 0; i < Encounter.Count; i++)
        {
            var pk = LairRNG.GetMaxLairNormalPokemon(s0, s1, Encounter, i);
            Log($"Lair Encounter Pokemon {i + 1}{Environment.NewLine}{ShowdownParsing.GetShowdownText(pk)}{Environment.NewLine}");
        }
        return true;
    }
    public async Task<(ulong s0, ulong s1)> GetMaxLairRNGState(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 16, token).ConfigureAwait(false);
        var s0 = BitConverter.ToUInt64(data, 0);
        var s1 = BitConverter.ToUInt64(data, 8);

        Log($"Lair Pokémon RNG state: {s0:X16}, {s1:X16}");

        return (s0, s1);
    }
    private async Task<bool> RentalRoutine(ushort noteSpecies, CancellationToken token)
    {
        bool match = false;
        uint[] RentalOfsList = { RentalMon1, RentalMon2, RentalMon3 };
        PK8 lairPk = new() { Species = noteSpecies };
        List<int> speedStat = new();
        List<PK8> pkList = new();
        List<double> damage = new();
        int monIndex = -1;

        await LairEntry(token).ConfigureAwait(false);
        for (int i = 0; i < RentalOfsList.Length; i++)
        {
            var pk = await ReadUntilPresent(RentalOfsList[i], 2_000, 0_200, 344, token).ConfigureAwait(false);
            if (pk == null)
            {
                Log("Entered the lobby too fast, correcting...");
                while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    await Click(B, 0_500, token).ConfigureAwait(false);

                await LairEntry(token).ConfigureAwait(false);
                continue;
            }

            if (!match)
                match = await IsMatchMaxLairLegendary(token).ConfigureAwait(false);

            if (!match)
                return false;

            pkList.Add(pk);
            int moveIndex = LairBotUtil.PriorityIndex(pk);
            if (Settings.EnableOHKO || Settings.FastMode)
            {
                if (moveIndex != -1) // Add Ditto override because Imposter is fun?
                {
                    monIndex = i;
                    break;
                }
                else
                {
                    speedStat.Add(LairBotUtil.CalculateEffectiveStat(pk.IV_SPE, pk.EV_SPE, pk.PersonalInfo.SPE, pk.CurrentLevel));
                }
            }
            else
            {
                damage.Add(LairUtils.WeightedDamage(new PK8[] { new() }, pk, lairPk, MoveInfo, false).Max());
            }
        }

        if (!Settings.EnableOHKO && !Settings.FastMode)
            monIndex = damage.IndexOf(damage.Max());

        var speedIndex = speedStat.Count > 0 ? speedStat.IndexOf(speedStat.Max()) : 0;
        var selection = pkList[monIndex == -1 ? speedIndex : monIndex];
        Log($"Selecting {SpeciesName.GetSpeciesNameGeneration(selection.Species, 2, 8)}{TradeExtensions<PK8>.FormOutput(selection.Species, selection.Form, out _)}.");
        await MoveAndRentalClicks(monIndex == -1 ? speedIndex : monIndex, token).ConfigureAwait(false);
        return true;
    }

    private async Task<LairBotUtil.PokeMoveInfo> SelectMove(PK8[] party, PK8 lairMon, bool stuck, int turn, bool dmax, bool dmaxEnded, CancellationToken token)
    {
        int[] movePP = new int[] { PlayerPk.Move1_PP, PlayerPk.Move2_PP, PlayerPk.Move3_PP, PlayerPk.Move4_PP };
        var dmgWeight = LairUtils.WeightedDamage(party, PlayerPk, lairMon, MoveInfo, dmax).ToList();
        var priorityMove = PlayerPk.Moves.ToList().IndexOf(PlayerPk.Moves.Intersect((IEnumerable<ushort>)Enum.GetValues(typeof(PriorityMoves))).FirstOrDefault());
        bool priority = (Settings.EnableOHKO || Settings.FastMode) && priorityMove != -1 && dmgWeight[priorityMove] > 0 && lairMon.Ability != (int)Ability.PsychicSurge && lairMon.Ability != (int)Ability.QueenlyMajesty && lairMon.Ability != (int)Ability.Dazzling;

        var bestMove = dmgWeight.IndexOf(dmgWeight.Max());
        bool movePass = false;
        while (!movePass)
        {
            var move = MoveInfo.Moves.FirstOrDefault(x => x.MoveID == PlayerPk.Moves[priority ? priorityMove : bestMove])!;
            bool recoil = move.Recoil >= 206 && move.EffectSequence >= 48;
            if ((stuck && (OldMoveIndex == (priority ? priorityMove : bestMove))) || ((Settings.EnableOHKO || Settings.FastMode) && (recoil || move.Charge)) || move.MoveID == (int)Move.Belch)
            {
                dmgWeight[priority ? priorityMove : bestMove] = 0.0;
                bestMove = dmgWeight.IndexOf(dmgWeight.Max());
                priority = false;
                stuck = false;
                continue;
            }
            else if (priority)
            {
                bestMove = priorityMove;
            }
            movePass = true;
        }

        var finalMove = MoveInfo.Moves.FirstOrDefault(x => x.MoveID == PlayerPk.Moves[bestMove])!;
        int dmaxMove = finalMove.Category != MoveCategory.Status ? (int)finalMove.Type : 18;
        Log($"Turn {turn}: Selecting {(dmax ? (DmaxMoves)dmaxMove : (Move)PlayerPk.Moves[bestMove])}.");
        var index = bestMove - OldMoveIndex;
        if (dmaxEnded)
            index = bestMove;

        await MoveAndRentalClicks(index, token).ConfigureAwait(false);
        OldMoveIndex = bestMove;
        return finalMove;
    }

    private bool CheckIfUpgrade(PK8[] party, PK8 lairPk)
    {
        bool upgrade = false;
        var dmgWeightPlayer = LairUtils.WeightedDamage(party, PlayerPk.Species == 132 ? LairBoss : PlayerPk, LairBoss, MoveInfo, false);
        var dmgWeightLair = LairUtils.WeightedDamage(new PK8[] { new() }, lairPk, LairBoss, MoveInfo, false);

        if (Settings.EnableOHKO || Settings.FastMode)
        {
            var ourSpeed = LairBotUtil.CalculateEffectiveStat(PlayerPk.IV_SPE, PlayerPk.EV_SPE, PlayerPk.PersonalInfo.SPE, PlayerPk.CurrentLevel);
            bool noPriority = LairBotUtil.PriorityIndex(PlayerPk) == -1;
            var lairPkSpeed = LairBotUtil.CalculateEffectiveStat(lairPk.IV_SPE, lairPk.EV_SPE, lairPk.PersonalInfo.SPE, lairPk.CurrentLevel);
            bool lairPkPriority = LairBotUtil.PriorityIndex(lairPk) != -1;

            var maxDmgMoveIndex = dmgWeightPlayer.ToList().IndexOf(dmgWeightPlayer.Max());
            var move = MoveInfo.Moves.FirstOrDefault(x => x.MoveID == PlayerPk.Moves[maxDmgMoveIndex])!;

            if (move.Charge || move.MoveID == (int)Move.Belch)
            {
                dmgWeightPlayer[maxDmgMoveIndex] = 0.0;
                if (!dmgWeightPlayer.Any(x => x > 0.0))
                    upgrade = true;
            }

            if ((noPriority && (lairPkSpeed > ourSpeed)) || (lairPkPriority && noPriority))
                upgrade = true;
        }
        else if (dmgWeightLair.Max() > dmgWeightPlayer.Max())
        {
            upgrade = true;
        }

        if (upgrade && lairPk.Species != LairBoss.Species)
            Log("Lair encounter is better than our current Pokémon. Going to catch it and swap our current Pokémon.");

        return upgrade;
    }
    private async Task<PK8?> ReadLairLegendary(CancellationToken token)
    {
        bool Valid = false;
        ulong Offset = 0;
        while (!Valid)
            (Valid, Offset) = await ValidatePointerAll(Offsets.MaxLairLegendaryPointer, token).ConfigureAwait(false);
        var pkm = await ReadUntilPresentAbsolute(Offset, 2_000, 0_200, token).ConfigureAwait(false);
        if (pkm is null)
            return null;
        bool isValid = PersonalTable.SWSH.IsPresentInGame(pkm.Species, pkm.Form);
        if (isValid && pkm != null && pkm.ChecksumValid && pkm.Valid && pkm.Species > 0 && (Species)pkm.Species <= Species.MAX_COUNT)
            return pkm;
        return null;
    }

    private async Task BattleRoutine(PK8[] party, PK8 lairPk, CancellationToken token)
    {
        int turn = 0;
        int dmaxEnd = 0;
        bool stuck = false;
        bool fainted = false;
        bool dmax = false;
        bool canDmax = false;
        bool asleep = false;
        var sleephax = 0x00000322;
        var hphax = 0x00000001;
        LairBotUtil.PokeMoveInfo move = new();

        while (true)
        {
            while (!await LairStatusCheckMain(OffsetValues.LairMovesMenu, LairMiscScreenCalc, token).ConfigureAwait(false))
            {
                if (await LairStatusCheckMain(OffsetValues.LairBattleMenu, LairMiscScreenCalc, token).ConfigureAwait(false))
                {
                    turn++;
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    if (!await LairStatusCheckMain(OffsetValues.LairMovesMenu, LairMiscScreenCalc, token).ConfigureAwait(false) && !fainted)
                    {
                        Log($"Turn {turn}: Cheering on...");
                        fainted = true;
                        canDmax = false;
                        dmaxEnd = 0;
                    }
                }
                else
                {
                    Lost = await LairStatusCheck(Caught > 0 ? OffsetValues.LairRewardsScreen : OffsetValues.LairLobby, CurrentScreenLairOffset, token).ConfigureAwait(false) || !await IsInBattle(token).ConfigureAwait(false);
                    if (await LairStatusCheckMain(OffsetValues.LairCatchScreen, LairMiscScreenCalc, token).ConfigureAwait(false) || Lost)
                        return;

                    if (!Settings.EnableOHKO && !canDmax && await LairStatusCheckMain(OffsetValues.LairDmax, LairMiscScreenCalc, token).ConfigureAwait(false))
                    {
                        await Task.Delay(2_000, token).ConfigureAwait(false);
                        if (await LairStatusCheckMain(OffsetValues.LairBattleMenu, LairMiscScreenCalc, token).ConfigureAwait(false))
                            canDmax = true;
                    }

                    await Click(B, 0_300, token).ConfigureAwait(false);
                }
            }

            fainted = false;
            if (stuck)
            {
                Log($"{(dmax ? (DmaxMoves)move.Type : (Move)PlayerPk.Moves[OldMoveIndex])} cannot be executed, trying to select a different move.");
                for (int i = 0; i < 2; i++)
                    await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
            }

            var newPlayerPk = await ReadUntilPresent(LairPartyP1Offset, 2_000, 0_200, 344, token).ConfigureAwait(false);
            var newLairPk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, 344, token).ConfigureAwait(false);
            if (newPlayerPk != null && newLairPk != null)
            {
                PlayerPk = newPlayerPk.Species == 132 ? newLairPk : newPlayerPk;
                lairPk = newLairPk.Species == 132 ? newPlayerPk : newLairPk;
                if (newPlayerPk.Species == 132)
                {
                    PlayerPk.Move1_PP = await GetPPCount(0, token).ConfigureAwait(false);
                    PlayerPk.Move2_PP = await GetPPCount(1, token).ConfigureAwait(false);
                    PlayerPk.Move3_PP = await GetPPCount(2, token).ConfigureAwait(false);
                    PlayerPk.Move4_PP = await GetPPCount(3, token).ConfigureAwait(false);
                }
                if (Settings.FastMode)
                {
                    asleep = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAsync(OpponetRaidPokemonAsleepOffset, 4, token).ConfigureAwait(false)) == sleephax;
                    if (!asleep)
                        await SwitchConnection.WriteBytesAsync(BitConverter.GetBytes(sleephax), OpponetRaidPokemonAsleepOffset, token).ConfigureAwait(false);
                    if (lairPk.Stat_HPCurrent > 1)
                        await SwitchConnection.WriteBytesAsync(BitConverter.GetBytes(hphax), OpponentRaidPokemonHPOffset, token).ConfigureAwait(false);
                }
            }

            bool dmaxEnded = dmax && dmaxEnd == 0;
            if (dmaxEnded)
                dmax = false;

            if (!Settings.FastMode && !Settings.EnableOHKO && !dmax && canDmax)
            {
                await Click(DLEFT, 0_400, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
                Log(PlayerPk.CanGigantamax ? "Gigantamaxing..." : "Dynamaxing...");
                dmax = true;
                canDmax = false;
                dmaxEnd = 3;
            }

            move = await SelectMove(party, lairPk, stuck, turn, dmax, dmaxEnded, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(B, 1_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);

            if (await LairStatusCheckMain(OffsetValues.LairMovesMenu, LairMiscScreenCalc, token).ConfigureAwait(false))
            {
                stuck = true;
            }
            else
            {
                stuck = false;
                if (dmax)
                    dmaxEnd--;
            }
        }
    }

    private async Task CatchRoutine(int raidCount, PK8[] party, PK8 lairPk, CancellationToken token)
    {
        bool upgrade = false;
        if (Settings.UpgradePokemon && raidCount != 4)
            upgrade = CheckIfUpgrade(party, lairPk);

        while (!await LairStatusCheckMain(OffsetValues.LairCatchScreen, LairMiscScreenCalc, token).ConfigureAwait(false))
            await Task.Delay(6_000, token).ConfigureAwait(false);
        await Task.Delay(6_000, token).ConfigureAwait(false);
        if (Settings.CatchLairPokémon || upgrade || raidCount == 4) // We want to catch the legendary regardless of settings for catching.
        {
            await SelectCatchingBall(token).ConfigureAwait(false); // Select ball to catch with.
            Log($"Catching {(raidCount < 4 ? "encounter" : "legendary")}...");
            await Task.Delay(raidCount == 4 ? 35_000 : 25_000, token).ConfigureAwait(false);
            while (await LairStatusCheckMain(OffsetValues.LairOnCatchScreen, LairMiscScreenCalc, token).ConfigureAwait(false) && !await LairStatusCheckMain(OffsetValues.LairChoosePokemonScreen, LairMiscScreenCalc, token).ConfigureAwait(false))
                await Task.Delay(3_000, token).ConfigureAwait(false);
            if (raidCount < 4)
            {
                if (!upgrade)
                    await Click(DDOWN, 1_000, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
            }
            else
            {
                while (await LairStatusCheckMain(OffsetValues.LairOnCatchScreen, LairMiscScreenCalc, token).ConfigureAwait(false))
                    await Task.Delay(3_000, token).ConfigureAwait(false);
            }
            CatchCount--;
            Caught++;
            while (!await LairStatusCheck(raidCount == 4 ? OffsetValues.LairRewardsScreen : OffsetValues.LairAdventurePath, CurrentScreenLairOffset, token).ConfigureAwait(false))
                await Task.Delay(raidCount == 4 ? 5_000 : 1_000, token).ConfigureAwait(false);
        }
        else
        {
            await Click(DDOWN, 1_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            while (!await LairStatusCheck(OffsetValues.LairAdventurePath, CurrentScreenLairOffset, token).ConfigureAwait(false))
                await Task.Delay(1_000, token).ConfigureAwait(false);
        }

        Log($"{(raidCount == 4 || Settings.CatchLairPokémon || upgrade ? "Caught" : "Defeated")} {SpeciesName.GetSpeciesNameGeneration(lairPk.Species, 2, 8)}{TradeExtensions<PK8>.FormOutput(lairPk.Species, lairPk.Form, out _)}.");
    }

    private async Task Results(CancellationToken token)
    {
        Settings.AddCompletedAdventures();
        int index = -1;

        while (!await LairStatusCheck(OffsetValues.LairRewardsScreen, CurrentScreenLairOffset, token).ConfigureAwait(false))
            await Task.Delay(5_000, token).ConfigureAwait(false);

        LairDetail.ResetAssets();
        for (int i = 0; i < Caught; i++)
        {
            var jumpAdj = i == 0 ? 0x00 : i == 1 ? 0x08 : i == 2 ? 0x10 : 0x18;
            IReadOnlyList<long> pointer = [0x28F4060, 0x1B0, 0x68, 0x58 + jumpAdj, 0x58, 0x00];
            var offset = await SwitchConnection.PointerAll(pointer, token);
            while (offset == 0)
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                Log($"Waiting for the offset address for {(i == Caught - 1 ? "legendary" : i == 0 ? "first" : i == 1 ? "secound" : i == 2 ? "third" : "forth")} lair reward pokemon");
                offset = await SwitchConnection.PointerAll(pointer, token);
            }
            var pk = await ReadUntilPresentAbsolute(offset, 2_000, 0_200, token).ConfigureAwait(false);


            if (pk != null)
            {
                LairCatchCount++;
                bool found = StopConditionSettings.EncounterFound(pk, Hub.Config.StopConditions);
                if (pk.IsShiny || found)
                    index = Settings.CatchLairPokémon ? i : Caught - (Caught - i);

                bool caughtLegend = !Lost && (Caught - 1 == index);
                if (caughtLegend)
                {
                    HackyNoteCheck = -1;
                    LegendFound = pk.Species;
                }

                bool caughtRegular = !caughtLegend && pk.IsShiny;
                if (!Settings.UseStopConditionsPathReset && found)
                    StopBot = true;
                if (caughtLegend && (Settings.UseStopConditionsPathReset && found || Settings.StopOnLegendary))
                    StopBot = true;

                if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                    DumpPokemon(DumpSetting.DumpFolder, "lairs", pk);

                if (Settings.AlwaysOutputShowdown)
                    Log($"Adventure {AdventureCounts.AdventureCount}.{Environment.NewLine}{ShowdownParsing.GetShowdownText(pk)}{Environment.NewLine}");

                await LairDetail.SetPokeDetail(pk, LairCatchCount, i, token).ConfigureAwait(false);
                await WebHook.SendNotification(pk, (found ? "Target Encounter Found!" : pk.IsShiny ? "Unwanted Shiny Found." : "Unwanted Enounter..."), token).ConfigureAwait(false);

                if (LairBotUtil.EmbedsInitialized && Settings.ResultsEmbedChannels != string.Empty && (caughtLegend || caughtRegular))
                {
                    LairBotUtil.EmbedMon = (pk, caughtLegend);
                }
                else
                {
                    if (caughtLegend)
                        EchoUtil.Echo($"{Hub.Config.StopConditions.MatchFoundEchoMention} Shiny Legendary found!\nEncounter {LairEncounterCount}. Adventure {AdventureCounts.AdventureCount}.{Environment.NewLine}{ShowdownParsing.GetShowdownText(pk)}{Environment.NewLine}");
                    else if (caughtRegular)
                        EchoUtil.Echo($"{Hub.Config.StopConditions.MatchFoundEchoMention} Found a shiny, but it's not quite legendary...\nEncounter {LairEncounterCount}. Adventure {AdventureCounts.AdventureCount}.{Environment.NewLine}{ShowdownParsing.GetShowdownText(pk)}{Environment.NewLine}");
                }
            }
        }

        if (Settings.EnableOHKO)
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(StandardDamage), MainNsoBase + DamageOutputOffset, token).ConfigureAwait(false);

        if (!Settings.InjectSeed && !Settings.EnableOHKO && !Settings.FastMode && Settings.KeepPath && !Settings.CatchLairPokémon && LegendFound == 0)
        {
            double winRate = KeepPathCounts.KeepPathWins / KeepPathCounts.KeepPathAdventures;
            if (KeepPathCounts.KeepPathAdventures < 5 || (KeepPathCounts.KeepPathAdventures >= 5 && winRate >= 0.3))
            {
                Log($"{(Lost ? "" : "No shiny legendary found. ")}Resetting the game to keep the seed.");
                await GameRestart(token).ConfigureAwait(false);
                if (await GetDyniteCount(token).ConfigureAwait(false) < 10)
                {
                    Log("Restoring Dynite Ore...");
                    await SetDyniteCount(token).ConfigureAwait(false);
                }
            }
            else if (KeepPathCounts.KeepPathAdventures >= 5 && winRate < 0.3)
            {
                KeepPathCounts.KeepPathWins = 0;
                KeepPathCounts.KeepPathAdventures = 0;
                await Task.Delay(1_000).ConfigureAwait(false);
                await Click(B, 1_000, token).ConfigureAwait(false);
                Log("Our win ratio isn't looking too good... Rolling our path.");
            }
            return;
        }

        await Task.Delay(1_000).ConfigureAwait(false);
        if (index == -1)
        {
            await Click(B, 1_000, token).ConfigureAwait(false);
            Log("No results found... Going deeper into the lair...");
            return;
        }

        for (int y = 0; y < index; y++)
            await Click(DDOWN, 0_250, token).ConfigureAwait(false);

        if (Hub.Config.StopConditions.CaptureVideoClip)
        {
            await Click(A, 1_000, token).ConfigureAwait(false);
            await Click(DDOWN, 1_000, token).ConfigureAwait(false);
            await Click(A, 2_000, token).ConfigureAwait(false);
            await PressAndHold(CAPTURE, 2_000, 10_000, token).ConfigureAwait(false);
            await Click(B, 4_000, token).ConfigureAwait(false);
        }
    }
    private async Task<bool> LegendReset(CancellationToken token)
    {
        ResetCount++;
        List<TargetShinyType> OriginalSettings = new();
        foreach (var setting in Hub.Config.StopConditions.SearchConditions)
        {
            if (!setting.IsEnabled)
                continue;
            OriginalSettings.Add(setting.ShinyTarget);
            setting.ShinyTarget = TargetShinyType.DisableOption;
        }
        Log("Reading legendary Pokémon offset...");
        Log($"Reset {ResetCount} {Environment.NewLine}{ShowdownParsing.GetShowdownText(LairBoss)}{Environment.NewLine}");

        if (!StopConditionSettings.EncounterFound(LairBoss, Hub.Config.StopConditions))
        {
            Log("No match found, restarting the game...");
            await GameRestart(token).ConfigureAwait(false);

            if (await GetDyniteCount(token).ConfigureAwait(false) < 10)
            {
                Log("Restoring Dynite Ore...");
                await SetDyniteCount(token).ConfigureAwait(false);
            }
            await ClearMaxLairPenalty(token).ConfigureAwait(false);
            LairBoss = new();
            return false;
        }

        Log("Stats match conditions, now let's continue the adventure and check if it's shiny...");
        var OriginalIndex = 0;
        for (int i = 0; i < Hub.Config.StopConditions.SearchConditions.Count; i++)
        {
            if (!Hub.Config.StopConditions.SearchConditions[i].IsEnabled)
                continue;
            Hub.Config.StopConditions.SearchConditions[i].ShinyTarget = OriginalSettings[OriginalIndex];
            OriginalIndex++;
        }
        return true;
    }

    private async Task<int> GetDyniteCount(CancellationToken token)
    {
        OtherItemsPouch = await Connection.ReadBytesAsync(OtherItemAddress, OtherItemPouchInventoryLength * 4, token).ConfigureAwait(false);
        var pouch = new InventoryPouch8(InventoryType.Items, ItemStorage8SWSH.Instance, 999, 0, OtherItemPouchInventoryLength);
        pouch.GetPouch(OtherItemsPouch);
        if (pouch.Items.FirstOrDefault(x => x.Index == 1604) == null || pouch.Items.FirstOrDefault(x => x.Index == 1604) is null)
            return 0;
        return pouch.Items.FirstOrDefault(x => x.Index == 1604)!.Count;
    }
    private async Task SetDyniteCount(CancellationToken token)
    {
        var TrainerSav = new SAV8SWSH();
        var Itempouch = new InventoryPouch8(InventoryType.Items, ItemStorage8SWSH.Instance, 999, 0, OtherItemPouchInventoryLength);
        Itempouch.GetPouch(OtherItemsPouch);
        var OriginalItems = Array.ConvertAll(Itempouch.Items, item => item.Index);
        Itempouch.GiveItem(TrainerSav, 1604);
        var writeItems = (InventoryItem8[])Itempouch.Items;
        var data = new byte[OtherItemsPouch.Length];
        for (int i = 0; i < writeItems.Length; i++)
        {
            var ItemIndex = i * 4;
            uint val = writeItems[i].GetValue(false, OriginalItems);
            WriteUInt32LittleEndian(data.AsSpan()[ItemIndex..(ItemIndex + 4)], val);

        }
        await Connection.WriteBytesAsync(data, OtherItemAddress, token).ConfigureAwait(false);
    }
    private async Task SetBallCount(CancellationToken token)
    {
        var TrainerSav = new SAV8SWSH();
        var ballpouch = new InventoryPouch8(InventoryType.Balls, ItemStorage8SWSH.Instance, 999, 0, PokeBallPouchInventoryLength);
        ballpouch.GetPouch(BallPouch);
        var OriginalItems = Array.ConvertAll(ballpouch.Items, item => item.Index);
        var index = new BallPouchUtil().BallIndex((int)Settings.LairBall);
        var Count = ballpouch.GiveItem(TrainerSav, (ushort)index);
        var data = new byte[BallPouch.Length];
        var writePouch = (InventoryItem8[])ballpouch.Items;
        for (int i = 0; i < writePouch.Length; i++)
        {
            var ItemIndex = i * 4;
            uint val = writePouch[i].GetValue(false, OriginalItems);
            WriteUInt32LittleEndian(data.AsSpan()[ItemIndex..(ItemIndex + 4)], val);
        }
        await Connection.WriteBytesAsync(data, PokeBallOffset, token).ConfigureAwait(false);
        Log($"Target Ball Count is set completely!{Environment.NewLine}LairBall: {(Ball)Settings.LairBall}, Count: {Count}");
    }
    private async Task<int> GetPokeBallCount(CancellationToken token)
    {
        BallPouch = await Connection.ReadBytesAsync(PokeBallOffset, PokeBallPouchInventoryLength * 4, token).ConfigureAwait(false);
        var counts = BallPouchUtil.GetBallCounts(BallPouch);
        return counts.PossibleCatches((Ball)Settings.LairBall);
    }
    private async Task ClearMaxLairPenalty(CancellationToken token)
    {
        Log("Cleaing MaxLair Penalty...");
        var data = BitConverter.GetBytes(0);
        await Connection.WriteBytesAsync(data, MaxLairPenaltyWarnOffset, token).ConfigureAwait(false);
        await Connection.WriteBytesAsync(data, MaxLairPenaltyCountOffset, token).ConfigureAwait(false);
    }
    private async Task SelectCatchingBall(CancellationToken token)
    {
        Log($"Selecting {Settings.LairBall} Ball...");
        await Click(A, 0_500, token).ConfigureAwait(false);
        var lairBall = (Ball)Settings.LairBall;
        var index = new BallPouchUtil().BallIndex((int)Settings.LairBall);
        bool valid = false;
        ulong ofs = 0;
        while (!valid)
            (valid, ofs) = await ValidatePointerAll(Offsets.CurrentBallIndexPointer, token).ConfigureAwait(false);
        while (true)
        {
            int ball = BitConverter.ToInt32(await SwitchConnection.ReadBytesAbsoluteAsync(ofs, 4, token).ConfigureAwait(false), 0);
            Log($"Current index is {ball}, so current ball is {GameInfo.GetStrings(GameLanguage.LanguageCode(1)).itemlist[ball]}");
            if (ball == index)
                break;
            if (lairBall.IsApricornBall())
                await Click(DLEFT, 0_050, token).ConfigureAwait(false);
            else await Click(DRIGHT, 0_050, token).ConfigureAwait(false);
        }
        await Click(A, 0_500, token).ConfigureAwait(false);
    }

    private async Task ResetLegendaryFlag(int species, CancellationToken token)
    {
        if (species == 0)
            return;

        var (offset, start) = await GetFlagOffset(species, token).ConfigureAwait(false);
        if (start != 0)
        {
            var index = (offset - start) / 0x38;
            Log($"Legendary Flag {(Species)species} Address: {offset:X}, Index:{index}!");
        }
        if (offset == 0)
            return;
        var val = BitConverter.ToUInt16(await Connection.ReadBytesAsync(offset, 2, token).ConfigureAwait(false));
        if (val == 0)
        {
            Log($"{(LairSpecies)species} is not Caught");
            return;
        }
        Log($"Resetting {(LairSpecies)species} Caught Flag...");
        await Connection.WriteBytesAsync(new byte[1], offset, token).ConfigureAwait(false);
        Log($"Complete!");
    }

    private async Task<(uint, uint)> GetFlagOffset(int species, CancellationToken token)
    {
        if (species == 0)
            return (0, 0);

        var index = Array.IndexOf(Enum.GetValues(typeof(LairSpeciesBlock)), Enum.Parse(typeof(LairSpeciesBlock), $"{(Species)species}"));
        return ((uint)(ResetLegendFlagOffset + (index * 0x38)), ResetLegendFlagOffset);
    }
    private async Task<uint> GetLairSpeciesNoteOffset(uint LairSpeciesNoteKey, CancellationToken token)
    {
        for(uint i = 0; i < 4; i++)
        {
            uint FirstOffset = LairSpeciesNoteKeyStart + i * SpeciesNoteLength;
            uint SecondOffset = LairSpeciesNoteKey2Start + i * SpeciesNoteLength;
            uint FirstKey = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAsync(FirstOffset, 4, token).ConfigureAwait(false), 0);
            uint SecondKey = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAsync(SecondOffset, 4, token).ConfigureAwait(false), 0);
            if (FirstKey == LairSpeciesNoteKey)
                return FirstOffset + 0x08;
            if(SecondKey == LairSpeciesNoteKey)
                return SecondOffset + 0x08;
        }
        return 0;
    }
    private async Task<List<uint>> GetAllLairSpeciesNoteOffset(CancellationToken token)
    {
        List<uint> NoteOffsets =
        [
            await GetLairSpeciesNoteOffset(KMaxLairSpeciesID1Noted, token).ConfigureAwait(false),
            await GetLairSpeciesNoteOffset(KMaxLairSpeciesID2Noted, token).ConfigureAwait(false),
            await GetLairSpeciesNoteOffset(KMaxLairSpeciesID3Noted, token).ConfigureAwait(false)
        ];
        if (NoteOffsets[0] == 0)
            return [];
        return NoteOffsets;
    }
    private async Task<List<uint>> GetAllLairSpeciesNoteOffsetFast(CancellationToken token)
    {
        var LairSpeciesNote1 = await GetLairSpeciesNoteOffset(KMaxLairSpeciesID1Noted, token).ConfigureAwait(false);
        if (LairSpeciesNote1 == 0)
            return [];
        return [LairSpeciesNote1, LairSpeciesNote1 + SpeciesNoteLength, LairSpeciesNote1 + SpeciesNoteLength * 2];
    }
    private async Task<bool> LairSeedInjector(CancellationToken token)
    {
        Log("Injecting specified Lair Seed...");
        if (!ulong.TryParse(Settings.SeedToInject, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong seedInj))
        {
            Log("Entered seed is invalid, stopping LairBot.");
            return false;
        }
        await Connection.WriteBytesAsync(BitConverter.GetBytes(seedInj), AdventureSeedOffset, token).ConfigureAwait(false);
        return true;
    }

    private async Task<Species> SetHuntedLairLegendary(CancellationToken token)
    {
        var LairSpeciesNoteOffsets = await GetAllLairSpeciesNoteOffsetFast(token).ConfigureAwait(false);
        if(LairSpeciesNoteOffsets.Count != 3)
        {
            Log("Failed to get Lair Species Note Offsets!");
            HackyNoteCheck = -1;
            return Species.None;
        }

        if ((HackyNoteCheck is -1 || LairBotUtil.DiscordQueueOverride))
        {
            bool isFirstGroup = LairSpeciesNoteOffsets[0] is LairSpeciesNote1 or LairSpeciesNote2;
            bool isSecondGroup = LairSpeciesNoteOffsets[0] is LairSpeciesNote1Pre or LairSpeciesNote2Pre;
            bool firstNoteIsFirst = isFirstGroup ? LairSpeciesNoteOffsets[0] == LairSpeciesNote1 : LairSpeciesNoteOffsets[0] == LairSpeciesNote1Pre;
            bool firstNoteIsSecond = isFirstGroup ? LairSpeciesNoteOffsets[0] == LairSpeciesNote2 : LairSpeciesNoteOffsets[0] == LairSpeciesNote2Pre;
            Log($"isFirstGroup is {isFirstGroup}!, isSecondGroup is {isSecondGroup}!");
            Log($"firstNoteIsFirst is {firstNoteIsFirst}!, firstNoteIsSecond is {firstNoteIsSecond}!");
            HackyNoteCheck = firstNoteIsFirst ? 1 : firstNoteIsSecond ? 2 : -1;
        }

        ushort FirstNote = 0;
        if (HackyNoteCheck is not -1)
            FirstNote = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LairSpeciesNoteOffsets[0], 2, token).ConfigureAwait(false), 0);
        else
            Log("First Lair Note is not found");
        if ((LairBotUtil.DiscordQueueOverride || ((ushort)Settings.LairSpeciesQueue[0] != FirstNote)) && HackyNoteCheck is not -1)
        {
            LairCatchCount = 0;
            LairEncounterCount = 0;
            AdventureCounts.AdventureCount = 0;
            AdventureCounts.WinCount = 0;
            for (int j = 0; j < 3; j++)            
                await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)LairSpecies.None), LairSpeciesNoteOffsets[j], token);                
            
            LairBotUtil.DiscordQueueOverride = false;
            for (int i = 0; i < Settings.LairSpeciesQueue.Count; i++)
            {
                var (offset, _) = await GetFlagOffset((int)Settings.LairSpeciesQueue[i], token).ConfigureAwait(false);
                if (offset != 0)
                {
                    var caughtFlag = await Connection.ReadBytesAsync(offset, 2, token).ConfigureAwait(false);
                    if (caughtFlag[0] != 0 && !Settings.ResetLegendaryCaughtFlag)
                    {
                        Log($"{(int)Settings.LairSpeciesQueue[i]} was caught prior and \"ResetLegendaryCaughtFlag\" is disabled. Skipping this note.");
                        continue;
                    }
                    if (Settings.ResetLegendaryCaughtFlag)
                        await ResetLegendaryFlag((int)Settings.LairSpeciesQueue[i], token).ConfigureAwait(false);
                }
                await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)Settings.LairSpeciesQueue[i]), LairSpeciesNoteOffsets[i], token);                
            }

            Log($"Lair Notes set to {string.Join(", ", Settings.LairSpeciesQueue)}!");
            return (Species)(ushort)Settings.LairSpeciesQueue[0];
        }
        else
        {
            if (HackyNoteCheck is -1)
            {
                Log("First Lair Note is not found, so LairSpecies is not able to inject!");
                return Species.None;
            }
            if (FirstNote != 0)
                return (Species)FirstNote;
            else if (Settings.LairSpeciesQueue.Count > 0 && Settings.LairSpeciesQueue[0] != LairSpecies.None)
                return (Species)(ushort)Settings.LairSpeciesQueue[0];
            return Species.Dialga;
        }

    }
    private async Task<ushort> SetHuntedPokemon(CancellationToken token)
    {
        var notecheck = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LairSpeciesNote1, 2, token).ConfigureAwait(false), 0);
        if ((HackyNoteCheck is -1 || LairBotUtil.DiscordQueueOverride) && notecheck != 0x20)
        {
            Log("Normal LairSpeciesNote Address detected!");
            for (int i = 0; i < 4; i++) // First note shifts due to yet unknown reasons, just clear possible slots, check which note to use on startup and after catching a legendary.
                await Connection.WriteBytesAsync(new byte[] { 0 }, i is 0 ? LairSpeciesNote1 : i is 1 ? LairSpeciesNote2 : i is 2 ? LairSpeciesNote3 : LairSpeciesNote4, token);

            var control = BitConverter.GetBytes((ushort)LairSpecies.Moltres);
            await Connection.WriteBytesAsync(control, LairSpeciesNote3, token).ConfigureAwait(false);
            await Click(A, 0_250, token).ConfigureAwait(false);

            var note1 = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LairSpeciesNote1, 2, token).ConfigureAwait(false), 0);
            var note2 = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LairSpeciesNote2, 2, token).ConfigureAwait(false), 0);
            for (int i = 0; i < 3; i++)
                await Click(B, 0_250, token).ConfigureAwait(false);

            bool firstNoteIsFirst = note1 is (ushort)LairSpecies.Moltres;
            bool firstNoteIsSecond = note2 is (ushort)LairSpecies.Moltres;
            string notestring = firstNoteIsFirst ? "note1" : firstNoteIsSecond ? "note2" : "None";
            Log($"Lair boss note1 is {note1}!, firstNoteIsFirst is {firstNoteIsFirst}!{Environment.NewLine}Lair boss note2 is {note2}!, firstNoteIsSecond is {firstNoteIsSecond}!{Environment.NewLine}So first note is {notestring}!");
            if (notestring != "None")
                await Connection.WriteBytesAsync(new byte[] { 0 }, firstNoteIsFirst ? LairSpeciesNote1 : LairSpeciesNote2, token).ConfigureAwait(false);
            HackyNoteCheck = firstNoteIsFirst ? 1 : firstNoteIsSecond ? 2 : -1;
        }
        else if (HackyNoteCheck is -1 || LairBotUtil.DiscordQueueOverride)
        {
            Log("Other LairSpeciesNote Address detected!");
            for (int i = 0; i < 4; i++) // First note shifts due to yet unknown reasons, just clear possible slots, check which note to use on startup and after catching a legendary.
                await Connection.WriteBytesAsync(new byte[] { 0 }, i is 0 ? LairSpeciesNote1Pre : i is 1 ? LairSpeciesNote2Pre : i is 2 ? LairSpeciesNote3Pre : LairSpeciesNote4Pre, token);

            var control = BitConverter.GetBytes((ushort)LairSpecies.Moltres);
            await Connection.WriteBytesAsync(control, LairSpeciesNote3Pre, token).ConfigureAwait(false);
            await Click(A, 0_250, token).ConfigureAwait(false);

            var note1 = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LairSpeciesNote1Pre, 2, token).ConfigureAwait(false), 0);
            var note2 = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LairSpeciesNote2Pre, 2, token).ConfigureAwait(false), 0);
            for (int i = 0; i < 3; i++)
                await Click(B, 0_250, token).ConfigureAwait(false);

            bool firstNoteIsFirst = note1 is (ushort)LairSpecies.Moltres;
            bool firstNoteIsSecond = note2 is (ushort)LairSpecies.Moltres;
            string notestring = firstNoteIsFirst ? "note1" : firstNoteIsSecond ? "note2" : "None";
            Log($"Lair boss note1 is {note1}!, firstNoteIsFirst is {firstNoteIsFirst}!{Environment.NewLine}Lair boss note2 is {note2}!, firstNoteIsSecond is {firstNoteIsSecond}!{Environment.NewLine}So first note is {notestring}!");
            if (notestring != "None")
                await Connection.WriteBytesAsync(new byte[] { 0 }, firstNoteIsFirst ? LairSpeciesNote1Pre : LairSpeciesNote2Pre, token).ConfigureAwait(false);
            HackyNoteCheck = firstNoteIsFirst ? 1 : firstNoteIsSecond ? 2 : -1;
        }

        /*if (LegendFound is not 0)
        {
            if (Settings.ResetLegendaryCaughtFlag)
                await ResetLegendaryFlag(LegendFound, token).ConfigureAwait(false);

            LegendFound = 0;
            if (Settings.LairSpeciesQueue[0] > LairSpecies.None)
            {
                for (int i = 0; i < Settings.LairSpeciesQueue.Count - 1; i++)
                {
                    Settings.LairSpeciesQueue[i] = Settings.LairSpeciesQueue[i + 1];
                }
                Settings.LairSpeciesQueue[Settings.LairSpeciesQueue.Count - 1] = LairSpecies.None;
            }
        }*/
        ushort note = 0;
        if ((HackyNoteCheck is not -1) && notecheck != 0x20)
            note = BitConverter.ToUInt16(await Connection.ReadBytesAsync(HackyNoteCheck is 1 ? LairSpeciesNote1 : LairSpeciesNote2, 2, token).ConfigureAwait(false), 0);
        if ((HackyNoteCheck is not -1) && notecheck == 0x20)
            note = BitConverter.ToUInt16(await Connection.ReadBytesAsync(HackyNoteCheck is 1 ? LairSpeciesNote1Pre : LairSpeciesNote2Pre, 2, token).ConfigureAwait(false), 0);
        if (HackyNoteCheck is -1)
            Log("First Lair Note is not found");
        if ((LairBotUtil.DiscordQueueOverride || ((ushort)Settings.LairSpeciesQueue[0] != note)) && HackyNoteCheck is not -1)
        {
            LairCatchCount = 0;
            LairEncounterCount = 0;
            AdventureCounts.AdventureCount = 0;
            AdventureCounts.WinCount = 0;
            for (int j = 0; j < 3; j++)
            {
                if (notecheck != 0x20)
                {
                    if (HackyNoteCheck is 1)
                        await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)LairSpecies.None), j is 0 ? LairSpeciesNote1 : j is 1 ? LairSpeciesNote2 : LairSpeciesNote3, token);
                    else await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)LairSpecies.None), j is 0 ? LairSpeciesNote2 : j is 1 ? LairSpeciesNote3 : LairSpeciesNote4, token);

                }
                else
                {
                    if (HackyNoteCheck is 1)
                        await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)LairSpecies.None), j is 0 ? LairSpeciesNote1Pre : j is 1 ? LairSpeciesNote2Pre : LairSpeciesNote3Pre, token);
                    else await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)LairSpecies.None), j is 0 ? LairSpeciesNote2Pre : j is 1 ? LairSpeciesNote3Pre : LairSpeciesNote4Pre, token);

                }
            }
            /*Settings.LairSpeciesQueue = Settings.LairSpeciesQueue.Where(x => x != LairSpecies.None).ToList();
            if (Settings.LairSpeciesQueue.Count > 3)
                Settings.LairSpeciesQueue.RemoveRange(3, Settings.LairSpeciesQueue.Count - 3);*/
            LairBotUtil.DiscordQueueOverride = false;
            for (int i = 0; i < Settings.LairSpeciesQueue.Count; i++)
            {
                var (offset, _) = await GetFlagOffset((int)Settings.LairSpeciesQueue[i], token).ConfigureAwait(false);
                if (offset != 0)
                {
                    var caughtFlag = await Connection.ReadBytesAsync(offset, 2, token).ConfigureAwait(false);
                    if (caughtFlag[0] != 0 && !Settings.ResetLegendaryCaughtFlag)
                    {
                        Log($"{(int)Settings.LairSpeciesQueue[i]} was caught prior and \"ResetLegendaryCaughtFlag\" is disabled. Skipping this note.");
                        continue;
                    }
                    if (Settings.ResetLegendaryCaughtFlag)
                        await ResetLegendaryFlag((int)Settings.LairSpeciesQueue[i], token).ConfigureAwait(false);
                }

                if (notecheck != 0x20)
                {
                    if (HackyNoteCheck is 1)
                        await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)Settings.LairSpeciesQueue[i]), i is 0 ? LairSpeciesNote1 : i is 1 ? LairSpeciesNote2 : LairSpeciesNote3, token);
                    else await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)Settings.LairSpeciesQueue[i]), i is 0 ? LairSpeciesNote2 : i is 1 ? LairSpeciesNote3 : LairSpeciesNote4, token);

                }
                else
                {
                    if (HackyNoteCheck is 1)
                        await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)Settings.LairSpeciesQueue[i]), i is 0 ? LairSpeciesNote1Pre : i is 1 ? LairSpeciesNote2Pre : LairSpeciesNote3Pre, token);
                    else await Connection.WriteBytesAsync(BitConverter.GetBytes((ushort)Settings.LairSpeciesQueue[i]), i is 0 ? LairSpeciesNote2Pre : i is 1 ? LairSpeciesNote3Pre : LairSpeciesNote4Pre, token);

                }
            }

            Log($"Lair Notes set to {string.Join(", ", Settings.LairSpeciesQueue)}!");
            return (ushort)Settings.LairSpeciesQueue[0];
        }
        else
        {
            if (HackyNoteCheck is -1)
                Log("First Lair Note is not found, so LairSpecies is not able to inject!");
            if (note != 0)
                return note;
            else if (Settings.LairSpeciesQueue.Count > 0 && Settings.LairSpeciesQueue[0] != LairSpecies.None)
                return (ushort)Settings.LairSpeciesQueue[0];
            return (ushort)LairSpecies.Dialga;
        }
    }
    private async Task<uint> GetLairLegendaryRand(CancellationToken token)
    {
        var LairSpeciesBlockValues = Enum.GetNames(typeof(LairSpeciesBlock)).Select(x => Enum.Parse(typeof(Species), x)).ToList();
        Log("Beginning get caught flag count.");
        uint CaughtCount = 0;
        var UBUnlocked = await IsUBUnlocked(token).ConfigureAwait(false);
        LairRNG.GenerateLairLegendayList(UBUnlocked, Version);
        //Log($"DmaxLegendaries List{Environment.NewLine}{string.Join($"{Environment.NewLine}", LairRNG.DmaxLegendaries.Select(x => (Species)x.Species))}");

        for (uint i = 0; i < LairSpeciesBlockValues.Count; i++)
        {
            uint offset = ResetLegendFlagOffset + (i * 0x38);
            var val = BitConverter.ToUInt16(await Connection.ReadBytesAsync(offset, 2, token).ConfigureAwait(false), 0);
            /*if (val == 0)
                Log($"{LairSpeciesBlockValues[(int)i]} is not caught.");
            else
                Log($"{LairSpeciesBlockValues[(int)i]} is caught.");*/
            if (val == 1 && LairRNG.DmaxLegendaries.Any(x => x.Species == (ushort)LairSpeciesBlockValues[(int)i]))
            {
                Log($"{LairSpeciesBlockValues[(int)i]} is caught.");
                LairRNG.DmaxLegendaries.RemoveAll(x => x.Species == (ushort)LairSpeciesBlockValues[(int)i]);
                CaughtCount++;
            }
        }
        return LairLegendariesCount + (UBUnlocked ? LairUBCount : 0) - CaughtCount - 1;
    }
    private async Task<bool> IsUBUnlocked(CancellationToken token)
    {
        var val = await Connection.ReadBytesAsync(UnlocksUBInMaxLairOffset, 1, token).ConfigureAwait(false);
        return val[0] == 0x01;
    }
    private async Task MoveAndRentalClicks(int clicks, CancellationToken token)
    {
        if (clicks > 0)
        {
            for (int i = 0; i < clicks; i++)
                await Click(DDOWN, 0_300, token);
        }
        else
        {
            for (int i = 0; i < Math.Abs(clicks); i++)
                await Click(DUP, 0_300, token).ConfigureAwait(false);
        }

        for (int i = 0; i < 6; i++)
            await Click(A, 0_500, token).ConfigureAwait(false);
    }
    private void CheckLairSpeciesList()
    {
        Settings.LairSpeciesQueue = Settings.LairSpeciesQueue.Where(x => x != LairSpecies.None).ToList();
        if (Settings.LairSpeciesQueue.Count > 3)
            Settings.LairSpeciesQueue.RemoveRange(3, Settings.LairSpeciesQueue.Count - 3);
    }
    private async Task ParseLairSpecies(CancellationToken token)
    {
        CheckLairSpeciesList();
        if (LegendFound is not 0)
        {
            if (Settings.ResetLegendaryCaughtFlag)
                await ResetLegendaryFlag(LegendFound, token).ConfigureAwait(false);

            LegendFound = 0;
            if (Settings.LairSpeciesQueue[0] > LairSpecies.None)
            {
                for (int i = 0; i < Settings.LairSpeciesQueue.Count - 1; i++)
                {
                    Settings.LairSpeciesQueue[i] = Settings.LairSpeciesQueue[i + 1];
                }
                Settings.LairSpeciesQueue[Settings.LairSpeciesQueue.Count - 1] = LairSpecies.None;
            }
            CheckLairSpeciesList();
        }
    }
    private async Task GameRestart(CancellationToken token)
    {
        await CloseGame(Hub.Config, token).ConfigureAwait(false);
        await StartGameLair(Hub.Config, token).ConfigureAwait(false);
        await InitializeSessionOffsets(token).ConfigureAwait(false);
    }

    private async Task LairEntry(CancellationToken token)
    {
        var ofsVal = BitConverter.ToUInt16(await Connection.ReadBytesAsync(CurrentScreenLairOffset, 2, token).ConfigureAwait(false), 0);
        while (await LairStatusCheck(ofsVal, CurrentScreenLairOffset, token).ConfigureAwait(false))
            await Click(A, 0_300, token).ConfigureAwait(false);

        await Task.Delay(2_000, token).ConfigureAwait(false);
        await Click(DDOWN, 0_250, token).ConfigureAwait(false);
        await Click(A, 2_000, token).ConfigureAwait(false);
    }
    private async Task<bool> SettingsCheck(CancellationToken token)
    {
        await ParseLairSpecies(token).ConfigureAwait(false);
        if (Settings.LairSpeciesQueue.Count <= 0 || Settings.LairSpeciesQueue.All(x => x <= LairSpecies.None))
        {
            Log("Lair Species List is None!");
            return false;
        }
        foreach (var setting in Hub.Config.StopConditions.SearchConditions)
        {
            if (!setting.IsEnabled)
                continue;

            if (setting.ShinyTarget == TargetShinyType.SquareOnly)
                setting.ShinyTarget = TargetShinyType.AnyShiny;
            if (setting.MarkOnly)
                setting.MarkOnly = false;
        }

        if (BallPouch.Length == 1 || PreviousBall != Settings.LairBall)
        {
            Log("Checking Poké Ball Pouch...");
            PreviousBall = Settings.LairBall;
            Log($"Previous LairBall is set to {PreviousBall} Ball!");
            CatchCount = await GetPokeBallCount(token).ConfigureAwait(false);
            if (CatchCount < 5)
            {
                Log($"Insufficient {Settings.LairBall} Ball count.");
                await SetBallCount(token).ConfigureAwait(false);
                CatchCount = await GetPokeBallCount(token).ConfigureAwait(false);
            }
        }
        else if (CatchCount < 5)
        {
            Log("Restoring original Ball Pouch...");
            await SetBallCount(token).ConfigureAwait(false);
            CatchCount = await GetPokeBallCount(token).ConfigureAwait(false);
        }

        if (OtherItemsPouch.Length == 1 && (Settings.UseStopConditionsPathReset || Settings.KeepPath))
        {
            Log("Checking Dynite Ore count...");
            var dyniteCount = await GetDyniteCount(token).ConfigureAwait(false);
            if (dyniteCount < 10)
            {
                Log($"{(dyniteCount == 0 ? "No" : $"Only {dyniteCount}")} Dynite Ore found. To be on the safe side, obtain more!.");
                await SetDyniteCount(token).ConfigureAwait(false);
            }
        }
        return true;
    }

    private async Task OffsetLogLoop(CancellationToken token)
    {
        MainNsoBase = await SwitchConnection.GetMainNsoBaseAsync(token).ConfigureAwait(false);
        LairMiscScreenCalc = MainNsoBase + LairMiscScreenOffset;

        if (Settings.EnableOHKO) // Enable dirty OHKO.
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(AlteredDamage), MainNsoBase + DamageOutputOffset, token).ConfigureAwait(false);

        string instructions =
            "\n\n1. LairLobbyValue (CurrentScreen): screen where you select \"Don't invite others\"." +
            "\n2. LairAdventurePathValue (CurrentScreen): screen where you choose a path." +
            "\n3. LairDmaxValue (MiscScreen): during the first battle, when your wristband glows." +
            "\n4. LairBattleMenuValue (MiscScreen): main in-battle screen." +
            "\n5. LairMovesMenuValue (MiscScreen): move selection screen." +
            "\n6. LairCatchScreenValue (MiscScreen): screen where it says \"Catch\" and \"Don't catch\"." +
            "\n7. LairRewardsScreenValue (CurrentScreen): screen at the end of an adventure where you can select which caught Pokémon to bring home.\n\n";

        EchoUtil.Echo($"Starting main OffsetLog loop. Please progress through an adventure while paying close attention to value changes.{instructions}");
        while (!token.IsCancellationRequested)
        {
            var valCur = BitConverter.ToUInt16(await Connection.ReadBytesAsync(CurrentScreenLairOffset, 2, token).ConfigureAwait(false), 0);
            var valMisc = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(LairMiscScreenCalc, 2, token).ConfigureAwait(false), 0);
            var hexCur = string.Format("0x{0:X8}", valCur);
            var hexMisc = string.Format("0x{0:X8}", valMisc);
            Log($"\nCurrentScreen offset value: {hexCur}\nMiscScreen offset value: {hexMisc}");
            await Task.Delay(2_000, token).ConfigureAwait(false);
        }
    }

    private async Task<int> GetPPCount(int move, CancellationToken token) => BitConverter.ToInt32(await Connection.ReadBytesAsync((uint)(LairMove1Offset + (move * 0xC)), 4, token).ConfigureAwait(false), 0);

    private LairOffsetValues ValueParse()
    {
        ushort path;
        ushort.TryParse(Settings.LairScreenValues.LairLobbyValue.Replace("0x", ""), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort lobby);
        if (Config.Connection.Protocol == SwitchProtocol.USB)
            ushort.TryParse(Settings.LairScreenValues.LairAdventurePathValueUSB.Replace("0x", ""), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out path);
        else
            ushort.TryParse(Settings.LairScreenValues.LairAdventurePathValueWiFi.Replace("0x", ""), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out path);
        ushort.TryParse(Settings.LairScreenValues.LairDmaxValue.Replace("0x", ""), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort dmax);
        ushort.TryParse(Settings.LairScreenValues.LairBattleMenuValue.Replace("0x", ""), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort battle);
        ushort.TryParse(Settings.LairScreenValues.LairMovesMenuValue.Replace("0x", ""), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort moves);
        ushort.TryParse(Settings.LairScreenValues.LairCatchScreenValue.Replace("0x", ""), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort catchScreen);
        ushort.TryParse(Settings.LairScreenValues.LairOnCatchScreenValue.Replace("0x", ""), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort oncatchScreen);
        ushort.TryParse(Settings.LairScreenValues.LairChoosePokemonScreenValue.Replace("0x", ""), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort chooseScreen);
        ushort.TryParse(Settings.LairScreenValues.LairRewardsScreenValue.Replace("0x", ""), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out ushort rewards);
        return new()
        {
            LairLobby = lobby,
            LairAdventurePath = path,
            LairDmax = dmax,
            LairBattleMenu = battle,
            LairMovesMenu = moves,
            LairCatchScreen = catchScreen,
            LairOnCatchScreen = oncatchScreen,
            LairChoosePokemonScreen = chooseScreen,
            LairRewardsScreen = rewards,
        };
    }
}
