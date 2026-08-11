namespace SysBot.Pokemon;

using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Base.SwitchButton;
using static Base.SwitchStick;
using static PokeDataOffsetsLZA;
using static SysBot.Pokemon.EncounterSettingsLZA;

public class EncounterBotOverworldScannerLZA(PokeBotState cfg, PokeTradeHub<PA9> hub) : EncounterBotLZA(cfg, hub)
{
    private bool _overworldKeyInitialized;
    private bool _shinyEntityKeyInitialized;
    private bool _lastSavedKeyInitialized;

    // Monitor intervals
    private const int WEATHER_CHECK_INTERVAL_MS = 60000;  // 1 minute
    private const int TIME_CHECK_INTERVAL_MS = 300000;    // 5 minutes
    private const int ERROR_RETRY_DELAY_MS = 1000;        // 1 Second

    private CancellationTokenSource? _weatherLockCts = null;
    private CancellationTokenSource? _timeLockCts = null;

    private ulong _speciesCount;
    private ulong _actionCount;

    private readonly List<PA9> _previous = [];
    protected override async Task EncounterLoop(SAV9ZA sav, CancellationToken token)
    {
        _speciesCount = _actionCount = 0;
        _overworldKeyInitialized = _shinyEntityKeyInitialized = _lastSavedKeyInitialized = false;
        _previous.Clear();

        await SetTime(Settings.Overworld.SetTime, token).ConfigureAwait(false);
        await SetWeather(Settings.Overworld.SetWeather, token).ConfigureAwait(false);

        while (!token.IsCancellationRequested)
        {
            var task = Settings.Overworld.Mode switch
            {
                OverworldModeLZA.BenchSit => BenchSit(token),
                OverworldModeLZA.WildZoneEntrance => WildZoneEntrance(token),
                OverworldModeLZA.Teleport => TeleportToRespawn(token),
                _ => throw new ArgumentOutOfRangeException()
            };
            await task.ConfigureAwait(false);

            await WalkInOverworld(token).ConfigureAwait(false);

            if (await PerformOverworldScan(token).ConfigureAwait(false))
                return;
        }
    }

    private async Task WalkInOverworld(CancellationToken token)
    {
        var walk = Settings.Overworld.WalkDurationMs;
        if (walk > 0)
        {
            Log($"Walking forward for {walk} milliseconds.", false);
            await Run(0, short.MaxValue, walk, token).ConfigureAwait(false);

            Log($"Walking back for {walk} milliseconds.", false);
            await Run(0, short.MinValue, walk, token).ConfigureAwait(false);
        }
    }

    private async Task Run(short x, short y, int walk, CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            const int defaultDelay = 0_100;

            await SetStick(LEFT, x, y, defaultDelay, token).ConfigureAwait(false);

            // Only press B if the configured walk duration is large enough to fit both stick movement and the B press
            const int clickDelay = defaultDelay * 2;
            if (walk > clickDelay)
                await FastClick(B, defaultDelay, token).ConfigureAwait(false);

            await Task.Delay(walk, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private Task<bool> PerformOverworldScan(CancellationToken token)
    {
        // Determine if slow mode is needed based on shiny search conditions
        // When searching for non-shiny or disabling shiny options, slow mode is required (because only a max. of 10 shinies are stored in a separate block)
        var useSlowMode = Hub.Config.StopConditions.SearchConditions.Any(sc => sc is { IsEnabled: true, ShinyTarget: TargetShinyType.DisableOption or TargetShinyType.NonShiny });

        if (useSlowMode)
        {
            Log("Using the slower, save and full scan, mode", false);
            return DoSlowOverworldScanning(token);
        }

        Log("Using the faster, shiny-only scan, mode", false);
        return DoShinyOverworldScanning(token);
    }

    private async Task<bool> DoSlowOverworldScanning(CancellationToken token)
    {
        //await Bench(token).ConfigureAwait(false);
        await AbsoluteSave(token).ConfigureAwait(false);
        Log("Scanning overworld...");

        await Click(HOME, 0, token).ConfigureAwait(false);
        var results = await GetAllOverworld(token);

        if (await HandleEncounters(results, token)) return true;

        await Click(HOME, 0_500, token).ConfigureAwait(false);
        Log($"Resuming, species found: {_speciesCount}");

        return false;
    }

    private async Task<bool> DoShinyOverworldScanning(CancellationToken token)
    {
        // Overworld spawn check disabled
        var overworld = Settings.Overworld;
        if (overworld.OverworldSpawnCheck == 0)
            return false;

        // Not the time to check yet
        if (_actionCount % (ulong)overworld.OverworldSpawnCheck != 0)
            return false;

        await AbsoluteSave(token).ConfigureAwait(false);

        Log("Scanning overworld...");

        await Click(HOME, 0, token).ConfigureAwait(false);
        var results = overworld.FastScan ? await LoadStashedShinies(token) : await LoadStashedShiniesBlock(token);

        if (await HandleEncounters(results, token)) return true;

        if (overworld.StopOnMaxShiniesStored && results.Count >= 10)
        {
            Log("Maximum number of shinies stored in overworld block reached, stopping bot.");
            return true;
        }

        await Click(HOME, 0_500, token).ConfigureAwait(false);
        Log($"Resuming, species found: {_speciesCount}");

        return false;
    }

    private async Task BenchSit(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            Log("Moving towards the bench", false);

            await SetStick(LEFT, 0, -30000, 1_000, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false);

            var later = DateTime.Now.AddSeconds(27);
            Log($"Repeatedly pressing 'A' until [{later}]", false);
            while (DateTime.Now <= later)
                await FastClick(A, 0_200, token);

            _actionCount++;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task WildZoneEntrance(CancellationToken token)
    {
        await _connectionLock.WaitAsync(token);
        try
        {
            Log("Moving towards the entrance", false);
            await SetStick(LEFT, 0, -30000, 1_000, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false);

            var later = DateTime.Now.AddSeconds(3);
            Log("Pass entrance", false);
            while (DateTime.Now <= later)
                await FastClick(A, 0_200, token);

            Log("Moving towards the entrance, again", false);
            await SetStick(LEFT, 0, -30000, 1_000, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false);

            later = DateTime.Now.AddSeconds(3);
            Log("Pass entrance, again", false);
            while (DateTime.Now <= later)
                await FastClick(A, 0_200, token);

            _actionCount++;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task TeleportToRespawn(CancellationToken token)
    {
        foreach (var pos in Settings.Overworld.TeleportCoords)
        {
            
            await SetPlayerPosition(pos.X, pos.Y, pos.Z, token);
            await Task.Delay(1_000, token).ConfigureAwait(false); // fall out and load species
                                                           // handle falling out
            int tries = 25;
            for (; tries > 0; --tries)
            {                
                // check for less than 0.02 difference to avoid float precision issues. We only care about Y here as X/Z may vary due to terrain
                var position = await GetPlayerPosition(token).ConfigureAwait(false);
                if (Math.Abs(position.Y - pos.Y) <= 0.02f)
                    break;
                await SetPlayerPosition(pos.X, pos.Y + (tries > 20 ? 1 : 0), pos.Z, token);
                await Task.Delay(1_200, token).ConfigureAwait(false);
            }

            if (tries == 0) // failed to load
            {
                UnlockTime();
                UnlockWeather();
                await Task.Delay(300_000, token).ConfigureAwait(false);
                string TitileID;
                await _connectionLock.WaitAsync(token);
                try
                {
                    SwitchConnection.Reset();
                    TitileID = await SwitchConnection.GetTitleID(token).ConfigureAwait(false); // keep connection alive
                }
                finally
                {
                    _connectionLock.Release();
                }
                if (TitileID != LegendsZAID)
                {
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    await StartGame(Hub.Config, token).ConfigureAwait(false);
                }
                else
                {
                    await AbsoluteSave(token).ConfigureAwait(false);
                    await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
                }
                _overworldKeyInitialized = _shinyEntityKeyInitialized = _lastSavedKeyInitialized = false;
                await SetTime(Settings.Overworld.SetTime, token).ConfigureAwait(false);
                await SetWeather(Settings.Overworld.SetWeather, token).ConfigureAwait(false);
                await TeleportToRespawn(token).ConfigureAwait(false);
                return;
            }

            await Task.Delay(Settings.Overworld.WaitInterval).ConfigureAwait(false);
        }

        _actionCount++;
    }
    private async Task AbsoluteSave(CancellationToken token)
    {
        var preLastSaved = await GetLastSavedTime(!_lastSavedKeyInitialized, token).ConfigureAwait(false);
        if(!_lastSavedKeyInitialized)
            _lastSavedKeyInitialized = true;
        await SaveGame(token).ConfigureAwait(false);
        var postLastSaved = await GetLastSavedTime(!_lastSavedKeyInitialized, token).ConfigureAwait(false);

        while (preLastSaved.TotalSeconds == postLastSaved.TotalSeconds)
        {
            await SaveGame(token).ConfigureAwait(false);
            postLastSaved = await GetLastSavedTime(!_lastSavedKeyInitialized, token).ConfigureAwait(false);
        }
    }
    private async Task<bool> HandleEncounters(List<PA9> results, CancellationToken token)
    {
        foreach (var current in results)
        {
            if (_previous.Any(p => p.Species == current.Species && p.EncryptionConstant == current.EncryptionConstant && p.PID == current.PID))
                continue;

            var (stop, success) = await HandleEncounter(current, token, minimize: true, skipDump: true).ConfigureAwait(false);
            _speciesCount++;

            if (success)
                Log("Your Pokémon has been found in the overworld!");

            if (stop)
                return true;
        }

        _previous.Clear();
        _previous.AddRange(results);

        return false;
    }

    private async Task<List<PA9>> GetAllOverworld(CancellationToken token)
    {
        var bytes = (await ReadEncryptedBlock(Offsets.KOverworldPointer, KOverworldKey, !_overworldKeyInitialized, token).ConfigureAwait(false)).AsSpan();

        // Only need to initialize once
        _overworldKeyInitialized = true;

        var list = new List<PA9>();

        // Really hacky way to scan for Pokémon in the overworld block
        // just slide over every possible offset and see if a valid PKM is found
        for (var i = 0; i < bytes.Length - FormatSlotSize; i++)
        {
            var entry = bytes.Slice(i, FormatSlotSize);

            if (!EntityDetection.IsPresent(entry)) continue;

            var pa9 = new PA9(entry.ToArray());
            if (!pa9.Valid || pa9.Species <= 0 || pa9.Checksum == 0 || !PersonalTable.ZA.IsSpeciesInGame(pa9.Species)) continue;

            list.Add(pa9);
        }

        return list;
    }

    private async Task<List<PA9>> GetShinyOverworld(CancellationToken token)
    {
        const int size = 0x1F0;

        var bytes = (await ReadEncryptedBlock(Offsets.KStoredShinyEntityBlockPointer, KStoredShinyEntityKey, !_shinyEntityKeyInitialized, token).ConfigureAwait(false)).AsSpan();

        // Only need to initialize once
        _shinyEntityKeyInitialized = true;

        var list = new List<PA9>();
        for (var i = 0; i < 10; i++)
        {
            var ofs = i * size + 8;
            var entry = bytes.Slice(ofs, FormatSlotSize);
            if (EntityDetection.IsPresent(entry))
            {
                var pa9 = new PA9(entry.ToArray());
                list.Add(pa9);
            }
            else
            {
                break;
            }
        }

        return list;
    }
    public void LockWeather(Weather weather)
    {
        // Dispose old token source if it exists
        _weatherLockCts?.Cancel();
        _weatherLockCts?.Dispose();

        _weatherLockCts = new CancellationTokenSource();

        // Start background task to monitor and maintain weather
        _ = Task.Run(() => MonitorWeather(_weatherLockCts.Token));
    }
    public void UnlockWeather()
    {
        _weatherLockCts?.Cancel();
        _weatherLockCts?.Dispose();
        _weatherLockCts = null;
    }
    public void LockTime(TimeOfDay time)
    {
        // Dispose old token source if it exists
        _timeLockCts?.Cancel();
        _timeLockCts?.Dispose();

        _timeLockCts = new CancellationTokenSource();

        // Start background task to monitor and maintain time
        _ = Task.Run(() => MonitorTime(_timeLockCts.Token));
    }

    public void UnlockTime()
    {
        _timeLockCts?.Cancel();
        _timeLockCts?.Dispose();
        _timeLockCts = null;
    }
    private async Task MonitorWeather(CancellationToken token)
    {
        while (!token.IsCancellationRequested && Settings.Overworld.SetWeather != Weather.None)
        {
            try
            {
                var currentWeatherBytes = await GetCurrentWeather(token).ConfigureAwait(false);
                var currentWeather = (Weather)BitConverter.ToUInt32(currentWeatherBytes, 0);

                if (currentWeather != Settings.Overworld.SetWeather && Settings.Overworld.SetWeather != Weather.None)
                {
                    var weatherBytes = BitConverter.GetBytes((uint)Settings.Overworld.SetWeather);
                    await SetCurrentWeather(weatherBytes, token).ConfigureAwait(false);
                    Log($"Weather changed from {currentWeather} to {Settings.Overworld.SetWeather}, correcting...");
                }

                await Task.Delay(WEATHER_CHECK_INTERVAL_MS, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Error monitoring weather: {ex.Message}");
                await Task.Delay(ERROR_RETRY_DELAY_MS, token);
            }
        }
    }

    private async Task MonitorTime(CancellationToken token)
    {
        var Mode = Settings.Overworld.Mode;
        while (!token.IsCancellationRequested && Settings.Overworld.SetTime != TimeOfDay.None && Mode != OverworldModeLZA.BenchSit)
        {
            try
            {
                var currentTimeBytes = await GetCurrentTime(token).ConfigureAwait(false);
                var currentTime = BitConverter.ToSingle(currentTimeBytes, 0);

                if (currentTime != (float)Settings.Overworld.SetTime && Settings.Overworld.SetTime != TimeOfDay.None)
                {
                    var timeBytes = BitConverter.GetBytes((float)Settings.Overworld.SetTime);
                    await SetCurrentTime(timeBytes, token).ConfigureAwait(false);
                    Log($"Time drifted from {(float)Settings.Overworld.SetTime} to {currentTime}, correcting...");
                }

                await Task.Delay(TIME_CHECK_INTERVAL_MS, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Error monitoring time: {ex.Message}");
                await Task.Delay(ERROR_RETRY_DELAY_MS, token);
            }
        }
    }
    public async Task SetWeather(Weather weather, CancellationToken token, bool forced = true)
    {
        try
        {
            if (weather == Weather.None && forced)
            {
                UnlockWeather();
                return;
            }

            if (weather != Weather.None)
            {
                var weatherBytes = BitConverter.GetBytes((uint)weather);
                await SetCurrentWeather(weatherBytes, token).ConfigureAwait(false);

                if (forced)
                    LockWeather(weather);
            }
        }
        catch (Exception ex)
        {
            Log($"Error setting weather: {ex.Message}");
        }
    }

    public async Task SetTime(TimeOfDay time, CancellationToken token, bool forced = true)
    {
        try
        {
            if ((time == TimeOfDay.None || Settings.Overworld.Mode == OverworldModeLZA.BenchSit) && forced)
            {
                UnlockTime();
                return;
            }

            if (time != TimeOfDay.None && Settings.Overworld.Mode != OverworldModeLZA.BenchSit)
            {
                var timeBytes = BitConverter.GetBytes((float)time);
                await SetCurrentTime(timeBytes, token).ConfigureAwait(false);

                if (forced)
                    LockTime(time);
            }
        }
        catch (Exception ex)
        {
            Log($"Error setting time: {ex.Message}");
        }
    }


}
