// 0.16.0 (C16-02): the item book - one rule per item of the 1.0.2 chest pool, written from the game's own descriptions (data\probe.json,
// game 1.0.2; 133 pool items + Jewel of Death, which Jewel of Life breaks into). It replaces the keyword patterns for every item it
// names: what the item gives (Gains) and takes (Costs) as the item rules' own tags, what it needs to work (Need), which run curve its
// worth follows (Clock), its worth when the guides give it no tier (Value) and the card's words when nothing squad-specific leads
// (Say). An item the book does not name (a newer game build's) falls back to the keyword patterns ('[items] no rule for ...', once),
// and so does every item while [Debug] ItemKeywords is on (UseKeywords below; ItemRules.UseBook). Pure C# (no game types): the
// bench compiles it (ItemBench.csproj links it) and pins every row in four reference squads (ItemBookCases.cs, itembook_expected.txt).
// knowledge.json keeps the guide tiers and notes; it is not the place for these rows (a player-edited file is never refreshed).
//
// THE VALUE SCALE. Value is on the tier scale (S 3 / A 2 / B 1 / C -1.5). For an untiered item it is read from the item's own
// numbers at 0.03 per 1 % of the squad's damage (B = 1.0 = about +33 % damage): Combat Knife +30 % on the 79 % of the damage that
// hits basic enemies (badgeModes Normal: elites 0.15, bosses 0.06) = 23.7 % = 0.7; Quicksilver Bullets +30 % weapon attack speed on
// about half the damage = 0.5. No untiered Value exceeds 1.0 (B) - the user's Q02 is not answered yet, so its default stands (cap
// at B); the needs that compute a worth from the run (luck, empty slots, pickup range, speed) use the same 0.03 per 1 % and may go
// higher, and so does the revive need (C16-12: a second life ranks at least like an A tier, ItemRules.ReviveFloor).
// 0.16.1 (C-1, the user's decision B2-Q2 of 10-10: mark them, the cap stays B): a Value is the Companion's estimate, never a guide's -
// the card says "(unrated)" where a guide-tiered item says its tier, and the WHY's first reason says whose rating it is
// (Wording.EstimateCue / EstimateWhy).
// 0.16.1 (C161-01): "economy" is one tag for XP, luck, level-ups and training points; each row that gains or costs it names what it
// really gives (.Economy("luck")), so the WHY's run-clock clause says "More luck - pays off all run" for Power of Friendship (+10 Luck,
// no XP) instead of the tag's generic "More XP and luck" (live 10-10 11:57:59). The bench holds every such row to the game's own text.
// 0.16.1 (the 10-11 Steam Deck round): a Say longer than a narrow card's 36 characters has a shorter form (.Briefly(...), R-03 - the
// bench's DR3 sweep holds every row to it), and Magazine Clip and Last Round say what they do (R-07): the clip rule's note puts the
// item's own words before its condition ("doubles every magazine - your guns use magazines").
// 0.16.4 (the game's content patch of 2026-10-16, still "1.0.2", buildName 2026/10/16/13/25#1.0.2): 47 chest items changed their
// texts (the I2 terms the game draws; the assets' EnglishDescription the Companion and its probe read kept the 10-02 words). The rows
// follow the 10-16 texts (data\gamedata.json, the offline extract): the numbers of the words (Hit Tracks +40 speed, Toilet Paper +30,
// Camping Set +15% a second, Fishing Pole +100%, Devil's Deal +30%, Broken Glass +30%, Treasure Finder +3 rerolls and banishes ...),
// Stretcher without its -1% dodge, Crowbar's +30% damage below half HP, the 18 new "When equipped with" pairs and the conversion
// pairs that now apply both statuses. The Values of the buffed untiered items are re-estimated: scaled by the item's own number where
// the row had one (Camping Set +50% -> +75% = 0.3 x 1.5, Broken Glass's slowed-enemy part 25 -> 30%, Power of Friendship's halving
// chance 10 -> 15%), the Companion's judgment elsewhere (Suspicious Pendrive now kills basic enemies, ACME Anvil +50% damage, ...: still
// at most B); +10 luck is economy:0.5 on every row (Ban Hammer's, Teddy Bear's, Rock And Roll's; Power of Friendship's +20 1.0). A tiered
// row keeps its knowledge tier (the guides' rating).
// 0.16.4, after review: where the game's own data or code does not do what a 10-16 text says, the book follows the game (and says so
// beside the table): Bleeding Edge and Slingshot are each other's partner (TagPairs), Battery Leakage's "both effects" does not exist
// and Warm Ice Cream's needs Biofuel Energy (Converts); each pair is worth what it does (PairWorths, TagPairs, Converts), not one weight.
using System;
using System.Collections.Generic;

namespace YazsCompanion
{
    internal sealed class BookEntry
    {
        public string Name;
        public double Value;          // worth on the tier scale when knowledge.json gives no tier (0 = nothing beyond its fit)
        public string Gains, Costs;   // rule tags with an optional weight: "critical, weapons:0.5" ("line" = the squad-size line's own words);
                                      // "~tag" = it WORKS WITH that (Modchip and crits): scored like a gain, never drawn as something it gives
        public string Need;           // what it needs to work (ItemRules.Need): clip[:f], short, long, shield, own:<Type>, status3, slots:damage,
                                      // slots:xp, luck, pickup, move:abilities, move:Chemical, gems, tags:omni, tags:zug, tags:hat, squad, solo,
                                      // power:<PowerTag>, lockdown, skip, hurt, basic, boss, healthy, revive (C16-12) (null = none)
        public string Clock;          // now (default) | economy (worth follows the economy curve) | grows (a scaling item) | cash | economy,grows
        public string Say;            // the card's words when nothing squad-specific leads (<= 40 visible characters, ASCII, the rails)
        internal double[] GainW, CostW;   // per ItemRules.All index (built once, ItemBook.Ready)
        internal bool[] Uses;             // per ItemRules.All index: a "~" gain
        // 0.16.0 (C16-01a): the held-item rules' facts of this item, on its own row (one table, C16-02 section 8; HeldRules keeps the
        // functions). Mana Potion held zeroes every ability cooldown reduction stat bonus: ManaKeep = the share of the item's worth that
        // stays (1 = all), ManaLead = the card's words then (null: a WHY reason only), ManaWhy = that reason; Asset = the game's asset, so
        // the rule still finds a renamed item (ItemBook.OfAsset)
        public string Asset;
        public double ManaKeep = 1;
        public string[] ManaLead;
        public string ManaWhy;

        /// <summary>0.16.0 (C16-01a): what is left of this item while Mana Potion is held (see the fields above).</summary>
        internal BookEntry ManaPotionHeld(string asset, double keep, string why, params string[] lead)
        {
            Asset = asset; ManaKeep = keep; ManaWhy = why; ManaLead = lead != null && lead.Length > 0 ? lead : null;
            return this;
        }

        // 0.16.1 (C161-01): what the row's "economy" gain (or cost) is, in the game's words for it: "luck", "XP", "level-ups", "XP and
        // training points" - the noun of the WHY's run-clock clause ("More luck - pays off all run"). Null on a row without that tag
        public string Econ;

        /// <summary>0.16.1 (C161-01): names what this row's "economy" tag stands for (see <see cref="Econ"/>).</summary>
        internal BookEntry Economy(string econ) { Econ = econ; return this; }

        // 0.16.1 (R-03 of the 10-11 Steam Deck round): the card's words in fewer characters - Say's, or those of the guide note
        // knowledge.json has for the item (Homing Pigeon, Gaslighter) - for a card that cannot hold them (36 beside the Deck's "2ND"):
        // 'Throws an Explosive grenade every' was Great Nade's Say cut to that room. Null: they fit there
        public string Brief;

        /// <summary>0.16.1 (R-03): the shorter words of this row (see <see cref="Brief"/>).</summary>
        internal BookEntry Briefly(string brief) { Brief = brief; return this; }
    }

    /// <summary>An effect two held items have on each other that is neither a pair nor a clash (the texts call it out).</summary>
    internal sealed class BookMixed { public string Offered, Held, Lead; public double Factor; }

    internal static class ItemBook
    {
        static BookEntry E(string name, double value, string gains, string costs, string need, string clock, string say)
        {
            return new BookEntry { Name = name, Value = value, Gains = gains, Costs = costs, Need = need, Clock = clock ?? "now", Say = say };
        }

        public static readonly BookEntry[] All =
        {
            // ---- name -------------------------- value  gains                                   costs              need              clock     say
            E("99'th Balloon",                      0.8,  "economy",                                null,              null,             "economy,grows",  "XP each second until a hit pops it").Economy("XP"),
            E("A Cookie",                           0.4,  "healing",                                null,              "skip",           null,     "+20% healing; a skip heals half max HP"),
            E("Access Keycard",                     1.0,  "survival, armor, economy:0.5, cash:0.3", null,              null,             null,     "HP, armor and XP per Parca item").Economy("XP"),
            E("Accumulator",                        3.0,  "pickups:0.5, control:0.5",               null,              "own:Electric",   null,     "magnets hit every enemy on screen"),
            E("ACME Anvil",                         0.8,  "~slow",                                  null,              null,             null,     "+50% damage to slowed or stunned enemies").Briefly("+50% damage to slowed enemies"),
            E("Acoustic Guitar",                    1.0,  "abilities",                              null,              null,             null,     null),
            E("An Apple",                           0.3,  "healing, survival:0.5",                  null,              null,             null,     "more Healthpaks, +20% healing"),
            E("Annoying Trumpet",                   0.4,  "control:0.5",                            null,              "own:Explosive",  null,     "an Explosive blast and stun when hit"),
            E("Ban Hammer",                         0.6,  "upgrade quality:0.5, economy:0.5",       null,              null,             "economy","a free banish on every screen, +10 luck").Economy("luck"),
            E("Barrel Roll",                        0.4,  "control:0.5",                            null,              "own:Explosive",  null,     "an Explosive blast and stun on a dodge").Briefly("a blast that stuns when you dodge"),
            E("Battery Leakage",                    0.0,  "electric, chemical",                     null,              null,             null,     "Electric damage toxifies, +2 tags"),
            E("Bells & Whistles",                   0.2,  "upgrade quality:0.5",                    null,              "lockdown",       "economy","better military cards under a lockdown").Briefly("better military cards in a lockdown"),
            E("Biofuel Energy",                     0.0,  "chemical, electric",                     null,              null,             null,     "Chemical damage electrifies, +2 tags"),
            E("Black Box",                          2.0,  "economy, cash:0.3",                      null,              null,             "economy,grows",  "a level-up for every chest").Economy("level-ups"),
            E("Bleeding Edge",                      1.0,  "slashing",                               null,              null,             null,     "bleeding ticks twice as fast"),
            E("Bloody Axe",                         1.0,  "slashing",                               null,              null,             null,     "bleeding lasts twice as long"),
            E("Boiling Pot",                        1.0,  null,                                     null,              "status3",        null,     "a third status effect blows enemies up").Briefly("3 status effects blow an enemy up"),
            E("Boxing Gloves",                      1.0,  "elites/bosses",                          null,              null,             null,     null),
            E("Brave Toaster",                     -1.5,  "status effects",                         null,              null,             null,     "status effects may spread a random one").Briefly("a status effect may add another"),
            E("Broken Glass",                       0.7,  "~slow",                                  null,              null,             null,     "+30% damage to slowed enemies"),
            E("Buckshot Roulette",                  0.8,  "weapons",                                null,              "clip:0.5",       null,     "quicker cooldowns, 20% instant reloads").Briefly("quicker cooldowns and reloads"),
            E("Bulletproof Vest",                   0.6,  "armor, survival:0.5",                    null,              null,             null,     "+30 armor, half damage from shots"),
            E("Burger",                             0.2,  "healing:1.5",                            "survival:0.5",    null,             null,     "triples healing, a fifth less max HP"),
            E("Camping Set",                        0.45, "weapons:0.5, abilities:0.5",             null,              null,             null,     "up to +75% damage while standing still").Briefly("up to +75% damage standing still"),
            E("Chick Magnet",                       2.0,  "abilities, pickups",                     null,              null,             null,     null)
                .ManaPotionHeld("Item_ChickMagnet", 0.5, "its cooldown bonus does nothing with Mana Potion",        // C16-01a: its 45 s +30% ability cooldown reduction after a magnet
                    "its cooldown bonus does nothing with Mana Potion", "Mana Potion cancels its cooldown part", "cooldown part off (Mana Potion)"),
            E("Chocolate Box",                      0.5,  "critical",                               null,              null,             null,     "crit chance from your max HP"),
            E("Coffee Cup",                         0.5,  "weapons, elites/bosses:0.5",             null,              null,             null,     "elite kills reload and speed up weapons").Briefly("elite kills reload your weapons"),
            E("Combat Knife",                       0.7,  null,                                     null,              "basic",          null,     "+30% damage to basic enemies"),
            E("Compass",                            0.3,  "abilities, move",                        null,              "move:abilities", null,     "+10 speed; speed adds ability power"),
            E("Congratulation Letter",              0.8,  "upgrade quality",                        null,              null,             "economy","military cards at least Rare"),
            E("Crowbar",                            0.3,  "weapons:0.5, abilities:0.5",             null,              null,             null,     "+30% damage below half HP; quick chests"),
            E("Dartboard",                          2.0,  "weapons",                                null,              "long",           null,     null),
            E("Detective's Pipe",                   1.0,  "fire, ice, electric",                    "kinetic, slashing, explosive", null, null,     null),
            E("Devil's Deal",                       1.0,  "weapons:0.5, abilities:0.5",             "survival:0.8",    null,             null,     "+30% survivor stats, a hit every 5s")
                .ManaPotionHeld("Item_DevilsDeal", 0.9, "its ability cooldown part is off (Mana Potion)"),     // C16-01a: PlayerAbilityCDRed is 1 of its 11 stats - a WHY reason only
            E("Duct Tape",                          0.8,  "line",                                   null,              null,             null,     null),
            E("Easter Egg",                        -1.5,  "upgrade quality:0.3",                    null,              null,             null,     null),
            E("Electric Personality",               2.0,  "pickups, upgrade quality:0.5",           null,              null,             "economy",null),
            E("Emerald Gem",                        1.0,  "economy, cash:0.5, dodge:0.3",           null,              "gems",           null,     "dodge, cash and XP per tag type at 3+").Economy("XP").Briefly("dodge, cash, XP per tag type at 3+"),
            E("Empty Chest",                        0.6,  null,                                     null,              "slots:damage",   null,     "+10% damage per empty item slot"),
            E("Expired Sushi",                      0.0,  "slashing, kinetic",                      null,              null,             null,     "Slashing damage injures, +2 tags"),
            E("Explosive Surprise",                 1.0,  "explosive",                              null,              null,             null,     "twice the implosion chance"),
            E("Farming Tools",                      0.2,  "cash",                                   null,              "squad",          "cash",   "+10% cash, class points per survivor"),
            E("Fire Extinguisher",                  0.0,  "fire, ice",                              null,              null,             null,     "Fire damage freezes, +2 tags"),
            E("Fishing Pole",                       2.0,  "weapons:0.3, abilities:0.3, survival:0.3", null,            null,             null,     "half hazard damage, then +100% damage").Briefly("half hazard damage, +100% damage"),
            E("Frozen Heart",                       1.0,  "survival, armor",                        null,              null,             "grows",  "max HP and armor each time you are hit").Briefly("max HP and armor when you are hit"),
            E("Frying Pan",                         0.5,  "control, weapons:0.5",                   null,              null,             null,     "weapon hits can stun"),
            E("Gaslighter",                         2.0,  "~status effects",                        null,              null,             null,     null).Briefly("+20% damage per status effect"),
            E("Giant Enemy Crab",                   2.0,  "critical",                               null,              null,             null,     null),
            E("Glass Cannon",                      -1.5,  null,                                     null,              "shield",         null,     null),
            E("Glass of Milk",                      0.3,  "survival",                               null,              null,             "grows",  "max HP grows with your kills"),
            E("Gold Medal",                         0.4,  "upgrade quality",                        "economy:0.6",     null,             "economy","more military cards, 10% slower levels").Economy("XP"),
            E("Golden Key",                         0.5,  "upgrade quality",                        null,              null,             "economy","chests may offer a second set"),
            E("Great Nade",                         1.0,  null,                                     null,              "own:Explosive",  null,     "throws an Explosive grenade every 10s").Briefly("an Explosive grenade every 10s"),
            E("Health Potion",                      0.4,  "healing, survival:0.5",                  null,              null,             null,     "Healthpaks heal three times as much"),
            E("Heavy Metal",                        1.0,  "pickups",                                null,              null,             "economy","twice as many magnets, +10% pickup range").Briefly("twice the magnets, +10% pickup range"),
            E("Hijacked Signal",                    1.0,  "economy, cash:0.3",                      null,              null,             "economy,grows",  "Liberate gives two level-ups").Economy("level-ups"),
            E("Hit Tracks",                         0.4,  "move, survival:0.3",                     null,              null,             null,     "+40 speed, slows on you halved"),
            E("Homing Pigeon",                      2.0,  "critical, abilities",                    null,              null,             null,     null).Briefly("abilities crit full-health enemies"),
            E("Hyperactivity",                      1.0,  "weapons, abilities, move",               null,              null,             null,     "+50% attack speed and cooldowns")
                .ManaPotionHeld("Item_Hyperactivity", 0.75, "its ability cooldown part is off (Mana Potion)",     // C16-01a: only the ability half of its +50% Cooldown Reduction
                    "its ability cooldown part is off (Mana Potion)", "Mana Potion cancels part of it"),
            E("Icon of Cinder",                     1.0,  "fire",                                   null,              null,             null,     "burning enemies may explode"),
            E("Icon of Pestilence",                 1.0,  "chemical",                               null,              null,             null,     "toxified enemies leave toxic clouds"),
            E("Icon of Stillness",                  1.0,  "ice",                                    null,              null,             null,     "frozen enemies freeze their neighbours").Briefly("frozen enemies freeze those nearby"),
            E("Icon of Tempest",                    1.0,  "electric",                               null,              null,             null,     "Electrify spreads to nearby enemies"),
            E("Jacob's Ladder",                     1.0,  "electric",                               null,              null,             null,     "Electrify lasts twice as long"),
            E("Jade Amulet",                        0.6,  "weapons:0.5, abilities:0.5",             null,              "luck",           null,     "damage bonus equal to your luck"),
            E("Jailbroken Phone",                   1.0,  "~taunt, economy:0.5",                    null,              "power:Taunt",    null,     "taunts become fears, richer XP gems").Economy("XP"),
            E("Jewel of Death",                    -1.0,  "economy",                                "survival",        null,             null,     "halves max HP, +44% XP").Economy("XP"),
            E("Jewel of Life",                      1.0,  "survival:1.5",                           null,              "revive",         null,     "one revive, then halves max HP"),
            E("Last Round",                         2.0,  "critical, weapons",                      null,              "clip",           null,     "the last bullet crits"),
            E("Last Unicorn",                       0.4,  "survival:0.3, economy:0.3",              null,              null,             null,     "luck, HP and damage per animal item").Economy("luck"),
            E("Life Savings",                       0.2,  "survival",                               "cash",            null,             "grows",  "cash heals you instead of paying out"),
            E("Magazine Clip",                      2.0,  "weapons",                                null,              "clip",           null,     "doubles every magazine"),
            E("Magical Hat",                        1.0,  "fire, ice, electric, chemical",          null,              "tags:hat",       null,     null),
            E("Mana Potion",                        2.0,  "abilities",                              null,              null,             null,     null),
            E("MedKit",                             0.7,  "survival, healing:0.5",                  null,              "hurt",           null,     "a full heal now, +100 max HP"),
            E("Metal Gear",                         0.3,  "abilities",                              null,              "pickup",         null,     "ability duration from pickup range"),
            E("Modchip",                            0.4,  "~critical, weapons",                     null,              null,             null,     "crits speed up your weapons"),
            E("Mouse Trap",                         0.9,  "taunt",                                  null,              "power:Taunt",    null,     "double damage to taunted enemies"),
            E("Mushroom Mushroom",                 -1.5,  null,                                     null,              null,             null,     "every equipped badge one rank up"),
            E("Nail Bat",                           0.0,  "kinetic, slashing",                      null,              null,             null,     "Kinetic damage bleeds, +2 tags"),
            E("Nine Inch Nails",                    1.0,  "kinetic",                                null,              null,             null,     "Injured lasts twice as long"),
            E("Nuclear Fusion",                     1.0,  null,                                     null,              "hurt",           null,     "clears the screen, then swaps for free"),
            E("Nuts & Bolts",                       0.4,  "abilities",                              null,              null,             null,     "abilities may halve their next cooldown").Briefly("abilities may halve a cooldown"),
            E("Omnigeode",                          1.0,  null,                                     null,              "tags:omni",      null,     "+1 tag to every damage type"),
            E("One For All",                        1.0,  null,                                     null,              null,             null,     null),
            E("Pawn Shop Receipt",                  1.0,  "weapons:0.5, abilities:0.5, economy:0.3",null,              null,             null,     "damage and luck for each item you equip").Economy("luck").Briefly("damage and luck per item equipped"),
            E("Pickup Pick",                        1.0,  "pickups",                                null,              null,             "economy","each timed power-up brings another"),
            E("Pills",                              0.8,  "weapons:0.3, abilities:0.3",             null,              "healthy",        null,     "full-health Healthpaks: +100% damage"),
            E("Plague's Visage",                    1.0,  "chemical",                               null,              null,             null,     "Toxify lasts twice as long"),
            E("Plot Armor",                         1.0,  "survival:1.5",                           null,              "revive",         null,     "survives a killing blow once a minute").Briefly("survives a killing blow every 60s"),
            E("Pocket Watch",                       0.5,  null,                                     null,              null,             null,     "timed power-ups last twice as long"),
            E("Positive Attitude",                  0.5,  "abilities:0.5, weapons:0.5",             null,              null,             null,     "a hit resets a cooldown and reloads"),
            E("Potato",                             0.0,  null,                                     null,              null,             null,     "the game hides what it does"),
            E("Power Generator",                    2.0,  "~critical, weapons, abilities",          null,              null,             null,     null),
            E("Power Glove",                        1.0,  "kinetic, slashing, explosive",           "fire, ice, electric", null,         null,     null),
            E("Power of Friendship",                0.6,  "survival:0.5, economy:1.0",              null,              "squad",          null,     "halves some hits, more in a big team").Economy("luck"),
            E("Quick Buck",                         0.3,  "cash, move:0.5",                         null,              null,             "cash",   "+20% cash; cash pickups speed you up"),
            E("Quicksilver Bullets",                0.5,  "weapons",                                null,              null,             null,     "+30% weapon attack speed"),
            E("Ragged Patch",                       2.0,  "upgrade quality",                        null,              null,             "economy",null),
            E("Reserve Bench",                      0.6,  "economy",                                null,              "skip",           "economy,grows",  "a level-up per chest or training skip").Economy("level-ups").Briefly("level-up per chest or training skip"),
            E("Retaliation",                        0.6,  "armor, weapons:0.5",                     null,              null,             null,     "+20 armor, faster weapons when hit"),
            E("Ring Of Power",                      1.0,  "weapons:0.5, abilities:0.5",             null,              null,             "grows",  "+10% damage per SOS answered, up to 40%"),
            E("Rock And Roll",                      0.8,  "upgrade quality, economy:0.5",           null,              null,             "economy","a free reroll on every screen, +10 luck").Economy("luck").Briefly("a free reroll per screen, +10 luck"),
            E("Ruby Gem",                           1.0,  "weapons",                                null,              "gems",           null,     "weapon power per tag type at 3+"),
            E("Sapphire Gem",                       1.0,  "abilities",                              null,              "gems",           null,     "ability power per tag type at 3+"),
            E("Scented Candle",                     0.4,  "status effects",                         null,              null,             null,     "+50% status chance, a bit shorter"),
            E("Schr\u00f6dinger's Cat",             0.3,  "upgrade quality:0.5",                    null,              "lockdown",       "economy","a locked military card grows rarer"),
            E("Scouter",                            2.0,  "critical, weapons",                      null,              null,             null,     null),
            E("Silencer",                           3.0,  "weapons",                                null,              "short",          null,     null),
            E("Silver Padlock",                     0.2,  "upgrade quality:0.5",                    null,              "lockdown",       "economy","extra item choices under a lockdown"),
            E("Skip Rope",                          0.3,  "economy:0.5",                            null,              "skip",           "economy","skips build XP and military points").Economy("XP and training points"),
            E("Slingshot",                          1.0,  "kinetic, critical",                      null,              null,             null,     "crits against Injured enemies"),
            E("Smooth Moves",                       0.6,  "dodge, survival:0.5",                    null,              null,             null,     "+10% dodge, often dodges shots"),
            E("Solar Panel",                        1.0,  "~critical, abilities",                   null,              null,             null,     "weapon crits cut ability cooldowns"),
            E("Special Snowflake",                  1.0,  "ice",                                    null,              null,             null,     "Frozen lasts twice as long"),
            E("Spoil Canister",                     1.0,  "fire",                                   null,              null,             null,     "Burn lasts twice as long"),
            E("Stretcher",                          1.0,  "healing:0.5, survival",                  null,              null,             null,     "regeneration while you keep moving"),
            E("Sugar Rush",                         0.2,  null,                                     null,              "boss",           null,     "a boss kill starts a Killing Frenzy"),
            E("Suspicious Pendrive",                0.8,  "control:0.5",                            null,              "own:Explosive",  null,     "opening a chest kills basic enemies"),
            E("T-Pose Doll",                        0.6,  "weapons:0.5, abilities:0.5",             null,              null,             null,     "up to +50% damage while you avoid hits").Briefly("up to +50% damage while unhurt"),
            E("Teddy Bear",                         0.6,  "upgrade quality, economy:0.5",           null,              null,             "economy","rerolls and banishes may cost nothing").Economy("luck").Briefly("rerolls and banishes may be free"),
            E("The Bomb",                           1.0,  "explosive",                              null,              null,             null,     "bigger explosions"),
            E("The Word",                           1.0,  "control",                                null,              null,             null,     "stops the horde 3s in every 20s"),
            E("Toilet Paper",                       0.2,  "chemical:0.5, move",                     null,              "move:Chemical",  null,     "+30 speed; speed adds Chemical damage"),
            E("Torchlight",                         0.6,  "critical",                               null,              null,             null,     "+40% critical damage"),
            E("Treasure Finder",                    0.6,  "upgrade quality",                        null,              null,             "economy","swap at any chest: +3 rerolls, banishes").Briefly("chest swaps: +3 rerolls, +3 banishes"),
            E("Ultra Instinct",                     1.0,  null,                                     null,              null,             null,     null),
            E("Vampire Survivor",                   0.5,  "healing:0.5, survival",                  null,              "solo",           null,     "your leader's hits heal you"),
            E("Walkie Talkie",                      0.4,  "abilities",                              null,              "squad",          null,     "ability area and duration per level"),
            E("Warm Ice Cream",                     0.0,  "ice, fire",                              null,              null,             null,     "Ice damage burns, +2 tags"),
            E("Well Prepped",                       1.0,  null,                                     null,              null,             "economy","one more item slot, kept after a swap"),
            E("Wooden Stick",                       2.0,  "economy",                                null,              "slots:xp",       "economy,grows",  null).Economy("XP"),
            E("Wrench",                             0.3,  "abilities",                              null,              "pickup",         null,     "ability area from pickup range"),
            E("Zugzwang Hypergaster XD",            0.0,  null,                                     null,              "tags:zug",       null,     "+5 to low tags, doubles a 6, -2 to high"),
        };

        // pairs the item texts state (worth more once the other half is held). Code, not knowledge.json: a player-edited knowledge
        // file is never refreshed (Knowledge.RefreshDefaults), and these are facts of the game's texts. ItemRules.Pairs takes the
        // union with knowledge.json's itemPairs, each unordered pair once. Devil's Deal: its self-hit calls Health.DealFlatDamage with
        // callOnDamageDelegate = true (the on-damage delegate a hit fires); its 10-16 text says it "can activate all on-hit effects" -
        // the four items a hit sets off.
        // 0.16.4: the "When equipped with" pairs of the 10-16 texts (the second block) - each worth what it does: the one pair weight
        // (+0.9, ItemRules.Pairs) where the partner doubles the item or adds an effect of its size (Annoying Trumpet + Barrel Roll, Camping
        // Set + Stretcher, Positive Attitude + Retaliation, Pills + MedKit), less for an extra +20% (PairWorths); Teddy Bear's "either Rock
        // And Roll or Ban Hammer" counts once (EitherPairs). The others are not here: the tag synergies (TagPairs, as tag points), the
        // conversion pairs (Converts' fourth column), Walkie Talkie + Power of Friendship (a rule, ItemRules.Need "squad").
        public static readonly string[][] Pairs =
        {
            new[] { "Omnigeode", "Ruby Gem" }, new[] { "Omnigeode", "Sapphire Gem" }, new[] { "The Word", "Pocket Watch" },
            new[] { "Devil's Deal", "Frozen Heart" }, new[] { "Devil's Deal", "Positive Attitude" }, new[] { "Devil's Deal", "Retaliation" }, new[] { "Devil's Deal", "Annoying Trumpet" },
            new[] { "Annoying Trumpet", "Barrel Roll" }, new[] { "Positive Attitude", "Retaliation" }, new[] { "Quick Buck", "Farming Tools" },
            new[] { "Teddy Bear", "Rock And Roll" }, new[] { "Teddy Bear", "Ban Hammer" }, new[] { "Pills", "MedKit" }, new[] { "Camping Set", "Stretcher" },
        };
        // 0.16.4: a pair the text states with EITHER of two items ("When equipped with either Rock And Roll or Ban Hammer: Chance is
        // further increased by +20%"): the first item, then the items that set it off - one of them is enough, a second adds nothing
        // (ItemRules.Pairs counts the first one held). Both pairs are in Pairs as well
        public static readonly string[][] EitherPairs =
        {
            new[] { "Teddy Bear", "Rock And Roll", "Ban Hammer" },
        };
        // 0.16.4 (review M3): the pairs of Pairs worth less than the one pair weight - an extra +20% of what the first item does, worth that
        // part of the item by the book's own method and on the same clock (ItemRules.PairWorth): item, partner, worth, clock (cash | economy)
        public static readonly string[][] PairWorths =
        {
            new[] { "Quick Buck", "Farming Tools", "0.2", "cash" },        // "+20% Cash modifier" more: Quick Buck's own +20% and its speed are 0.3
            new[] { "Teddy Bear", "Rock And Roll", "0.24", "economy" },    // its 50% chance 20 points up: 0.6 x 20 / 50
            new[] { "Teddy Bear", "Ban Hammer", "0.24", "economy" },
        };
        // 0.16.4 (review H2 / M4): the tag synergies of the 10-16 game data (sharedassets1: the item's synergyItem and synergyTagBonus;
        // ItemUnlockSuperEnemyStatusEffect.RefreshSynergyBonus adds the bonus to the item's own damage type tag while GetItemCount(the
        // partner) > 0, ItemExplosiveSurprise.RefreshSynergyBonus 6.0 Explosive): item, its type, the partner, the points. 6 = the texts'
        // "Doubles the damage type tag bonus gained from both items" (+2 and +4 again). Bleeding Edge and Slingshot point at EACH OTHER
        // (path ids 225340 / 225289) - not at Bloody Axe / Nine Inch Nails, as their texts say (TextOnly), and no item asset names either of
        // those two. Read by ItemRules.TagPoints (the card's grant), not by Pairs
        public static readonly string[][] TagPairs =
        {
            new[] { "Icon of Cinder", "Fire", "Spoil Canister", "6" }, new[] { "Icon of Pestilence", "Chemical", "Plague's Visage", "6" },
            new[] { "Icon of Stillness", "Ice", "Special Snowflake", "6" }, new[] { "Icon of Tempest", "Electric", "Jacob's Ladder", "6" },
            new[] { "Bleeding Edge", "Slashing", "Slingshot", "6" }, new[] { "Slingshot", "Kinetic", "Bleeding Edge", "6" },
            new[] { "Explosive Surprise", "Explosive", "The Bomb", "6" },
        };
        // 0.16.4 (review H1 / H2): what a 10-16 text states that the game's data or code does not do - the item, the partner its text
        // names. The book advises what the game does (TagPairs, Converts); the bench holds these apart (IB10 / IB12), so a game fix shows
        public static readonly string[][] TextOnly =
        {
            new[] { "Bleeding Edge", "Bloody Axe" }, new[] { "Slingshot", "Nine Inch Nails" },       // their synergyItem is each other
            new[] { "Battery Leakage", "Biofuel Energy" },        // "Electric damage applies both effects": ApplyElectrified tests no partner
            new[] { "Warm Ice Cream", "Fire Extinguisher" },      // "Ice damage applies both effects": ApplyFrozen tests Biofuel Energy instead
        };
        // clashes: one works against the other (worth less once the other is held). The status conversions (Fire Extinguisher ...
        // Expired Sushi against the status items) and Mana Potion against the cooldown items are the held-item rules' (C16-01j,
        // C16-01a/k), squad-aware; Jailbroken Phone and Mouse Trap do NOT clash (Mouse Trap's bonus reads taunted OR feared,
        // GameplayMaster.GetAdditionalDamageMod 0x1806835b7).
        public static readonly string[][] Clashes =
        {
            new[] { "Devil's Deal", "99'th Balloon" }, new[] { "Devil's Deal", "T-Pose Doll" },
        };
        // the one mixed effect the texts name: Hyperactivity "When equipped with Smooth Moves: Reduces this item's bonuses by half, but
        // enemy projectile attacks always miss"
        public static readonly BookMixed[] Mixed =
        {
            new BookMixed { Offered = "Hyperactivity", Held = "Smooth Moves", Factor = 0.6, Lead = "halved by your Smooth Moves; shots miss" },
            new BookMixed { Offered = "Smooth Moves", Held = "Hyperactivity", Factor = 1.0, Lead = "halves your Hyperactivity; shots miss" },
        };

        // the status each damage type applies, and the six conversion items (their 1.0.2 texts) - Boiling Pot's need counts statuses.
        // Converts is the ONE conversion table (C16-02 section 8): the held-item rules (C16-01j) read it, never a second list.
        // 0.16.4: the fourth column is the partner that makes the item's type apply both its own status AND the converted one
        // (HeldRules.StatusesOfType) - read in the 10-16 game code, which differs from two of the texts ("When equipped with Fire
        // Extinguisher: Ice damage applies both effects"). EnemyStatusApplication.ApplyBurn: Fire Extinguisher -> Freeze, and with Warm Ice
        // Cream active Burn too; ApplyFrozen: Warm Ice Cream -> Burn, and with BIOFUEL ENERGY active Freeze too; ApplyElectrified: Battery
        // Leakage -> Toxify, no partner (no fourth column); OnHitTryApplyStatusToxified: Biofuel Energy -> Electrify, and with Battery
        // Leakage Toxify too; OnHitTryApplyStatusInjured / ...Bleed: Nail Bat and Expired Sushi both ways. No other code reads the six
        // items (a whole-code scan; TextOnly lists the two texts the code does not follow)
        public static readonly string[][] StatusOf =
        {
            new[] { "Fire", "Burn" }, new[] { "Ice", "Freeze" }, new[] { "Electric", "Electrify" },
            new[] { "Chemical", "Toxify" }, new[] { "Slashing", "Bleed" }, new[] { "Kinetic", "Injured" },
        };
        public static readonly string[][] Converts =       // item, damage type, the status it applies instead, the partner that makes it both
        {
            new[] { "Fire Extinguisher", "Fire", "Freeze", "Warm Ice Cream" }, new[] { "Warm Ice Cream", "Ice", "Burn", "Biofuel Energy" },
            new[] { "Battery Leakage", "Electric", "Toxify" }, new[] { "Biofuel Energy", "Chemical", "Electrify", "Battery Leakage" },
            new[] { "Nail Bat", "Kinetic", "Bleed", "Expired Sushi" }, new[] { "Expired Sushi", "Slashing", "Injured", "Nail Bat" },
        };

        // 0.16.0 (C16-12): the second lives the game offers outside the chest - the level-up abilities that revive the squad (the
        // Medic's Resuscitation). The hint guard (C16-13) reads them beside Revive(); the bench checks each is the game's power name
        public static readonly string[] ReviveAbilities = { "Resuscitation" };

        static Dictionary<string, BookEntry> _byName;
        public static readonly List<string> BadTags = new List<string>();       // rows with a tag ItemRules.All lacks ('[items] bad rule tag', once each)

        /// <summary>[Debug] ItemKeywords: true = every chest item scored by the 0.15.0 keyword reading instead of the item book, for
        /// comparison (ItemRules.UseBook = !on). Called by Plugin.Load at the bind and on every change of the setting.</summary>
        public static void UseKeywords(bool on) { ItemRules.UseBook = !on; }

        /// <summary>0.16.0 (C16-12, DK-C01): the item is a second life - its row's need is "revive" (Jewel of Life, Plot Armor in the 1.0.2
        /// pool). False for a name the book lacks.</summary>
        public static bool Revive(string name) { var e = Of(name); return e != null && e.Need == "revive"; }

        /// <summary>Builds the name index and every row's weights once, a try per row: an unknown tag weighs 0 here, is listed in BadTags
        /// and logged once ('[items] bad rule tag: ' + the row + ': unknown item tag ...'), so a typo can never throw in a live chest offer (Score
        /// never builds weights lazily). The bench (IB2) calls Weights directly, which throws.</summary>
        static void Ready()
        {
            if (_byName != null) return;
            var d = new Dictionary<string, BookEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in All)
            {
                d[e.Name] = e;
                string bad = null;
                try { e.Uses = UsesOf(e.Gains, ItemRules.All); } catch (Exception x) { e.Uses = new bool[ItemRules.All.Length]; bad = x.Message; }
                try { e.GainW = Weights(e.Gains, ItemRules.All); } catch (Exception x) { e.GainW = new double[ItemRules.All.Length]; bad = bad ?? x.Message; }
                try { e.CostW = Weights(e.Costs, ItemRules.All); } catch (Exception x) { e.CostW = new double[ItemRules.All.Length]; bad = bad ?? x.Message; }
                if (bad == null) continue;
                BadTags.Add(e.Name + ": " + bad);
                try { Plugin.Logger.LogWarning("[items] bad rule tag: " + e.Name + ": " + bad); } catch { }
            }
            _byName = d;
        }

        public static BookEntry Of(string name)
        {
            if (name == null) return null;
            Ready();
            BookEntry r; return _byName.TryGetValue(name.Trim(), out r) ? r : null;
        }

        /// <summary>0.16.0 (C16-01a): the row whose game asset is <paramref name="asset"/> (only the rows that name one: the held-item rules'
        /// items), for a card the game renamed; null when none.</summary>
        public static BookEntry OfAsset(string asset)
        {
            if (string.IsNullOrEmpty(asset)) return null;
            foreach (var e in All) if (e.Asset != null && string.Equals(e.Asset, asset.Trim(), StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        /// <summary>Which rules of ItemRules.All a "~" gain names (unknown tags: none).</summary>
        public static bool[] UsesOf(string list, ItemRule[] rules)
        {
            var u = new bool[rules.Length];
            if (string.IsNullOrEmpty(list)) return u;
            foreach (var part in list.Split(','))
            {
                string p = part.Trim(); if (!p.StartsWith("~")) continue;
                p = p.Substring(1); int colon = p.LastIndexOf(':'); if (colon > 0) p = p.Substring(0, colon);
                int i = Array.FindIndex(rules, r => string.Equals(r.Tag, p.Trim(), StringComparison.OrdinalIgnoreCase));
                if (i >= 0) u[i] = true;
            }
            return u;
        }

        /// <summary>The weights per rule of ItemRules.All, from a "tag:w, tag" list (null: none). Unknown tags throw.</summary>
        public static double[] Weights(string list, ItemRule[] rules)
        {
            var w = new double[rules.Length];
            if (string.IsNullOrEmpty(list) || list == "line") return w;
            foreach (var part in list.Split(','))
            {
                string p = part.Trim().TrimStart('~'); if (p.Length == 0) continue;
                double weight = 1; int colon = p.LastIndexOf(':');
                if (colon > 0 && double.TryParse(p.Substring(colon + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out weight)) p = p.Substring(0, colon).Trim();
                else weight = 1;
                int i = Array.FindIndex(rules, r => string.Equals(r.Tag, p, StringComparison.OrdinalIgnoreCase));
                if (i < 0) throw new ArgumentException("unknown item tag '" + p + "'");
                w[i] = weight;
            }
            return w;
        }
    }
}
