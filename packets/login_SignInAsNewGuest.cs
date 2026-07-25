// /login/SignInAsNewGuest  ReqPacket_SignInAsNewGuest -> ResPacket_SignInAsNewGuest
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SignInAsNewGuest
{
    public string deviceModel;
    public string deviceLanguage;
    public long PacketId;
}

public class ResPacket_SignInAsNewGuest
{
    public UserAuthFormat userAuth;
    public string authToken;
    public string presenceToken;
    public long PacketId;
}

