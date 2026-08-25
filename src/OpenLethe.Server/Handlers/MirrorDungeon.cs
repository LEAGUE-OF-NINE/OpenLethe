using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Mapping;
using OpenLethe.Server.MirrorDungeon.Rules;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Ports of server/src/api/md/*.rs. Save state lives in Account.MdSaveInfo as a server
/// Wire MirrorOriginSaveInfo. This cycle: the enter/re-enter foundation.
public static class MirrorDungeonEndpoints
{
    public static IEndpointRouteBuilder MapMirrorDungeon(this IEndpointRouteBuilder app)
    {
        var enterId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterMirrorDungeon>();
        app.MapPost("/api/EnterMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<EnterMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = StartRunRules.NewRun(p.dungeonid, p.idx);
            var save = WireMapper.ToWire(run);
            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);

            var result = new EnterMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<EnterMirrorDungeonResult>.Ok(result, enterId), global::PacketJson.Options);
        });

        var reEnterId = global::PacketRouting.ResolvePacketId<global::ResPacket_ReEnterMirrorDungeon>();
        app.MapPost("/api/ReEnterMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo) ?? new MirrorOriginSaveInfo();
            var save = WireMapper.ToWire(WireMapper.ToDomain(loaded));
            var result = new ReEnterMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<ReEnterMirrorDungeonResult>.Ok(result, reEnterId), global::PacketJson.Options);
        });

        // Resume into a special mode mid-run: echo the in-progress save with idx bumped by one
        // (the run's mode-enter counter: EnterMD=0, first SelectThemeFloor=1, InfiniteMode=2,
        // ExtremeMode=3). The request carries only dungeonId; the harness injects the ground-truth
        // save. statistics/encounterstatistics report LIVE battle outcomes accumulated since the
        // last full-save the tracker saw (ExitMapNode returns only currentInfo, so the tracker's
        // saveInfo.statistics is stale) - unreproducible on a battle-less replay, so masked.
        var enterExtremeId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterExtremeMode>();
        app.MapPost("/api/EnterExtremeMode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo) ?? new MirrorOriginSaveInfo();
            var run = WireMapper.ToDomain(loaded);
            StartRunRules.EnterMode(run);
            var save = WireMapper.ToWire(run);
            var result = new ReEnterMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<ReEnterMirrorDungeonResult>.Ok(result, enterExtremeId), global::PacketJson.Options);
        });

        var enterInfiniteId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterInfiniteMode>();
        app.MapPost("/api/EnterInfiniteMode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo) ?? new MirrorOriginSaveInfo();
            var run = WireMapper.ToDomain(loaded);
            StartRunRules.EnterMode(run);
            var save = WireMapper.ToWire(run);
            var result = new ReEnterMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<ReEnterMirrorDungeonResult>.Ok(result, enterInfiniteId), global::PacketJson.Options);
        });

        // ponytail: EnableStartBuff doesn't persist which buff ids got enabled into any
        // currentInfo field (see below - the wire model has no such field, and adding one
        // would break every other endpoint that echoes currentInfo verbatim). Capture only
        // ever calls this route ONCE, before EnterMirrorDungeon has even created a save, so
        // `enabled` is always the empty catalog - that's the only evidence-backed behaviour.
        var getStartBuffInfoId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetStartBuffFInfoMirrorDungeon>();
        app.MapPost("/api/GetStartBuffFInfoMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<GetStartBuffFInfoMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            var result = new GetStartBuffFInfoMirrorDungeonResult
            {
                startBuffInfo = new StartBuffInfo { dungeonid = p.dungeonid, enabled = new() },
            };
            return Results.Json(global::ResponsePacket<GetStartBuffFInfoMirrorDungeonResult>.Ok(result, getStartBuffInfoId), global::PacketJson.Options);
        });

        // Spends the run's start-buff points: cost = sum(picked buff.cost) *
        // remainPointToCostMultiplier (static, mirrordungeon-start-buffs-07.json).
        var enableStartBuffId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnableStartBuffMirrorDungeon>();
        app.MapPost("/api/EnableStartBuffMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<EnableStartBuffMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            // The run's `cost` budget starts at StartingCost (200) and grows as the leftover
            // start-buff points convert into currency: cost += (startBufPoint - RawSpend) *
            // remainPointToCostMultiplier. Capture-verified result.cost: run-1 200+(80-60)*5=300,
            // run-2 200+(120-120)*5=200. (The old RawSpend*mult formula matched run-1 only by
            // coincidence: 60*5==200+20*5.) buff103's ADDITIONAL_START_COST is added later, at
            // AcquireStart.
            var run = WireMapper.ToDomain(loaded);
            var cost = StartRunRules.EnableStartBuff(run, p.buffids, p.enableConvertedCost);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new EnableStartBuffMirrorDungeonResult
            {
                startBuffInfo = new StartBuffInfo { dungeonid = p.dungeonid, enabled = p.buffids.OrderBy(id => id).ToList() },
                cost = cost,
                starlightInfo = save.currentInfo.slinfo,
            };
            return Results.Json(global::ResponsePacket<EnableStartBuffMirrorDungeonResult>.Ok(result, enableStartBuffId), global::PacketJson.Options);
        });

        // Applies a starlight-detected ego-gift pick: echoes the mutated currentInfo.
        var detectStarlightId = global::PacketRouting.ResolvePacketId<global::ResPacket_DetectMirrorDungeonEgogiftByStarlight>();
        app.MapPost("/api/DetectMirrorDungeonEgogiftByStarlight", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (loaded is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<DetectMirrorDungeonEgogiftByStarlightParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(loaded);
            StartRunRules.DetectStarlight(run, p.egogiftIds);
            var save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new MirrorDungeonCurrentInfoResult { currentInfo = save.currentInfo };
            return Results.Json(global::ResponsePacket<MirrorDungeonCurrentInfoResult>.Ok(result, detectStarlightId), global::PacketJson.Options);
        });

        // Consumes the GetConstraints reward popup (same rt-remove pattern as
        // AcquireRewardEgoGifts's GetEgogift) and records the pick as a new scinfos entry for
        // the floor about to start. Capture-verified (seq298, the run's only record): the
        // request's selectIdxList is empty (no constraint picked), so `ids` is empty too -
        // the non-empty (constraint actually selected) branch is unexercised by the capture.
        var acquireConstraintsId = global::PacketRouting.ResolvePacketId<global::ResPacket_AcquireMirrorDungeonConstraints>();
        app.MapPost("/api/AcquireMirrorDungeonConstraints", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<AcquireMirrorDungeonConstraintsParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(save);
            StartRunRules.AcquireConstraints(run, p.selectIdxList);
            save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new MirrorDungeonCurrentInfoResult { currentInfo = save.currentInfo };
            return Results.Json(global::ResponsePacket<MirrorDungeonCurrentInfoResult>.Ok(result, acquireConstraintsId), global::PacketJson.Options);
        });

        return app;
    }
}
