// /api/UpdateMirrorDungeon  ReqPacket_UpdateMirrorDungeon -> ResPacket_UpdateMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateMirrorDungeon
{
    public List<MirrorDungeonGetCharacterInfoFormat> characterInfos;
    public List<MirrorDungeonFormationFormat> formation;
    public long PacketId;
    public uint isOrigin;
}

public class ResPacket_UpdateMirrorDungeon
{
    public MirrorDungeonSaveInfoFormat saveInfo;
    public long PacketId;
    public MirrorDungeonSaveInfoFormat SaveInfo;
}

