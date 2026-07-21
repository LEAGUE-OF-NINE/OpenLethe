namespace OpenLethe.Server;

// Port of lethe-server/server/src/api/md/md_cost.rs CostManager::get_default_cost.
// load_from_dir is dead code in Rust (never called) - not ported.
public static class MdCost
{
    private static readonly long[] BossFloorCosts = { 200, 240, 300, 400 };

    public static long GetDefaultCost(long nodeE, long floor) => nodeE switch
    {
        1 => 60,
        2 => 120,
        5 => 100,
        6 => floor is >= 0 and <= 3 ? BossFloorCosts[floor] : 0,
        14 => 150,
        _ => 0,
    };
}
