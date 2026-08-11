using PKHeX.Core;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon;

public partial class CalyrexDetailForm : PokeDetailForm
{
    public CalyrexDetailForm()
    {
        InitializeComponent();
        SetUpForms();
    }
    public override void ResetAssets()
    {
        CalyrexPic.Image = null;
        HorsePic.Image = null;
        CalyrexText.Text = string.Empty;
        HorseText.Text = string.Empty;
    }
    public async Task SetPokeDetail(PKM pk, int EncounterCount, bool isKing, CancellationToken token)
    {
        var img = await GetPokeImage(pk, token, 4).ConfigureAwait(false);
        var set = GetPrintName(pk, EncounterCount, 0);
        if (isKing)
        {
            CalyrexPic.Image = img;
            CalyrexText.Text =  set;
        }
        else
        {
            HorsePic.Image = img;
            HorseText.Text =  set;
        }
    }
}

