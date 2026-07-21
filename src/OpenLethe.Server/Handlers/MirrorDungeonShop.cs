using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of server/src/api/md/{purchase_heal,purchase_ego_gift,sell_ego_gift,
/// upgrade_ego_gift,acquire_reward_ego_gifts,reject_reward_ego_gifts,
/// acquire_mirror_dungeon_exit_reward,select_formation,purchase_formation}_mirror_dungeon.rs.
/// Save state lives in Account.MdSaveInfo as a server Wire MirrorOriginSaveInfo (see
/// MirrorDungeon.cs for enter/re-enter).
public static class MirrorDungeonShopEndpoints
{
    public static IEndpointRouteBuilder MapMirrorDungeonShop(this IEndpointRouteBuilder app)
    {
        var purchaseHealId = global::PacketRouting.ResolvePacketId<global::ResPacket_PurchaseHealMirrorDungeon>();
        var purchaseEgoGiftId = global::PacketRouting.ResolvePacketId<global::ResPacket_PurchaseEgoGiftMirrorDungeon>();
        var sellEgoGiftId = global::PacketRouting.ResolvePacketId<global::ResPacket_SellEgoGiftMirrorDungeon>();
        var upgradeEgoGiftId = global::PacketRouting.ResolvePacketId<global::ResPacket_UpgradeEgoGiftMirrorDungeon>();
        var acquireRewardEgoGiftsId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireRewardEgoGiftsMirrorDungeon>();
        var rejectRewardEgoGiftsId = global::PacketRouting.ResolvePacketId<global::ResPacket_RejectRewardEgoGiftsMirrorDungeon>();
        var acquireExitRewardId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireMirrorDungeonExitReward>();
        var purchaseFormationId = global::PacketRouting.ResolvePacketId<global::ResPacket_PurchaseFormationMirrorDungeon>();
        global::PacketIds.TryGet("SelectFormationMirrorDungeon", out var selectFormationId);

        app.MapPost("/api/PurchaseHealMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<PurchaseHealMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            // TODO (mirrors Rust): bandaid until real max-health values exist.
            switch (p.idx)
            {
                case 0:
                {
                    save.currentInfo.cost -= 100;
                    var unit = save.currentInfo.dul.FirstOrDefault(u => u.pid == p.pid);
                    if (unit is not null) { unit.ch += 100; unit.cm += 30; }
                    account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
                    await HandlerContext.SaveAsync(ctx);
                    var result0 = new PurchaseHealMirrorDungeonResult
                    {
                        cost = save.currentInfo.cost,
                        dungeonUnitList = save.currentInfo.dul,
                        shopInfo = save.currentInfo.shop,
                        usedcost = 100,
                    };
                    return Results.Json(global::ResponsePacket<PurchaseHealMirrorDungeonResult>.Ok(result0, purchaseHealId), global::PacketJson.Options);
                }
                case 1:
                {
                    save.currentInfo.cost -= 100;
                    foreach (var unit in save.currentInfo.dul) { unit.ch += 30; unit.cm += 15; }
                    account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
                    await HandlerContext.SaveAsync(ctx);
                    var result1 = new PurchaseHealMirrorDungeonResult
                    {
                        cost = save.currentInfo.cost,
                        dungeonUnitList = save.currentInfo.dul,
                        shopInfo = save.currentInfo.shop,
                        usedcost = 100,
                    };
                    return Results.Json(global::ResponsePacket<PurchaseHealMirrorDungeonResult>.Ok(result1, purchaseHealId), global::PacketJson.Options);
                }
                default:
                    // No matching branch in Rust: default response, no save.
                    return Results.Json(global::ResponsePacket<PurchaseHealMirrorDungeonResult>.Ok(new PurchaseHealMirrorDungeonResult(), purchaseHealId), global::PacketJson.Options);
            }
        });

        app.MapPost("/api/PurchaseEgoGiftMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<PurchaseEgoGiftMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var shop = save.currentInfo.shop;
            long usedCost = 0;
            if (p.idx >= 0 && p.idx < shop.egpool.Count)
            {
                var selectedId = shop.egpool[(int)p.idx];
                var bought = MdEgoData.GetById(selectedId);
                if (bought is not null)
                {
                    save.currentInfo.cost -= bought.price;
                    usedCost = bought.price;
                    shop.peg.Add(bought.id);
                    save.currentInfo.egs.Add(new AcquiredEgogifts { id = bought.id });
                }
            }

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new PurchaseEgoGiftMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                egogifts = save.currentInfo.egs,
                shopInfo = shop,
                dungeonUnitList = save.currentInfo.dul,
                usedcost = usedCost,
            };
            return Results.Json(global::ResponsePacket<PurchaseEgoGiftMirrorDungeonResult>.Ok(result, purchaseEgoGiftId), global::PacketJson.Options);
        });

        app.MapPost("/api/SellEgoGiftMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<SellEgoGiftMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var egs = save.currentInfo.egs;
            var index = egs.FindIndex(e => e.id == p.id);
            if (index >= 0)
            {
                var info = MdEgoData.GetById(egs[index].id);
                if (info is not null)
                {
                    save.currentInfo.cost += info.price / 2;
                    egs.RemoveAt(index);
                }
            }

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new SellEgoGiftMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                egogifts = egs,
                shopInfo = save.currentInfo.shop,
                dungeonUnitList = save.currentInfo.dul,
            };
            return Results.Json(global::ResponsePacket<SellEgoGiftMirrorDungeonResult>.Ok(result, sellEgoGiftId), global::PacketJson.Options);
        });

        app.MapPost("/api/UpgradeEgoGiftMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<UpgradeEgoGiftMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var egoInfo = MdEgoData.GetById(p.egoGiftId);
            if (egoInfo is null) return Results.StatusCode(500);
            var ego = save.currentInfo.egs.FirstOrDefault(e => e.id == p.egoGiftId);
            if (ego is null) return Results.StatusCode(500);

            long usedCost = 0;
            if (egoInfo.upgradeDataList is not null)
            {
                var desiredUl = ego.ul + 1;
                if (desiredUl < 0 || desiredUl >= egoInfo.upgradeDataList.Count) return Results.StatusCode(500);
                usedCost = MdEgoData.UpgradeCost(egoInfo.price, desiredUl);
                save.currentInfo.cost -= usedCost;
                ego.ul = desiredUl;
            }

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new UpgradeEgoGiftMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                egoGift = ego,
                dungeonUnitList = save.currentInfo.dul,
                usedcost = usedCost,
            };
            return Results.Json(global::ResponsePacket<UpgradeEgoGiftMirrorDungeonResult>.Ok(result, upgradeEgoGiftId), global::PacketJson.Options);
        });

        app.MapPost("/api/AcquireRewardEgoGiftsMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireRewardEgoGiftsMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var ev = save.currentInfo.rre.FirstOrDefault(e => e.rt == "GetEgogift");
            if (ev is null) return Results.StatusCode(500);
            var index = (int)(p.selectIndexList.Count > 0 ? p.selectIndexList[0] : 0);
            if (index < 0 || index >= ev.pool.Count) return Results.StatusCode(500);

            save.currentInfo.egs.Add(new AcquiredEgogifts { id = ev.pool[index] });
            save.currentInfo.rre.Clear();

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new AcquireRewardEgoGiftsMirrorDungeonResult
            {
                egoGifts = save.currentInfo.egs,
                remainRewardEvent = new(),
                dungeonUnitList = save.currentInfo.dul,
                saveinfo = save,
            };
            return Results.Json(global::ResponsePacket<AcquireRewardEgoGiftsMirrorDungeonResult>.Ok(result, acquireRewardEgoGiftsId), global::PacketJson.Options);
        });

        app.MapPost("/api/RejectRewardEgoGiftsMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            // Params are unused by Rust too - read+discard just to 400 on an unparseable body.
            var p = await HandlerContext.ReadParamsAsync<object>(ctx);
            if (p is null) return Results.BadRequest();

            save.currentInfo.rre.Clear();
            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new RejectRewardEgoGiftsMirrorDungeonResult
            {
                remainRewardEvent = new(),
                saveinfo = save,
            };
            return Results.Json(global::ResponsePacket<RejectRewardEgoGiftsMirrorDungeonResult>.Ok(result, rejectRewardEgoGiftsId), global::PacketJson.Options);
        });

        app.MapPost("/api/AcquireMirrorDungeonExitReward", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            // Rust just deletes the save unconditionally - no get_mdsaveinfo()? guard here.
            var p = await HandlerContext.ReadParamsAsync<object>(ctx);
            if (p is null) return Results.BadRequest();

            account.MdSaveInfo = "{}";
            await HandlerContext.SaveAsync(ctx);
            var result = new AcquireMirrorDungeonExitRewardResult();
            return Results.Json(global::ResponsePacket<AcquireMirrorDungeonExitRewardResult>.Ok(result, acquireExitRewardId), global::PacketJson.Options);
        });

        app.MapPost("/api/SelectFormationMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<SelectFormationMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var levelMap = AccountDefaults.DerivePersonalities(account.Personalities)
                .ToDictionary(x => x.personality_id, x => x.level);
            var dul = p.formation.Select(u => new Dungeonunitlist1
            {
                upidx = new(),
                mlos = 0,
                pid = u.nextPersonalityId,
                ch = 10000,
                cm = 0,
                mhos = 0,
                g = 0,
                l = levelMap.TryGetValue(u.nextPersonalityId, out var lv) ? lv : 60,
                es = u.egos.Select(e => new Egos { id = e.nextEgoId, g = 0, idx = 0 }).ToList(),
                isp = 0,
            }).ToList();

            save.currentInfo.dul = dul;
            save.currentInfo.startBufPoint = 120;
            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new SelectFormationMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<SelectFormationMirrorDungeonResult>.Ok(result, selectFormationId), global::PacketJson.Options);
        });

        app.MapPost("/api/PurchaseFormationMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<PurchaseFormationMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            const long usedCost = 100;
            var replaceMap = new Dictionary<long, Formation>();
            foreach (var f in p.formation) replaceMap[f.pervPersonalityId] = f; // last wins, matches Rust HashMap collect

            save.currentInfo.cost -= usedCost;
            foreach (var unit in save.currentInfo.dul)
            {
                if (!replaceMap.TryGetValue(unit.pid, out var replacement)) continue;
                var egoMap = new Dictionary<long, long>();
                foreach (var e in replacement.egos) egoMap[e.prevEgoId] = e.nextEgoId; // last wins
                unit.pid = replacement.nextPersonalityId;
                foreach (var ego in unit.es)
                    if (egoMap.TryGetValue(ego.id, out var newId)) ego.id = newId;
            }

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new PurchaseFormationMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                dungeonUnitList = save.currentInfo.dul,
                shopInfo = save.currentInfo.shop,
                prevUnitInfo = new(),
                usedcost = usedCost,
            };
            return Results.Json(global::ResponsePacket<PurchaseFormationMirrorDungeonResult>.Ok(result, purchaseFormationId), global::PacketJson.Options);
        });

        return app;
    }
}
