// /api/CompleteTheaterStory  ReqPacket_CompleteTheaterStory -> ResPacket_CompleteTheaterStory
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_CompleteTheaterStory
{
    public string storyId;
    public long PacketId;
}

public class ResPacket_CompleteTheaterStory
{
    public bool isRewarded;
    public List<Element> acquiredElements;
    public UserTheaterInfoFormat theaterInfo;
    public long PacketId;
}

