using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server.MirrorDungeon.Model;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// Domain (Run-mutating) ports of the cross-cutting wire helpers in Handlers/MirrorDungeonMap.cs.
// The old wire-typed helpers stay in place (still called by not-yet-migrated handlers) until
// Task 12 removes them - these are exact behavioral twins, not reinterpretations. See the
// handler-migration Task 2 brief for the source file:line map.
public static class SharedRules
{
    // Port of map/mod.rs current_floor (MirrorDungeonMap.cs:33).
    public static long CurrentFloor(Run run) => run.LevelAdders.Count;

    // Port of map/mod.rs theme_pack_id (MirrorDungeonMap.cs:36).
    public static long ThemePackId(Run run) => run.ThemeFloors.Count > 0 ? run.ThemeFloors[^1].Tfid : 1001;

    // Port of map/mod.rs is_super_shop (MirrorDungeonMap.cs:39-44).
    public static bool? IsSuperShop(Run run)
    {
        var node = run.Floor.Nodes.FirstOrDefault(n => n.Nid == run.Floor.Current.Nid);
        if (node is null || node.E != 10) return null;
        return node.Eid != 0;
    }

    // Port of map/mod.rs shop_gift_count (MirrorDungeonMap.cs:50-55).
    public static long ShopGiftCount(Run run) => IsSuperShop(run) switch
    {
        true => 10,
        false => 5,
        null => 0,
    };

    // Domain port of MergeDungeonUnitList (MirrorDungeonMap.cs:62-72). Merges the client-reported
    // roster onto the server's authoritative party: preserve prior UpgradeIndices by pid, reset
    // Pord to -1 and every EgoSkill's gauge to 0. Same per-pid merge semantics, byte-verified
    // against the wire twin's captures.
    public static List<PartyUnit> MergeParty(List<PartyUnit> prior, List<PartyUnit> incoming)
    {
        var priorUpidx = prior.ToDictionary(u => u.PersonalityId, u => u.UpgradeIndices);
        foreach (var unit in incoming)
        {
            if (priorUpidx.TryGetValue(unit.PersonalityId, out var upidx)) unit.UpgradeIndices = upidx;
            unit.Pord = -1;
            foreach (var ego in unit.EgoSkills) ego.G = 0;
        }
        return incoming;
    }

    // Port of RecomputeEgmlos (MirrorDungeonMap.cs:117).
    public static void RecomputeEgmlos(Run run) =>
        run.LevelOffsets.Egmlos = run.Gifts.Items.Sum(g => Effects.HiddenGiftLevelBump(g.Id));

    // Domain port of GrantEgoGift (MirrorDungeonMap.cs:135-147). Reproduces the already-owned ->
    // tier-matched Vestige rule EXACTLY (see the wire twin's comment for the CEILING on the
    // "already acquired" approximation - unchanged here, only the storage type differs).
    public static void GrantEgoGift(Run run, long giftId)
    {
        if (run.Gifts.Items.Any(g => g.Id == giftId))
        {
            long? superId = OpenLethe.Server.MdEgoData.DetermineEgoTier(giftId) switch { 2 => 9992, 3 => 9993, _ => null };
            if (superId is { } sid)
            {
                run.Gifts.Items.Add(new EgoGift { Id = sid, Oid = giftId });
                return;
            }
        }
        run.Gifts.Items.Add(new EgoGift { Id = giftId });
    }
}
