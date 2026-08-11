using PKHeX.Core;
using System;

namespace SysBot.Pokemon;

public class TradeExtensions<T> where T : PKM, new()
{
    public static string GetPokemonImageURL(T pkm, bool canGmax, bool fullSize, bool egg)
    {
        bool md = false;
        bool fd = false;
        var eggURL = $"https://raw.githubusercontent.com/Omni-KingZeno/HomeImages/refs/heads/main/Sprites/256x256/Egg_{(MoveType)pkm.PersonalInfo.Type1}.png";
        string[] baseLink;
        if (fullSize)
            baseLink = "https://raw.githubusercontent.com/Omni-KingZeno/HomeImages/refs/heads/main/Sprites/512x512/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');
        else baseLink = "https://raw.githubusercontent.com/Omni-KingZeno/HomeImages/refs/heads/main/Sprites/256x256/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');

        if (Enum.IsDefined(typeof(GenderDependent), pkm.Species) && !canGmax && pkm.Form is 0)
        {
            if (pkm.Gender == 0 && pkm.Species != (int)Species.Torchic)
                md = true;
            else fd = true;
        }

        int form = pkm.Species switch
        {
            (int)Species.Sinistea or (int)Species.Polteageist or (int)Species.Rockruff or (int)Species.Mothim => 0,
            (int)Species.Alcremie when pkm.IsShiny || canGmax => 0,
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
            return $"https://raw.githubusercontent.com/Omni-KingZeno/HomeImages/refs/heads/main/Sprites/128x128/poke_capture_0" + $"{pkm.Species}" + "_00" + $"{pkm.Form}" + "_" + $"{g}" + "_n_00000000_f_" + $"{s}" + ".png";
        }

        baseLink[2] = pkm.Species < 10 ? $"000{pkm.Species}" : pkm.Species < 100 && pkm.Species > 9 ? $"00{pkm.Species}" : pkm.Species >= 1000 ? $"{pkm.Species}" : $"0{pkm.Species}";
        baseLink[3] = pkm.Form < 10 ? $"00{form}" : $"0{form}";
        baseLink[4] = pkm.PersonalInfo.OnlyFemale ? "fo" : pkm.PersonalInfo.OnlyMale ? "mo" : pkm.PersonalInfo.Genderless ? "uk" : fd ? "fd" : md ? "md" : "mf";
        baseLink[5] = canGmax ? "g" : "n";
        baseLink[6] = "0000000" + (pkm.Species == (int)Species.Alcremie && !canGmax && pkm is IFormArgument pkarg ? pkarg.FormArgument : 0);
        baseLink[8] = pkm.IsShiny ? "r.png" : "n.png";
        return egg ? eggURL : string.Join("_", baseLink);
    }
    public static string FormOutput(ushort species, byte form, out string[] formString)
    {
        var strings = GameInfo.GetStrings(GameLanguage.LanguageCode(1));
        formString = FormConverter.GetFormList(species, strings.Types, strings.forms, GameInfo.GenderSymbolASCII, typeof(T) == typeof(PK9) ? EntityContext.Gen9 : EntityContext.Gen4);
        if (formString.Length is 0)
            return string.Empty;

        formString[0] = "";
        if (form >= formString.Length)
            form = (byte)(formString.Length - 1);
        return formString[form].Contains('-') ? formString[form] : formString[form] == "" ? "" : $"-{formString[form]}";
    }

}
