// /api/EnableStartBuffMirrorDungeon  ReqPacket_EnableStartBuffMirrorDungeon -> ResPacket_EnableStartBuffMirrorDungeon
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_EnableStartBuffMirrorDungeon
{
    public int dungeonid;
    public Il2CppStructArray<int> buffids;
    public bool enableStarlight;
    public bool enableConvertedCost;
    public long PacketId;
}

public class ResPacket_EnableStartBuffMirrorDungeon
{
    public MirrorDungeonStartBuffInfoFormat startBuffInfo;
    public int startBufPoint;
    public int cost;
    public StarlightInfoFormat starlightInfo;
    public long PacketId;
    public int Cost;
    public StarlightInfoFormat StarlightInfo;
}

