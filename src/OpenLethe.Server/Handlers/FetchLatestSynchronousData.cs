using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// /api/FetchLatestSynchronousData - port of fetch_latest_synchronous_data.rs.
/// Not a static_response handler (it hand-builds a `synchronized` payload), so the
/// route generator skipped it and it needs a real handler. Returns the "Welcome to
/// Lethe" notice + a mail via the envelope's `synchronized` field. The client needs
/// a 200 here to proceed past login. Ignores the request body (no account needed).
public static class FetchLatestSynchronousDataEndpoint
{
    private const string NoticeContent = """
    {
        "list": [
        { "formatKey": "SubTitle", "formatValue": "<Lethe>" },
        { "formatKey": "Text", "formatValue": "Greetings, Dear manager. Have fun playing in this private server." },
        { "formatKey": "Text", "formatValue": "" },
        { "formatKey": "SubTitle", "formatValue": "<Credits>" },
        { "formatKey": "Text", "formatValue": "Private Server Developers: Limi, Zenocara, Yulian" },
        { "formatKey": "Text", "formatValue": "Note: This message may appear on the main server interface; however, it is stored locally and poses no harm." }
        ]
    }
    """;

    public static IEndpointRouteBuilder MapFetchLatestSynchronousData(this IEndpointRouteBuilder app)
    {
        var packetId = global::PacketRouting.ResolvePacketId<global::ResPacket_FetchLatestSynchronousData>();

        app.MapPost("/api/FetchLatestSynchronousData", () =>
        {
            var synchronized = new SynchronizedFormat
            {
                version = 513,
                noticeList = new()
                {
                    new NoticeFormat
                    {
                        id = 9004,
                        version = 513,
                        type_ = 0,
                        startDate = "2024-11-20T00:00:00.000Z",
                        endDate = "2098-12-31T21:00:00.000Z",
                        sprNameList = new() { "200001" },
                        title_KR = "Welcome to Lethe",
                        content_KR = NoticeContent,
                        title_EN = "Welcome to Lethe",
                        content_EN = NoticeContent,
                        title_JP = "Welcome to Lethe",
                        content_JP = NoticeContent,
                    },
                },
                mailContentList = new()
                {
                    new MailContent
                    {
                        id = 5858,
                        version = 9004,
                        senderSprName = "charon",
                        sender_KR = "Charon",
                        content_KR = "Charon found this. It's Charon's. Don't touch.",
                        sender_EN = "Charon",
                        content_EN = "Charon found this. It's Charon's. Don't touch.",
                        sender_JP = "Charon",
                        content_JP = "Charon found this. It's Charon's. Don't touch.",
                    },
                },
            };

            var response = global::ResponsePacket<global::ResPacket_FetchLatestSynchronousData>.Ok(
                new global::ResPacket_FetchLatestSynchronousData(), packetId);
            response.synchronized = synchronized;
            return Results.Json(response, global::PacketJson.Options);
        });

        return app;
    }
}
