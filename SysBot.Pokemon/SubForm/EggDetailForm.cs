using PKHeX.Core;
using System;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon;

public partial class EggDetailForm : PokeDetailForm
{
    public EggDetailForm()
    {
        InitializeComponent();
        SetUpForms();
    }
    public override void ResetAssets()
    {
        PokemonText.Text = string.Empty;
        PokePic.Image = null;
        ShinyPic.Image = null;
        MarkPic.Image = null;
    }
    public void ResetParentsAssets()
    {
        ParentOneText.Text = string.Empty;
        ParentTwoText.Text = string.Empty;
        P1PokePic.Image = null;
        P2PokePic.Image = null;
        P1ShinyPic.Image = null;
        P2ShinyPic.Image = null;
        P1MarkPic.Image = null;
        P2MarkPic.Image = null;
    }
    public async Task SetPokeDetail(PKM pk, EggDetail Detail, CancellationToken token, int EncounterCount = 0)
    {
        await SetPokeImage(pk, Detail, token).ConfigureAwait(false);
        SetShinyImage(pk, Detail);
        SetMarkImage(pk, Detail);
        SetPrintName(pk, Detail, EncounterCount);
    }
    private void SetPrintName(PKM pk, EggDetail Detail, int EncounterCount)
    {
        string set = string.Empty;
        if(Detail == EggDetail.Egg)
            set = $"Encounter: {EncounterCount}{Environment.NewLine}";
        set += ShowdownParsing.GetShowdownText(pk);
        if (pk is IRibbonIndex r)
        {
            var rstring = GetRibbonName(r, out _);
            if (!string.IsNullOrEmpty(rstring))
                set += $"\nPokémon found to have **{rstring}**!";
        }
        if (Detail == EggDetail.Egg)
            PokemonText.Text = set;
        else if (Detail == EggDetail.ParentOne)
            ParentOneText.Text = set;
        else
            ParentTwoText.Text = set;
    }
    private async Task SetPokeImage(PKM pk, EggDetail Detail, CancellationToken token)
    {
        var img = await GetPokeImage(pk, token, 8).ConfigureAwait(false);
        if(Detail == EggDetail.Egg)
            PokePic.Image = img;
        else if(Detail == EggDetail.ParentOne)
            P1PokePic.Image = img;
        else
            P2PokePic.Image = img;
    }
    private void SetMarkImage(PKM pk, EggDetail Detail)
    {
        if (pk is IRibbonIndex r)
        {
            string url = GetMarkURL(pk);
            if (string.IsNullOrEmpty(url))
                return;

            if(Detail == EggDetail.Egg)
                MarkPic.Load(url);
            else if(Detail == EggDetail.ParentOne)
                P1MarkPic.Load(url);
            else
                P2MarkPic.Load(url);
        }
    }
    private void SetShinyImage(PKM pk, EggDetail Detail)
    {
        if (pk.IsShiny)
        {
            Image? shiny = GetShinyImage(pk);
            if (shiny == null)
                return;

            if (Detail == EggDetail.Egg)
                ShinyPic.Image = shiny;
            else if (Detail == EggDetail.ParentOne)
                P1ShinyPic.Image = shiny;
            else
                P2ShinyPic.Image = shiny;
        }
    }
    public enum EggDetail
    {
        Egg = 0,
        ParentOne = 1,
        ParentTwo = 2,
    }
}

