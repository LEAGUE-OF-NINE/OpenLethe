using OpenLethe.Server.MirrorDungeon.Data;
using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.MirrorDungeon.Rules;

// Node-entry lifecycle. Verbatim port of the EnterMirrorDungeonMapNode handler body
// (Handlers/MirrorDungeonMap.cs :244-306). ExitMapNode (including its reward/rre engine and the
// LevelOffsetEngine methods it drives - sbmlos/egmlos/snft/csnft) is Task 7, deliberately NOT
// touched here: EnterMapNode never reads or writes run.LevelOffsets, so no LevelOffsetEngine
// method is warranted in this task (YAGNI - see the Task 6 brief).
public static class MapNodeRules
{
    // Port of the EnterMirrorDungeonMapNode handler body. Caller (the handler) is responsible for
    // validating the entered node exists in run.Floor.Nodes before calling this - request
    // validation, not game logic.
    // hiddenBattle: the hiddenBattleInfo to roll against (the current season's table by default).
    // customThemes: the account's uploaded packs, so a shop on a custom pack's floor stocks its gifts.
    public static (bool changedHiddenNode, long nr) EnterNode(Run run, Currentnode enteredNode,
        MdHiddenBattleInfo? hiddenBattle = null, IReadOnlyList<ThemeStatic>? customThemes = null)
    {
        var node = run.Floor.Nodes.First(n => n.Nid == enteredNode.nid);
        // Only a step onto a new node can reveal one. The client enters the same node again to
        // start its fight (md-extreme-run-2 seq336/338), and a revealed or skipped node must not
        // roll a second time.
        var firstEntry = run.Floor.Current.Nid != node.Nid && !run.Pnids.Contains(node.Nid);
        var changedHiddenNode = firstEntry
            && TryRevealHiddenBattle(run, node, hiddenBattle ?? MdCommonTable.HiddenBattle);

        run.Floor.Current = new CurrentPosition { F = enteredNode.f, S = enteredNode.s, Nid = enteredNode.nid };
        // Capture-confirmed (md-extreme seq316/317 -> seq321): entering a node updates eid the
        // same way ExitMirrorDungeonMapNode does on exit, just to the ENTERED node's own value.
        run.Eid = node.Eid;
        // Event/shop nodes (e==3/10) are worth 3 "node results"; every battle-type node
        // (e in {1,2,5,6,14,15}) is worth 4. Capture-verified 77/77.
        // The entry that reveals a hidden node is the exception: it records 5 - md-extreme-run-2
        // seq335/437 are the only rows whose changedHiddenNode flips true, and both carry "nr": 5,
        // while the re-entries that start the hidden fight (seq336/338/438) carry 4.
        var nr = changedHiddenNode ? 5
               : (node.E == 3 || node.E == 10) ? 3
               : 4;
        run.Nr = nr;

        // Shop is only (re)rolled on entering a shop node (e==10) - other node types leave the
        // tracked shop untouched. Fresh ShopState, not a slots-only mutation: capture-verified
        // (77/77) every scalar (rc/fre/fkre/cf/aec/aesp) is 0 on entry.
        if (node.E == 10)
        {
            // Slot counts come from the retail table (shopInfoByType): 4 gift slots in a
            // regular shop and 7 in the super shop, preceded by its skill-replacement slots - one
            // named-sinner "up" slot per skillSlotNum (id = the personality id the up-branch of
            // PurchaseUpgradePersonality matches on, i.e. "replace a skill of this sinner") and one
            // "upt" slot per skillTargetingSlotNum, which is the one that lets the buyer pick the
            // skill and can be paid with the run's starlight instead of funds (super shop only).
            // PurchaseEgoGift's idx skips those leading non-eg slots, per the reference captures.
            var super = SharedRules.IsSuperShop(run) ?? false;
            var type = MdShopTable.ShopType(super);

            var slots = new List<ShopSlotState>();
            // Captured super shops list the targeting slot first (upt:0, then up:<pid> x2, then the
            // eg slots) - see md-extreme-run seq67/96.
            for (var i = 0; i < (type?.skillTargetingSlotNum ?? 0); i++)
                slots.Add(new ShopSlotState { T = "upt", Id = 0, S = 1 });
            // Each named slot goes to a different sinner, drawn at random from the ones still
            // standing - taking the party in order offered only sinner 1 (and 2 in a super shop).
            var namedUpgradeSlots = (int)(type?.skillSlotNum ?? 0);
            foreach (var pid in SharedRules.RandomLivingSinners(run).Take(namedUpgradeSlots))
                slots.Add(new ShopSlotState { T = "up", Id = pid, S = 1 });

            slots.AddRange(new MdThemePool(customThemes).SelectRandomShopEgos(
                    SharedRules.ThemePackId(run), (int)SharedRules.ShopGiftCount(run), SharedRules.CurrentFloor(run), null,
                    SharedRules.OwnedGiftIds(run), StartBuffEffects.ShopHighTierMultiplier(run), run.Idx)
                .Select(id => new ShopSlotState { T = "eg", Id = id, S = 1 }));

            // fre/fkre: the visit's free normal / keyword refreshes still available (start buffs).
            run.Shop = new ShopState
            {
                Slots = slots,
                Fre = StartBuffEffects.FreeRefreshes(run),
                Fkre = StartBuffEffects.FreeKeywordRefreshes(run),
            };

            // The start buffs' shop-entry rewards (Binary Star-shop+ Cost, Chance Comet Vestige),
            // once per visit - the response's cost/egogifts carry them to the client.
            if (firstEntry) StartBuffEffects.OnEnterShop(run);
        }

        // True only on the entry that revealed the node: the client then reloads its map from the
        // response's dungeonMap and offers the fight or the skip.
        return (changedHiddenNode, nr);
    }

    // Hidden bosses (mirror-dungeon-common-data hiddenBattleInfo): from minFloorCondition on,
    // entering an event node has the floor's prob (10% on floors 11-15) of turning it into a
    // hidden boss - an unbeaten pool stage, keeping the node's edges. Both retail reveals
    // (md-extreme-run-2 seq335, seq437) were event nodes, and skipping one turns it back into an
    // event, so only event nodes qualify. The event it replaced is kept for that skip.
    public static bool TryRevealHiddenBattle(Run run, MapNode node, MdHiddenBattleInfo info)
    {
        if (node.E != 3) return false;
        if (Random.Shared.NextDouble() >= info.ProbForFloor(node.F)) return false;

        var pool = MdMapGen.UnbeatenHiddenBattles(info, SharedRules.OwnedGiftIds(run));
        if (pool.Count == 0) return false;

        run.HiddenNodeOrigins.RemoveAll(o => o.Nid == node.Nid);
        run.HiddenNodeOrigins.Add(new HiddenNodeOrigin { Nid = node.Nid, Eid = node.Eid });
        node.E = MdMapGen.HiddenBattleNodeType;
        node.Eid = pool[Random.Shared.Next(pool.Count)];
        return true;
    }

    // EscapeMirrorDungeonHiddenNode: the player skipped the hidden boss. The node goes back to the
    // event it was and counts as cleared - recorded in pnids with nr 1, exactly the bookkeeping an
    // ExitMapNode does - so the client lets the party move on. Nothing is granted: no rewards, no
    // peids entry (the event was not played). A node with no recorded origin (one a save
    // pre-placed before reveals happened on entry) gets an event from the dungeon's pool.
    // Returns false when the node is not a hidden node.
    public static bool EscapeHiddenNode(Run run, Currentnode at)
    {
        var node = run.Floor.Nodes.FirstOrDefault(n => n.Nid == at.nid);
        if (node is null || node.E != MdMapGen.HiddenBattleNodeType) return false;

        var origin = run.HiddenNodeOrigins.FirstOrDefault(o => o.Nid == node.Nid);
        node.E = 3;
        node.Eid = origin?.Eid ?? MdMapGen.PickFallbackEventId();
        run.HiddenNodeOrigins.RemoveAll(o => o.Nid == node.Nid);

        run.Floor.Current = new CurrentPosition { F = node.F, S = node.S, Nid = node.Nid };
        run.Eid = node.Eid;
        if (!run.Pnids.Contains(node.Nid)) run.Pnids.Add(node.Nid);
        run.Nr = 1;
        run.RewardEvents = new();
        run.ChoiceEvents.Clear();
        run.Etype = -1;
        return true;
    }
}
