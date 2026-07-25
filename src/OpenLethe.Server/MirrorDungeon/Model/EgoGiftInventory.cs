using System.Collections.Generic;

namespace OpenLethe.Server.MirrorDungeon.Model;

// One acquired E.G.O gift (wire egs[*] / AcquiredEgogifts).
public sealed class EgoGift
{
    public long Id { get; set; }
    public List<long> Pids { get; set; } = new();
    public long Un { get; set; }
    public long Ul { get; set; }            // upgrade level
    public long? Oid { get; set; }          // fusion origin id; null => omit-when-null on the wire
}

// The run's ordered E.G.O gift inventory (wire egs). A thin ordered wrapper for now; the
// Vestige/super rules that consult it arrive with RewardResolution in the handler-migration re-plan.
public sealed class EgoGiftInventory
{
    public List<EgoGift> Items { get; set; } = new();
}
