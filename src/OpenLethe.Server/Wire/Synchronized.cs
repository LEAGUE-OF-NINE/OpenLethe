using System.Text.Json.Serialization;

namespace OpenLethe.Server.Wire;

// Server-authored port of lethe-server/models/src/server.rs SynchronizedFormat +
// members. Carried in the envelope's `synchronized` field (typed object?). Field
// names are the wire contract - do not rename. Public fields (PacketJson.Options
// has IncludeFields=true).

public sealed class SynchronizedFormat
{
    public int version;
    public List<NoticeFormat> noticeList = new();
    public List<MailContent> mailContentList = new();
}

public sealed class NoticeFormat
{
    public int id;
    public int version;
    [JsonPropertyName("type")] public int type_;
    public string startDate = "";
    public string endDate = "";
    public List<string> sprNameList = new();
    public string title_KR = "";
    public string content_KR = "";
    public string title_EN = "";
    public string content_EN = "";
    public string title_JP = "";
    public string content_JP = "";
}

public sealed class MailContent
{
    public int id;
    public int version;
    public string senderSprName = "";
    public string sender_KR = "";
    public string content_KR = "";
    public string sender_EN = "";
    public string content_EN = "";
    public string sender_JP = "";
    public string content_JP = "";
}
