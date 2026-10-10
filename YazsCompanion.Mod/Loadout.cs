// Badge advice for the run setup screen ("SELECT LOADOUT", 0.13.0): which unlocked badges to equip for the team leader,
// the build, the mode, the difficulty and the doctrine the player picked - in priority order, with the reason.
//
// The currency is POINTS = "% more squad damage over the whole run, or its equivalent". A badge bonus is worth
//   value per level x level x w[stat] x relevance(stat, run) x axis(stat, run)      (knowledge.json "badgeStats")
// summed over its bonuses, plus its tag points (1.5 x the type's share per point) and a one-off SPECIAL pull when its
// points take the build's projected count of a type over the special threshold. A badge works from second 0 to the end
// of a run, so the run curves (economy, survival) are AVERAGED over the whole run, and cash counts by [Advice] RunGoal.
//
// The fill is greedy - forced (quest) badges first, then the build's pinned badges, then the best score each round, the
// special pull re-evaluated against the tag points already taken - so the advice for k slots is always the first k of
// the advice for more slots. Ties: score in whole centi-points, then the higher level, then badgeSortOrder, then
// badgeBaseId. Nothing here depends on dictionary order, input order or randomness.
//
// Pure C# (no game types): LoadoutState.cs fills the facts and the input from the live game, the offline bench
// (tools\ItemBench\LoadoutCases.cs) from the 1.0.2 catalogue - and checks this file line for line against the Python
// reference model of research\badges_1004\tools\value_model.py. Badge ids are badgeBaseId (the save's RunSetup_BadgeN).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YazsCompanion
{
    internal enum BadgeKind { Unknown, Stat, Tag, Physical, Elemental }

    /// <summary>One bonus of a badge: <see cref="Stat"/> = the PlayerStatistic asset name minus Team_ / GamePlayer_ / Internal_
    /// (the knowledge key), <see cref="Type"/> = the game's EType name (log only), the value per badge level.</summary>
    internal sealed class BadgeBonus
    {
        public string Stat = "", Type = "", Template = "";
        public double PerLevel;
    }

    /// <summary>What a badge does, read once per session (live) or from the fixture (bench). Levels are per visit and live in
    /// <see cref="LoadoutInput.Levels"/>.</summary>
    internal sealed class BadgeFacts
    {
        public int Id, Sort, Rank, Max = 5;            // Id = badgeBaseId, Sort = badgeSortOrder, Rank = the class rank of its tree node (asset digit)
        public string Asset = "", Short = "", Name = "", Owner = "", KindName = "";
        public BadgeKind Kind;
        public readonly List<BadgeBonus> Bonuses = new List<BadgeBonus>();
        public readonly List<string> TagTypes = new List<string>();       // TagProfile.Names spelling (Toxic -> Chemical)
        public int[] TagPoints = new int[0];                              // index = badge level
        public object Ref;                                                // live only: the game object

        /// <summary>Tag points per listed type at a level (the table is clamped at its last entry).</summary>
        public int PointsAt(int level)
        {
            if (TagPoints == null || TagPoints.Length == 0) return 0;
            return TagPoints[Math.Max(0, Math.Min(level, TagPoints.Length - 1))];
        }
        public override string ToString() { return Id + " " + Short; }
    }

    internal sealed class BadgeTerm
    {
        public double Points;
        public string Kind = "", Text = "", Stat = "";     // Kind: "type:Kinetic", "points:Kinetic", "special:Fire", "early:Fire", "survival", "weapon", ...
        public override string ToString() { return Text + " " + Loadout.Fixed(Points, 2); }
    }

    /// <summary>What the run will deal ("as the run will look"): the leader's build (and, with badgeRules.recruitWeight above 0,
    /// the likely recruits), its weapon / ability split, its leans and the tag points it reaches on its own.</summary>
    internal sealed class RunShape
    {
        public readonly Dictionary<string, double> Weight = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, List<string>> Sources = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, double> SourceWeight = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);   // "type|source"
        public double Total, WeaponW, AbilityW, DeployW, AbilityDmgW;
        public readonly Dictionary<string, int> Projected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> ProjectedOrder = new List<string>();
        public double Crit = 0.3, Heal = 0.3, Clip = 0.5, Deploy;
        public bool BuildSelected;
        // the build is one another mod lends and Auto follows, not one the player chose (0.13.0, F02's wording): the reasons
        // name it "Rifleman (Auto)", never "your Rifleman build". Set by the live reader; scores do not depend on it.
        public bool Auto;
        public string Lead = "", BuildName = "Auto";
        public readonly List<string> Wants = new List<string>();
        public readonly List<string> Recruits = new List<string>();

        /// <summary>One damage source with all the types it deals: counted once in <see cref="Total"/> (TagProfile semantics).</summary>
        public void Add(string source, double w, IEnumerable<string> types)
        {
            var list = types == null ? new List<string>() : types.Where(t => !string.IsNullOrEmpty(t)).ToList();
            if (list.Count == 0 || w <= 0) return;
            foreach (var t in list)
            {
                double x; Weight.TryGetValue(t, out x); Weight[t] = x + w;
                List<string> l; if (!Sources.TryGetValue(t, out l)) Sources[t] = l = new List<string>();
                if (!l.Contains(source)) l.Add(source);
                string key = t + "|" + source; double y; SourceWeight.TryGetValue(key, out y); SourceWeight[key] = y + w;
            }
            Total += w;
        }

        public void SetProjected(string type, int n)
        {
            if (!Projected.ContainsKey(type)) ProjectedOrder.Add(type);
            Projected[type] = n;
        }

        public double Share(string t) { double w; return Total > 0 && Weight.TryGetValue(t ?? "", out w) ? Math.Min(1.0, w / Total) : 0.0; }
        public double Fit(string t) { return Math.Min(1.0, Share(t) * 1.6); }
        public double WeaponShare { get { double s = WeaponW + AbilityW; return s > 0 ? WeaponW / s : 0.5; } }
        public double AbilityShare { get { return 1 - WeaponShare; } }

        /// <summary>The type worth stacking: a forced TagPlan type, none with "Spread", else the type with the best fit.</summary>
        public string Focus(string plan)
        {
            if (string.Equals(plan, "Spread", StringComparison.OrdinalIgnoreCase)) return null;
            foreach (var n in TagProfile.Names) if (string.Equals(n, plan, StringComparison.OrdinalIgnoreCase)) return n;
            string best = null; double key = -1;
            foreach (var t in TagProfile.Names) { double f = Fit(t); if (f > 0 && f > key) { best = t; key = f; } }
            return best;
        }

        /// <summary>The sources of a type, biggest first: "Assault Rifle, SMG" or "Assault Rifle, SMG +2".</summary>
        public string SourceText(string t)
        {
            List<string> l; if (!Sources.TryGetValue(t, out l) || l.Count == 0) return "";
            var sorted = l.Select((s, i) => new { s, i, w = SourceW(t, s) }).OrderByDescending(x => x.w).ThenBy(x => x.i).Select(x => x.s).ToList();
            return sorted.Count <= 2 ? string.Join(", ", sorted) : sorted[0] + ", " + sorted[1] + " +" + (sorted.Count - 2);
        }
        double SourceW(string t, string s) { double w; return SourceWeight.TryGetValue(t + "|" + s, out w) ? w : 0; }

        /// <summary>The types dealt, most first (ties: TagProfile order): (type, share).</summary>
        public List<KeyValuePair<string, double>> Dealt()
        {
            return TagProfile.Names.Select((t, i) => new { t, i, s = Share(t) }).Where(x => x.s > 0).OrderByDescending(x => x.s).ThenBy(x => x.i)
                .Select(x => new KeyValuePair<string, double>(x.t, x.s)).ToList();
        }

        /// <summary>The projected tag points, most first (ties: the order they were first projected).</summary>
        public List<KeyValuePair<string, int>> ProjectedList()
        {
            return ProjectedOrder.Select((t, i) => new { t, i, n = Projected[t] }).OrderByDescending(x => x.n).ThenBy(x => x.i)
                .Select(x => new KeyValuePair<string, int>(x.t, x.n)).ToList();
        }

        /// <summary>The same damage profile as the run ranker's TagProfile (shares and sources; no points).</summary>
        public TagProfile ToProfile(string plan = "Auto")
        {
            var p = new TagProfile { Plan = plan ?? "Auto" };
            foreach (var kv in Weight) p.Weight[kv.Key] = kv.Value;
            p.Total = Total;
            foreach (var kv in Sources) p.Sources[kv.Key] = new List<string>(kv.Value);
            return p;
        }

        static readonly double[] AbilityWeights = { 0.5, 0.35, 0.25, 0.15 };
        static double StyleWeapon(BuildStyle s) { return s == BuildStyle.Weapon ? 0.65 : s == BuildStyle.Ability ? 0.35 : 0.5; }

        /// <summary>The run shape of a leader's build (null = Auto: the guides' weapon branch from knowledge.json, else the three
        /// tier-3 weapons alike, the kit's ability order, <paramref name="autoStyle"/> = [Advice] LevelUpStyle) plus, when
        /// <paramref name="recruitWeight"/> is above 0, each recruit at that weight with its own build.
        /// <paramref name="levelUps"/> = the level-ups a run of this mode is expected to bring (projected tag points).</summary>
        public static RunShape Of(string leader, Build build, IList<KeyValuePair<string, Build>> recruits, double recruitWeight, double levelUps,
            Func<string, PowerFacts> facts, Knowledge k, BuildStyle autoStyle, Func<string, Kit> kitOf = null)
        {
            kitOf = kitOf ?? Builds.KitOf;
            k = k ?? Knowledge.Current;
            var s = new RunShape { Lead = leader ?? "", BuildSelected = build != null, BuildName = build != null ? (build.Name ?? "") : "Auto" };
            if (build != null) s.Wants.AddRange(build.Wants);
            Part(s, leader, build, 1.0, true, levelUps, facts, k, autoStyle, kitOf);
            if (recruitWeight > 0 && recruits != null)
                foreach (var r in recruits)
                {
                    if (string.IsNullOrEmpty(r.Key)) continue;
                    s.Recruits.Add(r.Key);
                    Part(s, r.Key, r.Value, recruitWeight, false, levelUps, facts, k, autoStyle, kitOf);
                }
            bool wantsCrit = s.Wants.Any(w => string.Equals(w, "critical", StringComparison.OrdinalIgnoreCase));
            bool wantsHeal = s.Wants.Any(w => string.Equals(w, "healing", StringComparison.OrdinalIgnoreCase));
            s.Crit = wantsCrit ? 1.0 : k.CritSquad.Any(c => string.Equals(c, leader, StringComparison.OrdinalIgnoreCase)) ? 0.6 : 0.3;
            s.Heal = wantsHeal ? 1.0 : (string.Equals(leader, "Medic", StringComparison.OrdinalIgnoreCase) || s.Recruits.Any(r => string.Equals(r, "Medic", StringComparison.OrdinalIgnoreCase))) ? 0.6 : 0.3;
            s.Deploy = s.AbilityDmgW > 0 ? s.DeployW / s.AbilityDmgW : 0.0;
            return s;
        }

        static List<string> Dmg(Func<string, PowerFacts> facts, string name)
        {
            var f = facts == null || string.IsNullOrEmpty(name) ? null : facts(name);
            return f == null ? new List<string>() : f.Damage.ToList();
        }
        static bool Tagged(Func<string, PowerFacts> facts, string name, string tag)
        {
            var f = facts == null || string.IsNullOrEmpty(name) ? null : facts(name);
            return f != null && f.Tags.Contains(tag);
        }

        static void Part(RunShape s, string survivor, Build build, double scale, bool leader, double levelUps, Func<string, PowerFacts> facts, Knowledge k, BuildStyle autoStyle, Func<string, Kit> kitOf)
        {
            var kit = kitOf(survivor);
            if (kit == null || kit.Line == null || kit.Line.Length < 2) return;
            string branch = "";
            if (build != null) branch = build.Branch ?? "";
            else { string g; if (k.WeaponBranch.TryGetValue(survivor ?? "", out g)) branch = g ?? ""; }
            var line = kit.Line;
            var wsrc = new List<KeyValuePair<string, double>> { new KeyValuePair<string, double>(line[0], 0.15), new KeyValuePair<string, double>(line[1], 0.25) };
            if (branch.Length > 0) wsrc.Add(new KeyValuePair<string, double>(branch, 0.60));
            else for (int i = 2; i < Math.Min(5, line.Length); i++) wsrc.Add(new KeyValuePair<string, double>(line[i], 0.20));
            foreach (var w in wsrc) s.Add(w.Key, w.Value * scale, Dmg(facts, w.Key));

            var order = build != null && build.Abilities.Count > 0 ? build.Abilities.ToList() : kit.Abilities.Select(a => a[0]).ToList();
            Func<string, bool> skipped = a => build != null && build.Skips(a);
            int pos = 0;
            foreach (var a in order)
            {
                if (skipped(a)) continue;
                if (pos >= AbilityWeights.Length) break;
                double w = AbilityWeights[pos] * scale; pos++;
                var d = Dmg(facts, a);
                string e = build != null ? build.EvolutionOf(a) : null;
                var de = e != null ? Dmg(facts, e) : new List<string>();
                if (e != null) { s.Add(a, w * 0.5, d); s.Add(e, w * 0.5, de); }
                else s.Add(a, w, d);
                if (d.Count > 0 || (e != null && de.Count > 0))
                {
                    s.AbilityDmgW += w;
                    if (Tagged(facts, a, "Deployable") || Tagged(facts, a, "Turret")) s.DeployW += w;
                }
            }
            double ws = StyleWeapon(build != null ? build.Style : autoStyle);
            s.WeaponW += ws * scale; s.AbilityW += (1 - ws) * scale;
            if (!leader) return;
            // the tag points the leader's build reaches on its own: 4 per weapon tier and per top-3 ability, +1 per evolution
            double f = Math.Min(1.0, levelUps / 30.0);
            var proj = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase); var projOrder = new List<string>();
            Action<string, int> addp = (name, n) =>
            {
                foreach (var t in Dmg(facts, name)) { double v; if (!proj.TryGetValue(t, out v)) projOrder.Add(t); proj[t] = v + n; }
            };
            addp(line[0], 4); addp(line[1], 4);
            if (branch.Length > 0) addp(branch, 4);
            foreach (var a in order.Where(x => !skipped(x)).Take(3))
            {
                addp(a, 4);
                string e = build != null ? build.EvolutionOf(a) : null;
                if (e != null) addp(e, 1);
            }
            foreach (var t in projOrder) s.SetProjected(t, (int)Math.Floor(proj[t] * f));
        }
    }

    /// <summary>The run, averaged over its whole length (a badge works from second 0 to the end), plus the mode's facts.</summary>
    internal sealed class LoadoutCtx
    {
        public string Mode = "Normal";
        public int Difficulty = 1;
        public ModeKind Kind = ModeKind.Timed;
        public bool Aware = true;
        public double Economy = 1, Survival = 1, Control = 1, Cash = 0.3, Elite = 0.15, Boss = 0.06, LevelUps = 50;
        public readonly HashSet<string> Zero = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public int Farming, Caution = 1, Timing = 1, SpecialAt = 10;
        public string TagPlan = "Auto", Note = "";

        public double Axis(string axis)
        {
            switch (axis)
            {
                case null: case "": return 1.0;
                case "economy": return Economy;
                case "cash": return Cash;
                case "survival": return Math.Max(0.0, Survival);
                case "dodge": case "move": return Math.Max(Control, Math.Min(1.3, Survival));
                default: return 1.0;           // consistency, unknown
            }
        }

        public string Text()
        {
            return Mode + " d" + Difficulty + " (economy x" + Loadout.Fixed(Economy, 2) + ", survival x" + Loadout.Fixed(Survival, 2) + ", cash x" + Loadout.Fixed(Cash, 2)
                + ", elites " + Loadout.Fixed(Elite * 100, 0) + "% / bosses " + Loadout.Fixed(Boss * 100, 0) + "% of damage, ~" + ((int)LevelUps) + " level-ups)";
        }
    }

    /// <summary>A build as the badge advice sees it (the replayable part of <see cref="Build"/>).</summary>
    internal sealed class LoadoutBuild
    {
        public string Id = "", Name = "", Pack = "", Branch = "", Style = "";
        public readonly List<string> Wants = new List<string>();
        public readonly List<string> Badges = new List<string>();       // pins, in order: id, short name, "X Badge" or asset name
        public readonly List<string> Skip = new List<string>();         // never

        public static LoadoutBuild From(Build b)
        {
            if (b == null) return null;
            var l = new LoadoutBuild { Id = b.Id ?? "", Name = b.Name ?? "", Pack = b.Pack ?? "", Branch = b.Branch ?? "", Style = b.Style.ToString() };
            l.Wants.AddRange(b.Wants); l.Badges.AddRange(b.Badges); l.Skip.AddRange(b.SkipBadges);
            return l;
        }
    }

    /// <summary>Everything one advice depends on - serializable (the `[loadout] input #N {json}` line), so a logged advice can be
    /// replayed offline without the game, the kits or the probe.</summary>
    internal sealed class LoadoutInput
    {
        public int Version = Loadout.InputVersion;
        public string Leader = "", Mode = "Normal";
        public int Difficulty = 1;
        public LoadoutBuild Build;                                  // null = Auto
        public int Farming, Caution = 1, Timing = 1;
        public bool ModeAware = true;
        public string TagPlan = "Auto", Style = "Balanced";
        public int Slots = 4;
        public readonly List<int> Forced = new List<int>();        // in the game's _forcedBadges order
        public readonly List<int> Equipped = new List<int>();      // in the game's _selectedBadges order
        public readonly Dictionary<int, int> Levels = new Dictionary<int, int>();          // missing = 0 (locked)
        public readonly Dictionary<int, bool> RankOpen = new Dictionary<int, bool>();      // missing = open (the UNLOCK hint may name it)
        public readonly HashSet<int> OffGrid = new HashSet<int>();                          // registry badges without a grid button: never advised
        public readonly List<string> Recruits = new List<string>();
        public RunShape Shape = new RunShape();
        public LoadoutCtx Ctx = new LoadoutCtx();
        public string Inventory = "", KnowledgeHash = "";

        public int LevelOf(int id) { int l; return Levels.TryGetValue(id, out l) ? l : 0; }
        public bool Open(int id) { bool o; return !RankOpen.TryGetValue(id, out o) || o; }
    }

    internal sealed class LoadoutRow
    {
        public BadgeFacts Badge;
        public int Level;
        public double Score, Per;                                   // Score: at max(level, 1) against the build's own projection; Per: at level 1, no projection
        public List<BadgeTerm> Terms = new List<BadgeTerm>();
        public bool Rated, OnGrid = true, Open = true, Skipped, Estimated;
        public bool Unlocked { get { return Level >= 1; } }
    }

    internal sealed class LoadoutPick
    {
        public BadgeFacts Badge;
        public int Level, Rank;                                     // Rank 0 = forced, else 1..n
        public double Score;
        public string Source = "score";                             // forced | pin | score
        public bool Close;                                          // a score pick whose lead over the best badge left out is under badgeRules.swapMargin
        public BadgeFacts CloseTo;
        public string Effect = "", Why = "", Why2 = "";
        public List<BadgeTerm> Terms = new List<BadgeTerm>();
        public bool Forced { get { return Source == "forced"; } }
    }

    internal sealed class LoadoutLevelHint
    {
        public BadgeFacts Badge;
        public int From, To, Cost;
        public double Gain, PerPoint;
    }

    internal sealed class LoadoutUnlockHint
    {
        public LoadoutRow Row;
        public bool Pinned;                                         // "pinned but locked"
        public int WouldBe;                                         // the number it would take at level 1
    }

    internal sealed class LoadoutAdvice
    {
        public int Number;
        public LoadoutInput Input;
        public readonly List<LoadoutPick> Picks = new List<LoadoutPick>();          // forced first (Rank 0), then pins and score picks 1..n
        public readonly List<LoadoutRow> Rows = new List<LoadoutRow>();             // every badge, in badgeSortOrder
        public readonly List<LoadoutRow> All = new List<LoadoutRow>();              // every badge, best first (locked ones by their level-1 score)
        public readonly List<LoadoutRow> Next = new List<LoadoutRow>();             // the top 2 unlocked rated badges left out
        public readonly List<LoadoutRow> Never = new List<LoadoutRow>();            // unlocked badges worth (about) 0
        public readonly List<LoadoutRow> Unrated = new List<LoadoutRow>();
        public readonly List<LoadoutUnlockHint> Unlocks = new List<LoadoutUnlockHint>();
        public LoadoutLevelHint Level;
        public readonly List<string> Notes = new List<string>();
        public readonly List<string> Estimated = new List<string>();               // "33 Sprint: Foo" - stats scored at the default
        public LoadoutUnlockHint Unlock { get { return Unlocks.Count > 0 ? Unlocks[0] : null; } }
        public string Note { get { return Notes.Count > 0 ? Notes[0] : ""; } }
        public List<int> Equip { get { return Picks.Select(p => p.Badge.Id).ToList(); } }
        public LoadoutRow RowOf(int id) { return Rows.FirstOrDefault(r => r.Badge.Id == id); }
        public LoadoutPick PickOf(int id) { return Picks.FirstOrDefault(p => p.Badge.Id == id); }
    }

    internal static class Loadout
    {
        public const int InputVersion = 1;
        /// <summary>The build the bench fixture was made from.</summary>
        public const string ReferenceGame = "1.0.2";
        /// <summary>SHA-256 of the canonical text of the 27 badges of 1.0.2 (<see cref="Hash"/>); the bench fails when the fixture
        /// drifts from it, the game logs whether the live registry equals it.</summary>
        public const string ReferenceHash = "237c6a7f05a0c3f630c3082b0d66c5f3a93c20c472c92b6984416d6a07cb34aa";
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------------------------------------ names and ids
        static readonly Regex AssetRx = new Regex(@"^Badge_[A-Za-z]+(\d)_(.+?)(Badge)?$");

        /// <summary>"Badge_SWAT3_Gunner" -> "Gunner", "Badge_SWAT4_CriticalBadge" -> "Critical"; anything else stays as it is.</summary>
        public static string ShortName(string asset)
        {
            var m = AssetRx.Match(asset ?? "");
            return m.Success ? m.Groups[2].Value : (asset ?? "");
        }
        /// <summary>The rank digit of the asset name, 0 when the pattern does not hold.</summary>
        public static int RankOf(string asset) { var m = AssetRx.Match(asset ?? ""); return m.Success ? m.Groups[1].Value[0] - '0' : 0; }

        /// <summary>The PlayerStatistic asset name without its Team_ / GamePlayer_ / Internal_ prefix (one prefix only).</summary>
        public static string StatKey(string asset)
        {
            foreach (var p in new[] { "GamePlayer_", "Team_", "Internal_" })
                if ((asset ?? "").StartsWith(p, StringComparison.Ordinal)) return asset.Substring(p.Length);
            return asset ?? "";
        }

        /// <summary>The game's tag type name in TagProfile spelling (Toxic -> Chemical).</summary>
        public static string TypeName(string t)
        {
            if (string.Equals(t, "Toxic", StringComparison.OrdinalIgnoreCase)) return "Chemical";
            foreach (var n in TagProfile.Names) if (string.Equals(n, t, StringComparison.OrdinalIgnoreCase)) return n;
            return t ?? "";
        }

        static readonly string[] ElementalOrder = { "Ice", "Fire", "Electric", "Chemical" };      // the game adds the points to types 4, 1, 2, 3
        static readonly string[] PhysicalOrder = { "Kinetic", "Slashing", "Explosive" };           // types 6, 7, 5

        /// <summary>The tag types of a badge from its bonus stats ("HashtagFire" ...) in the game's own order (Elemental: Ice, Fire,
        /// Electric, Chemical; Physical: Kinetic, Slashing, Explosive; a single-type badge: its "type" field) - the live reader's
        /// helper, so live facts read like the fixture's (the hash ignores the order anyway).</summary>
        public static List<string> TagTypesOf(BadgeKind kind, IEnumerable<string> statKeys, string singleType = null)
        {
            if (kind == BadgeKind.Tag && !string.IsNullOrEmpty(singleType)) return new List<string> { TypeName(singleType) };
            var found = new List<string>();
            foreach (var s in statKeys ?? Enumerable.Empty<string>())
                if ((s ?? "").StartsWith("Hashtag", StringComparison.Ordinal)) { string t = TypeName(s.Substring(7)); if (!found.Contains(t)) found.Add(t); }
            var order = kind == BadgeKind.Elemental ? ElementalOrder : kind == BadgeKind.Physical ? PhysicalOrder : null;
            if (order == null) return found;
            return order.Where(found.Contains).Concat(found.Where(t => !order.Contains(t))).ToList();
        }

        /// <summary>The badges sorted by badgeSortOrder (then id), each id once: a duplicate keeps the lower sort order and is said
        /// through <paramref name="warn"/>.</summary>
        public static List<BadgeFacts> Clean(IEnumerable<BadgeFacts> all, Action<string> warn = null)
        {
            var list = (all ?? Enumerable.Empty<BadgeFacts>()).Where(b => b != null).OrderBy(b => b.Sort).ThenBy(b => b.Id).ToList();
            var seen = new HashSet<int>(); var keep = new List<BadgeFacts>();
            foreach (var b in list)
            {
                if (seen.Add(b.Id)) { keep.Add(b); continue; }
                if (warn != null) warn("badge id " + b.Id + " twice (" + keep.First(x => x.Id == b.Id).Asset + ", " + b.Asset + "): the one with the lower sort order kept");
            }
            return keep;
        }

        /// <summary>A badge reference from a build, a pack or knowledge.json: an id ("16"), the short name ("Gunner"), the English name
        /// ("Gunner Badge", any case) or the asset name ("Badge_SWAT3_Gunner"). Never the save's legacy node keys. null = none.</summary>
        public static BadgeFacts Resolve(string reference, IList<BadgeFacts> all, out string how)
        {
            how = "";
            string r = (reference ?? "").Trim();
            if (r.Length == 0 || all == null) return null;
            int id;
            if (int.TryParse(r, NumberStyles.Integer, IC, out id)) { var b = all.FirstOrDefault(x => x.Id == id); if (b != null) how = "id"; return b; }
            var hit = all.FirstOrDefault(x => string.Equals(x.Asset, r, StringComparison.OrdinalIgnoreCase));
            if (hit != null) { how = "asset name"; return hit; }
            hit = all.FirstOrDefault(x => string.Equals(x.Short, r, StringComparison.OrdinalIgnoreCase));
            if (hit != null) { how = "short name"; return hit; }
            string bare = Regex.Replace(r, @"\s*badge$", "", RegexOptions.IgnoreCase).Trim();
            hit = all.FirstOrDefault(x => string.Equals(x.Short, bare, StringComparison.OrdinalIgnoreCase));
            if (hit != null) { how = "name"; return hit; }
            hit = all.FirstOrDefault(x => string.Equals(x.Name, r, StringComparison.OrdinalIgnoreCase));
            if (hit != null) { how = "name"; return hit; }
            return null;
        }

        /// <summary>The knowledge.json "badges" entry of a badge: by id, asset name or short name.</summary>
        public static BadgeNote NoteFor(BadgeFacts b, Knowledge k)
        {
            if (b == null || k == null || k.BadgeNotes.Count == 0) return null;
            BadgeNote n;
            if (k.BadgeNotes.TryGetValue(b.Id.ToString(IC), out n)) return n;
            if (k.BadgeNotes.TryGetValue(b.Asset ?? "", out n)) return n;
            if (k.BadgeNotes.TryGetValue(b.Short ?? "", out n)) return n;
            return null;
        }

        /// <summary>knowledge.json "badges" entries that match no badge (a renamed asset, a typo): each said once by the caller.</summary>
        public static List<string> KnowledgeWarnings(IList<BadgeFacts> all, Knowledge k)
        {
            var o = new List<string>();
            if (k == null) return o;
            foreach (var key in k.BadgeNotes.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                bool hit = all.Any(b => b.Id.ToString(IC) == key || string.Equals(b.Asset, key, StringComparison.OrdinalIgnoreCase) || string.Equals(b.Short, key, StringComparison.OrdinalIgnoreCase));
                if (!hit) o.Add("knowledge.json badges entry '" + key + "' matches no badge - not applied");
            }
            return o;
        }

        /// <summary>A badge the model can rate: a known class and at least one stat it has a weight for (or a tag table).</summary>
        public static bool Rated(BadgeFacts b, Knowledge k)
        {
            if (b == null || b.Kind == BadgeKind.Unknown) return false;
            if (b.TagTypes.Count > 0) return true;
            return b.Bonuses.Any(x => (x.Stat ?? "").StartsWith("Hashtag", StringComparison.Ordinal) || k.BadgeStats.ContainsKey(x.Stat ?? ""));
        }
        /// <summary>The stats of a rated badge that knowledge.json has no weight for (scored at badgeRules.unknownStatPerLevel).</summary>
        public static List<string> UnknownStats(BadgeFacts b, Knowledge k)
        {
            return b.Bonuses.Select(x => x.Stat ?? "").Where(s => !s.StartsWith("Hashtag", StringComparison.Ordinal) && !k.BadgeStats.ContainsKey(s)).Distinct().ToList();
        }

        // ------------------------------------------------------------------------------------------------ numbers
        /// <summary>Whole centi-points, half to even (the prototype's round(s * 100)): what every comparison works on.</summary>
        public static long Centi(double x) { return (long)Math.Round(x * 100, MidpointRounding.ToEven); }

        /// <summary>x with <paramref name="dec"/> decimals, rounded as Python's "%.Nf" does: the exact binary value, an exact half to
        /// even (.NET's "F2" takes an exact half away from zero: 2.125 -> "2.13", Python "2.12"). Log lines and reasons use it, so
        /// they read exactly as the reference prototype prints them.</summary>
        public static string Fixed(double x, int dec)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || dec < 0) return x.ToString(IC);
            long bits = BitConverter.DoubleToInt64Bits(x);
            bool neg = bits < 0;
            int exp = (int)((bits >> 52) & 0x7FF);
            long man = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0) exp = 1; else man |= 1L << 52;
            exp -= 1075;                                                   // |x| = man * 2^exp
            if (man != 0 && exp < 0)
            {
                var num = new BigInteger(man) * 2 * BigInteger.Pow(10, dec);
                var den = BigInteger.One << -exp;
                if ((num % den).IsZero)
                {
                    var q2 = num / den;                                    // 2 * |x| * 10^dec, an integer
                    if (!q2.IsEven)                                        // an exact half
                    {
                        var q = (q2 - 1) / 2;
                        if (!q.IsEven) q += 1;
                        string digits = q.ToString(IC).PadLeft(dec + 1, '0');
                        string txt = dec == 0 ? digits : digits.Substring(0, digits.Length - dec) + "." + digits.Substring(digits.Length - dec);
                        return (neg ? "-" : "") + txt;
                    }
                }
            }
            return x.ToString("F" + dec, IC);
        }
        /// <summary>Python's round(x, dec): the exact value, halves to even.</summary>
        public static double RoundPy(double x, int dec) { return double.Parse(Fixed(x, dec), NumberStyles.Float, IC); }
        public static double R2(double x) { return Centi(x) / 100.0; }

        /// <summary>The advice order: score (centi-points) desc, level desc, badgeSortOrder asc, badgeBaseId asc.</summary>
        public static int Order(double scoreA, int levelA, BadgeFacts a, double scoreB, int levelB, BadgeFacts b)
        {
            long ca = Centi(scoreA), cb = Centi(scoreB);
            if (ca != cb) return cb.CompareTo(ca);
            if (levelA != levelB) return levelB.CompareTo(levelA);
            if (a.Sort != b.Sort) return a.Sort.CompareTo(b.Sort);
            return a.Id.CompareTo(b.Id);
        }
        static int Order(LoadoutRow a, LoadoutRow b) { return Order(a.Score, a.Level, a.Badge, b.Score, b.Level, b.Badge); }

        /// <summary>Python 3.12+'s sum() of floats (Neumaier's compensated summation, started from the first item): the reference
        /// prototype adds a badge's terms this way, and ties are judged on centi-points, so the last bits matter.</summary>
        public static double Sum(IList<double> xs)
        {
            if (xs == null || xs.Count == 0) return 0;
            double f = xs[0], c = 0;
            for (int i = 1; i < xs.Count; i++)
            {
                double x = xs[i], t = f + x;
                if (Math.Abs(f) >= Math.Abs(x)) c += (f - t) + x; else c += (x - t) + f;
                f = t;
            }
            if (c != 0 && !double.IsInfinity(c) && !double.IsNaN(c)) f += c;
            return f;
        }

        // ------------------------------------------------------------------------------------------------ the run
        /// <summary>A RunContext for the run setup screen: the mode's goal from knowledge.json (an unknown mode: open-ended).</summary>
        public static RunContext ContextFor(string mode, int difficulty, Doctrine d, Knowledge k)
        {
            k = k ?? Knowledge.Current;
            BadgeMode m; k.BadgeModes.TryGetValue(mode ?? "", out m);
            return new RunContext { Mode = mode ?? "Normal", Difficulty = Math.Max(1, difficulty), Goal = m != null ? m.Goal : 0, D = d ?? new Doctrine() };
        }

        /// <summary>The run curves averaged over the whole run (21 samples of RunContext's own economy / survival curves), cash by
        /// [Advice] RunGoal (badgeRules.cashByRunGoal, unfaded: a badge earns over the whole run), the mode's elite / boss shares
        /// and zero list (badgeModes; only with ModeAware) and the level-ups the run is expected to bring.</summary>
        public static LoadoutCtx Averaged(RunContext rc, Knowledge k)
        {
            k = k ?? Knowledge.Current;
            var d = rc.D ?? new Doctrine();
            var c = new LoadoutCtx { Mode = rc.Mode ?? "Normal", Difficulty = Math.Max(1, rc.Difficulty), Aware = d.ModeAware, Farming = d.Farming, Caution = d.Caution, Timing = d.Timing, TagPlan = d.TagPlan ?? "Auto", SpecialAt = k.BadgeRules.SpecialAt };
            BadgeMode m; bool known = k.BadgeModes.TryGetValue(c.Mode, out m);
            c.Kind = c.Aware ? rc.Kind : ModeKind.Timed;
            double span = rc.Goal > 0 ? rc.Goal : 1200.0;
            var econ = new List<double>(); var surv = new List<double>();
            for (int i = 0; i <= 20; i++)
            {
                var sample = new RunContext { Mode = rc.Mode, Difficulty = rc.Difficulty, Goal = rc.Goal, Seconds = (i / 20.0) * span, Health = 1, D = d };
                econ.Add(sample.Economy); surv.Add(sample.Survival);
            }
            c.Economy = Sum(econ) / econ.Count;
            c.Survival = Sum(surv) / surv.Count;
            c.Control = rc.Control;
            c.Cash = k.BadgeRules.CashFor(d.Farming);
            if (c.Aware && known) foreach (var z in m.Zero) c.Zero.Add(z);
            double horizon = c.Aware && c.Kind == ModeKind.Open ? 600 : (c.Aware && rc.Goal > 0 ? rc.Goal : 1200);
            c.LevelUps = 2.5 * horizon / 60.0;
            c.Elite = c.Aware && known ? m.Elites : 0.15;
            c.Boss = c.Aware && known ? m.Bosses : 0.06;
            if (c.Aware && !known) c.Note = "mode '" + c.Mode + "' unknown: weighed as open-ended";
            return c;
        }

        /// <summary>The input for one advice, with its run shape and averaged context - what LoadoutState and the bench call.
        /// <paramref name="recruits"/> are used only while badgeRules.recruitWeight is above 0.</summary>
        public static LoadoutInput Prepare(string leader, Build build, string mode, int difficulty, Doctrine d, Knowledge k, Func<string, PowerFacts> facts,
            IList<KeyValuePair<string, Build>> recruits = null, Func<string, Kit> kitOf = null)
        {
            k = k ?? Knowledge.Current; d = d ?? new Doctrine();
            var inp = new LoadoutInput { Leader = leader ?? "", Mode = mode ?? "Normal", Difficulty = Math.Max(1, difficulty), Build = LoadoutBuild.From(build), Farming = d.Farming, Caution = d.Caution, Timing = d.Timing, ModeAware = d.ModeAware, TagPlan = d.TagPlan ?? "Auto", Style = d.Style.ToString() };
            inp.Ctx = Averaged(ContextFor(inp.Mode, inp.Difficulty, d, k), k);
            double rw = k.BadgeRules.RecruitWeight;
            inp.Shape = RunShape.Of(leader, build, rw > 0 ? recruits : null, rw, inp.Ctx.LevelUps, facts, k, d.Style, kitOf);
            inp.Recruits.AddRange(inp.Shape.Recruits);
            return inp;
        }

        // ------------------------------------------------------------------------------------------------ scoring
        static readonly Dictionary<string, string[]> WantMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "weapons", new[] { "WeaponDamage", "WeaponFireRate", "WeaponCooldownReduction", "InstantWeaponReloadChance" } },
            { "abilities", new[] { "AbilityDamage", "AbilityCooldownReduction", "AbilitySize", "AbilityDuration", "InstantAbilityReloadChance" } },
            { "armor", new[] { "MaxHealth", "Armor" } },
            { "healing", new[] { "HealthRegen", "HealthBonusesMod" } },
            { "dodge", new[] { "DodgeChance", "InviFrames" } },
            { "turret", new[] { "AbilityDuration" } },
            { "deployable", new[] { "AbilityDuration" } },
        };
        // crit wants are not here: they live in the crit lean
        static bool Wanted(RunShape s, string stat)
        {
            foreach (var w in s.Wants) { string[] stats; if (WantMap.TryGetValue(w ?? "", out stats) && stats.Contains(stat)) return true; }
            return false;
        }

        static double Relevance(string on, RunShape s, LoadoutCtx c)
        {
            switch (on)
            {
                case "weapon": return s.WeaponShare;
                case "ability": return s.AbilityShare;
                case "elite": return c.Elite;
                case "boss": return c.Boss;
                case "clip": return s.WeaponShare * s.Clip;
                case "heal": return s.Heal;
                default: return 1.0;
            }
        }
        static double CritFactor(string kind, double lean) { return kind == "chance" ? 0.85 + 0.3 * lean : 0.55 + 0.9 * lean; }

        static BadgeStat StatOf(Knowledge k, string key, double w)
        {
            BadgeStat s; return k.BadgeStats.TryGetValue(key, out s) ? s : new BadgeStat { W = w, On = "type" };
        }

        static void Term(List<BadgeTerm> terms, double v, string kind, string text, string stat = "")
        {
            terms.Add(new BadgeTerm { Points = v, Kind = kind, Text = text, Stat = stat });
        }

        /// <summary>The points of a badge at a level for this run; <paramref name="projected"/> = the tag points the run reaches
        /// before this badge (the build's own projection plus the badges taken already). The terms explain the sum.</summary>
        public static double Score(BadgeFacts b, int level, RunShape s, LoadoutCtx c, IDictionary<string, int> projected, Knowledge k, List<BadgeTerm> terms)
        {
            k = k ?? Knowledge.Current;
            terms = terms ?? new List<BadgeTerm>();
            int first = terms.Count;
            var rules = k.BadgeRules;
            foreach (var bo in b.Bonuses)
            {
                string key = bo.Stat ?? "";
                if (key.StartsWith("Hashtag", StringComparison.Ordinal))
                {
                    string t = TypeName(key.Substring("Hashtag".Length));
                    double v = bo.PerLevel * 100 * level * StatOf(k, "Hashtag", 0.75).W * s.Share(t);
                    Term(terms, v, "type:" + t, "+" + ((long)Math.Round(bo.PerLevel * 100 * level, MidpointRounding.ToEven)).ToString(IC) + "% " + t, key);
                    continue;
                }
                BadgeStat cfg;
                if (!k.BadgeStats.TryGetValue(key, out cfg))
                {
                    Term(terms, rules.UnknownStatPerLevel * level, "estimated", key, key);
                    continue;
                }
                double unit = cfg.Raw || bo.PerLevel >= 1 ? bo.PerLevel : bo.PerLevel * 100;
                double rel = Relevance(cfg.On, s, c);
                if (cfg.Dur) rel *= 0.5 + 0.5 * s.Deploy;
                if (!string.IsNullOrEmpty(cfg.Crit)) rel *= CritFactor(cfg.Crit, s.Crit);
                double ax = c.Axis(cfg.Axis);
                if (c.Zero.Contains(key)) ax = 0.0;
                if (cfg.Axis == "consistency" && s.BuildSelected) ax *= rules.SelectedBuildRerollBoost;
                double n = unit * level;
                if (cfg.Decay > 0 && cfg.Decay < 1) n = (1 - Math.Pow(cfg.Decay, n)) / (1 - cfg.Decay);
                double val = n * cfg.W * rel * ax;
                if (Wanted(s, key)) val *= rules.BuildWantsBoost;
                Term(terms, val, cfg.Axis ?? cfg.On, key, key);
            }
            if (b.TagTypes.Count > 0)
            {
                int pts = b.PointsAt(level);
                double tw = StatOf(k, "TagPoint", 1.5).W;
                bool spread = string.Equals(c.TagPlan, "Spread", StringComparison.OrdinalIgnoreCase);
                int at = c.SpecialAt > 0 ? c.SpecialAt : rules.SpecialAt;
                foreach (var t in b.TagTypes)
                {
                    double v = pts * tw * s.Share(t);
                    if (v > 0) Term(terms, v, "points:" + t, pts + " " + t + " point" + (pts == 1 ? "" : "s"));
                    int have; if (projected == null || !projected.TryGetValue(t, out have)) have = 0;
                    if (!spread && s.Share(t) >= 0.25 && pts > 0)
                    {
                        if (have < at && at <= have + pts) Term(terms, rules.SpecialPull * s.Share(t), "special:" + t, "takes " + t + " to the special (" + (have + pts) + " of " + at + ")");
                        else if (have >= at && t == s.Focus(c.TagPlan)) Term(terms, rules.EarlySpecialPerPoint * pts * s.Fit(t), "early:" + t, "the " + t + " special " + pts + " picks sooner");
                    }
                }
            }
            var note = NoteFor(b, k);
            if (note != null && note.Bias != 0) Term(terms, note.Bias, "bias", "knowledge.json bias");
            var vals = new List<double>();
            for (int i = first; i < terms.Count; i++) vals.Add(terms[i].Points);
            return Sum(vals);
        }

        static void AddPoints(Dictionary<string, int> proj, BadgeFacts b, int level)
        {
            int pts = b.PointsAt(level);
            foreach (var t in b.TagTypes) { int n; proj.TryGetValue(t, out n); proj[t] = n + pts; }
        }

        // ------------------------------------------------------------------------------------------------ the advice
        /// <summary>The advice: forced badges first (capped at the slots), then the build's pins in their order, then the best
        /// score each round against the tag points already taken. Locked, not rated, off-grid and skipped badges are never
        /// advised; an empty slot is never advised. <paramref name="number"/> = the #N of the log lines.</summary>
        public static LoadoutAdvice Recommend(LoadoutInput inp, IList<BadgeFacts> badges, Knowledge k, int number = 0)
        {
            k = k ?? Knowledge.Current;
            var a = new LoadoutAdvice { Number = number, Input = inp };
            var all = Clean(badges);
            var rules = k.BadgeRules;
            var s = inp.Shape ?? new RunShape(); var c = inp.Ctx ?? new LoadoutCtx();
            var skip = new HashSet<int>(); var skipUnknown = new List<string>();
            if (inp.Build != null)
                foreach (var r in inp.Build.Skip) { string how; var b = Resolve(r, all, out how); if (b != null) skip.Add(b.Id); else skipUnknown.Add(r); }
            var proj0 = new Dictionary<string, int>(s.Projected, StringComparer.OrdinalIgnoreCase);

            foreach (var b in all)
            {
                var row = new LoadoutRow { Badge = b, Level = inp.LevelOf(b.Id), Rated = Rated(b, k), OnGrid = !inp.OffGrid.Contains(b.Id), Open = inp.Open(b.Id), Skipped = skip.Contains(b.Id) };
                row.Score = R2(Score(b, Math.Max(row.Level, 1), s, c, proj0, k, row.Terms));
                row.Per = R2(Score(b, 1, s, c, new Dictionary<string, int>(), k, null));
                if (row.Rated) { var unknown = UnknownStats(b, k); if (unknown.Count > 0) { row.Estimated = true; a.Estimated.Add(b.Id + " " + b.Short + ": " + string.Join(", ", unknown)); } }
                a.Rows.Add(row);
            }
            a.All.AddRange(a.Rows); a.All.Sort(Order);
            if (string.IsNullOrEmpty(inp.Leader)) a.Notes.Add("leader unknown");
            if (!string.IsNullOrEmpty(c.Note)) a.Notes.Add(c.Note);

            var taken = new HashSet<int>();
            var proj = new Dictionary<string, int>(proj0, StringComparer.OrdinalIgnoreCase);
            int slots = Math.Max(0, inp.Slots);
            // 1. forced (quest) badges: the game locks them in
            foreach (var fid in inp.Forced)
            {
                if (taken.Contains(fid)) continue;
                var row = a.RowOf(fid);
                if (row == null) { a.Notes.Add("forced badge " + fid + " is not in the registry"); continue; }
                if (a.Picks.Count >= slots) break;
                var p = new LoadoutPick { Badge = row.Badge, Level = row.Level, Rank = 0, Score = row.Score, Source = "forced", Terms = row.Terms };
                if (row.Skipped) p.Why2 = (s.Auto ? "never on " + s.BuildName + " (Auto)" : "you marked it never") + " - the mission requires it";
                a.Picks.Add(p); taken.Add(fid);
                AddPoints(proj, row.Badge, Math.Max(row.Level, 1));
            }
            int forcedCount = a.Picks.Count;
            if (slots == 0) a.Notes.Add("no badge slots");
            else if (forcedCount > 0 && forcedCount >= slots) a.Notes.Add("the quest fixes every badge");
            // 2. the build's pins, in order
            var pinnedLocked = new List<LoadoutRow>();
            if (inp.Build != null && slots > 0)
            {
                var unknown = new List<string>(); var ignored = new List<string>();
                foreach (var r in inp.Build.Badges)
                {
                    string how; var b = Resolve(r, all, out how);
                    if (b == null) { if (!unknown.Contains(r)) unknown.Add(r); continue; }
                    if (taken.Contains(b.Id)) continue;
                    var row = a.RowOf(b.Id);
                    if (!row.Unlocked) { if (!pinnedLocked.Contains(row)) pinnedLocked.Add(row); continue; }
                    if (!row.Rated || !row.OnGrid) { ignored.Add(b.Short + (row.Rated ? " (not on the grid)" : " (not rated)")); continue; }
                    if (a.Picks.Count >= slots) { ignored.Add(b.Short); continue; }
                    var terms = new List<BadgeTerm>();
                    double sc = Score(b, row.Level, s, c, proj, k, terms);
                    a.Picks.Add(new LoadoutPick { Badge = b, Level = row.Level, Score = R2(sc), Source = "pin", Terms = terms });
                    taken.Add(b.Id);
                    AddPoints(proj, b, row.Level);
                }
                foreach (var u in unknown) a.Notes.Add("pin '" + u + "' matches no badge");
                foreach (var pl in pinnedLocked) a.Notes.Add("pinned " + pl.Badge.Short + " is locked");
                if (ignored.Count > 0) a.Notes.Add("pins ignored: " + string.Join(", ", ignored) + (ignored.Any(x => !x.Contains("(")) ? " (more pins than slots)" : ""));
            }
            foreach (var u in skipUnknown) a.Notes.Add("never-mark '" + u + "' matches no badge");
            // 3. greedy: the best score each round, the tag points of the badges already taken counted
            Func<LoadoutRow, bool> eligible = r => !taken.Contains(r.Badge.Id) && r.Unlocked && r.Rated && r.OnGrid && !r.Skipped;
            while (a.Picks.Count < slots)
            {
                LoadoutRow best = null; double bestScore = 0; List<BadgeTerm> bestTerms = null;
                foreach (var r in a.Rows)
                {
                    if (!eligible(r)) continue;
                    var terms = new List<BadgeTerm>();
                    double sc = Score(r.Badge, r.Level, s, c, proj, k, terms);
                    if (best == null || Order(sc, r.Level, r.Badge, bestScore, best.Level, best.Badge) < 0) { best = r; bestScore = sc; bestTerms = terms; }
                }
                if (best == null) break;
                a.Picks.Add(new LoadoutPick { Badge = best.Badge, Level = best.Level, Score = R2(bestScore), Source = "score", Terms = bestTerms });
                taken.Add(best.Badge.Id);
                AddPoints(proj, best.Badge, best.Level);
            }
            int rank = 0;
            foreach (var p in a.Picks) if (!p.Forced) p.Rank = ++rank;

            // annotations
            var rest = a.All.Where(r => !taken.Contains(r.Badge.Id) && r.Unlocked && r.Rated && r.OnGrid && !r.Skipped).ToList();
            var bestRest = rest.FirstOrDefault();
            a.Next.AddRange(rest.Take(2));
            foreach (var p in a.Picks)
                if (p.Source == "score" && bestRest != null && Centi(p.Score) - Centi(bestRest.Score) < Centi(rules.SwapMargin)) { p.Close = true; p.CloseTo = bestRest.Badge; }
            a.Never.AddRange(a.Rows.Where(r => r.Unlocked && r.Rated && r.Score <= 0.05));
            a.Unrated.AddRange(a.Rows.Where(r => !r.Rated));
            int free = a.Picks.Count(p => !p.Forced);
            if (slots > 0 && forcedCount < slots)
            {
                if (!a.Rows.Any(r => r.Unlocked && r.Rated)) a.Notes.Add("no badges unlocked yet");
                else if (free < slots - forcedCount) a.Notes.Add("EQUIP " + free + " of " + (slots - forcedCount) + " slots (only " + free + " rated badge" + (free == 1 ? "" : "s") + " unlocked)");
            }
            // UNLOCK: a pinned locked badge always; else the best locked badge whose rank is open and whose level-1 score beats the
            // weakest score pick by the margin
            var scorePicks = a.Picks.Where(p => !p.Forced).ToList();
            double weakest = scorePicks.Count > 0 ? scorePicks.Min(p => p.Score) : 0;
            Func<double, int> wouldBe = sc => 1 + scorePicks.Count(p => Centi(p.Score) > Centi(sc));
            // only with a slot the advice fills: with no badge slots, or every slot fixed by a quest, an unlocked badge could not be
            // equipped either ("UNLOCK ... would be #1" under "THE QUEST FIXES EVERY BADGE")
            if (slots - forcedCount > 0)
            {
                foreach (var pl in pinnedLocked) a.Unlocks.Add(new LoadoutUnlockHint { Row = pl, Pinned = true, WouldBe = wouldBe(pl.Score) });
                foreach (var r in a.All)
                {
                    if (r.Unlocked || !r.Rated || !r.OnGrid || !r.Open || r.Skipped || pinnedLocked.Contains(r)) continue;
                    if (Centi(r.Score) - Centi(weakest) >= Centi(rules.SwapMargin)) a.Unlocks.Add(new LoadoutUnlockHint { Row = r, WouldBe = wouldBe(r.Score) });
                }
            }
            // LEVEL: the advised badge whose next level adds the most per Training Yard point (level k costs k points)
            double bestGain = double.NegativeInfinity;
            foreach (var p in scorePicks)
            {
                int L = Math.Max(p.Level, 1);
                if (L >= p.Badge.Max) continue;
                double s1 = Score(p.Badge, L, s, c, new Dictionary<string, int>(), k, null), s2 = Score(p.Badge, L + 1, s, c, new Dictionary<string, int>(), k, null);
                double g = (s2 - s1) / (L + 1);
                if (g > bestGain) { bestGain = g; a.Level = new LoadoutLevelHint { Badge = p.Badge, From = L, To = L + 1, Cost = L + 1, Gain = s2 - s1, PerPoint = g }; }
            }
            // reasons
            foreach (var p in a.Picks)
            {
                p.Effect = Effect(p.Badge, Math.Max(p.Level, 1));
                p.Why = Why(p.Badge, Math.Max(p.Level, 1), p.Terms, s, c, k, p.Forced);
                if (p.Source == "pin" && string.IsNullOrEmpty(p.Why2)) p.Why2 = PinnedOn(s);
            }
            return a;
        }

        /// <summary>The reason of a row (not advised, never, locked, not rated): the same templates as a pick's.</summary>
        public static string WhyOf(LoadoutRow r, LoadoutAdvice a, Knowledge k)
        {
            if (!r.Rated) return "not rated by the Companion (unknown effect)";
            return Why(r.Badge, Math.Max(r.Level, 1), r.Terms, a.Input.Shape, a.Input.Ctx, k, false);
        }

        // ------------------------------------------------------------------------------------------------ reasons
        static readonly Dictionary<string, string> ShortLabel = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "WeaponDamage", "weapon dmg" }, { "AbilityDamage", "ability dmg" }, { "WeaponFireRate", "fire rate" }, { "MovementSpeed", "move" },
            { "WeaponCooldownReduction", "weapon CDR" }, { "AbilityCooldownReduction", "ability CDR" }, { "AbilitySize", "area" }, { "AbilityDuration", "duration" },
            { "WeaponCriticalChance", "crit" }, { "AbilityCriticalChance", "crit" }, { "WeaponCriticalDamage", "crit dmg" }, { "AbilityCriticalDamage", "crit dmg" },
            { "DamageToElites", "vs elites" }, { "DamageToBosses", "vs bosses" }, { "InstantWeaponReloadChance", "instant reload" }, { "InstantAbilityReloadChance", "instant refresh" },
            { "InternalMilitaryTrainingRarityBonus", "military rarity" }, { "XPMultiplier", "XP" }, { "Luck", "luck" }, { "MagnetRange", "pickup" }, { "XPGemRarity", "gem rarity" },
            { "PowerupDurationExtension", "power-up time" }, { "NumRerolls", "rerolls" }, { "NumBanishes", "banishes" }, { "MoneyMultiplier", "cash" }, { "SpecializationPointsMod", "survivor XP" },
            { "MaxHealth", "HP" }, { "Armor", "armor" }, { "DodgeChance", "dodge" }, { "InviFrames", "s i-frames" }, { "HealthRegen", "HP/s" }, { "HealthBonusesMod", "healing" },
        };
        static readonly HashSet<string> Flat = new HashSet<string>(StringComparer.Ordinal) { "MaxHealth", "Armor", "Luck", "MovementSpeed", "NumRerolls", "NumBanishes", "HealthRegen", "InviFrames" };

        static string Pct(double x) { return ((long)Math.Round(x, MidpointRounding.ToEven)).ToString(IC); }

        /// <summary>The badge's effect at a level, short (36 characters at most): "+20% Kinetic, +2 Kinetic tags", "+8% weapon & ability dmg",
        /// "+160 HP, +8 armor", "+20% and +3 tags to 4 types" (0.14.0, A1: "tags", the game's damage type tags - it said "2 pts").</summary>
        public static string Effect(BadgeFacts b, int level)
        {
            if (b.TagTypes.Count > 0 && b.Bonuses.Count > 0)
            {
                double per = b.Bonuses[0].PerLevel; int pts = b.PointsAt(level);
                if (b.TagTypes.Count == 1) return "+" + Pct(per * 100 * level) + "% " + b.TagTypes[0] + ", +" + pts + " " + b.TagTypes[0] + " tag" + (pts == 1 ? "" : "s");
                return "+" + Pct(per * 100 * level) + "% and +" + pts + " tag" + (pts == 1 ? "" : "s") + " to " + b.TagTypes.Count + " types";
            }
            var keys = new HashSet<string>(b.Bonuses.Select(x => x.Stat ?? ""));
            if (keys.Count == 2 && keys.Contains("WeaponDamage") && keys.Contains("AbilityDamage") && b.Bonuses.Count > 0)
                return "+" + Pct(b.Bonuses[0].PerLevel * 100 * level) + "% weapon & ability dmg";
            var parts = new List<string>(); var seen = new HashSet<string>();
            foreach (var bo in b.Bonuses)
            {
                string k = bo.Stat ?? ""; string lab; if (!ShortLabel.TryGetValue(k, out lab)) lab = k;
                if (!seen.Add(lab)) continue;
                if (k == "InviFrames") parts.Add("+" + Fixed(bo.PerLevel * level, 2) + lab);
                else if (Flat.Contains(k))
                {
                    double n = RoundPy(bo.PerLevel * level, 2);
                    string one = lab == "rerolls" ? "reroll" : lab == "banishes" ? "banish" : lab;
                    parts.Add("+" + n.ToString("0.######", IC) + " " + (n == 1 ? one : lab));
                }
                else parts.Add("+" + Pct(bo.PerLevel * 100 * level) + "% " + lab);
            }
            return string.Join(", ", parts);
        }

        static readonly string[] Roman = { "I", "II", "III", "IV", "V" };
        public static string RomanOf(int difficulty) { return Roman[Math.Max(0, Math.Min(4, difficulty - 1))]; }

        static bool CritStat(Knowledge k, string stat) { BadgeStat s; return k.BadgeStats.TryGetValue(stat ?? "", out s) && !string.IsNullOrEmpty(s.Crit); }

        /// <summary>How a reason names the run's build: "your Rifleman build" for one the player chose, "the Rifleman build" for a lent
        /// build Auto follows (<see cref="RunShape.Auto"/>; 0.13.0, the in-run wording of F02; 0.14.0, A1: no "(Auto)" on screen).</summary>
        public static string BuildRef(RunShape s) { return s != null && s.Auto ? "the " + s.BuildName + " build" : "your " + (s != null ? s.BuildName : "") + " build"; }
        /// <summary>The second WHY line of a pinned badge: "pinned on your build", or "pinned on the Rifleman build" for a lent build.</summary>
        public static string PinnedOn(RunShape s) { return s != null && s.Auto ? "pinned on the " + s.BuildName + " build" : "pinned on your build"; }

        /// <summary>The game's name of a mode ("Boss Rush" for BossRush, "One Hit" for OneHit).</summary>
        public static string ModeName(string mode) { return mode == "BossRush" ? "Boss Rush" : mode == "OneHit" ? "One Hit" : mode ?? ""; }

        /// <summary>A run's name as the reasons say it: "Normal II", "Boss Rush I", "Hardcore" / "Endless" / "Infinite" (no difficulty).
        /// 0.16.0 (C16-07): factored out of the survival reason; the Training Yard's badge reasons name the run with it.</summary>
        public static string RunName(string mode, int difficulty) { return mode == "Hardcore" || mode == "Endless" || mode == "Infinite" ? mode : ModeName(mode) + " " + RomanOf(difficulty); }

        /// <summary>Why the badge fits THIS run, short (48 characters at most); the biggest term decides. A knowledge.json note
        /// replaces it; a forced badge always says the mission.</summary>
        public static string Why(BadgeFacts b, int level, List<BadgeTerm> terms, RunShape s, LoadoutCtx c, Knowledge k, bool forced)
        {
            k = k ?? Knowledge.Current;
            if (forced) return "the active mission requires it";
            var note = NoteFor(b, k);
            if (note != null && !string.IsNullOrEmpty(note.Note)) return note.Note;
            if (terms == null || terms.Count == 0) return "no effect read";
            BadgeTerm big = terms[0];
            foreach (var t in terms) if (t.Points > big.Points) big = t;
            if (big.Points <= 0.05)
            {
                if (terms.Any(t => t.Kind.StartsWith("type:", StringComparison.Ordinal)))
                {
                    if (b.TagTypes.Count > 2) return "no " + (b.TagTypes.Contains("Fire") ? "elemental" : "physical") + " damage in this run";
                    return "nothing in this run deals " + string.Join("/", b.TagTypes);
                }
                if (c.Aware && c.Mode == "OneHit" && terms.Any(t => t.Kind == "survival" || t.Kind == "dodge")) return "One Hit: a single hit ends the run";
                if (c.Aware && c.Mode == "Extermination" && terms.Any(t => t.Kind == "economy")) return "Extermination: the game drops XP and luck";
                return "little use for this run";
            }
            string kind = big.Kind;
            if (kind.StartsWith("special:", StringComparison.Ordinal)) return big.Text;
            if (kind.StartsWith("type:", StringComparison.Ordinal) || kind.StartsWith("points:", StringComparison.Ordinal))
            {
                if (b.TagTypes.Count > 1)
                {
                    var dealt = b.TagTypes.Select((t, i) => new { t, i, sh = s.Share(t) }).Where(x => x.sh > 0).OrderByDescending(x => x.sh).ThenBy(x => x.i).ToList();
                    if (dealt.Count == 0) return "nothing in this run deals these types";
                    return string.Join(", ", dealt.Take(2).Select(x => x.t + " " + Pct(x.sh * 100) + "%")) + " of the damage";
                }
                string type = kind.Substring(kind.IndexOf(':') + 1);
                long pct = (long)Math.Round(s.Share(type) * 100, MidpointRounding.ToEven);
                string who = s.Recruits.Count == 0 ? "your" : "the squad's";
                string lead = type + " is " + (pct >= 95 ? "all" : pct.ToString(IC) + "%") + " of " + who + " damage";
                string src = s.SourceText(type);
                if (lead.Length + src.Length + 3 > 48 || src.Length == 0) return lead;
                int comma = src.IndexOf(", ", StringComparison.Ordinal);
                return lead + " (" + (comma >= 0 ? src.Substring(0, comma) : src) + ")";
            }
            if (kind == "weapon" || kind == "ability")
            {
                if (CritStat(k, big.Stat)) return s.Wants.Any(w => string.Equals(w, "critical", StringComparison.OrdinalIgnoreCase)) ? BuildRef(s) + " wants crits" : "crits on every hit";
                var keys = new HashSet<string>(b.Bonuses.Select(x => x.Stat ?? ""));
                if (keys.Count == 2 && keys.Contains("WeaponDamage") && keys.Contains("AbilityDamage")) return "counts on every hit";
                double heavy = kind == "ability" ? s.AbilityShare : s.WeaponShare;
                return heavy >= 0.6 ? s.BuildName + " build leans on " + (kind == "ability" ? "abilities" : "weapons") : "faster weapons and abilities";
            }
            switch (kind)
            {
                case "economy": return c.Kind == ModeKind.Open ? "open-ended: XP never stops paying" : "pays back over the whole run";
                case "cash": return c.Farming == 2 ? "run goal Farm: the Training Yard counts" : "Training Yard only (run goal: " + (c.Farming == 0 ? "win" : "balanced") + ")";
                case "survival":
                    {
                        // 0.14.0 (A1): the multiplier as a share in words ("survival weighs x1.31" counted nothing a player knows);
                        // under 1 (the early run's 0.8 over a short horizon, [Advice] Caution = Low's x0.6) it counts LESS
                        if (c.Survival < 0.95) return "survival picks count " + Pct((1 - c.Survival) * 100) + "% less";
                        if (c.Survival < 1.10) return "survival picks always help";
                        string where = RunName(c.Mode, c.Difficulty);
                        return where + ": survival picks count " + Pct((c.Survival - 1) * 100) + "% more";
                    }
                case "dodge": case "move": return "keeps you out of the horde";
                case "consistency": return s.BuildSelected ? (s.Auto ? "finds the " + s.BuildName + " build's pieces" : "finds your " + s.BuildName + " build's pieces") : "steadier offers all run";
                case "elite": case "boss": return ModeName(c.Mode) + ": ~" + Pct((c.Elite + c.Boss) * 100) + "% of damage hits elites/bosses";
                case "clip": return "instant reloads on a clip weapon";
                case "heal": return "more from every heal on the squad";
                case "estimated": return "an effect the Companion estimates";
                case "bias": return "your knowledge.json bias";
            }
            return big.Text;
        }

        // ------------------------------------------------------------------------------------------------ identity, drift
        /// <summary>The canonical text of the badges (one line per badge, sorted by id; levels and selection are not part of it).</summary>
        public static string Canonical(IEnumerable<BadgeFacts> all)
        {
            var sb = new StringBuilder();
            foreach (var b in all.OrderBy(x => x.Id))
            {
                sb.Append(b.Id).Append('|').Append(b.Short).Append('|').Append(b.Kind).Append('|');
                sb.Append(string.Join(";", b.Bonuses.Select(x => x.Stat + "=" + x.PerLevel.ToString("0.######", IC))));
                sb.Append("|tags=").Append(string.Join(",", b.TagTypes.OrderBy(t => t, StringComparer.Ordinal)));      // the order does not matter to the game
                sb.Append("|points=").Append(string.Join(",", (b.TagPoints ?? new int[0]).Select(p => p.ToString(IC))));
                sb.Append('\n');
            }
            return sb.ToString();
        }
        public static string Hash(IEnumerable<BadgeFacts> all) { return Updater.Sha256(Canonical(all)); }
        public static string ShortHash(string hash) { return string.IsNullOrEmpty(hash) ? "" : hash.Substring(0, Math.Min(8, hash.Length)); }

        /// <summary>"8 Boss DamageToElites 0.1 -> 0.12; 33 Sprint new; 4 Carver gone" - what differs between two inventories.</summary>
        public static string Drift(IList<BadgeFacts> reference, IList<BadgeFacts> live)
        {
            var parts = new List<string>();
            foreach (var b in live.OrderBy(x => x.Id))
            {
                var r = reference.FirstOrDefault(x => x.Id == b.Id);
                if (r == null) { parts.Add(b.Id + " " + b.Short + " new"); continue; }
                if (r.Short != b.Short) parts.Add(b.Id + " renamed " + r.Short + " -> " + b.Short);
                if (r.Kind != b.Kind) parts.Add(b.Id + " " + b.Short + " class " + r.Kind + " -> " + b.Kind);
                foreach (var bo in b.Bonuses)
                {
                    var ro = r.Bonuses.FirstOrDefault(x => x.Stat == bo.Stat);
                    if (ro == null) parts.Add(b.Id + " " + b.Short + " " + bo.Stat + " new " + bo.PerLevel.ToString("0.######", IC));
                    else if (ro.PerLevel != bo.PerLevel) parts.Add(b.Id + " " + b.Short + " " + bo.Stat + " " + ro.PerLevel.ToString("0.######", IC) + " -> " + bo.PerLevel.ToString("0.######", IC));
                }
                foreach (var ro in r.Bonuses) if (!b.Bonuses.Any(x => x.Stat == ro.Stat)) parts.Add(b.Id + " " + b.Short + " " + ro.Stat + " gone");
                if (!r.TagTypes.SequenceEqual(b.TagTypes) || !(r.TagPoints ?? new int[0]).SequenceEqual(b.TagPoints ?? new int[0])) parts.Add(b.Id + " " + b.Short + " tag table changed");
            }
            foreach (var r in reference) if (!live.Any(x => x.Id == r.Id)) parts.Add(r.Id + " " + r.Short + " gone");
            return string.Join("; ", parts);
        }

        // ------------------------------------------------------------------------------------------------ JSON (log lines and replay)
        static string Json(Action<Utf8JsonWriter> write)
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                    write(w);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
        static void Strings(Utf8JsonWriter w, string name, IEnumerable<string> values) { w.WriteStartArray(name); foreach (var v in values) w.WriteStringValue(v ?? ""); w.WriteEndArray(); }
        static void Ints(Utf8JsonWriter w, string name, IEnumerable<int> values) { w.WriteStartArray(name); foreach (var v in values) w.WriteNumberValue(v); w.WriteEndArray(); }
        static List<string> ReadStrings(JsonElement o, string name)
        {
            var l = new List<string>(); JsonElement e;
            if (o.TryGetProperty(name, out e) && e.ValueKind == JsonValueKind.Array) foreach (var v in e.EnumerateArray()) l.Add(v.ValueKind == JsonValueKind.Number ? v.GetRawText() : v.GetString() ?? "");
            return l;
        }
        static List<int> ReadInts(JsonElement o, string name)
        {
            var l = new List<int>(); JsonElement e;
            if (o.TryGetProperty(name, out e) && e.ValueKind == JsonValueKind.Array) foreach (var v in e.EnumerateArray()) if (v.ValueKind == JsonValueKind.Number) l.Add(v.GetInt32());
            return l;
        }
        static string S(JsonElement o, string name, string fallback = "") { JsonElement e; return o.TryGetProperty(name, out e) && e.ValueKind == JsonValueKind.String ? e.GetString() : fallback; }
        static double D(JsonElement o, string name, double fallback = 0) { JsonElement e; return o.TryGetProperty(name, out e) && e.ValueKind == JsonValueKind.Number ? e.GetDouble() : fallback; }
        static int I(JsonElement o, string name, int fallback = 0) { JsonElement e; return o.TryGetProperty(name, out e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : fallback; }
        static bool B(JsonElement o, string name, bool fallback = false) { JsonElement e; return o.TryGetProperty(name, out e) ? (e.ValueKind == JsonValueKind.True ? true : e.ValueKind == JsonValueKind.False ? false : fallback) : fallback; }

        /// <summary>The `[loadout] inventory {json}` line: every badge as the advice reads it (once per session).</summary>
        public static string InventoryJson(IList<BadgeFacts> all, string game)
        {
            string hash = ShortHash(Hash(all));
            return Json(w =>
            {
                w.WriteStartObject();
                w.WriteNumber("v", 1); w.WriteString("hash", hash); w.WriteString("game", game ?? "");
                w.WriteStartArray("badges");
                foreach (var b in all.OrderBy(x => x.Sort).ThenBy(x => x.Id))
                {
                    w.WriteStartObject();
                    w.WriteNumber("id", b.Id); w.WriteNumber("sort", b.Sort); w.WriteString("asset", b.Asset); w.WriteString("name", b.Name); w.WriteString("kind", b.Kind.ToString());
                    if (b.Kind == BadgeKind.Unknown) w.WriteString("class", b.KindName);
                    w.WriteString("owner", b.Owner); w.WriteNumber("rank", b.Rank); w.WriteNumber("max", b.Max);
                    w.WriteStartArray("stats");
                    foreach (var bo in b.Bonuses) { w.WriteStartArray(); w.WriteStringValue(bo.Stat); w.WriteStringValue(bo.Type); w.WriteNumberValue(bo.PerLevel); w.WriteEndArray(); }
                    w.WriteEndArray();
                    Strings(w, "tags", b.TagTypes); Ints(w, "points", b.TagPoints ?? new int[0]);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            });
        }

        public static List<BadgeFacts> ReadInventory(string json)
        {
            var o = new List<BadgeFacts>();
            using (var doc = JsonDocument.Parse(json))
            {
                JsonElement arr;
                if (!doc.RootElement.TryGetProperty("badges", out arr)) return o;
                foreach (var e in arr.EnumerateArray())
                {
                    var b = new BadgeFacts { Id = I(e, "id"), Sort = I(e, "sort"), Asset = S(e, "asset"), Name = S(e, "name"), Owner = S(e, "owner"), Rank = I(e, "rank"), Max = I(e, "max", 5), KindName = S(e, "class") };
                    BadgeKind kind; b.Kind = Enum.TryParse(S(e, "kind"), out kind) ? kind : BadgeKind.Unknown;
                    b.Short = ShortName(b.Asset);
                    JsonElement st;
                    if (e.TryGetProperty("stats", out st))
                        foreach (var x in st.EnumerateArray())
                        {
                            var parts = x.EnumerateArray().ToList();
                            if (parts.Count >= 3) b.Bonuses.Add(new BadgeBonus { Stat = parts[0].GetString() ?? "", Type = parts[1].ValueKind == JsonValueKind.String ? parts[1].GetString() : parts[1].GetRawText(), PerLevel = parts[2].GetDouble() });
                        }
                    b.TagTypes.AddRange(ReadStrings(e, "tags"));
                    b.TagPoints = ReadInts(e, "points").ToArray();
                    o.Add(b);
                }
            }
            return o;
        }

        /// <summary>The badge sections of knowledge.json as they are in force (the `[loadout] knowledge {json}` line when they differ
        /// from the code defaults); Knowledge.FromJson reads it back.</summary>
        public static string KnowledgeJson(Knowledge k)
        {
            return Json(w =>
            {
                w.WriteStartObject();
                w.WriteStartObject("badgeStats");
                foreach (var kv in k.BadgeStats.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    var s = kv.Value;
                    w.WriteStartObject(kv.Key);
                    w.WriteNumber("w", s.W); w.WriteString("on", s.On);
                    if (s.Axis != null) w.WriteString("axis", s.Axis);
                    if (s.Crit != null) w.WriteString("crit", s.Crit);
                    if (s.Dur) w.WriteBoolean("dur", true);
                    if (s.Raw) w.WriteBoolean("raw", true);
                    if (s.Decay > 0) w.WriteNumber("decay", s.Decay);
                    w.WriteEndObject();
                }
                w.WriteEndObject();
                w.WriteStartObject("badgeModes");
                foreach (var kv in k.BadgeModes.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    w.WriteStartObject(kv.Key);
                    w.WriteNumber("elites", kv.Value.Elites); w.WriteNumber("bosses", kv.Value.Bosses); w.WriteNumber("goal", kv.Value.Goal);
                    if (kv.Value.Zero.Count > 0) Strings(w, "zero", kv.Value.Zero.OrderBy(x => x, StringComparer.Ordinal));
                    w.WriteEndObject();
                }
                w.WriteEndObject();
                var r = k.BadgeRules;
                w.WriteStartObject("badgeRules");
                w.WriteNumber("specialAt", r.SpecialAt); w.WriteNumber("specialPull", r.SpecialPull); w.WriteNumber("earlySpecialPerPoint", r.EarlySpecialPerPoint);
                w.WriteNumber("recruitWeight", r.RecruitWeight); w.WriteNumber("swapMargin", r.SwapMargin); w.WriteNumber("swapMarginShare", r.SwapMarginShare);
                w.WriteNumber("buildWantsBoost", r.BuildWantsBoost); w.WriteNumber("selectedBuildRerollBoost", r.SelectedBuildRerollBoost); w.WriteNumber("unknownStatPerLevel", r.UnknownStatPerLevel);
                w.WriteStartArray("cashByRunGoal"); foreach (var x in r.CashByRunGoal) w.WriteNumberValue(x); w.WriteEndArray();
                w.WriteEndObject();
                w.WriteStartObject("badges");
                foreach (var kv in k.BadgeNotes.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    w.WriteStartObject(kv.Key);
                    if (kv.Value.Tier.Length > 0) w.WriteString("tier", kv.Value.Tier);
                    if (kv.Value.Bias != 0) w.WriteNumber("bias", kv.Value.Bias);
                    if (kv.Value.Note.Length > 0) w.WriteString("note", kv.Value.Note);
                    w.WriteEndObject();
                }
                w.WriteEndObject();
                w.WriteEndObject();
            });
        }
        public static string KnowledgeHash(Knowledge k) { return ShortHash(Updater.Sha256(KnowledgeJson(k))); }

        /// <summary>Whether the badge sections in force are the code defaults (else the knowledge line is logged once per session).</summary>
        public static bool KnowledgeIsDefault(Knowledge k)
        {
            var d = new Knowledge();
            if (k.BadgeStats.Count != d.BadgeStats.Count || k.BadgeModes.Count != d.BadgeModes.Count || k.BadgeNotes.Count > 0) return false;
            foreach (var kv in d.BadgeStats) { BadgeStat s; if (!k.BadgeStats.TryGetValue(kv.Key, out s) || !s.SameAs(kv.Value)) return false; }
            foreach (var kv in d.BadgeModes) { BadgeMode m; if (!k.BadgeModes.TryGetValue(kv.Key, out m) || !m.SameAs(kv.Value)) return false; }
            return k.BadgeRules.SameAs(d.BadgeRules);
        }

        static void WriteShape(Utf8JsonWriter w, RunShape s)
        {
            w.WriteStartObject("shape");
            w.WriteString("lead", s.Lead); w.WriteString("build", s.BuildName); w.WriteBoolean("sel", s.BuildSelected);
            if (s.Auto) w.WriteBoolean("auto", true);          // only when set: the lines of a build the player chose stay as they were
            Strings(w, "wants", s.Wants); Strings(w, "recruits", s.Recruits);
            w.WriteNumber("total", s.Total);
            w.WriteStartObject("w"); foreach (var t in TagProfile.Names) { double v; if (s.Weight.TryGetValue(t, out v)) w.WriteNumber(t, v); } w.WriteEndObject();
            w.WriteStartObject("src");
            foreach (var t in TagProfile.Names)
            {
                List<string> l; if (!s.Sources.TryGetValue(t, out l)) continue;
                w.WriteStartArray(t);
                foreach (var src in l) { double v; s.SourceWeight.TryGetValue(t + "|" + src, out v); w.WriteStartArray(); w.WriteStringValue(src); w.WriteNumberValue(v); w.WriteEndArray(); }
                w.WriteEndArray();
            }
            w.WriteEndObject();
            w.WriteNumber("wpn", s.WeaponW); w.WriteNumber("abl", s.AbilityW); w.WriteNumber("dep", s.DeployW); w.WriteNumber("admg", s.AbilityDmgW);
            w.WriteNumber("crit", s.Crit); w.WriteNumber("heal", s.Heal); w.WriteNumber("clip", s.Clip); w.WriteNumber("deploy", s.Deploy);
            w.WriteStartArray("proj"); foreach (var t in s.ProjectedOrder) { w.WriteStartArray(); w.WriteStringValue(t); w.WriteNumberValue(s.Projected[t]); w.WriteEndArray(); } w.WriteEndArray();
            w.WriteEndObject();
        }
        static RunShape ReadShape(JsonElement o)
        {
            var s = new RunShape { Lead = S(o, "lead"), BuildName = S(o, "build", "Auto"), BuildSelected = B(o, "sel"), Auto = B(o, "auto"), Total = D(o, "total"), WeaponW = D(o, "wpn"), AbilityW = D(o, "abl"),
                DeployW = D(o, "dep"), AbilityDmgW = D(o, "admg"), Crit = D(o, "crit", 0.3), Heal = D(o, "heal", 0.3), Clip = D(o, "clip", 0.5), Deploy = D(o, "deploy") };
            s.Wants.AddRange(ReadStrings(o, "wants")); s.Recruits.AddRange(ReadStrings(o, "recruits"));
            JsonElement e;
            if (o.TryGetProperty("w", out e)) foreach (var p in e.EnumerateObject()) s.Weight[p.Name] = p.Value.GetDouble();
            if (o.TryGetProperty("src", out e))
                foreach (var p in e.EnumerateObject())
                {
                    var l = new List<string>();
                    foreach (var x in p.Value.EnumerateArray()) { var two = x.EnumerateArray().ToList(); string src = two[0].GetString(); l.Add(src); s.SourceWeight[p.Name + "|" + src] = two[1].GetDouble(); }
                    s.Sources[p.Name] = l;
                }
            if (o.TryGetProperty("proj", out e)) foreach (var x in e.EnumerateArray()) { var two = x.EnumerateArray().ToList(); s.SetProjected(two[0].GetString(), two[1].GetInt32()); }
            return s;
        }
        static void WriteCtx(Utf8JsonWriter w, LoadoutCtx c)
        {
            w.WriteStartObject("ctx");
            w.WriteString("mode", c.Mode); w.WriteNumber("diff", c.Difficulty); w.WriteString("kind", c.Kind.ToString()); w.WriteBoolean("aware", c.Aware);
            w.WriteNumber("econ", c.Economy); w.WriteNumber("surv", c.Survival); w.WriteNumber("ctrl", c.Control); w.WriteNumber("cash", c.Cash);
            w.WriteNumber("elite", c.Elite); w.WriteNumber("boss", c.Boss); w.WriteNumber("lvl", c.LevelUps); w.WriteNumber("special", c.SpecialAt);
            w.WriteNumber("farm", c.Farming); w.WriteNumber("caution", c.Caution); w.WriteNumber("timing", c.Timing); w.WriteString("plan", c.TagPlan);
            Strings(w, "zero", c.Zero.OrderBy(x => x, StringComparer.Ordinal));
            if (c.Note.Length > 0) w.WriteString("note", c.Note);
            w.WriteEndObject();
        }
        static LoadoutCtx ReadCtx(JsonElement o)
        {
            var c = new LoadoutCtx { Mode = S(o, "mode", "Normal"), Difficulty = I(o, "diff", 1), Aware = B(o, "aware", true), Economy = D(o, "econ", 1), Survival = D(o, "surv", 1), Control = D(o, "ctrl", 1),
                Cash = D(o, "cash", 0.3), Elite = D(o, "elite", 0.15), Boss = D(o, "boss", 0.06), LevelUps = D(o, "lvl", 50), SpecialAt = I(o, "special", 10), Farming = I(o, "farm"),
                Caution = I(o, "caution", 1), Timing = I(o, "timing", 1), TagPlan = S(o, "plan", "Auto"), Note = S(o, "note") };
            ModeKind kind; c.Kind = Enum.TryParse(S(o, "kind"), out kind) ? kind : ModeKind.Timed;
            foreach (var z in ReadStrings(o, "zero")) c.Zero.Add(z);
            return c;
        }

        /// <summary>The `[loadout] input #N {json}` line: everything the advice depended on (canonical: levels by id, notes not part of
        /// it) plus, when given, what it advised ("out") for the replay to compare.</summary>
        public static string InputJson(LoadoutInput inp, LoadoutAdvice outcome = null)
        {
            return Json(w =>
            {
                w.WriteStartObject();
                w.WriteNumber("v", inp.Version);
                if (outcome != null) w.WriteNumber("n", outcome.Number);
                w.WriteString("inv", inp.Inventory ?? ""); w.WriteString("kh", inp.KnowledgeHash ?? "");
                w.WriteString("leader", inp.Leader);
                if (inp.Build == null) w.WriteNull("build");
                else
                {
                    var b = inp.Build;
                    w.WriteStartObject("build");
                    w.WriteString("id", b.Id); w.WriteString("name", b.Name); w.WriteString("pack", b.Pack); w.WriteString("branch", b.Branch); w.WriteString("style", b.Style);
                    Strings(w, "wants", b.Wants); Strings(w, "badges", b.Badges); Strings(w, "skip", b.Skip);
                    w.WriteEndObject();
                }
                w.WriteString("mode", inp.Mode); w.WriteNumber("diff", inp.Difficulty);
                w.WriteStartObject("doctrine");
                w.WriteNumber("goal", inp.Farming); w.WriteNumber("caution", inp.Caution); w.WriteNumber("timing", inp.Timing); w.WriteBoolean("modeAware", inp.ModeAware);
                w.WriteString("tagPlan", inp.TagPlan); w.WriteString("style", inp.Style);
                w.WriteEndObject();
                w.WriteNumber("slots", inp.Slots);
                Ints(w, "forced", inp.Forced); Ints(w, "equipped", inp.Equipped);
                w.WriteStartObject("levels"); foreach (var kv in inp.Levels.OrderBy(x => x.Key)) w.WriteNumber(kv.Key.ToString(IC), kv.Value); w.WriteEndObject();
                Ints(w, "closed", inp.RankOpen.Where(x => !x.Value).Select(x => x.Key).OrderBy(x => x));
                Ints(w, "offGrid", inp.OffGrid.OrderBy(x => x));
                Strings(w, "recruits", inp.Recruits);
                WriteShape(w, inp.Shape); WriteCtx(w, inp.Ctx);
                if (outcome != null)
                {
                    w.WriteStartObject("out");
                    w.WriteStartArray("equip"); foreach (var p in outcome.Picks) { w.WriteStartArray(); w.WriteNumberValue(p.Badge.Id); w.WriteNumberValue(R2(p.Score)); w.WriteEndArray(); } w.WriteEndArray();
                    w.WriteStartArray("next"); foreach (var r in outcome.Next) { w.WriteStartArray(); w.WriteNumberValue(r.Badge.Id); w.WriteNumberValue(R2(r.Score)); w.WriteEndArray(); } w.WriteEndArray();
                    Ints(w, "unrated", outcome.Unrated.Select(r => r.Badge.Id));
                    w.WriteEndObject();
                }
                w.WriteEndObject();
            });
        }

        /// <summary>The input of an `input #N` line; <paramref name="logged"/> = its "out" part: (id, score) of the advice as it was.</summary>
        public static LoadoutInput ReadInput(string json, out List<KeyValuePair<int, double>> logged)
        {
            logged = new List<KeyValuePair<int, double>>();
            var inp = new LoadoutInput();
            using (var doc = JsonDocument.Parse(json))
            {
                var o = doc.RootElement; JsonElement e;
                inp.Version = I(o, "v", 1); inp.Inventory = S(o, "inv"); inp.KnowledgeHash = S(o, "kh"); inp.Leader = S(o, "leader");
                if (o.TryGetProperty("build", out e) && e.ValueKind == JsonValueKind.Object)
                {
                    inp.Build = new LoadoutBuild { Id = S(e, "id"), Name = S(e, "name"), Pack = S(e, "pack"), Branch = S(e, "branch"), Style = S(e, "style") };
                    inp.Build.Wants.AddRange(ReadStrings(e, "wants")); inp.Build.Badges.AddRange(ReadStrings(e, "badges")); inp.Build.Skip.AddRange(ReadStrings(e, "skip"));
                }
                inp.Mode = S(o, "mode", "Normal"); inp.Difficulty = I(o, "diff", 1);
                if (o.TryGetProperty("doctrine", out e))
                {
                    inp.Farming = I(e, "goal"); inp.Caution = I(e, "caution", 1); inp.Timing = I(e, "timing", 1); inp.ModeAware = B(e, "modeAware", true);
                    inp.TagPlan = S(e, "tagPlan", "Auto"); inp.Style = S(e, "style", "Balanced");
                }
                inp.Slots = I(o, "slots", 4);
                inp.Forced.AddRange(ReadInts(o, "forced")); inp.Equipped.AddRange(ReadInts(o, "equipped"));
                if (o.TryGetProperty("levels", out e)) foreach (var p in e.EnumerateObject()) inp.Levels[int.Parse(p.Name, IC)] = p.Value.GetInt32();
                foreach (var id in ReadInts(o, "closed")) inp.RankOpen[id] = false;
                foreach (var id in ReadInts(o, "offGrid")) inp.OffGrid.Add(id);
                inp.Recruits.AddRange(ReadStrings(o, "recruits"));
                if (o.TryGetProperty("shape", out e)) inp.Shape = ReadShape(e);
                if (o.TryGetProperty("ctx", out e)) inp.Ctx = ReadCtx(e);
                if (o.TryGetProperty("out", out e) && e.TryGetProperty("equip", out var eq))
                    foreach (var x in eq.EnumerateArray()) { var two = x.EnumerateArray().ToList(); logged.Add(new KeyValuePair<int, double>(two[0].GetInt32(), two[1].GetDouble())); }
            }
            return inp;
        }

        // ------------------------------------------------------------------------------------------------ log lines (the UI writes them with "[loadout] ")
        static string F2(double x) { return Fixed(x, 2); }
        static string Ids(IEnumerable<int> ids) { var l = ids.ToList(); return l.Count == 0 ? "-" : string.Join(",", l); }

        /// <summary>"shape #7: deals Kinetic 92%, Explosive 8% | wpn 65 / abl 35 | crit 1.0 | deploy 85% | projected Kinetic 20 | econ x0.80 ..."</summary>
        public static string ShapeLine(LoadoutAdvice a)
        {
            var s = a.Input.Shape; var c = a.Input.Ctx;
            string deals = string.Join(", ", s.Dealt().Select(x => x.Key + " " + Pct(x.Value * 100) + "%"));
            string proj = string.Join(", ", s.ProjectedList().Select(x => x.Key + " " + x.Value));
            return "shape #" + a.Number + ": deals " + (deals.Length > 0 ? deals : "-") + " | wpn " + Pct(s.WeaponShare * 100) + " / abl " + Pct(s.AbilityShare * 100)
                + " | crit " + Fixed(s.Crit, 1) + " | deploy " + Pct(s.Deploy * 100) + "% | projected " + (proj.Length > 0 ? proj : "-")
                + " | econ x" + F2(c.Economy) + " surv x" + F2(c.Survival) + " cash x" + F2(c.Cash) + " elites " + Pct(c.Elite * 100) + "% bosses " + Pct(c.Boss * 100) + "%"
                + " | level-ups ~" + ((int)c.LevelUps) + " | recruits " + (s.Recruits.Count > 0 ? string.Join("+", s.Recruits) : "-");
        }

        static readonly string[] GoalNames = { "WinTheRun", "Balanced", "FarmProgress" };
        public static string GoalName(int farming) { return GoalNames[Math.Max(0, Math.Min(2, farming))]; }

        /// <summary>"advise #7: SWAT 'Rifleman' Normal d2 WinTheRun | 4 slots, forced -, pins - | EQUIP 1 Gunner L2 17.15, ... | next ... | never ... | not rated none"</summary>
        public static string AdviseLine(LoadoutAdvice a)
        {
            var inp = a.Input;
            string build = inp.Build == null ? "Auto" : "'" + inp.Build.Name + "'" + (inp.Build.Pack.Length > 0 ? " (pack " + inp.Build.Pack + ")" : "");
            string forced = a.Picks.Any(p => p.Forced) ? string.Join(", ", a.Picks.Where(p => p.Forced).Select(p => p.Badge.Short)) : "-";
            string pins = inp.Build != null && inp.Build.Badges.Count > 0 ? string.Join(", ", inp.Build.Badges) : "-";
            string equip = a.Picks.Count == 0 ? "none" : string.Join(", ", a.Picks.Select(p => (p.Forced ? "Q" : p.Rank.ToString(IC)) + " " + p.Badge.Short + " L" + Math.Max(p.Level, 1) + " " + F2(p.Score)
                + (p.Source == "pin" ? " (pin)" : "") + (p.Close ? " (close: " + p.CloseTo.Short + ")" : "")));
            string next = a.Next.Count == 0 ? "-" : string.Join(", ", a.Next.Select(r => r.Badge.Short + " " + F2(r.Score)));
            string never = a.Never.Count == 0 ? "-" : string.Join(", ", a.Never.Select(r => r.Badge.Short));
            string unrated = a.Unrated.Count == 0 ? "none" : string.Join(", ", a.Unrated.Select(r => r.Badge.Id + " " + r.Badge.Short));
            return "advise #" + a.Number + ": " + (inp.Leader.Length > 0 ? inp.Leader : "?") + " " + build + " " + inp.Mode + " d" + inp.Difficulty + " " + GoalName(inp.Farming)
                + " | " + inp.Slots + " slots, forced " + forced + ", pins " + pins + " | EQUIP " + equip + " | next " + next + " | never " + never + " | not rated " + unrated
                + (a.Notes.Count > 0 ? " | note: " + string.Join("; ", a.Notes) : "");
        }

        /// <summary>"why #7 1 Gunner: +20% Kinetic, 2 pts - Kinetic is 92% of your damage", one per pick.</summary>
        public static List<string> WhyLines(LoadoutAdvice a)
        {
            return a.Picks.Select(p => "why #" + a.Number + " " + (p.Forced ? "Q" : p.Rank.ToString(IC)) + " " + p.Badge.Short + ": " + p.Effect + " - " + p.Why + (p.Why2.Length > 0 ? " (" + p.Why2 + ")" : "")).ToList();
        }

        /// <summary>"  #7 16 Gunner L2 = 17.15 [score]: +20% Kinetic 13.88 | 2 Kinetic points 2.78 | the Kinetic special 2 picks sooner 0.50", one per pick.</summary>
        public static List<string> TermLines(LoadoutAdvice a)
        {
            return a.Picks.Select(p => "  #" + a.Number + " " + p.Badge.Id + " " + p.Badge.Short + " L" + Math.Max(p.Level, 1) + " = " + F2(p.Score) + " [" + p.Source + "]: "
                + string.Join(" | ", p.Terms.Where(t => Math.Abs(t.Points) > 0.004).OrderByDescending(t => t.Points).Select(t => t.Text + " " + F2(t.Points)))).ToList();
        }

        /// <summary>"hint #7: level Gunner 2>3 (3 SWAT points) +8.33 | unlock -"</summary>
        public static string HintLine(LoadoutAdvice a)
        {
            string level = a.Level == null ? "-" : a.Level.Badge.Short + " " + a.Level.From + ">" + a.Level.To + " (" + a.Level.Cost + " " + a.Level.Badge.Owner + " points) +" + F2(a.Level.Gain);
            var u = a.Unlock;
            string unlock = u == null ? "-" : u.Row.Badge.Short + " (" + u.Row.Badge.Owner + " tree, rank " + u.Row.Badge.Rank + ") " + (u.Pinned ? "pinned but locked" : F2(u.Row.Score) + " at L1, would be #" + u.WouldBe);
            return "hint #" + a.Number + ": level " + level + " | unlock " + unlock;
        }
    }
}
