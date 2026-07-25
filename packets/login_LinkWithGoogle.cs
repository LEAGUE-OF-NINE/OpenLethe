// /login/LinkWithGoogle  ReqPacket_LinkWithGoogle -> ResPacket_LinkWithGoogle
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_LinkWithGoogle
{
    public string googleToken;
    public string version;
    public long PacketId;
}

public class ResPacket_LinkWithGoogle
{
    public UserAuthFormat userAuth;
    public AccountInfoFormat accountInfo;
    public long PacketId;
}

