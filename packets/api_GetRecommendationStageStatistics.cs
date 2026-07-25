// /api/GetRecommendationStageStatistics  ReqPacket_GetRecommendationStageStatistics -> ResPacket_GetRecommendationStageStatistics
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetRecommendationStageStatistics
{
    public int type;
    public int subchapterId;
    public int nodeId;
    public long PacketId;
}

public class ResPacket_GetRecommendationStageStatistics
{
    public List<UserStageStatisticFormat> stageStatistics;
    public long PacketId;
    public List<UserStageStatisticFormat> StageStatistics;
}

public class UserStageStatisticFormat
{
    public long public_uid;
    public int subchapterid;
    public int nodeid;
    public int turn;
    public int isexclear;
    public List<UserStageStatisticPersonalityFormat> unitinfo;
    public long Public_uid;
    public int Subchapterid;
    public int NodeId;
    public int Turn;
    public int IsExclear;
    public List<UserStageStatisticPersonalityFormat> UnitInfo;
}

