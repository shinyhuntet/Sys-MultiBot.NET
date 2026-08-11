using Newtonsoft.Json;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.BasePokeDataOffsetsBS;
using SysBot.Pokemon.Properties;

namespace SysBot.Pokemon;

public abstract class PokeRoutineExecutor8BS : PokeRoutineExecutor<PB8>
{    
    protected IPokeDataOffsetsBS Offsets { get; private set; } = new PokeDataOffsetsBS_BD();
    protected PokeRoutineExecutor8BS(PokeBotState cfg) : base(cfg)
    {
    }

    public override async Task<PB8> ReadPokemon(ulong offset, CancellationToken token) => await ReadPokemon(offset, BoxFormatSlotSize, token).ConfigureAwait(false);

    public override async Task<PB8> ReadPokemon(ulong offset, int size, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
        return new PB8(data);
    }

    public override async Task<PB8> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return new PB8();
        return await ReadPokemon(offset, token).ConfigureAwait(false);
    }

    public async Task SetPokemonPointer(IEnumerable<long> jumps, PB8 pkm, CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return;

        await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedPartyData, offset, token).ConfigureAwait(false);
    }

    public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
    {
        var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
        return !result.SequenceEqual(original);
    }

    public override async Task<PB8> ReadBoxPokemon(int box, int slot, CancellationToken token)
    {
        // Shouldn't be reading anything but box1slot1 here. Slots are not consecutive.
        var jumps = Offsets.BoxStartPokemonPointer.ToArray();
        return await ReadPokemonPointer(jumps, BoxFormatSlotSize, token).ConfigureAwait(false);
    }

    public async Task SetBoxPokemon(PB8 pkm, CancellationToken token)
    {
        // Shouldn't be reading anything but box1slot1 here. Slots are not consecutive.
        var jumps = Offsets.BoxStartPokemonPointer.ToArray();
        await SetPokemonPointer(jumps, pkm, token).ConfigureAwait(false);
    }

    public async Task SetBoxPokemonAbsolute(ulong offset, PB8 pkm, CancellationToken token, ITrainerInfo? sav = null)
    {
        if (sav != null)
        {
            // Update PKM to the current save's handler data
            pkm.UpdateHandler(sav);
            pkm.RefreshChecksum();
        }

        pkm.ResetPartyStats();
        await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedPartyData, offset, token).ConfigureAwait(false);
    }

    public async Task<(PB8, byte[]?)> ReadRawBoxPokemon(int box, int slot, CancellationToken token)
    {
        var jumps = Offsets.BoxStartPokemonPointer.ToArray();
        var (valid, b1s1) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return (new PB8(), null);

        var copiedData = new byte[BoxFormatSlotSize];
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(b1s1, BoxFormatSlotSize, token).ConfigureAwait(false);

        data.CopyTo(copiedData, 0);

        if (!data.SequenceEqual(copiedData))
            throw new InvalidOperationException("Raw data is not copied correctly");

        return (new PB8(data), copiedData);
    }

    public async Task<SAV8BS> IdentifyTrainer(CancellationToken token)
    {
        // Check if botbase is on the correct version or later.
        await VerifyBotbaseVersion(token).ConfigureAwait(false);

        // Pull title so we know which set of offsets to use.
        string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
        Offsets = title switch
        {
            BrilliantDiamondID => new PokeDataOffsetsBS_BD(),
            ShiningPearlID => new PokeDataOffsetsBS_SP(),
            _ => throw new Exception($"{title} is not a valid Pokémon BDSP title. Is your mode correct?"),
        };

        // Verify the game version.
        var game_version = await SwitchConnection.GetGameInfo("version", token).ConfigureAwait(false);
        if (!game_version.SequenceEqual(BSGameVersion))
            throw new Exception($"Game version is not supported. Expected version {BSGameVersion}, and current game version is {game_version}.");

        var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
        InitSaveData(sav);

        if (!IsValidTrainerData())
        {
            await CheckForRAMShiftingApps(token).ConfigureAwait(false);
            throw new Exception("Refer to the SysBot.NET wiki (https://github.com/kwsch/SysBot.NET/wiki/Troubleshooting) for more information.");
        }

        if (await GetTextSpeed(token).ConfigureAwait(false) < TextSpeedOption.Fast)
            throw new Exception("Text speed should be set to FAST. Fix this for correct operation.");

        return sav;
    }

    public async Task<SAV8BS> GetFakeTrainerSAV(CancellationToken token)
    {
        var sav = new SAV8BS();
        var info = sav.MyStatus;

        // Set the OT.
        var name = await SwitchConnection.PointerPeek(TradePartnerBS.MaxByteLengthStringObject, Offsets.MyStatusTrainerPointer, token).ConfigureAwait(false);
        info.OT = TradePartnerBS.ReadStringFromRAMObject(name);

        // Set the TID, SID, and Language
        var offset = await SwitchConnection.PointerAll(Offsets.MyStatusTIDPointer, token).ConfigureAwait(false);
        var tid = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 2, token).ConfigureAwait(false);
        var sid = await SwitchConnection.ReadBytesAbsoluteAsync(offset + 2, 2, token).ConfigureAwait(false);

        info.TID16 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(tid);
        info.SID16 = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(sid);

        var lang = await SwitchConnection.PointerPeek(1, Offsets.ConfigLanguagePointer, token).ConfigureAwait(false);
        sav.Language = lang[0];
        return sav;
    }

    public async Task InitializeHardware(IBotStateSettings settings, CancellationToken token)
    {
        Log("Detaching on startup.");
        await DetachController(token).ConfigureAwait(false);
        if (settings.ScreenOff)
        {
            Log("Turning off screen.");
            await SetScreen(ScreenState.Off, token).ConfigureAwait(false);
        }

        Log("Setting BDSP-specific hid waits.");
        await Connection.SendAsync(SwitchCommand.Configure(SwitchConfigureParameter.keySleepTime, 50), token).ConfigureAwait(false);
        await Connection.SendAsync(SwitchCommand.Configure(SwitchConfigureParameter.pollRate, 50), token).ConfigureAwait(false);
    }

    public async Task CleanExit(CancellationToken token)
    {
        await SetScreen(ScreenState.On, token).ConfigureAwait(false);
        Log("Detaching controllers on routine exit.");
        await DetachController(token).ConfigureAwait(false);
    }

    protected virtual async Task EnterLinkCode(int code, PokeTradeHubConfig config, CancellationToken token)
    {
        // Default implementation to just press directional arrows. Can do via Hid keys, but users are slower than bots at even the default code entry.
        var keys = TradeUtil.GetPresses(code);
        foreach (var key in keys)
        {
            int delay = config.Timings.KeypressTime;
            await Click(key, delay, token).ConfigureAwait(false);
        }
        // Confirm Code outside of this method (allow synchronization)
    }

    public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
    {
        Log("Error detected, restarting the game!!");
        await CloseGame(config, token).ConfigureAwait(false);
        await StartGame(config, token).ConfigureAwait(false);
    }

    public async Task UnSoftBan(CancellationToken token)
    {
        Log("Soft ban detected, unbanning.");
        // Write the float value to 0.
        var data = BitConverter.GetBytes(0);
        await SwitchConnection.PointerPoke(data, Offsets.UnionWorkPenaltyPointer, token).ConfigureAwait(false);
    }

    public async Task<bool> CheckIfSoftBanned(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 4, token).ConfigureAwait(false);
        return BitConverter.ToUInt32(data, 0) != 0;
    }

    public async Task CloseGame(PokeTradeHubConfig config, CancellationToken token)
    {
        var timing = config.Timings;
        // Close out of the game
        await Click(HOME, 2_000 + timing.ExtraTimeReturnHome, token).ConfigureAwait(false);
        await Click(X, 1_000, token).ConfigureAwait(false);
        await Click(A, 5_000 + timing.ExtraTimeCloseGame, token).ConfigureAwait(false);
        Log("Closed out of the game!");
    }

    public async Task StartGame(PokeTradeHubConfig config, CancellationToken token)
    {
        var timing = config.Timings;
        // Open game.
        await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);

        // Menus here can go in the order: Update Prompt -> Profile -> Starts Game
        //  The user can optionally turn on the setting if they know of a breaking system update incoming.
        if (timing.AvoidSystemUpdate)
        {
            await Task.Delay(1_000, token).ConfigureAwait(false); // Reduce the chance of misclicking here.
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);
        }

        await Click(A, 0_600, token).ConfigureAwait(false);

        Log("Restarting the game!");

        // Switch Logo lag, skip cutscene, game load screen
        await Task.Delay(22_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

        for (int i = 0; i < 10; i++)
            await Click(A, 1_000, token).ConfigureAwait(false);

        var timer = 60_000;
        while (!await IsSceneID(SceneID_Field, token).ConfigureAwait(false))
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            timer -= 1_000;
            // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
            // Don't risk it if hub is set to avoid updates.
            if (timer <= 0 && !timing.AvoidSystemUpdate)
            {
                Log("Still not in the game, initiating rescue protocol!");
                if (!await CheckBootError(config, token).ConfigureAwait(false))                
                {
                    //Click A until overworld
                    while (!await IsSceneID(SceneID_Field, token).ConfigureAwait(false))
                        await Click(A, 6_000, token).ConfigureAwait(false);
                }
                break;
            }
        }

        await Task.Delay(5_000 + timing.ExtraTimeLoadOverworld, token).ConfigureAwait(false);
        Log("Back in the overworld!");
    }
    public async Task<bool> ResumeStart(PokeTradeHubConfig config, CancellationToken token)
    {
        var success = true;
        var timing = config.Timings;

        // Switch Logo lag, skip cutscene, game load screen
        await Task.Delay(17_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

        for (int i = 0; i < 10; i++)
            await Click(A, 1_000, token).ConfigureAwait(false);

        var timer = 60_000;
        while (!await IsSceneID(SceneID_Field, token).ConfigureAwait(false))
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            timer -= 1_000;
            // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
            // Don't risk it if hub is set to avoid updates.
            if (timer <= 0 && !timing.AvoidSystemUpdate)
            {
                Log("Still not in the game, initiating rescue protocol!");
                //Check if the game loading dropped an error
                if (await CheckBootError(config, token).ConfigureAwait(false))
                    success = false;
                else
                {
                    //Click A until overworld
                    while (!await IsSceneID(SceneID_Field, token).ConfigureAwait(false))
                        await Click(A, 6_000, token).ConfigureAwait(false);
                }

                break;
            }
        }

        await Task.Delay(5_000 + timing.ExtraTimeLoadOverworld, token).ConfigureAwait(false);
        Log("Back in the overworld!");

        return success;
    }
    public async Task<bool> CheckBootError(PokeTradeHubConfig config, CancellationToken token)
    {
        //Check if the game loading dropped an error
        var state = await SwitchConnection.PointerPeek(16, Offsets.MainRNGState, token).ConfigureAwait(false);
        var S1 = BitConverter.ToUInt32(state, 4);
        var S3 = BitConverter.ToUInt32(state, 12);
        if (S1 == S3)
        {
            Log("Error message detected.");
            await Click(A, 1_000, token).ConfigureAwait(false);
            await StartGame(config, token).ConfigureAwait(false);
            return true;
        }
        return false;
    }

    private async Task<bool> IsSceneID(uint expected, CancellationToken token)
    {
        var byt = await SwitchConnection.PointerPeek(1, Offsets.SceneIDPointer, token).ConfigureAwait(false);
        return byt[0] == expected;
    }

    // Uses absolute offset which is set each session. Checks for IsGaming or IsTalking.
    public async Task<bool> IsUnionWork(ulong offset, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
        return data[0] == 1;
    }

    // Whenever we're in a trade, this pointer will be loaded, otherwise 0
    public async Task<bool> IsPartnerParamLoaded(CancellationToken token)
    {
        var byt = await SwitchConnection.PointerPeek(8, Offsets.LinkTradePartnerParamPointer, token).ConfigureAwait(false);
        return BitConverter.ToUInt64(byt, 0) != 0;
    }

    public async Task<ulong> GetTradePartnerNID(CancellationToken token) => BitConverter.ToUInt64(await SwitchConnection.PointerPeek(sizeof(ulong), Offsets.LinkTradePartnerNIDPointer, token).ConfigureAwait(false), 0);

    public async Task<TextSpeedOption> GetTextSpeed(CancellationToken token)
    {
        var data = await SwitchConnection.PointerPeek(1, Offsets.ConfigTextSpeedPointer, token).ConfigureAwait(false);
        return (TextSpeedOption)data[0];
    }

    public async Task<bool> OpenBag(CancellationToken token)
    {
        Log("Opening Bag...");
        await Click(SwitchButton.X, 0_900, token).ConfigureAwait(false);
        await Click(SwitchButton.PLUS, 1_350, token).ConfigureAwait(false);
        await Click(SwitchButton.B, 1_000, token).ConfigureAwait(false);
        await Click(SwitchButton.DUP, 0_250, token).ConfigureAwait(false);
        await Click(SwitchButton.DRIGHT, 0_250, token).ConfigureAwait(false);
        await Click(SwitchButton.A, 1_000, token).ConfigureAwait(false);
        return true;
    }

    public async Task CloseBag(CancellationToken token)
    {
        Log("Closing Bag...");
        await Click(SwitchButton.B, 0_250, token).ConfigureAwait(false);
        for (int i = 0; i < 3; i++)
        {
            await Click(SwitchButton.B, 1_000, token).ConfigureAwait(false);
        }
    }

    public async Task<int> Wildloop(int counter, int timings, CancellationToken token)
    {
        int loopcount = counter;
        if (loopcount == 0)
        {
            for (int loop = 0; loop < 5; loop++)
            {
                await Click(SwitchButton.DRIGHT, timings, token).ConfigureAwait(false);
            }

            await Click(SwitchButton.A, timings, token).ConfigureAwait(false);
        }
        else
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);
            await Click(SwitchButton.A, timings, token).ConfigureAwait(false);
        }

        loopcount++;

        return loopcount;
    }

    public async Task<bool> Runaway(CancellationToken token)
    {
        Log("Running away from unwanted encounter...");
        await Task.Delay(30_000).ConfigureAwait(false);
        await Click(SwitchButton.DUP, 1_000, token).ConfigureAwait(false);
        await Click(SwitchButton.A, 0_900, token).ConfigureAwait(false);
        await Click(SwitchButton.A, 0_900, token).ConfigureAwait(false);
        await Task.Delay(20_000).ConfigureAwait(false);
        return true;
    }

    public async Task OpenDex(int inputDelay, CancellationToken token)
    {
        Log("Opening Pokedex...");
        await Click(SwitchButton.X, 1_000 + inputDelay, token).ConfigureAwait(false);
        await Click(SwitchButton.PLUS, 1_750 + inputDelay, token).ConfigureAwait(false);
        await Click(SwitchButton.B, 1_150 + inputDelay, token).ConfigureAwait(false);
        await Click(SwitchButton.DUP, 0_150 + inputDelay, token).ConfigureAwait(false);
        await Click(SwitchButton.A, 1_000 + inputDelay, token).ConfigureAwait(false);
    }

    public async Task ReOpenDex(int inputDelay, CancellationToken token)
    {
        Log("ReOpening dex for better advancement performance.");
        await Click(B, 1_150 + inputDelay, token).ConfigureAwait(false);
        await Click(A, 1_000 + inputDelay, token).ConfigureAwait(false);
    }

    public async Task CloseDex(CancellationToken token)
    {
        for (int i = 0; i < 5; i++)
            await Click(SwitchButton.B, 0_350, token).ConfigureAwait(false);
    }

    public List<int> GetEncounterSlots(GameVersion version, int location, GameTime time, WildMode mode, int index = 0)
    {
        string res_data = version is GameVersion.BD ? Properties.Resource.FieldEncountTable_d : Resource.FieldEncountTable_p;
        //string res_data_ug = Resource.UgEncount(index);
        EncounterTable? ectable = JsonConvert.DeserializeObject<EncounterTable>(res_data);
        //EncounterTable_ug? ectable_ug = JsonConvert.DeserializeObject<EncounterTable_ug>(res_data_ug);
        Table current_table = new();
        var list = new List<int>();
        int i = 2;

        if (mode is not WildMode.Underground)
        {
            if (ectable != null)
            {
                if (ectable.table != null)
                {
                    foreach (var table in ectable.table)
                    {
                        if (table.zoneID == location)
                            current_table = table;
                    }
                    if (current_table != null)
                    {
                        if ((mode is WildMode.Grass || mode is WildMode.Swarm) && current_table.ground_mons != null)
                        {
                            foreach (var specie in current_table.ground_mons)
                                list.Add(specie.monsNo);
                            if ((time is GameTime.Night or GameTime.DeepNight) && current_table.night != null)
                                foreach (var specie in current_table.night)
                                {
                                    list[i] = specie.monsNo;
                                    i++;
                                }
                            else if ((time is GameTime.Day or GameTime.Sunset) && current_table.day != null)
                                foreach (var specie in current_table.day)
                                {
                                    list[i] = specie.monsNo;
                                    i++;
                                }
                            if (mode is WildMode.Swarm && current_table.tairyo != null)
                            {
                                i = 0;
                                foreach (var specie in current_table.tairyo)
                                {
                                    list[i] = specie.monsNo;
                                    i++;
                                }
                            }
                        }
                        else if (mode is WildMode.Surf && current_table.water_mons != null)
                            foreach (var specie in current_table.water_mons)
                                list.Add(specie.monsNo);
                        else if (mode is WildMode.OldRod && current_table.boro_mons != null)
                            foreach (var specie in current_table.boro_mons)
                                list.Add(specie.monsNo);
                        else if (mode is WildMode.GoodRod && current_table.ii_mons != null)
                            foreach (var specie in current_table.ii_mons)
                                list.Add(specie.monsNo);
                        else if (mode is WildMode.SuperRod && current_table.sugoi_mons != null)
                            foreach (var specie in current_table.sugoi_mons)
                                list.Add(specie.monsNo);
                    }
                }
            }
        }
        else
        {
            /*if (ectable_ug != null)
            {
                if (ectable_ug.table != null)
                {
                    Table_ug? ground_mons = ectable_ug.table;
                    if (ground_mons != null && ground_mons.ground_mons != null)
                    {
                        foreach (var specie in ground_mons.ground_mons)
                            list.Add(specie.monsNo);
                    }
                }
            }*/
        }
        return list;
    }

    public int[] GetUnownForms(int location)
    {
        return location switch
        {
            229 => new int[] { 5 },
            231 => new int[] { 17 },
            237 => new int[] { 8 },
            238 => new int[] { 13 },
            239 => new int[] { 4 },
            240 => new int[] { 3 },
            225 => new int[] { 26, 27 },
            _ => new int[] { 0, 1, 2, 6, 7, 9, 10, 11, 12, 14, 15, 16, 18, 19, 20, 21, 22, 23, 24, 25 },
        };
    }

    public List<SwitchButton> ParseActions(string config_actions)
    {
        var action = new List<SwitchButton>();
        var actions = $"{config_actions.ToUpper()},";
        var word = "";
        var index = 0;

        while (index < actions.Length - 1)
        {
            if ((actions.Length > 1 && (actions[index + 1] == ',' || actions[index + 1] == '.')) || actions.Length == 1)
            {
                word += actions[index];
                if (Enum.IsDefined(typeof(SwitchButton), word))
                    action.Add((SwitchButton)Enum.Parse(typeof(SwitchButton), word));
                actions.Remove(0, 1);
                word = "";
            }
            else if (actions[index] == ',' || actions[index] == '.' || actions[index] == ' ' || actions[index] == '\n' || actions[index] == '\t' || actions[index] == '\0')
                actions.Remove(0, 1);
            else
            {
                word += actions[index];
                actions.Remove(0, 1);
            }
            index++;
        }

        return action;
    }

    public string GetString(PB8 pk)
    {
        return $"\nGender: {(Gender)pk.Gender}\nEC: {pk.EncryptionConstant:X}\nPID: {pk.PID:X} {GetShinyType(pk)}\n" +
        $"{(Nature)pk.Nature} nature\nAbility slot: {pk.AbilityNumber}\nAbility: {(Ability)pk.Ability}\n" +
        $"IVs: [{pk.IV_HP}, {pk.IV_ATK}, {pk.IV_DEF}, {pk.IV_SPA}, {pk.IV_SPD}, {pk.IV_SPE}]\n";
    }

    public string GetShinyType(PB8 pk)
    {
        if (pk.IsShiny)
        {
            if (pk.ShinyXor == 0)
                return "(Square)";
            return "(Star)";
        }
        return "";
    }

    public async Task DoActions(List<SwitchButton> actions, int timings, CancellationToken token)
    {
        for (var i = 0; i < actions.Count - 1; i++)
        {
            Log($"Press {actions[i]}.");
            await Click(actions[i], timings, token).ConfigureAwait(false);
        }
    }

    public async Task<InventoryItem8b[]> GetItems(CancellationToken token)
    {
        var ItemOffset = await SwitchConnection.PointerAll(Offsets.ItemBlockPointer, token).ConfigureAwait(false);
        var ItemBlock = await SwitchConnection.ReadBytesAbsoluteAsync(ItemOffset, ITEM_BLOCK_SIZE_RAM, token).ConfigureAwait(false);
        var pouch = new InventoryPouch8b(InventoryType.Treasure, ItemStorage8BDSP.Instance, 999);
        pouch.GetPouch(ItemBlock);
        return (InventoryItem8b[])pouch.Items;
    }
}
