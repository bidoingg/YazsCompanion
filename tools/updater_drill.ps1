<#
.SYNOPSIS
  The updater drill (0.16.2, the beta channel WP1; critic 20 of the plan): the Companion's REAL update check - HttpClient, the
  files, the renames - against a feed served from http://localhost:<port>, over scratch plugin folders whose running DLL is loaded
  the way BepInEx loads a plugin. Nothing of the game, its folder or its config is touched; everything lives in a temp folder that
  goes at the end.

  1. compiles five made-up builds (a type each and the InformationalVersion the updater reads: 0.16.1, 0.16.2, 0.16.3, 0.16.4
     and a "liar" whose file says 0.16.9) with Add-Type into the scratch folder;
  2. serves <scratch>\feed with "python -I -m http.server --bind 127.0.0.1" on a free port (the loopback only: no firewall
     prompt), and leaves a second free port closed (a feed that does not answer);
  3. runs the bench's host (ItemBench --updater-drill, tools\ItemBench\UpdaterDrill.cs; the bench must be built: tools\bench.cmd)
     through the scenes D1 - D18: download, a second run doing nothing more, step down, a withdrawn build, a SHA-256 mismatch, a
     file that says another version, a refused rename leaving .offnext, the kept release with no download, a pending test build
     set aside when Off, a feed that does not answer, no test-builds feed (404), a development deploy, a feed at 127.0.0.1 naming
     a plain-http DLL (refused: the https exemption is http://localhost only); and since the review fixes (2026-10-12) D14 - D18:
     Off with either feed not answering still sets a downloaded test build aside, the kept release is always the release (copied
     at a test build's download, the only .keep after test build to test build offline, none after a new release or at the
     first 0.16.2 start), and D7 asserts that a refused rename promises no restart and that its next start downloads nothing;
  4. stops the server and removes the scratch folder (-Keep leaves it).
  The last line: "updater drill: N of M PASS"; exit 0 only when every scene passed (2 = could not start).
.EXAMPLE
  pwsh -NoProfile -File mod\tools\updater_drill.ps1
#>
param(
    [int]$Port = 0,
    [switch]$Keep
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
$bench = Join-Path $PSScriptRoot 'ItemBench\bin\Release\net8.0\ItemBench.dll'
if (-not (Test-Path $bench)) { Write-Host "updater drill: the bench is not built ($bench) - run tools\bench.cmd --no-data first"; exit 2 }
if (-not (Get-Command python -ErrorAction SilentlyContinue)) { Write-Host "updater drill: python is not on PATH (it serves the feed)"; exit 2 }

$root = Join-Path ([IO.Path]::GetTempPath()) ("yazs_updater_drill_" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$dlls = Join-Path $root 'dlls'
$feed = Join-Path $root 'feed'
New-Item -ItemType Directory -Force -Path $dlls, $feed | Out-Null

# 1. the made-up builds: a type each (its own namespace) and the InformationalVersion the updater reads from the metadata
foreach ($b in @(@('v0.16.1', '0.16.1+drill161'), @('v0.16.2', '0.16.2+drill162'), @('v0.16.3', '0.16.3+drill163'), @('v0.16.4', '0.16.4+drill164'), @('liar', '0.16.9+drilliar'))) {
    $ns = 'YazsDrill' + ($b[0] -replace '[^A-Za-z0-9]', '')
    $src = "[assembly: System.Reflection.AssemblyInformationalVersion(""$($b[1])"")] namespace $ns { public static class Build { public const string Info = ""$($b[1])""; } }"
    Add-Type -TypeDefinition $src -OutputAssembly (Join-Path $dlls "$($b[0]).dll") -OutputType Library
}

# 2. the feed server on the loopback, and a port left closed
function Get-FreePort {
    $l = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $l.Start(); $p = $l.LocalEndpoint.Port; $l.Stop()
    return $p
}
if ($Port -eq 0) { $Port = Get-FreePort }
$closed = Get-FreePort
while ($closed -eq $Port) { $closed = Get-FreePort }
$log = Join-Path $root 'server.log'
$server = Start-Process -FilePath python -ArgumentList @('-I', '-m', 'http.server', "$Port", '--bind', '127.0.0.1', '--directory', $feed) -PassThru -NoNewWindow `
    -RedirectStandardError $log -RedirectStandardOutput (Join-Path $root 'server.out')
$code = 2
try {
    $up = $false
    for ($i = 0; $i -lt 50 -and -not $up; $i++) {
        try { $c = [Net.Sockets.TcpClient]::new(); $c.Connect('127.0.0.1', $Port); $c.Dispose(); $up = $true }
        catch { Start-Sleep -Milliseconds 200 }
    }
    if (-not $up) { Write-Host "updater drill: the feed server did not come up on 127.0.0.1:$Port"; exit 2 }
    Write-Host "updater drill: feed served from $feed at http://localhost:$Port (server pid $($server.Id))"

    # 3. the scenes, in the bench's host
    & dotnet $bench --updater-drill --root $root --base "http://localhost:$Port" --ip-base "http://127.0.0.1:$Port" --dlls $dlls --closed $closed
    $code = $LASTEXITCODE
    if (Test-Path $log) {
        $gets = @(Get-Content $log | Where-Object { $_ -match '"GET ' })
        Write-Host "updater drill: the server answered $($gets.Count) request(s) ($(@($gets | Where-Object { $_ -match '/dl/' }).Count) for a DLL)"
    }
}
finally {
    # 4. the server stops, the scratch folder goes
    try { Stop-Process -Id $server.Id -Force -ErrorAction Stop } catch { }
    if ($Keep) { Write-Host "updater drill: kept $root" }
    else { Start-Sleep -Milliseconds 300; Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
}
exit $code
