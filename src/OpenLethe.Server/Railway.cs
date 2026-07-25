using System.Collections.Generic;
using System.Linq;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server;

/// Ports of lethe-server/server/src/api/railway/mod.rs list helpers.
public static class RailwayHelpers
{
    /// Replace the node with the same nodeid, else append.
    public static void UpsertNode(List<UpdateNodeDatas> nodes, UpdateNodeDatas newNode)
    {
        for (var i = 0; i < nodes.Count; i++)
            if (nodes[i].nodeid == newNode.nodeid) { nodes[i] = newNode; return; }
        nodes.Add(newNode);
    }

    /// Return the node with nodeId; if absent, append a fresh default and return it.
    public static UpdateNodeDatas FindOrDefaultNode(List<UpdateNodeDatas> nodes, long nodeId)
    {
        foreach (var n in nodes)
            if (n.nodeid == nodeId) return n;
        var made = new UpdateNodeDatas { nodeid = nodeId };
        nodes.Add(made);
        return made;
    }

    /// Replace the buff set with the same nid, else append.
    public static void UpsertBuff(List<Buffsetsbyegogift> buffs, Buffsetsbyegogift newBuff)
    {
        for (var i = 0; i < buffs.Count; i++)
            if (buffs[i].nid == newBuff.nid) { buffs[i] = newBuff; return; }
        buffs.Add(newBuff);
    }

    /// Buff sets whose nid is strictly less than nodeId.
    public static List<Buffsetsbyegogift> BuffsBelowNode(List<Buffsetsbyegogift> buffs, long nodeId) =>
        buffs.Where(b => b.nid < nodeId).ToList();
}
