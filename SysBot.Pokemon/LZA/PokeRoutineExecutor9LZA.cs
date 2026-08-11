namespace SysBot.Pokemon;

using Base;
using Google.FlatBuffers;
using PKHeX.Core;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Base.SwitchButton;
using static PokeDataOffsetsLZA;
using static SysBot.Pokemon.EncounterSettingsLZA;
using static System.Buffers.Binary.BinaryPrimitives;

public abstract class PokeRoutineExecutor9LZA(PokeBotState cfg) : PokeRoutineExecutor<PA9>(cfg)
{
    protected PokeDataOffsetsLZA Offsets { get; } = new();
    private const int STASHED_SHINIES_MAX = 10;
    private const int PA9_SIZE = 0x148;
    private const int PA9_BLOCK_SIZE = 0x158;
    private const int STRUCT_SIZE = 0x28;
    private const int PA9_BUFFER = 0x1F0;

    public override async Task<PA9> ReadPokemon(ulong offset, CancellationToken token) => await ReadPokemon(offset, FormatSlotSize, token).ConfigureAwait(false);

    public override async Task<PA9> ReadPokemon(ulong offset, int size, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
        return new PA9(data);
    }

    public override async Task<PA9> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
    {
        var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        if (!valid)
            return new PA9();

        return await ReadPokemon(offset, token).ConfigureAwait(false);
    }

    public async Task<(PA9, byte[]?)> ReadRawBoxPokemon(int box, int slot, CancellationToken token)
    {
        var jumps = Offsets.BoxStartPokemonPointer.ToArray();
        bool valid;
        ulong b1s1;
        await _connectionLock.WaitAsync(token);
        try
        {
            (valid, b1s1) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
        if (!valid)
            return (new PA9(), null);

        const int boxSize = 30 * BoxSlotSize;
        var boxStart = b1s1 + (ulong)(box * boxSize);
        var slotStart = boxStart + (ulong)(slot * BoxSlotSize);

        var copiedData = new byte[BoxSlotSize];

        byte[] data;
        await _connectionLock.WaitAsync(token);
        try
        {
            data = await SwitchConnection.ReadBytesAbsoluteAsync(slotStart, BoxSlotSize, token).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }

        data.CopyTo(copiedData, 0);

        if (!data.SequenceEqual(copiedData))
            throw new InvalidOperationException("Raw data is not copied correctly");

        return (new PA9(data), copiedData);
    }

    public override async Task<PA9> ReadBoxPokemon(int box, int slot, CancellationToken token)
    {
        var (pa9, _) = await ReadRawBoxPokemon(box, slot, token).ConfigureAwait(false);
        return pa9;
    }
    public async Task<(int, int)> ReadEmptySlot(int box, int slot, CancellationToken token)
    {
        bool valid;
        ulong b1s1;
        await _connectionLock.WaitAsync(token);
        try
        {
            (valid, b1s1) = await ValidatePointerAll(Offsets.BoxStartPokemonPointer, token).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
        if (!valid)
            return (-1, -1);

        const int boxSize = 30 * BoxSlotSize;
        byte[] data;
        
        while (box < 32)
        {
            var boxStart = b1s1 + (ulong)(box * boxSize);
            var slotStart = boxStart + (ulong)(slot * BoxSlotSize);
            await _connectionLock.WaitAsync(token);
            try
            {
                data = await SwitchConnection.ReadBytesAbsoluteAsync(slotStart, BoxSlotSize, token).ConfigureAwait(false);
            }
            finally
            {
                _connectionLock.Release();
            }
            var pk = new PA9(data);
            if (pk.Species == 0)
                return (box, slot);
            slot++;
            if (slot == 30)
            {
                slot = 0;
                box++;
            }
        }
        return (-1, -1);
    }
    public async Task SetBoxPokemonAbsolute(ulong offset, PK9 pkm, CancellationToken token, ITrainerInfo? sav = null)
    {
        if (sav != null)
        {
            // Update PKM to the current save's handler data
            pkm.UpdateHandler(sav);
            pkm.RefreshChecksum();
        }

        pkm.ResetPartyStats();

        var encrypted = pkm.EncryptedBoxData;
        var boxData = new byte[encrypted.Length + 0x40];
        Buffer.BlockCopy(encrypted, 0, boxData, 0, encrypted.Length);

        await SwitchConnection.WriteBytesAbsoluteAsync(boxData, offset, token).ConfigureAwait(false);
    }

    public async Task<SAV9ZA> IdentifyTrainer(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            // Check if botbase is on the correct version or later.
            await VerifyBotbaseVersion(token).ConfigureAwait(false);

            // Check title so we can warn if mode is incorrect.
            string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
            if (title != LegendsZAID)
                throw new Exception($"{title} is not a valid Pokémon Legends: ZA title. Is your mode correct?");

            // Verify the game version.
            var game_version = await SwitchConnection.GetGameInfo("version", token).ConfigureAwait(false);
            if (!game_version.SequenceEqual(ZAGameVersion))
                throw new Exception($"Game version is not supported. Expected version {ZAGameVersion}, and current game version is {game_version}.");

            var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
            InitSaveData(sav);

            if (!IsValidTrainerData())
            {
                await CheckForRAMShiftingApps(token).ConfigureAwait(false);
                throw new Exception("Refer to the SysBot.NET wiki (https://github.com/kwsch/SysBot.NET/wiki/Troubleshooting) for more information.");
            }

            return sav;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    public async Task SetPlayerPosition(float x, float y, float z, CancellationToken token)
    {
        byte[] xb = BitConverter.GetBytes(x);
        byte[] yb = BitConverter.GetBytes(y);
        byte[] zb = BitConverter.GetBytes(z);
        var bytes = xb.Concat(yb).Concat(zb).ToArray();

        await _connectionLock.WaitAsync(token);
        try
        {
            var offset = await SwitchConnection.PointerAll(Offsets.PlayerCoordPointer, token);
            await SwitchConnection.WriteBytesAbsoluteAsync(bytes, offset, token);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    public async Task<Vector3> GetPlayerPosition(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            var offset = await SwitchConnection.PointerAll(Offsets.PlayerCoordPointer, token);
            var bytes = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 12, token);

            float xn = BitConverter.ToSingle(bytes, 0);
            float yn = BitConverter.ToSingle(bytes, 4);
            float zn = BitConverter.ToSingle(bytes, 8);

            return new Vector3( xn, yn, zn );
        }
        catch
        {
            return new Vector3(0, 0, 0);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    public async Task<SAV9ZA> GetFakeTrainerSAV(CancellationToken token)
    {
        var sav = new SAV9ZA();
        var info = sav.MyStatus;
        var read = await SwitchConnection.PointerPeek(info.Data.Length, Offsets.MyStatusPointer, token).ConfigureAwait(false);
        read.CopyTo(info.Data);
        return sav;
    }

    public async Task InitializeHardware(IBotStateSettings settings, CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            Log("Detaching on startup.");
            await DetachController(token).ConfigureAwait(false);
            if (settings.ScreenOff)
            {
                Log("Turning off screen.");
                await SetScreen(ScreenState.Off, token).ConfigureAwait(false);
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task CleanExit(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            await SetScreen(ScreenState.On, token).ConfigureAwait(false);
            Log("Detaching controllers on routine exit.");
            await DetachController(token).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
    {
        Log("Restarting the game!!");
        await CloseGame(config, token).ConfigureAwait(false);
        await StartGame(config, token).ConfigureAwait(false);
    }
    public async Task CloseGame(PokeTradeHubConfig config, CancellationToken token)
    {
        var timing = config.Timings;
        // Close out of the game
        await _connectionLock.WaitAsync(token);
        try
        {
            await FastClick(B, 0_500, token).ConfigureAwait(false);
            await FastClick(HOME, 2_000 + timing.ExtraTimeReturnHome, token).ConfigureAwait(false);
            await FastClick(X, 1_000, token).ConfigureAwait(false);
            await FastClick(A, 5_000 + timing.ExtraTimeCloseGame, token).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
        Log("Closed out of the game!");
    }

    public async Task StartGame(PokeTradeHubConfig config, CancellationToken token)
    {
        var timing = config.Timings;
        await _connectionLock.WaitAsync(token);
        try
        {
            // Open game.
            await FastClick(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            // Menus here can go in the order: Update Prompt -> Profile -> Starts Game
            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            if (timing.AvoidSystemUpdate)
            {
                await Task.Delay(1_000, token).ConfigureAwait(false); // Reduce the chance of misclicking here.
                await FastClick(DUP, 0_600, token).ConfigureAwait(false);
                await FastClick(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);
            }

            await FastClick(DUP, 0_600, token).ConfigureAwait(false);
            await FastClick(A, 0_600, token).ConfigureAwait(false);

            Log("Restarting the game!");

            // Switch Logo...
            await Task.Delay(12_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

            // ... and game load screen
            for (int i = 0; i < 8; i++)
                await FastClick(A, 1_000, token).ConfigureAwait(false);

            int timer = 60_000;
            while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                timer -= 1_000;
                if (timer <= 0)
                {
                    while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
                        await FastClick(A, 6_000, token).ConfigureAwait(false);

                    break;
                }
            }

            await Task.Delay(4_000 + timing.ExtraTimeLoadOverworld, token).ConfigureAwait(false);
            Log("Back in the overworld!");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<byte[]> GetCurrentWeather(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            var offset = await SwitchConnection.PointerAll(Offsets.WeatherPointer, token).ConfigureAwait(false);
            var weather = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 4, token).ConfigureAwait(false);
            return weather;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task SetCurrentWeather(byte[] weather, CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            var offset = await SwitchConnection.PointerAll(Offsets.WeatherPointer, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(weather, offset, token).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<byte[]> GetCurrentTime(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            var offset = await SwitchConnection.PointerAll(Offsets.TimePointer, token).ConfigureAwait(false);
            var time = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 4, token).ConfigureAwait(false);
            return time;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task SetCurrentTime(byte[] time, CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            var offset = await SwitchConnection.PointerAll(Offsets.TimePointer, token).ConfigureAwait(false);
            await SwitchConnection.WriteBytesAbsoluteAsync(time, offset, token).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    public async Task<bool> IsOnOverworldTitle(CancellationToken token)
    {
        return await IsOnMenu(MenuState.Overworld, token).ConfigureAwait(false) && await IsOnOverworld(token).ConfigureAwait(false);
    }

    public async Task<bool> IsOnMenu(MenuState state, CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesMainAsync(MenuOffset, 1, token).ConfigureAwait(false);
        return (MenuState)data[0] == state;
    }
    public async Task<bool> IsOnOverworld(CancellationToken token)
    {
        var data = await SwitchConnection.ReadBytesMainAsync(OverworldOffset, 1, token).ConfigureAwait(false);
        return data[0] == 0x01;
    }

    public async Task<ulong> GetArrayStartOffset(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            return await SwitchConnection.PointerAll(Offsets.ArrayStartPointer, token);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<ulong> GetInvalidStartOffset(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            return await SwitchConnection.PointerAll(Offsets.InvalidStartPointer, token);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    /// <summary>
    /// Gets the count of stashed shinies by reading the array boundaries.
    /// </summary>
    public async Task<(int, ulong)> GetStashedShinyCount(CancellationToken token)
    {
        var structArrayStart = await GetArrayStartOffset(token);
        var invalidStartAddress = await GetInvalidStartOffset(token);

        if (invalidStartAddress <= structArrayStart)
            return (0, structArrayStart);

        return (Math.Min((int)((invalidStartAddress - structArrayStart) / STRUCT_SIZE), STASHED_SHINIES_MAX), structArrayStart);
    }
    /// <summary>
    /// Loads the stashed shinies from RAM, compares them to previous and saves
    /// </summary>
    /// <param name="bot"></param>
    /// <returns>whether or not a new one has entered since previous</returns>
    public async Task<List<PA9>> LoadStashedShinies(CancellationToken token)
    {
        List<(PA9, ulong)> AllData = [];
        // Get the actual count and start address of stashed shinies
        (int stashCount, ulong structArrayStart) = await GetStashedShinyCount(token);

        if (stashCount == 0)
        {
            return [];
        }

        for (int i = 0; i < stashCount; i++)
        {
            try
            {
                // Calculate offset to structure i in the array
                var structOffset = structArrayStart + (ulong)(i * STRUCT_SIZE);

                // Read the 0x28 structure: u64 hash, u64 address, 3x u64 unknown
                var structData = await ReadBytesAbsolute(structOffset, STRUCT_SIZE, token);

                var hash = BitConverter.ToUInt64(structData, 0);
                var address = BitConverter.ToUInt64(structData, 8);

                // Skip if address is 0 (despawned/invalid)
                if (address == 0)
                    continue;

                // Follow pointer chain: [[[address]+50]+30]+0]
                var pkmAddress = await FollowPointerChain(address, token, 0x50, 0x30).ConfigureAwait(false);
                if (!pkmAddress.HasValue)
                    continue;

                // Read the PKM data (0x148 bytes)
                var pkmData = await ReadBytesAbsolute(pkmAddress.Value, PA9_SIZE, token);
                var pk = new PA9(pkmData);

                if (pk.Species > 0 && pk.Species <= (ushort)Species.MAX_COUNT && pk.Valid && !AllData.Any(e => e.Item1.EncryptionConstant == pk.EncryptionConstant && e.Item2 == hash))               
                    AllData.Add((pk, hash));                                                        
            }
            catch (Exception ex)
            {
                Log($"Error reading shiny at index {i}: {ex.Message}");
                continue;
            }
        }

        return AllData.Select(x => x.Item1).ToList();
    }
    public async Task<List<PA9>> LoadStashedShiniesBlock(CancellationToken token)
    {
        List<(PA9, ulong)> AllData = [];
        var offs = await GetStashedShiniesOffset(token).ConfigureAwait(false);

        for (int i = 0; i < STASHED_SHINIES_MAX; i++)
        {
            var data = await ReadBytesAbsolute(offs + (ulong)(i * PA9_BUFFER), PA9_BLOCK_SIZE + 8, token).ConfigureAwait(false);
            var construct = typeof(PA9).GetConstructor(new Type[1] { typeof(Memory<byte>) });
            Debug.Assert(construct != null, "PKM type must have a Memory<byte> constructor");

            var pk = (PA9)construct.Invoke(new object[] { new Memory<byte>(data[8..]) });
            var location = BitConverter.ToUInt64(data, 0);
            if (pk.Species > 0 && pk.Species <= (ushort)Species.MAX_COUNT && pk.Valid && !AllData.Any(e => e.Item1.EncryptionConstant == pk.EncryptionConstant && e.Item2 == location))            
                AllData.Add((pk, location));            
        }
        return AllData.Select(x => x.Item1).ToList();
    }
    public async Task<ulong> GetStashedShiniesOffset(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            return await SwitchConnection.PointerAll(Offsets.KStoredShinyEntityPointer, token);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    public async Task SaveGame(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            Log("Saving the game");
            await FastClick(X, 1_000, token).ConfigureAwait(false);
            await FastClick(R, 0_500, token).ConfigureAwait(false);
            await FastClick(A, 3_500, token).ConfigureAwait(false);
            await FastClick(B, 0_400, token).ConfigureAwait(false);

            while (!await IsOnOverworldTitle(token).ConfigureAwait(false))
                await FastClick(B, 0_500, token).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    public async Task<Epoch1900DateTimeValue> GetLastSavedTime(bool init, CancellationToken token)
    {
        var bytes = await ReadEncryptedBlock(Offsets.KLastSavedPointer, KLastSaved, init, token).ConfigureAwait(false);
        return new Epoch1900DateTimeValue(bytes);
    }
    public async Task<byte[]> ReadBytesAbsolute(ulong offset, int length, CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            return await SwitchConnection.ReadBytesAbsoluteAsync(offset, length, token);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    /// <summary>
    /// Follows a chain of pointers starting from a base address.
    /// </summary>
    /// <returns>The final pointer address, or null if any pointer in the chain is 0</returns>
    private async Task<ulong?> FollowPointerChain(ulong startAddress, CancellationToken token, params ulong[] offsets)
    {
        ulong current = startAddress;
        foreach (var offset in offsets)
        {
            var data = await ReadBytesAbsolute(current + offset, 8, token);
            current = BitConverter.ToUInt64(data, 0);
            if (current == 0)
                return null;
        }
        return current;
    }
    private readonly Dictionary<uint, ulong> _cacheBlockAddresses = new();
    public async Task<byte[]> ReadEncryptedBlock(IEnumerable<long> pointer, uint blockKey, bool init, CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            var exists = _cacheBlockAddresses.TryGetValue(blockKey, out var cachedAddress);
            if (init || !exists)
            {
                var address = await SwitchConnection.PointerAll(pointer, token);
                address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
                cachedAddress = address;

                if (exists)
                {
                    _cacheBlockAddresses[blockKey] = cachedAddress;
                    Log($"Refreshed address for {blockKey:X8} found at {cachedAddress:X8}");
                }
                else
                {
                    _cacheBlockAddresses.Add(blockKey, cachedAddress);
                    Log($"Initial address for {blockKey:X8} found at {cachedAddress:X8}");
                }
            }

            return await ReadEncryptedBlock(cachedAddress, blockKey, token);
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    public async Task<bool> WriteEncryptedBlockArray(IEnumerable<long> pointer, uint blockKey, int blockSize, byte[] arrayToExpect, byte[] arrayToInject, CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            ulong address;
            try
            {
                address = await SwitchConnection.PointerAll(pointer, token);
                address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(address, 6 + blockSize, token).ConfigureAwait(false);
            data = DecryptBlock(blockKey, data);
            //Validate ram data
            var ram = data[6..];
            if (!ram.SequenceEqual(arrayToExpect)) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            Array.ConstrainedCopy(arrayToInject, 0, data, 6, blockSize);
            data = DecryptBlock(blockKey, data);
            await SwitchConnection.WriteBytesAbsoluteAsync(data, address, token).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    public async Task<bool> WriteEncryptedBlockObject(IEnumerable<long> pointer, uint blockKey, int blockSize, byte[] arrayToExpect, byte[] arrayToInject, CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            ulong address;
            try
            {
                address = await SwitchConnection.PointerAll(pointer, token);
                address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
            }
            catch (Exception) { return false; }
            //If we get there without exceptions, the block address is valid
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(address, 5 + blockSize, token).ConfigureAwait(false);
            data = DecryptBlock(blockKey, data);
            //Validate ram data
            var ram = data[5..];
            if (!ram.SequenceEqual(arrayToExpect)) return false;
            //If we get there then both block address and block data are valid, we can safely inject
            Array.ConstrainedCopy(arrayToInject, 0, data, 5, blockSize);
            data = DecryptBlock(blockKey, data);
            await SwitchConnection.WriteBytesAbsoluteAsync(data, address, token).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
    public async Task<byte[]> ReadEncryptedBlock(uint blockKey, bool init, CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            var exists = _cacheBlockAddresses.TryGetValue(blockKey, out var cachedAddress);
            if (init || !exists)
            {
                var address = await SearchSaveKey(blockKey, token).ConfigureAwait(false);
                address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + 8, 0x8, token).ConfigureAwait(false), 0);
                cachedAddress = address;

                if (exists)
                {
                    _cacheBlockAddresses[blockKey] = cachedAddress;
                    Log($"Refreshed address for {blockKey:X8} found at {cachedAddress:X8}");
                }
                else
                {
                    _cacheBlockAddresses.Add(blockKey, cachedAddress);
                    Log($"Initial address for {blockKey:X8} found at {cachedAddress:X8}");
                }
            }

            return await ReadEncryptedBlock(cachedAddress, blockKey, token);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<ulong> SearchSaveKey(uint key, CancellationToken token)
    {
        
        var ptr = await SwitchConnection.PointerAll(Offsets.SaveBlockPointer, token).ConfigureAwait(false);
        var dt = await SwitchConnection.ReadBytesAbsoluteAsync(ptr + 8, 16, token).ConfigureAwait(false);
        var start = ReadUInt64LittleEndian(dt[..8]);
        var keystart = start;
        var end = ReadUInt64LittleEndian(dt[8..]);
        ulong size = 32;

        while (start < end)
        {
            var count = (end - start) / size;
            var mid = start + ((count >> 1) * size);
            var found = ReadUInt32LittleEndian(await SwitchConnection.ReadBytesAbsoluteAsync(mid, 4, token).ConfigureAwait(false));
            if (found == key)
            {
                Log($"Pointer: SaveBlockPointer]+0x08]+0x{mid - keystart:X}");
                return mid;
            }

            if (found >= key)
                end = mid;
            else
                start = mid + size;
        }
        return 0;
    }
}
