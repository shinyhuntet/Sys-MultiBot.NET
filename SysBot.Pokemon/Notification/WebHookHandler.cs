using PKHeX.Core;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using SysBot.Base;
using Discord.Rest;
using System.Collections.Specialized;
using Newtonsoft.Json;

namespace SysBot.Pokemon;

public class WebHookHandler<T> where T : PKM, new()
{
    private readonly HttpClient _client;

    private readonly PokeTradeHub<T> Hub;

    private readonly PokeRoutineType Type;

    public WebHookHandler(PokeTradeHub<T> hub, PokeRoutineType type)
    {
        Hub = hub;
        Type = type;
        _client = new HttpClient();
    }
    public async Task SendNotification(T pk, string Message, CancellationToken token, bool isStaic = false, bool isGift = false, int Encounter = 0, double Rate = 0)
    {        
        if (Hub.Config.Discord.EmbedResultChannels.List.Count <= 0) return;

        foreach(var channel in Hub.Config.Discord.EmbedResultChannels.List)
        {
            if (string.IsNullOrEmpty(channel.URL))
                continue;

            if (channel.Generation != pk.Context)
                continue;

            var embed = new TradeEmbedBuilder<T>(pk, Hub, channel, Type, isStaic, isGift, Encounter, Rate);
            var Webhook = new
            {
                username = channel.Name,
                avatar_url = $"https://raw.githubusercontent.com/Omni-KingZeno/HomeImages/refs/heads/main/Sprites/128x128/poke_capture_0251_000_uk_n_00000000_f_r.png",
                content = Message,
                embeds = new[] { embed.BuildObject() }
            };

            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(Webhook), Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(channel.URL.Trim(), content, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

    }    
}
