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

        return app;
    }
}
