using Discord;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SysBot.Pokemon;

public class TradeEmbedBuilder<T>(T PKM, PokeTradeHub<T> Hub, RemoteControlAccess Channel, PokeRoutineType rType, bool isStaic = false, bool isGift = false, int Encounter = 0, double Rate = 0) where T : PKM, new()
{
    private bool Initialized { get; set; } = false;
    public EmbedBuilder Builder { get; init; } = new();
    private PKMStringWrapper<T> Strings { get; init; } = new(PKM, Hub.Config.Discord.TradeEmbedSettings, rType);

    public Embed Build()
    {
        if (!Initialized)
            InitializeEmbed();

        return Builder.Build();
    }
    public object BuildObject()
    {
        if (!Initialized)
        {
            if (Hub.Config.Discord.TradeEmbedSettings.ShowDetail)
                InitializeEmbedManu();
            else
                InitializeEmbed();
        }

        var fields = new List<object>();
        foreach (var field in Builder.Fields)
        {
            fields.Add(new
            {
                name = field.Name,
                value = field.Value.ToString(),
                inline = field.IsInline
            });
        }
        
        var embed = new
        {
            color = Builder.Color.GetValueOrDefault().RawValue,
            title = Builder.Title,
            author = new
            {
                name = Builder.Author.Name,
                icon_url = Builder.Author.IconUrl,
            },
            footer = new
            {
                text = Builder.Footer.Text,
                icon_url = Builder.Footer.IconUrl
            },
            fields,
            image = new
            {
                url = Builder.ImageUrl
            },
            thumbnail = new
            {
                url = Builder.ThumbnailUrl
            },
            description = Builder.Description,
            timestamp = Builder.Timestamp.GetValueOrDefault().DateTime.ToString("yyyy-MM-dd HH:mm:ss"),
        };
        return embed;
    }
    public void InitializeEmbedManu()
    {
        const string UNSET = "** **";

        var altStyle = Hub.Config.Discord.TradeEmbedSettings.UseAlternateLayout;
        Builder.Title = isStaic || isGift || PKM.IsEgg  ? $"{(isStaic ? "Static" : isGift ? "Mystery Gift" : "Egg")} Pokemon Found!{(Encounter > 0 ? $"{Environment.NewLine}Encounter #{Encounter}" : "")}{(Rate <= 0 ? "" : $"{Environment.NewLine}Target Rate: {Rate:0.00}%")}" : "Unwanted match..";
        Builder.Color = InitializeColor();
        Builder.Author = InitializeAuthor();
        Builder.Footer = InitializeFooter();
        if (altStyle)
        {
            Builder.ImageUrl = Strings.GetImageURL();
            Builder.ThumbnailUrl = Strings.GetThumbnailURL();
        }
        else
        {
            Builder.ImageUrl = string.Empty;
            Builder.ThumbnailUrl = Strings.GetImageURL();
        }

        // Set the Pokémon Species as Embed Title
        var mark = Strings.Mark;
        var fieldName = mark.HasMark switch
        {
            true => $"{Strings.Shiny} {Strings.Species} {Strings.Gender} {mark.Title}",
            _ => $"{Strings.Shiny} {Strings.Species} {Strings.Gender}",
        };

        Builder.AddField(x =>
        {
            x.Name = fieldName;
            x.Value = UNSET;
            x.IsInline = false;
        });

        // Add Pokémon Held Item, if any
        if (Strings.HasItem)
        {
            Builder.AddField(x =>
            {
                x.Name = $"**Held Item**: {Strings.HeldItem}";
                x.Value = UNSET;
                x.IsInline = false;
            });
        }

        // Add general Pokémon informations
        var fieldValue = $"**Level:** {PKM.CurrentLevel}{Environment.NewLine}" +
                         $"**Ability:** {Strings.Ability}{Environment.NewLine}" +
                         $"**Nature:** {Strings.Nature}{(PKM is IScaledSize or IScaledSize3 ? $"{Environment.NewLine}" : "")}" +
                         $"{(PKM is IScaledSize or IScaledSize3 ? $"**Scale:** {Strings.Scale}" : "")}" +
                         $"{((PKM is PK8 or PB8) && (PKM is IScaledSize pks) ? $"{Environment.NewLine}**Height:** {pks.HeightScalar}" : "")}";

        if (PKM is PA9 pa9)
        {
            string IsAlpha = pa9.IsAlpha ? "Yes" : "No";
            fieldValue += $"{Environment.NewLine}**Alpha:** {IsAlpha}";
        }
        else if (PKM is PA8 pa8)
        {
            string IsAlpha = pa8.IsAlpha ? "Yes" : "No";
            fieldValue += $"{Environment.NewLine}**Alpha:** {IsAlpha}";
        }        
        else if(PKM is PK8 pk8)
        {
            string CanGmax = pk8.CanGigantamax ? "Yes" : "No";
            CanGmax += $"{Environment.NewLine}**Dynamax Level:** {pk8.DynamaxLevel}";
            fieldValue += $"{Environment.NewLine}**Gigantamax:** {CanGmax}";
        }

        if (Strings.HasTeraType)
            fieldValue += $"{Environment.NewLine}**Tera Type:** {Strings.TeraType}";

        if (mark.HasMark)
            fieldValue += $"{Environment.NewLine}**Mark:** {mark.Name}";

        Builder.AddField(x =>
        {
            x.Name = "Pokémon Info:";
            x.Value = fieldValue;
            x.IsInline = true;
        });

        // Empty Field, so we build a two-column layout
        var unsetField = new EmbedFieldBuilder
        {
            Name = UNSET,
            Value = UNSET,
            IsInline = true
        };
        Builder.AddField(unsetField);

        // Add Pokémon Moveset
        Builder.AddField(x =>
        {
            x.Name = "Moveset:";
            x.Value = string.Join(Environment.NewLine, Strings.Moves);
            x.IsInline = true;
        });

        //Add Pokémon IVs and EVs, if enabled
        if (Hub.Config.Discord.TradeEmbedSettings.ShowIVsAndEVs)
        {
            var ivs = $"**HP:** {PKM.IV_HP}{Environment.NewLine}" +
                      $"**Atk:** {PKM.IV_ATK}{Environment.NewLine}" +
                      $"**Def:** {PKM.IV_DEF}{Environment.NewLine}" +
                      $"**SpA:** {PKM.IV_SPA}{Environment.NewLine}" +
                      $"**SpD:** {PKM.IV_SPD}{Environment.NewLine}" +
                      $"**Spe:** {PKM.IV_SPE}{Environment.NewLine}";

            Builder.AddField(x =>
            {
                x.Name = "Pokémon IVs:";
                x.Value = ivs;
                x.IsInline = true;
            });

            Builder.AddField(unsetField); // Add empty field for two-column layout

            var evs = $"**HP:** {PKM.EV_HP}{Environment.NewLine}" +
                      $"**Atk:** {PKM.EV_ATK}{Environment.NewLine}" +
                      $"**Def:** {PKM.EV_DEF}{Environment.NewLine}" +
                      $"**SpA:** {PKM.EV_SPA}{Environment.NewLine}" +
                      $"**SpD:** {PKM.EV_SPD}{Environment.NewLine}" +
                      $"**Spe:** {PKM.EV_SPE}";

            Builder.AddField(x =>
            {
                x.Name = "Pokémon EVs:";
                x.Value = evs;
                x.IsInline = true;
            });
        }

        Builder.Timestamp = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow.DateTime, TimeZoneInfo.Local);

        Initialized = true;
    }
    public void InitializeEmbed()
    {
        // Embed layout Style
        var altStyle = Hub.Config.Discord.TradeEmbedSettings.UseAlternateLayout;
        Builder.Title = isStaic || isGift || PKM.IsEgg ? $"{(isStaic ? "Static" : isGift ? "Mystery Gift" : "Egg")} Pokemon Found!{(Encounter > 0 ? $"{Environment.NewLine}Encounter #{Encounter}" : "")}{(Rate <= 0 ? "" : $"{Environment.NewLine}Target Rate: {Rate:0.00}%")}" : "Unwanted match..";
        Builder.Color = InitializeColor();
        Builder.Author = InitializeAuthor();
        Builder.Footer = InitializeFooter();
        if (altStyle)
        {
            Builder.ImageUrl = Strings.GetImageURL();
            Builder.ThumbnailUrl = Strings.GetThumbnailURL();
        }
        else
        {
            Builder.ImageUrl = string.Empty;
            Builder.ThumbnailUrl = Strings.GetImageURL();
        }

        // Build field value based on EmbedDisplayedInfo setting
        var fieldValue = "";
        var moves = "";
        var displayedInfo = Hub.Config.Discord.TradeEmbedSettings.EmbedDisplayedInfo;

        foreach (var info in displayedInfo)
        {
            var line = GetDisplayInfoLine(info);
            if (!string.IsNullOrEmpty(line))
            {
                if (info == DisplayedInfo.Moves)
                {
                    moves = line; // Store moves separately for alternate layout
                }
                else
                {
                    fieldValue += line + Environment.NewLine;
                }
            }
        }

        if (altStyle)
        {
            Builder.AddField(x =>
            {
                x.Name = "__Details:__";
                x.Value = fieldValue;
                x.IsInline = true;
            });

            if (!string.IsNullOrEmpty(moves))
            {
                Builder.AddField(x =>
                {
                    x.Name = "__Moves:__";
                    x.Value = moves;
                    x.IsInline = true;
                });
            }

        }
        else
        {
            Builder.Description = fieldValue += moves;

        }

        Builder.Timestamp = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow.DateTime, TimeZoneInfo.Local);

        Initialized = true;
    }

    private string GetDisplayInfoLine(DisplayedInfo info)
    {
        return info switch
        {
            DisplayedInfo.Ability => $"**Ability:** {Strings.Ability}",

            DisplayedInfo.Alpha when PKM is IAlpha alpha && alpha.IsAlpha => "**Alpha:** Yes",

            DisplayedInfo.AVs when PKM is IAwakened awakened => GetAwakenedValuesString(awakened),

            DisplayedInfo.Ball => $"**Ball:** {GameInfo.Strings.balllist[PKM.Ball]}",

            DisplayedInfo.EVs => GetEVString(),

            DisplayedInfo.Form when PKM.Form > 0 && Strings.HasForm => $"**Form:** {Strings.Form}",

            DisplayedInfo.Friendship => $"**Friendship:** {PKM.CurrentFriendship}",

            DisplayedInfo.Gigantamax when PKM is IGigantamax gmax && gmax.CanGigantamax => "**Gigantamax:** Yes",

            DisplayedInfo.DmaxLevel when PKM is PK8 dmax => $"**Dynamax Level:** {dmax.DynamaxLevel}",

            DisplayedInfo.GVs when PKM is IGanbaru ganbaru => GetGanbaruValuesString(ganbaru),

            DisplayedInfo.Height when PKM is IScaledSize scaled => $"**Height:** {scaled.HeightScalar}",

            DisplayedInfo.HeldItem when Strings.HasItem => $"**Held Item:** {Strings.HeldItem}",

            DisplayedInfo.IVs => GetIVString(),

            DisplayedInfo.Language => $"**Language:** {(LanguageID)PKM.Language}",

            DisplayedInfo.Level => $"**Level:** {PKM.CurrentLevel}",

            DisplayedInfo.Mark when Strings.Mark.HasMark => $"**Mark:** {Strings.Mark.Name}",

            DisplayedInfo.Moves => string.Join(Environment.NewLine, Strings.Moves),

            DisplayedInfo.Nature => $"**Nature:** {Strings.Nature}",

            DisplayedInfo.Nickname when !string.IsNullOrEmpty(PKM.Nickname) && PKM.Nickname != GameInfo.Strings.Species[PKM.Species] => $"**Nickname:** {PKM.Nickname}",

            DisplayedInfo.Scale => $"**Scale:** {Strings.Scale}",

            DisplayedInfo.Shiny when PKM.IsShiny => $"**Shiny:** {(PKM is PK8 ? PKM.ShinyXor == 0 ? "Square" : "Star" : "Yes")}",

            DisplayedInfo.Species => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender}**",

            DisplayedInfo.SpeciesForm when Strings.HasForm => $"**{Strings.Shiny}{Strings.Species}-{Strings.Form}{Strings.Gender}**",
            DisplayedInfo.SpeciesForm => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender}**",

            DisplayedInfo.SpeciesHeldItem when Strings.HasItem => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender} ➜ {Strings.HeldItem}**",
            DisplayedInfo.SpeciesHeldItem => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender}**",

            DisplayedInfo.SpeciesFormHeldItem when Strings.HasForm && Strings.HasItem => $"**{Strings.Shiny}{Strings.Species}-{Strings.Form}{Strings.Gender} ➜ {Strings.HeldItem}**",
            DisplayedInfo.SpeciesFormHeldItem when Strings.HasForm => $"**{Strings.Shiny}{Strings.Species}-{Strings.Form}{Strings.Gender}**",
            DisplayedInfo.SpeciesFormHeldItem when Strings.HasItem => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender} ➜ {Strings.HeldItem}**",
            DisplayedInfo.SpeciesFormHeldItem => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender}**",

            DisplayedInfo.SpeciesMark when Strings.Mark.HasMark => $"**{Strings.Shiny}{Strings.Species}{Strings.Mark.Title}{Strings.Gender}**",
            DisplayedInfo.SpeciesMark => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender}**",

            DisplayedInfo.SpeciesFormMark when Strings.HasForm && Strings.Mark.HasMark => $"**{Strings.Shiny}{Strings.Species}-{Strings.Form}{Strings.Mark.Title}{Strings.Gender}**",
            DisplayedInfo.SpeciesFormMark when Strings.HasForm => $"**{Strings.Shiny}{Strings.Species}-{Strings.Form}{Strings.Gender}**",
            DisplayedInfo.SpeciesFormMark when Strings.Mark.HasMark => $"**{Strings.Shiny}{Strings.Species}{Strings.Mark.Title}{Strings.Gender}**",
            DisplayedInfo.SpeciesFormMark => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender}**",

            DisplayedInfo.SpeciesMarkHeldItem when Strings.Mark.HasMark && Strings.HasItem => $"**{Strings.Shiny}{Strings.Species}{Strings.Mark.Title}{Strings.Gender} ➜ {Strings.HeldItem}**",
            DisplayedInfo.SpeciesMarkHeldItem when Strings.Mark.HasMark => $"**{Strings.Shiny}{Strings.Species}{Strings.Mark.Title}{Strings.Gender}**",
            DisplayedInfo.SpeciesMarkHeldItem when Strings.HasItem => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender} ➜ {Strings.HeldItem}**",
            DisplayedInfo.SpeciesMarkHeldItem => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender}**",

            DisplayedInfo.SpeciesFormMarkHeldItem when Strings.HasForm && Strings.Mark.HasMark && Strings.HasItem => $"**{Strings.Shiny}{Strings.Species}-{Strings.Form}{Strings.Mark.Title}{Strings.Gender} ➜ {Strings.HeldItem}**",
            DisplayedInfo.SpeciesFormMarkHeldItem when Strings.Mark.HasMark && Strings.HasItem => $"**{Strings.Shiny}{Strings.Species}{Strings.Mark.Title}{Strings.Gender} ➜ {Strings.HeldItem}**",
            DisplayedInfo.SpeciesFormMarkHeldItem when Strings.HasForm && Strings.Mark.HasMark => $"**{Strings.Shiny}{Strings.Species}-{Strings.Form}{Strings.Mark.Title}{Strings.Gender}**",
            DisplayedInfo.SpeciesFormMarkHeldItem when Strings.HasForm && Strings.HasItem => $"**{Strings.Shiny}{Strings.Species}-{Strings.Form}{Strings.Gender} ➜ {Strings.HeldItem}**",
            DisplayedInfo.SpeciesFormMarkHeldItem when Strings.Mark.HasMark => $"**{Strings.Shiny}{Strings.Species}{Strings.Mark.Title}{Strings.Gender}**",
            DisplayedInfo.SpeciesFormMarkHeldItem when Strings.HasItem => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender} ➜ {Strings.HeldItem}**",
            DisplayedInfo.SpeciesFormMarkHeldItem when Strings.HasForm => $"**{Strings.Shiny}{Strings.Species}-{Strings.Form}{Strings.Gender}**",
            DisplayedInfo.SpeciesFormMarkHeldItem => $"**{Strings.Shiny}{Strings.Species}{Strings.Gender}**",

            DisplayedInfo.StatNature when PKM.StatNature != PKM.Nature => $"**Stat Nature:** {PKM.StatNature}",

            DisplayedInfo.Sweet when PKM.Species is (ushort)Species.Alcremie => $"**Sweet:** {Strings.FormArgument}",

            DisplayedInfo.TeraType when Strings.HasTeraType => $"**Tera Type:** {Strings.TeraType}",

            DisplayedInfo.TeraTypeOverride when PKM is ITeraType tera && tera.TeraTypeOverride != tera.TeraType => $"**Tera Type Override:** {tera.TeraTypeOverride}",

            DisplayedInfo.Weight when PKM is IScaledSize scaled => $"**Weight:** {scaled.WeightScalar}",

            _ => ""
        };
    }

    private string GetIVString()
    {
        int[] ivs = [PKM.IV_HP, PKM.IV_ATK, PKM.IV_DEF, PKM.IV_SPE, PKM.IV_SPA, PKM.IV_SPD];
        string[] statNames = ["HP", "ATK", "DEF", "SPE", "SpA", "SpD"];
        bool ivsHyperTrained = false;
        List<string> ivList = [];

        for (int i = 0; i < 6; i++)
        {
            if (ivs[i] < 31)
            {
                bool isHT = PKM is IHyperTrain ht && ht.IsHyperTrained(i);
                if (isHT)
                {
                    ivsHyperTrained = true;
                }
                else
                {
                    ivList.Add($"{ivs[i]} {statNames[i]}");
                }
            }
        }

        if (ivList.Count == 0 && !ivsHyperTrained)
            return "**IVs:** 6IV";

        if (ivList.Count == 0 && ivsHyperTrained)
            return "**IVs:** 6IV (HyperTrained)";

        string ivString = string.Join(" / ", ivList);
        if (ivsHyperTrained)
            ivString += " (HyperTrained)";

        return "**IVs:** " + ivString;
    }

    private string GetEVString()
    {
        List<string> evList =
        [
            PKM.EV_HP  > 0 ? $"{PKM.EV_HP} HP" : "",
            PKM.EV_ATK > 0 ? $"{PKM.EV_ATK} Atk" : "",
            PKM.EV_DEF > 0 ? $"{PKM.EV_DEF} Def" : "",
            PKM.EV_SPA > 0 ? $"{PKM.EV_SPA} SpA" : "",
            PKM.EV_SPD > 0 ? $"{PKM.EV_SPD} SpD" : "",
            PKM.EV_SPE > 0 ? $"{PKM.EV_SPE} Spe" : "",
        ];
        evList = [.. evList.Where(s => !string.IsNullOrEmpty(s))];
        return evList.Count == 0 ? "" : "**EVs:** " + string.Join(" / ", evList);
    }

    private string GetAwakenedValuesString(IAwakened awakened)
    {
        List<string> avList =
        [
            awakened.AV_HP  > 0 ? $"{awakened.AV_HP} HP" : "",
            awakened.AV_ATK > 0 ? $"{awakened.AV_ATK} Atk" : "",
            awakened.AV_DEF > 0 ? $"{awakened.AV_DEF} Def" : "",
            awakened.AV_SPA > 0 ? $"{awakened.AV_SPA} SpA" : "",
            awakened.AV_SPD > 0 ? $"{awakened.AV_SPD} SpD" : "",
            awakened.AV_SPE > 0 ? $"{awakened.AV_SPE} Spe" : "",
        ];
        avList = [.. avList.Where(s => !string.IsNullOrEmpty(s))];
        return avList.Count == 0 ? "" : "**AVs:** " + string.Join(" / ", avList);
    }

    private string GetGanbaruValuesString(IGanbaru ganbaru)
    {
        List<string> gvList =
        [
            ganbaru.GV_HP  > 0 ? $"{ganbaru.GV_HP} HP" : "",
            ganbaru.GV_ATK > 0 ? $"{ganbaru.GV_ATK} Atk" : "",
            ganbaru.GV_DEF > 0 ? $"{ganbaru.GV_DEF} Def" : "",
            ganbaru.GV_SPA > 0 ? $"{ganbaru.GV_SPA} SpA" : "",
            ganbaru.GV_SPD > 0 ? $"{ganbaru.GV_SPD} SpD" : "",
            ganbaru.GV_SPE > 0 ? $"{ganbaru.GV_SPE} Spe" : "",
        ];
        gvList = [.. gvList.Where(s => !string.IsNullOrEmpty(s))];
        return gvList.Count == 0 ? "" : "**GVs:** " + string.Join(" / ", gvList);
    }

    private Discord.Color InitializeColor() =>
        EmbedColorHelper.GetDiscordColor(PKM.IsShiny ? EmbedColorHelper.ShinyMap[((Species)PKM.Species, PKM.Form)] : (PersonalColor)PKM.PersonalInfo.Color);

    private EmbedAuthorBuilder InitializeAuthor() => new()
    {
        Name = Strings.GetAuthorText(Channel.Name),
        IconUrl = Strings.GetBallImageURL(),
    };

    private EmbedFooterBuilder InitializeFooter()
    {        
        string footerText = $"OT: {PKM.OriginalTrainerName}{Environment.NewLine}" +
                      $"TID: {PKM.DisplayTID} | SID: {PKM.DisplaySID}";

        var imgURL = Strings.GetMarkImageURL();
        return new EmbedFooterBuilder { Text = footerText, IconUrl = imgURL };
    }
}

public record QueueUser(ulong UID, string Username);
