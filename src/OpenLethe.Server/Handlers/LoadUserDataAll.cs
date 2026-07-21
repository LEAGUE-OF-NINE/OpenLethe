using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenLethe.Data;
using OpenLethe.Server.Defaults;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Port of lethe-server/server/src/api/load_user_data_all.rs. First-load handler:
/// returns the account's game data, applying read-time defaults where Rust does.
public static class LoadUserDataAllEndpoint
{
    private const string StaminaRecover = "2025-03-31T15:10:00.000Z";

    public static IEndpointRouteBuilder MapLoadUserDataAll(this IEndpointRouteBuilder app)
    {
        var packetId = PacketRouting.ResolvePacketId<global::ResPacket_LoadUserDataAll>();

        app.MapPost("/api/LoadUserDataAll", async (HttpContext ctx) =>
        {
            var sub = ctx.Items["sub"] as string;
            if (sub is null) return Results.Unauthorized();

            var store = ctx.RequestServices.GetRequiredService<AccountStore>();
            var account = await store.FindByUsernameAsync(sub);
            if (account is null) return Results.Unauthorized();

            var dirty = false;

            // egos: lazy-fill defaults when empty (Rust load_user_data_all).
            var egos = AccountFields.Get<List<Ego>>(account.Egos) ?? new List<Ego>();
            if (egos.Count == 0)
            {
                egos = DefaultData.GetFormattedEgos();
                account.Egos = AccountFields.Set(egos);
                dirty = true;
            }

            // chapterstate: lazy-fill when empty.
            var chapterState = AccountFields.Get<List<MainChapterState>>(account.ChapterState)
                ?? new List<MainChapterState>();
            if (chapterState.Count == 0)
            {
                chapterState = DefaultData.LoadMainChapterState();
                account.ChapterState = AccountFields.Set(chapterState);
                dirty = true;
            }

            // personalities: re-derive each read (defaults overlaid by stored edits),
            // ordered by personality_id (Rust BTreeMap).
            var personalities = DerivePersonalities(account.Personalities);

            // user_info: fixed default when absent or uid==0; persist the reset.
            var userInfo = AccountFields.Get<UserInfo>(account.UserInfo);
            if (userInfo is null || userInfo.uid == 0)
            {
                userInfo = DefaultUserInfo();
                account.UserInfo = AccountFields.Set(userInfo);
                dirty = true;
            }

            if (dirty)
                await ctx.RequestServices.GetRequiredService<AppDbContext>().SaveChangesAsync();

            var announcers = AccountFields.Get<List<long>>(account.Announcers) ?? new List<long>();
            var formations = AccountFields.Get<List<System.Text.Json.Nodes.JsonNode>>(account.Formations)
                ?? new List<System.Text.Json.Nodes.JsonNode>();
            var danteIds = DefaultData.GetDanteAbilityIds();

            var updated = new UpdatedFormat
            {
                isInitialized = true,
                userInfo = userInfo,
                personalityList = personalities,
                egoList = egos,
                formationList = formations,
                lobbyCG = new LobbyCg { characterId = 1, lobbycgdetails = new(), isShowProfile = true },
                itemList = new List<AcquiredFromLostGlobalPieces>
                {
                    new() { item_id = 11, num = 99999 }, // Enkap modules
                    new() { item_id = 2, num = 1300 },   // Lunacy
                },
                battlePass = new BattlePass { is_limbus = true, today_rand_value = 12, limbus_apply_level = 4 },
                mainChapterStateList = chapterState,
                mailList = new List<InitializedMail>
                {
                    new()
                    {
                        mail_id = 1,
                        sent_date = "2024-06-24T20:00:00.000Z",
                        expiry_date = "2094-06-24T20:00:00.000Z",
                        content_id = 5860,
                        attachments = { new Wire.Element { type_ = "ITEM", id = 3, num = 50000 } },
                    },
                },
                announcer = new Announcer
                {
                    announcer_ids = Enumerable.Range(0, 100).Select(i => (long)i).ToList(),
                    cur_announcer_ids = announcers,
                },
                membershipList = new(),
                userUnlockCodeList = DefaultData.GetFormattedUserCodes(),
                eventRewardStateList = new(),
                danteAbilityList = new List<DanteAbility>
                {
                    new() { category = 0, abilityids = danteIds, remaincount = 2325 },
                    new() { category = 1, abilityids = danteIds, remaincount = 2325 },
                },
                personalitySkinList = DefaultData.GetFormattedPersonalities()
                    .Select(p => new PersonalitySkin { id = p.personality_id, regdate = "" })
                    .ToList(),
            };

            var result = new global::ResPacket_LoadUserDataAll
            {
                showedWeekByMinistory = 3,
                profile = new global::UserPublicProfileWithSupportersFormat
                {
                    support_personalities = new(),
                    public_uid = "Private Account",
                    illust_id = 11107,
                    illust_gacksung_level = 3,
                    leftborder_id = 29,
                    rightborder_id = 29,
                    egobackground_id = 32,
                    sentence_id = 231,
                    word_id = 116,
                    banners = BuildBanners(),
                    level = 200,
                    date = StaminaRecover,
                },
                isExistReceiveFriendRequest = false,
                danteNoteTodayPage = 49,
                dailyLoginId = -1,
                dailyLoginWeekId = -1,
                dailyLoginRewardStates = new(),
            };

            var response = global::ResponsePacket<global::ResPacket_LoadUserDataAll>.Ok(result, packetId);
            response.updated = updated;

            return Results.Json(response, global::PacketJson.Options);
        });

        return app;
    }

    private static List<ResultPersonality> DerivePersonalities(string? stored)
    {
        var map = new SortedDictionary<long, ResultPersonality>();
        foreach (var p in DefaultData.GetFormattedPersonalities()) map[p.personality_id] = p;

        // Overlay stored edits, but only for ids already in the default set.
        foreach (var p in AccountFields.Get<List<ResultPersonality>>(stored) ?? new())
            if (map.ContainsKey(p.personality_id)) map[p.personality_id] = p;

        return map.Values.ToList();
    }

    private static List<global::UserPublicBannerFormat> BuildBanners()
    {
        var banners = new List<global::UserPublicBannerFormat>
        {
            new() { id = 35, value = -1, value2 = -1, value3 = -1, idx = 0 },
        };
        for (var i = 1; i <= 4; i++)
            banners.Add(new global::UserPublicBannerFormat
            {
                id = Random.Shared.Next(1, 44), // 1..=43 inclusive
                value = -1, value2 = -1, value3 = -1, idx = i,
            });
        return banners;
    }

    private static UserInfo DefaultUserInfo() => new()
    {
        uid = 1234,
        level = 200,
        exp = 0,
        stamina = 99999,
        last_stamina_recover = StaminaRecover,
        first_login_today = StaminaRecover,
    };
}
