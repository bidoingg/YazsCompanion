// 0.14.0 (A1 stage 2): the plain words under the cards (Wording.cs), section 11 of the bench:
//   W1  the rails: every builder swept over the game's names (probe.json) - abilities x levels x evolution x reach x the build's
//       order and the other headlines, weapon levels x styles x the lift x ranks, the next tier and the other branches, evolutions,
//       every item x a few squads (the item rules' own reasoning), Research Pod cards, rescues and Liberate, quest lines, stat
//       cards, the reroll hint - each line at most 50 visible characters, only the card font's glyphs, none of the ranking's own
//       words (focus, style:, (Auto), #2, special, stack, tree boost, pts, weighs, x1.31), no head that repeats the card's rarity.
//       The rails themselves must catch the old headlines: "focus 2>3 of 4 toward its evolution" fails them.
//       Then the same sweep again as the card draws it (the integration review, PF3 / PF4): every name 19 characters longer - the
//       most another mod's lent names run over the game's - measured after the swap, within the 48 characters a card that shows
//       "AVOID   " leaves of the Deck's 56.
//   W2  the glyphs: Wording.Safe maps what the card font lacks (› → » ; · • — – → - ; ≥ → >=) and drops '<'.
//   W3  the user's logged offers of 2026-10-05 (the 18:02 and the 19:01 sessions) through the builders: the logged headline, what
//       0.13.0 drew of it (the '>' dropped), and the line now. The chests run through the real item rules with the squad the log
//       shows (its damage types and tag points; the weights are estimates).
//   W4  the game's stat labels (Knowledge.StatLabel) for every military card, and the sources: the badge draws the plain words, the
//       ranking fills them after the ranks, the [shown] line, the ribbon's fade, the card's scale chain, the readout's vocabulary.
//   W5  (0.15.0, C15-03) the 10-06 live wording fixes: a #1 weapon's WHY under "abilities first" (and the mirror), evolution names whole
//       unless their base is named, the build's main / core ability evolving as the head, TAGS "effect on" - and the rails over each.
// Generic names only (the repository is public): the run's lent builds are "Bench Anchor" (the leader's), "Bench Rocket" and
// "Bench Summons" here; every card name is the game's own.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class Wordings
    {
        static int _bad, _swept;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        static readonly string[] Builds = { null, "Rifleman", "Shield Anchor", "Bombardier", "A Build Named Twenty1" };     // the last: 21 characters
        static readonly string[] Clocks = { "2:10 left", "12:05 left", "wave 3/10", "open-ended" };
        static readonly string[] Boosts = { "Turret Expertise", "Trap Expertise", "Cold Chain", "Grenade Expertise" };
        static string[] Classes { get { return YazsCompanion.Builds.Survivors; } }

        // ---- the sweep's bookkeeping: offenders, and a spread of what each builder said (read by hand: the bench prints them)
        static readonly List<string> Offenders = new List<string>();
        static readonly Dictionary<string, List<string>> Said = new Dictionary<string, List<string>>();
        static readonly Dictionary<string, HashSet<string>> Seen = new Dictionary<string, HashSet<string>>();
        static readonly Dictionary<string, int> Count = new Dictionary<string, int>();
        static string _longest = "";

        // the sweep as drawn: a lender that makes every name 19 characters longer, and the room of an AVOID card
        static Func<string, string> _lent; static int _lentRoom, _lentSwept;
        static readonly List<string> LentOffenders = new List<string>(); static int _lentBad;
        static string _lentLongest = "";
        const string Pad = " Nnnnnnnnnnnnnnnnnn";        // 19 characters: the longest a lent name runs over the game's (the character packs)
        static Func<string, string> Lender(IEnumerable<string> names)
        {
            var pickups = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Health", "Cash", "Luck", "XP", "Magnet" };     // the game's pickups: no mod lends them a name
            var keys = names.Where(n => !string.IsNullOrEmpty(n) && n.Length > 2 && !pickups.Contains(n)).Distinct().OrderByDescending(n => n.Length).Select(Regex.Escape).ToList();
            var rx = new Regex("(?<![\\p{L}\\p{N}_])(?:" + string.Join("|", keys) + ")(?![\\p{L}\\p{N}_])", RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var cache = new Dictionary<string, string>(StringComparer.Ordinal);
            return s =>
            {
                if (string.IsNullOrEmpty(s)) return s;
                string r; if (cache.TryGetValue(s, out r)) return r;
                r = rx.Replace(s, m => m.Value + Pad);
                if (cache.Count > 200000) cache.Clear();
                cache[s] = r; return r;
            };
        }

        static void Rail(string kind, string line, string from = null, int budget = Wording.Budget)
        {
            if (_lent != null)
            {
                _lentSwept++;
                string drawn = Wording.Safe(_lent(line ?? ""));
                var off = Wording.Rails(drawn, _lentRoom);
                if (off.Count > 0) { _lentBad++; string o = kind + ": '" + drawn + "' (" + string.Join(", ", off) + ")"; if (LentOffenders.Count < 400 && !LentOffenders.Contains(o)) LentOffenders.Add(o); }
                else if (drawn.Length > _lentLongest.Length) _lentLongest = drawn;
                return;
            }
            _swept++;
            int n; Count.TryGetValue(kind, out n); Count[kind] = n + 1;
            var bad = Wording.Rails(line);
            if (budget > Wording.Budget && line != null && line.Length <= budget) bad.RemoveAll(b => b.StartsWith("length", StringComparison.Ordinal));
            if (bad.Count > 0) { if (Offenders.Count < 400) Offenders.Add(kind + ": '" + line + "' (" + string.Join(", ", bad) + ")" + (from != null ? " <- " + from : "")); return; }
            HashSet<string> seen; if (!Seen.TryGetValue(kind, out seen)) { Seen[kind] = seen = new HashSet<string>(); Said[kind] = new List<string>(); }
            if (seen.Add(line)) Said[kind].Add(line);
            if (budget == Wording.Budget && line.Length > _longest.Length) _longest = line;
        }

        public static int Run(List<PowerFacts> powers, List<ProbeItem> items, List<ProbeWeapon> weapons)
        {
            _bad = 0; _swept = 0; Offenders.Clear(); Said.Clear(); Seen.Clear(); Count.Clear(); _longest = "";
            Console.WriteLine("\n=== 0.14.0: the plain words under the cards - W1 the rails over every builder, W2 the glyphs, W3 the 10-05 offers replayed, W4 labels and sources");
            RailsCatch();
            var names = powers.Select(p => p.Name).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            var weaponNames = weapons.Select(w => w.Name).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            Abilities();
            Weapons(names, weaponNames, weapons);
            Evolutions(names);
            Items(items);
            ResearchPods();
            Rescues(names);
            Quests();
            Stats(powers);
            Rerolls();
            Check("W1", "every builder within the rails: <= 50 visible characters, the card font's glyphs, none of the ranking's words (" + _swept + " lines, " + Seen.Sum(kv => kv.Value.Count) + " different)",
                Offenders.Count == 0, Offenders.Count == 0 ? "the longest: '" + _longest + "' (" + _longest.Length + ")" : Offenders.Count + " off: " + string.Join(" || ", Offenders.Take(12)));
            // the same sweep as the card draws it: the names 19 characters longer, the room an AVOID card leaves (48)
            LentOffenders.Clear(); _lentSwept = 0; _lentBad = 0; _lentLongest = "";
            _lent = Lender(names.Concat(weaponNames).Concat(items.Select(i => i.Name)).Concat(Classes).Concat(new[] { "Medical Drone", "Handgun", "Experiment 21", "Pump-Action Shotgun" }));
            _lentRoom = Wording.RoomBeside(8);
            Wording.Ambient(_lentRoom, _lent);
            try { Abilities(); Weapons(names, weaponNames, weapons); Evolutions(names); Items(items); ResearchPods(); Rescues(names); Quests(); Stats(powers); }
            finally { Wording.Ambient(0, null); _lent = null; }
            Check("W1", "the same sweep as drawn - every name 19 characters longer (another mod's lent names), within the " + _lentRoom + " characters an AVOID card leaves (" + _lentSwept + " lines)",
                _lentBad == 0, _lentBad == 0 ? "the longest: '" + _lentLongest + "' (" + _lentLongest.Length + ")" : _lentBad + " off (" + LentOffenders.Count + " different): " + string.Join(" || ", LentOffenders.Take(60)));
            Console.WriteLine("    lines per builder: " + string.Join(", ", Count.OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value + " (" + (Seen.ContainsKey(kv.Key) ? Seen[kv.Key].Count : 0) + " different)")));
            Samples();
            Glyphs();
            Replay(items);
            Labels(powers);
            Sources();
            Live1006(names, weaponNames, items);                // 0.15.0 (C15-03): the 10-06 live wording fixes (W5)
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ---------------------------------------------------------------- W1: the rails catch the old headlines
        static void RailsCatch()
        {
            var old = new[]
            {
                "focus 2>3 of 4 toward its evolution - #2 in Rifleman", "focus 23 of 4 toward its evolution - #2 in Rifleman",
                "Endless: Ability Size - Rifleman (Auto) wants it", "Common, team-wide: Movement Speed", "new ability - #1 in Rifleman",
                "weapon level: 1 to 2 of 4; style: abilities first", "reaches the Chemical special effect (10)", "Hardcore: survival weighs x1.31",
                "REROLL - Tank would rate higher (6.2 vs 4.1)", "tree boost 3/5", "Kinetic 16, stack Explosive",
            };
            var missed = old.Where(o => Wording.Rails(o).Count == 0).ToList();
            Check("W1", "the rails fail every old headline (a reintroduced 'focus 2>3 of 4' fails them)", missed.Count == 0,
                "focus 2>3 -> " + string.Join(", ", Wording.Rails(old[0])) + (missed.Count > 0 ? " | missed: " + string.Join(" / ", missed) : ""));
        }

        // ---------------------------------------------------------------- W1: abilities
        sealed class Head { public int Rank; public string[] Forms; }
        static List<Head> Heads()
        {
            var heads = new List<Head> { null };
            foreach (var b in Builds.Where(x => x != null))
            {
                for (int p = 0; p <= 4; p++) heads.Add(new Head { Rank = p <= 1 ? 4 : 1, Forms = Wording.Role(b, p) });
                heads.Add(new Head { Rank = 6, Forms = Wording.Skips(b) });
            }
            foreach (var t in new[] { "S", "A", "B", "C" }) heads.Add(new Head { Rank = t == "C" ? 1 : 2, Forms = Wording.Tier(t) });
            foreach (var c in Classes) heads.Add(new Head { Rank = 3, Forms = Wording.Synergy(c) });
            foreach (var bo in Boosts) foreach (var c in new[] { "Huntress", "Mechanic", "Engineer" }) heads.Add(new Head { Rank = 3, Forms = Wording.Boosted(bo, c) });
            foreach (var t in TagProfile.Names) { heads.Add(new Head { Rank = 5, Forms = Wording.Special(t) }); foreach (int pct in new[] { 40, 79, 100 }) heads.Add(new Head { Rank = 1, Forms = Wording.Share(t, pct) }); }
            return heads;
        }

        static void Abilities()
        {
            var heads = Heads();
            foreach (int max in new[] { 4, 5 })
                for (int level = 0; level < max; level++)
                    foreach (var evo in new[] { 0, 1, 2 })              // none, locked, bought
                        foreach (double reach in new[] { 1.0, 0.55, 0.3 })
                            foreach (var h in heads)
                                foreach (var clock in Clocks)
                                    foreach (bool focus in new[] { false, true })
                                        foreach (int owned in new[] { 1, 4 })
                                        {
                                            var w = new CardWords { Kind = SayKind.Ability, Level = level, Max = max, EvoExists = evo > 0, EvoOwned = evo == 2, Reach = reach, Clock = clock, Focus = focus, Owned = owned,
                                                HeadRank = h == null ? 0 : h.Rank, Head = h == null ? null : h.Forms };
                                            Rail(level == 0 ? "new ability" : "ability level", Wording.Card(w, 2, "Medical Drone", "Handgun"));
                                        }
        }

        // ---------------------------------------------------------------- W1: weapons
        static void Weapons(List<string> names, List<string> weaponNames, List<ProbeWeapon> weapons)
        {
            string longest = names.OrderByDescending(x => x.Length).First();
            var pairs = new[] { Tuple.Create("Experiment 21", "Medical Drone"), Tuple.Create(longest, longest), Tuple.Create("Medical Drone: Offense", "Helicopter Strike: Chemtrails") };
            var styles = new[] { BuildStyle.Weapon, BuildStyle.Balanced, BuildStyle.Ability };
            foreach (var style in styles)
                foreach (var build in Builds)
                    foreach (int lift in new[] { 0, 1, 2 })            // no lift, the abilities done, one still short
                        for (int rank = 1; rank <= 4; rank++)
                            foreach (var special in new string[] { null }.Concat(TagProfile.Names))
                                foreach (bool share in new[] { false, true })
                                    foreach (double reach in new[] { 1.0, 0.3 })
                                        for (int level = 1; level <= 3; level++)
                                            foreach (var pair in pairs)
                                            {
                                                var w = new CardWords { Kind = SayKind.Weapon, Level = level, Max = 4, Style = style, Build = build, Lifted = lift > 0, LiftLeft = lift == 2 ? "Experiment 21" : null,
                                                    Special = special, ShareType = share ? "Kinetic" : null, SharePct = 80, Reach = reach, Clock = "2:10 left", Next = level == 3 ? "Pump-Action Shotgun" : null };
                                                Rail("weapon level", Wording.Card(w, rank, pair.Item1, pair.Item2));
                                            }
            foreach (var n in weaponNames) Rail("weapon level", Wording.Card(new CardWords { Kind = SayKind.Weapon, Level = 3, Max = 4, Next = n }, 1, null, null), n);
            foreach (var n in names)
            {
                Rail("weapon level", Wording.Card(new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Lifted = true }, 2, n, longest), n);
                Rail("weapon level", Wording.Card(new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Lifted = true }, 3, longest, n), n);
            }
            Rail("first weapon", Wording.Card(new CardWords { Kind = SayKind.FirstWeapon }, 1, null, null));
            foreach (var branch in new[] { null, "build", "type", "guides", "yard" })
                foreach (var build in Builds)
                    foreach (var t in TagProfile.Names)
                        foreach (var with in names.Concat(new string[] { null }))
                            Rail("next tier", Wording.Card(new CardWords { Kind = SayKind.NextTier, Branch = branch, Build = build, BranchType = t, BranchWith = with }, 1, null, null), with);
            foreach (var n in weapons.Where(x => x.Tier == "Tier3").Select(x => x.Name).Concat(new string[] { null }))
                foreach (bool offered in new[] { true, false })
                    foreach (var branch in new[] { "build", "squad" })
                        Rail("other branch", Wording.Card(new CardWords { Kind = SayKind.OtherBranch, Next = n, NextOffered = offered, Branch = branch }, 3, null, null), n);
        }

        // ---------------------------------------------------------------- W1: evolutions
        static void Evolutions(List<string> names)
        {
            var evos = names.Where(n => n.Contains(':')).ToList();
            foreach (var e in evos)
                foreach (var build in Builds.Where(b => b != null))
                    foreach (bool mine in new[] { true, false })
                        foreach (bool offered in new[] { true, false })
                            Rail("evolution", Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = e.Substring(0, e.IndexOf(':')), Build = build, Pick = e, Mine = mine, PickOffered = offered }, 1, null, null), e);
            foreach (var t in TagProfile.Names)
                foreach (var with in names.Concat(new string[] { null }))
                    foreach (bool isNew in new[] { true, false })
                        foreach (bool pair in new[] { true, false })
                            foreach (var other in new[] { null, "Fire" })
                                Rail("evolution", Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = "Bombing Strike", Adds = t, AddsNew = isNew, AddsWith = with, Pair = pair, OtherAdds = other }, 1, null, null), with);
            foreach (var t in TagProfile.Names)
            {
                Rail("evolution", Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = "x", Special = t }, 1, null, null));
                Rail("evolution", Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = "x", ShareType = t, SharePct = 100 }, 1, null, null));
                Rail("evolution", Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = "x", ShortType = t, Short = 3 }, 1, null, null));
            }
            foreach (var bo in Boosts) Rail("evolution", Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = "x", Boost = bo }, 1, null, null));
            Rail("evolution", Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = "x" }, 1, null, null));
        }

        // ---------------------------------------------------------------- W1: items, through the item rules' own reasoning
        static RunContext Clock(double seconds, string mode = "Normal", double goal = 1200, double health = 1)
        {
            return new RunContext { Mode = mode, Goal = goal, Seconds = seconds, LevelRate = 3.0, LevelUps = (int)(seconds / 25), Health = health, D = new Doctrine() };
        }

        static List<ItemContext> Contexts()
        {
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            var list = new List<ItemContext>();
            // SWAT + Engineer: Kinetic and Electric, a deployable, magazines - the 0.10 checks' squad
            var a = new TagProfile { SpecialAt = 10 };
            a.Source("Assault Rifle", 1.0, new[] { "Kinetic" }); a.Source("Helicopter Strike", 0.4, new[] { "Kinetic" }); a.Source("Tesla", 0.7, new[] { "Electric" });
            a.Points["Kinetic"] = 8; a.Points["Electric"] = 4;
            var ca = new ItemContext { Squad = new[] { "SWAT", "Engineer" }, Tags = a, K = k, Ctx = Clock(120), OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Deployable" }, Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase), CloseShare = 0.5, LongShare = 0, ClipShare = 1, AbilityLean = 0.4, CritSquad = true };
            ca.Wants.Add(new KeyValuePair<string, string>("your Rifleman build", "critical"));
            list.Add(ca);
            // the Medic + Tank of the user's run: Kinetic, Explosive, Chemical, Ice; a lent build on Auto that wants ice, healing, abilities
            var b = new TagProfile { SpecialAt = 10 };
            b.Source("Experiment 21", 0.5, new[] { "Explosive", "Chemical", "Ice" }); b.Source("Rocket Launcher", 1.0, new[] { "Explosive" }); b.Source("Handgun", 0.4, new[] { "Kinetic" }); b.Source("Medical Drone: Offense", 0.5, new[] { "Kinetic" });
            b.Points["Kinetic"] = 16; b.Points["Explosive"] = 9; b.Points["Chemical"] = 3; b.Points["Ice"] = 3;
            var cb = new ItemContext { Squad = new[] { "Medic", "Tank" }, Tags = b, K = k, Ctx = Clock(274, "Hardcore", 600), OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Grenade" }, Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Golden Key" }, CloseShare = 0, LongShare = 0.5, ClipShare = 0, AbilityLean = 0.6 };
            foreach (var want in new[] { "ice", "healing", "abilities", "status effects" }) cb.Wants.Add(new KeyValuePair<string, string>("A Build Named Twenty1 (Auto)", want));
            list.Add(cb);
            // a hurt squad late in a run: survival, economy too late
            var c = new TagProfile { SpecialAt = 10 };
            c.Source("Flamethrower", 1.0, new[] { "Fire" }); c.Source("Molotov", 0.5, new[] { "Fire" }); c.Source("Syringe Rifle", 1.0, new[] { "Chemical" }); c.Source("Nitro-Gun", 1.0, new[] { "Ice" });
            c.Points["Fire"] = 12; c.Points["Chemical"] = 2; c.Points["Ice"] = 6; c.Points["Kinetic"] = 31;
            list.Add(new ItemContext { Squad = new[] { "Pyro", "Medic", "Mechanic" }, Tags = c, K = k, Ctx = Clock(1110, "Normal", 1200, 0.3), OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Turret", "Melee" }, Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Accumulator", "Silver Padlock" }, CloseShare = 1, LongShare = 0, ClipShare = 0.5, AbilityLean = 0.3 });
            // early, nothing known about the squad's damage
            list.Add(new ItemContext { Squad = new[] { "Ghost" }, Tags = new TagProfile(), K = k, Ctx = Clock(30, "Endless", 0) });
            return list;
        }

        static string ItemLine(ProbeItem it, ItemContext c, List<string> why, out ItemSay say)
        {
            say = new ItemSay();
            c.Stats = it.Stats; c.Healing = it.Healing; c.Say = say;
            ItemRules.Evaluate(it.Name, it.Desc, c, why);
            c.Say = null;
            return Wording.Item(say);
        }

        static void Items(List<ProbeItem> items)
        {
            foreach (var c in Contexts())
                foreach (var it in items)
                {
                    ItemSay say; var why = new List<string>();
                    Rail("item", ItemLine(it, c, why, out say), it.Name);
                    say.Quest = true; Rail("item", Wording.Item(say), it.Name);
                    say.Quest = false; say.Held = true; Rail("item", Wording.Item(say), it.Name);
                }
        }

        // ---------------------------------------------------------------- W1: Research Pod cards
        static void ResearchPods()
        {
            var profiles = new List<TagProfile> { null, new TagProfile() };
            var a = new TagProfile { SpecialAt = 10 }; a.Source("Assault Rifle", 1.0, new[] { "Kinetic" }); a.Source("Tesla", 0.7, new[] { "Electric" }); a.Points["Kinetic"] = 8; a.Points["Electric"] = 4; profiles.Add(a);
            var b = new TagProfile { SpecialAt = 10 }; b.Source("Handgun", 0.4, new[] { "Kinetic" }); b.Source("Medical Drone: Offense", 0.5, new[] { "Kinetic" }); b.Source("Experiment 21: 73", 0.5, new[] { "Explosive", "Ice" }); b.Source("Rocket Launcher", 1, new[] { "Explosive" });
            b.Points["Kinetic"] = 18; b.Points["Explosive"] = 16; b.Points["Ice"] = 9; b.Points["Chemical"] = 8; b.Points["Fire"] = 2; b.Points["Electric"] = 2; profiles.Add(b);
            var c = new TagProfile { SpecialAt = 10, Plan = "Spread" }; c.Source("Shuriken", 0.1, new[] { "Slashing" }); c.Source("Katana", 1.0, new[] { "Kinetic" }); profiles.Add(c);
            foreach (var p in profiles) foreach (var t in TagProfile.Names) foreach (int n in new[] { 1, 2, 4, 10 }) Rail("research pod", Wording.Card(new CardWords { Kind = SayKind.Tags, Type = t, Points = n, Tags = p }, 1, null, null));
        }

        // ---------------------------------------------------------------- W1: rescues and Liberate
        static void Rescues(List<string> names)
        {
            string longest = names.OrderByDescending(x => x.Length).First();
            var labels = new[] { null, "armor", "max health", "movement speed", "weapon cooldown reduction", "XP modifier" };
            foreach (var tier in new[] { null, "S", "A", "B", "C" })
                foreach (int bought in new[] { 0, 1, 2 })
                    foreach (int partners in new[] { 1, 2 })
                        foreach (var shared in new string[] { null }.Concat(TagProfile.Names))
                            foreach (var with in new[] { null, "Handgun", longest })
                                foreach (var bonus in labels)
                                    foreach (var boost in new[] { null, "Grenade Expertise" })
                                        foreach (var late in new[] { null, "2:10 left" })
                                            foreach (int unbought in new[] { 0, 1, 2 })
                                            {
                                                var r = new RecruitSay { Tier = tier, Bought = bought, SharedType = shared, SharedWith = with, TeamBonus = bonus, Boosts = boost, Covered = 2, Late = late, Unbought = unbought, RankLevels = unbought == 2 ? 3 : 0, Rank = 5 };
                                                if (bought > 0) { r.Partners.Add("Huntress"); if (partners > 1) r.Partners.Add("Engineer"); }
                                                Rail("rescue", Wording.Card(new CardWords { Kind = SayKind.Recruit, Recruit = r }, 1, null, null));
                                            }
            foreach (bool full in new[] { true, false }) foreach (var late in new[] { null, "1:40 left", "wave 9/10" })
                Rail("liberate", Wording.Card(new CardWords { Kind = SayKind.Recruit, Recruit = new RecruitSay { Liberate = true, Full = full, Late = late } }, 1, null, null));
        }

        // ---------------------------------------------------------------- W1: the quest's own lines (QuestSos.Card), as the cards say them
        static void Quests()
        {
            var rules = new List<QuestTeam>();
            var solo = new QuestTeam { Quest = "solo" }; solo.AtMost(1); rules.Add(solo);
            var pair = new QuestTeam { Quest = "pair" }; pair.AtLeast(2); pair.AtMost(2); pair.Need("Ghost"); pair.Need("Huntress"); rules.Add(pair);
            var full = new QuestTeam { Quest = "full" }; full.AtLeast(3); rules.Add(full);
            var need = new QuestTeam { Quest = "need" }; need.Need("Huntress"); rules.Add(need);
            foreach (var q in rules)
                foreach (var squad in new[] { new[] { "Medic" }, new[] { "Medic", "Ghost" }, new[] { "Ghost" } })
                {
                    var sos = q.Judge(squad);
                    foreach (var cand in new[] { null, "Tank", "Huntress", "Ghost", "Mechanic" })
                    {
                        var why = new List<string> { "level-up and cash instead of a recruit" };
                        sos.Card(cand, 3.0, why);
                        if (why[0].StartsWith("quest: ", StringComparison.Ordinal)) Rail("quest", Wording.Quest(why[0]), why[0]);
                    }
                }
            Rail("quest", Wording.Item(new ItemSay { QuestLine = "quest: health item 0/2" }));
            Rail("quest", Wording.Item(new ItemSay { Quest = true }));
        }

        // ---------------------------------------------------------------- W1: stat cards
        static void Stats(List<PowerFacts> powers)
        {
            var stats = MilitaryStats();
            var notes = new List<string[]> { null };
            foreach (var kind in new[] { "weapons", "abilities", "early", "late", "now", "hurting" }) foreach (var clock in Clocks) notes.Add(Wording.StatNote(kind, clock));
            foreach (var stat in stats)
                foreach (bool team in new[] { false, true })
                    foreach (var note in notes)
                        foreach (var build in Builds)
                            Rail("stat", Wording.Card(new CardWords { Kind = SayKind.Stat, Stat = Knowledge.StatLabel(stat) ?? stat, Team = team, StatWhy = note, WantBuild = build }, 1, null, null), stat);
        }
        // the military cards' stats (MilitaryTraining_<stat>), as the game names their assets
        static readonly string[] Military = { "AbilityCooldown", "AbilityCritChance", "AbilityCritDamage", "AbilityDamage", "AbilityDuration", "AbilitySize", "Armor", "DodgeChance", "HPRegen", "Luck",
            "MagnetRange", "MaxHealth", "MovementSpeedMod", "WeaponCooldownMod", "WeaponCritChance", "WeaponCritDamage", "WeaponDamageMod", "WeaponFireRateMod", "XPModifierMod" };
        static IEnumerable<string> MilitaryStats() { return Military; }

        // ---------------------------------------------------------------- W1: the reroll hint (drawn after "REROLL  -  ", on a wider band: 64)
        static void Rerolls()
        {
            foreach (var a in Classes) foreach (var b in Classes.Concat(new string[] { null })) foreach (int more in new[] { 0, 1, 3 })
                Rail("reroll", Wording.Reroll(b == null ? new[] { a } : new[] { a, b }, more), null, 64);
        }

        // ---------------------------------------------------------------- what the builders said: a spread per builder, read by hand
        static void Samples()
        {
            Console.WriteLine("    a spread of the lines (what a player reads):");
            foreach (var kv in Said.OrderBy(k => k.Key))
            {
                var list = kv.Value; int take = Math.Min(list.Count, Environment.GetEnvironmentVariable("YAZS_BENCH_ALLWORDS") == "1" ? 100000 : kv.Key == "item" ? 24 : 10);
                for (int i = 0; i < take; i++) Console.WriteLine("      " + kv.Key.PadRight(14) + list[(int)((long)i * list.Count / take)]);
            }
        }

        // ---------------------------------------------------------------- W2: the glyphs
        static void Glyphs()
        {
            Check("W2", "Safe maps the glyphs the card font lacks and drops '<' (keeps '>' and '»')",
                Wording.Safe("a → b › c · d • e — f – g ≥ h <b>i</b> | j\\k^l`m 2>3 »") == "a » b » c - d - e - f - g >= h b>i/b> / jklm 2>3 »" && Wording.Safe(null) == "" && ReferenceEquals(Wording.Safe("plain"), "plain"),
                Wording.Safe("a → b › c · d • e — f – g ≥ h <b>i</b> | j\\k^l`m 2>3 »"));
            Check("W2", "the forms: too long loses the parenthesis first, then the next form", Wording.Say("Level 3 of 4", "too late to evolve it (2:10 left on a long clock)", "x") == "Level 3 of 4 - too late to evolve it"
                && Wording.Say(null, "level one") == "Level one" && Wording.Short("Bombing Strike: Bioweapon") == "Bioweapon" && Wording.Short("Experiment 21: 73") == "Experiment 21: 73" && Wording.Short("Bear Trap: Fire") == "Bear Trap: Fire" && Wording.FirstSource("Experiment 21: 73, Rocket Launcher +2") == "Experiment 21: 73"
                && Wording.FirstSource("Rocket Launcher +2") == "Rocket Launcher" && Wording.BuildOf("your Rifleman build") == "Rifleman" && Wording.BuildOf("Rifleman (Auto)") == "Rifleman");
        }

        // ---------------------------------------------------------------- W3: the user's offers of 2026-10-05
        sealed class Logged { public string At, Card, Head; public CardWords W; public int Rank = 1; public string First, Second; }

        static CardWords Ab(int level, int max, string build, int priority, bool evoOwned = false, bool evoExists = true, double reach = 1, bool focus = false, int owned = 1, string clock = "4:20 left")
        {
            var w = new CardWords { Kind = SayKind.Ability, Level = level, Max = max, Build = build, Priority = priority, EvoExists = evoExists, EvoOwned = evoOwned, Reach = reach, Focus = focus, Owned = owned, Clock = clock };
            if (build != null && priority >= 0) { w.HeadRank = priority <= 1 ? 4 : 1; w.Head = Wording.Role(build, priority); }
            return w;
        }

        static void Replay(List<ProbeItem> items)
        {
            const string A = "Bench Anchor", R = "Bench Rocket", S = "Bench Summons";
            var shown = new List<Logged>
            {
                // 18:02 session: Hardcore, the Medic leads on a lent build (Auto), the Tank and the Ranger join
                new Logged { At = "18:02:59", Card = "Medical Drone", Head = "new ability - #2 in " + A, W = Ab(0, 4, A, 1, owned: 0) },
                new Logged { At = "18:02:59", Card = "Experiment 21", Head = "new ability - #1 in " + A, W = Ab(0, 4, A, 0, owned: 0), Rank = 2 },
                new Logged { At = "18:02:59", Card = "Handgun", Head = "weapon level: 1 to 2 of 4", Rank = 3, W = new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Build = A } },
                new Logged { At = "18:03:19", Card = "Medical Drone", Head = "focus 1>2 of 4 toward its evolution - #2 in " + A, W = Ab(1, 4, A, 1, true, focus: true) },
                new Logged { At = "18:03:55", Card = "Medical Drone", Head = "focus 2>3 of 4 toward its evolution - #2 in " + A, W = Ab(2, 4, A, 1, true, focus: true) },
                new Logged { At = "18:15:26", Card = "Medical Drone", Head = "focus 3>4 of 4 toward its evolution - #2 in " + A, W = Ab(3, 4, A, 1, true, focus: true) },
                new Logged { At = "18:15:33", Card = "Medical Drone: Offense", Head = "evolution of Medical Drone: 3 tags from the Kinetic special", W = new CardWords { Kind = SayKind.Evolution, Base = "Medical Drone", ShortType = "Kinetic", Short = 3, ShareType = "Kinetic", SharePct = 100 } },
                new Logged { At = "18:15:41", Card = "Zone of Action (Endless)", Head = "Endless: Ability Size - " + A + " (Auto) wants it", W = new CardWords { Kind = SayKind.Stat, Stat = Knowledge.StatLabel("AbilitySize"), WantBuild = A } },
                new Logged { At = "18:16:03", Card = "Fast Mover (Endless)", Head = "Endless, team-wide: Movement Speed", Rank = 3, W = new CardWords { Kind = SayKind.Stat, Stat = Knowledge.StatLabel("MovementSpeedMod"), Team = true } },
                new Logged { At = "18:16:03", Card = "Handgun", Head = "weapon level: 1 to 2 of 4", Rank = 2, First = "Experiment 21", W = new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Build = A, Lifted = true, LiftLeft = "Experiment 21" } },
                new Logged { At = "18:16:24", Card = "Kevlar Plating (Endless)", Head = "Endless, team-wide: Armor - " + A + " (Auto) wants it", Rank = 2, W = new CardWords { Kind = SayKind.Stat, Stat = Knowledge.StatLabel("Armor"), Team = true, WantBuild = A } },
                new Logged { At = "18:16:42", Card = "Huntress", Head = "S-tier rescue in the guides", W = new CardWords { Kind = SayKind.Recruit, Recruit = new RecruitSay { Tier = "S", Unbought = 2, SharedType = "Kinetic", SharedWith = "Handgun" } } },
                new Logged { At = "18:16:42", Card = "Liberate", Head = "level-up and cash instead of a recruit", Rank = 3, W = new CardWords { Kind = SayKind.Recruit, Recruit = new RecruitSay { Liberate = true } } },
                new Logged { At = "18:16:51", Card = "Tank", Head = "A-tier rescue, 1 bought synergy with Medic", W = new CardWords { Kind = SayKind.Recruit, Recruit = Bought("A", "Medic") } },
                new Logged { At = "18:17:05", Card = "Shotgun", Head = "weapon level: 1 to 2 of 4", W = new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Weapon, Build = R, Special = "Kinetic" } },
                new Logged { At = "18:17:30", Card = "Shotgun", Head = "completes the weapon: 3 to 4 of 4, then Pump-Action Shotgun", W = new CardWords { Kind = SayKind.Weapon, Level = 3, Max = 4, Style = BuildStyle.Weapon, Build = R, Next = "Pump-Action Shotgun" } },
                new Logged { At = "18:17:41", Card = "Fury Unleashed", Head = "new ability - synergy: Medic", W = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Build = R, Priority = 3, HeadRank = 3, Head = Wording.Synergy("Medic"), Owned = 1 } },
                new Logged { At = "18:17:55", Card = "Fury Unleashed", Head = "level 1>2 of 4 - synergy: Medic", Rank = 3, W = new CardWords { Kind = SayKind.Ability, Level = 1, Max = 4, Build = R, Priority = 3, HeadRank = 3, Head = Wording.Synergy("Medic"), EvoExists = true } },
                new Logged { At = "18:18:02", Card = "Pump-Action Shotgun", Head = "next weapon tier", W = new CardWords { Kind = SayKind.NextTier } },
                new Logged { At = "18:19:17", Card = "Rocket Launcher", Head = "next weapon tier: " + R + " (Auto)", W = new CardWords { Kind = SayKind.NextTier, Branch = "build", Build = R } },
                new Logged { At = "18:19:17", Card = "Super Shotgun", Head = "other branch; Auto follows " + R + ", which takes Rocket Launcher", Rank = 4, W = new CardWords { Kind = SayKind.OtherBranch, Next = "Rocket Launcher", Branch = "build" } },
                new Logged { At = "18:21:34", Card = "Experiment 21: 73", Head = "evolution of Experiment 21: 2 tags from the Explosive special", W = new CardWords { Kind = SayKind.Evolution, Base = "Experiment 21", ShortType = "Explosive", Short = 2, ShareType = "Explosive", SharePct = 61 } },
                new Logged { At = "18:23:08", Card = "Chem-Light Battery", Head = "Common: Ability Cooldown - " + A + " (Auto) wants it", W = new CardWords { Kind = SayKind.Stat, Stat = Knowledge.StatLabel("AbilityCooldown"), WantBuild = A } },
                new Logged { At = "18:23:08", Card = "Magnet Mount", Head = "Common, team-wide: Magnet Range", Rank = 4, W = new CardWords { Kind = SayKind.Stat, Stat = Knowledge.StatLabel("MagnetRange"), Team = true } },
                new Logged { At = "18:23:40", Card = "Bombing Strike: Bioweapon", Head = "evolution of Bombing Strike: +1 Explosive tag: 56% of the squad's damage (...)", W = new CardWords { Kind = SayKind.Evolution, Base = "Bombing Strike", Adds = "Chemical", AddsNew = true, ShareType = "Explosive", SharePct = 56 } },
                new Logged { At = "18:23:40", Card = "Bombing Strike: Supercharge", Head = "evolution of Bombing Strike: +1 Explosive tag: 56% of the squad's damage (...)", Rank = 2, W = new CardWords { Kind = SayKind.Evolution, Base = "Bombing Strike", Adds = "Electric", AddsNew = true, ShareType = "Explosive", SharePct = 56 } },
                new Logged { At = "18:24:57", Card = "Minefield: Shrapnel", Head = "evolution of Minefield: +1 Explosive tag: 56% of the squad's damage (...)", W = new CardWords { Kind = SayKind.Evolution, Base = "Minefield", Adds = "Slashing", AddsNew = false, AddsWith = "Sawblade Drone", ShareType = "Explosive", SharePct = 56 } },
                new Logged { At = "18:26:29", Card = "Max Pain (Rare)", Head = "Rare: Weapon Damage", W = new CardWords { Kind = SayKind.Stat, Stat = Knowledge.StatLabel("WeaponDamageMod") } },
                new Logged { At = "18:27:26", Card = "Sawblade Drone", Head = "focus 1>2 of 4, evolution out of reach - #3 in " + R, Rank = 2, W = Ab(1, 4, R, 2, true, reach: 0.4, focus: true, clock: "2:58 left") },
                new Logged { At = "18:28:24", Card = "Sawblade Drone", Head = "focus 2>3 of 4, evolution out of reach - unlocks the Slashing special", W = new CardWords { Kind = SayKind.Ability, Level = 2, Max = 4, Build = R, Priority = 2, HeadRank = 5, Head = Wording.Special("Slashing"), EvoExists = true, EvoOwned = true, Reach = 0.3, Focus = true, Clock = "1:36 left" } },
                // 19:01 session: Endless, the Ranger leads on a lent build (Auto), then the Engineer on the Companion's Shield Anchor
                new Logged { At = "19:04:02", Card = "Falcon", Head = "new ability - #2 in " + S, W = Ab(0, 4, S, 1, owned: 0, clock: "open-ended") },
                new Logged { At = "19:05:27", Card = "Siege Engines (Endless)", Head = "Endless: Ability Crit Damage - " + S + " (Auto) wants it", W = new CardWords { Kind = SayKind.Stat, Stat = Knowledge.StatLabel("AbilityCritDamage"), WantBuild = S } },
                new Logged { At = "19:06:23", Card = "Huntress", Head = "S-tier rescue in the guides", W = new CardWords { Kind = SayKind.Recruit, Recruit = new RecruitSay { Tier = "S" } } },
                new Logged { At = "19:10:47", Card = "Electric Turret", Head = "new ability - #2 in Shield Anchor", W = Ab(0, 4, "Shield Anchor", 1, owned: 1, clock: "open-ended") },
                new Logged { At = "19:12:19", Card = "Electric Turret", Head = "3>4 of 4 toward its evolution - unlocks the Electric special", W = new CardWords { Kind = SayKind.Ability, Level = 3, Max = 4, Build = "Shield Anchor", Priority = 1, HeadRank = 5, Head = Wording.Special("Electric"), EvoExists = true, EvoOwned = true, Clock = "open-ended" } },
                new Logged { At = "19:12:19", Card = "Energy Shield", Head = "focus 3>4 of 4 - unlocks the Electric special", Rank = 2, W = new CardWords { Kind = SayKind.Ability, Level = 3, Max = 4, Build = "Shield Anchor", Priority = 0, HeadRank = 5, Head = Wording.Special("Electric"), EvoExists = true, Focus = true, Clock = "open-ended" } },
                new Logged { At = "19:15:25", Card = "Taser", Head = "completes the weapon: 3 to 4 of 4, then Tesla", Rank = 3, W = new CardWords { Kind = SayKind.Weapon, Level = 3, Max = 4, Style = BuildStyle.Balanced, Build = "Shield Anchor", Next = "Tesla" } },
                new Logged { At = "19:16:04", Card = "Tesla", Head = "next weapon tier", W = new CardWords { Kind = SayKind.NextTier } },
            };
            // the Research Pod of 18:23:30 with the squad the log shows (Kinetic 18 points, stacked; Chemical 8: nobody deals it)
            var pod = new TagProfile { SpecialAt = 10 };
            pod.Source("Experiment 21: 73", 0.6, new[] { "Explosive", "Ice" }); pod.Source("Rocket Launcher", 1.0, new[] { "Explosive" }); pod.Source("Handgun", 0.45, new[] { "Kinetic" }); pod.Source("Medical Drone: Offense", 0.7, new[] { "Kinetic" });
            foreach (var kv in new Dictionary<string, int> { { "Kinetic", 18 }, { "Explosive", 16 }, { "Ice", 9 }, { "Chemical", 8 }, { "Fire", 2 }, { "Electric", 2 } }) pod.Points[kv.Key] = kv.Value;
            shown.Add(new Logged { At = "18:23:30", Card = "#Kinetic +10", Head = "Kinetic: 44% of the squad's damage (Handgun, Medical Drone: Offense +2)", W = new CardWords { Kind = SayKind.Tags, Type = "Kinetic", Points = 10, Tags = pod } });
            shown.Add(new Logged { At = "18:23:30", Card = "#Chemical +10", Head = "reaches the Chemical special effect (10)", Rank = 2, W = new CardWords { Kind = SayKind.Tags, Type = "Chemical", Points = 10, Tags = pod } });

            // what the ranking does once the ranks are final: two evolutions of one base on one offer learn of each other (Wording.Pair)
            foreach (var g in shown.GroupBy(l => l.At)) Wording.Pair(g.Select(l => l.W).ToList());
            Console.WriteLine("    W3 the logged offers of 2026-10-05: the logged headline -> as 0.13.0 drew it -> the line now");
            int n = 0;
            foreach (var l in shown)
            {
                string now = Wording.Card(l.W, l.Rank, l.First, l.Second);
                string before = l.Head.Replace(">", "");
                Rail("replay", now, l.Card);
                Console.WriteLine("      " + l.At + " " + (l.Rank == 1 ? "    " : Ord(l.Rank) + " ") + l.Card.PadRight(28) + (before != l.Head ? "'" + before + "' (log: " + l.Head + ")" : "'" + l.Head + "'") + "\n        -> '" + now + "'");
                n++;
            }
            // the chests, through the real item rules with the squad of the log
            var chests = new[]
            {
                Tuple.Create("18:15:41", new[] { "Last Unicorn", "Vampire Survivor", "Potato", "Jewel of Life" }, 0),
                Tuple.Create("18:19:31", new[] { "Detective's Pipe", "Frying Pan", "Golden Key", "Crowbar" }, 1),
                Tuple.Create("18:19:39", new[] { "Power Generator", "Heavy Metal", "T-Pose Doll" }, 1),
                Tuple.Create("18:19:50", new[] { "Giant Enemy Crab", "Boxing Gloves", "Health Potion", "Teddy Bear" }, 1),
                Tuple.Create("18:21:34", new[] { "Magical Hat", "Coffee Cup", "Ultra Instinct", "Quicksilver Bullets" }, 2),
                Tuple.Create("18:24:37", new[] { "Great Nade", "Solar Panel", "Boxing Gloves", "Nuts & Bolts" }, 3),
                Tuple.Create("18:24:42", new[] { "Magazine Clip", "Icon of Tempest", "Teddy Bear", "Pocket Watch" }, 3),
                Tuple.Create("18:26:37", new[] { "Battery Leakage", "Glass of Milk", "Heavy Metal", "Mouse Trap" }, 3),
                Tuple.Create("19:06:14", new[] { "Skip Rope", "Golden Key", "Buckshot Roulette", "Hit Tracks" }, 4),
            };
            var ctxs = RunContexts();
            foreach (var chest in chests)
                foreach (var name in chest.Item2)
                {
                    var it = items.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (it == null) { Console.WriteLine("      " + chest.Item1 + "     (no item " + name + " in the probe)"); continue; }
                    ItemSay say; var why = new List<string>(); var c = ctxs[chest.Item3];
                    string now = ItemLine(it, c, why, out say);
                    Rail("replay", now, name);
                    Console.WriteLine("      " + chest.Item1 + "     " + name.PadRight(28) + "'" + (why.Count > 0 ? why[0] : "no squad-specific value") + "'\n        -> '" + now + "'");
                    n++;
                }
            Check("W3", "the 10-05 offers replayed through the builders, each within the rails", n >= 15 && !Offenders.Any(o => o.StartsWith("replay", StringComparison.Ordinal)), n + " cards");
        }

        static string Ord(int rank) { return rank == 2 ? "2ND" : rank == 3 ? "3RD" : rank + "TH"; }
        static RecruitSay Bought(string tier, string with) { var r = new RecruitSay { Tier = tier, Bought = 1, SharedType = "Kinetic", SharedWith = "Handgun", TeamBonus = "armor" }; r.Partners.Add(with); return r; }

        // the run of 18:02 at four moments (damage types and tag points from its [tags] lines; the weights are estimates), and the 19:01 Ranger
        static List<ItemContext> RunContexts()
        {
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            Func<double, ItemContext> medic = t =>
            {
                var c = new ItemContext { Squad = new[] { "Medic", "Tank" }, K = k, Ctx = new RunContext { Mode = "Hardcore", Goal = 600, Seconds = t, LevelRate = 4.4, D = new Doctrine() }, OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase), CloseShare = 0.5, LongShare = 0, ClipShare = 0.5, AbilityLean = 0.55 };
                foreach (var want in new[] { "ice", "healing", "status effects", "abilities" }) c.Wants.Add(new KeyValuePair<string, string>("Bench Anchor (Auto)", want));
                return c;
            };
            var list = new List<ItemContext>();
            var c0 = medic(41); var t0 = new TagProfile { SpecialAt = 10 }; t0.Source("Handgun", 0.4, new[] { "Kinetic" }); t0.Source("Medical Drone", 0.5, new[] { "Kinetic" }); t0.Points["Kinetic"] = 7; c0.Tags = t0; c0.Squad = new[] { "Medic" }; list.Add(c0);
            var c1 = medic(184); var t1 = new TagProfile { SpecialAt = 10 };
            t1.Source("Experiment 21", 0.5, new[] { "Explosive", "Chemical", "Ice" }); t1.Source("Rocket Launcher", 0.5, new[] { "Explosive" }); t1.Source("Handgun", 0.4, new[] { "Kinetic" }); t1.Source("Medical Drone: Offense", 0.5, new[] { "Kinetic" });
            foreach (var kv in new Dictionary<string, int> { { "Kinetic", 16 }, { "Explosive", 5 }, { "Chemical", 3 }, { "Ice", 3 } }) t1.Points[kv.Key] = kv.Value; c1.Tags = t1; list.Add(c1);
            var c2 = medic(274); var t2 = new TagProfile { SpecialAt = 10 };
            t2.Source("Experiment 21: 73", 0.6, new[] { "Explosive", "Ice" }); t2.Source("Rocket Launcher", 1.0, new[] { "Explosive" }); t2.Source("Handgun", 0.4, new[] { "Kinetic" }); t2.Source("Medical Drone: Offense", 0.5, new[] { "Kinetic" });
            foreach (var kv in new Dictionary<string, int> { { "Kinetic", 16 }, { "Explosive", 10 }, { "Ice", 5 }, { "Chemical", 4 } }) t2.Points[kv.Key] = kv.Value; c2.Tags = t2; list.Add(c2);
            var c3 = medic(400); var t3 = new TagProfile { SpecialAt = 10 };
            t3.Source("Experiment 21: 73", 0.6, new[] { "Explosive", "Ice" }); t3.Source("Rocket Launcher", 1.0, new[] { "Explosive" }); t3.Source("Handgun", 0.45, new[] { "Kinetic" }); t3.Source("Medical Drone: Offense", 0.7, new[] { "Kinetic" });
            t3.Source("Bombing Strike: Bioweapon", 0.5, new[] { "Explosive", "Chemical" }); t3.Source("Crossbow", 0.3, new[] { "Kinetic" });
            foreach (var kv in new Dictionary<string, int> { { "Kinetic", 29 }, { "Explosive", 18 }, { "Chemical", 9 }, { "Ice", 9 }, { "Fire", 2 }, { "Electric", 2 } }) t3.Points[kv.Key] = kv.Value;
            c3.Tags = t3; c3.Squad = new[] { "Medic", "Tank", "Ranger" }; c3.ClipShare = 0.67; list.Add(c3);
            var c4 = new ItemContext { Squad = new[] { "Ranger" }, K = k, Ctx = new RunContext { Mode = "Endless", Goal = 0, Seconds = 120, LevelRate = 4, D = new Doctrine() }, OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase), Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase), AbilityLean = 0.6 };
            var t4 = new TagProfile { SpecialAt = 10 }; t4.Source("Crossbow", 0.4, new[] { "Kinetic" }); t4.Source("Falcon", 0.5, new[] { "Kinetic" }); t4.Source("Good Boy", 0.5, new[] { "Slashing" }); t4.Points["Kinetic"] = 6; t4.Points["Slashing"] = 4; c4.Tags = t4;
            list.Add(c4);
            return list;
        }

        // ---------------------------------------------------------------- W4: the game's stat labels, and the sources
        static void Labels(List<PowerFacts> powers)
        {
            var missing = Military.Where(s => Knowledge.StatLabel(s) == null).ToList();
            Check("W4", "every military card's stat has the game's own label (" + Military.Length + ")", missing.Count == 0,
                "AbilitySize '" + Knowledge.StatLabel("AbilitySize") + "', MagnetRange '" + Knowledge.StatLabel("MagnetRange") + "', HPRegen '" + Knowledge.StatLabel("HPRegen") + "', WeaponFireRateMod '" + Knowledge.StatLabel("WeaponFireRateMod")
                + "', WeaponCooldownMod '" + Knowledge.StatLabel("WeaponCooldownMod") + "', XPModifierMod '" + Knowledge.StatLabel("XPModifierMod") + "', TeamArmor '" + Knowledge.StatLabel("TeamArmor") + "'" + (missing.Count > 0 ? " | missing: " + string.Join(", ", missing) : ""));
            Check("W4", "the labels are the game's (data\\gamedata.json UI/StatType), in a sentence's case", Knowledge.StatLabel("AbilitySize") == "ability area" && Knowledge.StatLabel("MagnetRange") == "pickup range"
                && Knowledge.StatLabel("HPRegen") == "health regeneration" && Knowledge.StatLabel("WeaponFireRateMod") == "weapon attack speed" && Knowledge.StatLabel("WeaponCooldownMod") == "weapon cooldown reduction"
                && Knowledge.StatLabel("XPModifierMod") == "XP modifier" && Knowledge.StatLabel("AbilitySize", false) == "Ability area" && Knowledge.StatLabel("PlayerAbilitySize") == "ability area" && Knowledge.StatLabel("Nonsense") == null);
        }

        static void Sources()
        {
            string badge = Src("Badge.cs"), ranker = Src("Ranker.cs"), advisor = Src("Advisor.cs"), plan = Src("Plan.cs"), hint = Src("RerollHint.cs"), panel = Src("Panel.cs"), notice = Src("Notice.cs"), menu = Src("Menu.cs"), proj = File.Exists(Proj()) ? File.ReadAllText(Proj()) : null;
            Check("W4", "the badge draws the plain words: Wording.Safe(Names.Text(c.Display ?? c.Reason)), after its ReasonPrefix",
                badge != null && badge.Contains("Wording.Safe(Names.Text(c.Display ?? c.Reason))") && badge.Contains("prefix + Text(c)") && badge.Contains("Synergy.ReasonPrefix(c.Rank, c.Score)"));
            int ranks = ranker == null ? -1 : ranker.IndexOf("order[i].Rank = i + 1;", StringComparison.Ordinal), say = ranker == null ? -1 : ranker.IndexOf("SayAll(order);", StringComparison.Ordinal);
            Check("W4", "the ranking fills Card.Display once the ranks are final (SayAll after the ranks), measured as drawn (beside its prefix, the lent names) and told apart",
                ranks > 0 && say > ranks && ranker.Contains("c.Display = Wording.Card(c.Say, c.Rank, first, second, room, LendNames)") && ranker.Contains("Wording.RoomBeside(Synergy.ReasonPrefixWidth(c.Rank, c.Score), width)") && ranker.Contains("width = Badge.LineChars();")
                && ranker.Contains("WhyText.Distinct(lines,") && ranker.Contains("LendNames = Names.Text"));
            Check("W4", "one [shown] line per offer with the drawn text; the [card] line keeps its shape", advisor != null && advisor.Contains("\"[shown] \"") && advisor.Contains("Badge.Text(c)")
                && advisor.Contains("line.Append(\"[card] #\").Append(c.Rank).Append(c.Rank == 1 ? \" PICK \" : \"      \").Append(c.Name).Append(\" (\").Append(c.Kind);"));
            Check("W4", "the ribbon follows the game's Skill Tree label from the HUD tick (no Harmony hook of its own); the scale uses the card's chain",
                advisor != null && Regex.IsMatch(advisor, @"P_HudTick[\s\S]{0,400}Badge\.Tick\(\)") && badge.Contains("skillTreeCurrentLevelGameObject") && badge.Contains("1f - a") && !badge.Contains("[HarmonyPatch")
                && badge.Contains("RestingRootScale = 0.95f") && badge.Contains("canvas.pixelRect.height") && badge.Contains("CardTextSize.Scale(") && badge.Contains("[badge] scale s="));     // 0.15.0 (C15-06): the cap is CardTextSize's (x1.3, x1.6 under 1080 px tall)
            Check("W4", "the readout's words: '»' when '›' is missing, 'next Pod: T', 'max tier', no 'stack T' / 'build T' / 'line complete'; no score on the reroll hint; no bullet in the notice",
                plan != null && plan.Contains("\"next Pod: \" + focus") && !plan.Contains("\"build \" + focus") && plan.Contains("max tier") && !plan.Contains("\"stack ") && !plan.Contains("line complete") && plan.Contains("no new ability - too late to level one") && plan.Contains("\" later\"")
                && panel != null && panel.Contains("HasCharacter('»'") && hint != null && !hint.Contains("\" would rate higher") && hint.Contains("Wording.Reroll(") && notice != null && !notice.Contains("•"));
            Check("W4", "the menu's vocabulary: 'main ability', '10-tag effect', the readout legend, no 'stack'", menu != null && !menu.Contains("focus ability") && !menu.Contains("#1 ability") && !menu.Contains("tag special") && menu.Contains("TAGS damage type tags") && menu.Contains("GRAB items worth a chest pick")
                && !menu.Contains("stack the main type") && !menu.Contains("AUTO stacks"));
            string plugin = Src("Plugin.cs");
            Check("W4", "the cfg's vocabulary: 'the main ability', '10-tag effects', no 'tag specials' / 'focus ability' / 'stack the type'",
                plugin != null && !plugin.Contains("focus ability") && !plugin.Contains("tag specials") && !plugin.Contains("stack the type") && plugin.Contains("10-tag effects within reach"));
            Check("W4", "Wording.cs is compiled into the bench (pure: no game types)", proj != null && proj.Contains("Wording.cs"));
        }
        static string Proj() { return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ItemBench.csproj")); }

        // ---------------------------------------------------------------- W5: the 10-06 live wording fixes (0.15.0, C15-03)
        // (a) the first card, a weapon level under "abilities first" and not lifted, says what decided instead of the style the order
        //     contradicts (live 19:18:27.924: "The ... build levels abilities first" on the #1 Handgun) - the best ability on the offer has a
        //     low place in the same build, none, or the build skips it; plainly, the abilities rank lower. The mirror: an ability first over a
        //     level of a weapon its style puts first says the clock, and that weapon's own WHY drops "levels weapons first".
        // (b) an evolution's own part of its name only where its base is named on the card or in the line (live 19:33:05.529: "Frost goes
        //     first: ..." on a Sawblade Drone card, the first card being an evolution named "...: Frost").
        // (c) a build that names no evolution for the base, its main or a core ability: "Evolves the <B> build's main ability" (19:33:04.927).
        // (d) the readout's TAGS row says a met 10-tag effect: "Kinetic 31 - effect on" (19:33:51.096 read "Kinetic 10").
        // Game names only; the builds are the bench's ("Bench Anchor", "Bench Rocket").
        sealed class Offered { public string Name; public double Score; public CardWords W; public string Shown; }

        // an offer once the ranks are final, as Ranker.SayAll makes it: the ranks and rivals (Wording.Ranks), then each card's line
        static List<Offered> Offer(Func<string, string> lend, params Offered[] cards)
        {
            var list = cards.ToList();
            Wording.Ranks(list.Select(c => c.W).ToList(), list.Select(c => c.Name).ToList());
            string first = list.Count > 0 ? list[0].Name : null, second = list.Count > 1 ? list[1].Name : null;
            foreach (var c in list) c.Shown = Wording.Card(c.W, c.W.Rank, first, second, Wording.Budget, lend);
            return list;
        }

        // the WHY band's words for the card at <rank>, as WhyUi.InputOf asks for them
        static WhyBlock WhyOf(List<Offered> offer, int rank, Func<string, string> lend)
        {
            var c = offer[rank - 1]; var f = offer[0]; var s = offer.Count > 1 ? offer[1] : null;
            return WhyText.Block(new WhyIn { Name = c.Name, Rank = rank, Score = c.Score, Shown = c.Shown, Say = c.W, FirstName = f.Name, FirstScore = f.Score, FirstShown = f.Shown,
                SecondName = s != null ? s.Name : null, SecondScore = s != null ? s.Score : double.NaN, FirstSay = f.W, Lend = lend });
        }

        static CardWords Gun(int level, BuildStyle style, string build, double reach = 1, string clock = "18:19 left", int max = 4)
        {
            return new CardWords { Kind = SayKind.Weapon, Level = level, Max = max, Style = style, Build = build, Reach = reach, Clock = clock };
        }

        static void Live1006(List<string> names, List<string> weaponNames, List<ProbeItem> items)
        {
            Console.WriteLine("    W5 the 10-06 live wording fixes (C15-03): the #1 weapon's WHY, names whole, the build's evolution head, TAGS 'effect on'");
            const string A = "Bench Anchor", R = "Bench Rocket";
            var lines = new List<string>(); var whys = new List<string>(); var versus = new List<string>();     // every card line, WHY reason and "vs #1" sentence built here, through the rails at the end
            Action<WhyBlock> keep = k => { whys.AddRange(k.Reasons); if (k.Versus != null) versus.Add(k.Versus); };

            // ---- (a) the live 19:18:27 offer: Handgun 3.65 (abilities first, not lifted), Resuscitation 3.45 (new, 4th in the build), Stimpack 3.20
            var live = Offer(null,
                new Offered { Name = "Handgun", Score = 3.65, W = Gun(2, BuildStyle.Ability, A) },
                new Offered { Name = "Resuscitation", Score = 3.45, W = Ab(0, 4, A, 3, owned: 2) },
                new Offered { Name = "Stimpack", Score = 3.20, W = Ab(1, 4, A, 2) });
            var b = WhyOf(live, 1, null); string items1 = string.Join("  /  ", b.Items);
            Console.WriteLine("      19:18:27 #1 Handgun under the card '" + live[0].Shown + "' -> WHY  " + items1);
            Check("W5", "(a) the live #1 Handgun under abilities first: no 'levels abilities first' - the best ability's low place in the same build",
                !items1.Contains("abilities first") && b.Reasons.Contains("Resuscitation is 4th in the build's order") && live[0].Shown == "Level 3 of 4 - the best of this offer", items1);
            keep(b); lines.AddRange(live.Select(c => c.Shown));
            Func<CardWords, string, List<string>> firstWhy = (rival, rivalName) =>
            {
                var cards = new List<Offered> { new Offered { Name = "Handgun", Score = 4.10, W = Gun(1, BuildStyle.Ability, A) } };
                if (rival != null) cards.Add(new Offered { Name = rivalName, Score = 3.90, W = rival });
                cards.Add(new Offered { Name = "Accumulator", Score = 2.10, W = new CardWords { Kind = SayKind.Item, Item = new ItemSay { Tier = "B" } } });
                var o = Offer(null, cards.ToArray()); var blk = WhyOf(o, 1, null);
                keep(blk); lines.AddRange(o.Select(c => c.Shown));
                return blk.Reasons;
            };
            var none = firstWhy(null, null);
            var skip = firstWhy(new CardWords { Kind = SayKind.Ability, Level = 1, Max = 4, Build = A, Priority = -1, HeadRank = 6, Head = Wording.Skips(A), EvoExists = true }, "Resuscitation");
            var outside = firstWhy(Ab(0, 4, A, -1, owned: 2), "Stimpack");
            var other = firstWhy(Ab(2, 4, "Rifleman", 0), "Electric Turret");
            var core = firstWhy(Ab(2, 4, A, 1), "Medical Drone");
            Check("W5", "(a) what decided, by the best ability on the offer: none, the build skips it, not in the build, another survivor's build, a core ability",
                none.Contains("No ability on this offer") && skip.Contains("The Bench Anchor build skips Resuscitation") && outside.Contains("Stimpack is not in the Bench Anchor build")
                && other.Contains("The abilities on offer rank lower here") && core.Contains("The abilities on offer rank lower here")
                && !none.Concat(skip).Concat(outside).Concat(other).Concat(core).Any(r => r.Contains("levels abilities first")),
                string.Join(" | ", new[] { none, skip, outside, other, core }.Select(l => l.FirstOrDefault(r => r.StartsWith("No ") || r.Contains("skips") || r.Contains("not in") || r.Contains("rank lower")) ?? "?")));
            // the ranks unknown (the sweeps, a card scored outside an offer): as before
            var unranked = WhyText.Reasons(Gun(2, BuildStyle.Ability, A), null, "Level 3 of 4 - the best of this offer");
            Check("W5", "(a) a weapon level whose rank is not known keeps the style line (the bench's sweeps unchanged)", unranked.Contains("The Bench Anchor build levels abilities first"), string.Join(" / ", unranked));
            // a lent name 19 characters longer: the decider stays within the card line's 50 as drawn
            Func<string, string> longer = s => s == null ? s : s.Replace("Resuscitation", "Resuscitation Nnnnnnnnnnnnnnnnnn");
            var lentLive = Offer(longer, live.Select(c => new Offered { Name = c.Name, Score = c.Score, W = c.W }).ToArray());
            var lb = WhyOf(lentLive, 1, longer);
            string decider = lb.Reasons.FirstOrDefault(r => r.Contains("4th") || r.Contains("rank lower"));
            Check("W5", "(a) with a lent name 19 characters longer the decider is the form that fits 50 as drawn", decider == "The abilities on offer rank lower here" && Wording.Safe(longer(decider)).Length <= Wording.Budget, decider);
            // the mirror: an ability first over a level of a weapon its style puts first
            var mirror = Offer(null,
                new Offered { Name = "Electric Turret", Score = 5.40, W = Ab(3, 4, "Rifleman", 0, evoOwned: true, clock: "2:10 left") },
                new Offered { Name = "Pump-Action Shotgun", Score = 4.95, W = Gun(1, BuildStyle.Weapon, "Rifleman", 0.3, "2:10 left") });
            var m1 = WhyOf(mirror, 1, null); var m2 = WhyOf(mirror, 2, null);
            var mirrorEarly = Offer(null,
                new Offered { Name = "Electric Turret", Score = 6.00, W = Ab(3, 4, "Rifleman", 0, evoOwned: true) },
                new Offered { Name = "Pump-Action Shotgun", Score = 5.90, W = Gun(1, BuildStyle.Weapon, "Rifleman") });
            var m3 = WhyOf(mirrorEarly, 1, null);
            Console.WriteLine("      the mirror: #1 Electric Turret -> WHY  " + string.Join("  /  ", m1.Items) + "\n                  #2 Pump-Action Shotgun -> WHY  " + string.Join("  /  ", m2.Items));
            Check("W5", "(a) the mirror: the #1 ability over a weapons-first level says the clock; that weapon's WHY drops 'levels weapons first' (its own line says the clock)",
                m1.Reasons.Contains("Too late to finish Pump-Action Shotgun (2:10 left)") && !string.Join(" ", m2.Items).Contains("weapons first") && mirror[1].Shown == "Level 2 of 4 - too late to finish it (2:10 left)"
                && m3.Reasons.Contains("The Pump-Action Shotgun level ranks lower here"), string.Join(" | ", m1.Reasons) + " || " + string.Join(" | ", m3.Reasons));
            keep(m1); keep(m2); keep(m3); lines.AddRange(mirror.Select(c => c.Shown));

            // ---- (b) names whole unless the base is named on the card or in the line
            var evo = Offer(null,
                new Offered { Name = "Bombing Strike: Bioweapon", Score = 8.20, W = new CardWords { Kind = SayKind.Evolution, Base = "Bombing Strike", Adds = "Chemical", AddsNew = true } },
                new Offered { Name = "Bombing Strike: Supercharge", Score = 8.00, W = new CardWords { Kind = SayKind.Evolution, Base = "Bombing Strike", Adds = "Electric", AddsNew = true } },
                new Offered { Name = "Sawblade Drone", Score = 4.40, W = Ab(1, 4, R, 2, evoOwned: true) });
            string v3 = WhyOf(evo, 3, null).Versus, v2 = WhyOf(evo, 2, null).Versus;
            Func<string, string> mist = s => s == null ? s : s.Replace("Bombing Strike: Bioweapon", "Mist Bombs: Frost");
            var evoLent = Offer(mist, evo.Select(c => new Offered { Name = c.Name, Score = c.Score, W = c.W }).ToArray());
            string vl = WhyOf(evoLent, 3, mist).Versus;
            Console.WriteLine("      #3 Sawblade Drone: '" + v3 + "'; #2 Bombing Strike: Supercharge: '" + v2 + "'; lent '...: Frost': '" + vl + "'");
            Check("W5", "(b) the vs sentence names another card whole ('Bombing Strike: Bioweapon goes first'), its own part only on a card of the same base",
                v3 != null && v3.StartsWith("Bombing Strike: Bioweapon goes first", StringComparison.Ordinal) && v2 != null && v2.StartsWith("Bioweapon goes first", StringComparison.Ordinal)
                && vl != null && vl.StartsWith("Mist Bombs: Frost goes first", StringComparison.Ordinal), v3 + " | " + v2 + " | " + vl);
            Check("W5", "(b) ShortBeside / BaseNamed: the short form only beside its base; Short alone as before",
                Wording.ShortBeside("Bombing Strike: Bioweapon", "Bombing Strike") == "Bioweapon" && Wording.ShortBeside("Bombing Strike: Bioweapon", "Sawblade Drone") == "Bombing Strike: Bioweapon"
                && Wording.ShortBeside("Bear Trap: Fire", "Bear Trap") == "Bear Trap: Fire" && Wording.ShortBeside("Handgun", "Handgun") == "Handgun" && !Wording.BaseNamed("Falcon: Assault", null)
                && Wording.BaseNamed("Falcon: Assault", "after Falcon: Guardian and") && Wording.Short("Bombing Strike: Bioweapon") == "Bioweapon");
            string lift2 = Wording.Card(new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Lifted = true }, 2, "Helicopter Strike: Chemtrails", "Handgun");
            string lift3 = Wording.Card(new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Lifted = true }, 3, "Falcon: Guardian", "Falcon: Assault");
            string pick = Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = "Bombing Strike", Build = R, Pick = "Bombing Strike: Bioweapon", Mine = false }, 1, null, null);
            Check("W5", "(b) card lines: another card whole or the generic form (never a bare 'Chemtrails'); the second name short where the first names its base; the build's pick short on its base's card",
                lift2 == "Level 2 of 4 - next, after the top card" && lift3 == "Level 2 of 4 - after Falcon: Guardian and Assault" && pick == "The Bench Rocket build takes Bioweapon instead", lift2 + " | " + lift3 + " | " + pick);
            lines.AddRange(new[] { lift2, lift3, pick }); lines.AddRange(evo.Select(c => c.Shown));

            // ---- (c) the build's main or core ability evolving, the build picking no evolution
            Func<int, string, string, string> evolves = (p, build, special) => Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = "Experiment 21", Build = build, BasePriority = p, Special = special }, 1, null, null);
            string c0 = evolves(0, A, null), c2 = evolves(2, A, null), c3 = evolves(3, A, null), cn = evolves(-1, A, null), cs = evolves(0, A, "Ice"), cl = evolves(0, "A Build Named Twenty1", null), cb = evolves(0, null, null);
            string cp = Wording.Card(new CardWords { Kind = SayKind.Evolution, Base = "Experiment 21", Build = A, BasePriority = 0, Pick = "Experiment 21: 73", Mine = true }, 1, null, null);
            Check("W5", "(c) 'Evolves the <B> build's main ability' / 'a core ability of the <B> build' for a base the build ranks first to third and names no evolution for",
                c0 == "Evolves the Bench Anchor build's main ability" && c2 == "Evolves a core ability of the Bench Anchor build" && c3 == "Evolution - a big step up" && cn == "Evolution - a big step up"
                && cs == "Evolution - turns on the Ice 10-tag effect" && cl == "Evolves the build's main ability" && cb == "Evolution - a big step up" && cp == "The Bench Anchor build's evolution",
                string.Join(" | ", new[] { c0, c2, c3, cn, cs, cl, cb, cp }));
            var cw = WhyText.Reasons(new CardWords { Kind = SayKind.Evolution, Base = "Experiment 21", Build = A, BasePriority = 0, Special = "Ice" }, null, cs);
            Check("W5", "(c) the WHY band says it when the card's line says something stronger (a 10-tag effect)", cw.Contains("Evolves the Bench Anchor build's main ability"), string.Join(" / ", cw));
            lines.AddRange(new[] { c0, c2, c3, cn, cs, cl, cb, cp }); whys.AddRange(cw);

            // ---- (d) TAGS: a met 10-tag effect is said
            Func<int, int, Dictionary<string, int>, string> plan = (at, max, pts) => { var t = new TagProfile { SpecialAt = at }; foreach (var kv in pts) t.Points[kv.Key] = kv.Value; return t.PlanText(max); };
            var p31 = new Dictionary<string, int> { { "Kinetic", 31 } }; var p2 = new Dictionary<string, int> { { "Kinetic", 31 }, { "Electric", 22 } };
            var pm = new Dictionary<string, int> { { "Kinetic", 12 }, { "Electric", 4 } }; var pu = new Dictionary<string, int> { { "Kinetic", 8 }, { "Slashing", 4 } };
            var pt = new TagProfile { SpecialAt = 10 }; foreach (var kv in pu) pt.Points[kv.Key] = kv.Value;
            string d1 = plan(10, 1, p31), d2 = plan(10, 2, p2), d3 = plan(10, 2, pm), d4 = plan(10, 2, pu), d5 = plan(0, 2, p2), d6 = plan(10, 1, p2), d7 = plan(10, 2, new Dictionary<string, int> { { "Kinetic", 10 } });
            Check("W5", "(d) TAGS says a met 10-tag effect ('Kinetic 31 - effect on'); short of it, or no threshold known, as before",
                d1 == "Kinetic 31 - effect on" && d2 == "Kinetic 31, Electric 22 - effects on" && d3 == "Kinetic 12 - effect on, Electric 4/10" && d4 == "Kinetic 8/10, Slashing 4/10" && d4 == pt.PointsText(2)
                && d5 == "Kinetic 31, Electric 22" && d6 == "Kinetic 31 - effect on" && d7 == "Kinetic 10 - effect on", string.Join(" | ", new[] { d1, d2, d3, d4, d5, d6, d7 }));

            // ---- the sources: the ranks and rivals before the lines, the evolution's base place, the TAGS row
            string ranker = Src("Ranker.cs"), plansrc = Src("Plan.cs");
            int rk = ranker == null ? -1 : ranker.IndexOf("Wording.Ranks(order.ConvertAll(c => c.Say), order.ConvertAll(c => c.Name))", StringComparison.Ordinal);
            int disp = ranker == null ? -1 : ranker.IndexOf("c.Display = Wording.Card(c.Say, c.Rank, first, second, room, LendNames)", StringComparison.Ordinal);
            Check("W5", "the sources: SayAll ranks and pairs the rivals before the lines; ScoreEvolution fills the base's place; the readout's TAGS row asks PlanText",
                rk > 0 && disp > rk && ranker.Contains("BasePriority = build != null && !parent.Skipped ? parent.Priority : -1") && plansrc != null && plansrc.Contains("s.Tags.PlanText(Compact ? 1 : 2)"));

            // ---- the rails over every new form: the deciders for every ability as the rival, the mirror for every weapon, the evolution
            // heads for every build, in the game's names and as drawn (every name 19 characters longer, the room an AVOID card leaves)
            var builds = Builds.Where(x => x != null).ToList();
            foreach (var n in names)
                foreach (var build in builds)
                    foreach (int p in new[] { -1, 1, 3, 4 })
                        foreach (bool skipped in new[] { false, true })
                        {
                            var rival = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Build = build, Priority = skipped ? -1 : p, HeadRank = skipped ? 6 : 0, Head = skipped ? Wording.Skips(build) : null, Owned = 2 };
                            var o = Offer(null, new Offered { Name = "Handgun", Score = 4.0, W = Gun(1, BuildStyle.Ability, build) }, new Offered { Name = n, Score = 3.0, W = rival });
                            whys.AddRange(WhyOf(o, 1, null).Reasons);
                        }
            foreach (var wn in weaponNames)
                foreach (double reach in new[] { 1.0, 0.3 })
                {
                    var o = Offer(null, new Offered { Name = "Electric Turret", Score = 5.0, W = Ab(3, 4, "Rifleman", 0, evoOwned: true, clock: "12:05 left") }, new Offered { Name = wn, Score = 4.9, W = Gun(1, BuildStyle.Weapon, "Rifleman", reach, "12:05 left") });
                    whys.AddRange(WhyOf(o, 1, null).Reasons);
                }
            foreach (var build in builds) for (int p = -1; p <= 4; p++) foreach (var special in new[] { null, "Kinetic" }) lines.Add(evolves(p, build, special));
            var railBad = new List<string>();
            foreach (var l in lines.Where(x => x != null).Distinct()) { var off = Wording.Rails(l); if (off.Count > 0) railBad.Add("'" + l + "' (" + string.Join(", ", off) + ")"); }
            foreach (var r in whys.Where(x => x != null).Distinct()) if (!WhyText.Fits(r)) railBad.Add("WHY '" + r + "'");
            foreach (var r in versus.Where(x => x != null).Distinct()) if (!WhyText.Fits(r, WhyText.VersusBudget)) railBad.Add("vs #1 '" + r + "'");
            // the deciders keep the card line's 50, as drawn too (the band swaps the lent names in)
            var lender = Lender(names.Concat(weaponNames).Concat(items.Select(i => i.Name)).Concat(new[] { "Handgun", "Resuscitation", "Stimpack" }));
            int drawnBad = 0; string drawnLongest = "";
            foreach (var n in names.Take(400))
                foreach (var build in builds)
                {
                    var o = Offer(lender, new Offered { Name = "Handgun", Score = 4.0, W = Gun(1, BuildStyle.Ability, build) }, new Offered { Name = n, Score = 3.0, W = new CardWords { Kind = SayKind.Ability, Max = 4, Build = build, Priority = 3, Owned = 2 } });
                    foreach (var r in WhyOf(o, 1, lender).Reasons.Where(x => x.Contains("4th") || x.Contains("rank lower")))
                    {
                        string drawn = Wording.Safe(lender(r));
                        if (drawn.Length > Wording.Budget || Wording.Rails(drawn).Count > 0) drawnBad++;
                        else if (drawn.Length > drawnLongest.Length) drawnLongest = drawn;
                    }
                    foreach (var c in o) if (c.Shown != null && Wording.Rails(Wording.Safe(lender(c.Shown))).Count > 0) drawnBad++;
                }
            Check("W5", "every new form within the rails - card lines <= 50, WHY items within the band's, the deciders <= 50 as drawn with names 19 characters longer ("
                + lines.Distinct().Count() + " lines, " + whys.Distinct().Count() + " WHY items)", railBad.Count == 0 && drawnBad == 0,
                railBad.Count == 0 && drawnBad == 0 ? "the longest decider as drawn: '" + drawnLongest + "' (" + drawnLongest.Length + ")" : railBad.Count + " off, " + drawnBad + " as drawn: " + string.Join(" || ", railBad.Take(12)));
        }
    }
}
