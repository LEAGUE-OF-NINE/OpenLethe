using OpenLethe.Server.MirrorDungeon.Mapping;
using OpenLethe.Server.Wire;

namespace OpenLethe.Tests;

// Focused unit tests on the domain types the foundation promotes. Grown one cluster per task.
public class MdDomainMappingTests
{
    [Fact]
    public void PartyUnit_MapsAndPreservesPordAndEgoSkills()
    {
        var save = new MirrorOriginSaveInfo();
        save.currentInfo.dul.Add(new Dungeonunitlist1
        {
            pid = 10101, ch = 9000, cm = 15, l = 42, g = 3, mlos = 4, pord = -1,
            upidx = new() { 1, 2 },
            es = new() { new Egos { id = 5, g = 1, idx = 0 } },
        });

        var run = WireMapper.ToDomain(save);
        var unit = Assert.Single(run.Party);
        Assert.Equal(10101, unit.PersonalityId);
        Assert.Equal(9000, unit.CurrentHp);
        Assert.Equal(42, unit.Level);
        Assert.Equal(-1, unit.Pord);
        Assert.Equal(new long[] { 1, 2 }, unit.UpgradeIndices);
        Assert.Equal(5, Assert.Single(unit.EgoSkills).Id);

        var back = WireMapper.ToWire(run);
        var wu = Assert.Single(back.currentInfo.dul);
        Assert.Equal(10101, wu.pid);
        Assert.Equal(-1, wu.pord);
        Assert.Equal(2, wu.upidx.Count);
    }

    [Fact]
    public void EgoGift_PreservesNullVsPresentOid()
    {
        var save = new MirrorOriginSaveInfo();
        save.currentInfo.egs.Add(new AcquiredEgogifts { id = 9053, ul = 1 });               // oid null -> omitted
        save.currentInfo.egs.Add(new AcquiredEgogifts { id = 9993, ul = 1, oid = 9053 });   // oid present

        var run = WireMapper.ToDomain(save);
        Assert.Null(run.Gifts.Items[0].Oid);
        Assert.Equal(9053, run.Gifts.Items[1].Oid);

        var back = WireMapper.ToWire(run);
        Assert.Null(back.currentInfo.egs[0].oid);
        Assert.Equal(9053, back.currentInfo.egs[1].oid);
    }

    [Fact]
    public void FloorAndLevelOffsets_Map()
    {
        var save = new MirrorOriginSaveInfo();
        save.currentInfo.cn = new Currentnode { f = 3, s = 1, nid = 7 };
        save.currentInfo.efs = new Efs { sbmlos = 3, egmlos = 2, snft = 1, csnft = 4 };
        save.dungeonMap.ns.Add(new Ns { f = 3, s = 1, nid = 7, e = 10, eid = 99, nnids = new() { 8, 9 } });

        var run = WireMapper.ToDomain(save);
        Assert.Equal(3, run.Floor.Current.F);
        Assert.Equal(2, run.LevelOffsets.Egmlos);
        Assert.Equal(10, Assert.Single(run.Floor.Nodes).E);

        var back = WireMapper.ToWire(run);
        Assert.Equal(7, back.currentInfo.cn.nid);
        Assert.Equal(4, back.currentInfo.efs.csnft);
        Assert.Equal(new long[] { 8, 9 }, Assert.Single(back.dungeonMap.ns).nnids);
    }

    [Fact]
    public void RewardEvent_PreservesNullVsPresentPoolVariants()
    {
        var save = new MirrorOriginSaveInfo();
        // e==3 confirmed-gift entry: pool_v2/v3 omitted entirely.
        save.currentInfo.rre.Add(new RemainRewardEvent { rt = "GetConfirmedEgogift", se = 1, sh = 1, pool = new() { 9040 } });
        // enemy-buff entry: pool_v2 and pool_v3 present as [].
        save.currentInfo.rre.Add(new RemainRewardEvent { rt = "GetEgogiftWithEnemyBuf", pool = new(), pool_v2 = new(), pool_v3 = new() });

        var run = WireMapper.ToDomain(save);
        Assert.Null(run.RewardEvents[0].PoolV2);
        Assert.NotNull(run.RewardEvents[1].PoolV2);
        Assert.Null(run.RewardEvents[0].PoolV3);
        Assert.NotNull(run.RewardEvents[1].PoolV3);

        var back = WireMapper.ToWire(run);
        Assert.Null(back.currentInfo.rre[0].pool_v2);
        Assert.NotNull(back.currentInfo.rre[1].pool_v2);
        Assert.Null(back.currentInfo.rre[0].pool_v3);
        Assert.NotNull(back.currentInfo.rre[1].pool_v3);
    }

    [Fact]
    public void ShopMiscCarriers_MapAndPreserveDefaultsAndNullNei()
    {
        var save = new MirrorOriginSaveInfo();
        save.currentInfo.shop = new ShopInfo { rc = 2, slots = new() { new ShopSlot { t = "eg", id = 9001, s = 1 } } };
        save.currentInfo.pce.Add(new ChoiceEventData { sl = new() { 1 }, cs = 0, ri = 3, nei = null });

        var run = WireMapper.ToDomain(save);
        Assert.Equal(2, run.Shop.Rc);
        Assert.Equal("eg", Assert.Single(run.Shop.Slots).T);
        Assert.Equal(383, run.Starlight.Pslp);   // wire default preserved through the domain
        Assert.Null(run.ChoiceEvents[0].Nei);

        var back = WireMapper.ToWire(run);
        Assert.Equal(9001, Assert.Single(back.currentInfo.shop.slots).id);
        Assert.Equal(383, back.currentInfo.slinfo.pslp);
        Assert.Null(back.currentInfo.pce[0].nei);
    }

    [Fact]
    public void Cels_OpaqueListRoundTripsVerbatim()
    {
        var save = new MirrorOriginSaveInfo();
        var node = System.Text.Json.Nodes.JsonNode.Parse("""{"eventId":901021,"choiceIdx":0,"count":[7,2,2]}""")!;
        save.currentInfo.cels.Add(node);

        var run = WireMapper.ToDomain(save);
        var back = WireMapper.ToWire(run);

        var reser = System.Text.Json.JsonSerializer.Serialize(back, global::PacketJson.Options);
        Assert.Contains("901021", reser);
        Assert.Contains("[7,2,2]", reser);
    }
}
