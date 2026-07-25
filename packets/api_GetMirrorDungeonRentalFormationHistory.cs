// /api/GetMirrorDungeonRentalFormationHistory  ReqPacket_GetMirrorDungeonRentalFormationHistory -> ResPacket_GetMirrorDungeonRentalFormationHistory
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetMirrorDungeonRentalFormationHistory
{
    public long PacketId;
}

public class ResPacket_GetMirrorDungeonRentalFormationHistory
{
    public Il2CppStructArray<int> activatedRentalFormationIds;
    public Il2CppStructArray<int> alreadyRentedFormationIds;
    public long PacketId;
}

