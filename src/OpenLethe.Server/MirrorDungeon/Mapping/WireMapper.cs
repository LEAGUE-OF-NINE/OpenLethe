using OpenLethe.Server.MirrorDungeon.Model;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.MirrorDungeon.Mapping;

// TOTAL, LOSSLESS wire<->domain mapper. Run is now a clean, wire-independent domain aggregate;
// every assignment here builds the domain type from the wire type (ToDomain) or rebuilds the
// wire type from the domain type (ToWire). The round-trip test guarantees no field is lost.
public static class WireMapper
{
    // ponytail: leaf List<T> fields are reference-aliased across the map (only nested object lists are rebuilt via Select().ToList()). Safe while nothing mutates a mapped object — the handler-migration re-plan MUST .ToList() or replace-whole-list before introducing run.Operation() mutations.

    public static Run ToDomain(MirrorOriginSaveInfo w)
    {
        var c = w.currentInfo;
        return new Run
        {
            DungeonId = w.dungeonId,
            Idx = w.idx,
            Floor = new Floor
            {
                Current = new CurrentPosition { F = c.cn.f, S = c.cn.s, Nid = c.cn.nid },
                Nodes = w.dungeonMap.ns.Select(n => new MapNode
                {
                    F = n.f, S = n.s, Nid = n.nid, E = n.e, Eid = n.eid, Nnids = n.nnids,
                }).ToList(),
            },
            ChoiceEventList = w.choiceEventList,
            SinnerStats = w.statistics.Select(s => new SinnerStat { Id = s.id, Gd = s.gd, Rd = s.rd }).ToList(),
            EncounterStatistics = w.encounterstatistics,
            IsEndDungeon = w.isEndDungeon,
            IsReset = w.isReset,
            Version = w.version,
            StartDate = w.startdate,

            Eid = c.eid,
            Party = c.dul.Select(u => new PartyUnit
            {
                PersonalityId = u.pid, CurrentHp = u.ch, Cm = u.cm, Mhos = u.mhos,
                Gacksung = u.g, Level = u.l, Isp = u.isp, Sid = u.sid, Mlos = u.mlos,
                Pord = u.pord,
                UpgradeIndices = u.upidx,
                EgoSkills = u.es.Select(e => new EgoSkill { Id = e.id, G = e.g, Idx = e.idx }).ToList(),
            }).ToList(),
            SepsId = c.sepsId,
            StartPools = c.seps.Select(s => new StartEgoGiftPool
            {
                SetId = s.setId, Keyword = s.keyword, Pool = s.pool,
            }).ToList(),
            SepsCreated = c.sepsCreated,
            ThemeFloors = c.tfs.Select(t => new ThemeFloor
            {
                Idx = t.idx, F = t.f, Tfid = t.tfid, Egs = t.egs, Upegs = t.upegs, Ch = t.ch,
            }).ToList(),
            ThemePools = c.tfps.Select(t => new ThemePool
            {
                Idx = t.idx, Tfid = t.tfid, Egs = t.egs, Upegs = t.upegs,
            }).ToList(),
            TfpsCreated = c.tfpsCreated,
            RewardEvents = c.rre.Select(e => new RewardEvent
            {
                Rt = e.rt, Se = e.se, Sh = e.sh, Pool = e.pool, PoolV2 = e.pool_v2, PoolV3 = e.pool_v3,
            }).ToList(),
            Ri = c.ri,
            Cost = c.cost,
            UsedCost = c.usedcost,
            Shop = new ShopState
            {
                Slots = c.shop.slots.Select(s => new ShopSlotState { T = s.t, Id = s.id, S = s.s }).ToList(),
                Rc = c.shop.rc, Fre = c.shop.fre, Fkre = c.shop.fkre, Cf = c.shop.cf, Aec = c.shop.aec, Aesp = c.shop.aesp,
            },
            PrevUnits = c.prevdul.Select(p => new PrevUnit { Pid = p.pid, Upidx = p.upidx }).ToList(),
            Preves = c.preves,
            LevelAdders = c.leveladders,
            StartKeyword = c.startKeyword,
            StartBuffPoint = c.startBufPoint,
            ConstraintScores = c.cfs.Select(f => new ConstraintScore { Floor = f.floor, Difficulty = f.difficulty }).ToList(),
            LevelOffsets = new LevelOffsets
            {
                Sbmlos = c.efs.sbmlos, Egmlos = c.efs.egmlos, Snft = c.efs.snft, Csnft = c.efs.csnft,
            },
            Gifts = new EgoGiftInventory
            {
                Items = c.egs.Select(g => new EgoGift
                {
                    Id = g.id, Pids = g.pids, Un = g.un, Ul = g.ul, Oid = g.oid,
                }).ToList(),
            },
            Pnids = c.pnids,
            Nr = c.nr,
            ChoiceEvents = c.pce.Select(p => new ChoiceEvent { Sl = p.sl, Cs = p.cs, Ri = p.ri, Nei = p.nei }).ToList(),
            SkillStocks = c.ess.Select(e => new SkillStock { T = e.t, N = e.n }).ToList(),
            Dn = c.dn,
            Peids = c.peids,
            Phbids = c.phbids,
            Etype = c.etype,
            Upids = c.upids,
            Spid = c.spid,
            Missions = new MissionState
            {
                Sr = c.missions.sr, Sbe = c.missions.sbe, Smuet = c.missions.smuet, Sc = c.missions.sc,
                Sec = c.missions.sec, Nlm = c.missions.nlm, Sfs = c.missions.sfs, Sfb = c.missions.sfb, Ds = c.missions.ds,
            },
            Cels = c.cels,
            Starlight = new StarlightState
            {
                Pslp = c.slinfo.pslp, Rsbp = c.slinfo.rsbp, Rucc = c.slinfo.rucc, Dpp = c.slinfo.dpp,
                Brcp = c.slinfo.brcp, Pfb = c.slinfo.pfb, Scc = c.slinfo.scc, Ieedt = c.slinfo.ieedt,
                Degids = c.slinfo.degids, Segr = c.slinfo.segr, Ds = c.slinfo.ds,
            },
            RentalId = c.rentalid,
            ConstraintSelections = c.scinfos.Select(s => new ConstraintSelection { Flooridx = s.flooridx, Ids = s.ids }).ToList(),
            EnemyAbility = new EnemyAbility { EnemyLevelAdder = c.egabilityinfo.enemyleveladder },
            Isegr = c.isegr,
        };
    }

    public static MirrorOriginSaveInfo ToWire(Run r) => new()
    {
        dungeonId = r.DungeonId,
        idx = r.Idx,
        dungeonMap = new DungeonMap
        {
            ns = r.Floor.Nodes.Select(n => new Ns
            {
                f = n.F, s = n.S, nid = n.Nid, e = n.E, eid = n.Eid, nnids = n.Nnids,
            }).ToList(),
        },
        choiceEventList = r.ChoiceEventList,
        statistics = r.SinnerStats.Select(s => new MdStatistics { id = s.Id, gd = s.Gd, rd = s.Rd }).ToList(),
        encounterstatistics = r.EncounterStatistics,
        isEndDungeon = r.IsEndDungeon,
        isReset = r.IsReset,
        version = r.Version,
        startdate = r.StartDate,
        currentInfo = new CurrentInfo
        {
            eid = r.Eid,
            dul = r.Party.Select(u => new Dungeonunitlist1
            {
                pid = u.PersonalityId, ch = u.CurrentHp, cm = u.Cm, mhos = u.Mhos,
                g = u.Gacksung, l = u.Level, isp = u.Isp, sid = u.Sid, mlos = u.Mlos,
                pord = u.Pord,
                upidx = u.UpgradeIndices,
                es = u.EgoSkills.Select(e => new Egos { id = e.Id, g = e.G, idx = e.Idx }).ToList(),
            }).ToList(),
            sepsId = r.SepsId,
            seps = r.StartPools.Select(s => new StartEgoGiftPoolSets
            {
                setId = s.SetId, keyword = s.Keyword, pool = s.Pool,
            }).ToList(),
            sepsCreated = r.SepsCreated,
            tfs = r.ThemeFloors.Select(t => new Tfs
            {
                idx = t.Idx, f = t.F, tfid = t.Tfid, egs = t.Egs, upegs = t.Upegs, ch = t.Ch,
            }).ToList(),
            tfps = r.ThemePools.Select(t => new Tfps
            {
                idx = t.Idx, tfid = t.Tfid, egs = t.Egs, upegs = t.Upegs,
            }).ToList(),
            tfpsCreated = r.TfpsCreated,
            rre = r.RewardEvents.Select(e => new RemainRewardEvent
            {
                rt = e.Rt, se = e.Se, sh = e.Sh, pool = e.Pool, pool_v2 = e.PoolV2, pool_v3 = e.PoolV3,
            }).ToList(),
            ri = r.Ri,
            cost = r.Cost,
            usedcost = r.UsedCost,
            shop = new ShopInfo
            {
                slots = r.Shop.Slots.Select(s => new ShopSlot { t = s.T, id = s.Id, s = s.S }).ToList(),
                rc = r.Shop.Rc, fre = r.Shop.Fre, fkre = r.Shop.Fkre, cf = r.Shop.Cf, aec = r.Shop.Aec, aesp = r.Shop.Aesp,
            },
            prevdul = r.PrevUnits.Select(p => new PrevUnitInfo { pid = p.Pid, upidx = p.Upidx }).ToList(),
            preves = r.Preves,
            leveladders = r.LevelAdders,
            startKeyword = r.StartKeyword,
            startBufPoint = r.StartBuffPoint,
            cfs = r.ConstraintScores.Select(c => new Cfs { floor = c.Floor, difficulty = c.Difficulty }).ToList(),
            cn = new Currentnode { f = r.Floor.Current.F, s = r.Floor.Current.S, nid = r.Floor.Current.Nid },
            efs = new Efs
            {
                sbmlos = r.LevelOffsets.Sbmlos, egmlos = r.LevelOffsets.Egmlos,
                snft = r.LevelOffsets.Snft, csnft = r.LevelOffsets.Csnft,
            },
            egs = r.Gifts.Items.Select(g => new AcquiredEgogifts
            {
                id = g.Id, pids = g.Pids, un = g.Un, ul = g.Ul, oid = g.Oid,
            }).ToList(),
            pnids = r.Pnids,
            nr = r.Nr,
            pce = r.ChoiceEvents.Select(p => new ChoiceEventData { sl = p.Sl, cs = p.Cs, ri = p.Ri, nei = p.Nei }).ToList(),
            ess = r.SkillStocks.Select(s => new EgoSkillStock { t = s.T, n = s.N }).ToList(),
            dn = r.Dn,
            peids = r.Peids,
            phbids = r.Phbids,
            etype = r.Etype,
            upids = r.Upids,
            spid = r.Spid,
            missions = new Missions
            {
                sr = r.Missions.Sr, sbe = r.Missions.Sbe, smuet = r.Missions.Smuet, sc = r.Missions.Sc,
                sec = r.Missions.Sec, nlm = r.Missions.Nlm, sfs = r.Missions.Sfs, sfb = r.Missions.Sfb, ds = r.Missions.Ds,
            },
            cels = r.Cels,
            slinfo = new Slinfo
            {
                pslp = r.Starlight.Pslp, rsbp = r.Starlight.Rsbp, rucc = r.Starlight.Rucc, dpp = r.Starlight.Dpp,
                brcp = r.Starlight.Brcp, pfb = r.Starlight.Pfb, scc = r.Starlight.Scc, ieedt = r.Starlight.Ieedt,
                degids = r.Starlight.Degids, segr = r.Starlight.Segr, ds = r.Starlight.Ds,
            },
            rentalid = r.RentalId,
            scinfos = r.ConstraintSelections.Select(s => new Scinfo { flooridx = s.Flooridx, ids = s.Ids }).ToList(),
            egabilityinfo = new EgAbilityInfo { enemyleveladder = r.EnemyAbility.EnemyLevelAdder },
            isegr = r.Isegr,
        },
    };
}
