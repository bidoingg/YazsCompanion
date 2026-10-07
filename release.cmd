@echo off
rem Publishes a release after its gates (name scans, clean tree, build, DLL identity, bench, zip): Steam Deck zip, latest.json,
rem git tag, GitHub release, Drive copy. Does not deploy to the local game (build.cmd does).
rem Usage: release.cmd -Notes "what changed"      (-DryRun: every gate, nothing published; -Draft to publish later; -NoBuild to reuse the build)
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0release.ps1" %*
