using PKHeX.Core;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;
using System.Collections.Generic;
using System.Buffers.Binary;

namespace SysBot.Pokemon;

public sealed class CurryBotSWSH : EncounterBotSWSH
{
    private new readonly CurryBotSettings Settings;
    private readonly StopConditionSettings StopSettings;
    private int charizardclass;
    private int copperajahclass;
    private int milceryclass;
    private int wobbuffetclass;
    private int koffingclass;
    private byte[] BerryPouch = { 0 };
    private byte[] IngredientPouch = { 0 };
    private int curryCount = 0;
    private bool ScrollUpIngr;
    private bool ScrollUpBerry;
    private int IngredientCount;
    private int BerryCount;

    public CurryBotSWSH(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg, hub)
    {
        Settings = Hub.Config.EncounterSWSH.CurrySWSH;
        StopSettings = Hub.Config.StopConditions;
    }

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        if (StopSettings.SearchConditions.Any(f => f.IsEnabled && f.MarkOnly))
        {
            foreach (var condition in StopSettings.SearchConditions.Where(f => f.IsEnabled && f.MarkOnly))
                condition.MarkOnly = false;
        }
        
        Log("Logging berry and ingredient counts...");
        int ingrIndex = await GetIngredientIndex(token).ConfigureAwait(false);
        int berryIndex = await GetBerryIndex(token).ConfigureAwait(false);
        int berryCount = BerryCount;
        int ingredientCount = IngredientCount;
        await DoCurryMonEncounter(ingrIndex, berryIndex, berryCount, ingredientCount, token).ConfigureAwait(false);
    }
    
    private async Task DoCurryMonEncounter(int ingrIndex, int berryIndex, int berryCount, int ingredientCount, CancellationToken token)
    {
        bool firstRun = true;
        PK8? comparison = null;
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.CurryBot)
        {
            if (await IsOnOverworld(OverworldOffset, token).ConfigureAwait(false))
            {
                Log("Entering camp...");
                await Click(X, 2_000, token).ConfigureAwait(false);
                await Click(A, Settings.EnterCamp, token).ConfigureAwait(false);
            }

            if (!await LairStatusCheck(0xFF000000, 0x6B311300, token).ConfigureAwait(false)) // Check if camp screen.
                await Click(B, 2_000, token).ConfigureAwait(false);

            await CookingCurry(ingrIndex, berryIndex, berryCount, firstRun, token).ConfigureAwait(false);
            ingredientCount--;
            berryCount -= berryCount >= 10 ? 10 : 1;
            firstRun = false;

            Log("Checking for a camper...");
            PK8? camperMon = await GetCampPokemon(token).ConfigureAwait(false);
            if (camperMon != null && string.IsNullOrEmpty(camperMon.OriginalTrainerName) && camperMon.EncryptionConstant != comparison!.EncryptionConstant)
            {
                comparison = camperMon;
                await SetStick(RIGHT, 0, 30_000, 1_000, token).ConfigureAwait(false);
                await SetStick(RIGHT, 0, 0, 0_100, token).ConfigureAwait(false);
                Log($"New camper found on curry #{curryCount}.");
                var (stop, _) = await HandleEncounter(camperMon, token).ConfigureAwait(false);
                PokeDetail.ResetAssets();
                await PokeDetail.SetPokeDetail(camperMon, encounterCount, 0, token).ConfigureAwait(false);
                if (stop)
                    return;
            }
            else
            {
                Log($"No new campers on curry #{curryCount}...");
            }

            if (!Settings.RestorePouches && (ingredientCount <= 0 || berryCount <= 0))
            {
                Log("Ran out of ingredients to make curry. Stopping...");
                return;
            }
            else if (Settings.RestorePouches && (ingredientCount <= 1 || berryCount <= 10))
            {
                Log("Restoring ingredient and berry pouches...");
                berryCount = await SetBerriesCount(token).ConfigureAwait(false);
                ingredientCount = await SetIngredientsCount(token).ConfigureAwait(false);
            }
        }
    }
    private async Task<int> SetIngredientsCount(CancellationToken token)
    {
        var TrainerSav = new SAV8SWSH();
        var ballpouch = new InventoryPouch8(InventoryType.Ingredients, ItemStorage8SWSH.Instance, 999, 0, IngredientPouchInventoryLength);
        ballpouch.GetPouch(IngredientPouch);
        var OriginalItems = Array.ConvertAll(ballpouch.Items, item => item.Index);
        var Count = ballpouch.GiveItem(TrainerSav, (ushort)Settings.Ingredient);
        var data = new byte[IngredientPouch.Length];
        var writePouch = (InventoryItem8[])ballpouch.Items;
        var itemsize = sizeof(uint);
        for (int i = 0; i < writePouch.Length; i++)
        {
            var ItemIndex = i * itemsize;
            uint val = writePouch[i].GetValue(false, OriginalItems);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan()[ItemIndex..(ItemIndex + itemsize)], val);
        }
        await Connection.WriteBytesAsync(data, IngredientPouchOffset, token).ConfigureAwait(false);
        Log($"Target Ingredient is set completely!{Environment.NewLine}Ingredients: {Settings.Ingredient}, Count: {Count}");
        return Count;
    }
    private async Task<int> SetBerriesCount(CancellationToken token)
    {
        var TrainerSav = new SAV8SWSH();
        var berrypouch = new InventoryPouch8(InventoryType.Berries, ItemStorage8SWSH.Instance, 999, 0, BerryPouchInventoryLength);
        berrypouch.GetPouch(BerryPouch);
        var OriginalItems = Array.ConvertAll(berrypouch.Items, item => item.Index);
        var Count = berrypouch.GiveItem(TrainerSav, (ushort)Settings.Berry);
        var data = new byte[BerryPouch.Length];
        var writePouch = (InventoryItem8[])berrypouch.Items;
        var itemsize = sizeof(uint);
        for (int i = 0; i < writePouch.Length; i++)
        {
            var ItemIndex = i * itemsize;
            uint val = writePouch[i].GetValue(false, OriginalItems);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan()[ItemIndex..(ItemIndex + itemsize)], val);
        }
        await Connection.WriteBytesAsync(data, BerryPouchOffset, token).ConfigureAwait(false);
        Log($"Target Berry is set completely!{Environment.NewLine}Berry: {Settings.Berry}, Count: {Count}");
        return Count;
    }

    private async Task CookingCurry(int ingr, int berry, int berryCount, bool firstRun, CancellationToken token)
    {
        var sw = new Stopwatch();
        await Click(X, 0_500, token).ConfigureAwait(false);
        if (firstRun)
            await Click(DRIGHT, 0_250, token).ConfigureAwait(false);

        Log("Let's make curry!");
        await Click(A, 1_000, token).ConfigureAwait(false);
        await Click(A, 5_000, token).ConfigureAwait(false);

        Log("Selecting ingredients...");
        for (int i = 0; i < ingr; i++)
            await Click(ScrollUpIngr ? DUP : DDOWN, 0_150, token).ConfigureAwait(false);

        await Click(A, 1_000, token).ConfigureAwait(false);
        await Click(A, 3_000, token).ConfigureAwait(false);

        Log("Selecting berries...");
        for (int i = 0; i < berry; i++)
            await Click(ScrollUpBerry ? DUP : DDOWN, 0_200, token).ConfigureAwait(false);

        await Click(A, 1_000, token).ConfigureAwait(false);
        bool bunchOfBerries = berryCount >= 10;
        if (bunchOfBerries)
            await Click(DDOWN, 0_150, token).ConfigureAwait(false);

        await Click(A, bunchOfBerries ? 2_000 : 1_000, token).ConfigureAwait(false);
        if (!bunchOfBerries)
            await Click(PLUS, 1_000, token).ConfigureAwait(false);

        Log("Dropping ingredients in!");
        await Click(A, Settings.IngredientDrop, token).ConfigureAwait(false);

        Log("Time to cook!");
        sw.Start();
        while (sw.ElapsedMilliseconds < Settings.FanningDuration - 4_000)
            Click(A, 0_100, token).Wait(token);

        while (sw.ElapsedMilliseconds < Settings.FanningDuration)
            Click(A, 0_150, token).Wait(token);

        Log("Stirring the pot!");
        sw.Restart();
        while (sw.ElapsedMilliseconds < Settings.StirringDuration)
        {
            SetStick(RIGHT, -30_000, 0, 0_050, token).Wait(token); // ←
            SetStick(RIGHT, 0, 30_000, 0_050, token).Wait(token); // ↑
            SetStick(RIGHT, 30_000, 0, 0_050, token).Wait(token); // →
            SetStick(RIGHT, 0, -30_000, 0_050, token).Wait(token); // ↓
        }
        sw.Stop();

        await Task.Delay(Settings.SprinkleOfLove, token).ConfigureAwait(false);
        Log("Adding a sprinkle of love!");
        await Click(A, Settings.CurryChowCutscene, token).ConfigureAwait(false); // Delay until we can present our curry.
        await SetStick(RIGHT, 0, 0, 0_100, token).ConfigureAwait(false);

        Log("Presenting our curry!");
        curryCount++;
        Settings.AddCompletedCurries();
        string msg = await GetCurryRating(token).ConfigureAwait(false);
        Log($"The curry has a {msg} class taste rating!\n" +
            $"Current Cooking Session Totals\n" +
            $"- Charizard: {charizardclass}\n" +
            $"- Copperajah: {copperajahclass}\n" +
            $"- Milcery: {milceryclass}\n" +
            $"- Wobbuffet: {wobbuffetclass}\n" +
            $"- Koffing: {koffingclass}");

        await Click(A, 12_000, token).ConfigureAwait(false); // Delay until cutscene is over.
        await Click(A, 3_000, token).ConfigureAwait(false); // Delay until camp.
    }

    private async Task<PK8?> GetCampPokemon(CancellationToken token)
    {
        List<IReadOnlyList<long>> campers =
        [
            [0x2636120, 0x280, 0xD8, 0x78, 0x10, 0x98, 0x00],
            [0x2636170, 0x2F0, 0x58, 0x130, 0x138, 0xD0],
            [0x28ED668, 0x68, 0x1E8, 0x1D0, 0x128],
            [0x296C030, 0x60, 0x40, 0x1B0, 0x58, 0x00]
        ];
        for (int i = 0; i < campers.Count; i++)
        {
            var pointer = campers[i];
            var ofs = await SwitchConnection.PointerAll(pointer, token).ConfigureAwait(false);
            var pk = await ReadUntilPresentAbsolute(ofs, 0_500, 0_250, token).ConfigureAwait(false);
            if (pk != null)
                return pk;
        }
        return null;
    }

    private async Task<string> GetCurryRating(CancellationToken token)
    {
        var rating = await Connection.ReadBytesAsync(0x2C2A909E, 1, token).ConfigureAwait(false);
        string result = System.Text.Encoding.ASCII.GetString(rating);
        string[] expectedVals = { "m", "v", "g", "n", "b" };
        if (!expectedVals.Contains(result))
        {
            Log("Checking backup rating offset...");
            rating = await Connection.ReadBytesAsync(0x2C2B009E, 1, token).ConfigureAwait(false);
            result = System.Text.Encoding.ASCII.GetString(rating);
        }

        string message = string.Empty;
        switch (result)
        {
            case "m": message = "Charizard"; charizardclass++; break;
            case "v": message = "Copperajah"; copperajahclass++; break;
            case "g": message = "Milcery"; milceryclass++; break;
            case "n": message = "Wobbuffet"; wobbuffetclass++; break;
            case "b": message = "Koffing"; koffingclass++; break;
        };
        return message;
    }

    private async Task<int> GetIngredientIndex(CancellationToken token)
    {
        IngredientPouch = await Connection.ReadBytesAsync(IngredientPouchOffset, 100, token).ConfigureAwait(false);
        var pouch = GetItemPouch(IngredientPouch, InventoryType.Ingredients, 999, 0, ItemStorage8SWSH.Ingredients.Length);
        var item = pouch.Items.FirstOrDefault(x => x.Index == (int)Settings.Ingredient && x.Count > 0);
        if (item == default)
            item = pouch.Items.FirstOrDefault(x => x.Count > 0);

        IngredientCount = item!.Count;
        var index = pouch.Items.ToList().IndexOf(item);
        ScrollUpIngr = pouch.Items.Length - index < index;
        return ScrollUpIngr ? pouch.Items.Length - index : index;
    }

    private async Task<int> GetBerryIndex(CancellationToken token)
    {        
        BerryPouch = await Connection.ReadBytesAsync(BerryPouchOffset, 212, token).ConfigureAwait(false);
        var pouch = GetItemPouch(BerryPouch, InventoryType.Berries, 999, 0, ItemStorage8SWSH.Berry.Length);
        var item = pouch.Items.FirstOrDefault(x => x.Index == (int)Settings.Berry && x.Count > 0);
        if (item == default)
            item = pouch.Items.FirstOrDefault(x => x.Count >= 10);

        BerryCount = item!.Count;
        var index = pouch.Items.ToList().IndexOf(item);
        ScrollUpBerry = pouch.Items.Length - index < index;
        return ScrollUpBerry ? pouch.Items.Length - index : index;
    }

    private static InventoryPouch8 GetItemPouch(byte[] data, InventoryType type, int maxCount, int offset, int length)
    {
        var pouch = new InventoryPouch8(type, ItemStorage8SWSH.Instance, maxCount, offset, length);
        pouch.GetPouch(data);
        return pouch;
    }
}
