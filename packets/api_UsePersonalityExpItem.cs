// /api/UsePersonalityExpItem  ReqPacket_UsePersonalityExpItem -> ResPacket_UsePersonalityExpItem
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UsePersonalityExpItem
{
    public int targetPersonalityId;
    public List<ItemFormat> items;
    public long PacketId;
}

public class ResPacket_UsePersonalityExpItem
{
    public PersonalityFormat resultPersonality;
    public long PacketId;
}

public class PersonalityFormat
{
    public int personality_id;
    public int level;
    public int exp;
    public int gacksung;
    public int order_id;
    public int gacksung_illust_type;
    public string acquire_time;
}

