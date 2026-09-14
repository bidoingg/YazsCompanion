@echo off
rem Publishes a release: build + deploy, Steam Deck zip, latest.json, git tag, GitHub release, Drive copy.
rem Usage: release.cmd -Notes "what changed"      (add -Draft to publish later, -NoBuild to reuse the build)
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0release.ps1" %*
