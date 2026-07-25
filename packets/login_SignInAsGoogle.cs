// /login/SignInAsGoogle  ReqPacket_SignInAsGoogle -> ResPacket_SignInAsGoogle
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SignInAsGoogle
{
    public string googleToken;
    public string version;
    public string deviceModel;
    public string deviceLanguage;
    public long PacketId;
}

public class ResPacket_SignInAsGoogle
{
    public UserAuthFormat userAuth;
    public AccountInfoFormat accountInfo;
    public string presenceToken;
    public long PacketId;
}

