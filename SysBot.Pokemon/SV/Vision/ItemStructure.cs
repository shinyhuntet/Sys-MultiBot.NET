using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Pokemon;
using SysBot.Base;

namespace SysBot.Pokemon.SV.Vision;

public class ItemStructure
{
    protected readonly PokeTradeHub<PK9> Hub;
    protected readonly EncounterSettingsSV Settings;
    protected readonly ISwitchConnectionAsync SwitchConnection;
    private readonly PokeDataOffsetsSV Offsets = new();
    private SAV9SV SAV = new();
    public ItemStructure(PokeBotState cfg, PokeTradeHub<PK9> hub, ISwitchConnectionAsync switchConnection)
    {
        Hub = hub;
        Settings = Hub.Config.EncounterSV;
        SwitchConnection = switchConnection;
    }
    private async Task<SAV9SV> GetFakeTrainerSAVSV(CancellationToken token)
    {
        var sav = new SAV9SV();
        var info = sav.MyStatus;
        var read = await SwitchConnection.PointerPeek(info.Data.Length, Offsets.MyStatusPointer, token).ConfigureAwait(false);
        read.CopyTo(info.Data);
        return sav;
    }
    private async Task<List<InventoryPouch>> ReadItems(ulong ItemOffset, CancellationToken token)
    {
        SAV9SV TrainerSav = await GetFakeTrainerSAVSV(token).ConfigureAwait(false);
        while (ItemOffset == 0)
        {
            await Task.Delay(0_050, token).ConfigureAwait(false);
            ItemOffset = await SwitchConnection.PointerAll(Offsets.ItemBlock, token).ConfigureAwait(false);
        }
        var data = await SwitchConnection.ReadBytesAbsoluteAsync(ItemOffset, TrainerSav.Items.Data.Length, token).ConfigureAwait(false);
        data.CopyTo(TrainerSav.Items.Data);
        return TrainerSav.Inventory.ToList();
    }
    private int GetBallIndex(List<InventoryPouch> pouches)
    {
        int index = 0;
        foreach (var pouch in pouches)
        {
            if (pouch.Type == InventoryType.Balls)
                break;
            index++;
        }
        return index;
    }
    public async Task<(int, bool)> GetBall(CancellationToken token)
    {
        Ball TargetBall = Hub.Config.EncounterSV.TargetBall;
        if (BallExtensions.IsLegendBall(TargetBall))
            return (-1, false);
        ulong ItemOffset = await SwitchConnection.PointerAll(Offsets.ItemBlock, token).ConfigureAwait(false);
        var pouches = await ReadItems(ItemOffset, token).ConfigureAwait(false);
        var index = GetBallIndex(pouches);
        InventoryPouch BallPouch = pouches[index];
        var ItemBalls = BallPouch.Items.Where(z => z.Index > 0 && z.Count > 0).ToArray();
        ItemBalls = ItemBalls.OrderBy(z => BallCategoryDict[(ItemIndexBalls)z.Index]).ToArray();
        LogAllItems(ItemBalls, InventoryType.Balls);
        int CaptureBall = TargetBall == Ball.Strange ? 1785 : TargetBall == Ball.Beast ? 851 : TargetBall == Ball.Dream ? 576 : BallExtensions.IsApricornBall(TargetBall) || TargetBall == Ball.Sport ? (int)TargetBall + 475 : (int)TargetBall;
        foreach (var item in ItemBalls)
        {
            if (item.Index == CaptureBall)
            {
                var BallIndex = ItemBalls.ToList().IndexOf(item);
                bool Left = ItemBalls.Length - BallIndex < BallIndex;
                SwitchConnection.Log($"Target item is found!{Environment.NewLine}All Balls Count: {ItemBalls.Length}, Target ball: {GameInfo.GetStrings("en").itemlist[item.Index]}, Original index: {BallIndex}, index: {(Left ? ItemBalls.Length - BallIndex : BallIndex)}");
                if (Left)
                    BallIndex = ItemBalls.Length - BallIndex;
                return (BallIndex, Left);
            }
        }
        return (-1, false);
    }
    private void LogAllItems(InventoryItem[] items, InventoryType type)
    {
        var ItemList = GameInfo.GetStrings("en").itemlist;
        int ItemCount = 1;

        SwitchConnection.Log($"All Bag Items(Type: {type})");
        SwitchConnection.Log($"All Items Count is {items.Length}");
        foreach (var item in items)
        {
            SwitchConnection.Log($"Item {ItemCount}: {ItemList[item.Index]}(Item Number: {item.Index}), Count: {item.Count}");
            ItemCount++;
        }
    }
    private Dictionary<ItemIndexBalls, int> BallCategoryDict = new()
    {
        { ItemIndexBalls.Poke, 0 },
        { ItemIndexBalls.Great, 1 },
        { ItemIndexBalls.Ultra, 2 },
        { ItemIndexBalls.Master, 3 },
        { ItemIndexBalls.Premier, 4 },
        { ItemIndexBalls.Heal, 5 },
        { ItemIndexBalls.Net, 6 },
        { ItemIndexBalls.Nest, 7 },
        { ItemIndexBalls.Dive, 8 },
        { ItemIndexBalls.Dusk, 9 },
        { ItemIndexBalls.Timer, 10 },
        { ItemIndexBalls.Quick, 11 },
        { ItemIndexBalls.Repeat, 12 },
        { ItemIndexBalls.Luxury, 13 },
        { ItemIndexBalls.Fast, 14 },
        { ItemIndexBalls.Friend, 15 },
        { ItemIndexBalls.Lure, 16 },
        { ItemIndexBalls.Level, 17 },
        { ItemIndexBalls.Heavy, 18 },
        { ItemIndexBalls.Love, 19 },
        { ItemIndexBalls.Moon, 20 },
        { ItemIndexBalls.Dream, 21 },
        { ItemIndexBalls.Sport, 22 },
        { ItemIndexBalls.Safari, 23 },
        { ItemIndexBalls.Park, 24 },
        { ItemIndexBalls.Beast, 25 },
        { ItemIndexBalls.Cherish, 26 },
        { ItemIndexBalls.Strange, 27 },
    };
    private enum ItemIndexBalls : int
    {
        Master = 1,
        Ultra = 2,
        Great = 3,
        Poke = 4,
        Safari = 5,
        Net = 6,
        Dive = 7,
        Nest = 8,
        Repeat = 9,
        Timer = 10,
        Luxury = 11,
        Premier = 12,
        Dusk = 13,
        Heal = 14,
        Quick = 15,
        Cherish = 16,
        Fast = 492,
        Level = 493,
        Lure = 494,
        Heavy = 495,
        Love = 496,
        Friend = 497,
        Moon = 498,
        Sport = 499,
        Park = 500,
        Dream = 576,
        Beast = 851,
        Strange = 1785,
    }
}
