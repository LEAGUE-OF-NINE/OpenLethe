// /api/ProjectGSChangeGameState  ReqPacket_ProjectGSChangeGameState -> ResPacket_ProjectGSChangeGameState
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ProjectGSChangeGameState
{
    public ProjectGSGameState targetState;
    public long PacketId;
}

public class ResPacket_ProjectGSChangeGameState
{
    public ProjectGSGameData gameData;
    public long PacketId;
}

