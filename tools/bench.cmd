@echo off
rem Runs the offline item / tag rule bench (tools\ItemBench) over the extracted game data.
rem Usage: tools\bench.cmd [path\to\gamedata.json] [--all]     the last line: "bench: all as wanted" (exit 0) or what is not
rem        tools\bench.cmd --check-log companion.log [--since last / all / a session stamp]   a log's health, PASS / FAIL per check
rem        tools\bench.cmd --no-data (the cases without game data: CI)   --strict (the release gate)   --api-surface
setlocal
set DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet
set PATH=%DOTNET_ROOT%;%PATH%
dotnet run --project "%~dp0ItemBench\ItemBench.csproj" -c Release -- %*
