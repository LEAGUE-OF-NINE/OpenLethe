// /login/UpdateTermsOfUseState  ReqPacket_UpdateTermsOfUseState -> ResPacket_UpdateTermsOfUseState
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateTermsOfUseState
{
    public long uid;
    public int termsVersion;
    public int state;
    public long PacketId;
}

public class ResPacket_UpdateTermsOfUseState
{
    public long PacketId;
}

