// /api/EnterStoryMirrorDungeonMapNodeBattleAfterChoice  ReqPacket_EnterStoryMirrorDungeonMapNodeBattleAfterChoice -> ResPacket_EnterStoryMirrorDungeonMapNodeBattleAfterChoice
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterStoryMirrorDungeonMapNodeBattleAfterChoice
{
    public List<int> participatedPids;
    public List<int> abnormalityids;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public long PacketId;
}

public class ResPacket_EnterStoryMirrorDungeonMapNodeBattleAfterChoice
{
    public List<AbnormalityUnlockInformationFormat> abnormalityLogs;
    public long PacketId;
    public List<AbnormalityUnlockInformationFormat> AbnormalityLogs;
}

