<#
.SYNOPSIS
  Builds a ready-to-extract zip of the YAZS Companion mod bundled with the exact BepInEx install
  it was developed against (BepInEx 6 IL2CPP + bundled .NET runtime + pre-generated interop), for
  Steam Deck (Proton) and Windows. Extract the zip into the game folder; done.

.PARAMETER GameDir   Game install that holds a working BepInEx (source of the runtime files).
.PARAMETER OutDir    Where the zip goes (default: mod\dist).
.PARAMETER NoBuild   Skip rebuilding the mod DLL first.
#>
param(
    [string]$GameDir = "J:\SteamLibrary\steamapps\common\Yet Another Zombie Survivors",
    [string]$OutDir = (Join-Path $PSScriptRoot "dist"),
    [switch]$NoBuild
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$modDir = Join-Path $PSScriptRoot "YazsCompanion.Mod"
$dll = Join-Path $modDir "bin\Release\YazsCompanionMod.dll"
$version = (Select-String -Path (Join-Path $modDir "Plugin.cs") -Pattern 'VERSION = "([^"]+)"').Matches[0].Groups[1].Value
$bepinex = (Get-Content (Join-Path $GameDir "BepInEx\LogOutput.log") -ErrorAction SilentlyContinue | Select-String -Pattern 'BepInEx ([0-9][^ ]*) -' | Select-Object -First 1).Matches[0].Groups[1].Value
if (-not $bepinex) { $bepinex = "6.0.0-be.725" }
$gameExe = Join-Path $GameDir "Yet Another Zombie Survivors.exe"
$gameBuild = (Get-Item (Join-Path $GameDir "GameAssembly.dll")).LastWriteTime.ToString("yyyy-MM-dd")

if (-not $NoBuild) {
    Write-Host "Building mod $version ..."
    $env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
    Push-Location $modDir
    try { dotnet build -c Release -p:Deploy=false | Select-String -Pattern "error|Build succeeded" | ForEach-Object { $_.Line.Trim() } } finally { Pop-Location }
}
if (-not (Test-Path $dll)) { throw "mod DLL not found: $dll" }

# ---- stage ----
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("yazs-package-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
New-Item -ItemType Directory -Path $stage | Out-Null
function CopyTree($from, $to, $exclude) {
    New-Item -ItemType Directory -Path $to -Force | Out-Null
    Get-ChildItem -Path $from -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($from.Length).TrimStart('\', '/')
        if ($exclude -and ($exclude | Where-Object { $rel -like $_ })) { return }
        $dest = Join-Path $to $rel
        New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
        Copy-Item $_.FullName $dest
    }
}
foreach ($f in @("winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt")) {
    $src = Join-Path $GameDir $f
    if (Test-Path $src) { Copy-Item $src (Join-Path $stage $f) } else { Write-Warning "missing $f" }
}
CopyTree (Join-Path $GameDir "dotnet") (Join-Path $stage "dotnet") $null
CopyTree (Join-Path $GameDir "BepInEx\core") (Join-Path $stage "BepInEx\core") $null
CopyTree (Join-Path $GameDir "BepInEx\unity-libs") (Join-Path $stage "BepInEx\unity-libs") $null
CopyTree (Join-Path $GameDir "BepInEx\interop") (Join-Path $stage "BepInEx\interop") $null
New-Item -ItemType Directory -Path (Join-Path $stage "BepInEx\patchers") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stage "BepInEx\config") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stage "BepInEx\plugins\YazsCompanion") -Force | Out-Null

# BepInEx.cfg: the user's file with the console window turned off (a stray console is a nuisance under Proton / Gaming Mode)
$cfgSrc = Join-Path $GameDir "BepInEx\config\BepInEx.cfg"
if (Test-Path $cfgSrc) {
    $cfg = Get-Content $cfgSrc -Raw
    $cfg = [regex]::Replace($cfg, '(\[Logging\.Console\][^\[]*?Enabled\s*=\s*)true', '${1}false')
    Set-Content -Path (Join-Path $stage "BepInEx\config\BepInEx.cfg") -Value $cfg -NoNewline
}

Copy-Item $dll (Join-Path $stage "BepInEx\plugins\YazsCompanion\YazsCompanionMod.dll")
$kj = Join-Path $GameDir "BepInEx\plugins\YazsCompanion\knowledge.json"
if (Test-Path $kj) { Copy-Item $kj (Join-Path $stage "BepInEx\plugins\YazsCompanion\knowledge.json") }
Copy-Item (Join-Path $PSScriptRoot "README.md") (Join-Path $stage "BepInEx\plugins\YazsCompanion\README.md")

$readme = @"
YAZS Companion $version  -  in-game advisor for Yet Another Zombie Survivors
============================================================================
Bundled: BepInEx $bepinex (IL2CPP) with its .NET 6 runtime, the Unity base libraries and the
interop assemblies already generated for the game build of $gameBuild, and the mod itself.
Extract this archive INTO the game folder so that winhttp.dll sits next to
"Yet Another Zombie Survivors.exe". Nothing else to install.

STEAM DECK (Proton)
-------------------
1. Switch to Desktop Mode.
2. Find the game folder: in Steam, right-click the game > Manage > Browse local files.
   (Internal storage: ~/.local/share/Steam/steamapps/common/Yet Another Zombie Survivors
    SD card:          /run/media/mmcblk0p1/steamapps/common/Yet Another Zombie Survivors)
3. Copy this zip there and extract it in place (right-click > Extract > Extract archive here).
   Afterwards the folder contains winhttp.dll, doorstop_config.ini, a dotnet folder and a BepInEx folder.
4. In Steam: right-click the game > Properties > General > Launch Options, paste exactly:
       WINEDLLOVERRIDES="winhttp=n,b" %command%
   Properties > Compatibility: Proton Experimental or Proton 9 or newer.
5. Launch the game. The first start takes a little longer than usual.

Check it worked: BepInEx/LogOutput.log in the game folder contains "YAZS Companion $version loaded".
In a run, a PLAN block appears on the right edge of the HUD, and the first level-up screen shows a
gold frame and a RECOMMENDED ribbon under one card.

If the log file never appears, Proton did not load winhttp.dll: install Protontricks from Discover,
open it, pick this game, "Select the default wineprefix" > "Run winecfg" > Libraries tab > type winhttp,
Add, make sure it reads "(native, builtin)", Apply. Then launch again.
Game Mode works fine once it runs; no console window opens (disabled in BepInEx/config/BepInEx.cfg).

WINDOWS
-------
Extract this archive into the game folder (Steam > right-click the game > Manage > Browse local files)
and launch. Same files, no launch option needed.

WHAT IT DOES
------------
Read-only. It never writes to the save files, never picks for you, and does not touch achievements.
On every power-up, chest, military training and SOS screen it frames the recommended card, hangs a
RECOMMENDED ribbon under it and prints one reason line under every card. During play a PLAN block on
the right edge lists, per survivor, the weapon line and its next step, the ability to feed and the next
ability worth taking, then who to rescue (SOS) and which items to grab. Rules follow published guides
(sources in BepInEx/plugins/YazsCompanion/knowledge.json, which you may edit; delete it to reset).
Config: BepInEx/config/bidoi.yazs.companion.cfg (ShowBadges, ShowPanel, PanelTop, PanelRight).
Mod log: BepInEx/plugins/YazsCompanion/companion.log.  Full notes: BepInEx/plugins/YazsCompanion/README.md.

AUTO-UPDATE
-----------
At every launch the mod checks the release feed (https://github.com/bidoingg/YazsCompanion/releases) and
downloads a newer build next to the running one. A gold strip at the top of the screen then reads
"YAZS COMPANION x.y.z DOWNLOADED - RESTART THE GAME TO APPLY"; the next launch runs the new build and
removes the old file. Only the mod DLL is updated this way; BepInEx itself never needs to change.
Turn it off with AutoUpdate = false in BepInEx/config/bidoi.yazs.companion.cfg.

AFTER A GAME UPDATE
-------------------
BepInEx regenerates its interop assemblies by itself (a slower first launch). If the game renamed
internals, the mod may stop logging until it is rebuilt against the new build.

UNINSTALL
---------
Delete winhttp.dll, doorstop_config.ini, .doorstop_version, changelog.txt, the dotnet folder and the
BepInEx folder from the game folder, and remove the launch option. To only disable the mod, delete
BepInEx/plugins/YazsCompanion/YazsCompanionMod.dll.
"@
Set-Content -Path (Join-Path $stage "README-INSTALL.txt") -Value $readme

# ---- zip (forward slashes in entry names so Linux extracts folders, not files with backslashes) ----
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$zipName = "YazsCompanion-$version-BepInEx-$bepinex-SteamDeck-Windows.zip"
$zip = Join-Path $OutDir $zipName
if (Test-Path $zip) { Remove-Item $zip }
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $files = Get-ChildItem -Path $stage -Recurse -File
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($stage.Length).TrimStart('\', '/').Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $f.FullName, $rel, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    # keep the empty folders BepInEx expects
    foreach ($d in @("BepInEx/patchers/")) { $archive.CreateEntry($d) | Out-Null }
}
finally { $archive.Dispose() }
Remove-Item -Recurse -Force $stage

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Packaged $($files.Count) files -> $zip ($size MB)"
