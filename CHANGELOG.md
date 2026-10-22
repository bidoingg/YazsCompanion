# Changelog

What changed in each release of the YAZS Companion mod, newest first: what each version changed, why, and how it was
checked. The [README](README.md) describes the mod as it is now. Dates are the release dates on the maintainer's clock
(US Central); the [GitHub releases](https://github.com/bidoingg/YazsCompanion/releases) carry the exact times, the zips
and the one-paragraph notes the auto-updater shows.

## 0.16.5 (2026-10-22) - the survivors' balance pass of the 10-16 update

A test build. The game's patch notes for the 10-16 update (published with the 10-20 rebuild) list a balance pass over
every survivor's weapons and abilities, the badges, Military Training and the missions, besides the items 0.16.4 already
followed. The Companion reads almost all of it from the game while it runs - each mission's objectives and counts, the
Military Training values per rarity, each badge's values, the skill tree's nodes, each weapon's range bonus - so that
advice was already right. What was written by hand before the update is updated here.

### Presets

- **New: Venom Archer** (Huntress, an alternative read from the game's data): Toxic Arrows, which the update raised to the
  other arrows' damage while it cut Explosive and Freezing Arrows. Poison on one chemical tag, next to a chemical Medic
  (Plague Doctor).
- **Windcutter** (Ghost) takes Kunai Dance last: the update cut its damage by about a third and the Holo-bait got stronger.
- Texts that quoted a view from before the update now say so: Bombardier (Explosive Arrows' "highest recorded damage"),
  Shotgunner (the shotguns were cut), Inferno (the Flamethrower was raised), Cold Chain (the car got more cars and more
  range), Ripper (the Chainsaw's damage was doubled).

### Abilities on Auto

- The Remote Control Car is no longer marked down as a guide "C" ability: the update gives it up to 4 cars, more range
  and a stronger Nemesis. It now counts like any ability no guide singles out. The Mechanic's Training Yard plan buys it
  first among the starting abilities (the Transmitter stays "C"). An unedited knowledge.json is refreshed at the first
  start (the old copy is kept as knowledge.json.bak); if you edited yours, delete the car's line under `abilities`.

### The 10-20 rebuild's item texts

- The 10-20 rebuild changed the wording of four item texts (Crowbar, Annoying Trumpet, Barrel Roll, Devil's Deal) and a
  stray full stop (Mana Potion). The advice of all five is unchanged (they have rows in the item book). With the book
  off, Crowbar's new "below 50% HP" no longer reads as a healing item: a health share is not a heal.

### How it was checked, and what was not seen

- The patch notes line by line against the game's data of the 10-20 build (its extract and code), the Companion's code,
  its knowledge defaults and its docs; the bench's game data is now the 10-20 extract (the item texts the game draws), and
  IB10 holds Crowbar's card to the new words. Bench (full, `--strict`, `--no-data`) all as wanted; the Mechanic's Yard
  plan in `yard_015_expected.txt` changes for the car above only.
- Not seen in the game yet: the new preset, the Mechanic's Yard plan.
- Not modelled (no measured data yet): Hardcore's more frequent Military Training points, and main bosses' 300-900 damage
  hits that pass through the invincibility after a hit.

## 0.16.4 (2026-10-17) - the items of the game's 10-16 update

A test build. On 2026-10-16 the game was updated without a new version number (still 1.0.2): 47 chest items got new
numbers or new effects, and 20 item texts now name a partner ("When equipped with ...": 18 pairs). The Companion's chest
advice was still reading the old texts; it now follows the new ones - and where the game itself does something other
than a text says, it follows the game. Nothing else changed.

### The cards say today's numbers

- Hit Tracks `+40 speed, slows on you halved` (was +20), Toilet Paper `+30 speed; ...` (was +20), Camping Set `Up to +75%
  damage while standing still` (was +50%), Fishing Pole `... then +100% damage` (was +50%), Devil's Deal `+30% survivor
  stats, ...` (was +25%), Broken Glass `+30% damage to slowed enemies` (was +25%), Treasure Finder `Swap at any chest: +3
  rerolls, banishes` (was +1 reroll and +1 banish).
- New words where an item does something new: ACME Anvil `+50% damage to slowed or stunned enemies`, Annoying Trumpet and
  Barrel Roll `An Explosive blast and stun ...`, Suspicious Pendrive `Opening a chest kills basic enemies`, Crowbar `+30%
  damage below half HP; quick chests`, Heavy Metal `... +10% pickup range`, Rock And Roll `... +10 luck`, Quick Buck `+20%
  cash; ...`, Zugzwang Hypergaster XD `+5 to low tags, doubles a 6, -2 to high`, A Cookie `+20% healing; a skip heals half
  max HP`.
- Unchanged on purpose: Glass of Milk (its cap is now 250 instead of 300, rarely reached), Pocket Watch (it now lasts
  longer; its words stay true) and Retaliation ("Max 1 stack") keep their advice.

### Rules that follow the new texts

- **Toilet Paper** counts its own +30 speed when it works out the Chemical damage your speed gives (`+40% Chemical damage
  from your speed` at 150 speed, was +35%).
- **Zugzwang Hypergaster XD**: a damage type at exactly 6 tags now doubles (to 12) instead of losing 2, so the card can
  now switch on that type's 10-tag effect (`Turns on the Ice 10-tag effect`).
- **Walkie Talkie** is no longer halved in a full squad while you hold Power of Friendship (`Not halved: you hold Power of
  Friendship`), and a Power of Friendship card in a full squad counts the half it gives back (`Keeps your Walkie Talkie
  whole`). With one or two survivors nothing is halved, so nothing changes there.
- **Stretcher** no longer costs dodge.
- **Crowbar** now gives +30% damage while you are below half health; it was rated as an item that only saves time. The old
  note in knowledge.json ("saves time, nothing else") is gone: an unedited knowledge.json is refreshed at the first start
  (the old copy is kept as knowledge.json.bak); if you edited yours, delete Crowbar's line under `itemNotes`.
- **A Cookie held**: a skip now heals half your max HP more (was 500), on a Research Pod too (the update added it there),
  so the SKIP hint counts the bigger heal (`the skip heals 730 more (A Cookie)` on a skip that heals 292 by itself).

### The new item pairs, each worth what it does

- **The pair weight** (`Pairs with your Barrel Roll`, as the pairs before): Annoying Trumpet and Barrel Roll, Camping Set and
  Stretcher, Positive Attitude and Retaliation, Pills and MedKit.
- **An extra +20%** counts less, like the item's own part of the same kind: Quick Buck and Farming Tools (+20% cash, weighed
  as cash), Teddy Bear and Rock And Roll or Ban Hammer (+20% chance of a free reroll or banish; one of the two is enough, the
  second adds nothing).
- **Tag points**: each Icon and its status item (Cinder and Spoil Canister, Pestilence and Plague's Visage, Stillness and
  Special Snowflake, Tempest and Jacob's Ladder) and Explosive Surprise and The Bomb add +6 to a damage type tag together
  (both items' tag bonus doubled). They count as tag points, so the card says `Turns on the Fire 10-tag effect` when they
  take a type to 10.
- **Bleeding Edge and Slingshot pair with each other.** Their texts name Bloody Axe and Nine Inch Nails, but the game's own
  data links Bleeding Edge to Slingshot and Slingshot to Bleeding Edge (+6 Slashing and +6 Kinetic with both held), and
  nothing to Bloody Axe or Nine Inch Nails. The Companion follows the game.
- **Both effects**: with both items of a conversion pair held, a damage type applies both statuses - as the game's code does
  it, which differs from two of the texts. Fire with Fire Extinguisher and Warm Ice Cream (Burn and Freeze), Chemical with
  Biofuel Energy and Battery Leakage (Toxify and Electrify), Kinetic and Slashing with Nail Bat and Expired Sushi. Battery
  Leakage's Electric still causes Toxify only (its text says both), so `No Electrify while you hold Battery Leakage` still
  shows with the pair held; Warm Ice Cream's Ice applies both with Biofuel Energy, not with Fire Extinguisher (its text
  names Fire Extinguisher). A pair is worth as much as your squad deals the damage type it changes; a status item counts
  every cause it has, and Boiling Pot counts both statuses.

### Items worth more

- Where the update made an item much stronger and no guide rates it, the Companion's own estimate went up (still at most
  like a guide B item, still marked `(unrated)`): Suspicious Pendrive, ACME Anvil, Annoying Trumpet, Barrel Roll, Power of
  Friendship, Treasure Finder, Camping Set, Crowbar, Quick Buck, A Cookie, Hit Tracks, Broken Glass, Rock And Roll, MedKit
  and Mouse Trap. +10 luck counts the same on every item (Ban Hammer's, Teddy Bear's, Rock And Roll's; Power of
  Friendship's +20 twice that). Items a guide rates keep that rating (Accumulator, Fishing Pole, Frozen Heart, Detective's
  Pipe, Power Glove, Heavy Metal, Pickup Pick, Mana Potion ...): the guides have not rated the update yet.

### How it was checked, and what was not seen

- Compiled with 0 warnings; bench (full, `--strict`, `--no-data`) all as wanted, with the new cases IB10 (every number on
  the cards, every stated pair and every changed rule held to the item texts the game draws, from its own data files),
  IB11 (the changed rules, each new pair at its worth, Teddy Bear's either, the tag points), IB12 (where the game's own
  code and data differ from the texts: the conversion pairs, Bleeding Edge and Slingshot) and CV8 (both effects); the item
  table of the four reference squads refreshed for the changes above only; the public API unchanged (`api_v3.txt`). The
  bench's game data is now the updated build's: its data files and a live probe of it. `[Debug] Probe` now also writes
  each item's partner as the game links it, and the bench holds the pairs to it.
- Not seen in the game yet: a chest with one of the changed items, a new pair, a held conversion pair.

## 0.16.3 (2026-10-12) - the REROLL line placed once, rows that only show a value

The first test build (Beta updates On). Three small fixes from the PC sessions of 10-11 and 10-12; the advice and everything
else drawn in play are unchanged.

### The REROLL / SKIP line stays where it is drawn

- The line over the hinted button (`RECOMMENDED: REROLL  -  ...`) was placed the frame after the cards came, then moved down
  once the screen had flown in, about 0.6 s later: on 5 of 5 first draws in the 10-11 PC session (4 chests and 1 rescue
  screen) and 2 of 2 on 10-12 - the band over the button went from 85 to 111 units at 3440 x 1440. Now the line waits those
  0.6 s and is drawn once, where the screen has settled. The button's gold frame still comes with the cards (0.16.1's one
  gold frame per screen); a verdict that comes later (judged again, a reroll's new cards) is drawn at once, as before.
- The log: the `[squad] action hint: ... drawn over the Reroll button - ...` line (`reroll hint:` on the rescue screen) ends
  `, 0.60 s after the cards`; the `the line moved / came / went as the screen settled` lines are gone.

### Rows of other mods that only show a value

- An option another mod adds to the MODS tab with one value only (a status: which build runs, how the session went) is shown
  as information: its value without the `<` `>` arrows of a choice, and left / right, select, a click or a touch on it change
  nothing. Up to 0.16.2 such rows looked like a choice that did nothing: each press "changed" the value to itself, lit
  `SAVED` and wrote a log line (43 in under three minutes on 10-12). The row keeps the focus, so its help can be read at the
  bottom of the menu, and that help is no longer typed on again from nothing when the other mod refreshes the row while it has
  the focus. Rows with two or more values work as before.
- The log: `[ext] '<mod>', <group> / <label>: one choice - shown as information (no arrows; left / right and select change
  nothing)` once per row and session. For mod authors: register the row again to show another value (README, Extensions).

### Characters the menu font cannot draw

- A character in another mod's words that the menu font cannot draw was shown as a box (a backslash in a help text on 10-12).
  It is now drawn as a close stand-in - `/` for a backslash, straight quotes for curly ones, `-` for a long dash, `...` for an
  ellipsis - or left out. The font is asked once per character, while the MODS tab is drawn; nothing in play.
- The log: `[ext] '<mod>': 1 character(s) the menu font cannot draw ('\' as '/') - in '<group> / <label>'` once per mod
  (again only for a character not named yet).

### How it was checked, and what was not seen

- Compiled with 0 warnings; bench (full, `--strict`, `--no-data`) all as wanted, with the new cases LA1 - LA2 (the line's
  time: first draw, judged again, a later verdict, new words; the 10-11 screen replayed - placed twice in 0.16.2, once now),
  CI1 - CI2 (no path from a one-choice row to its mod's `set`; the help not typed on again when the row is rebuilt under the
  focus) and CG1 - CG4 (a help text with a path, as on 10-12, with a font that lacks the backslash; the stand-ins, markup
  and line breaks kept, the font asked once per character); the public API unchanged (`api_v3.txt`).
- Not seen in the game yet: a REROLL or SKIP hint's line drawn once (`, 0.60 s after the cards`, no `as the screen settled`
  line), the MODS tab's one-value rows without arrows, and a stand-in for a missing character.

## 0.16.2 (2026-10-12) - Beta updates: test builds, and the way back

From this release on, new builds can go out as test builds (GitHub pre-releases) before they become the release, and one
switch says which you take. 0.16.2 is the first build that knows both feeds and the first that can step back to a release;
it changes no advice and nothing drawn in play.

### The switch: DISPLAY > Beta updates

- The last row of the mod menu's DISPLAY tab, **Beta updates**: `On - newest builds` (the default) takes the newest published
  build, test builds included - a release that follows replaces them; `Off - releases only` takes releases only, and a test
  build that runs now is swapped for the current release at the next start. In the cfg: `[Update] BetaUpdates = true`.
- The environment variable `YAZS_BETA` overrides it: `0` / `off` / `false` / `no` = releases only, `1` / `on` / `true` / `yes` =
  test builds (any case; anything else is ignored). On the Steam Deck it goes first in the launch options; the row then says
  `Set by the launch options (YAZS_BETA=0)` and its arrows do nothing.
- The row's help says what runs and what the switch would do: `This mod: 0.16.3, a test build (release 0.16.2, newest 0.16.3) -
  up to date, checked 19:42. Off would put the release 0.16.2 back at the next start.`; after a change `The release 0.16.2 is
  downloaded and checked; the test build 0.16.3 is set aside - the release runs from the next start.`; a development build,
  auto-update off, offline, a machine that cannot set a build aside each say so. It ends with the rescue for when the game cannot
  reach the menu: on Windows `set BetaUpdates = false under [Update] in BepInEx\config\bidoi.yazs.companion.cfg`, under Proton
  `put YAZS_BETA=0 first in the launch options` (the mod tells the two apart).
- A change is checked at once (the download is done before the restart) and says `SAVED - applies at the next start`; the tab's
  lead line says so too. The menu header names the kind of build: `v0.16.3  TEST BUILD`, `v0.16.4  DEV`, a release bare.
- The restart strip names the kind (`YAZS COMPANION 0.16.2 (RELEASE) DOWNLOADED - RESTART THE GAME TO APPLY`) and is drawn on
  the menus only: hidden in a run, back on the pause menu.

### Two feeds, one check

- The release feed is unchanged (`releases/latest/download/latest.json`). The test builds' feed is new:
  `releases/download/beta/latest.json`, the newest published build (a release or a test build) plus a `withdrawn` list. Both are
  fetched side by side at every check; latest.json also carries the build's `commit` now. Older Companions ignore both fields and
  never see a test build.
- The check moved to the top of the load, before the knowledge, the API and every hook: a build that fails further down still
  fetches its fix (or, with Off, the release).
- One line per check says it all: `[update] channel beta (Beta updates on): release 0.16.2, newest 0.16.3, running 0.16.3 = test
  build - up to date`. A test-builds feed that does not answer moves nothing (`the test-builds feed did not answer`; the build
  stays `test build`) and is asked once more a minute later; a missing one (404) means no test build is out.
- A hand-set `UpdateUrl` is checked alone and only ever moves forward, as before.

### Going back

- Off on a published test build, or a build the maintainer withdraws, steps back: the lower build is downloaded, its SHA-256 and
  its own version stamp checked, and only then the running file is renamed `<file>.off` (the next start loads the lower build and
  removes the .off). Turned back On before the restart, the file is put back.
- The kept release: when a test build is downloaded while the release runs, the release is copied beside it at once as
  `YazsCompanionMod-<version>.dll.keep` (about 1 MB), so going back needs no download - and a launcher can put it back before
  the game starts, even when a test build fails before the menu. It is only ever the release: every check that reaches the
  release feed notes that build in `update_release.txt` (version and SHA-256), and a `.keep` is kept only while it is that
  build - at most one, never just the next older build; a start that runs the release again drops it.
- A newer build downloaded while On is set aside when the switch goes Off before the restart (and a withdrawn download likewise),
  so it cannot win the next start - also when a feed does not answer that check.
- Where a loaded file cannot be renamed, the build keeps running, a `<file>.offnext` request is left for a launcher (the release
  waits beside it, so the next start downloads nothing), the restart strip does not promise a restart, and the help says how to
  do it by hand. Whether renaming works is probed once per build at the first start (`[update] setting this build aside works
  here` or `... is refused here (why)`) - before it is ever needed.
- A withdrawn build is never downloaded, kept or called up to date, also in the seconds after a pull when the release feed still
  names it.
- Never below 0.16.2 (the floor): the first build that can step back.

### For the maintainer

- `release.ps1 -Beta` publishes a test build (`--prerelease`, never the latest release, no Drive copy, above the newest published
  build); `-Promote <version> -VerdictFile <file>` makes a test build the release without a rebuild (refused without a
  PROMOTE-READY verdict that names the version, whose commit is the tag's, that is not older than the evidence it names or the
  PC's companion.log, and that judged exactly the Deck upload folders `-DeckLogs` holds now - a late upload is caught by its
  folder; `-Force` overrides); `-Pull <version>` withdraws a build (the feed steps every updater back, the latest
  release moves when needed, the release becomes a draft; refused when nothing at or above 0.16.2 would remain). Every publish
  re-points the test builds' feed (the first creates the rolling prerelease `beta`). A new `channel` gate checks the floor and the
  feed. `-TestState` lets the dry runs try made-up GitHub states.
- The release's local deploy judges the highest `YazsCompanionMod*.dll` of the PC's plugin folder (after a step back that is the
  versioned file), puts the release in as `YazsCompanionMod.dll` and sets the others aside; for a test build only while the PC's
  switch is On.
- Development builds are left alone by the updater: a commit with `-dirty`, a version published from another commit, or the
  `dev_build.txt` that `build.cmd`'s deploy now writes beside the DLL (its version and SHA-256; release.ps1's local deploy -
  also when the PC already runs the bytes it publishes - and the updater's own installs remove it, and it gives way once a feed
  publishes the very bytes that run). The deploy first removes downloaded and set-aside builds (no tie with a download of the
  same version), and refuses while the PC's switch is Off unless `-p:DeployOverRelease=true`.
- `tools\ItemBench\channel_truth.txt`: the switch's one truth table (YAZS_BETA x the cfg x a missing key -> channel), read by
  every program that follows the switch. `tools\updater_drill.ps1`: the real check against a localhost feed.

### How it was checked, and what was not seen

- Bench (`--strict` and `--no-data`): UpdaterCases UP1 - UP11 - the decision table, the labels, YAZS_BETA, the 103 rows of the
  truth table for the Companion and an outside reader, latest.json and the https rule (plain http only from a feed at
  `http://localhost`), development builds, the words of every state, a scratch plugin folder, whole checks over in-memory feeds
  (download, step back with and without the kept release, refusals, withdrawn builds, a failed feed and its retry, the dev
  marker), and a real loaded DLL set aside and put back. The drill (`tools\updater_drill.ps1`): 18 scenes against a real HTTP
  feed. release.ps1's dry runs of a release, a test build, a promotion and a pull; the deploy guard against made-up cfgs.
- Seen in a PC launch on 10-12 (a development build, so the updater left it alone, as it should): `[update] setting this build
  aside works here`, `[update] the release is 0.16.1 (kept in update_release.txt ...)` and `[update] channel beta (Beta updates
  on): release 0.16.1, newest none, running 0.16.2 = development build`. Not seen yet: the DISPLAY row on screen, a real step
  back to a release, and the Steam Deck - its probe line answers whether a build can be set aside under Proton (the Steam Deck's
  updater sets one aside by renames before the game starts either way).

## 0.16.1 (2026-10-11) - seen live

Fixes from the first PC session with 0.16.0 (10-10, 3440 x 1440): a chest line that named the wrong gain and a WHY whose
text size changed from card to card, plus the answers given on 10-10 - mark the items no guide rates, AVOID only where a
card costs something, one gold frame per screen, a link between the 21:9 WHY panel and its card, and the quest rows in
the game's own words. The 10-11 PC session ran it; see the end of this entry for what it showed.

### The chest cards' words

- **What an item gives, named right**: an item scored for the run's economy said "XP and luck" whatever it gave. Live at
  11:57:59 on 10-10 Power of Friendship (+10 Luck, no XP) read `More XP and luck - pays off all run (17:29 left)`; it now
  reads `More luck - pays off all run`. The 16 items with an economy gain or cost name their own: luck (Power of
  Friendship, Ban Hammer, Teddy Bear, Last Unicorn, Pawn Shop Receipt), XP (99'th Balloon, Access Keycard, Emerald Gem,
  Jailbroken Phone, Jewel of Death, Wooden Stick, Gold Medal's cost), level-ups (Black Box, Hijacked Signal, Reserve
  Bench), XP and training points (Skip Rope); an item a newer game build adds reads it from its own text.
- **Items no guide rates are marked**: where a guide-rated item says its tier (`B-tier - ...`), an item rated by the
  Companion's own estimate says `... (unrated)` - `Halves some hits, more in a big team (unrated)` -, and its WHY starts
  with `Unrated by the guides - the Companion's estimate from its text`. On a narrow card (the Steam Deck's) the cue gives
  way to the item's words, as a guide tier does; the WHY says it every time. The cap stays: no unrated item ranks above a
  guide B pick. The mark moves no card.
- **AVOID only where a card costs something**: an item under 1 that takes nothing from the squad (it is only weak for
  it) stays at 1.00, grey, without AVOID; a guide C tier then reads `C-tier in the guides`. Up to 0.16.0 Brave Toaster read
  `AVOID   Stronger status effects - suits Huntress` on a Huntress squad. AVOID stays where the item costs the squad
  something: a damage type or a stat it uses, health, a clash with a held item (`Clashes with your Devil's Deal`), a
  10-tag effect switched off, Glass Cannon without an Energy Shield, the cooldown reduction Mana Potion switches off, a
  conversion that stops a held item's status, the empty slot of Wooden Stick or Empty Chest. On the item book's reference
  squads 20 scores moved, every one from under 1 to 1.00; no other score changed. Ability and stat cards are as they were
  (a stat card that does nothing still reads AVOID).
- The SKIP hint of a screen whose every card is under 1 says `every card here would hurt this squad` only when each card
  costs something, else `every card here is weak for this squad - take the skip bonus` (`every card is weak` under the
  button).
- From the 10-11 Steam Deck run: the Liberate card reads `Level-up and cash, no recruit` where its long line ended in
  "..."; an item line too long for a narrow card takes a short form (`An Explosive grenade every 10s`, not `Throws an
  Explosive grenade every`) and a cut line never ends on a word like "every" or "under a"; a name another mod lends for a
  weapon no longer changes a military card's name (`Master Blaster`); Magazine Clip says what it does: `Doubles every
  magazine - your guns use magazines`, not `The squad reloads magazines`.

### One gold frame per screen

Up to 0.16.0 a REROLL or SKIP hint framed its button in gold while the best card kept its own gold frame and RECOMMENDED
ribbon - two gold frames saying opposite things -, and the hint came 0.6 s after the cards (on the 10-07 Deck round 1 of
4 hints was followed; on 10-10 the framed card was taken 8 s after a REROLL hint).

- When the hint says REROLL or SKIP, the gold frame goes to that button, and the line over it reads `RECOMMENDED: REROLL  -
  the best card here is weak for this squad`. The best card gives its frame up: its ribbon turns grey and reads `IF YOU
  KEEP`, over the card's own line, grey too. When the hint goes and the screen stays, the card's frame and ribbon come
  back.
- The frame comes with the cards; the line follows a frame later and is measured again once the screen has flown in
  (0.6 s).
- Under its button (1280 x 800, the short words) the line reads as in 0.16.0 (`REROLL  -  a weak offer (2 left)`), so the
  WHY band keeps its room.
- BANISH (off by default) keeps the gold frame on the best card, which stays the pick; the Banish button's frame is grey
  now.
- The gold frame's bottom line stops either side of the military training card's plate too (0.16.0: level-up and rescue
  cards).

### The WHY

- **One size on every card**: the WHY's text size was read off the selected card, which the game enlarges while it is
  selected - on 10-10 the first card's WHY read 21.6 px and the others' 20.5 px. It is now taken from the card at rest:
  20.5 px at 1440p, 16 px on the Steam Deck, the same on every card of a screen.
- **A link to its card on 21:9**: the WHY panel in the side wing can stand far from its card (card 3 of 4 is explained in
  the left wing). A small gold jewel - a gold diamond with a dark ring and a gold core - now sits on the top edge of the
  selected card, and the panel's end diamond on the cards' side becomes the same jewel, at the same size. Only with the
  side panel; the band under the cards needs none.

### The QUEST row in the game's words

The PLAN readout's info-only QUEST row says what the game's quest box says:

- a story quest: `Broken Vials 3 of 5` (0.15.0 - 0.16.0: `story objective 3 of 5`, seen live under "Genesis" on 10-10),
  `Broken Vials, done`; a Boss Rush story quest names its boss: `Defeat Tower`;
- the other counted objectives are on the row now too (0.16.0 only logged them): `Liberate Survivors 1 of 3`, `Open Item
  Chests 4 of 7`, `Collect Cash 12 of 30`, `Defeat Elite Bosses 0 of 3`;
- two rows at most, the story first, open ones before met ones; dim and info only as before (no card moves for them),
  switched by `[General] QuestProgress` as before. A count over ten rebuilds the readout once per tenth of the way
  (Collect Cash 30: every third pickup), not at every step.

The log keeps the ids: `[quest] GameHubQuest_Main_06 -> story objective main_story_objective_q6: 3 of 5 - progress only`.

### EQUIP ADVICE in the advice's order, and by controller

- EQUIP ADVICE (off by default) now leaves the slots in the advice's order, slot 1 = badge 1, also when some badges are
  equipped already; up to 0.16.0 the new badges went to the end of the row (from full slots on 10-07 it read 4 2 1 3), so
  on the Deck the slots were emptied by hand first. It takes up to 8 presses (about 1 s); UNDO puts the old badges back
  in their old slots, and a controller's Y (`· Y` on the plate) or A on the plate, reached with the d-pad, presses it.

### From the 10-11 PC session

- **Extermination's clock**: in Extermination the advice thought the run was almost over from the second wave. It took
  the time left as the share of waves still to play times the game's goal number (`TimeRequiredForSuccess`), which in
  that mode is not the run's length: on 10-11 it counted `~3 level-ups to come` at wave 2 of 20 and `~2` at wave 3, and
  about 50 came. So the rescue cards put Liberate over strong recruits (`Too late for a recruit to grow (wave 3/20)`;
  the player took the recruit both times), about a hundred card lines said `too late` or `little time left`, evolutions
  read as out of reach, and the PLAN's SOS row said Liberate from the first wave. It was so in every version since
  0.10.0. The time left is now the waves still to play times a wave's length - 42 s (that run's average, 20 waves in
  14:03) until the run has finished waves of its own, then blended with its own pace -, with `[Advice] ModeAware` off
  too; the game's number is not read for it. Extermination's level-up pace starts from that run's 3.9 a minute, not
  Normal's 2.9. With 42 s waves a recruit keeps its full value to about wave 16; Liberate passes a weak recruit at about
  wave 18 and says `Too late for a recruit to grow` on the last wave. Every other mode reads the clock as before.
- **One 'Boosted by' reason**: a WHY could say `Boosted by <passive> (<survivor>)` and `Boosted by <passive>` side by
  side for one team passive (16 WHY bands on 10-11, and in every log since 10-08), spending one of its four reasons. It
  says it once, with the survivor - on an evolution without it, as before, so a card whose own line is `Evolution -
  boosted by <passive>` does not say it again under its WHY.

### For the maintainer

- The pause walk (`[Debug] PreviewPause`) draws a REROLL and then a SKIP hint on its first offer, before the hover tour,
  as real hints draw them, with a shot each (`hint_reroll`, `hint_skip`); the run clock stands meanwhile, and no
  `[squad] ... SHOWN` line is written for them (advice_audit.py counts those).
- New log lines: `[badge] LevelUp 02:31: REROLL advised - the gold frame on the Reroll button; #1 <card> gave it up: 'IF
  YOU KEEP' over its line '...'` once per offer and verdict, then `[badge] ...: the gold frame back on #1 <card> (the hint
  went)`; `[squad] action hint: the line moved as the screen settled - now ...` (`came` / `went`) when the 0.6 s measure
  changed it; once a session `[why] link marker: ...`, and `, diamond link` after `card N of M` on the side panel's
  `[why]` lines; a weak item's `[card]` reason `weak here, but it costs nothing (no AVOID)`; the all-AVOID SKIP's `| every
  card is under 1 (AVOID; none costs the squad anything: weak)`; the quest's read line ends `| counted: N (progress on the
  readout)`; an Extermination offer's `[ctx]` line ends `..., ~38 s a wave, game goal 55 s)` (ahead of `, health N%` and
  `, survive quest`): the wave length the time left used and the game's `TimeRequiredForSuccess`, which the next
  Extermination log shows for the first time (`waves not read: knowledge goal 1200 s, game goal ...` when the waves could
  not be read).
- `build.cmd` refuses to deploy while the game runs (`error YAZSDEPLOY: DEPLOY REFUSED ...`): nothing is copied, the
  compiled DLL stays in `bin\Release`; `-p:Deploy=false` never asks.
- The bench: `SeenLiveCases.cs` (SL1 - SL4: the economy word, the estimate cue, AVOID only with a cost, the SKIP words;
  the 10-10 11:57:59 chest replayed), `QuestWordsCases.cs` (Q6a: the 16 counted objectives' words, steps and rows; every
  row of the game's 53 events within 40 characters), `GeometryCases.cs` G15 - G18 (one gold frame in 44 hint states, the
  line widths, the room of IF YOU KEEP, one WHY size, the link jewel's place) and G13's military plate;
  `itembook_expected.txt` rewritten for the cue and the 1.00 floor; 0.16.0 joins the released list; `PcRoundCases.cs` (made-up
  runs only: XH1 - XH8 Extermination's time left at wave 3 of 20 and over the last waves, the wave-1 and waves-not-read
  guards, ModeAware off, 2,304 contexts of the other modes read as 0.16.0 read them, the `[ctx]` line, the pace prior, the
  sources; BO1 one 'Boosted by' reason, none under an evolution whose line says it).

### Choices made for room

Two of the answers of 10-10 needed a form; either can change:

- The cue reads `(unrated)`, not "no guide rating": with the longer words 24 of the 73 unrated items could not carry it
  on a PC card; with `(unrated)` every one can, and about 6 in 10 on the Steam Deck's first card (the WHY says it on
  every one).
- `IF YOU KEEP` stands in the ribbon's place, not at the head of the card's line: there it cost 14 characters, and half
  the Steam Deck's lines lost words.

### How it was checked, and what was not seen

Checked offline: the bench with `--strict`, in full and without game data (as the GitHub workflow runs it) - all as
wanted -, and `--check-log` over the 10-10 PC session (42 hooks patched, 0 warnings, 0 errors, 276 card texts on the
rails, 5 chest offers each with its `[items]` line).

That session (0.16.0, 11:42 - 12:16, three runs at 3440 x 1440) showed four things 0.16.0 was released without seeing
live: the chest item advice (5 chests, every item with a rule), the WHY side panel at 21:9 (168 views), the story row
stepping from 0 to 2 of 5 under "Genesis", and the Training Yard following the badge advice (8 of 9 purchases the
advised #1).

Seen in the 10-11 PC session (0.16.1, 19:42 - 20:56, an Extermination win and an Endless run at 3440 x 1440;
`--check-log`: all as wanted): EQUIP ADVICE put badges chosen out of order into slots 1 2 3 4 (4 and 8 presses) and UNDO
gave the row back exactly twice, by pad (A and Y both press it); the gold frame on the REROLL button with the grey IF YOU
KEEP ribbon in the same millisecond as the hint (7 of 7); one WHY size, 20.5 px, on all 542 side lines, each with the
link marker; `(unrated)` on 33 card texts and never on a guide-tiered item; `More luck` and Magazine Clip's line.

Not yet seen in the game:

- **The jewel by eye** on the selected card at 21:9 - whether it covers anything of the card's top (the log has it, no
  shot does).
- **`C-tier in the guides`** on a real chest card; on the Steam Deck, whether the WHY's `Unrated by the guides` reason
  leaves room for another (the band there shows one or two).
- **The QUEST row's new words** under "Genesis" and under a quest that counts liberations, chests, cash or elites.
- **EQUIP ADVICE** with one or two badges already equipped, UNDO by mouse, and on the Steam Deck the Y button from the
  camp and from the main menu's Play.
- **Extermination's clock**: at wave 3 of 20 the `[ctx]` line should count about 40 - 50 level-ups to come, the rescue
  cards should put recruits first until the last waves, and the line shows the game's goal number for the first time.

## 0.16.0 (2026-10-10) - every item

Every chest item read by its own text, the items the squad holds in the advice, and the fixes of the 10-07 Steam Deck
round for the 1280 x 800 screen. Two parts of it have not been seen in a live run yet - the WHY side panel on a 21:9
screen and the chest item advice in the game; see the end of this entry.

### Chest cards read every item by its own text

- **A rule per chest item**: the 134 items of the 1.0.2 chest pool are scored by their own 1.0.2 texts (the item book)
  instead of keyword patterns. On the 150 logged chests the top card changes on 22. Lines stop claiming what an item does
  not do:
  - max HP, a revive or a health cost is no longer read as healing for the Medic ('More healing - suits Medic' under
    Frozen Heart, Electric Personality, Chocolate Box, Devil's Deal, Glass of Milk, Jewel of Life, Plot Armor, Pills, Last
    Unicorn);
  - 'damage' is no longer counted for words about damage you take or a damage type ('More damage' under Plot Armor,
    Power of Friendship, Omnigeode, the conversion items); the blanket damage bonus is gone, so most guide-rated items
    score about 0.2 lower;
  - items that deal damage of their own are no longer cut because nobody on the squad deals that type ('Nobody on the
    squad deals Electric' under Accumulator; Boiling Pot, Great Nade - no more 'no grenade on the squad' for its own
    grenade -, Annoying Trumpet, Barrel Roll, Suspicious Pendrive): the squad's tags of that type raise them instead;
  - a cost is no longer read as a gain (Gold Medal's slower levels as XP, Life Savings' cash that heals as cash, Farming
    Tools' class points as XP);
  - the wrong keyword: The Word, Frying Pan and Sugar Rush are not elite / boss items; Hit Tracks halves slows on you, it
    does not slow enemies; Fishing Pole parries, it does not dodge; A Cookie, Black Box, Hijacked Signal and Jailbroken
    Phone are not upgrade-quality items; Pills, Nuclear Fusion, Metal Gear and Wrench are not pickup-range items;
    Potato's hidden text is no longer XP and luck; Modchip, Solar Panel and Power Generator work with crits but give none
    ('More crits'), Broken Glass and ACME Anvil work with slows but slow nothing, Gaslighter and Jailbroken Phone work with
    status effects / taunts;
  - effects the keywords never saw are scored: Well Prepped, Pocket Watch, Zugzwang Hypergaster XD, Combat Knife,
    Omnigeode's and Magical Hat's tag grants, and the values that depend on the run (Empty Chest and Wooden Stick per
    empty item slot, Jade Amulet per luck, the gems per damage type at 3 tags);
  - Bulletproof Vest's 'enemy projectiles deal -50 %' is no longer read as a malus.
- Items no guide rates are scored from their own numbers and say what they do ('+30% damage to basic enemies'); for now
  none ranks above a guide B pick, and none is ever shown with a tier.
- New card lines: 'Pairs with The Word', 'Clashes with your Devil's Deal', 'Needs 3 status effects, the squad has 2',
  'Halved by your Smooth Moves; shots miss'. The pairs and clashes the item texts state are built in; knowledge.json gains
  `itemClashes` for your own.
- **The item book reads the run**: free item slots, luck, pickup range and movement speed are read once per snapshot
  from the team statistics. Empty Chest, Wooden Stick, Jade Amulet, Metal Gear, Wrench, Compass and Toilet Paper say what
  they give in this run ("+20% damage now (2 empty slots)", "+20% damage at your luck") instead of the per-unit rule. The
  free slots use the game's own formula, InternalMaxItems - InternalNumEquippedItems (team statistics 43 - 49; `max -
  equipped` in the log). That formula is what GamePlayer.AddItem writes into statistic 50, and it is right before the
  run's first item too; statistic 50 (team and leader) is only logged. Every chest offer logs `[items] held: <items> |
  free slots N (max - equipped) | luck L | pickup P% | speed S`, so chest replays are exact.
- **Second lives are worth keeping**: Jewel of Life and Plot Armor, the only second-life items in the chest pool, rank
  at least like an A-tier item, and higher while your quest asks you to survive the run or the squad is under 60 %
  health: 'A second life - the squad is at 55%' / 'A second life - survive the run (quest)'. On the 10-07 Deck chest
  Jewel of Life scored 1.27 and a REROLL hint sent it away two minutes before a defeat on a Survive quest; it now scores
  3.95 and is the recommended card. The `[ctx]` line of an offer names the squad's health under 100 % and a followed
  survive quest.
- `[Debug] ItemKeywords = true` scores chests the 0.15.0 way, for comparison.
- knowledge.json gains `itemClashes` (empty) and `yardRules` (the Training Yard's badge floor and reach): a
  knowledge.json you never edited is refreshed on the first launch of 0.16.0 (`knowledge.json refreshed to this build's
  defaults`, the previous copy kept as `knowledge.json.bak`); an edited one keeps its text and the code's defaults apply.

### Held items change the advice

- **What the squad holds** is asked of one table (`HeldRules.cs`), read once a snapshot (`HeldRead.cs`): the items held
  (by name and asset), the item slots by the game's own formula (`GamePlayer.AddItem` keeps team statistic 50 = 43 - 49;
  read in its machine code), the leader's ability cooldown reduction, the seconds since the last survivor joined, the
  abilities' cooldowns. `[held] ...` after every `[ctx]` line, `[held] reads: ...` once a run. Until now the only
  held-item rule was the pairs.
- **Mana Potion held**: the game zeroes every ability cooldown reduction stat bonus while it is held
  (`GamePlayer.GetStatisticFinalValue`, read in machine code) - a Chem-Light Battery card (any rarity, and the level-up's
  Endless one) now shows AVOID `Does nothing while you hold Mana Potion` (0.80); Chick Magnet keeps half its worth,
  Hyperactivity three quarters, Devil's Deal 90 %; Grenade Trail: Shrapnel -0.3. Chick Magnet's card says `Its cooldown
  bonus does nothing with Mana Potion`.
- **The Mana Potion card** weighs what it gives (an ability reset every 4 s, spread over the abilities with a cooldown)
  against what it costs (the squad's cooldown reduction, switched off), from the live cooldowns: it keeps or raises its
  A tier on a squad without cooldown reduction (`Resets an ability every 4s - 55% more casts here`) and falls to AVOID
  once the reduction is high (`Switches off your 60% ability cooldown reduction`). Up to 0.15.0 it scored 3.12 whatever
  the squad had invested.
- **Reserve Bench held**: the skip of a weak Item Chest or Military Training is a level-up (the game's GrantLevelUp on
  those two skips, read in machine code) - REROLL while a reroll is left, then `SKIP - the skip is a level-up (Reserve
  Bench) - worth more than these`; a recruit brings a level-up per 2 minutes since the last join, so late recruits keep
  beating Liberate (`Recruiting gives 3 level-ups (Reserve Bench)`), the PLAN's SOS row and the rescue reroll hint
  included.
- **Hijacked Signal held**: Liberate gives two level-ups (`Two level-ups and cash (Hijacked Signal)`, one more level-up
  in its score; the SOS row says `Liberate - two level-ups` once it beats every recruit); a chest item pays the skip's
  cash.
- **Life Savings held**: granted cash heals instead - the skip's worth counts it as a heal, Liberate says `A level-up and
  a heal instead of a recruit`, cash items are weighed as healing (`Its cash heals you instead (Life Savings)`).
- **A Cookie and Skip Rope held**: the skip's worth counts A Cookie's 500 extra health and Skip Rope's points (5 make a
  Military Training point); once no reroll is left they can tip a weak screen to a SKIP that names them. The skip's worth
  (`ScreenCall.SkipWorth`) is generalised for these; every 0.15.0 value is unchanged (pinned over 19,250 calls).
- **Wooden Stick / Empty Chest held**: an item that fills an empty slot costs the slot's 10 % XP (1.6 x the run's
  economy) or 10 % damage (2.0): AVOID `Fills an empty slot: -10% XP (Wooden Stick)` where that takes the card under 1, a
  WHY reason otherwise; no cost for a second copy of an item held, with no empty slot or more than 8; Well Prepped `Adds
  the slot it takes - keeps your empty slots`; the PLAN's GRAB row counts it too. The chest REROLL hint is judged on the
  best card before the cost; a good chest whose cards all cost the slot says `SKIP - an empty slot is worth more than
  these (Wooden Stick)`.
- **Last Unicorn** counts the animal items held, itself included (the game's own isAnimalItem flag): +0.9 per animal item
  (two at most), `Counts 3 animal items: +30 Luck, +15% damage`; an animal item `Pairs with your Last Unicorn` while it
  is held. (The pairs the item texts name - The Word + Pocket Watch, Omnigeode + Ruby Gem / Sapphire Gem - came with the
  item book.)
- **Status conversions held** (Fire Extinguisher ... Expired Sushi): a status item is judged by what still causes its
  status on this squad (`No Electrify while you hold Battery Leakage` / `Your Electric now causes Toxify (Battery
  Leakage)`), and a conversion item offered while a status item is held says what it would stop or feed (`Would stop
  your Jacob's Ladder's Electrify`, -0.9; +0.6 for a fed one). Up to 0.15.0 a Jacob's Ladder kept its Electric fit
  beside a Battery Leakage.
- `[Debug] HeldPretend`: the advice treats the named items as held (a test switch; a Warning once a session).
- The WHY band lists a held item's reason first.
- Held items with no rule (checked in machine code): Black Box, Ring Of Power, Ban Hammer, Rock And Roll, Teddy Bear,
  Jailbroken Phone with Mouse Trap, Gold Medal (beyond Skip Rope's points); the replace / swap items wait for the item
  replace advice.

### The hints never send a second life away

- While the quest asks to survive the run or the squad is under 60 % health, no REROLL and no SKIP hint sends away a
  second life on the cards (Jewel of Life, Plot Armor, a new Resuscitation) - the 10-07 Deck chest's REROLL sent Jewel of
  Life away about 2 minutes before the defeat on a Survive quest; a BANISH never names a second life. The no-item quest's
  SKIP keeps its place.

### The cards and the WHY

- **Rare and Legendary stat cards are weighed by what they give**. A military training card counts its own value against
  the Common card of its stat at every rarity - the list the game applies for the card's rarity - not a flat 2 for Rare
  and 3 for Legendary: in the game's data a Rare is 1.9 - 2.5 times its Common card (Chem-Light Battery, UAV System and
  Energy Awareness 2.5) and a Legendary 3 - 5 times (Chem-Light Battery, UAV System, Energy Awareness, Fast Mover and
  Armor Piercing 5). Replayed on the 41 logged military offers with a Rare or Legendary card, the first card changes once
  (a Rare Chem-Light Battery, +25 % ability cooldown, over a Rare Dear John). The log gives the weight once per rarity and
  stat: `[rank] Rare stat cards: weight x2.50 for Ability Cooldown (the card's own +0.25 vs Common +0.1)`.
- **A Research Pod card that reaches a 10-tag effect counts it as much as the squad deals that type**. Each effect works
  through its own type's status or explosions, so its +1.5 is now scaled by the card's fit: a type nobody on the squad
  deals - whose card already said "but nobody deals" it - scores 2.50 where it scored 4.00. Replayed on the 19 logged
  Research Pod offers (14 screens, rerolls counted), the first card changes twice (Electric at 44 % of the squad's damage
  over a Chemical card at 11 % that reaches its effect; Explosive at 57 % over Slashing at 22 %).
- **Ties go to the build's order**. Two cards with the same score on a level-up, chest, military training or Research Pod
  screen went to the card further left, so a build's second ability could stand RECOMMENDED over its first ("A core
  ability of the Bombardier build" framed over "The Bombardier build's main ability") and the PLAN readout named it as
  next. Ties now go to the ability higher in its owner's build, then the stat card with more of its own value (the
  level-up's Endless filler after any other card), then the card whose damage types carry more of the squad's tag points
  (a Research Pod card: its type, fewer under "Spread"), then the card further left; the PLAN readout follows the same
  order. Replayed on the offers logged since 0.13.0, 10 of the 20 exact ties for the first place change their first card
  (a 21st, on a Research Pod screen, no longer ties under the fit rule above). The log says each one: `[rank] tie for #1
  at 4.81: Bombing Strike before Minefield - the build's order (#1 before #2)`.
- **The WHY band says an exact tie as one**: `Either works - even with Minefield; this one is higher in the build's
  order`, where it said "a hair ahead" of a card with the very same score; a third card with that score says the same.
- **An evolution card says when it closes the PLAN's preferred one**. The PLAN readout names the evolution that fits the
  squad better ("evolve Helicopter Strike (Chemtrails preferred)"); when the other one is offered and the build names no
  evolution for the base, its WHY band now starts with `Closes Chemtrails - the PLAN's preferred one` and its `[card]`
  line adds `closes Helicopter Strike: Chemtrails (the PLAN's preferred evolution, fit <a> vs <b>)`. One rule for both
  (fits at least 0.25 apart); a toss-up says neither. The score is unchanged: an evolution still outranks the rest of its
  offer.
- **The WHY panel in the side wing names the card it explains**: its first line reads the lead and the card's name as
  the card shows it (`WHY  Remote Control Car`; the name on a line or two of its own when both do not fit), so a panel
  that stands beside a neighbouring card (the middle cards of a four-card offer) is not read as that card's (the 10-07
  review). No panel moved; where the panel goes on four-card screens is still open. The `[why]` line says which card of
  the row it speaks for (`..., 3 of 3 shown, card 2 of 3`) and `, header in 2 rows` when the name took a row of its own;
  `header + N lines` now counts the reasons' lines alone.
- **The gold RECOMMENDED frame no longer runs through the card's NEW / TIER / RECRUIT plate**: its bottom line stops
  either side of the plate, as the game's own highlight does (seen on every offer since 0.13.0). Weapon and ability cards
  and rescue cards (the plate is the image the card's TIER / NEW / RECRUIT label is written on, `AddonDescription`);
  chest, military and Research Pod cards keep the whole line until their plate is known. The log says once per card
  class what it measured (`[badge] plate: UIPowerupButtonSkill AddonDescription 400x150 units, y -782..-632 (the frame's
  bottom edge -726..-720) - edge cut -211..209`); a plate not found or not on the edge also lists, once per card class,
  what does lie on the edge (`[badge] plate dump: ...`). The first build of 0.16.0 measured the card's level diamond on the portrait instead
  and never cut (the 10-07 PC series: `no overlap, edge whole` on every card, the gold line still through RECRUIT); the
  plate was found in the game's own card layout and is checked on the bench with its numbers.
- **At 1280 x 800 the WHY band uses two 15 px lines before it leaves a reason out**: where the cards' own size is within
  2 px of the 15 px floor (the Deck's 16 - 16.8 px), the band tries the lines it wants at 15 px (with the tighter pad in
  a 139-unit band) before one line at the larger size, and counts the lines it wants in the band it gets (a band the
  REROLL line cut asks for more). On the 10-07 Deck run about 40 of 97 bands lost reasons; the model of that run says
  about 2 would now. When an item still drops, the band says the other card's lead in the side panel's short form
  (`Medical Drone goes first`) when that shows more (`..., the short "goes first"` in the `[why]` line). The PC (20.5 px
  at 1440p, 5.5 px over the floor) keeps every layout. A 1080p desktop (16 px) is near the floor too and takes the same
  order.
- **At 1280 x 800 the REROLL / SKIP line under its button no longer takes the WHY band's room** (the 10-07 Deck round:
  one rescue card had no WHY at all, chests showed 1 - 2 of their reasons). The line drawn under its button - only there;
  over the button (the PC) nothing changes - says its short words: `REROLL - a weak offer`, `REROLL - Tank fits better`,
  `SKIP - every card hurts`, about half as wide (956 -> 484 units on a chest, 874 -> 614 on the rescue screen). And the
  WHY takes the selected card's own part of the band only when it holds its first reason at 15 px, else the band's
  widest part: the rescue card's 640-unit part left of the line gives way to the 1028 (1348 with the short words) right
  of it. The log: `[squad] action hint: ... drawn under the Reroll button (...), short form - 484 x 74 units`, the
  `[why]` line `..., the widest stretch` when the WHY moved off the card's own part.

### The Training Yard and the run setup screen

- **The Training Yard plans badges by the badge advice** (`[Advice] YardBadges`, on by default; ADVICE tab: Training Yard
  badges - Follow the badge advice / As their ranks open): a survivor's badge nodes follow the run setup screen's badge
  advice, worked out for every survivor you can lead (the mode and difficulty of your last run setup visit, else Normal
  I). A badge is unlocked and levelled when that advice would equip it and its levels pay for their points; a badge
  investment that only pays at a higher level (Growth Badge 1>3: no survivor's advice equips it at level 2) is planned
  whole - it is never partly bought, THEN asks to save for the whole chunk (`save 3 more for Growth Badge 1>3`) and the
  leftover points still go to cheaper steps; a badge no survivor's advice equips is held until every other open step of
  the tab is done. This changes the leftover rule for badges: on 10-06 the Medic tab's last point bought Healing Badge
  0>1, a level no advice equips (it pays from level 4) - now that point stays and Healing Badge comes last. WHY on a
  waiting badge says why ("The badge advice equips it only from level 4 (Normal I). (step 39 of 39)"). The run setup
  screen's TRAINING YARD row (LoadoutHint = Full) stays for its leader alone and can differ from the plan. Off = every
  badge to level 1 as its rank opens (0.15). Checked offline: the 0.15 plan of a made-up tree and of the 9 survivor trees
  is unchanged with the switch off, the 10-06 Medic replay, the approximation within 0.933 points per point of a full
  re-advice and the same best level in 108 of 108 rows.
- **SELECT LOADOUT at 1280 x 800**: the SWAP OUT / FILL THE FREE SLOTS / ALL EQUIPPED part stays on the EQUIP row in a
  shorter form (first without the EQUIP row's ranks - the grid's markers show them -, then with a short label: OUT, KEEP,
  FILL THE FREE SLOTS, ALL EQUIPPED) instead of being dropped (`[loadout] drawn ... swap row joined to row 1 (form 2: the
  ranks left to the markers; ...)`); the marker digits are 15 px (14 px before on the Deck's cells; the PC keeps its
  20 px).

### For the maintainer

- Debug screenshots: the second step of the results flow (the stats screen) is captured now - game 1.0.2 never calls the
  hook it waited for.
- Debug: `[Debug] PreviewPauseRecruit` (for scripted test runs with `PreviewPause`): one hero joins the pause walk's run
  3 s in, so its level-ups offer four cards and the hover tour reaches cards 3 and 4.
- The pause walk grants its first level-up with the game's own `ExperienceProgress.Debug_LevelUp()` when none has come
  by 20 s of play with the recruit (36 s without): the walk's squad stands still, and with a second hero shooting the
  zombies fall outside the pickup ring - both walks of the first 0.16.0 series gathered 0 of 100 XP in 42 s and ended
  without an offer. Once a session; the line says where the XP gems lay. The walk's wait for an offer is bounded at
  41 s of play now (was 48 s): with the ~3 s to its done line and a test script's kill a few seconds later, a killed
  walk stays under the ~50 s after which the game writes a save.
- `[Debug] Perf` splits the run setup screen's first draw (89 - 103 ms in one frame in the 10-07 series) into its parts
  (`loadout.draw.first`, `loadout.draw.measure` / `.layout` / `.marks` / `.summary` / `.why` / `.equip`), so the next
  round can fix the biggest one. A debug walk also logs the sums so far just before its done line: the Training Yard and
  run setup walks end before the first once-a-minute line.
- **Proof report**: `--check-log` gains a seventh check, `items` (every chest offer with its `[items] held:` line, no
  item without a rule), and lists the proofs a log holds after its checks, as INFO sections (first input, Training Yard
  purchase, EQUIP press with its source, the ribbon, the quest story step, the walk proofs). `--expect kind,kind` makes
  them checks. The EQUIP ADVICE / UNDO line now says how it was pressed (`pressed by touch`, `mouse` or `key`).
- **advice_audit.py** reports the Training Yard per session (purchases against the advice shown, which place was bought,
  badge levels bought while held), also for a session without a run, and the exact ties for the first place by rule;
  `--json` carries them under `numbers.yard` / `numbers.ties`.
- `release.ps1` also puts the released DLL on the maintainer's PC (only with the game closed, no updater download
  waiting and no newer build there; `-NoLocalDeploy` skips it): a pre-release build of the same version stayed on the PC,
  and the updater, which compares versions only, could not replace it.
- The bench: the part without game data (the GitHub workflow) runs the pure cases of every 0.16.0 section;
  `ItemBench --write-itembook` rewrites the item book's parity table after an intended rule change.

### A request for the release notes: take one of these items once

The held-item rules stand on the game's machine code and on `[Debug] HeldPretend`, which proves the advice but not what
the game does. No logged session has ever held Mana Potion, Reserve Bench, Wooden Stick, Hijacked Signal, Life Savings,
Empty Chest, A Cookie or Gold Medal. If a chest offers Reserve Bench (first choice), Mana Potion, Hijacked Signal, Life
Savings or Wooden Stick in a normal run, please take it once even when it is not the #1 card, skip one weak chest
afterwards, play on and send the log. It is read for:

- Reserve Bench: a skipped weak chest or training gives `[pick] Chest mm:ss: skip / nothing`, then an `[offer] LevelUp` at
  the same clock; after a rescue pick (`[pick] CharacterRescue mm:ss: <class>`), as many `[offer] LevelUp` lines as the
  card's "recruiting gives n" (n = whole 2-minute steps since the last join, from `[held] reads`).
- Mana Potion: the next `[held] reads` / `[held]` lines show the leader's ability cooldown reduction at 0 %; a Chem-Light
  Battery card says AVOID.
- Hijacked Signal: two `[offer] LevelUp` lines after a Liberate pick.
- Life Savings: after a skip, the next `[ctx]` line's health is higher by about the cash shown, and the cash counter on
  screen does not move (press the mark key there).
- Wooden Stick / Empty Chest: `[held] reads: item slots N of M filled, K empty (team statistic: K)` matches the slots on
  the game's screen.
- A Cookie: health after a skip rises by at least 500 more than the plain skip heal.
- A status conversion item (Battery Leakage, which you already take): a marked frame of an enemy showing the converted
  status.

### Defaults this release ships with

Four choices were left at their defaults while no answer had come; each can change in a later release:

- Items no guide rates are capped at a guide B pick (above).
- The two second-life items, Jewel of Life and Plot Armor, are the one exception to that cap: at least like an A-tier
  item (above).
- The chest REROLL floor stays 2.5 (2.45 was proposed). The item book scores most items a little lower, so on the 150
  logged chests the REROLL hint shows on 49 where 0.15.0 showed it on 40.
- The Training Yard follows the badge advice by default: every survivor you can lead counts equally, for the mode and
  difficulty of your last run setup visit (else Normal I), with a badge floor of 0.15 (ADVICE tab: Training Yard badges -
  As their ranks open keeps 0.15.0's plan).

### How it was checked, and what was not seen

Checked offline: the bench with `--strict` (all as wanted), its part without game data as the GitHub workflow runs it,
`advice_audit.py --self-test`, and the name scan of the tree.

Checked in a scripted PC series of the 0.16.0 source (10-08 04:38 - 10-09 23:57, about 340 game sessions; the builds
were still numbered 0.15.0, and the released DLL differs from the last of them only in its number): 42 hooks patched
with 0 failed classes, and `--check-log` over the series' sessions all as wanted (the sessions with the test switch
`[Debug] HeldPretend` left out: their one Warning says the switch is on). What ran in the game:

- the mod menu walk at 3440 x 1440 (20 shots, every one at 3440 x 1440): the ADVICE tab's Training Yard badges row;
- the pause walk with a recruit, so its level-ups offer four cards, in a true 1280 x 800 window and at 1024 x 768: the
  first level-up granted by the walk, the hover tour over cards 2 to 4, and at 1280 x 800 a WHY band that left no
  reason out on any of the four cards (0.15.0 on the Deck: 41 of 97 bands lost one);
- the gold RECOMMENDED frame cut round the card's plate on level-up and rescue cards, measured as the bench predicts;
- the results flow's stats step (both results steps captured), the rescue screen's reroll hint, the Training Yard walks
  at both sizes and the run setup screen's joined swap row at 1280 x 800;
- the held-item rules with `[Debug] HeldPretend` (Mana Potion, Reserve Bench, Wooden Stick, Hijacked Signal, Life Savings,
  Skip Rope with A Cookie, Battery Leakage, Pocket Watch with The Word) - three card lines did not come up in the draws:
  a Chem-Light Battery card under Mana Potion, Wooden Stick's slot cost and Life Savings' Liberate line -, and the
  `[held] reads` line in real runs, also after a Try Again.

Not yet seen in a live run - released anyway:

- **The WHY side panel on a 21:9 screen** (headed with its card's name). Every pause walk of the series ran at 1024 x 768
  or 1280 x 800, where the side wing does not open. Its layout is checked on the bench at 3440 x 1440, 2560 x 1080 and
  5120 x 1440, and the side wing itself has drawn since 0.15.0; where it goes on four-card screens is still open.
- **The chest item advice in the game.** No chest was offered in the series' runs, so the `[items]` lines and the item
  book's card texts have not been seen live; they are replayed on the 150 logged chests on the bench. A failure there is
  caught and logged: the chest's cards would show without the mod's ranking, the game goes on.
- **The Steam Deck itself**: the WHY band and the REROLL / SKIP line's short words at 16 px, EQUIP ADVICE by touch and
  SELECT LOADOUT's joined row on the Deck's own screen (the PC's 1280 x 800 window showed the band and the joined row).
  The Deck's Companion takes this release through its updater, and its next session checks them there.

## 0.15.0 (2026-10-07) - accurate on 1.0.2

Accurate on the game's 1.0.2, readable on every screen, and a release that checks itself.

- **Card text size** (DISPLAY tab, `[General] BadgeScale`): Auto / 90% / 115% / 130% / 150% - a share of what Auto draws
  on that screen, so 115% is larger than Auto on a monitor (20.5 -> 23.6 px at 1440p) as on the Steam Deck (16 -> 18.4 px)
  -, with a reason line and the WHY band at their real size in the menu's preview and the size of every step on this
  screen in the row's help; a step that would draw what the screen shows already is passed over. The WHY band and the
  REROLL / SKIP hint follow it, and nothing of it is drawn under 15 px. Auto reaches 16 px on the Steam Deck now (x1.56;
  0.14.0 stopped at x1.3, about 13 px); a 16:9 screen under 1080 px (1280 x 720) keeps the x1.3 cap of a 16:9 canvas and
  reads 15 px. Larger text holds fewer words: each card's line is sized to the room its size leaves (56 characters at
  the PC's size, 42 on the Deck) and picks a shorter form - a line still too wide shrinks to 15 px before the ellipsis
  would cut it (`[badge] a reason line shrank to fit its card: ...`, once a session). The RECOMMENDED ribbon stops growing
  at x1.3, so the reason line under it stays above the game's divider over the buttons up to the largest size (x1.85).
- **The WHY in a side wing** on screens wider than 16:9: where the wide menus opened the wings, a panel beside the
  selected card - its lead (`WHY` / `CLOSE CALL`) on a line of its own, then up to four reasons at the cards' own reason
  size, one reason a line (a long one wraps onto a second line and the panel grows upward for it, so no reason is left
  out for wrapping - the series of 10-06 showed 2 of 3 on three WHYs when wrapped lines were counted), clear of the
  cards, the buttons, the team panel and the skip reward (`[why] ... (side wing left, header + 3 lines, 21.6 px, ...)`);
  16:9, the Deck's 16:10 and WideMenus off keep the band as it was. The band's lines are measured as drawn: its end diamond no longer stands over the last glyph (seen at 3440x1440).
  At 1920 x 1080 a four-button level-up gets its WHY line (one line at 15 px with a tighter pad) where 0.14.0 found no
  room for it; every other band stays as it was.
- **The ADVICE and DISPLAY tabs scroll** like the BUILDS and MODS tabs (with the focus, the mouse wheel, the gold arrows):
  eleven rows show at once, where they always stood, and a twelfth scrolls in - DISPLAY has twelve now (Card text size),
  ADVICE twelve while another mod lends builds (the Lent builds' row below; without one it keeps its eleven).
- **The game's 1.0.2 names**: 1.0.2 renamed five Ranger evolutions (Falcon: Guardian / Assault, Good Boy: Dobermann,
  Animal Whistle: Panic / Rally), and a build's evolution pick among them lost its bonus without a word. The kits and
  presets have the new names, a name match falls back to the powerup's asset (a rename alone no longer breaks one), and
  once a session, on the main menu, the Companion's names are checked against the game's class data: `[data] all N names
  of the Companion's tables are in this game build`, or one warning naming what is missing; a lent build pack or a build
  of your own with a name the game lacks gets a `[builds]` line per name.
- **A lent build's level-up style, said and switchable** (your answer of 10-06): a build another mod lends, followed
  through Auto, keeps its own level-up style by default - as in 0.14.0, but no longer silently: the cards say `Level 3
  of 4 - abilities first, the build's style`, the WHY band `Abilities first, the <build> build's style`, the PLAN
  `Handgun later - the build's style`, wherever it differs from your Level-up style; the BUILDS tab's Auto and VIA AUTO
  cards name whose style it is. A new ADVICE row **Lent builds' level-up style** (`[Advice] LentBuildStyle`: The
  build's style / Mine) lets yours win there too, for the cards, the PLAN and the badge advice. The cfg text of
  `LevelUpStyle` now says exactly when it applies (it said "for survivors on Auto" while a lent build's style won).
  The `[ctx]` line names the source: `(Ability, Auto, the build's own)`, `(Balanced, Auto, mine)`.
- **Live wording fixes** from the 10-06 run: the WHY of a weapon level ranked first under "abilities first" no longer
  says "the build levels abilities first" against its own rank - it says what decided (`Resuscitation is 4th in the
  build's order`, `No ability on this offer`, `The abilities on offer rank lower here`), and the mirror under "weapons
  first" says the clock; another card is named whole (`Bombing Strike: Bioweapon goes first`, not a bare `Bioweapon`
  - the run drew `Frost goes first` for an evolution named "...: Frost"), an evolution's own part only on a card of the
  same base; an evolution of the build's main or a core ability, when the build names no evolution for it, reads
  `Evolves the <build> build's main ability` instead of `Evolution - a big step up`; the PLAN's TAGS row says a met
  10-tag effect (`Kinetic 31 - effect on`). The WHY's "your style levels abilities first" for a survivor on plain Auto
  shows now (0.14.0 wrote it as "your level-up style: ...", which its own rails left out).
- **Story quests on the readout**: a story quest, which no card is steered by ("Genesis": "Broken Vials 0/5" on the game's
  quest box), had no word from the mod; the PLAN's QUEST row now shows its progress, dim and info only - `story objective 3
  of 5`, `story objective: the Boss Rush boss` - stepping with the game's count, never on a selection screen, and not when
  the run cannot complete the quest (`[General] QuestProgress`, on). The `[quest]` lines name every counted objective's id
  and count (`1. CustomGameplayEvent (Live) id main_story_objective_q6 0/5 - progress only`; event counts and kills of a
  rank too) and log each step once.
- **A steadier '~N level-ups to come'**: the pace behind it starts from the mode's measured pace (from the logged runs:
  Normal 2.9 level-ups a minute, Hardcore 4.7, Endless 3.2, Boss Rush 4.6), takes in the run's own as level-ups come (the
  measured pace counts as four of them) and is smoothed. The 10-06 run read 49 - 76 in its first two minutes and 66 - 77 at
  3:00; replayed, it reads 54 - 58 from 0:59 to 3:00, and over a whole logged Normal run the estimate lands nearer the
  level-ups that really came (off by 8 on average instead of 14). Hardcore no longer starts at 2.5 a minute.
- **Proof in a sent log**: once a session, `[menu] first input this session: pad` (or `mouse`, `key`, `touch`) and
  `[badge] ribbon stepped aside for the Skill Tree label (ribbon alpha ...)`; a Training Yard node bought or refunded
  between two reads of its tab logs `[yard] bought <node> 1>2 (advice #1)` (or `not advised`) / `[yard] refunded <node>
  2>1`, so whether the yard advice is followed shows; the pause walk turns "Menus on wide screens" off and back and
  logs the `[wide] ... restored (...)` line and the re-apply.
- **The release notes kept on disk**: the auto-updater keeps the feed's notes next to the DLL as `notes_<version>.txt`
  (the new version's when it is downloaded, the running version's when the feed names it the latest; older versions'
  notes are removed), for a later what's-new card.
- **The load line names the build**: `YAZS Companion 0.15.0 (abc1234) loaded from ...` - the commit stamped into the DLL
  (`-dirty` when it was built from uncommitted changes).
- For the maintainer: `release.ps1` runs gates before anything is tagged or published (the name scan, a clean tree and a
  new tag, its own build with the exit code checked, the DLL's identity, the bench with `--strict`, the package's
  contents, the name scan of the zip and the notes, a logged-in `gh`; `-DryRun` runs them alone); the bench's last line
  is its verdict (`bench: all as wanted`, exit 0), its part without game data runs on GitHub on every push
  (`.github/workflows/bench.yml`), the extension API is frozen in `tools/ItemBench/api_v3.txt`, and every kit and preset
  name is checked exactly against the 1.0.2 data (`--live-probe` compares a newer game build's probe, `--packs` checks
  lent build packs); `ItemBench --check-log` says PASS / FAIL per check for a log, `tools/advice_audit.py` what the player
  did with the advice; the README lost its release history to this file and its "Next steps" to a short "Open checks"
  list.


Checked in two scripted series on the PC (10-06 23:07 - 10-07 08:12; the 3440x1440 monitor and a true 1280x800 window):
the pause, menu and run setup walks at both sizes (the ADVICE / DISPLAY scroll lines and shots, Card text size Auto -> 150% at
21 -> 31 px on the PC and Auto -> 130% at 16 -> 19 px in the Deck window, `[badge] scale s=1.56 px=16.0 ... 42 characters` at
1280x800, the wide menus Off / On cycle, the run setup's slot markers on their own layer), a level-up at both sizes with the WHY
in the left and the right wing at 21:9 (a hover over cards 2 and 3, `4 of 4 shown`) and the band at 16:10, the rescue screen's
SOS badges and WHY panel, the results screen at 21:9, and 0 Companion warnings over the series' sessions. The bench: all as
wanted. Not seen yet: a four-card offer's third card (it keeps the left wing), the first-input line on a real pad, and the
Training Yard purchase lines - all for the next real sessions.

## 0.14.0 (2026-10-05) - the review of a live run

- **the reason under a card keeps its `>`**: an ability's level step was logged as `focus 2>3 of 4` and drawn as
  `focus 23 of 4` - the line passed a filter that dropped both angle brackets, on about a quarter of all card lines
  since 0.10.0. The filter now drops only `<` (the one that opens a text tag), and the step reads `2 to 3 of 4`, like
  the weapon line;
- **the reason under a card in plain words**. The card drew the ranking's own headline, written for the log (`focus
  2>3 of 4 toward its evolution - #2 in Rifleman`, `Endless: Ability Size - Rifleman (Auto) wants it`, `B-tier item`);
  it now says what the pick does and why, in the game's words, every number with what it counts, in 50 characters at
  most: `Level 3 of 4 - evolution unlocks at level 4`, `The Rifleman build's main ability`, `The Rifleman build wants
  ability area`, `Mixed: boosts Ice, weakens Kinetic and Explosive`, `Unlocked synergy with Medic - A-tier in the
  guides`, `Turns on the Ice 10-tag effect`. Each line is measured as it will be drawn - beside its `2ND` / `AVOID`, and
  with the names another mod lends (they run up to 19 characters longer than the game's) - and two cards of one offer
  never draw the same line: the lower one says its next reason. The `[card]` log lines keep the headline as before; a
  new `[shown]` line logs what was drawn. With it: the RECOMMENDED ribbon steps aside while the game shows its own
  `Skill Tree 3 / 5` label under the hovered card; the reason line follows the card's own scale (about 13 px on the Steam Deck, 12 before); the
  reroll hint says `REROLL - Tank would fit this squad better` (the scores stay in the log); SELECT LOADOUT says
  `level 2 · score 6.8`, `replaces Thunder (+17 score)`, `Hardcore: survival picks count 31% more`, `+20% Kinetic, +2
  Kinetic tags`; the PLAN readout says `next Pod: Explosive` and `max tier`, draws `»` where its font lacks `›`, and orders
  a survivor's row the way the cards will rank it (`Handgun later` while the abilities come first); the DISPLAY tab
  explains `TAGS` / `SOS` / `GRAB`;
- **Endless stat cards are weighed by what they give**. The Endless cards of a level-up are the game's filler (Ability
  Area +2.5 % where the Common card gives +10 %), but they were scored like a near-Legendary (x2.6) and came first over
  the build's own abilities: in a logged run `Zone of Action` 6.07 over `Experiment 21` 4.72 (the player took the
  ability), and the player overrode 8 of the 13 such offers in the logs. An Endless card now counts its own value
  against the Common card of its stat (a quarter to a half; 0.4 when it cannot be read) - that card scores 1.49.
  Common, Rare and Legendary are unchanged; two Endless cards of different stats may swap places. The log gives the
  weight once per stat: `[rank] Endless stat cards: weight x0.25 for Ability Size (the card's own +0.025 vs Common +0.1)`;
- **a class rank reached mid-run no longer opens an ability**. Abilities of a class rank (II at class level 20, III at
  40, ...) come only if the survivor had the rank when the run began, and the class level climbs during a run: once it
  passed 40 the readout named a rank III ability as `next` for the rest of the run (`next Resuscitation` on a Medic
  that started at 37 - the card never came in eleven offers), and "abilities first" lost its weapon lift with it. The
  level a survivor had at its first sight in a run now counts until the next run;
- **`AVOID` in words**: a card scored under 1 said so only by the dull red of its line; it now reads `AVOID   ...` in
  place of its `2ND` / `3RD` (the colour stays as the second cue) - its reason keeps 48 of the 56 characters the Steam
  Deck shows;
- **companion.log is rotated**: at load, a log over 4 MB moves to `companion.log.1` (the older copies to `.2` and
  `.3`, three kept) and a fresh one begins with the session header; the line after it says so (`[log] the log was
  8.4 MB: moved to companion.log.1 ...`);
- the `an active quest (...) and no forced badge on the screen ... (worth a look)` note is a `[Logging] Verbose` line
  now - it came on every quest's first setup visit, and most quests have no badge objective;
- **the active quest steers the cards** (`QuestRules.cs`). A quest asks more than a team size: a weapon finished at
  max level, an ability or an evolution held at the end, a health item, kills with one survivor, tag points, Rare
  trainings, armor - or that something is NOT taken. Up to 0.13.0 all of it was logged `advice unchanged`: in a logged
  run under the Engineer's *Heroic Theory* (finish with the tier-3 weapon at max level in hand, 2000 kills with the
  Engineer) the Taser came last of three on every offer under an "abilities first" build, and the player took it
  against the advice. Now what the quest asks for comes first with a `Quest: ...` line under the card - `Quest: leads
  to the tier-3 weapon (Level 2 of 4)` on the Taser and the Tesla, `Quest: max the tier-3 weapon (Level 2 of 4)` on the
  tier-3 weapon itself, over the abilities (under evolutions and a recruit's first weapon), `Quest: hold a health item
  (0 of 1)` (+2.0 until one is held), `Quest: Electric tags to 40 (22 now)`; a pick that would fail it reads AVOID
  (`Quest: any Pyro ability fails it`, `Quest: taking any item fails it`, a tier-3 branch the quest does not count);
  kills with a survivor and the like weigh a little and never pass the build's own core abilities. The build and its tag plan stay. The PLAN readout gets a `QUEST` row while a rule changes something
  (`QUEST  Taser to tier 3, max level · Engineer kills to 2000`). A new ADVICE-tab row, **The active quest**
  (`[Advice] QuestSteer`: On by default / Info only - the row and the log, no card moves / Off).
- **WHY on highlight and CLOSE CALL** (`WhyText.cs`, `WhyUi.cs`). A card's line says one thing; while a card is
  selected (the mouse over it, or the controller's focus on it) a band under the cards says the rest of its verdict in
  plain words - up to four more reasons, and what the first card has that puts it first (`WHY  Medical Drone goes
  first: A core ability of the Rifleman build  /  The Rifleman build levels abilities first  /  Kinetic is 100% of your
  damage`); when the first two cards are less than 0.20 apart both say `CLOSE CALL` (`CLOSE CALL  Either works -
  Medical Drone is a hair ahead`). On a card a quest decided the quest comes first and the build's own order is not
  argued against it. It goes with the selection; the DISPLAY tab's **WHY band on the selected card** row (`[General]
  ShowWhy`) switches it.
- **action hints on every selection screen** (`ScreenCall.cs`). The rescue screen's reroll hint, generalised: a chest,
  a level-up, a training or a Research Pod whose best card is weak for the screen says `REROLL  -  the best card here
  is weak for this squad` over the Reroll button (`(2 rerolls left)` when few are) - a chest under 2.5 on the cards'
  own scale (the user rerolled such chests by hand; at 3.0 the hint would have spoken on 70 of the 147 logged chests,
  65 of them taken as offered); `SKIP` when every card would hurt the squad or the skip's cash and heal are worth more;
  `BANISH` for a card the build skips (off by default: a banish is for the whole run; never the recommended card). One
  hint at most per screen, never a pressed button. On the Skip button, next to the team panel, the line grows to the
  left. Banishes are logged now (`[pick] ... banish on X`). `[Advice] ActionHints` (Reroll, Skip by default - off when an
  older cfg had the rescue screen's `RerollHint` off); the ADVICE tab's reroll row became **Hints on the selection
  screens** (Reroll and skip / + banish / Rescue screen only / Off). One band manager (`ScreenBand.cs`) places the
  hint's line and the WHY band so neither covers the other, a card's text, a button or the game's divider over the
  buttons.
- **menus fill wide and tall screens** (`WideMenus.cs`). On a screen that is not 16:9 - a 21:9 or 32:9 monitor, the
  Steam Deck's 16:10 - the game frames its menus in black bars; they are gone, and each menu's own dark backdrop
  reaches the screen's edges: in a run (level-up, chest, rescue, training, Research Pod, pause, results) and on the
  main menu and in the camp. **On by default** (`[General] WideMenus` = `Everywhere`; `DuringRuns`; `Off`), a row on
  the DISPLAY tab (**Menus on wide screens**). The end credits keep the frame, and so does the main menu past about
  2.65:1 (wider than its art). Every element is checked against the game's own layout first, every change is undone
  when the setting goes off or the screen changes, and `[wide]` lines say what was done. Nothing changes at 16:9.

The bench (`RunFixCases.cs`, compiling `LogFile.cs` too) replays the fix-now items of a 10-05 run: the four logged Endless
stat cards under the card each should go under, the class rank a run keeps (a rank III ability first looked at after the
level passed 40 stays closed), the `AVOID` prefix, the `>` kept in a reason, and the log rotation in a test folder (8 MB
moved to `.1`, the copies one step up, nothing overwritten when a step cannot run). `WordingCases.cs` (compiling
`Wording.cs`) holds every builder of the cards' plain words to the writing rules - swept over the probe's names:
abilities by level, evolution, clock and the build's order, weapon levels by style, lift and rank, the next tier and the
other branches, evolutions, every item for a few squads through the item rules' own reasoning, Research Pod cards,
rescues, quest lines, stat cards and the reroll hint: 50 visible characters at most, only the card font's glyphs, none
of the ranking's own words (a reintroduced `focus 2>3` fails) - and replays the run's offers (the logged headline next
to the line now), checks the game's stat labels and that the sources draw, log and fade as specified; the sweep runs a
second time as the card draws it, every name 19 characters longer, within the 48 characters beside `AVOID`.
`QuestCases.cs` (compiling `QuestRules.cs`) decodes every statistic objective of the 1.0.2 quests to the rule the advice
follows, replays the logged offers of a 10-05 *Heroic Theory* run (the Taser first on all seven, the abilities in their
order, the QUEST row along the weapon line), runs one case per other objective kind (the 10-05 *Trauma* chests for the
health item among them), the QuestSteer gates and the card rails for every quest line. `HintCases.cs` (compiling
`ScreenCall.cs`, `WhyText.cs` and `ScreenBand.cs`) replays the logged chests through the action hints (1.98 speaks, 2.51 /
2.76 / 2.78 and 3.82 do not) and their edges (the floors, the reroll count, SKIP, BANISH off by default and never on a
protected or the recommended card, one hint at most, the rescue screen's verdict unchanged), the 10-05 offer at 00:18
through the WHY band (CLOSE CALL at 4.75 / 4.72, the "vs #1" sentences), a sweep of every kind of card (no reason
repeats the card's own line, all within the rails), the packing of the band's lines, and the band manager on the PC's
level-up screen as measured on a 10-05 mark and the Steam Deck's modelled from it. `ReviewCases.cs` replays the round's
integration review: the WHY band on a quest-decided card, the "vs #1" sentence, two equal lines told apart, lent short
names, the rewordings, the loadout's survival words and the sources.

Open checks at the release (the README's "Next steps" then):

- 0.14.0's quest rules for the cards were built from the decoded quests and the objectives' machine code and replayed
  offline (the bench), but not seen in the game. With *Heroic Theory* (the Engineer's third quest) read the first
  `[quest]` line (every objective says what it does), the `[quest] ... ->` line, a level-up with the weapon on it
  (`Quest: leads to the tier-3 weapon (Level n of 4)` under the card, `#1`; `max the tier-3 weapon` on the tier-3
  weapon itself), the QUEST row along the line, and that the
  runtimes were found (no `progress not read` warning; the row's `Plasma 3/4` follows the picks; the weapon rule says
  `met` once the tier-3 weapon is maxed in hand). With *Trauma* (the Medic's first): a chest with a health item
  (`Quest: hold a health item (0 of 1)`, first) and `[quest] health items read from the objective's own count`. Then
  the ADVICE tab's new eleventh row (it must end above the footer rule at 1280x800) and Info only / Off.
- 0.14.0's WHY band and action hints were built against the 1.0.2 interop and machine code and replayed offline (the
  bench), but not seen in the game. With `[Debug] Screenshots = true` at 3440x1440 and `PreviewResolution = 1280x800`:
  the `[why] hooks` line at load; on each of the five screens, hover (mouse) and focus (controller) a card - the band
  appears at once, `[why] the game reports a card selected` names the card class, the band goes when the cursor leaves
  the cards and swaps its words from card to card, and it never covers a card's text, the RECOMMENDED ribbon, the game's
  Skill Tree label or a button (the `[why]` line says which band it took and every band's size: the hover allowance of
  about 7 % for the selected card is from the prefab, not measured); a CLOSE CALL offer; the Deck with the game's frame
  shown (WideMenus Off: the band may find no room) and hidden. The bench's PC geometry is the 10-05 mark's (four
  buttons, the divider rule at 1902 - 1906 units and its diamond); the Deck's is modelled - read the first `[why]` line's
  bands on both (on the PC the band is expected `over the buttons`, one line at 15 px, beside the diamond). Then a weak
  chest (best under 2.5) with a reroll left (`REROLL  -  the best card here is weak for this squad` over the Reroll
  button; the `[squad] action hint` line), a chest after rerolls down to two, a level-up whose SKIP hint shows (the
  line ends at the Skip button's right edge, `growing left`, clear of the team panel), and one banish by hand (`[pick]
  ... banish on ...` and `replaced (banish)`). Whether OnSelected also fires on a mouse hover (not only on the
  controller's focus) was inferred, not seen.

## 0.13.0 (2026-10-04) - badge advice on the run setup screen

- NEW: **badge advice on the SELECT LOADOUT screen** (difficulty and badges). The badges worth equipping for the team
  leader, the build, the mode, the difficulty and the standing orders you picked are numbered on the game's own badge
  buttons (gold diamonds, 1 = most worth it), equipped badges the advice would swap out are marked, a WHY line explains
  the badge under the cursor (mouse and pad), and a summary under CHOSEN BADGES lists what to equip and what to swap
  out. A **BADGES page** in the mod menu (BUILDS tab) previews it for every survivor, mode and difficulty, lets your own
  build pin or never-advise badges, and holds the settings (`[Advice] LoadoutHint`, `[General] LoadoutSize`). An
  optional one-click **EQUIP ADVICE** button presses the game's own badge buttons along the advice (removes first, then
  adds, never a quest's or a locked badge; UNDO until the screen closes) - **off by default** (`[Advice] LoadoutEquip`;
  mouse / touch, no key unless you set `[Advice] LoadoutEquipKey`): unless you switch it on and click it, the mod still
  never touches your loadout. Build packs may carry `badges` / `skipBadges` (extension API 3). See "Badge advice on the
  run setup screen";
- NEW: **the active quest's team rules on the rescue screen**. A quest can limit the team - "Team Size" of the
  Huntress's *Readjust* wants the leader alone (survivors == 1), the Ghost's third quest exactly Ghost + Huntress, two
  others a full team - and up to now the rescue cards knew none of it: in a logged run the cards ranked a survivor first
  on six of seven rescue screens and the reroll hint invited three rerolls, while the quest wanted no recruit at all.
  The quest's objectives are read once a run (`[quest] ...` in the log); at the quest's limit Liberate comes first
  (`quest: stay solo - take the level-up and cash`) and every recruit last (`quest: stay solo - Tank would fail it`),
  the reroll hint stays away and the readout's row reads `SOS  quest: stay solo`; a class the quest needs comes first
  (`quest: needs Huntress`; the reroll hint speaks for it alone, `REROLL - the quest needs Huntress`) while Liberate
  keeps its slot; a quest that wants a full team keeps Liberate under every recruit (late in a run the row then reads
  `SOS  Tank, SWAT · quest: full team`). Other objectives (survive, kills, weapons at the end ...) are logged and leave
  the advice as it was;
- **"abilities first" lets the weapon in**: its weapon levels sat on a floor (3.3) under every other survivor's ability
  levels, so once recruits had joined the weapon never "filled in" - in a logged run the leader's weapon (a lent
  Ability-style build on Auto) ranked last all run and was taken against the advice nine times. Now, once the
  survivor's own abilities are as good as done (every ability its build ranks owned, at most one short of its last
  level), a level of the weapon in hand scores on the balanced floor (4.3) and goes ahead of the recruits' ability
  levels - but stays just under a card of the survivor's own open build ability (`abilities first: after EMP Grenade,
  ahead of the rest`), so "abilities first" still holds for its own abilities;
- the reroll hint on the rescue screen judges with the right reroll count from the start. The game fills the cards
  before it refreshes the Reroll button, so the button still showed its previous number when the hint first read it: 0
  on the first rescue screen of every session (a first verdict of "no reroll left" with 8 in hand, put right a few
  milliseconds later when the game handed over its count), or a stale 8 after the rerolls were spent on other screens.
  The team's `Rerolls available` comes first now - the very number the game is about to hand over - and the button's
  number last; when the game's count for the screen still differs, the hint is judged again once (`[squad] reroll hint:
  (re-judged with rerolls 8 - ...)`);
- a build pack another mod lends: **Auto follows the tier-3 branch you actually take**. Up to 0.12.2 the pack's default
  stood in for Auto all run - after you took the other branch the cards still followed the default's abilities, focus,
  evolutions and item leanings, and the other branch was held out (2.00) as `your build takes Rocket Launcher` for a
  build you never chose. Now, once you own a tier-3 weapon that is not the default's branch, Auto follows the pack's
  build for that branch (else its build that decides the branch live, else plain Auto); the other branch is scored as on
  Auto (`other branch; Auto follows <build>, which takes <weapon>`), the reasons and the `[ctx]` line name such a build
  `(Auto)` instead of "your build", and `[builds] Tank on Auto follows ...` says when it changes. A build you choose
  yourself stays whatever you take, and between runs (the menu, the Training Yard, the run setup screen) Auto is the
  pack's default as before;
- each reason is said once: an item that both armors and heals listed `survival matters now (the squad is hurting)`
  twice, and a rescue card repeated its guides' tier and its bought synergies under the headline that sums them up
  (`A-tier rescue, 1 bought synergy with the squad; A-tier rescue in the guides; 1 bought synergy: 1 with Huntress`) -
  the headline now names the partners and the two lines go (`A-tier rescue, 1 bought synergy with Huntress`). An
  ability card's damage-type and team-passive headlines left their source line standing as well (`new ability -
  Trap Expertise boosts it; ...; Trap Expertise (Huntress) boosts it: taunt`, the same with `unlocks the Electric
  special` / `this level unlocks the Electric special (10 tags)` and `Kinetic: 100% of the squad` / `+1 Kinetic tag:
  100% of the squad's damage`): the line a headline sums up now goes, for all three;
- the EQUIP ADVICE button has **no key by default** (it was F9, another plugin's key on the team leader screen just
  before); a key you set is checked once the plugins are loaded, and a clash with another plugin's key, the game's
  (F8 feedback form, BackQuote console), Steam's F12 or the mod menu's own key is noted in the log (`[config]
  Advice.LoadoutEquipKey = F9 is also ...`). A config written by an earlier 0.13.0 build keeps the F9 it has - clear
  `LoadoutEquipKey` there by hand.

Nothing else in the ranking changed: only a survivor on Auto with a lent build pack (the card of the other tier-3
branch, and every card once that branch is taken), the rescue cards under a quest's team rule, and the weapon of an
"abilities first" survivor whose own abilities are as good as done score differently.

The bench checks what Auto follows when a build pack is lent (a made-up Tank pack, through a provider as another mod
registers one: before and after the other tier-3 branch is taken, a build the player chose, the `[builds]` line), the
words and scores of the other-branch and evolution cards on a lent build, and that each reason is said once (Frozen
Heart's survival clause, the rescue card's headline) - `AdviceFixCases.cs` - and the badge advice (`LoadoutCases.cs`: the
Python reference model line for line, the view, the layout of the PC and the Deck, the safety scan, the replay), and
replays the review of a 10-04 match (`MatchFixCases.cs`, compiling `QuestTeam.cs` too): the quest's team rules on that
run's seven rescue screens and three reroll hints plus made-up screens for the class rule and the full team, the logged
hands of the "abilities first" weapon (the lift and the cap under the survivor's own build ability), that no ability,
item or Research Pod card's headline is said again by one of its reasons (every ability of the probe on swept squads),
and that the EQUIP ADVICE key has no default.

Open checks at the release (the README's "Next steps" then):

- 0.13.0's badge advice was built and checked offline (the bench: the Python reference model line for line, the
  view, the layout of the PC and the Deck, the safety scan, the replay) but not seen in the game: run `[Debug]
  PreviewSetup` at 3440x1440 and with `PreviewResolution = 1280x800`, read the `[loadout]` lines (hooks, members,
  inventory hash, `drawn ... level A`, `layout`, `cursor sources`, `run start`, `measure`) and the screenshots, and
  replay the log. Then the unmeasured weights (U1 tag points vs the Hashtag stat, U2 player stats on recruits, U3 the
  crit pools) from the `measure` line, the EQUIP ADVICE button by hand (mouse and Deck touch), and whether the game's
  hover popup covers the WHY line.
- 0.13.0's quest rules were built from the 1.0.2 interop and the decoded quest assets, and replayed offline, but not
  seen in the game: start a run with a team-size quest active and read the `[quest]` line (which list held the
  objectives on the runtime copy - `objectives` or `_objectiveDefinitions` -, the rule, whether the run fits the
  quest's conditions), then a rescue screen (Liberate first, `quest: ...` on the cards, no reroll hint, `SOS  quest:
  stay solo`); and once a run that does not fit (another leader or mode): `does NOT fit` and the usual cards. The
  Ghost's third quest (Ghost + Huntress) is a good test of the class rule. Also see the "abilities first" weapon lift in a run
  with an Ability-style build (`style: abilities first - only ... left`), and `[config] ... is also ...` with a key set
  to F9 while another plugin uses it.

## 0.12.2 (2026-09-25) - fixes from a round of Steam Deck logs

- the rescue (SOS) cards have their RECOMMENDED ribbon and reason line back. The game's patch of 2026-09-23 gave them
  a card class of their own (`UIPowerupButtonSOS`), which the badges did not know: only the gold frame was drawn, and
  every rescue card logged a warning. A card class the mod has never seen now borrows the card's first label (said
  once per class), and at load the log lists the game's card classes (`[badge] card classes: ...`);
- the fifth weapon of every survivor (Grenade Launcher, Super Shotgun, Plasma, Toxic Arrows, Soul Reaper, Syringer,
  Axerangs, Spanner Spammer, Flarebolt) is a THIRD branch of the fork, as the game's data has it (it follows the tier-2
  weapon like the other two tier-3 weapons), not a "final weapon": a build may name it (the editor cycles three
  branches; up to 0.12.1 a build pack naming it was cut back to "decided live"), and the Training Yard no longer
  pushes points into it ahead of rank III abilities while the build takes another branch - the other two branches
  come last. The SWAT's Grenadier and the Tank's Shotgunner presets, written around their fifth weapon, now name it
  as their branch (they were "decided live", so the Training Yard put the guides' weapon first), and no preset text
  promises a second tier-3 weapon after its branch any more;
- two recruits that score the same are ordered the same way on the SOS cards and in the readout's SOS row (the
  guides' rescue tier, then bought synergies, then a fixed class order) - the card used to follow button position;
  a recruit's team passive reads `team passive: +Armor` instead of the game's sentence cut after 41 characters;
- when a level-up offers only the evolution your build does NOT name, the card still comes first and now says why
  (`only X is offered - your build prefers Y; still the biggest spike`), and the readout says `evolve Arrow Rain
  (Downpour preferred)` rather than naming one evolution as if the other were wrong;
- a reroll or a banish that refills the same screen is recognised (`[offer] ... replaced (reroll): gone ...; new ...`)
  and no longer counted as another level-up by the pace estimate;
- NEW: a **reroll hint on the rescue screen** - when a survivor who could still come rates clearly higher than every
  card on offer and the game still has a reroll for the screen, its Reroll button gets a gold frame and a line:
  `REROLL - Tank would rate higher (5.9 vs 4.6)`. A switch on the ADVICE tab (`[Advice] RerollHint`, on by default);
  see "The reroll hint on the rescue screen".

The ranking scores themselves are unchanged; only exact ties between recruits are ordered anew.

The bench compiles `TreePlan.cs` too and checks every survivor's weapon fork against the game's data (three tier-3
weapons after the tier-2 one, each accepted in a build), walks the Training Yard plan of every survivor over the tree in
`gamedata.json` (Auto, and a build on the fifth weapon), and replays the recruit ties and evolution headlines of the
round of Steam Deck logs, and the reroll hint on that round's rescue screens (the four the player rerolled must speak,
the offers after the reroll must not) and on made-up screens for its edges (the margin, ties, no reroll left, late,
Liberate on top).

Open checks at the release (the README's "Next steps" then):

- 0.12.2: see a rescue offer with `[Debug] Screenshots = true` - the ribbon and the reason line on the new SOS card
  class (its root size and the new synergy row under the portrait were never measured: ribbon centre at -48, reason
  at -134 below the card), a reroll and a banish (`replaced (reroll)` / `replaced (banish)`), and one Training Yard
  tab per survivor with a build on each kind of branch.
- 0.12.2's reroll hint was built and replayed offline (the bench) but not seen in the game: on a rescue screen with a
  better survivor off the cards, check the frame on the Reroll button, the line in the band over it (or under it -
  the `drawn ...` line says which and how tall the band was), that it never covers a card's text, that it is judged
  again after a reroll and gone after the pick, and which reroll count the log names (`the screen's count` = the
  game's own hook answered).

## 0.12.1 (2026-09-23) - display names from other mods

Another mod can lend the names the Companion SHOWS for a class, a powerup or an item (`RegisterDisplayNames`, extension
API version 2). The PLAN readout, the reasons under the cards, the BUILDS tab and the Training Yard strip draw them; the
ranking, the builds and the `[card]` / `[pick]` log lines keep the game's own names, so the advice is exactly what it was.
Nothing changes while no mod lends names (the pure rule files and the offline bench are untouched). Compiled, and the API
driven through reflection against the built DLL outside the game; not seen in the game yet.

Open checks at the release (the README's "Next steps" then):

- 0.12.1's display names have only been driven outside the game: see them with a second plugin that lends names -
  the readout's rows and a label longer than HUNTRESS (the label column widens), the card reasons, the BUILDS tab,
  the Training Yard strip, a switch of names mid-run (`InvalidateDisplayNames`), and `[card]` lines unchanged.

## 0.12.0 (2026-09-20) - extensions

Other mods can add options of their own to the mod menu (a fourth tab, MODS, that exists only while one does) and lend
build guides per survivor that stand next to the presets - see "Extensions (for other mods)" in the README. With more
than five build cards the BUILDS row scrolls with the focus. Nothing changes while no mod registers anything: the offline
bench prints the same 772 lines before and after. Seen in the game before the release: the MODS tab and a lent build
driving Auto in a run.

Open checks at the release (the README's "Next steps" then):

- 0.12.0's extension point has only been driven outside the game (reflection against the built DLL, the pack
  parser in a console): see the MODS tab and a lent build with a real second plugin - four tabs in the header, the
  rows with mouse / keyboard / controller, a list long enough to scroll, six or more build cards, `VIA AUTO`.

## 0.11.0 (2026-09-20) - a readable readout, a menu that shows what it does, sharper advice

- **The PLAN readout follows the screen**: its body text is 1.875 % of the screen's height and never under 15 px - 15 px
  on the Steam Deck (x1.45, as before), 20 px at 1080p, 27 px at 1440p, 40 px at 4K. Up to 0.10.2 the block was only
  enlarged until its text reached 15 px, which left every desktop at x1.00: 21 px at 3440x1440, smaller than the HUD's
  own quest text and hard to read from a desk. New `[General] PanelSize`, the DISPLAY tab's **Readout size** (70 - 200 %,
  accepted range 0.5 - 2.5); `PanelScale` still replaces the automatic part.
- **The DISPLAY tab shows the readout itself**: the widget the HUD gets, built with the settings as they stand, in a
  window of the menu at the pixels it has in play, rebuilt on every change. `SAVED - applies at once` in the header and a
  `[config]` log line for every change. `Ui.CloneText` zeroes the clone's margin (a menu caption's side margins wrapped
  the preview per character).
- **Menu input** is armed only once the press that opened the menu is released: a double click on the COMPANION button
  used to land on whatever control lay under the cursor (a logged run has a build chosen 75 ms after the menu opened); a
  frame with a mouse click never also counts as Submit.
- **The advice, from the review of a logged 74-offer run** (the bench gained a section replaying its offers):
  - military training rarity weighs 1 / 2 / 2.6 / 3 (was 1 / 1.6 / 2 / 2.3: both logged overrides by the player were
    right);
  - a magazine item with no magazine on the squad keeps a fifth of its tier and no fit: Last Round had been RECOMMENDED
    with the reason "no weapon on the squad uses a magazine";
  - the readout's row names the ability the cards will rank first (`Ranker.TopAbility`): "next Minefield" had stood in
    the row for fourteen minutes of the logged run while the cards ranked Minefield first in none of eighteen offers;
    an ability of a class rank not reached when the run began is not announced;
  - under Balanced the first level of an ability the survivor does not have yet is lifted (up to +1.5, under every
    tier-up and evolution) while a fresh ability can still grow up: in the logged run the first seven level-ups had all
    gone to the weapon and half the ability slots were still empty at 20:00 ("Weapon first" on the ADVICE tab gives the
    old behaviour);
  - an evolution's added damage type nobody else on the squad deals costs 0.15, so two level evolutions are settled by
    the tag doctrine, not by card position;
  - economy items fade with the clock without exceptions (a health word in the text of an item the game does not flag
    as healing - Electric Personality - kept one from fading); Ultra Instinct is judged by what pooling gains against
    what it wipes instead of a flat +3; an item with a line per squad size (Duct Tape) is scored by the current size;
    wrong keyword matches are guarded (Crowbar, Glass of Milk, Vampire Survivor);
  - a weapon branch's reason names only a damage type that sets it apart from the other branch; the card no longer
    repeats the line its headline was cut from, and a new ability's card no longer says "take each once early" and
    "before another weapon level" both; Research Pod reasons state the type's share and a crossing of 30.
- **The pause walk** (`[Debug] PreviewPause`) asked for the pause half a second after a pick: the game takes no pause
  key while a selection screen animates out, the pause menu never came, and the walk "put back" settings it had never
  read (PanelSize = 0). It now waits for plain play, asks again when an offer got in first, answers every offer that is
  up, restores only what it changed, and plays up to forty seconds when no offer came in the first thirty (a walk
  without one ranked card checks little).

Quick Run sometimes starts the run after the team leader screen and sometimes opens the whole run wizard. Since
0.11.0 the walk clicks through all of it with the game's own handlers (`UIViewChooseHero.OnClickStart`,
`UIViewChooseArena.OnClickStartArena` / `OnClickStartGameMode(button)` - which also continues -,
`UIViewRunSetup.OnClickDifficulty(button)`, then `UIStartGameBar.OnClickStartGame()`), taking what each screen
proposes (the profile's last arena, mode and difficulty; else the game's default; else the first unlocked) - those
choices are stored in the profile, which is why it was left to hands before. Seen in the game: Quick Run to a
running round in 22 s. It still gives up 200 s after pressing Quick Run. The game also opens its pause menu by
itself when its window is not the focused one as the run comes up, as happens after a launch from a script: the
walk resumes once when it sees the pause menu before it asked for it. After closing the mod menu it resumes the
run for five seconds so the log shows the readout coming back (`[panel] created` / `shown`, `[plan]`).

## 0.10.2 (2026-09-20) - the readout after the mod menu

A follow-up to the performance pass, found in the log of a full run: using the mod menu from the pause menu left the old
readout behind under the HUD and brought the new one up with a search through every label in the scene, a 27 - 28 ms
frame; now 4 ms, and nothing is left behind. Nothing you see changes.

Closing the mod menu takes the readout down so it comes back with the display settings and builds as they are now.
Up to 0.10.1 that only *forgot* the old one: every use of the mod menu during a run left an inactive `YazsPlan`
object under the HUD until the run ended, and the new readout was made on the first tick back in play - a label
search over everything loaded, the widget, a full snapshot and a plan, all in one frame of play. Now:

- **The old readout is destroyed** (`Panel.Rebuild` from `Menu.Close`; `Panel.Reset`, from the HUD's `OnDestroy`,
  does the same - the object is dying there anyway, destroying it twice is harmless).
- **The label it was cloned from is kept** across the menu while it still lives under the same HUD, so the new
  readout needs no search at all (`[panel] created ... using label 'GameTimer_Txt' (kept)`) and looks exactly like the
  one before - the search used to come back with 'Quest_Obj1' instead of 'GameTimer_Txt' once a quest was up.
- **The plan is worked out when the menu closes**, while the game is still paused (`plan.ahead`), and only put up on
  the first tick back in play - the same route a pick takes since 0.10.1.
- **`Ui.FindLabel` asks the canvas before it asks the world**: the active labels under the HUD canvas (or the menu
  view) come from `GetComponentsInChildren` on that object; `Resources.FindObjectsOfTypeAll` remains as the fallback
  when it has none, and for the callers without a canvas (the restart notice, the design preview - menus only). Same
  order of preference: the named HUD labels first, then any active label under it.

Measured on the PC with both searches run side by side in the game (a temporary diagnostic, removed again):

| where | the walk under the canvas | the search through everything | label found |
|---|---|---|---|
| HUD, as the run begins | 2.0 - 3.0 ms (first call) | 19.8 ms | the same object, `HUD/CanvasTop/GameTimer_Txt` |
| pause menu (mod menu opening) | 0.15 - 0.48 ms | 19.0 - 20.6 ms | a button caption `Name` each: same font, material, size, spacing (the search picked the caption of the mod's own COMPANION button, the walk picks the first button's) |
| main menu (mod menu opening) | 2.4 ms | 55.3 ms | as above |

And the frame it was about (one survivor; `[Debug] Perf`), from the scripted pause walk and from the same steps done
by hand on the released build (resume, a level-up, pause, COMPANION, close, resume):

| the readout coming back after the mod menu | 0.10.1 (the real run, one survivor) | 0.10.2 |
|---|---|---|
| worst `tick.hud` frame | 26.7 and 28.3 ms | 4.3 ms scripted, 5.3 ms by hand |
| making the widget (`panel.create`) | with the search, ~20 ms | 1.4 - 2.0 ms, no search |
| snapshot + plan in that frame | `read` + `plan.build` 4.9 ms | none: `plan.ahead` 2.4 - 3.8 ms when the menu closed, paused |
| `[panel] created` -> `[plan]` | 7 ms | 1 - 2 ms |
| objects left under the HUD per use of the mod menu | one inactive `YazsPlan` | none |

Still the most expensive frame of a session, and not changed here: the very first readout of the first run, at 00:00
on the clock - `panel.create` 27 ms (code compiled on first use, the backing's textures, the glyph check; less when
the mod menu or the Training Yard was used before, which warms the same code) plus the first plan, 30 ms.

The Training Yard's one slow tick per visit (49 - 58 ms, in a menu) was looked at as well: it is not a search - the
strip clones the view's own description label - but first-use cost spread over everything (`yard.read` 7 ms,
`yard.advise` 8 ms, `yard.marks` 15 ms, `yard.strip` 19 ms on the first tab; 0.5 ms a poll afterwards). Left alone.
The mod menu's own opening (`ReadGameArt`, ~30 ms once a session, and building its shell, ~50 ms) happens over a
paused game or the main menu and was left alone too.

## 0.10.1 (2026-09-19) - the performance pass

Nothing you see or read changes. The rule: during play the mod does nothing per frame beyond a fade and a few time
checks, polls cheaply, and does its heavy work while the game is paused anyway. What 0.10.1 changed to get there - none
of it changes what is drawn, ranked or logged:

- **No object searches during play.** Up to 0.10.0 the upkeep of the COMPANION buttons looked for the main menu and
  the pause menu with `Resources.FindObjectsOfTypeAll` twice every half second - also in the middle of a run, where
  that walks every loaded object - and every frame while the mod menu was open. The menus now report themselves: the
  hold-still prefixes on `UIViewMainMenu.Update` / `UIPauseMenu.Update` run every frame a menu is live, so the view
  they saw within the last three frames is the live one. The search remains as a fenced-in fallback (the main menu
  before its first Update of the session; the pause menu only while the game's `IsPauseMenuFlowActive` is on and its
  Update has never been seen; an explicit open the hooks cannot place). MEASURED with `[Debug] Perf` on the PC, on
  the run-setup screens (no main menu, no run): one search costs about 41 ms - with the search still running there
  twice a second the mod took 4,524 - 4,951 ms of every minute (worst single frame 145 ms); fenced in, 41.7 ms a
  minute (worst 0.14 ms), 0.012 ms a frame. In 0.10.0 two such searches ran every half second of every run.
- **The plan is rebuilt while the game is still paused.** A new plan re-scores every item that can still drop and
  every recruit: 4-5 ms on the PC (measured from the log: `[panel] shown` to `[plan]`), more on the Deck - and it
  ran on the first tick back in play after every pick. It now runs in the `Hide` post-fix, in the second the screen
  takes to animate out, and the tick only puts it up (`Panel.PlanAhead`). If the run moved on in between, the tick
  rebuilds as before.
- **A fingerprint instead of a snapshot every two seconds.** `G.QuickKey()` hashes who is on the squad, every powerup
  and item with its level or count, the tag points and the active quest in a few dozen calls; the full snapshot
  (names, the tree, the tag profile - over a thousand calls into the game) is only taken when it moved.
- **Asset data is read once.** The Training Yard's node list and its split per class (it was walked once per
  recruitable class per plan, 245 nodes a time) and the team passives among them are kept for the run, levels are
  still read live; an item's English text, statistics and carry limit are kept for the session (by item id, checked
  against the name); inside one offer or one plan build (`using (G.Cache())`) names and powerup facts are fetched
  once per object instead of thousands of times (`G.Same` falls back to comparing names).
- **The item rules parse each description once**: the rich-text strip, the clause split and some sixty pattern
  matches per item are kept per description (`ItemRules.Parse`); only the part that depends on the squad and the
  clock runs per score. The offline bench prints the same 753 lines before and after.
- **The first plan of a session is prepared on the main menu.** It cost 170 - 190 ms in the first second of the
  first run of every game session (six sessions in the log; 7 - 9 ms for every later plan): patterns being set up,
  every item text read for the first time, code compiled on first use. Two things: the item rules' 33 patterns are
  no longer `RegexOptions.Compiled` - with each description parsed once, a pattern runs a few hundred times a
  session and compiling cost more than it saved (offline, `tools\bench.cmd --time`: first pass over all items 89 ms
  compiled, 28 ms interpreted; later passes 0.3 - 0.5 ms either way) - and `Warmup.cs` builds two throw-away plans
  on the main menu four seconds after it is up, on separate frames (an empty squad: the GRAB row scores every item,
  ~50 ms; then a stand-in survivor from the game's class data: weapon line, abilities, recruits, ~44 ms). Measured
  with the first step alone: first plan 174 ms -> 70 ms (`plan.build` 133 -> 42 ms, `read` 30 -> 12 ms); with both
  steps, on a real run: 31 ms.
- **Small things**: one per-frame Harmony hook less (the fallback tick on `GameplayMaster.Update` now rides on the
  `GameMaster.Update` hook and only steps in when the HUD's own tick goes quiet), the gating flags are compared as a
  number and only put into words when they change, the gold highlight re-renders only the groups that hold a changed
  row and only when the colour moved, a label is only written to when its text differs, the menu key's name is parsed
  when the setting changes rather than every frame.

Taken on the PC (3440x1440, 60 fps) with `[Debug] Perf` from two scripted thirty-second runs (`[Debug] PreviewPause`: it
presses Start on the team leader screen, takes the recommended card of every offer, then pauses; one SWAT, so a late
three-survivor squad will cost more per poll and per plan):

| what | 0.10.0 | 0.10.1 |
|---|---|---|
| object searches during a run | 2 every 0.5 s, ~41 ms each | none |
| per frame in play (`tick.hud`, without the one-off first tick) | not measured | ~0.011 ms |
| the two-second change poll (`refresh`) | a full snapshot each time | 0.17 ms (14 polls = 2.4 ms) |
| after a pick, back in play: `[panel] shown` -> `[plan]` | 4 - 5 ms (three survivors, from the log) | 2 ms: the plan was built while paused (`plan.ahead` 2.2 ms) |
| first offer of a session, ranked while paused (`offer`) | not measured | 27 ms |
| first plan of a session | 170 - 190 ms | 70 ms with the item warm-up alone; 31 ms with both steps (the real run below) |

And from a real run on the same PC the same day (Normal, 19:49 on the clock, three survivors at the end, 74 offers of
all five kinds, 110 minutes of session, 394,589 frames, no warnings): `tick.hud` 0.0081 ms a frame on average
(3.1 s in all), `tick.master` 0.0087 ms; 622 change polls at 0.21 ms; 70 of the 74 plans came from `plan.ahead`
while the game was paused (2.2 ms on average, worst 52 ms), only 4 were built in play (`plan.build` 4.9 ms on
average); `[panel] shown` -> `[plan]` after a pick: 1 ms median over 53 picks (worst 8 ms); an offer ranked in
5.2 ms on average (worst 25 ms, paused). The three worst frames in play all belonged to the readout being created:
57 ms as the run began, 27 and 28 ms when it came back after the mod menu had been used from the pause menu - each
time with a search through every label in the scene for one to clone. That is what 0.10.2 is about.

## 0.10.0 (2026-09-19) - the advice review, build guides and the mod menu

An independent review of the ranking (a fresh-eyes audit of the code against a logged run, the game's own data dumped
from the running game, and the published guides re-read and graded) rebuilt it around four things read live: the BUILD
you follow per survivor, the SQUAD's synergy as the run stands, the run CLOCK, and the game MODE - see "How it ranks" in
the README. A mod menu (a COMPANION entry in the main menu and the pause menu, or F10; mouse, keyboard and controller)
picks a build per survivor (24 presets labelled GUIDE PICK / ALTERNATIVE, and an editor for your own), and sets the
standing orders of the advice and what the mod draws - see "The mod menu". The level-up style is yours to set (Balanced
by default; the ADVICE tab puts "weapon first" back). Ranking fixes: evolution ties, recruit synergies, Silencer and
Dartboard by the weapons' real range, military stat weights, GRAB lists only what can still drop. The mod has its own
artwork now (`art/make_art.py`).

Verified in the game: the menu at 3440x1440 and in a real 1280x800 window, real keyboard input, the entry in both menus,
the mod menu over a paused run (the pause menu survives closing it), and the new ranking on a live level-up offer;
chests, SOS, military and Research Pod offers ran through the offline bench and the live plan rows but not yet through a
live offer of their own.

Open checks at the release (the README's "Next steps" then):

- Play 0.10.0 and judge it: the new default level-up style (Balanced; ADVICE tab puts "weapon first" back), the
  card headlines, a build selected for the leader. With `[Debug] Screenshots = true` a run leaves the offers on disk.
- Still unseen on a live offer of their own: chests, SOS (Liberate late), military training and Research Pod cards
  under the new rules, and an evolution offer with both cards up (the `[card]` lines carry the reasons).
- Controller input in the menu was built against the game's own action names but only the keyboard was driven in
  a test; check it on the Deck (the pad uses `GameMaster.GetButtonDown("UISubmit" / "Cancel" / "GoNextTab")`).
- Item tiers cover 59 of 136 items; the rest score on fit alone. Boss Rush's item pool mask is not understood yet.

## 0.9.0 (2026-09-18) - the motion pass

A small tween runner and effects made from the game's own vocabulary - see "Motion" in the README. Verified frame by
frame on the Training Yard and for the PLAN readout's entrance and change cue; the card badges' entrance uses the same
primitives but was not seen on a real selection screen at the release.

## 0.8.0 (2026-09-18) - Training Yard advice

On "Train your survivors" the mod numbers the nodes worth buying with the points on hand (gold diamonds, in purchase
order), rings the node to save for next, and prints a SPEND / THEN / WHY strip under the tree; see "Training Yard
advice" in the README. Checked on all nine tabs at 3440x1440 and in a real 1280x800 window (the Steam Deck's layout and
pixels) by a preview walk that opens the Training Yard from code.

## 0.7.0 (2026-09-18) - the PLAN readout gets out of the way

The user's verdict on 0.6.0: polished, but "over-opaque/sized - it's blocking essential awareness of surroundings". So
the framed, nearly opaque 800-unit panel at the right edge became a HUD readout in the style of the game's quest tracker:
a small gold title over a fading rule, the rows on a soft dark wash (42 % at most) that dissolves towards the middle of
the screen, no frame; it sits in the empty bottom-left corner under the weapon / ability icons, is only as wide as its
text, shows one row per survivor (`PanelDetail = Compact`), and dims to 70 % when nothing has changed for a few seconds.
Checked with the preview mode over real gameplay frames at the PC's and the Deck's pixels (new: a backdrop image behind
the preview): on the Deck a full squad covers about 22 % x 21 % of the screen, see-through, where 0.6.0 covered up to
30 % x 65 %, opaque. `PanelPosition = Right` and `PanelDetail = Full` bring the old place and the two-rows-per-survivor
plan back.

## 0.6.0 (2026-09-16) - the design pass

The PLAN sidebar is a framed panel in the game's own style (body, gold hairline, corner diamonds, header band, label
column, hairlines between groups, fade in / out), the card badges and the restart strip share its palette and primitives
(`Ui.cs`), and a preview mode renders the sidebar on the main menu with sample plans so the look can be checked from
screenshots without a run - verified this way at the PC's and the Deck's pixel sizes, from a 1024x768 remote desktop
(`PreviewResolution`).

## 0.5.5 (2026-09-15) - debug screenshots, the ability line no longer wraps

Card verdicts and the PLAN sidebar working on PC and Steam Deck; auto-update validated on both. 0.5.5 stops the ability
line from wrapping (evolution names only from one level below max) and ships the screenshot flag (`[Debug] Screenshots`).

## 0.5.4 (2026-09-14) - compact PLAN sidebar, changed lines highlighted

The plan is compact (two lines per survivor, nine lines for a full squad) and the lines whose advice changed after a
pick are highlighted. A PC run captured with the new debug screenshots confirmed, without anyone watching the screen,
the highlight timing (gold at 0.3 s, mid-fade at 1.5 s, white by 3.5 s), the gating on the pause menu and the pick
transition, and the PC geometry (canvas 5161x2160 at 3440x1440, scale 1.00, the font lacks "›" so ">" is used).

## 0.5.3 (2026-09-14) - the sidebar stays above the minimap

A long plan stays above the minimap: the block shrinks (down to 0.75x) and is 700 units wide, so fewer lines wrap.

## 0.5.2 (2026-09-14) - sidebar gating fixed from the Deck log, auto scale on small screens

The sidebar had appeared only on the results screen in 0.4.1 because it was gated on
`GameplayMaster.IsGameplayUIVisible()`, which the Deck log of 0.5.0 proved to mean "a UI view is showing" (false during
play; true on the pause menu, the selection screens and the results): the diagnostic builds 0.4.2 - 0.5.1 logged every
candidate flag (`[panel] players=1 active=True paused=False pauseMenu=False defeat=False hudVisible=False
selecting=False screen=False hud=True` is play), and 0.5.2 hides the sidebar on any of `players == 0`, an open selection
screen (our tracker or `UIGameplay.IsDisplayingUpgradeSelection()`), `GameplayMaster.IsPaused`, `IsPauseMenuFlowActive`,
`IsDefeatResultsFlowActive` or `IsGameplayUIVisible()`. The flags are still logged when they change, with `[panel]
shown` / `hidden` / `created ... x1.45 canvas WxH screen WxH`. 0.5.2 also scales the sidebar and the card badges up on
small screens (the Deck's 1280x800 gets a 3840x2400 canvas, so a canvas unit is a third of a pixel there). Deck
screenshots confirmed the sidebar in play at the enlarged size, gone on the pause menu, and the scaled badges under the
cards.

## 0.5.1 (2026-09-14) - Research Pod verdicts, damage-type item fit, TAGS line, offline bench

Ranks the Research Pod reward cards, scores chest items against what the squad actually deals, adds a `TAGS` line to the
plan and the offline bench (`tools\bench.cmd`). The Steam Deck, hand-installed with 0.5.0, got its first automatic
update with 0.5.1.

## 0.5.0 (2026-09-14) - card verdicts, the PLAN sidebar, auto-update

The first public release: the card verdicts on the selection screens, the PLAN sidebar (still diagnostic: it logged
every candidate flag for its gating), the auto-updater and the public repository. The updater was validated on the PC
with a local feed and with a real newer build next to the running one. The 0.4.x builds before it predate the public
repository.
