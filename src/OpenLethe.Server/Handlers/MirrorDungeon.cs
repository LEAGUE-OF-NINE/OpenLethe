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

            // The current client's MD static table id is 8; old saves/clients send 7 or 0. Normalize
            // before saving, or the entrance plate shows 00 and the banner falls back to the Korean
            // desc (see StartRunRules.CurrentDungeonId).
            var run = StartRunRules.NewRun(StartRunRules.NormalizeDungeonIdValue(p.dungeonid), p.idx);
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
            // An empty save (dungeonId <= 0) or an old season's id reports as the current dungeon.
            StartRunRules.NormalizeDungeonId(loaded);
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
            var stored = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            var run = WireMapper.ToDomain(stored ?? new MirrorOriginSaveInfo());
            // Forced, not incremented: the capture shows saveInfo.idx == 3 exactly on this call
            // (seq295) and == 2 on EnterInfiniteMode - the client picks its mode off that value.
            StartRunRules.EnterMode(run, 3);
            var save = WireMapper.ToWire(run);
            // Persisted, not just echoed: retail's next RecreateThemeFloorPool still reports idx 3
            // (run-2 seq333). An echo-only idx fell back to the stored 2 on that refresh, and the
            // client, now in infinite mode on floor 11, offered no theme packs at all.
            if (stored is not null)
            {
                account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
                await HandlerContext.SaveAsync(ctx);
            }
            var result = new ReEnterMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<ReEnterMirrorDungeonResult>.Ok(result, enterExtremeId), global::PacketJson.Options);
        });

        var enterInfiniteId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterInfiniteMode>();
        app.MapPost("/api/EnterInfiniteMode", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var stored = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            var run = WireMapper.ToDomain(stored ?? new MirrorOriginSaveInfo());
            // Forced, not incremented: the client calls this mid-run at every act boundary (floor 5,
            // 10, ...) and wants saveInfo.idx == 2 (capture seq165); a drifting +1 left it at the
            // "run just started" value, so the client settled the run instead of opening the next act.
            StartRunRules.EnterMode(run, 2);
            var save = WireMapper.ToWire(run);
            // Persisted for the same reason as EnterExtremeMode (retail run-1 seq168 refresh: idx 2).
            if (stored is not null)
            {
                account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
                await HandlerContext.SaveAsync(ctx);
            }
            var result = new ReEnterMirrorDungeonResult { saveInfo = save };
            return Results.Json(global::ResponsePacket<ReEnterMirrorDungeonResult>.Ok(result, enterInfiniteId), global::PacketJson.Options);
        });

        // Drop the E.G.O gift(s) the client just removed locally (an event cost / a discard). The route used
        // to be a stateless MapPacket stub: it discarded the request and answered with a fixed empty packet,
        // so the client's list emptied while the save kept every gift - "all my gifts disappeared" was that
        // divergence. The request type is generated, so read whatever numeric list/scalar it carries.
        var removeGiftId = global::PacketRouting.ResolvePacketId<global::ResPacket_RemoveMirrorDungeonEgoGift>();
        app.MapPost("/api/RemoveMirrorDungeonEgoGift", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_RemoveMirrorDungeonEgoGift>(ctx);

            var ids = new System.Collections.Generic.List<long>();
            void Collect(object? v)
            {
                if (v is null) return;
                if (v is long l) { if (l > 0 && !ids.Contains(l)) ids.Add(l); return; }
                if (v is int i) { if (i > 0 && !ids.Contains(i)) ids.Add(i); return; }
                if (v is string) return;
                if (v is System.Collections.IEnumerable seq) { foreach (var e in seq) Collect(e); }
            }
            if (p is not null)
            {
                foreach (var pi in p.GetType().GetProperties())
                    if (pi.GetIndexParameters().Length == 0) Collect(pi.GetValue(p));
                foreach (var fi in p.GetType().GetFields())
                    Collect(fi.GetValue(p));
            }

            if (ids.Count > 0)
            {
                var run = WireMapper.ToDomain(OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo) ?? new MirrorOriginSaveInfo());
                var before = run.Gifts.Items.Count;
                run.Gifts.Items.RemoveAll(g => ids.Contains(g.Id));
                if (run.Gifts.Items.Count != before)
                {
                    account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(WireMapper.ToWire(run));
                    await HandlerContext.SaveAsync(ctx);
                }
            }

            return Results.Json(global::ResponsePacket<global::ResPacket_RemoveMirrorDungeonEgoGift>.Ok(
                new global::ResPacket_RemoveMirrorDungeonEgoGift(), removeGiftId), global::PacketJson.Options);
        });

        // EnableStartBuff now persists the enabled ids on currentInfo.enabledStartbufIds - the wire
        // model has no separate slot for them, and the run state is the only place the later steps
        // can read the player's real picks from. Repeated reads of this route therefore echo the
        // selection back instead of the empty catalog; the capture's single call ran before any
        // buffs were enabled, so it still sees an empty list.
        var getStartBuffInfoId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetStartBuffFInfoMirrorDungeon>();
        // The 2026-10-15 client refreshes the MD entrance (MirrorDungeonEntranceUIObject.UpdateUI)
        // right after this response. The old stateless stub answered startBuffInfo: null, which NREs
        // there and leaves the entrance unclickable - answer the same shape as GetStartBuffFInfo.
        app.MapPost("/api/EnableSpecialStartBuffMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var loaded = AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            var enableSpecialId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnableSpecialStartBuffMirrorDungeon>();
            var specialResult = new global::ResPacket_EnableSpecialStartBuffMirrorDungeon
            {
                startBuffInfo = new global::MirrorDungeonStartBuffInfoFormat
                {
                    dungeonid = (int)StartRunRules.CurrentDungeonId,
                    enabled = loaded?.currentInfo?.enabledStartbufIds?.Select(x => (int)x).ToList() ?? new List<int>(),
                    bufstate = loaded?.currentInfo?.enabledStartbufIds?.Select(x => (int)x).ToList() ?? new List<int>(),
                },
            };
            return Results.Json(global::ResponsePacket<global::ResPacket_EnableSpecialStartBuffMirrorDungeon>.Ok(specialResult, enableSpecialId), global::PacketJson.Options);
        });

        app.MapPost("/api/GetStartBuffFInfoMirrorDungeon", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<GetStartBuffFInfoMirrorDungeonParams>(ctx);
            if (p is null) return Results.BadRequest();

            // Echo the run's persisted picks (EnableStartBuff stores them on currentInfo).
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            var result = new GetStartBuffFInfoMirrorDungeonResult
            {
                startBuffInfo = new StartBuffInfo
                {
                    dungeonid = p.dungeonid,
                    enabled = save?.currentInfo.enabledStartbufIds ?? new(),
                    // The 2026-10-15 client reads bufstate from the entrance UI; null NREs in
                    // MirrorDungeonEntranceUIObject.UpdateUI, so it mirrors `enabled`.
                    bufstate = save?.currentInfo.enabledStartbufIds ?? new(),
                },
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
                startBuffInfo = new StartBuffInfo { dungeonid = p.dungeonid, enabled = run.StartBuffIds },
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

        // The Adversity pick: consumes the GetConstraints reward popup, records the pick as the
        // scinfos entry for the floor about to start and grants the picked Adversities as their
        // hidden ego gifts (see StartRunRules.AcquireConstraints). Capture-verified for empty picks
        // (seq298 and run-2's four records).
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

        // Starlight theme-floor detection: the client names the offered pack to swap out
        // (originThemeFloorId) and the pack to reveal in its place (detectThemeFloorId); the run
        // pays starlight points and reads the pool back like any re-rolled one. This route used to
        // be unhandled, so the client's detect button did nothing.
        var detectThemeFloorId = global::PacketRouting.ResolvePacketId<global::ResPacket_DetectMirrorDungeonThemeFloorByStarlight>();
        app.MapPost("/api/DetectMirrorDungeonThemeFloorByStarlight", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.Md);
            if (account is null) return Results.Unauthorized();
            var save = OpenLethe.Server.AccountFields.Get<MirrorOriginSaveInfo>(account.MdSaveInfo);
            if (save is null) return Results.StatusCode(500);
            var p = await HandlerContext.ReadParamsAsync<DetectMirrorDungeonThemeFloorByStarlightParams>(ctx);
            if (p is null) return Results.BadRequest();

            var run = WireMapper.ToDomain(save);
            StartRunRules.DetectThemeFloor(run, p.originThemeFloorId, p.detectThemeFloorId);
            save = WireMapper.ToWire(run);

            account.MdSaveInfo = OpenLethe.Server.AccountFields.Set(save);
            await HandlerContext.SaveAsync(ctx);
            var result = new MirrorDungeonCurrentInfoResult { currentInfo = save.currentInfo };
            return Results.Json(global::ResponsePacket<MirrorDungeonCurrentInfoResult>.Ok(result, detectThemeFloorId), global::PacketJson.Options);
        });

        return app;
    }
}
