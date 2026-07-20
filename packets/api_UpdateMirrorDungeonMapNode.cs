// /api/UpdateMirrorDungeonMapNode  ReqPacket_UpdateMirrorDungeonMapNode -> ResPacket_UpdateMirrorDungeonMapNode
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateMirrorDungeonMapNode
{
    public DungeonMapNodeFormat currentnode;
    public DungeonChoiceEventSaveDataFormat choiceEventData;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public List<DungeonMapEgoGiftFormat> updatedEgoGifts;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_UpdateMirrorDungeonMapNode
{
    public List<DungeonChoiceEventSaveDataFormat> prevChoiceEvent;
    public List<DungeonMapEgoGiftFormat> currentEgoGifts;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public Il2CppReferenceArray<ChoiceEventLogFormat> cels;
    public long PacketId;
    public List<DungeonChoiceEventSaveDataFormat> PrevChoiceEvent;
    public List<DungeonMapEgoGiftFormat> CurrentEgoGifts;
    public List<MirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public Il2CppReferenceArray<ChoiceEventLogFormat> Cels;
}

