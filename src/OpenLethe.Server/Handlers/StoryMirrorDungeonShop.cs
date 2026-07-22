using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of server/src/api/storymirrordungeon/{purchase_ego_gift,sell_ego_gift,
/// upgrade_ego_gift,refresh_shop_ego_gifts,purchase_heal}_story_mirror_dungeon.rs.
/// Kept separate from StoryMirrorDungeon.cs (map/entry handlers), mirroring the plain-MD
/// MirrorDungeonMap.cs / MirrorDungeonShop.cs split. Save state lives in
/// account.StoryMdSaveInfo as a StoryMirrorSaveInfo; note `currentinfo` is all-lowercase here
/// (the plain-MD save uses `currentInfo`).
public static class StoryMirrorDungeonShopEndpoints
{
    public static IEndpointRouteBuilder MapStoryMirrorDungeonShop(this IEndpointRouteBuilder app)
    {
        var purchaseEgoGiftId = global::PacketRouting.ResolvePacketId<global::ResPacket_PurchaseEgoGiftStoryMirrorDungeon>();
        var sellEgoGiftId = global::PacketRouting.ResolvePacketId<global::ResPacket_SellEgoGiftStoryMirrorDungeon>();
        var upgradeEgoGiftId = global::PacketRouting.ResolvePacketId<global::ResPacket_UpgradeEgoGiftStoryMirrorDungeon>();
        var refreshShopId = global::PacketRouting.ResolvePacketId<global::ResPacket_RefreshShopEgoGiftsStoryMirrorDungeon>();
        var purchaseHealId = global::PacketRouting.ResolvePacketId<global::ResPacket_PurchaseHealStoryMirrorDungeon>();
        var acquireRewardId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireRewardEgoGiftsStoryMirrorDungeon>();
        var combineId = global::PacketRouting.ResolvePacketId<global::ResPacket_CombineEgoGiftStoryMirrorDungeon>();

        app.MapPost("/api/PurchaseEgoGiftStoryMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<PurchaseEgoGiftStoryMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var shop = save.currentinfo.shop;
            // Out-of-range index or unknown ego id -> silent no-op (Rust: no error branch).
            if (p.idx >= 0 && p.idx < shop.egpool.Count)
            {
                var selectedId = shop.egpool[(int)p.idx];
                var bought = MdEgoData.GetById(selectedId);
                if (bought is not null)
                {
                    save.currentinfo.cost -= bought.price;
                    shop.peg.Add(bought.id);
                    save.currentinfo.egs.Add(new AcquiredEgogifts { id = bought.id });
                }
            }

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new StoryMirrorDungeonShopEgoGiftResult
            {
                cost = save.currentinfo.cost,
                egogifts = save.currentinfo.egs,
                shopInfo = shop,
                dungeonUnitList = save.currentinfo.dul,
            };
            return Results.Json(global::ResponsePacket<StoryMirrorDungeonShopEgoGiftResult>.Ok(result, purchaseEgoGiftId), global::PacketJson.Options);
        });

        app.MapPost("/api/SellEgoGiftStoryMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<SellEgoGiftStoryMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var egs = save.currentinfo.egs;
            var index = egs.FindIndex(e => e.id == p.id);
            // Not found or unknown static-data id -> silent no-op.
            if (index >= 0)
            {
                var info = MdEgoData.GetById(egs[index].id);
                if (info is not null)
                {
                    save.currentinfo.cost += info.price / 2; // integer division, matches Rust
                    egs.RemoveAt(index);
                }
            }

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new StoryMirrorDungeonShopEgoGiftResult
            {
                cost = save.currentinfo.cost,
                egogifts = egs,
                shopInfo = save.currentinfo.shop,
                dungeonUnitList = save.currentinfo.dul,
            };
            return Results.Json(global::ResponsePacket<StoryMirrorDungeonShopEgoGiftResult>.Ok(result, sellEgoGiftId), global::PacketJson.Options);
        });

        app.MapPost("/api/UpgradeEgoGiftStoryMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<UpgradeEgoGiftStoryMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var egoInfo = MdEgoData.GetById(p.egoGiftId);
            if (egoInfo is null) return Results.StatusCode(500);
            var ego = save.currentinfo.egs.FirstOrDefault(e => e.id == p.egoGiftId);
            if (ego is null) return Results.StatusCode(500);

            // ponytail: preserving Rust's `if let Some(eg) = ego_info.upgrade_data_list` - a
            // null upgradeDataList has no else branch, so it silently succeeds with no change.
            if (egoInfo.upgradeDataList is not null)
            {
                var desiredUl = ego.ul + 1;
                if (desiredUl < 0 || desiredUl >= egoInfo.upgradeDataList.Count) return Results.StatusCode(500);
                save.currentinfo.cost -= MdEgoData.UpgradeCost(egoInfo.price, desiredUl);
                ego.ul = desiredUl;
            }

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new UpgradeEgoGiftStoryMirrorDungeonResult
            {
                cost = save.currentinfo.cost,
                egoGift = ego,
                dungeonUnitList = save.currentinfo.dul,
            };
            return Results.Json(global::ResponsePacket<UpgradeEgoGiftStoryMirrorDungeonResult>.Ok(result, upgradeEgoGiftId), global::PacketJson.Options);
        });

        app.MapPost("/api/RefreshShopEgoGiftsStoryMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            // p.keyword is declared because the client sends it, but Rust's handler never reads
            // it - only used here to 400 on an unparseable body.
            var p = await HandlerContext.ReadParamsAsync<RefreshShopEgoGiftsStoryMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var shop = save.currentinfo.shop;
            // ponytail (D2 guard): Rust does `4 - peg.len()` as an unguarded usize subtraction,
            // which underflows once more than 4 gifts have been bought (buy 4, refresh, buy
            // again) - a panic in debug and a near-usize::MAX allocation in release. Clamp to 0
            // instead; MdEgoData.GetRandomMdEgoGifts would otherwise throw on a negative count.
            var count = Math.Max(0, 4 - shop.peg.Count);
            var randomGifts = MdEgoData.GetRandomMdEgoGifts(count);
            shop.egpool = shop.peg.Concat(randomGifts).ToList();
            save.currentinfo.cost -= 15;

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new RefreshShopEgoGiftsStoryMirrorDungeonResult
            {
                cost = save.currentinfo.cost,
                shopInfo = shop,
            };
            return Results.Json(global::ResponsePacket<RefreshShopEgoGiftsStoryMirrorDungeonResult>.Ok(result, refreshShopId), global::PacketJson.Options);
        });

        app.MapPost("/api/PurchaseHealStoryMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<PurchaseHealStoryMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            // TODO (mirrors Rust): bandaid until real max-health values exist; ch/cm uncapped.
            switch (p.idx)
            {
                case 0:
                {
                    save.currentinfo.cost -= 100;
                    var unit = save.currentinfo.dul.FirstOrDefault(u => u.pid == p.pid);
                    if (unit is not null) { unit.ch += 100; unit.cm += 30; }
                    account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
                    await HandlerContext.SaveAsync(ctx);
                    var result0 = new PurchaseHealStoryMirrorDungeonResult
                    {
                        cost = save.currentinfo.cost,
                        dungeonUnitList = save.currentinfo.dul,
                        shopInfo = save.currentinfo.shop,
                    };
                    return Results.Json(global::ResponsePacket<PurchaseHealStoryMirrorDungeonResult>.Ok(result0, purchaseHealId), global::PacketJson.Options);
                }
                case 1:
                {
                    save.currentinfo.cost -= 100;
                    foreach (var unit in save.currentinfo.dul) { unit.ch += 30; unit.cm += 15; }
                    account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
                    await HandlerContext.SaveAsync(ctx);
                    var result1 = new PurchaseHealStoryMirrorDungeonResult
                    {
                        cost = save.currentinfo.cost,
                        dungeonUnitList = save.currentinfo.dul,
                        shopInfo = save.currentinfo.shop,
                    };
                    return Results.Json(global::ResponsePacket<PurchaseHealStoryMirrorDungeonResult>.Ok(result1, purchaseHealId), global::PacketJson.Options);
                }
                default:
                    // No matching arm in Rust: default/zeroed response, no save.
                    return Results.Json(global::ResponsePacket<PurchaseHealStoryMirrorDungeonResult>.Ok(new PurchaseHealStoryMirrorDungeonResult(), purchaseHealId), global::PacketJson.Options);
            }
        });

        app.MapPost("/api/AcquireRewardEgoGiftsStoryMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireRewardEgoGiftsStoryMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var rre = save.currentinfo.rre;
            var rewardEvent = rre.FirstOrDefault(e => e.rt == "GetEgogift");
            if (rewardEvent is null) return Results.StatusCode(500);

            var index = (int)p.selectIndexList.FirstOrDefault();
            // ponytail (D3 guard): Rust does `event.pool.get(index).unwrap()` where `index`
            // comes straight from the client's selectIndexList - a panic on an out-of-range
            // index. Return 500 instead; do NOT substitute a default id, that would invent a
            // reward the player never rolled.
            if (index < 0 || index >= rewardEvent.pool.Count) return Results.StatusCode(500);

            save.currentinfo.egs.Add(new AcquiredEgogifts { id = rewardEvent.pool[index] });
            // Rust clears the ENTIRE rre list on success, not just the matched entry.
            rre.Clear();

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var acquireResult = new AcquireRewardEgoGiftsStoryMirrorDungeonResult
            {
                egoGifts = save.currentinfo.egs,
                remainRewardEvent = save.currentinfo.rre,
                dungeonUnitList = save.currentinfo.dul,
            };
            return Results.Json(global::ResponsePacket<AcquireRewardEgoGiftsStoryMirrorDungeonResult>.Ok(acquireResult, acquireRewardId), global::PacketJson.Options);
        });

        // D1: Rust's combine handler first checks app_state.ego_gift_data.fixed_combinations
        // for a recipe matching the sorted material ids. That field is wired to
        // models/src/data/egogifts.rs:16 `Lazy::new(|| HashSet::new())` - a permanently EMPTY
        // stub, so the branch can never match; every combine falls through to the tier-table
        // path below. Omitting it is a code-size decision only, observable behaviour is
        // identical. Cycle 4e's MdEgoFusion.EgoRecipeMapping already holds the real
        // fixed-recipe data should anyone ever wire a non-stub set up.
        app.MapPost("/api/CombineEgoGiftStoryMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<StoryMirrorSaveInfo>(account.StoryMdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            // p.keyword is declared because the client sends it; Rust's handler never reads it.
            var p = await HandlerContext.ReadParamsAsync<CombineEgoGiftStoryMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var egs = save.currentinfo.egs;
            // Bulk filter (Rust `retain`): removes ALL copies of a duplicated material id.
            // This deliberately differs from the plain-MD combine in MirrorDungeonRewards.cs,
            // which removes only the FIRST match of each material id - do not "fix" one to
            // match the other, they are ports of two different Rust handlers.
            egs.RemoveAll(e => p.materialEgoGiftIds.Contains(e.id));

            // Sort the TIERS (not the ids - the id sort in Rust only fed the fixed-combination
            // branch we omitted above).
            var tiers = p.materialEgoGiftIds.Select(MdEgoFusion.TierToInt).OrderBy(t => t).ToList();

            // ponytail (D4 guard): Rust does
            // `combine_tier_table.first().cloned().expect("...")`. The data is present today
            // so this is unreachable, but a static-data change would turn it into a crash;
            // treating a null table as "no matching row" (-> tier 1) is exactly what an empty
            // table would yield anyway, so this costs nothing.
            var table = MdEgoFusion.CombineTierTable;
            var resultTier = 1L;
            if (table is not null)
            {
                if (tiers.Count == 2)
                {
                    var row = table.combineTwo.FirstOrDefault(c => c.aTier == tiers[0] && c.bTier == tiers[1]);
                    if (row is not null) resultTier = row.resultTier;
                }
                else if (tiers.Count == 3)
                {
                    var row = table.combineThree.FirstOrDefault(c => c.aTier == tiers[0] && c.bTier == tiers[1] && c.cTier == tiers[2]);
                    if (row is not null) resultTier = row.resultTier;
                }
                // any other material count -> resultTier stays 1
            }

            var candidates = MdEgoData.AllIds().Where(id => MdEgoFusion.TierToInt(id) == resultTier).ToList();
            // ponytail: 9020 is Rust's hard-coded fallback when no MD ego gift matches the
            // tier (the plain-MD combine's fallback is 9034 - different handler, do not unify).
            var resultId = candidates.Count > 0 ? candidates[Random.Shared.Next(candidates.Count)] : 9020;
            var resultGift = new AcquiredEgogifts { id = resultId };
            egs.Add(resultGift);

            account.StoryMdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var combineResult = new CombineEgoGiftStoryMirrorDungeonResult
            {
                resultEgoGift = resultGift,
                isSuccess = true,
                egoGifts = egs,
                dungeonUnitList = save.currentinfo.dul,
            };
            return Results.Json(global::ResponsePacket<CombineEgoGiftStoryMirrorDungeonResult>.Ok(combineResult, combineId), global::PacketJson.Options);
        });

        return app;
    }
}
