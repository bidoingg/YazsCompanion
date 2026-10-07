// 0.15.0 (C15-11): a companion.log's health, PASS or FAIL per check - the live series and a real session end with it, and the
// release notes quote it:
//   ItemBench --check-log <companion.log> [--since last|all|<session stamp prefix>] [--wide on|off]
//   hooks     every session logs '[hooks] N methods patched (class by class; 0 patch classes failed)'
//   why       every session logs '[why] hooks: ...' with every hook ok
//   wide      at least one '[wide] ... frame off' while WideMenus is on and the screen is not 16:9 (16:9: nothing to hide)
//   warnings  no '[Warning]' line
//   errors    no '[Error]' / '[Fatal]' line
//   shown     every [shown] text within the Wording rails, with the room it had as drawn (beside its "2ND" / "AVOID")
// --since: 'last' (the default: the newest session of the file), 'all', or a session stamp prefix: '2026-10-06' or
// '2026-10-06 19:02' selects every session that started then or later. --wide overrides what the cfg next to the log says
// (BepInEx\config\bidoi.yazs.companion.cfg two folders up from the log; WideMenus = Everywhere when there is none); a
// '[config] General.WideMenus = ... (saved)' line of the menu counts as well.
// Exit 0 when every check passes, 1 when one fails, 2 when the file or a session is missing.
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
        static readonly Version V013 = new Version(0, 13, 0), V014 = new Version(0, 14, 0);   // the class-by-class hooks line; the WHY band and the wide menus

        static string Older(int n, string build) { return n == 0 ? "" : "; " + Count(n, "session") + " of a build before " + build + " not checked"; }
        static string OnlyOlder(int n, string build) { return "not checked - " + (n == 1 ? "the session is" : "every session is") + " of a build before " + build; }

        // ---------------------------------------------------------------- the command
        public static int Command(string[] args, int at)
        {
            string file = at + 1 < args.Length && !args[at + 1].StartsWith("--") ? args[at + 1] : null;
            string since = Arg(args, "--since") ?? "last";
            string wideArg = Arg(args, "--wide");
            if (file == null) { Console.WriteLine("usage: ItemBench --check-log <companion.log> [--since last|all|<stamp>] [--wide on|off]"); return 2; }
            if (!File.Exists(file)) { Console.WriteLine("check-log: no file " + file); return 2; }
            bool? wide = null;
            if (wideArg != null)
            {
                if (wideArg.Equals("on", StringComparison.OrdinalIgnoreCase)) wide = true;
                else if (wideArg.Equals("off", StringComparison.OrdinalIgnoreCase)) wide = false;
                else { Console.WriteLine("check-log: --wide takes on or off, not '" + wideArg + "'"); return 2; }
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
            if (sessions.Count == 0) { Console.WriteLine("check-log: no session in " + file + " matches --since " + since); return 2; }
            Console.WriteLine("check-log " + file + ": " + Count(sessions.Count, "session") + " ("
                + (sessions.Count == 1 ? sessions[0].Stamp : sessions[0].Stamp + " .. " + sessions[sessions.Count - 1].Stamp) + "), "
                + sessions.Sum(s => s.Lines.Count) + " lines");
            var results = Run(sessions, wide, wideFrom);
            Print(results);
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

        static void Print(List<Result> results)
        {
            foreach (var r in results)
            {
                Console.WriteLine("  " + (r.Pass ? "PASS" : "FAIL") + "  " + r.Id + ": " + r.Text);
                foreach (var d in r.Detail.Take(8)) Console.WriteLine("        " + (d.Length > 220 ? d.Substring(0, 220) + "..." : d));
                if (r.Detail.Count > 8) Console.WriteLine("        ... " + (r.Detail.Count - 8) + " more");
            }
            int failed = results.Count(r => !r.Pass);
            Console.WriteLine("check-log: " + (failed == 0 ? "all as wanted" : failed + " check" + (failed == 1 ? "" : "s") + " not as wanted"));
        }

        // ---------------------------------------------------------------- the checks
        /// <summary>The six checks over the sessions. <paramref name="wide"/>: WideMenus on / off as known from outside the
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
            Check("L1", "a healthy session passes all six checks", ok.Count == 6 && ok.All(r => r.Pass), Fails(ok) + " | " + R(ok, "shown").Text);

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
            Check("L6", "a text with an apostrophe is read whole ('The Rifleman build's main ability')", R(ok, "shown").Text.Contains("3 card texts"), R(ok, "shown").Text);

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

            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }
    }
}
