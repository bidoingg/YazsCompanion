@echo off
rem Builds the BepInEx plugin and copies it into the game's BepInEx\plugins\YazsCompanion folder.
setlocal
set DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet
set PATH=%DOTNET_ROOT%;%PATH%
cd /d "%~dp0YazsCompanion.Mod"
dotnet build -c Release %*
