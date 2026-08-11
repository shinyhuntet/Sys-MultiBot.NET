namespace SysBot.Pokemon;

using PKHeX.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static Base.SwitchButton;
using static SysBot.Pokemon.EncounterSettingsSV;

public class EncounterBotResetSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotSV(cfg, hub)
{
    private byte CurrentBox = 0;
    private int CurrentSlot = 0;
    private int PartySlot = 0;
    private bool success;
    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
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
            
            Log("Looking for a Pokémon...");

            PK9? b1s1 = null;
            byte[]? bytes = null;

            var later = DateTime.Now.AddMinutes(5);
            Log($"Wait till [{later}] before we force a game restart", false);

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
                        Log("Your Pokémon has been catched and placed in B1S1. Be sure to save your game!");

                    if (stop)
                        return;
                }

                await Click(A, 0_200, token).ConfigureAwait(false);
            }

            if (DateTime.Now >= later)
                Log("Force restart of the game..");

            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            Log($"Single encounter duration: [{sw.Elapsed}]", false);
        }
    }
}
