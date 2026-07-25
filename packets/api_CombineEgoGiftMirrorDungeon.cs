// /api/CombineEgoGiftMirrorDungeon  ReqPacket_CombineEgoGiftMirrorDungeon -> ResPacket_CombineEgoGiftMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_CombineEgoGiftMirrorDungeon
{
    public List<int> materialEgoGiftIds;
    public string keyword;
    public bool enableStarlight;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_CombineEgoGiftMirrorDungeon
{
    public DungeonMapEgoGiftFormat resultEgoGift;
    public Il2CppReferenceArray<DungeonMapEgoGiftFormat> resultEgoGifts;
    public bool isSuccess;
    public List<DungeonMapEgoGiftFormat> egoGifts;
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public StarlightInfoFormat starlightInfo;
    public long PacketId;
    public DungeonMapEgoGiftFormat ResultEgoGift;
    public Il2CppReferenceArray<DungeonMapEgoGiftFormat> ResultEgoGifts;
    public bool IsSuccess;
    public List<DungeonMapEgoGiftFormat> EgoGifts;
    public List<MirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
    public StarlightInfoFormat StarlightInfo;
}

