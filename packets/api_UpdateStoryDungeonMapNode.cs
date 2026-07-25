// /api/UpdateStoryDungeonMapNode  ReqPacket_UpdateStoryDungeonMapNode -> ResPacket_UpdateStoryDungeonMapNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateStoryDungeonMapNode
{
    public DungeonChoiceEventSaveDataFormat choiceEventData;
    public List<StoryDungeonSaveUnitInfoFormat> dungeonUnitList;
    public List<DungeonMapEgoGiftFormat> updatedEgoGifts;
    public int curEventEncounterId;
    public long PacketId;
}

public class ResPacket_UpdateStoryDungeonMapNode
{
    public List<DungeonChoiceEventSaveDataFormat> prevChoiceEvent;
    public List<DungeonMapEgoGiftFormat> currentEgoGifts;
    public Il2CppReferenceArray<ChoiceEventLogFormat> cels;
    public long PacketId;
    public List<DungeonChoiceEventSaveDataFormat> PrevChoiceEvent;
    public List<DungeonMapEgoGiftFormat> CurrentEgoGifts;
    public Il2CppReferenceArray<ChoiceEventLogFormat> Cels;
}

