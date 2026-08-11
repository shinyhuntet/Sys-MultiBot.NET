namespace SysBot.Pokemon;

using PKHeX.Core;

public class EncounterBotSnackSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotEncounterSV(cfg, hub)
{
    protected override int StartBattleA => 12;
    protected override int StartBattleB => 2;
}

