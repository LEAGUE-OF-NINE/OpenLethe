using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenLethe.Tests;

/// Task 12 guard: after the handler-migration refactor, every Mirror Dungeon HTTP handler
/// must be dispatch-only (resolve account -> read params -> WireMapper.ToDomain -> one or
/// more Rules.Operation(run, ...) calls -> WireMapper.ToWire -> build the wire response DTO
/// -> persist). No game logic - specifically no inline mutation of the wire save's fields -
/// may remain in Handlers/MirrorDungeon*.cs.
///
/// METHOD (a heuristic textual scan, NOT a full AST/Roslyn check - deliberately, per the
/// task brief): for each `app.MapPost("/api/...", async (HttpContext ctx) => { ... });` route
/// registered in the MD handler files, extract the lambda body by brace-counting from the
/// `async (HttpContext ctx) =>` arrow to its matching close, then regex-scan that body for a
/// dotted-field ASSIGNMENT on a save-like local: `save.currentInfo.<field> =`, `save.<field> =`,
/// or `loaded.currentInfo.<field> =` / `loaded.<field> =` (`loaded` is the pre-ToDomain wire
/// save in several handlers). Plain equality (`==`, `!=`, `<=`, `>=`) is excluded so response
/// reads like `save.currentInfo.cost` and comparisons don't trip it. Reassigning the whole
/// local (`save = WireMapper.ToWire(run);`, `loaded = AccountFields.Get&lt;...&gt;(...)`) is
/// fine and does not match (no dot before the `=`).
///
/// HONEST LIMITS: this cannot see mutation hidden behind a call to a handler-file-local helper
/// method (that class of bug is exactly what deleting the dead wire-typed helpers in this same
/// task closes off - once they're gone there is nowhere left for a handler to route inline
/// logic through). It also cannot see mutation many calls deep inside a Rules method - that is
/// covered by the MdRules*Tests component suites and the 407-replay gate, not this test. This
/// test's job is narrow and specific: catch a handler that (re)gained an inline
/// `save.currentInfo.x = ...`/`save.x = ...` assignment instead of routing the change through
/// a `Rules` call.
public class MdHandlersLogicFreeTests
{
    private static readonly string[] HandlerFiles =
    {
        "MirrorDungeon.cs",
        "MirrorDungeonMap.cs",
        "MirrorDungeonShop.cs",
        "MirrorDungeonRewards.cs",
        "MirrorDungeonEvents.cs",
    };

    // A save-field ASSIGNMENT: `save`/`loaded` followed by one-or-more dotted/indexed path
    // segments (`.field`, `[index]`, arbitrarily nested - e.g. `save.currentInfo.shop.rc` or
    // `save.currentInfo.shop.slots[0].id`), then either a bare `=` or a compound assignment
    // operator (`+=`, `-=`, `*=`, `/=`, `%=`, `&=`, `|=`, `^=`) - both are inline mutations of
    // the save and must be caught. Comparison operators (`==`, `!=`, `<=`, `>=`) are excluded:
    // the char immediately before `=` only matches a bare `=` (via `(?!=)`) or one of the
    // compound-op characters above, so `!`/`<`/`>` before `=` never matches either branch.
    private static readonly Regex SaveFieldAssignment = new(
        @"\b(save|loaded)(?:\.\w+|\[[^\]]*\])+\s*(?:[-+*/%&|^]=|=(?!=))",
        RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "OpenLethe.sln")))
            dir = Path.GetDirectoryName(dir);
        if (dir is null) throw new InvalidOperationException("Could not locate repo root (OpenLethe.sln) from " + AppContext.BaseDirectory);
        return dir;
    }

    // Extracts every `app.MapPost("...", async (HttpContext ctx) => { <body> });` lambda body
    // via brace counting, starting from each `async (HttpContext ctx) =>` occurrence.
    private static List<string> ExtractRouteBodies(string source)
    {
        var bodies = new List<string>();
        const string marker = "async (HttpContext ctx) =>";
        var pos = 0;
        while (true)
        {
            var idx = source.IndexOf(marker, pos, StringComparison.Ordinal);
            if (idx < 0) break;
            var braceStart = source.IndexOf('{', idx + marker.Length);
            if (braceStart < 0) break;
            var depth = 0;
            var i = braceStart;
            for (; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) break;
                }
            }
            bodies.Add(source.Substring(braceStart, i - braceStart + 1));
            pos = i + 1;
        }
        return bodies;
    }

    public static IEnumerable<object[]> HandlerFileCases() => HandlerFiles.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(HandlerFileCases))]
    public void RouteHandlerBodies_ContainNoInlineSaveFieldMutation(string fileName)
    {
        var path = Path.Combine(RepoRoot(), "src", "OpenLethe.Server", "Handlers", fileName);
        Assert.True(File.Exists(path), $"expected handler file at {path}");
        var source = File.ReadAllText(path);

        var bodies = ExtractRouteBodies(source);
        Assert.NotEmpty(bodies); // sanity: the file must have at least one MapPost route.

        foreach (var body in bodies)
        {
            var match = SaveFieldAssignment.Match(body);
            Assert.False(match.Success,
                $"{fileName}: route handler body contains an inline save-field assignment " +
                $"(\"{match.Value}\") - game logic must live in MirrorDungeon/Rules/, not the handler.");
        }
    }

    // Sanity check that the heuristic itself is not vacuous: it must actually catch the
    // pattern it claims to catch, on a synthetic body shaped like the pre-migration handlers.
    [Fact]
    public void Heuristic_DetectsInlineSaveMutation_OnSyntheticBody()
    {
        const string dirty = "{ var save = X(); save.currentInfo.cost = 5; }";
        Assert.Matches(SaveFieldAssignment, dirty);

        const string clean = "{ var save = WireMapper.ToWire(run); var cost = save.currentInfo.cost; " +
                              "var ok = save.currentInfo.cost == 5; account.MdSaveInfo = AccountFields.Set(save); }";
        Assert.DoesNotMatch(SaveFieldAssignment, clean);
    }

    // Positive control: a nested wire-field write (through shop/cn/efs/slinfo, per the domain
    // field map) or a compound assignment on a save field is exactly the class of regression
    // this guard exists to catch. If a future edit to SaveFieldAssignment accidentally narrows
    // it back to single-segment paths or drops compound-op detection, these must fail loudly.
    [Theory]
    [InlineData("save.currentInfo.shop.rc = 999;")]
    [InlineData("save.currentInfo.cn.nid = 3;")]
    [InlineData("save.currentInfo.shop.slots[0].id = 5;")]
    [InlineData("save.currentInfo.cost += 5;")]
    [InlineData("loaded.isEndDungeon = 1;")]
    public void Heuristic_DetectsNestedAndCompoundSaveMutation_PositiveControl(string mutation)
    {
        Assert.Matches(SaveFieldAssignment, mutation);
    }
}
