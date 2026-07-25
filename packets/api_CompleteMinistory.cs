// /api/CompleteMinistory  ReqPacket_CompleteMinistory -> ResPacket_CompleteMinistory
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_CompleteMinistory
{
    public string storyId;
    public long PacketId;
}

public class ResPacket_CompleteMinistory
{
    public long PacketId;
}

