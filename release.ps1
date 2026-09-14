<#
.SYNOPSIS
  Publishes a release of the YAZS Companion mod. Builds (and deploys to the local game), packages the
  Steam Deck / Windows zip, writes latest.json (version, DLL url, SHA-256) for the in-game auto-updater,
  tags the commit, creates the GitHub release with the versioned DLL, latest.json and the zip, and copies
  the zip into the Drive folder for hand installs.

  The in-game updater reads https://github.com/<repo>/releases/latest/download/latest.json, so every
  non-draft, non-prerelease release becomes "latest" the moment it is published.

.PARAMETER Notes     Release notes (a line or a paragraph); also shown in the in-game log when downloaded.
.PARAMETER Repo      GitHub repository, owner/name.
.PARAMETER DriveDir  Folder mirrored by Drive for Desktop; pass "" to skip the copy.
.PARAMETER NoBuild   Reuse the existing build output.
.PARAMETER Draft     Create the release as a draft (the updater ignores drafts until published).
.EXAMPLE
  .\release.ps1 -Notes "Sidebar gates fixed from the Deck log"
#>
param(
    [string]$Notes = "",
    [string]$Repo = "bidoingg/YazsCompanion",
    [string]$DriveDir = "N:\My Drive\YAZS Mods",
    [switch]$NoBuild,
    [switch]$Draft
)
$ErrorActionPreference = "Stop"
$modDir = Join-Path $PSScriptRoot "YazsCompanion.Mod"
$dist = Join-Path $PSScriptRoot "dist"
$version = (Select-String -Path (Join-Path $modDir "Plugin.cs") -Pattern 'VERSION = "([^"]+)"').Matches[0].Groups[1].Value
$tag = "v$version"
if (-not $Notes) { $Notes = "YAZS Companion $version" }

Push-Location $PSScriptRoot
try {
    $dirty = git status --porcelain
    if ($dirty) { throw "working tree has uncommitted changes; commit first:`n$dirty" }
    if (git tag -l $tag) { throw "tag $tag already exists; bump VERSION in Plugin.cs first" }

    if (-not $NoBuild) {
        Write-Host "Building $version ..."
        & (Join-Path $PSScriptRoot "build.cmd") | Select-String -Pattern "error|Build succeeded|Deployed" | ForEach-Object { $_.Line.Trim() }
    }
    $dll = Join-Path $modDir "bin\Release\YazsCompanionMod.dll"
    if (-not (Test-Path $dll)) { throw "build output missing: $dll" }

    # the full zip for fresh installs (BepInEx + runtime + mod)
    & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "package.ps1") -NoBuild
    $zip = Get-ChildItem $dist -Filter "YazsCompanion-$version-*.zip" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $zip) { throw "zip for $version not found in $dist" }

    # the versioned DLL the updater downloads, and the feed it reads
    $vdll = Join-Path $dist "YazsCompanionMod-$version.dll"
    Copy-Item $dll $vdll -Force
    $sha = (Get-FileHash $vdll -Algorithm SHA256).Hash.ToLowerInvariant()
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
    Write-Host "latest.json:"; Get-Content $latest

    git tag -a $tag -m "YAZS Companion $version"
    git push origin HEAD --tags
    $ghArgs = @("release", "create", $tag, $vdll, $latest, $zip.FullName, "--repo", $Repo, "--title", "YAZS Companion $version", "--notes", $Notes)
    if ($Draft) { $ghArgs += "--draft" }
    & gh @ghArgs
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }

    if ($DriveDir -and (Test-Path $DriveDir)) {
        Copy-Item $zip.FullName (Join-Path $DriveDir $zip.Name) -Force
        Write-Host "Copied $($zip.Name) to $DriveDir"
    }
    Write-Host "Released $tag -> https://github.com/$Repo/releases/tag/$tag"
}
finally { Pop-Location }
