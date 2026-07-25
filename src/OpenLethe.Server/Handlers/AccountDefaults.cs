using OpenLethe.Server.Defaults;
using OpenLethe.Server.Wire;

namespace OpenLethe.Server.Handlers;

/// Shared account-field defaults, extracted from LoadUserDataAll so other handlers
/// (e.g. UseCoupon) can derive the same personalities/user_info without duplication.
internal static class AccountDefaults
{
    private const string StaminaRecover = "2025-03-31T15:10:00.000Z";

    internal static List<ResultPersonality> DerivePersonalities(string? stored)
    {
        var map = new SortedDictionary<long, ResultPersonality>();
        foreach (var p in DefaultData.GetFormattedPersonalities()) map[p.personality_id] = p;

        // Overlay stored edits, but only for ids already in the default set.
        foreach (var p in AccountFields.Get<List<ResultPersonality>>(stored) ?? new())
            if (map.ContainsKey(p.personality_id)) map[p.personality_id] = p;

        return map.Values.ToList();
    }

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
