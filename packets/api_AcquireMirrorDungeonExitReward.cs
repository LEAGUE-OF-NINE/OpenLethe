// /api/AcquireMirrorDungeonExitReward  ReqPacket_AcquireMirrorDungeonExitReward -> ResPacket_AcquireMirrorDungeonExitReward
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_AcquireMirrorDungeonExitReward
{
    public bool useEnkephalinModule;
    public int chanceConsumption;
    public long PacketId;
}

public class ResPacket_AcquireMirrorDungeonExitReward
{
    public List<Element> rewardList;
    public MirrorDungeonSaveInfoFormat saveInfo;
    public MirrorDungeonHistoryFormat history;
    public MirrorDungeonStartBuffInfoFormat startBuffInfo;
    public List<int> currentClearedConstraintIds;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfo;
    public MirrorDungeonHistoryFormat History;
    public MirrorDungeonStartBuffInfoFormat StartBuffInfo;
    public List<int> CurrentClearedConstraintIds;
}

