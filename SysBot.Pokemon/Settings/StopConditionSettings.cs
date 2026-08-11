namespace SysBot.Pokemon;

using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Xml;

public class StopConditionSettings
{
    private const string StopConditions = nameof(StopConditions);
    public override string ToString() => "Stop Condition Settings";

    [Category(StopConditions), Description("Desired spreads, search for nature and IVs. In the format HP/Atk/Def/SpA/SpD/Spe. Use \"x\" for unchecked IVs and \"/\" as a separator.")]
    public List<SearchCondition> SearchConditions { get; set; } = new();
    
    [Category(StopConditions), Description("Holds Capture button to record a 30 second clip when a matching Pokémon is found by EncounterBot or Fossilbot.")]
    public bool CaptureVideoClip { get; set; }

    [Category(StopConditions), Description("Extra time in milliseconds to wait after an encounter is matched before pressing Capture for EncounterBot or Fossilbot.")]
    public int ExtraTimeWaitCaptureVideo { get; set; } = 10000;

    [Category(StopConditions), Description("If not empty, the provided string will be prepended to the result found log message to Echo alerts for whomever you specify. For Discord, use <@userIDnumber> to mention.")]
    public string MatchFoundEchoMention { get; set; } = string.Empty;

    [Category(StopConditions)]
    public class SearchCondition
    {
        public Species Species;
        public int? Form;
        public TargetGenderType Gender;
        public string[]? formstringlist;
        public string? FormString { get; set; }
        public string[]? FormList { get { return formstringlist; } }
        public override string ToString()
        {
            if (!IsEnabled) return $"{Nature}, condition is disabled";

            var ivsStr = FlawlessIVs == TargetFlawlessIVsType.Disabled
                ? $"{TargetMinIVs} - {TargetMaxIVs}"
                : $"Flawless IVs: {Convert(FlawlessIVs)}";

            var isAlpha = AlphaTarget == TargetAlphaType.AnyAlpha ? " (A)" : "";

            return $"{Nature}, {StopOnSpecies}{isAlpha}, {ivsStr}";
        }

        [Category(StopConditions), Description("Target Rate")]
        public string Rate { get; set; } = string.Empty;

        [Category(StopConditions), DisplayName("a. Enabled")]
        public bool IsEnabled { get; set; } = true;
        
        [Category(StopConditions), DisplayName("b. Species")]
        public Species StopOnSpecies { get { return Species; } set { Species = value; SetForm(); GenderCheck(); } }

        [Category(StopConditions), DisplayName("c. Form")]
        public int? StopOnForm { get { return Form; } set { Form = value; SetForm(); GenderCheck(); } }

        [Category(StopConditions), DisplayName("d. IsMarkOnly")]
        public bool MarkOnly { get; set; }

        [Category(StopConditions), DisplayName("e. UnwantedMarks")]
        public string UnwantedMarks { get; set; } = "";

        [Category(StopConditions), DisplayName("f. Alpha (if applicable)")]
        public TargetAlphaType AlphaTarget { get; set; } = TargetAlphaType.DisableOption;

        [Category(StopConditions), DisplayName("g. Selects the shiny type to stop on.")]
        public TargetShinyType ShinyTarget { get; set; } = TargetShinyType.DisableOption;

        [Category(StopConditions), DisplayName("h. Nature")]
        public Nature Nature { get; set; } = Nature.Random;

        [Category(StopConditions), DisplayName("i. Ability")]
        public TargetAbilityType AbilityTarget { get; set; } = TargetAbilityType.Any;

        [Category(StopConditions), DisplayName("j. Gender")]
        public TargetGenderType GenderTarget { get { if (Species <= Species.None) { Gender = TargetGenderType.Any; } return Gender; } set { Gender = value; GenderCheck(); } }

        [Category(StopConditions), DisplayName("k. Minimum flawless IVs")]
        [TypeConverter(typeof(DescriptionAttributeConverter))]
        public TargetFlawlessIVsType FlawlessIVs { get; set; } = TargetFlawlessIVsType.Disabled;

        [Category(StopConditions), DisplayName("l. Minimum accepted IVs")]
        public string TargetMinIVs { get; set; } = "";

        [Category(StopConditions), DisplayName("m. Maximum accepted IVs")]
        public string TargetMaxIVs { get; set; } = "";
        [Category(StopConditions), DisplayName("n. IsMinMaxScaleOnly")]
        public bool MinMaxScaleOnly { get; set; } = false;

        [Category(StopConditions), DisplayName("o. IsOneInOneHundredOnly")]
        public bool OneInOneHundredOnly { get; set; } = true;
        private void SetForm()
        {
            GameStrings gameStrings = GameInfo.GetStrings("en");
            var TypesList = gameStrings.types;
            string[] GenderList = [.. GameInfo.GenderSymbolUnicode];
            var FormList = gameStrings.forms;
            var form = PersonalTable.SV.GetFormEntry((ushort)Species, Form == null ? (byte)0 : (byte)Form).FormCount;
            if (Form == null)
            {
                FormString = null;
                formstringlist = null;
                return;
            }
            else if (Form > form - 1 || Form < 0)
            {
                Form = 0;
            }
            var formlist = FormConverter.GetFormList((ushort)Species, TypesList, FormList, GenderList, EntityContext.Gen9);
            if (Species == Species.Minior)
                formlist = formlist.Take((formlist.Length + 1) / 2).ToArray();

            if (formlist.Length == 0 || (formlist.Length == 1 && formlist[0].Equals("")))
            {
                Form = null;
                FormString = null;
                formstringlist = null;
            }
            else
            {
                formstringlist = formlist;
                FormString = formlist[Form != null ? (int)Form : 0];
            }
        }
        private void GenderCheck()
        {
            var gender = PersonalTable.SV.GetFormEntry((ushort)Species, Form == null ? (byte)0 : (byte)Form).Gender;
            if (gender is PersonalInfo.RatioMagicGenderless or PersonalInfo.RatioMagicMale or PersonalInfo.RatioMagicFemale)
                Gender = TargetGenderType.Any;
            else if (Gender == TargetGenderType.Genderless)
                Gender = TargetGenderType.Any;
        }
        public void ReadUnwantedMarks(out IReadOnlyList<string> marks) =>
            marks = UnwantedMarks.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

        public virtual bool IsUnwantedMark(string mark)
        {
            ReadUnwantedMarks(out IReadOnlyList<string> marklist);
            return marklist is not null && marklist.Contains(mark);
        }
    }


    public static bool EncounterFound<T>(T pk, StopConditionSettings settings, bool skipSpeciesCheck = false) where T : PKM
    {        
        // Reorder the speed to be last.
        Span<int> pkIVList = stackalloc int[6];
        pk.GetIVs(pkIVList);
        (pkIVList[5], pkIVList[3], pkIVList[4]) = (pkIVList[3], pkIVList[4], pkIVList[5]);
        var pkIVsArr = pkIVList.ToArray();

        // No search conditions to match
        if (!settings.SearchConditions.Any(s => s.IsEnabled))
            return true;

        return settings.SearchConditions.Any(s =>
            (skipSpeciesCheck || s.StopOnSpecies == (Species)pk.Species || s.StopOnSpecies == Species.None) &&
            (skipSpeciesCheck || !s.StopOnForm.HasValue || s.StopOnForm == pk.Form) &&
            (MatchScale(pk, s)) &&
            (MatchOneInOneHundred(pk, s)) &&
            (MatchMarks(pk as IRibbonIndex, s)) &&
            (MatchIVs(pkIVsArr, s.TargetMinIVs, s.TargetMaxIVs, s.FlawlessIVs) || MatchFlawlessIVs(pkIVsArr, s.FlawlessIVs)) &&
            (s.Nature == pk.Nature || s.Nature == Nature.Random) &&
            MatchGender(s.GenderTarget, (Gender)pk.Gender) &&
            MatchAbility(s.AbilityTarget, pk.Ability) &&
            MatchAlpha(s.AlphaTarget, pk as IAlpha) &&
            MatchShiny(s.ShinyTarget, pk) &&
            s.IsEnabled);
    }

    public static double CalcRate(StopConditionSettings filters)
    {
        double Rate = 0.00;
        foreach (var filter in filters.SearchConditions)
        {
            if (!filter.IsEnabled)
                continue;
            string rate = filter.Rate.Replace(" ", "");
            string[] ratearray = rate.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (ratearray.Length == 2)
            {
                if (int.TryParse(ratearray[0], out var Rate1) && int.TryParse(ratearray[1], out var Rate2))
                    Rate += (Rate1 * 1.00) / Rate2;
            }
        }
        return Rate;
    }
    private static bool MatchScale(PKM pk, SearchCondition search)
    {
        if (!search.MinMaxScaleOnly)
            return true;

        var pks = pk as IScaledSize;
        var pks3 = pk as IScaledSize3;

        if (pks3 is not null)
            return pks3.Scale == 0 || pks3.Scale == 255;

        if (pks is not null)
            return pks.HeightScalar == 0 || pks.HeightScalar == 255;
        
        return true;
    }
    private static bool MatchOneInOneHundred<T>(T pk, SearchCondition search) where T : PKM
    {
        if (!search.OneInOneHundredOnly)
            return true;

        if((Species)pk.Species is not Species.Dunsparce or Species.Tandemaus )
            return true;

        return pk.EncryptionConstant % 100 == 0;
    }
    private static bool MatchMarks(IRibbonIndex? pk, SearchCondition search)
    {
        if (pk is null)
            return true;

        if (!search.MarkOnly)
            return true;

        var unmarked =!HasMark(pk);
        var unwanted = search.IsUnwantedMark(GetMarkName(pk));
        return !unmarked && !unwanted;
    }
    private static bool MatchAlpha(TargetAlphaType alphaTarget, IAlpha? pk)
    {
        if (pk is null)
            return true;

        return alphaTarget switch
        {
            TargetAlphaType.NonAlpha => !pk.IsAlpha,
            TargetAlphaType.AnyAlpha => pk.IsAlpha,
            TargetAlphaType.DisableOption => true,
            _ => throw new ArgumentOutOfRangeException(nameof(alphaTarget), alphaTarget, null)
        };
    }

    private static bool MatchShiny<T>(TargetShinyType shinyTarget, T pk) where T : PKM
    {
        return shinyTarget switch
        {
            TargetShinyType.AnyShiny => pk.IsShiny,
            TargetShinyType.NonShiny => !pk.IsShiny,
            TargetShinyType.StarOnly => pk.IsShiny && pk.ShinyXor != 0,
            TargetShinyType.SquareOnly => pk.ShinyXor == 0,
            TargetShinyType.DisableOption => true,
            _ => throw new ArgumentException(nameof(TargetShinyType)),
        };
    }

    private static bool MatchAbility(TargetAbilityType target, int result)
    {
        return target switch
        {
            TargetAbilityType.Any => true,
            TargetAbilityType.First => 1 == result,
            TargetAbilityType.Second => 2 == result,
            TargetAbilityType.Hidden => 4 == result,
            _ => throw new ArgumentOutOfRangeException(nameof(target), $"{nameof(TargetAbilityType)} value {target} is not valid"),
        };
    }

    private static bool MatchGender(TargetGenderType target, Gender result)
    {
        return target switch
        {
            TargetGenderType.Any => true,
            TargetGenderType.Male => Gender.Male == result,
            TargetGenderType.Female => Gender.Female == result,
            TargetGenderType.Genderless => Gender.Genderless == result,
            _ => throw new ArgumentOutOfRangeException(nameof(target), $"{nameof(TargetGenderType)} value {target} is not valid"),
        };
    }

    private static bool MatchFlawlessIVs(IReadOnlyList<int> pkIVs, TargetFlawlessIVsType targetFlawlessIVs)
    {
        var count = pkIVs.Count(iv => iv == 31);

        return targetFlawlessIVs switch
        {
            TargetFlawlessIVsType.Disabled => false,
            TargetFlawlessIVsType._0 => count >= 0,
            TargetFlawlessIVsType._1 => count >= 1,
            TargetFlawlessIVsType._2 => count >= 2,
            TargetFlawlessIVsType._3 => count >= 3,
            TargetFlawlessIVsType._4 => count >= 4,
            TargetFlawlessIVsType._5 => count >= 5,
            TargetFlawlessIVsType._6 => count == 6,
            _ => throw new ArgumentOutOfRangeException(nameof(targetFlawlessIVs), targetFlawlessIVs, null)
        };
    }

    private static bool MatchIVs(IReadOnlyList<int> pkIVs, string targetMinIVsStr, string targetMaxIVsStr, TargetFlawlessIVsType targetFlawlessIVs)
    {
        if (targetFlawlessIVs != TargetFlawlessIVsType.Disabled) return false;

        var targetMinIVs = ReadTargetIVs(targetMinIVsStr, true);
        var targetMaxIVs = ReadTargetIVs(targetMaxIVsStr, false);

        for (var i = 0; i < 6; i++)
        {
            if (targetMinIVs[i] > pkIVs[i] || targetMaxIVs[i] < pkIVs[i])
                return false;
        }

        return true;
    }

    private static int[] ReadTargetIVs(string splitIVsStr, bool min)
    {
        var targetIVs = new int[6];
        char[] split = ['/'];

        var splitIVs = splitIVsStr.Split(split, StringSplitOptions.RemoveEmptyEntries);

        // Only accept up to 6 values. Fill it in with default values if they don't provide 6.
        // Anything that isn't an integer will be a wild card.
        for (var i = 0; i < 6; i++)
        {
            if (i < splitIVs.Length)
            {
                var str = splitIVs[i];
                if (int.TryParse(str, out var val))
                {
                    targetIVs[i] = val;
                    continue;
                }
            }
            targetIVs[i] = min ? 0 : 31;
        }
        return targetIVs;
    }

    public static bool HasMark(IRibbonIndex pk)
    {
        return HasMark(pk, out _);
    }

    public static bool HasMark(IRibbonIndex pk, out RibbonIndex result)
    {
        result = default;
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
            {
                result = mark;
                return true;
            }
        }
        return false;
    }

    public static ReadOnlySpan<BattleTemplateToken> TokenOrder =>
    [
        BattleTemplateToken.FirstLine,
        BattleTemplateToken.Shiny,
        BattleTemplateToken.Nature,
        BattleTemplateToken.IVs,
    ];

    public string GetPrintName(PKM pk)
    {
        const LanguageID lang = LanguageID.English;
        var settings = new BattleTemplateExportSettings(TokenOrder, lang);
        var set = ShowdownParsing.GetShowdownText(pk, settings);

        if(pk is IAlpha pka)
            set += pka.IsAlpha ? "Alpha - " + set : set;

        // Since we can match on Min/Max Height for transfer to future games, display it.
        var scales = new List<string>();
        if (pk is IScaledSize p)
            scales.Add($"Height: {p.HeightScalar}");

        if (pk is IScaledSize3 p3)
            scales.Add($"Scale: {p3.Scale}");

        if (scales.Count > 0)
            set += $"\n{string.Join(", ", scales)}";

        if (pk is IRibbonIndex r)
        {
            var rstring = GetMarkName(r);
            if (!string.IsNullOrEmpty(rstring))
                set += $"\nPokémon found to have **{GetMarkName(r)}**!";
        }
        return set;
    }        

    public static string GetMarkName(IRibbonIndex pk)
    {
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
                return GameInfo.Strings.Ribbons.GetName($"Ribbon{mark}");
        }
        return "";
    }

    // Quite ugly solution to display DescriptionAttribute
    private static string Convert<T>(T value) where T : Enum
    {
        var name = Enum.GetName(typeof(T), value);
        if (string.IsNullOrWhiteSpace(name))
            return value.ToString();

        var fieldInfo = typeof(T).GetField(name);
        if (fieldInfo == null)
            return value.ToString();

        return Attribute.GetCustomAttribute(fieldInfo, typeof(DescriptionAttribute)) is DescriptionAttribute dna
            ? dna.Description
            : value.ToString();
    }
}

public enum TargetAbilityType
{
    Any,            // Doesn't care
    First,          // Match first only
    Second,         // Match second only
    Hidden,         // Match hidden only
}

public enum TargetShinyType
{
    DisableOption,  // Doesn't care
    NonShiny,       // Match nonshiny only
    AnyShiny,       // Match any shiny regardless of type
    StarOnly,       // Match star shiny only
    SquareOnly,     // Match square shiny only
}

public enum TargetGenderType
{
    Any,            // Doesn't care
    Male,           // Match male only
    Female,         // Match female only
    Genderless,     // Match genderless only
}

public enum TargetAlphaType
{
    DisableOption,  // Doesn't care
    NonAlpha,       // Match non alpha only
    AnyAlpha,       // Match alpha only
}

public enum TargetFlawlessIVsType
{
    Disabled,
    [Description("0")] _0,
    [Description("1")] _1,
    [Description("2")] _2,
    [Description("3")] _3,
    [Description("4")] _4,
    [Description("5")] _5,
    [Description("6")] _6,
}
