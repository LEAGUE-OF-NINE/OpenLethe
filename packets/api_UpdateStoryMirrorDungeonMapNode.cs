// /api/UpdateStoryMirrorDungeonMapNode  ReqPacket_UpdateStoryMirrorDungeonMapNode -> ResPacket_UpdateStoryMirrorDungeonMapNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateStoryMirrorDungeonMapNode
{
    public DungeonMapNodeFormat currentnode;
    public DungeonChoiceEventSaveDataFormat choiceEventData;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public List<DungeonMapEgoGiftFormat> updatedEgoGifts;
    public long PacketId;
}

public class ResPacket_UpdateStoryMirrorDungeonMapNode
{
    public List<DungeonChoiceEventSaveDataFormat> prevChoiceEvent;
    public List<DungeonMapEgoGiftFormat> currentEgoGifts;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public Il2CppReferenceArray<ChoiceEventLogFormat> cels;
    public long PacketId;
    public List<DungeonChoiceEventSaveDataFormat> PrevChoiceEvent;
    public List<DungeonMapEgoGiftFormat> CurrentEgoGifts;
    public List<StoryMirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public Il2CppReferenceArray<ChoiceEventLogFormat> Cels;
}

