// /iap/Purchase  ReqPacket_Purchase -> ResPacket_Purchase
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_Purchase
{
    public string productId;
    public string receipt;
    public int platform;
    public long PacketId;
}

public class ResPacket_Purchase
{
    public long PacketId;
}

