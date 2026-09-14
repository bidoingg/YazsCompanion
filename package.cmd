@echo off
rem Builds the mod and packs it with BepInEx into mod\dist\*.zip (Steam Deck + Windows).
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0package.ps1" %*
