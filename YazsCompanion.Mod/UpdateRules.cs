// 0.16.2 (the beta channel, WP1 / WP2 of the plan, 2026-10-12): the pure half of the auto-updater - no file, no network, no
// game type - so the bench runs every rule of it (UpdaterCases.cs; ItemBench.csproj links this file with Updater.cs).
//   - the switch: Companion menu > DISPLAY > "Beta updates" = [Update] BetaUpdates (default true), overridden by the environment
//     variable YAZS_BETA (0 off false no / 1 on true yes, any case; anything else is ignored). The one truth table every reader
//     of the switch follows is tools\ItemBench\channel_truth.txt (critic 15): EnvChoice + CfgValue + Switch are its reference.
//   - the two feeds: the stable feed (releases/latest/download/latest.json, unchanged) and the beta feed (the rolling prerelease
//     'beta', releases/download/beta/latest.json: the newest published build + "withdrawn"); each answered / absent (404) /
//     failed (critic 13). latest.json carries "commit" from 0.16.2 on (critic 5); older updaters ignore both new fields.
//   - Decide: one decision per check - Nothing / Download / UpToDate / StepDown - with the label of the running build
//     (release, test build, development build, withdrawn test build) and the floor 0.16.2 (the first build that can step back).
//   - the words: the one [update] channel line per check, the DISPLAY row's footer (Windows and Proton rescue words, critic 8),
//     the header tag and the restart strip's text.
// Versions are plain x.y.z (System.Version, three parts); a prerelease suffix is never used (the GitHub prerelease flag is the
// channel). Generic words only: the repository is public.
// Review fixes (2026-10-12, before the tag): Decide says "no feed answered" when neither feed named a build (a failed beta feed
// was blamed alone) and "the release feed did not answer" for Off without the release; a withdrawn build is never a target (the
// pull window: release.ps1 -Pull re-points the beta feed before the release moves, and the CDN serves the old release feed ~30 s -
// no download of a withdrawn build, no "up to date" on one); the footer's refused-rename words are shorter and its reason capped
// (a 90-character reason with the game path ran past the help box).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace YazsCompanion
{
    /// <summary>What a feed fetch came to (critic 13): a 404 is "no such feed" (absent), anything else that is not a usable
    /// latest.json is "failed" - and a failed beta feed must never make a published test build look like a local one.</summary>
    internal enum FeedState { NotAsked, Answered, Absent, Failed }

    internal enum UpdateAct { Nothing, Download, UpToDate, StepDown }

    /// <summary>One latest.json as read (version, dll url, sha256, notes, commit, withdrawn) and how the fetch went.</summary>
    internal sealed class Feed
    {
        public string Url;
        public FeedState State = FeedState.NotAsked;
        public string Error = "";               // why it failed / is absent (HTTP 404, a timeout, unusable json ...)
        public string VersionText, Dll, Sha, Notes = "", Commit, Zip;
        public Version Version;
        public List<string> Withdrawn = new List<string>();

        public bool Ok { get { return State == FeedState.Answered && Version != null; } }

        public static Feed Of(FeedState state, string url, string error = "") { return new Feed { State = state, Url = url, Error = error ?? "" }; }
    }

    /// <summary>The switch as read for one check: the channel, what decided it, and the words the log line and the footer use.</summary>
    internal sealed class SwitchRead
    {
        public bool Beta;
        public string From = "cfg";             // env | cfg | missing (channel_truth.txt's column)
        public string Env;                      // YAZS_BETA's value when it decided
        /// <summary>The log line's reason: "Beta updates on|off" or "set by YAZS_BETA=0".</summary>
        public string Why { get { return From == "env" ? "set by " + UpdateRules.EnvName + "=" + Env : "Beta updates " + (Beta ? "on" : "off"); } }
        public string Channel { get { return Beta ? "beta" : "stable"; } }
    }

    internal sealed class Decision
    {
        public UpdateAct Act;
        public Version Target;                  // Download / UpToDate / StepDown
        public Feed From;                       // the feed that names Target (its dll url, sha256, notes)
        public string Why = "";                 // Nothing's reason (the spec's words)
        public string Label = "";               // the running build: release | test build | development build | withdrawn test build
        public string Kind = "";                // what Target is: release | test build

        public override string ToString()
        {
            switch (Act)
            {
                case UpdateAct.Download: return "Download " + Target;
                case UpdateAct.StepDown: return "StepDown " + Target;
                case UpdateAct.UpToDate: return "UpToDate";
                default: return "Nothing (" + Why + ")";
            }
        }
    }

    /// <summary>A snapshot of the updater for the menu (Updater.State): replaced whole, never changed in place, so the main thread
    /// reads one consistent picture while a check runs on a worker.</summary>
    internal sealed class UpdateState
    {
        public int Changes;                     // grows with every new snapshot (the DISPLAY row's footer redraws on a change)
        public string Running = "", Commit;
        public string RunningFile = UpdateFiles.Prefix + ".dll";       // the running DLL's file name (the hand step names it)
        public SwitchRead Switch = new SwitchRead { Beta = true };
        public bool AutoUpdateOff;
        public bool Checking;                   // no check has finished yet this session
        public DateTime? Checked;               // local time of the last finished check
        public bool Offline;                    // neither feed answered
        public string Stable, Newest;           // versions, or null
        public FeedState StableState = FeedState.NotAsked, NewestState = FeedState.NotAsked;
        public bool SingleFeed;                 // a hand-set UpdateUrl: that feed only, forward only
        public string Label = "";
        public string Dev;                      // why this is a development build (commit, marker), or null
        public UpdateAct Act = UpdateAct.Nothing;
        public string Target, TargetKind;       // what the last decision named
        public string Result = "";              // the last check's outcome in words
        public string Pending, PendingKind;     // downloaded and waiting for a restart
        public string SetAside;                 // null; "done"; "refused: <why>" (StepDown's Retire)
        public string Probe;                    // null (not probed); "works"; "refused: <why>"
        public bool Wine;                       // running under Wine / Proton (the rescue words differ, critic 8)

        public UpdateState Copy() { return (UpdateState)MemberwiseClone(); }
    }

    internal static class UpdateRules
    {
        /// <summary>The first build that can see the beta feed and step back (2.1 of the plan): the updater never steps down below it,
        /// and release.ps1 refuses to make anything older the release.</summary>
        public static readonly Version Floor = new Version(0, 16, 2);
        public const string EnvName = "YAZS_BETA";
        public const string Section = "Update", Key = "BetaUpdates";
        public const string CfgFile = "bidoi.yazs.companion.cfg";

        // ---------------------------------------------------------------- versions

        /// <summary>"0.16.2", " 0.16.2 ", "0.16.2+abc1234" -> 0.16.2 (three parts); anything else -> null.</summary>
        public static Version ParseVersion(string text)
        {
            string t = (text ?? "").Trim();
            int plus = t.IndexOf('+'); if (plus >= 0) t = t.Substring(0, plus);
            Version v;
            if (!Version.TryParse(t, out v) || v.Revision > 0) return null;
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }

        static Version Max(Version a, Version b) { return a == null ? b : b == null ? a : (a >= b ? a : b); }

        /// <summary>Two commit stamps name the same commit: equal, or one a prefix of the other (short and long hashes), any case;
        /// a "-dirty" stamp never equals a published one.</summary>
        public static bool SameCommit(string a, string b)
        {
            a = (a ?? "").Trim().ToLowerInvariant(); b = (b ?? "").Trim().ToLowerInvariant();
            if (a.Length < 7 || b.Length < 7 || a.EndsWith("-dirty", StringComparison.Ordinal) || b.EndsWith("-dirty", StringComparison.Ordinal)) return false;
            return a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------- the switch (channel_truth.txt)

        /// <summary>Rule 1: YAZS_BETA = 0 off false no -> false (stable); 1 on true yes -> true (beta); unset, empty or anything else
        /// -> null (ignored). Taken as it is: no trimming.</summary>
        public static bool? EnvChoice(string env)
        {
            if (string.IsNullOrEmpty(env)) return null;
            switch (env.ToLowerInvariant())
            {
                case "0": case "off": case "false": case "no": return false;
                case "1": case "on": case "true": case "yes": return true;
            }
            return null;
        }

        /// <summary>Rule 2 / 3: the value of [Update] BetaUpdates in a config file's text, read as BepInEx reads it (the section and
        /// the key matched exactly, # lines skipped, the text after the first '=' trimmed; true / false in any case; any other text
        /// = true, the key's default): null when the text is null (no file), has no [Update] section or no BetaUpdates key in it.</summary>
        public static bool? CfgValue(string fileText)
        {
            if (fileText == null) return null;
            string section = null; bool? found = null;
            foreach (var raw in fileText.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal)) { section = line.Substring(1, line.Length - 2); continue; }
                int eq = line.IndexOf('=');
                if (eq < 0 || section != Section) continue;
                if (line.Substring(0, eq).Trim() != Key) continue;
                string value = line.Substring(eq + 1).Trim();
                bool b;
                found = bool.TryParse(value, out b) ? b : true;     // BepInEx: an unparsable value keeps the default (true)
            }
            return found;
        }

        /// <summary>The channel from YAZS_BETA and the config value (null = no file / section / key): the env decides when it says
        /// something; else the value; else the Companion's bound default (true) or, for a reader outside the Companion, stable.</summary>
        public static SwitchRead Switch(string env, bool? cfg, bool companion = true)
        {
            var e = EnvChoice(env);
            if (e.HasValue) return new SwitchRead { Beta = e.Value, From = "env", Env = env };
            if (cfg.HasValue) return new SwitchRead { Beta = cfg.Value, From = "cfg" };
            return new SwitchRead { Beta = companion, From = "missing" };
        }

        // ---------------------------------------------------------------- the feeds

        /// <summary>A latest.json read into a Feed (Answered), or a Failed feed saying why it is unusable.</summary>
        public static Feed ParseFeed(string json, string url)
        {
            var f = Feed.Of(FeedState.Answered, url);
            try
            {
                using (var doc = JsonDocument.Parse(json ?? ""))
                {
                    var root = doc.RootElement; JsonElement e;
                    if (root.ValueKind != JsonValueKind.Object) return Feed.Of(FeedState.Failed, url, "latest.json unusable: not an object");
                    if (root.TryGetProperty("version", out e) && e.ValueKind == JsonValueKind.String) f.VersionText = e.GetString();
                    if (root.TryGetProperty("dll", out e) && e.ValueKind == JsonValueKind.String) f.Dll = e.GetString();
                    if (root.TryGetProperty("sha256", out e) && e.ValueKind == JsonValueKind.String) f.Sha = (e.GetString() ?? "").Trim().ToLowerInvariant();
                    if (root.TryGetProperty("notes", out e) && e.ValueKind == JsonValueKind.String) f.Notes = e.GetString() ?? "";
                    if (root.TryGetProperty("commit", out e) && e.ValueKind == JsonValueKind.String) f.Commit = e.GetString();
                    if (root.TryGetProperty("zip", out e) && e.ValueKind == JsonValueKind.String) f.Zip = e.GetString();
                    if (root.TryGetProperty("withdrawn", out e) && e.ValueKind == JsonValueKind.Array)
                        foreach (var w in e.EnumerateArray())
                        {
                            var v = w.ValueKind == JsonValueKind.String ? ParseVersion(w.GetString()) : null;
                            if (v != null && !f.Withdrawn.Contains(v.ToString())) f.Withdrawn.Add(v.ToString());
                        }
                }
            }
            catch (Exception ex) { return Feed.Of(FeedState.Failed, url, "latest.json unusable: " + ex.GetType().Name); }
            f.Version = ParseVersion(f.VersionText);
            if (f.Version == null || string.IsNullOrEmpty(f.Dll) || string.IsNullOrEmpty(f.Sha))
                return Feed.Of(FeedState.Failed, url, "latest.json unusable: " + Trim(json, 120));
            return f;
        }

        /// <summary>A download url is taken when it is https - or plain http while the feed that named it is itself served from
        /// http://localhost (the hand tests and updater_drill.ps1; critic 20: localhost exactly, never 127.0.0.1 or a look-alike
        /// host - the exemption is not widened).</summary>
        public static bool UrlAllowed(string feedUrl, string dllUrl)
        {
            if (string.IsNullOrEmpty(dllUrl)) return false;
            if (dllUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return true;
            Uri u;
            return Uri.TryCreate(feedUrl ?? "", UriKind.Absolute, out u) && u.Scheme == Uri.UriSchemeHttp && string.Equals(u.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                && dllUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------------------------------------------- the decision

        /// <summary>Why the running build is a development build that updates leave alone, or null (critic 7): no commit stamped, a
        /// "-dirty" commit, a dev_build.txt the build's own deploy left next to it (<paramref name="marker"/>), or a version the
        /// feeds name as published from another commit.</summary>
        public static string DevWhy(string commit, bool marker, Version run, Feed s, Feed b)
        {
            if (string.IsNullOrEmpty(commit)) return "no commit stamped";
            if (commit.EndsWith("-dirty", StringComparison.OrdinalIgnoreCase)) return commit;
            if (marker) return commit + ", dev_build.txt";
            foreach (var f in new[] { s, b })
                if (f != null && f.Ok && run != null && f.Version == run && !string.IsNullOrEmpty(f.Commit) && !SameCommit(f.Commit, commit))
                    return commit + ", published as " + ShortCommit(f.Commit);
            return null;
        }

        /// <summary>The label of the running build (the spec's 2.3 + critic 13: a failed beta feed keeps "test build").</summary>
        public static string Label(Version run, string dev, Feed s, Feed b)
        {
            if (dev != null) return "development build";
            if (run != null && Withdrawn(s, b).Contains(run.ToString())) return "withdrawn test build";
            Version S = s != null && s.Ok ? s.Version : null;
            if (S != null && run == S) return "release";
            Version newest = null;
            if (b != null && b.Ok) newest = Max(S, b.Version);
            else if ((b == null || b.State == FeedState.Absent || b.State == FeedState.NotAsked) && S != null) newest = S;
            if (newest != null && run != null && run > newest) return "development build";
            return "test build";
        }

        static HashSet<string> Withdrawn(Feed s, Feed b)
        {
            var w = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in new[] { s, b }) if (f != null && f.Ok) foreach (var v in f.Withdrawn) w.Add(v);
            return w;
        }

        /// <summary>The one decision of a check (2.3 of the plan, with the critic's 13 and the floor). <paramref name="b"/> is the
        /// beta feed (NotAsked for a hand-set UpdateUrl: <paramref name="single"/> = that feed only, forward only).</summary>
        public static Decision Decide(Version run, string dev, bool betaOn, Feed s, Feed b, bool single = false)
        {
            var d = new Decision { Label = Label(run, dev, s, b) };
            if (dev != null) return Nothing(d, "a development build - updates leave it alone");
            if (run == null) return Nothing(d, "the running version does not parse");
            Version S = s != null && s.Ok ? s.Version : null;
            Version B = !single && b != null && b.Ok ? b.Version : null;
            bool bFailed = !single && b != null && b.State == FeedState.Failed;
            // review fix: neither feed named a build - said first (both failing used to read "the test-builds feed did not answer")
            if (S == null && B == null) return Nothing(d, "no feed answered");
            // critic 13: whenever the answer needs the beta feed (On; Off above the release) and that feed failed, nothing moves this
            // check (one retry follows after 60 s); S is known here
            if (bFailed && (betaOn || run > S)) return Nothing(d, "the test-builds feed did not answer");
            if (S == null && !betaOn) return Nothing(d, "the release feed did not answer");
            // review fix (the pull window): a withdrawn build is never a target - release.ps1 -Pull re-points the beta feed before the
            // release moves, and the CDN can serve the old release feed for ~30 s
            var w = Withdrawn(s, b);
            Version Sw = S != null && !w.Contains(S.ToString()) ? S : null, Bw = B != null && !w.Contains(B.ToString()) ? B : null;
            Version T = betaOn ? Max(Sw, Bw) : Sw;
            if (T == null) return Nothing(d, "the feeds name only a withdrawn build");
            d.Target = T;
            d.From = (Sw != null && T == Sw) ? s : b;        // a build both feeds name: the release's own feed
            d.Kind = (Sw != null && T == Sw) ? "release" : "test build";
            if (T > run) { d.Act = UpdateAct.Download; return d; }
            if (T == run) { d.Act = UpdateAct.UpToDate; return d; }
            // T < run
            if (single) return Nothing(d, "a hand-set feed (UpdateUrl) only goes forward");
            if (T < Floor) return Nothing(d, "would go below " + Floor);
            if (w.Contains(run.ToString())) { d.Act = UpdateAct.StepDown; return d; }                 // a withdrawn build, either channel
            if (!betaOn && B != null && run <= B) { d.Act = UpdateAct.StepDown; return d; }            // Off on a published test build
            return Nothing(d, "ahead of every published build - a local build");
        }

        static Decision Nothing(Decision d, string why) { d.Act = UpdateAct.Nothing; d.Target = null; d.From = null; d.Kind = ""; d.Why = why; return d; }

        // ---------------------------------------------------------------- words

        /// <summary>The one line per check (the analyzer's channel source): "[update] channel beta (Beta updates on): release
        /// 0.16.2, newest 0.16.3, running 0.16.3 = test build - up to date".</summary>
        public static string ChannelLine(SwitchRead sw, Feed s, Feed b, string running, string label, string result)
        {
            string release = s != null && s.Ok ? s.Version.ToString() : "none";
            string newest = b == null || b.State == FeedState.NotAsked || b.State == FeedState.Absent ? "none" : b.State == FeedState.Failed ? "feed failed" : b.Version.ToString();
            return "[update] channel " + sw.Channel + " (" + sw.Why + "): release " + release + ", newest " + newest + ", running " + running + " = " + label + " - " + result;
        }

        /// <summary>The menu header's tag after the version: TEST BUILD, DEV, or nothing (a release, or not known yet).</summary>
        public static string HeaderTag(string label)
        {
            if (label == "test build" || label == "withdrawn test build") return "TEST BUILD";
            if (label == "development build") return "DEV";
            return "";
        }

        /// <summary>The restart strip: "YAZS COMPANION 0.16.2 (RELEASE) DOWNLOADED - RESTART THE GAME TO APPLY".</summary>
        public static string NoticeText(string version, string kind)
        {
            string k = string.IsNullOrEmpty(kind) ? "" : " (" + kind.ToUpperInvariant() + ")";
            return "YAZS COMPANION " + version + k + " DOWNLOADED  -  RESTART THE GAME TO APPLY";
        }

        /// <summary>The DISPLAY tab's "Beta updates" value.</summary>
        public static string RowValue(bool beta) { return beta ? "On - newest builds" : "Off - releases only"; }

        /// <summary>The row's footer, rebuilt from the updater's snapshot while the menu draws (3.2 of the plan; critic 3 and 8).
        /// Plain words, at most 560 characters (the menu's help box holds about five lines; the bench measures every state).</summary>
        public static string Footer(UpdateState st)
        {
            var sw = st.Switch ?? new SwitchRead { Beta = true };
            var sb = new StringBuilder(RowValue(sw.Beta)).Append(". ");
            string me = st.Running;
            bool refused = false;
            if (st.AutoUpdateOff) sb.Append("This mod: ").Append(me).Append(". Auto-update is off ([Update] AutoUpdate): nothing is checked or downloaded. ");
            else if (st.Dev != null) sb.Append("This mod: ").Append(me).Append(", a development build (").Append(st.Dev).Append("): updates leave it alone. ");
            else if (st.Checking) sb.Append("This mod: ").Append(me).Append(" - checking for builds now. ");
            else if (st.Offline) sb.Append("This mod: ").Append(me).Append(". Checked ").Append(Clock(st.Checked)).Append(" - offline. ");
            else if (st.Act == UpdateAct.StepDown && st.SetAside == "done")
                sb.Append("The ").Append(st.TargetKind).Append(' ').Append(st.Target).Append(" is downloaded and checked; the ").Append(Short(st.Label)).Append(' ').Append(me)
                  .Append(" is set aside - the ").Append(st.TargetKind).Append(" runs from the next start. ");
            else if (st.Act == UpdateAct.StepDown && st.SetAside != null && st.SetAside.StartsWith("refused", StringComparison.Ordinal))
            {   // review fix: shorter words and a capped reason (a 90-character reason with the game's path ran past the help box)
                refused = true;
                sb.Append("The ").Append(st.TargetKind).Append(' ').Append(st.Target).Append(" is downloaded and checked, but this system cannot set the ").Append(Short(st.Label)).Append(' ').Append(me)
                  .Append(" aside (").Append(Reason(st.SetAside)).Append("): it runs until a launcher does, or rename ")
                  .Append(st.RunningFile).Append(" to ").Append(UpdateFiles.Off(st.RunningFile)).Append(" with the game closed. ");
            }
            else
            {
                sb.Append("This mod: ").Append(me);
                if (!string.IsNullOrEmpty(st.Label)) sb.Append(", a").Append(Vowel(st.Label) ? "n " : " ").Append(st.Label);      // no label: no feed named a build
                sb.Append(" (release ").Append(st.Stable ?? (st.StableState == FeedState.Failed ? "not reached" : "none"))
                  .Append(st.SingleFeed ? "" : ", newest " + (st.Newest ?? (st.NewestState == FeedState.Failed ? "not reached" : "none"))).Append(") - ").Append(st.Result);
                if (st.Checked.HasValue) sb.Append(", checked ").Append(Clock(st.Checked));
                sb.Append(". ");
                if (sw.Beta && (st.Label == "test build" || st.Label == "withdrawn test build") && st.Stable != null)
                    sb.Append("Off would put the release ").Append(st.Stable).Append(" back at the next start. ");
                else if (!sw.Beta && st.Label == "release" && st.Newest != null && ParseVersion(st.Newest) > ParseVersion(st.Stable ?? "0.0.0"))
                    sb.Append("On would take the test build ").Append(st.Newest).Append(" at the next start. ");
            }
            if (st.Pending != null && (st.Act == UpdateAct.Nothing || st.Act == UpdateAct.UpToDate)) sb.Append(st.Pending).Append(" (").Append(st.PendingKind).Append(") is downloaded - it runs from the next start. ");
            if (sw.From == "env")
                sb.Append(st.Wine ? "Set by the launch options (" : "Set by the environment variable (").Append(EnvName).Append('=').Append(sw.Env)
                  .Append(st.Wine ? ") - remove it there to use this switch. " : ") - remove it to use this switch. ");
            sb.Append("Other mods that follow this setting change at their own next start. ");
            sb.Append(st.Wine ? "If the game cannot reach this menu, put " + EnvName + "=0 first in the launch options."
                              : "If the game cannot reach this menu, set BetaUpdates = false under [Update] in BepInEx\\config\\" + CfgFile + ".");
            if (st.Probe == "works") sb.Append(" Going back works on this machine.");
            else if (st.Probe != null && !refused) sb.Append(" Going back needs the launcher or a hand step on this machine.");     // the refused words say it
            return sb.ToString();
        }

        /// <summary>A refused rename's reason for the footer: whole up to 40 characters, else its exception type (else cut).</summary>
        static string Reason(string setAside)
        {
            string r = (setAside ?? "").StartsWith("refused", StringComparison.Ordinal) ? setAside.Substring(7).Trim(' ', ':') : (setAside ?? "");
            if (r.Length <= 40) return r;
            int colon = r.IndexOf(':');
            return colon > 0 && colon <= 40 ? r.Substring(0, colon) : r.Substring(0, 37) + "...";
        }

        /// <summary>"test build" for both test-build labels (the footer's "the test build 0.16.3").</summary>
        static string Short(string label) { return label == "withdrawn test build" ? "test build" : string.IsNullOrEmpty(label) ? "build" : label; }
        static bool Vowel(string s) { return !string.IsNullOrEmpty(s) && "aeiou".IndexOf(char.ToLowerInvariant(s[0])) >= 0; }
        public static string Clock(DateTime? t) { return t.HasValue ? t.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : "-"; }
        static string ShortCommit(string c) { c = (c ?? "").Trim(); return c.Length > 7 ? c.Substring(0, 7) : c; }
        static string Trim(string s, int n) { s = (s ?? "").Replace("\r", " ").Replace("\n", " "); return s.Length > n ? s.Substring(0, n - 3) + "..." : s; }
    }

    /// <summary>The file names the updater uses beside the running DLL (2026-10-12, the contract shared with the other updaters): a
    /// build set aside = "&lt;file&gt;.off", a request for a launcher to set it aside = "&lt;file&gt;.offnext", the kept release =
    /// "YazsCompanionMod-&lt;S&gt;.dll.keep" (critic 4), the marker of a development deploy = dev_build.txt (critic 7), the rename
    /// probe's result = update_probe.txt; the release the last answered check saw = update_release.txt (review fix: line 1 its
    /// version, line 2 "sha256 &lt;hex&gt;" - a .keep is only ever that build). Integration fix 2026-10-12: a launcher may read
    /// update_release.txt (line 1 the version, line 2 "sha256 &lt;hex&gt;") to put the release back without a network; the format
    /// does not change without them.</summary>
    internal static class UpdateFiles
    {
        public const string Prefix = "YazsCompanionMod";
        public const string DevMarker = "dev_build.txt", ProbeFile = "update_probe.txt", ReleaseFile = "update_release.txt";
        public static string Off(string file) { return file + ".off"; }
        public static string OffNext(string file) { return file + ".offnext"; }
        public static string Versioned(string version) { return Prefix + "-" + version + ".dll"; }
        public static string Keep(string version) { return Versioned(version) + ".keep"; }
        /// <summary>The probe's sibling name: still a .dll (a cut between the two renames leaves a loadable file).</summary>
        public static string ProbeName(string file)
        {
            if (file.EndsWith(".probe.dll", StringComparison.OrdinalIgnoreCase)) return file.Substring(0, file.Length - 10) + ".dll";
            return (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? file.Substring(0, file.Length - 4) : file) + ".probe.dll";
        }
        /// <summary>"YazsCompanionMod-0.16.3.dll" (also .dll.off / .dll.keep / .dll.offnext) -> 0.16.3; the plain name -> null.</summary>
        public static Version VersionInName(string file)
        {
            string f = file ?? "";
            if (!f.StartsWith(Prefix + "-", StringComparison.OrdinalIgnoreCase)) return null;
            int dll = f.IndexOf(".dll", StringComparison.OrdinalIgnoreCase);
            if (dll < 0) return null;
            string v = f.Substring(Prefix.Length + 1, dll - Prefix.Length - 1);
            if (v.EndsWith(".probe", StringComparison.OrdinalIgnoreCase)) v = v.Substring(0, v.Length - 6);
            return UpdateRules.ParseVersion(v);
        }
    }
}
