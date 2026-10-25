// 0.17.0 (C16-09): the replay that decides the survive quest's caution - 'ItemBench --replay-caution <companion.log> [more logs] [--data
// <gamedata.json>] [--probe <probe.json>]'. A quest that asks to survive the run on Hardcore or difficulty 3+ weighs survival x k from 30 % of the
// run (RunContext.QuestCautionOn); this replays the logged hard-mode offers with that factor and says whether the #1 card would have changed:
//  1. every [offer] block (LevelUp / Chest / Military / Hashtag; the rescue screen left out) with its [ctx] line (mode, 'dN', 'm:ss left' or
//     'open-ended', 'survival xS', ', health N%' since 0.16.0), its [squad] / [tags] lines when written and its [card] lines; a block repeated
//     across files (the same screen, clock, mode, cards and scores) is folded;
//  2. the hard set (QuestRules.Hard) and the control set (every other offer: the gate keeps the factor off there - printed as a check);
//  3. each offer as a RunContext (mode, difficulty, the clock, the [Advice] Caution factor found from the logged survival - 0.6 / 1.0 / 1.4 -,
//     the logged health, or the hurting term inferred from the survival when no health was logged: 'approximate'), and the same with
//     QuestCaution = k: S and S'(k) come from RunContext.Survival itself, so the replay applies the real gate;
//  4. the survival part of each card re-scored with S'(k): survival stat cards (Max Health / Armor / Health Regen) rebuilt as Synergy.StatCard
//     (rarity, Knowledge.MilitaryStat x S x 1.2 when a build wants it, team) and checked against the logged score (more than 0.02 apart: the
//     card keeps its logged score and is counted; Endless cards skipped - their rarity weight changed in 0.16.0), then scaled by S'/S; Dodge
//     Chance / Movement Speed by max(Control, min(1.3, S')) / max(Control, min(1.3, S)); items by ItemRules.Evaluate under S' minus under S (the
//     terms that do not read Survival cancel); healing abilities +0.4 when S < 1.3 <= S'; healing evolutions +0.3 x (S' - S) (the healing flag
//     from probe.json);
//  5. per k in 1.10 / 1.15 / 1.20 / 1.30: the hard set's #1 changes by screen and phase, each one written out, the counts skipped and approximate,
//     and the verdict (Decide): fidelity first (95 % of the rebuilt stat cards within 0.02, and at k = 1.00 every #1 as logged); k = 1.15 is adopted
//     when 0.5 % - 8 % of the hard set's #1 change and none displaces a card of the build's own order ('#<n> in <build>'); if 1.15 failed only on
//     the upper bound or the build order, 1.10 under the same test; otherwise 'drop' (QuestRules.SurviveCaution = 1.0: the QUEST row alone). Every
//     change must move to a survival card (a replay bug otherwise: no verdict).
// Read-only over the logs; the bench's own cases (QuestCases Q7) run Decide over a made-up fixture, never a real log.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class CautionReplay
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static string F2(double v) { return v.ToString("0.00", IC); }
        public static readonly double[] Ks = { 1.10, 1.15, 1.20, 1.30 };
        public const double LowShare = 0.005, HighShare = 0.08, Fidelity = 0.95;

        sealed class CardRow { public int Rank; public string Name, Kind, Owner, Why; public double Score; }
        sealed class Offer
        {
            public string Screen, Clock, Mode = "?", File; public int Line, D = 1; public double Left = -1, S = double.NaN, Health = double.NaN;
            public readonly List<string> Squad = new List<string>(); public readonly Dictionary<string, int> Points = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly List<CardRow> Cards = new List<CardRow>();
            public double Seconds { get { var p = Clock.Split(':'); int m, s; return p.Length == 2 && int.TryParse(p[0], out m) && int.TryParse(p[1], out s) ? m * 60 + s : 0; } }
        }

        /// <summary>One k's outcome over the hard set (what Decide reads).</summary>
        internal sealed class KRow
        {
            public double K; public int Offers, Changes;
            public bool BuildOrder;                 // a change displaced a card of the build's own order ('#<n> in <build>')
            public bool NonSurvival;                // a change moved to a card that is no survival card (a replay bug)
            public double Share { get { return Offers > 0 ? (double)Changes / Offers : 0; } }
        }

        /// <summary>The verdict (pure; the bench's Q7 runs it over a made-up fixture): fidelity first, then k = 1.15 inside 0.5 % - 8 % with no
        /// build-order card displaced, else 1.10 when 1.15 failed only on the upper bound or the build order, else drop.</summary>
        internal static string Decide(double fidelity, bool k1Same, IList<KRow> rows)
        {
            if (fidelity < Fidelity || !k1Same) return "caution replay: no verdict (fidelity " + (fidelity * 100).ToString("0.#", IC) + " %" + (k1Same ? "" : ", k = 1.00 moved a #1") + ")";
            if (rows.Any(r => r.NonSurvival)) return "caution replay: no verdict (a change to a card that is no survival card - a replay bug)";
            var r115 = rows.FirstOrDefault(r => Math.Abs(r.K - 1.15) < 1e-9); var r110 = rows.FirstOrDefault(r => Math.Abs(r.K - 1.10) < 1e-9);
            if (r115 == null || r110 == null) return "caution replay: no verdict (k = 1.10 / 1.15 not replayed)";
            Func<KRow, bool> low = r => r.Share >= LowShare - 1e-12, high = r => r.Share <= HighShare + 1e-12;
            if (low(r115) && high(r115) && !r115.BuildOrder) return "caution replay: adopt k=1.15 from " + QuestRules.SurviveFrom.ToString("0.0", IC);
            if (low(r115) && (!high(r115) || r115.BuildOrder) && low(r110) && high(r110) && !r110.BuildOrder) return "caution replay: adopt k=1.10 from " + QuestRules.SurviveFrom.ToString("0.0", IC);
            return "caution replay: drop";
        }

        // ---------------------------------------------------------------- the command
        public static int Command(string[] args, int at)
        {
            var logs = new List<string>(); string data = null, probe = null;
            for (int i = at + 1; i < args.Length; i++)
            {
                if (args[i] == "--data" && i + 1 < args.Length) { data = args[++i]; continue; }
                if (args[i] == "--probe" && i + 1 < args.Length) { probe = args[++i]; continue; }
                if (!args[i].StartsWith("--", StringComparison.Ordinal)) logs.Add(args[i]);
            }
            if (logs.Count == 0) { Console.Error.WriteLine("usage: ItemBench --replay-caution <companion.log> [more logs] [--data <gamedata.json>] [--probe <probe.json>]"); return 2; }
            data = data ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "data", "gamedata.json"));
            probe = probe ?? Path.Combine(Path.GetDirectoryName(data) ?? ".", "probe.json");
            var desc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(data))
                using (var doc = JsonDocument.Parse(File.ReadAllText(data)))
                    foreach (var it in doc.RootElement.GetProperty("items").EnumerateArray()) desc[it.GetProperty("name").GetString()] = it.GetProperty("desc").GetString();
            else Console.WriteLine("(no gamedata.json at " + data + ": items keep their logged scores)");
            var itemHeal = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase); var itemStats = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var powerHeal = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase); var evoBase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(probe))
                using (var doc = JsonDocument.Parse(File.ReadAllText(probe)))
                {
                    foreach (var it in doc.RootElement.GetProperty("items").EnumerateArray())
                    {
                        string n = Str(it, "name"); if (n == null) continue;
                        itemHeal[n] = Bool(it, "healing");
                        var st = new List<string>(); JsonElement e; if (it.TryGetProperty("stats", out e) && e.ValueKind == JsonValueKind.Array) foreach (var v in e.EnumerateArray()) if (v.ValueKind == JsonValueKind.String) st.Add(v.GetString());
                        itemStats[n] = st;
                    }
                    foreach (var p in doc.RootElement.GetProperty("powerups").EnumerateArray())
                    {
                        string n = Str(p, "name"); if (n == null) continue;
                        powerHeal[n] = Bool(p, "healing"); string b = Str(p, "evolutionOf"); if (!string.IsNullOrEmpty(b)) evoBase[n] = b;
                    }
                }
            else Console.WriteLine("(no probe.json at " + probe + ": healing abilities and evolutions counted as approximate)");
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            Knowledge.Current = k;

            var offers = new List<Offer>(); var seen = new HashSet<string>(); int folded = 0, sos = 0, noCtx = 0;
            foreach (var f in logs)
            {
                if (!File.Exists(f)) { Console.WriteLine("(not found: " + f + ")"); continue; }
                foreach (var o in Parse(f))
                {
                    if (o.Screen == "SOS") { sos++; continue; }
                    if (double.IsNaN(o.S) || o.Cards.Count == 0) { noCtx++; continue; }
                    string key = o.Screen + "|" + o.Clock + "|" + o.Mode + "|" + string.Join(",", o.Cards.Select(c => c.Name + " " + F2(c.Score)));
                    if (!seen.Add(key)) { folded++; continue; }
                    offers.Add(o);
                }
            }
            var hard = offers.Where(o => QuestRules.Hard(o.Mode, o.D)).ToList();
            var control = offers.Where(o => !QuestRules.Hard(o.Mode, o.D)).ToList();
            Console.WriteLine("caution replay over " + logs.Count + " log(s): " + offers.Count + " offers (" + folded + " repeated blocks folded, " + sos + " rescue screens and " + noCtx + " without a [ctx] line left out)");
            Console.WriteLine("  hard set (Hardcore or difficulty 3+, One Hit excluded): " + hard.Count + " offers - " + string.Join(", ", hard.GroupBy(o => o.Mode + " d" + o.D).Select(g => g.Key + " " + g.Count()))
                + "; by screen " + string.Join(", ", hard.GroupBy(o => o.Screen).Select(g => g.Key + " " + g.Count())));
            Console.WriteLine("  control set: " + control.Count + " offers");

            // fidelity: every non-Endless survival stat card of the hard set rebuilt from its logged survival
            int statAll = 0, statOk = 0, endless = 0, approxHurt = 0, approxHeal = 0; var misses = new List<string>();
            foreach (var o in hard)
            {
                var ctx = Ctx(o, 1.0); if (Hurt(o)) approxHurt++;
                foreach (var c in o.Cards.Where(x => x.Kind == "stat"))
                {
                    var st = Stat(c); if (st == null || !st.Survival) continue;
                    if (st.Rarity == "Endless") { endless++; continue; }
                    statAll++;
                    double rebuilt = StatScore(st, k, ctx.Survival, ctx.Control);
                    if (Math.Abs(rebuilt - c.Score) <= 0.02 + 1e-9) statOk++; else misses.Add(o.Screen + " " + o.Clock + " " + c.Name + " logged " + F2(c.Score) + " rebuilt " + F2(rebuilt) + " (S " + F2(o.S) + ")");
                }
            }
            double fidelity = statAll == 0 ? 1.0 : (double)statOk / statAll;
            Console.WriteLine("  fidelity: " + statOk + " of " + statAll + " survival stat cards rebuilt within 0.02 (" + (fidelity * 100).ToString("0.#", IC) + " %); Endless cards skipped: " + endless
                + "; offers with the hurting term inferred (no health logged): " + approxHurt);
            foreach (var m in misses.Take(8)) Console.WriteLine("      apart: " + m);

            // k = 1.00: every #1 as logged (the re-ranking itself)
            bool k1Same = hard.All(o => NewFirst(o, 1.0, k, desc, itemHeal, itemStats, powerHeal, evoBase, ref approxHeal).Name == First(o).Name);
            Console.WriteLine("  k = 1.00: every #1 as logged - " + (k1Same ? "yes" : "NO"));

            var rows = new List<KRow>();
            foreach (double kk in Ks)
            {
                var row = new KRow { K = kk, Offers = hard.Count }; var lines = new List<string>(); var by = new Dictionary<string, int>();
                int heal = 0;
                foreach (var o in hard)
                {
                    var was = First(o); var now = NewFirst(o, kk, k, desc, itemHeal, itemStats, powerHeal, evoBase, ref heal);
                    if (now.Name == was.Name) continue;
                    row.Changes++;
                    var ctx = Ctx(o, 1.0); string phase = ctx.Phase;
                    string g = o.Screen + " " + phase; by[g] = by.TryGetValue(g, out int n0) ? n0 + 1 : 1;
                    if (System.Text.RegularExpressions.Regex.IsMatch(was.Why ?? "", @"#\d+ in ")) row.BuildOrder = true;
                    if (!SurvivalCard(now, powerHeal, evoBase, desc)) row.NonSurvival = true;
                    lines.Add(o.Screen + " " + o.Clock + " " + o.Mode + " d" + o.D + " (" + (int)Math.Round(ctx.QuestProgress * 100) + "% of the run): " + was.Name + " (" + F2(was.Score) + ", " + Cut(was.Why) + ") -> "
                        + now.Name + " (" + now.Kind + ", " + F2(now.Rescored) + ")");
                }
                rows.Add(row);
                Console.WriteLine("  k=" + kk.ToString("0.00", IC) + ": #1 changes on " + row.Changes + " of " + row.Offers + " (" + (row.Share * 100).ToString("0.0", IC) + " %)" + (by.Count > 0 ? " - " + string.Join(", ", by.Select(x => x.Key + " " + x.Value)) : "")
                    + (row.BuildOrder ? "; a build-order card displaced" : "") + (row.NonSurvival ? "; a change to a card that is no survival card" : "") + (heal > 0 ? "; " + heal + " ability card(s) without a healing flag (approximate)" : ""));
                foreach (var l in lines) Console.WriteLine("      " + l);
            }
            // the control set: the gate keeps the factor off (a check of the gate, not of the advice)
            int ctrlMoved = 0, h0 = 0; foreach (var o in control) if (NewFirst(o, 1.30, k, desc, itemHeal, itemStats, powerHeal, evoBase, ref h0).Name != First(o).Name) ctrlMoved++;
            Console.WriteLine("  control set at k=1.30: " + ctrlMoved + " #1 changes of " + control.Count + " (the gate keeps the factor off outside the hard modes)");
            string verdict = Decide(fidelity, k1Same, rows);
            Console.WriteLine(verdict);
            return 0;
        }

        // ---------------------------------------------------------------- parsing
        static readonly Regex OfferRx = new Regex(@"\[offer\] (\w+) (\d+:\d\d) \((\w+) horde");
        static readonly Regex CtxRx = new Regex(@"\[ctx\] (\w+) d(\d), (?:(\d+):(\d\d) left|[^,]*), \w+ \(economy x[\d.]+, survival x(\d+\.\d+)");
        static readonly Regex HealthRx = new Regex(@", health (\d+)%");
        static readonly Regex BuildsRx = new Regex(@"\| builds: (.*?)(?: \| |$)");
        static readonly Regex CardRx = new Regex(@"\[card\] #(\d+) (?:PICK )?\s*(.+?) \((\w+)(?:, (\w+))?\) (-?\d+\.\d+)(?: - (.*))?$");
        static readonly Regex PointRx = new Regex(@"(\w+) (\d+)/\d+");
        static readonly string[] Classes = { "Tank", "SWAT", "Engineer", "Huntress", "Ghost", "Medic", "Pyro", "Mechanic", "Ranger" };

        static IEnumerable<Offer> Parse(string file)
        {
            Offer cur = null; int n = 0;
            foreach (var l in File.ReadLines(file))
            {
                n++;
                var m = OfferRx.Match(l);
                if (m.Success) { if (cur != null) yield return cur; cur = new Offer { Screen = m.Groups[1].Value, Clock = m.Groups[2].Value, Mode = m.Groups[3].Value, File = Path.GetFileName(file), Line = n }; continue; }
                if (cur == null) continue;
                if (l.Contains("session start ====")) { yield return cur; cur = null; continue; }
                var c = CtxRx.Match(l);
                if (c.Success && double.IsNaN(cur.S))
                {
                    cur.Mode = c.Groups[1].Value; cur.D = int.Parse(c.Groups[2].Value, IC); cur.S = double.Parse(c.Groups[5].Value, IC);
                    if (c.Groups[3].Success) cur.Left = int.Parse(c.Groups[3].Value, IC) * 60 + int.Parse(c.Groups[4].Value, IC);
                    var h = HealthRx.Match(l); if (h.Success) cur.Health = int.Parse(h.Groups[1].Value, IC) / 100.0;
                    var b = BuildsRx.Match(l);
                    if (b.Success && cur.Squad.Count == 0) foreach (var part in b.Groups[1].Value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)) { string cls = part.Split(' ')[0]; if (Classes.Contains(cls) && !cur.Squad.Contains(cls)) cur.Squad.Add(cls); }
                    continue;
                }
                int sq = l.IndexOf("[squad] ", StringComparison.Ordinal);
                if (sq >= 0 && !l.Contains("action hint") && !l.Contains("reroll hint"))
                {
                    var list = new List<string>();
                    foreach (var seg in l.Substring(sq + 8).Split('|')) { string cls = seg.Trim().Split(' ')[0].TrimEnd('*'); if (Classes.Contains(cls)) list.Add(cls); }
                    if (list.Count > 0) { cur.Squad.Clear(); cur.Squad.AddRange(list); }
                    continue;
                }
                int tg = l.IndexOf("[tags] points: ", StringComparison.Ordinal);
                if (tg >= 0) { string pts = l.Substring(tg + 15); int bar = pts.IndexOf('|'); if (bar >= 0) pts = pts.Substring(0, bar); foreach (Match p in PointRx.Matches(pts)) cur.Points[p.Groups[1].Value] = int.Parse(p.Groups[2].Value, IC); continue; }
                var k = CardRx.Match(l);
                if (k.Success) cur.Cards.Add(new CardRow { Rank = int.Parse(k.Groups[1].Value, IC), Name = k.Groups[2].Value.Trim(), Kind = k.Groups[3].Value, Owner = k.Groups[4].Success ? k.Groups[4].Value : null,
                    Score = double.Parse(k.Groups[5].Value, IC), Why = k.Groups[6].Success ? k.Groups[6].Value : "" });
            }
            if (cur != null) yield return cur;
        }
        static string Str(JsonElement e, string p) { JsonElement v; return e.TryGetProperty(p, out v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null; }
        static bool Bool(JsonElement e, string p) { JsonElement v; return e.TryGetProperty(p, out v) && (v.ValueKind == JsonValueKind.True); }
        static string Cut(string s) { s = (s ?? "").Split(new[] { "; " }, StringSplitOptions.None)[0]; return s.Length > 60 ? s.Substring(0, 57) + "..." : s; }

        // ---------------------------------------------------------------- the run context of an offer
        static double Goal(string mode) { return mode == "Hardcore" || mode == "BossRush" ? 600 : mode == "OneHit" ? 300 : 1200; }
        // the [Advice] Caution factor that reproduces the logged survival at the logged health (or full health), else 1.0
        static int CautionOf(Offer o)
        {
            foreach (int c in new[] { 1, 0, 2 })
            {
                var x = Base(o, c); x.Health = double.IsNaN(o.Health) ? 1 : o.Health;
                if (Math.Abs(x.Survival - o.S) <= 0.02) return c;
            }
            return 1;           // none reproduces it: a squad that was hurting with no health logged (the hurting term is inferred under 1.0)
        }
        static RunContext Base(Offer o, int caution)
        {
            var d = new Doctrine { Caution = caution };
            double goal = Goal(o.Mode);
            var x = new RunContext { Mode = o.Mode, Difficulty = o.D, D = d };
            if (o.Left >= 0) { x.Goal = goal; x.Seconds = Math.Max(0, goal - o.Left); }
            else { x.Goal = o.Mode == "Endless" || o.Mode == "Infinite" ? 0 : goal; x.Seconds = o.Seconds; }
            return x;
        }
        // the offer's context with QuestCaution = k: the logged health, else the hurting term inferred from the logged survival
        static RunContext Ctx(Offer o, double k)
        {
            int c = CautionOf(o);
            var x = Base(o, c);
            if (!double.IsNaN(o.Health)) x.Health = o.Health;
            else
            {
                x.Health = 1;
                double cf = c == 0 ? 0.6 : c == 2 ? 1.4 : 1.0, hurt = o.S / cf - x.Survival / cf;
                if (hurt > 0.01) x.Health = Math.Max(0, Math.Min(0.4999, (1 - hurt / 0.4) / 2));
            }
            x.QuestCaution = k;
            return x;
        }
        static bool Hurt(Offer o) { if (!double.IsNaN(o.Health)) return false; var x = Ctx(o, 1.0); return x.Health < 1; }

        // ---------------------------------------------------------------- the cards
        sealed class StatRow { public string Rarity, Key; public bool Team, Wants, Survival, Move; }
        static readonly Regex StatRx = new Regex(@"^(Common|Rare|Legendary|Endless)(, team-wide)?: ([A-Za-z ]+?)(?: - (.*))?$");
        static StatRow Stat(CardRow c)
        {
            string first = (c.Why ?? "").Split(new[] { "; " }, StringSplitOptions.None)[0];
            var m = StatRx.Match(first); if (!m.Success) return null;
            string shown = m.Groups[3].Value.Trim(); string key = null;
            switch (shown)
            {
                case "Max Health": key = "MaxHealth"; break;
                case "Armor": key = "Armor"; break;
                case "Health Regen": key = "HPRegen"; break;
                case "Dodge Chance": key = "DodgeChance"; break;
                case "Movement Speed": key = "MovementSpeedMod"; break;
            }
            if (key == null) return new StatRow { Rarity = m.Groups[1].Value };
            return new StatRow { Rarity = m.Groups[1].Value, Key = key, Team = m.Groups[2].Success, Wants = (m.Groups[4].Value ?? "").Contains(" wants it"), Survival = key == "MaxHealth" || key == "Armor" || key == "HPRegen",
                Move = key == "DodgeChance" || key == "MovementSpeedMod" };
        }
        // as Ranker.ScoreMilitary builds it (the weight by the asset's key, x Survival, x 1.2 when a build wants it; Rare / Legendary 2 / 3 as the logs
        // of 0.14 - 0.15 had them)
        static double StatScore(StatRow st, Knowledge k, double survival, double control)
        {
            double w = 0.5; foreach (var kv in k.MilitaryStat) if (string.Equals(kv.Key, st.Key, StringComparison.OrdinalIgnoreCase)) w = kv.Value;
            if (st.Survival) w *= Math.Max(0, survival); else if (st.Move) w *= Math.Max(control, Math.Min(1.3, survival));
            if (st.Wants && st.Survival) w *= 1.2;
            return Synergy.StatCard(Synergy.RarityWeight(st.Rarity, double.NaN), w, st.Team);
        }

        sealed class Scored { public string Name, Kind, Why; public double Score, Rescored; public int Rank; }
        static Scored First(Offer o) { var c = o.Cards.OrderBy(x => x.Rank).First(); return new Scored { Name = c.Name, Kind = c.Kind, Why = c.Why, Score = c.Score, Rescored = c.Score, Rank = c.Rank }; }

        // the offer's #1 with every card's survival part re-scored under S'(k) (the gate applies: RunContext.QuestCautionOn)
        static Scored NewFirst(Offer o, double kk, Knowledge k, Dictionary<string, string> desc, Dictionary<string, bool> itemHeal, Dictionary<string, List<string>> itemStats,
            Dictionary<string, bool> powerHeal, Dictionary<string, string> evoBase, ref int noFlag)
        {
            var c1 = Ctx(o, 1.0); var ck = Ctx(o, kk);
            double s = c1.Survival, s2 = ck.Survival;
            var list = new List<Scored>();
            foreach (var c in o.Cards)
            {
                double v = c.Score;
                if (Math.Abs(s2 - s) > 1e-9)
                {
                    if (c.Kind == "stat")
                    {
                        var st = Stat(c);
                        if (st != null && st.Rarity != "Endless" && (st.Survival || st.Move))
                        {
                            double rebuilt = StatScore(st, k, s, c1.Control);
                            if (Math.Abs(rebuilt - c.Score) <= 0.02 + 1e-9) v = StatScore(st, k, s2, ck.Control) + (c.Score - rebuilt);
                        }
                    }
                    else if (c.Kind == "item")
                    {
                        string d; if (desc.TryGetValue(c.Name, out d)) v = c.Score + Item(c.Name, d, o, ck, k, itemHeal, itemStats) - Item(c.Name, d, o, c1, k, itemHeal, itemStats);
                    }
                    else if (c.Kind == "ability")
                    {
                        bool h; if (!powerHeal.TryGetValue(c.Name, out h)) { noFlag++; h = false; }
                        if (h && s < 1.3 && s2 >= 1.3) v += 0.4;
                    }
                    else if (c.Kind == "evolution")
                    {
                        bool h, bh = false; string b;
                        if (!powerHeal.TryGetValue(c.Name, out h)) { noFlag++; h = false; }
                        if (evoBase.TryGetValue(c.Name, out b)) powerHeal.TryGetValue(b, out bh);
                        if (h && !bh) v += 0.3 * (s2 - s);
                    }
                }
                list.Add(new Scored { Name = c.Name, Kind = c.Kind, Why = c.Why, Score = c.Score, Rescored = v, Rank = c.Rank });
            }
            return list.OrderByDescending(x => Math.Round(x.Rescored, 2)).ThenBy(x => x.Rank).First();
        }

        static double Item(string name, string d, Offer o, RunContext ctx, Knowledge k, Dictionary<string, bool> itemHeal, Dictionary<string, List<string>> itemStats)
        {
            var tags = new TagProfile(); foreach (var p in o.Points) tags.Points[p.Key] = p.Value;
            var squad = o.Squad.Count > 0 ? (IList<string>)o.Squad : new List<string>();
            bool h; itemHeal.TryGetValue(name, out h); List<string> st; itemStats.TryGetValue(name, out st);
            var c = new ItemContext { Squad = squad, Tags = tags, K = k, Ctx = ctx, Healing = h, Stats = st };
            c.CritSquad = squad.Any(x => k.CritSquad.Contains(x, StringComparer.OrdinalIgnoreCase));
            c.ShieldOwned = squad.Contains("Engineer", StringComparer.OrdinalIgnoreCase);
            try { return ItemRules.Evaluate(name, d, c, new List<string>()); } catch { return 0; }
        }

        // a card the factor can move: a survival stat, an item the book or the keywords weigh by survival, a healing ability or evolution
        static bool SurvivalCard(Scored c, Dictionary<string, bool> powerHeal, Dictionary<string, string> evoBase, Dictionary<string, string> desc)
        {
            if (c.Kind == "stat") { var st = Stat(new CardRow { Why = c.Why }); return st != null && (st.Survival || st.Move); }
            if (c.Kind == "item") return Math.Abs(c.Rescored - c.Score) > 1e-9;
            if (c.Kind == "ability" || c.Kind == "evolution") { bool h; return powerHeal.TryGetValue(c.Name, out h) && h; }
            return false;
        }
    }
}
