// /api/EnableSpecialStartBuffMirrorDungeon  ReqPacket_EnableSpecialStartBuffMirrorDungeon -> ResPacket_EnableSpecialStartBuffMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnableSpecialStartBuffMirrorDungeon
{
    public int dungeonid;
    public Il2CppStructArray<int> buffids;
    public long PacketId;
}

public class ResPacket_EnableSpecialStartBuffMirrorDungeon
{
    public MirrorDungeonStartBuffInfoFormat startBuffInfo;
    public long PacketId;
}

