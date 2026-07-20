// /api/CreatePersonalitySkin  ReqPacket_CreatePersonalitySkin -> ResPacket_CreatePersonalitySkin
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_CreatePersonalitySkin
{
    public int skinId;
    public List<ItemFormat> consumeItems;
    public long PacketId;
}

public class ResPacket_CreatePersonalitySkin
{
    public long PacketId;
}

