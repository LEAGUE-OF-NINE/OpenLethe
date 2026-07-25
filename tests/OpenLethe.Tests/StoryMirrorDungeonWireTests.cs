using System.Collections.Generic;
using OpenLethe.Server;
using OpenLethe.Server.Wire;
using Xunit;

namespace OpenLethe.Tests;

public class StoryMirrorDungeonWireTests
{
    [Fact]
    public void StoryMirrorSaveInfo_RoundTripsWithEveryFieldIntact()
    {
        var save = new StoryMirrorSaveInfo { dungeonid = 910301 };
        var ci = save.currentinfo;
        ci.cn = new Currentnode { f = 1, s = 2, nid = 10200 };
        ci.egs.Add(new AcquiredEgogifts { id = 9001 });
        ci.pnids.Add(7);
        ci.nr = 1;
        ci.pce.Add(new ChoiceEventData { sl = { 3 }, cs = -1, ri = 0, nei = 42 });
        ci.ess.Add(new EgoSkillStock { t = "CR", n = 5 });
        ci.eid = 99;
        ci.dul.Add(new Dungeonunitlist2
        {
            sp = 50, upidx = { 1, 2 }, mlos = 3, pid = 10101, ch = 10000,
            cm = 11, mhos = 12, g = 13, l = 60, isp = 14,
            es = { new Egos { id = 21, g = 22, idx = 23 } },
        });
        ci.rre.Add(new RemainRewardEvent { rt = "GetEgogift", se = 1, sh = 1, pool = { 9002 } });
        ci.shop.ph = 1; ci.shop.pup = 2; ci.shop.upid = 3; ci.shop.peg.Add(4);
        ci.shop.pcf = 5; ci.shop.egpool.Add(6); ci.shop.rc = 7; ci.shop.fre = 8;
        ci.cost = 200;
        ci.prevdul.Add(new PrevUnitInfo { pid = 31 });
        ci.preves.Add(32);
        ci.seps.Add(new StartEgoGiftPoolSets { setId = 0, keyword = "Combustion", pool = { 9001 } });
        ci.sepsCreated = 1;
        save.map.ns.Add(new Ns { f = 0, s = 1, nid = 100, e = 1, eid = 2, nnids = { 200 } });
        save.choiceeventlist.Add(55);

        var r = AccountFields.Get<StoryMirrorSaveInfo>(AccountFields.Set(save))!;

        Assert.Equal(910301, r.dungeonid);
        Assert.Equal(10200, r.currentinfo.cn.nid);
        Assert.Equal(9001, Assert.Single(r.currentinfo.egs).id);
        Assert.Equal(7, Assert.Single(r.currentinfo.pnids));
        Assert.Equal(1, r.currentinfo.nr);
        Assert.Equal(42, Assert.Single(r.currentinfo.pce).nei);
        Assert.Equal(5, Assert.Single(r.currentinfo.ess).n);
        Assert.Equal(99, r.currentinfo.eid);
        Assert.Equal(200, r.currentinfo.cost);
        Assert.Equal(1, r.currentinfo.sepsCreated);
        Assert.Equal(32, Assert.Single(r.currentinfo.preves));
        Assert.Equal(31, Assert.Single(r.currentinfo.prevdul).pid);
        Assert.Equal("Combustion", Assert.Single(r.currentinfo.seps).keyword);
        Assert.Equal("GetEgogift", Assert.Single(r.currentinfo.rre).rt);
        Assert.Equal(55, Assert.Single(r.choiceeventlist));
        Assert.Equal(100, Assert.Single(r.map.ns).nid);

        var shop = r.currentinfo.shop;
        Assert.Equal(1, shop.ph);
        Assert.Equal(2, shop.pup);
        Assert.Equal(3, shop.upid);
        Assert.Equal(4, Assert.Single(shop.peg));
        Assert.Equal(5, shop.pcf);
        Assert.Equal(6, Assert.Single(shop.egpool));
        Assert.Equal(7, shop.rc);
        Assert.Equal(8, shop.fre);

        var u = Assert.Single(r.currentinfo.dul);
        Assert.Equal(50, u.sp);
        Assert.Equal(new List<long> { 1, 2 }, u.upidx);
        Assert.Equal(3, u.mlos);
        Assert.Equal(10101, u.pid);
        Assert.Equal(10000, u.ch);
        Assert.Equal(11, u.cm);
        Assert.Equal(12, u.mhos);
        Assert.Equal(13, u.g);
        Assert.Equal(60, u.l);
        Assert.Equal(14, u.isp);
        var e = Assert.Single(u.es);
        Assert.Equal(21, e.id);
        Assert.Equal(22, e.g);
        Assert.Equal(23, e.idx);
    }

    [Fact]
    public void StoryMdEventSave_ReadsAndWritesUnitStatsByPid()
    {
        var save = new StoryMirrorSaveInfo();
        save.currentinfo.dul.Add(new Dungeonunitlist2 { pid = 7, ch = 100, cm = 50 });
        save.currentinfo.dul.Add(new Dungeonunitlist2 { pid = 8, ch = 200, cm = 60 });
        var adapter = new StoryMdEventSave(save);

        var stats = adapter.GetUnitStats();
        Assert.Equal(100, stats[7].hp);
        Assert.Equal(50, stats[7].sp);

        stats[7] = new UnitStats { hp = 1, sp = 2 };
        adapter.SetUnitStats(stats);
        Assert.Equal(1, save.currentinfo.dul[0].ch);
        Assert.Equal(2, save.currentinfo.dul[0].cm);
        Assert.Equal(200, save.currentinfo.dul[1].ch);
    }

    [Fact]
    public void StoryMdEventSave_PushEgoGiftAppends_AndAddCostIsNoOp()
    {
        var save = new StoryMirrorSaveInfo();
        save.currentinfo.cost = 200;
        var adapter = new StoryMdEventSave(save);

        adapter.PushEgoGift(9001);
        Assert.Equal(9001, Assert.Single(save.currentinfo.egs).id);

        // Rust events/mod.rs:127 - `fn add_cost(&mut self, _: i64) {}`. Currentinfo1 DOES
        // have a cost field, but the Rust impl deliberately ignores it. Match that.
        adapter.AddCost(500);
        Assert.Equal(200, save.currentinfo.cost);
    }
}
