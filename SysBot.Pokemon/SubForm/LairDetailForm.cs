using PKHeX.Core;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SysBot.Pokemon;

public partial class LairDetailForm : PokeDetailForm
{
    private readonly List<PictureBox> PokemonImg;
    private readonly List<TextBox> PokeText;
    private readonly List<PictureBox> ShinyImg;
    private readonly List<PictureBox> DmaxImg;
    public LairDetailForm()
    {
        InitializeComponent();
        SetUpForms();
        PokemonImg = [LairPK1, LairPK2, LairPK3, LairPK4];
        PokeText = [LairPK1Text, LairPK2Text, LairPK3Text, LairPK4Text];
        ShinyImg = [LairPK1Shiny, LairPK2Shiny, LairPK3Shiny, LairPK4Shiny];
        DmaxImg = [DmaxPic1, DmaxPic2, DmaxPic3, DmaxPic4];
    }
    public override void ResetAssets()
    {
        for(int i = 0; i < 4; i++)
        {
            PokemonImg[i].Image = null;
            PokeText[i].Text = string.Empty;
            ShinyImg[i].Image = null;
            DmaxImg[i].Image = null;
        }
    }
    public async Task SetPokeDetail(PKM pk, int EncounterCount, int index, CancellationToken token)
    {
        PokemonImg[index].Image = await GetPokeImage(pk, token, 8).ConfigureAwait(false);
        var shiny = GetShinyImage(pk);
        if (shiny != null)
            ShinyImg[index].Image = shiny;
        var img = GetDynamaxImage(pk);
        if (img != null)
            DmaxImg[index].Image = img;
        PokeText[index].Text =  GetPrintName(pk, EncounterCount, 0);
    }

    private Image? GetDynamaxImage(PKM pk)
    {
        if (pk is PK8 pk8)
        {
            if (!pk8.CanGigantamax)
                return null;
            return Properties.Resource.GigantamaxIcon;            
        }
        return null;
    }

}

