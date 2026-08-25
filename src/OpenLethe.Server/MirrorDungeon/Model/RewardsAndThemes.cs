namespace OpenLethe.Server.MirrorDungeon.Model;

// A queued reward/event popup the client must resolve (wire rre[*] / RemainRewardEvent).
// PoolV2/PoolV3 are null when omitted on the wire and MUST stay null (distinct bytes from []).
public sealed class RewardEvent
{
    public string Rt { get; set; } = "";
    public long Se { get; set; }
    public long Sh { get; set; }
    public List<long> Pool { get; set; } = new();
    public List<long>? PoolV2 { get; set; }
    public List<long>? PoolV3 { get; set; }
}

// A rollable theme-floor option (wire tfps[*] / Tfps).
public sealed class ThemePool
{
    public long Idx { get; set; }
    public long Tfid { get; set; }
    public List<long> Egs { get; set; } = new();
    public List<long> Upegs { get; set; } = new();
}

// A selected/committed theme floor (wire tfs[*] / Tfs).
public sealed class ThemeFloor
{
    public long Idx { get; set; }
    public long F { get; set; }
    public long Tfid { get; set; }
    public List<long> Egs { get; set; } = new();
    public List<long> Upegs { get; set; } = new();
    public long Ch { get; set; }
}
