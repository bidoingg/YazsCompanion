// 0.16.2 (the beta channel, WP1 / WP2 / WP3 of the plan, 2026-10-12; its critic items 2, 3, 4, 5, 7, 8, 13, 15, 20, 26): the
// auto-updater's two feeds, the "Beta updates" switch and the way back from a test build. No network: the whole checks below are
// fed from memory (Updater.Setup.Fetch); the real HTTP runs in tools\updater_drill.ps1 (UpdaterDrill.cs is its host).
//   UP1   Decide - the plan's table (WP1's tests) and every critic case: Download / StepDown / UpToDate / Nothing, the floor, a
//         withdrawn build, Off on a published test build, a local build, a failed beta feed (13), a hand-set feed (forward only)
//   UP2   the labels: release, test build, development build, withdrawn test build (a failed beta feed keeps "test build")
//   UP3   YAZS_BETA: 0 off false no / 1 on true yes in any case, anything else ignored
//   UP4   channel_truth.txt, the one truth table (15): every row, the Companion's reading and an outside reader's, CRLF and LF
//   UP5   latest.json read (commit, withdrawn, junk), the https rule - plain http only from a feed at http://localhost (20)
//   UP6   development builds (7): no commit, -dirty, dev_build.txt, a version published from another commit
//   UP7   the words: the channel line (the contract's shape), the header tag, the restart strip, the DISPLAY row's footer in every
//         state - its length, the Windows and the Proton rescue words (8), generic words only
//   UP8   a scratch plugin folder: CleanupSiblings (the kept release, .off, .offnext - 3, 4), Retire twice, Undo, a refused
//         rename leaving .offnext, the rename probe (once per build)
//   UP9   whole checks over in-memory feeds: download, already downloaded, step down, the kept release with no download (4), the
//         identity check, a SHA mismatch, a withdrawn build, a pending test build set aside when Off and a pending build then
//         withdrawn (2), a failed beta feed and its one retry (13), a missing beta feed, auto-update off, the dev marker (7),
//         Off then On before a restart (Undo), a refused rename, a hand-set feed, the feed's commit (5)
//   UP10  a REAL loaded DLL: a scratch copy of this bench's own DLL loaded (as BepInEx loads a plugin), set aside and put back
//   UP11  the sources: the cfg key and its default, the check before the knowledge load, the recheck, the DISPLAY row, the strip
//         off in play, the deploy hygiene and marker, release.ps1's switches and no private path in it (26), -Promote's Deck
//         folder set (21, integration fix 2026-10-12)
// Review fixes (2026-10-12, two adversarial reviews): UP1 which feed failed, the pull window; UP7 Retire's longest reason with
// YAZS_BETA on both systems within the help box; UP8 a .keep is only ever the release the last check recorded (S1 test build to
// test build offline, S2 a new release offline, S2b the first 0.16.2 start), the release beside a waiting .offnext; UP9 the
// refused rename forgets the pending download (and its loop needs no download), the release kept at a test build's download
// (4a), Off with either feed failing, the dev marker giving way to the published bytes, the pull window; UP11 generic.
// Pure (no game data): the full bench and --no-data run it (CI: windows-latest). Generic names only (the repository is public).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class UpdaterCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (ok || string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string ModSrc(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", file));
            return File.Exists(p) ? File.ReadAllText(p).Replace("\r\n", "\n") : null;
        }
        internal static string TruthPath { get { return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "channel_truth.txt")); } }
        static Version V(string s) { return UpdateRules.ParseVersion(s); }

        public static int Run()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.16.2, the beta channel: the switch, two feeds, the way back (WP1 - WP3; critic 2 3 4 5 7 8 13 15 20 26) - UpdaterCases.cs");
            var echo = Updater.Echo;
            try
            {
                DecideTable();
                Labels();
                EnvParse();
                Truth();
                Feeds();
                DevBuilds();
                Words();
                Scratch();
                WholeChecks();
                LoadedDll();
                Sources();
            }
            catch (Exception e) { Check("UP", "the cases ran to the end", false, e.GetType().Name + ": " + e.Message); }
            finally { Updater.Echo = echo; Updater.ResetForTests(); }
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ================================================================ feeds made up
        static Feed F(string ver, string commit = null, params string[] withdrawn)
        {
            var f = Feed.Of(FeedState.Answered, "https://example.invalid/" + ver + "/latest.json");
            f.VersionText = ver; f.Version = V(ver); f.Dll = "https://example.invalid/YazsCompanionMod-" + ver + ".dll"; f.Sha = "00"; f.Commit = commit;
            foreach (var w in withdrawn) f.Withdrawn.Add(w);
            return f;
        }
        static Feed Absent() { return Feed.Of(FeedState.Absent, "https://example.invalid/beta", "HTTP 404"); }
        static Feed Failed() { return Feed.Of(FeedState.Failed, "https://example.invalid/beta", "TaskCanceledException"); }
        static Feed NotAsked() { return Feed.Of(FeedState.NotAsked, null); }

        static string D(string run, bool on, Feed s, Feed b, string dev = null, bool single = false)
        {
            var d = UpdateRules.Decide(V(run), dev, on, s, b, single);
            return d.ToString();
        }

        // ================================================================ UP1
        static void DecideTable()
        {
            var why = new List<string>();
            Action<string, string, string> want = (what, got, wanted) => { if (got != wanted) why.Add(what + ": " + got + ", wanted " + wanted); };
            Feed S2 = F("0.16.2"), B3 = F("0.16.3");
            // the plan's WP1 table
            want("on, release 0.16.2 runs, beta 0.16.3", D("0.16.2", true, S2, B3), "Download 0.16.3");
            want("off, test build 0.16.3 runs", D("0.16.3", false, S2, B3), "StepDown 0.16.2");
            want("on, test build 0.16.3 runs", D("0.16.3", true, S2, B3), "UpToDate");
            want("off, release 0.16.2 runs", D("0.16.2", false, S2, B3), "UpToDate");
            want("a -dirty build", D("0.16.3", true, S2, B3, "abc1234-dirty"), "Nothing (a development build - updates leave it alone)");
            want("a clean 0.16.4 above both feeds, on", D("0.16.4", true, S2, B3), "Nothing (ahead of every published build - a local build)");
            want("a clean 0.16.4 above both feeds, off", D("0.16.4", false, S2, B3), "Nothing (ahead of every published build - a local build)");
            Feed B2w = F("0.16.2", null, "0.16.3");
            want("0.16.3 withdrawn, the beta feed back at 0.16.2, on", D("0.16.3", true, S2, B2w), "StepDown 0.16.2");
            want("0.16.3 withdrawn, the beta feed back at 0.16.2, off", D("0.16.3", false, S2, B2w), "StepDown 0.16.2");
            want("off, the release 0.16.1 is under the floor", D("0.16.3", false, F("0.16.1"), B3), "Nothing (would go below 0.16.2)");
            want("on, no beta feed (404), the release 0.16.2, 0.16.1 runs", D("0.16.1", true, S2, Absent()), "Download 0.16.2");
            want("on, no beta feed (404), the release runs", D("0.16.2", true, S2, Absent()), "UpToDate");
            want("on, no beta feed (404), 0.16.3 runs", D("0.16.3", true, S2, Absent()), "Nothing (ahead of every published build - a local build)");
            want("neither feed answered", D("0.16.2", true, Failed(), Absent()), "Nothing (no feed answered)");
            // critic 13: a FAILED beta feed moves nothing where the answer needs it
            want("13: on, the beta feed failed", D("0.16.3", true, S2, Failed()), "Nothing (the test-builds feed did not answer)");
            want("13: on, the beta feed failed, a release above", D("0.16.1", true, S2, Failed()), "Nothing (the test-builds feed did not answer)");
            want("13: off, the beta feed failed, the release runs", D("0.16.2", false, S2, Failed()), "UpToDate");
            want("13: off, the beta feed failed, an older release runs", D("0.16.1", false, S2, Failed()), "Download 0.16.2");
            want("13: off, the beta feed failed, a test build runs", D("0.16.3", false, S2, Failed()), "Nothing (the test-builds feed did not answer)");
            // more: an older test build Off, a withdrawn build to a test build, a withdrawn build with a newer one out, the hand-set feed
            want("off, the older test build 0.16.3 runs, newest 0.16.4", D("0.16.3", false, S2, F("0.16.4")), "StepDown 0.16.2");
            want("on, 0.16.4 withdrawn, the beta feed back at 0.16.3", D("0.16.4", true, S2, F("0.16.3", null, "0.16.4")), "StepDown 0.16.3");
            want("on, 0.16.3 withdrawn but 0.16.4 out", D("0.16.3", true, S2, F("0.16.4", null, "0.16.3")), "Download 0.16.4");
            want("a hand-set feed, a newer build", D("0.16.1", true, S2, NotAsked(), null, true), "Download 0.16.2");
            want("a hand-set feed, an older build", D("0.16.3", false, S2, NotAsked(), null, true), "Nothing (a hand-set feed (UpdateUrl) only goes forward)");
            want("a version that does not parse", UpdateRules.Decide(null, null, true, S2, B3).ToString(), "Nothing (the running version does not parse)");
            // review fixes (2026-10-12): which feed failed is said right; the pull window never targets a withdrawn build
            want("both feeds failed, off", D("0.16.3", false, Failed(), Failed()), "Nothing (no feed answered)");
            want("both feeds failed, on", D("0.16.2", true, Failed(), Failed()), "Nothing (no feed answered)");
            want("off, the release feed failed, the beta feed answered", D("0.16.3", false, Failed(), B3), "Nothing (the release feed did not answer)");
            want("on, the release feed failed, the beta feed answered", D("0.16.2", true, Failed(), B3), "Download 0.16.3");
            want("the pull window, on: the release feed still names the withdrawn 0.16.3, the beta feed back at 0.16.2", D("0.16.2", true, F("0.16.3"), F("0.16.2", null, "0.16.3")), "UpToDate");
            want("the pull window, on: the withdrawn release 0.16.3 runs", D("0.16.3", true, F("0.16.3"), F("0.16.2", null, "0.16.3")), "StepDown 0.16.2");
            want("the pull window, off: only the withdrawn release is named", D("0.16.3", false, F("0.16.3"), F("0.16.2", null, "0.16.3")), "Nothing (the feeds name only a withdrawn build)");
            want("the pull window, off, an older build: no download of the withdrawn release", D("0.16.2", false, F("0.16.3"), F("0.16.2", null, "0.16.3")), "Nothing (the feeds name only a withdrawn build)");
            // which feed names the target, and what it is
            var d1 = UpdateRules.Decide(V("0.16.2"), null, true, S2, B3);
            var d2 = UpdateRules.Decide(V("0.16.1"), null, true, S2, F("0.16.2"));
            var d3 = UpdateRules.Decide(V("0.16.3"), null, false, S2, B3);
            if (!(d1.From == B3 && d1.Kind == "test build")) why.Add("a test build's target is named by the beta feed and is a test build");
            if (!(d2.From == S2 && d2.Kind == "release")) why.Add("a build both feeds name comes from the release's feed and is the release");
            if (!(d3.From == S2 && d3.Kind == "release")) why.Add("a step down off goes to the release's feed");
            Check("UP1", "Decide: the plan's table (download, step down, up to date, a dev build, a local build both ways, a withdrawn build both ways, the floor, no beta feed, no feed), a failed beta feed moves nothing where it is needed (13), an older test build off, a withdrawn build to a test build or forward, a hand-set feed only forward, the target's feed and kind; which feed failed said right, never a withdrawn target (the pull window)",
                why.Count == 0, string.Join("; ", why));
        }

        // ================================================================ UP2
        static void Labels()
        {
            var why = new List<string>();
            Action<string, string, string> want = (what, got, wanted) => { if (got != wanted) why.Add(what + ": '" + got + "', wanted '" + wanted + "'"); };
            Feed S2 = F("0.16.2"), B3 = F("0.16.3");
            want("the release", UpdateRules.Label(V("0.16.2"), null, S2, B3), "release");
            want("the newest test build", UpdateRules.Label(V("0.16.3"), null, S2, B3), "test build");
            want("above every published build", UpdateRules.Label(V("0.16.4"), null, S2, B3), "development build");
            want("a dev build", UpdateRules.Label(V("0.16.2"), "abc1234-dirty", S2, B3), "development build");
            want("a withdrawn build", UpdateRules.Label(V("0.16.3"), null, S2, F("0.16.2", null, "0.16.3")), "withdrawn test build");
            want("13: the beta feed failed, above the release", UpdateRules.Label(V("0.16.3"), null, S2, Failed()), "test build");
            want("no beta feed (404), above the release", UpdateRules.Label(V("0.16.3"), null, S2, Absent()), "development build");
            want("a hand-set feed, above it", UpdateRules.Label(V("0.16.3"), null, S2, NotAsked()), "development build");
            want("nothing answered", UpdateRules.Label(V("0.16.3"), null, Failed(), Failed()), "test build");
            Check("UP2", "the labels: release, test build, development build (above every published build, or a dev build), withdrawn test build; a failed beta feed keeps 'test build' (13)", why.Count == 0, string.Join("; ", why));
        }

        // ================================================================ UP3
        static void EnvParse()
        {
            var why = new List<string>();
            foreach (var s in new[] { "0", "off", "false", "no", "OFF", "Off", "FALSE", "No", "NO", "fAlSe" }) if (UpdateRules.EnvChoice(s) != false) why.Add("'" + s + "' is not stable");
            foreach (var s in new[] { "1", "on", "true", "yes", "ON", "On", "TRUE", "Yes", "YES", "tRuE" }) if (UpdateRules.EnvChoice(s) != true) why.Add("'" + s + "' is not beta");
            foreach (var s in new[] { null, "", " ", " 1", "0 ", "2", "-1", "beta", "stable", "enabled", "y", "n" }) if (UpdateRules.EnvChoice(s) != null) why.Add("'" + (s ?? "null") + "' was not ignored");
            var sw = UpdateRules.Switch("0", true);
            if (!(sw.Beta == false && sw.From == "env" && sw.Why == "set by YAZS_BETA=0")) why.Add("YAZS_BETA=0 over true: " + sw.Why);
            sw = UpdateRules.Switch("maybe", false);
            if (!(sw.Beta == false && sw.From == "cfg" && sw.Why == "Beta updates off")) why.Add("junk over false: " + sw.Why);
            sw = UpdateRules.Switch(null, true);
            if (sw.Why != "Beta updates on" || sw.Channel != "beta") why.Add("on: " + sw.Why);
            Check("UP3", "YAZS_BETA: 0 off false no / 1 on true yes, any case, taken as set (no trimming); anything else ignored; the reasons 'set by YAZS_BETA=0' / 'Beta updates on|off'", why.Count == 0, string.Join("; ", why));
        }

        // ================================================================ UP4
        /// <summary>The config file channel_truth.txt describes for a cfg column value (null = no file).</summary>
        internal static string CfgText(string token, string nl)
        {
            if (token == "<no file>") return null;
            var head = new List<string> { "## Settings file was created by plugin YAZS Companion v0.16.2", "## Plugin GUID: bidoi.yazs.companion", "", "[General]", "", "ShowBadges = true", "" };
            var update = new List<string> { "[Update]", "", "## At every launch, fetch the release feed ...", "# Setting type: Boolean", "# Default value: true", "AutoUpdate = true", "" };
            var tail = new List<string> { "[Logging]", "", "Verbose = false" };
            string[] key = { "# Setting type: Boolean", "# Default value: true" };
            switch (token)
            {
                case "<no section>": update.Clear(); break;
                case "<other section>": head.Insert(6, "BetaUpdates = false"); update.Clear(); break;
                case "<no key>": break;
                case "<key case>": update.AddRange(key); update.Add("betaupdates = false"); update.Add(""); break;
                case "<commented>": update.AddRange(key); update.Add("# BetaUpdates = false"); update.Add(""); break;
                case "<empty>": update.AddRange(key); update.Add("BetaUpdates ="); update.Add(""); break;
                default: update.AddRange(key); update.Add("BetaUpdates = " + token); update.Add(""); break;
            }
            return string.Join(nl, head.Concat(update).Concat(tail)) + nl;
        }

        internal sealed class TruthRow { public string Env, Cfg, Companion, Outside, From; public int Line; }

        internal static List<TruthRow> ReadTruth(string path, List<string> errors)
        {
            var rows = new List<TruthRow>();
            if (!File.Exists(path)) { errors.Add("no " + path); return rows; }
            int n = 0;
            foreach (var raw in File.ReadAllLines(path))
            {
                n++;
                string line = raw.TrimEnd('\r');
                if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#", StringComparison.Ordinal)) continue;
                var c = line.Split('|').Select(x => x.Trim()).ToArray();
                if (c.Length != 5) { errors.Add("line " + n + ": " + c.Length + " columns"); continue; }
                rows.Add(new TruthRow { Env = c[0], Cfg = c[1], Companion = c[2], Outside = c[3], From = c[4], Line = n });
            }
            return rows;
        }

        static void Truth()
        {
            var why = new List<string>();
            var rows = ReadTruth(TruthPath, why);
            int envSet = 0, missing = 0, junkCfg = 0;
            foreach (var r in rows)
            {
                string env = r.Env == "<unset>" ? null : r.Env == "<empty>" ? "" : r.Env;
                foreach (var nl in new[] { "\r\n", "\n" })
                {
                    bool? cfg = UpdateRules.CfgValue(CfgText(r.Cfg, nl));
                    var comp = UpdateRules.Switch(env, cfg ?? true);            // the Companion: BepInEx binds the key with its default
                    var outside = UpdateRules.Switch(env, cfg, false);
                    if (comp.Channel != r.Companion || outside.Channel != r.Outside || outside.From != r.From)
                        why.Add("line " + r.Line + " (" + r.Env + " | " + r.Cfg + (nl == "\n" ? ", LF" : ", CRLF") + "): companion " + comp.Channel + ", outside " + outside.Channel + " from " + outside.From);
                }
                if (r.From == "env") envSet++;
                if (r.From == "missing") missing++;
                if (r.From == "cfg" && r.Companion == "beta" && !r.Cfg.Equals("true", StringComparison.OrdinalIgnoreCase)) junkCfg++;
            }
            if (rows.Count < 90) why.Add("only " + rows.Count + " rows");
            Check("UP4", "channel_truth.txt (" + rows.Count + " rows: " + envSet + " decided by YAZS_BETA, " + missing + " with no setting, " + junkCfg + " with a value BepInEx cannot read = true): the Companion's reading and an outside reader's, CRLF and LF (15)",
                why.Count == 0, string.Join("; ", why.Take(6)));
        }

        // ================================================================ UP5
        static void Feeds()
        {
            var why = new List<string>();
            var f = UpdateRules.ParseFeed("{\"version\":\"0.16.3\",\"dll\":\"https://x/YazsCompanionMod-0.16.3.dll\",\"sha256\":\" ABCDEF \",\"zip\":\"https://x/z.zip\",\"notes\":\"n\",\"published\":\"2026-10-12T00:00:00Z\",\"commit\":\"abc1234\",\"withdrawn\":[\"0.16.2\",\"junk\",\"0.16.2\",7]}", "u");
            if (!(f.Ok && f.Version.ToString() == "0.16.3" && f.Sha == "abcdef" && f.Commit == "abc1234" && string.Join(",", f.Withdrawn) == "0.16.2" && f.Notes == "n")) why.Add("a full latest.json: " + f.State + " " + f.Error + " w=" + string.Join(",", f.Withdrawn));
            var old = UpdateRules.ParseFeed("{\"version\":\"0.16.1\",\"dll\":\"https://x/a.dll\",\"sha256\":\"aa\",\"notes\":\"\"}", "u");
            if (!(old.Ok && old.Commit == null && old.Withdrawn.Count == 0)) why.Add("a 0.16.1 latest.json (no commit, no withdrawn)");
            foreach (var bad in new[] { "", "[]", "{\"version\":\"0.16.2-beta.1\",\"dll\":\"https://x\",\"sha256\":\"a\"}", "{\"version\":\"0.16.2\",\"dll\":\"https://x\"}", "{\"version\":\"0.16.2.1\",\"dll\":\"https://x\",\"sha256\":\"a\"}", "not json" })
            {
                var g = UpdateRules.ParseFeed(bad, "u");
                if (g.State != FeedState.Failed || !g.Error.StartsWith("latest.json unusable", StringComparison.Ordinal)) why.Add("'" + bad + "' read as " + g.State);
            }
            Func<string, string, bool> ok = UpdateRules.UrlAllowed;
            if (!ok("https://github.com/x/latest.json", "https://github.com/x/a.dll")) why.Add("https");
            if (!ok("http://localhost:8123/beta/latest.json", "http://localhost:8123/dl/a.dll")) why.Add("20: the beta feed at http://localhost with an http dll");
            if (!ok("http://localhost/latest.json", "http://localhost/a.dll")) why.Add("http://localhost without a port");
            if (ok("http://127.0.0.1:8123/latest.json", "http://127.0.0.1:8123/a.dll")) why.Add("20: 127.0.0.1 took the exemption");
            if (ok("http://localhost.example.com/latest.json", "http://localhost.example.com/a.dll")) why.Add("20: a look-alike host took the exemption");
            if (ok("https://github.com/x/latest.json", "http://github.com/x/a.dll")) why.Add("an http dll from an https feed");
            if (ok("http://localhost:8123/latest.json", "ftp://localhost/a.dll") || ok("http://localhost:8123/latest.json", "")) why.Add("not http(s)");
            Check("UP5", "latest.json read with its new fields (commit, withdrawn: versions only, once each; 5) and without them (0.16.1's); junk, a SemVer suffix, four parts or no sha256 = a failed feed; https, or http only from a feed at http://localhost exactly (20: not 127.0.0.1, not a look-alike)",
                why.Count == 0, string.Join("; ", why));
        }

        // ================================================================ UP6
        static void DevBuilds()
        {
            var why = new List<string>();
            Feed S2c = F("0.16.2", "d203d42"), B3c = F("0.16.3", "1234567");
            Action<string, string, string> want = (what, got, wanted) => { if (got != wanted) why.Add(what + ": " + (got ?? "null") + ", wanted " + (wanted ?? "null")); };
            want("no commit", UpdateRules.DevWhy(null, false, V("0.16.2"), S2c, B3c), "no commit stamped");
            want("-dirty", UpdateRules.DevWhy("d203d42-dirty", false, V("0.16.2"), S2c, B3c), "d203d42-dirty");
            want("the marker", UpdateRules.DevWhy("d203d42", true, V("0.16.2"), S2c, B3c), "d203d42, dev_build.txt");
            want("the release's version from another commit", UpdateRules.DevWhy("abcdef0", false, V("0.16.2"), S2c, B3c), "abcdef0, published as d203d42");
            want("the test build's version from another commit", UpdateRules.DevWhy("abcdef0", false, V("0.16.3"), S2c, B3c), "abcdef0, published as 1234567");
            want("the release itself (a long hash in the feed)", UpdateRules.DevWhy("d203d42", false, V("0.16.2"), F("0.16.2", "d203d42aaaabbbbccccdddd"), B3c), null);
            want("a feed without a commit (0.16.1's)", UpdateRules.DevWhy("abcdef0", false, V("0.16.2"), F("0.16.2"), Absent()), null);
            want("a version no feed names", UpdateRules.DevWhy("abcdef0", false, V("0.16.4"), S2c, B3c), null);
            if (UpdateRules.SameCommit("abc", "abc")) why.Add("a 3-character stamp matched");
            if (UpdateRules.SameCommit("d203d42-dirty", "d203d42")) why.Add("a -dirty stamp matched the published commit");
            Check("UP6", "development builds (7): no commit, -dirty, dev_build.txt naming it, or the version published from another commit (short or long hashes); a published build itself, a feed without a commit and an unpublished version are not",
                why.Count == 0, string.Join("; ", why));
        }

        // ================================================================ UP7
        static readonly Regex ContractLine = new Regex(@"^\[update\] channel (beta|stable) \(([^)]*)\): release (\S+), newest (.+?), running (\S+) = (release|test build|development build|withdrawn test build) - (.+)$");

        /// <summary>Retire's longest refusal (review fix): the exception's type and 90 characters of its message, a game path in it.</summary>
        static readonly string LongRefusal = "refused: IOException: " + "The process cannot access the file 'C:\\Games\\SteamLibrary\\steamapps\\common\\SomeGame\\BepInEx\\plugins\\YazsCompanion\\YazsCompanionMod-0.16.3.dll' because it is being used by another process.".Substring(0, 87) + "...";

        static UpdateState St(Action<UpdateState> set)
        {
            var st = new UpdateState { Running = "0.16.3", Commit = "1234567", RunningFile = "YazsCompanionMod-0.16.3.dll", Checking = false, Checked = new DateTime(2026, 10, 13, 19, 42, 0),
                Stable = "0.16.2", Newest = "0.16.3", StableState = FeedState.Answered, NewestState = FeedState.Answered, Label = "test build", Act = UpdateAct.UpToDate, Result = "up to date", Probe = "works",
                Switch = new SwitchRead { Beta = true, From = "cfg" } };
            set(st); return st;
        }

        static void Words()
        {
            var why = new List<string>();
            var sw = new SwitchRead { Beta = true, From = "cfg" };
            string line = UpdateRules.ChannelLine(sw, F("0.16.2"), F("0.16.3"), "0.16.3", "test build", "up to date");
            if (line != "[update] channel beta (Beta updates on): release 0.16.2, newest 0.16.3, running 0.16.3 = test build - up to date") why.Add("the plan's example: " + line);
            var lines = new[]
            {
                line,
                UpdateRules.ChannelLine(new SwitchRead { Beta = false, From = "env", Env = "0" }, F("0.16.2"), Failed(), "0.16.3", "test build", "the test-builds feed did not answer"),
                UpdateRules.ChannelLine(new SwitchRead { Beta = true, From = "missing" }, Failed(), Absent(), "0.16.2", "test build", "no feed answered"),
                UpdateRules.ChannelLine(sw, F("0.16.2"), NotAsked(), "0.16.4", "development build", "a development build - updates leave it alone"),
            };
            foreach (var l in lines) if (!ContractLine.IsMatch(l)) why.Add("off the contract's shape: " + l);
            if (!lines[1].Contains("(set by YAZS_BETA=0): release 0.16.2, newest feed failed,")) why.Add("env / failed: " + lines[1]);
            if (!lines[2].Contains("release none, newest none,")) why.Add("none: " + lines[2]);
            if (UpdateRules.HeaderTag("test build") != "TEST BUILD" || UpdateRules.HeaderTag("withdrawn test build") != "TEST BUILD" || UpdateRules.HeaderTag("development build") != "DEV" || UpdateRules.HeaderTag("release") != "" || UpdateRules.HeaderTag("") != "")
                why.Add("the header tags");
            if (UpdateRules.NoticeText("0.16.2", "release") != "YAZS COMPANION 0.16.2 (RELEASE) DOWNLOADED  -  RESTART THE GAME TO APPLY" || UpdateRules.NoticeText("0.16.3", "test build").Length > 80)
                why.Add("the strip: " + UpdateRules.NoticeText("0.16.2", "release"));
            if (UpdateRules.RowValue(true) != "On - newest builds" || UpdateRules.RowValue(false) != "Off - releases only") why.Add("the row's values");

            // the footer, state by state
            var states = new Dictionary<string, UpdateState>
            {
                { "on, a test build, up to date", St(s => { }) },
                { "off, switching", St(s => { s.Switch = new SwitchRead { Beta = false, From = "cfg" }; s.Act = UpdateAct.StepDown; s.Target = "0.16.2"; s.TargetKind = "release"; s.SetAside = "done"; s.Pending = "0.16.2"; s.PendingKind = "release"; }) },
                { "off, the rename refused", St(s => { s.Switch = new SwitchRead { Beta = false, From = "cfg" }; s.Act = UpdateAct.StepDown; s.Target = "0.16.2"; s.TargetKind = "release"; s.SetAside = "refused: IOException: the file is in use"; s.Probe = "refused: IOException: in use"; }) },
                { "set by YAZS_BETA, Windows", St(s => { s.Switch = new SwitchRead { Beta = false, From = "env", Env = "0" }; s.Label = "release"; s.Running = "0.16.2"; }) },
                { "set by YAZS_BETA, Proton", St(s => { s.Switch = new SwitchRead { Beta = false, From = "env", Env = "0" }; s.Label = "release"; s.Running = "0.16.2"; s.Wine = true; }) },
                { "a dev build", St(s => { s.Dev = "abc1234-dirty"; s.Label = "development build"; s.Running = "0.16.4"; }) },
                { "auto-update off", St(s => { s.AutoUpdateOff = true; }) },
                { "offline", St(s => { s.Offline = true; s.Stable = null; s.Newest = null; }) },
                { "checking", St(s => { s.Checking = true; s.Probe = null; }) },
                { "off, the release, a test build out", St(s => { s.Switch = new SwitchRead { Beta = false, From = "cfg" }; s.Label = "release"; s.Running = "0.16.2"; }) },
                { "on, downloaded", St(s => { s.Label = "release"; s.Running = "0.16.2"; s.Act = UpdateAct.Download; s.Result = "downloaded 0.16.3 (test build) - it runs from the next start"; s.Pending = "0.16.3"; s.PendingKind = "test build"; }) },
                { "a withdrawn build, Proton", St(s => { s.Label = "withdrawn test build"; s.Wine = true; s.Newest = "0.16.2"; }) },
                { "up to date, a download waiting", St(s => { s.Label = "release"; s.Running = "0.16.2"; s.Pending = "0.16.3"; s.PendingKind = "test build"; }) },
                // review fixes: a refused rename with Retire's longest reason (90 characters, the game's path in it) and YAZS_BETA, on
                // both systems - the help box holds about 560 characters; the probe refused while nothing is stepping; no feed named a build
                { "refused, a long reason, YAZS_BETA, Windows", St(s => { s.Switch = new SwitchRead { Beta = false, From = "env", Env = "off" }; s.Act = UpdateAct.StepDown; s.Target = "0.16.2"; s.TargetKind = "release"; s.SetAside = LongRefusal; s.Probe = "refused: IOException: in use"; }) },
                { "refused, a long reason, YAZS_BETA, Proton", St(s => { s.Switch = new SwitchRead { Beta = false, From = "env", Env = "off" }; s.Act = UpdateAct.StepDown; s.Target = "0.16.2"; s.TargetKind = "release"; s.SetAside = LongRefusal; s.Probe = "refused: IOException: in use"; s.Wine = true; }) },
                { "on, the probe refused", St(s => { s.Probe = "refused: IOException: in use"; }) },
                { "no feed named a build", St(s => { s.Label = ""; s.Stable = null; s.StableState = FeedState.Failed; s.Newest = null; s.NewestState = FeedState.Absent; s.Act = UpdateAct.Nothing; s.Result = "no feed answered"; }) },
            };
            var foot = states.ToDictionary(kv => kv.Key, kv => UpdateRules.Footer(kv.Value));
            Action<string, string> has = (k, part) => { if (!foot[k].Contains(part)) why.Add(k + " lacks '" + part + "': " + foot[k]); };
            has("on, a test build, up to date", "On - newest builds. This mod: 0.16.3, a test build (release 0.16.2, newest 0.16.3) - up to date, checked 19:42. Off would put the release 0.16.2 back at the next start.");
            has("off, switching", "Off - releases only. The release 0.16.2 is downloaded and checked; the test build 0.16.3 is set aside - the release runs from the next start.");
            has("off, the rename refused", "cannot set the test build 0.16.3 aside (IOException: the file is in use)");
            has("off, the rename refused", "rename YazsCompanionMod-0.16.3.dll to YazsCompanionMod-0.16.3.dll.off with the game closed.");
            if (foot["off, the rename refused"].Contains("Going back needs")) why.Add("off, the rename refused: the probe's words repeat what the refusal says");
            has("on, the probe refused", "Going back needs the launcher or a hand step on this machine.");
            has("refused, a long reason, YAZS_BETA, Windows", "cannot set the test build 0.16.3 aside (IOException):");
            has("refused, a long reason, YAZS_BETA, Windows", "Set by the environment variable (YAZS_BETA=off)");
            has("refused, a long reason, YAZS_BETA, Proton", "Set by the launch options (YAZS_BETA=off)");
            has("no feed named a build", "This mod: 0.16.3 (release not reached, newest none) - no feed answered, checked 19:42.");
            has("set by YAZS_BETA, Windows", "Set by the environment variable (YAZS_BETA=0) - remove it to use this switch.");
            has("set by YAZS_BETA, Proton", "Set by the launch options (YAZS_BETA=0) - remove it there to use this switch.");
            has("a dev build", "a development build (abc1234-dirty): updates leave it alone");
            has("auto-update off", "Auto-update is off ([Update] AutoUpdate)");
            has("offline", "Checked 19:42 - offline.");
            has("checking", "checking for builds now");
            has("off, the release, a test build out", "On would take the test build 0.16.3 at the next start.");
            has("on, downloaded", "- downloaded 0.16.3 (test build) - it runs from the next start, checked 19:42.");
            has("up to date, a download waiting", "0.16.3 (test build) is downloaded - it runs from the next start.");
            has("a withdrawn build, Proton", "a withdrawn test build");
            has("a withdrawn build, Proton", "Off would put the release 0.16.2 back");
            foreach (var kv in foot)
            {
                string k = kv.Key, t = kv.Value;
                bool wine = states[k].Wine;
                if (!t.Contains("Other mods that follow this setting change at their own next start.")) why.Add(k + ": no 'other mods' line");
                if (wine && !t.Contains("If the game cannot reach this menu, put YAZS_BETA=0 first in the launch options.")) why.Add(k + ": no Proton rescue (8)");
                if (!wine && !t.Contains("If the game cannot reach this menu, set BetaUpdates = false under [Update] in BepInEx\\config\\bidoi.yazs.companion.cfg.")) why.Add(k + ": no Windows rescue (8)");
                if (!wine && t.Contains("launch options")) why.Add(k + ": the launch options named on Windows (8)");
                if (t.Length > 560) why.Add(k + ": " + t.Length + " characters (the help box holds about 560)");
                if (Regex.IsMatch(t, "  |\\.\\.|\\ba a\\b|\\ban (r|t|d)")) why.Add(k + ": a doubled space, '..' or a wrong article: " + t);
            }
            Check("UP7", "the words: the channel line in the contract's shape (the plan's example exactly), the header tags, the strip (the kind, 80 characters at most), the row's values; the footer in " + states.Count + " states (on / off / switching / refused / YAZS_BETA on Windows and Proton / dev / auto-update off / offline / checking / downloaded / withdrawn / a refusal with Retire's longest reason and YAZS_BETA on both systems / no feed named a build), each with the rescue words of its system (8) and at most 560 characters (longest " + foot.Values.Max(t => t.Length) + ")",
                why.Count == 0, string.Join("; ", why.Take(5)));
        }

        // ================================================================ UP8
        static string Scratch(string tag)
        {
            string dir = Path.Combine(Path.GetTempPath(), "yazs_bench_update_" + tag + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            return dir;
        }
        static void Junk(string dir, string name, string text = "x") { File.WriteAllText(Path.Combine(dir, name), text); }
        static string Listing(string dir) { return string.Join(", ", Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(s => s, StringComparer.OrdinalIgnoreCase)); }
        /// <summary>update_release.txt as a check writes it (the release the last answered check saw).</summary>
        static void Record(string dir, string ver, string sha) { File.WriteAllText(Path.Combine(dir, UpdateFiles.ReleaseFile), ver + "\nsha256 " + sha + "\n"); }

        /// <summary>Review fix (critic 4, the two reviews' S1 / S2 / S2b): CleanupSiblings over a made-up folder; returns what is left.</summary>
        static string Tidy(string tag, string me, string[] files, Action<string> more, out string did)
        {
            string dir = Scratch("tidy_" + tag);
            try
            {
                foreach (var n in files) Junk(dir, n, n.StartsWith(UpdateFiles.Prefix, StringComparison.Ordinal) ? "build " + n : "x");
                if (more != null) more(dir);
                did = string.Join("; ", Updater.CleanupSiblings(Path.Combine(dir, me), UpdateFiles.VersionInName(me).ToString()));
                return Listing(dir);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        static void Scratch()
        {
            var why = new List<string>();
            string dir = Scratch("files");
            Updater.Echo = null;
            try
            {
                // review fix: a .keep is the RELEASE the last answered check recorded (update_release.txt), never just the next lower build
                string rel2 = "release bytes 0.16.2", did;
                Action<string> rec2 = d => { Junk(d, "YazsCompanionMod-0.16.2.dll", rel2); Record(d, "0.16.2", Updater.Sha256(rel2)); };
                var common = new[] { "YazsCompanionMod-0.16.3.dll", "YazsCompanionMod.dll", "YazsCompanionMod-0.16.1.dll", "YazsCompanionMod-0.16.0.dll.off", "YazsCompanionMod-0.15.0.dll.offnext",
                    "YazsCompanionMod-0.16.4.dll.keep", "YazsCompanionMod-0.16.4.dll.part", "notes_0.16.3.txt", "companion.log", "update_probe.txt" };
                string left = Tidy("a", "YazsCompanionMod-0.16.3.dll", common, rec2, out did);
                if (left != "companion.log, notes_0.16.3.txt, update_probe.txt, update_release.txt, YazsCompanionMod-0.16.2.dll.keep, YazsCompanionMod-0.16.3.dll")
                    why.Add("the release kept, the rest removed: " + left + " (did: " + did + ")");
                // critic 3: while the running build's .offnext waits, the release stays a .dll beside it (a launcher needs another build to run)
                left = Tidy("b", "YazsCompanionMod-0.16.3.dll", common.Concat(new[] { "YazsCompanionMod-0.16.3.dll.offnext" }).ToArray(), rec2, out did);
                if (left != "companion.log, notes_0.16.3.txt, update_probe.txt, update_release.txt, YazsCompanionMod-0.16.2.dll, YazsCompanionMod-0.16.3.dll, YazsCompanionMod-0.16.3.dll.offnext")
                    why.Add("3: the release beside a waiting request: " + left);
                // S1: a test build to a test build, offline - the only .keep is the release, the older test build goes
                left = Tidy("s1", "YazsCompanionMod-0.16.4.dll", new[] { "YazsCompanionMod-0.16.4.dll", "YazsCompanionMod-0.16.3.dll" }, d => { Junk(d, UpdateFiles.Keep("0.16.2"), rel2); Record(d, "0.16.2", Updater.Sha256(rel2)); }, out did);
                if (left != "update_release.txt, YazsCompanionMod-0.16.2.dll.keep, YazsCompanionMod-0.16.4.dll") why.Add("S1, test build to test build: " + left);
                // S2: the first start of a new release, offline (its check recorded it) - no .keep, the old release and its old .keep go
                left = Tidy("s2", "YazsCompanionMod-0.16.3.dll", new[] { "YazsCompanionMod-0.16.3.dll" }, d => { Junk(d, "YazsCompanionMod-0.16.2.dll", rel2); Junk(d, UpdateFiles.Keep("0.16.2"), rel2); Record(d, "0.16.3", Updater.Sha256("build YazsCompanionMod-0.16.3.dll")); }, out did);
                if (left != "update_release.txt, YazsCompanionMod-0.16.3.dll") why.Add("S2, a new release: " + left);
                // S2b: the first 0.16.2 start coming from 0.16.1 (no record yet) - nothing kept
                left = Tidy("s2b", "YazsCompanionMod-0.16.2.dll", new[] { "YazsCompanionMod-0.16.2.dll", "YazsCompanionMod.dll", "YazsCompanionMod-0.16.1.dll" }, null, out did);
                if (left != "YazsCompanionMod-0.16.2.dll") why.Add("S2b, the first 0.16.2 start: " + left);
                // a .keep of the release's version with other bytes, and a release under the floor: nothing kept
                left = Tidy("sha", "YazsCompanionMod-0.16.3.dll", new[] { "YazsCompanionMod-0.16.3.dll" }, d => { Junk(d, UpdateFiles.Keep("0.16.2"), "other bytes"); Record(d, "0.16.2", Updater.Sha256(rel2)); }, out did);
                if (left != "update_release.txt, YazsCompanionMod-0.16.3.dll") why.Add("a .keep of other bytes: " + left);
                left = Tidy("floor", "YazsCompanionMod-0.16.3.dll", new[] { "YazsCompanionMod-0.16.3.dll" }, d => { Junk(d, "YazsCompanionMod-0.16.1.dll", "old"); Record(d, "0.16.1", Updater.Sha256("old")); }, out did);
                if (left != "update_release.txt, YazsCompanionMod-0.16.3.dll") why.Add("a release under the floor: " + left);

                string me = Path.Combine(dir, "YazsCompanionMod-0.16.3.dll");
                foreach (var n in new[] { "YazsCompanionMod-0.16.3.dll", "update_probe.txt" }) Junk(dir, n);

                // Retire twice, Undo
                string r1 = Updater.Retire(me), r2 = Updater.Retire(me);
                if (r1 != "done" || r2 != "already" || File.Exists(me) || !File.Exists(me + ".off") || File.Exists(me + ".offnext")) why.Add("Retire twice: " + r1 + " / " + r2 + " -> " + Listing(dir));
                bool back = Updater.Undo(me);
                if (!back || !File.Exists(me) || File.Exists(me + ".off")) why.Add("Undo: " + back + " -> " + Listing(dir));
                if (Updater.Undo(me)) why.Add("a second Undo put something back");

                // a refused rename: the file held open without FILE_SHARE_DELETE
                string refused;
                using (new FileStream(me, FileMode.Open, FileAccess.Read, FileShare.Read)) refused = Updater.Retire(me);
                if (!refused.StartsWith("refused: ", StringComparison.Ordinal) || !File.Exists(me) || !File.Exists(me + ".offnext")) why.Add("a refused rename: " + refused + " -> " + Listing(dir));
                Updater.CleanupSiblings(me, "0.16.3");
                if (!File.Exists(me + ".offnext")) why.Add("3: CleanupSiblings removed the .offnext of a DLL that is still there");
                if (!Updater.Undo(me) && File.Exists(me + ".offnext")) why.Add("Undo did not withdraw the launcher request");
                Junk(dir, "YazsCompanionMod-0.17.0.dll.offnext");
                Updater.CleanupSiblings(me, "0.16.3");
                if (File.Exists(Path.Combine(dir, "YazsCompanionMod-0.17.0.dll.offnext"))) why.Add("3: a .offnext whose DLL is gone was kept");

                // the probe: once per build
                if (UpdateFiles.ProbeName("YazsCompanionMod-0.16.2.dll") != "YazsCompanionMod-0.16.2.probe.dll" || UpdateFiles.ProbeName("YazsCompanionMod-0.16.2.probe.dll") != "YazsCompanionMod-0.16.2.dll" || UpdateFiles.ProbeName("YazsCompanionMod.dll") != "YazsCompanionMod.probe.dll")
                    why.Add("the probe's names");
                File.Delete(Path.Combine(dir, "update_probe.txt"));
                string p1 = Updater.ProbeOnce(me, "0.16.3+1234567");
                string pf = File.Exists(Path.Combine(dir, "update_probe.txt")) ? File.ReadAllText(Path.Combine(dir, "update_probe.txt")) : "";
                if (p1 != "works" || !File.Exists(me) || File.Exists(Path.Combine(dir, "YazsCompanionMod-0.16.3.probe.dll")) || !pf.StartsWith("0.16.3+1234567\nworks\n", StringComparison.Ordinal)) why.Add("the probe: " + p1 + " / '" + pf.Replace("\n", "|") + "'");
                string p2, p3;
                using (new FileStream(me, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    p2 = Updater.ProbeOnce(me, "0.16.3+1234567");        // remembered: the locked file is not tried again
                    p3 = Updater.ProbeOnce(me, "0.16.4+7654321");        // another build: tried, refused
                }
                if (p2 != "works" || !p3.StartsWith("refused: ", StringComparison.Ordinal) || !File.Exists(me)) why.Add("the probe remembered / refused: " + p2 + " / " + p3);
                if (UpdateFiles.VersionInName("YazsCompanionMod-0.16.2.dll.keep") != V("0.16.2") || UpdateFiles.VersionInName("YazsCompanionMod-0.16.2.probe.dll") != V("0.16.2") || UpdateFiles.VersionInName("YazsCompanionMod.dll") != null)
                    why.Add("the version in a file name");
            }
            catch (Exception e) { why.Add("threw " + e.GetType().Name + ": " + e.Message); }
            finally { try { Directory.Delete(dir, true); } catch { } }
            Check("UP8", "a scratch plugin folder: CleanupSiblings keeps as .keep only the release the last check recorded (4: never the next lower build - test build to test build offline, a new release offline, the first 0.16.2 start, other bytes, under the floor) and removes the rest, .off files, a .offnext whose DLL is gone (never one whose DLL is there, and the release stays a .dll beside it: 3) and .part; Retire twice (done, then already), Undo; a refused rename leaves .offnext, Undo withdraws it; the rename probe works, is remembered per build, is refused on a locked file",
                why.Count == 0, string.Join("; ", why));
        }

        // ================================================================ UP9: whole checks, the feeds from memory
        sealed class Net
        {
            public readonly Dictionary<string, Updater.Fetched> Map = new Dictionary<string, Updater.Fetched>(StringComparer.Ordinal);
            public readonly List<string> Asked = new List<string>();
            public Task<Updater.Fetched> Fetch(string url)
            {
                lock (Asked) Asked.Add(url);
                Updater.Fetched f;
                return Task.FromResult(Map.TryGetValue(url, out f) ? f : new Updater.Fetched { Status = 404, Error = "HTTP 404" });
            }
            public void Feed(string url, string json) { Map[url] = new Updater.Fetched { Status = 200, Body = Encoding.UTF8.GetBytes(json) }; }
            public void Fail(string url) { Map[url] = new Updater.Fetched { Status = 500, Error = "HTTP 500" }; }
            public void File(string url, byte[] body) { Map[url] = new Updater.Fetched { Status = 200, Body = body }; }
            public int Count(string url) { lock (Asked) return Asked.Count(u => u == url); }
        }

        const string SUrl = "https://example.invalid/releases/latest/download/latest.json", BUrl = "https://example.invalid/releases/download/beta/latest.json";
        static string Json(string ver, string sha, string commit = null, params string[] withdrawn)
        {
            return "{\"version\":\"" + ver + "\",\"dll\":\"https://example.invalid/dl/YazsCompanionMod-" + ver + ".dll\",\"sha256\":\"" + sha + "\",\"notes\":\"notes of " + ver + "\""
                + (commit != null ? ",\"commit\":\"" + commit + "\"" : "") + (withdrawn.Length > 0 ? ",\"withdrawn\":[" + string.Join(",", withdrawn.Select(w => "\"" + w + "\"")) + "]" : "") + "}";
        }
        static string DllUrl(string ver) { return "https://example.invalid/dl/YazsCompanionMod-" + ver + ".dll"; }

        static UpdateState Run(Updater.Setup s, List<string> lines)
        {
            Updater.ResetForTests();
            lines.Clear();
            Updater.Echo = (m, w) => { lock (lines) lines.Add((w ? "W " : "") + m); };
            Updater.CheckInBackground(s).GetAwaiter().GetResult();
            return Updater.State;
        }

        static void WholeChecks()
        {
            var why = new List<string>();
            var lines = new List<string>();
            // the build files: this bench's own DLL is a real assembly whose InformationalVersion says its version (R)
            string self = typeof(UpdaterCases).Assembly.Location;
            byte[] real = File.ReadAllBytes(self);
            string sha = Updater.Sha256(real);
            var R = Updater.FileVersion(self);
            if (R == null) { Check("UP9", "whole checks over in-memory feeds", false, "this bench's DLL says no version (" + Updater.BuildIdentity(self) + ")"); return; }
            string r0 = R.ToString(), r1 = new Version(R.Major, R.Minor, R.Build + 1).ToString(), r2 = new Version(R.Major, R.Minor, R.Build + 2).ToString();
            string dir = null;
            Func<string, string, bool, Net, Updater.Setup> setup = (runFile, runVer, on, net) => new Updater.Setup
            {
                StableUrl = SUrl, BetaUrl = BUrl, Running = Path.Combine(dir, runFile), Version = runVer, Commit = "1234567", Info = runVer + "+1234567", Dir = dir,
                CfgBeta = () => on, Env = () => null, AutoUpdate = () => true, Fetch = net.Fetch, Probe = false, RetryMs = 0,
            };
            Func<string> last = () => { lock (lines) return lines.LastOrDefault(l => l.StartsWith("[update] channel", StringComparison.Ordinal)) ?? "(no channel line)"; };
            Action<string, bool, string> need = (what, ok, detail) => { if (!ok) why.Add(what + ": " + detail + " | " + last()); };
            try
            {
                // a. on: the release runs, a test build is out -> downloaded
                dir = Scratch("check_a");
                Junk(dir, "YazsCompanionMod.dll");
                var net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Feed(BUrl, Json(r1, sha)); net.File(DllUrl(r1), real);
                var st = Run(setup("YazsCompanionMod.dll", r0, true, net), lines);
                need("a. download", st.Act == UpdateAct.Download && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1))) && Updater.Pending == r1 && Updater.PendingKind == "test build"
                    && last() == "[update] channel beta (Beta updates on): release " + r0 + ", newest " + r1 + ", running " + r0 + " = release - downloaded " + r1 + " (test build) - it runs from the next start", Listing(dir));
                // b. the same again: nothing more is downloaded
                st = Run(setup("YazsCompanionMod.dll", r0, true, net), lines);
                need("b. a second run", net.Count(DllUrl(r1)) == 1 && st.Result.Contains("already downloaded") && Updater.Pending == r1, "dll asked " + net.Count(DllUrl(r1)) + "x, " + st.Result);
                Directory.Delete(dir, true);

                // c. off on a published test build: the release downloaded, checked, this build set aside
                dir = Scratch("check_c");
                Junk(dir, UpdateFiles.Versioned(r1));
                net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Feed(BUrl, Json(r1, sha)); net.File(DllUrl(r0), real);
                st = Run(setup(UpdateFiles.Versioned(r1), r1, false, net), lines);
                need("c. step down", st.Act == UpdateAct.StepDown && st.SetAside == "done" && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r0))) && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1) + ".off"))
                    && !File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1))) && Updater.Pending == r0 && Updater.PendingKind == "release" && st.Label == "test build"
                    && last().StartsWith("[update] channel stable (Beta updates off): release " + r0 + ", newest " + r1 + ", running " + r1 + " = test build - stepped down: the release " + r0 + " downloaded and checked, this build set aside", StringComparison.Ordinal), Listing(dir));
                // n. On again before the restart: put back (Undo), the strip forgets the release
                st = RunKeep(setup(UpdateFiles.Versioned(r1), r1, true, net), lines);
                need("n. on again before the restart", st.Act == UpdateAct.UpToDate && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1))) && !File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1) + ".off")) && Updater.Pending == null, Listing(dir) + ", pending " + Updater.Pending);
                Directory.Delete(dir, true);

                // d. the kept release: no download (4)
                dir = Scratch("check_d");
                Junk(dir, UpdateFiles.Versioned(r1));
                File.WriteAllBytes(Path.Combine(dir, UpdateFiles.Keep(r0)), real);
                net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Feed(BUrl, Json(r1, sha));         // the release's DLL is NOT served
                st = Run(setup(UpdateFiles.Versioned(r1), r1, false, net), lines);
                need("d. the kept release", st.SetAside == "done" && net.Count(DllUrl(r0)) == 0 && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r0))) && !File.Exists(Path.Combine(dir, UpdateFiles.Keep(r0)))
                    && st.Result.Contains("the kept release " + r0 + " put back"), Listing(dir) + ", " + st.Result);
                Directory.Delete(dir, true);
                // d2. a kept build that is not the release's SHA-256 is removed at the check, and a kept build while the release runs too
                dir = Scratch("check_d2");
                Junk(dir, UpdateFiles.Versioned(r1));
                Junk(dir, UpdateFiles.Keep(r0), "another build");
                net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Feed(BUrl, Json(r1, sha));
                st = Run(setup(UpdateFiles.Versioned(r1), r1, true, net), lines);
                need("d2. a kept build of another SHA-256", !File.Exists(Path.Combine(dir, UpdateFiles.Keep(r0))), Listing(dir));
                Directory.Delete(dir, true);

                // e. the identity check: the feed says r2's lower neighbour, the file says r0 -> not installed, nothing set aside
                dir = Scratch("check_e");
                Junk(dir, UpdateFiles.Versioned(r2));
                net = new Net(); net.Feed(SUrl, Json(r1, sha)); net.Feed(BUrl, Json(r2, sha)); net.File(DllUrl(r1), real);
                st = Run(setup(UpdateFiles.Versioned(r2), r2, false, net), lines);
                need("e. the identity check", st.Result == "not installed: the file is not " + r1 && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r2))) && !File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1))) && st.SetAside == null, Listing(dir));
                // f. a SHA-256 mismatch
                net = new Net(); net.Feed(SUrl, Json(r0, "ff" + sha.Substring(2))); net.Feed(BUrl, Json(r2, sha)); net.File(DllUrl(r0), real);
                st = Run(setup(UpdateFiles.Versioned(r2), r2, false, net), lines);
                need("f. a sha256 mismatch", st.Result == "not installed: sha256 mismatch" && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r2))) && !File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r0))), Listing(dir));
                Directory.Delete(dir, true);

                // g. a withdrawn build steps back on either switch
                foreach (bool on in new[] { true, false })
                {
                    dir = Scratch("check_g");
                    Junk(dir, UpdateFiles.Versioned(r1));
                    net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Feed(BUrl, Json(r0, sha, null, r1)); net.File(DllUrl(r0), real);
                    st = Run(setup(UpdateFiles.Versioned(r1), r1, on, net), lines);
                    need("g. withdrawn, " + (on ? "on" : "off"), st.Act == UpdateAct.StepDown && st.SetAside == "done" && st.Label == "withdrawn test build", Listing(dir));
                    Directory.Delete(dir, true);
                }

                // h. critic 2: a test build downloaded while On, then Off before the restart -> set aside; and a pending build then withdrawn
                dir = Scratch("check_h");
                Junk(dir, "YazsCompanionMod.dll");
                Junk(dir, UpdateFiles.Versioned(r2), "a downloaded test build");
                net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Feed(BUrl, Json(r2, sha));
                Updater.ResetForTests(); Updater.Pending = r2;
                st = RunKeep(setup("YazsCompanionMod.dll", r0, false, net), lines);
                need("h. on, download, off before the restart", st.Act == UpdateAct.UpToDate && !File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r2))) && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r2) + ".off")) && Updater.Pending == null, Listing(dir));
                File.Move(Path.Combine(dir, UpdateFiles.Versioned(r2) + ".off"), Path.Combine(dir, UpdateFiles.Versioned(r2)));
                net.Feed(BUrl, Json(r0, sha, null, r2));
                Updater.ResetForTests(); Updater.Pending = r2;
                st = RunKeep(setup("YazsCompanionMod.dll", r0, true, net), lines);
                need("h. pending, then withdrawn", !File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r2))) && Updater.Pending == null, Listing(dir));
                Directory.Delete(dir, true);

                // i. critic 13: a failed beta feed - nothing moves, the label stays, one retry a little later
                dir = Scratch("check_i");
                Junk(dir, UpdateFiles.Versioned(r1));
                net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Fail(BUrl);
                var si = setup(UpdateFiles.Versioned(r1), r1, true, net); si.RetryMs = 40;
                st = Run(si, lines);
                // review fix: wait for the retry's check to END (not a fixed sleep: a slow CI runner could still be in it at the next case)
                for (int i = 0; i < 400 && (net.Count(BUrl) < 2 || Updater.Busy); i++) Thread.Sleep(20);
                need("i. the beta feed failed", st.Act == UpdateAct.Nothing && st.Label == "test build" && last().Contains("newest feed failed") && last().EndsWith("= test build - the test-builds feed did not answer", StringComparison.Ordinal)
                    && net.Count(BUrl) == 2 && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1))), "asked " + net.Count(BUrl) + "x");
                Directory.Delete(dir, true);

                // j. no beta feed yet (404): the release's rules
                dir = Scratch("check_j");
                Junk(dir, "YazsCompanionMod.dll");
                net = new Net(); net.Feed(SUrl, Json(r0, sha));
                st = Run(setup("YazsCompanionMod.dll", r0, true, net), lines);
                need("j. no beta feed", st.Act == UpdateAct.UpToDate && last() == "[update] channel beta (Beta updates on): release " + r0 + ", newest none, running " + r0 + " = release - up to date", st.Result);
                // k. auto-update off: nothing asked
                net = new Net(); var sk = setup("YazsCompanionMod.dll", r0, true, net); sk.AutoUpdate = () => false;
                st = Run(sk, lines);
                need("k. auto-update off", st.AutoUpdateOff && net.Asked.Count == 0 && last() == "[update] channel beta (Beta updates on): auto-update is off ([Update] AutoUpdate) - nothing checked", string.Join(",", net.Asked));
                // l. YAZS_BETA=0 beats the switch On
                net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Feed(BUrl, Json(r1, sha)); net.File(DllUrl(r1), real);
                var sl = setup("YazsCompanionMod.dll", r0, true, net); sl.Env = () => "0";
                st = Run(sl, lines);
                need("l. YAZS_BETA=0 over On", st.Act == UpdateAct.UpToDate && st.Switch.From == "env" && last().StartsWith("[update] channel stable (set by YAZS_BETA=0):", StringComparison.Ordinal) && net.Count(DllUrl(r1)) == 0, st.Result);
                Directory.Delete(dir, true);

                // m. the dev marker (7): naming this build -> nothing; naming another -> removed, the check goes on
                dir = Scratch("check_m");
                Junk(dir, "YazsCompanionMod.dll");
                net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Feed(BUrl, Json(r1, sha)); net.File(DllUrl(r1), real);
                File.WriteAllText(Path.Combine(dir, UpdateFiles.DevMarker), r0 + "+1234567\nsha256 0000\n");
                st = Run(setup("YazsCompanionMod.dll", r0, true, net), lines);
                need("m. the dev marker", st.Act == UpdateAct.Nothing && st.Dev == "1234567, dev_build.txt" && st.Label == "development build" && net.Count(DllUrl(r1)) == 0 && File.Exists(Path.Combine(dir, UpdateFiles.DevMarker)), st.Result);
                File.WriteAllText(Path.Combine(dir, UpdateFiles.DevMarker), "0.0.1+fedcba9\nsha256 0000\n");
                st = Run(setup("YazsCompanionMod.dll", r0, true, net), lines);
                need("m. a stale dev marker", st.Act == UpdateAct.Download && !File.Exists(Path.Combine(dir, UpdateFiles.DevMarker)), st.Result);
                // the marker by the running file's SHA-256 (a deploy whose InformationalVersion line differs)
                File.WriteAllText(Path.Combine(dir, UpdateFiles.DevMarker), "something else\nsha256 " + Updater.Sha256(File.ReadAllBytes(Path.Combine(dir, "YazsCompanionMod.dll"))).ToUpperInvariant() + "\n");
                st = Run(setup("YazsCompanionMod.dll", r0, true, net), lines);
                need("m. the dev marker by SHA-256", st.Act == UpdateAct.Nothing && st.Dev != null, st.Result);
                Directory.Delete(dir, true);

                // p. a refused rename during the check: .offnext, the footer's words
                dir = Scratch("check_p");
                Junk(dir, UpdateFiles.Versioned(r1));
                net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Feed(BUrl, Json(r1, sha)); net.File(DllUrl(r0), real);
                string pr = Path.Combine(dir, UpdateFiles.Versioned(r1));
                using (new FileStream(pr, FileMode.Open, FileAccess.Read, FileShare.Read))
                    st = Run(setup(UpdateFiles.Versioned(r1), r1, false, net), lines);
                // review fix (critic 3): the strip forgets the release - a restart would load this higher build again where no launcher helps
                need("p. a refused rename", st.SetAside != null && st.SetAside.StartsWith("refused", StringComparison.Ordinal) && File.Exists(pr + ".offnext")
                    && File.Exists(pr) && UpdateRules.Footer(st).Contains("cannot set the test build " + r1 + " aside") && lines.Any(l => l.StartsWith("W [update] could not set the test build aside", StringComparison.Ordinal))
                    && Updater.Pending == null && st.Pending == null, Listing(dir) + ", pending " + Updater.Pending);
                // v. the next start of that loop (no launcher came): the release stays a .dll beside the request (3); the check needs no download and promises no restart
                Updater.CleanupSiblings(pr, r1);
                need("v. the release stays beside the request", File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r0))) && !File.Exists(Path.Combine(dir, UpdateFiles.Keep(r0))) && File.Exists(pr + ".offnext"), Listing(dir));
                using (new FileStream(pr, FileMode.Open, FileAccess.Read, FileShare.Read))
                    st = Run(setup(UpdateFiles.Versioned(r1), r1, false, net), lines);
                need("v. the second start of the refused loop", st.SetAside != null && st.SetAside.StartsWith("refused", StringComparison.Ordinal) && net.Count(DllUrl(r0)) == 1 && Updater.Pending == null && File.Exists(pr + ".offnext"),
                    "dll asked " + net.Count(DllUrl(r0)) + "x, pending " + Updater.Pending + ", " + Listing(dir));
                // a later check that cannot tell (the beta feed failed) leaves the launcher's request alone; one that wants the build withdraws it
                net.Fail(BUrl);
                st = RunKeep(setup(UpdateFiles.Versioned(r1), r1, true, net), lines);
                need("p. the request kept while the feed failed", st.Act == UpdateAct.Nothing && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1) + ".offnext")), Listing(dir));
                net.Feed(BUrl, Json(r1, sha));
                st = RunKeep(setup(UpdateFiles.Versioned(r1), r1, true, net), lines);
                need("p. the request withdrawn when On again", st.Act == UpdateAct.UpToDate && !File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1) + ".offnext")), Listing(dir));
                Directory.Delete(dir, true);

                // o. a hand-set feed: forward only, the beta feed never asked
                dir = Scratch("check_o");
                Junk(dir, UpdateFiles.Versioned(r1));
                net = new Net(); net.Feed(SUrl, Json(r0, sha));
                var so = setup(UpdateFiles.Versioned(r1), r1, false, net); so.BetaUrl = null;
                st = Run(so, lines);
                need("o. a hand-set feed", st.Act == UpdateAct.Nothing && net.Count(BUrl) == 0 && st.Result == "a hand-set feed (UpdateUrl) only goes forward" && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1))), st.Result);
                // q. the feed's commit (5 / 7): the release's version from another commit is a dev build
                net = new Net(); net.Feed(SUrl, Json(r1, sha, "fedcba9")); net.Feed(BUrl, Json(r1, sha, "fedcba9"));
                st = Run(setup(UpdateFiles.Versioned(r1), r1, true, net), lines);
                need("q. published from another commit", st.Act == UpdateAct.Nothing && st.Dev == "1234567, published as fedcba9", st.Result);
                net = new Net(); net.Feed(SUrl, Json(r1, sha, "1234567aaaa")); net.Feed(BUrl, Json(r1, sha, "1234567aaaa"));
                st = Run(setup(UpdateFiles.Versioned(r1), r1, true, net), lines);
                need("q. published from this commit", st.Act == UpdateAct.UpToDate && st.Dev == null && st.Label == "release", st.Result);
                Directory.Delete(dir, true);

                // ---- review fixes (2026-10-12)
                // r. critic 4a: a test build downloaded while the release runs (its very bytes) - the release goes to its .keep in the same check
                dir = Scratch("check_r");
                string rel = Path.Combine(dir, "YazsCompanionMod.dll");
                File.WriteAllBytes(rel, real);
                net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Feed(BUrl, Json(r1, sha)); net.File(DllUrl(r1), real);
                st = Run(setup("YazsCompanionMod.dll", r0, true, net), lines);
                string keep0 = Path.Combine(dir, UpdateFiles.Keep(r0));
                var rec = Updater.ReadRelease(dir);
                need("r. the release kept at the download (4a)", st.Act == UpdateAct.Download && File.Exists(keep0) && Updater.Sha256(File.ReadAllBytes(keep0)) == sha && rec != null && rec.Version.ToString() == r0 && rec.Sha == sha, Listing(dir));
                // the test build's first start: the only .keep is the release, the plain release goes; an offline check keeps it (and the menu names no kind)
                string t1 = Path.Combine(dir, UpdateFiles.Versioned(r1));
                Updater.CleanupSiblings(t1, r1);
                need("r. the test build's first start", File.Exists(keep0) && !File.Exists(rel) && Directory.GetFiles(dir, "*.dll.keep").Length == 1, Listing(dir));
                net = new Net(); net.Fail(SUrl); net.Fail(BUrl);
                st = Run(setup(UpdateFiles.Versioned(r1), r1, true, net), lines);
                need("r. an offline check keeps the release's .keep", st.Offline && File.Exists(keep0) && Updater.Sha256(File.ReadAllBytes(keep0)) == Updater.ReadRelease(dir).Sha && st.Label == ""
                    && last().EndsWith("= test build - no feed answered", StringComparison.Ordinal), Listing(dir));
                Directory.Delete(dir, true);

                // t. critic 2 with 13: On, a test build downloaded, Off before the restart while the test-builds feed fails - set aside all the same
                dir = Scratch("check_t");
                Junk(dir, UpdateFiles.Versioned(r1));
                string t2 = Path.Combine(dir, UpdateFiles.Versioned(r2));
                Junk(dir, UpdateFiles.Versioned(r2), "a downloaded test build");
                net = new Net(); net.Feed(SUrl, Json(r0, sha)); net.Fail(BUrl);
                Updater.ResetForTests(); Updater.Pending = r2; Updater.PendingKind = "test build";
                st = RunKeep(setup(UpdateFiles.Versioned(r1), r1, false, net), lines);
                need("t. off while the test-builds feed fails", st.Act == UpdateAct.Nothing && st.Result == "the test-builds feed did not answer" && !File.Exists(t2) && File.Exists(t2 + ".off") && Updater.Pending == null && st.Pending == null
                    && File.Exists(Path.Combine(dir, UpdateFiles.Versioned(r1))), Listing(dir));
                // u. ... while the release feed fails: the test build this session downloaded goes (the release is not known)
                File.Move(t2 + ".off", t2);
                net = new Net(); net.Fail(SUrl); net.Feed(BUrl, Json(r2, sha));
                Updater.ResetForTests(); Updater.Pending = r2; Updater.PendingKind = "test build";
                st = RunKeep(setup(UpdateFiles.Versioned(r1), r1, false, net), lines);
                need("u. off while the release feed fails", st.Act == UpdateAct.Nothing && last().EndsWith("- the release feed did not answer", StringComparison.Ordinal) && !File.Exists(t2) && File.Exists(t2 + ".off") && Updater.Pending == null, Listing(dir));
                // ... and a pending RELEASE stays (the one Off wants)
                File.Move(t2 + ".off", t2);
                Updater.ResetForTests(); Updater.Pending = r2; Updater.PendingKind = "release";
                st = RunKeep(setup(UpdateFiles.Versioned(r1), r1, false, net), lines);
                need("u. a pending release stays", File.Exists(t2) && Updater.Pending == r2, Listing(dir));
                Directory.Delete(dir, true);

                // w. critic 7: a dev_build.txt naming this build gives way when a feed publishes these very bytes (the deploy was the released build)
                dir = Scratch("check_w");
                File.WriteAllBytes(Path.Combine(dir, "YazsCompanionMod.dll"), real);
                string mk = Path.Combine(dir, UpdateFiles.DevMarker);
                File.WriteAllText(mk, r0 + "+1234567\nsha256 " + sha.ToUpperInvariant() + "\n");
                net = new Net(); net.Feed(SUrl, Json(r0, sha, "1234567")); net.Feed(BUrl, Json(r0, sha, "1234567"));
                st = Run(setup("YazsCompanionMod.dll", r0, true, net), lines);
                need("w. the marker gives way to the published bytes", st.Dev == null && st.Label == "release" && st.Act == UpdateAct.UpToDate && !File.Exists(mk), st.Result + ", " + Listing(dir));
                File.WriteAllText(mk, r0 + "+1234567\nsha256 " + sha + "\n");
                net = new Net(); net.Feed(SUrl, Json(r0, sha, "fedcba9")); net.Feed(BUrl, Json(r0, sha, "fedcba9"));
                st = Run(setup("YazsCompanionMod.dll", r0, true, net), lines);
                need("w. ... not when the feed names another commit", st.Dev != null && File.Exists(mk), st.Result);
                Directory.Delete(dir, true);

                // x. the pull window: the release feed still names the withdrawn r1, the test-builds feed is back at r0 - nothing downloaded
                dir = Scratch("check_x");
                Junk(dir, "YazsCompanionMod.dll");
                net = new Net(); net.Feed(SUrl, Json(r1, sha)); net.Feed(BUrl, Json(r0, sha, null, r1)); net.File(DllUrl(r1), real);
                st = Run(setup("YazsCompanionMod.dll", r0, true, net), lines);
                need("x. the pull window", st.Act == UpdateAct.UpToDate && net.Count(DllUrl(r1)) == 0 && Updater.ReadRelease(dir) == null, st.Result + ", " + Listing(dir));
                Directory.Delete(dir, true);
                dir = null;
            }
            catch (Exception e) { why.Add("threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()); }
            finally { if (dir != null) try { Directory.Delete(dir, true); } catch { } Updater.Echo = null; }
            Check("UP9", "whole checks over in-memory feeds (R = " + r0 + "): download, a second run downloads nothing, step down with the identity check, On again before the restart puts it back, the kept release with no download (4), a kept build of another SHA-256 removed, the identity and SHA-256 refusals, a withdrawn build on either switch, a pending test build set aside when Off and a pending build then withdrawn (2), a failed beta feed with one retry (13), no beta feed, auto-update off, YAZS_BETA=0 over On, the dev marker by version and by SHA-256 and a stale one (7), a refused rename (3), a hand-set feed, the feed's commit (5); review fixes: a refused rename forgets the pending download and its next start needs no download (3), the release kept at the download of a test build and the only .keep after (4a), Off with either feed failing still sets the pending test build aside (2 with 13), the marker gives way to the published bytes (7), the pull window downloads nothing",
                why.Count == 0, string.Join("; ", why.Take(4)));
        }

        /// <summary>A whole check without ResetForTests (the case set Pending itself).</summary>
        static UpdateState RunKeep(Updater.Setup s, List<string> lines)
        {
            lines.Clear();
            Updater.Echo = (m, w) => { lock (lines) lines.Add((w ? "W " : "") + m); };
            Updater.CheckInBackground(s).GetAwaiter().GetResult();
            return Updater.State;
        }

        // ================================================================ UP10: a REAL loaded DLL
        static void LoadedDll()
        {
            var why = new List<string>();
            string dir = Scratch("loaded");
            string copy = Path.Combine(dir, "YazsCompanionMod-9.9.9.dll");
            bool deleteRefused = false;
            try
            {
                File.Copy(typeof(UpdaterCases).Assembly.Location, copy);
                LoadAndRetire(copy, why, out deleteRefused);
            }
            catch (Exception e) { why.Add("threw " + e.GetType().Name + ": " + e.Message); }
            // the context is collectible: once it is gone the file is free again and the folder goes
            for (int i = 0; i < 5 && Directory.Exists(dir); i++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers();
                try { Directory.Delete(dir, true); } catch { Thread.Sleep(50); }
            }
            Check("UP10", "a REAL loaded DLL (a scratch copy of this bench's DLL loaded into its own context, as BepInEx loads a plugin; its delete " + (deleteRefused ? "refused" : "allowed") + " while loaded): set aside by rename while loaded, put back, probed",
                why.Count == 0, string.Join("; ", why));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void LoadAndRetire(string copy, List<string> why, out bool deleteRefused)
        {
            deleteRefused = false;
            var alc = new AssemblyLoadContext("yazs-bench-loaded-" + Guid.NewGuid().ToString("N").Substring(0, 6), isCollectible: true);
            try
            {
                var asm = alc.LoadFromAssemblyPath(copy);           // as BepInEx loads a plugin: the image is mapped from this file
                if (!string.Equals(asm.Location, copy, StringComparison.OrdinalIgnoreCase)) why.Add("loaded from " + asm.Location);
                try { File.Delete(copy); } catch { deleteRefused = true; }
                if (!File.Exists(copy)) { why.Add("the loaded file could be deleted - not really loaded?"); return; }
                string r = Updater.Retire(copy);
                if (r != "done" || File.Exists(copy) || !File.Exists(copy + ".off")) why.Add("Retire of the loaded file: " + r);
                if (asm.GetTypes().Length == 0) why.Add("the loaded assembly lost its types");
                if (!Updater.Undo(copy) || !File.Exists(copy)) why.Add("Undo of the loaded file");
                string p = Updater.Probe(copy);
                if (p != "works") why.Add("the probe on the loaded file: " + p);
            }
            finally { alc.Unload(); }
        }

        // ================================================================ UP11: the sources
        static void Sources()
        {
            var why = new List<string>();
            string plugin = ModSrc("YazsCompanion.Mod/Plugin.cs"), menu = ModSrc("YazsCompanion.Mod/Menu.cs"), notice = ModSrc("YazsCompanion.Mod/Notice.cs"), proj = ModSrc("YazsCompanion.Mod/YazsCompanion.Mod.csproj"),
                rs = ModSrc("release.ps1"), bench = ModSrc("tools/ItemBench/ItemBench.csproj"), drill = ModSrc("tools/updater_drill.ps1");
            if (plugin == null || menu == null || notice == null || proj == null || rs == null) { Check("UP11", "the sources", false, "a source file is missing"); return; }
            if (!plugin.Contains("Config.Bind(\"Update\", \"BetaUpdates\", true, \"Take test builds")) why.Add("Plugin: [Update] BetaUpdates, default true (D2)");
            if (!plugin.Contains("public const string DefaultBetaUrl = \"https://github.com/bidoingg/YazsCompanion/releases/download/beta/latest.json\";")) why.Add("Plugin: the beta feed's url");
            int check = plugin.IndexOf("Updater.CheckInBackground(UpdaterSetup())", StringComparison.Ordinal), know = plugin.IndexOf("Knowledge.Load(", StringComparison.Ordinal), patch = plugin.IndexOf("harmony.CreateClassProcessor", StringComparison.Ordinal);
            if (check < 0 || know < 0 || check > know || check > patch) why.Add("Plugin: the check before the knowledge load and the patches");
            if (!plugin.Contains("Updater.Recheck(UpdaterSetup()); Menu.SettingSaved(written, null, \"applies at the next start\");")) why.Add("Plugin: the recheck on a change of [Update]");
            if (!menu.Contains("const string BetaRow = \"di:beta\";") || !menu.Contains("\"Beta updates\", null, () => UpdateRules.RowValue(BetaOn())") || !menu.Contains("Beta updates at the next start")) why.Add("Menu: the DISPLAY row and the lead");
            if (!menu.Contains("internal static bool InRun()") || !notice.Contains("pending == null || Menu.InRun()")) why.Add("Notice: hidden in play");
            if (!menu.Contains("UpdateRules.HeaderTag(Updater.State.Label)")) why.Add("Menu: the header tag");
            if (!proj.Contains("dev_build.txt") || !proj.Contains("DeployOverRelease") || !proj.Contains("YazsCompanionMod-*.dll") || !proj.Contains("*.dll.offnext")) why.Add("csproj: the deploy hygiene, the marker and the guard");
            foreach (var s in new[] { "[switch]$Beta", "[string]$Promote", "[string]$Pull", "[string]$VerdictFile", "--prerelease", "withdrawn", "--clobber", "commit    = $id.Commit", "$Floor = [version]'0.16.2'", "function Get-LocalDeploy([string]$PluginDir" })
                if (!rs.Contains(s)) why.Add("release.ps1 lacks " + s);
            // 26 (review fix: no private folder names in this public bench either): the only path outside this repository release.ps1
            // may name is the maintainer's name scan, and no per-version verdict file
            foreach (Match m in Regex.Matches(rs, @"\.\.\\[^\s'""`)]+"))
                if (!m.Value.EndsWith("\\franchise_scan.ps1", StringComparison.Ordinal)) why.Add("26: release.ps1 names a path outside this repository (" + m.Value + ")");
            if (rs.Contains("verdict_" + "<")) why.Add("26: release.ps1 names a verdict file's private layout");
            if (!rs.Contains("Remove-DevMarker $pluginDir") || !rs.Contains("$ld.Act -eq 'same'")) why.Add("release.ps1: dev_build.txt removed when the PC already runs the released DLL (7)");
            // integration fix 2026-10-12 (critic 21's late upload): -Promote checks the verdict's Deck part by the folder set, not by time
            if (!rs.Contains("[string]$DeckLogs") || !rs.Contains("$ds = Get-DeckSet $DeckLogs") || !rs.Contains("'Deck not read: ([^)]*)'")) why.Add("release.ps1: -Promote checks the verdict's Deck uploads by their folder set (21)");
            if (bench == null || !bench.Contains("UpdateRules.cs")) why.Add("the bench links UpdateRules.cs");
            if (drill == null || !drill.Contains("--updater-drill") || !drill.Contains("http://localhost:")) why.Add("tools\\updater_drill.ps1");
            Check("UP11", "the sources: [Update] BetaUpdates default true, the beta feed's url, the check before the knowledge load and the patches, the recheck on a change, the DISPLAY row and lead, the strip off in play, the header tag, the deploy hygiene + marker + guard, release.ps1's switches with no path outside the repository but the name scan (26), the marker removed when the PC runs the released DLL (7) and -Promote's Deck folder set (21), the drill",
                why.Count == 0, string.Join("; ", why));
        }
    }
}
