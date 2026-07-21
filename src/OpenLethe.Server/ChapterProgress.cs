using System.Collections.Generic;
using OpenLethe.Data;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server;

/// Port of lethe-server/server/src/chapterstate.rs ChapterState::register_won_node.
/// Marks a battle/story node as won in the account's persisted chapter state.
public static class ChapterProgress
{
    /// Set the node with `nodeId` to won (ct=2, cn=1, dn=0) across the whole chapter
    /// tree, mutate account.ChapterState, and return the UpdatedFormat carrying the
    /// new list. A node id absent from the tree leaves the state unchanged (matches Rust).
    // ponytail: reads the PERSISTED column (a fresh deserialization), never the memoized
    // default builder from cycle 3b - so the shallow-copy shared-ref hazard does not apply.
    public static UpdatedFormat RegisterWonNode(Account account, long nodeId)
    {
        var state = AccountFields.Get<List<MainChapterState>>(account.ChapterState)
            ?? new List<MainChapterState>();

        foreach (var chapter in state)
            foreach (var sub in chapter.subcss)
                for (var i = 0; i < sub.nss.Count; i++)
                    if (sub.nss[i].id == nodeId)
                        sub.nss[i] = new Nss { id = nodeId, ct = 2, cn = 1, dn = 0 };

        account.ChapterState = AccountFields.Set(state);
        return new UpdatedFormat { mainChapterStateList = state };
    }
}
