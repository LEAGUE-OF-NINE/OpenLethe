# Generates StaticRoutes.cs from the client packet headers plus Rust handler
# classification. Re-run when the client adds packet files or the Rust server
# adds endpoints; output is committed.
#
# Route existence and packet types come from the CLIENT (packets/api_*.cs header
# comments) - not from the route name. Rust (router.rs + handler source) is only
# consulted for whether a route may be answered with a canned default response
# (static_response, never UserRepository). Rust is behind the client (it doesn't
# know about every *v2 route the client calls), so a client-declared route Rust
# has never heard of can still be emitted if it shares its packet contract - the
# (ReqPacket_X, ResPacket_Y) pair - with a Rust route already proven static; see
# the $staticClassPairs comment below.
#
# Scope is /api/* and /login/* routes (matches the brief's regex) - the /iap
# and /log route groups use the same static_response/UserRepository idiom but
# are a separate subsystem and out of scope for this task.
#
# Rust-declared routes the client does not declare at all are commented out with
# `// NOT IN CLIENT:` and reported, deterministically, so a regen reproduces the
# same comment-outs instead of silently dropping them.

param(
    [string]$RustRoot = "C:\Users\Limi\Desktop\Projects\lethe-server",
    [string]$PacketsRoot = "$PSScriptRoot\..\packets",
    [string]$OutFile  = "$PSScriptRoot\..\src\OpenLethe.Server\StaticRoutes.cs"
)

$ErrorActionPreference = "Stop"

$routerText = Get-Content "$RustRoot\server\src\router.rs" -Raw

# router.route("/api/Name", post(path::to::handler_fn)) - handler may be
# wrapped across lines and/or have a trailing comma before the closing paren,
# e.g. BattlePassExLevelReward's post(\n    crate::...::handle_x,\n),
$routePattern = 'route\(\s*"(?<route>/(?:api|login)/[^"]+)"\s*,\s*post\(\s*(?<handler>[\w:]+)\s*,?\s*\)'
$routeMatches = [regex]::Matches($routerText, $routePattern)
if ($routeMatches.Count -eq 0) { throw "No routes matched - check regex against router.rs" }

# Build the set of ReqPacket_/ResPacket_ class names actually present under
# /packets, so a route/handler name that has drifted from the client packet
# name gets caught and commented out instead of failing the build with CS0246.
$packetClassNames = New-Object 'System.Collections.Generic.HashSet[string]'
Get-ChildItem -Path $PacketsRoot -Filter *.cs -Recurse | ForEach-Object {
    $text = Get-Content $_.FullName -Raw
    foreach ($cm in [regex]::Matches($text, 'class\s+((Req|Res)Packet_\w+)')) {
        $className = $cm.Groups[1].Value
        [void]$packetClassNames.Add($className)
    }
}

# Client route map: every packets/api_*.cs file starts with a header comment
# naming the route it answers and the request/response classes it uses, e.g.
#   // /api/EnterExpDungeonv2  ReqPacket_EnterExpDungeon -> ResPacket_EnterExpDungeon
# The file carries the v2, but (per the client's own source) the classes don't -
# that's exactly the drift a route-name-derived guess misses. This is the
# authoritative route/type map; $packetClassNames above is only used to validate it.
#
# Dictionary (not a Hashtable) built with OrdinalIgnoreCase explicitly, so its
# case-insensitive key lookup is a stated choice rather than an incidental
# property of PowerShell's default Hashtable comparer - it must match ASP.NET's
# own case-insensitive route matching (see the comment above $rustStatic).
$clientRoutes = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([System.StringComparer]::OrdinalIgnoreCase)
Get-ChildItem -Path $PacketsRoot -Filter *.cs -Recurse | ForEach-Object {
    $fileName = $_.Name
    $text = Get-Content $_.FullName -Raw
    foreach ($hm in [regex]::Matches($text, '(?m)^//\s*(?<route>/(?:api|login)/\S+)\s+(?<req>ReqPacket_\w+)\s*->\s*(?<res>ResPacket_\w+)\s*$')) {
        $route = $hm.Groups['route'].Value
        $req   = $hm.Groups['req'].Value
        $res   = $hm.Groups['res'].Value

        if (-not $packetClassNames.Contains($req)) { throw "$fileName header names $req, which has no class declaration under /packets" }
        if (-not $packetClassNames.Contains($res)) { throw "$fileName header names $res, which has no class declaration under /packets" }

        if ($clientRoutes.ContainsKey($route)) {
            $existing = $clientRoutes[$route]
            if ($existing.Req -ne $req -or $existing.Res -ne $res) {
                throw "$route is declared twice with different class pairs: ($($existing.Req) -> $($existing.Res)) vs ($req -> $res)"
            }
        } else {
            $clientRoutes[$route] = [pscustomobject]@{ Req = $req; Res = $res }
        }
    }
}
if ($clientRoutes.Count -eq 0) { throw "No client routes matched - check regex against packets/*.cs headers" }

# Routes whose Rust handler uses static_response but with NON-DEFAULT data the
# generic MapPacket can't reproduce (it would return an empty/default result).
# These need a hand-written handler and are registered in Program.cs, not here.
# This is a curated list of routes INTENDED to get a real handler - a distinct
# concept from $handRegisteredRoutes below (routes that ALREADY have one, auto-
# detected from source rather than hand-maintained). The two lists complement
# each other; neither replaces the other.
$forceRealHandler = @(
    '/login/GetTermsOfUseStateAll'      # returns terms as accepted (version 1, state 1)
    '/api/GetMirrorDungeonEgoGiftRecord' # real handler: enumerates all MD ego gifts + themes
    '/api/ExitMirrorDungeon'             # real handler: returns isEndDungeon=1, isclear=1
)

# Routes already served by a hand-written C# handler, auto-detected by scanning
# this repo's own server source for literal MapPost("...")/MapGet("...") route
# registrations. The C# server has drifted ahead of Rust - some client-declared
# routes already have real handlers here that Rust has never heard of, so they
# can't be caught by checking $rustKnown/$forceRealHandler alone. Scanning the
# source directly means a newly hand-written handler is picked up automatically
# on the next regen, instead of relying on someone remembering to add it to a
# hand-maintained list.
$serverRoot = Join-Path $PSScriptRoot "..\src\OpenLethe.Server"
$handRegisteredRoutes = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$handRoutePattern = 'Map(?:Post|Get)\(\s*"(?<route>/[^"]+)"'
$handScanFiles = @((Join-Path $serverRoot "Program.cs"))
$handScanFiles += (Get-ChildItem -Path (Join-Path $serverRoot "Handlers") -Filter *.cs -Recurse | ForEach-Object { $_.FullName })
foreach ($file in $handScanFiles) {
    $text = Get-Content $file -Raw
    foreach ($hrm in [regex]::Matches($text, $handRoutePattern)) {
        [void]$handRegisteredRoutes.Add($hrm.Groups['route'].Value)
    }
}
$autoExcluded = @()

# Classify every Rust-declared route (unchanged classification semantics: static
# iff static_response present and UserRepository absent). Recorded into sets
# instead of emitted directly, so type resolution (below) can be client-driven.
#
# NB comparer: routes are matched by ASP.NET case-insensitively at request time
# (MapPost/MapGet route templates), so every set/dictionary below that holds or
# looks up routes uses OrdinalIgnoreCase consistently - otherwise a route that
# differs only by case from another could slip past the duplicate-emission
# guard and register twice, surfacing as an AmbiguousMatchException at runtime
# instead of being caught here at generation time.
$rustStatic = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$rustKnown  = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$skipped = @()

foreach ($m in $routeMatches) {
    $route   = $m.Groups['route'].Value
    $handler = $m.Groups['handler'].Value
    [void]$rustKnown.Add($route)

    if ($forceRealHandler -contains $route) { $skipped += "$route (real handler required - non-default static data)"; continue }
    if ($handRegisteredRoutes.Contains($route)) { $autoExcluded += $route; $skipped += "$route (real handler already registered in C# source - auto-detected)"; continue }

    # crate::api::battlepass::battle_pass_reward::handle_x -> api/battlepass/battle_pass_reward.rs
    $parts = $handler -split '::'
    if ($parts.Length -lt 2) { continue }
    $moduleParts = $parts[1..($parts.Length - 2)]   # drop leading "crate" and trailing fn name
    $file = Join-Path "$RustRoot\server\src" (($moduleParts -join '\') + ".rs")

    if (-not (Test-Path $file)) {
        $file = Join-Path "$RustRoot\server\src" (($moduleParts -join '\') + "\mod.rs")
    }
    if (-not (Test-Path $file)) { $skipped += "$route (source not found: $file)"; continue }

    $body = Get-Content $file -Raw
    $isStatic = ($body -match 'static_response') -and ($body -notmatch 'UserRepository')
    if (-not $isStatic) { $skipped += "$route (stateful)"; continue }

    [void]$rustStatic.Add($route)
}

# Resolve a (Req,Res) type pair for every Rust-static route, and record every
# resolved pair into $staticClassPairs.
#
# ponytail: prefer the client header's classes when the client declares this
# exact route (fixes the 5 name-drift routes - the packet CLASSES a route uses
# haven't changed, only the route string drifted from them). When the client
# does not declare the route at all (e.g. the dead v1 dungeon paths, which
# predate the client's move to v2), fall back to the old route-name-equals-
# class-name guess - that guess still resolves for those, and dropping it would
# silently de-register routes that currently work. $staticClassPairs is then the
# set of contracts proven safe to answer with a canned default - the exact thing
# a client-only route (below) can inherit staticness from.
$static = @()
$notInClient = @()
# Same comparer choice, and for the same reason, as $rustStatic/$rustKnown above.
$staticClassPairs = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$emittedRoutes = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
$fallbackResolved = @()

foreach ($route in $rustStatic) {
    $name = $route -replace '^/(?:api|login)/', ''
    $usedFallback = $false
    if ($clientRoutes.ContainsKey($route)) {
        $req = $clientRoutes[$route].Req
        $res = $clientRoutes[$route].Res
    } else {
        $req = "ReqPacket_$name"
        $res = "ResPacket_$name"
        $usedFallback = $true
    }

    if ($packetClassNames.Contains($req) -and $packetClassNames.Contains($res)) {
        if (-not $emittedRoutes.Add($route)) { throw "Duplicate emission for $route" }
        $static += [pscustomobject]@{ Route = $route; Name = $name; Req = $req; Res = $res }
        [void]$staticClassPairs.Add("$req|$res")
        if ($usedFallback) { $fallbackResolved += $route }
    } else {
        # Accurate only because the $clientRoutes build above THROWS when a header names a
        # class with no declaration under /packets - if that throw were ever relaxed to
        # warn-and-skip, client-declared routes could land here too and this label (and the
        # "NOT IN CLIENT" comment it drives) would misrepresent them.
        $notInClient += $route
    }
}

# Client-only routes: Rust has never heard of them (it's behind the client), so
# they can only be answered if they share a packet contract with a Rust route
# already proven static above. This is what lets /api/EnterThreadDungeonv2
# inherit staticness from /api/EnterThreadDungeon - same (Req,Res) pair, same
# canned default is correct. It is deliberately narrow: a client route whose
# pair no Rust-static route uses is never emitted, which is what keeps
# ProjectGS, Starlight and the Railway getters out (they need real handlers).
$inheritedRoutes = @()
$rejectedClientOnly = @()
foreach ($route in $clientRoutes.Keys) {
    if ($rustKnown.Contains($route)) { continue }   # already resolved (or excluded) above
    if ($forceRealHandler -contains $route) { $skipped += "$route (real handler required - non-default static data)"; continue }
    if ($handRegisteredRoutes.Contains($route)) { $autoExcluded += $route; $skipped += "$route (real handler already registered in C# source - auto-detected)"; continue }

    $cr = $clientRoutes[$route]
    $pairKey = "$($cr.Req)|$($cr.Res)"
    if ($staticClassPairs.Contains($pairKey)) {
        if (-not $emittedRoutes.Add($route)) { throw "Duplicate emission for $route" }
        $name = $route -replace '^/(?:api|login)/', ''
        $static += [pscustomobject]@{ Route = $route; Name = $name; Req = $cr.Req; Res = $cr.Res }
        $inheritedRoutes += $route
    } else {
        # Server doesn't answer this yet: a client-declared route whose (Req,Res) pair no
        # Rust-static route shares, so there is nothing to inherit staticness from.
        $rejectedClientOnly += $route
    }
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("// <auto-generated by tools/extract-static-routes.ps1 - do not edit by hand>")
[void]$sb.AppendLine("// Routes and packet types come from the client packet headers (packets/api_*.cs),")
[void]$sb.AppendLine("// not from the route name - Rust is behind the client and doesn't declare every")
[void]$sb.AppendLine("// route the client calls. Rust supplies only the stateless classification: a")
[void]$sb.AppendLine("// route is emitted iff its Rust handler used static_response and never touched")
[void]$sb.AppendLine("// UserRepository (or, for a route Rust doesn't declare at all, iff some other")
[void]$sb.AppendLine("// Rust-static route uses the same ReqPacket_/ResPacket_ pair). Stateful endpoints")
[void]$sb.AppendLine("// are handled in cycles 3 and 4.")
[void]$sb.AppendLine("// NOT IN CLIENT lines are routes Rust declares and classifies stateless that the")
[void]$sb.AppendLine("// client does not declare at all. Re-running the generator reproduces the same")
[void]$sb.AppendLine("// comment-outs.")
[void]$sb.AppendLine("using Microsoft.AspNetCore.Routing;")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("public static class StaticRoutes")
[void]$sb.AppendLine("{")
[void]$sb.AppendLine("    public static IEndpointRouteBuilder MapStaticPackets(this IEndpointRouteBuilder app)")
[void]$sb.AppendLine("    {")
foreach ($r in ($static | Sort-Object Name)) {
    [void]$sb.AppendLine("        app.MapPacket<$($r.Req), $($r.Res)>(`"$($r.Route)`");")
}
foreach ($route in ($notInClient | Sort-Object)) {
    [void]$sb.AppendLine("        // NOT IN CLIENT: $route")
}
[void]$sb.AppendLine("        return app;")
[void]$sb.AppendLine("    }")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("    public static int RegisteredCount => $($static.Count);")
[void]$sb.AppendLine("}")

Set-Content -Path $OutFile -Value $sb.ToString() -Encoding utf8

Write-Output "Static routes generated: $($static.Count)"
Write-Output "  of which client-only, inherited via class-pair match: $($inheritedRoutes.Count)"
$inheritedRoutes | Sort-Object | ForEach-Object { Write-Output "    - $_" }
Write-Output "  of which resolved via route-name fallback (client declares no header for them): $($fallbackResolved.Count)"
$fallbackResolved | Sort-Object | ForEach-Object { Write-Output "    - $_" }
Write-Output "Auto-excluded (real handler already registered in C# source): $($autoExcluded.Count)"
$autoExcluded | Sort-Object -Unique | ForEach-Object { Write-Output "  - $_" }
Write-Output "Skipped (stateful or unresolved): $($skipped.Count)"
$skipped | ForEach-Object { Write-Output "  - $_" }
Write-Output "Not in client (commented out): $($notInClient.Count)"
$notInClient | Sort-Object | ForEach-Object { Write-Output "  - $_" }
Write-Output "Rejected client-only routes (no Rust-static route shares their packet pair): $($rejectedClientOnly.Count)"
$rejectedClientOnly | Sort-Object | ForEach-Object { Write-Output "  - $_" }
