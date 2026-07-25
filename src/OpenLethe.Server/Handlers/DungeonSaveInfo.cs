using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace OpenLethe.Server.Handlers;

/// Port of dungeon/get_dungeon_save_info_all.rs. Aggregates the dungeon save-info
/// columns; empty column -> sentinel (Rust's unwrap_or_else fallback). The client
/// ResPacket drifts from the Rust type: no railwaySaveInfo, plus mirrorDungeonHistories.
public static class DungeonSaveInfoEndpoints
{
    public static IEndpointRouteBuilder MapGetDungeonSaveInfoAll(this IEndpointRouteBuilder app)
    {
        var packetId = global::PacketRouting.ResolvePacketId<global::ResPacket_GetDungeonSaveInfoAll>();
        app.MapPost("/api/GetDungeonSaveInfoAll", async (HttpContext ctx) =>
        {
            var account = await HandlerContext.ResolveAsync(ctx);
            if (account is null) return Results.Unauthorized();

            // ponytail: Rust used `!x.dungeonMap.ns.is_empty()` to detect a populated save;
            // AccountFields.Get already treats "{}"/"[]"/null as absent, which is the same
            // "empty column -> sentinel" test without transliterating a Rust field the
            // drifted client type may not carry. A future subsystem stores a full save or
            // clears the column, so the null check is sufficient.
            // ponytail: sentinels set only the fields Rust set explicitly; Rust's
            // ..Default::default() yields empty collections where C# leaves nested reference
            // fields null - tolerated by the client for the -1 "no dungeon" marker.
            var md = OpenLethe.Server.AccountFields.Get<global::MirrorDungeonSaveInfoFormat>(account.MdSaveInfo)
                ?? new global::MirrorDungeonSaveInfoFormat
                {
                    dungeonId = -1,
                    idx = -1,
                    currentInfo = new global::MirrorDungeonCurrentInfoFormat { eid = -1 },
                };

            var storyMd = OpenLethe.Server.AccountFields.Get<global::StoryMirrorDungeonSaveInfoFormat>(account.StoryMdSaveInfo)
                ?? new global::StoryMirrorDungeonSaveInfoFormat
                {
                    dungeonid = -1,
                    currentinfo = new global::StoryMirrorDungeonCurrentInfoFormat { eid = -1 },
                };

            var story = OpenLethe.Server.AccountFields.Get<global::StoryDungeonSaveInfoFormat>(account.StorySaveInfo)
                ?? new global::StoryDungeonSaveInfoFormat();

            var result = new global::ResPacket_GetDungeonSaveInfoAll
            {
                mirrorOriginSaveInfo = md,
                storyMirrorSaveInfo = storyMd,
                storySaveInfo = story,
                mirrorDungeonClearInfos = new List<global::MirrorDungeonClearInfoFormat>
                {
                    new() { dungeonid = 4, idx = 0, clearnumber = 9999, defeatnumber = 9999 },
                    new() { dungeonid = 4, idx = 1, clearnumber = 9999, defeatnumber = 9999 },
                },
                mirrorDungeonHistories = new List<global::MirrorDungeonHistoryFormat>(),
            };
            return Results.Json(global::ResponsePacket<global::ResPacket_GetDungeonSaveInfoAll>.Ok(result, packetId), global::PacketJson.Options);
        });
        return app;
    }
}
