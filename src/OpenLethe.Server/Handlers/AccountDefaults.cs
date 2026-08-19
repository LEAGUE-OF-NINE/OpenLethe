using OpenLethe.Data;
using OpenLethe.Server.Defaults;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Shared account-field defaults, extracted from LoadUserDataAll so other handlers
/// (e.g. UseCoupon) can derive the same personalities/user_info without duplication.
internal static class AccountDefaults
{
    private const string StaminaRecover = "2025-03-31T15:10:00.000Z";

    internal static List<ResultPersonality> DerivePersonalities(Account account)
    {
        var map = new SortedDictionary<long, ResultPersonality>();
        foreach (var p in DefaultData.GetFormattedPersonalities()) map[p.personality_id] = p;

        // Identities uploaded via POST /custom/upload/personality join the allowed set at
        // default stats (Rust get_personalities). No-op for an account that uploaded none.
        foreach (var c in AccountFields.Get<List<CustomIdentity>>(account.CustomIdentities) ?? new())
            map[c.id] = new ResultPersonality
            {
                personality_id = c.id,
                level = DefaultData.DefaultPersonalityLevel,
                exp = 100,
                gacksung = 4,
                order_id = 0,
                gacksung_illust_type = 1,
                acquire_time = DefaultData.AcquireTime,
            };

        // Overlay stored edits, but only for ids already in the allowed set.
        foreach (var p in AccountFields.Get<List<ResultPersonality>>(account.Personalities) ?? new())
            if (map.ContainsKey(p.personality_id)) map[p.personality_id] = p;

        return map.Values.ToList();
    }

    /// Owned egos: shipped defaults overlaid by stored edits. Rust seeds the column at
    /// account creation, so its raw reads already carry the defaults; OpenLethe fills the
    /// column lazily (LoadUserDataAll), so readers must derive to match.
    internal static List<Ego> DeriveEgos(Account account) =>
        AccountFields.MergeById(
            DefaultData.GetFormattedEgos(),
            AccountFields.Get<List<Ego>>(account.Egos) ?? new(),
            e => e.ego_id);

    internal static UserInfo DefaultUserInfo() => new()
    {
        uid = 1234,
        level = 200,
        exp = 0,
        stamina = 99999,
        last_stamina_recover = StaminaRecover,
        first_login_today = StaminaRecover,
    };
}
