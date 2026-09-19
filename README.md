# YAZS Companion — in-game mod (BepInEx 6 IL2CPP)

The mod version of the companion: it runs inside *Yet Another Zombie Survivors*, reads every
selection screen, the squad and the run clock straight from the game's objects, ranks the offered
cards with a C# port of the PC app's rules (`lib/engine.js` → `Ranker.cs`) and draws the verdict
above each card. No OCR, no overlay, no save-file polling. It never writes to the game's saves
and never picks for you.

Status (2026-09-18): **0.9.0 — the motion pass**, see "Motion" below: verified frame by frame on the Training Yard and for the
PLAN readout's entrance and change cue; the card badges' entrance uses the same primitives but has not been seen on a
real selection screen yet. **0.8.0 — Training Yard advice.** On "Train your survivors" the mod numbers the nodes worth
buying with the points on hand (gold diamonds, in purchase order), rings the node to save for next, and prints a
SPEND / THEN / WHY strip under the tree; see "Training Yard advice" below. Checked on all nine tabs at 3440x1440
and in a real 1280x800 window (the Steam Deck's layout and pixels) by a preview walk that opens the Training Yard
from code. **0.7.0 — the PLAN readout gets out of the way.** The user's verdict on 0.6.0: polished, but
"over-opaque/sized - it's blocking essential awareness of surroundings". So the framed, nearly opaque 800-unit
panel at the right edge became a HUD readout in the style of the game's quest tracker: a small gold title over a
fading rule, the rows on a soft dark wash (42 % at most) that dissolves towards the middle of the screen, no
frame; it sits in the empty bottom-left corner under the weapon / ability icons, is only as wide as its text,
shows one row per survivor (`PanelDetail = Compact`), and dims to 70 % when nothing has changed for a few seconds.
Checked with the preview mode over real gameplay frames at the PC's and the Deck's pixels (new: a backdrop image
behind the preview): on the Deck a full squad covers about 22 % x 21 % of the screen, see-through, where 0.6.0
covered up to 30 % x 65 %, opaque. `PanelPosition = Right` and `PanelDetail = Full` bring the old place and the
two-rows-per-survivor plan back. **0.6.0 (2026-09-16) — the design pass: the PLAN sidebar is a framed panel in the game's own style (body,
gold hairline, corner diamonds, header band, label column, hairlines between groups, fade in/out), the card
badges and the restart strip share its palette and primitives (`Ui.cs`), and a preview mode renders the sidebar
on the main menu with sample plans so the look can be checked from screenshots without a run - verified this
way at the PC's and the Deck's pixel sizes. 0.5.5:** card verdicts and the PLAN sidebar working on PC and Steam Deck; auto-update
validated on both.** Deck screenshots of 0.5.2 confirmed the sidebar in play at the enlarged size, gone on the pause
menu, and the scaled badges under the cards; 0.5.3 keeps a long plan above the minimap by shrinking the block (down
to 0.75x) and widens it to 700 units; 0.5.4 makes the plan compact (two lines per survivor, nine lines for a full
squad, see below) and highlights the lines whose advice changed after a pick. A PC run of 0.5.4 captured with the
new debug screenshots (below) confirmed the highlight timing (gold at 0.3 s, mid-fade at 1.5 s, white by 3.5 s),
the gating on the pause menu and the pick transition, and the PC geometry (canvas 5161x2160 at 3440x1440, scale
1.00, the font lacks "›" so ">" is used); 0.5.5 stops the ability line from wrapping (evolution names only from one
level below max) and ships the screenshot flag. The sidebar had appeared only on the results screen in 0.4.1 because it was gated on
`GameplayMaster.IsGameplayUIVisible()`, which the Deck log of 0.5.0 proved to mean "a UI view is showing"
(false during play; true on the pause menu, the selection screens and the results): the diagnostic builds
0.4.2–0.5.1 logged every candidate flag (`[panel] players=1 active=True paused=False pauseMenu=False
defeat=False hudVisible=False selecting=False screen=False hud=True` is play), and 0.5.2 hides the sidebar on
any of `players == 0`, an open selection screen (our tracker or `UIGameplay.IsDisplayingUpgradeSelection()`),
`GameplayMaster.IsPaused`, `IsPauseMenuFlowActive`, `IsDefeatResultsFlowActive` or `IsGameplayUIVisible()`.
The flags are still logged when they change, with `[panel] shown` / `hidden` / `created ... x1.45 canvas WxH
screen WxH`. 0.5.2 also scales the sidebar and the card badges up on small screens (the Deck's 1280x800 gets a
3840x2400 canvas, so a canvas unit is a third of a pixel there). 0.5.0 added the auto-updater and the public
repository; 0.5.1 ranked the Research Pod reward cards, scored chest items against what the squad actually
deals, added a `TAGS` line to the plan and an offline bench.

## The PLAN readout (during play)

A HUD readout in the empty bottom-left corner, under the weapon and ability icons (0.7.0; up to 0.6.0 it was a
framed panel at the right edge, which hid too much of the field). It is drawn like the game's quest tracker, not
like a menu panel: a small gold diamond and `PLAN` over a gold rule that fades out, then the rows on a soft dark
wash that is solid only under the text, dissolves towards the middle of the screen and is feathered at the top
and bottom - no frame, no box. Every row has a label column (the survivor's name in bold white, `TAGS` / `SOS` /
`GRAB` in gold) and a value column that wraps under itself, names never breaking across lines. The block is only
as wide as its text (at most 640 units in Compact, 800 in Full), fades in and out (0.25 / 0.15 s), is at full
strength for a few seconds after new advice (a pick, a recruit, coming back from a pause) and then settles at
`PanelIdle` (70 %).

```
◆ PLAN
────────────────────────────── ─ ─  ─
TANK      Pump-Action Shotgun 3/4  ·  Sawblade Drone 2/4
PYRO      Fireaxe 1/4  ·  Molotov 3/4
─────────────────────── ─ ─  ─
TAGS      Kinetic 8/10
SOS       SWAT, Huntress
GRAB      Accumulator, Bleeding Edge
```

**Compact (the default):** one row per survivor with only what to pick next. The weapon item is the level to
finish (`Pump-Action Shotgun 3/4`), or the next tier in gold once the weapon is maxed (`› Rocket Launcher`), or
nothing when the line is complete; the ability item is, in priority order, a maxed ability whose unlocked
evolution is waiting (`evolve Arrow Rain`, gold), the ability to keep feeding (`Sawblade Drone 2/4`), or the next
ability worth taking (`next Minefield`). `TAGS` shows the highest damage-type tag count and `stack X` only when
the type to stack at the next Research Pod is another one. A full squad is five or six rows.

**Full (`PanelDetail = Full`):** two rows per survivor with a hairline between the groups, as in 0.5.4 - 0.6.0:

```
TANK      Pump-Action Shotgun 3/4 › Rocket Launcher
          Sawblade Drone 2/4  ·  next  Minefield
PYRO      Fireaxe 1/4 › Blowtorch
          Molotov 3/4 › Napalm / Cocktail Party  ·  next  No Pain, No Gain
TAGS      Kinetic 8/10, Slashing 4/10
```

The weapon line shows the current weapon and its next step (gold once the weapon is maxed; `take Shotgun` for a
recruit without one; `next tier locked` / `line complete` at the end of a line). The ability line holds at most
two items in priority order: a maxed ability waiting for its unlocked evolution card (`Minefield 4/4 › Shrapnel /
Taunt`, gold), the ability to keep feeding (with its evolutions once it is one level below max and the Training Yard
unlocked them; earlier the names only made the line wrap), and the next ability worth taking. `TAGS` shows the two
highest tag point counts. A full squad is nine lines.

In both: `SOS` is the two best rescues by the SOS-card rules (hidden with a full squad); `GRAB` the two best items
worth a chest slot (S/A tier or quest target, not held). When a rebuild changes a line (a pick, a recruit, a
Research Pod), that row's value comes back gold and eases to white over `PanelHighlight` seconds (default 3,
0.8 s of it held gold; the block itself never grows or jumps for it); the log names the changed lines (`[plan] ... [changed: Tank.plan]`).
The `›` and `·` glyphs are checked against the HUD font at creation and replaced by `>` and `|` when missing
(`[panel] glyphs: ...`). It is driven by
the HUD's own `UIGameplay.Update`, rebuilds only when the squad state changes (a cheap key of the squad text,
the active quest and the tag points; a rebuild walks every tree node and item), hides while a selection screen,
the pause menu, the results screen or any other game view is up, and dies with the HUD when the run ends.

Config (`[General]`, canvas units; the canvas is 3840 wide on every screen, 2160 tall on 16:9, 2400 on the Deck's
16:10): `ShowPanel`; `PanelPosition` = `BottomLeft` (with `PanelLeft` 44 / `PanelBottom` 70; the block grows up
and to the right and stays under 62 % of the screen height, where the icon column ends) or `Right` (with
`PanelRight` 44 / `PanelTop` 780; the block grows down and to the left, stays above the minimap, and its backing
dissolves to the left); `PanelDetail` = `Compact` / `Full`; `PanelOpacity` (0.42; 0 = text only); `PanelIdle`
(0.7; 1 = never dims); `PanelHighlight`. `PanelScale` (default 0 = automatic) keeps the text at least 15 px tall:
1.0 on a desktop monitor, about 1.45 on the Deck. Every change is logged as a `[plan] 03:31: ...` line; `[panel]
created under UIGameplay using label 'GameTimer_Txt' at (44, 70) x1.45 canvas 3840x2400 screen 1280x800` shows
which HUD text it cloned for the font, the scale and the geometry.

## Training Yard advice (0.8.0)

On the Training Yard (`UIViewSkillTree`, "Train your survivors") every tab gets, read-only:

- a **gold diamond with a number** on the top-right corner of each node worth buying with the points on hand, in
  purchase order (the game's own green "can buy" diamond sits on the top-left), and a **hollow gold diamond** on the
  node to save for next;
- a strip in the empty band under the tree, between the legend and the Reset points button:
  `SPEND 9   1 Blowtorch 3>4 · 2 No pain no gain Evolutions 0>1` / `THEN   save 4 more for Rocket Launcher 3>4 ·
  after that Tank <-> Pyro, Bombing Strike` / `WHY   Blowtorch — Tier-1 weapon levels are cheap and speed up the
  start.` The WHY row follows the cursor when the node under it is part of the advice, and otherwise says where that
  node stands ("later in the plan (step 14 of 41)", "its rank is still locked", "maxed").

The plan is a port of the PC app's "Spend now" (`lib/engine.js`) without its run-history tailoring (`TreePlan.cs`,
pure): the General tab follows a fixed order (economy, then the strongest multipliers, survivability in between);
a survivor's tab follows a rule sequence - starting abilities to 2 then 3, the cheap tier-1 weapons, the evolutions
of the starting abilities, the main tier-2 weapon (the guides' branch from `knowledge.json`, else the one levelled
most), abilities maxed, the final weapon, rank III abilities, badges and synergies as their ranks open, rank V
passives by impact, then everything that is left; abilities inside a group go by the guides' tier. The walk spends
the points level by level (from level L to L + 1 costs `levelUpCosts[L]`, levels below `levelMin` are free), skips
locked ranks (the node shows its lock) and unmet prerequisites, keeps the first step that does not fit as the thing
to save for and lets leftover points go to cheaper steps further down, like the PC app. `TreeState.cs` reads the
tab on screen (`currentContainer._columns[].nodes[].attachedSkillTreeUpgrade`; kind from the upgrade's class; the
points from the number the view prints, so it is right for the General tab too); `TreeUi.cs` draws (post-fixes on
`UIViewSkillTree.Update` and `OnHighlighted`; recomputed only when the tab, the points or a level changes; logged
as `[yard] Pyro: 9 points; buy 1 Blowtorch 3>4 (4), ...`). The strip enlarges its text to 15 px on small screens
and, where the band is short (the Deck letterboxes this 16:9 view), shrinks to 13 px at most and then says less
(drops the "after that" tail, then the WHY row) rather than let the text get small. `ShowYard = false` turns it off.

`[Debug] PreviewYard = true` opens the Training Yard from the main menu about 6 s after launch
(`UIViewMainMenu.OnClickUpgrades()`), walks the nine tabs with the game's own tab change (`ChangeTabIdx(1)`),
saves `yard1_general` .. `yard9_mechanic` and `yard10_highlight` (the cursor moved onto a node) into the shots
folder and goes back. With `PreviewResolution = 1280x800` the walk runs in a window of that size - the real Deck
layout and pixels - and restores the display mode afterwards (`[preview] yard: display back to ...`).

## Motion (0.9.0)

`Fx.cs` is a small tween runner (ticked once per frame from `GameMaster.Update`, `UIGameplay.Update` and
`UIViewSkillTree.Update`; every tween has its own clock advanced by the frame time capped at 50 ms, so a hitch delays
an entrance instead of swallowing it; unscaled, so it runs on paused selection screens) plus effects made from the
game's own vocabulary:

- **Training Yard:** on a tab change the gold rule draws itself from the left and its diamond spins in, the SPEND /
  THEN / WHY rows type on (TMP `maxVisibleCharacters`, 240 characters a second), and the order diamonds stamp in one
  after the other - 2.3x to 1x with an overshoot and a half turn (the number stays upright), then a **ping**: a
  hollow gold diamond that expands and fades like a sonar ring. Diamond 1 keeps breathing and pings every 2.8 s,
  the hollow "save for" diamond glows slowly, a glint runs along the rule every 6 s. After a purchase only the
  diamonds that changed stamp again, and the rows type again only when SPEND / THEN changed.
- **PLAN readout (during play): only when it matters.** On appearing, the rule draws, the title diamond spins in
  and the groups slide in from the screen edge (0.3 s, staggered); when the advice changes, the title diamond spins
  and pings once while the changed values do their gold-to-white fade. Nothing loops over the field.
- **Cards:** the gold frame settles onto the recommended card (fade + 5 % scale), the RECOMMENDED ribbon unfolds
  from its middle with an overshoot, and its two diamond tips ping.

Fail-safes: with `Motion = false`, or when no tween clock has ticked in the last half second, every element is put
straight into its end pose (nothing can wait, hidden, for a tween that will not run); a step that throws ends its
tween in the end pose. The previews capture the entrances frame by frame (`fxyard0..7`, `fxplan0..4`,
`fxpulse0..3`; captures whose label starts with `fx` may be 50 ms apart).

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
(the file appears after the first launch). The log keeps working either way. `BadgeScale` (default 0 =
automatic) enlarges the ribbon and the reason lines on small screens (up to 1.3, so they stay inside the band
under the card; about 1.2 on the Deck, 1.0 on a desktop monitor); the frame is not scaled.

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
- Set `Screenshots = true` under `[Debug]` in the config to have the game save a PNG of its own frame
  (`BepInEx\plugins\YazsCompanion\shots\HHmmss_fff_<label>.png`, logged as `[shot] ...`) at the moments that matter
  for judging the UI: each offer with its badges (`offer`), each pick (`pick`), the sidebar 0.3 / 1.5 / 3.5 s into
  a change highlight (`hl1`..`hl3`), every show / hide / creation of the sidebar, and one a minute during play
  (`base`). At most 90 per session; a 3440x1440 frame is about 4 MB, a Deck frame about 1 MB. This is how the
  0.5.4 highlight was verified without anyone watching the screen. Off by default; delete the folder afterwards.
- Set `Preview = true` under `[Debug]` to see the sidebar without playing: about 5 s after launch, on the main
  menu, it is built on an overlay canvas with sample plans - two survivors, then a pick's change highlight
  (PNGs at 0.35 / 1.5 / 3.6 s), then a full squad, then the fade-out - and a PNG of each stage goes to the shots
  folder whatever the Screenshots setting (`[preview] ...` and `[panel] layout: ...` log lines mark the stages).
  Put a gameplay frame next to the DLL as `preview_bg_<WIDTH>x<HEIGHT>.jpg` (or `.png`; `preview_bg.jpg` without a
  target resolution) and it is drawn behind the readout at that screen's pixels, so the captures show how much of
  the field the readout hides - the menu art cannot tell. Stages since 0.7.0: `preview_a`, `preview_hl1..3`,
  `preview_squad` (full squad, compact), `preview_idle` (settled at `PanelIdle`), `preview_detail` (Full),
  `preview_fade`.
  `PreviewResolution = 3440x1440` (or `1280x800`) makes those PNGs show the sidebar with the pixels it has on that
  screen even when the game runs on another desktop: the frame is rendered at a whole multiple of the window and
  the block scaled to match, so only the menu art around it differs. This is how 0.6.0 was checked for the PC and
  the Deck from a 1024x768 remote desktop. Off by default.

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
    Fx.cs                         motion: the tween runner and the effects (stamp-in, ping, rule draw, type-on, spin, glint)
    Ui.cs                         the shared look (Theme: the game's gold, panel body, hairlines) and uGUI primitives
    Badge.cs                      gold frame on the game's selection rect, RECOMMENDED ribbon, reason line
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

## Next steps

1. The Deck has not shown the compact sidebar (0.5.4+) or the 0.6.0 panel yet: check it there with a full squad
   (the nine-row block computes to 65 % of the canvas height at x1.45, so it should end above the minimap without
   shrinking) and the highlight on a 1280x800 screen. The screenshot flag works there too (Desktop Mode to edit
   the config and to fetch the PNGs). The card badges and the restart strip were moved onto the shared palette
   without a visual check (same geometry as 0.5.5): glance at them on the next offer / update.
2. Validate the Research Pod verdicts and the `[tags]` lines on a run that has the Research Pod event unlocked
   (a General tree node); confirm the badge draws on those cards (`hashtagShortDescriptionText` is the template).
3. Item ranking: the guide tiers now cover 44 items; the rest score on fit alone. Grow the tiers when a better
   source appears, or add a per-survivor item table.
4. Run history in-process (the game's run-history save) to restore the damage-share and partner weights.
5. Optional, opt-in: drive the game's autoselection with this ranking.
