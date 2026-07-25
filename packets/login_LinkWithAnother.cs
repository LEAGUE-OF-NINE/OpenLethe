// /login/LinkWithAnother  ReqPacket_LinkWithAnother -> ResPacket_LinkWithAnother
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_LinkWithAnother
{
    public string targetPublicId;
    public string password;
    public bool mainIsTarget;
    public long PacketId;
}

public class ResPacket_LinkWithAnother
{
    public string state;
    public long PacketId;
}

