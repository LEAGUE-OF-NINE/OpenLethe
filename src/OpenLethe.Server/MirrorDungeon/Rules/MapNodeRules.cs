using System.Linq;
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
    public static (bool changedHiddenNode, long nr) EnterNode(Run run, Currentnode enteredNode)
    {
        var node = run.Floor.Nodes.First(n => n.Nid == enteredNode.nid);

        run.Floor.Current = new CurrentPosition { F = enteredNode.f, S = enteredNode.s, Nid = enteredNode.nid };
        // Capture-confirmed (md-extreme seq316/317 -> seq321): entering a node updates eid the
        // same way ExitMirrorDungeonMapNode does on exit, just to the ENTERED node's own value.
        run.Eid = node.Eid;
        // Event/shop nodes (e==3/10) are worth 3 "node results"; every battle-type node
        // (e in {1,2,5,6,14}) is worth 4. Capture-verified 77/77.
        var nr = (node.E == 3 || node.E == 10) ? 3 : 4;
        run.Nr = nr;

        // Shop is only (re)rolled on entering a shop node (e==10) - other node types leave the
        // tracked shop untouched. Fresh ShopState, not a slots-only mutation: capture-verified
        // (77/77) every scalar (rc/fre/fkre/cf/aec/aesp) is 0 on entry.
        if (node.E == 10)
        {
            run.Shop = new ShopState
            {
                Slots = new MdThemePool().SelectRandomShopEgos(
                    SharedRules.ThemePackId(run), (int)SharedRules.ShopGiftCount(run), SharedRules.CurrentFloor(run), null)
                    .Select(id => new ShopSlotState { T = "eg", Id = id, S = 1 }).ToList(),
            };
        }

        // ponytail: always false in the captured run (77/77) - a hidden node revealed on entry
        // never fires here. Upgrade to a real reveal check when a capture shows it true.
        return (false, nr);
    }
}
