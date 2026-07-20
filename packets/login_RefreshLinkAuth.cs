// /login/RefreshLinkAuth  ReqPacket_RefreshLinkAuth -> ResPacket_RefreshLinkAuth
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_RefreshLinkAuth
{
    public string details;
    public long PacketId;
}

public class ResPacket_RefreshLinkAuth
{
    public LinkAuthFormat linkAuth;
    public string state;
    public long PacketId;
}

public class LinkAuthFormat
{
    public long public_id;
    public string password;
    public string expiry_date;
    public string details;
}

