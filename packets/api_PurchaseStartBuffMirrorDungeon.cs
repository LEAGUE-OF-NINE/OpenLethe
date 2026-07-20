// /api/PurchaseStartBuffMirrorDungeon  ReqPacket_PurchaseStartBuffMirrorDungeon -> ResPacket_PurchaseStartBuffMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PurchaseStartBuffMirrorDungeon
{
    public int dungeonid;
    public Il2CppStructArray<int> buffids;
    public long PacketId;
}

public class ResPacket_PurchaseStartBuffMirrorDungeon
{
    public MirrorDungeonStartBuffInfoFormat startBuffInfo;
    public long PacketId;
}

