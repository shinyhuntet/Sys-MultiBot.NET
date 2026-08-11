namespace SysBot.Pokemon;

using Base;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Base.SwitchButton;
using static Base.SwitchStick;

public abstract class EncounterBotSV : PokeRoutineExecutor9SV, IEncounterBot
{
    protected readonly PokeTradeHub<PK9> Hub;
    protected readonly EncounterSettingsSV Settings;
    protected readonly IDumper DumpSetting;
    protected readonly WebHookHandler<PK9> WebHook;
    public ICountSettings Counts => Settings;
    protected PokeDetailForm PokeDetail;
    
    protected EncounterBotSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg)
    {
        Hub = hub;
        Settings = Hub.Config.EncounterSV;
        DumpSetting = Hub.Config.Folder;
        WebHook = new WebHookHandler<PK9>(Hub, Config.NextRoutineType);
        PokeDetail = new();
    }

    protected int EncounterCount;
    protected double Rate;

    public override async Task MainLoop(CancellationToken token)
    {
        if (CheckBotState(Config))
        {
            await Task.Run(() =>
            {
                if (SyncContextHolder.SyncContext == null)
                {
                    Log("SyncContext is null!");
                    return;
                }
                SyncContextHolder.SyncContext.Post(_ =>
                {
                    if (PokeDetail.IsDisposed)
                        PokeDetail = new();
                    if (PokeDetail.ControlBox)
                        PokeDetail.ControlBox = false;
                    if(!PokeDetail.Visible)
                        PokeDetail.Show();
                    if(PokeDetail.WindowState == FormWindowState.Minimized || PokeDetail.WindowState == FormWindowState.Maximized)
                        PokeDetail.WindowState = FormWindowState.Normal;
                }, null);
            });
            Log("show detail form");
            
        }
        var settings = Hub.Config.EncounterSV;
        if (!SwitchConnection.Connected)
            SwitchConnection.Reset();
        Log("Identifying trainer data of the host console.");
        var sav = await IdentifyTrainer(token).ConfigureAwait(false);
        await InitializeHardware(settings, token).ConfigureAwait(false);

        try
        {
            Log($"Starting main {GetType().Name} loop.");
            Config.IterateNextRoutine();

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);
            await EncounterLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            try
            {
                if (token.IsCancellationRequested)
                {
                    Log("Operation is still canceled!");
                    Log($"Ending {GetType().Name} loop.");
                    if (CheckBotState(Config) && !PokeDetail.ControlBox)
                        PokeDetail.ControlBox = true;
                    if (SwitchConnection.Connected)
                    {
                        await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
                        await HardStop().ConfigureAwait(false);
                    }
                    return;
                }
                Log($"Current Connection State: {SwitchConnection.Connected}{Environment.NewLine}Start Auto Reconneting...");
                if (SwitchConnection.Connected)
                    SwitchConnection.Disconnect();
                Log($"Updated Switch Connection State: {SwitchConnection.Connected}");
                await Task.Delay(300_000, token).ConfigureAwait(false);
                SwitchConnection.Reset();
                if (!SwitchConnection.Connected)
                    throw new Exception("SwitchConnection can't Reconnect!");
                await ResetStick(token).ConfigureAwait(false);
                await ReOpenGame(Hub.Config, token).ConfigureAwait(false);                
            }
            catch(SocketException socketerr)
            {
                Log(socketerr.Message);
                Log($"Ending {GetType().Name} loop.");
                if (CheckBotState(Config) && !PokeDetail.ControlBox)
                    PokeDetail.ControlBox = true;
                if (SwitchConnection.Connected)
                {
                    await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
                    await HardStop().ConfigureAwait(false);
                }
                return;
            }
            catch(Exception err)
            {
                Log(err.ToString());
                Log($"Ending {GetType().Name} loop.");
                if (CheckBotState(Config) && !PokeDetail.ControlBox)
                    PokeDetail.ControlBox = true;
                if (SwitchConnection.Connected)
                {
                    await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
                    await HardStop().ConfigureAwait(false);
                }
                return;
            }
            await MainLoop(token).ConfigureAwait(false);
            return;
        }

        Log($"Ending {GetType().Name} loop.");
        if (CheckBotState(Config) && !PokeDetail.ControlBox)        
            PokeDetail.ControlBox = true;
        if (SwitchConnection.Connected)
        {
            await Click(HOME, 1_000, CancellationToken.None).ConfigureAwait(false);
            await HardStop().ConfigureAwait(false);
        }
    }

    public override async Task HardStop()
    {
        await ResetStick(CancellationToken.None).ConfigureAwait(false);
        await CleanExit(CancellationToken.None).ConfigureAwait(false);
    }

    protected abstract Task EncounterLoop(SAV9SV sav, CancellationToken token);

    // Return true if breaking loop
    protected async Task<(bool Stop, bool Success)> HandleEncounter(PK9 pk, CancellationToken token, byte[]? raw = null, bool minimize = false, bool skipDump = false)
    {

        if(CheckBotState(Config))
            PokeDetail.ResetAssets();
        EncounterCount++;
        var print = Hub.Config.StopConditions.GetPrintName(pk);
        Rate = (1.00 - Math.Pow(1.00 - StopConditionSettings.CalcRate(Hub.Config.StopConditions), EncounterCount)) * 100.00;
        if (CheckBotState(Config))
            await PokeDetail.SetPokeDetail(pk, EncounterCount, Rate, token).ConfigureAwait(false);
        Log($"Encounter: {EncounterCount}, Target Rate: {Rate:0.0000}%");

        if (!string.IsNullOrWhiteSpace(print))
            Log($"{print}{Environment.NewLine}", !minimize);

        var folder = IncrementAndGetDumpFolder(pk);

        if (!skipDump && pk.Valid)
        {
            switch (DumpSetting)
            {
                case { Dump: true, DumpShinyOnly: true } when pk.IsShiny:
                case { Dump: true, DumpShinyOnly: false }:
                    DumpPokemon(DumpSetting.DumpFolder, folder, pk);
                    break;
            }

            if (raw != null)
            {
                switch (DumpSetting)
                {
                    case { DumpRaw: true, DumpShinyOnly: true } when pk.IsShiny:
                    case { DumpRaw: true, DumpShinyOnly: false }:
                        DumpPokemon(DumpSetting.DumpFolder, folder, pk, raw);
                        break;
                }
            }
        }

        if (!StopConditionSettings.EncounterFound(pk, Hub.Config.StopConditions))
        {
            if (folder.Equals("egg") && Hub.Config.StopConditions.SearchConditions.Exists(f => f.IsEnabled && f.ShinyTarget is TargetShinyType.AnyShiny or TargetShinyType.StarOnly or TargetShinyType.SquareOnly) && pk.IsShiny)
                Hub.LogEmbed(pk, false);

            return (false, false);
        }        
        
        var mode = Settings.ContinueAfterMatch;
        var msg = $"Result found!\n{print}\n" + mode switch
        {
            ContinueAfterMatch.Continue => "Continuing...",
            ContinueAfterMatch.PauseWaitAcknowledge => "Waiting for instructions to continue.",
            ContinueAfterMatch.StopExit => "Stopping routine execution; restart the bot to search again.",
            _ => throw new ArgumentOutOfRangeException("Match result type was invalid.", nameof(ContinueAfterMatch))
        };

        if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
            msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
        EchoUtil.Echo(msg);
        Hub.LogEmbed(pk, true);

        if (mode == ContinueAfterMatch.StopExit)
            return (true, true);
        if (mode == ContinueAfterMatch.Continue)
            return (false, true);

        _isWaiting = true;
        while (_isWaiting)
            await Task.Delay(1_000, token).ConfigureAwait(false);

        return (false, true);
    } 
    private bool CheckBotState(PokeBotState cfg) => cfg.NextRoutineType switch
    {
        PokeRoutineType.EggFetch => false,
        PokeRoutineType.Reset => true,
        PokeRoutineType.EncounterRuinous => true,
        PokeRoutineType.EncounterGimmighoul => true,
        PokeRoutineType.EncounterLoyal => true,
        PokeRoutineType.EncounterParadox => true,
        PokeRoutineType.EncounterSnack => true,
        PokeRoutineType.EncounterOverworld => false,
        
        _ => throw new ArgumentException(nameof(cfg.InitialRoutine)),
    };

    private string IncrementAndGetDumpFolder(PK9 pk)
    {
        try
        {
            var loggingFolder = string.IsNullOrWhiteSpace(Hub.Config.LoggingFolder)
                ? string.Empty
                : Hub.Config.LoggingFolder;

            var legendary = SpeciesCategory.IsLegendary(pk.Species) || SpeciesCategory.IsMythical(pk.Species) || SpeciesCategory.IsSubLegendary(pk.Species);
            if (legendary)
            {
                Settings.AddCompletedLegends();
                OutputExtensions<PK9>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_LegendSV.txt"));
                OutputExtensions<PK9>.EncounterScaleLogs(pk, Path.Combine(loggingFolder, "EncounterLogScale_LegendSV.txt"));
                return "legends";
            }

            if (pk.IsEgg)
            {
                Settings.AddCompletedEggs();
                OutputExtensions<PK9>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_EggSV.txt"));
                OutputExtensions<PK9>.EncounterScaleLogs(pk, Path.Combine(loggingFolder, "EncounterLogScale_EggSV.txt"));
                return "egg";
            }

            Settings.AddCompletedEncounters();
            OutputExtensions<PK9>.EncounterLogs(pk, Path.Combine(loggingFolder, "EncounterLogPretty_EncounterSV.txt"));
            OutputExtensions<PK9>.EncounterScaleLogs(pk, Path.Combine(loggingFolder, "EncounterLogScale_EncounterSV.txt"));
            return "encounters";
        }
        catch (Exception e)
        {
            Log($"Couldn't update encounters:\n{e.Message}\n{e.StackTrace}");
            return "random";
        }
    }

    private bool _isWaiting;
    public void Acknowledge() => _isWaiting = false;

    protected async Task ResetStick(CancellationToken token)
    {
        // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
        await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
    }

    protected async Task EnableAlwaysCatch(CancellationToken token)
    {
        if (!Hub.Config.EncounterSV.EnableCatchCheat)
            return;

        Log("Enable critical capture cheat", false);
        // Source: https://gbatemp.net/threads/pokemon-scarlet-violet-cheat-database.621563/

        // Original cheat:
        /*
         * [100% Fast capture on(v3.0.0)]
         * 04000000 0197E428 52800028
         * 04000000 0197E434 14000020
         * 04000000 0197E48C 52800028
         * 04000000 0197E4C4 52800028
         */

        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x0197E428, token);
        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x14000020), 0x0197E434, token);
        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x0197E48C, token);
        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(0x52800028), 0x0197E4C4, token);
    }
    protected async Task CatchRoutine(bool LeftFlag, int BallIndex, CancellationToken token)
    {
        Log($"Catch using {Settings.TargetBall} Ball, Shift Left?: {LeftFlag}, Ball Index: {BallIndex}");
        await Click(X, 1_000, token);
        for (int i = 0; i < BallIndex; i++)
            await Click(LeftFlag ? DLEFT : DRIGHT, 0_500, token).ConfigureAwait(false);
        await Click(A, 7_500, token);
    }
}
