namespace SysBot.Pokemon;

using PKHeX.Core;
using SysBot.Pokemon.SV.Vision;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static Base.SwitchButton;
using static SysBot.Pokemon.EncounterSettingsSV;

public abstract class EncounterBotEncounterSV : EncounterBotSV
{
    protected abstract int StartBattleA { get; }
    protected abstract int StartBattleB { get; }

    private byte CurrentBox = 0;
    private int CurrentSlot = 0;
    private int PartySlot = 0;
    private bool success;
    private readonly ItemStructure itemStructure;
    protected EncounterBotEncounterSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg, hub)
    {
        itemStructure = new(cfg, hub, SwitchConnection);
    }
    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
        var (BallIndex, Left) = await itemStructure.GetBall(token).ConfigureAwait(false);
        if(BallIndex < 0)
        {
            Log($"TargetBall: {Settings.TargetBall} Ball is out of stock!");
            return;
        }

        if (Settings.Readmode == ReadModeSV.Party)
            (success, PartySlot) = await GetPartyEmptySlot(token).ConfigureAwait(false);
        else
            (success, CurrentBox, CurrentSlot) = await ReadEmptySlot(false, CurrentBox, CurrentSlot, token).ConfigureAwait(false);
        if (!success)
        {
            Log("No empty slot in party, cannot continue.");
            return;
        }

        while (!token.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();

            var later = DateTime.Now.AddSeconds(StartBattleA);
            if (!await IsInBattleToStatic(token).ConfigureAwait(false))
            {
                Log($"Starting battle");
                Log($"Press A till [{later}]", false);
                while (DateTime.Now <= later)
                    await Click(A, 200, token).ConfigureAwait(false);
            }

            if(!await IsInBattleToStatic(token).ConfigureAwait(false))
                Log("Still not in battle click A");

            while (!await IsInBattleToStatic(token).ConfigureAwait(false))                            
                await Click(A, 1_000, token).ConfigureAwait(false);            
            
            later = DateTime.Now.AddSeconds(StartBattleB);
            Log($"Press B till [{later}]", false);
            while (DateTime.Now <= later)
                await Click(B, 200, token).ConfigureAwait(false);

            await Task.Delay(500, token).ConfigureAwait(false);
            await EnableAlwaysCatch(token).ConfigureAwait(false);
ReCatch:
            await CatchRoutine(Left, BallIndex, token).ConfigureAwait(false);
            later = DateTime.Now.AddSeconds(20);
            Log($"Exit battle, wait till [{later}] before we force a game restart", false);
            PK9? b1s1 = null;
            byte[]? bytes = null;

            while ((b1s1 == null || (Species)b1s1.Species == Species.None || !PersonalTable.SV.IsPresentInGame(b1s1.Species, b1s1.Form)) && DateTime.Now <= later)
            {
                if (Settings.Readmode == ReadModeSV.Box)
                    (b1s1, bytes) = await ReadRawBoxPokemon(CurrentBox, CurrentSlot, token).ConfigureAwait(false);
                else
                    (b1s1, bytes) = await ReadRawPartyPokemon(PartySlot, token).ConfigureAwait(false);

                if (b1s1 is { Valid: true, EncryptionConstant: > 0 } && (Species)b1s1.Species != Species.None && PersonalTable.SV.IsPresentInGame(b1s1.Species, b1s1.Form))
                {
                    var (stop, success) = await HandleEncounter(b1s1, token, bytes, true).ConfigureAwait(false);

                    await WebHook.SendNotification(b1s1, (success ? "Target Encounter Found!" : b1s1.IsShiny ? "Unwanted Shiny Found." : "Unwanted Enounter..."), token, true, false, EncounterCount, Rate).ConfigureAwait(false);
                    if (success)
                        Log($"{(Species)b1s1.Species} has been catched and placed in Box{CurrentBox + 1} Slot{CurrentSlot + 1}. Be sure to save your game!");

                    if (stop)                    
                        return;                    
                }

                await Click(B, 200, token).ConfigureAwait(false);
            }
            if (DateTime.Now >= later && (b1s1 == null || (Species)b1s1.Species == Species.None) && await IsInBattleToStatic(token).ConfigureAwait(false))
            {
                Log("Redo Catch routine.. ");
                goto ReCatch;
            }

            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            Log($"Single encounter duration: [{sw.Elapsed}]", false);
        }
    }
}
