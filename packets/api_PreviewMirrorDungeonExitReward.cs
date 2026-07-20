// /api/PreviewMirrorDungeonExitReward  ReqPacket_PreviewMirrorDungeonExitReward -> ResPacket_PreviewMirrorDungeonExitReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PreviewMirrorDungeonExitReward
{
    public long PacketId;
}

public class ResPacket_PreviewMirrorDungeonExitReward
{
    public List<MirrorDungeonExitRewardFormat> rewardList;
    public int totalConstraintScore;
    public long PacketId;
    public List<MirrorDungeonExitRewardFormat> RewardList;
    public int TotalConstraintScore;
}

public class MirrorDungeonExitRewardFormat
{
    public int chanceConsumption;
    public List<Element> rewardList;
    public int moduleConsumption;
    public int starlightConsumption;
    public int mdpassOriginalAmount;
    public int mdpassCurrentChanceUsage;
    public int ChanceConsumption;
    public int ModuleConsumption;
    public int StarlightConsumption;
    public int mdpassOriginal;
    public int mdpassCurrent;
    public List<Element> GetItems;
    public int GetUserEXP;
    public int GetBattlePassPoint;
}

