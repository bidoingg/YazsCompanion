<#
.SYNOPSIS
  Publishes a release of the YAZS Companion mod, after a row of gates; each one stops the release before anything is tagged,
  pushed or published (0.15.0):
    names     the maintainer's private name scan of the tree (only where overhaul\tools is checked out next to this
              repository; other clones skip it): no name of another mod's content packs may reach this public repository;
    tree      a clean working tree and a tag that does not exist yet;
    channel   (0.16.2) the version against the floor and the feeds: a release never below 0.16.2 (the first build that can step
              back), a test build (-Beta) above the newest published build; the test builds' feed must be readable;
    build     dotnet build -c Release -p:Deploy=false, its exit code checked (-NoBuild: the DLL already in bin\Release);
    identity  the DLL's BepInPlugin version is VERSION of Plugin.cs and the commit stamped into it is HEAD's, clean (read with
              Mono.Cecil from the game's BepInEx\core, nothing loaded) - a stale or uncommitted build is refused;
    bench     tools\ItemBench --strict: every check as wanted, the frozen public API (api_v3.txt) included;
    package   package.ps1 -NoBuild: the zip holds this very DLL (same SHA-256) and no knowledge.json;
    zip names the name scan again over the zip's text files, the DLL's string literals and the release notes;
    gh        the GitHub CLI is there and logged in.
  Then it writes latest.json (version, DLL url, SHA-256, zip url, notes, date, and from 0.16.2 the commit) for the in-game
  auto-updater, tags the commit, pushes, creates the GitHub release with the versioned DLL, latest.json and the zip, and copies
  the zip into the Drive folder for hand installs. After publishing (not with -Draft), the released DLL is copied over the PC's
  deployed one when the game is closed and the PC does not run a newer build (-NoLocalDeploy skips it); build.cmd stays the
  deploy of work in progress.

  The in-game updater reads https://github.com/<repo>/releases/latest/download/latest.json, so every
  non-draft, non-prerelease release becomes "latest" the moment it is published.

  0.16.2 - the beta channel (test builds). The GitHub prerelease flag is the channel; versions stay plain (0.16.3, 0.16.4 ...):
    -Beta          publishes the build as a test build: every gate as above, "gh release create ... --prerelease" (never the
                   latest release), no Drive copy; the PC's DLL is replaced only while its "Beta updates" switch is On.
    -Promote <v>   makes the published test build v the release - no rebuild: the release is byte for byte the build that ran
                   ("gh release edit v<v> --prerelease=false --latest"), then the Drive copy of its zip. It needs -VerdictFile, a
                   file whose first line starts PROMOTE-READY and names v (and the commit, when it names one, must be v's), whose
                   second line is the newest evidence time it judged - refused when the file is older than that time, or when
                   this PC's companion.log was written after it (judge again) - and names the Deck upload folders it judged
                   ("Deck uploads: <n>, newest <folder>, set <hash>"): refused when it did not read them ("Deck not read"), names
                   none, -DeckLogs cannot be reached or holds another set of folders now (a late upload) -, or -Force.
    -Pull <v>      withdraws a published build: adds v to the test builds' feed's "withdrawn" list and points the feed at the newest
                   build left (every updater on v steps back by itself), moves "latest" to the newest release left when v was
                   it, then turns v's release into a draft. Refused when no build at or above the floor would remain.
  The test builds' feed is the rolling prerelease 'beta' (title "Test builds feed"), one asset latest.json = a copy of the
  newest published build's own latest.json plus "withdrawn": [...]: https://github.com/<repo>/releases/download/beta/latest.json.
  Every publish (a release when its version is at or above the feed's, every test build) re-points it; the first one creates it.

.PARAMETER Notes       Release notes (a line or a paragraph); also shown in the in-game log when downloaded.
.PARAMETER Repo        GitHub repository, owner/name.
.PARAMETER DriveDir    Folder mirrored by Drive for Desktop; pass "" to skip the copy.
.PARAMETER GameDir     Game install: Mono.Cecil in BepInEx\core reads the DLL, and package.ps1 takes BepInEx from it.
.PARAMETER NoBuild     Reuse the DLL in bin\Release - only when it was built from HEAD with a clean tree.
.PARAMETER Draft       Create the release as a draft (the updater ignores drafts until published). The PC's DLL is left as it is.
.PARAMETER DryRun      Run every gate, stage the zip in dist and latest.json in dist\dryrun (0.16.2: dist\latest.json stays the
                       release's), print the git / gh commands it would run, and stop:
                       nothing is tagged, pushed, published or copied.
.PARAMETER AllowDirty  With -DryRun only: run the gates on a tree with uncommitted changes (the tree gate is skipped, the
                       DLL's commit may carry "-dirty"). A real release always needs a clean tree.
.PARAMETER NoLocalDeploy  Leave the PC's deployed DLL as it is.
.PARAMETER Beta        (0.16.2) Publish a test build (a GitHub prerelease) instead of a release.
.PARAMETER Promote     (0.16.2) The version of a published test build to make the release (no build, no new files).
.PARAMETER Pull        (0.16.2) The version of a published build to withdraw.
.PARAMETER VerdictFile (0.16.2) With -Promote: the verdict that says the test build is ready (see above).
.PARAMETER Force       (0.16.2) With -Promote: promote without a ready verdict (write the reason down where decisions are kept).
.PARAMETER DeckLogs    (0.16.2) With -Promote: the folder the Deck's log uploads land in (one yyyyMMdd_HHmmss folder per upload), for
                       the verdict's Deck part; like -DriveDir, a folder mirrored by Drive for Desktop.
.PARAMETER TestState   (0.16.2) With -DryRun only, for the script's own tests: a JSON file standing in for GitHub - the feeds and the
                       release list - so -Beta / -Promote / -Pull can be tried on made-up states: { "stableFeed": {...} | null,
                       "betaFeed": {...} | null, "releases": [ { "tag", "prerelease", "latest", "draft" } ], "feeds": { "<tag>": {...} },
                       "pluginDir": "<a made-up plugin folder for the local deploy's verdict>" (optional) }.
.EXAMPLE
  .\release.ps1 -Notes "Sidebar gates fixed from the Deck log"
  .\release.ps1 -DryRun -Notes "0.15.0 test"
  .\release.ps1 -Beta -Notes "0.16.3: ..."
  .\release.ps1 -Promote 0.16.3 -VerdictFile <the analyzer's verdict file>
  .\release.ps1 -Pull 0.16.3
#>
param(
    [string]$Notes = "",
    [string]$Repo = "bidoingg/YazsCompanion",
    [string]$DriveDir = "N:\My Drive\YAZS Mods",
    [string]$GameDir = "J:\SteamLibrary\steamapps\common\Yet Another Zombie Survivors",
    [switch]$NoBuild,
    [switch]$Draft,
    [switch]$DryRun,
    [switch]$AllowDirty,
    [switch]$NoLocalDeploy,
    [switch]$Beta,
    [string]$Promote = "",
    [string]$Pull = "",
    [string]$VerdictFile = "",
    [switch]$Force,
    [string]$TestState = "",
    [string]$DeckLogs = "N:\My Drive\YAZS Deck logs"
)
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------------------------- the gates' tools
$script:passed = New-Object System.Collections.Generic.List[string]
function Pass([string]$Gate, [string]$What) { $script:passed.Add($Gate); Write-Host ("[gate] PASS  {0,-10}{1}" -f $Gate, $What) }
function Skip([string]$Gate, [string]$Why) { Write-Host ("[gate] skip  {0,-10}{1}" -f $Gate, $Why) }
function Stop-Release([string]$Gate, [string]$Why) {
    Write-Host ("[gate] FAIL  {0,-10}{1}" -f $Gate, $Why) -ForegroundColor Red
    throw "release stopped at the '$Gate' gate - nothing was tagged, pushed or published"
}

# 0.16.2 (the beta channel): the first build that can see the test builds' feed and step back - nothing older becomes the release
$Floor = [version]'0.16.2'
$betaFeedUrl = "https://github.com/$Repo/releases/download/beta/latest.json"
$stableFeedUrl = "https://github.com/$Repo/releases/latest/download/latest.json"
$script:state = $null
if ($TestState) {
    if (-not $DryRun) { throw "-TestState goes with -DryRun only" }
    $script:state = Get-Content -Raw $TestState | ConvertFrom-Json -AsHashtable
}

# A feed's latest.json: @{ State = answered | absent | failed; Json; Version; Why } (read-only; -TestState stands in for GitHub).
function Read-Feed([string]$Url, [string]$TestKey) {
    if ($script:state) {
        $j = $null
        if ($TestKey -like 'tag:*') { if ($script:state.feeds) { $j = $script:state.feeds[$TestKey.Substring(4)] } }
        else { $j = $script:state[$TestKey] }
        if ($null -eq $j) { return @{ State = 'absent'; Why = 'HTTP 404 (test state)' } }
        return @{ State = 'answered'; Json = $j; Version = [version]$j.version }
    }
    try {
        $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -SkipHttpErrorCheck -TimeoutSec 40 -Headers @{ 'Cache-Control' = 'no-cache' }
        if ($r.StatusCode -eq 404) { return @{ State = 'absent'; Why = 'HTTP 404' } }
        if ($r.StatusCode -ne 200) { return @{ State = 'failed'; Why = "HTTP $($r.StatusCode)" } }
        $text = if ($r.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($r.Content) } else { [string]$r.Content }
        $j = $text.TrimStart([char]0xFEFF) | ConvertFrom-Json -AsHashtable
        $v = $null
        if (-not [version]::TryParse([string]$j.version, [ref]$v)) { return @{ State = 'failed'; Why = "its version '$($j.version)' does not parse" } }
        return @{ State = 'answered'; Json = $j; Version = $v }
    }
    catch { return @{ State = 'failed'; Why = $_.Exception.Message } }
}

# The published builds: tag, version, prerelease, latest, draft (the rolling 'beta' tag and anything not v<x.y.z> left out).
function Get-Releases {
    $rows = if ($script:state) { @($script:state.releases) }
    else {
        $out = & gh release list --repo $Repo --limit 200 --json tagName,isPrerelease,isLatest,isDraft 2>&1
        if ($LASTEXITCODE -ne 0) { throw "gh release list failed: $out" }
        @($out | ConvertFrom-Json -AsHashtable | ForEach-Object { @{ tag = $_.tagName; prerelease = $_.isPrerelease; latest = $_.isLatest; draft = $_.isDraft } })
    }
    foreach ($r in $rows) {
        if ([string]$r.tag -match '^v(\d+\.\d+\.\d+)$') {
            [pscustomobject]@{ Tag = [string]$r.tag; Version = [version]$Matches[1]; Prerelease = [bool]$r.prerelease; Latest = [bool]$r.latest; Draft = [bool]$r.draft }
        }
    }
}

# The PC's "Beta updates" switch as the Companion reads it (tools\ItemBench\channel_truth.txt, the companion column): YAZS_BETA
# 0 off false no / 1 on true yes (any case) first; else [Update] BetaUpdates as BepInEx reads it (true / false any case, other
# text = true); no file, section or key = On (the Companion binds the key with its default).
function Get-BetaSwitch([string]$Cfg) {
    $ev = [Environment]::GetEnvironmentVariable('YAZS_BETA')
    if ($null -ne $ev) {
        if ($ev -in @('0', 'off', 'false', 'no')) { return @{ On = $false; From = "YAZS_BETA=$ev" } }
        if ($ev -in @('1', 'on', 'true', 'yes')) { return @{ On = $true; From = "YAZS_BETA=$ev" } }
    }
    $sw = @{ On = $true; From = 'no setting: the default' }
    if (Test-Path -LiteralPath $Cfg) {
        $section = $null
        foreach ($raw in [IO.File]::ReadAllLines($Cfg)) {
            $line = $raw.Trim()
            if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
            if ($line.StartsWith('[') -and $line.EndsWith(']')) { $section = $line.Substring(1, $line.Length - 2); continue }
            $eq = $line.IndexOf('=')
            if ($eq -lt 0 -or $section -cne 'Update' -or $line.Substring(0, $eq).Trim() -cne 'BetaUpdates') { continue }
            $v = $line.Substring($eq + 1).Trim(); $b = $false
            $sw = @{ On = $(if ([bool]::TryParse($v, [ref]$b)) { $b } else { $true }); From = "[Update] BetaUpdates = $v" }
        }
    }
    return $sw
}

# What the release does with the PC's deployed DLL (game closed, the PC not on a newer build). 0.16.2 (critic 25 of the plan): the
# DLL BepInEx loads is the HIGHEST YazsCompanionMod*.dll of the folder - after a step back that is YazsCompanionMod-<v>.dll, not the
# plain name -, so that one is judged; the release then goes in as YazsCompanionMod.dll and every other build at or below its
# version is set aside (.off, removed by the next start).
function Get-LocalDeploy([string]$PluginDir, [string]$Sha, [string]$Version) {
    $plain = Join-Path $PluginDir 'YazsCompanionMod.dll'
    $rows = @(Get-ChildItem -LiteralPath $PluginDir -Filter 'YazsCompanionMod*.dll' -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '\.dll$' } | ForEach-Object {
            $pv = [Diagnostics.FileVersionInfo]::GetVersionInfo($_.FullName).ProductVersion
            $v = $null; [void][version]::TryParse(([string]$pv -replace '\+.*$', ''), [ref]$v)
            if (-not $v -and $_.Name -match '^YazsCompanionMod-(\d+\.\d+\.\d+)\.dll$') { $v = [version]$Matches[1] }
            [pscustomobject]@{ Path = $_.FullName; Name = $_.Name; Pv = $pv; V = $(if ($v) { $v } else { [version]'0.0.0' }) }
        })
    if ($rows.Count -eq 0) { return @{ Act = 'none'; Was = ''; Why = "no YazsCompanionMod*.dll in $PluginDir"; Target = $plain; Others = @() } }
    $top = $rows | Sort-Object @{ Expression = { $_.V }; Descending = $true }, @{ Expression = { $_.Name -eq 'YazsCompanionMod.dll' }; Descending = $true } | Select-Object -First 1
    $was = "$(if ($top.Pv) { $top.Pv } else { 'unknown' }) ($($top.Name))"
    $others = @($rows | Where-Object { $_.Path -ne $plain } | ForEach-Object { $_.Path })
    if ((Get-FileHash -LiteralPath $top.Path -Algorithm SHA256).Hash.ToLowerInvariant() -eq $Sha) { return @{ Act = 'same'; Was = $was; Why = "the PC already runs the released DLL ($($top.Name))"; Target = $plain; Others = @() } }
    if ($top.V -gt [version]$Version) { return @{ Act = 'skip'; Was = $was; Why = "the PC runs a newer build ($was) - left as it is"; Target = $plain; Others = @() } }
    if (Get-Process -Name 'Yet Another Zombie Survivors' -ErrorAction SilentlyContinue) { return @{ Act = 'skip'; Was = $was; Why = "the game is running - the PC keeps $was; copy the released DLL over it after it closes"; Target = $plain; Others = @() } }
    return @{ Act = 'copy'; Was = $was; Why = "the PC runs $was$(if ($others.Count) { '; set aside: ' + (($others | ForEach-Object { Split-Path $_ -Leaf }) -join ', ') })"; Target = $plain; Others = $others }
}

# Carries Get-LocalDeploy's 'copy' out: the release as YazsCompanionMod.dll, the other builds set aside, the dev marker gone (critic 7).
function Invoke-LocalDeploy($Ld, [string]$Dll) {
    Copy-Item -LiteralPath $Dll -Destination $Ld.Target -Force -ErrorAction Stop
    foreach ($o in $Ld.Others) { Move-Item -LiteralPath $o -Destination "$o.off" -Force -ErrorAction Stop }
    [void](Remove-DevMarker (Split-Path $Ld.Target))
}

# The development deploy's marker (dev_build.txt, written by build.cmd) beside the PC's DLL goes once the PC runs a published build.
# Review fix (critic 7): also on 'same' - build.cmd deployed the very bytes this run publishes (the deploy -> smoke -> publish order
# of the plan), so nothing is copied, and the marker would keep the published build a development build for good. Returns its path
# when there is one (-WhatIf: left in place).
function Remove-DevMarker([string]$PluginDir, [switch]$WhatIf) {
    $marker = Join-Path $PluginDir 'dev_build.txt'
    if (-not (Test-Path -LiteralPath $marker)) { return $null }
    if (-not $WhatIf) { Remove-Item -LiteralPath $marker -Force -ErrorAction Stop }
    return $marker
}

# The Deck upload folders under -DeckLogs as a verdict's second line names them (critic 21, integration fix 2026-10-12): @{ Count;
# Newest; Set } - Set = the first 12 hex digits of SHA-256 over the sorted yyyyMMdd_HHmmss folder names joined by "`n" (UTF-8), the
# analyzer's own sum; $null when the folder cannot be reached. An upload's files keep the Deck's own clock, so a LATE upload (maybe the
# one with the crash) looks older than the verdict and no time check sees it - its folder changes the set.
function Get-DeckSet([string]$Dir) {
    if (-not $Dir -or -not (Test-Path -LiteralPath $Dir -PathType Container)) { return $null }
    $n = [string[]]@(Get-ChildItem -LiteralPath $Dir -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^\d{8}_\d{6}$' } | ForEach-Object { $_.Name })
    [Array]::Sort($n, [StringComparer]::Ordinal)
    $alg = [System.Security.Cryptography.SHA256]::Create()
    try { $h = -join @($alg.ComputeHash([System.Text.Encoding]::UTF8.GetBytes(($n -join "`n"))) | ForEach-Object { $_.ToString('x2') }) } finally { $alg.Dispose() }
    return @{ Count = $n.Count; Newest = $(if ($n.Count -gt 0) { $n[$n.Count - 1] } else { '-' }); Set = $h.Substring(0, 12) }
}

# The DLL's identity without loading it: the BepInPlugin attribute's version and the InformationalVersion the build stamps
# ("0.15.0+abc1234", "+abc1234-dirty"; YazsCompanion.Mod.csproj). Mono.Cecil comes from the game's BepInEx\core.
function Get-DllIdentity([string]$Dll, [string]$Game) {
    $cecil = Join-Path $Game "BepInEx\core\Mono.Cecil.dll"
    if (-not (Test-Path $cecil)) { throw "Mono.Cecil not found at $cecil (pass -GameDir)" }
    if (-not ('Mono.Cecil.AssemblyDefinition' -as [type])) { Add-Type -Path $cecil }
    $rp = New-Object Mono.Cecil.ReaderParameters
    $rp.ReadingMode = [Mono.Cecil.ReadingMode]::Immediate
    $ms = New-Object IO.MemoryStream (, [IO.File]::ReadAllBytes($Dll))        # from memory: the file is never locked
    $asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($ms, $rp)
    try {
        $plugin = $null; $info = $null
        foreach ($t in $asm.MainModule.Types) {
            foreach ($a in $t.CustomAttributes) { if ($a.AttributeType.Name -eq 'BepInPlugin' -and $a.ConstructorArguments.Count -eq 3) { $plugin = [string]$a.ConstructorArguments[2].Value } }
        }
        foreach ($a in $asm.CustomAttributes) {
            if ($a.AttributeType.FullName -eq 'System.Reflection.AssemblyInformationalVersionAttribute') { $info = [string]$a.ConstructorArguments[0].Value }
        }
        $commit = $null
        if ($info -and $info.Contains('+')) { $commit = $info.Substring($info.IndexOf('+') + 1) }
        return [pscustomobject]@{ Version = $plugin; Info = $info; Commit = $commit }
    }
    finally { $asm.Dispose(); $ms.Dispose() }
}

# The zip's text entries (by extension) into a folder, for the name scan; returns how many.
function Export-ZipText([string]$Zip, [string]$To) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $text = '.txt', '.md', '.json', '.cfg', '.ini', '.xml', '.config', '.csv', '.log', '.yml', '.yaml', '.html', '.htm'
    $n = 0
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Zip)
    try {
        foreach ($e in $archive.Entries) {
            if (-not $e.Name) { continue }
            $ext = [IO.Path]::GetExtension($e.Name).ToLowerInvariant()
            if ($text -notcontains $ext -and $e.Name -notlike '.*') { continue }
            $dest = Join-Path $To ($e.FullName -replace '[\\/]', '__')
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dest, $true)
            $n++
        }
    }
    finally { $archive.Dispose() }
    return $n
}

# The DLL's string literals (the #US heap: every "..." in the code) as text, one per line; returns how many.
function Export-DllStrings([string]$Dll, [string]$To) {
    $fs = [IO.File]::OpenRead($Dll)
    try {
        $pe = New-Object System.Reflection.PortableExecutable.PEReader $fs
        $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $sb = New-Object System.Text.StringBuilder
        $n = 0
        $h = [System.Reflection.Metadata.Ecma335.MetadataTokens]::UserStringHandle(1)
        $size = [System.Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetHeapSize($md, [System.Reflection.Metadata.Ecma335.HeapIndex]::UserString)
        while (-not $h.IsNil -and [System.Reflection.Metadata.Ecma335.MetadataTokens]::GetHeapOffset($h) -lt $size) {
            [void]$sb.AppendLine($md.GetUserString($h)); $n++
            $h = [System.Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetNextHandle($md, $h)
        }
        [IO.File]::WriteAllText($To, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
        $pe.Dispose()
        return $n
    }
    finally { $fs.Dispose() }
}

function Quote([string]$s) { if ($s -match '[\s"]') { '"' + ($s -replace '"', '\"') + '"' } else { $s } }

# A feed read back from GitHub in latest.json's own key order.
function Format-Feed($J) {
    $f = [ordered]@{}
    foreach ($k in @('version', 'dll', 'sha256', 'zip', 'notes', 'published', 'commit', 'withdrawn')) { if ($J.Contains($k)) { $f[$k] = $J[$k] } }
    return $f
}

# latest.json as the updater reads it (ordered, UTF-8 without a BOM, a final newline).
function Save-Feed($Feed, [string]$Path) {
    New-Item -ItemType Directory -Path (Split-Path $Path) -Force | Out-Null
    [IO.File]::WriteAllText($Path, ($Feed | ConvertTo-Json -Depth 4) + "`n", (New-Object System.Text.UTF8Encoding($false)))
}

# The test builds' feed: a copy of one build's latest.json plus "withdrawn" (versions, the oldest first), staged in dist\beta\latest.json;
# then (not on a dry run) the rolling prerelease 'beta' is created when it is missing and its one asset replaced.
function Update-BetaFeed($From, [string[]]$Withdrawn, [string]$Why, [bool]$Exists) {
    $feed = [ordered]@{}
    foreach ($k in @('version', 'dll', 'sha256', 'zip', 'notes', 'published', 'commit')) { if ($From.Contains($k)) { $feed[$k] = $From[$k] } }
    $feed['withdrawn'] = @($Withdrawn | Where-Object { $_ } | Sort-Object { [version]$_ } -Unique)
    $staged = Join-Path $dist 'beta\latest.json'
    Save-Feed $feed $staged
    Write-Host "test builds' feed ($Why)$(if ($DryRun) { ' - staged in dist\beta, not published' }):"; Get-Content $staged | ForEach-Object { Write-Host "  $_" }
    $create = @('release', 'create', 'beta', '--repo', $Repo, '--prerelease', '--title', 'Test builds feed', '--target', 'main', '--notes', 'The feed of the in-game auto-updater''s test builds (Beta updates): latest.json names the newest published build and the withdrawn ones. Not a build to download.')
    $upload = @('release', 'upload', 'beta', $staged, '--clobber', '--repo', $Repo)
    if ($DryRun) {
        if (-not $Exists) { Write-Host ("  [dry run] would run: gh " + (($create | ForEach-Object { Quote $_ }) -join ' ')) }
        Write-Host ("  [dry run] would run: gh " + (($upload | ForEach-Object { Quote $_ }) -join ' '))
        return
    }
    if (-not $Exists) { & gh @create; if ($LASTEXITCODE -ne 0) { throw "gh release create beta failed (the feed is not published)" } }
    & gh @upload
    if ($LASTEXITCODE -ne 0) { throw "gh release upload beta failed (the test builds' feed still names the previous build)" }
    Write-Host "[feed] the test builds' feed now names $($feed.version)$(if ($feed.withdrawn.Count) { ' (withdrawn: ' + ($feed.withdrawn -join ', ') + ')' })"
}

# ---------------------------------------------------------------------------------------------------- the release
if ($AllowDirty -and -not $DryRun) { throw "-AllowDirty goes with -DryRun only: a release is made from a clean tree" }
if ((@($Beta.IsPresent, [bool]$Promote, [bool]$Pull) | Where-Object { $_ }).Count -gt 1) { throw "-Beta, -Promote and -Pull go one at a time" }
if (($VerdictFile -or $Force) -and -not $Promote) { throw "-VerdictFile and -Force go with -Promote" }
$modDir = Join-Path $PSScriptRoot "YazsCompanion.Mod"
$dist = Join-Path $PSScriptRoot "dist"
$version = (Select-String -Path (Join-Path $modDir "Plugin.cs") -Pattern 'VERSION = "([^"]+)"').Matches[0].Groups[1].Value
$tag = "v$version"
if (-not $Notes) { $Notes = "YAZS Companion $version" }
$scan = Join-Path $PSScriptRoot '..\overhaul\tools\franchise_scan.ps1'
$hasScan = Test-Path $scan
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$pluginDir = Join-Path $GameDir 'BepInEx\plugins\YazsCompanion'
if ($script:state -and $script:state.pluginDir) { $pluginDir = [string]$script:state.pluginDir }     # the dry runs' own tests (-TestState)

# ---------------------------------------------------------------------------------------------------- -Promote / -Pull (no build)
if ($Promote -or $Pull) {
    $ver = ($(if ($Promote) { $Promote } else { $Pull })).TrimStart('v')
    $vv = $null
    if (-not [version]::TryParse($ver, [ref]$vv) -or $ver -notmatch '^\d+\.\d+\.\d+$') { throw "'$ver' is not a version (x.y.z)" }
    $what = if ($Promote) { 'promote' } else { 'pull' }
    Write-Host ("YAZS Companion: $what $ver" + $(if ($DryRun) { " - DRY RUN: nothing published" } else { "" }))
    if (-not $script:state) {
        if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Stop-Release 'gh' "the GitHub CLI (gh) is not on PATH" }
        $null = & gh auth status 2>&1
        if ($LASTEXITCODE -ne 0) { Stop-Release 'gh' "gh auth status exited ${LASTEXITCODE}: log in with 'gh auth login'" }
        Pass 'gh' "logged in"
    }
    else { Skip 'gh' "-TestState: GitHub stood in for by $TestState" }
    $rels = @(Get-Releases)
    $mine = $rels | Where-Object { $_.Tag -eq "v$ver" } | Select-Object -First 1
    $latest = $rels | Where-Object { $_.Latest } | Select-Object -First 1
    if (-not $mine) { Stop-Release 'channel' "no published release v$ver in $Repo" }
    if ($mine.Draft) { Stop-Release 'channel' "v$ver is a draft (already pulled?)" }

    if ($Promote) {
        if ($vv -lt $Floor) { Stop-Release 'floor' "$ver is below the floor $Floor (the first build that can step back) - never the release" }
        if (-not $mine.Prerelease) { Stop-Release 'channel' "v$ver is already a release, not a test build" }
        if ($latest -and $latest.Version -gt $vv) { Stop-Release 'channel' "the release is $($latest.Version), newer than $ver - promote the newest ready build only" }
        Pass 'channel' "v$ver is a published test build; the release now is $(if ($latest) { $latest.Version } else { 'none' })"
        # the verdict (critic 21: never older than the evidence it names; critic 5: the build it judged is this tag's)
        $feedOf = Read-Feed "https://github.com/$Repo/releases/download/v$ver/latest.json" "tag:v$ver"
        $tagCommit = if ($feedOf.State -eq 'answered' -and $feedOf.Json.commit) { [string]$feedOf.Json.commit } else { $null }
        if ($Force) { Skip 'verdict' "-Force: promoted without a ready verdict - write the reason down where decisions are kept" }
        else {
            if (-not $VerdictFile) { Stop-Release 'verdict' "no -VerdictFile: a test build becomes the release on a PROMOTE-READY verdict (or -Force with a written reason)" }
            if (-not (Test-Path -LiteralPath $VerdictFile)) { Stop-Release 'verdict' "no verdict file at $VerdictFile" }
            $vl = @(Get-Content -LiteralPath $VerdictFile)
            $first = if ($vl.Count -gt 0) { [string]$vl[0] } else { '' }
            if (-not $first.StartsWith('PROMOTE-READY')) { Stop-Release 'verdict' "the verdict says '$first' - not PROMOTE-READY" }
            if ($first -notmatch "(^|[^\d.])$([regex]::Escape($ver))([^\d.]|$)") { Stop-Release 'verdict' "the verdict does not name $ver ('$first')" }
            # the commit it judged: "commit abc1234" or "(abc1234)" on the first line
            $named = [regex]::Matches($first, '\bcommit ([0-9a-f]{7,40})\b|\(([0-9a-f]{7,40})\)') | ForEach-Object { if ($_.Groups[1].Success) { $_.Groups[1].Value } else { $_.Groups[2].Value } }
            if ($named -and $tagCommit) {
                $hit = @($named | Where-Object { $_.StartsWith($tagCommit) -or $tagCommit.StartsWith($_) })
                if ($hit.Count -eq 0) { Stop-Release 'verdict' "the verdict judged commit $($named -join '/'), v$ver is $tagCommit - judge the published build" }
            }
            elseif (-not $named) { Write-Host "  [verdict] WARNING the verdict names no commit; v$ver is $(if ($tagCommit) { $tagCommit } else { 'unknown (its latest.json has no commit)' })" -ForegroundColor Yellow }
            $judged = $null
            $second = if ($vl.Count -gt 1) { ([string]$vl[1]).Trim() } else { '' }
            $fmt = @('yyyy-MM-ddTHH:mm:ssK', 'yyyy-MM-ddTHH:mm:ss', 'yyyy-MM-dd HH:mm:ss', 'yyyy-MM-dd HH:mm', 'yyyyMMdd_HHmmss')
            # 'evidence until <time> (...)': the time is looked for before the parenthesis only - inside it the newest input file's
            # path and the Deck part's "newest <yyyyMMdd_HHmmss>" folder would pass for one when the time is missing (integration fix)
            $m = [regex]::Match(($second -split '\(', 2)[0], '\d{4}-\d\d-\d\d[T ]\d\d:\d\d(:\d\d)?(Z|[+-]\d\d:\d\d)?|\d{8}_\d{6}')
            if ($m.Success) { $t = [datetime]::MinValue; if ([datetime]::TryParseExact($m.Value, [string[]]$fmt, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeLocal, [ref]$t)) { $judged = $t } }
            if (-not $judged) { Stop-Release 'verdict' "the verdict's second line names no evidence time ('$second')" }
            $written = (Get-Item -LiteralPath $VerdictFile).LastWriteTime
            if ($written -lt $judged) { Stop-Release 'verdict' "the verdict file ($($written.ToString('yyyy-MM-dd HH:mm'))) is older than the evidence time it names ($($judged.ToString('yyyy-MM-dd HH:mm'))) - judge again" }
            $logs = @(Get-ChildItem -LiteralPath $pluginDir -Filter 'companion.log*' -File -ErrorAction SilentlyContinue)
            $newest = if ($logs) { ($logs | Sort-Object LastWriteTime -Descending | Select-Object -First 1) } else { $null }
            if ($newest -and $newest.LastWriteTime -gt $judged.AddMinutes(2)) { Stop-Release 'verdict' "$($newest.Name) was written at $($newest.LastWriteTime.ToString('yyyy-MM-dd HH:mm')), after the evidence the verdict judged ($($judged.ToString('yyyy-MM-dd HH:mm'))) - judge again" }
            # critic 21's late upload (integration fix 2026-10-12, the rule the maintainer's other release script applies): the Deck part
            # by FOLDER, never by time - the verdict names the upload folders it judged and -DeckLogs must hold exactly those now
            $dm = [regex]::Match($second, 'Deck uploads: (\d+), newest (\S+), set ([0-9a-f]{12})')
            $nr = [regex]::Match($second, 'Deck not read: ([^)]*)')
            if ($nr.Success) { Stop-Release 'verdict' "the verdict did not read the Deck uploads ($($nr.Groups[1].Value)) - make $DeckLogs reachable, then judge again" }
            if (-not $dm.Success) { Stop-Release 'verdict' "the verdict's second line names no Deck uploads ('Deck uploads: <n>, newest <folder>, set <hash>') - judge again with the current analyzer" }
            $ds = Get-DeckSet $DeckLogs
            if (-not $ds) { Stop-Release 'verdict' "the Deck uploads folder '$DeckLogs' cannot be reached - the verdict's Deck part cannot be checked (mount it or pass -DeckLogs)" }
            if ($ds.Set -ne $dm.Groups[3].Value) { Stop-Release 'verdict' "the Deck uploads changed since the verdict: it judged $($dm.Groups[1].Value) upload folder(s) (newest $($dm.Groups[2].Value), set $($dm.Groups[3].Value)), $DeckLogs holds $($ds.Count) now (newest $($ds.Newest), set $($ds.Set)) - a late upload keeps the Deck's own file times; judge again" }
            Pass 'verdict' "$first (evidence to $($judged.ToString('yyyy-MM-dd HH:mm')); this PC's log $(if ($newest) { 'not newer' } else { 'not found' }); the Deck uploads as judged: $($ds.Count), set $($ds.Set))"
        }
        $edit = @('release', 'edit', "v$ver", '--repo', $Repo, '--prerelease=false', '--latest')
        $dl = @('release', 'download', "v$ver", '--repo', $Repo, '--pattern', "YazsCompanion-$ver-*.zip", '--dir', '<temp>')
        if ($DryRun) {
            Write-Host "[dry run] would run:"
            Write-Host ("  gh " + (($edit | ForEach-Object { Quote $_ }) -join ' '))
            if ($DriveDir) { Write-Host ("  gh " + (($dl | ForEach-Object { Quote $_ }) -join ' ') + "  -> copy the zip to $DriveDir") }
            Write-Host "[dry run] every gate passed ($($script:passed -join ', ')); nothing was published"
            return
        }
        & gh @edit
        if ($LASTEXITCODE -ne 0) { throw "gh release edit v$ver failed - nothing changed on GitHub" }
        Write-Host "[promote] v$ver is the release: $stableFeedUrl now serves its own latest.json"
        if ($feedOf.State -eq 'answered') { Save-Feed (Format-Feed $feedOf.Json) (Join-Path $dist 'latest.json'); Write-Host "[promote] dist\latest.json holds the release's feed again (v$ver)" }
        if ($DriveDir -and (Test-Path $DriveDir)) {
            $tmp = Join-Path ([IO.Path]::GetTempPath()) ("yazs-promote-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
            New-Item -ItemType Directory -Path $tmp -Force | Out-Null
            try {
                $dl[-1] = $tmp
                & gh @dl
                $zip = Get-ChildItem $tmp -Filter "YazsCompanion-$ver-*.zip" | Select-Object -First 1
                if ($zip) { Copy-Item $zip.FullName (Join-Path $DriveDir $zip.Name) -Force; Write-Host "Copied $($zip.Name) to $DriveDir" }
                else { Write-Host "[promote] WARNING no zip of $ver downloaded - copy it to $DriveDir by hand" -ForegroundColor Yellow }
            }
            finally { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
        }
        return
    }

    # -Pull
    $bf = Read-Feed $betaFeedUrl 'betaFeed'
    if ($bf.State -eq 'failed') { Stop-Release 'channel' "the test builds' feed could not be read ($($bf.Why)) - try again" }
    $w = @(); if ($bf.State -eq 'answered' -and $bf.Json.withdrawn) { $w = @($bf.Json.withdrawn | ForEach-Object { [string]$_ }) }
    $w = @($w + $ver | Sort-Object -Unique)
    $left = @($rels | Where-Object { -not $_.Draft -and $_.Version -ge $Floor -and ($w -notcontains $_.Version.ToString()) } | Sort-Object Version -Descending)
    if ($left.Count -eq 0) { Stop-Release 'floor' "pulling $ver would leave no published build at or above $Floor" }
    $to = $left[0]
    $newLatest = $null
    if ($mine.Latest) {
        $newLatest = $left | Where-Object { -not $_.Prerelease } | Select-Object -First 1
        if (-not $newLatest) { Stop-Release 'floor' "$ver is the release and no other release at or above $Floor would remain" }
    }
    $toFeed = Read-Feed "https://github.com/$Repo/releases/download/$($to.Tag)/latest.json" "tag:$($to.Tag)"
    if ($toFeed.State -ne 'answered') { Stop-Release 'channel' "the latest.json of $($to.Tag) could not be read ($($toFeed.Why))" }
    Pass 'channel' "withdrawn after this: $($w -join ', '); the feed goes to $($to.Tag)$(if ($newLatest) { "; the release moves to $($newLatest.Tag)" })"
    Update-BetaFeed $toFeed.Json $w "withdraw $ver, point at $($to.Version)" ($bf.State -eq 'answered')
    $moves = @()
    if ($newLatest) { $moves += , @('release', 'edit', $newLatest.Tag, '--repo', $Repo, '--latest') }
    $moves += , @('release', 'edit', "v$ver", '--repo', $Repo, '--draft=true')
    if ($DryRun) {
        Write-Host "[dry run] would run:"
        foreach ($a in $moves) { Write-Host ("  gh " + (($a | ForEach-Object { Quote $_ }) -join ' ')) }
        Write-Host "[dry run] every gate passed ($($script:passed -join ', ')); nothing was published"
        return
    }
    foreach ($a in $moves) { & gh @a; if ($LASTEXITCODE -ne 0) { throw "gh $($a -join ' ') failed (the feed already withdraws ${ver}: finish by hand)" } }
    if ($newLatest) {
        $nf = Read-Feed "https://github.com/$Repo/releases/download/$($newLatest.Tag)/latest.json" "tag:$($newLatest.Tag)"
        if ($nf.State -eq 'answered') { Save-Feed (Format-Feed $nf.Json) (Join-Path $dist 'latest.json'); Write-Host "[pull] dist\latest.json holds the release's feed again ($($newLatest.Tag))" }
    }
    Write-Host "[pull] $ver withdrawn: every updater on it steps back to $($to.Version)$(if ($newLatest) { "; the release is $($newLatest.Tag)" }); v$ver is a draft"
    return
}

# ---------------------------------------------------------------------------------------------------- a release or a test build
Write-Host ("YAZS Companion $version" + $(if ($Beta) { " (test build)" } else { "" }) + $(if ($DryRun) { " - DRY RUN: every gate, nothing published" } else { "" }))

Push-Location $PSScriptRoot
try {
    # names: the tree, first (a hit stops the release before anything is built)
    if ($hasScan) {
        $out = & $scan 2>&1; $code = $LASTEXITCODE
        if ($code -ne 0) { $out | ForEach-Object { Write-Host "  $_" }; Stop-Release 'names' "the name scan of the tree exited $code" }
        Pass 'names' ([string]($out | Select-Object -Last 1)).Replace('[franchise scan] ', '')
    }
    else { Skip 'names' "no ..\overhaul\tools\franchise_scan.ps1 next to this repository" }

    # tree: clean, and the tag is new
    $head = (git rev-parse --short HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $head) { Stop-Release 'tree' "not a git checkout" }
    $dirty = git status --porcelain
    if (git tag -l $tag) { Stop-Release 'tree' "tag $tag already exists; bump VERSION in Plugin.cs first" }
    if ($dirty) {
        if ($AllowDirty) { Skip 'tree' "uncommitted changes allowed (-AllowDirty, dry run): $(@($dirty).Count) path(s)" }
        else { Stop-Release 'tree' "uncommitted changes; commit first:`n$($dirty -join "`n")" }
    }
    else { Pass 'tree' "clean at $head; tag $tag is new" }
    $wantCommit = if ($dirty) { "$head-dirty" } else { $head }

    # channel (0.16.2): the floor, and the test builds' feed - a test build above the newest published build; a release re-points
    # the feed when it is at or above the feed's version
    $vv = [version]$version
    if ($vv -lt $Floor) { Stop-Release 'channel' "$version is below the floor $Floor (the first build that can step back)" }
    $bf = Read-Feed $betaFeedUrl 'betaFeed'
    if ($bf.State -eq 'failed') { Stop-Release 'channel' "the test builds' feed could not be read ($($bf.Why)) - try again" }
    $feedVer = if ($bf.State -eq 'answered') { $bf.Version } else { $null }
    $withdrawn = @(); if ($bf.State -eq 'answered' -and $bf.Json.withdrawn) { $withdrawn = @($bf.Json.withdrawn | ForEach-Object { [string]$_ }) }
    if ($Beta) {
        $above = $feedVer
        if (-not $above) {
            $sf = Read-Feed $stableFeedUrl 'stableFeed'
            if ($sf.State -eq 'failed') { Stop-Release 'channel' "the release feed could not be read ($($sf.Why)) - try again" }
            if ($sf.State -eq 'answered') { $above = $sf.Version }
        }
        if ($above -and $vv -le $above) { Stop-Release 'channel' "a test build must be above the newest published build ($above): bump VERSION in Plugin.cs" }
        Pass 'channel' "test build $version above $(if ($above) { $above } else { 'nothing published' }) (feed: $(if ($feedVer) { $feedVer } else { 'not there yet - this run creates it' }))"
    }
    else {
        Pass 'channel' "release $version at or above the floor $Floor; the test builds' feed $(if (-not $feedVer) { 'is not there yet - this run creates it' } elseif ($vv -ge $feedVer) { "names $feedVer - this release replaces it" } else { "names the newer $feedVer - left as it is" })"
    }

    # build: our own, its exit code checked
    $dll = Join-Path $modDir "bin\Release\YazsCompanionMod.dll"
    if (-not $NoBuild) {
        Write-Host "Building $version ..."
        $out = & dotnet build $modDir -c Release -p:Deploy=false -nologo 2>&1; $code = $LASTEXITCODE
        $out | Where-Object { $_ -match 'error|warning CS|Build succeeded' } | Select-Object -First 40 | ForEach-Object { Write-Host "  $_" }
        if ($code -ne 0) { Stop-Release 'build' "dotnet build exited $code" }
        Pass 'build' "dotnet build -c Release -p:Deploy=false, exit 0"
    }
    else { Skip 'build' "-NoBuild: the DLL in bin\Release, if the identity gate accepts it" }
    if (-not (Test-Path $dll)) { Stop-Release 'build' "build output missing: $dll" }

    # identity: this source, this commit
    $id = Get-DllIdentity $dll $GameDir
    if ($id.Version -ne $version) { Stop-Release 'identity' "the DLL's BepInPlugin version is '$($id.Version)', Plugin.cs says $version (a stale build?)" }
    if (-not $id.Commit) { Stop-Release 'identity' "the DLL carries no commit (InformationalVersion '$($id.Info)'): built outside git or before 0.15.0" }
    if ($id.Commit -ne $wantCommit) {
        Stop-Release 'identity' ("the DLL was built from '$($id.Commit)', this tree is '$wantCommit'" + $(if ($NoBuild) { " - -NoBuild found a stale or uncommitted build: build again" } else { "" }))
    }
    Pass 'identity' "BepInPlugin $($id.Version), InformationalVersion $($id.Info)"

    # bench: every check, strict (the game data and the built DLL required)
    $benchLog = Join-Path $dist "bench-$version.txt"
    $out = & dotnet run --project (Join-Path $PSScriptRoot "tools\ItemBench\ItemBench.csproj") -c Release -- --strict 2>&1; $code = $LASTEXITCODE
    $out | Out-File -FilePath $benchLog -Encoding utf8
    $verdict = [string]($out | Where-Object { "$_" -like 'bench: *' } | Select-Object -Last 1)
    if ($code -ne 0 -or $verdict -ne 'bench: all as wanted') { Stop-Release 'bench' "$(if ($verdict) { $verdict } else { 'no verdict line' }) (exit $code; full output: $benchLog)" }
    Pass 'bench' "all as wanted, the API contract included ($(@($out).Count) lines in $benchLog)"

    # package: the zip for fresh installs (BepInEx + runtime + mod), holding this DLL and no knowledge.json
    & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "package.ps1") -NoBuild -GameDir $GameDir
    if ($LASTEXITCODE -ne 0) { Stop-Release 'package' "package.ps1 exited $LASTEXITCODE" }
    $zip = Get-ChildItem $dist -Filter "YazsCompanion-$version-*.zip" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $zip) { Stop-Release 'package' "zip for $version not found in $dist" }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip.FullName)
    try {
        $inZip = $archive.Entries | Where-Object { $_.FullName -eq 'BepInEx/plugins/YazsCompanion/YazsCompanionMod.dll' } | Select-Object -First 1
        $stale = @($archive.Entries | Where-Object { $_.Name -like 'knowledge.json*' } | ForEach-Object { $_.FullName })
        $zipSha = $null
        if ($inZip) {
            $s = $inZip.Open()
            try { $zipSha = ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($s))).Replace('-', '').ToLowerInvariant() } finally { $s.Dispose() }
        }
    }
    finally { $archive.Dispose() }
    $sha = (Get-FileHash $dll -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not $inZip) { Stop-Release 'package' "the zip has no BepInEx/plugins/YazsCompanion/YazsCompanionMod.dll" }
    if ($zipSha -ne $sha) { Stop-Release 'package' "the zip's DLL ($zipSha) is not the one just checked ($sha)" }
    if ($stale.Count -gt 0) { Stop-Release 'package' "the zip holds $($stale -join ', ') (the DLL writes its own defaults on the first run)" }
    Pass 'package' "$($zip.Name), $([math]::Round($zip.Length / 1MB, 1)) MB, the DLL's SHA-256 $($sha.Substring(0, 12)), no knowledge.json"

    # zip names: the zip's text files, the DLL's string literals and the notes through the name scan
    if ($hasScan) {
        $tmp = Join-Path ([IO.Path]::GetTempPath()) ("yazs-release-scan-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
        New-Item -ItemType Directory -Path (Join-Path $tmp 'zip') -Force | Out-Null
        try {
            $texts = Export-ZipText $zip.FullName (Join-Path $tmp 'zip')
            $strings = Export-DllStrings $dll (Join-Path $tmp 'YazsCompanionMod.dll.strings.txt')
            [IO.File]::WriteAllText((Join-Path $tmp 'release-notes.txt'), $Notes, (New-Object System.Text.UTF8Encoding($false)))
            $out = & $scan -Path $tmp 2>&1; $code = $LASTEXITCODE
        }
        finally { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
        if ($code -ne 0) { $out | ForEach-Object { Write-Host "  $_" }; Stop-Release 'zip names' "the name scan of the zip, the DLL's strings and the notes exited $code" }
        Pass 'zip names' "$texts text file(s) of the zip, $strings string literal(s) of the DLL and the notes: clean"
    }
    else { Skip 'zip names' "no ..\overhaul\tools\franchise_scan.ps1 next to this repository" }

    # gh: there and logged in
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Stop-Release 'gh' "the GitHub CLI (gh) is not on PATH" }
    $null = & gh auth status 2>&1; $code = $LASTEXITCODE
    if ($code -ne 0) { Stop-Release 'gh' "gh auth status exited ${code}: log in with 'gh auth login'" }
    Pass 'gh' "logged in"

    # the versioned DLL the updater downloads, and the feed it reads (staged in dist, also on a dry run)
    $vdll = Join-Path $dist "YazsCompanionMod-$version.dll"
    Copy-Item $dll $vdll -Force
    $feed = [ordered]@{
        version   = $version
        dll       = "https://github.com/$Repo/releases/download/$tag/YazsCompanionMod-$version.dll"
        sha256    = $sha
        zip       = "https://github.com/$Repo/releases/download/$tag/$($zip.Name)"
        notes     = $Notes
        published = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        commit    = $id.Commit                  # 0.16.2 (critic 5): the build a test build's verdict judged; the updater's dev rule reads it
    }
    # dist\latest.json stays the copy of the RELEASE's feed (a real release, a promotion or a pull writes it; readers take it for
    # the release): a test build's own latest.json is staged in dist\test, a dry run's in dist\dryrun (the asset keeps its name)
    $latest = if ($DryRun) { Join-Path $dist "dryrun\latest.json" } elseif ($Beta) { Join-Path $dist "test\latest.json" } else { Join-Path $dist "latest.json" }
    Save-Feed $feed $latest
    Write-Host "latest.json$(if ($DryRun) { ' (staged in dist\dryrun, not published)' }):"; Get-Content $latest

    $ghArgs = @("release", "create", $tag, $vdll, $latest, $zip.FullName, "--repo", $Repo, "--title", "YAZS Companion $version$(if ($Beta) { ' (test build)' })", "--notes", $Notes)
    if ($Beta) { $ghArgs += "--prerelease" }        # 0.16.2: a test build is never the latest release
    if ($Draft) { $ghArgs += "--draft" }
    $feedToo = -not $Draft -and ($Beta -or -not $feedVer -or $vv -ge $feedVer)
    $switch = Get-BetaSwitch (Join-Path $GameDir 'BepInEx\config\bidoi.yazs.companion.cfg')
    if ($DryRun) {
        Write-Host ""
        Write-Host "[dry run] would run:"
        Write-Host "  git tag -a $tag -m $(Quote "YAZS Companion $version")"
        Write-Host "  git push origin HEAD --tags"
        Write-Host ("  gh " + (($ghArgs | ForEach-Object { Quote $_ }) -join ' '))
        if ($DriveDir -and -not $Beta) { Write-Host "  copy $($zip.Name) to $DriveDir$(if (-not (Test-Path $DriveDir)) { ' (not there now: the copy would be skipped)' })" }
        if ($feedToo) { Update-BetaFeed $feed $withdrawn "$(if ($Beta) { 'a test build' } else { 'a release' }) $version" ($bf.State -eq 'answered') }
        else { Write-Host "  the test builds' feed is left at $feedVer$(if ($Draft) { ' (a draft)' })" }
        if (-not $NoLocalDeploy -and -not $Draft) {
            if ($Beta -and -not $switch.On) { Write-Host "  local DLL: left as it is - Beta updates is Off on this PC ($($switch.From))" }
            else {
                try {
                    $ld = Get-LocalDeploy $pluginDir $sha $version; Write-Host ('  local DLL: ' + $(if ($ld.Act -eq 'copy') { "copy $vdll over $($ld.Target) ($($ld.Why))" } else { $ld.Why }))
                    if ($ld.Act -eq 'copy' -or $ld.Act -eq 'same') { $m = Remove-DevMarker $pluginDir -WhatIf; if ($m) { Write-Host "  local DLL: remove $m (the PC runs the published build)" } }
                }
                catch { Write-Host "  local DLL: not checked ($($_.Exception.Message))" }
            }
        }
        Write-Host "[dry run] every gate passed ($($script:passed -join ', ')); nothing was tagged, pushed, published or copied"
        return
    }

    git tag -a $tag -m "YAZS Companion $version"
    if ($LASTEXITCODE -ne 0) { throw "git tag $tag failed" }
    git push origin HEAD --tags
    if ($LASTEXITCODE -ne 0) { throw "git push failed (the tag $tag exists locally: push it by hand, or 'git tag -d $tag' to start over)" }
    & gh @ghArgs
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed (the tag $tag is pushed: create the release by hand, or delete the tag)" }

    if ($feedToo) {
        try { Update-BetaFeed $feed $withdrawn "$(if ($Beta) { 'a test build' } else { 'a release' }) $version" ($bf.State -eq 'answered') }
        catch { Write-Host "[feed] WARNING $($_.Exception.Message) - the release is out; upload $(Join-Path $dist 'beta\latest.json') to the 'beta' release by hand" -ForegroundColor Yellow }
    }
    if ($DriveDir -and -not $Beta -and (Test-Path $DriveDir)) {
        Copy-Item $zip.FullName (Join-Path $DriveDir $zip.Name) -Force
        Write-Host "Copied $($zip.Name) to $DriveDir"
    }
    if (-not $NoLocalDeploy -and -not $Draft) {
        if ($Beta -and -not $switch.On) { Write-Host "[local] Beta updates is Off on this PC ($($switch.From)) - the test build is not put on it" }
        else {
            try {
                $ld = Get-LocalDeploy $pluginDir $sha $version
                if ($ld.Act -eq 'copy') { Invoke-LocalDeploy $ld $vdll; Write-Host "[local] the PC now runs the released DLL ($($id.Info); was $($ld.Was))" }
                elseif ($ld.Act -eq 'same') {
                    $m = Remove-DevMarker $pluginDir
                    Write-Host "[local] $($ld.Why)$(if ($m) { " - removed $($m) (the deployed build is the published one)" })"
                }
                elseif ($ld.Act -ne 'none') { Write-Host "[local] $($ld.Why)" }
            }
            catch { Write-Host "[local] WARNING the PC's DLL was not replaced ($($_.Exception.Message)) - copy $vdll to $(Join-Path $pluginDir 'YazsCompanionMod.dll') by hand with the game closed; the release itself is out" -ForegroundColor Yellow }
        }
    }
    Write-Host "Released $tag$(if ($Beta) { ' (test build)' }) -> https://github.com/$Repo/releases/tag/$tag (gates: $($script:passed -join ', '))"
}
finally { Pop-Location }
