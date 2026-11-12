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

    // Every gift id the run owns - what a shop must never offer or sell: the gifts it holds, plus
    // the components of every fixed-recipe fusion gift it holds (MdEgoFusion.FusionComponents),
    // which count as owned and never come back. Lunar Memory's parts are not among them.
    public static HashSet<long> OwnedGiftIds(Run run)
    {
        var owned = run.Gifts.Items.Select(g => g.Id).ToHashSet();
        foreach (var id in owned.ToList())
            owned.UnionWith(OpenLethe.Server.MdEgoFusion.FusionComponents(id));
        return owned;
    }

    // Whether the run owns giftId in the OwnedGiftIds sense.
    public static bool OwnsGift(Run run, long giftId) =>
        run.Gifts.Items.Any(g => g.Id == giftId)
        || run.Gifts.Items.Any(g => OpenLethe.Server.MdEgoFusion.FusionComponents(g.Id).Contains(giftId));

    // Port of map/mod.rs is_super_shop (MirrorDungeonMap.cs:39-44).
    public static bool? IsSuperShop(Run run)
    {
        var node = run.Floor.Nodes.FirstOrDefault(n => n.Nid == run.Floor.Current.Nid);
        if (node is null || node.E != 10) return null;
        return node.Eid != 0;
    }

    // Port of map/mod.rs shop_gift_count (MirrorDungeonMap.cs:50-55), but the counts come from
    // the retail table now (shopInfoByType.egoGiftSlotNum: 4 regular / 7 super) instead of the
    // reference server's hardcoded 5/10, plus the start buffs' "Shop E.G.O Gift Choice +1"
    // (Star of the Beginning, Binary Star-shop).
    public static long ShopGiftCount(Run run) => IsSuperShop(run) switch
    {
        true => MdShopTable.GiftSlots(true) + StartBuffEffects.ShopGiftSlotBonus(run),
        false => MdShopTable.GiftSlots(false) + StartBuffEffects.ShopGiftSlotBonus(run),
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

    // The sinners a named skill-replacement ("up") shop slot can go to: the living ones, in a
    // random order, leaving out the pids in `exclude` (the shop's other "up" slots).
    public static IEnumerable<long> RandomLivingSinners(Run run, IEnumerable<long>? exclude = null)
    {
        var taken = exclude?.ToHashSet() ?? new HashSet<long>();
        var living = run.Party.Where(u => u.CurrentHp > 0 && !taken.Contains(u.PersonalityId))
            .Select(u => u.PersonalityId).ToArray();
        Random.Shared.Shuffle(living);
        return living;
    }

    // 9273 "Spiderweb Entangled in Red": while Blade of the House of Spiders Ryoshu (10415) is in
    // the party, no other defeated identity is revived after clearing a floor boss or a hidden node.
    private const long SpiderwebEntangledInRedGift = 9273;
    private const long BladeOfTheHouseOfSpidersRyoshu = 10415;

    // The "revive all" after a floor boss (applied on the next floor's start) and a hidden-boss
    // clear: every unit back to full hp, and a unit revived from 0 hp also has its Cm (corrosion)
    // cleared - live units keep their Cm (md-extreme-run-2 seq339). Shop revives don't go here.
    public static void ReviveAll(Run run)
    {
        var noRevive = run.Gifts.Items.Any(g => g.Id == SpiderwebEntangledInRedGift)
            && run.Party.Any(u => u.PersonalityId == BladeOfTheHouseOfSpidersRyoshu);
        foreach (var u in run.Party)
        {
            if (u.CurrentHp == 0)
            {
                if (noRevive && u.PersonalityId != BladeOfTheHouseOfSpidersRyoshu) continue;
                u.Cm = 0;
            }
            u.CurrentHp = 10000;
        }
    }

    // Port of RecomputeEgmlos (MirrorDungeonMap.cs:117).
    public static void RecomputeEgmlos(Run run) =>
        run.LevelOffsets.Egmlos = run.Gifts.Items.Sum(g => Effects.HiddenGiftLevelBump(g.Id));

    // Domain port of GrantEgoGift (MirrorDungeonMap.cs:135-147). Reproduces the already-owned ->
    // tier-matched Vestige rule EXACTLY (see the wire twin's comment for the CEILING on the
    // "already acquired" approximation - unchanged here, only the storage type differs).
    // Every tier has its Vestige (Dark/Faint/Twinkling/Brilliant/Lunar, 9991-9995): only mapping
    // Tiers 2 and 3 handed a second copy of an owned Tier 1/4/5 gift (Thorny Path twice off an
    // abno node's reward). A fusion component counts as owned (OwnsGift) and turns into one too.
    public static void GrantEgoGift(Run run, long giftId)
    {
        if (OwnsGift(run, giftId))
        {
            long? superId = OpenLethe.Server.MdEgoData.DetermineEgoTier(giftId) is { } tier and >= 1 and <= 5
                ? StartBuffEffects.VestigeOfTier(tier)
                : null;
            if (superId is { } sid)
            {
                run.Gifts.Items.Add(new EgoGift { Id = sid, Oid = giftId });
                return;
            }
        }
        run.Gifts.Items.Add(new EgoGift { Id = giftId });
    }
}
