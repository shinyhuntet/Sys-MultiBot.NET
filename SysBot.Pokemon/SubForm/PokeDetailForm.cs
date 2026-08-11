using PKHeX.Core;
using PKHeX.Drawing;
using PKHeX.Drawing.PokeSprite;
using System;
using System.Drawing;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SysBot.Pokemon;
public partial class PokeDetailForm : Form
{
    private Image ShinySquare = null!;
    private Image ShinyStar = null!;
    private Image Egg = null!;
    private Image ManaEgg = null!;
    public PokeDetailForm()
    {
        InitializeComponent();
        Task.Run(async () =>
        {
            await AnticipateResponse(CancellationToken.None).ConfigureAwait(false);
            await GetEggImage(CancellationToken.None).ConfigureAwait(false);
        });
        SyncContextHolder.SyncContext = SynchronizationContext.Current;
        SpriteBuilder.ShowTeraThicknessStripe = 0x4;
        SpriteBuilder.ShowTeraOpacityStripe = 0xAF;
        SpriteBuilder.ShowTeraOpacityBackground = 0xFF;
    }
    public void RefreshComponents()
    {
        InitializeComponent();
    }
    private async Task AnticipateResponse(CancellationToken token)
    {
        using HttpClient client = new();
        string shinyicon = "https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.WinForms/Resources/img/Markings/";
        var square = await client.GetStreamAsync(shinyicon + "rare_icon_2.png", token).ConfigureAwait(false);
        ShinySquare = Image.FromStream(square);

        var star = await client.GetStreamAsync(shinyicon + "rare_icon.png", token).ConfigureAwait(false);
        ShinyStar = Image.FromStream(star);
    }
    public void SetUpForms()
    {
        PokemonText.Visible = false;
        PokePic.Visible = false;
        ShinyPic.Visible = false;
        TypePic.Visible = false;
        MarkPic.Visible = false;
    }
    public virtual void ResetAssets()
    {
        PokemonText.Text = string.Empty;
        PokePic.Image = null;
        ShinyPic.Image = null;
        TypePic.Image = null;
        MarkPic.Image = null;
    }
    public async Task SetPokeDetail(PKM pk, int EncounterCount, double Rate, CancellationToken token)
    {
        await SetPokeImage(pk, token).ConfigureAwait(false);
        SetShinyImage(pk);
        if(pk is PK9 pk9)
            SetGemImage((int)pk9.TeraType);
        SetMarkImage(pk);
        SetPrintName(pk, EncounterCount, Rate);
    }
    private void SetPrintName(PKM pk, int EncounterCount, double Rate)
    {
        var set = $"Encounter: {EncounterCount}{Environment.NewLine}Target Rate: {Rate:0.0000}%{Environment.NewLine}";
        set += ShowdownParsing.GetShowdownText(pk);
        if (pk is IRibbonIndex r)
        {
            var rstring = GetRibbonName(r, out _);
            if (!string.IsNullOrEmpty(rstring))
                set += $"\nPokémon found to have **{rstring}**!";
        }
        PokemonText.Text = set;
    }
    protected string GetPrintName(PKM pk, int EncounterCount, double Rate = 0)
    {
        var set = $"Encounter: {EncounterCount}{Environment.NewLine}{(Rate <= 0 ? "" : $"Target Rate: {Rate:0.0000}%{Environment.NewLine}")}";
        set += ShowdownParsing.GetShowdownText(pk);
        if (pk is IRibbonIndex r)
        {
            var rstring = GetRibbonName(r, out _);
            if (!string.IsNullOrEmpty(rstring))
                set += $"\nPokémon found to have **{rstring}**!";
        }
        return set;
    }
    public string GetRibbonName(IRibbonIndex pk, out RibbonIndex ribbon)
    {
        ribbon = RibbonIndex.MAX_COUNT;
        for (var mark = RibbonIndex.ChampionKalos; mark <= RibbonIndex.MarkTitan; mark++)
        {
            if (pk.GetRibbon((int)mark))
            {
                ribbon = mark;
                return mark.GetPropertyName();
            }
        }
        return "";
    }
    protected async Task<Image> GetPokeImage(PKM pk, CancellationToken token, int? rate = null)
    {
        HttpClient client = new();
        var sprite = PokeImg(pk, false);
        var response = await client.GetStreamAsync(sprite, token).ConfigureAwait(false);
        Image img = Image.FromStream(response);
        img = MakePokeSprites((Bitmap)img, pk, (Bitmap)(pk.Species == (ushort)Species.Manaphy ? ManaEgg : Egg), rate);
        return img;
    }
    private async Task GetEggImage(CancellationToken token)
    {
        HttpClient client = new();
        var eggurl = "https://raw.githubusercontent.com/zyro670/HomeImages/master/512x512/poke_capture_0000_000_uk_n_00000000_f_n.png";
        var manaeggurl = "https://raw.githubusercontent.com/zyro670/HomeImages/master/512x512/poke_capture_0000_001_uk_n_00000000_f_n.png";
        var egg = await client.GetStreamAsync(eggurl, token).ConfigureAwait(false);
        Egg = Image.FromStream(egg);
        var manaegg = await client.GetStreamAsync(manaeggurl, token).ConfigureAwait(false);
        ManaEgg = Image.FromStream(manaegg);
    }
    private async Task SetPokeImage(PKM pk, CancellationToken token)
    {
        PokePic.Image = await GetPokeImage(pk, token, 4).ConfigureAwait(false);
    }
    private void SetMarkImage(PKM pk)
    {
        if (pk is IRibbonIndex r)
        {
            var ribbonstring = GetRibbonName(r, out RibbonIndex mark);
            if (string.IsNullOrEmpty(ribbonstring))
                return;
            string url = $"https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.Misc/Resources/img/ribbons/{ribbonstring.ToLower()}.png";
            MarkPic.Load(url);
        }
    }
    protected string GetMarkURL(PKM pk)
    {
        if (pk is IRibbonIndex r)
        {
            var ribbonstring = GetRibbonName(r, out RibbonIndex mark);
            if (string.IsNullOrEmpty(ribbonstring))
                return string.Empty;
            string url = $"https://raw.githubusercontent.com/kwsch/PKHeX/master/PKHeX.Drawing.Misc/Resources/img/ribbons/{ribbonstring.ToLower()}.png";
            return url;
        }
        return string.Empty;
    }
    protected Image? GetShinyImage(PKM pk)
    {
        if (pk.IsShiny)
        {
            Image? shiny = pk.ShinyXor == 0 ? ShinySquare : ShinyStar;
            return shiny;
        }
        return null;
    }
    private void SetShinyImage(PKM pk)
    {
        if (pk.IsShiny)
        {
            Image? shiny = pk.ShinyXor == 0 ? ShinySquare : ShinyStar;
            ShinyPic.Image = shiny;
        }
    }
    private void SetGemImage(int teratype)
    {
        var baseurl = $"https://raw.githubusercontent.com/LegoFigure11/RaidCrawler/main/RaidCrawler.WinForms/Resources/gem_{teratype:D2}.png";
        PictureBox picture = new();
        picture.Load(baseurl);
        var baseImg = picture.Image;
        if (baseImg is null)
            return;

        var backlayer = new Bitmap(baseImg.Width + 10, baseImg.Height + 10, baseImg.PixelFormat);
        baseImg = ImageUtil.LayerImage(backlayer, baseImg, 5, 5);
        var pixels = ImageUtil.GetPixelData((Bitmap)baseImg);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 0)
            {
                pixels[i] = 0;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
            }
        }

        baseImg = ImageUtil.GetBitmap(pixels, baseImg.Width, baseImg.Height, baseImg.PixelFormat);
        TypePic.Image = baseImg;
    }
    private Image MakePokeSprites(Bitmap img, PKM pk, Bitmap? egg = null, int? rate = null)
    {
        if (pk is ITeraType t)
        {
            var TeraType = (byte)t.TeraTypeOriginal;
            SpriteBackgroundType SpType = SpriteBackgroundType.BottomStripe;
            img = (Bitmap)ApplyTeraColor(TeraType, img, SpType);
        }
        if (pk.IsEgg)
        {
            if (egg is null)
                return img;
            if (rate is null)
                return img;
            //var egg = pk.Species == (ushort)Species.Manaphy ? PKHeX.Drawing.PokeSprite.Properties.Resources.b_490_e : PKHeX.Drawing.PokeSprite.Properties.Resources.b_egg;
            egg = new Bitmap(egg, egg.Width / (int)rate, egg.Height / (int)rate);
            img = ImageUtil.LayerImage(img, egg, 2, img.Height - egg.Height - 2, 0.5);
        }
        img = SpriteUtil.Spriter.GetSprite(img, pk.Species, pk.HeldItem, false, ShinyExtensions.GetType(pk));
        if (pk is IShadowCapture { IsShadow: true })
        {
            SpriteUtil.GetSpriteGlow(img, 75, 0, 130, out var pixels, true);
            var glowImg = ImageUtil.GetBitmap(pixels, img.Width, img.Height, img.PixelFormat);
            return ImageUtil.LayerImage(glowImg, img, 0, 0);
        }
        if (pk is IGigantamaxReadOnly { CanGigantamax: true })
        {
            var gm = PKHeX.Drawing.PokeSprite.Properties.Resources.dyna;
            return ImageUtil.LayerImage(img, gm, (img.Width - gm.Width) / 2, 0);
        }
        if (pk is IAlphaReadOnly { IsAlpha: true })
        {
            var alpha = PKHeX.Drawing.PokeSprite.Properties.Resources.alpha_alt;
            return ImageUtil.LayerImage(img, alpha, SpriteUtil.Spriter.Width - 19, 0);
        }
        return img;
    }
    private Image ApplyTeraColor(byte elementalType, Image img, SpriteBackgroundType type)
    {
        var color = TypeColor.GetTypeSpriteColor(elementalType);
        var thk = SpriteBuilder.ShowTeraThicknessStripe;
        var op = SpriteBuilder.ShowTeraOpacityStripe;
        var bg = SpriteBuilder.ShowTeraOpacityBackground;
        return ApplyColor(img, type, color, thk, op, bg);
    }
    private Image ApplyColor(Image img, SpriteBackgroundType type, Color color, int thick, byte opacStripe, byte opacBack)
    {
        if (type == SpriteBackgroundType.BottomStripe)
        {
            int stripeHeight = thick; // from bottom
            if ((uint)stripeHeight > img.Height) // clamp negative & too-high values back to height.
                stripeHeight = img.Height;

            return ImageUtil.BlendTransparentTo(img, color, opacStripe, img.Width * 4 * (img.Height - stripeHeight));
        }
        if (type == SpriteBackgroundType.TopStripe)
        {
            int stripeHeight = thick; // from top
            if ((uint)stripeHeight > img.Height) // clamp negative & too-high values back to height.
                stripeHeight = img.Height;

            return ImageUtil.BlendTransparentTo(img, color, opacStripe, 0, (img.Width * 4 * stripeHeight) - 4);
        }
        if (type == SpriteBackgroundType.FullBackground) // full background
            return ImageUtil.BlendTransparentTo(img, color, opacBack);
        return img;
    }
    private string PokeImg(PKM pkm, bool canGmax)
    {
        bool md = false;
        bool fd = false;
        string[] baseLink;
        baseLink = "https://raw.githubusercontent.com/zyro670/HomeImages/master/512x512/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');

        if (Enum.IsDefined(typeof(GenderDependent), pkm.Species) && !canGmax && pkm.Form is 0)
        {
            if (pkm.Gender is 0 && pkm.Species is not (ushort)Species.Torchic)
                md = true;
            else fd = true;
        }

        int form = pkm.Species switch
        {
            (ushort)Species.Sinistea or (ushort)Species.Polteageist or (ushort)Species.Rockruff or (ushort)Species.Mothim => 0,
            (ushort)Species.Alcremie when pkm.IsShiny || canGmax => 0,
            _ => pkm.Form,

        };

        if (pkm.Species is (ushort)Species.Sneasel)
        {
            if (pkm.Gender is 0)
                md = true;
            else fd = true;
        }

        if (pkm.Species is (ushort)Species.Basculegion)
        {
            if (pkm.Gender is 0)
            {
                md = true;
                pkm.Form = 0;
            }
            else
            {
                pkm.Form = 1;
            }

            string s = pkm.IsShiny ? "r" : "n";
            string g = md && pkm.Gender is not 1 ? "md" : "fd";
            return $"https://raw.githubusercontent.com/zyro670/HomeImages/master/128x128/poke_capture_0" + $"{pkm.Species}" + "_00" + $"{pkm.Form}" + "_" + $"{g}" + "_n_00000000_f_" + $"{s}" + ".png";
        }

        baseLink[2] = pkm.Species < 10 ? $"000{pkm.Species}" : pkm.Species < 100 && pkm.Species > 9 ? $"00{pkm.Species}" : pkm.Species >= 1000 ? $"{pkm.Species}" : $"0{pkm.Species}";
        baseLink[3] = pkm.Form < 10 ? $"00{form}" : $"0{form}";
        baseLink[4] = pkm.PersonalInfo.OnlyFemale ? "fo" : pkm.PersonalInfo.OnlyMale ? "mo" : pkm.PersonalInfo.Genderless ? "uk" : fd ? "fd" : md ? "md" : "mf";
        baseLink[5] = canGmax ? "g" : "n";
        baseLink[6] = "0000000" + (pkm.Species is (ushort)Species.Alcremie && !canGmax ? pkm.Data[0xD0] : 0);
        baseLink[8] = pkm.IsShiny ? "r.png" : "n.png";
        return string.Join("_", baseLink);
    }

}
public static class SyncContextHolder
{
    public static SynchronizationContext? SyncContext { get; set; }
}

