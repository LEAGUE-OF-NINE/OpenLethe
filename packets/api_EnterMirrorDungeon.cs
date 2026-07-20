// /api/EnterMirrorDungeon  ReqPacket_EnterMirrorDungeon -> ResPacket_EnterMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnterMirrorDungeon
{
    public int dungeonid;
    public int rentalFormationId;
    public int idx;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_EnterMirrorDungeon
{
    public MirrorDungeonSaveInfoFormat saveInfo;
    public List<MirrorDungeonGetCharacterInfoFormat> recentCharacterList;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfo;
    public List<MirrorDungeonGetCharacterInfoFormat> RecentCharacterList;
}

