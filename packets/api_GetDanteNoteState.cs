// /api/GetDanteNoteState  ReqPacket_GetDanteNoteState -> ResPacket_GetDanteNoteState
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetDanteNoteState
{
    public long PacketId;
}

public class ResPacket_GetDanteNoteState
{
    public int page;
    public int todayPage;
    public long PacketId;
}

