// 0.16.0 (C16-01): what the items the squad HOLDS change - one pure table every consumer asks "what do the held items change
// here?". Pure (no game types): the bench compiles it (ItemBench.csproj links it), so every number a held-item rule adds is a
// function here that the game-side files (Ranker, RerollHint, Plan, Advisor) only call; HeldRead.cs reads the facts into
// Snapshot.Held once per snapshot (GameState.Read).
//
// The rules (each read in the game's machine code, research\roadmap_1007\scratch\c16_held_items):
//   C16-01a  Mana Potion held: every ability cooldown reduction stat bonus is 0 (GamePlayer.GetStatisticFinalValue returns 0 for
//            PlayerAbilityCDRed while ItemManaPotion is active) - a Chem-Light Battery card does nothing (DeadCard), Chick Magnet /
//            Hyperactivity / Devil's Deal keep only the rest of their worth (the shares sit on their item book rows, ItemBook.cs
//            ManaPotionHeld), Grenade Trail: Shrapnel loses its scaling (ShrapnelCut).
//   C16-01k  the Mana Potion card itself: what it gives (an ability reset every 4 s, spread at random over the abilities with a
//            cooldown) against what it costs (the squad's ability cooldown reduction), from the live cooldowns (ManaPotionGain).
//   C16-01b  Reserve Bench held: a chest or training skip is a level-up (REROLL first, SKIP once no reroll is left), and a recruit brings
//            a level-up per 2 minutes since the last join (rescue cards, Liberate, the PLAN SOS row, the rescue reroll hint).
//   C16-01d  Hijacked Signal held: Liberate gives two level-ups (the card, the PLAN SOS row); a chest item pays the skip's cash too.
//   C16-01e  Life Savings held: granted cash heals instead - the skip's and Liberate's cash read as a heal, cash items are weighed as healing.
//   C16-01f  A Cookie (a skip heals 500 more) and Skip Rope (a skip pays points toward a Military Training and XP) raise the skip's worth.
//   C16-01c  Wooden Stick / Empty Chest held: an item that fills an empty slot costs 10% XP (1.6 x economy) / 10% weapon and ability
//            damage (2.0) - the card's net decides AVOID and SKIP, its merit (before the cost) decides REROLL (ScreenCall).
//   C16-01i  Last Unicorn counts the animal items held (the game's own isAnimalItem flag, itself included): +0.9 per animal item held (two at
//            most) on its card, and an animal item pairs with a held Last Unicorn (ItemRules.Pairs).
//   C16-01j  the status conversions (Fire Extinguisher ... Expired Sushi, ItemBook.Converts): a status item is judged by what still causes
//            its status on this squad (StatusFit), and a conversion item card by what it would stop or feed of the status items held.
// Held items with no rule (C16-01g, each checked in machine code - they change no pick the Companion advises):
//   Black Box (its level-up arrives when the chest opens, its cash on any rescue pick), Ring Of Power (every rescue card applied
//   stacks it, Liberate included), Ban Hammer and Rock And Roll (the game's FREE banish / reroll labels, read already), Teddy Bear
//   (a 50% chance not to spend a reroll - no card value changes), Jailbroken Phone with Mouse Trap (no conflict: Mouse Trap reads a
//   taunted OR a feared enemy), Treasure Finder, Pawn Shop Receipt, Nuclear Fusion and Easter Egg's first clause (the item replace
//   and swap screens, not advised yet), Gold Medal (folded into the skip and level-up grants).
// [Debug] HeldPretend (Plugin.cs) names items the advice treats as held - only HeldFacts.Pretend and ItemContext.Held see them, never
// the game; a Warning once a session (PretendWarning) and every [held] line led by 'PRETEND (debug): <names> - ' make a leftover
// visible.
// Game names only (the repository is public).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YazsCompanion
{
    /// <summary>An item a held-item rule is about: the table's id, the game's English name and its asset (an asset match survives a
    /// rename).</summary>
    internal sealed class HeldItem
    {
        public string Id, Name, Asset;
        public HeldItem(string id, string name, string asset) { Id = id; Name = name; Asset = asset; }
        public override string ToString() { return Name; }
    }

    /// <summary>What the squad holds and what the held-item rules read beside it, one per snapshot (Snapshot.Held, filled by
    /// HeldRead.Fill). -1 / NaN / null = not read.</summary>
    internal sealed class HeldFacts
    {
        public HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);      // the game's names of the items held
        public HashSet<string> Assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);     // their assets ('(Clone)' cut)
        public HashSet<string> Pretend = new HashSet<string>(StringComparer.OrdinalIgnoreCase);    // [Debug] HeldPretend: treated as held
        public int Max = -1, Equipped = -1, FreeSlots = -1, FreeStat = -1;     // item slots: the team's maximum, filled, empty (the game's own
                                                                               // formula), and statistic 50 as read (the log only)
        public double LeaderCdr = double.NaN;      // the leader's ability cooldown reduction (PlayerAbilityCDRed, 0..1)
        public double SinceJoin = -1;              // seconds of play since the last survivor joined (-1: not read)
        public int AnimalHeld, AbilitiesOwned;     // animal items held (pretend names counted), the squad's abilities at level 1 or more
        public List<KeyValuePair<double, double>> Cooldowns;     // C16-01k: (base, with the stat) per ability with a cooldown; null = not read

        public bool Has(HeldItem i) { return i != null && (Names.Contains(i.Name) || Assets.Contains(i.Asset) || Pretend.Contains(i.Name)); }
        public bool HasName(string n) { return n != null && (Names.Contains(n) || Pretend.Contains(n)); }
        public bool AnyPretend { get { return Pretend.Count > 0; } }
    }

    internal static class HeldRules
    {
        /// <summary>A reason caused by an item the squad holds (WhyText lists these first).</summary>
        public const string Prefix = "held: ";
        /// <summary>What this card would do to what is held or invested (C16-01k).</summary>
        public const string ItemPrefix = "item: ";
        /// <summary>Liberate's own base for 'the level-up and cash' (Ranker.Liberate).</summary>
        public const double RescueLevelUp = 1.0;

        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // ---- the items a held-item rule is about (exact 1.0.2 names and assets, data\probe.json)
        public static readonly HeldItem ManaPotion = new HeldItem("ManaPotion", "Mana Potion", "Item_ManaPotion");
        public static readonly HeldItem ReserveBench = new HeldItem("ReserveBench", "Reserve Bench", "Item_ReserveBench");
        public static readonly HeldItem WoodenStick = new HeldItem("WoodenStick", "Wooden Stick", "Item_WoodenStick");
        public static readonly HeldItem EmptyChest = new HeldItem("EmptyChest", "Empty Chest", "Item_EmptyChest");
        public static readonly HeldItem HijackedSignal = new HeldItem("HijackedSignal", "Hijacked Signal", "Item_Quest_004_HijackedSignal");
        public static readonly HeldItem LifeSavings = new HeldItem("LifeSavings", "Life Savings", "Item_LifeSavings");
        public static readonly HeldItem ACookie = new HeldItem("ACookie", "A Cookie", "Item_ACookie");
        public static readonly HeldItem SkipRope = new HeldItem("SkipRope", "Skip Rope", "Item_SkipRope");
        public static readonly HeldItem GoldMedal = new HeldItem("GoldMedal", "Gold Medal", "Item_GoldMedal");
        public static readonly HeldItem LastUnicorn = new HeldItem("LastUnicorn", "Last Unicorn", "Item_LastUnicorn");
        public static readonly HeldItem FireExtinguisher = new HeldItem("FireExtinguisher", "Fire Extinguisher", "Item_FireExtinguisher");
        public static readonly HeldItem WarmIceCream = new HeldItem("WarmIceCream", "Warm Ice Cream", "Item_WarmIceCream");
        public static readonly HeldItem BatteryLeakage = new HeldItem("BatteryLeakage", "Battery Leakage", "Item_BatteryLeakage");
        public static readonly HeldItem BiofuelEnergy = new HeldItem("BiofuelEnergy", "Biofuel Energy", "Item_BiofuelEnergy");
        public static readonly HeldItem NailBat = new HeldItem("NailBat", "Nail Bat", "Item_NailBat");
        public static readonly HeldItem ExpiredSushi = new HeldItem("ExpiredSushi", "Expired Sushi", "Item_ExpiredSushi");

        /// <summary>Every item a held-item rule reads, in the table's order (Active keeps it).</summary>
        public static readonly HeldItem[] All =
        {
            ManaPotion, ReserveBench, WoodenStick, EmptyChest, HijackedSignal, LifeSavings, ACookie, SkipRope, GoldMedal, LastUnicorn,
            FireExtinguisher, WarmIceCream, BatteryLeakage, BiofuelEnergy, NailBat, ExpiredSushi,
        };

        /// <summary>The 8 items data\probe.json flags animal (1.0.2) - used ONLY to count pretend names (the live count reads the game's
        /// own isAnimalItem flag). The game spells the cat with an o-umlaut: written as an escape here and matched by its asset; never
        /// written into a line (Wording.Rails flags it).</summary>
        public static readonly HeldItem[] AnimalNames =
        {
            new HeldItem("ChickMagnet", "Chick Magnet", "Item_ChickMagnet"),
            new HeldItem("GiantEnemyCrab", "Giant Enemy Crab", "Item_GiantEnemyCrab"),
            new HeldItem("HomingPigeon", "Homing Pigeon", "Item_HomingPigeon"),
            new HeldItem("TeddyBear", "Teddy Bear", "Item_TeddyBear"),
            new HeldItem("SchrodingersCat", "Schr\u00f6dinger's Cat", "Item_SchrodingersCat"),
            LastUnicorn,
            LifeSavings,
            new HeldItem("MouseTrap", "Mouse Trap", "Item_MouseTrap"),
        };

        /// <summary>[Debug] HeldPretend as names: split on ',', trimmed, empty entries dropped (the order kept, no duplicates).</summary>
        public static List<string> SplitNames(string raw)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(raw)) return list;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in raw.Split(','))
            {
                string n = part.Trim();
                if (n.Length > 0 && seen.Add(n)) list.Add(n);
            }
            return list;
        }

        // ================================================================ names and assets
        /// <summary>A name to compare by: lower case, the umlaut of the cat's name folded to its plain letter (a pretend name typed
        /// without it still matches).</summary>
        static string Fold(string s) { return (s ?? "").Trim().Replace('\u00f6', 'o').Replace('\u00d6', 'O').ToLowerInvariant(); }
        static bool Is(string a, string b) { return a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        /// <summary>An item object's asset without the '(Clone)' an instantiated copy carries (whether held items are clones is unverified:
        /// cut either way).</summary>
        public static string CutClone(string asset)
        {
            if (string.IsNullOrEmpty(asset)) return "";
            string a = asset.Trim();
            return a.EndsWith("(Clone)", StringComparison.Ordinal) ? a.Substring(0, a.Length - "(Clone)".Length).TrimEnd() : a;
        }

        /// <summary>The card's item is <paramref name="i"/>: by the game's name, or by its asset (a renamed item).</summary>
        public static bool IsItem(HeldItem i, string name, string asset)
        {
            if (i == null) return false;
            return Is(name, i.Name) || (!string.IsNullOrEmpty(asset) && Is(CutClone(asset), i.Asset));
        }

        /// <summary>A name of the held-item table (the umlaut folded).</summary>
        public static bool InTable(string name) { string f = Fold(name); return f.Length > 0 && All.Any(i => Fold(i.Name) == f); }

        /// <summary>A name of an animal item of 1.0.2 (the umlaut folded) - what a pretend name counts for (HeldFacts.AnimalHeld).</summary>
        public static bool IsAnimalName(string name) { return AnimalOf(name) != null; }
        public static HeldItem AnimalOf(string name) { string f = Fold(name); return f.Length == 0 ? null : AnimalNames.FirstOrDefault(i => Fold(i.Name) == f); }

        // ================================================================ C16-01: the [held] lines, the run, the debug key
        /// <summary>The once-a-session Warning while [Debug] HeldPretend names items: '[held] HeldPretend is on (debug): &lt;names&gt; - the
        /// advice treats them as held; the game is unchanged' (+ ' | not a held-item rule's item: &lt;names&gt;'); null = nothing to say.</summary>
        public static string PretendWarning(IList<string> names)
        {
            if (names == null) return null;
            var list = names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToList();
            if (list.Count == 0) return null;
            var none = list.Where(n => !InTable(n) && !IsAnimalName(n)).ToList();
            return "[held] HeldPretend is on (debug): " + string.Join(", ", list) + " - the advice treats them as held; the game is unchanged"
                + (none.Count > 0 ? " | not a held-item rule's item: " + string.Join(", ", none) : "");
        }

        /// <summary>The table's items the squad holds (or pretends to), in table order.</summary>
        public static List<HeldItem> Active(HeldFacts h)
        {
            var list = new List<HeldItem>();
            if (h == null) return list;
            foreach (var i in All) if (h.Has(i)) list.Add(i);
            return list;
        }

        /// <summary>'PRETEND (debug): &lt;names&gt; - ' while [Debug] HeldPretend names items (every [held] line starts with it), else empty.</summary>
        public static string PretendLead(HeldFacts h)
        {
            return h != null && h.AnyPretend ? "PRETEND (debug): " + string.Join(", ", h.Pretend) + " - " : "";
        }

        /// <summary>The '[held] ...' line of an offer: what each held item changes now; null when nothing is active (or no active item
        /// has its part yet).</summary>
        public static string Line(HeldFacts h, RunContext ctx)
        {
            if (h == null) return null;
            var parts = new List<string>();
            foreach (var i in Active(h)) { string p = Part(i, h, ctx); if (!string.IsNullOrEmpty(p)) parts.Add(p); }
            if (parts.Count == 0) return null;
            return "[held] " + PretendLead(h) + string.Join(" | ", parts);
        }

        /// <summary>One held item's part of the [held] line (null: the item has no rule here yet). Each lane adds its item's part with its
        /// rule: Reserve Bench, Hijacked Signal, Life Savings, A Cookie, Skip Rope (C16-01b, d, e, f), Wooden Stick / Empty Chest (C16-01c),
        /// the conversion items (C16-01j).</summary>
        static string Part(HeldItem i, HeldFacts h, RunContext ctx)
        {
            switch (i.Id)
            {
                case "ManaPotion": return "Mana Potion: ability cooldown reduction off";        // C16-01a
                case "ReserveBench":                                                              // C16-01b
                    return "Reserve Bench: a chest or training skip is a level-up; recruiting gives "
                        + (h.SinceJoin < 0 || double.IsNaN(h.SinceJoin) || double.IsInfinity(h.SinceJoin) ? "? (the time since the last join not read)" : RecruitLevelUps(h).ToString(IC) + " (" + MinSec(h.SinceJoin) + " since the last join)");
                case "HijackedSignal": return "Hijacked Signal: Liberate gives two level-ups; chest items pay cash";      // C16-01d
                case "LifeSavings": return "Life Savings: cash heals instead";                    // C16-01e
                case "ACookie": return "A Cookie: a skip heals " + CookieHeal.ToString(IC) + " more";                     // C16-01f
                case "SkipRope": return "Skip Rope: a skip pays " + RopeBig.ToString(IC) + " points (" + RopeSmall.ToString(IC) + " on a level-up)";   // C16-01f
                case "WoodenStick": return SlotPart(i, h, "XP", Math.Round(WoodenStickCost * Economy(ctx), 2), " now");             // C16-01c
                case "EmptyChest": return SlotPart(i, h, "damage", EmptyChestCost, "");                                           // C16-01c
                case "FireExtinguisher": case "WarmIceCream": case "BatteryLeakage": case "BiofuelEnergy": case "NailBat": case "ExpiredSushi":
                    return ConvertPart(i);                                                                                      // C16-01j
                default: return null;
            }
        }

        /// <summary>Seconds as the [held] line writes a time: m:ss (whole seconds, down).</summary>
        public static string MinSec(double seconds)
        {
            int t = seconds > 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds) ? (int)Math.Floor(seconds) : 0;
            return (t / 60).ToString(IC) + ":" + (t % 60).ToString("00", IC);
        }

        /// <summary>The '[held] reads: ...' line, once per run (slots, the leader's cooldown reduction, the time since the last join,
        /// the animal items, the abilities with a cooldown).</summary>
        public static string ReadsLine(HeldFacts h)
        {
            if (h == null) return null;
            return "[held] " + PretendLead(h) + "reads: item slots " + N(h.Equipped) + " of " + N(h.Max) + " filled, " + N(h.FreeSlots) + " empty (team statistic: " + N(h.FreeStat) + ")"
                + "; leader's ability cooldown reduction " + (double.IsNaN(h.LeaderCdr) || double.IsInfinity(h.LeaderCdr) ? "?" : Math.Round(h.LeaderCdr * 100, MidpointRounding.AwayFromZero).ToString("0", IC) + "%")
                + "; " + (h.SinceJoin < 0 || double.IsNaN(h.SinceJoin) ? "?" : Math.Round(h.SinceJoin, MidpointRounding.AwayFromZero).ToString("0", IC)) + " s since the last survivor joined"
                + "; animal items held " + h.AnimalHeld.ToString(IC)
                + "; abilities with a cooldown " + (h.Cooldowns == null ? "?" : h.Cooldowns.Count.ToString(IC)) + " of " + h.AbilitiesOwned.ToString(IC) + " owned";
        }
        static string N(int v) { return v < 0 ? "?" : v.ToString(IC); }

        static bool _runSeen; static double _runSeconds; static long _runKey;
        /// <summary>True on the first call, when the play clock went back by more than 5 s (Try Again keeps the scene and the
        /// GameplayMaster) or when the GameplayMaster changed; the bench resets it with <see cref="ResetRun"/>.</summary>
        public static bool NewRun(double seconds, long masterKey)
        {
            bool fresh = !_runSeen || seconds + 5 < _runSeconds || (masterKey != 0 && _runKey != 0 && masterKey != _runKey);
            _runSeen = true; _runSeconds = seconds;
            if (masterKey != 0) _runKey = masterKey;
            return fresh;
        }

        /// <summary>Forget what <see cref="NewRun"/> remembers (the bench).</summary>
        public static void ResetRun() { _runSeen = false; _runSeconds = 0; _runKey = 0; }

        /// <summary>The free item slots by the game's own formula (GamePlayer.AddItem: statistic 50 = (int)(statistic 43 - statistic 49)):
        /// max(0, max - equipped); -1 when the maximum is not read.</summary>
        public static int FreeOf(int max, int equipped) { return max >= 0 ? Math.Max(0, max - Math.Max(0, equipped)) : -1; }

        /// <summary>A statistic read as a count: rounded, -1 when it is NaN, infinite or negative (not read).</summary>
        public static int CountOf(double v) { return double.IsNaN(v) || double.IsInfinity(v) || v < 0 ? -1 : (int)Math.Round(v, MidpointRounding.AwayFromZero); }

        /// <summary>The filled item slots: statistic 49 as read, or - NaN or negative - the DISTINCT items held (a stack takes one slot;
        /// not Snapshot.ItemsHeld, which sums the stack counts).</summary>
        public static int EquippedOf(double stat49, int distinctHeld) { int n = CountOf(stat49); return n >= 0 ? n : Math.Max(0, distinctHeld); }

        // ================================================================ C16-01a: Mana Potion held - every ability cooldown reduction bonus is 0
        /// <summary>A stat card that does nothing scores this: under 1 (AVOID) and under every live stat card (StatCard's floor is 1.0).</summary>
        public const double DeadCard = 0.80;
        /// <summary>What Grenade Trail: Shrapnel loses without a cooldown reduction to scale its drops with.</summary>
        public const double ShrapnelCut = 0.3;
        public const string ShrapnelName = "Grenade Trail: Shrapnel", ShrapnelAsset = "Grenade Trail-Infite";
        /// <summary>The dead stat card's words, longest first (39 / 27).</summary>
        public static readonly string[] DeadSay = { "does nothing while you hold Mana Potion", "Mana Potion switches it off" };
        public const string DeadWhy = Prefix + "does nothing while you hold Mana Potion";
        public const string ShrapnelWhy = Prefix + "no cooldown reduction to scale with (Mana Potion)";

        /// <summary>Mana Potion is held (or pretended): the game returns 0 for every ability cooldown reduction stat bonus.</summary>
        public static bool CdrOff(HeldFacts h) { return h != null && h.Has(ManaPotion); }

        /// <summary>The stat card of <paramref name="stat"/> (the asset's: AbilityCooldown; the statistic: PlayerAbilityCDRed) does nothing
        /// while Mana Potion is held; <paramref name="say"/> = its words.</summary>
        public static bool StatDead(HeldFacts h, string stat, out string[] say)
        {
            say = null;
            if (!CdrOff(h) || string.IsNullOrEmpty(stat)) return false;
            if (!Is(stat, "AbilityCooldown") && stat.IndexOf("AbilityCDRed", StringComparison.OrdinalIgnoreCase) < 0) return false;
            say = (string[])DeadSay.Clone();
            return true;
        }

        /// <summary>Grenade Trail: Shrapnel (its drops scale with the ability cooldown reduction) while Mana Potion is held.</summary>
        public static bool ShrapnelOff(HeldFacts h, string name, string asset)
        {
            return CdrOff(h) && (Is(name, ShrapnelName) || Is(CutClone(asset), ShrapnelAsset));
        }

        /// <summary>A chest item whose worth is partly an ability cooldown reduction bonus, while Mana Potion is held: its score keeps
        /// 1 + (score - 1) x the row's share (ItemBook ManaPotionHeld: Chick Magnet 0.5, Hyperactivity 0.75, Devil's Deal 0.9);
        /// <paramref name="lead"/> = the card's words (null: a WHY reason only), <paramref name="why"/> = the reason with its prefix.
        /// Unchanged otherwise.</summary>
        public static double CdrItem(HeldFacts h, string name, string asset, double score, out string[] lead, out string why)
        {
            lead = null; why = null;
            if (!CdrOff(h)) return score;
            var row = ItemBook.Of(name) ?? ItemBook.OfAsset(CutClone(asset));
            if (row == null || row.ManaKeep >= 1 || row.ManaWhy == null) return score;
            lead = row.ManaLead != null ? (string[])row.ManaLead.Clone() : null;
            why = Prefix + row.ManaWhy;
            return 1 + (score - 1) * row.ManaKeep;
        }

        // ================================================================ C16-01k: the Mana Potion card - an ability reset every 4 s against the
        // squad's ability cooldown reduction. ItemManaPotion.Process takes a random candidate every 4 s and cuts its currentCooldownTimer by
        // 250 (it fires at once and a fresh cooldown starts, the stat's reduction applied - a reset is not a free cast); with Mana Potion
        // held the stat is 0, so each ability runs on its base cooldown. Random resets at rate lambda = 1 / (4 x N) per ability cut a
        // cooldown of b short: the expected cast rate is lambda / (1 - exp(-lambda x b)).
        public const double ManaInterval = 4.0;
        const double MinCooldown = 0.05;        // PowerupBase.InstantAbilityCooldown: shorter is no cooldown

        /// <summary>Casts with Mana Potion held over casts now (1 = the same; 0 = no ability with a cooldown): N = the abilities with a
        /// cooldown (base, with the stat); with = sum lambda / (1 - exp(-lambda x base)), lambda = 1 / (interval x N); now = sum 1 / withStat.</summary>
        public static double ManaPotionGain(IList<KeyValuePair<double, double>> cds, double interval = ManaInterval)
        {
            if (cds == null) return 0;
            var valid = cds.Where(kv => kv.Key > MinCooldown).ToList();
            int n = valid.Count;
            if (n == 0 || interval <= 0) return 0;
            double lambda = 1.0 / (interval * n), with = 0, now = 0;
            foreach (var kv in valid)
            {
                with += lambda / (1 - Math.Exp(-lambda * kv.Key));
                now += 1.0 / Math.Max(MinCooldown, kv.Value);
            }
            return now > 0 ? with / now : 0;
        }

        /// <summary>The squad's ability cooldown reduction in per cent: the mean of 1 - withStat / base over the abilities with a cooldown.</summary>
        public static int CdrPct(IList<KeyValuePair<double, double>> cds)
        {
            if (cds == null) return 0;
            var valid = cds.Where(kv => kv.Key > MinCooldown).ToList();
            if (valid.Count == 0) return 0;
            double mean = valid.Average(kv => 1 - Math.Max(MinCooldown, kv.Value) / kv.Key);
            return (int)Math.Round(100 * mean, MidpointRounding.AwayFromZero);
        }

        /// <summary>The Mana Potion card's held-state term (it is not held; <paramref name="cds"/> = HeldFacts.Cooldowns, null: not read -
        /// false, nothing changes): the guide tier times what it gives against what it costs. <paramref name="lead"/> = the card's words
        /// (null: none), <paramref name="log"/> = the log's 'Mana Potion: casts x... over N abilities with a cooldown' (no prefix: it
        /// stays in the log).</summary>
        public static bool ManaPotionCard(IList<KeyValuePair<double, double>> cds, ref double tierScore, out string[] lead, out string log)
        {
            lead = null; log = null;
            if (cds == null) return false;
            int n = cds.Count(kv => kv.Key > MinCooldown);
            double g = ManaPotionGain(cds);
            int pct = CdrPct(cds);
            if (n == 0)
            {
                if (tierScore > 0) tierScore *= 0.2;
                lead = new[] { "no ability with a cooldown on the squad", "no ability with a cooldown" };
            }
            else if (g >= 1.3)
            {
                if (tierScore > 0) tierScore *= 1.25;
                int more = (int)Math.Round((g - 1) * 100, MidpointRounding.AwayFromZero);
                lead = new[] { "resets an ability every 4s - " + more.ToString(IC) + "% more casts here", "resets an ability every 4s" };
            }
            else if (g >= 1.0) { }
            else if (g >= 0.7)
            {
                if (tierScore > 0) tierScore *= (g - 0.7) / 0.3;
                lead = new[] { "would switch off " + pct.ToString(IC) + "% cooldown reduction", "costs your cooldown reduction" };
            }
            else
            {
                tierScore = Math.Min(tierScore, -1.0);
                lead = new[] { "switches off your " + pct.ToString(IC) + "% ability cooldown reduction", "would switch off " + pct.ToString(IC) + "% cooldown reduction", "costs your cooldown reduction" };
            }
            log = "Mana Potion: casts x" + g.ToString("0.00", IC) + " over " + n.ToString(IC) + " abilities with a cooldown";
            return true;
        }

        // ================================================================ the plumbing the other held-item rules land on (C16-01 item 4)
        // Ranker, RerollHint, Plan and ItemRules call these; with none of the items held each returns 'no effect' (the 0.15.0 behaviour).
        // C16-01b / d / e / f (lane CH2) below, C16-01c (Wooden Stick / Empty Chest) after them.

        // ---- C16-01b Reserve Bench (machine code, dis_reservebench.txt): ItemReserveBench.OnApply attaches GrantLevelUp (ExperienceProgress.
        // CollectXPRepeated of the xp to the next level x (Gold Medal ? 1.1 : 1.0) = one level-up) to GameEvents 23 SkipItemChest and 22
        // SkipMilitaryTraining - NOT 21 SkipLevelUp, 40 SkipHashtagEvent, 31 SkipSOSSignal - and CalculateLevelUps to 39 NewPlayerSpawned:
        // (int)((play time - RunStats.lastCharacterSpawnTime) / 120.0) level-ups; the event fires before the spawn time is written
        // (revise\dis_spawnnew.txt), so the count runs from the PREVIOUS join (the run start for the first recruit). The Skip button stays
        // after a reroll (companion.log 10-07 19:35:11 '(the cards were replaced) ... | actions Reroll, Skip'): REROLL stays first (Q35's
        // default, no answer yet), SKIP once no reroll is left.
        /// <summary>Seconds per level-up a recruit brings with Reserve Bench (the game's constant 120.0).</summary>
        public const double JoinSeconds = 120.0;

        /// <summary>C16-01b: level-ups a skip gives on <paramref name="screen"/> (Reserve Bench: 1 on "Chest" and "Military").</summary>
        public static int SkipLevelUps(HeldFacts h, string screen)
        {
            return h != null && h.Has(ReserveBench) && (screen == "Chest" || screen == "Military") ? 1 : 0;
        }

        /// <summary>C16-01b: level-ups a recruit brings (Reserve Bench: one per 2 minutes since the last survivor joined; 0 while that time
        /// is not read).</summary>
        public static int RecruitLevelUps(HeldFacts h)
        {
            if (h == null || !h.Has(ReserveBench) || double.IsNaN(h.SinceJoin) || double.IsInfinity(h.SinceJoin) || h.SinceJoin < 0) return 0;
            return (int)(h.SinceJoin / JoinSeconds);
        }

        /// <summary>C16-01b: a recruit's score with the level-ups it brings (<paramref name="n"/> = RecruitLevelUps), each worth Liberate's
        /// own level-up (RescueLevelUp). Unchanged while n is 0.</summary>
        public static double RecruitHeld(double score, HeldFacts h, out int n)
        {
            n = RecruitLevelUps(h);
            return n > 0 ? score + n * RescueLevelUp : score;
        }

        /// <summary>C16-01b: a recruit's card reason (Ranker.RecruitScore) - 'held: recruiting gives N level-ups (Reserve Bench)'.</summary>
        public static string RecruitWhy(int n) { return Prefix + "recruiting gives " + n.ToString(IC) + (n == 1 ? " level-up" : " level-ups") + " (Reserve Bench)"; }

        /// <summary>C16-01b: Liberate's reason while a recruit brings level-ups of its own - 'held: a recruit brings N level-ups now (Reserve Bench)'.</summary>
        public static string RecruitNowWhy(int n) { return Prefix + "a recruit brings " + n.ToString(IC) + (n == 1 ? " level-up" : " level-ups") + " now (Reserve Bench)"; }

        // ---- C16-01d Hijacked Signal (dis_hijacked.txt): ItemHijackedSignal.OnApply attaches GrantLevelUp (ExperienceProgress.Debug_LevelUp,
        // +1 level) to 45 SOSLiberate and GrantCash to 13 ItemChosen; GetBonusMoney = (statistic 11 + the enemy scaling - 1) x 300 = the
        // skip's GetSkipBonusMoney. Liberate (LootCharacterPowerup.OnApply) raises onSOSLiberate before its own cash and level-up
        // (dis_lootchar.txt): with the item a Liberate is two level-ups and cash, and a chest item pays the skip's cash too.
        /// <summary>C16-01d: level-ups a Liberate gives (Hijacked Signal: 2).</summary>
        public static int LiberateLevelUps(HeldFacts h)
        {
            return h != null && h.Has(HijackedSignal) ? 2 : 1;
        }

        /// <summary>C16-01d / e: what a Liberate gives in words: 'the level-up and cash', 'two level-ups and a heal' ... (<paramref name="one"/>
        /// = how one level-up reads: 'the level-up', 'a level-up', 'level-up').</summary>
        public static string LiberateWhat(int levelUps, bool cashHeals, string one = "the level-up")
        {
            string ups = levelUps <= 1 ? one : levelUps == 2 ? "two level-ups" : levelUps.ToString(IC) + " level-ups";
            return ups + " and " + (cashHeals ? "a heal" : "cash");
        }

        /// <summary>C16-01d: the Liberate card's reason with Hijacked Signal held - 'held: two level-ups and cash (Hijacked Signal)' (cash
        /// reads 'a heal' with Life Savings); null with one level-up.</summary>
        public static string LiberateWhy(int levelUps, bool cashHeals)
        {
            return levelUps > 1 ? Prefix + LiberateWhat(levelUps, cashHeals) + " (Hijacked Signal)" : null;
        }

        /// <summary>C16-01d: the PLAN panel's SOS row names Liberate - late in the run (the 0.15.0 clock rule), or while Liberate gives two
        /// level-ups and outscores every recruit that could still come.</summary>
        public static bool PlanSaysLiberate(bool late, int levelUps, double bestRecruit, double liberate)
        {
            return late || (levelUps > 1 && liberate > bestRecruit);
        }

        /// <summary>C16-01d: Liberate's score - 5 with a full squad (the only card), else Ranker's 0.15.0 formula 1 + 3.2 x (1 - recruit
        /// value) + 0.4 x farming, plus RescueLevelUp for each level-up beyond the first.</summary>
        public static double LiberateScore(double recruitValue, int farming, bool full, int levelUps)
        {
            if (full) return 5;
            return 1.0 + 3.2 * (1 - recruitValue) + 0.4 * farming + (levelUps - 1) * RescueLevelUp;
        }

        // ---- C16-01e Life Savings (verify\dis_trygrantcash.txt): GameplayMaster.TryGrantCash checks ItemLifeSavings.IsActive - when set it
        // raises the money event and calls Health.Refill(amount) on the main player's health; the run's cash and GamePermanentData are added
        // only in the other branch. Every granted cash heals: the skip's (ApplySkipBonus -> GetSkipBonusMoney -> TryGrantCash), Liberate's,
        // Black Box's and Hijacked Signal's; the money event still fires (cash-triggered items still trigger).
        /// <summary>C16-01e: granted cash heals instead (Life Savings).</summary>
        public static bool CashHeals(HeldFacts h)
        {
            return h != null && h.Has(LifeSavings);
        }

        /// <summary>C16-01e: an item's cash rule with Life Savings held - its card words and its reason ('held: its cash heals you instead
        /// (Life Savings)').</summary>
        public static readonly string[] CashHealSay = { "its cash heals you instead (Life Savings)", "cash heals you (Life Savings)", "cash heals you" };
        public const string CashHealWhy = Prefix + "its cash heals you instead (Life Savings)";

        /// <summary>C16-01d: a chest item pays the same cash as the skip (Hijacked Signal on "Chest").</summary>
        public static bool CashAlsoOnPick(HeldFacts h, string screen)
        {
            return h != null && h.Has(HijackedSignal) && screen == "Chest";
        }

        // ---- C16-01f the skip extras. A Cookie (Item_ACookie, an ItemEventActivated on 21 SkipLevelUp, 22 SkipMilitaryTraining, 23
        // SkipItemChest, 31 SkipSOSSignal - not 40 SkipHashtagEvent): 'every use of Skip additionally restores at least +500 Health Points'
        // (500 is the game's floor; how it picks more is not read). Skip Rope (ItemSkipRope.OnApply, dis_skiprope.txt): AddPoint1 on 21
        // SkipLevelUp, AddPoint5 on 31 / 22 / 23 / 40; REQ_MILITARY_TRAINING = 5 points per Military Training Point; a Military Training
        // needs 4 points, 3 with Gold Medal (CollectibleLevelUp.get_RequiredPerLevelUp, dis_reqmt.txt). 'Every 2 points: +1% XP'.
        public const int CookieHeal = 500, RopeBig = 5, RopeSmall = 1, RopePerTraining = 5;
        /// <summary>A Military Training's worth on the cards' scale: the median #1 card of 98 logged training offers (levelup_value_out.txt).</summary>
        public const double MilitaryWorth = 3.40;
        /// <summary>A Skip Rope point's XP on the cards' scale per unit of economy: half of a 1% XP (0.16 x economy; a Common Operation Planning's
        /// +10% XP is 1.6 x economy, C16-01c).</summary>
        public const double RopeXpPoint = 0.08;

        /// <summary>C16-01f: health a skip restores beyond its own heal (A Cookie: 500 on LevelUp, Military, Chest - and the rescue screen;
        /// never a Research Pod).</summary>
        public static int SkipExtraHeal(HeldFacts h, string screen)
        {
            return h != null && h.Has(ACookie) && (screen == "LevelUp" || screen == "Military" || screen == "Chest" || screen == "SOS") ? CookieHeal : 0;
        }

        /// <summary>C16-01f: the points a skip on <paramref name="screen"/> pays with Skip Rope held (1 on a level-up, 5 on a training, a chest, a
        /// Research Pod and the rescue screen; 0 without the item).</summary>
        public static int SkipRopePoints(HeldFacts h, string screen)
        {
            if (h == null || !h.Has(SkipRope)) return 0;
            switch (screen ?? "")
            {
                case "LevelUp": return RopeSmall;
                case "Military": case "Chest": case "Hashtag": case "SOS": return RopeBig;
                default: return 0;
            }
        }

        /// <summary>Military Training Points a Military Training needs (4; 3 with Gold Medal).</summary>
        public static int TrainingReq(HeldFacts h) { return h != null && h.Has(GoldMedal) ? 3 : 4; }

        /// <summary>C16-01f: what Skip Rope's points of a skip are worth on the cards' scale: p x 0.08 x economy (the XP) + (p / 5) x
        /// (3.40 / the points a training needs), to two decimals; 0 without the item.</summary>
        public static double SkipRopeWorth(HeldFacts h, string screen, double economy)
        {
            int p = SkipRopePoints(h, screen);
            if (p <= 0) return 0;
            double e = double.IsNaN(economy) || double.IsInfinity(economy) ? 1 : Math.Max(0, economy);
            return Math.Round(p * RopeXpPoint * e + (p / (double)RopePerTraining) * (MilitaryWorth / TrainingReq(h)), 2);
        }

        /// <summary>C16-01f: the skip extras held that pay on <paramref name="screen"/>, joined ' and ' (A Cookie, Skip Rope); null: none.
        /// Never Reserve Bench (its level-up has words of its own, ScreenCallIn.SkipLevelUpBy).</summary>
        public static string SkipBy(HeldFacts h, string screen)
        {
            var by = new List<string>();
            if (SkipExtraHeal(h, screen) > 0) by.Add(ACookie.Name);
            if (SkipRopePoints(h, screen) > 0) by.Add(SkipRope.Name);
            return by.Count == 0 ? null : string.Join(" and ", by);
        }

        /// <summary>C16-01f: the log's words for Skip Rope's points of a skip - '+5 points (a quarter of a Military Training)'.</summary>
        public static string RopeNote(int points, int req)
        {
            if (points <= 0) return null;
            int per = RopePerTraining * Math.Max(1, req);       // points per Military Training: 20, 15 with Gold Medal
            string share = points * 4 == per ? "a quarter" : points * 3 == per ? "a third" : points * 20 == per ? "a twentieth" : points * 15 == per ? "a fifteenth" : points.ToString(IC) + "/" + per.ToString(IC);
            return "+" + points.ToString(IC) + (points == 1 ? " point" : " points") + " (" + share + " of a Military Training)";
        }

        // ---- C16-01c Wooden Stick / Empty Chest (machine code of 1.0.2, scratch c16_held_items item_raw.txt, revise\item_raw_emptychest.txt):
        // both are ItemStatRelationship items. Wooden Stick: InternalNumFreeItemSlots x 0.1 -> TeamXPMultiplier (maxBonus 0.8) plus a flat
        // +0.1; Empty Chest: the same 0.1 / 0.8 onto weapon damage and onto ability damage plus a flat +0.1 on both. Recalculated on 13
        // ItemChosen from the TEAM statistic 50 (revise\dis_isr_recalc.txt), which AddItem / RemoveItem keep at (int)(max - equipped); an item
        // already held as a stack takes no slot (revise\dis_gp_additem.txt); Well Prepped takes a slot and adds one (the empty count stays).
        // Q36 (no answer yet) -> its default: the full Wooden Stick cost, Empty Chest 2.0.
        /// <summary>What filling an empty slot costs with Wooden Stick held, per unit of economy: a Common Operation Planning (+10% XP) above a
        /// stat card's base (1 x 0.8 militaryStats XPModifierMod x 2).</summary>
        public const double WoodenStickCost = 1.6;
        /// <summary>What filling an empty slot costs with Empty Chest held: one Common +10% damage training at an even lean (weight 1.0 x 2;
        /// 4.0 would price both damage trainings - Q36's other option).</summary>
        public const double EmptyChestCost = 2.0;
        /// <summary>The relationship's cap: 0.1 per empty slot up to 0.8 - with more than 8 empty, filling one keeps the bonus at the cap.</summary>
        public const int SlotCap = 8;
        public static readonly HeldItem WellPrepped = new HeldItem("WellPrepped", "Well Prepped", "Item_WellPrepped");
        /// <summary>The card's words, longest first (43 / 27 / 12; 46 / 30 / 16; both 44 / 27 / 19; Well Prepped 47 / 22).</summary>
        public static readonly string[] WoodenStickSay = { "fills an empty slot: -10% XP (Wooden Stick)", "costs 10% XP (Wooden Stick)", "costs 10% XP" };
        public static readonly string[] EmptyChestSay = { "fills an empty slot: -10% damage (Empty Chest)", "costs 10% damage (Empty Chest)", "costs 10% damage" };
        public static readonly string[] BothSlotSay = { "fills an empty slot: -10% XP and -10% damage", "costs 10% XP and 10% damage", "costs XP and damage" };
        public static readonly string[] WellPreppedSay = { "adds the slot it takes - keeps your empty slots", "adds the slot it takes" };

        /// <summary>The run's economy factor (RunContext.Economy); 1 without a clock (the bench).</summary>
        static double Economy(RunContext ctx)
        {
            if (ctx == null) return 1;
            double e = ctx.Economy;
            return double.IsNaN(e) || double.IsInfinity(e) ? 1 : Math.Max(0, e);
        }

        /// <summary>C16-01c: what filling an empty item slot costs this card - (Wooden Stick held: 1.6 x economy) + (Empty Chest held: 2.0), to two
        /// decimals, while 1 to 8 slots are empty; 0 when neither is held, no slot is empty or the slots were not read, the card is Well Prepped
        /// (<paramref name="say"/> = its words then: it adds the slot it takes) or an item already held (a stack takes no slot). <paramref name="say"/> =
        /// the words, <paramref name="by"/> = the items that price the slot.</summary>
        public static double SlotCost(ItemContext c, string itemName, out string[] say, out string by)
        {
            say = null; by = null;
            var h = c == null ? null : c.HeldFacts;
            if (h == null) return 0;
            bool ws = h.Has(WoodenStick), ec = h.Has(EmptyChest);
            if ((!ws && !ec) || h.FreeSlots <= 0) return 0;         // nothing prices a slot; no empty slot, or the slots not read (-1)
            if (IsItem(WellPrepped, itemName, c.Asset)) { say = (string[])WellPreppedSay.Clone(); by = SlotBy(h); return 0; }
            if (Stacks(c, itemName)) return 0;
            if (h.FreeSlots > SlotCap) return 0;
            by = SlotBy(h);
            say = (string[])(ws && ec ? BothSlotSay : ws ? WoodenStickSay : EmptyChestSay).Clone();
            return Math.Round((ws ? WoodenStickCost * Economy(c.Ctx) : 0) + (ec ? EmptyChestCost : 0), 2);
        }

        /// <summary>The card's item is held already (by name, pretend or asset): a second one stacks and takes no slot.</summary>
        static bool Stacks(ItemContext c, string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return false;
            if (c.Held != null && c.Held.Contains(itemName)) return true;
            var h = c.HeldFacts;
            return h != null && (h.HasName(itemName) || (!string.IsNullOrEmpty(c.Asset) && h.Assets.Contains(CutClone(c.Asset))));
        }

        /// <summary>C16-01c: the held items that price a slot ('Wooden Stick', 'Empty Chest' or 'Wooden Stick and Empty Chest'); null: none.</summary>
        public static string SlotBy(HeldFacts h)
        {
            if (h == null) return null;
            bool ws = h.Has(WoodenStick), ec = h.Has(EmptyChest);
            return ws && ec ? WoodenStick.Name + " and " + EmptyChest.Name : ws ? WoodenStick.Name : ec ? EmptyChest.Name : null;
        }

        /// <summary>C16-01c: the [held] part - 'Wooden Stick: 5 empty slots (+50% XP), filling one costs 2.18 now'.</summary>
        static string SlotPart(HeldItem i, HeldFacts h, string what, double cost, string now)
        {
            int f = h.FreeSlots;
            if (f < 0) return i.Name + ": the empty slots not read - no slot cost";
            if (f == 0) return i.Name + ": no empty slot left";
            string slots = f.ToString(IC) + (f == 1 ? " empty slot" : " empty slots");
            if (f > SlotCap) return i.Name + ": " + slots + " (+" + (SlotCap * 10).ToString(IC) + "% " + what + ", the cap), filling one costs nothing";
            return i.Name + ": " + slots + " (+" + (f * 10).ToString(IC) + "% " + what + "), filling one costs " + cost.ToString("0.00", IC) + now;
        }

        // ---- C16-01i Last Unicorn (machine code, revise\dis_lastunicorn.txt): ItemLastUnicorn.Recalculate walks the main player's
        // GetActiveItemsList and counts every item whose isAnimalItem (+0x2C) is set; Last Unicorn is flagged itself, so taking it gives (the
        // animal items held + 1) x (+10 Luck, +5% Max HP, +5% damage). A live flag (HeldFacts.AnimalHeld, ItemContext.Animal), not a name list:
        // a later animal item joins by itself. The named pairs of the item texts (The Word + Pocket Watch, Omnigeode + Ruby / Sapphire Gem) are
        // ItemBook.Pairs (C16-02); this is the count only.
        /// <summary>One animal item's worth on the Last Unicorn card, and an animal item's with Last Unicorn held (the pairs' +0.9; cap 1.8).</summary>
        public const double AnimalPair = 0.9;

        /// <summary>C16-01i: the Last Unicorn card's term - +0.9 per animal item held, two at most, with its words ('counts 3 animal items: +30
        /// Luck, +15% damage' / '3 animal items: +15% damage'); 0 when no animal item is held, nothing was read, or Last Unicorn is held already (a
        /// second copy: how it stacks is not read).</summary>
        public static double UnicornCount(HeldFacts h, ICollection<string> held, out string[] lead)
        {
            lead = null;
            if (h == null || h.AnimalHeld <= 0 || UnicornHeld(h, held)) return 0;
            int n = h.AnimalHeld + 1;       // itself included
            lead = new[]
            {
                "counts " + n.ToString(IC) + " animal items: +" + (10 * n).ToString(IC) + " Luck, +" + (5 * n).ToString(IC) + "% damage",
                n.ToString(IC) + " animal items: +" + (5 * n).ToString(IC) + "% damage",
            };
            return AnimalPair * Math.Min(2, h.AnimalHeld);
        }

        /// <summary>C16-01i: Last Unicorn is held (by name, asset or pretend; <paramref name="held"/> = ItemContext.Held).</summary>
        public static bool UnicornHeld(HeldFacts h, ICollection<string> held)
        {
            return (h != null && h.Has(LastUnicorn)) || (held != null && held.Contains(LastUnicorn.Name));
        }

        // ---- C16-01j the status conversions (machine code read sites, rva_names.ps1): Fire Extinguisher in EnemyStatusApplication.ApplyBurn,
        // Warm Ice Cream in ApplyFrozen, Battery Leakage in ApplyElectrified (every application of the status); Biofuel Energy, Nail Bat and
        // Expired Sushi in OnHitTryApplyStatusToxified / Injured / Bleed.ApplyHitEffect (the on-hit applications - a status an ability applies
        // by its own effect may keep its old type for these three, unverified; the rule follows the 1.0.2 texts). The conversions are
        // ItemBook.Converts (the one table, C16-02 section 8), the default causes ItemBook.StatusOf; StatusItems are the items whose worth IS a
        // status (exact 1.0.2 names and assets; the game spells Item_IconOfStilness so).
        public static readonly StatusItem[] StatusItems =
        {
            new StatusItem("SpoilCanister", "Spoil Canister", "Item_SpoilCanister", "Burn"),
            new StatusItem("IconOfCinder", "Icon of Cinder", "Item_IconOfCinder", "Burn"),
            new StatusItem("SpecialSnowflake", "Special Snowflake", "Item_SpecialSnowflake", "Freeze"),
            new StatusItem("IconOfStillness", "Icon of Stillness", "Item_IconOfStilness", "Freeze"),
            new StatusItem("JacobsLadder", "Jacob's Ladder", "Item_JacobsLadder", "Electrify"),
            new StatusItem("IconOfTempest", "Icon of Tempest", "Item_IconOfTempest", "Electrify"),
            new StatusItem("PlaguesVisage", "Plague's Visage", "Item_PlaguesVisage", "Toxify"),
            new StatusItem("IconOfPestilence", "Icon of Pestilence", "Item_IconOfPestilence", "Toxify"),
            new StatusItem("NineInchNails", "Nine Inch Nails", "Item_NineInchNails", "Injured"),
            new StatusItem("Slingshot", "Slingshot", "Item_Slingshot", "Injured"),
            new StatusItem("BloodyAxe", "Bloody Axe", "Item_BloodyAxe", "Bleed"),
            new StatusItem("BleedingEdge", "Bleeding Edge", "Item_BleedingEdge", "Bleed"),
        };
        /// <summary>A stop of a held status item's only cause, a new cause fed to one (rule B), and the clamp of their sum.</summary>
        public const double StatusStop = 0.9, StatusFeed = 0.6, StatusCardMin = -1.8, StatusCardMax = 1.2;

        /// <summary>The held-item table's entry of a conversion item (ItemBook.Converts' first column), for its asset.</summary>
        static HeldItem ConvItem(string name) { foreach (var i in All) if (Is(i.Name, name)) return i; return null; }

        /// <summary>C16-01j: the conversion items held (by name, asset or pretend; <paramref name="held"/> = ItemContext.Held), as rows of
        /// ItemBook.Converts (item, damage type, the status it causes instead), in that table's order.</summary>
        public static List<string[]> ConversionsHeld(HeldFacts h, ICollection<string> held)
        {
            var list = new List<string[]>();
            foreach (var cv in ItemBook.Converts)
            {
                var item = ConvItem(cv[0]);
                if ((h != null && ((item != null && h.Has(item)) || h.HasName(cv[0]))) || (held != null && held.Contains(cv[0]))) list.Add(cv);
            }
            return list;
        }

        /// <summary>C16-01j: a conversion item is held (or pretended).</summary>
        public static bool AnyConversion(HeldFacts h) { return ConversionsHeld(h, null).Count > 0; }

        /// <summary>The status a damage type causes with <paramref name="convs"/> held (the default without them); null for a type no row names.</summary>
        public static string StatusOfType(string type, IList<string[]> convs)
        {
            string s = null;
            foreach (var st in ItemBook.StatusOf) if (Is(st[0], type)) s = st[1];
            if (s == null) return null;
            if (convs != null) foreach (var cv in convs) if (Is(cv[1], type)) return cv[2];
            return s;
        }

        /// <summary>The damage type that causes <paramref name="status"/> without a conversion (ItemBook.StatusOf).</summary>
        public static string DefaultType(string status) { foreach (var st in ItemBook.StatusOf) if (Is(st[1], status)) return st[0]; return null; }

        /// <summary>C16-01j: the damage types whose (converted) status is <paramref name="status"/> with <paramref name="convs"/> held.</summary>
        public static List<string> StatusSources(string status, IList<string[]> convs)
        {
            var list = new List<string>();
            foreach (var st in ItemBook.StatusOf) if (Is(StatusOfType(st[0], convs), status)) list.Add(st[0]);
            return list;
        }

        /// <summary>C16-01j: how well <paramref name="status"/> fits the squad with <paramref name="convs"/> held: min(1, 1.6 x the combined share
        /// of its sources) - TagProfile.Fit's own lift over the sum. 0 when the profile is unknown.</summary>
        public static double StatusFit(string status, IList<string[]> convs, TagProfile tags)
        {
            if (tags == null || !tags.Known) return 0;
            double share = 0;
            foreach (var t in StatusSources(status, convs)) share += tags.Share(t);
            return Math.Min(1.0, 1.6 * share);
        }

        /// <summary>The status item a card is (by name, or by its asset for a renamed one); null: not a status item.</summary>
        public static StatusItem StatusItemOf(string name, string asset)
        {
            foreach (var s in StatusItems) if (IsItem(s.Item, name, asset)) return s;
            return null;
        }

        /// <summary>The status items held (by name, asset or pretend), in the table's order.</summary>
        public static List<StatusItem> StatusHeld(HeldFacts h, ICollection<string> held)
        {
            var list = new List<StatusItem>();
            foreach (var s in StatusItems) if ((h != null && h.Has(s.Item)) || (held != null && held.Contains(s.Item.Name))) list.Add(s);
            return list;
        }

        /// <summary>"Jacob's Ladder's", "Nine Inch Nails'".</summary>
        public static string Poss(string name) { return name + (name.EndsWith("s", StringComparison.Ordinal) ? "'" : "'s"); }

        /// <summary>C16-01j rule A: a status item while a conversion item is held (and the squad's damage types are known) - its default type's fit
        /// (<paramref name="oldFit"/>) against the fit of what causes its status now (<paramref name="newFit"/>). False when nothing changes (not
        /// a status item, no conversion held, the profile unknown, its status still caused by its own type alone). <paramref name="lead"/> =
        /// the card's words when the status lost every cause ('no Electrify while you hold Battery Leakage') or gained one the squad deals
        /// ('your Electric now causes Toxify (Battery Leakage)'), null otherwise; <paramref name="note"/> = a log reason when the cause changed
        /// otherwise (null: none).</summary>
        public static bool StatusRule(ItemContext c, string name, out string type, out double oldFit, out double newFit, out string[] lead, out string note)
        {
            type = null; oldFit = 0; newFit = 0; lead = null; note = null;
            if (c == null || c.Tags == null || !c.Tags.Known) return false;
            var si = StatusItemOf(name, c.Asset);
            if (si == null) return false;
            var convs = ConversionsHeld(c.HeldFacts, c.Held);
            if (convs.Count == 0) return false;
            type = DefaultType(si.Status);
            var src = StatusSources(si.Status, convs);
            if (src.Count == 1 && Is(src[0], type)) return false;          // no held conversion touches this status
            oldFit = c.Tags.Fit(type);
            newFit = StatusFit(si.Status, convs, c.Tags);
            string lost = null; foreach (var cv in convs) if (Is(cv[1], type)) lost = cv[0];        // the item that took the default type away
            string gained = null, by = null; double best = -1;                                       // the new cause the squad deals most, its item
            foreach (var t in src)
            {
                if (Is(t, type)) continue;
                double sh = c.Tags.Share(t);
                if (sh > best) { best = sh; gained = t; foreach (var cv in convs) if (Is(cv[1], t)) by = cv[0]; }
            }
            if (newFit <= 0 && oldFit > 0 && lost != null)
                lead = new[] { "no " + si.Status + " while you hold " + lost, "no " + si.Status + " with " + lost, lost + " stops it" };
            else if (newFit > oldFit + 0.1 && gained != null && best > 0 && by != null)
                lead = new[] { "your " + gained + " now causes " + si.Status + " (" + by + ")", "your " + gained + " causes " + si.Status + " now" };
            else if (Math.Abs(newFit - oldFit) > 1e-9)
                note = src.Count == 0 ? "nothing causes " + si.Status + " now" : si.Status + " comes from your " + string.Join(" and ", src) + " now";
            return true;
        }

        /// <summary>C16-01j rule B: a conversion item card (not held) while status items are held (the squad's damage types known): -0.9 for each
        /// held status item whose status would have no cause on this squad after taking it, +0.6 for each whose status it would feed from a type
        /// the squad deals (StatusFit 0.2 or more and above today's), the sum clamped to -1.8 .. +1.2. <paramref name="whys"/> = one reason per
        /// item (without the prefix: 'would stop your Jacob's Ladder's Electrify'), <paramref name="lead"/> = the card's words (a stop before a
        /// feed); 0 and nulls when nothing changes.</summary>
        public static double ConvertCard(ItemContext c, string name, out List<string> whys, out string[] lead)
        {
            whys = null; lead = null;
            if (c == null || c.Tags == null || !c.Tags.Known || string.IsNullOrEmpty(name)) return 0;
            string[] row = null;
            foreach (var cv in ItemBook.Converts) { var item = ConvItem(cv[0]); if (item != null ? IsItem(item, name, c.Asset) : Is(cv[0], name)) row = cv; }
            if (row == null) return 0;
            var now = ConversionsHeld(c.HeldFacts, c.Held);
            if (now.Any(cv => Is(cv[0], row[0]))) return 0;                 // held already: nothing changes by taking another
            var held = StatusHeld(c.HeldFacts, c.Held);
            if (held.Count == 0) return 0;
            var after = new List<string[]>(now) { row };
            double v = 0; string[] stop = null, feed = null; var list = new List<string>();
            foreach (var si in held)
            {
                double fNow = StatusFit(si.Status, now, c.Tags), fAfter = StatusFit(si.Status, after, c.Tags);
                // the item named ('would stop your Jacob's Ladder's Electrify'), or the status alone where the name breaks the card's writing
                // rules (Special Snowflake: 'special' is the 10-tag effect's word on a card)
                bool named = Wording.Rails(Wording.Cap("would stop your " + Poss(si.Item.Name) + " " + si.Status)).Count == 0;
                if (fNow > 0 && fAfter <= 0)
                {
                    string w = named ? "would stop your " + Poss(si.Item.Name) + " " + si.Status : "would stop the " + si.Status + " your item needs";
                    v -= StatusStop; list.Add(w);
                    if (stop == null) stop = new[] { w, "stops a status your items need" };
                }
                else if (fAfter >= 0.2 && fAfter > fNow + 1e-9)
                {
                    string w = named ? "would feed your " + Poss(si.Item.Name) + " " + si.Status : "would feed the " + si.Status + " your item uses";
                    v += StatusFeed; list.Add(w);
                    if (feed == null) feed = new[] { w, "feeds a status your items use" };
                }
            }
            if (list.Count == 0) return 0;
            whys = list; lead = stop ?? feed;
            return Math.Max(StatusCardMin, Math.Min(StatusCardMax, v));
        }

        /// <summary>C16-01j: the [held] part of a conversion item - 'Battery Leakage: Electric causes Toxify, not Electrify'.</summary>
        static string ConvertPart(HeldItem i)
        {
            foreach (var cv in ItemBook.Converts)
                if (Is(cv[0], i.Name)) return cv[0] + ": " + cv[1] + " causes " + cv[2] + ", not " + (StatusOfType(cv[1], null) ?? "?");
            return null;
        }
    }

    /// <summary>C16-01j: an item whose worth is a status (HeldRules.StatusItems) - the item and the status it works on.</summary>
    internal sealed class StatusItem
    {
        public readonly HeldItem Item; public readonly string Status;
        public StatusItem(string id, string name, string asset, string status) { Item = new HeldItem(id, name, asset); Status = status; }
        public override string ToString() { return Item.Name + " (" + Status + ")"; }
    }
}
