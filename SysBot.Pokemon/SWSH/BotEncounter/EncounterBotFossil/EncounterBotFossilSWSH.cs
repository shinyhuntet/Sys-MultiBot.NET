using PKHeX.Core;
using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

namespace SysBot.Pokemon;

public class EncounterBotFossilSWSH : EncounterBotSWSH
{
    private new readonly FossilSettings Settings;
    private readonly IDumper DumpSetting;

    public EncounterBotFossilSWSH(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg, hub)
    {
        Settings = Hub.Config.EncounterSWSH.Fossil;
        DumpSetting = Hub.Config.Folder;
    }

    private static readonly PK8 Blank = new();
    private byte[] TreasurePouch = [0];

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        await SetupBoxState(DumpSetting, token).ConfigureAwait(false);

        Log("Checking item counts...");
        (int reviveCount, FossilCount counts) = await CheckFossiCount(token).ConfigureAwait(false);
        Log($"Enough fossil pieces are available to revive {reviveCount} {Settings.Species}.");

        while (!token.IsCancellationRequested)
        {
            if (encounterCount != 0 && encounterCount % reviveCount == 0)
            {
                Log($"Ran out of fossils to revive {Settings.Species}.");
                if (Settings.InjectWhenEmpty)
                {
                    Log("Restoring original pouch data.");
                    (reviveCount, counts) = await CheckFossiCount(token).ConfigureAwait(false);
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
                else
                {
                    Log("Fossil pieces have been depleted. Resetting the game.");
                    await CloseGame(Hub.Config, token).ConfigureAwait(false);
                    await StartGame(Hub.Config, token).ConfigureAwait(false);
                    await SetupBoxState(DumpSetting, token).ConfigureAwait(false);
                }
            }

            await ReviveFossil(counts, token).ConfigureAwait(false);
            Log("Fossil revived. Checking details...");

            var pk = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
            if (pk.Species == 0 || !pk.ChecksumValid)
            {
                Log("No fossil found in Box 1, slot 1. Ensure that the party is full. Restarting loop.");
                continue;
            }

            var (stop, _) = await HandleEncounter(pk, token).ConfigureAwait(false);
            if (stop)
                return;

            Log("Clearing destination slot.");
            await SetBoxPokemon(Blank, 0, 0, token).ConfigureAwait(false);
        }
    }
    private async Task<int> SetFossilCount(ushort itemID, CancellationToken token)
    {
        var TrainerSav = new SAV8SWSH();
        var Treasure = new InventoryPouch8(InventoryType.Ingredients, ItemStorage8SWSH.Instance, 999, 0, ItemStorage8SWSH.Treasure.Length);
        Treasure.GetPouch(TreasurePouch);
        var OriginalItems = Array.ConvertAll(Treasure.Items, item => item.Index);
        var Count = Treasure.GiveItem(TrainerSav, itemID);
        var data = new byte[TreasurePouch.Length];
        var writePouch = (InventoryItem8[])Treasure.Items;
        var itemsize = sizeof(uint);
        for (int i = 0; i < writePouch.Length; i++)
        {
            var ItemIndex = i * itemsize;
            uint val = writePouch[i].GetValue(false, OriginalItems);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan()[ItemIndex..(ItemIndex + itemsize)], val);
        }
        await Connection.WriteBytesAsync(data, ItemTreasureAddress, token).ConfigureAwait(false);
        Log($"Target Fossil is set completely!{Environment.NewLine}Fossil: {GameInfo.GetStrings(GameLanguage.LanguageCode(1)).itemlist[itemID]}, Count: {Count}");
        return Count;
    }
    private async Task<(int, FossilCount)> CheckFossiCount(CancellationToken token)
    {
        int reviveCount = 0;
        ushort minfossil = 0;
        FossilCount counts = new();
        while (reviveCount == 0)
        {
            TreasurePouch = await Connection.ReadBytesAsync(ItemTreasureAddress, 80, token).ConfigureAwait(false);
            counts = FossilCount.GetFossilCounts(TreasurePouch);
            (reviveCount, minfossil) = counts.PossibleRevives(Settings.Species);
            if (reviveCount == 0)
            {
                Log("Insufficient fossil pieces. Please obtain at least one of each required fossil piece first.");
                var fossilcount = await SetFossilCount(minfossil, token).ConfigureAwait(false);
            }
        }
        return (reviveCount, counts);
    }
    private async Task ReviveFossil(FossilCount count, CancellationToken token)
    {
        Log("Starting fossil revival routine...");
        if (GameLang == LanguageID.Spanish)
            await Click(A, 0_900, token).ConfigureAwait(false);

        await Click(A, 1_100, token).ConfigureAwait(false);

        // French is slightly slower.
        if (GameLang == LanguageID.French)
            await Task.Delay(0_200, token).ConfigureAwait(false);

        await Click(A, 1_300, token).ConfigureAwait(false);

        // Selecting first fossil.
        if (count.UseSecondOption1(Settings.Species))
            await Click(DDOWN, 0_300, token).ConfigureAwait(false);
        await Click(A, 1_300, token).ConfigureAwait(false);

        // Selecting second fossil.
        if (count.UseSecondOption2(Settings.Species))
            await Click(DDOWN, 300, token).ConfigureAwait(false);

        // A spam through accepting the fossil and agreeing to revive.
        for (int i = 0; i < 16; i++)
            await Click(A, 0_200, token).ConfigureAwait(false);

        // Safe to mash B from here until we get out of all menus.
        while (!await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            await Click(B, 0_200, token).ConfigureAwait(false);
    }
}
