# YAZS Companion - internals

For whoever builds, checks or releases the mod; the [README](../README.md) describes what it does, the
[CHANGELOG](../CHANGELOG.md) what each version changed. Moved here from the README in 0.15.0; 0.16.0 moved the build and
package steps, the photo walks, the log lines of the hints and of the run setup screen, and the cost table here too.

## Offline bench

`tools\bench.cmd [path\to\gamedata.json] [--probe path\to\probe.json] [--all]` compiles the pure files - the item, tag,
clock, synergy, build, quest, wording, WHY, hint and badge rules, the item book and the held-item rules, the Training Yard
plan, the log rotation - into a console app (`tools\ItemBench`) and runs them over the game's own data: the PC app's
extracted `data\gamedata.json` (from `tools\extract_gamedata.py` in the project root, outside this repository; pass the
path if it lives elsewhere) and the `[Debug] Probe` dump `probe.json` (by default next to it). Use it before changing a
rule or a tier. One file per round of cases (`Checks.cs`, `AdviceFixCases.cs`, `LoadoutCases.cs`, `MatchFixCases.cs`,
`RunFixCases.cs`, `WordingCases.cs`, `QuestCases.cs`, `HintCases.cs`, `ReviewCases.cs`, 0.16.0's `ItemBookCases.cs`,
`HeldCases.cs`, `RankSmallCases.cs`, `YardBadgeCases.cs`, ...); together they hold:

- **The names**: every preset and kit name against the game's own (a misspelt evolution would silently never match), a
  preset's text against its branch (a tier-3 weapon its summary names is its branch), every survivor's weapon fork
  (three tier-3 weapons after the tier-2 one, each accepted in a build), the game's stat labels; since 0.16.0 also the
  item book's names and the revive abilities (as power names).
- **The rules at work**: the same items at 02:00 / 10:00 / 18:30, the modes side by side, which evolution and which
  branch fit which squad, the Training Yard plan of every survivor over the real tree, what Auto follows when a build
  pack is lent (a made-up pack, before and after the other tier-3 branch is taken), the objectives of the 1.0.2 quests
  decoded to the rules the advice follows, every item scored for a few squads (the top picks, anything that reaches the
  `GRAB` threshold 3.0, the bottom), a Research Pod screen.
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
- **0.16.0's sections**:
  - `ItemBookCases.cs`, the item book (and the revive rule on it): IB1 every pool item of the probe has a rule; IB2 the
    rows parse, needs and clocks are known, every Say is on the rails, no unrated value over B, the stated pairs /
    clashes / conversions name rows; IB3 the table of all 134 rows in four reference squads against
    `tools\ItemBench\itembook_expected.txt` (numbers within 0.01, lines equal; `ItemBench --write-itembook [--probe
    <probe.json>]` rewrites it after an intended rule change); IB4 the 0.15.0 mis-hits stay fixed; IB5 the needs; IB6
    pairs, clashes, the mixed effect, knowledge.json's own pairs and clashes counted once; IB7 ten logged chest offers;
    IB8 the keyword fallback and `[Debug] ItemKeywords`; IB9 the revive need, the 10-07 Deck chest rebuilt (IB9b) and the
    `[ctx]` tail.
  - `HeldCases.cs`, `=== 0.16.0: the held-item rules`: H0 - H10 the table, the lines, the run, the debug key, the rails,
    the WHY band's order, nothing held changes nothing, the sources; MA1 - MA5 Mana Potion held; MK1 - MK4 the Mana
    Potion card; LS1 - LS7 Life Savings (the generalised skip worth - 19,250 old calls pinned to 0.15.0's formula -, the
    hint, the cash items, Liberate's words, the keyword reading, the `[held]` part); RB1 - RB3 Reserve Bench; HS1 - HS4
    Hijacked Signal (Liberate's score and the crossover 0.645 / 0.814, the PLAN row, the chest's cash, the words); SR1 -
    SR4 A Cookie / Skip Rope; H12 the rails and sources of those; WS1 - WS6 Wooden Stick / Empty Chest; LU1 - LU4 Last
    Unicorn's count and the stated pairs; CV1 - CV6 the status conversions. Full bench only: H11 (the animal list and the
    table's assets against `data\probe.json`), LU5 (every animal item of the probe pairs with a held Last Unicorn), CV7
    (the status items' assets and the six conversion texts against the probe).
  - `HintCases.RunPure`, `=== 0.16.0: the action hints' pure cases`: H3 the revive guard (the 10-07 Deck chest on its
    logged scores - silent at 55 % / with the quest, REROLL as live without them -, the replaced chest, the weak and the
    all-AVOID SKIPs held back, the no-item quest's SKIP kept, BANISH never on a revive, a new Resuscitation on an early
    level-up, the held-item SKIPs held back, the sources); H4 the short words of every hint, the line widths on the
    hint's model and the sources.
  - `RankSmallCases.cs`: stat cards by their own value at every rarity, the Research Pod's 10-tag effect by fit, the
    ties, the WHY band on an exact tie, the evolution card that closes the PLAN's preferred one.
  - `YardBadgeCases.cs`: the Training Yard's badge steering, YB0 - YB9; the 0.15 goldens are
    `tools\ItemBench\yard_015_expected.txt` (YB0b) and inline (YB0a) - regenerate only on purpose.
  - `GeometryCases.cs`: the WHY side panel's header, the near-floor passes of the band at 1280 x 800 and B3, the live
    1280 x 800 geometry with the hint line's rects (1745 / 2255 on the chest, 640 -> 1028 / 1348 on the rescue screen, the
    stacking arithmetic, the PC unchanged, no block over the line in any layout).
  - `LoadoutCases.cs` L6c: SELECT LOADOUT's second summary row joined to the EQUIP row at 1280 x 800.
  - `ReviewCases.cs`, `=== 0.16.0: the test walks`: the pause walk's recruit key (`PauseRecruit.Parse`, called -
    `Menu.Walk016.cs` is linked into the bench with `YAZS_BENCH` defined, which leaves its game side out) and the results
    flow's step-2 hooks.
  - `LogCheck.cs` L11 - L15: the `items` check and the proof report of `--check-log`.

Since 0.15.0 every line the bench prints is watched: its last line reads `bench: all as wanted` (exit 0) or how many lines
are not as wanted (exit 1, or 3 when a section counted them; 2 when the game data is missing). `--strict` is the release
gate (the game data and the built DLL required), `--no-data` the part that needs no game data (the GitHub workflow
`.github/workflows/bench.yml` runs it on every push) - since 0.16.0 also the card text size and WHY panel cases
(GeometryCases) and the pure parts of every 0.16.0 section (the item book, the held-item rules, the stat cards and ties,
the Training Yard's badges, the revive guard, the badge summary row); no pure case reads `data\probe.json` or
`data\gamedata.json`, and a test log quoted in a case is written `Lf(@"...")`, so a CRLF checkout reads it the same. A
bench log is the same bytes on every run of one tree: the Training Yard's cost line (YardBadgeCases YB8) prints its
milliseconds only with `--timings`. The frozen public API is checked against `tools/ItemBench/api_v3.txt` (`--api-surface` prints the built DLL's, the start of a
new file). Two tools read a log:

- `tools\bench.cmd --check-log <companion.log> [--since last|all|<session stamp>] [--wide on|off] [--expect kind,kind]` -
  PASS or FAIL per check: every session's hooks line shows 0 patch classes failed, the WHY band's hooks all took, the
  game's frame was hidden at least once on a screen wider or taller than 16:9 while WideMenus is on, no `[Warning]` and
  no `[Error]` line, every `[shown]` card text within the writing rules above (with the room it had beside its `2ND` /
  `AVOID`), and (0.16.0, `items`) every chest offer with its `[items] held:` line and no `[items] no rule for` line naming
  an item. The newest session by default; checks a session's build did not have yet are skipped for it (`items` also
  checks a session whose load line still names 0.15.0 when it holds an `[items]` or `[held]` line - a build of the 0.16.0
  tree before the version bump). After the checks
  come INFO sections, which are not checks: the proofs the log holds, each kind as `seen N, first at HH:MM:SS: <source or
  line>` or `not in this log`. The real-session proofs are `first-input`, `yard`, `equip` (with how EQUIP ADVICE / UNDO
  was pressed: `touch`, `mouse` or `key`) and `ribbon`. The quest proof is `quest-story`. The walk proofs are
  `side-panel`, `results` and `recruit-tour`. `--expect kind,kind` turns those kinds into checks: a kind not in the log
  fails, and so does `equip` when no line says how it was pressed (exit 1). An unknown kind exits 2 with the list.
  Without `--expect` the exit codes are 0.15.0's.
- `python tools\advice_audit.py companion.log.1 companion.log [--since 2026-10-06] [--until HH:MM:SS] [--brief] [--json f]`
  - what the player did with the advice, per session and in total: picks and overrides by screen and by the reason head
  of the recommended card, CLOSE CALL screens, the action hints shown and whether they were acted on within 10 s, WHY
  views per pick and the bands that dropped reasons, the quest objectives met on the run clock, the level-up pace per
  game mode by phase of the run, the exact ties for the first place by the rule that settled them, and the Training Yard
  purchases against the advice shown, and badge levels bought while held (a session that only visited the Training Yard
  is reported too). Standard library only; `--self-test` checks it against a made-up log.

## Prerequisites (already done on this machine)

- The Nexus "BepInEx 6 IL2CPP Pack" (6.0.0-be.725) extracted into the game folder
  `J:\SteamLibrary\steamapps\common\Yet Another Zombie Survivors`, launched once so that
  `BepInEx\interop\*.dll` exist (the generated managed views of the game code we compile against).
- .NET SDK 8, installed user-locally (no admin) with Microsoft's `dotnet-install.ps1` into
  `%LOCALAPPDATA%\Microsoft\dotnet`. `build.cmd` puts it on `PATH` for you.

## Build, package and deploy

```bash
mod\build.cmd
```

That compiles `YazsCompanion.Mod\YazsCompanion.Mod.csproj` (net6.0, matching the .NET 6 runtime
BepInEx ships) and copies `YazsCompanionMod.dll` into `BepInEx\plugins\YazsCompanion\`.
Pass `-p:Deploy=false` to build without copying, `-p:GameDir=...` for another install.
No NuGet packages are needed: it references the BepInEx core and interop DLLs from the game folder.

```bash
mod\package.cmd
```

builds the mod and writes `mod\dist\YazsCompanion-<version>-BepInEx-<build>-SteamDeck-Windows.zip`
(about 53 MB): the exact BepInEx build from this install (core, bundled .NET 6 runtime, `winhttp.dll`,
`doorstop_config.ini`), the Unity base libraries and the interop assemblies already generated for the
current game build (so the Deck needs no download and no slow first launch), `BepInEx\config\BepInEx.cfg`
with the console window turned off, the mod DLL (it writes `knowledge.json` on its first launch), the README and
`CHANGELOG.md`, and a `README-INSTALL.txt` with the Deck steps. Entry names use forward slashes, so Linux extracts real
folders.

Install on the Deck: Desktop Mode, extract the zip into the game folder (Steam > Manage > Browse local
files), set the launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%`, use Proton Experimental or 9+,
launch. `BepInEx/LogOutput.log` proves it loaded. Verified on the user's Deck (Wine 11, SD card path
`S:\steamapps\...`): the card verdicts and the log work as on Windows.

## Publish a release

```bash
mod\release.cmd -Notes "what changed"
```

`release.ps1` first runs its gates (0.15.0), each of which stops it before anything is tagged or published: the
maintainer's name scan of the tree (where it is checked out next to this repository), a clean tree and a new tag, its
own build (`dotnet build -c Release -p:Deploy=false`, the exit code checked; the build itself never deploys), the DLL's
identity (its BepInPlugin version is `VERSION`, and the commit the build stamps into it - `0.15.0+abc1234`, also on the
load line `YAZS Companion 0.15.0 (abc1234) loaded from ...` - is HEAD's, clean), the bench with `--strict` (every check
as wanted, the frozen API included), the package (the zip holds this very DLL and no `knowledge.json`), the name scan
again over the zip's text files, the DLL's string literals and the notes, and a logged-in `gh`. Then it copies the DLL to
`dist\YazsCompanionMod-<version>.dll`, writes `dist\latest.json` (version, download URL, SHA-256, zip URL, notes, date),
tags `v<version>`, pushes, creates the GitHub release with the three assets, and copies the zip into the Drive folder.
Since 0.16.0 it then puts the released DLL on the maintainer's PC: it is copied over the deployed one when the game is
closed, no updater download waits in the plugin folder and the PC does not run a newer build (`[local] the PC now runs
the released DLL (0.16.0+abc1234; was ...)`, else `[local] <why it was left as it is>`); a failed copy is a yellow
warning after the release, never a stop. `-NoLocalDeploy` skips it, `-Draft` never does it, and `-DryRun` prints what it
would do (`local DLL: copy ... (the PC runs <version>)` or the reason to skip). `build.cmd` stays the deploy of work in
progress. Without it the PC kept a pre-release build of the same version that the updater cannot replace (it compares
versions only). Bump `VERSION` in `Plugin.cs` first; the tag and the feed come from it. `-DryRun` runs every gate and
prints the commands it would run, publishing nothing (`-AllowDirty` lets a dry run go on a tree with uncommitted
changes); `-Draft` publishes later; `-NoBuild` reuses the build only when the identity gate finds it built from HEAD.
Load-test the build in the game before releasing: a build that fails to load leaves every auto-updated install without
the mod until the next release.

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
  count`).
  The held items (0.16.0, `HeldRules.cs`): after every `[ctx]` line, while an item a held-item rule reads is held, what it
  changes now - `[held] Mana Potion: ability cooldown reduction off | Reserve Bench: ... | Wooden Stick: ...` (one part per
  item, in the table's order) - and once a run (Try Again counts: the play clock going back starts a new one) what was
  read for them: `[held] reads: item slots 3 of 8 filled, 5 empty (team statistic: 5); leader's ability cooldown
  reduction 15%; 372 s since the last survivor joined; animal items held 1; abilities with a cooldown 4 of 4 owned`
  (`?` for a value not read; the empty slots are the game's own formula, the team statistic beside it is what Wooden
  Stick and Empty Chest read). A read that fails says so once a session: `[held] <what> not read (<exception>)`. While
  `[Debug] HeldPretend` names items, every `[held]` line starts `PRETEND (debug): <names> - `, and a Warning says so once a
  session: `[held] HeldPretend is on (debug): Mana Potion - the advice treats them as held; the game is unchanged` (`| not a
  held-item rule's item: <names>` for a name no rule reads). On a card: `[card] #3      Chem-Light Battery (stat) 0.80 -
  held: does nothing while you hold Mana Potion`; the Mana Potion card ends with its numbers, `...; Mana Potion: casts
  x1.55 over 4 abilities with a cooldown` (the log only - the WHY band never shows them; four abilities of 10 / 20 / 30 /
  12 s: x1.55 with no reduction, x0.93 at 40 %, x0.62 at 60 %; one 12 s ability alone: x3.16). The other items' parts:
  `Reserve Bench: a chest or training skip is a level-up; recruiting gives 2 (4:10 since the last join)`, `Hijacked
  Signal: Liberate gives two level-ups; chest items pay cash`, `Life Savings: cash heals instead`, `A Cookie: a skip
  heals 500 more`, `Skip Rope: a skip pays 5 points (1 on a level-up)`, `Wooden Stick: 5 empty slots (+50% XP), filling
  one costs 2.17 now`, `Empty Chest: 5 empty slots (+50% damage), filling one costs 2.00` (`... (+80% XP, the cap),
  filling one costs nothing` over 8 empty, `no empty slot left`, `the empty slots not read - no slot cost`), a conversion
  item `Battery Leakage: Electric causes Toxify, not Electrify`. On the cards: a recruit `...; held: recruiting gives 2
  level-ups (Reserve Bench)` (its first reason late in the run), Liberate `held: two level-ups and cash (Hijacked Signal);
  ...` and `...; held: a recruit brings 2 level-ups now (Reserve Bench)`, a cash item `...; held: its cash heals you
  instead (Life Savings)`, `held: fills an empty slot: -10% XP (Wooden Stick)` (the first reason of every card it costs),
  `held: counts 3 animal items: +30 Luck, +15% damage`, `held: no Electrify while you hold Battery Leakage`, `held: would
  stop your Jacob's Ladder's Electrify`; a status item whose cause changed but did not die or grow: `Burn comes from your
  Ice now`.
  The chest items (0.16.0): `[items] no rule for '<name>' - keyword reading` - an item the item book does not name (a
  newer game build's) is scored by the 0.15.0 keywords; once per name and session (never for the game's unlocalized
  `Powerups/...` duplicates). `[items] bad rule tag: <row>: unknown item tag '<tag>'` - a warning, once per book row at
  the first chest of the session: a rule names a tag the item rules do not have; that tag weighs nothing (the row still
  scores). Never expected in a release (the bench's IB2 fails on it); `ItemBench --check-log`'s 'warnings' check counts
  it. On every chest offer, right after the `[ctx]` line and not behind `[Logging] LogSquad`: `[items] held: Homing
  Pigeon, Black Box | free slots 3 (max - equipped) | luck 10 | pickup 130% | speed 100`. It lists the items every
  survivor holds (a stack as `Name x2`, `none` when there are none), then the free item slots and where they came from,
  then the team's luck (points), pickup range (percent) and movement speed (points). A value not read is `?`. The item
  book's needs (Empty Chest, Wooden Stick, Jade Amulet, Metal Gear, Wrench, Compass, Toilet Paper) read the same values,
  and the line makes a chest replay exact. Once a session, at the first read in a run: `[items] item slot reads: team T
  | leader L | max M - equipped E`. These are the raw team statistic 50 (InternalNumFreeItemSlots), the leader's own
  statistic 50, and the team statistics 43 (InternalMaxItems) and 49 (InternalNumEquippedItems), with `?` where a read
  threw. Warnings, once a session: `[items] statistics not read: <why>`, or `[items] <luck | pickup range | movement
  speed | free item slots> read as V - outside A .. B, not used: the item book leaves that need out (said once a
  session)`. The plausibility gates are pickup range 0.5 - 6, movement speed 30 - 400, luck -50 - 1000, and free slots a
  whole number 0 - 16. A gate missed in a run's first 2 s gives `?` without the warning.
  The `[ctx]` line ends `..., ~N level-ups to come, health 55%, survive quest)` while the leader's health bar is under
  100 % (floored: a squad under 60 % never reads 60 %) and while the active quest asks to survive the run and is
  followed (QuestSteer On, the run fits it, not failed); at full health and with no such quest it is the 0.15.0 line. A
  revive card's first fit reason is then `a second life: the squad is at N% health` or `a second life: the quest asks to
  survive the run`.
  The ranking (0.16.0): once a session per rarity and stat, `[rank] Rare stat cards: weight x2.50 for Ability Cooldown
  (the card's own +0.25 vs Common +0.1)` (an Endless card: `[rank] Endless stat cards: weight x0.25 for Ability Size
  (...)`; `(the card's values not read: <exception> - the fallback)` when the game's lists could not be read, `;
  GetMyRarityBonusList <exception> - read from the rarity's field` when only the field could). On an exact tie for the
  first place on any screen but the rescue one, `[rank] tie for #1 at 4.81: Bombing Strike before Minefield - the
  build's order (#1 before #2)` (the rule that decided: the build's order, its own value, the stat card's own value, tag
  points, or the card further left; once per offer). An evolution card's `[card]` reasons may hold `closes <the other
  evolution> (the PLAN's preferred evolution, fit a vs b)` (its headline is unchanged).
  The WHY (0.16.0): the side panel's `[why]` line says which card of the row it speaks for (`..., 3 of 3 shown, card 2
  of 3`) and `, header in 2 rows` when the name took a row of its own; `header + N lines` counts the reasons' lines alone.
  `, the short "goes first"` (after `X of Y shown`) when the band said the other card's lead in its short form. `[why]
  SOS 02:12: #2 ... (under the buttons, 2 lines, 15 px, 3 of 3 shown, the widest stretch; ...)` - the band's widest piece
  was taken because the selected card's own (at least 600 units) could not hold the first item at 15 px; the offer's first
  `[why]` line's band notes add `; the widest stretch 1028x153 - the selected card's own 640x153 holds no first item (785
  units at 15 px)`, or `; no stretch holds the first item (N units at 15 px)` when no piece of any band could (then the
  band is laid out as before 0.16.0); near the 15 px floor `; 2 lines at 15 px[ with the tight pad (0.25 of the font)] -
  the preferred size is within 2 px of it`.
  The card frame (0.16.0), once per card class and outcome, at the 0.5 s re-measure: `[badge] plate: UIPowerupButtonSkill
  AddonDescription <w>x<h> units, y .. (the frame's bottom edge ..) - edge cut <a1>..<b0>` (or `- no overlap, edge
  whole`, `... not shown - the frame is drawn whole`, `... leaves no room either side of it on the edge ..., edge whole`).
  The object is the plate image the card's `className` label sits on (`Content/AddonDescription`, 400 x 150 at y -782 ..
  -632 of the 832 x 1462 card root, astride the frame's bottom edge -726 .. -720: the game's level2 card layout, read
  offline); the expected line on skill and rescue cards ends `400x150 units, y -782..-632 (the frame's bottom edge
  -726..-720) - edge cut -211..209`. A plate unknown, off the edge or too wide adds, once per card class, `[badge] plate dump: <class> - on the
  frame's bottom edge -726..-720: <child> <w>x<h> at x .., y ..; ...` - the card's active children reaching the edge.
  A key of ours that another plugin, the game or Steam uses as well: `[config] Advice.LoadoutEquipKey = F9 is also
  <plugin>'s [Section] Key ...` (a warning, once per value).
  At load, `[badge] card classes: Hashtag, Item, Military, SOS, Skill - all have a label template`; a card class
  the badges do not know is named in a warning (a game build older than the rescue card class says so once, and its
  rescue cards borrow their first label; the other cards are not affected). The PLAN readout: `[panel] created under
  UIGameplay using label 'GameTimer_Txt' at (44, 70) x1.45 canvas 3840x2400 screen 1280x800` shows which HUD text it
  cloned for the font, the scale and the geometry; `[panel] glyphs: ...` which of `›` / `·` the HUD font lacked (drawn
  as `»` - `>` when that is missing too - and `|`).

Once a session (0.15.0): `[menu] first input this session: pad` (`mouse`, `key`, `touch`); `[badge] ribbon stepped aside
for the Skill Tree label (ribbon alpha ...)`; `[badge] a reason line shrank to fit its card: 16.0 -> 15.2 px (...)`; on the
main menu `[data] all N names of the Companion's tables are in this game build (...)`, or one warning naming what is
missing. A Training Yard node bought or refunded between two reads of its tab: `[yard] bought <node> 1>2 (advice #1)` (or
`not advised`), `[yard] refunded <node> 2>1`. Wide menus put back: `[wide] <site> restored (<why>): N changes undone, M
frame pieces back`.

**The hints.** The rescue screen's reroll hint, one line per judgement with the scores: `[squad] reroll hint: SHOWN -
Tank would rate higher (5.93 vs Huntress 4.64, +1.29) | rerolls 3 (the team's Rerolls available) | on the cards:
Huntress 4.64, Ghost 2.98, Liberate 1.00 | could still come: Tank 5.93, Engineer 4.03, Ranger 3.78 | margin 0.75,
recruit value 1.00`, or `not shown - the best survivor is on the cards ...` / `... only 0.60 above the cards - under the
0.75 margin` / `no reroll left (0 ...)`; then where it was drawn (`[squad] reroll hint: drawn over the Reroll button -
... band N units ...`). The other screens' hints, one line per judgement: `[squad] action hint: Chest 03:09: SHOWN
REROLL - best Detective's Pipe 1.98 under the chest floor 2.50 | rerolls 8 (the screen's count) | skip +120 cash, +40
health (worth 0.15) | actions Reroll, Skip | 'the best card here is weak for this squad'`, or `not shown - best ... at or
over the chest floor 2.50`; the banishes: `[pick] Chest 03:09: the Banish button pressed (4 banishes left)`, `[pick]
Chest 03:09: banish on Golden Key (#4, 1.19)` (the next `[offer]` line says `replaced (banish)` when it took). 0.16.0:
after `(worth X)` the skip part names what held items add (`ScreenCall.SkipNote`): ` (Life Savings: the cash heals)`,
`, a level-up (Reserve Bench)`, `, the cash also on a pick (Hijacked Signal)`, `, with A Cookie and Skip Rope` - e.g.
`skip +675 cash, +292 health (worth 2.66), a level-up (Reserve Bench)`. A REROLL with Reserve Bench held ends `| Reserve
Bench: if it stays weak, the skip is a level-up`; its SKIP `| Reserve Bench: the skip is a level-up, worth 2.66 (+675
cash, +292 health)`; the weak-cards SKIP adds `| A Cookie: +500 health` / `| Skip Rope: +5 points (a quarter of a
Military Training) worth 1.39`. With Wooden Stick or Empty Chest held both numbers show: `best Emerald Gem 0.35 (2.52
before the held slot cost 2.17) at or over the chest floor 2.50 | every card is under 1 (AVOID) (merit 2.52 before the
held slot cost 2.17); skip bonus +675 cash, +292 health`. The revive guard: `- held back: <card> is a second life (a
survive quest, squad health 55%)` where a REROLL would have shown (`not shown - best Frozen Heart 2.45 under the chest
floor 2.50 - held back: Jewel of Life is a second life (...)`), `| revive guard: <card> is a second life (...)` otherwise
(grep `held back\|revive guard`). Under its button (1280 x 800) the line says its short words: `[squad] action hint:
Chest 06:31: drawn under the Reroll button (the band over it is 63 units), short form - 484 x 74 units, font 48 (x1.20),
...`; `, short form` is absent when the words have no shorter form (BANISH, the rescue screen's quest form `the quest
needs <class>`). The line's `| '<words>'` tail keeps the long words (advice_audit.py reads them).

**The run setup screen** (`[loadout] ...`; replay them offline with `ItemBench --replay-loadout <companion.log>`, which
recomputes every logged advice and checks every drawn set against it). Per session `[loadout] inventory 27 badges, hash
237c6a7f = the 1.0.2 reference` and the inventory as JSON; per change `[loadout] visit N ...`, `shape #N`, `advise #N:
SWAT 'Rifleman' Normal d2 WinTheRun | 4 slots ... | EQUIP 1 Gunner L2 17.15, ...`, `why #N ...`, the replayable `input #N
{json}`, `hint #N`, `equipped #N: ... | swap Thunder>Gunner +17.15, ... | 8 clicks to match`, `drawn #N level A (...)`;
once per resolution `[loadout] layout ...`; per run `[loadout] run start: SWAT Normal d2 | badges ... (advice #N: k of
m)`. Robust to game patches: every game member is read through its own small accessor, each capability has a fallback (the
grid buttons instead of the registry, the slot buttons instead of the selection list ...), every `drawn` line names its
level (A everything / B no WHY line / C summary only / D log only / E nothing), and 3 errors in one visit switch the
feature off for the session with one line. At load: `[loadout] hooks: ...`, `[loadout] game members: 74 of 74 readable`
(73 of 73 in 0.15.0; 0.16.0 reads `SkillTreeUpgradeBadgeBoost.targetBadge`), `[loadout] badge classes: ... - all
readable`. 0.16.0: the EQUIP ADVICE plate's start line names how it was pressed: `[loadout] equip: EQUIP ADVICE (advice
#1) pressed by touch - remove Tough, add Gunner = 2 clicks ...` or `UNDO pressed by mouse - ...` (`touch` / `mouse` is the
mod menu's own test, Input.touchCount; `key` is `[Advice] LoadoutEquipKey`); at 1280 x 800 the drawn line says `swap row
joined to row 1 (form 2: the ranks left to the markers; ...)` and `marker 80 u (number 15 px)`.

**The test walks** (0.16.0, debug only): `[menu] pause walk: PreviewPauseRecruit <Auto|Class> - one hero joins 3 s into
the run`, then `[menu] pause walk: recruit <Class> joined at 3.0 s (Unlock Character <Class> Powerup, OnApply(false)) -
squad 2: the level-ups offer four cards`; refusals `[menu] pause walk: recruit not applied - ...`, each one Warning (no
such powerup; OnApply(false) threw; squad still 1 a second later; the class leads the run; no run to join) - "the squad
has N heroes already" is an Info line. The results flow: `[shot] results: state 1 continued +X s after step 1`, `[shot]
results step 2 set up (WxH, via <hook>[, +N s after step 1][, state 1 gone | , state 1 still up N s after its continue])
- captures at 0.8 and 1.6 s`, the fallback form `... via OnState1Continue: no state-2 OnEnable 1.5 s after it) - captures
now and at 0.8 s`; `[shot] results: state 2 enabled with the flow ... - waiting for state 1's continue`.

## Debug switches

In `BepInEx\config\bidoi.yazs.companion.cfg`, all off by default.

- Set `Screenshots = true` under `[Debug]` in the config to have the game save a PNG of its own frame
  (`BepInEx\plugins\YazsCompanion\shots\HHmmss_fff_<label>.png`, logged as `[shot] ...`) at the moments that matter
  for judging the UI: each offer with its badges (`offer`), each pick (`pick`), the sidebar 0.3 / 1.5 / 3.5 s into
  a change highlight (`hl1`..`hl3`), every show / hide / creation of the sidebar, one a minute during play
  (`base`), and the run's results flow: its first step (`results1`, `results1b`) and its stats step (`results2`,
  `results2b`). Game 1.0.2 never calls `UIDefeatState2.Setup()`, so since 0.16.0 the stats step is taken when state 2's
  own `OnEnable` fires (held while state 1 is still up, 3 s at most), else 1.5 s after state 1's continue; the `[shot]
  results step 2 set up (WxH, via OnEnable | Setup | OnState1Continue, ...)` line names the path, and a state 2 enabled
  together with the flow logs `[shot] results: state 2 enabled with the flow ... - waiting for state 1's continue` and
  waits for that fallback. At most 90 per session; a 3440x1440 frame is about 4 MB, a Deck frame about 1 MB. Off by
  default; delete the folder afterwards.
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
- `[Debug] PreviewMenu` walks the menu on the main menu and photographs every screen (with `PreviewResolution =
  1280x800` in a real Deck-sized window), the BADGES page (`menu_badges0..2`) and the MODS tab while there is one;
  `[Debug] PreviewPause` starts a Quick Run (clicking through the run wizard when it comes up), plays thirty seconds,
  pauses, opens the menu over the pause menu, changes the readout's detail and size through the DISPLAY tab's own
  controls, closes it, resumes so the log and a screenshot show the readout coming back with those settings, and puts
  the settings back - well under fifty seconds of play, so no save is written. The menu's artwork (the crest, a 5 x 5
  atlas of glyphs, a chamfered 9-slice panel, the focus glow and the backdrop) is generated by `art/make_art.py`
  (Pillow + numpy, supersampled) into `YazsCompanion.Mod/Art`, embedded in the DLL and decoded by `Art.cs`; portraits
  and weapon / ability icons are the game's own sprites.
- `PreviewPauseRecruit = Auto` (or a class name; Ghost or Ninja for the Ghost) with `PreviewPause = true` (0.16.0): 3 s
  into the walk's Quick Run one hero joins the squad the way a rescue adds one (the game's Unlock Character <Class>
  Powerup, without the rescue bonuses), so the level-ups offer four cards and the hover tour reaches cards 3 and 4
  (`[menu] pause walk: recruit <Class> joined at ...`). Auto picks the first of Medic, Tank, Pyro, Engineer, Huntress,
  SWAT, Mechanic, Ranger, Ghost that is not the leader. Only one hero, and only while the leader is alone
  (`gamePlayers.Count == 1`), once a session: the game unlocks an achievement when a SECOND recruit joins (playerIndex
  2), and the walk never adds one. A class that leads the run, or a name that is no class, is said in one Warning line
  and nothing joins. Set without PreviewPause it does nothing (`[menu] PreviewPauseRecruit is set but PreviewPause is
  off - nothing happens` at load). For scripted test runs; empty by default.
  The walk's squad stands still, so it picks up only the XP gems that fall inside its pickup ring; with the recruit's
  second gun the zombies fall short of it (the first 0.16.0 series: 0 of 100 XP at 0:42 in both walks). When no offer has
  come by 20 s of play with the recruit (36 s without: before the walk's 40 s pause), the walk grants its first level-up
  with the game's `ExperienceProgress.Debug_LevelUp()` - once a session, never while a screen is up, under its 41 s bound
  (`PauseRecruit.PlayBound` = 50 s, after which a killed run leaves a save, less ~3 s of play to the done line and a
  script's ~6 s to its kill; the walk's wait for an offer after resuming uses the same bound):
  `[menu] pause walk: no offer by 20.0 s of play (XP 0 of 100, level 1, squad 2 standing still; N XP gem(s) on the
  ground, the nearest X m from the squad's centre, the pickup ring Y m (0 inside it)) - the first level-up granted ...`,
  1.5 s later `[menu] pause walk: level-up granted - level 1 -> 2, ...` (or, when the level did not rise,
  `ExperienceProgress.LevelUp()` direct once: the screen without the level counter).
- `[Debug] PreviewYard = true` opens the Training Yard from the main menu about 6 s after launch
  (`UIViewMainMenu.OnClickUpgrades()`), walks the nine tabs with the game's own tab change (`ChangeTabIdx(1)`),
  saves `yard1_general` .. `yard9_mechanic` and `yard10_highlight` (the cursor moved onto a node), and on the Medic tab
  `yard7b_medic_badge` (its held badge under the cursor: the WHY row says why it waits), into the shots folder and goes
  back. With `PreviewResolution = 1280x800` the walk runs in a window of that size - the real Deck layout and pixels -
  and restores the display mode afterwards (`[preview] yard: display back to ...`).
- `[Debug] PreviewSetup` walks the start flow to the run setup screen (Play, team leader, arena, mode), photographs it
  (`fxsetup0..3`, `setup0_loadout`), moves the game's cursor over the first advised badge, an equipped one the advice
  would swap out and a locked one (`setup1_why`, `setup2_why_swap`, `setup3_why_locked`) - it never clicks a badge -
  then presses the difficulty and START and logs `[loadout] measure: ...` 3 s into the run (`PreviewSetupRun = false`
  backs out of the start flow instead).
- Set `ItemKeywords = true` under `[Debug]` (0.16.0) to score every chest item by the 0.15.0 keyword reading instead of
  the item book, for comparison (the `[card]` lines of a chest then read like 0.15.0: the 'damage' reasons are back).
  Off by default; it applies at once (no restart).
- `HeldPretend = Mana Potion, Reserve Bench` under `[Debug]` (0.16.0): the game's English item names, comma-separated, that
  the ADVICE treats as held - the held-item rules switch on as if you held them (the items' pairs too); the game is
  unchanged, and the `[squad]` line and the "already held" check stay with the real items. A Warning once a session and
  `PRETEND (debug): ...` at the start of every `[held]` line keep a leftover visible. For proving a held-item rule in a
  scripted run (no live session ever held Mana Potion, Reserve Bench, Wooden Stick, Hijacked Signal, Life Savings, Empty
  Chest, A Cookie or Gold Medal). Empty by default.

Motion's fail-safes: with `Motion = false`, or when no tween clock has ticked in the last half second, every element is
put straight into its end pose (nothing can wait, hidden, for a tween that will not run); a step that throws ends its
tween in the end pose. The previews capture the entrances frame by frame (`fxyard0..7`, `fxplan0..4`, `fxpulse0..3`;
captures whose label starts with `fx` may be 50 ms apart).

`[Debug] Perf = true` measures it on a real run (and a debug walk logs the sums so far once more just before its done
line - `Perf.ReportNow`, since the Training Yard and run setup walks end before the first minute): once a minute a line
of the shape `[perf] 60 s, <frames> frames:
tick.hud <calls>x <total> ms (max <worst>) | tick.master ... | refresh ... | read ... | plan.build ... | offer ... |
plan.ahead ...` - calls, total and the worst single call per section (sections nest: a tick contains the refresh it
triggered). `offer` and `plan.ahead` run while the game is paused; `tick.*`, `refresh`, `read`, `plan.build` and
`panel.create` (the readout's widget being made: once a run, and once after each use of the mod menu) are what play
pays; `yard.read` / `yard.advise` / `yard.marks` / `yard.strip` split the Training Yard's tick (a menu); `yard.badges`
(the badge demand of a tab, every recompute) holds `yard.badges.compute` (the nine badge advices, once per key);
`loadout.draw` (the run setup screen's draw) holds `loadout.draw.first` (a visit's first draw, with its parts
`loadout.draw.measure` / `.layout` / `.marks` / `.summary` / `.why` / `.equip`). How 0.10.1 and 0.10.2 got there, with
the measurements before and after, is in the CHANGELOG.

## What the mod costs, measured

The rule is in the README. From a real run on the PC (3440x1440, 60 fps; Normal to 19:49, three survivors at the end,
74 offers of all five kinds, 110 minutes of session, no warnings) and the 0.10.2 measurements:

| what | cost |
|---|---|
| a frame in play (`tick.hud` / `tick.master`) | 0.008 / 0.009 ms on average |
| the two-second change poll (`refresh`) | 0.21 ms |
| the plan after a pick | worked out while paused: `plan.ahead` 2.2 ms on average (worst 52 ms); 4 of 74 built in play, 4.9 ms |
| an offer ranked (paused) | 5.2 ms on average, worst 25 ms |
| the readout coming back after the mod menu | 4 - 5 ms, no search |
| the session's first readout and plan | `panel.create` 27 ms + the first plan 30 ms, at 00:00 of the first run |

Outside the mod: BepInEx's console window (`[Logging.Console] Enabled = true` in `BepInEx.cfg`, as on the
development PC) makes every log line of every plugin and of the game a synchronous console write; the packaged
zip ships with it off.

## The Training Yard plan

`TreeState.cs` reads the tab on screen (`currentContainer._columns[].nodes[].attachedSkillTreeUpgrade`; kind from the
upgrade's class; the points from the number the view prints, so it is right for the General tab too); `TreeUi.cs` draws
(post-fixes on `UIViewSkillTree.Update` and `OnHighlighted`; recomputed only when the tab, the points or a level changes;
logged as `[yard] Pyro: 9 points; buy 1 Blowtorch 3>4 (4), ...`). Its photo walk is `[Debug] PreviewYard` (see Debug
switches).

### Badge steering (0.16.0)

`TreeState.BadgeDemand` (live; `[Advice] YardBadges`) works out the run setup screen's badge advice (`Loadout.Recommend`,
the BADGES page's path) once for every survivor you can lead - the classes `G.Unlocked` reads, else all nine - at the mode
and difficulty of the last run setup visit (`LoadoutUi.Last`, else Normal I), the slots the game gives and the badge levels
`LoadoutState.ReadLevels` reads (no override per tab). `YardBadges.Build` (TreePlan.cs, pure) turns them into a demand per
badge node of the tab: for each survivor whose advice rates the badge (not skipped, on the grid), a level counts when the
advice equips it now (the gain over the level now, the LEVEL hint's convention: no projected tags) or when the badge at
that level would beat the survivor's weakest score pick (the difference; a free slot = 0). The unit is badge-advice points
(% squad damage over a run) per Training Yard point, averaged over the survivors counted - with one survivor unlocked a
badge it equips clears the floor about 9x more easily, by design. `knowledge.json` `yardRules`: `badgeFloor` (0.15) = the
least a planned chunk must add per point, `badgeReach` (2) = the levels a rank's stage plans at once.

`TreePlan.ClassSteps(nodes, badges)`: at each rank's stage a badge goes to the level whose points per point are best
within the reach, if they clear the floor (a WHOLE step; pinned on a build while locked: unlocked first); after the rank V
passives its further levels follow, the best per point first, while they clear the floor (WHOLE); whatever is left of
every badge is HELD. `Simulate` walks the plan twice: pass 1 the steps that are not held - a whole step is bought whole or
becomes the thing to save for, never partly, and the leftover still flows to cheaper steps further down -, pass 2 the held
steps, only once pass 1 left no open, buyable step unfinished (a held step that does not fit is saved for, so a tab with
only held badges left is never 'complete'). Without `badges` (switch off, the General tab, no badge readable) the steps
and the walk are 0.15's exactly. The approximation against a full re-advice with the badge at each level: within 0.933
points per point summed over 9 survivors, the same best level in 108 of 108 rows (bench YB5).

The nine advices are cached by a key (mode, difficulty, slots, every badge level, each survivor with its build, the
doctrine, the knowledge): every tab and every tick reads the same advices; a badge purchase changes the key, a weapon
purchase does not. RankOpen is not in the key: `Recommend` reads it only for the non-pinned UNLOCK hint, which the demand
never reads. Cost: about 1.5-1.8 ms per key change on the PC in the bench (0.5 ms per advice live), the Deck about 1.8x
slower (unmeasured live). The badge of a node: `SkillTreeUpgradeBadgeBoost.targetBadge.badgeBaseId`
(`LoadoutState.BadgeIdOf`); where that field is not readable, the badges' own tree nodes
(`skillTreeRequirement.GetSaveFileKey()`) are matched to the node's save key - never a name or the key's text: in 1.0.2
two save keys name another badge (`SkillTree_Huntress_BadgeBoost_3_Glacier` is the Physical Badge,
`SkillTree_Pyro_BadgeBoost_4_Powerup` the Elemental Badge).

Log lines: `[yard] badges of <tree> (Normal I, 9 survivors, floor 0.15, reach 2): Growth Badge 1>3 stage 9/9 0.41/pt;
...; Healing Badge held (from level 4)` once per change per tree; `[yard] badge advice for 9 survivors (Normal I): 4.8 ms`
once per key; `[yard] badge steering off: no badge readable here - ...` (Info) and `[yard] badge steering off for this
read: <message> - ...` (Warning), each once a session; `[yard] <badge>: the tab reads level a, the badge advice b` once a
session per badge. The run setup screen's TRAINING YARD row (`LoadoutView`, LoadoutHint = Full) stays the leader's own and
can differ.

## Calling the extension API from another plugin

The README's *Extensions* describes the calls. A plugin calls them through reflection, without a reference to this
DLL (a plugin that referenced it would not load where the Companion is missing), and declares the soft dependency so
that the Companion's assembly is loaded before its `Load()` looks for it (moved here from the README in 0.16.0):

```csharp
[BepInPlugin("my.mod", "My Mod", "1.0.0")]
[BepInDependency("bidoi.yazs.companion", BepInDependency.DependencyFlags.SoftDependency)]   // load after it, when it is there
public class MyMod : BasePlugin
{
    static Type _companion;
    bool Companion(string method, params object[] args)
    {
        try
        {
            if (_companion == null)
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    if ((_companion = asm.GetType("YazsCompanion.Api.Extensions", false)) != null) break;
            if (_companion == null) return false;                   // no Companion, or one older than 0.12.0
            _companion.GetMethod(method).Invoke(null, args);
            return true;
        }
        catch (Exception e) { Log.LogWarning("YAZS Companion: " + e.Message); return false; }
    }

    public override void Load()
    {
        var style = Config.Bind("Ghost", "BladeStyle", 0, "0 = Classic, 1 = Wind");
        Companion("RegisterOption", "My Mod", "Ghost", "Blade style", "Which moveset the blade uses.",
            (Func<string[]>)(() => new[] { "Classic", "Wind" }),
            (Func<int>)(() => style.Value),
            (Action<int>)(i => style.Value = i));                   // BepInEx saves inside the setter
        string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        Companion("RegisterBuildProvider", "My Mod",
            (Func<string, string>)(survivor => survivor == "Ghost" ? Path.Combine(dir, "ghost-builds.json") : null));
        // ApiVersion 2 and later: what the Companion shows for the class and its blade (null = the game's own name)
        Companion("RegisterDisplayNames", "My Mod", (Func<string, string, string>)((kind, key) =>
            kind == "class" && key == "Ninja" ? "Shade" : kind == "powerup" && key == "KatanaUpgrade" ? "Moonblade" : null));
    }
}
```

Check `ApiVersion` (a static property: `_companion.GetProperty("ApiVersion").GetValue(null)`) before calling
`RegisterDisplayNames` - a 0.12.0 Companion has no such method (the helper above would log a warning and return false).

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
`GameplayMaster.hashtagSystem` (`GetNumType`, `NumRequiredForSpecial`). Since 0.16.0 a snapshot also reads the items
every survivor holds (by name and asset), the team statistics 43 / 49 / 50 (the free item slots: the game's
`GamePlayer.AddItem` keeps statistic 50 = 43 - 49), the team's luck, pickup range and movement speed, the leader's ability
cooldown reduction, `RunStats.lastCharacterSpawnTime` and the abilities' cooldowns - once a snapshot, only in a run. The
results flow's stats step is hooked on `UIDefeatState2.OnEnable` and `UIDefeat.OnState1Continue` (1.0.2 never calls
`UIDefeatState2.Setup()`; never `Awake`, a body the compiler shares with thousands of methods).

The sidebar ticks from a post-fix on `UIGameplay.Update` (the HUD object that owns the gameplay canvas, confirmed in the
scene file `level2`: a GameObject named `UIGameplay` with a Canvas, four `Ability_0n` slots per survivor), rebuilds only
when the squad state changes (a cheap key of the squad text, the active quest and the tag points; a rebuild walks every
tree node and item) and forgets its objects on `UIGameplay.OnDestroy`. The `›` and `·` glyphs are checked against the HUD
font at creation and replaced by `»` (`>` when that is missing too) and `|` when missing. The game's own auto-pick lives
in `GameOptions.AutoselectionModes` (Off / On / Skip / Liberate per category); the mod never touches it.

The card verdicts' placement comes from the card prefab read out of the game's asset files: every card root is 832 x
1462 canvas units on a 3840 x 2160 canvas, the game's ribbon overhangs the bottom edge, and about 200 canvas units lie
below the card before the divider line. Everything is parented to the card root, so it rises with a hovered card and
vanishes with the screen.

SELECT LOADOUT's summary (0.16.0): where the band holds one row (1280 x 800), the second summary row joins the EQUIP row in
the first form that fits at the 15 px floor (`LoadoutView.JoinForms`: as is, the EQUIP row without its ranks, the second row
under its short label), else the drawn line says it was dropped; the marker digits are `max(0.52, min(15 px, 0.60 x the
diamond))` of the diamond (`LoadoutLayout`, `Ui.Marker`'s numberShare; the menu's BADGES page keeps 0.52).

The reroll count the hints trust is the game's own for that screen: the number it hands the screen's action buttons,
else the team's `Rerolls available` (the very number the game is about to hand over - it fills the cards before it
refreshes the buttons), else the number on the Reroll button (until that refresh it still shows what it showed last
time); a FREE reroll counts too. When the game's own count comes and differs from the one the hint was judged with (or
the FREE label changed with it), the hint is judged again once (`[squad] reroll hint: (re-judged with rerolls 8 - ...)
SHOWN - ...`).

`Fx.cs` is a small tween runner (ticked once per frame from `GameMaster.Update`, `UIGameplay.Update` and
`UIViewSkillTree.Update`; every tween has its own clock advanced by the frame time capped at 50 ms, so a hitch delays an
entrance instead of swallowing it; unscaled, so it runs on paused selection screens). The auto-updater's downloaded DLL
waits next to the running one because BepInEx loads only the highest `[BepInPlugin]` version of a GUID and skips the
others ("because a newer version exists"); the restart strip (`Notice.cs`) lives on its own overlay canvas that
survives scene changes, ticked from `GameMaster.Update`; the feed's notes are written for the new version when it is
downloaded (or found downloaded) and for the running version at a check that finds it the latest, and older versions'
notes are removed (`[update] release notes of 0.15.0 kept in notes_0.15.0.txt`).

Every patch is a post-fix that only reads state, so a game update that renames a member breaks the build
(compile error), not the game.

## Layout

```
mod/                              (the GitHub repository bidoingg/YazsCompanion is this folder)
  build.cmd                       build + deploy
  package.cmd / package.ps1       Steam Deck / Windows zip with BepInEx bundled
  release.cmd / release.ps1       the gates (-DryRun), then tag + GitHub release + latest.json for the auto-updater + Drive copy,
                                  then the released DLL on the maintainer's PC (0.16.0; -NoLocalDeploy skips it)
  tools/bench.cmd, tools/ItemBench/   offline bench: preset validation, clock / mode / evolution / branch scenarios, item scores;
                                  --check-log (a log's health and proofs), the frozen API (api_v3.txt), the verdict line;
                                  itembook_expected.txt and yard_015_expected.txt, the 0.16.0 parity tables
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
                                  the stat card weights, the class rank a run keeps, a card's reason prefix, the card ties (pure)
    Builds.cs / BuildPresets.cs   the build model, the nine kits, builds.json, build packs lent by other mods; the presets and where each comes from (pure)
    Knowledge.cs                  guide-derived tiers, item pairs and clashes, scaling items, stat weights, the yard's badge rules; knowledge.json
    ItemBook.cs                   the item book: one rule per chest item from its 1.0.2 text (pure, 0.16.0)
    ItemRules.cs                  the pure item score: the book's rule (or the keyword fallback), exact squad fit, the clock, live tag points, pairs and clashes, build leanings
    HeldRules.cs                  what the items the squad holds change: the held-item table, the rules' numbers and words, the
                                  [held] lines, the debug key's Warning (pure, 0.16.0)
    HeldRead.cs                   reads the held-item facts once a snapshot: names and assets, the item slots, the leader's
                                  cooldown reduction, the time since the last join, the abilities' cooldowns (0.16.0)
    ItemStats.cs                  the item book's live needs (free item slots, luck, pickup range, speed), read once per snapshot from the
                                  team statistics, and the [items] held: / [items] item slot reads: lines (0.16.0)
    Tags.cs                       damage type tag profile of the squad and the Research Pod card score (pure)
    Menu.cs                       the mod menu: builds, editor, advice, display, other mods' options; its own focus and input; the COMPANION buttons
    Menu.Walk016.cs               the pause walk's recruit ([Debug] PreviewPauseRecruit; its key parser is pure, 0.16.0)
    Api/Extensions.cs             the one public class: other mods register menu options, lend build packs and display names (reflection-friendly)
    Names.cs                      the names drawn for classes / powerups / items: lent by another mod, else the game's; the rules never see them
    Art.cs + Art/                 the embedded artwork (crest, glyph atlas, 9-slice panel, glow, backdrop) as sprites
    Probe.cs                      [Debug] Probe: dumps items, powerups, input actions and the menu layout to probe.json
    Perf.cs                       [Debug] Perf: times the mod's own sections and logs the sums once a minute
    Warmup.cs                     two throw-away plans on the main menu, so a session's first plan is not built cold in the run
    Fx.cs                         motion: the tween runner and the effects (stamp-in, ping, rule draw, type-on, spin, glint)
    Ui.cs                         the shared look (Theme: the game's gold, panel body, hairlines) and uGUI primitives
    Badge.cs                      gold frame on the game's selection rect, RECOMMENDED ribbon (it yields to the game's Skill Tree label), reason line;
                                  its bottom edge cut round the card's TIER / NEW / RECRUIT plate (Cut, 0.16.0)
    Wording.cs                    the cards' reason lines in plain words, built from the verdicts; the glyphs the card font has; the rails (pure, 0.14.0)
    RerollHint.cs                 the action hints: the rescue screen's reroll hint (who could still come, 0.12.2) and REROLL / SKIP / BANISH
                                  on every other screen (0.14.0); the game's reroll count, frame + line, the banishes logged;
                                  the short words under the button (0.16.0)
    ScreenCall.cs                 the action hints' verdict per screen: the floors, the skip bonus's worth, banish protection, the revive
                                  guard, the held items' skip extras; each hint's short words (Short, 0.16.0) (pure, 0.14.0)
    WhyText.cs                    the WHY band's words: more reasons in plain words, the "vs #1" sentence, CLOSE CALL, the packing;
                                  two cards' equal lines told apart (pure, 0.14.0)
    ScreenBand.cs                 the band manager: where the hint's line and the WHY band go on the measured screen (pure, 0.14.0);
                                  the side panel's header (Header), the recommend frame's edge cut (EdgeCut), the near-floor passes of
                                  the band (Near / WantIn), a stretch must hold the WHY's first item (NeedW) - all 0.16.0
    WhyUi.cs                      the WHY band on the selection screens: the selection hooks, the measures, the plate (0.14.0)
    WideMenus.cs                  menus on wide and tall screens: the game's black frame hidden, the menus' own backdrops grown to the edges (0.14.0)
    QuestTeam.cs                  the active quest's team rules and what they mean on a rescue screen, the SOS row and the reroll hint (pure, 0.13.0)
    QuestRules.cs                 the active quest's other objectives as rules for the cards (lift, AVOID, modest) and the QUEST row (pure, 0.14.0)
    Quest.cs                      reads the active quest's objectives once a run (one accessor per member), their progress per snapshot, and logs them
    Loadout.cs                    badge advice, pure: badge facts, the run shape, the score, the greedy fill, reasons, the replayable input (0.13.0)
    LoadoutView.cs                badge advice, pure: the diamonds per badge, swaps and the keep margin, summary rows, WHY texts, the EQUIP plan;
                                  at 1280 x 800 the second summary row joined to the EQUIP row in the first form that fits (JoinForms, 0.16.0)
    LoadoutLayout.cs              badge advice, pure: where the WHY line and the summary go on the measured screen, text and diamond sizes
    LoadoutState.cs               badge advice: the game reader (one accessor per member, a fallback per capability, the load-time checks)
    LoadoutUi.cs                  badge advice on the run setup screen: diamonds, frames, WHY line, summary, motion, logs, hooks, the debug stage
    LoadoutEquip.cs               the optional one-click EQUIP ADVICE button (off by default): guarded presses of the game's badge button, UNDO
    Menu.Badges.cs                the mod menu's BADGES page, the BUILDS cards' badge sentence, the editor's Badges row
    Plan.cs                       the run plan, Compact or Full (keyed rows with label / value / group; sample plans for the preview)
    Panel.cs                      the PLAN readout during play (soft backing, corner placement, text-hugging width, fade, idle dimming, highlight)
    TreePlan.cs                   Training Yard advice, pure: node model, the General order, the survivor rules, simulate / advise;
                                  the badge steering (YardBadges, 0.16.0)
    TreeState.cs                  reads the Training Yard tab on screen into TNode values; the badge demand (BadgeDemand, 0.16.0)
    TreeUi.cs                     the order diamonds on the nodes and the SPEND / THEN / WHY strip under the tree
    Preview.cs                    the design previews (config [Debug] Preview / PreviewYard / PreviewResolution)
    Updater.cs                    release-feed check, hash-verified download next to the running DLL, old-build cleanup
    Notice.cs                     the "restart to apply" strip on its own overlay canvas
    Shots.cs                      debug screenshots of the game frame at UI moments (config [Debug] Screenshots)
    Describe.cs                   verbose raw-field dump (config Verbose)
```
