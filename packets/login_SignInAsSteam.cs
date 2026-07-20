// /login/SignInAsSteam  ReqPacket_SignInAsSteam -> ResPacket_SignInAsSteam
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_SignInAsSteam
{
    public string steamToken;
    public string version;
    public string deviceModel;
    public string deviceLanguage;
    public long PacketId;
}

public class ResPacket_SignInAsSteam
{
    public UserAuthFormat userAuth;
    public AccountInfoFormat accountInfo;
    public string walletCurrency;
    public string presenceToken;
    public long PacketId;
}

