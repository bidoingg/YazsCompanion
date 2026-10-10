// 0.15.0 (C15-11): a companion.log's health, PASS or FAIL per check - the live series and a real session end with it, and the
// release notes quote it:
//   ItemBench --check-log <companion.log> [--since last|all|<session stamp prefix>] [--wide on|off] [--expect kind,kind]
//   hooks     every session logs '[hooks] N methods patched (class by class; 0 patch classes failed)'
//   why       every session logs '[why] hooks: ...' with every hook ok
//   wide      at least one '[wide] ... frame off' while WideMenus is on and the screen is not 16:9 (16:9: nothing to hide)
//   warnings  no '[Warning]' line
//   errors    no '[Error]' / '[Fatal]' line
//   shown     every [shown] text within the Wording rails, with the room it had as drawn (beside its "2ND" / "AVOID")
//   items     0.16.0 (C16-02b): every chest offer has its '[items] held: ...' line before the next offer, and no
//             '[items] no rule for '<name>'' line names an item (the game's 'Powerups/...' duplicates aside). A session is
//             checked when its load line names 0.16.0 or later, or when it holds an '[items] ' or '[held] ' line (tags no
//             build before 0.16.0 writes): a build of the 0.16.0 tree whose VERSION still says 0.15.0 is checked too
// --since: 'last' (the default: the newest session of the file), 'all', or a session stamp prefix: '2026-10-06' or
// '2026-10-06 19:02' selects every session that started then or later. --wide overrides what the cfg next to the log says
// (BepInEx\config\bidoi.yazs.companion.cfg two folders up from the log; WideMenus = Everywhere when there is none); a
// '[config] General.WideMenus = ... (saved)' line of the menu counts as well.
// 0.16.0 (C16-11e): after the checks, INFO sections - the proofs a log may hold, not checks: per kind 'seen N, first at
// HH:MM:SS: <text>' or 'not in this log', each kind's pattern anchored on the line's message (after '[Info] '):
//   real-session proofs  first-input (prints the source: pad / key / mouse / touch), yard (a Training Yard purchase or
//                        refund), equip (EQUIP ADVICE / UNDO pressed - prints how: touch / mouse / key; a line of a build
//                        before 0.16.0 has no source and counts apart), ribbon (the badge ribbon stepped aside)
//   quest proofs         quest-story (a '[quest] ... -> story objective ...' step)
//   walk proofs          side-panel (a WHY panel in a side wing), results (a results-screen shot step), recruit-tour (the
//                        pause walk's hover tour over cards 2 to 4 of 4)
// --expect kind,kind turns those kinds into checks: FAIL when the kind is not in the log (equip: when no line says how it
// was pressed). An unknown kind exits 2 with the list.
// Exit 0 when every check passes, 1 when one fails, 2 when the file or a session is missing (or --expect names an unknown
// kind). Without --expect the exit codes are 0.15.0's.
// LogCheck.Cases() is its bench part: made-up logs (the game's names only) through every check, passing and failing.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class LogCheck
    {
        sealed class Session
        {
            public string Stamp; public readonly List<string> Lines = new List<string>();
            Version _v; bool _read;
            /// <summary>The build of the session, from its load line ('YAZS Companion 0.15.0 (abc1234) loaded'); null: not logged.</summary>
            public Version Build
            {
                get
                {
                    if (_read) return _v;
                    _read = true;
                    foreach (var l in Lines) { var m = RxLoaded.Match(l); if (m.Success) { Version.TryParse(m.Groups[1].Value, out _v); break; } }
                    return _v;
                }
            }
            /// <summary>The session's build has a feature of <paramref name="since"/> (a build not logged counts as current).</summary>
            public bool Has(Version since) { return Build == null || Build >= since; }
            bool? _items016;
            /// <summary>The session's build writes the 0.16.0 '[items] held:' lines: its load line names 0.16.0 or later, or (a build of the
            /// 0.16.0 tree whose VERSION is not bumped yet: 'YAZS Companion 0.15.0 (abc1234-dirty) loaded') it wrote an '[items] ' or
            /// '[held] ' line - tags no build before 0.16.0 writes (ItemStats' 'item slot reads' line comes once a session, before the
            /// first chest offer).</summary>
            public bool Has016
            {
                get
                {
                    if (_items016.HasValue) return _items016.Value;
                    bool has = Has(V016);
                    if (!has)
                        foreach (var l in Lines)
                        {
                            var m = RxLine.Match(l);
                            if (m.Success && (m.Groups[2].Value.StartsWith("[items] ", StringComparison.Ordinal) || m.Groups[2].Value.StartsWith("[held] ", StringComparison.Ordinal))) { has = true; break; }
                        }
                    _items016 = has;
                    return has;
                }
            }
        }
        internal sealed class Result { public string Id; public bool Pass; public string Text; public readonly List<string> Detail = new List<string>(); }

        static readonly Regex RxSession = new Regex(@"^==== (\d{4}-\d\d-\d\d \d\d:\d\d:\d\d) session start ====");
        static readonly Regex RxLine = new Regex(@"^\d\d:\d\d:\d\d(?:\.\d+)? \[(\w+)\s*\] (.*)$");
        static readonly Regex RxHooks = new Regex(@"^\[hooks\] (\d+) methods patched \(class by class; (\d+) patch class(?:es)? failed\)");
        static readonly Regex RxWhyHooks = new Regex(@"^\[why\] hooks: (.*)$");
        static readonly Regex RxScreen = new Regex(@"^\[(?:wide|badge|panel)\] .*?screen (\d+)x(\d+)");
        static readonly Regex RxWideCfg = new Regex(@"^\[config\] General\.WideMenus = (\w+)");
        static readonly Regex RxOffer = new Regex(@"^\[offer\] ");
        static readonly Regex RxCard = new Regex(@"^\[card\] #(\d+) (?:PICK|    ) .+? (-?\d+\.\d\d) - ");
        static readonly Regex RxShown = new Regex(@"^\[shown\] (\w+) (\d+:\d\d): (.*)$");
        static readonly Regex RxText = new Regex(@"^(\d+) '(.*)'$");
        static readonly Regex RxLoaded = new Regex(@"\] YAZS Companion (\d+\.\d+\.\d+)\S* (?:\([^)]*\) )?loaded");
        // 0.16.0 (C16-02b): a chest offer as Advisor.Offer writes it ('[offer] Chest 01:20 (Normal horde 2)', a reroll's 'replaced ...'
        // tail too - not the '[offer] Chest 01:20: no active cards' line, which returns before the [items] line), its '[items] held:'
        // line, and the item book's fallback line (a name may hold an apostrophe: the last quote before ' - ' ends it)
        static readonly Regex RxOfferChest = new Regex(@"^\[offer\] Chest \S+ \(");
        static readonly Regex RxItemsHeld = new Regex(@"^\[items\] held: ");
        static readonly Regex RxNoRule = new Regex(@"^\[items\] no rule for '(.*)'(?: - .*)?$");
        static readonly Regex RxClock = new Regex(@"^(\d\d:\d\d:\d\d)");
        static readonly Version V013 = new Version(0, 13, 0), V014 = new Version(0, 14, 0);   // the class-by-class hooks line; the WHY band and the wide menus
        static readonly Version V016 = new Version(0, 16, 0);                                 // the '[items] held:' line of every chest offer

        // ---------------------------------------------------------------- 0.16.0 (C16-11e): the proofs a log may hold (INFO, not checks)
        internal sealed class ProofKind
        {
            public readonly string Id, Group; public readonly Regex Rx; public readonly bool Source;
            public ProofKind(string id, string group, string rx, bool source = false) { Id = id; Group = group; Rx = new Regex(rx); Source = source; }
        }
        /// <summary>The kinds, in the order the INFO sections print them. Each pattern is anchored on the line's message (RxLine's group 2),
        /// so a line of another tag that quotes the same words does not count. A kind with a source prints it (its group 1).</summary>
        internal static readonly ProofKind[] Kinds =
        {
            new ProofKind("first-input", "real-session proofs", @"^\[menu\] first input this session: (pad|key|mouse|touch)$", source: true),
            new ProofKind("yard", "real-session proofs", @"^\[yard\] (bought|refunded) "),
            new ProofKind("equip", "real-session proofs", @"^\[loadout\] equip: (?:EQUIP ADVICE \(advice #\d+\)|UNDO) pressed(?: by (touch|mouse|key))? - ", source: true),
            new ProofKind("ribbon", "real-session proofs", @"^\[badge\] ribbon stepped aside "),
            // Quest.cs writes the quest's key, or 'the quest' when it has none
            new ProofKind("quest-story", "quest proofs", @"^\[quest\] (?:\S+|the quest) -> story objective "),
            new ProofKind("side-panel", "walk proofs", @"^\[why\] \w+ .*\(side wing (left|right), header \+ \d+ lines?, "),
            new ProofKind("results", "walk proofs", @"^\[shot\] results step [12] set up "),
            new ProofKind("recruit-tour", "walk proofs", @"^\[menu\] pause walk: hover tour - cards 2 to 4 of 4 "),
        };
        static string KindList() { return string.Join(", ", Kinds.Select(k => k.Id)); }

        /// <summary>What the log holds of one kind: the lines seen (for a kind with a source: the lines that name one), the first of them,
        /// the sources by count (first seen first) and, for 'equip', the lines of a build before 0.16.0 (no source).</summary>
        internal sealed class Proof
        {
            public ProofKind Kind; public int Seen, NoSource; public string FirstAt, FirstText;
            public readonly List<KeyValuePair<string, int>> Sources = new List<KeyValuePair<string, int>>();
            public bool Proven { get { return Seen > 0; } }
            public void AddSource(string src)
            {
                int i = Sources.FindIndex(kv => kv.Key == src);
                if (i < 0) Sources.Add(new KeyValuePair<string, int>(src, 1)); else Sources[i] = new KeyValuePair<string, int>(src, Sources[i].Value + 1);
            }
            public string Text
            {
                get
                {
                    const string old = " without a source (a build before 0.16.0)";
                    if (Seen == 0) return NoSource > 0 ? "seen " + NoSource + old : "not in this log";
                    string t = "seen " + Seen + ", first at " + FirstAt + (string.IsNullOrEmpty(FirstText) ? "" : ": " + FirstText);
                    if (Kind.Source && Sources.Count > 1) t += " (" + string.Join(", ", Sources.Select(kv => kv.Key + " " + kv.Value)) + ")";
                    if (NoSource > 0) t += "; " + NoSource + old;
                    return t;
                }
            }
        }

        /// <summary>The INFO sections' content over the selected sessions (with several sessions, 'first at' carries the day).</summary>
        static List<Proof> Proofs(List<Session> sessions)
        {
            var list = Kinds.Select(k => new Proof { Kind = k }).ToList();
            foreach (var s in sessions)
                foreach (var l in s.Lines)
                {
                    var lm = RxLine.Match(l); if (!lm.Success) continue;
                    string msg = lm.Groups[2].Value;
                    foreach (var p in list)
                    {
                        var m = p.Kind.Rx.Match(msg); if (!m.Success) continue;
                        string src = p.Kind.Source && m.Groups[1].Success ? m.Groups[1].Value : null;
                        if (p.Kind.Source && src == null) { p.NoSource++; continue; }     // an EQUIP line of a build before 0.16.0
                        p.Seen++;
                        if (src != null) p.AddSource(src);
                        if (p.FirstAt != null) continue;
                        var cm = RxClock.Match(l);
                        string day = sessions.Count > 1 && !s.Stamp.StartsWith("(") ? s.Stamp.Substring(0, 10) + " " : "";
                        p.FirstAt = day + (cm.Success ? cm.Groups[1].Value : "?");
                        p.FirstText = src ?? (msg.Length > 90 ? msg.Substring(0, 87) + "..." : msg);
                    }
                }
            return list;
        }

        /// <summary>'--expect a,b' -> the kinds (lower case, in the order given, once each); null and <paramref name="error"/> set when
        /// the list is empty or names a kind there is none of.</summary>
        internal static List<string> ParseExpect(string arg, out string error)
        {
            error = null;
            var kinds = new List<string>(); var unknown = new List<string>();
            foreach (var raw in (arg ?? "").Split(','))
            {
                string k = raw.Trim().ToLowerInvariant(); if (k.Length == 0) continue;
                if (!Kinds.Any(x => x.Id == k)) { unknown.Add(raw.Trim()); continue; }
                if (!kinds.Contains(k)) kinds.Add(k);
            }
            if (unknown.Count > 0) error = "check-log: --expect: unknown kind" + (unknown.Count == 1 ? "" : "s") + " '" + string.Join("', '", unknown) + "' - the kinds: " + KindList();
            else if (kinds.Count == 0) error = "check-log: --expect takes a list of kinds (kind,kind) - the kinds: " + KindList();
            return error == null ? kinds : null;
        }

        /// <summary>The kinds --expect names as checks: PASS when the log holds one (equip: one that says how it was pressed).</summary>
        internal static List<Result> Expect(List<Proof> proofs, List<string> kinds)
        {
            var list = new List<Result>();
            foreach (var k in kinds)
            {
                var p = proofs.First(x => x.Kind.Id == k);
                var r = new Result { Id = "expect " + k, Pass = p.Proven, Text = p.Text };
                if (!r.Pass) r.Text = k == "equip" && p.NoSource > 0 ? p.Text + " - none says how it was pressed" : "expected, and not in this log";
                list.Add(r);
            }
            return list;
        }

        static string Older(int n, string build) { return n == 0 ? "" : "; " + Count(n, "session") + " of a build before " + build + " not checked"; }
        static string OnlyOlder(int n, string build) { return "not checked - " + (n == 1 ? "the session is" : "every session is") + " of a build before " + build; }

        // ---------------------------------------------------------------- the command
        public static int Command(string[] args, int at) { return Command(args, at, Console.Out); }

        /// <summary>The command, printing to <paramref name="o"/> (the bench's L13 reads it from a StringWriter).</summary>
        internal static int Command(string[] args, int at, TextWriter o)
        {
            string file = at + 1 < args.Length && !args[at + 1].StartsWith("--") ? args[at + 1] : null;
            string since = Arg(args, "--since") ?? "last";
            string wideArg = Arg(args, "--wide");
            if (file == null) { o.WriteLine("usage: ItemBench --check-log <companion.log> [--since last|all|<stamp>] [--wide on|off] [--expect kind,kind]"); return 2; }
            // 0.16.0 (C16-11e): --expect kind,kind - the proof kinds that must be in the log (an unknown kind: exit 2 with the list)
            List<string> expect = null;
            if (Array.IndexOf(args, "--expect") >= 0)
            {
                string ea = Arg(args, "--expect"), err = null;
                if (ea != null && !ea.StartsWith("--")) expect = ParseExpect(ea, out err);
                else err = "check-log: --expect takes a list of kinds (kind,kind) - the kinds: " + KindList();
                if (expect == null) { o.WriteLine(err); return 2; }
            }
            if (!File.Exists(file)) { o.WriteLine("check-log: no file " + file); return 2; }
            bool? wide = null;
            if (wideArg != null)
            {
                if (wideArg.Equals("on", StringComparison.OrdinalIgnoreCase)) wide = true;
                else if (wideArg.Equals("off", StringComparison.OrdinalIgnoreCase)) wide = false;
                else { o.WriteLine("check-log: --wide takes on or off, not '" + wideArg + "'"); return 2; }
            }
            string wideFrom = wide.HasValue ? "--wide " + wideArg : null;
            if (!wide.HasValue)
            {
                string cfg = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".", "..", "..", "config", "bidoi.yazs.companion.cfg"));
                string mode = CfgWide(cfg);
                if (mode != null) { wide = !mode.Equals("Off", StringComparison.OrdinalIgnoreCase); wideFrom = "WideMenus = " + mode + " in the cfg"; }
            }
            // the game may hold the log open for writing: read it shared
            var lines = new List<string>();
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var rd = new StreamReader(fs))
                for (string l; (l = rd.ReadLine()) != null;) lines.Add(l);
            var sessions = Select(Sessions(lines), since);
            if (sessions.Count == 0) { o.WriteLine("check-log: no session in " + file + " matches --since " + since); return 2; }
            o.WriteLine("check-log " + file + ": " + Count(sessions.Count, "session") + " ("
                + (sessions.Count == 1 ? sessions[0].Stamp : sessions[0].Stamp + " .. " + sessions[sessions.Count - 1].Stamp) + "), "
                + sessions.Sum(s => s.Lines.Count) + " lines");
            var results = Run(sessions, wide, wideFrom);
            var proofs = Proofs(sessions);
            if (expect != null) results.AddRange(Expect(proofs, expect));      // 0.16.0 (C16-11e): the kinds asked for, as checks
            Print(results, proofs, o);
            return results.All(r => r.Pass) ? 0 : 1;
        }

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static string Count(int n, string what) { return n + " " + what + (n == 1 ? "" : "s"); }

        /// <summary>WideMenus as the cfg file says (null: no file, or no such line).</summary>
        internal static string CfgWide(string cfg)
        {
            try
            {
                if (!File.Exists(cfg)) return null;
                bool general = false;
                foreach (var raw in File.ReadAllLines(cfg))
                {
                    string l = raw.Trim();
                    if (l.StartsWith("[")) { general = l.Equals("[General]", StringComparison.OrdinalIgnoreCase); continue; }
                    if (!general || l.StartsWith("#")) continue;
                    var m = Regex.Match(l, @"^WideMenus\s*=\s*(\w+)");
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            catch { }
            return null;
        }

        static List<Session> Sessions(IEnumerable<string> lines)
        {
            var list = new List<Session>(); Session cur = null;
            foreach (var l in lines)
            {
                var m = RxSession.Match(l);
                if (m.Success) { cur = new Session { Stamp = m.Groups[1].Value }; list.Add(cur); continue; }
                if (cur == null) { cur = new Session { Stamp = "(before the first session header)" }; list.Add(cur); }
                cur.Lines.Add(l);
            }
            return list;
        }

        static List<Session> Select(List<Session> all, string since)
        {
            if (since == null || since.Equals("last", StringComparison.OrdinalIgnoreCase)) return all.Count == 0 ? all : all.GetRange(all.Count - 1, 1);
            if (since.Equals("all", StringComparison.OrdinalIgnoreCase)) return all;
            return all.Where(s => !s.Stamp.StartsWith("(") && string.CompareOrdinal(s.Stamp, since) >= 0).ToList();
        }

        static void Print(List<Result> results, List<Proof> proofs, TextWriter o)
        {
            foreach (var r in results)
            {
                o.WriteLine("  " + (r.Pass ? "PASS" : "FAIL") + "  " + r.Id + ": " + r.Text);
                foreach (var d in r.Detail.Take(8)) o.WriteLine("        " + (d.Length > 220 ? d.Substring(0, 220) + "..." : d));
                if (r.Detail.Count > 8) o.WriteLine("        ... " + (r.Detail.Count - 8) + " more");
            }
            // 0.16.0 (C16-11e): the INFO sections - what the log proves, not checks ('--expect kind,kind' makes kinds checks)
            if (proofs != null)
                foreach (var g in proofs.GroupBy(p => p.Kind.Group))
                {
                    o.WriteLine("  INFO  " + g.Key + " (not checks)");
                    foreach (var p in g) o.WriteLine("        " + p.Kind.Id + ": " + p.Text);
                }
            int failed = results.Count(r => !r.Pass);
            o.WriteLine("check-log: " + (failed == 0 ? "all as wanted" : failed + " check" + (failed == 1 ? "" : "s") + " not as wanted"));
        }

        // ---------------------------------------------------------------- the checks
        /// <summary>The seven checks over the sessions (0.16.0: 'items'). <paramref name="wide"/>: WideMenus on / off as known from outside the
        /// log (null: not known - the default Everywhere, unless a [config] line of the log says otherwise).</summary>
        static List<Result> Run(List<Session> sessions, bool? wide, string wideFrom)
        {
            var results = new List<Result>();

            // hooks: every session patched every class
            var hooks = new Result { Id = "hooks" };
            int patchedMin = int.MaxValue, patchedMax = 0, okSessions = 0, hooksOld = 0;
            foreach (var s in sessions)
            {
                if (!s.Has(V013)) { hooksOld++; continue; }
                Match m = null;
                foreach (var l in s.Lines) { var lm = RxLine.Match(l); if (!lm.Success) continue; var hm = RxHooks.Match(lm.Groups[2].Value); if (hm.Success) { m = hm; break; } }
                if (m == null) { hooks.Detail.Add(s.Stamp + ": no '[hooks] ... patch classes failed' line (the plugin never loaded, or a build before 0.13.0)"); continue; }
                int n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), failed = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                patchedMin = Math.Min(patchedMin, n); patchedMax = Math.Max(patchedMax, n);
                if (failed > 0)
                {
                    hooks.Detail.Add(s.Stamp + ": " + failed + " patch class" + (failed == 1 ? "" : "es") + " failed");
                    foreach (var l in s.Lines) if (l.Contains("[hooks] ") && l.Contains(" not patched: ")) hooks.Detail.Add("  " + Msg(l));
                }
                else okSessions++;
            }
            hooks.Pass = hooks.Detail.Count == 0;
            hooks.Text = hooks.Pass
                ? (okSessions == 0 ? OnlyOlder(hooksOld, "0.13.0") : (patchedMin == patchedMax ? patchedMax.ToString(CultureInfo.InvariantCulture) : patchedMin + "-" + patchedMax) + " methods patched, 0 patch classes failed (" + okSessions + " of " + Count(sessions.Count, "session") + ")" + Older(hooksOld, "0.13.0"))
                : okSessions + " of " + Count(sessions.Count - hooksOld, "session") + " patched every class" + Older(hooksOld, "0.13.0");
            results.Add(hooks);

            // why: the WHY band's hooks all took
            var why = new Result { Id = "why hooks" }; int whyOk = 0, whyOld = 0;
            foreach (var s in sessions)
            {
                if (!s.Has(V014)) { whyOld++; continue; }
                string body = null;
                foreach (var l in s.Lines) { var lm = RxLine.Match(l); if (!lm.Success) continue; var wm = RxWhyHooks.Match(lm.Groups[2].Value); if (wm.Success) { body = wm.Groups[1].Value; break; } }
                if (body == null) { why.Detail.Add(s.Stamp + ": no '[why] hooks: ...' line (a build before 0.14.0, or the check threw)"); continue; }
                var bad = WhyHooksBad(body);
                if (bad.Count > 0) why.Detail.Add(s.Stamp + ": " + string.Join(", ", bad) + " - '" + body + "'");
                else whyOk++;
            }
            why.Pass = why.Detail.Count == 0;
            why.Text = why.Pass ? (whyOk == 0 ? OnlyOlder(whyOld, "0.14.0") : "every OnSelected / OnDeselected hook ok (" + whyOk + " of " + Count(sessions.Count, "session") + ")" + Older(whyOld, "0.14.0"))
                : whyOk + " of " + Count(sessions.Count - whyOld, "session") + " with every hook ok" + Older(whyOld, "0.14.0");
            results.Add(why);

            // wide: a non-16:9 screen with WideMenus on hides the game's frame at least once
            var wideR = new Result { Id = "wide" };
            int frameOff = 0, restored = 0, wideOld = 0; string screen = null; var wideScreens = new List<string>(); string cfgLine = null; bool cfgOn = false;
            foreach (var s in sessions)
            {
                if (!s.Has(V014)) { wideOld++; continue; }
                foreach (var l in s.Lines)
                {
                    var lm = RxLine.Match(l); if (!lm.Success) continue;
                    string msg = lm.Groups[2].Value;
                    if (msg.StartsWith("[wide] ") && msg.Contains(": frame off")) frameOff++;
                    if (msg.StartsWith("[wide] ") && msg.Contains("restored (")) restored++;
                    var cm = RxWideCfg.Match(msg);
                    if (cm.Success) { cfgLine = cm.Groups[1].Value; if (!cfgLine.Equals("Off", StringComparison.OrdinalIgnoreCase)) cfgOn = true; }
                    var sm = RxScreen.Match(msg);
                    if (sm.Success)
                    {
                        int w = int.Parse(sm.Groups[1].Value, CultureInfo.InvariantCulture), h = int.Parse(sm.Groups[2].Value, CultureInfo.InvariantCulture);
                        string wh = w + "x" + h;
                        if (screen == null) screen = wh;
                        if (h > 0 && Math.Abs(w / (double)h - 16.0 / 9.0) >= 0.01 && !wideScreens.Contains(wh)) wideScreens.Add(wh);
                    }
                }
            }
            // who says WideMenus is on: --wide first, then the menu's [config] lines in the session, then the cfg, then the default
            bool on; string from;
            if (wide.HasValue && wideFrom != null && wideFrom.StartsWith("--wide", StringComparison.Ordinal)) { on = wide.Value; from = wideFrom; }
            else if (cfgLine != null) { on = cfgOn; from = "the menu set WideMenus = " + cfgLine; }
            else if (wide.HasValue) { on = wide.Value; from = wideFrom ?? (on ? "WideMenus on" : "WideMenus off"); }
            else { on = true; from = "WideMenus = Everywhere, the default (no cfg found)"; }
            if (!on) { wideR.Pass = true; wideR.Text = "not checked - WideMenus off (" + from + ")" + (frameOff > 0 ? "; " + frameOff + " frame off line(s) anyway" : "") + Older(wideOld, "0.14.0"); }
            else if (wideScreens.Count == 0)
            {
                wideR.Pass = true;
                wideR.Text = wideOld == sessions.Count ? OnlyOlder(wideOld, "0.14.0")
                    : "not checked - " + (screen != null ? "a 16:9 screen (" + screen + "): the game draws no frame to hide" : "no screen size in the log (no menu or run reached)") + Older(wideOld, "0.14.0");
            }
            else
            {
                wideR.Pass = frameOff > 0;
                wideR.Text = frameOff + " '[wide] ... frame off' line(s)" + (restored > 0 ? ", " + restored + " restored" : "") + " (" + from + "; screen " + string.Join(", ", wideScreens) + ")"
                    + (wideR.Pass ? "" : " - the frame was never hidden") + Older(wideOld, "0.14.0");
            }
            results.Add(wideR);

            // warnings / errors
            var warn = new Result { Id = "warnings" }; var err = new Result { Id = "errors" };
            foreach (var s in sessions)
                foreach (var l in s.Lines)
                {
                    var lm = RxLine.Match(l); if (!lm.Success) continue;
                    string lv = lm.Groups[1].Value;
                    string at = sessions.Count > 1 && !s.Stamp.StartsWith("(") ? s.Stamp.Substring(0, 10) + " " : "";      // the day, when several sessions
                    if (lv == "Warning") warn.Detail.Add(at + l);
                    else if (lv == "Error" || lv == "Fatal") err.Detail.Add(at + l);
                }
            warn.Pass = warn.Detail.Count == 0; warn.Text = warn.Detail.Count + " [Warning] line" + (warn.Detail.Count == 1 ? "" : "s");
            err.Pass = err.Detail.Count == 0; err.Text = err.Detail.Count + " [Error] / [Fatal] line" + (err.Detail.Count == 1 ? "" : "s");
            results.Add(warn); results.Add(err);

            // shown: the card texts as drawn, within the rails and the room beside their prefix
            var shown = new Result { Id = "shown" };
            int shownLines = 0, texts = 0; string longest = "";
            foreach (var s in sessions)
            {
                var score = new Dictionary<int, double>();
                foreach (var l in s.Lines)
                {
                    var lm = RxLine.Match(l); if (!lm.Success) continue;
                    string msg = lm.Groups[2].Value;
                    if (RxOffer.IsMatch(msg)) { score.Clear(); continue; }
                    var cm = RxCard.Match(msg);
                    if (cm.Success) { score[int.Parse(cm.Groups[1].Value, CultureInfo.InvariantCulture)] = double.Parse(cm.Groups[2].Value, CultureInfo.InvariantCulture); continue; }
                    var sm = RxShown.Match(msg);
                    if (!sm.Success) continue;
                    shownLines++;
                    foreach (var part in Texts(sm.Groups[3].Value))
                    {
                        var tm = RxText.Match(part);
                        if (!tm.Success) { shown.Detail.Add(sm.Groups[1].Value + " " + sm.Groups[2].Value + ": not read: " + part); continue; }
                        texts++;
                        int rank = int.Parse(tm.Groups[1].Value, CultureInfo.InvariantCulture);
                        string text = tm.Groups[2].Value;
                        double sc; bool known = score.TryGetValue(rank, out sc);
                        int room = rank == 1 || !known ? Wording.Budget : Wording.RoomBeside(Synergy.ReasonPrefixWidth(rank, sc));
                        var bad = Wording.Rails(text, room);
                        if (text.StartsWith("(not read: ", StringComparison.Ordinal)) bad.Add("the card's text was not read");
                        if (bad.Count > 0) shown.Detail.Add(sm.Groups[1].Value + " " + sm.Groups[2].Value + " #" + rank + " '" + text + "' (" + string.Join(", ", bad) + (room != Wording.Budget ? "; room " + room : "") + ")");
                        else if (text.Length > longest.Length) longest = text;
                    }
                }
            }
            shown.Pass = shown.Detail.Count == 0;
            shown.Text = shownLines == 0 ? "no [shown] line (no selection screen, or ShowBadges off)"
                : shown.Pass ? shownLines + " [shown] line" + (shownLines == 1 ? "" : "s") + ", " + texts + " card texts within the Wording rails (longest " + longest.Length + " characters)"
                : shown.Detail.Count + " of " + texts + " card texts break the Wording rails";
            results.Add(shown);

            // items (0.16.0, C16-02b): every chest offer has its '[items] held:' line before the next offer, and the item book has a
            // row for every item a chest showed ('[items] no rule for' names only the game's unlocalized 'Powerups/...' duplicates)
            var items = new Result { Id = "items" };
            int chests = 0, heldMissing = 0, noRule = 0, itemsOld = 0, itemsNew = 0;
            foreach (var s in sessions)
            {
                if (!s.Has016) { itemsOld++; continue; }        // the version, or an [items] / [held] line (a 0.16.0 tree not bumped yet)
                itemsNew++;
                string at = sessions.Count > 1 && !s.Stamp.StartsWith("(") ? s.Stamp.Substring(0, 10) + " " : "";
                string open = null;                 // the chest offer still waiting for its [items] held line
                foreach (var l in s.Lines)
                {
                    var lm = RxLine.Match(l); if (!lm.Success) continue;
                    string msg = lm.Groups[2].Value;
                    if (RxOffer.IsMatch(msg))
                    {
                        if (open != null) { heldMissing++; items.Detail.Add(at + open + " - no '[items] held:' line before the next offer"); }
                        open = RxOfferChest.IsMatch(msg) ? l : null;
                        if (open != null) chests++;
                        continue;
                    }
                    if (open != null && RxItemsHeld.IsMatch(msg)) { open = null; continue; }
                    var nm = RxNoRule.Match(msg);
                    if (nm.Success && !nm.Groups[1].Value.StartsWith("Powerups/", StringComparison.Ordinal)) { noRule++; items.Detail.Add(at + l + " - the item book has no row for it"); }
                }
                if (open != null) { heldMissing++; items.Detail.Add(at + open + " - no '[items] held:' line (the session ends)"); }
            }
            items.Pass = items.Detail.Count == 0;
            items.Text = itemsNew == 0 ? OnlyOlder(itemsOld, "0.16.0")
                : items.Pass ? (chests == 0 ? "no chest offer to check" : Count(chests, "chest offer") + ", each with its [items] held line") + "; every item has a rule" + Older(itemsOld, "0.16.0")
                : (heldMissing > 0 ? heldMissing + " of " + Count(chests, "chest offer") + " without their [items] held line" : "every chest offer with its [items] held line")
                    + (noRule > 0 ? "; " + Count(noRule, "item") + " without a rule" : "; every item has a rule") + Older(itemsOld, "0.16.0");
            results.Add(items);
            return results;
        }

        static string Msg(string line) { var m = RxLine.Match(line); return m.Success ? m.Groups[2].Value : line; }

        /// <summary>'OnSelected Hashtag ok, Item ok | OnDeselected (one body for every card class) ok' -> what did not take.</summary>
        internal static List<string> WhyHooksBad(string body)
        {
            var bad = new List<string>();
            var halves = body.Split(new[] { " | " }, StringSplitOptions.None);
            if (halves.Length < 2) bad.Add("no OnDeselected part");
            foreach (var half in halves)
            {
                string h = half.Trim();
                if (h.StartsWith("OnSelected ", StringComparison.Ordinal))
                {
                    string list = h.Substring("OnSelected ".Length);
                    if (list == "none found") { bad.Add("OnSelected: none found"); continue; }
                    foreach (var part in list.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                        if (!part.EndsWith(" ok", StringComparison.Ordinal)) bad.Add("OnSelected " + part);
                }
                else if (h.StartsWith("OnDeselected", StringComparison.Ordinal)) { if (!h.EndsWith(" ok", StringComparison.Ordinal)) bad.Add(h); }
                else bad.Add("'" + h + "'");
            }
            return bad;
        }

        /// <summary>"1 'a' | 2 'it's b'" -> "1 'a'", "2 'it's b'" (a text may hold an apostrophe; the rails keep '|' out of it).</summary>
        static IEnumerable<string> Texts(string body)
        {
            return Regex.Split(body, @"(?<=') \| (?=\d+ ')");
        }

        // ---------------------------------------------------------------- the bench part
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }

        static readonly string Healthy = Lf(@"==== 2030-01-02 10:00:00 session start ====
10:00:01.000 [Info] [hooks] 38 methods patched (class by class; 0 patch classes failed)
10:00:01.001 [Info] YAZS Companion 9.9.9 (abc1234) loaded from X; 38 methods patched; badges on
10:00:01.002 [Info] [why] hooks: OnSelected Hashtag ok, Item ok, Military ok, Skill ok, SOS ok | OnDeselected (one body for every card class) ok
10:00:05.000 [Info] [wide] main menu: screen 3440x1440, canvas 5161x2160 (21:9, 440 px each side): frame off; grown +1321 x +0 units: backdrop
10:01:10.000 [Info] [offer] LevelUp 00:30 (Normal horde 1)
10:01:10.001 [Info] [card] #1 PICK Medical Drone (ability, Medic) 4.84 - new ability - #2 in Rifleman
10:01:10.002 [Info] [card] #2      Handgun (weapon, Medic) 3.72 - weapon level: 1 to 2 of 4
10:01:10.003 [Info] [card] #3      Potato (item) 0.60 - economy
10:01:10.004 [Info] [shown] LevelUp 00:30: 1 'The Rifleman build's main ability' | 2 'Level 2 of 4 - this build levels abilities first' | 3 'More XP and luck over the run'
10:01:13.000 [Info] [pick] LevelUp 00:30: Medical Drone (#1, the pick)
10:02:00.000 [Info] [offer] Chest 01:20 (Normal horde 2)
10:02:00.001 [Info] [items] held: none | free slots 6 (max - equipped) | luck 0 | pickup 100% | speed 100
10:02:00.002 [Info] [card] #1 PICK Magazine Clip (item) 3.92 - A-tier item, the squad reloads magazines
10:02:00.003 [Info] [card] #2      Pocket Watch (item) 1.50 - timed power-ups last twice as long
10:02:00.004 [Info] [shown] Chest 01:20: 1 'A-tier, the squad reloads magazines' | 2 'Timed power-ups last twice as long'
10:02:03.000 [Info] [pick] ChestOpened 01:20: Magazine Clip (#1, the pick)
");
        // the literal holds the file's own line endings: CRLF where git checks the source out with them (the CI runner), so the
        // cases' line-by-line replaces (ending in a bare LF) would miss - one form for every checkout
        static string Lf(string s) => s.Replace("\r\n", "\n");

        static List<Result> RunText(string log, string since = "last", bool? wide = null, string wideFrom = null)
        {
            var lines = log.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
            return Run(Select(Sessions(lines), since), wide, wideFrom);
        }

        static Result R(List<Result> rs, string id) { return rs.First(r => r.Id == id); }
        // (in words the bench's verdict does not read as a failure: an ok line may say which checks a bad log fails)
        static string Fails(List<Result> rs) { var f = rs.Where(r => !r.Pass).Select(r => r.Id).ToList(); return f.Count == 0 ? "all pass" : "fails " + string.Join(", ", f); }

        public static int Cases()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.15.0: --check-log - the hook lines, the WHY hooks, the wide frame, warnings and errors, the [shown] texts on the rails (C15-11)");
            var ok = RunText(Healthy);
            Check("L1", "a healthy session passes all seven checks", ok.Count == 7 && ok.All(r => r.Pass), Fails(ok) + " | " + R(ok, "shown").Text + " | " + R(ok, "items").Text);

            var hooks = RunText(Healthy.Replace("0 patch classes failed", "1 patch class failed") + "10:00:01.500 [Warning] [hooks] RerollPatch not patched: method not found\n");
            Check("L2", "a failed patch class fails 'hooks' (and its warning 'warnings'), naming the class", !R(hooks, "hooks").Pass && !R(hooks, "warnings").Pass && R(hooks, "hooks").Detail.Any(d => d.Contains("RerollPatch")), Fails(hooks));
            var noHooks = RunText(Healthy.Replace("[hooks] 38 methods patched (class by class; 0 patch classes failed)", "[hooks] (none)"));
            Check("L2", "a session without the hooks line fails 'hooks'", !R(noHooks, "hooks").Pass, R(noHooks, "hooks").Detail.FirstOrDefault());

            var why = RunText(Healthy.Replace("SOS ok |", "SOS not patched |"));
            var why2 = RunText(Healthy.Replace("card class) ok", "card class) missing"));
            Check("L3", "a WHY hook not patched (OnSelected) or missing (OnDeselected) fails 'why hooks'", !R(why, "why hooks").Pass && !R(why2, "why hooks").Pass && R(why, "warnings").Pass,
                string.Join(" / ", R(why, "why hooks").Detail.Concat(R(why2, "why hooks").Detail).Select(d => d.Split(new[] { " - '" }, StringSplitOptions.None)[0])));

            string noFrame = Healthy.Replace("10:00:05.000 [Info] [wide] main menu: screen 3440x1440, canvas 5161x2160 (21:9, 440 px each side): frame off; grown +1321 x +0 units: backdrop\n", "")
                + "10:01:10.010 [Info] [badge] scale s=1.00 px=20.5 (screen 3440x1440, canvas height 2160, card scale 0.77)\n";
            var wideOn = RunText(noFrame);
            var wideOff = RunText(noFrame, wide: false, wideFrom: "WideMenus = Off in the cfg");
            var wideMenu = RunText(noFrame + "10:02:00.000 [Info] [config] General.WideMenus = Off (saved)\n");
            var wide169 = RunText(noFrame.Replace("3440x1440", "1920x1080"));
            Check("L4", "a 21:9 screen with WideMenus on and no 'frame off' fails 'wide'; off (cfg or the menu's [config] line) and a 16:9 screen are not checked",
                !R(wideOn, "wide").Pass && R(wideOff, "wide").Pass && R(wideMenu, "wide").Pass && R(wide169, "wide").Pass && R(ok, "wide").Pass,
                R(wideOn, "wide").Text + " | " + R(wideOff, "wide").Text + " | " + R(wide169, "wide").Text);

            var warn = RunText(Healthy + "10:02:00.000 [Warning] [badge] scale not read\n");
            var err = RunText(Healthy + "10:02:00.000 [Error] [offer] LevelUp failed: NullReferenceException\n");
            Check("L5", "a [Warning] line fails 'warnings', an [Error] line 'errors'", !R(warn, "warnings").Pass && R(warn, "errors").Pass && !R(err, "errors").Pass && R(err, "warnings").Pass, Fails(warn) + " / " + Fails(err));

            var old = RunText(Healthy.Replace("1 'The Rifleman build's main ability'", "1 'focus 2>3 of 4 toward its evolution'"));
            string fifty = "Level 2 of 4 - this build levels abilities first!!";           // 50 visible characters
            var room = RunText(Healthy.Replace("3 'More XP and luck over the run'", "3 '" + fifty.Substring(0, 49) + "'").Replace("1 'The Rifleman build's main ability'", "1 '" + fifty + "'"));
            Check("L6", "an old headline fails 'shown' (its '>', 'focus'); 50 characters pass at #1, 49 fail beside AVOID (room 48)",
                !R(old, "shown").Pass && R(room, "shown").Detail.Count == 1 && R(room, "shown").Detail[0].Contains("#3") && R(room, "shown").Detail[0].Contains("room 48"),
                string.Join(" / ", R(old, "shown").Detail.Concat(R(room, "shown").Detail)));
            Check("L6", "a text with an apostrophe is read whole ('The Rifleman build's main ability'; 3 texts of the level-up + 2 of the chest)", R(ok, "shown").Text.Contains("2 [shown] lines, 5 card texts"), R(ok, "shown").Text);

            string two = Healthy.Replace("[Info] [wide]", "[Warning] [wide]") + Healthy.Replace("2030-01-02 10:00:00", "2030-01-02 11:00:00");
            Check("L7", "--since: 'last' is the newest session, 'all' every one, a stamp prefix the sessions from then on",
                R(RunText(two), "warnings").Pass && !R(RunText(two, "all"), "warnings").Pass && R(RunText(two, "2030-01-02 10:30"), "warnings").Pass && !R(RunText(two, "2030-01-02"), "warnings").Pass,
                R(RunText(two, "all"), "warnings").Text);

            string dir = Path.Combine(Path.GetTempPath(), "yazs_bench_checklog_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "config"));
                string cfg = Path.Combine(dir, "config", "bidoi.yazs.companion.cfg");
                File.WriteAllText(cfg, "[Debug]\nWideMenus = Everywhere\n\n[General]\n## Menus on screens wider or taller than 16:9\n# Setting type: WideMode\nWideMenus = Off\n");
                string got = CfgWide(cfg);
                File.WriteAllText(cfg, "[General]\nPanelSize = 1\n");
                Check("L8", "the cfg's [General] WideMenus is read (another section's key left out); no line: null", got == "Off" && CfgWide(cfg) == null && CfgWide(Path.Combine(dir, "none.cfg")) == null, got ?? "null");
            }
            finally { try { Directory.Delete(dir, true); } catch { } }

            Check("L9", "WhyHooksBad: every part ok -> none; 'none found', a part not ok, no OnDeselected -> named",
                WhyHooksBad("OnSelected Hashtag ok, Item ok | OnDeselected (one body for every card class) ok").Count == 0
                && WhyHooksBad("OnSelected none found | OnDeselected (x) ok").Count == 1 && WhyHooksBad("OnSelected Item not patched | OnDeselected (x) not patched").Count == 2
                && WhyHooksBad("OnSelected Item ok").Count == 1);

            // a 0.13.0 session had no WHY band and no wide menus: those two are not asked of it, its hooks line still is
            string older = noFrame.Replace("YAZS Companion 9.9.9 (abc1234) loaded", "YAZS Companion 0.13.0 loaded")
                .Replace("10:00:01.002 [Info] [why] hooks: OnSelected Hashtag ok, Item ok, Military ok, Skill ok, SOS ok | OnDeselected (one body for every card class) ok\n", "");
            var old13 = RunText(older);
            var old13Bad = RunText(older.Replace("0 patch classes failed", "2 patch classes failed"));
            Check("L10", "a session of 0.13.0: 'why hooks' and 'wide' not checked (no WHY band, no wide menus yet), 'hooks' still checked",
                old13.All(r => r.Pass) && R(old13, "why hooks").Text.Contains("of a build before 0.14.0") && !R(old13Bad, "hooks").Pass,
                R(old13, "why hooks").Text + " | " + R(old13, "wide").Text);

            Console.WriteLine("\n=== 0.16.0: --check-log - the [items] held line of every chest offer (C16-02b); the proofs a log holds: INFO sections, --expect, the EQUIP press's source (C16-11e)");
            // L11: 'items'
            string noHeld = Healthy.Replace("10:02:00.001 [Info] [items] held: none | free slots 6 (max - equipped) | luck 0 | pickup 100% | speed 100\n", "");
            var itemsNoHeld = RunText(noHeld);
            Check("L11", "'items': the healthy log passes - its chest offer has its [items] held line", R(ok, "items").Pass && R(ok, "items").Text.StartsWith("1 chest offer, each with its [items] held line"), R(ok, "items").Text);
            Check("L11", "'items': the chest block without its [items] held line fails 'items' (and nothing else), naming the offer",
                !R(itemsNoHeld, "items").Pass && itemsNoHeld.Count(r => !r.Pass) == 1 && R(itemsNoHeld, "items").Detail.Any(d => d.Contains("[offer] Chest 01:20 (Normal horde 2)")),
                Fails(itemsNoHeld) + " | " + R(itemsNoHeld, "items").Text);
            var itemsWidget = RunText(Healthy + "10:02:00.002 [Info] [items] no rule for 'Bench Widget' - keyword reading\n");
            var itemsPowerups = RunText(Healthy + "10:02:00.002 [Info] [items] no rule for 'Powerups/Item/Potato' - keyword reading\n");
            var itemsQuote = RunText(Healthy + "10:02:00.002 [Info] [items] no rule for 'Schrodinger's Cat' - keyword reading\n");
            Check("L11", "'items': a '[items] no rule for 'Bench Widget'' line fails 'items'; a 'Powerups/...' name passes; a name with an apostrophe is read whole",
                !R(itemsWidget, "items").Pass && R(itemsWidget, "items").Detail.Any(d => d.Contains("'Bench Widget'")) && R(itemsPowerups, "items").Pass
                && !R(itemsQuote, "items").Pass && R(itemsQuote, "items").Text.EndsWith("1 item without a rule"),
                R(itemsWidget, "items").Text + " | " + R(itemsPowerups, "items").Text);
            var items15 = RunText(noHeld.Replace("YAZS Companion 9.9.9 (abc1234) loaded", "YAZS Companion 0.15.0 (abc1234) loaded"));
            Check("L11", "'items': a session of 0.15.0 is not checked (no [items] or [held] line before 0.16.0)", R(items15, "items").Pass && R(items15, "items").Text.Contains("not checked"), R(items15, "items").Text);
            // a build of the 0.16.0 tree whose VERSION still says 0.15.0 (the live rounds before the release's bump): checked by its lines
            const string unbumped = "YAZS Companion 0.15.0 (abc1234-dirty) loaded";
            string slotReads = "10:01:00.000 [Info] [items] item slot reads: team 6 | leader 6 | max 6 - equipped 0\n";
            var items16ok = RunText(Healthy.Replace("YAZS Companion 9.9.9 (abc1234) loaded", unbumped));
            var items16bad = RunText(noHeld.Replace("YAZS Companion 9.9.9 (abc1234) loaded", unbumped).Replace("10:01:10.000 [Info] [offer] LevelUp", slotReads + "10:01:10.000 [Info] [offer] LevelUp"));
            Check("L11", "'items': a 0.16.0 tree still named 0.15.0 ('0.15.0 (abc1234-dirty)') is checked by its [items] lines: it passes with its held line, and fails without it",
                R(items16ok, "items").Pass && R(items16ok, "items").Text.StartsWith("1 chest offer, each with its [items] held line") && !R(items16ok, "items").Text.Contains("not checked")
                && !R(items16bad, "items").Pass && R(items16bad, "items").Detail.Any(d => d.Contains("[offer] Chest 01:20 (Normal horde 2)")),
                R(items16ok, "items").Text + " | " + R(items16bad, "items").Text);
            string rerolled = Healthy.Replace("10:02:03.000 [Info] [pick] ChestOpened", "10:02:01.000 [Info] [offer] Chest 01:20 (Normal horde 2) replaced (reroll): gone Pocket Watch; new Potato\n10:02:03.000 [Info] [pick] ChestOpened");
            string rerolledOk = rerolled.Replace("new Potato\n", "new Potato\n10:02:01.001 [Info] [items] held: none | free slots 6 (max - equipped) | luck 0 | pickup 100% | speed 100\n");
            var noCards = RunText(Healthy + "10:03:00.000 [Info] [offer] Chest 01:40: no active cards\n10:03:01.000 [Info] [offer] LevelUp 01:41 (Normal horde 2)\n");
            Check("L11", "'items': a reroll's replaced chest offer needs its own [items] held line; the 'no active cards' line needs none",
                !R(RunText(rerolled), "items").Pass && R(RunText(rerolledOk), "items").Pass && R(RunText(rerolledOk), "items").Text.StartsWith("2 chest offers") && R(noCards, "items").Pass,
                R(RunText(rerolled), "items").Text + " | " + R(noCards, "items").Text);
            string isSrc = Src("ItemStats.cs"), adSrc = Src("Advisor.cs"), gsSrc = Src("GameState.cs");
            if (isSrc == null || adSrc == null || gsSrc == null) Check("L11", "the sources are readable from the bench", false, "a file is missing next to tools\\ItemBench");
            else
            {
                int chest = adSrc.IndexOf("if (screen == Screen.Chest)", StringComparison.Ordinal);
                Check("L11", "the writer: ItemStats writes '[items] held: ' and '[items] item slot reads: '; Advisor calls HeldLine on chest offers; GameState calls ItemStats.Fill",
                    isSrc.Contains("\"[items] held: \"") && isSrc.Contains("\"[items] item slot reads: team \"") && chest > 0 && adSrc.IndexOf("ItemStats.HeldLine(snap)", chest, StringComparison.Ordinal) > chest
                    && gsSrc.Contains("ItemStats.Fill(master, s)"));
                Check("L11", "the free slots are one read, the game's formula: InternalMaxItems - InternalNumEquippedItems (statistic 50 only logged)",
                    isSrc.Contains("EType.InternalMaxItems") && isSrc.Contains("EType.InternalNumEquippedItems") && isSrc.Contains("Slots(max, equipped)")
                    && isSrc.Contains("ReadsLine(team, leader, max, equippedRaw)"));
            }

            // L12: the INFO sections
            var pr = ProofsText(Healthy + ProofLog);
            var bare = ProofsText(Healthy);
            Check("L12", "INFO: each kind seen, with its first time and its source or line; a log without them: 'not in this log' for every kind",
                Pt(pr, "first-input") == "seen 1, first at 10:03:00: touch" && Pt(pr, "yard") == "seen 2, first at 10:03:01: [yard] bought Weapon Damage 1>2 (advice #1)"
                && Pt(pr, "equip") == "seen 2, first at 10:03:03: touch (touch 1, mouse 1)" && Pt(pr, "ribbon").StartsWith("seen 1, first at 10:03:05: [badge] ribbon stepped aside")
                && Pt(pr, "quest-story").StartsWith("seen 1, first at 10:03:06: [quest] GameHubQuest_Main_06 -> story objective") && Pt(pr, "side-panel").StartsWith("seen 1, first at 10:03:07: [why] LevelUp")
                && Pt(pr, "side-panel").EndsWith("...") && Pt(pr, "results").StartsWith("seen 1, first at 10:03:08: [shot] results step 1")
                && Pt(pr, "recruit-tour").StartsWith("seen 1, first at 10:03:09: [menu] pause walk: hover tour") && bare.All(p => p.Text == "not in this log"),
                Pt(pr, "first-input") + " | " + Pt(pr, "equip") + " | " + Pt(bare, "yard"));
            var twoP = ProofsText(Healthy + Healthy.Replace("2030-01-02 10:00:00", "2030-01-03 11:00:00") + ProofLog, "all");
            Check("L12", "INFO over several sessions (--since all): 'first at' carries the day", Pt(twoP, "first-input") == "seen 1, first at 2030-01-03 10:03:00: touch", Pt(twoP, "first-input"));

            // L13: --expect (through the command, on files - its output read from a StringWriter, so the bench's verdict sees none of it)
            string tmp = Path.Combine(Path.GetTempPath(), "yazs_bench_expect_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                string logDir = Path.Combine(tmp, "BepInEx", "plugins", "YazsCompanion");      // no cfg two folders up: WideMenus = Everywhere
                Directory.CreateDirectory(logDir);
                string withProofs = Path.Combine(logDir, "companion.log"), plain = Path.Combine(logDir, "companion.log.1");
                File.WriteAllText(withProofs, Healthy + ProofLog);
                File.WriteAllText(plain, Healthy);
                var o1 = new StringWriter(); int c1 = Command(new[] { "--check-log", withProofs, "--expect", "first-input,yard" }, 0, o1);
                var o2 = new StringWriter(); int c2 = Command(new[] { "--check-log", plain, "--expect", "first-input,Yard" }, 0, o2);
                var o3 = new StringWriter(); int c3 = Command(new[] { "--check-log", withProofs, "--expect", "first-input,bogus" }, 0, o3);
                var o4 = new StringWriter(); int c4 = Command(new[] { "--check-log", withProofs, "--expect" }, 0, o4);
                var o5 = new StringWriter(); int c5 = Command(new[] { "--check-log", plain }, 0, o5);
                var o6 = new StringWriter(); int c6 = Command(new[] { "--check-log", plain, "--since", "2031" }, 0, o6);
                string s1 = o1.ToString(), s2 = o2.ToString(), s3 = o3.ToString(), s5 = o5.ToString();
                Check("L13", "--expect first-input,yard: exit 0 on a log that holds both (two more checks), 1 on one that holds neither (kinds read in any case)",
                    c1 == 0 && s1.Contains("PASS  expect first-input: seen 1, first at 10:03:00: touch") && s1.Contains("PASS  expect yard: seen 2")
                    && c2 == 1 && s2.Contains("  expect first-input: expected, and not in this log") && s2.Contains("  expect yard: expected, and not in this log"),
                    "exit " + c1 + " / " + c2);
                Check("L13", "--expect with an unknown kind exits 2 naming it and every kind; --expect without a list exits 2",
                    c3 == 2 && s3.Contains("unknown kind 'bogus'") && Kinds.All(k => s3.Contains(k.Id)) && !s3.Contains("PASS") && c4 == 2 && o4.ToString().Contains("the kinds: first-input"),
                    "exit " + c3 + " / " + c4);
                Check("L13", "without --expect the exit codes are 0.15.0's (a healthy log 0, no session 2); the INFO sections print after the checks, before the verdict line",
                    c5 == 0 && c6 == 2 && s5.IndexOf("  INFO  real-session proofs (not checks)", StringComparison.Ordinal) > s5.IndexOf("  PASS  items: ", StringComparison.Ordinal)
                    && s5.IndexOf("check-log: all as wanted", StringComparison.Ordinal) > s5.IndexOf("  INFO  walk proofs (not checks)", StringComparison.Ordinal)
                    && s5.Contains("        recruit-tour: not in this log") && s1.Contains("        first-input: seen 1, first at 10:03:00: touch"),
                    "exit " + c5 + " / " + c6);
            }
            finally { try { Directory.Delete(tmp, true); } catch { } }

            // L14: the patterns are anchored on the message - a line of another tag that quotes the words, or a near miss, does not count
            var quoted = ProofsText(Healthy + Quoted);
            Check("L14", "a line of another tag with the same words, a source the menu does not have, a step 3, a tour of 3 cards: nothing counts",
                quoted.All(p => p.Text == "not in this log"), string.Join(", ", quoted.Where(p => p.Text != "not in this log").Select(p => p.Kind.Id + " " + p.Text)));

            // L15: the EQUIP press's source
            string eqNew = Lf(@"10:04:00.000 [Info] [loadout] equip: EQUIP ADVICE (advice #2) pressed by touch - remove Tough, add Gunner = 2 clicks through the game's own badge button, 0.12 s apart; selection before 20,18
");
            string eqOld = Lf(@"10:04:00.000 [Info] [loadout] equip: EQUIP ADVICE (advice #1) pressed - remove Tough, add Gunner = 2 clicks through the game's own badge button, 0.12 s apart; selection before 20,18
");
            string eqKey = Lf(@"10:04:05.000 [Info] [loadout] equip: UNDO pressed by key - remove Gunner, add Tough = 2 clicks through the game's own badge button, 0.12 s apart; selection before 20,6
");
            var pNew = ProofsText(Healthy + eqNew); var pOld = ProofsText(Healthy + eqOld); var pBoth = ProofsText(Healthy + eqOld + eqNew + eqKey);
            var exOld = Expect(pOld, new List<string> { "equip" })[0]; var exNew = Expect(pNew, new List<string> { "equip" })[0];
            Check("L15", "equip: 'pressed by touch' -> touch; a 0.15.0 'pressed - ' line -> 'seen 1 without a source'; both, and an UNDO by key -> the sources, the old line apart",
                Pt(pNew, "equip") == "seen 1, first at 10:04:00: touch" && Pt(pOld, "equip") == "seen 1 without a source (a build before 0.16.0)"
                && Pt(pBoth, "equip") == "seen 2, first at 10:04:00: touch (touch 1, key 1); 1 without a source (a build before 0.16.0)",
                Pt(pBoth, "equip"));
            Check("L15", "--expect equip: not met by the 0.15.0 line, met by the new one", !exOld.Pass && exOld.Text.EndsWith("none says how it was pressed") && exNew.Pass, exOld.Text + " | " + exNew.Text);
            string leSrc = Src("LoadoutEquip.cs");
            Check("L15", "the writer: LoadoutEquip's pressed line says 'pressed by <source>' - Menu.Pointer() for a click on the plate (touch / mouse), 'key' for the key",
                leSrc != null && leSrc.Contains("\" pressed by \"") && leSrc.Contains("source = Menu.Pointer()") && leSrc.Contains("source = \"key\""));

            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // the 0.16.0 cases' made-up lines (the game's names only; badge names like Gunner, yard nodes like Weapon Damage)
        static readonly string ProofLog = Lf(@"10:03:00.000 [Info] [menu] first input this session: touch
10:03:01.000 [Info] [yard] bought Weapon Damage 1>2 (advice #1)
10:03:02.000 [Info] [yard] refunded Armor 1>0
10:03:03.000 [Info] [loadout] equip: EQUIP ADVICE (advice #1) pressed by touch - remove Tough, add Gunner = 2 clicks through the game's own badge button, 0.12 s apart; selection before 20,18
10:03:04.000 [Info] [loadout] equip: UNDO pressed by mouse - remove Gunner, add Tough = 2 clicks through the game's own badge button, 0.12 s apart; selection before 20,6
10:03:05.000 [Info] [badge] ribbon stepped aside for the Skill Tree label (ribbon alpha 0.41)
10:03:06.000 [Info] [quest] GameHubQuest_Main_06 -> story objective main_story_objective_q6: 3 of 5 - progress only
10:03:07.000 [Info] [why] LevelUp 00:30: #2 Handgun 3.72 - WHY 'Medical Drone goes first' | 'Level 2 of 4' (side wing left, header + 2 lines, 20.5 px, 2 of 2 shown)
10:03:08.000 [Info] [shot] results step 1 set up (3440x1440) - captures at 0.8 and 1.6 s
10:03:09.000 [Info] [menu] pause walk: hover tour - cards 2 to 4 of 4 selected in turn (the game's selection, never a click), a 'why_card' shot 0.4 s after each '[why]' line
");
        static readonly string Quoted = Lf(@"10:03:00.000 [Info] [config] note: [menu] first input this session: pad
10:03:01.000 [Info] [menu] first input this session: pad, then key
10:03:02.000 [Info] [why] LevelUp 00:30: #1 Handgun 3.72 - '[yard] bought Weapon Damage 1>2 (advice #1)' (2 lines, 20.5 px, 1 of 1 shown)
10:03:03.000 [Info] [loadout] equip: EQUIP ADVICE (advice #x) pressed by touch - add Gunner = 1 click
10:03:04.000 [Info] [loadout] equip: EQUIP ADVICE (advice #1) pressed by finger - add Gunner = 1 click
10:03:05.000 [Info] [card] #1 PICK Handgun (weapon, Medic) 3.72 - [badge] ribbon stepped aside for the Skill Tree label
10:03:06.000 [Info] [quest] -> story objective main_story_objective_q6: 3 of 5 - progress only
10:03:07.000 [Info] [why] LevelUp 00:30: #2 Handgun 3.72 - WHY 'Level 2 of 4' (band, 2 lines, 20.5 px, 2 of 2 shown) side wing left, header + 2 lines,
10:03:08.000 [Info] [shot] results step 3 set up (3440x1440)
10:03:09.000 [Info] [menu] pause walk: hover tour - cards 2 to 3 of 3 selected in turn
");

        static List<Proof> ProofsText(string log, string since = "last")
        {
            var lines = log.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
            return Proofs(Select(Sessions(lines), since));
        }
        static string Pt(List<Proof> ps, string id) { return ps.First(p => p.Kind.Id == id).Text; }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p).Replace("\r\n", "\n") : null;
        }
    }
}
