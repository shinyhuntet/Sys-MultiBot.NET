using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;
using System.IO;
using System.Linq;

namespace SysBot.Pokemon;

public class EncounterBotEggSWSH : EncounterBotSWSH, IEncounterBot
{
    private readonly IDumper DumpSetting;
    private new readonly EggSettings Settings;
    private SwordShieldDaycare Location;
    private PK8? ParentOne;
    private PK8? ParentTwo;
    
    public EncounterBotEggSWSH(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg, hub)
    {
        Settings = Hub.Config.EncounterSWSH.Egg;
        DumpSetting = Hub.Config.Folder;
    }

    
    private const int InjectBox = 0;
    private const int InjectSlot = 0;

    private static readonly PK8 Blank = new();

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        if (Settings.Mode == EggMode.Hatch)
        {
            await MassEggHatch(token);
            return;
        }
        Location = Settings.Location;
        if (Settings.UnilimtedModeFromStart && !await IsUnlimited(token).ConfigureAwait(false))
            return;
        Log("reading egg parents.");
        (ParentOne, ParentTwo) = await GetDayCare(token).ConfigureAwait(false);
        if (ParentOne == null || ParentTwo == null)
        {
            Log("No Valid parent data!");
            return;
        }
        var print1 = Hub.Config.StopConditions.GetPrintName(ParentOne);
        Log($"Parent1:{Environment.NewLine}{print1}{Environment.NewLine}");
        var print2 = Hub.Config.StopConditions.GetPrintName(ParentTwo);
        Log($"Parent2:{Environment.NewLine}{print2}{Environment.NewLine}");
        await RefreshParents(ParentOne, ParentTwo, token).ConfigureAwait(false);

        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.EggFetch)
        {
            if (!await InnerLoop(sav, token).ConfigureAwait(false))
                break;
        }
    }

    public override async Task HardStop()
    {
        // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
        await SetStick(LEFT, 0, 0, 0, CancellationToken.None).ConfigureAwait(false); // reset
        await CleanExit(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Return true if we need to stop looping.
    /// </summary>
    private async Task<bool> InnerLoop(SAV8SWSH sav, CancellationToken token)
    {
        // Walk a step left, then right => check if egg was generated on this attempt.
        // Repeat until an egg is generated.
        bool found = false;
        var attempts = await StepUntilEgg(token).ConfigureAwait(false);
        if (attempts < 0) // aborted
            return true;

        Log($"Egg available after {attempts} attempts! Reading Egg Pokemon from seed..");
        var seed = await ReadSeed(Location, token).ConfigureAwait(false);
        if (seed == 0)
        {
            Log("no seed found");
            return true;
        }
        var pk = EggRNG.GenerateEgg(seed, ParentOne!, ParentTwo!, true, sav);
        if (pk.Species == 0)
        {
            Log("Invalid data detected in destination slot. Restarting loop.");
            return true;
        }

        EggDetail.ResetAssets();
        var print = Hub.Config.StopConditions.GetPrintName(pk);
        Log($"Encounter: {encounterCount}{Environment.NewLine}{print}{Environment.NewLine}");
        await EggDetail.SetPokeDetail(pk, EggDetailForm.EggDetail.Egg, token, encounterCount).ConfigureAwait(false);
        Settings.AddCompletedEggs();
        
        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            DumpPokemon(DumpSetting.DumpFolder, "egg", pk);
        found = StopConditionSettings.EncounterFound(pk, Hub.Config.StopConditions);

        if (found)
        {
            for (int i = 0; i < 6; i++)
                await Click(A, 0_400, token).ConfigureAwait(false);
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(B, 0_400, token).ConfigureAwait(false);                
        }
        else
        {
            await Click(A, 1_000, token).ConfigureAwait(false);
            /*for (int i = 0; i < 6; i++)
                await Click(B, 0_400, token).ConfigureAwait(false);*/ 
            while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(B, 0_400, token).ConfigureAwait(false);
        }

        encounterCount++;

        if (!found)
            return true;

        // no need to take a video clip of us receiving an egg.
        var mode = Settings.ContinueAfterMatch;
        var msg = $"Result found!\n{print}\n" + mode switch
        {
            ContinueAfterMatch.Continue => "Continuing...",
            ContinueAfterMatch.PauseWaitAcknowledge => "Waiting for instructions to continue.",
            ContinueAfterMatch.StopExit => "Stopping routine execution; restart the bot to search again.",
            _ => throw new ArgumentOutOfRangeException(),
        };

        if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
            msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
        EchoUtil.Echo(msg);
        Log(msg);

        if (mode == ContinueAfterMatch.StopExit)
            return false;
        if (mode == ContinueAfterMatch.Continue)
        {
            if (await IsUnlimited(token).ConfigureAwait(false))
            {
                (ParentOne, ParentTwo) = await GetDayCare(token).ConfigureAwait(false);
                var success = ParentOne != null && ParentTwo != null;
                if (success && Settings.UnlimitedMode)
                {
                    var print1 = Hub.Config.StopConditions.GetPrintName(ParentOne!);
                    Log($"Updated Parent1:{Environment.NewLine}{print1}{Environment.NewLine}");
                    var print2 = Hub.Config.StopConditions.GetPrintName(ParentTwo!);
                    Log($"Updated Parent2:{Environment.NewLine}{print2}{Environment.NewLine}");
                    await RefreshParents(ParentOne!, ParentTwo!, token).ConfigureAwait(false);
                }
                return success;
            }
            else
            {
                return false;
            }
        }

        IsWaiting = true;
        while (IsWaiting)
            await Task.Delay(1_000, token).ConfigureAwait(false);
        return false;
    }

    private async Task SetupBoxState(CancellationToken token)
    {
        await SetCurrentBox(0, token).ConfigureAwait(false);

        var existing = await ReadBoxPokemon(InjectBox, InjectSlot, token).ConfigureAwait(false);
        if (existing.Species != 0 && existing.ChecksumValid)
        {
            Log("Destination slot is occupied! Dumping the Pokémon found there...");
            DumpPokemon(DumpSetting.DumpFolder, "saved", existing);
        }

        Log("Clearing destination slot to start the bot.");
        await SetBoxPokemon(Blank, InjectBox, InjectSlot, token).ConfigureAwait(false);
    }

    private bool IsWaiting;
    
    private async Task RefreshParents(PK8 ParentOne, PK8 ParentTwo, CancellationToken token)
    {
        EggDetail.ResetParentsAssets();
        await EggDetail.SetPokeDetail(ParentOne, EggDetailForm.EggDetail.ParentOne, token).ConfigureAwait(false);
        await EggDetail.SetPokeDetail(ParentTwo, EggDetailForm.EggDetail.ParentTwo, token).ConfigureAwait(false);
    }

    private async Task<int> StepUntilEgg(CancellationToken token)
    {
        Log("Walking around until an egg is ready...");
        int attempts = 0;
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.EggFetch)
        {
            await SetEggStepCounter(Location, token).ConfigureAwait(false);

            // Walk Diagonally Left
            await SetStick(LEFT, -19000, 19000, 0_500, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset

            // Walk Diagonally Right, slightly longer to ensure we stay at the Daycare lady.
            await SetStick(LEFT, 19000, 19000, 0_550, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset

            bool eggReady = await IsEggReady(Location, token).ConfigureAwait(false);
            if (eggReady)
                return attempts;

            attempts++;
            if (attempts % 10 == 0 && attempts != 0)
            {
                Log($"Tried {attempts} times, still no egg. Attempting full recovery..");
                await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                attempts = 0;
            }

        }

        return -1; // aborted
    }
    private async Task<bool> IsUnlimited(CancellationToken token)
    {
        if (Settings.UnlimitedMode)
        {
            if (!Directory.Exists(Settings.UnlimitedParentsFolder))
            {
                Log($"Directory for unlimited doesn't exist: [{Settings.UnlimitedParentsFolder}]");
                return false;
            }

            var parents = Directory.GetFiles(Settings.UnlimitedParentsFolder, "*pk*");
            if (parents.Length == 0)
            {
                Log($"No valid parents found in [{Settings.UnlimitedParentsFolder}]");
                return false;
            }

            if (!await SetNextParent(parents, token))
                return false;
        }

        return true;
    }

    private async Task<bool> SetNextParent(IEnumerable<string> parents, CancellationToken token)
    {
        var parent = parents.FirstOrDefault();
        if (parent == null)
            return false;

        var fileInfo = new FileInfo(parent);
        var bytes = await File.ReadAllBytesAsync(parent, token);

        if (!FileUtil.TryGetPKM(bytes, out var pk, fileInfo.Extension))
        {
            Log($"Parent file [{parent}] isn't valid!");
            return false;
        }

        if (EntityConverter.ConvertToType(pk, typeof(PK8), out var result) is not PK8 pk8)
        {
            Log($"Parent {pk.FileName} isn't valid: {result}");
            return false;
        }

        if(!pk8.Valid || pk8.IsEgg)
        {
            Log($"Parent {pk.FileName} File data isn't valid!"); 
            return false;
        }

        var ParentDatatable = PersonalTable.SWSH.GetFormEntry(pk8.Species, pk8.Form);
        if(!ParentDatatable.IsPresentInGame || pk8.Species > PersonalTable.SWSH.MaxSpeciesID || pk8.Species <= 0)
        {
            Log($"Parent is not Valid: {ParentDatatable.Species}");
            return false;
        }

        if(ParentDatatable.HatchSpecies <= 0 || (ParentDatatable.IsEggGroup(13)))
        {
            Log($"{ParentDatatable.Species} can't be hatched!{Environment.NewLine}Hacth Species: {ParentDatatable.HatchSpecies}{Environment.NewLine}Egg Gruop1: {ParentDatatable.EggGroup1}{Environment.NewLine}Egg Group2: {ParentDatatable.EggGroup2}");
            return false;
        }

        var (slot1, slot2) = await GetDayCare(token);
        if (slot1?.Species != (int)Species.Ditto && slot2?.Species != (int)Species.Ditto)
        {
            Log($"There is not {Species.Ditto} in Day Care");
            return false;
        }

        var dittoFirstSlot = slot1?.Species == (int)Species.Ditto;
        await SetDayCare(pk8, !dittoFirstSlot, token);

        (slot1, slot2) = await GetDayCare(token);

        if(slot1 == null || slot2 == null)
        {
            Log("Failed to set parent in Day Care!");
            return false;
        }

        Log($"Set parent: {pk8.FileName}, slot 1 is {slot1.Species}, valid: {slot1.Valid} and slot 2 is {slot2.Species}, valid: {slot2.Valid}");
        var Parent1Data = PersonalTable.SWSH.GetFormEntry(slot1.Species, slot1.Form);
        var Parent2Data = PersonalTable.SWSH.GetFormEntry(slot2.Species, slot2.Form);
        Log($"Parent Details{Environment.NewLine}Parent1{Environment.NewLine}Egg Group1: {Parent1Data.EggGroup1}, Egg Group2: {Parent1Data.EggGroup2}{Environment.NewLine}Parent2{Environment.NewLine}Egg Gruop1: {Parent2Data.EggGroup1}, Egg Group2: {Parent2Data.EggGroup2}");

        var info = new FileInfo(parent);
        File.Move(info.FullName, Path.Combine(DumpSetting.DumpFolder, "saved", info.Name));

        return true;
    }

    private async Task<(PK8? Slot1, PK8? Slot2)> GetDayCare(CancellationToken token)
    {
        PK8? slot1 = null;
        PK8? slot2 = null;

        var ParentsOffs = GetDaycareEggParentsOffset(Location);
        var dayCareBytes = await SwitchConnection.ReadBytesAsync(ParentsOffs, DayCareSize, token);

        if (dayCareBytes[0] == 1)
        {
            var slot1Bytes = dayCareBytes.Skip(1).Take(0x148).ToArray();
            slot1 = new PK8(slot1Bytes);
        }

        if (dayCareBytes[0x149] == 1)
        {
            var slot1Bytes = dayCareBytes.Skip(0x149 + 1).Take(0x148).ToArray();
            slot2 = new PK8(slot1Bytes);
        }

        return (slot1, slot2);
    }

    private async Task SetDayCare(PKM pk8, bool firstSlot, CancellationToken token)
    {
        var ParentsOffs = GetDaycareEggParentsOffset(Location);
        var dayCareBytes = await SwitchConnection.ReadBytesAsync(ParentsOffs, DayCareSize, token);

        var newBytes = new List<byte>();
        if (firstSlot)
        {
            newBytes.Add(1);
            newBytes.AddRange(pk8.EncryptedBoxData);

            newBytes.AddRange(dayCareBytes.Skip(0x149).Take(0x148));
        }
        else
        {
            newBytes.AddRange(dayCareBytes.Take(0x149));

            newBytes.Add(1);
            newBytes.AddRange(pk8.EncryptedBoxData);
        }

        await SwitchConnection.WriteBytesAsync([.. newBytes], ParentsOffs, token);
    }
    private async Task MassEggHatch(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            while(await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                await SetStick(LEFT, -30000, 0, 2_400, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset

                if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    break;

                await SetStick(LEFT, 30000, 0, 2_400, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset

                if (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                    break;
            }
            while(!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
                await Click(B, 0_400, token).ConfigureAwait(false);
        }
    }
    public async Task<List<PK8>> ReadParent(SwordShieldDaycare Location, CancellationToken token)
    {
        List<PK8> parenntlist = new();
        var ParentOffs = GetDaycareEggParentsOffset(Location);
        var ofs1 = ParentOffs + 0x1;
        var ofs2 = ofs1 + 0x149;
        var parent1 = new PK8(await SwitchConnection.ReadBytesAsync(ofs1, 0x148, token).ConfigureAwait(false));
        var parent2 = new PK8(await SwitchConnection.ReadBytesAsync(ofs2, 0x148, token).ConfigureAwait(false));

        if (parent1 != null)
            parenntlist.Add(parent1);
        if (parent2 != null)
            parenntlist.Add(parent2);

        return parenntlist;
    }

    public async Task<ulong> ReadSeed(SwordShieldDaycare Location, CancellationToken token)
    {
        uint ofs = GetDaycareEggSeedOffset(Location);
        var seed = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAsync(ofs, 8, token).ConfigureAwait(false), 0);
        return seed;
    }
    public async Task<bool> IsEggReady(SwordShieldDaycare daycare, CancellationToken token)
    {
        var ofs = GetDaycareEggIsReadyOffset(daycare);
        // Read a single byte of the Daycare metadata to check the IsEggReady flag.
        var data = await Connection.ReadBytesAsync(ofs, 1, token).ConfigureAwait(false);
        return data[0] == 1;
    }

    public async Task SetEggStepCounter(SwordShieldDaycare daycare, CancellationToken token)
    {
        // Set the step counter in the Daycare metadata to 180. This is the threshold that triggers the "Should I create a new egg" subroutine.
        // When the game executes the subroutine, it will generate a new seed and set the IsEggReady flag.
        // Just setting the IsEggReady flag won't refresh the seed; we want a different egg every time.
        var data = new byte[] { 0xB4, 0, 0, 0 }; // 180
        var ofs = GetDaycareStepCounterOffset(daycare);
        await Connection.WriteBytesAsync(data, ofs, token).ConfigureAwait(false);
    }    
}

