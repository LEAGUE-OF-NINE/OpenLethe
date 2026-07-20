// /login/UnLinkWithAnother  ReqPacket_UnLinkWithAnother -> ResPacket_UnLinkWithAnother
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UnLinkWithAnother
{
    public AccountInfoFormat accountInfo;
    public bool isUnlinkGoogle;
    public bool isUnlinkApple;
    public bool isUnlinkSteam;
    public string accountType;
    public long PacketId;
}

public class ResPacket_UnLinkWithAnother
{
    public AccountInfoFormat accountInfo;
    public long PacketId;
    public AccountInfoFormat AccountInfo;
}

