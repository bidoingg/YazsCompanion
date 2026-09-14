# Changelog

Versions come from `VERSION` in `YazsCompanion.Mod/Plugin.cs`. `release.cmd` tags one, publishes the GitHub release
with `latest.json`, and every installed copy (Windows PC, Steam Deck) fetches it at its next launch and asks for a
restart. The README describes the current state only; this file keeps what each version changed.

## Unreleased (0.5.1)

Hardening only, nothing game-facing changes:

- The update check starts before the game hooks are applied. A build that a game update has outgrown (a renamed
  member fails its hook) can still fetch the fixed build and show the restart notice.
- Hooks are applied one patch class at a time instead of one `PatchAll`: a renamed game method costs that one hook,
  logged as an error and counted in the load line, instead of the whole mod.
- The updater refuses a download whose assembly name is not `YazsCompanionMod` (a wrong asset behind the feed's
  `dll` URL), on top of the SHA-256 and managed-assembly checks.
- `companion.log` is rotated to `companion.log.1` at launch once it passes 2 MB.
- A `[update] auto-update is off (config)` line when the check is disabled.
- Install notes: disabling the mod after an auto-update means deleting `YazsCompanionMod*.dll`, since the running
  file carries the version from then on.

## 0.5.0 (2026-09-14)

First public release on GitHub (`bidoingg/YazsCompanion`), installed on the Windows PC and the Steam Deck the same day.

- Auto-update: `Updater.cs` fetches `latest.json` from the release feed at launch, downloads a newer DLL next to the
  running one with a SHA-256 check, and `Notice.cs` shows the "restart to apply" strip; the new build removes the old
  file at its first launch. `knowledge.json` is refreshed with new defaults only when never edited (hash stamp).
- Packaging (`package.ps1`): one zip with the exact BepInEx build, its .NET runtime, the generated interop and the mod,
  with a `README-INSTALL.txt` for the Deck and Windows. Release script (`release.ps1`): tag, GitHub release with the
  versioned DLL, `latest.json` and the zip, Drive copy.
- Carries the 0.4.3 diagnostic gating of the PLAN sidebar unchanged.

## 0.4.3

- The selection-screen tracker that hides the sidebar is cleared on the base `Hide` hook instead of waiting for the
  screen object to deactivate.

## 0.4.2 (diagnostic)

- The sidebar had only been seen on the results screen of the Deck's 0.4.1 runs, so every signal that could hide it is
  logged when it changes (`[panel] players=.. active=.. paused=.. ...`) and only the proven ones hide it (no players,
  an open selection screen). `[panel] shown` / `hidden` / `created ...` lines trace the drawing, and
  `GameplayMaster.Update` ticks the sidebar as a fallback next to `UIGameplay.Update`.

## 0.4.1

- Code-review pass on the sidebar: HUD-driven tick, rebuild only when the squad changes, evolve line, tree-locked
  wording, the tier-2 fork choice limited to branches unlocked in the Training Yard.

## 0.4.0

- The PLAN sidebar during play (`Plan.cs`, `Panel.cs`): per survivor the weapon line and its next step, the ability to
  feed and what it evolves into, the next ability worth taking; for the squad the two best rescues (SOS) and the items
  worth a chest slot (GRAB).

## 0.3.1

- Card verdicts accepted on sight: the gold frame on the game's own selection rect, the RECOMMENDED ribbon, one reason
  line under every card; rankings behaving (weapon first, guide rescue tiers).
