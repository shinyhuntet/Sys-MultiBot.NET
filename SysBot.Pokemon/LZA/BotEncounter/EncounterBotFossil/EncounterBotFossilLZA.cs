namespace SysBot.Pokemon;

using System;
using System.Buffers.Binary;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchButton;
using static PokeDataOffsetsLZA;

public class EncounterBotFossilLZA(PokeBotState cfg, PokeTradeHub<PA9> hub) : EncounterBotLZA(cfg, hub)
{
    private new readonly FossilSettingsLZA Settings = hub.Config.EncounterLZA.Fossil;

    private bool _itemKeyInitialized;
    private byte _box;
    private byte _slot;
    private byte[] TreasurePouch = [];

    protected override async Task EncounterLoop(SAV9ZA sav, CancellationToken token)
    {
        Log("Make sure the first box is selected and there's enough free space!");

        Log("Checking item counts...");
        (int reviveCount, FossilCountLZA counts) = await CheckFossil(token).ConfigureAwait(false);
        Log($"Enough fossil pieces are available to revive {reviveCount} {(Settings.Species is FossilSpeciesLZA.Any ? "fossils" : Settings.Species)}.");

        PA9? prev = null;
        while (!token.IsCancellationRequested)
        {
            if (EncounterCount != 0 && EncounterCount % reviveCount == 0)
            {
                if (Settings.InjectWhenEmpty)
                {
                    Log("Restoring original pouch data.");
                    (reviveCount, counts) = await CheckFossil(token).ConfigureAwait(false);
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
                else
                {
                    Log("Fossil pieces have been depleted. Resetting the game.");
                    _box = _slot = 0;
                    await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                }
            }

            await ReviveFossil(token).ConfigureAwait(false);
            Log("Fossil revived. Checking details...");

            var (pa9, raw) = await ReadRawBoxPokemon(_box, _slot, token).ConfigureAwait(false);
            if (pa9.Species == 0 || !pa9.ChecksumValid || pa9.EncryptionConstant == prev?.EncryptionConstant)
            {
                Log($"No fossil found in Box {_box + 1}, slot {_slot + 1}. Ensure that the party is full. Restarting loop.");
                continue;
            }

            if (new[] { (int)Species.Aerodactyl, (int)Species.Tyrunt, (int)Species.Amaura }.Contains(pa9.Species) == false)
            {
                Log($"Fossil revival appears to have failed, found {(Species)pa9.Species}.");
                return;
            }

            var (stop, success) = await HandleEncounter(pa9, token, raw).ConfigureAwait(false);

            if (success) Log($"You're fossil has been claimed and placed in B{_box + 1}S{_slot + 1}. Be sure to save your game!");

            _slot += 1;
            if (_slot == 30)
            {
                _box++;
                _slot = 0;
            }

            if (stop)
                return;

            prev = pa9;
        }
    }

    private async Task<byte[]> GetPouchData(CancellationToken token)
    {
        _itemKeyInitialized = false;
        var bytes = await ReadEncryptedBlock(Offsets.KItemPointer, KItemKey, !_itemKeyInitialized, token).ConfigureAwait(false);
        _itemKeyInitialized = true;

        return bytes;
    }
    private async Task<(int, FossilCountLZA)> CheckFossil(CancellationToken token)
    {
        var reviveCount = 0;
        FossilCountLZA counts = new();
        while(reviveCount == 0)
        {
            var pouchData = await GetPouchData(token).ConfigureAwait(false);
            counts = FossilCountLZA.GetFossilCounts(pouchData);
            reviveCount = counts.PossibleRevives(Settings.Species);
            if (reviveCount == 0)
            {
                Log("Insufficient fossil pieces. Please obtain at least one of each required fossil piece first.");
                var data = await SetFossilCount(token).ConfigureAwait(false);
                await WriteEncryptedBlockObject(Offsets.KItemPointer, KItemKey, KItemSize, pouchData, data, token).ConfigureAwait(false);
            }
        }
        return (reviveCount, counts);

    }
    private async Task<byte[]> SetFossilCount(CancellationToken token)
    {
        var sav = new SAV9ZA();
        var pouch = FossilCountLZA.GetTreasurePouch(TreasurePouch);
        pouch.GiveItem(sav, 710);
        pouch.GiveItem(sav, 711);
        pouch.GiveItem(sav, 103);
        var writepouch = (InventoryItem9a[])pouch.Items;
        byte[] data = new byte[TreasurePouch.Length];
        var writesize = sizeof(uint) * 4;
        for (int i = 0; i < writepouch.Length; i++)
        {
            var ItemIndex = i * writesize;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan()[ItemIndex..], writepouch[i].Pouch);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan()[(ItemIndex + 0x4)..], (uint)writepouch[i].Count);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan()[(ItemIndex + 0x8)..], writepouch[i].Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan()[(ItemIndex + 0xC)..], writepouch[i].Padding);
        }
        return data;
    }
    private async Task ReviveFossil(CancellationToken token)
    {
        Log("Starting fossil revival routine...");

        if (Settings.Species == FossilSpeciesLZA.Any)
        {
            // Just mash the buttons through the menus if any fossil is acceptable.
            for (var i = 0; i < 14; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);

            await Task.Delay(3_000, token).ConfigureAwait(false);

            for (var i = 0; i < 16; i++)
                await Click(B, 0_500, token).ConfigureAwait(false);

            return;
        }

        for (var i = 0; i < 4; i++)
            await Click(A, 1_100, token).ConfigureAwait(false);

        switch (Settings.Species)
        {
            // Selecting second fossil.
            case FossilSpeciesLZA.Amaura:
                await Click(DDOWN, 0_300, token).ConfigureAwait(false);
                break;

            // Selecting third fossil.
            case FossilSpeciesLZA.Aerodactyl:
                {
                    for (var i = 0; i < 2; i++) await Click(DDOWN, 0_300, token).ConfigureAwait(false);
                    break;
                }
        }

        // A spam through accepting the fossil and agreeing to revive.
        for (var i = 0; i < 6; i++)
            await Click(A, 0_500, token).ConfigureAwait(false);

        await Task.Delay(3_000, token).ConfigureAwait(false);

        for (var i = 0; i < 16; i++)
            await Click(B, 0_500, token).ConfigureAwait(false);
    }
}
