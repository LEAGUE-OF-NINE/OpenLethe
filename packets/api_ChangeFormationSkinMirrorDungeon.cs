// /api/ChangeFormationSkinMirrorDungeon  ReqPacket_ChangeFormationSkinMirrorDungeon -> ResPacket_ChangeFormationSkinMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_ChangeFormationSkinMirrorDungeon
{
    public List<MirrorDungeonFormationFormat> formation;
    public long PacketId;
}

public class ResPacket_ChangeFormationSkinMirrorDungeon
{
    public List<MirrorDungeonSaveUnitInfoFormat> dungeonUnitList;
    public long PacketId;
    public List<MirrorDungeonSaveUnitInfoFormat> DungeonUnitList;
}

