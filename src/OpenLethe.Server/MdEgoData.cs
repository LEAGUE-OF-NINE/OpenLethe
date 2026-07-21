using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using OpenLethe.Resources;

namespace OpenLethe.Server;

// Port of lethe-server/models/src/data/mod.rs get_md_ego_by_id +
// models/src/mirror_dungeon/ego.rs Ego (only the fields MD-shop handlers need).
public sealed class MdEgo
{
    public long id;
    public long price;
    public List<JsonNode>? upgradeDataList;
}

public static class MdEgoData
{
    // ponytail: memoize the folder scan - handlers call this per-request; data is static.
    private static readonly Lazy<List<MdEgo>> Data =
        new(() => StaticData.GetList<MdEgo>("static-data/ego-gift-mirrordungeon"));

    public static MdEgo? GetById(long id) => Data.Value.FirstOrDefault(e => e.id == id);

    public static long UpgradeCost(long price, long desiredUl) => ((price * desiredUl / 3) / 10) * 10;
}
