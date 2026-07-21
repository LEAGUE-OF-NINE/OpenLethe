using System;
using System.Collections.Generic;
using System.Linq;
using OpenLethe.Resources;

namespace OpenLethe.Server;

// Static-data record types (camelCase JSON keys; StaticData is case-sensitive, no naming
// policy). Only the fields the EventManager engine reads. Port of lethe-server
// models/src/mirror_dungeon/{mod.rs (Event), action_event.rs, personality_event.rs}.
//
// NOTE: real static-data files use the JSON key "nextBattleID" (capital ID) for
// ResultForm.next_battle_id, while Rust's #[serde(rename_all = "camelCase")] on the
// `next_battle_id` field expects "nextBattleId" (lowercase d) - a genuine key mismatch
// in the Rust source, so that field is always None/null there. Ported verbatim: the C#
// field below is named `nextBattleId` (matching what Rust's derive expects) with no
// [JsonPropertyName], so it likewise never matches the real "nextBattleID" key and stays
// null - faithfully reproducing the same always-null behavior.
public sealed class MdEvent
{
    public long id;
    public ActionEvent? actionEvent;
    public PersonalityEvent? personalityEvent;
}

public sealed class ActionEvent
{
    public List<EachOption> eachOptionList = new();
}

public sealed class EachOption
{
    public List<ActionResult> resultList = new();
}

// Rust `Result` - renamed to avoid clashing with System.
public sealed class ActionResult
{
    public long? nextEventID;
    public List<ActionResultData>? eventResultDataList;
}

public sealed class ActionResultData
{
    public ResultForm resultForm = new();
}

public sealed class PersonalityEvent
{
    public List<PersonalityEventResult> eventResults = new();
}

// Rust `EventResult`.
public sealed class PersonalityEventResult
{
    public List<PersonalityResultData> eventResultDataList = new();
}

// Rust personality_event::EventResultData.
public sealed class PersonalityResultData
{
    public long nextEventID;
    public List<ResultFormWrapper>? eventResultDataList;
}

public sealed class ResultFormWrapper
{
    public ResultForm resultForm = new();
}

public sealed class ResultForm
{
    public string? resultEffectTarget;
    public string resultEffect = "";
    public long? nextBattleId; // see NOTE above - never matches real JSON key "nextBattleID"
    public ItemReward? itemReward;
}

public sealed class ItemReward
{
    public long? rewardId;
    public long num;
}

public static class MdEventData
{
    // ponytail: memoize the folder scan - handlers/tests call GetById repeatedly; data is static.
    private static readonly Lazy<Dictionary<long, MdEvent>> Events = new(() =>
    {
        var dict = new Dictionary<long, MdEvent>();
        foreach (var ev in StaticData.GetList<MdEvent>("static-data/abnormality-event"))
        {
            dict[ev.id] = ev; // last-wins on duplicate id, matching Rust HashMap::insert
        }
        return dict;
    });

    public static MdEvent? GetById(long id) => Events.Value.GetValueOrDefault(id);

    public static int Count => Events.Value.Count;
}
