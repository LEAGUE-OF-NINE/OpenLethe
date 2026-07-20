// /api/ProjectGSGetGameStatus  ReqPacket_ProjectGSGetGameStatus -> ResPacket_ProjectGSGetGameStatus
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ProjectGSGetGameStatus
{
    public long PacketId;
}

public class ResPacket_ProjectGSGetGameStatus
{
    public ProjectGSGameData gameData;
    public List<int> discoveredComboIds;
    public long PacketId;
}

