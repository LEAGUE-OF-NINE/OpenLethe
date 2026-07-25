// /login/LinkWithApple  ReqPacket_LinkWithApple -> ResPacket_LinkWithApple
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_LinkWithApple
{
    public string appleToken;
    public string version;
    public long PacketId;
}

public class ResPacket_LinkWithApple
{
    public UserAuthFormat userAuth;
    public AccountInfoFormat accountInfo;
    public long PacketId;
}

