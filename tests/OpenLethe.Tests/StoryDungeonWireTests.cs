using System.Collections.Generic;
using System.Text.Json;
using OpenLethe.Server;
using OpenLethe.Server.Wire;
using Xunit;

public class StoryDungeonWireTests
{
    [Fact]
    public void StorySaveInfo_SerializesRustFieldNames()
    {
        var save = new StorySaveInfo
        {
            dungeonid = 10501,
            currentinfo = new Currentinfo
            {
                cn = new Currentnode { f = 1, s = 2, nid = 10505 },
                pnids = new List<long> { 10501, 10505 },
                nr = 1,
            },
        };
        var json = JsonSerializer.Serialize(save, global::PacketJson.Options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(10501, root.GetProperty("dungeonid").GetInt64());
        var ci = root.GetProperty("currentinfo");
        Assert.Equal(10505, ci.GetProperty("cn").GetProperty("nid").GetInt64());
        Assert.Equal(2, ci.GetProperty("pnids").GetArrayLength());
        Assert.Equal(JsonValueKind.Array, ci.GetProperty("egs").ValueKind); // empty [] not null
    }

    [Fact]
    public void StorySaveInfo_TypedDulAndEss_RoundTripWithEveryFieldIntact()
    {
        var save = new StorySaveInfo { dungeonid = 10301 };
        save.currentinfo.dul.Add(new Dungeonunitlist
        {
            sp = 1, gi = 2, pid = 3, ch = 4, cm = 5,
            mhos = 6, g = 7, l = 8, isp = 9,
            es = { new Egos { id = 11, g = 12, idx = 13 } },
        });
        save.currentinfo.ess.Add(new EgoSkillStock { t = "CR", n = 42 });

        var restored = AccountFields.Get<StorySaveInfo>(AccountFields.Set(save))!;

        var unit = Assert.Single(restored.currentinfo.dul);
        Assert.Equal(1, unit.sp);
        Assert.Equal(2, unit.gi);
        Assert.Equal(3, unit.pid);
        Assert.Equal(4, unit.ch);
        Assert.Equal(5, unit.cm);
        Assert.Equal(6, unit.mhos);
        Assert.Equal(7, unit.g);
        Assert.Equal(8, unit.l);
        Assert.Equal(9, unit.isp);
        var ego = Assert.Single(unit.es);
        Assert.Equal(11, ego.id);
        Assert.Equal(12, ego.g);
        Assert.Equal(13, ego.idx);

        var stock = Assert.Single(restored.currentinfo.ess);
        Assert.Equal("CR", stock.t);
        Assert.Equal(42, stock.n);
    }

    [Fact]
    public void StoryEventSave_ReadsAndWritesUnitStatsByPid()
    {
        var save = new StorySaveInfo();
        save.currentinfo.dul.Add(new Dungeonunitlist { pid = 7, ch = 100, cm = 50 });
        save.currentinfo.dul.Add(new Dungeonunitlist { pid = 8, ch = 200, cm = 60 });
        var adapter = new StoryEventSave(save);

        var stats = adapter.GetUnitStats();
        Assert.Equal(100, stats[7].hp);
        Assert.Equal(50, stats[7].sp);
        Assert.Equal(200, stats[8].hp);

        stats[7] = new UnitStats { hp = 1, sp = 2 };
        adapter.SetUnitStats(stats);
        Assert.Equal(1, save.currentinfo.dul[0].ch);
        Assert.Equal(2, save.currentinfo.dul[0].cm);
        // Untouched entries keep their values.
        Assert.Equal(200, save.currentinfo.dul[1].ch);
    }

    [Fact]
    public void StoryEventSave_PushEgoGiftAppends_AndAddCostIsNoOp()
    {
        var save = new StorySaveInfo();
        var adapter = new StoryEventSave(save);

        adapter.PushEgoGift(9001);
        var gift = Assert.Single(save.currentinfo.egs);
        Assert.Equal(9001, gift.id);

        // Rust events/mod.rs:94 - `fn add_cost(&mut self, _: i64) {}`. The story save has
        // no cost field, so this must not throw and must not mutate anything.
        adapter.AddCost(500);
        Assert.Single(save.currentinfo.egs);
    }
}
