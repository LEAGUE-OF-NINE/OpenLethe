// /api/ProjectGSStartGame  ReqPacket_ProjectGSStartGame -> ResPacket_ProjectGSStartGame
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ProjectGSStartGame
{
    public bool isRestart;
    public long PacketId;
}

public class ResPacket_ProjectGSStartGame
{
    public ProjectGSGameData gameData;
    public List<int> discoveredComboIds;
    public long PacketId;
}

