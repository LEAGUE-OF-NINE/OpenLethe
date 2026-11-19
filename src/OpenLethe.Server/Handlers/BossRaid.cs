using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Boss-raid (Refraction trial) stateful endpoints. Account.BossRaidSaveInfo holds a
/// BossRaidStore: the client's BossRaidSaveDataFormat plus the run state the real server keeps
/// beside it. Behaviour follows a captured official run of raid 10004:
///  - EnterBossRaid opens a run and sends the raid's durante pool (its danteAbilityCategory)
///    in updated.danteAbilityList; ExitBossRaidBattle spends from it and sends it again.
///  - saveInfo.personalities ACCUMULATES every identity of every party used this run, which is
///    what keeps all earlier teams blocked - not just the previous one.
///  - Each exit advances currentIdx. A win, or running out of tries (maxPartyCount from the
///    raid entry), ends the run with state 3; EndBossRaid then pays out and clears it.
public static class BossRaidEndpoints
{
    private const int StateNone = 0;
    private const int StateInProgress = 2;
    private const int StateFinished = 3;

    private static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");

    private static void SetDante(object? updated, DanteAbility? pool)
    {
        if (pool is not null && updated is UpdatedFormat u)
            u.danteAbilityList = new List<DanteAbility> { pool };
    }

    public static IEndpointRouteBuilder MapBossRaid(this IEndpointRouteBuilder app)
    {
        // Save info: the stored save for this raid (null when none), plus the parties used so far.
        var getSaveInfoId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetBossRaidSaveInfo>();
        app.MapPost("/api/GetBossRaidSaveInfo", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.BossRaid);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_GetBossRaidSaveInfo>(ctx);

            var store = BossRaidRules.Load(account.BossRaidSaveInfo);
            if (store is not null && p is not null && p.raidId != 0 && store.save!.raidId != p.raidId)
                store = null; // a run of a different raid

            var result = new GetBossRaidSaveInfoResult
            {
                saveInfo = store?.save,
                partyDatas = store?.parties ?? new(),
                logData = null,
            };
            var resp = global::ResponsePacket<GetBossRaidSaveInfoResult>.Ok(result, getSaveInfoId);
            SetDante(resp.updated, store?.dante);
            return Results.Json(resp, global::PacketJson.Options);
        });

        // States: every boss raid in static data; the one with a stored run reports its state.
        var getStatesId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetBossRaidStates>();
        app.MapPost("/api/GetBossRaidStates", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.BossRaid);
            if (account is null) return Results.Unauthorized();

            var save = BossRaidRules.Load(account.BossRaidSaveInfo)?.save;
            var ids = BossRaidRules.Raids.Select(r => r.id).ToList();
            if (ids.Count == 0) ids.Add(10001);
            if (save is not null && !ids.Contains(save.raidId)) ids.Add(save.raidId);

            var result = new global::ResPacket_GetBossRaidStates
            {
                raidStates = ids.Select(id => new global::BossRaidStateFormat
                {
                    raidId = id,
                    difficulty = save?.raidId == id ? save.difficulty : 0,
                    state = save?.raidId == id ? save.state : StateNone,
                }).ToList(),
            };
            return Results.Json(global::ResponsePacket<global::ResPacket_GetBossRaidStates>.Ok(result, getStatesId), global::PacketJson.Options);
        });

        // Enter: a fresh run with a full durante pool for the raid's danteAbilityCategory.
        var enterId = global::PacketRouting.ResolvePacketId<global::ResPacket_EnterBossRaid>();
        app.MapPost("/api/EnterBossRaid", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.BossRaid);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_EnterBossRaid>(ctx);
            if (p is null) return Results.BadRequest();

            var save = new global::BossRaidSaveDataFormat
            {
                raidId = p.raidId,
                difficulty = p.difficulty,
                state = StateInProgress,
                currentIdx = 0,
                isClear = false,
                personalities = new(),
                egostocks = new(),
                enemy = new(),
                startdate = Now(),
            };
            var store = new BossRaidStore
            {
                save = save,
                dante = BossRaidRules.FreshDantePool(BossRaidRules.Raid(p.raidId)?.danteAbilityCategory ?? 0),
            };
            account.BossRaidSaveInfo = AccountFields.Set(store);
            await HandlerContext.SaveAsync(ctx);

            var resp = global::ResponsePacket<global::ResPacket_EnterBossRaid>.Ok(
                new global::ResPacket_EnterBossRaid { saveInfo = save }, enterId);
            SetDante(resp.updated, store.dante);
            return Results.Json(resp, global::PacketJson.Options);
        });

        // End: pay out a clear, return the run's log, and close the run (reset or not).
        var endId = global::PacketRouting.ResolvePacketId<global::ResPacket_EndBossRaid>();
        app.MapPost("/api/EndBossRaid", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.BossRaid);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_EndBossRaid>(ctx);
            if (p is null) return Results.BadRequest();

            var store = BossRaidRules.Load(account.BossRaidSaveInfo);
            var run = store?.save;

            BossRaidLogData? log = null;
            var rewards = new List<OpenLethe.Server.Wire.Element>();
            if (run is not null && !p.reset)
            {
                log = new BossRaidLogData
                {
                    idx = 0,
                    raidId = run.raidId,
                    difficulty = run.difficulty,
                    clearturn = store!.parties.Sum(x => x.turn),
                    partydetails = store.parties,
                    date = Now(),
                    startdate = run.startdate,
                };
                if (run.isClear)
                {
                    var diff = BossRaidRules.Difficulty(run.raidId, run.difficulty);
                    if (diff is not null)
                        rewards = diff.rewards.Select(r => new OpenLethe.Server.Wire.Element { type_ = r.type, id = r.id, num = r.num }).ToList();
                }
            }

            account.BossRaidSaveInfo = "{}";
            await HandlerContext.SaveAsync(ctx);

            var save = new global::BossRaidSaveDataFormat
            {
                raidId = p.raidId, difficulty = 0, state = StateNone, currentIdx = 0, isClear = false,
                personalities = new(), egostocks = new(), enemy = new(), startdate = null!,
            };
            var result = new EndBossRaidResult { saveInfo = save, logData = log, rewards = rewards };
            return Results.Json(global::ResponsePacket<EndBossRaidResult>.Ok(result, endId), global::PacketJson.Options);
        });

        // Start battle: refuse once the run is over or out of tries, block identities from
        // earlier parties, then remember the party and hand back the carried enemy + ego stock.
        var startId = global::PacketRouting.ResolvePacketId<global::ResPacket_StartBossRaidBattle>();
        app.MapPost("/api/StartBossRaidBattle", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.BossRaid);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_StartBossRaidBattle>(ctx);
            if (p is null) return Results.BadRequest();

            var store = BossRaidRules.Load(account.BossRaidSaveInfo);
            var save = store?.save;
            if (save is null) return Results.BadRequest(); // no active raid
            if (save.state != StateInProgress || save.currentIdx >= BossRaidRules.MaxPartyCount(save.raidId))
                return Results.BadRequest(); // run finished / no tries left

            var party = (p.personalities ?? new()).Where(x => x is not null).ToList();
            save.personalities ??= new();
            if (party.Any(x => save.personalities.Contains(x.pid)))
                return Results.BadRequest(); // identity already used by an earlier party

            store!.pending = party
                .Select(x => new BossRaidPartyPersonality { pid = x.pid, pord = x.pord, lv = (int)Defaults.DefaultData.DefaultPersonalityLevel, g = 4 })
                .ToList();
            account.BossRaidSaveInfo = AccountFields.Set(store);
            await HandlerContext.SaveAsync(ctx);

            var result = new global::ResPacket_StartBossRaidBattle { enemy = save.enemy ?? new(), egostocks = save.egostocks ?? new() };
            return Results.Json(global::ResponsePacket<global::ResPacket_StartBossRaidBattle>.Ok(result, startId), global::PacketJson.Options);
        });

        // Exit battle: lock the party's identities for the rest of the run, log the party, spend
        // durantes, carry enemy + ego stock forward, and advance to the next try. A win - or using
        // up the last try - finishes the run (state 3); the save stays until EndBossRaid.
        var exitId = global::PacketRouting.ResolvePacketId<global::ResPacket_ExitBossRaidBattle>();
        app.MapPost("/api/ExitBossRaidBattle", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx, SaveColumn.BossRaid);
            if (account is null) return Results.Unauthorized();
            var p = await HandlerContext.ReadParamsAsync<global::ReqPacket_ExitBossRaidBattle>(ctx);
            if (p is null) return Results.BadRequest();

            var store = BossRaidRules.Load(account.BossRaidSaveInfo);
            var save = store?.save;
            if (save is null) return Results.BadRequest();
            if (save.state != StateInProgress) // already finished: just echo it back
            {
                var done = global::ResponsePacket<global::ResPacket_ExitBossRaidBattle>.Ok(
                    new global::ResPacket_ExitBossRaidBattle { saveInfo = save }, exitId);
                SetDante(done.updated, store!.dante);
                return Results.Json(done, global::PacketJson.Options);
            }

            save.personalities ??= new();
            foreach (var x in store!.pending)
                if (!save.personalities.Contains(x.pid)) save.personalities.Add(x.pid);

            store.parties.Add(new BossRaidPartyDetail
            {
                idx = save.currentIdx,
                turn = p.clearTurn,
                personalities = store.pending,
                statistics = (p.statistics ?? new())
                    .Where(s => s is not null)
                    .Select(s => new BossRaidStatistic { id = s.id, gd = s.gd, rd = s.rd })
                    .ToList(),
            });
            store.pending = new();

            store.dante ??= BossRaidRules.FreshDantePool(BossRaidRules.Raid(save.raidId)?.danteAbilityCategory ?? 0);
            BossRaidRules.SpendDante(store.dante, p.danteAbilityUsageLogs);

            if (p.egostocks is not null) save.egostocks = p.egostocks;
            if (p.enemy is not null) save.enemy = p.enemy;
            save.currentIdx += 1;

            if (p.isWin)
            {
                save.isClear = true;
                save.state = StateFinished;
            }
            else if (save.currentIdx >= BossRaidRules.MaxPartyCount(save.raidId))
            {
                save.state = StateFinished; // out of parties: the run is over, uncleared
            }

            account.BossRaidSaveInfo = AccountFields.Set(store);
            await HandlerContext.SaveAsync(ctx);

            var resp = global::ResponsePacket<global::ResPacket_ExitBossRaidBattle>.Ok(
                new global::ResPacket_ExitBossRaidBattle { saveInfo = save }, exitId);
            SetDante(resp.updated, store.dante);
            return Results.Json(resp, global::PacketJson.Options);
        });

        return app;
    }
}
