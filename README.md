# YAZS Companion — in-game mod (BepInEx 6 IL2CPP)

![YAZS Companion](art/banner.png)

The mod version of the companion: it runs inside *Yet Another Zombie Survivors*, reads every
selection screen, the squad and the run clock straight from the game's objects, ranks the offered
cards with a C# port of the PC app's rules (`lib/engine.js` → `Ranker.cs`) and draws the verdict
above each card. No OCR, no overlay, no save-file polling. It never writes to the game's saves
and never picks for you - the one exception is yours to switch on: the one-click EQUIP ADVICE button of the run setup
screen (0.13.0, off by default) presses the game's own badge buttons when you click it, as your own clicks would.

What it does - each has its section below:

- **Card verdicts** on every selection screen (level-up, chest, military training, rescue, Research Pod): the
  recommended card framed, one reason in plain words under every card, a WHY band on the selected card (a panel
  beside it in the side wing on screens wider than 16:9), CLOSE CALL when the first two are level. REROLL or SKIP over the game's own button when the cards on offer are weak for the
  squad (BANISH too, off by default).
- **The PLAN readout** in a corner of the HUD during play: what to pick next per survivor, the tag plan, the rescues
  and items worth a slot, what the active quest still asks.
- **Badge advice** on the run setup screen (with an optional one-click EQUIP ADVICE, off by default) and **Training
  Yard advice** (the nodes worth buying, in order).
- **The mod menu** (COMPANION in the main menu and the pause menu, or F10): a build per survivor, the standing orders
  of the advice, what the mod draws; menus that fill wide and tall screens.
- **Auto-update** from the GitHub releases, and an **extension API** for other BepInEx plugins.

What changed in each version, and how it was checked: [CHANGELOG.md](CHANGELOG.md).

## The mod menu (0.10.0)

Open it with the **Companion** entry the mod adds to the main menu and to the pause menu (a clone of one of the
game's own buttons, wired into their up / down navigation), or with **F10** (`[Menu] MenuKey`). Mouse, keyboard
(arrows or WASD, Enter, Esc, Q / E for the tabs) and controller (the game's own `UISubmit`, `Cancel`,
`GoNextTab` / `GoPrevTab` actions and the move axes). While it is open the game's menu underneath holds still.

- **BUILDS** - the nine survivors on the left, the builds of the one in focus on the right; select one and the
  advice follows it: its **weapon branch** (one of the three tier-3 weapons), the **order of the abilities** (#1
  is its main ability), the **evolution** to take when both are offered, its **level-up style**, and what it **wants from items**. `Auto`
  (the default for everyone) fixes nothing and reads the squad instead. Presets are labelled honestly:
  `GUIDE PICK` = a build human guide writers describe, `ALTERNATIVE` = an option read from the game's own data where
  the guides are silent (Medic and Mechanic have ONE build in the guides; their alternatives say so). The footer
  explains the build in focus.
- **MAKE MY OWN BUILD** - copies the selected build and opens the editor: level-up style, weapon branch (any of the
  three tier-3 weapons, or "decided live"), a rank or SKIP per ability, an evolution per ability (or "live"), item
  leanings as toggles, "start over from" any preset. Saved as you change it (`builds.json` next to the DLL; presets stay in the DLL, so
  updates refresh them without touching your own).
- **ADVICE** - the standing orders for every survivor: level-up style for survivors on Auto (weapon first /
  balanced / abilities first), whose style a build another mod lends follows while Auto picks it (the build's style,
  the default - the cards and the PLAN say so - or yours: `[Advice] LentBuildStyle`; the row shows while another mod
  lends builds), how strongly the run clock moves the advice, whether the game mode steers it, the
  weight of squad synergy, the damage type tag plan (Auto: favour the main type / spread / a fixed type), what the run is for
  (win it / balanced / farm progress), caution, what to do with SOS signals late in a run, the hints on the selection
  screens (Reroll and skip / Reroll, skip, banish / Rescue screen only / Off) and how hard the active quest steers the
  cards. Stored in the `[Advice]` section of the config file, so they can be hand-edited too. Eleven rows show at
  once; a twelfth scrolls in with the focus, the wheel and the gold arrows (0.15.0), on DISPLAY too.
- **DISPLAY** - what the mod draws: card verdicts, the card text size, the WHY band on the selected card, the PLAN
  readout (size, detail, position, backing, idle opacity), Training Yard advice, motion, menus on wide screens. Next
  to the settings sits the readout itself: the same widget the HUD gets, built with the settings as they stand, in a
  window of the menu - the menu's canvas has the HUD canvas's geometry, so the text has exactly the pixels it will
  have in play (the caption says how many). It is rebuilt on every change, runs through sample plans so the gold
  change highlight and the settling to the idle opacity can be seen, and sits on a stand-in field with bright effects
  where the readout can be placed.
- **Menus on wide screens** (DISPLAY tab, `[General] WideMenus`) - on a screen wider or taller than 16:9
  (an ultrawide monitor, a 16:10 handheld like the Steam Deck) the game frames its menus in black: in a run the bars
  come and go with every level-up, chest, rescue, pause and the results; on the main menu and in the camp they stay.
  `Everywhere` (the default) hides that frame and carries each menu's own dark backdrop to the screen edges - the
  cards, buttons and text stay where the game puts them; `DuringRuns` does it for the run's menus only; `Off` keeps
  the game's frame. The end credits keep it, and so does the main menu on screens wider than its art (about 2.65:1).
  Every element is checked against the game's own layout before it is touched (a menu that does not match keeps the
  frame), every change is undone when the setting goes off or the resolution changes, and the log says what was done
  (`[wide] ...` lines). Nothing changes on a 16:9 screen.
- **MODS** - only there while another mod has registered an option (see "Extensions (for other mods)"): a
  header per mod, a sub-header per group, and under them the same left / right rows as on the other tabs. A list
  longer than the tab scrolls with the focus and the mouse wheel. Builds another mod lends show up on the BUILDS
  tab instead, tagged with that mod's title; with more than five cards the row keeps the cards readable and scrolls
  with the focus. A survivor on Auto whose lender names a default reads `Auto: <build>` and the card says `VIA AUTO`.

Every change is written as it is made (BepInEx saves the config file inside the setter; `builds.json` is written by
`Builds`), the header says `SAVED - applies at once` for a moment (or `NOT SAVED` when the file could not be
written), and the log gets a `[config] General.PanelSize = 1.2 (saved)` line. The readout is rebuilt with the new
settings while the game is still paused, so it is there when play resumes. Input is armed only once the click or
key that opened the menu has been released, and a frame with a mouse click never also counts as Submit.

The Training Yard advice follows the selected build as well (its branch, and its ability order in place of the
guides' tiers).

Artwork: the crest, a 5 x 5 atlas of glyphs, a chamfered 9-slice panel, the focus glow and the backdrop are
generated by `art/make_art.py` (Pillow + numpy, supersampled) into `YazsCompanion.Mod/Art`, embedded in the DLL
and decoded by `Art.cs`; portraits and weapon / ability icons are the game's own sprites. `[Debug] PreviewMenu`
walks the menu on the main menu and photographs every screen (with `PreviewResolution = 1280x800` in a real
Deck-sized window); `[Debug] PreviewPause` starts a Quick Run (clicking through the run wizard when it comes up),
plays thirty seconds, pauses, opens the menu over the pause menu, changes the readout's detail and size through
the DISPLAY tab's own controls, closes it, resumes so the log and a screenshot show the readout coming back with
those settings, and puts the settings back - well under fifty seconds of play, so no save is written.

## The PLAN readout (during play)

A HUD readout in the empty bottom-left corner, under the weapon and ability icons. It is drawn like the game's quest
tracker, not like a menu panel: a small gold diamond and `PLAN` over a gold rule that fades out, then the rows on a
soft dark wash that is solid only under the text, dissolves towards the middle of the screen and is feathered at the
top and bottom - no frame, no box. Every row has a label column (the survivor's name in bold white, `TAGS` / `SOS` /
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
evolution is waiting (`evolve Arrow Rain`, gold; `evolve Arrow Rain (Downpour preferred)` when your build or the
squad favours one of the two - either one is the spike of its level-up, and the cards rank whichever the game offers
first), the ability to keep feeding (`Sawblade Drone 2/4`), or the next ability worth taking (`next Minefield`) -
always the ability the cards will rank first (the readout asks the ranker, `Ranker.TopAbility`: the next level of
every owned ability and the best missing one, by the very scores the cards get), never one of a class rank the
survivor had not reached when the run began (ranks open at class level 20 / 40 / 60 / 80; the game does not offer
it). The two items stand in the order the cards will rank them, and under "abilities first" a weapon level that
waits reads `Handgun later` until the survivor's own abilities are as good as done (`Handgun later - the build's
style` when that "abilities first" is a lent build's own and your level-up style says otherwise); a row with nothing
left to pick says `build complete`, or `no new ability - too late to level one` when an ability slot is still empty
late in the run. `TAGS` shows the highest damage-type tag count (`Kinetic 31 - effect on` once its 10-tag effect is
on) and `next Pod: X` only when the type to build up at the next Research Pod is another one. A full squad is five or six rows.

**Full (`PanelDetail = Full`):** two rows per survivor with a hairline between the groups:

```
TANK      Pump-Action Shotgun 3/4 › Rocket Launcher
          Sawblade Drone 2/4  ·  next  Minefield
PYRO      Fireaxe 1/4 › Blowtorch
          Molotov 3/4 › Napalm / Cocktail Party  ·  next  No Pain, No Gain
TAGS      Kinetic 8/10, Slashing 4/10
```

The weapon line shows the current weapon and its next step (gold once the weapon is maxed; `take Shotgun` for a
recruit without one; `next tier locked in the Skill Tree` / `max tier` at the end of a line). The ability line holds at most
two items in priority order: a maxed ability waiting for its unlocked evolution card (`Minefield 4/4 › Shrapnel /
Taunt`, gold), the ability to keep feeding (with its evolutions once it is one level below max and the Training Yard
unlocked them; earlier the names only made the line wrap), and the next ability worth taking. `TAGS` shows the two
highest tag point counts (`Kinetic 31, Electric 22 - effects on` past the 10-tag effects). A full squad is nine lines.

In both: `SOS` is the two best rescues by the SOS-card rules, exact ties settled as on the cards (hidden with a
full squad), or what the active quest's team rule says (`quest: stay solo`, `quest: Huntress`, the recruits with
`quest: full team` late in a run - see "How it ranks", SOS); `GRAB` the two best items worth a chest slot (S/A tier or
quest target, not held); `QUEST` what the active quest still asks while one of its rules changes the advice - at most two items, in
words that change with a pick, not with every kill (`Taser to tier 3, max level`, `Plasma 3/4 to max level`, `a
health item (0 of 1)`, `evolutions (2 of 6)`, `no Pyro abilities`, `Explosive+Slashing+Fire under 20`, `Engineer kills to 2000`); gone once
met. Since 0.15.0 the row also follows a story quest, which no card is steered by: `story objective 3 of 5` (the story's own
event count - the game's quest box says what it is, "Broken Vials 0/5" under "Genesis") or `story objective: the Boss Rush boss`,
dim and info only, after the rules' items, `, done` once the count is reached, never when the run cannot complete the quest; a
story step rebuilds the row. `[General] QuestProgress` (on) switches it; the `[quest]` lines name every counted objective's id and
count either way (`1. CustomGameplayEvent (Live) id main_story_objective_q6 0/5 - progress only`, then `[quest]
GameHubQuest_Main_06 -> story objective main_story_objective_q6: 3 of 5 - progress only` at each step; event counts and kills of
a rank too, log only). When a rebuild changes a line (a pick, a recruit, a
Research Pod), that row's value comes back gold and eases to white over `PanelHighlight` seconds (default 3,
0.8 s of it held gold; the block itself never grows or jumps for it); the log names the changed lines (`[plan] ... [changed: Tank.plan]`).
The `›` and `·` glyphs are checked against the HUD font at creation and replaced by `»` (`>` when that is
missing too) and `|` when missing (`[panel] glyphs: ...`). It is driven by
the HUD's own `UIGameplay.Update`, rebuilds only when the squad state changes (a cheap key of the squad text,
the active quest and the tag points; a rebuild walks every tree node and item), hides while a selection screen,
the pause menu, the results screen or any other game view is up, and dies with the HUD when the run ends.

Config (`[General]`, canvas units; the canvas is 3840 wide on every screen, 2160 tall on 16:9, 2400 on the Deck's
16:10): `ShowPanel`; `PanelPosition` = `BottomLeft` (with `PanelLeft` 44 / `PanelBottom` 70; the block grows up
and to the right and stays under 62 % of the screen height, where the icon column ends) or `Right` (with
`PanelRight` 44 / `PanelTop` 780; the block grows down and to the left, stays above the minimap, and its backing
dissolves to the left); `PanelDetail` = `Compact` / `Full`; `PanelOpacity` (0.42; 0 = text only); `PanelIdle`
(0.7; 1 = never dims); `PanelHighlight`. Every change is logged as a `[plan] 03:31: ...` line; `[panel]
created under UIGameplay using label 'GameTimer_Txt' at (44, 70) x1.45 canvas 3840x2400 screen 1280x800` shows
which HUD text it cloned for the font, the scale and the geometry.

**Size.** The readout follows the screen: its body text is 1.875 % of the screen's height and never under 15 px -
15 px on the Steam Deck (x1.45), 20 px at 1080p, 27 px at 1440p, 40 px at 4K (x1.31 each; an ultrawide screen counts
by its height, like the game's own HUD). `PanelSize` (the menu's **Readout size**, 70 % - 200 % in steps
of ten) multiplies the automatic size; `PanelScale`, when set, replaces the automatic part for hand-tuning. A
plan too tall for its corner still shrinks to stay clear of the HUD (`[panel] scale x...` in the log).

## Training Yard advice (0.8.0)

On the Training Yard (`UIViewSkillTree`, "Train your survivors") every tab gets, read-only:

- a **gold diamond with a number** on the top-right corner of each node worth buying with the points on hand, in
  purchase order (the game's own green "can buy" diamond sits on the top-left), and a **hollow gold diamond** on the
  node to save for next;
- a strip in the empty band under the tree, between the legend and the Reset points button:
  `SPEND 9   1 Blowtorch 3>4 · 2 No pain no gain Evolutions 0>1` / `THEN   save 4 more for Rocket Launcher 3>4 ·
  after that Tank <-> Pyro, Bombing Strike` / `WHY   Blowtorch — Levels of the first two weapons are cheap and speed
  up the start.` The WHY row follows the cursor when the node under it is part of the advice, and otherwise says
  where that node stands ("later in the plan (step 14 of 41)", "its rank is still locked", "maxed").

The plan is a port of the PC app's "Spend now" (`lib/engine.js`) without its run-history tailoring (`TreePlan.cs`,
pure): the General tab follows a fixed order (economy, then the strongest multipliers, survivability in between);
a survivor's tab follows a rule sequence - starting abilities to 2 then 3, the cheap first two weapons, the evolutions
of the starting abilities, the main weapon branch (one of the three tier-3 weapons: your build's, else the guides'
branch from `knowledge.json`, else the one levelled most), abilities maxed, rank III abilities, badges and synergies
as their ranks open, rank V passives by impact, then the two other branches for the runs that offer them, then
everything that is left; abilities inside a group go by the guides' tier. The weapon nodes are sorted by the weapon
they boost, not by their column (the fifth weapon's node sits one column further right; it is a branch, not "the
final weapon of the line"). The walk spends the points level by level (from level L to L + 1 costs `levelUpCosts[L]`, levels below `levelMin` are free), skips
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
  label, in the card's own font. While you hover a recommended ability card the game shows its own green
  `Skill Tree 3 / 5` label in that place; the ribbon fades out with the label's fade-in and comes back as it goes.
- **Every card gets one reason line** just below it, in plain words (`Wording.cs`): what the pick
  does and why, in the game's own words, every number with what it counts, 50 characters at most - `Level 3 of 4 -
  evolution unlocks at level 4`, `Max level - next tier: Pump-Action Shotgun`, `2ND   The Rifleman build's main
  ability`, `3RD   Level 2 of 4 - this build levels abilities first`, `Evolution - Kinetic is 100% of your damage`,
  `Evolves the Rifleman build's main ability` (a build that names no evolution for it), `Level 3 of 4 - abilities first,
  the build's style` (a lent build's own, followed through Auto),
  `The Bombardier build wants ability area`, `Mixed: boosts Ice, weakens Kinetic and Explosive`, `Does nothing until a
  tag hits 30 (Kinetic is 16)`, `S-tier, also deals Kinetic like your Handgun`, `Chemical 10-tag effect - but nobody
  deals Chemical`. Grey on the others, warm on the pick, dull red with `AVOID` in place of its place when a card is
  worth avoiding (a score under 1). The 50 characters are counted as drawn: with the names
  another mod lends (a shorter form when they make a line too long), 48 beside `AVOID`; fewer at a larger card text
  size (the line is sized to the room its size leaves: 42 characters in all on the Deck), and a line still too wide
  shrinks to 15 px before an ellipsis would cut it. Two cards of one offer never
  draw the same line (a line a quest decided aside: each of those cards counts for it). Scores stay in the log; the
  `[card]` line keeps the ranking's own headline and a `[shown]` line says what was drawn.
- **WHY on highlight**: while a card is selected - the mouse over it, the controller's focus on it - a
  plate under the cards (in the band down to the action buttons, or under the buttons when that band holds more)
  says the rest of its verdict: a gold `WHY`, then the "vs #1" sentence (`Experiment 21 goes first: The Rifleman
  build's main ability`; on the first card `Just ahead of Medical Drone` / `Well ahead of ...`) and up to four more
  reasons (`Evolution locked in the Skill Tree`, `Kinetic is 44% of your damage`, `Boosted by Trap Expertise`, `The
  Rifleman build levels abilities first`, `Your squad has 9 Explosive tags - 1 short of its 10-tag effect`), each in the
  game's words, separated by a dim `/`; at least 15 px (the cards' own size where that is more), up to three lines
  (at 1920 x 1080's four-button level-up one 15 px line with a tighter pad, 0.15.0 - 0.14.0 found no room there).
  On a screen wider than 16:9 whose menus fill the screen (WideMenus), the WHY goes in a panel in the side wing beside
  the selected card instead (0.15.0): its lead on a line of its own, then up to four reasons at the cards' own reason
  size, one reason a line - a long one wraps onto a second line, slightly indented, and the panel grows upward for it
  rather than leaving a reason out (`side wing left, header + 3 lines` in the `[why]` line: the lead's line, then the
  reasons' lines); 16:9 and the Deck's 16:10 keep the band.
  The "vs #1" sentence names what the first card has that this one lacks - never a place in a build this card stands
  higher in. When the first two cards are less than 0.20 apart on the cards' own scale, the lead reads `CLOSE CALL` on
  both and the sentence does not argue the order (`Either works - Medical Drone is a hair ahead`, `Either works - a hair
  ahead of Experiment 21`). On a card a quest decided, the quest stands first (`The quest comes before the build's
  order` on a lifted weapon; a card the quest makes AVOID keeps its merits to itself). A weapon level ranked first
  under "abilities first" says what decided instead of the style (`Resuscitation is 4th in the build's order`, `No
  ability on this offer`), an ability first over a weapon-first level says the clock (`Too late to finish Pump-Action
  Shotgun (2:10 left)`), and a lent build's own style is said as the build's (`Abilities first, the Medic build's
  style`). Another card is named whole (`Bombing Strike: Bioweapon goes first`) - an evolution's own part only on a card
  of the same base. It sits above or below the game's
  divider over the buttons, never across it. It appears with the selection (0.6 s into a screen, once it has flown
  in), goes with it, and swaps its words as the selection moves from card to card. The DISPLAY tab's **WHY band on the
  selected card** (`[General] ShowWhy`) turns it off (it needs `ShowBadges`).
- Screens covered: level-up (including the stat cards of Endless level-ups), chest, military training,
  SOS rescue (survivors and Liberate), and the Research Pod reward (damage type tag points).

Placement comes from the card prefab read out of the game's asset files (`tools`-free, see
*How it hooks the game*): every card root is 832 x 1462 canvas units on a 3840 x 2160 canvas, the
game's ribbon overhangs the bottom edge, and about 200 canvas units lie below the card before the
divider line. Everything is parented to the card root, so it rises with a hovered card and
vanishes with the screen. Nothing captures clicks.

Turn the badges off with `ShowBadges = false` in `BepInEx\config\bidoi.yazs.companion.cfg`
(the file appears after the first launch). The log keeps working either way. The card text's size is the DISPLAY
tab's **Card text size** row (`BadgeScale`, 0.15.0). Auto (0, the default) enlarges the ribbon and the reason lines on
small screens until the reason reads 16 px - up to x1.3, x1.6 on a screen taller than 16:9 (x1 on a desktop monitor,
about 21 px at 1440p; x1.56 on the Deck, 16 px - 0.14.0 stopped at x1.3 there, about 13 px). 90% / 115% / 130% / 150%
are shares of what Auto draws on that screen (the cfg takes 0.5 - 1.6): 115% reads 23.6 px at 1440p and 18.4 px on
the Deck; at most x1.85, the RECOMMENDED ribbon stopping at x1.3 so the line under it stays above the game's divider.
A step that would draw what the screen shows already is passed over, and the row's help lists every step's size on
this screen; the preview shows a reason line and the WHY band at their real size. The WHY band and the REROLL / SKIP
hint follow the row, and nothing of it is drawn under 15 px, the WHY band's floor (a small screen gets the size that
reads 15 px). `[badge] scale s=.. px=..` is logged once per screen size and setting, with the characters a line
holds; the frame is not scaled.

## The reroll hint on the rescue screen (0.12.2)

The rescue screen says when a reroll is worth it (in a round of Steam Deck logs the player rerolled until the
survivor the readout's `SOS` row named came up):

```
REROLL  -  Tank would fit this squad better
```

- **When.** The best survivor who could still come rates at least **0.75** above the best card on the table (a
  recruit, or Liberate when the cards rank it first), and the game still has a reroll for the screen. Everyone is
  scored by the very rules of the SOS cards, so the hint, the cards and the readout's `SOS` row always agree. Why
  0.75: early in a run one guide tier apart is 0.5 and one bought synergy 1.1, so a tier alone, a trained level or
  an exact tie stay silent (a reroll is spent for the run, and the next draw may be no better), while a synergy, or a
  tier together with shared damage types, speaks. On the Deck's four screens the gap was 0.96 - 1.29 each time. When
  two survivors clear the margin the line names both (`SWAT or Tank would fit this squad better`, and `(+1 more)` when
  still more do; the scores stay in the log). No hint late in a run, when the cards say Liberate anyway (a recruit no
  longer has the time to grow).
- **Who could still come** follows how the game draws the cards: every survivor unlocked in your profile who is not
  on the squad, not on the cards, and not held back after a reroll (the cards shown before one); where the game's
  own availability check of a survivor's unlock card can be read, it must agree.
- **The reroll count is the game's own** for that screen: the number it hands the screen's action buttons, else the
  team's `Rerolls available` (the very number the game is about to hand over - it fills the cards before it refreshes
  the buttons), else the number on the Reroll button (until that refresh it still shows what it showed last time);
  a FREE reroll counts too. When the game's own count comes and differs from the one the hint was judged with (or
  the FREE label changed with it), the hint is judged again once (`[squad] reroll hint: (re-judged with rerolls 8 -
  ...) SHOWN - ...`).
- **Where.** The Reroll button gets the recommended card's gold frame, and the line stands over the button in the
  band between the cards' reason lines and the button - measured on screen 0.6 s after the cards came, never over
  a card's text; when that band is too low it goes under the button, and without room for either only the frame
  shows. Motion only when it appears (the frame settles, the line unfolds from its left tip and types on); nothing
  loops.
- **The active quest first**: at the quest's team limit no hint (`not shown - quest: stay solo - no recruit
  wanted (Liberate)`); while the quest needs a class the squad lacks, the hint speaks for that class alone, whatever
  the margin or the clock: `REROLL - the quest needs Huntress` when she could still come and a reroll is left, nothing
  when she is on the cards already.
- **After a reroll** the screen is judged again (the replaced offer); the pick takes the hint away. It never rerolls
  for you.
- **Off:** the ADVICE tab's "Hints on the selection screens" at Off (`[Advice] RerollHint = false`; the row's "Rescue
  screen only" keeps this hint alone).
- **The log**, one line per judgement with the scores:
  `[squad] reroll hint: SHOWN - Tank would rate higher (5.93 vs Huntress 4.64, +1.29) | rerolls 3 (the team's
  Rerolls available) | on the cards: Huntress 4.64, Ghost 2.98, Liberate 1.00 | could still come: Tank 5.93, Engineer
  4.03, Ranger 3.78 | margin 0.75, recruit value 1.00`, or `not shown - the best survivor is on the cards ...` /
  `... only 0.60 above the cards - under the 0.75 margin` / `no reroll left (0 ...)`; then where it was drawn
  (`[squad] reroll hint: drawn over the Reroll button - ... band N units ...`).

## Action hints on every selection screen (0.14.0)

The level-up, chest, military training and Research Pod screens get the rescue screen's hint for their own buttons
(`ScreenCall.cs`, pure - the bench replays the logged chests), one at most per screen, the first that applies:

```
SKIP    -  Quest: taking any item fails it
REROLL  -  the best card here is weak for this squad (2 rerolls left)
SKIP    -  every card here would hurt this squad - take the skip bonus
SKIP    -  the heal and the cash are worth more than these cards
BANISH  -  Medical Drone - the Rifleman build skips it
```

- **REROLL** when the best card is under the screen's floor on the cards' own scale - a chest 2.5, a level-up 3.5 in
  the first half of the run and 2.5 after, a military training 2.0, a Research Pod 2.0 - and the game still has a
  reroll for the screen (its own count, as on the rescue screen; a FREE reroll counts). `(2 rerolls left)` / `(1 reroll
  left)` when that few are. The chest floor: at 3.0 the hint would have spoken on 70 of the 147 logged chests (48 %, 65
  of them taken as offered), at 2.5 on 34; the words say the best card is weak, not that nothing fits - the card's own
  line may well say `A-tier, fits the Rifleman build`.
- **SKIP** when a quest wants no item at all (a chest: a reroll brings only more items); when every card is worth
  avoiding (all under 1) and no reroll is left; or when every card is under 1.5 and the skip bonus is worth more. The
  game's skip grants cash (`(money multiplier + enemy scaling - 1) x 300`) and heals 20 % of the team's max health
  (read in its machine code: `GetSkipBonusMoney` / `GetSkipBonusHp`); the heal counts as much as the squad is missing
  of it, the cash as the run goal values it.
- **BANISH** (off by default - a banish is for the whole run): the weakest card the build skips, or one worth
  avoiding, that the game will banish (`isBanishable`) and the active quest does not name (its class under "fully
  upgrade", its weapon, ability or evolution, a health item it counts, a card a quest line decided) - never the card
  framed RECOMMENDED.
- **Lockdown** is not advised: what a lockdown keeps has not been read in the game's code yet.
- **Where.** As on the rescue screen: the action's button gets the gold frame, the line stands over it in the band
  under the cards (or under the button, or the frame alone) - the band manager's rule (`ScreenBand.Hint`); the WHY band
  keeps clear of it. On a button right of the screen's centre (Skip, next to the team panel) the line ends at the
  button's right edge and grows left, and no line runs into the team panel or the skip reward (cut short with an
  ellipsis; the frame alone when too little is left).
- **An older cfg:** a 0.13.0 cfg with the rescue screen's hint off (`RerollHint = false`) and no `ActionHints` line
  starts with these hints off too (`[config] Advice.ActionHints = None: ...` in the log).
- **Switch:** `[Advice] ActionHints` - any of `Reroll, Skip, Banish` (`Reroll, Skip` by default, `None` = off); the
  ADVICE tab's **Hints on the selection screens** cycles Reroll and skip / Reroll, skip, banish / Rescue screen only /
  Off (it sets `ActionHints` and the rescue screen's `RerollHint` together; another mix set in the cfg shows as
  `Custom`). It never presses a button.
- **The log**, one line per judgement: `[squad] action hint: Chest 03:09: SHOWN REROLL - best Detective's Pipe 1.98
  under the chest floor 2.50 | rerolls 8 (the screen's count) | skip +120 cash, +40 health (worth 0.15) | actions
  Reroll, Skip | 'the best card here is weak for this squad'`, or `not shown - best ... at or over the chest floor 2.50`; the
  banishes: `[pick] Chest 03:09: the Banish button pressed (4 banishes left)`, `[pick] Chest 03:09: banish on Golden Key
  (#4, 1.19)` (the next `[offer]` line says `replaced (banish)` when it took).

## Badge advice on the run setup screen (0.13.0)

The game's run setup screen ("SELECT LOADOUT": difficulty and badges) had no advice. Now it says which of your unlocked
badges to equip for the team leader, the build, the mode, the difficulty and the standing orders you picked, in
priority order, and why - in the game's own look, on the game's own buttons:

```
 BADGES COLLECTION                                         CHOSEN BADGES
 [Lev o][Gun 1][Cri 2][Tou 4][Bom ][Pow 3][Cov ][Thu o]    [Lev o][Spe o][Gro o][Thu o]
 ...                                                       EQUIP  1 Gunner · 2 Critical · 3 Power · 4 Tough
 [ L ][ L ][ L ]  <> #1 EQUIP  Kinetic is 92% of your      SWAP OUT  Thunder · Leveling · Growth · Speed
                  damage · L2, in for Thunder (+17.15)     DIFFICULTY LEVEL
 n = numbered gold diamond (top-right)   o = small hollow rust diamond   L = locked (nothing drawn)
```

- **The diamonds** sit on the top-right corner of a badge (the game's own green "selected" diamond hangs under the
  bottom centre). A numbered gold diamond = advised (1 = keep it even with fewer slots; numbers are priority, not slot
  positions - the game applies every slot the same); plus a gold frame when it is not equipped yet; a small hollow rust
  diamond = equipped, but the advice would swap it out; a hollow numbered diamond = equipped and kept, because the swap
  would gain too little (`KEEP · close`: a swap is shown only when it gains at least 1.0 point and at least 15 % of the
  kept badge's points); `Q` = forced by a quest;
  a pin tick = pinned on your build. The CHOSEN BADGES slots carry the same diamonds. Shape and number carry the
  meaning, so colour is never the only cue. Locked badges get nothing.
- **The WHY line** explains the badge the game's info panel shows - it follows the cursor (mouse and pad) through the
  game's own highlight: `#1 EQUIP  Kinetic is 92% of your damage` and, dim, `level 2 · replaces Thunder (+17 score)`,
  `close call: Tough (score 5.9)`, `pinned on your build`, `level 2 · never advised (score 0.0)`. A lent build Auto
  follows is `the Rifleman build`, never `Rifleman (Auto)`. The reasons count in the game's terms: `Hardcore: survival
  picks count 31% more`, `+20% Kinetic, +2 Kinetic tags`, `faster weapons and abilities`. It goes into the first free
  band the screen has (measured: the empty cells after the last grid row, then under the info text, then over the
  badge's name; on the measured 3440x1440 frame the grid gap, and by the offline mock on the 1280x800 Deck too), else one
  line appended to the game's bonus text.
- **The summary** under CHOSEN BADGES: `EQUIP 1 Gunner · 2 Critical ...` (equipped names white, missing ones gold),
  `SWAP OUT ...` / `YOUR BADGES MATCH THE ADVICE` / `THE QUEST FIXES EVERY BADGE`, and with Full detail `TRAINING YARD
  Gunner to level 3: best use of 3 SWAT points` or `UNLOCK <badge> (<class> tree) would be #k`. Text follows the readout's rule (1.9 %
  of the screen's height, never under 15 px) times `[General] LoadoutSize`; it shrinks to 0.87x at most, never under
  15 px, then drops the YARD row, then SWAP OUT.
- **How it values a badge.** Points = "% more squad damage over the whole run, or its equivalent", from the game's own
  badge values (read live: 1.0.2 changed 24 of them) times the weights in knowledge.json (`badgeStats`, `badgeModes`,
  `badgeRules`, with `why` texts; the ones not measured in the game say so) and what the run will deal: the build's
  weapon line, abilities and evolutions (Auto: the guides' branch), its weapon / ability split, crit and healing leans,
  the tag points it reaches on its own (a badge that takes a type to its special gets a pull), the mode's elite / boss
  share and its pointless stats (One Hit: no health; Extermination: no XP or luck), survival by difficulty and
  `[Advice] Caution`, and cash / survivor-XP badges by `[Advice] RunGoal` (x0.3 win the run / x0.6 balanced / x1.2
  farm progress). The team leader alone is scored (the one survivor known before the run starts;
  `badgeRules.recruitWeight` 0.3 would blend in the two likely recruits). The fill is greedy - quest badges first, then your build's pins in order, then the best
  score each round - so the advice for 3 slots is the first 3 of the advice for 4. Ties: the score in whole
  centi-points, then the higher level, then the game's grid order, then the badge id.
- **Detail** (`[Advice] LoadoutHint`, the ADVICE tab's tenth row): Off (nothing drawn, still logged) / Numbers / Numbers
  + reason (default) / Full (close calls and the YARD / UNLOCK row).
- **The BADGES page** (BUILDS tab, `BADGES`, or your build editor's `Badges` row): the 27 badges in the game's 8 x 4
  grid with their icons, levels and the same diamonds at the screen's size, a legend, the summary rows and the WHY of
  the badge in focus; "Preview for" every mode and difficulty (session only); the screen's detail, the size (70 - 200
  %) and the EQUIP ADVICE switch, each saved at once (SAVED in the header). On your own build a press on a badge cycles
  PIN (it takes a slot first) -> NEVER -> back to the advice; presets and Auto offer MAKE MY OWN BUILD; a build pack's
  pins are read-only (packs may carry `badges` / `skipBadges`: extension API 3). Over the pause menu the page shows the
  run being played (`THIS RUN: 3 of 4 as advised`). The BUILDS cards' description gains one sentence: `Badges as leader
  (Normal II): 1 Gunner L2 · 2 Critical L2 · 3 Power L2 · 4 Tough L2.`
- **EQUIP ADVICE** (off by default; `[Advice] LoadoutEquip`; no key unless `[Advice] LoadoutEquipKey` names one - a
  clash with another plugin's, the game's or Steam's key is noted in the log): a plate next to the slots; a click or
  the key presses the game's own badge button along the plan - removes first, then adds, one press
  every 0.12 s, never a quest's badge, never a locked one, never past the slots, every guard checked again against the
  screen before each press, each press logged - exactly what your own clicks would do (the game saves the selection).
  Afterwards it offers UNDO until the screen closes or you click a badge by hand.
- **During play nothing new appears** (badges cannot change in a run); the `[ctx]` lines gain `| badges: Gunner L2, ...
  (advice #7: 4 of 4)`.
- **Robust to game patches:** every game member is read through its own small accessor, each capability has a fallback
  (the grid buttons instead of the registry, the slot buttons instead of the selection list ...), every `drawn` line
  names its level (A everything / B no WHY line / C summary only / D log only / E nothing), and 3 errors in one visit
  switch the feature off for the session with one line. At load: `[loadout] hooks: ...`, `[loadout] game members: n of n
  readable`, `[loadout] badge classes: ... - all readable`.
- **The log**: per session `[loadout] inventory 27 badges, hash 237c6a7f = the 1.0.2 reference` and the inventory as
  JSON; per change `[loadout] visit N ...`, `shape #N`, `advise #N: SWAT 'Rifleman' Normal d2 WinTheRun | 4 slots ... |
  EQUIP 1 Gunner L2 17.15, ...`, `why #N ...`, the replayable `input #N {json}`, `hint #N`, `equipped #N: ... | swap
  Thunder>Gunner +17.15, ... | 8 clicks to match`, `drawn #N level A (...)`; once per resolution `[loadout] layout ...`;
  per run `[loadout] run start: SWAT Normal d2 | badges ... (advice #N: k of m)`. `ItemBench --replay-loadout
  <companion.log>` recomputes every logged advice and checks every drawn set against it.
- `[Debug] PreviewSetup` walks the start flow to the screen (Play, team leader, arena, mode), photographs it
  (`fxsetup0..3`, `setup0_loadout`), moves the game's cursor over the first advised badge, an equipped one the advice
  would swap out and a locked one (`setup1_why`, `setup2_why_swap`, `setup3_why_locked`) - it never clicks a badge -
  then presses the difficulty and START and logs `[loadout] measure: ...` 3 s into the run (`PreviewSetupRun = false`
  backs out of the start flow instead). `[Debug] PreviewMenu` photographs the BADGES page (`menu_badges0..2`).

## How it ranks (0.10.0: the build, the squad, the clock, the mode)

What a card is worth is read live from four things. Every score is logged with its reasons (`[ctx]`, `[card]`), so
a verdict you disagree with can be traced, and the menu or `knowledge.json` adjusted.

**Sources, graded** (2026-09 review). Most "1.0 wikis" for this game are auto-generated and contradict the game's
own data (wrong weapon forks, abilities that do not exist), so they only count where a human source agrees.
*Official*: the Steam patch notes 0.3 - 1.0.1a and developer forum replies (modes, tag rules, item changes).
*Human*: Kudesnik's "0.7 General guide" (Steam id 3345006120), GoldMath's "Synergy Guide" (id 3352985777; the mod
recomputes its counts from the game's own nodes), JHG's achievements guide, the Steam forum threads on builds,
weapons and modes, Destructoid's 1.0 tier list. *Wiki*: yetanotherzombiesurvivors.wiki's tier lists, kept as a weak
prior. The game's own data - damage types, powerup tags, tag points per level, weapon range modifiers, mode masks,
dumped from the running game by `[Debug] Probe` - always wins over any of them. The human guides DISAGREE on
"weapon first" (one goes ability-first for five survivors), which is why the level-up style is yours to set.

- **The build** (mod menu). A selected build ranks its abilities (#1 +1.6, #2 +1.0, #3 +0.5, skipped -2.0), picks
  the weapon branch - one of the three tier-3 weapons (the other two drop to 2.0: the branches exclude each other) -,
  picks the evolution (+1.0 / -0.5, so its pick is on top when both are offered; offered alone, the other one still
  outranks any tier-up and its card says `only X is offered - your build prefers Y`) and sets the level-up style.
  On Auto: the guides' ability tiers (S +1.2, A +0.6, C -0.8), and the branch that shares damage types with the REST of the squad, then your
  Training Yard investment (+0.5 a paid level), then the guides' branch (+0.9). A build another mod lends that Auto
  follows (a pack's `default`) ranks abilities, branch and evolution like a selected one, but it is Auto, not yours:
  the other branches score as on Auto (3.6 while its branch can be offered, 6.2 when it cannot), the reasons
  say `#1 in <build> (Auto)`, and once a tier-3 weapon of another branch is owned Auto follows the pack's build of that
  branch.
- **Level-up styles.** Evolutions (7.6 and up), a recruit's first weapon (7.2) and the next weapon tier (6.6) are
  always on top. Below them a weapon level scores `floor + 0.1 x level`: floor 6.0 *weapon
  first* (every weapon level before any ability level), 4.3 *balanced* (default), 3.3 *abilities first* - lifted to 4.3
  once the survivor's own abilities are as good as done (every ability its build ranks - without a build, every free
  slot's worth - owned and at most one short of its last level; on Auto with a lent build the floor of your own
  level-up style if that is higher), then kept 0.01 under a card of its own open build ability on the same offer
  (`style: abilities first - only EMP Grenade left: ...`, `abilities first: after EMP Grenade, ahead of the rest`). Abilities:
  a new one 3.6 / 3.1 / 2.4 (fewer than two / up to four / after) - "take each ability once" early beats another
  level of an old one - a level 2.6, +1.0 for the focus ability (the build's highest-ranked open one, else the one
  furthest along), +0.5 for the level that completes it, up to +0.9 toward an unlocked evolution; all soft-capped
  under the weapon-first band so strong abilities keep their order instead of tying. Under *balanced* the first level
  of an ability the survivor does not have yet is lifted by up to +1.5 while a fresh ability can still grow up
  (`Reach(10)`: the full lift through the first half of a 20:00 run, nothing from about 11:50 on), squeezed in under
  6.1 so it never passes a tier-up, a recruit's first weapon or an evolution: "each ability once early, then the
  weapon and the focus ability side by side".
- **The squad, live.** Every level of a weapon or ability adds ONE tag point to each damage type it deals (an
  evolution too, including the types it adds); a point is +2 % for everything dealing that type and the special
  switches on at 10. So a level is worth more the larger the share of the squad's damage that carries its type
  (weapons count 1, abilities 0.5, each scaled by its level; an evolved ability counts once), +0.9 when it carries
  a type over the threshold, +0.3 within three of it. Team passives that are BOUGHT and whose owner is on the squad
  (Grenade / Turret / Trap Expertise, Cold Chain) add +0.5 to every powerup carrying their tag - including the
  evolutions that add one (Helicopter Strike: Chemtrails throws grenades, Automatic Turret: Provocation taunts).
  Bought synergy nodes with the partner on the team +2.0. **Evolutions** are chosen by exactly this: Bombing Strike
  goes Supercharge next to an Engineer and Bioweapon next to a Medic. A type an evolution adds that nobody else on
  the squad deals costs 0.15: two evolutions that are otherwise level are settled by the tag doctrine
  (stay inside the stacked type), not by card position. A weapon branch's reason names only a damage type that sets
  it apart from the other branch ("shares Kinetic with Bow" said nothing when both branches deal Kinetic); else it
  says the guides or your Training Yard investment decided.
- **The clock** (`Context.cs`). `Reach(n)` = can a plan that needs n more picks of one powerup still finish, from
  the level-up pace and the time left. The pace (`LevelPace`, 0.15.0) starts from the mode's measured pace - Normal 2.9
  level-ups a minute, Hardcore 4.7, Endless 3.2, Boss Rush 4.6, measured over the logged runs of three minutes or more -
  which counts as four level-ups seen, blends in the last three minutes' rate by the level-ups the run has seen, and is
  smoothed at each level-up (half-life 60 s of play), so `~N level-ups to come` on the `[ctx]` line holds steady from the
  first minute (it swung 58 - 76 in the first two minutes up to 0.14.0). It scales a new ability, the pull toward an
  evolution, an unfinished weapon (late, a weapon that cannot be finished falls back to the balanced floor: a
  level-1 bow with ninety seconds left is not a carry) and a recruit. Economy - XP, luck, pickup range, chest and
  upgrade quality, items that grow over the run - is worth x1.5 at the start, x1 a third in, x0.3 near the end;
  cash only ever buys Training Yard levels; survival weighs more as the clock runs, on higher difficulties and
  while the squad is hurting.
- **The mode.** The goal comes from the game (`TimeRequiredForSuccess`: Default 20:00, Hardcore and Boss Rush
  10:00, One Hit 5:00), Extermination counts waves, Endurance and Infinite are open-ended (economy never fades).
  Boss damage x2 in Boss Rush and x1.3 in Endurance; survival x1.25 in Hardcore and nothing in One Hit, where
  crowd control counts x1.6 instead. The game already withholds what is pointless per mode (no health, armor or
  dodge cards in One Hit; no XP, luck or pickup range in Extermination), so the mode only steers what is left.
- **Chests** (`ItemRules.cs`, pure). Guide tier (S +3, A +2, B +1, C -1.5; 59 items tiered, human go-to picks
  added), then the fit: damage types by the squad's share; turret / melee / taunt / deployable / grenade by
  whether the squad OWNS such a powerup (the game's powerup tags, not a class table); Silencer and Dartboard by the
  squad's weapons' own range modifiers; Magazine Clip and Last Round by clip sizes (with no magazine on the squad
  they keep a fifth of their tier and no fit); Glass Cannon by an owned Energy Shield. An item about a type the squad
  does not deal keeps 30 % of its tier; an item that is ONLY about the economy follows the clock with its whole tier,
  even when its text has a health word the game does not flag as healing (Electric Personality's "Healthpaks"). Items
  that grant tag points are judged from the run's live points (+1.2 when a +N reaches the special; Ultra Instinct
  needs a tag at 30 and is judged by what pooling the tags gains against the stack and the specials it wipes, -1.5 ..
  +3.0; One For All is a malus on a stacked type), pairs the items name are worth more once the other half is held
  (Accumulator and the magnet items, Golden Key and Silver Padlock, the Parca set), and what your builds want adds
  +0.5. Malus clauses count against the squad; an enemy's malus ("enemy projectiles deal -50 %") does not. An item
  with a line per squad size (Duct Tape) is scored by the line for the squad as it is. +3 for the quest target, -1 for
  a single-slot item already held. The GRAB row only lists what can still drop (`stillAvailableItems`).
- **Research Pod rewards**: 1 + 0.15 per point, +2.5 scaled by the squad's share of that type, +1.0 for the type
  you stack (or the type fixed on the ADVICE tab; nothing with "Spread"), +1.5 when the card reaches the special.
  The reason states the share ("Slashing: 51% of the squad's damage") and says when a card takes a type past 30.
- **SOS**: a recruit is a third gun and +20 % XP for the rest of the run (2.0), plus the guides' rescue tier, +1.1
  per BOUGHT synergy node either way (an unbought node does nothing in a run), shared damage types, team passives
  either way, how trained the recruit is - all scaled by the time a newcomer still has to grow. Liberate: 5 with a
  full squad, else 1.0 rising to 4.2 as that time runs out. Two recruits that score the same (an A-tier rescue with
  a team passive ties an S-tier one once both are fully trained) go by the guides' tier, then bought synergies, then
  the class order - on the cards and in the readout's SOS row alike. **The active quest's team rule goes over all of
  it** (`QuestTeam.cs`): the
  quest's objectives are read once a run - `InternalNumSurvivors` thresholds give the team size (the leader counts;
  "Team Size" == 1 = stay solo), a `FinishSpecificTeamSetup` the classes the team must hold, the full-team objectives
  (`FullTeamTier3Weapons`, `TimedPowerupsFullTeam`) a team of `requiredTeamSize`. At the limit: Liberate 5.0 with
  `quest: <rule> - take the level-up and cash`, every recruit 0.3 with `quest: <rule> - <class> would fail it`. A class
  the quest needs and the squad lacks: that recruit 8.0+ (`quest: needs <class>`), a recruit that would take its slot
  0.3, Liberate 4.0 (`keep the slot for <class>`). More survivors wanted than the squad has: Liberate 0.2 (`Liberate
  leaves a slot empty`), a recruit at least 0.3. A quest whose objectives match *Any*, one the game counts as failed,
  a run that does not fit the quest's conditions (arena, mode, difficulty or leader - the game shows the quest box in
  every run but completes the quest only in one that fits) or a team that has broken the rule already leaves the cards
  as they are (the log says why).
- **The active quest's other objectives** (`QuestRules.cs`), after everything above, on every screen. What
  each objective counts was read in the game's machine code: `FinishWithWeapons` = a player holds one of the weapons
  at the level (in hand when it says so), checked again at every pick; `FinishWithEvolutions` = an evolution of the
  ability held; `FinishWithoutClassAbilities` = no ability of the class, failed for good at the pick;
  `FinishWithoutTier3Weapons` = no tier-3 weapon in hand at the end; `FullyUpgradeClass` = every ability the Skill
  Tree unlocked at its last level (evolved where its evolution is unlocked) and a top-tier weapon maxed;
  `HealingItemCount` = health items the leader holds (the game's own test: its healing flag or the `HealthRelated`
  tag). The weapon line asked for: every step - a level, the next tier, a branch that counts - lifted to 6.8 - 7.15
  (over every ability and tier-up, under a recruit's first weapon 7.2 and every evolution), a little of the card's own
  score kept on top so the favoured branch stays first; a branch the quest does not count, AVOID. An ability or the
  base of an evolution asked for: to 6.7 - 7.15; that evolution +1.0. Health items +2.0 while too few are held;
  Research Pods, Rare+ trainings, armor trainings and synergy recruits the quest counts +2.0. AVOID (0.4) on a pick that
  fails the quest for good: an ability of a forbidden class, a tier-3 weapon under "no tier-3", any item under "no
  items", tag points that reach a cap. A modest lift (+0.3 - 0.6: kills with a class, tag points, evolutions, full
  health, a fully upgraded class; together at most +0.8, a cap's near side -0.8) is written after the card's own
  reasons and never takes a card past one of a build's first three abilities that stood above it. A decisive rule's
  line goes first (`[card]`) and is what the card says; AVOID wins over a lift. A quest that needs a class the squad
  lacks (a weapon, ability or evolution of it, a fully upgraded class, its synergies) also gives the rescue cards
  that class (the team rule above). `[Advice] QuestSteer`: InfoOnly writes the reasons after the cards' own, `(info
  only)`, and moves nothing - the team rule neither; Off follows nothing. The game's own quest item (`CollectItem`,
  +3) follows the switch too.
- **Military training**: `1 + rarity x weight x 2` (Common 1, Rare 2, Legendary 3 - the cards' own numbers go about
  1 : 2 : 3 by rarity: rarity multiplies the stat instead of outvoting it). An Endless card counts its own value
  against the Common card of its stat (`bonusesEndless` / `bonuses` of the card: 0.25 - 0.5, held to 0.1 - 1.0, 0.4
  when unreadable). Weights by the card's real asset name, weapon stats scaled by how
  much of the squad's levels are weapons and ability stats likewise, XP / luck / pickup range by the clock, health
  / armor / regeneration by how much survival matters right now, +20 % when a selected build wants it.

Run history is deliberately not used. The rules are checked offline by the bench (`tools\bench.cmd`, also `--check-log`
for a log, and `tools\advice_audit.py`): [docs/INTERNALS.md](docs/INTERNALS.md#offline-bench).

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
skip the check; a bad download is discarded on a hash or assembly check failure. Since 0.15.0 the feed's release
notes are kept next to the DLL as `notes_<version>.txt`, for a later what's-new card: the new version's when it is
downloaded (or found downloaded), the running version's at a check that finds it the latest; the notes of older
versions are removed. Log lines: `[update] ...` (`release notes of 0.15.0 kept in notes_0.15.0.txt`), `[notice] ...`.

## Build and deploy

```bash
mod\build.cmd
```

That compiles `YazsCompanion.Mod\YazsCompanion.Mod.csproj` (net6.0, matching the .NET 6 runtime
BepInEx ships) and copies `YazsCompanionMod.dll` into `BepInEx\plugins\YazsCompanion\`.
Pass `-p:Deploy=false` to build without copying, `-p:GameDir=...` for another install.
No NuGet packages are needed: it references the BepInEx core and interop DLLs from the game folder. The toolchain,
the release gates (`release.cmd`), how the mod hooks the game and the source layout: [docs/INTERNALS.md](docs/INTERNALS.md).

## Package for Steam Deck / another PC

```bash
mod\package.cmd
```

builds the mod and writes `mod\dist\YazsCompanion-<version>-BepInEx-<build>-SteamDeck-Windows.zip`
(about 53 MB): the exact BepInEx build from this install (core, bundled .NET 6 runtime, `winhttp.dll`,
`doorstop_config.ini`), the Unity base libraries and the interop assemblies already generated for the
current game build (so the Deck needs no download and no slow first launch), `BepInEx\config\BepInEx.cfg`
with the console window turned off, the mod DLL (it writes `knowledge.json` on its first launch), this README and
`CHANGELOG.md`, and a `README-INSTALL.txt` with the Deck steps. Entry names use forward slashes, so Linux extracts real folders.

Install on the Deck: Desktop Mode, extract the zip into the game folder (Steam > Manage > Browse local
files), set the launch option `WINEDLLOVERRIDES="winhttp=n,b" %command%`, use Proton Experimental or 9+,
launch. `BepInEx/LogOutput.log` proves it loaded. Verified on the user's Deck (Wine 11, SD card path
`S:\steamapps\...`): the card verdicts and the log work as on Windows.

## Logs

- `BepInEx\plugins\YazsCompanion\companion.log` — only this mod's lines, appended across launches; each launch starts
  with `==== <date> <time> session start ====`, and a log over 4 MB is moved to `companion.log.1` at load
  (the older copies to `.2` / `.3`, three kept; `[log] the log was 8.4 MB: moved to companion.log.1 ...` after the
  header). What each kind of line says, with examples - the offers, cards and picks, the hints, the WHY band, the
  quest, the run setup screen: [docs/INTERNALS.md](docs/INTERNALS.md#log-lines).
- Once a session (0.15.0): `[menu] first input this session: pad` (`mouse`, `key`, `touch`); `[badge] ribbon stepped
  aside for the Skill Tree label (ribbon alpha ...)`; `[badge] a reason line shrank to fit its card: 16.0 -> 15.2 px
  (...)`; on the main menu `[data] all N names of the Companion's tables are in this game build (...)`, or one warning
  naming what is missing. A Training Yard node bought or refunded between two reads of its tab: `[yard] bought <node>
  1>2 (advice #1)` (or `not advised`), `[yard] refunded <node> 2>1`. Wide menus put back: `[wide] <site> restored
  (<why>): N changes undone, M frame pieces back`.
- `BepInEx\LogOutput.log` — everything BepInEx logged this launch (overwritten per launch).
- Set `Verbose = true` in the config to also log every raw field of every card and survivor
  (`[raw]` lines) when a verdict looks wrong. The debug switches (`[Debug] Screenshots`, `Preview`, `PreviewResolution`,
  `Perf`) are in [docs/INTERNALS.md](docs/INTERNALS.md#debug-switches).

To disable the mod without uninstalling BepInEx, delete or rename `BepInEx\plugins\YazsCompanion\YazsCompanionMod.dll`.
To disable BepInEx entirely, set `enabled = false` in `doorstop_config.ini` in the game folder.

## What the mod costs

The rule (since 0.10.1): during play the mod does nothing per frame beyond a fade and a few time checks, polls cheaply
(a fingerprint of the squad every two seconds; the full snapshot only when it moved), searches the scene for objects
only in fenced-in fallbacks, and does its heavy work while the game is paused anyway: a new plan is worked out in the
second a selection screen takes to animate out, and the first plan of a session is prepared on the main menu
(`Warmup.cs`). From a real run on the PC (3440x1440, 60 fps; Normal to 19:49, three survivors at the end, 74 offers of
all five kinds, 110 minutes of session, no warnings) and the 0.10.2 measurements:

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

## Extensions (for other mods)

Since 0.12.0 another BepInEx plugin can put options into the mod menu and lend build guides; since 0.12.1 it can
also lend the names the Companion shows. The one public class is `YazsCompanion.Api.Extensions`
(`Api/Extensions.cs`); its signatures use BCL types only, so it is called through reflection, without a reference to
this DLL (a plugin that referenced it would not load where the Companion is missing). Nothing changes for anyone
while nothing is registered. Since 0.15.0 the bench freezes these signatures (`tools/ItemBench/api_v3.txt`, read from the
built DLL): a call changes or joins only with a new `ApiVersion`, and the calls of an older version stay as they were.

```csharp
public static int  ApiVersion { get; }      // 3 (0.13.0: build packs' "badges" / "skipBadges", no new call); 2 = 0.12.1; 1 = 0.12.0, the first three calls below
public static void RegisterOption(string owner, string group, string label, string description,
                                  Func<string[]> choices, Func<int> get, Action<int> set);
public static void RegisterBuildProvider(string owner, Func<string, string> buildPackPathForClass);
public static void RegisterDisplayNames(string owner, Func<string, string, string> nameFor);   // 2
public static void InvalidateDisplayNames();                                                   // 2
public static void Unregister(string owner);
```

- **`RegisterOption`** adds a row to the **MODS** tab: `owner` is your mod's display name (the section header),
  `group` a sub-header under it (may be empty), `description` the footer text while the row has the focus.
  `choices()` is asked every time the row is drawn or changed, so the list may change at run time; `get()` is the
  index shown; `set(index)` is called when the player changes the value - save it there. The menu then reads
  `get()` again (for every MODS row: one option may move another) and shows `SAVED - applies at once`, or
  `NOT SAVED` when `set` threw. The same owner + group + label again replaces the row in place.
- **`RegisterBuildProvider`** lends builds. The function is asked with a survivor's name as `builds.json` has it -
  `SWAT`, `Tank`, `Engineer`, `Huntress`, `Ghost` (the class the game's code calls Ninja), `Medic`, `Pyro`,
  `Mechanic`, `Ranger` - and answers the full path of a build pack file, or `null` for "none right now". It is asked
  whenever a survivor's build is resolved (an answer stands for one second: the ranking asks dozens of times per
  offer) and every time the BUILDS tab is drawn or a MODS option changes, so the answer may change at run time.
  Files are parsed once per path and write time. One provider per owner.
- **`RegisterDisplayNames`** lends the names the Companion SHOWS - in the PLAN readout (survivor labels, weapons,
  abilities, evolutions, SOS and GRAB rows), the reasons under the cards, the BUILDS tab (survivors, branches,
  abilities, evolutions, skips, summaries) and the Training Yard strip. `nameFor(kind, key)` answers the name to show,
  or `null` for the game's own:

  | kind | key |
  | --- | --- |
  | `"class"` | the game's class enum name: `SWAT`, `Tank`, `Engineer`, `Huntress`, `Ninja` (the class shown as Ghost), `Medic`, `Pyro`, `Mechanic`, `Ranger` |
  | `"powerup"` | the powerup's asset name (`PowerupBase.name`, e.g. `KatanaUpgrade`) - weapons, abilities, evolutions |
  | `"item"` | the item's asset name (`ItemBase.name`, e.g. `Item_BloodyAxe`); answering `null` for every item is fine |

  Only what is drawn changes: the ranking, the builds (`builds.json` and build packs still name things by their
  English names), the plan's keys and the `[card]` / `[pick]` / `[yard]` log lines keep the game's names, so the
  advice is the same whatever is shown (the `[plan]` log line records the readout as drawn). Names are plain text:
  markup and line breaks are dropped, an empty answer counts as `null`. Where a sentence of the rules names a game
  name ("first weapon for Ghost", "then Katana Splash"), whole words in the game's spelling are swapped; a longer
  game name you did not rename stays whole even when it contains one you did. Every answer is kept - per class,
  powerup and item, until the run ends, a new one starts or the mod menu closes - so `nameFor` is asked about once
  per name and run, always on the game's main thread, and must be quick. Several mods may lend names: they are asked
  in the order they registered and the first answer that is not `null` wins. One function per owner: a second call
  replaces it and keeps its place.
- **`InvalidateDisplayNames`** says your answers changed (another look chosen in your options, say): everything kept
  is asked again, and the PLAN readout on screen is redrawn within two seconds. Cheap; any thread.
- **`Unregister`** takes back everything the owner registered, display names included.
- A call never throws back at you, and your callbacks may throw: each failure is caught and logged once (`[ext]`
  lines; `[builds]` for the pack files). Registrations may come before or after the Companion's own `Load()`, while
  the menu is open, from any thread. Declare the soft dependency below so that the Companion's assembly is loaded
  before your `Load()` looks for it.

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

**A build pack** is a JSON file for ONE survivor: `"builds"` holds the same objects as `"custom"` in `builds.json`
(comments and trailing commas are fine), plus two optional keys:

| key | |
| --- | --- |
| `title` | shown on the cards of these builds (where a preset says GUIDE PICK / ALTERNATIVE) and in the header over them; default: the owner |
| `default` | the `id` or `name` of the build that **Auto** follows for this survivor while the pack is lent; without it Auto stays Auto. In a run (0.13.0), once the survivor owns a tier-3 weapon that is not the default's `branch`, Auto follows the first lent build whose `branch` is that weapon instead, else the first with `"branch": ""`, else plain Auto; a default with `"branch": ""` stays. A build the player selected is never swapped |
| `builds[].id` | your own id, used for `default` and kept in `builds.json` as `ext:<owner>:<id>` when the player selects the build |
| `builds[].name`, `summary` | the card's title, and the footer text while the card has the focus |
| `builds[].glyph` | card art, one of `crosshair bullets blast flame bolt snow flask blade turret shield cross paw gear magnet chevrons clock skull link coin heart hash radio eye diamond up` |
| `builds[].style` | `Weapon` (every weapon level first), `Balanced` or `Ability` (abilities first) |
| `builds[].branch` | the weapon branch - any of the survivor's three tier-3 weapons, by its English name; `""` = decided live |
| `builds[].abilities` | priority order, the first is the build's main ability; abilities left out come after these |
| `builds[].skip` | abilities the build does not want |
| `builds[].evolution` | ability -> the evolution to take when both are offered (`"Kunai Dance: Microbombs"`, or just `"Microbombs"`); an ability left out is decided live |
| `builds[].wants` | item leanings: `weapons abilities critical armor healing slow turret melee`, other `ItemRules` tags, damage types |

Names are the game's English names, as in `Builds.Kits` (`Builds.cs`); they are checked against the survivor's kit
when the file is read, and what does not fit is dropped and named in the log - a misspelt name would otherwise
silently never match. For Ghost (weapon line Katana, Katana Splash, then Thousand Cuts, Windcutter OR Soul Reaper;
abilities Pulsar, Shuriken, Holo-bait, Kunai Dance):

```json
{
  "title": "My Mod",
  "default": "gale",
  "builds": [
    {
      "id": "gale",
      "name": "Gale",
      "summary": "Weapon first: the katana line into Windcutter, Pulsar as the ability to focus.",
      "glyph": "up",
      "style": "Weapon",
      "branch": "Windcutter",
      "abilities": [ "Pulsar", "Shuriken", "Holo-bait", "Kunai Dance" ],
      "skip": [],
      "wants": [ "weapons", "critical", "Slashing" ],
      "evolution": { "Pulsar": "Pulsar: Unstoppable" }
    },
    {
      "id": "bomber",
      "name": "Kunai Bomber",
      "summary": "Abilities first: Kunai Dance to its last level, then Microbombs; the blade fills in.",
      "glyph": "blast",
      "style": "Ability",
      "branch": "",
      "abilities": [ "Kunai Dance", "Shuriken", "Pulsar", "Holo-bait" ],
      "skip": [],
      "wants": [ "abilities", "Explosive", "Slashing" ],
      "evolution": { "Kunai Dance": "Kunai Dance: Microbombs" }
    }
  ]
}
```

Lent builds are listed on the BUILDS tab after the presets, selected and saved like any of them, and followed
through the same `Build` object as a preset - the ranking, the PLAN readout and the Training Yard advice have no
code of their own for them. When the pack goes away (the provider answers `null`, or the mod unregisters) a
survivor that followed one of its builds reads as Auto again, without a word; `builds.json` keeps the selection, so
it is back in force when the pack returns. `[Debug] PreviewMenu` photographs the MODS tab too while there is one.

## Open checks

What has not been seen in the game yet, or waits on a measurement (each version's own checks are in its CHANGELOG
entry). A sent log answers most of them - the lines to look for are named below - and `tools\bench.cmd --check-log
<companion.log>` checks a session's hooks, warnings and card texts.

1. **Steam Deck pad and touch**: the mod menu, the WHY band and the badge advice's cursor driven with the Deck's
   controls and its touch screen. They are built on the game's own `UISubmit` / `Cancel` / `GoNextTab` actions and
   its highlight; the tests so far drove the keyboard and the mouse.
2. **EQUIP ADVICE by touch**: the one-click button of the run setup screen pressed with a finger on the Deck - the
   presses, the guards before each one and UNDO.
3. **The look on wider and taller screens**: the verdicts on what 0.15.0 draws at 21:9 (the WHY panel in the side
   wings) and on the Deck's 16:10 (the wide menus, the card text at 16 px), from screenshots (`[Debug] Screenshots
   = true`).
4. **The quest rules on a kind they handle, live**: a run under a quest whose objective moves the cards (a tier-3
   weapon at max level, a health item, a team size): the `[quest]` lines, the `Quest: ...` line under the card and the
   QUEST row along the run.
5. **Banish logging**: one banish by hand on a chest or a level-up - `[pick] ... banish on ...` and `replaced
   (banish)` on the next offer line.
6. **The badge weights U1 - U3**, not measured yet (tag points against the Hashtag stat, player stats on recruits,
   the crit pools): read from the `[loadout] measure` line of real runs, then set in `knowledge.json`.
7. **LOCKDOWN**: what a lockdown keeps has not been read in the game's code, so the action hints never advise one.
8. **The story progress row, live** (0.15.0): a run under a story quest ("Genesis": `story objective 0 of 5` on the readout,
   stepping with the game's quest box, the `[quest] ... -> story objective ...` line at each step; a Boss Rush one: `story
   objective: the Boss Rush boss`). It widens the PLAN rule "the QUEST row shows only while a rule changes the advice" on purpose:
   the user had "Genesis" active for two sessions without a word from the mod. Whether `_count` reads the game's own count (its
   runtime class is the only source) is what the run proves; `[General] QuestProgress = false` takes the row away.
