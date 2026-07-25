// /login/TryToSecede  ReqPacket_TryToSecede -> ResPacket_TryToSecede
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_TryToSecede
{
    public long PacketId;
}

public class ResPacket_TryToSecede
{
    public string secessionDate;
    public DateUtil _secessionDate;
    public long PacketId;
    public DateUtil SecessionDate;
}

