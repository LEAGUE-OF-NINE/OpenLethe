using OpenLethe.Server.MirrorDungeon.Mapping;
using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.MirrorDungeon.Rules;
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
        var purchaseUpgradePersonalityId = global::PacketRouting.ResolvePacketId<global::ResPacket_PurchaseUpgradePersonalityMirrorDungeon>();
        var sellEgoGiftId = global::PacketRouting.ResolvePacketId<global::ResPacket_SellEgoGiftMirrorDungeon>();
        var upgradeEgoGiftId = global::PacketRouting.ResolvePacketId<global::ResPacket_UpgradeEgoGiftMirrorDungeon>();
        var acquireRewardEgoGiftsId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireRewardEgoGiftsMirrorDungeon>();
        var rejectRewardEgoGiftsId = global::PacketRouting.ResolvePacketId<global::ResPacket_RejectRewardEgoGiftsMirrorDungeon>();
        var acquireExitRewardId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireMirrorDungeonExitReward>();
        var previewExitRewardId = global::PacketRouting.ResolvePacketId<global::ResPacket_PreviewMirrorDungeonExitReward>();
        var purchaseFormationId = global::PacketRouting.ResolvePacketId<global::ResPacket_PurchaseFormationMirrorDungeon>();
        // No ResPacket_SelectFormationMirrorDungeon exists in the client packets.
        var selectFormationId = global::PacketRouting.PacketId;

        app.MapPost("/api/PurchaseHealMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<PurchaseHealMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            // Any idx the retail table does not define gets the empty default response, no domain
            // round-trip and no save - the reference server's "no matching branch" case. The real
            // options are 0 (50% hp / 45 cm, one unit), 1 (20% / 15 cm, party) and 2 (revive).
            if (MdShopTable.Heal(p.idx) is null)
                return Results.Json(global::ResponsePacket<PurchaseHealMirrorDungeonResult>.Ok(new PurchaseHealMirrorDungeonResult(), purchaseHealId), global::PacketJson.Options);

            var run = WireMapper.ToDomain(loaded);
            ShopRules.Heal(run, p.idx, p.pid);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new PurchaseHealMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                dungeonUnitList = save.currentInfo.dul,
                shopInfo = save.currentInfo.shop,
                // idx==0's bandaid path doesn't track a running usedcost (ShopRules.Heal leaves
                // UsedCost untouched there) - the DTO hardcodes 100, matching the old handler.
                usedcost = p.idx == 0 ? 100 : save.currentInfo.usedcost,
            };
            return Results.Json(global::ResponsePacket<PurchaseHealMirrorDungeonResult>.Ok(result, purchaseHealId), global::PacketJson.Options);
        });

        app.MapPost("/api/PurchaseEgoGiftMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<PurchaseEgoGiftMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            var granted = ShopRules.PurchaseEgoGift(run, p.idx);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new PurchaseEgoGiftMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                egogifts = save.currentInfo.egs,
                shopInfo = save.currentInfo.shop,
                dungeonUnitList = save.currentInfo.dul,
                // usedcost stays 0 unless a purchase actually happened (matches the old handler's
                // usedCost local, which only ever got assigned inside the success branch).
                usedcost = granted.Count > 0 ? save.currentInfo.usedcost : 0,
            };
            return Results.Json(global::ResponsePacket<PurchaseEgoGiftMirrorDungeonResult>.Ok(result, purchaseEgoGiftId), global::PacketJson.Options);
        });

        app.MapPost("/api/PurchaseUpgradePersonalityMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<PurchaseUpgradePersonalityMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            // isDetected (the buyer picks the skill back) and useStarlight (pay the run's starlight
            // instead of funds) are both real paths now, not capture-only fields.
            var run = WireMapper.ToDomain(loaded);
            ShopRules.PurchaseUpgradePersonality(run, p.pid, p.idx, p.isDetected, p.useStarlight);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new PurchaseUpgradePersonalityMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                usedcost = save.currentInfo.usedcost,
                dungeonUnitList = save.currentInfo.dul,
                shopInfo = save.currentInfo.shop,
                starlightInfo = save.currentInfo.slinfo,
            };
            return Results.Json(global::ResponsePacket<PurchaseUpgradePersonalityMirrorDungeonResult>.Ok(result, purchaseUpgradePersonalityId), global::PacketJson.Options);
        });

        app.MapPost("/api/SellEgoGiftMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<SellEgoGiftMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            ShopRules.SellEgoGift(run, p.id);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new SellEgoGiftMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                egogifts = save.currentInfo.egs,
                shopInfo = save.currentInfo.shop,
                dungeonUnitList = save.currentInfo.dul,
            };
            return Results.Json(global::ResponsePacket<SellEgoGiftMirrorDungeonResult>.Ok(result, sellEgoGiftId), global::PacketJson.Options);
        });

        app.MapPost("/api/UpgradeEgoGiftMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<UpgradeEgoGiftMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            var (gift, charged) = FusionRules.UpgradeGift(run, p.egoGiftId, p.to);
            if (gift is null) return Results.StatusCode(500);
            var save = WireMapper.ToWire(run);
            var egoWire = save.currentInfo.egs.First(e => e.id == p.egoGiftId);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new UpgradeEgoGiftMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                egoGift = egoWire,
                dungeonUnitList = save.currentInfo.dul,
                usedcost = charged ? save.currentInfo.usedcost : 0,
            };
            return Results.Json(global::ResponsePacket<UpgradeEgoGiftMirrorDungeonResult>.Ok(result, upgradeEgoGiftId), global::PacketJson.Options);
        });

        app.MapPost("/api/AcquireRewardEgoGiftsMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireRewardEgoGiftsMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            try
            {
                RewardResolution.AcquireReward(run, p.selectIndexList);
            }
            catch (KeyNotFoundException)
            {
                return Results.StatusCode(500);
            }
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new AcquireRewardEgoGiftsMirrorDungeonResult
            {
                egoGifts = save.currentInfo.egs,
                remainRewardEvent = save.currentInfo.rre,
                dungeonUnitList = save.currentInfo.dul,
                saveinfo = save,
            };
            return Results.Json(global::ResponsePacket<AcquireRewardEgoGiftsMirrorDungeonResult>.Ok(result, acquireRewardEgoGiftsId), global::PacketJson.Options);
        });

        app.MapPost("/api/RejectRewardEgoGiftsMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            // Params are unused by Rust too - read+discard just to 400 on an unparseable body.
            var p = await HandlerContext.ReadParamsAsync<object>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            RewardResolution.RejectReward(run);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new RejectRewardEgoGiftsMirrorDungeonResult
            {
                remainRewardEvent = new(),
                saveinfo = save,
            };
            return Results.Json(global::ResponsePacket<RejectRewardEgoGiftsMirrorDungeonResult>.Ok(result, rejectRewardEgoGiftsId), global::PacketJson.Options);
        });

        app.MapPost("/api/PreviewMirrorDungeonExitReward", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            // Params are unused (empty {} in the capture) - read+discard just to 400 on an
            // unparseable body, same pattern as RejectRewardEgoGifts.
            var p = await HandlerContext.ReadParamsAsync<object>(ctx);
            if (p is null) return Results.BadRequest();

            // Read-only preview: no save write, matching the pre-migration handler (which never
            // touched account.MdSaveInfo either). A missing save falls back to a fresh Run - the
            // option engine doesn't depend on run state (see RewardResolution.PreviewExitReward).
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            var run = loaded is null ? new Run() : WireMapper.ToDomain(loaded);
            var (options, totalConstraintScore) = RewardResolution.PreviewExitReward(run);

            var result = new PreviewMirrorDungeonExitRewardResult
            {
                rewardList = options,
                totalConstraintScore = totalConstraintScore,
            };
            return Results.Json(global::ResponsePacket<PreviewMirrorDungeonExitRewardResult>.Ok(result, previewExitRewardId), global::PacketJson.Options);
        });

        app.MapPost("/api/AcquireMirrorDungeonExitReward", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            var p = await HandlerContext.ReadParamsAsync<AcquireMirrorDungeonExitRewardParams>(ctx);
            if (p is null) return Results.BadRequest();

            // No run left (already claimed, or ended elsewhere): answer with the "no run" marker
            // and nothing granted instead of a 500, so a client retrying the claim can leave the
            // reward screen rather than being stuck on it.
            if (loaded is null)
            {
                var none = new MirrorOriginSaveInfo { dungeonId = -1, idx = -1 };
                none.currentInfo.eid = -1;
                return Results.Json(global::ResponsePacket<AcquireMirrorDungeonExitRewardResult>.Ok(
                    new AcquireMirrorDungeonExitRewardResult
                    {
                        rewardList = new(),
                        saveInfo = none,
                        history = new MirrorDungeonHistories { dungeonid = -1 },
                        enabledStartbufIds = new(),
                        startBuffInfo = new StartBuffInfo { dungeonid = -1 },
                        currentClearedConstraintIds = new(),
                    }, acquireExitRewardId), global::PacketJson.Options);
            }

            var run = WireMapper.ToDomain(loaded);
            // Snapshot the run's banked starlight before AcquireExitReward settles it into pslp -
            // this is what the client shows as "Starlight +N" on the run-end screen.
            var starlightGain = run.Starlight.Scc;
            var granted = RewardResolution.AcquireExitReward(run, p.useEnkephalinModule, p.chanceConsumption);
            var save = WireMapper.ToWire(run);

            // Build the response from the save BEFORE clearing it - the capture returns the
            // final run save, then the run ends (see brief).
            var result = new AcquireMirrorDungeonExitRewardResult
            {
                rewardList = granted,
                saveInfo = save,
                history = new MirrorDungeonHistories { dungeonid = save.dungeonId },
                // The run's enabled start-buff ids (retail seq321: [102, 106]). Masked in the replay.
                enabledStartbufIds = run.StartBuffIds,
                startBuffInfo = new StartBuffInfo { dungeonid = save.dungeonId },
                mdpassOriginalAmount = 0, // masked
                mdpassCurrentChanceUsage = 0, // masked
                // The run's accumulated Adversity score: the picks' own score, same formula as
                // PreviewExitReward (RewardResolution.cs:176). Summing ConstraintScores added the
                // per-floor act tiers (1/2/3) on top and disagreed with the preview.
                totalConstraintScore = OpenLethe.Server.MdConstraints.TotalScore(run.ConstraintSelections.SelectMany(s => s.Ids)),
                starlightChangeAmount = starlightGain,
                currentClearedConstraintIds = save.currentInfo.scinfos.SelectMany(s => s.ids).ToList(),
                lastclearedFloor = save.currentInfo.cn.f,
            };

            account.MdSaveInfo = "{}";
            await HandlerContext.SaveAsync(ctx);
            return Results.Json(global::ResponsePacket<AcquireMirrorDungeonExitRewardResult>.Ok(result, acquireExitRewardId), global::PacketJson.Options);
        });

        app.MapPost("/api/SelectFormationMirrorDungeon", async (HttpContext ctx) =>
        {
            // DerivePersonalities below reads CustomIdentities and Personalities as well as MdSaveInfo.
            var account = await HandlerContext.ResolveAsync(ctx,
                SaveColumn.Md, SaveColumn.Personalities, SaveColumn.CustomIdentities);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            // No save yet (the column is empty right after Enter): start a normal run instead of a
            // 500 - the new client retries SelectFormation on a 500 and hangs on the formation screen.
            loaded ??= WireMapper.ToWire(StartRunRules.NewRun(StartRunRules.CurrentDungeonId, 0));
            var p = await HandlerContext.ReadParamsAsync<SelectFormationMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var personalities = AccountDefaults.DerivePersonalities(account);
            var levelMap = personalities.ToDictionary(x => x.personality_id, x => x.level);
            // account-state (like l): a fresh replay account has no real per-identity awakening
            // (gacksung) investment, so this falls back to DefaultData's uniform 4 - masked in
            // the replay, same class as the l mask on SelectThemeFloor's seq15 (see ReplayMasks).
            var gradeMap = personalities.ToDictionary(x => x.personality_id, x => x.gacksung);

            var run = WireMapper.ToDomain(loaded);
            StartRunRules.SelectFormation(run, p.formation, gradeMap, levelMap);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new SelectFormationMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<SelectFormationMirrorDungeonResult>.Ok(result, selectFormationId), global::PacketJson.Options);
        });

        app.MapPost("/api/PurchaseFormationMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<PurchaseFormationMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            var applied = StartRunRules.PurchaseFormation(run, p.formation);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new PurchaseFormationMirrorDungeonResult
            {
                cost = save.currentInfo.cost,
                dungeonUnitList = save.currentInfo.dul,
                shopInfo = save.currentInfo.shop,
                prevUnitInfo = new(),
                // The reference server always reported the flat 100; a refused swap (allowance
                // used up) reports 0 so the client does not show a charge that never happened.
                usedcost = applied ? MdShopTable.ChangeFormationPrice : 0,
            };
            return Results.Json(global::ResponsePacket<PurchaseFormationMirrorDungeonResult>.Ok(result, purchaseFormationId), global::PacketJson.Options);
        });

        return app;
    }
}
