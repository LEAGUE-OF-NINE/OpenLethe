// /api/UpdateAnnouncerPreset  ReqPacket_UpdateAnnouncerPreset -> ResPacket_UpdateAnnouncerPreset
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_UpdateAnnouncerPreset
{
    public int presetId;
    public List<int> presetAnnouncerIds;
    public long PacketId;
}

public class ResPacket_UpdateAnnouncerPreset
{
    public long PacketId;
}

