@echo off
rem Runs the offline item / tag rule bench (tools\ItemBench) over the extracted game data.
rem Usage: tools\bench.cmd [path\to\gamedata.json] [--all]
setlocal
set DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet
set PATH=%DOTNET_ROOT%;%PATH%
dotnet run --project "%~dp0ItemBench\ItemBench.csproj" -c Release -- %*
