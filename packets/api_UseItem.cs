// /api/UseItem  ReqPacket_UseItem -> ResPacket_UseItem
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UseItem
{
    public int itemId;
    public int usage;
    public int targetIdx;
    public Element target;
    public long PacketId;
}

public class ResPacket_UseItem
{
    public List<Element> pickedUpElementList;
    public List<Element> resultElementList;
    public long PacketId;
}

