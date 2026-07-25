// /api/UpdateLobbyCg  ReqPacket_UpdateLobbyCg -> ResPacket_UpdateLobbyCg
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateLobbyCg
{
    public LobbyCgFormat lobbyCg;
    public long PacketId;
}

public class ResPacket_UpdateLobbyCg
{
    public long PacketId;
}

public class LobbyCgFormat
{
    public int characterId;
    public List<LobbyCgDetailFormat> lobbycgdetails;
    public bool isShowProfile;
}

public class LobbyCgDetailFormat
{
    public int id;
    public int g;
}

