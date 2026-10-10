<#
.SYNOPSIS
  Publishes a release of the YAZS Companion mod, after a row of gates; each one stops the release before anything is tagged,
  pushed or published (0.15.0):
    names     the maintainer's private name scan of the tree (only where overhaul\tools is checked out next to this
              repository; other clones skip it): no name of another mod's content packs may reach this public repository;
    tree      a clean working tree and a tag that does not exist yet;
    build     dotnet build -c Release -p:Deploy=false, its exit code checked (-NoBuild: the DLL already in bin\Release);
    identity  the DLL's BepInPlugin version is VERSION of Plugin.cs and the commit stamped into it is HEAD's, clean (read with
              Mono.Cecil from the game's BepInEx\core, nothing loaded) - a stale or uncommitted build is refused;
    bench     tools\ItemBench --strict: every check as wanted, the frozen public API (api_v3.txt) included;
    package   package.ps1 -NoBuild: the zip holds this very DLL (same SHA-256) and no knowledge.json;
    zip names the name scan again over the zip's text files, the DLL's string literals and the release notes;
    gh        the GitHub CLI is there and logged in.
  Then it writes latest.json (version, DLL url, SHA-256) for the in-game auto-updater, tags the commit, pushes, creates the
  GitHub release with the versioned DLL, latest.json and the zip, and copies the zip into the Drive folder for hand installs.
  After publishing (not with -Draft), the released DLL is copied over the PC's deployed one when the game is closed, no
  updater download waits and the PC does not run a newer build (-NoLocalDeploy skips it); build.cmd stays the deploy of
  work in progress.

  The in-game updater reads https://github.com/<repo>/releases/latest/download/latest.json, so every
  non-draft, non-prerelease release becomes "latest" the moment it is published.

.PARAMETER Notes       Release notes (a line or a paragraph); also shown in the in-game log when downloaded.
.PARAMETER Repo        GitHub repository, owner/name.
.PARAMETER DriveDir    Folder mirrored by Drive for Desktop; pass "" to skip the copy.
.PARAMETER GameDir     Game install: Mono.Cecil in BepInEx\core reads the DLL, and package.ps1 takes BepInEx from it.
.PARAMETER NoBuild     Reuse the DLL in bin\Release - only when it was built from HEAD with a clean tree.
.PARAMETER Draft       Create the release as a draft (the updater ignores drafts until published). The PC's DLL is left as it is.
.PARAMETER DryRun      Run every gate, stage the zip and latest.json in dist, print the git / gh commands it would run, and stop:
                       nothing is tagged, pushed, published or copied.
.PARAMETER AllowDirty  With -DryRun only: run the gates on a tree with uncommitted changes (the tree gate is skipped, the
                       DLL's commit may carry "-dirty"). A real release always needs a clean tree.
.PARAMETER NoLocalDeploy  Leave the PC's deployed DLL as it is.
.EXAMPLE
  .\release.ps1 -Notes "Sidebar gates fixed from the Deck log"
  .\release.ps1 -DryRun -Notes "0.15.0 test"
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
    [switch]$NoLocalDeploy
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

# what the release does with the PC's deployed DLL (game closed, no updater download waiting, not newer than this release)
function Get-LocalDeploy([string]$LocalDll, [string]$Sha, [string]$Version) {
    if (-not (Test-Path $LocalDll)) { return @{ Act = 'none'; Was = ''; Why = "no $LocalDll" } }
    $was = [Diagnostics.FileVersionInfo]::GetVersionInfo($LocalDll).ProductVersion
    $wasV = $null; [void][version]::TryParse(($was -replace '\+.*$', ''), [ref]$wasV)
    $pending = @(Get-ChildItem (Split-Path $LocalDll) -Filter 'YazsCompanionMod-*.dll' -ErrorAction SilentlyContinue | ForEach-Object Name)
    if ((Get-FileHash $LocalDll -Algorithm SHA256).Hash.ToLowerInvariant() -eq $Sha) { return @{ Act = 'same'; Was = $was; Why = 'the PC already runs the released DLL' } }
    if ($wasV -and $wasV -gt [version]$Version) { return @{ Act = 'skip'; Was = $was; Why = "the PC runs a newer build ($was) - left as it is" } }
    if ($pending.Count -gt 0) { return @{ Act = 'skip'; Was = $was; Why = "an updater download waits in the plugin folder ($($pending -join ', ')) - the PC's DLL ($was) left as it is" } }
    if (Get-Process -Name 'Yet Another Zombie Survivors' -ErrorAction SilentlyContinue) { return @{ Act = 'skip'; Was = $was; Why = "the game is running - the PC keeps $was; copy the released DLL over it after it closes" } }
    return @{ Act = 'copy'; Was = $was; Why = "the PC runs $was" }
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

# ---------------------------------------------------------------------------------------------------- the release
if ($AllowDirty -and -not $DryRun) { throw "-AllowDirty goes with -DryRun only: a release is made from a clean tree" }
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
Write-Host ("YAZS Companion $version" + $(if ($DryRun) { " - DRY RUN: every gate, nothing published" } else { "" }))

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
    $localDll = Join-Path $GameDir 'BepInEx\plugins\YazsCompanion\YazsCompanionMod.dll'
    $feed = [ordered]@{
        version   = $version
        dll       = "https://github.com/$Repo/releases/download/$tag/YazsCompanionMod-$version.dll"
        sha256    = $sha
        zip       = "https://github.com/$Repo/releases/download/$tag/$($zip.Name)"
        notes     = $Notes
        published = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    }
    $latest = Join-Path $dist "latest.json"
    [IO.File]::WriteAllText($latest, ($feed | ConvertTo-Json) + "`n", (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "latest.json$(if ($DryRun) { ' (staged in dist, not published)' }):"; Get-Content $latest

    $ghArgs = @("release", "create", $tag, $vdll, $latest, $zip.FullName, "--repo", $Repo, "--title", "YAZS Companion $version", "--notes", $Notes)
    if ($Draft) { $ghArgs += "--draft" }
    if ($DryRun) {
        Write-Host ""
        Write-Host "[dry run] would run:"
        Write-Host "  git tag -a $tag -m $(Quote "YAZS Companion $version")"
        Write-Host "  git push origin HEAD --tags"
        Write-Host ("  gh " + (($ghArgs | ForEach-Object { Quote $_ }) -join ' '))
        if ($DriveDir) { Write-Host "  copy $($zip.Name) to $DriveDir$(if (-not (Test-Path $DriveDir)) { ' (not there now: the copy would be skipped)' })" }
        if (-not $NoLocalDeploy -and -not $Draft) {
            try { $ld = Get-LocalDeploy $localDll $sha $version; Write-Host ('  local DLL: ' + $(if ($ld.Act -eq 'copy') { "copy $vdll over $localDll ($($ld.Why))" } else { $ld.Why })) }
            catch { Write-Host "  local DLL: not checked ($($_.Exception.Message))" }
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

    if ($DriveDir -and (Test-Path $DriveDir)) {
        Copy-Item $zip.FullName (Join-Path $DriveDir $zip.Name) -Force
        Write-Host "Copied $($zip.Name) to $DriveDir"
    }
    if (-not $NoLocalDeploy -and -not $Draft) {
        try {
            $ld = Get-LocalDeploy $localDll $sha $version
            if ($ld.Act -eq 'copy') { Copy-Item $vdll $localDll -Force -ErrorAction Stop; Write-Host "[local] the PC now runs the released DLL ($($id.Info); was $($ld.Was))" }
            elseif ($ld.Act -ne 'none') { Write-Host "[local] $($ld.Why)" }
        }
        catch { Write-Host "[local] WARNING the PC's DLL was not replaced ($($_.Exception.Message)) - copy $vdll to $localDll by hand with the game closed; the release itself is out" -ForegroundColor Yellow }
    }
    Write-Host "Released $tag -> https://github.com/$Repo/releases/tag/$tag (gates: $($script:passed -join ', '))"
}
finally { Pop-Location }
