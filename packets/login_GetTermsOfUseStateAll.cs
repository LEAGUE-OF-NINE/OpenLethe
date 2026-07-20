// /login/GetTermsOfUseStateAll  ReqPacket_GetTermsOfUseStateAll -> ResPacket_GetTermsOfUseStateAll
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetTermsOfUseStateAll
{
    public long uid;
    public long PacketId;
}

public class ResPacket_GetTermsOfUseStateAll
{
    public List<TermsOfUseState> termsOfUseStateList;
    public long PacketId;
}

public class TermsOfUseState
{
    public int version;
    public int state;
    public int Version;
    public TERMSOFUSE_STATE State;
}

public enum TERMSOFUSE_STATE { NONE, AGREE, DISAGREE }

