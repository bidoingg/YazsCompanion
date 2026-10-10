# Changelog

What changed in each release of the YAZS Companion mod, newest first: what each version changed, why, and how it was
checked. The [README](README.md) describes the mod as it is now. Dates are the release dates on the maintainer's clock
(US Central); the [GitHub releases](https://github.com/bidoingg/YazsCompanion/releases) carry the exact times, the zips
and the one-paragraph notes the auto-updater shows.

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
