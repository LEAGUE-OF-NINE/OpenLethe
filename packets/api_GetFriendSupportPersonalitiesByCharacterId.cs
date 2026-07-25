// /api/GetFriendSupportPersonalitiesByCharacterId  ReqPacket_GetFriendSupportPersonalitiesByCharacterId -> ResPacket_GetFriendSupportPersonalitiesByCharacterId
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetFriendSupportPersonalitiesByCharacterId
{
    public int characterid;
    public long PacketId;
}

public class ResPacket_GetFriendSupportPersonalitiesByCharacterId
{
    public List<SupportPersonalityFormat> supportpersonalities;
    public long PacketId;
}

public class SupportPersonalityFormat
{
    public int pid;
    public int l;
    public List<ProfileEgoContainIndexFormat> egos;
    public int gl;
    public int gi;
    public int sid;
}

