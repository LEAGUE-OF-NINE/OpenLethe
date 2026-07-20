// /api/GetMirrorDungeonEgoGiftRecord  ReqPacket_GetMirrorDungeonEgoGiftRecord -> ResPacket_GetMirrorDungeonEgoGiftRecord
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetMirrorDungeonEgoGiftRecord
{
    public long PacketId;
}

public class ResPacket_GetMirrorDungeonEgoGiftRecord
{
    public Il2CppStructArray<int> acquiredegogifts;
    public Il2CppStructArray<int> themeFloorIds;
    public Il2CppStructArray<int> clearedConstraints;
    public long PacketId;
}

