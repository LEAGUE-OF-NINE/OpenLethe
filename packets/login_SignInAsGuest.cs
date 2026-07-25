// /login/SignInAsGuest  ReqPacket_SignInAsGuest -> ResPacket_SignInAsGuest
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SignInAsGuest
{
    public long guestId;
    public string authToken;
    public string version;
    public string deviceModel;
    public string deviceLanguage;
    public long PacketId;
}

public class ResPacket_SignInAsGuest
{
    public UserAuthFormat userAuth;
    public string presenceToken;
    public long PacketId;
}

