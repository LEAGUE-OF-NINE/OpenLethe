// /api/GetAnnouncerPresetInfo  ReqPacket_GetAnnouncerPresetInfo -> ResPacket_GetAnnouncerPresetInfo
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetAnnouncerPresetInfo
{
    public long PacketId;
}

public class ResPacket_GetAnnouncerPresetInfo
{
    public AnnouncerFormat announcerInfo;
    public long PacketId;
    public AnnouncerFormat AnnouncerInfo;
}

public class AnnouncerFormat
{
    public List<int> announcer_ids;
    public int cur_preset_id;
    public List<AnnouncerPresetFormat> presets;
}

public class AnnouncerPresetFormat
{
    public int presetId;
    public List<int> presetAnnouncerIds;
}

