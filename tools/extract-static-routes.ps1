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
$clientRoutes = @{}
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
$forceRealHandler = @(
    '/login/GetTermsOfUseStateAll'      # returns terms as accepted (version 1, state 1)
    '/api/GetMirrorDungeonEgoGiftRecord' # real handler: enumerates all MD ego gifts + themes
    '/api/ExitMirrorDungeon'             # real handler: returns isEndDungeon=1, isclear=1
)

# Classify every Rust-declared route (unchanged classification semantics: static
# iff static_response present and UserRepository absent). Recorded into sets
# instead of emitted directly, so type resolution (below) can be client-driven.
$rustStatic = New-Object 'System.Collections.Generic.HashSet[string]'
$rustKnown  = New-Object 'System.Collections.Generic.HashSet[string]'
$skipped = @()

foreach ($m in $routeMatches) {
    $route   = $m.Groups['route'].Value
    $handler = $m.Groups['handler'].Value
    [void]$rustKnown.Add($route)

    if ($forceRealHandler -contains $route) { $skipped += "$route (real handler required - non-default static data)"; continue }

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
$staticClassPairs = New-Object 'System.Collections.Generic.HashSet[string]'
$emittedRoutes = New-Object 'System.Collections.Generic.HashSet[string]'

foreach ($route in $rustStatic) {
    $name = $route -replace '^/(?:api|login)/', ''
    if ($clientRoutes.ContainsKey($route)) {
        $req = $clientRoutes[$route].Req
        $res = $clientRoutes[$route].Res
    } else {
        $req = "ReqPacket_$name"
        $res = "ResPacket_$name"
    }

    if ($packetClassNames.Contains($req) -and $packetClassNames.Contains($res)) {
        if (-not $emittedRoutes.Add($route)) { throw "Duplicate emission for $route" }
        $static += [pscustomobject]@{ Route = $route; Name = $name; Req = $req; Res = $res }
        [void]$staticClassPairs.Add("$req|$res")
    } else {
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
foreach ($route in $clientRoutes.Keys) {
    if ($rustKnown.Contains($route)) { continue }   # already resolved (or excluded) above
    if ($forceRealHandler -contains $route) { $skipped += "$route (real handler required - non-default static data)"; continue }

    $cr = $clientRoutes[$route]
    $pairKey = "$($cr.Req)|$($cr.Res)"
    if ($staticClassPairs.Contains($pairKey)) {
        if (-not $emittedRoutes.Add($route)) { throw "Duplicate emission for $route" }
        $name = $route -replace '^/(?:api|login)/', ''
        $static += [pscustomobject]@{ Route = $route; Name = $name; Req = $cr.Req; Res = $cr.Res }
        $inheritedRoutes += $route
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
Write-Output "Skipped (stateful or unresolved): $($skipped.Count)"
$skipped | ForEach-Object { Write-Output "  - $_" }
Write-Output "Not in client (commented out): $($notInClient.Count)"
$notInClient | Sort-Object | ForEach-Object { Write-Output "  - $_" }
