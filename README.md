# YAZS Companion — in-game mod (BepInEx 6 IL2CPP)

The mod version of the companion: it runs inside *Yet Another Zombie Survivors*, reads every
selection screen, the squad and the run clock straight from the game's objects, ranks the offered
cards with a C# port of the PC app's rules (`lib/engine.js` → `Ranker.cs`) and draws the verdict
above each card. No OCR, no overlay, no save-file polling. It never writes to the game's saves
and never picks for you.

Status (2026-09-14): **0.5.1 — card verdicts accepted on PC and Steam Deck; auto-update validated
end to end; the PLAN sidebar is still a diagnostic build.** The sidebar appeared only on the results
screen of the user's Steam Deck runs (0.4.1): one of the 0.4.1 gates (`IsGameplayActive`, `IsPaused`,
`IsGameplayUIVisible()`, `IsDisplayingUpgradeSelection()`) reads false during play and true on the results
screen; the plan logic itself is fine. Since 0.4.2 every signal that could hide the sidebar is logged when it
changes (`[panel] players=1 active=True paused=False pauseMenu=False defeat=False hudVisible=True
selecting=False screen=False hud=True`), only the proven ones hide it (no players in the run, an open
selection screen tracked by the badge code), `[panel] shown` / `hidden` / `created ... canvas WxH screen WxH`
lines trace the drawing, and `GameplayMaster.Update` ticks it as a fallback next to `UIGameplay.Update`
(`[panel] first tick from ...` says which fired). The log of the next run decides which gates to re-enable.
0.5.0 added the auto-updater and the public repository; 0.5.1 ranks the Research Pod reward cards, scores
chest items against what the squad actually deals, adds a `TAGS` line to the plan and an offline bench.

## The PLAN sidebar (during play)

A right-aligned block on the right edge, under the held-item icons and above the minimap, in the
style of the game's quest-objective text: a small diamond-tipped `PLAN` header, then per survivor

```
TANK  L95  +1
Shotgun 2/4  finish it, then Pump-Action Shotgun
Minefield 4/4  evolve: Taunt / Cluster
Sawblade Drone 3/4  evolves: Enchantment / Cogwheels
next ability  Bombing Strike
```

(the `evolve:` line appears only while a maxed ability waits for its unlocked evolution card; a maxed
weapon whose next tier is not bought in the Training Yard reads `next tier locked in the Training Yard`),
then for the run `TAGS  Explosive 7/10, Kinetic 3/10  stack Explosive` (damage type tag points per type,
`/N` until the type's special effect, and the type worth stacking at the next Research Pod), and for the
squad `SOS  Engineer, Huntress` (the two best rescues for this squad by the same rules as the SOS cards) and
`GRAB  Silencer, Black Box` (items worth a chest slot: S/A tier or quest target, not held). It is driven by
the HUD's own `UIGameplay.Update`, rebuilds only when the squad state changes (a cheap key of the squad text,
the active quest and the tag points; a rebuild walks every tree node and item), and dies with the HUD when
the run ends. The intended gates (a selection screen, the pause menu, the results screen) are the open item
above. `ShowPanel`, `PanelTop` and `PanelRight` in the config move or disable it (canvas units on a
3840 x 2160 canvas). Every change is logged as a `[plan] 03:31: ...` line; `[panel] created under UIGameplay
using label 'Quest_Obj1'` shows which HUD text it cloned for the font.

## What you see in the game

- **The recommended card is framed** with a thin gold line on exactly the rect the game's own orange
  hover frame uses (the card root inset 5 canvas units), with a small gold diamond on each corner.
- **A `RECOMMENDED` ribbon hangs under that card**, diamond-tipped like the game's `NEW` / `UPGRADE`
  label, in the card's own font.
- **Every card gets one reason line** just below it (`2ND   new ability`, `finish the weapon: 2 to 3
  of 4`, `S-tier rescue, 2 synergies with the squad`), grey on the others, warm on the pick, dull red
  when a card is worth avoiding. Scores stay in the log.
- Screens covered: level-up (including the stat cards of Endless level-ups), chest, military training,
  SOS rescue (survivors and Liberate), and the Research Pod reward (damage type tag points).

Placement comes from the card prefab read out of the game's asset files (`tools`-free, see
*How it hooks the game*): every card root is 832 x 1462 canvas units on a 3840 x 2160 canvas, the
game's ribbon overhangs the bottom edge, and about 170 units of free band lie below the card before
the divider line. Everything is parented to the card root, so it rises with a hovered card and
vanishes with the screen. Nothing captures clicks.

Turn the badges off with `ShowBadges = false` in `BepInEx\config\bidoi.yazs.companion.cfg`
(the file appears after the first launch). The log keeps working either way.

## How it ranks (guides first, live state second)

The rules follow published guides rather than the player's own history. The tiers they use live in
`BepInEx\plugins\YazsCompanion\knowledge.json` (written on first launch, editable; delete it to reset).
Sources, all read 2026-09-13/14: GoldMath's *Synergy Guide* on Steam (id 3352985777) for survivor pairings
(the mod recomputes the pair synergy counts from the game's own synergy nodes at run time);
yetanotherzombiesurvivors.wiki's tier lists for survivors, weapons, squads and items (1.0.0c2), and its rule
"take each ability once, finish the weapon, then dump into the one ability that is already working";
yetanotherzombiesurvivorswiki.wiki's *best upgrades* page for "max the starting weapon first, one
weapon line, then crit / turret-shield / poison / dodge-regen by squad identity".

- **Level-ups, weapons first**: every weapon level (6.0 and up, "finish the weapon: 2 to 3 of 4") beats
  every ability level (capped at 5.5); a recruit's first weapon 6.6; the next step of the weapon line
  6.4 (+0.2 in the first five minutes; the guides' branch per survivor wins the tier-2 fork among the
  branches your Training Yard unlocked, a branch already taken wins outright); the final weapon 6.8;
  the other branch 1.5.
- **Evolutions**: always the pick (7.5 and up); they only appear when the ability is maxed and the tree
  allows it.
- **Abilities, one each then focus**: a new ability scores 2.8 while you hold fewer than two, 2.2 up
  to four, 1.6 after; leveling scores 2.4 plus 1.2 when it is the open (unmaxed) ability furthest
  along, plus 0.6 on the last level before an unlocked evolution. Guide tiers (S +1.2, A +0.6, C −0.8),
  owned Training Yard synergies with a partner on the squad (+2.5), an unlocked evolution (+1.5) and
  paid tree levels (+0.3 each) add on top, scaled; the total never exceeds 5.5.
- **Chests**: guide tier first (S +3, A +2, B +1, C −1.5), then fit with the squad's damage types from
  the item's description, +3 for the active quest's target item, −1 for a single-slot item already
  held. The damage types come from the game itself: every weapon and ability lists the tags it deals
  (`PowerupBase.hashtagTypes`), so "+4 to Fire damage type tag" scores by how much of the squad's damage
  is Fire (each survivor's current weapon counts 1, each owned ability 0.5), named in the reason
  (`Flamethrower, Molotov: fire`); a malus clause ("−10% Kinetic, Slashing, Explosive damage") counts
  against the squad the same way. The class table (Pyro → fire, ...) is only the fallback when no
  powerup is known yet. Silencer drops without a crit survivor; Glass Cannon is only rated behind an
  Engineer.
- **Research Pod rewards** (`UIGameplayHashtagEvent`, cards of "+N points of one damage type"):
  1 + 0.15 per point, +2.5 scaled by the squad's share of that type, +1.0 for the type you already stack
  most among the ones you deal, +1.5 when the card reaches the special-effect threshold
  (`HashtagSystem.NumRequiredForSpecial`, read live), and the distance to it in the reason
  (`3 more to the special after this`). Types nobody on the squad deals stay at the bottom.
- **SOS**: guide rescue tier (S +2.5 down to C −0.5) plus synergy potential: 0.7 per synergy node
  between the survivor and anyone on the squad, 0.6 more per node you already own, +0.5 per owned
  "while X is on the team" passive, a small bonus for low Training Yard levels and a rank within five
  levels. Liberate: 5 with a full squad, 1.2 otherwise.
- **Military training and Endless stat cards**: rarity (Common 1, Rare 2, Legendary 3, Endless 2.5) plus
  the stat weight from the knowledge file (damage, crit and cooldowns first), +0.3 team-wide.

Run history is deliberately not used. The log shows every score with its reasons, so a verdict you
disagree with can be traced and the knowledge file adjusted.

### Offline bench

`tools\bench.cmd [path\to\gamedata.json] [--all]` compiles the pure rule files (`ItemRules.cs`, `Tags.cs`,
`Knowledge.cs`) into a console app (`tools\ItemBench`) and scores every item of the game for a few squads,
listing the top picks, any item that reaches the `GRAB` threshold (3.0) on keywords alone, the bottom of
the list, and how a Research Pod screen would rank. It reads the PC app's extracted `data\gamedata.json`
(from `tools\extract_gamedata.py` in the project root, outside this repository); pass the path if it lives
elsewhere. Use it before changing a rule or a tier.

## Get it

Fresh install: download the `YazsCompanion-<version>-BepInEx-...-SteamDeck-Windows.zip` from the
[latest release](https://github.com/bidoingg/YazsCompanion/releases/latest) and extract it into the game
folder (steps in the zip's `README-INSTALL.txt`, Steam Deck included). From then on the mod updates itself.

## Auto-update

At every launch the mod fetches `https://github.com/bidoingg/YazsCompanion/releases/latest/download/latest.json`
in the background (`Updater.cs`). If it names a newer version, the DLL is downloaded next to the running one as
`YazsCompanionMod-<version>.dll`, verified against the published SHA-256 and checked to be a managed assembly.
Nothing in use is replaced: BepInEx loads only the highest `[BepInPlugin]` version of a GUID and skips the
others ("because a newer version exists"), so the next launch runs the new file, which deletes the older copies
on load. From the download until that restart a gold strip at the top of the screen reads `YAZS COMPANION
x.y.z DOWNLOADED - RESTART THE GAME TO APPLY` (`Notice.cs`, on its own overlay canvas that survives scene
changes, ticked from `GameMaster.Update`). `knowledge.json` is refreshed with a build's new defaults only if
you never edited it (a hash stamp in `knowledge.json.stamp`; the old file is kept as `.bak`). Config:
`AutoUpdate` (default on) and `UpdateUrl` in `BepInEx\config\bidoi.yazs.companion.cfg`. Offline launches just
skip the check; a bad download is discarded on a hash or assembly check failure. Log lines: `[update] ...`,
`[notice] ...`. Validated on the PC with a local feed and with a real newer build next to the running one;
the Steam Deck (0.5.0 hand-installed) gets its first automatic update with 0.5.1.

## Prerequisites (already done on this machine)

- The Nexus "BepInEx 6 IL2CPP Pack" (6.0.0-be.725) extracted into the game folder
  `J:\SteamLibrary\steamapps\common\Yet Another Zombie Survivors`, launched once so that
  `BepInEx\interop\*.dll` exist (the generated managed views of the game code we compile against).
- .NET SDK 8, installed user-locally (no admin) with Microsoft's `dotnet-install.ps1` into
  `%LOCALAPPDATA%\Microsoft\dotnet`. `build.cmd` puts it on `PATH` for you.

## Build and deploy

```bash
mod\build.cmd
```

That compiles `YazsCompanion.Mod\YazsCompanion.Mod.csproj` (net6.0, matching the .NET 6 runtime
BepInEx ships) and copies `YazsCompanionMod.dll` into `BepInEx\plugins\YazsCompanion\`.
Pass `-p:Deploy=false` to build without copying, `-p:GameDir=...` for another install.
No NuGet packages are needed: it references the BepInEx core and interop DLLs from the game folder.

## Package for Steam Deck / another PC

```bash
mod\package.cmd
```

builds the mod and writes `mod\dist\YazsCompanion-<version>-BepInEx-<build>-SteamDeck-Windows.zip`
(about 53 MB): the exact BepInEx build from this install (core, bundled .NET 6 runtime, `winhttp.dll`,
`doorstop_config.ini`), the Unity base libraries and the interop assemblies already generated for the
current game build (so the Deck needs no download and no slow first launch), `BepInEx\config\BepInEx.cfg`
with the console window turned off, the mod DLL, `knowledge.json`, this README, and a `README-INSTALL.txt`
with the Deck steps. Entry names use forward slashes, so Linux extracts real folders.

Install on the Deck: Desktop Mode, extract the zip into the game folder (Steam > Manage > Browse local
files), set the launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%`, use Proton Experimental or 9+,
launch. `BepInEx/LogOutput.log` proves it loaded. Verified on the user's Deck (Wine 11, SD card path
`S:\steamapps\...`): the card verdicts and the log work as on Windows.

## Publish a release

```bash
mod\release.cmd -Notes "what changed"
```

`release.ps1` refuses to run on an uncommitted tree or an existing tag, builds and deploys, runs `package.ps1`,
copies the DLL to `dist\YazsCompanionMod-<version>.dll`, writes `dist\latest.json` (version, download URL,
SHA-256, zip URL, notes, date), tags `v<version>`, pushes, creates the GitHub release with the three assets, and
copies the zip into the Drive folder. Bump `VERSION` in `Plugin.cs` first; the tag and the feed come from it.
`-Draft` publishes later, `-NoBuild` reuses the build. Load-test the build in the game before releasing: a
build that fails to load leaves every auto-updated install without the mod until the next release.

## Logs

- `BepInEx\plugins\YazsCompanion\companion.log` — only this mod's lines, appended across launches:
  `[offer] LevelUp 03:31 (Normal horde 1)`, `[squad] Tank* L57 Pump-Action Shotgun:4 Sawblade Drone:4 | SWAT L80 ...`,
  `[tags] points: Explosive 7/10, Kinetic 3/10 (special at 10) | deals: Explosive (Rocket Launcher, Minefield),
  Kinetic (Assault Rifle) | stack Explosive`, one `[card] #1 PICK Rocket Launcher (weapon, Tank) 6.40 - next
  step of the weapon line` per card, then `[pick] LevelUp 03:31: Rocket Launcher (#1, the pick)` when the
  screen closes.
- `BepInEx\LogOutput.log` — everything BepInEx logged this launch (overwritten per launch).
- Set `Verbose = true` in the config to also log every raw field of every card and survivor
  (`[raw]` lines) when a verdict looks wrong.

To disable the mod without uninstalling BepInEx, delete or rename `BepInEx\plugins\YazsCompanion\YazsCompanionMod.dll`.
To disable BepInEx entirely, set `enabled = false` in `doorstop_config.ini` in the game folder.

## How it hooks the game

The game code is not obfuscated. The selection screens are `UIGameplayLevelUp`, `UIGameplayChestOpened`,
`UIGameplayMilitaryTraining`, `UIGameplayHashtagEvent` (the Research Pod reward) and `UIGameplayCharacterRescue`
(SOS), all deriving from `UIGameplayUpgradeSelection`. Each overrides `AssignGeneratedElements()`, which fills
the `powerupButtons` array; the mod post-fixes each override, reads `attachedPowerup` / `attachedItem` /
`attachedHashtagEvent` from every active button, ranks, and draws. The base `Hide(clicked)` runs once per screen
for every type and is the pick event. Squad state comes from `GameplayMaster.s_instance.gamePlayers` (the game
stores every survivor's powerups on the leader's player object; they are regrouped by
`targetClassProperties.characterType`), the Training Yard from the nodes reachable through each powerup
(`skillTreeRequirement`, `skillTreeAbilityBoost`) and each class's `skillTreeSynergies`, the run clock from
`currentGameMode`, the damage types from each powerup's `hashtagTypes` and the tag points from
`GameplayMaster.hashtagSystem` (`GetNumType`, `NumRequiredForSpecial`). The sidebar ticks from a post-fix on
`UIGameplay.Update` (the HUD object that owns the gameplay canvas, confirmed in the scene file `level2`: a
GameObject named `UIGameplay` with a Canvas, four `Ability_0n` slots per survivor) and forgets its objects on
`UIGameplay.OnDestroy`. The game's own auto-pick lives in `GameOptions.AutoselectionModes` (Off / On / Skip /
Liberate per category), which a later version could drive with this ranking.

Every patch is a post-fix that only reads state, so a game update that renames a member breaks the build
(compile error), not the game.

## Layout

```
mod/                              (the GitHub repository bidoingg/YazsCompanion is this folder)
  build.cmd                       build + deploy
  package.cmd / package.ps1       Steam Deck / Windows zip with BepInEx bundled
  release.cmd / release.ps1       tag + GitHub release + latest.json for the auto-updater + Drive copy
  tools/bench.cmd, tools/ItemBench/   offline bench of the item and tag rules over the extracted game data
  YazsCompanion.Mod/
    YazsCompanion.Mod.csproj      references BepInEx\core + BepInEx\interop from the game folder
    Plugin.cs                     BepInEx entry point, config, Harmony bootstrap, per-mod log file
    Advisor.cs                    Harmony patches; collects the cards, ranks, draws, logs offers and picks
    GameState.cs                  live squad / clock / Training Yard / damage-tag readers over the IL2CPP objects
    Ranker.cs                     the ranking rules (guide principles + live state)
    Knowledge.cs                  guide-derived tiers; writes/reads plugins\YazsCompanion\knowledge.json
    ItemRules.cs                  item keyword table and the pure item score (squad fit by damage type)
    Tags.cs                       damage type tag profile of the squad and the Research Pod card score (pure)
    Badge.cs                      gold frame on the game's selection rect, RECOMMENDED ribbon, reason line
    Plan.cs                       the run plan (weapon line, ability to feed, next ability, TAGS, SOS, GRAB)
    Panel.cs                      the PLAN sidebar during play (cloned HUD font, throttled rebuilds)
    Updater.cs                    release-feed check, hash-verified download next to the running DLL, old-build cleanup
    Notice.cs                     the "restart to apply" strip on its own overlay canvas
    Describe.cs                   verbose raw-field dump (config Verbose)
```

## Next steps

1. Read `companion.log` of a run on 0.5.x: the `[panel]` gate lines during play, on pause and on the results
   screen, and the `created ... canvas WxH screen WxH` line; then re-enable the gates that read right (expected:
   players == 0, an open selection screen, `GameplayMaster.IsPauseMenuFlowActive`,
   `GameplayMaster.IsDefeatResultsFlowActive`) and fix the placement from what the run showed.
2. Validate the Research Pod verdicts and the `[tags]` lines on a run that has the Research Pod event unlocked
   (a General tree node); confirm the badge draws on those cards (`hashtagShortDescriptionText` is the template).
3. Item ranking: the guide tiers now cover 44 items; the rest score on fit alone. Grow the tiers when a better
   source appears, or add a per-survivor item table.
4. Run history in-process (the game's run-history save) to restore the damage-share and partner weights.
5. Optional, opt-in: drive the game's autoselection with this ranking.
