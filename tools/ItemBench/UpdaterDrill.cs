// 0.16.2 (the beta channel, WP1 of the plan; critic 20): the host of tools\updater_drill.ps1 - the REAL update check
// (Updater.CheckInBackground: HttpClient, the files, the renames) against a feed served from http://localhost:<port> by
// "python -I -m http.server --bind 127.0.0.1", over a scratch plugin folder per scene whose running DLL is loaded the way
// BepInEx loads a plugin (its own load context: the image mapped from the file).
//   ItemBench --updater-drill --root <scratch> --base http://localhost:<port> --ip-base http://127.0.0.1:<port> --dlls <folder> --closed <port>
//   <folder> holds the made-up builds updater_drill.ps1 compiles: v0.16.1.dll, v0.16.2.dll, v0.16.3.dll, v0.16.4.dll
//   (InformationalVersion "<v>+drill<n>") and liar.dll (it says 0.16.9); <root>\feed is the served folder (stable\latest.json,
//   beta\latest.json, dl\*.dll), rewritten scene by scene. Every scene prints PASS or FAIL; the last line is "updater drill: N of
//   M PASS" and the exit code is 0 only when every scene passed.
//   Review fixes (2026-10-12): D7 also asserts no restart is promised after a refused rename and that its next start needs no
//   download; D14 / D15 Off with the test-builds / release feed not answering still sets the downloaded test build aside; D16 -
//   D18 the kept release is the release (copied at a test build's download, the only .keep after test build to test build,
//   kept through an offline check; none after a new release or at the first 0.16.2 start).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class UpdaterDrill
    {
        static string _root, _base, _ipBase, _dlls, _feed;
        static int _closed, _pass, _all;
        static readonly List<string> _lines = new List<string>();
        static readonly List<AssemblyLoadContext> _loaded = new List<AssemblyLoadContext>();

        static string Arg(string[] args, string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }

        public static int Run(string[] args)
        {
            _root = Arg(args, "--root"); _base = (Arg(args, "--base") ?? "").TrimEnd('/'); _ipBase = (Arg(args, "--ip-base") ?? "").TrimEnd('/'); _dlls = Arg(args, "--dlls");
            int.TryParse(Arg(args, "--closed") ?? "0", out _closed);
            if (_root == null || _dlls == null || !_base.StartsWith("http://localhost:", StringComparison.Ordinal))
            {
                Console.WriteLine("updater drill: --root, --dlls and --base http://localhost:<port> are needed (run tools\\updater_drill.ps1)");
                return 2;
            }
            _feed = Path.Combine(_root, "feed");
            foreach (var d in new[] { "stable", "beta", "dl" }) Directory.CreateDirectory(Path.Combine(_feed, d));
            foreach (var v in new[] { "0.16.2", "0.16.3", "0.16.4" }) File.Copy(Dll(v), Path.Combine(_feed, "dl", UpdateFiles.Versioned(v)), true);
            File.Copy(Path.Combine(_dlls, "liar.dll"), Path.Combine(_feed, "dl", "YazsCompanionMod-0.16.3-liar.dll"), true);
            Updater.Echo = (m, w) => { lock (_lines) _lines.Add((w ? "W " : "") + m); };     // the lines themselves go to stderr (Stubs.cs)
            Console.WriteLine("updater drill: the real check against " + _base + " (closed port " + _closed + ")");

            Scene("D1", "download a test build (On), the rename probe on the loaded DLL", () =>
            {
                Stable("0.16.2"); Beta("0.16.3");
                string run = Plugin(1, "YazsCompanionMod.dll", "0.16.2");
                var st = Check(run, true, probe: true);
                string got = Path.Combine(Path.GetDirectoryName(run), UpdateFiles.Versioned("0.16.3"));
                return Need(st.Act == UpdateAct.Download && File.Exists(got) && Updater.FileVersion(got).ToString() == "0.16.3" && st.Probe == "works" && Saw("[update] setting this build aside works here")
                    && Saw("[update] channel beta (Beta updates on): release 0.16.2, newest 0.16.3, running 0.16.2 = release - downloaded 0.16.3 (test build) - it runs from the next start"), st);
            });
            Scene("D2", "a second run downloads nothing more", () =>
            {
                string run = Path.Combine(_root, "p1", "YazsCompanionMod.dll"), got = Path.Combine(_root, "p1", UpdateFiles.Versioned("0.16.3"));
                var before = File.GetLastWriteTimeUtc(got);
                var st = Check(run, true);
                return Need(st.Result.Contains("already downloaded") && File.GetLastWriteTimeUtc(got) == before, st);
            });
            Scene("D3", "Off on a published test build: the release downloaded, checked, the loaded test build set aside", () =>
            {
                Stable("0.16.2"); Beta("0.16.3");
                string run = Plugin(3, UpdateFiles.Versioned("0.16.3"), "0.16.3");
                var st = Check(run, false);
                string dir = Path.GetDirectoryName(run);
                return Need(st.Act == UpdateAct.StepDown && st.SetAside == "done" && File.Exists(Path.Combine(dir, UpdateFiles.Versioned("0.16.2"))) && File.Exists(run + ".off") && !File.Exists(run), st);
            });
            Scene("D4", "a withdrawn test build steps back to the build the feed names (On)", () =>
            {
                Stable("0.16.2"); Beta("0.16.3", null, "0.16.4");
                string run = Plugin(4, UpdateFiles.Versioned("0.16.4"), "0.16.4");
                var st = Check(run, true);
                string dir = Path.GetDirectoryName(run);
                return Need(st.Act == UpdateAct.StepDown && st.Label == "withdrawn test build" && st.SetAside == "done" && File.Exists(Path.Combine(dir, UpdateFiles.Versioned("0.16.3"))) && File.Exists(run + ".off"), st);
            });
            Scene("D5", "a SHA-256 mismatch: nothing installed, nothing set aside", () =>
            {
                Stable("0.16.2", null, null, "00" + new string('f', 62)); Beta("0.16.3");
                string run = Plugin(5, UpdateFiles.Versioned("0.16.3"), "0.16.3");
                var st = Check(run, false);
                return Need(st.Result == "not installed: sha256 mismatch" && File.Exists(run) && !File.Exists(Path.Combine(Path.GetDirectoryName(run), UpdateFiles.Versioned("0.16.2"))), st);
            });
            Scene("D6", "the version check: a file that says it is another build is not installed, nothing set aside", () =>
            {
                Stable("0.16.3", "YazsCompanionMod-0.16.3-liar.dll"); Beta("0.16.4");
                string run = Plugin(6, UpdateFiles.Versioned("0.16.4"), "0.16.4");
                var st = Check(run, false);
                return Need(st.Result == "not installed: the file is not 0.16.3" && File.Exists(run) && !File.Exists(run + ".off"), st);
            });
            Scene("D7", "a refused rename (the file held without delete sharing): a request for a launcher, the build keeps running, no restart promised; its next start needs no download", () =>
            {
                Stable("0.16.2"); Beta("0.16.3");
                string run = Plugin(7, UpdateFiles.Versioned("0.16.3"), "0.16.3");
                string dir = Path.GetDirectoryName(run), rel = Path.Combine(dir, UpdateFiles.Versioned("0.16.2"));
                UpdateState st;
                using (new FileStream(run, FileMode.Open, FileAccess.Read, FileShare.Read)) st = Check(run, false);
                // review fix (critic 3): the strip forgets the release - BepInEx loads 0.16.3 again unless a launcher completes the request
                string first = Need(st.SetAside != null && st.SetAside.StartsWith("refused", StringComparison.Ordinal) && File.Exists(run) && File.Exists(run + ".offnext")
                    && File.Exists(rel) && Saw("W [update] could not set the test build aside") && Updater.Pending == null, st);
                if (first != null) return "first start: " + first;
                // the next start, no launcher came: the release stays a .dll beside the request, the check finds it there (no download)
                Updater.CleanupSiblings(run, "0.16.3");
                if (!File.Exists(rel) || File.Exists(Path.Combine(dir, UpdateFiles.Keep("0.16.2"))) || !File.Exists(run + ".offnext")) return "the next start's tidy: " + Listing(dir);
                using (new FileStream(run, FileMode.Open, FileAccess.Read, FileShare.Read)) st = Check(run, false);
                return Need(st.SetAside != null && st.SetAside.StartsWith("refused", StringComparison.Ordinal) && Saw("[update] 0.16.2 is already downloaded") && !Saw("[update] 0.16.2 (release) to go back to")
                    && Updater.Pending == null && File.Exists(run + ".offnext"), st);
            });
            Scene("D8", "the kept release goes back without a download (the feed's DLL is not even there)", () =>
            {
                Stable("0.16.2", "missing-0.16.2.dll", null, Updater.Sha256(File.ReadAllBytes(Dll("0.16.2")))); Beta("0.16.3");
                string run = Plugin(8, UpdateFiles.Versioned("0.16.3"), "0.16.3");
                string dir = Path.GetDirectoryName(run);
                File.Copy(Dll("0.16.2"), Path.Combine(dir, UpdateFiles.Keep("0.16.2")));
                var st = Check(run, false);
                return Need(st.SetAside == "done" && st.Result.Contains("the kept release 0.16.2 put back") && File.Exists(Path.Combine(dir, UpdateFiles.Versioned("0.16.2"))) && !File.Exists(Path.Combine(dir, UpdateFiles.Keep("0.16.2"))), st);
            });
            Scene("D9", "a test build downloaded while On, the switch Off before the restart: set aside, the strip forgets it", () =>
            {
                Stable("0.16.2"); Beta("0.16.4");
                string run = Plugin(9, "YazsCompanionMod.dll", "0.16.2");
                string dir = Path.GetDirectoryName(run);
                File.Copy(Dll("0.16.4"), Path.Combine(dir, UpdateFiles.Versioned("0.16.4")));
                var st = Check(run, false, pending: "0.16.4");
                return Need(st.Act == UpdateAct.UpToDate && File.Exists(Path.Combine(dir, UpdateFiles.Versioned("0.16.4") + ".off")) && !File.Exists(Path.Combine(dir, UpdateFiles.Versioned("0.16.4"))) && Updater.Pending == null, st);
            });
            Scene("D10", "the test-builds feed does not answer (a closed port): nothing moves, the label stays", () =>
            {
                Stable("0.16.2"); Beta("0.16.3");
                string run = Plugin(10, UpdateFiles.Versioned("0.16.3"), "0.16.3");
                var st = Check(run, true, betaUrl: "http://localhost:" + _closed + "/beta/latest.json");
                return Need(st.Act == UpdateAct.Nothing && st.Label == "test build" && st.NewestState == FeedState.Failed && st.Result == "the test-builds feed did not answer" && File.Exists(run), st);
            });
            Scene("D11", "no test-builds feed yet (404): the release's rules", () =>
            {
                Stable("0.16.2"); NoFeed("beta");
                string run = Plugin(11, "YazsCompanionMod.dll", "0.16.2");
                var st = Check(run, true);
                return Need(st.Act == UpdateAct.UpToDate && st.NewestState == FeedState.Absent && Saw("[update] channel beta (Beta updates on): release 0.16.2, newest none, running 0.16.2 = release - up to date"), st);
            });
            Scene("D12", "a development deploy's dev_build.txt: updates leave it alone - until a feed publishes these very bytes (review fix: the deploy was the released build)", () =>
            {
                Stable("0.16.2", null, null, "00" + new string('f', 62)); Beta("0.16.3");      // the published 0.16.2 is other bytes: this is a local build of it
                string run = Plugin(12, "YazsCompanionMod.dll", "0.16.2");
                string marker = Path.Combine(Path.GetDirectoryName(run), UpdateFiles.DevMarker);
                File.WriteAllText(marker, "0.16.2+drill162\n");
                var st = Check(run, true);
                if (!(st.Act == UpdateAct.Nothing && st.Label == "development build" && !File.Exists(Path.Combine(Path.GetDirectoryName(run), UpdateFiles.Versioned("0.16.3"))) && File.Exists(marker)))
                    return "a local build of the published version: " + Need(false, st);
                Stable("0.16.2");                                                               // the feed publishes these very bytes
                st = Check(run, false);
                return Need(st.Act == UpdateAct.UpToDate && st.Label == "release" && st.Dev == null && !File.Exists(marker), st);
            });
            Scene("D13", "a feed at 127.0.0.1 naming a plain-http DLL is refused (the exemption is http://localhost only)", () =>
            {
                if (_ipBase.Length == 0) return "no --ip-base";
                Stable("0.16.3", null, _ipBase);
                string run = Plugin(13, "YazsCompanionMod.dll", "0.16.2");
                var st = Check(run, true, stableUrl: _ipBase + "/stable/latest.json", betaUrl: "");
                return Need(st.Result == "not installed: a non-https download url" && !File.Exists(Path.Combine(Path.GetDirectoryName(run), UpdateFiles.Versioned("0.16.3"))), st);
            });

            // ---- review fixes (2026-10-12, two adversarial reviews)
            string closed = "http://localhost:" + _closed;
            Scene("D14", "critic 2 with 13: a test build downloaded while On, Off before the restart while the test-builds feed does not answer - set aside all the same", () =>
            {
                Stable("0.16.2"); Beta("0.16.3");
                string run = Plugin(14, UpdateFiles.Versioned("0.16.3"), "0.16.3");
                string next = Path.Combine(Path.GetDirectoryName(run), UpdateFiles.Versioned("0.16.4"));
                File.Copy(Dll("0.16.4"), next);
                var st = Check(run, false, pending: "0.16.4", pendingKind: "test build", betaUrl: closed + "/beta/latest.json");
                return Need(st.Act == UpdateAct.Nothing && st.Result == "the test-builds feed did not answer" && !File.Exists(next) && File.Exists(next + ".off") && Updater.Pending == null && File.Exists(run), st);
            });
            Scene("D15", "... the same while the release feed does not answer - the test build this session downloaded goes", () =>
            {
                Stable("0.16.2"); Beta("0.16.4");
                string run = Plugin(15, UpdateFiles.Versioned("0.16.3"), "0.16.3");
                string next = Path.Combine(Path.GetDirectoryName(run), UpdateFiles.Versioned("0.16.4"));
                File.Copy(Dll("0.16.4"), next);
                var st = Check(run, false, pending: "0.16.4", pendingKind: "test build", stableUrl: closed + "/stable/latest.json");
                return Need(st.Act == UpdateAct.Nothing && st.Result == "the release feed did not answer" && !File.Exists(next) && File.Exists(next + ".off") && Updater.Pending == null, st);
            });
            Scene("D16", "critic 4: the kept release IS the release - copied when a test build comes (4a), the only .keep after test build to test build offline, kept through an offline check", () =>
            {
                Stable("0.16.2"); Beta("0.16.3");
                string run = Place(16, "YazsCompanionMod.dll", "0.16.2");          // the release by hand / by release.ps1: the feed's very bytes
                string dir = Path.GetDirectoryName(run), keep = Path.Combine(dir, UpdateFiles.Keep("0.16.2"));
                string t3 = Path.Combine(dir, UpdateFiles.Versioned("0.16.3")), t4 = Path.Combine(dir, UpdateFiles.Versioned("0.16.4"));
                var st = Check(run, true);
                if (!(st.Act == UpdateAct.Download && File.Exists(t3) && File.Exists(keep) && Sha(keep) == Sha(Dll("0.16.2")))) return "4a, the release kept at the download: " + Need(false, st) + " | " + Listing(dir);
                Updater.CleanupSiblings(t3, "0.16.3");                              // 0.16.3's first start, offline
                if (!(File.Exists(keep) && !File.Exists(run) && Keeps(dir) == 1)) return "0.16.3's first start: " + Listing(dir);
                st = Check(t3, true, stableUrl: closed + "/stable/latest.json", betaUrl: closed + "/beta/latest.json");      // an offline check
                var rec = Updater.ReadRelease(dir);
                if (!(st.Offline && st.Label == "" && File.Exists(keep) && rec != null && rec.Version.ToString() == "0.16.2" && rec.Sha == Sha(keep))) return "an offline check: " + Need(false, st) + " | " + Listing(dir);
                Beta("0.16.4");
                st = Check(t3, true);                                              // 0.16.4 comes (On)
                if (!(st.Act == UpdateAct.Download && File.Exists(t4))) return "0.16.4's download: " + Need(false, st);
                Updater.CleanupSiblings(t4, "0.16.4");                              // 0.16.4's first start, offline (S1)
                return File.Exists(keep) && Keeps(dir) == 1 && !File.Exists(t3) && File.Exists(t4) ? null : "S1, test build to test build: " + Listing(dir);
            });
            Scene("D17", "S2: the first start of a new release, offline - no .keep, the old release goes", () =>
            {
                Stable("0.16.2"); Beta("0.16.2");
                string run = Place(17, "YazsCompanionMod.dll", "0.16.2");
                string dir = Path.GetDirectoryName(run), t3 = Path.Combine(dir, UpdateFiles.Versioned("0.16.3"));
                var st = Check(run, false);
                if (st.Act != UpdateAct.UpToDate) return "the release up to date: " + Need(false, st);
                Stable("0.16.3"); Beta("0.16.3");
                st = Check(run, false);
                if (!(st.Act == UpdateAct.Download && File.Exists(t3) && Keeps(dir) == 0)) return "the new release downloaded: " + Need(false, st) + " | " + Listing(dir);
                Updater.CleanupSiblings(t3, "0.16.3");
                return Keeps(dir) == 0 && !File.Exists(run) && File.Exists(t3) ? null : "S2, the new release's first start: " + Listing(dir);
            });
            Scene("D18", "S2b: the first 0.16.2 start coming from 0.16.1 (no release recorded yet) - nothing kept", () =>
            {
                string old = Place(18, "YazsCompanionMod.dll", "0.16.1");
                string dir = Path.GetDirectoryName(old), t2 = Path.Combine(dir, UpdateFiles.Versioned("0.16.2"));
                File.Copy(Dll("0.16.2"), t2);
                Updater.CleanupSiblings(t2, "0.16.2");
                return Keeps(dir) == 0 && !File.Exists(old) && File.Exists(t2) ? null : Listing(dir);
            });

            Updater.Echo = null;
            Console.WriteLine("updater drill: " + _pass + " of " + _all + " PASS");
            return _pass == _all ? 0 : 1;
        }

        static string Dll(string v) { return Path.Combine(_dlls, "v" + v + ".dll"); }

        static void Scene(string id, string what, Func<string> body)
        {
            _all++;
            string fail;
            Console.WriteLine("  " + id + " " + what);
            try { fail = body(); }
            catch (Exception e) { fail = "threw " + e.GetType().Name + ": " + e.Message; }
            if (fail == null) _pass++;
            Console.WriteLine("  " + (fail == null ? "PASS" : "FAIL") + " " + id + (fail == null ? "" : ": " + fail));
        }

        static string Need(bool ok, UpdateState st)
        {
            return ok ? null : "act " + st.Act + ", label '" + st.Label + "', result '" + st.Result + "', set aside '" + st.SetAside + "', pending " + (Updater.Pending ?? "none");
        }

        static bool Saw(string start) { lock (_lines) return _lines.Any(l => l.StartsWith(start, StringComparison.Ordinal)); }
        static string Listing(string dir) { return string.Join(", ", Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(s => s, StringComparer.OrdinalIgnoreCase)); }
        static int Keeps(string dir) { return Directory.GetFiles(dir, "*.dll.keep").Length; }
        static string Sha(string path) { return Updater.Sha256(File.ReadAllBytes(path)); }

        /// <summary>A plugin folder p&lt;n&gt; holding the made-up build &lt;ver&gt; as &lt;name&gt;, NOT loaded (review fix scenes: the next
        /// start's tidy deletes the build that ran before, which a loaded image would refuse).</summary>
        static string Place(int n, string name, string ver)
        {
            string dir = Path.Combine(_root, "p" + n);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.Copy(Dll(ver), path, true);
            return path;
        }

        /// <summary>A plugin folder p&lt;n&gt; holding the made-up build &lt;ver&gt; as &lt;name&gt;, loaded as BepInEx loads a plugin.</summary>
        static string Plugin(int n, string name, string ver)
        {
            string dir = Path.Combine(_root, "p" + n);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name);
            File.Copy(Dll(ver), path, true);
            var alc = new AssemblyLoadContext("yazs-drill-" + n);
            alc.LoadFromAssemblyPath(path);
            _loaded.Add(alc);
            return path;
        }

        static void Stable(string ver, string dll = null, string at = null, string sha = null) { Write("stable", ver, dll, at, sha); }
        static void Beta(string ver, string dll = null, params string[] withdrawn) { Write("beta", ver, dll, null, null, withdrawn); }
        static void NoFeed(string which) { string p = Path.Combine(_feed, which, "latest.json"); if (File.Exists(p)) File.Delete(p); }

        /// <summary>feed\&lt;which&gt;\latest.json naming &lt;ver&gt;: its DLL from feed\dl (a SHA-256 of the file served, unless given),
        /// served from &lt;at&gt; (the localhost base by default).</summary>
        static void Write(string which, string ver, string dll, string at, string sha, params string[] withdrawn)
        {
            dll = dll ?? UpdateFiles.Versioned(ver);
            string file = Path.Combine(_feed, "dl", dll);
            if (sha == null) sha = File.Exists(file) ? Updater.Sha256(File.ReadAllBytes(file)) : new string('0', 64);
            var sb = new StringBuilder("{\n");
            sb.Append("  \"version\": \"" + ver + "\",\n");
            sb.Append("  \"dll\": \"" + (at ?? _base) + "/dl/" + dll + "\",\n");
            sb.Append("  \"sha256\": \"" + sha + "\",\n");
            sb.Append("  \"notes\": \"drill build " + ver + "\",\n");
            sb.Append("  \"published\": \"2026-10-12T00:00:00Z\",\n");
            sb.Append("  \"commit\": \"drill" + ver.Replace(".", "").Substring(1) + "\"");
            if (withdrawn.Length > 0) sb.Append(",\n  \"withdrawn\": [" + string.Join(", ", withdrawn.Select(w => "\"" + w + "\"")) + "]");
            sb.Append("\n}\n");
            File.WriteAllText(Path.Combine(_feed, which, "latest.json"), sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>One real check of the running build at &lt;run&gt; (its version and commit from the file itself).</summary>
        static UpdateState Check(string run, bool on, bool probe = false, string pending = null, string betaUrl = null, string stableUrl = null, string pendingKind = null)
        {
            string info = Updater.BuildIdentity(run) ?? "";
            int plus = info.IndexOf('+');
            var s = new Updater.Setup
            {
                StableUrl = stableUrl ?? _base + "/stable/latest.json",
                BetaUrl = betaUrl == "" ? null : betaUrl ?? _base + "/beta/latest.json",
                Running = run, Version = plus > 0 ? info.Substring(0, plus) : info, Commit = plus > 0 ? info.Substring(plus + 1) : null, Info = info,
                Dir = Path.GetDirectoryName(run), CfgBeta = () => on, Env = () => null, AutoUpdate = () => true, Probe = probe, RetryMs = 0,
            };
            Updater.ResetForTests();
            if (pending != null) { Updater.Pending = pending; Updater.PendingKind = pendingKind ?? ""; }
            lock (_lines) _lines.Clear();
            Updater.CheckInBackground(s).GetAwaiter().GetResult();
            return Updater.State;
        }
    }
}
