using System.Collections.Generic;
using OpenLethe.Data;
using OpenLethe.Server;
using OpenLethe.Server.Wire;
using Xunit;

public class ChapterProgressTests
{
    private static Account AccountWithNode(long nodeId)
    {
        var state = new List<MainChapterState>
        {
            new()
            {
                id = 1,
                subcss = new List<Subcss>
                {
                    new()
                    {
                        id = 1,
                        nss = new List<Nss> { new() { id = nodeId, ct = 0, cn = 0, dn = 0 } },
                        rss = new List<long>(),
                    },
                },
            },
        };
        return new Account { ChapterState = AccountFields.Set(state) };
    }

    [Fact]
    public void RegisterWonNode_MarksMatchingNode_Won()
    {
        var account = AccountWithNode(100);

        var updated = ChapterProgress.RegisterWonNode(account, 100);

        var node = updated.mainChapterStateList![0].subcss[0].nss[0];
        Assert.Equal(100, node.id);
        Assert.Equal(2, node.ct);
        Assert.Equal(1, node.cn);
        Assert.Equal(0, node.dn);

        // Mutation is persisted back onto the account's column.
        var stored = AccountFields.Get<List<MainChapterState>>(account.ChapterState)!;
        Assert.Equal(2, stored[0].subcss[0].nss[0].ct);
    }

    [Fact]
    public void RegisterWonNode_NodeAbsent_LeavesStateUnchanged()
    {
        var account = AccountWithNode(100);

        var updated = ChapterProgress.RegisterWonNode(account, 999);

        var node = updated.mainChapterStateList![0].subcss[0].nss[0];
        Assert.Equal(100, node.id);
        Assert.Equal(0, node.ct); // untouched
    }

    [Fact]
    public void RegisterWonNode_EmptyChapterState_ReturnsEmptyList()
    {
        var account = new Account(); // ChapterState defaults to "{}"

        var updated = ChapterProgress.RegisterWonNode(account, 100);

        Assert.NotNull(updated.mainChapterStateList);
        Assert.Empty(updated.mainChapterStateList!);
    }
}
