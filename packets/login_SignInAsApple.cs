// /login/SignInAsApple  ReqPacket_SignInAsApple -> ResPacket_SignInAsApple
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SignInAsApple
{
    public string appleToken;
    public string version;
    public string deviceModel;
    public string deviceLanguage;
    public long PacketId;
}

public class ResPacket_SignInAsApple
{
    public UserAuthFormat userAuth;
    public AccountInfoFormat accountInfo;
    public string presenceToken;
    public long PacketId;
}

