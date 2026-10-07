# YAZS Companion - internals

For whoever builds, checks or releases the mod; the [README](../README.md) describes what it does, the
[CHANGELOG](../CHANGELOG.md) what each version changed. Moved here from the README in 0.15.0.

## Offline bench

`tools\bench.cmd [path\to\gamedata.json] [--probe path\to\probe.json] [--all]` compiles the pure files - the item, tag,
clock, synergy, build, quest, wording, WHY, hint and badge rules, the Training Yard plan, the log rotation - into a
console app (`tools\ItemBench`) and runs them over the game's own data: the PC app's extracted `data\gamedata.json`
(from `tools\extract_gamedata.py` in the project root, outside this repository; pass the path if it lives elsewhere)
and the `[Debug] Probe` dump `probe.json` (by default next to it). Use it before changing a rule or a tier. One file per
round of cases (`Checks.cs`, `AdviceFixCases.cs`, `LoadoutCases.cs`, `MatchFixCases.cs`, `RunFixCases.cs`,
`WordingCases.cs`, `QuestCases.cs`, `HintCases.cs`, `ReviewCases.cs`, ...); together they hold:

- **The names**: every preset and kit name against the game's own (a misspelt evolution would silently never match), a
  preset's text against its branch (a tier-3 weapon its summary names is its branch), every survivor's weapon fork
  (three tier-3 weapons after the tier-2 one, each accepted in a build), the game's stat labels.
- **The rules at work**: the same items at 02:00 / 10:00 / 18:30, the modes side by side, which evolution and which
  branch fit which squad, the Training Yard plan of every survivor over the real tree, what Auto follows when a build
  pack is lent (a made-up pack, before and after the other tier-3 branch is taken), the objectives of the 1.0.2 quests
  decoded to the rules the advice follows, every item scored for a few squads (the top picks, anything that reaches the
  `GRAB` threshold 3.0 on keywords alone, the bottom), a Research Pod screen.
- **Logged runs replayed**: a round of Steam Deck logs (recruit ties, evolution headlines, the reroll hint on the
  screens the player rerolled and on the offers after), a 10-04 match (the quest's team rules on seven rescue screens,
  the "abilities first" weapon), a 10-05 run (the Endless stat cards, the class rank a run keeps, the offers under the
  plain words, the *Heroic Theory* and *Trauma* quests, the logged chests through the action hints - 1.98 speaks, 2.51
  / 2.76 / 2.78 and 3.82 do not -, the WHY band's CLOSE CALL at 4.75 / 4.72), the badge advice against its reference
  model, and the log rotation in a test folder.
- **The writing rules** of the cards' plain words, every builder swept over the probe's names: 50 visible characters at
  most (48 beside `AVOID`, also with the names another mod lends, 19 characters longer), only the card font's glyphs,
  none of the ranking's own words (a reintroduced `focus 2>3` fails), each reason said once, two cards of one offer
  never on the same line.
- **The layout**: the band manager on the PC's level-up screen as measured on a 10-05 mark and on the Steam Deck's
  modelled from it - the hint where 0.12.2 drew it, a SKIP line growing left clear of the team panel, the WHY band never
  over a card's lines, a button, the divider, the hint or the team panel, 15 px or more, nowhere when there is no room.

Since 0.15.0 every line the bench prints is watched: its last line reads `bench: all as wanted` (exit 0) or how many lines
are not as wanted (exit 1, or 3 when a section counted them; 2 when the game data is missing). `--strict` is the release
gate (the game data and the built DLL required), `--no-data` the part that needs no game data (the GitHub workflow
`.github/workflows/bench.yml` runs it on every push), and the frozen public API is checked against
`tools/ItemBench/api_v3.txt` (`--api-surface` prints the built DLL's, the start of a new file). Two tools read a log:

- `tools\bench.cmd --check-log <companion.log> [--since last|all|<session stamp>] [--wide on|off]` - PASS or FAIL per
  check: every session's hooks line shows 0 patch classes failed, the WHY band's hooks all took, the game's frame was
  hidden at least once on a screen wider or taller than 16:9 while WideMenus is on, no `[Warning]` and no `[Error]` line,
  and every `[shown]` card text within the writing rules above (with the room it had beside its `2ND` / `AVOID`). The
  newest session by default; checks a session's build did not have yet are skipped for it.
- `python tools\advice_audit.py companion.log.1 companion.log [--since 2026-10-06] [--until HH:MM:SS] [--brief] [--json f]`
  - what the player did with the advice, per session and in total: picks and overrides by screen and by the reason head
  of the recommended card, CLOSE CALL screens, the action hints shown and whether they were acted on within 10 s, WHY
  views per pick and the bands that dropped reasons, the quest objectives met on the run clock, and the level-up pace per
  game mode by phase of the run. Standard library only; `--self-test` checks it against a made-up log.

## Prerequisites (already done on this machine)

- The Nexus "BepInEx 6 IL2CPP Pack" (6.0.0-be.725) extracted into the game folder
  `J:\SteamLibrary\steamapps\common\Yet Another Zombie Survivors`, launched once so that
  `BepInEx\interop\*.dll` exist (the generated managed views of the game code we compile against).
- .NET SDK 8, installed user-locally (no admin) with Microsoft's `dotnet-install.ps1` into
  `%LOCALAPPDATA%\Microsoft\dotnet`. `build.cmd` puts it on `PATH` for you.

## Publish a release

```bash
mod\release.cmd -Notes "what changed"
```

`release.ps1` first runs its gates (0.15.0), each of which stops it before anything is tagged or published: the
maintainer's name scan of the tree (where it is checked out next to this repository), a clean tree and a new tag, its
own build (`dotnet build -c Release -p:Deploy=false`, the exit code checked; it no longer deploys to the local game),
the DLL's identity (its BepInPlugin version is `VERSION`, and the commit the build stamps into it - `0.15.0+abc1234`,
also on the load line `YAZS Companion 0.15.0 (abc1234) loaded from ...` - is HEAD's, clean), the bench with `--strict`
(every check as wanted, the frozen API included), the package (the zip holds this very DLL and no `knowledge.json`), the
name scan again over the zip's text files, the DLL's string literals and the notes, and a logged-in `gh`. Then it
copies the DLL to `dist\YazsCompanionMod-<version>.dll`, writes `dist\latest.json` (version, download URL,
SHA-256, zip URL, notes, date), tags `v<version>`, pushes, creates the GitHub release with the three assets, and
copies the zip into the Drive folder. Bump `VERSION` in `Plugin.cs` first; the tag and the feed come from it.
`-DryRun` runs every gate and prints the commands it would run, publishing nothing (`-AllowDirty` lets a dry run go
on a tree with uncommitted changes); `-Draft` publishes later; `-NoBuild` reuses the build only when the identity gate
finds it built from HEAD. Load-test the build in the game before releasing: a build that fails to load leaves every
auto-updated install without the mod until the next release.

## Log lines

In `BepInEx\plugins\YazsCompanion\companion.log` (the README's *Logs* says where it is and how it rotates): Lines like
  `[offer] LevelUp 03:31 (Normal horde 1)`, `[squad] Tank* L57 Pump-Action Shotgun:4 Sawblade Drone:4 | SWAT L80 ...`,
  `[tags] points: Explosive 7/10, Kinetic 3/10 (special at 10) | deals: Explosive (Rocket Launcher, Minefield),
  Kinetic (Assault Rifle) | stack Explosive` (the log's word; the readout says `next Pod: Explosive`), one `[card] #1 PICK Rocket Launcher (weapon, Tank) 6.40 - next
  step of the weapon line` per card (the ranking's own headline first), one `[shown] LevelUp 03:31: 1 'Next
  weapon tier - the Bombardier build's branch' | 2 '...'` with the lines as drawn (the names another mod lends, no
  `2ND` / `AVOID`), then `[pick] LevelUp 03:31: Rocket Launcher (#1, the pick)` when the
  screen closes. A screen refilled by a reroll or a banish says so on its offer line:
  `[offer] LevelUp 01:52 (Normal horde 1) replaced (reroll): gone Molotov Cocktail; new Fireaxe` (the kind comes
  from the game's own flags when it sets them; a replaced level-up is not counted again by the pace estimate).
  A rescue screen adds `[squad] reroll hint: SHOWN - ...` or `not shown - <why>` with the scores on the cards and of
  everyone who could still come (see "The reroll hint on the rescue screen"); every other screen an
  `[squad] action hint: <screen> <clock>: SHOWN REROLL - ...` or `not shown - ...` line (see "Action hints on every
  selection screen"), a banish `[pick] ... banish on <card> (#4, 1.19)`, and the first time a card is selected on a
  screen `[why] <screen> <clock>: #2 Experiment 21 4.72 - CLOSE CALL 'Either works - ...' | '...' (over the
  buttons, 1 line, 15 px, 3 of 3 shown; <size> units at <x>,<y>; <every band measured>)`, or `no room for the band -
  ...`; at load `[why] hooks: OnSelected Skill ok, Item ok, Military ok, Hashtag ok, SOS ok | OnDeselected (one body
  for every card class) ok`, and once a session `[why] the game reports a card selected (...)`.
  Once a run, with the first plan: `[quest] GameHubQuest_Huntress_4 "Readjust" - 2 objectives (objectives),
  all must hold: 1. StatisticThreshold (LiveAndOnRunFinished) InternalNumSurvivors -> survivors == 1 (the leader
  counts); 2. Survive (LiveAndOnRunFinished) advice unchanged; the run fits the quest's conditions (arena, mode,
  difficulty, leader Huntress); | team rule: stay solo`
  (or `[quest] no active quest this run ...`), then a line whenever what the rule does changes (`[quest]
  GameHubQuest_Huntress_4 (stay solo, team of 1): Liberate first on rescue screens ...`). The first line says what
  every objective does (`2. FinishWithWeapons (Live) requiredWeapons Plasma / Laser / Blaster at level 4,
  in hand (any) -> lifts the Engineer's weapon line to max the tier-3 weapon`), and a second kind of line follows the
  rules for the cards whenever their state changes (a step of the weapon line, a health item taken, a rule met - not
  every kill): `[quest] GameHubQuest_Engineer_3 -> Taser to tier 3, max level: lifts the Engineer's weapon line to max
  the tier-3 weapon; Engineer kills to 2000: a modest lift on the Engineer's damage cards, never over the build's
  core`; once a run, where the health-item count came from (`[quest] health items read from the objective's own
  count`). A key of ours that another
  plugin, the game or Steam uses as well: `[config] Advice.LoadoutEquipKey = F9 is also <plugin>'s [Section] Key ...`
  (a warning, once per value).
  The run setup screen adds `[loadout] ...` lines (see "Badge advice on the run setup screen"); replay them
  offline with `ItemBench --replay-loadout <companion.log>`.
  At load, `[badge] card classes: Hashtag, Item, Military, SOS, Skill - all have a label template`; a card class
  the badges do not know is named in a warning (a game build older than the rescue card class says so once, and its
  rescue cards borrow their first label; the other cards are not affected).

## Debug switches

In `BepInEx\config\bidoi.yazs.companion.cfg`, all off by default.

- Set `Screenshots = true` under `[Debug]` in the config to have the game save a PNG of its own frame
  (`BepInEx\plugins\YazsCompanion\shots\HHmmss_fff_<label>.png`, logged as `[shot] ...`) at the moments that matter
  for judging the UI: each offer with its badges (`offer`), each pick (`pick`), the sidebar 0.3 / 1.5 / 3.5 s into
  a change highlight (`hl1`..`hl3`), every show / hide / creation of the sidebar, and one a minute during play
  (`base`). At most 90 per session; a 3440x1440 frame is about 4 MB, a Deck frame about 1 MB. Off by default; delete
  the folder afterwards.
- Set `Preview = true` under `[Debug]` to see the sidebar without playing: about 5 s after launch, on the main
  menu, it is built on an overlay canvas with sample plans - two survivors, then a pick's change highlight
  (PNGs at 0.35 / 1.5 / 3.6 s), then a full squad, then the fade-out - and a PNG of each stage goes to the shots
  folder whatever the Screenshots setting (`[preview] ...` and `[panel] layout: ...` log lines mark the stages).
  Put a gameplay frame next to the DLL as `preview_bg_<WIDTH>x<HEIGHT>.jpg` (or `.png`; `preview_bg.jpg` without a
  target resolution) and it is drawn behind the readout at that screen's pixels, so the captures show how much of
  the field the readout hides - the menu art cannot tell. Stages: `preview_a`, `preview_hl1..3`,
  `preview_squad` (full squad, compact), `preview_idle` (settled at `PanelIdle`), `preview_detail` (Full),
  `preview_fade`.
  `PreviewResolution = 3440x1440` (or `1280x800`) makes those PNGs show the sidebar with the pixels it has on that
  screen even when the game runs on another desktop: the frame is rendered at a whole multiple of the window and
  the block scaled to match, so only the menu art around it differs (a 1024x768 remote desktop can check the PC's
  and the Deck's look). Off by default.

`[Debug] Perf = true` measures it on a real run: once a minute a line of the shape `[perf] 60 s, <frames> frames:
tick.hud <calls>x <total> ms (max <worst>) | tick.master ... | refresh ... | read ... | plan.build ... | offer ... |
plan.ahead ...` - calls, total and the worst single call per section (sections nest: a tick contains the refresh it
triggered). `offer` and `plan.ahead` run while the game is paused; `tick.*`, `refresh`, `read`, `plan.build` and
`panel.create` (the readout's widget being made: once a run, and once after each use of the mod menu) are what play
pays; `yard.read` / `yard.advise` / `yard.marks` / `yard.strip` split the Training Yard's tick (a menu). How 0.10.1
and 0.10.2 got there, with the measurements before and after, is in the CHANGELOG.

## How it hooks the game

The game code is not obfuscated. The selection screens are `UIGameplayLevelUp`, `UIGameplayChestOpened`,
`UIGameplayMilitaryTraining`, `UIGameplayHashtagEvent` (the Research Pod reward) and `UIGameplayCharacterRescue`
(SOS), all deriving from `UIGameplayUpgradeSelection`. Each overrides `AssignGeneratedElements()`, which fills
the `powerupButtons` array; the mod post-fixes each override, reads `attachedPowerup` / `attachedItem` /
`attachedHashtagEvent` from every active button, ranks, and draws. The base `Hide(clicked)` runs once per screen
for every type and is the pick event. The rescue screen's `SetActionButtonsInteractivity(numRerolls, numBanishes)`
is post-fixed too: the reroll count the game hands it is the one the reroll hint trusts first - on every screen's
own override (never the base's: its body is an empty function the compiler folded with 4,075 others, so a detour
there would fire for all of them). The mod also post-fixes each card class's own `OnSelected()` (Skill, Item,
Military, Hashtag, SOS - the WHY band) and ONE `OnDeselected()` - the five overrides are a single function in
`GameAssembly.dll`, so the skill card's carries every card's deselection and the post-fix reads only the card's
pointer -, `ClickBanish` and `ProcessBanish` (each screen's own; the Research Pod screen's is an empty shared body and
is left alone) to log the banishes, and reads `GetSkipBonusMoney` / `GetSkipBonusHp` for the skip hint. The run setup
screen: post-fixes on `UIViewRunSetup.Update` (the tick), `OnBadgeHighlight` and `UIViewRunSetupBadgeButton.OnHighlight`
(the cursor), `RefreshSelectedBadgeButtons` (a selection changed) and `OnDisable` (the screen closed), each skipped
when its target is missing (`Prepare`), plus a fallback tick from `GameMaster.Update` that reads the main menu's run
setup field; the badges from `PowerupReferences.badges` (`GameplayBadgeStatBoost` / `...HashtagBoost` / `...Physical` /
`...ElementalBoost`: `bonusesPerLevel`, `hashtagsOnLevel` / `hashtagsPerLevel`), their levels from each badge's tree node,
the selection from `_selectedBadges` / `_forcedBadges`, the leader / mode / difficulty from `UIStartGameBar`. The
patch classes are applied one by one, so a target a game patch took away fails alone. Squad state comes from
`GameplayMaster.s_instance.gamePlayers` (the game
stores every survivor's powerups on the leader's player object; they are regrouped by
`targetClassProperties.characterType`), the Training Yard from the nodes reachable through each powerup
(`skillTreeRequirement`, `skillTreeAbilityBoost`) and each class's `skillTreeSynergies`, the run clock from
`currentGameMode`, the damage types from each powerup's `hashtagTypes` and the tag points from
`GameplayMaster.hashtagSystem` (`GetNumType`, `NumRequiredForSpecial`). The sidebar ticks from a post-fix on
`UIGameplay.Update` (the HUD object that owns the gameplay canvas, confirmed in the scene file `level2`: a
GameObject named `UIGameplay` with a Canvas, four `Ability_0n` slots per survivor) and forgets its objects on
`UIGameplay.OnDestroy`. The game's own auto-pick lives in `GameOptions.AutoselectionModes` (Off / On / Skip /
Liberate per category); the mod never touches it.

Every patch is a post-fix that only reads state, so a game update that renames a member breaks the build
(compile error), not the game.

## Layout

```
mod/                              (the GitHub repository bidoingg/YazsCompanion is this folder)
  build.cmd                       build + deploy
  package.cmd / package.ps1       Steam Deck / Windows zip with BepInEx bundled
  release.cmd / release.ps1       the gates (-DryRun), then tag + GitHub release + latest.json for the auto-updater + Drive copy
  tools/bench.cmd, tools/ItemBench/   offline bench: preset validation, clock / mode / evolution / branch scenarios, item scores;
                                  --check-log (a log's health), the frozen API (api_v3.txt), the verdict line
  tools/advice_audit.py           what the player did with the advice, read from companion.log (Python, standard library)
  .github/workflows/bench.yml     the bench's part without game data, on every push and pull request
  art/make_art.py, art/banner.png     the artwork generator (Pillow + numpy) and the repository banner
  README.md, CHANGELOG.md         what the mod does; what each version changed (both travel in the zip)
  docs/INTERNALS.md               this page
  YazsCompanion.Mod/
    YazsCompanion.Mod.csproj      references BepInEx\core + BepInEx\interop from the game folder
    Plugin.cs                     BepInEx entry point, config, Harmony bootstrap, per-mod log file
    LogFile.cs                    companion.log's session header and its rotation over 4 MB at load (pure, 0.14.0)
    Advisor.cs                    Harmony patches; collects the cards, ranks, draws, logs offers and picks
    GameState.cs                  live squad / clock / Training Yard / damage-tag readers over the IL2CPP objects
    Ranker.cs                     the ranking rules: the build, the squad, the clock, the mode
    Context.cs                    the run context (mode, clock, pace, health), the player's doctrine, the timing curves (pure)
    Synergy.cs                    tag value of a level, team-passive boosts, evolution and branch fit, the recruit order, the reroll call,
                                  the stat card weights, the class rank a run keeps, a card's reason prefix (pure)
    Builds.cs / BuildPresets.cs   the build model, the nine kits, builds.json, build packs lent by other mods; the presets and where each comes from (pure)
    Knowledge.cs                  guide-derived tiers, item pairs, scaling items, stat weights; knowledge.json
    ItemRules.cs                  the pure item score: exact squad fit, the clock, live tag points, pairs, build leanings
    Tags.cs                       damage type tag profile of the squad and the Research Pod card score (pure)
    Menu.cs                       the mod menu: builds, editor, advice, display, other mods' options; its own focus and input; the COMPANION buttons
    Api/Extensions.cs             the one public class: other mods register menu options, lend build packs and display names (reflection-friendly)
    Names.cs                      the names drawn for classes / powerups / items: lent by another mod, else the game's; the rules never see them
    Art.cs + Art/                 the embedded artwork (crest, glyph atlas, 9-slice panel, glow, backdrop) as sprites
    Probe.cs                      [Debug] Probe: dumps items, powerups, input actions and the menu layout to probe.json
    Perf.cs                       [Debug] Perf: times the mod's own sections and logs the sums once a minute
    Warmup.cs                     two throw-away plans on the main menu, so a session's first plan is not built cold in the run
    Fx.cs                         motion: the tween runner and the effects (stamp-in, ping, rule draw, type-on, spin, glint)
    Ui.cs                         the shared look (Theme: the game's gold, panel body, hairlines) and uGUI primitives
    Badge.cs                      gold frame on the game's selection rect, RECOMMENDED ribbon (it yields to the game's Skill Tree label), reason line
    Wording.cs                    the cards' reason lines in plain words, built from the verdicts; the glyphs the card font has; the rails (pure, 0.14.0)
    RerollHint.cs                 the action hints: the rescue screen's reroll hint (who could still come, 0.12.2) and REROLL / SKIP / BANISH
                                  on every other screen (0.14.0); the game's reroll count, frame + line, the banishes logged
    ScreenCall.cs                 the action hints' verdict per screen: the floors, the skip bonus's worth, banish protection (pure, 0.14.0)
    WhyText.cs                    the WHY band's words: more reasons in plain words, the "vs #1" sentence, CLOSE CALL, the packing;
                                  two cards' equal lines told apart (pure, 0.14.0)
    ScreenBand.cs                 the band manager: where the hint's line and the WHY band go on the measured screen (pure, 0.14.0)
    WhyUi.cs                      the WHY band on the selection screens: the selection hooks, the measures, the plate (0.14.0)
    WideMenus.cs                  menus on wide and tall screens: the game's black frame hidden, the menus' own backdrops grown to the edges (0.14.0)
    QuestTeam.cs                  the active quest's team rules and what they mean on a rescue screen, the SOS row and the reroll hint (pure, 0.13.0)
    QuestRules.cs                 the active quest's other objectives as rules for the cards (lift, AVOID, modest) and the QUEST row (pure, 0.14.0)
    Quest.cs                      reads the active quest's objectives once a run (one accessor per member), their progress per snapshot, and logs them
    Loadout.cs                    badge advice, pure: badge facts, the run shape, the score, the greedy fill, reasons, the replayable input (0.13.0)
    LoadoutView.cs                badge advice, pure: the diamonds per badge, swaps and the keep margin, summary rows, WHY texts, the EQUIP plan
    LoadoutLayout.cs              badge advice, pure: where the WHY line and the summary go on the measured screen, text and diamond sizes
    LoadoutState.cs               badge advice: the game reader (one accessor per member, a fallback per capability, the load-time checks)
    LoadoutUi.cs                  badge advice on the run setup screen: diamonds, frames, WHY line, summary, motion, logs, hooks, the debug stage
    LoadoutEquip.cs               the optional one-click EQUIP ADVICE button (off by default): guarded presses of the game's badge button, UNDO
    Menu.Badges.cs                the mod menu's BADGES page, the BUILDS cards' badge sentence, the editor's Badges row
    Plan.cs                       the run plan, Compact or Full (keyed rows with label / value / group; sample plans for the preview)
    Panel.cs                      the PLAN readout during play (soft backing, corner placement, text-hugging width, fade, idle dimming, highlight)
    TreePlan.cs                   Training Yard advice, pure: node model, the General order, the survivor rules, simulate / advise
    TreeState.cs                  reads the Training Yard tab on screen into TNode values
    TreeUi.cs                     the order diamonds on the nodes and the SPEND / THEN / WHY strip under the tree
    Preview.cs                    the design previews (config [Debug] Preview / PreviewYard / PreviewResolution)
    Updater.cs                    release-feed check, hash-verified download next to the running DLL, old-build cleanup
    Notice.cs                     the "restart to apply" strip on its own overlay canvas
    Shots.cs                      debug screenshots of the game frame at UI moments (config [Debug] Screenshots)
    Describe.cs                   verbose raw-field dump (config Verbose)
```
