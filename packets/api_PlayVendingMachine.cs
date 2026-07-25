// /api/PlayVendingMachine  ReqPacket_PlayVendingMachine -> ResPacket_PlayVendingMachine
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_PlayVendingMachine
{
    public int vendingMachineId;
    public string targetType;
    public int targetId;
    public List<int> coupons;
    public bool isPaidByLunacy;
    public long PacketId;
}

public class ResPacket_PlayVendingMachine
{
    public List<ItemFormat> itemConsumptions;
    public long PacketId;
}

