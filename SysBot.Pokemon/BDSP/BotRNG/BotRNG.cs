using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using SysBot.Base;
using System.Linq;
using static SysBot.Pokemon.BasePokeDataOffsetsBS;



namespace SysBot.Pokemon
{
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class BDSPBotRNG : PokeRoutineExecutor8BS
    {
        private readonly PokeTradeHub<PB8> Hub;
        private readonly RNGSettings Settings;
        private readonly RNG8b Calc;
        private readonly List<string> locations;
        private readonly WebHookHandler<PB8> WebHook;



        /// <summary>
        /// Folder to dump received trade data to.
        /// </summary>
        /// <remarks>If null, will skip dumping.</remarks>
        private readonly IDumper DumpSetting;

        /// <summary>
        /// Synchronized start for multiple bots.
        /// </summary>
        public bool ShouldWaitAtBarrier { get; private set; }

        /// <summary>
        /// Tracks failed synchronized starts to attempt to re-sync.
        /// </summary>
        public int FailedBarrier { get; private set; }

        public BDSPBotRNG(PokeBotState cfg, PokeTradeHub<PB8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = hub.Config.BDSP_RNG;
            DumpSetting = hub.Config.Folder;
            WebHook = new WebHookHandler<PB8>(Hub, Config.CurrentRoutineType);
            Calc = new RNG8b();
            string res_data = Properties.Resource.text_bdsp_00000_en;
            res_data = res_data.Replace("\r", String.Empty);
            locations = res_data.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        // Cached offsets that stay the same per session.
        private ulong RNGOffset;
        private ulong PlayerLocation;
        private ulong DayTime;
        private GameTime GameTime;
        private bool HasShinyCharm;
        private bool HasOvalCharm;

        public override async Task MainLoop(CancellationToken token)
        {
            try
            {
                await InitializeHardware(Hub.Config.BDSP_RNG, token).ConfigureAwait(false);

                Log("Identifying trainer data of the host console.");
                var sav = await IdentifyTrainer(token).ConfigureAwait(false);

                await InitializeSessionOffsets(token).ConfigureAwait(false);
                await HasCharms(token).ConfigureAwait(false);

                Log($"Starting main {nameof(BDSPBotRNG)} loop.");
                Config.IterateNextRoutine();
                SettingCheck();
                var task = Hub.Config.BDSP_RNG.Routine switch
                {
                    RNGRoutine.AutoRNG => AutoRNG(sav, token),
                    RNGRoutine.Generator => Generator(sav, token, Hub.Config.BDSP_RNG.GeneratorSettings.GeneratorVerbose, Hub.Config.BDSP_RNG.GeneratorSettings.GeneratorMaxResults),
                    RNGRoutine.DelayCalc => CalculateDelay(sav, token),
                    RNGRoutine.LogAdvances => TrackAdvances(sav, token),
                    RNGRoutine.CheckAvailablePKM => CheckAvailablePKM(sav, token),
                    _ => TrackAdvances(sav, token),
                };
                await task.ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(BDSPBotRNG)} loop.");
            await CleanExit(token).ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            await ResetStick(CancellationToken.None).ConfigureAwait(false);
            await CleanExit(CancellationToken.None).ConfigureAwait(false);
        }

        private async Task CheckAvailablePKM(SAV8BS sav, CancellationToken token)
        {
            var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
            var time = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
            GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;

            var mode = Hub.Config.BDSP_RNG.WildMode == WildMode.None ? WildMode.Grass : Hub.Config.BDSP_RNG.WildMode;

            var slots = GetEncounterSlots(version, route, time, mode);
            var unownForms = GetLocation(route).Contains("Solaceon Ruins") ? GetUnownForms(route) : null;

            Log($"({version}) {GetLocation(route)} ({route}) [{time}]");
            Log($"Available mons for {mode} encounters:");
            if (slots.Count > 0)
            {
                var i = 0;
                foreach (var slot in slots)
                {
                    if (unownForms is null || unownForms.Length == 0)
                        Log($"[{i}] {(Species)slot}");
                    else
                    {
                        var formstr = " ";
                        foreach (var form in unownForms!)
                            formstr = $"{formstr}{form} ";
                        Log($"[{i}] {(Species)slot}-[{formstr}]");
                    }
                    i++;
                }
            }
            return;
        }

        private async Task AutoRNG(SAV8BS sav, CancellationToken token)
        {
            bool found;
            if (Hub.Config.BDSP_RNG.AutoRNGSettings.AutoRNGMode is AutoRNGMode.AutoCalc)
            {
                if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootIfFailed)
                {
                    GameTime = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
                    while (!await AutoCalc(sav, token).ConfigureAwait(false))
                    {
                        var target = int.MaxValue;
                        while ((Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0 && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue))
                        {
                            await RestartGameBDSP(false, token).ConfigureAwait(false);
                            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
                            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
                            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
                            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
                            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
                            Log("Calculating target...");
                            if (Hub.Config.BDSP_RNG.Mod != 0)
                                Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil = 10000;
                            target = await CalculateTarget(xoro, sav, Hub.Config.BDSP_RNG.RNGType, Hub.Config.BDSP_RNG.WildMode, token).ConfigureAwait(false);
                            string msg = $"\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}";
                            if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0 && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue)
                                msg = $"{msg}\nTarget above the limit settings. Rebooting.";
                            else
                            {
                                msg = $"{msg}\nTarget in: {target}";
                                await Task.Delay(Hub.Config.Timings.ExtraTimeLoadGame).ConfigureAwait(false);
                                for (int i = 0; i < 4; i++)
                                {
                                    await Click(SwitchButton.A, 0_800, token).ConfigureAwait(false);
                                }
                            }
                            Log(msg);
                        }
                        if (!await ResumeStart(Hub.Config, token).ConfigureAwait(false))
                                await InitializeSessionOffsets(token).ConfigureAwait(false);
                        
                    }
                    found = true;
                }
                else
                    found = await AutoCalc(sav, token).ConfigureAwait(false);
            }
            else
                found = await TrackAdvances(sav, token, true).ConfigureAwait(false);

            if (found)
            {
                if (Hub.Config.StopConditions.CaptureVideoClip)
                {
                    await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
                    await PressAndHold(SwitchButton.CAPTURE, 2_000, 0, token).ConfigureAwait(false);
                }
                if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                    Log($"{Hub.Config.StopConditions.MatchFoundEchoMention} result found.");
            }
            return;
        }

        private async Task<bool> TrackAdvancesWild(SAV8BS sav, CancellationToken token, bool auto = false, Xorshift? ex_xoro = null)
        {
            int counter = 0;
            var in_bag = false;
Restart:
            var oldtarget = 0;
            var target = 0;
            var dex_time = new Stopwatch();
            int checkcount = 0;
            var can_act = true;
            uint mod = Hub.Config.BDSP_RNG.Mod;
            var correcttimeline = mod == 0 ? true : false;
            Nature syncnature = Hub.Config.BDSP_RNG.AutoRNGSettings.SyncNature;
            var print = true;
            var mode = Hub.Config.BDSP_RNG.CheckMode;
            var type = Hub.Config.BDSP_RNG.RNGType;
            var wild = Hub.Config.BDSP_RNG.WildMode;
            var advances = 0;
            var actions = ParseActions(Hub.Config.BDSP_RNG.AutoRNGSettings.Actions);
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = ex_xoro is null ? new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3) : ex_xoro;
            var in_dex = false;

            while (!token.IsCancellationRequested)
            {
                tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                var ramS0 = BitConverter.ToUInt32(tmpRamState, 0);
                var ramS1 = BitConverter.ToUInt32(tmpRamState, 4);
                var ramS2 = BitConverter.ToUInt32(tmpRamState, 8);
                var ramS3 = BitConverter.ToUInt32(tmpRamState, 12);

                while (ramS0 != tmpS0 || ramS1 != tmpS1 || ramS2 != tmpS2 || ramS3 != tmpS3)
                {
                    xoro.Next();
                    tmpS0 = xoro.GetU32State()[0];
                    tmpS1 = xoro.GetU32State()[1];
                    tmpS2 = xoro.GetU32State()[2];
                    tmpS3 = xoro.GetU32State()[3];
                    advances++;

                    if (ramS0 == tmpS0 && ramS1 == tmpS1 && ramS2 == tmpS2 && ramS3 == tmpS3)
                    {
                        if (auto)
                        {
                            if (actions.Count <= 0)
                            {
                                Log("\nYou must input at least One Action to trigger the encounter in the Hub settings.\n");
                                return true;
                            }

                            Hub.Config.BDSP_RNG.AutoRNGSettings.Target = 0;

                            oldtarget = target;
                            target = await CalculateTarget(xoro, sav, type, wild, token).ConfigureAwait(false) - Hub.Config.BDSP_RNG.AutoRNGSettings.Delay;
                            if(oldtarget < target)
                            {
                                if(in_bag)
                                    await CloseBag(token).ConfigureAwait(false);
                                if(in_dex)
                                    await CloseDex(token).ConfigureAwait(false);
                            }

                            if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootIfFailed && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue && Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0)
                            {
                                Log($"Target above the limit settings. Rebooting...");
                                return false;
                            }

                            if (print)
                            {

                                Log($"Target in {target} advances.");
                                print =false;

                            }
                            
                            if (0 >= target)
                            {
                                if (in_dex)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await CloseDex(token).ConfigureAwait(false);
                                }

                                if (actions.Last() is not SwitchButton.HOME && target == 0)
                                {
                                    System.Diagnostics.Stopwatch stopwatch = new();
                                    stopwatch.Start();
                                    await Click(actions.Last(), 0_100, token).ConfigureAwait(false);
                                    Log($"\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\nStarting encounter...");
                                    var offset = GetDestOffset(mode, type);
                                    PB8? pk;
                                    do
                                    {                                        
                                       pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);                                        
                                    } while (pk is null && stopwatch.ElapsedMilliseconds < 5_000);
                                    if (pk is null)
                                        return false;

                                    Log($"\n\nSpecies: {(Species)pk.Species}{GetString(pk)}");
                                    var success = await HandleTarget(pk, true, type, token, true).ConfigureAwait(false);
                                    if (!success)
                                    {
                                        var can_runaway = await Runaway(token).ConfigureAwait(false);
                                        Log("If target is missed, calculate a proper delay with DelayCalc mode and retry.");
                                        var mismatch = await CalculateMismatch(new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3), sav, type, wild, pk.EncryptionConstant, token).ConfigureAwait(false);
                                        if (mismatch is not null)
                                        {
                                            await WebHook.SendNotification(pk, $"Calculated delay mismatch is {mismatch}.", token).ConfigureAwait(false);
                                            Log($"Calculated delay mismatch is {mismatch}.");
                                        }
                                        if (can_runaway)
                                        {
                                            if (in_bag)                                            
                                              in_bag =false;
                                            
                                        }
                                        else
                                        {
                                            while(!can_runaway)
                                                await Runaway(token).ConfigureAwait(false);
                                            if (in_bag)
                                                in_bag =false;
                                        }
                                    }
                                    goto Restart;
                                }
                                if (actions.Last() is not SwitchButton.HOME && 0 > target)
                                {
                                    Log("If target is missed, calculate a proper delay with DelayCalc mode and retry.");
                                    if(in_bag)
                                    {
                                        await CloseBag(token).ConfigureAwait(false);
                                    }
                                    goto Restart;
                                }

                                if (actions.Last() is SwitchButton.HOME)
                                {
                                    Log("Game paused.");
                                    return true;
                                }
                            }
                            else if (Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil > 0 && target > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil + 50)
                            {
                                if (!in_dex && target > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil + 1100)
                                {
                                    await Task.Delay(2_000, token).ConfigureAwait(false);
                                    await OpenDex(Hub.Config.Timings.KeypressTime, token).ConfigureAwait(false);
                                    dex_time.Restart();
                                    in_dex = true;
                                }

                                if (in_dex && target - 400 > 7000)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await SetStick(SwitchStick.LEFT, 30_000, 0, 2_000, token).ConfigureAwait(false);
                                    if (dex_time.ElapsedMilliseconds > 185_000 && in_dex)
                                    {
                                        await ResetStick(token).ConfigureAwait(false);
                                        await ReOpenDex(Hub.Config.Timings.KeypressTime, token).ConfigureAwait(false);
                                        dex_time.Restart();
                                    }
                                }
                                else if (in_dex && target - 400 > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await SetStick(SwitchStick.LEFT, 0, 30_000, 1_000, token).ConfigureAwait(false);
                                    if (dex_time.ElapsedMilliseconds > 185_000 && in_dex)
                                    {
                                        await ResetStick(token).ConfigureAwait(false);
                                        await ReOpenDex(Hub.Config.Timings.KeypressTime, token).ConfigureAwait(false);
                                        dex_time.Restart();
                                    }
                                }
                                else if (in_dex)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await Click(SwitchButton.DUP, 0_500, token).ConfigureAwait(false);
                                }
                            }
                            else if (in_dex)
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await CloseDex(token).ConfigureAwait(false);
                                in_dex = false;
                                if (can_act && actions.Count > 1)
                                {
                                    await Task.Delay(0_700).ConfigureAwait(false);

                                    if (mod == 0)
                                    {
                                        Log("Perfoming Actions");
                                        in_bag = await OpenBag(token).ConfigureAwait(false);
                                        if(in_bag)
                                        {
                                            counter = await Wildloop(counter, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                        }
                                        can_act = false;
                                    }
                                    else if (target % mod == 0)
                                    {
                                        Log("Perfoming Actions");
                                        in_bag = await OpenBag(token).ConfigureAwait(false);
                                        if (in_bag)
                                        {
                                            counter = await Wildloop(counter, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                        }
                                        can_act = false;
                                    }

                                }
                            }
                            else if (can_act && actions.Count > 1)
                            {
                                if (mod == 0)
                                {
                                    Log("Perfoming Actions");
                                    in_bag = await OpenBag(token).ConfigureAwait(false);
                                    if (in_bag)
                                    {
                                        counter = await Wildloop(counter, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                    }
                                    can_act = false;
                                }
                                else if (target % mod == 0)
                                {
                                    Log("Perfoming Actions");
                                    in_bag = await OpenBag(token).ConfigureAwait(false);
                                    if (in_bag)
                                    {
                                        counter = await Wildloop(counter, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                    }
                                    can_act = false;
                                }

                            }
                            else
                            {
                                if (mod != 0 && Hub.Config.BDSP_RNG.CheckCount > 0)
                                {
                                    if (checkcount < Hub.Config.BDSP_RNG.CheckCount)
                                    {
                                        if (target % mod == 0)
                                        {
                                            correcttimeline = true;
                                            checkcount++;
                                            Log($"Target in {target} Advances.");
                                        }
                                        else
                                        {
                                            checkcount++;
                                            Log($"Target in {target} Advances.");
                                        }

                                    }
                                    else if (checkcount >= Hub.Config.BDSP_RNG.CheckCount && !correcttimeline)
                                    {
                                                                               
                                         Log("timeline is not match. Researching...");                                       
                                        if(in_bag)
                                        {
                                            await CloseBag(token).ConfigureAwait(false);
                                        }
                                        goto Restart;
                                    }
                                    else
                                        Log($"Target in {target} Advances.");

                                }
                                else
                                    Log($"Target in {target} Advances.");

                            }

                        }
                        else
                            Log($"\nAdvance {advances}\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\n");
                    }
                }

            }
            return false;
        }
        private async Task<bool> TrackAdvances(SAV8BS sav, CancellationToken token, bool auto = false, int aux_target = 0, Xorshift? ex_xoro = null)
		{
            var timeline_count = 0;
            var timeline_advances = 0;
            var checkcount = 0;
            var target = 0;
            var to_hit = 0;
            var steps = 1;
            bool initialtry = true;
            var dex_time = new Stopwatch();
            var EggStepOffset = await SwitchConnection.PointerAll(Offsets.EggStepPointer, token).ConfigureAwait(false);
            var EggFlagOffset = await SwitchConnection.PointerAll(Offsets.EggFlagPointer, token).ConfigureAwait(false);
            var can_act = true;
            var print = Hub.Config.BDSP_RNG.AutoRNGSettings.LogAdvances;
            var mode = Hub.Config.BDSP_RNG.CheckMode;
            var type = Hub.Config.BDSP_RNG.RNGType;
            var wild = Hub.Config.BDSP_RNG.WildMode;
            var advances = 0;
            var actions = ParseActions(Hub.Config.BDSP_RNG.AutoRNGSettings.Actions);
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = ex_xoro is null ? new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3) : ex_xoro;
            var in_dex = false;
            bool recalc = false;
            bool readeggstep = true;
            ushort step_tmp = 0;
            int step_until_generation = (0xB4 - step_tmp) == 0 ? 180 : (0xB4 - step_tmp);
            if (auto && aux_target == 0)
			{
                if (actions.Count <= 0)
                {
                    Log("\nYou must input at least One Action to trigger the encounter in the Hub settings.\n");
                    return true;
                }
                Hub.Config.BDSP_RNG.AutoRNGSettings.Target = 0;
                if(type is RNGType.Egg)                
                Log($"\n\nCurrent states:\n[S1] {tmpS0:X8}{tmpS1:X8}\n[S2] {tmpS2:X8}{tmpS3:X8}\nCalculate a target and set it in the Hub Settings. The routine will continue automatically once detected a target.");
                else
                Log($"\n\nCurrent states:\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\nCalculate a target and set it in the Hub Settings. The routine will continue automatically once detected a target.");
                while (Hub.Config.BDSP_RNG.AutoRNGSettings.Target <= 0)
                    await Task.Delay(1_000, token).ConfigureAwait(false);
                Log("CONTINUING...");
			}
            while (!token.IsCancellationRequested)
            {
                tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                var ramS0 = BitConverter.ToUInt32(tmpRamState, 0);
                var ramS1 = BitConverter.ToUInt32(tmpRamState, 4);
                var ramS2 = BitConverter.ToUInt32(tmpRamState, 8);
                var ramS3 = BitConverter.ToUInt32(tmpRamState, 12);

                while (ramS0 != tmpS0 || ramS1 != tmpS1 || ramS2 != tmpS2 || ramS3 != tmpS3)
                {
                    xoro.Next();
                    tmpS0 = xoro.GetU32State()[0];
                    tmpS1 = xoro.GetU32State()[1];
                    tmpS2 = xoro.GetU32State()[2];
                    tmpS3 = xoro.GetU32State()[3];
                    advances++;

                    if (ramS0 == tmpS0 && ramS1 == tmpS1 && ramS2 == tmpS2 && ramS3 == tmpS3)
                    {
                        if (auto)
                        {
                           if(to_hit <= 50000 && !recalc && target > 500000)
                            {
                                Log("Recalc target");
                                if (in_dex)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await CloseDex(token).ConfigureAwait(false);
                                }
                                var found = await AutoCalc(sav, token).ConfigureAwait(false);
                                return found;
                            }
                            target = aux_target > 0 ? aux_target : Hub.Config.BDSP_RNG.AutoRNGSettings.Target;
                            to_hit = target - advances;
                            if (actions.Last() is SwitchButton.HOME && Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil > 0 && to_hit <= Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil)
                            {
                                if (in_dex)
                                {
                                    await CloseDex(token).ConfigureAwait(false);
                                }
                                await Click(SwitchButton.L, 0_100, token).ConfigureAwait(false);
                                await Click(actions.Last(), 0_100, token).ConfigureAwait(false);
                                Log("Game paused.");
                                return true;
                            }
                            if (type is RNGType.Egg)
                            {
                                if (readeggstep)
                                {
                                    step_tmp = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(EggStepOffset, 2, token).ConfigureAwait(false), 0);

                                    step_until_generation = (0xB4 - step_tmp) == 0 ? 180 : (0xB4 - step_tmp);
                                    steps = step_until_generation;
                                    if (step_until_generation != 1)
                                    {
                                        var egg_step = BitConverter.GetBytes((ushort)179);
                                        await SwitchConnection.WriteBytesAbsoluteAsync(egg_step, EggStepOffset, token).ConfigureAwait(false);
                                    }
                                    if (step_until_generation == 1)
                                        readeggstep = false;
                                }
                                if (steps != step_until_generation || readeggstep)
                                    Log($"Steps until possible egg generation: {step_until_generation}\n");
                            }
                            else
                            {
                                if (print)
                                {

                                    Log($"Target in {to_hit} advances.");

                                }
                            }
                            if (target != 0 && to_hit <= 0)
                                {
                                    if (in_dex)
                                    {
                                        await ResetStick(token).ConfigureAwait(false);
                                        await CloseDex(token).ConfigureAwait(false);
                                    }

                                    if (actions.Last() is not SwitchButton.HOME && to_hit == 0)
                                    {
                                        Stopwatch stopwatch = new();
                                        stopwatch.Start();
                                        await Click(actions.Last(), 0_100, token).ConfigureAwait(false);
                                        Log($"\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\nStarting encounter...");
                                        var offset = GetDestOffset(mode, type);
                                        PB8? pk = null;
                                        uint seed = 0;
                                    do
                                    {
                                        if (mode is CheckMode.Seed)
                                        {
                                            var species = (ushort)Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies;
                                            pk = new PB8
                                            {
                                                TrainerTID7 = sav.TrainerTID7,
                                                TrainerSID7 = sav.TrainerSID7,
                                                OriginalTrainerName = sav.OT,
                                            };
                                            if (species == 0)
                                            {
                                                if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.Genderless)
                                                    pk.Species = (int)Species.Azelf;
                                                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.MaleOnly)
                                                    pk.Species = (int)Species.Volbeat;
                                                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.FemaleOnly)
                                                    pk.Species = (int)Species.Illumise;
                                                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F1)
                                                    pk.Species = (int)Species.Absol;
                                                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F3)
                                                    pk.Species = (int)Species.Jigglypuff;
                                                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M3F1)
                                                    pk.Species = (int)Species.Growlithe;
                                                else
                                                    pk.Species = (int)Species.Piplup;
                                            }
                                            else
                                                pk.Species = species;
                                            if (type is RNGType.Roamer)
                                            {
                                                pk.Species = (species == (ushort)Species.Mesprit) ? species : (ushort)Species.Cresselia;
                                                seed = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(await SwitchConnection.PointerAll(offset, token).ConfigureAwait(false), 4, token).ConfigureAwait(false), 0);
                                                pk = Calc.CalculateFromSeed(pk, Shiny.Random, type, seed);
                                            }
                                            else if (type is RNGType.Egg)
                                            {
                                                await Task.Delay(5000).ConfigureAwait(false);
                                                var egg_flag = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(EggFlagOffset, 2, token).ConfigureAwait(false), 0);
                                                seed = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(await SwitchConnection.PointerAll(offset, token).ConfigureAwait(false), 4, token).ConfigureAwait(false), 0);
                                                if (seed > 0 && egg_flag == 1)
                                                {
                                                    Log($"Egg generated. Egg seed is {seed:X8}.");
                                                    var parent1 = new PB8(await SwitchConnection.ReadBytesAbsoluteAsync(await SwitchConnection.PointerAll(Offsets.DayCareParent1PokemonPointer, token).ConfigureAwait(false), 0x158, token).ConfigureAwait(false));
                                                    var parent2 = new PB8(await SwitchConnection.ReadBytesAbsoluteAsync(await SwitchConnection.PointerAll(Offsets.DayCareParent2PokemonPointer, token).ConfigureAwait(false), 0x158, token).ConfigureAwait(false));
                                                    pk = Calc.EggGenerator(pk, (ulong)seed, HasShinyCharm, parent1, parent2);
                                                    
                                                }
                                                else
                                                {
                                                    Log("Egg not generated, target frame probably missed.");
                                                    return true;
                                                }

                                            }
                                        }
                                        else
                                        {
                                            seed = 1;
                                            pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                                        }
                                        if (type is RNGType.Gift or RNGType.Gift_3IV)
                                            await Click(SwitchButton.B, 0_050, token).ConfigureAwait(false);

                                    } while ((pk is null || seed ==0) && stopwatch.ElapsedMilliseconds < 10_000);
                                    if (pk is null)
                                    {
                                        if (!Hub.Config.BDSP_RNG.AutoRNGSettings.RebootIfFailed && Hub.Config.BDSP_RNG.Mod != 0 && can_act && timeline_count < Settings.CheckCount)
                                        {
                                            Log("Recalc Target.");
                                            Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil = 5000;
                                            return await AutoCalc(sav, token).ConfigureAwait(false);

                                        }
                                        else
                                            return false;
                                    }

                                        Log($"\n\nSpecies: {(Species)pk.Species}{GetString(pk)}");
                                        var success = await HandleTarget(pk, true, type, token, true).ConfigureAwait(false);
                                    if (!success)
                                    {
                                            Log("If target is missed, calculate a proper delay with DelayCalc mode and retry.");
                                            var mismatch = await CalculateMismatch(new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3), sav, type, wild, pk.EncryptionConstant, token).ConfigureAwait(false);
                                        if (mismatch is not null)
                                        {
                                            await WebHook.SendNotification(pk, $"Calculated delay mismatch is {mismatch}.", token).ConfigureAwait(false);
                                            Log($"Calculated delay mismatch is {mismatch}.");
                                        }
                                        if (Hub.Config.BDSP_RNG.RNGType == RNGType.Shamin)
                                        {
                                            await Runaway(token).ConfigureAwait(false);
                                            await Click(SwitchButton.A, 1_000, token).ConfigureAwait(false);
                                            await PressAndHold(SwitchButton.DDOWN, 15_000, 0, token).ConfigureAwait(false);
                                            await PressAndHold(SwitchButton.DUP, 20_000, 0, token).ConfigureAwait(false);
                                            Log("Recalc Target.");
                                            Hub.Config.BDSP_RNG.AutoRNGSettings.Actions = "A,A";
                                            Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil = 10000;
                                            return await AutoCalc(sav, token).ConfigureAwait(false);

                                        }
                                        else if (Hub.Config.BDSP_RNG.Mod != 0 && can_act)
                                        {
                                            await Click(SwitchButton.B, 1_000, token).ConfigureAwait(false);
                                            Log("Recalc Target.");
                                            Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil = 5000;
                                            return await AutoCalc(sav, token).ConfigureAwait(false);

                                        }
                                    }
                                        return success;
                                    }
                                    if (actions.Last() is not SwitchButton.HOME && 0 > to_hit)
                                    {
                                        Log("Target frame missed.");
                                    if (!Hub.Config.BDSP_RNG.AutoRNGSettings.RebootIfFailed && Hub.Config.BDSP_RNG.RNGType == RNGType.Shamin)
                                    {
                                        Log("Recalc Target.");
                                        var old_target = await CalculateTarget(xoro, sav, type, wild, token).ConfigureAwait(false) - Hub.Config.BDSP_RNG.AutoRNGSettings.Delay;
                                        Log($"old_target is {old_target}");
                                        while (old_target <= 0)
                                        {
                                            tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                                            ramS0 = BitConverter.ToUInt32(tmpRamState, 0);
                                            ramS1 = BitConverter.ToUInt32(tmpRamState, 4);
                                            ramS2 = BitConverter.ToUInt32(tmpRamState, 8);
                                            ramS3 = BitConverter.ToUInt32(tmpRamState, 12);

                                            while (ramS0 != tmpS0 || ramS1 != tmpS1 || ramS2 != tmpS2 || ramS3 != tmpS3)
                                            {
                                                xoro.Next();
                                                tmpS0 = xoro.GetU32State()[0];
                                                tmpS1 = xoro.GetU32State()[1];
                                                tmpS2 = xoro.GetU32State()[2];
                                                tmpS3 = xoro.GetU32State()[3];

                                                if (ramS0 == tmpS0 && ramS1 == tmpS1 && ramS2 == tmpS2 && ramS3 == tmpS3)
                                                {
                                                    Log($"old_target is {old_target}");
                                                    old_target = await CalculateTarget(xoro, sav, type, wild, token).ConfigureAwait(false) - Hub.Config.BDSP_RNG.AutoRNGSettings.Delay;
                                                }
                                            }
                                        }
                                        if (old_target >= 50000 || old_target < 2000)
                                        {
                                            await Click(SwitchButton.A, 1_000, token).ConfigureAwait(false);
                                            await Runaway(token).ConfigureAwait(false);
                                            await Click(SwitchButton.A, 1_000, token).ConfigureAwait(false);
                                            await PressAndHold(SwitchButton.DDOWN, 15_000, 0, token).ConfigureAwait(false);
                                            await PressAndHold(SwitchButton.DUP, 20_000, 0, token).ConfigureAwait(false);
                                            Log("Recalc Target.");
                                            Hub.Config.BDSP_RNG.AutoRNGSettings.Actions = "A,A";
                                            Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil = 10000;
                                            return await AutoCalc(sav, token).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            Hub.Config.BDSP_RNG.AutoRNGSettings.Actions ="A";
                                            Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil = 0;
                                            return await AutoCalc(sav, token).ConfigureAwait(false);
                                        }
                                    }
                                    else if (Hub.Config.BDSP_RNG.Mod != 0 && can_act)
                                    {
                                        await Click(SwitchButton.B, 1_000, token).ConfigureAwait(false);
                                        Log("Recalc Target.");
                                        Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil = 5000;
                                        return await AutoCalc(sav, token).ConfigureAwait(false);
                                    }
                                    else
                                        return false;
                                    }

                                    if (actions.Last() is SwitchButton.HOME)
                                    {
                                        await Click(SwitchButton.L, 0_100, token).ConfigureAwait(false);
                                        await Click(actions.Last(), 0_100, token).ConfigureAwait(false);
                                        Log("Game paused.");
                                        return true;
                                    }
                                }
                            else if (Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil > 0 && target - advances > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil + 50)
                            {
                                if (!in_dex && target - advances > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil + 1100)
                                {
                                    await Task.Delay(2_000, token).ConfigureAwait(false);
                                    await OpenDex(Hub.Config.Timings.KeypressTime, token).ConfigureAwait(false);
                                    dex_time.Restart();
                                    in_dex = true;
                                }

                                if (in_dex && target - (advances + 400) > 7000)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await SetStick(SwitchStick.LEFT, 30_000, 0, 2_000, token).ConfigureAwait(false);
                                    if (dex_time.ElapsedMilliseconds > 185_000 && in_dex)
                                    {
                                        await ResetStick(token).ConfigureAwait(false);
                                        await ReOpenDex(Hub.Config.Timings.KeypressTime, token).ConfigureAwait(false);
                                        dex_time.Restart();

                                    }
                                }
                                else if (in_dex && target - (advances + 400) > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await SetStick(SwitchStick.LEFT, 0, 30_000, 1_000, token).ConfigureAwait(false);
                                    if (dex_time.ElapsedMilliseconds > 185_000 && in_dex)
                                    {
                                        await ResetStick(token).ConfigureAwait(false);
                                        await ReOpenDex(Hub.Config.Timings.KeypressTime, token).ConfigureAwait(false);
                                        dex_time.Restart();

                                    }
                                }
                                else if(in_dex)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await Click(SwitchButton.DUP, 0_500, token).ConfigureAwait(false);
                                }
                            }
                            else if (in_dex)
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await CloseDex(token).ConfigureAwait(false);
                                in_dex = false;
                                if (can_act && actions.Count > 1)
                                {
                                    await Task.Delay(0_700).ConfigureAwait(false);

                                    if (Hub.Config.BDSP_RNG.Mod == 0)
                                    {
                                        Log("Perfoming Actions");
                                        await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                        can_act = false;
                                    }
                                    else
                                    {
                                        await Click(SwitchButton.PLUS, 0_800, token).ConfigureAwait(false);
                                        await Click(SwitchButton.B, 0_800, token).ConfigureAwait(false);
                                        Log($"Target in {target-advances} Advances. Frame diff is {advances - timeline_advances}.");
                                        if ((advances - timeline_advances) % Hub.Config.BDSP_RNG.Mod == 0 && timeline_count < Settings.CheckCount)
                                            timeline_count += 1;
                                        else if(timeline_count < Settings.CheckCount)
                                            timeline_count = 0;
                                        checkcount++;
                                        if (timeline_count >= Settings.CheckCount - 1 && target - advances <= Settings.AutoRNGSettings.DoActionUntil + 50)
                                        {
                                            if (initialtry)
                                            {
                                                Log("Click B!");
                                                await Click(SwitchButton.B, 0_500, token).ConfigureAwait(false);
                                                initialtry = false;
                                            }
                                            if ((target - advances) % Hub.Config.BDSP_RNG.Mod == 0)
                                            {
                                                Log("Perfoming Actions");
                                                await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                                can_act = false;
                                            }

                                        }
                                    }

                                }
                            }
                            else if (can_act && actions.Count > 1)
                            {
                                if (Hub.Config.BDSP_RNG.Mod == 0)
                                {
                                    Log("Perfoming Actions");
                                    await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                    can_act = false;
                                }
                                else
                                {
                                    Log($"Target in {target-advances} Advances. Frame diff is {advances - timeline_advances}.");
                                    if (checkcount == 0)
                                    {
                                        await Click(SwitchButton.PLUS, 0_800, token).ConfigureAwait(false);
                                        await Click(SwitchButton.B, 0_800, token).ConfigureAwait(false);
                                    }
                                    if ((advances - timeline_advances) % Hub.Config.BDSP_RNG.Mod == 0 && timeline_count < Settings.CheckCount)
                                        timeline_count += 1;
                                    else if (timeline_count < Settings.CheckCount)
                                        timeline_count = 0;
                                    checkcount++;
                                    if (timeline_count >= Settings.CheckCount && target - advances <= Settings.AutoRNGSettings.DoActionUntil + 50)
                                    {
                                        if (initialtry)
                                        {
                                            Log("Click B!");
                                            await Click(SwitchButton.B, 0_500, token).ConfigureAwait(false);
                                            initialtry = false;
                                        }
                                        if ((target - advances) % Hub.Config.BDSP_RNG.Mod == 0)
                                        {
                                            Log("Perfoming Actions");
                                            await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                            can_act = false;
                                        }

                                    }
                                }
                            }
                            else
                                Log($"Target in {target-advances} Advances.");
                        }
                        else
                            Log($"\nAdvance {advances}\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\n");
                        timeline_advances = advances;
                    }
                }
                
            }
             return false;
        }
        private async Task<bool> TrackAdvances_For_Mew_or_Jirachi(SAV8BS sav, CancellationToken token, bool auto = false, int aux_target = 0, Xorshift? ex_xoro = null)
        {
            var timeline_count = 0;
            var timeline_advances = 0;
            var target = 0;
            var to_hit = 0;
            var print = Hub.Config.BDSP_RNG.AutoRNGSettings.LogAdvances;
            var mode = Hub.Config.BDSP_RNG.CheckMode;
            var type = Hub.Config.BDSP_RNG.RNGType;
            var wild = Hub.Config.BDSP_RNG.WildMode;
            var advances = 0;
            var actions = ParseActions(Hub.Config.BDSP_RNG.AutoRNGSettings.Actions);
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = ex_xoro is null ? new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3) : ex_xoro;
            bool recalc = false;
            if (auto && aux_target == 0)
            {
                if (actions.Count <= 0)
                {
                    Log("\nYou must input at least One Action to trigger the encounter in the Hub settings.\n");
                    return true;
                }
                Hub.Config.BDSP_RNG.AutoRNGSettings.Target = 0;
                Log($"\n\nCurrent states:\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\nCalculate a target and set it in the Hub Settings. The routine will continue automatically once detected a target.");
                while (Hub.Config.BDSP_RNG.AutoRNGSettings.Target <= 0)
                    await Task.Delay(1_000, token).ConfigureAwait(false);
                Log("CONTINUING...");
            }
            while (!token.IsCancellationRequested)
            {
                tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                var ramS0 = BitConverter.ToUInt32(tmpRamState, 0);
                var ramS1 = BitConverter.ToUInt32(tmpRamState, 4);
                var ramS2 = BitConverter.ToUInt32(tmpRamState, 8);
                var ramS3 = BitConverter.ToUInt32(tmpRamState, 12);

                while (ramS0 != tmpS0 || ramS1 != tmpS1 || ramS2 != tmpS2 || ramS3 != tmpS3)
                {
                    xoro.Next();
                    tmpS0 = xoro.GetU32State()[0];
                    tmpS1 = xoro.GetU32State()[1];
                    tmpS2 = xoro.GetU32State()[2];
                    tmpS3 = xoro.GetU32State()[3];
                    advances++;

                    if (ramS0 == tmpS0 && ramS1 == tmpS1 && ramS2 == tmpS2 && ramS3 == tmpS3)
                    {
                        if (auto)
                        {
                            if (to_hit <= 50000 && !recalc && target > 500000)
                            {
                                Log("Recalc target");
                                return await AutoCalc_For_Mew_and_Jirachi(sav, token).ConfigureAwait(false);
                            }
                            target = aux_target > 0 ? aux_target : Hub.Config.BDSP_RNG.AutoRNGSettings.Target;
                            to_hit = target - advances;
                            if (print)
                                Log($"Target in {to_hit} advances.");

                            if (target != 0 && to_hit <= 0)
                            {
                                if (actions.Last() is not SwitchButton.HOME && to_hit == 0)
                                {
                                    Stopwatch stopwatch = new();
                                    stopwatch.Start();
                                    await Click(actions.Last(), 0_100, token).ConfigureAwait(false);
                                    Log($"\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\nStarting encounter...");
                                    var offset = GetDestOffset(mode, type);
                                    PB8? pk = null;
                                    do
                                    {
                                        pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                                        
                                    } while (pk is null && stopwatch.ElapsedMilliseconds < 10_000);
                                    if (pk is null)
                                        return false;

                                    Log($"\n\nSpecies: {(Species)pk.Species}{GetString(pk)}");
                                    var success = await HandleTarget(pk, true, type, token, true).ConfigureAwait(false);
                                    if (!success)
                                    {
                                        Log("If target is missed, calculate a proper delay with DelayCalc mode and retry.");
                                        var mismatch = await CalculateMismatch(new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3), sav, type, wild, pk.EncryptionConstant, token).ConfigureAwait(false);
                                        if (mismatch is not null)
                                        {
                                            await WebHook.SendNotification(pk, $"Calculated delay mismatch is {mismatch}.", token).ConfigureAwait(false);
                                            Log($"Calculated delay mismatch is {mismatch}.");
                                        }
                                    }
                                    return success;
                                }
                                if (actions.Last() is not SwitchButton.HOME && 0 > to_hit)
                                {
                                    Log("Target frame missed.");
                                    Log("Recalc Target.");
                                    return await AutoCalc_For_Mew_and_Jirachi(sav, token).ConfigureAwait(false);
                                }

                                if (actions.Last() is SwitchButton.HOME)
                                {
                                    await Click(SwitchButton.L, 0_100, token).ConfigureAwait(false);
                                    await Click(actions.Last(), 0_100, token).ConfigureAwait(false);
                                    Log("Game paused.");
                                    return true;
                                }
                            }
                            else
                            {
                                Log($"Target in {target-advances} Advances. Frame diff is {advances - timeline_advances}.");
                                if ((advances - timeline_advances) % Hub.Config.BDSP_RNG.Mod == 0 && timeline_count < Settings.CheckCount)
                                    timeline_count += 1;
                                else if (timeline_count < Settings.CheckCount)
                                    timeline_count = 0;
                                if (timeline_count >= Settings.CheckCount)
                                    Log("Timelinig is succes!");
                            }
                        }
                        else
                            Log($"\nAdvance {advances}\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\n");
                        timeline_advances = advances;
                    }
                }

            }
            return false;
        }

        private async Task<bool> AutoCalc(SAV8BS sav, CancellationToken token)
        {
            var advances = 0;
            var actions = ParseActions(Hub.Config.BDSP_RNG.AutoRNGSettings.Actions);
            var type = Hub.Config.BDSP_RNG.RNGType;
            var mode = Hub.Config.BDSP_RNG.WildMode;
            var checkmode = Hub.Config.BDSP_RNG.CheckMode;
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);

            if (actions.Count <= 0)
            {
                Log("\nYou must input at least One Action to trigger the encounter in the Hub settings.\n");
                return true;
            }

            if (Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies < 0)
            {
                Log("\nPlease set a valid Species in the Stop Conditions settings.");
                if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.Genderless)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Azelf;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.MaleOnly)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Volbeat;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.FemaleOnly)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Illumise;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F1)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Absol;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F3)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Jigglypuff;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M3F1)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Growlithe;
                else
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Piplup;
            }
            if (type is RNGType.Mew_or_Jirachi)
            {
                if (Hub.Config.BDSP_RNG.Mod == 0 || Hub.Config.BDSP_RNG.CheckCount == 0)
                {
                    Hub.Config.BDSP_RNG.Mod = 41;
                    Hub.Config.BDSP_RNG.CheckCount = 50;
                }
                Log("Performing Actions...");
                await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
                tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
                tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
                tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
                xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
            }
            var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
            GameTime = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
            GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
            Log($"[{version}] - Route: {GetLocation(route)} ({route}) [{GameTime}]");

            Log($"Initial States: \n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}.");

recalc:
            while (!token.IsCancellationRequested)
            {
                tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                var ramS0 = BitConverter.ToUInt32(tmpRamState, 0);
                var ramS1 = BitConverter.ToUInt32(tmpRamState, 4);
                var ramS2 = BitConverter.ToUInt32(tmpRamState, 8);
                var ramS3 = BitConverter.ToUInt32(tmpRamState, 12);

                while (ramS0 != tmpS0 || ramS1 != tmpS1 || ramS2 != tmpS2 || ramS3 != tmpS3)
                {
                    xoro.Next();
                    tmpS0 = xoro.GetU32State()[0];
                    tmpS1 = xoro.GetU32State()[1];
                    tmpS2 = xoro.GetU32State()[2];
                    tmpS3 = xoro.GetU32State()[3];
                    advances++;

                    if (ramS0 == tmpS0 && ramS1 == tmpS1 && ramS2 == tmpS2 && ramS3 == tmpS3)
                    {
                        Log("Calculating target...");
                        var target = await CalculateTarget(xoro, sav, type, mode, token).ConfigureAwait(false) - Hub.Config.BDSP_RNG.AutoRNGSettings.Delay;
                        if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootIfFailed && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue && Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0)
                        {                           
                            Log($"Target above the limit settings. Rebooting...");
                            return false;
                        }
                        if (target <= 0)
                        {
                            await Task.Delay(2_000).ConfigureAwait(false);
                            goto recalc;
                        }
                        Log($"Target in {target} Advances.");

                        if (type is not RNGType.Wild)
                            return await TrackAdvances(sav, token, true, target, xoro).ConfigureAwait(false);
                        else if (type is RNGType.Mew_or_Jirachi)
                            return await TrackAdvances_For_Mew_or_Jirachi(sav, token, true, target, xoro).ConfigureAwait(false);
                        else
                            return await TrackAdvancesWild(sav, token, true, xoro).ConfigureAwait(false);
                    }
                }
            }
            return false;
        }

        private async Task<bool> AutoCalc_For_Mew_and_Jirachi(SAV8BS sav, CancellationToken token)
        {
            var advances = 0;
            var actions = ParseActions(Hub.Config.BDSP_RNG.AutoRNGSettings.Actions);
            var type = Hub.Config.BDSP_RNG.RNGType;
            var mode = Hub.Config.BDSP_RNG.WildMode;
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);

            if (actions.Count <= 0)
            {
                Log("\nYou must input at least One Action to trigger the encounter in the Hub settings.\n");
                return true;
            }

            if (Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies < 0)
            {
                Log("\nPlease set a valid Species in the Stop Conditions settings.");
                if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.Genderless)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Mew;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.MaleOnly)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Volbeat;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.FemaleOnly)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Illumise;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F1)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Absol;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F3)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Jigglypuff;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M3F1)
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Growlithe;
                else
                    Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies = Species.Piplup;
            }
            var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
            GameTime = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
            GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
            Log($"[{version}] - Route: {GetLocation(route)} ({route}) [{GameTime}]");

            Log($"Initial States: \n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}.");

recalc:
            while (!token.IsCancellationRequested)
            {
                tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                var ramS0 = BitConverter.ToUInt32(tmpRamState, 0);
                var ramS1 = BitConverter.ToUInt32(tmpRamState, 4);
                var ramS2 = BitConverter.ToUInt32(tmpRamState, 8);
                var ramS3 = BitConverter.ToUInt32(tmpRamState, 12);

                while (ramS0 != tmpS0 || ramS1 != tmpS1 || ramS2 != tmpS2 || ramS3 != tmpS3)
                {
                    xoro.Next();
                    tmpS0 = xoro.GetU32State()[0];
                    tmpS1 = xoro.GetU32State()[1];
                    tmpS2 = xoro.GetU32State()[2];
                    tmpS3 = xoro.GetU32State()[3];
                    advances++;

                    if (ramS0 == tmpS0 && ramS1 == tmpS1 && ramS2 == tmpS2 && ramS3 == tmpS3)
                    {
                        Log("Calculating target...");
                        var target = await CalculateTarget_For_Mew_and_Jirachi(xoro, sav, type, mode).ConfigureAwait(false) - Hub.Config.BDSP_RNG.AutoRNGSettings.Delay;
                        if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootIfFailed && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue && Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0)
                        {
                            Log($"Target above the limit settings. Rebooting...");
                            return false;
                        }
                        if (target <= 0)
                        {
                            await Task.Delay(2_000).ConfigureAwait(false);
                            goto recalc;
                        }
                        Log($"Target in {target} Advances.");

                        return await TrackAdvances_For_Mew_or_Jirachi(sav, token, true, target, xoro).ConfigureAwait(false);
                    }
                }
            }
            return false;
        }



        async Task<int?> CalculateMismatch(Xorshift xoro, SAV8BS sav, RNGType type, WildMode mode, uint hit_ec, CancellationToken token)
		{
            ulong seed = 0;
            var EggParent1offset = await SwitchConnection.PointerAll(Offsets.DayCareParent1PokemonPointer, token).ConfigureAwait(false);
            var EggParent2offset = await SwitchConnection.PointerAll(Offsets.DayCareParent2PokemonPointer, token).ConfigureAwait(false);
            var parent1 = new PB8(await SwitchConnection.ReadBytesAbsoluteAsync(EggParent1offset, 0x158, token).ConfigureAwait(false));
            var parent2 = new PB8(await SwitchConnection.ReadBytesAbsoluteAsync(EggParent2offset, 0x158, token).ConfigureAwait(false));
            Nature syncnature = Hub.Config.BDSP_RNG.AutoRNGSettings.SyncNature;
            var delay = Hub.Config.BDSP_RNG.AutoRNGSettings.Delay * 2;
            var range = delay > 500 ? delay : 500;
            var states = xoro.GetU32State();
            var species = Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies;
            var events = Hub.Config.BDSP_RNG.Event;
            var rng = new Xorshift(states[0], states[1], states[2], states[3]);
            var rngegg = new Xorshift(states[0], states[1], states[2], states[3]);
            RNGList rnglist = new RNGList(rngegg);
            var mod = Settings.Mod;
            Settings.Mod = 0;
            var target = await CalculateTarget(xoro, sav, type, mode, token).ConfigureAwait(false);
            int[]? unownForms = null;
            var advances = 0;
            List<int>? slots = null;

            if (mode is not WildMode.None)
            {
                var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
                GameTime = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
                GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
                //Log($"Route: {GetLocation(route)} ({route}) [{time}]");
                slots = GetEncounterSlots(version, route, GameTime, mode);
                if (GetLocation(route).Contains("Solaceon Ruins"))
                    unownForms = GetUnownForms(route);
            }

            var pk = new PB8
            {
                TrainerTID7 = sav.TrainerTID7,
                TrainerSID7 = sav.TrainerSID7,
                OriginalTrainerName = sav.OT,
                Species = (species != 0) ? (ushort)species : (ushort)Species.Azelf,
            };

            do
            {
                //Log($"{advances}");
                bool foundflag = false;
                if (type is RNGType.Roamer)
                    pk = Calc.CalculateFromSeed(pk, Shiny.Random, type, rng.Next());
                else if (type is RNGType.Egg)
                {
                    int compatability = (int)Hub.Config.BDSP_RNG.AutoRNGSettings.Compatability;
                    if(HasOvalCharm)
                        compatability = compatability == 20 ? 40 : compatability == 50 ? 80 : 88;
                    if ((rnglist.getValue() % 100) < compatability)
                    {
                        seed = (ulong)rnglist.getValue();
                        pk = Calc.EggGenerator(pk, seed, HasShinyCharm, parent1, parent2);
                        foundflag = true;
                    }
                }
                else
                {
                    states = rng.GetU32State();
                    pk = Calc.CalculateFromStates(pk, (type is not RNGType.MysteryGift) ? Shiny.Random : Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]), syncnature, mode, slots, events, unownForms);
                    rng.Next();
                }
                advances++;
                if(type is RNGType.Egg)
                    rnglist.advanceState();
                if (type is RNGType.Egg && !foundflag)
                    continue;
            } while (pk.EncryptionConstant != hit_ec && advances <= range);

            Settings.Mod = mod;
            if (advances >= range || target >= range)
                return null;
            return (advances - target);
        }

        async Task<int> CalculateTarget(Xorshift xoro, SAV8BS sav, RNGType type, WildMode mode, CancellationToken token)
		{
            ulong seed;
            var check_time = new Stopwatch();
            var EggParent1offset = await SwitchConnection.PointerAll(Offsets.DayCareParent1PokemonPointer, token).ConfigureAwait(false);
            var EggParent2offset = await SwitchConnection.PointerAll(Offsets.DayCareParent2PokemonPointer, token).ConfigureAwait(false);
            var parent1 = new PB8(await SwitchConnection.ReadBytesAbsoluteAsync(EggParent1offset, 0x158, token).ConfigureAwait(false));
            var parent2 = new PB8(await SwitchConnection.ReadBytesAbsoluteAsync(EggParent2offset, 0x158, token).ConfigureAwait(false));
            Nature syncnature = Hub.Config.BDSP_RNG.AutoRNGSettings.SyncNature;
            int advances = 0;
            uint[] states = xoro.GetU32State();
            Xorshift rng = new(states[0], states[1], states[2], states[3]);
            Xorshift rngegg = new(states[0], states[1], states[2], states[3]);
            RNGList rnglist = new RNGList(rngegg);
            List<int>? slots = null;
            int[]? unownForms = null;
            ushort species = (ushort)Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies;
            var events = Hub.Config.BDSP_RNG.Event;
            bool success = false;

            if (mode is not WildMode.None)
            {
                var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
                var tmp = (await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
                if (tmp >= 0 && tmp <= 4)
                    GameTime = (GameTime)tmp;
                GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
                //Log($"Route: {GetLocation(route)} ({route}) [{time}]");
                slots = GetEncounterSlots(version, route, GameTime, mode);
                if (GetLocation(route).Contains("Solaceon Ruins"))
                    unownForms = GetUnownForms(route);
            }

            var pk = new PB8
            {
                TrainerTID7 = sav.TrainerTID7,
                TrainerSID7 = sav.TrainerSID7,
                OriginalTrainerName = sav.OT,
            };
            if (species == 0)
            {
                if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.Genderless)
                    pk.Species = (int)Species.Azelf;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.MaleOnly)
                    pk.Species = (int)Species.Volbeat;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.FemaleOnly)
                    pk.Species = (int)Species.Illumise;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F1)
                    pk.Species = (int)Species.Absol;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F3)
                    pk.Species = (int)Species.Jigglypuff;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M3F1)
                    pk.Species = (int)Species.Growlithe;
                else
                    pk.Species = (int)Species.Piplup;
            }
            else
                pk.Species = species;
restart:
            do
            {
start:
//Log($"{advances}");
                bool foundfalg = false;
                if (type is RNGType.Roamer)
                    pk = Calc.CalculateFromSeed(pk, Shiny.Random, type, rng.Next());
                else if (type is RNGType.Egg)
                {
                    int compatability = (int)Hub.Config.BDSP_RNG.AutoRNGSettings.Compatability;
                    if (HasOvalCharm)
                        compatability = compatability == 20 ? 40 : compatability == 50 ? 80 : 88;
                    if ((rnglist.getValue() % 100) < compatability)
                    {
                        seed = (ulong)rnglist.getValue();
                        pk = Calc.EggGenerator(pk, seed, HasShinyCharm, parent1, parent2);
                        foundfalg = true;
                    }
                }
                else
                {
                    states = rng.GetU32State();
                    pk = Calc.CalculateFromStates(pk, (type is not RNGType.MysteryGift) ? Shiny.Random : Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]), syncnature, mode, slots, events, unownForms);
                    rng.Next();
                }
                advances++;
                if (type is RNGType.Egg)
                    rnglist.advanceState();
                if (type is RNGType.Egg && !foundfalg)
                    goto start;
                success = await HandleTarget(pk, false, type, token, false).ConfigureAwait(false);
            } while (!success && (advances - Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue < Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue && Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0));
            if (type is RNGType.Mew_or_Jirachi && Settings.Mod != 0 && ((advances - Settings.AutoRNGSettings.Delay) % Settings.Mod != 0) || advances - Settings.AutoRNGSettings.Delay < 2000000)
                goto restart;
            if (type is not RNGType.Mew_or_Jirachi && Settings.Mod != 0 && advances - Settings.AutoRNGSettings.Delay < Settings.AutoRNGSettings.ScrollDexUntil + 500)
                goto restart;
            if (success)
                Log($"\n\nTarget Species: { (Species)pk.Species}{ GetString(pk)}");
            return advances;
        }

        private async Task<int> CalculateTarget_For_Mew_and_Jirachi(Xorshift xoro, SAV8BS sav, RNGType type, WildMode mode)
        {
            var check_time = new Stopwatch();
            Nature syncnature = Hub.Config.BDSP_RNG.AutoRNGSettings.SyncNature;
            int advances = 0;
            uint[] states = xoro.GetU32State();
            Xorshift rng = new(states[0], states[1], states[2], states[3]);
            List<int>? slots = null;
            ushort species = (ushort)Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies;
            var events = Hub.Config.BDSP_RNG.Event;
            bool success = false;

            var pk = new PB8
            {
                TrainerTID7 = sav.TrainerTID7,
                TrainerSID7 = sav.TrainerSID7,
                OriginalTrainerName = sav.OT,
            };
            if (species == 0)
            {
                if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.Genderless)
                    pk.Species = (int)Species.Mew;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.MaleOnly)
                    pk.Species = (int)Species.Volbeat;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.FemaleOnly)
                    pk.Species = (int)Species.Illumise;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F1)
                    pk.Species = (int)Species.Absol;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F3)
                    pk.Species = (int)Species.Jigglypuff;
                else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M3F1)
                    pk.Species = (int)Species.Growlithe;
                else
                    pk.Species = (int)Species.Piplup;
            }
            else
                pk.Species = species;
restart:
            do
            {
//Log($"{advances}");
                states = rng.GetU32State();
                pk = Calc.CalculateFromStates(pk, Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]), syncnature, mode, slots, events);
                rng.Next();
                advances++;
                success = await HandleTarget(pk, false, type, CancellationToken.None, false).ConfigureAwait(false);
            } while (!success && (advances - Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue < Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue && Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0));
            if ((advances - Settings.AutoRNGSettings.Delay) % Settings.Mod != 0)
                goto restart;
            if (success)
                Log($"\n\nTarget Species: {(Species)pk.Species}{GetString(pk)}");
            return advances;
        }

        private async Task CalculateDelay(SAV8BS sav, CancellationToken token)
		{
            var EggFlagOffset = await SwitchConnection.PointerAll(Offsets.EggFlagPointer, token).ConfigureAwait(false);
            var action = Hub.Config.BDSP_RNG.DelayCalcSettings.Action;
            var dest = Hub.Config.BDSP_RNG.CheckMode;
            var type = Hub.Config.BDSP_RNG.RNGType;
            var advances = 0;
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
            if (type is RNGType.Egg)
            {
                var egg_flag = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(EggFlagOffset, 2, token).ConfigureAwait(false), 0);
                if (egg_flag == 0)
                    return;
            }
            var parent1 = new PB8(await SwitchConnection.ReadBytesAbsoluteAsync(await SwitchConnection.PointerAll(Offsets.DayCareParent1PokemonPointer, token).ConfigureAwait(false), 0x158, token).ConfigureAwait(false));
            var parent2 = new PB8(await SwitchConnection.ReadBytesAbsoluteAsync(await SwitchConnection.PointerAll(Offsets.DayCareParent2PokemonPointer, token).ConfigureAwait(false), 0x158, token).ConfigureAwait(false));
            var calculatedlist = await Generator(sav, token, false, 500, xoro).ConfigureAwait(false);
            int used;

            //Log($"Initial State:\n[S0]: {tmpS0:X8}, [S1]: {tmpS1:X8}\n[S2]: {tmpS2:X8}, [S3]: {tmpS3:X8}\n");

            while (!token.IsCancellationRequested)
			{
                tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                var ramS0 = BitConverter.ToUInt32(tmpRamState, 0);
                var ramS1 = BitConverter.ToUInt32(tmpRamState, 4);
                var ramS2 = BitConverter.ToUInt32(tmpRamState, 8);
                var ramS3 = BitConverter.ToUInt32(tmpRamState, 12);

                while (ramS0 != tmpS0 || ramS1 != tmpS1 || ramS2 != tmpS2 || ramS3 != tmpS3)
                {
                    xoro.Next();
                    tmpS0 = xoro.GetU32State()[0];
                    tmpS1 = xoro.GetU32State()[1];
                    tmpS2 = xoro.GetU32State()[2];
                    tmpS3 = xoro.GetU32State()[3];
                    advances++;

                    if (ramS0 == tmpS0 && ramS1 == tmpS1 && ramS2 == tmpS2 && ramS3 == tmpS3)
                    {
                        await Click(action, 0_100, token).ConfigureAwait(false);
                        used = advances;
                        PB8? pk;
                        uint seed = 0;
                        //Log($"Waiting for pokemon...");
                        var offset = GetDestOffset(dest, type);
                        do
                        {
                            if (dest is CheckMode.Seed)
                            {
                                var species = (ushort)Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies;
                                pk = new PB8
                                {
                                    TrainerTID7 = sav.TrainerTID7,
                                    TrainerSID7 = sav.TrainerSID7,
                                    OriginalTrainerName = sav.OT,
                                    Species = (species != 0) ? species : (ushort)Species.Cresselia,
                                };
                                if (type is RNGType.Roamer)
                                {
                                    seed = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(await SwitchConnection.PointerAll(offset, token).ConfigureAwait(false), 4, token).ConfigureAwait(false), 0);
                                    pk = Calc.CalculateFromSeed(pk, Shiny.Random, RNGType.Roamer, seed);
                                }
                                else if (type is RNGType.Egg)
                                {
                                    seed = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(await SwitchConnection.PointerAll(offset, token).ConfigureAwait(false), 4, token).ConfigureAwait(false), 0);
                                    pk = Calc.EggGenerator(pk, (ulong)seed, HasShinyCharm, parent1, parent2);
                                }
                            }
                            else
                            {
                                seed = 1;
                                pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                            }
                            if (type is RNGType.Gift or RNGType.Gift_3IV)
                                await Click(SwitchButton.B, 0_050, token).ConfigureAwait(false);
                        } while (pk is null || seed == 0);

                        var hit = pk.EncryptionConstant;

                        Log($"\nFinal State:\n[S0]: {tmpS0:X8}, [S1]: {tmpS1:X8}\n[S2]: {tmpS2:X8}, [S3]: {tmpS3:X8}\n\nSpecies: {(Species)pk.Species}{GetString(pk)}");

                        var result = 0;
                        var i = 0;

                        foreach (var item in calculatedlist)
                        {
                            i++;
                            if (item.EncryptionConstant == hit)
                            {
                                result = i;
                                break;
                            }
                        }
                        Log($"Result: {result}, used: {used}, difference: {result - used}");
                        var delay = (result-used) != -1 ? (result-used) : 0;
                        Log($"\nCalculated delay is {delay}.\n");

                        return;
                    }
				} 
            }
        }

        private async Task<List<PB8>> Generator(SAV8BS sav, CancellationToken token, bool verbose, int maxadvances, Xorshift? xoro = null)
		{
            ulong seed;
            var EggParent1offset = await SwitchConnection.PointerAll(Offsets.DayCareParent1PokemonPointer, token).ConfigureAwait(false);
            var EggParent2offset = await SwitchConnection.PointerAll(Offsets.DayCareParent2PokemonPointer, token).ConfigureAwait(false);
            var parent1 = new PB8(await SwitchConnection.ReadBytesAbsoluteAsync(EggParent1offset, 0x158, token).ConfigureAwait(false));
            var parent2 = new PB8(await SwitchConnection.ReadBytesAbsoluteAsync(EggParent2offset, 0x158, token).ConfigureAwait(false));
            Nature syncnature = Hub.Config.BDSP_RNG.AutoRNGSettings.SyncNature;
            var type = Hub.Config.BDSP_RNG.RNGType;
            var mode = Hub.Config.BDSP_RNG.WildMode;
            var events = Hub.Config.BDSP_RNG.Event;
            var isroutine = xoro == null;
            var result = new List<PB8>();
            int[]? unownForms = null;
            List<int>? encounterslots = null;
            int advance;
            uint initial_s0f;
            uint initial_s1f;
            uint initial_s2f;
            uint initial_s3f;

            if (isroutine)
            {
                var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);

                initial_s0f = BitConverter.ToUInt32(tmpRamState, 0);
                initial_s1f = BitConverter.ToUInt32(tmpRamState, 4);
                initial_s2f = BitConverter.ToUInt32(tmpRamState, 8);
                initial_s3f = BitConverter.ToUInt32(tmpRamState, 12);

                Log($"Initial states:\n[S0] {initial_s0f:X8}, [S1] {initial_s1f:X8}\n[S2] {initial_s2f:X8}, [S3] {initial_s3f:X8}\n");
            } 
            else
			{
                initial_s0f = (xoro != null) ? xoro.GetU32State()[0] : 0;
                initial_s1f = (xoro != null) ? xoro.GetU32State()[1] : 0;
                initial_s2f = (xoro != null) ? xoro.GetU32State()[2] : 0;
                initial_s3f = (xoro != null) ? xoro.GetU32State()[3] : 0;
            }

            if (mode is not WildMode.None)
            {
                var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
                var time = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
                GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
                encounterslots = GetEncounterSlots(version, route, time, mode);
                if (GetLocation(route).Contains("Solaceon Ruins"))
                    unownForms = GetUnownForms(route);
                if (isroutine)
                {
                    Log($"({version}) {GetLocation(route)} ({route}) [{time}]");
                    Log("Available mons:");
                    if (encounterslots.Count > 0)
                    {
                        var i = 0;
                        foreach (var mon in encounterslots)
                        {
                            if (unownForms is null || unownForms.Length == 0)
                                Log($"[{i}] {(Species)mon}");
                            else
                            {
                                var formstr = " ";
                                foreach (var form in unownForms!)
                                    formstr = $"{formstr}{form} ";
                                Log($"[{i}] {(Species)mon}-[{formstr}]");
                            }

                            i++;
                        }
                    }
                    else
                    {
                        Log("None");
                    }
                }
            }

            var rng = new Xorshift(initial_s0f, initial_s1f, initial_s2f, initial_s3f);
            var rngegg = new Xorshift(initial_s0f, initial_s1f, initial_s2f, initial_s3f);
            var rnglist = new RNGList(rngegg);

            for (advance = 0; advance < maxadvances; advance++)
            {
                bool foundflag = false;
                uint[] states = rng.GetU32State();
				var pk = new PB8
				{
					TrainerTID7 = sav.TrainerTID7,
					TrainerSID7 = sav.TrainerSID7,
					OriginalTrainerName = sav.OT,
				};
                if (Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies == 0)
                {
                    if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.Genderless)
                        pk.Species = (int)Species.Azelf;
                    else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.MaleOnly)
                        pk.Species = (int)Species.Volbeat;
                    else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.FemaleOnly)
                        pk.Species = (int)Species.Illumise;
                    else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F1)
                        pk.Species = (int)Species.Absol;
                    else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M1F3)
                        pk.Species = (int)Species.Jigglypuff;
                    else if (Hub.Config.BDSP_RNG.AutoRNGSettings.GenderRatio == GenderRatio.M3F1)
                        pk.Species = (int)Species.Growlithe;
                    else
                        pk.Species = (int)Species.Piplup;
                }
                else
                    pk.Species = (ushort)Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies;

                if (type is RNGType.Egg)
                {
                    int compatability = (int)Hub.Config.BDSP_RNG.AutoRNGSettings.Compatability;
                    if (HasOvalCharm)
                        compatability = compatability == 20 ? 40 : compatability == 50 ? 80 : 88;
                    if ((rnglist.getValue() % 100) < compatability)
                    {
                        seed = (ulong)rnglist.getValue();
                        pk = Calc.EggGenerator(pk, seed, HasShinyCharm, parent1, parent2);
                        foundflag = true;
                    }
                    rnglist.advanceState();
                }
                else if (type is RNGType.Roamer)
                    pk = Calc.CalculateFromSeed(pk, Shiny.Random, type, rng.Next());
                else
                {
                    pk = Calc.CalculateFromStates(pk, (type is not RNGType.MysteryGift) ? Shiny.Random : Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]), syncnature, mode, encounterslots, events, unownForms);
                    rng.Next();
                }

                result.Add(pk);

                var msg = $"\nAdvances: {advance}\n[S0] {states[0]:X8}, [S1] {states[1]:X8}\n[S2] {states[2]:X8}, [S3] {states[3]:X8}";
                if (Hub.Config.BDSP_RNG.WildMode is not WildMode.None)
                    msg = $"{msg}\nSpecies: {(Species)pk.Species}-{(pk.PersonalInfo.FormCount > 0 ? $"{pk.Form}" : "")} (EncounterSlot: {pk.Move1})";
                if (type is RNGType.Egg)
                    msg = $"{msg}\nSpecies: {(Species)pk.Species}-{(pk.PersonalInfo.FormCount > 0 ? $"{pk.Form}" : "")}";
                msg = $"{msg}{GetString(pk)}";
                if (verbose == true)
                    Log($"{Environment.NewLine}{msg}");

                bool found = false;
                if(type is not RNGType.Egg || foundflag)
                    found = await HandleTarget(pk, false, type, token, isroutine).ConfigureAwait(false);
                if (token.IsCancellationRequested || (found && isroutine))
                {
					if (found)
					{
                        msg = $"Details for found target:\n{msg}";
                        Log($"{Environment.NewLine}{msg}");
					}
                    return result;
                }
            }
            if(isroutine)
                Log($"Target not found in {advance} attempts.");
            return result;
        }

        private async Task<bool> HandleTarget(PB8 pk, bool encounter, RNGType type, CancellationToken token, bool dump = false)
        {
            //Initialize random species
            if (pk.Species == 0)
                pk.Species = 1;

            if (!StopConditionSettings.EncounterFound(pk, Hub.Config.StopConditions))
                return false;
            
            if (dump && DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder) && encounter)
                    DumpPokemon(DumpSetting.DumpFolder, "BDSP_RNG_Encounters", pk);

            if (encounter)
            {
                await WebHook.SendNotification(pk, "Target Encounter Found!", token).ConfigureAwait(false);
                Settings.AddCompletedRNGs();
            }

            return true;
        }

        private string GetLocation(int location_id)
        {
            return (this.locations.ElementAt(location_id));
        }

        // These don't change per session and we access them frequently, so set these each time we start.
        private async Task InitializeSessionOffsets(CancellationToken token)
        {
            Log("Caching session offsets...");
            RNGOffset = await SwitchConnection.PointerAll(Offsets.MainRNGState, token).ConfigureAwait(false);
            PlayerLocation = await SwitchConnection.PointerAll(Offsets.LocationPointer, token).ConfigureAwait(false);
            DayTime = await SwitchConnection.PointerAll(Offsets.DayTimePointer, token).ConfigureAwait(false);
            //Click useless key to actually initialize simulated controller
            await Click(SwitchButton.L, 0_050, token).ConfigureAwait(false);
        }

        private async Task RestartGameBDSP(bool untiloverworld, CancellationToken token)
        {
            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            await InitializeSessionOffsets(token).ConfigureAwait(false);
        }
        private async Task HasCharms(CancellationToken token)
        {
            var Items = await GetItems(token).ConfigureAwait(false);
            HasShinyCharm = false;
            HasOvalCharm = false;
            foreach (var item in Items)
            {
                if (item.Index == 632 && item.Count == 1)
                    HasShinyCharm = true;
                if (item.Index == 631 && item.Count == 1)
                    HasOvalCharm = true;
            }
            Log("HasCharms State is initialized!");
            Log($"HasShinyCharm: {HasShinyCharm}, HasOvalCharm: {HasOvalCharm}");
        }
        protected async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(SwitchStick.RIGHT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
            await SetStick(SwitchStick.LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }

        private IReadOnlyList<long> GetDestOffset(CheckMode mode, RNGType type = RNGType.Custom)
        {
            return mode switch
            {
                CheckMode.TeamSlot1 => Offsets.PartyStartPokemonPointer,
                CheckMode.TeamSlot2 => Offsets.PartySlot2PokemonPointer,
                CheckMode.Box1Slot1 => Offsets.BoxStartPokemonPointer,
                CheckMode.Wild => Offsets.OpponentPokemonPointer,
                CheckMode.Seed => type is RNGType.Egg ? Offsets.EggSeedPointer : GetRoamerOffset(),
                _ => Offsets.OpponentPokemonPointer,
            };
        }

        private IReadOnlyList<long> GetRoamerOffset()
		{
            if ((int)Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies == (ushort)Species.Mesprit)
                return Offsets.R1_SeedPointer;
            else
                return Offsets.R2_SeedPointer;
		}

        private void SettingCheck()
        {
            foreach (var s in Hub.Config.StopConditions.SearchConditions)
            {
                if (s.StopOnSpecies != Species.None && s.StopOnSpecies != Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies)
                    s.IsEnabled = false;

                /*if (!s.IsEnabled)
                    continue;

                var species = (ushort)Hub.Config.BDSP_RNG.AutoRNGSettings.TargetSpecies;
                var form = s.Form;
                if (form == null)
                    form = 0;
                var table = PersonalTable.BDSP.GetFormEntry(species, (byte)form);
                var fixgender = table.Gender is 0 or 254 or 255;
                if (fixgender)
                    s.Gender = TargetGenderType.Any;*/
            }
        }
    }
}
