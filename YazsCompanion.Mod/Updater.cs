// Auto-update. On every launch a background task fetches latest.json from the GitHub release feed
// (config UpdateUrl); if it names a newer version, the DLL is downloaded next to the running one as
// YazsCompanionMod-<version>.dll and verified against the published SHA-256. Nothing running is ever
// replaced: BepInEx loads only the highest [BepInPlugin] version of a GUID ("... because a newer version
// exists"), so the next launch runs the new file, which then deletes the older copies (CleanupSiblings).
// Since 0.15.0 the feed's release notes are kept next to the DLL as notes_<version>.txt for a later what's-new
// card: the pending version's when it is downloaded (or found downloaded), the running version's when the feed
// names it as the latest; the notes of versions older than the running one are removed (CacheNotes / PruneNotes).
// Only .NET file and network calls happen off the main thread; the game objects are never touched here.
//
// 0.16.2 (the beta channel, WP1 of the plan, 2026-10-12) - two feeds and a way back:
//   - Every check reads the switch ([Update] BetaUpdates, overridden by YAZS_BETA) and fetches BOTH feeds side by side: the
//     stable feed (unchanged) and the beta feed (releases/download/beta/latest.json, the newest published build plus a
//     "withdrawn" list). The decision is UpdateRules.Decide (pure, benched); this file only carries it out.
//   - StepDown (Off on a published test build, or a withdrawn build): the lower build is downloaded - or taken from the kept
//     release YazsCompanionMod-<S>.dll.keep without a download when its SHA-256 is the feed's (critic 4) - and checked to be
//     that version (its InformationalVersion, read from the metadata); only then is the running file set aside
//     (Retire: <file>.off). A refused rename leaves <file>.offnext for a launcher and the build keeps running (critic 3).
//     The switch turned back On before the restart puts the set-aside file back (Undo).
//   - After every decision a sibling build newer than the target, or a withdrawn one, is set aside too: it would win the next
//     start (critic 2). CleanupSiblings keeps the highest build below the running one as .keep (critic 4), deletes .off files
//     and a .offnext only once its DLL is gone (critic 3).
//   - Updates leave a development build alone: no commit stamped, a "-dirty" commit, a dev_build.txt that names this build
//     (written by the csproj's deploy), or a version the feeds name as published from another commit (critic 7).
//   - A failed beta feed (not a 404) moves nothing and is asked once more after 60 s (critic 13).
//   - The rename probe: once per build, the running DLL is renamed to a sibling .dll name and straight back, and the result
//     kept in update_probe.txt - whether this machine can set a build aside is known before it is ever needed (R1, Proton).
//   - One check at a time; a change of the switch asks for another (Recheck), so a switch needs one restart.
//   - One line per check: "[update] channel beta (Beta updates on): release 0.16.2, newest 0.16.3, running 0.16.3 = test
//     build - up to date"; the snapshot State feeds the menu's DISPLAY row.
//
// Review fixes (2026-10-12, two adversarial reviews before the tag):
//   - A .keep is the RELEASE, never just the next lower build (critic 4 and the contract; a launcher puts the highest .keep back
//     while the switch reads Off). Every check whose release feed answers records that build in update_release.txt (version +
//     SHA-256); CleanupSiblings keeps a lower sibling as .keep only when it is that record, at or above the floor and below the
//     running build, and removes every other .keep - at most one, always the release; no record (offline since the first 0.16.2
//     start) keeps nothing. A test build downloaded while the release runs copies the release to its .keep in the same check, so
//     the way back is there before the test build's first start (4a: one that dies before its own load tidies anything).
//   - While <file>.offnext waits, the release stays beside it as a .dll (critic 3: a launcher completes the request only when
//     another build is there to run) and the re-check loop needs no download.
//   - Off sets aside a newer download also when the check could not decide (a feed failed: critic 2 with 13).
//   - A refused Retire forgets the pending download: a restart would not apply it where no launcher helps (critic 3; the row's
//     help says what to do).
//   - dev_build.txt gives way when an answered feed publishes the very bytes that run (a deploy of the build then released: the
//     release's local deploy finds "the same DLL" and copies nothing - critic 7).
//   - The probe line once per session; a change of the switch earns its own retry of a failed beta feed; the "once more" pass
//     runs with the newest setup.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YazsCompanion
{
    internal static class Updater
    {
        public const string FilePrefix = "YazsCompanionMod";
        /// <summary>Version downloaded and waiting for a restart, or null. Read on the main thread by Notice.</summary>
        public static volatile string Pending;
        public static volatile string PendingNotes = "";
        /// <summary>0.16.2: what the pending build is - "release" or "test build" (the restart strip names it).</summary>
        public static volatile string PendingKind = "";
        /// <summary>0.16.2: the updater as the menu shows it (a new snapshot per change; see UpdateState).</summary>
        public static volatile UpdateState State = new UpdateState { Checking = true };
        /// <summary>0.16.2: running under Wine / Proton (Plugin sets it at load; the rescue words differ, critic 8).</summary>
        internal static bool Wine;
        /// <summary>The bench and the drill: every [update] line as well (true = a warning).</summary>
        internal static Action<string, bool> Echo;

        static void Info(string m) { Plugin.Logger.LogInfo(m); var e = Echo; if (e != null) e(m, false); }
        static void Warn(string m) { Plugin.Logger.LogWarning(m); var e = Echo; if (e != null) e(m, true); }

        /// <summary>What one check needs, handed in by Plugin (the bench and updater_drill.ps1 hand in their own).</summary>
        internal sealed class Setup
        {
            public string StableUrl, BetaUrl;               // BetaUrl null = a hand-set UpdateUrl: that feed only, forward only
            public string Running;                          // the running DLL's path (Assembly.Location)
            public string Version, Commit, Info;            // VERSION, the commit stamp, the whole InformationalVersion
            public string Dir;                              // the plugin folder
            public Func<bool> CfgBeta = () => true;         // [Update] BetaUpdates as BepInEx bound it
            public Func<string> Env = () => Environment.GetEnvironmentVariable(UpdateRules.EnvName);
            public Func<bool> AutoUpdate = () => true;
            public Func<string, Task<Fetched>> Fetch;       // the bench's stand-in for the network (null = HTTP)
            public bool Probe = true;
            public int RetryMs = 60000;                     // critic 13: one more check after a failed beta feed (0 = none)
        }

        internal sealed class Fetched { public int Status; public byte[] Body; public string Error; }

        // ================================================================ at load: the siblings

        /// <summary>Tidies the plugin folder at load (BepInEx chose the running build as the newest). The release the last answered
        /// check recorded (update_release.txt) - when it is at or above the floor and below the running build - is the way back: a
        /// lower sibling with its version and SHA-256 is kept as YazsCompanionMod-&lt;S&gt;.dll.keep (critic 4), or left as it is while
        /// &lt;running&gt;.offnext waits for a launcher (critic 3); every other sibling is deleted, and every .keep that is not that
        /// release (no record, another version or SHA-256, the release itself running) - at most one .keep stays, always the release
        /// (review fix: the highest lower build was kept whatever it was). Set-aside .off files are deleted (never the running file),
        /// a .offnext only once its DLL is gone; stray .part files go. Returns what it did.</summary>
        public static List<string> CleanupSiblings(string me, string myVersion)
        {
            var done = new List<string>();
            try
            {
                if (string.IsNullOrEmpty(me)) return done;
                string dir = Path.GetDirectoryName(me);
                Version mine = UpdateRules.ParseVersion(myVersion);
                var rel = ReadRelease(dir);
                Version relV = rel != null && mine != null && rel.Version >= UpdateRules.Floor && rel.Version < mine ? rel.Version : null;
                string keepPath = relV != null ? Path.Combine(dir, UpdateFiles.Keep(relV.ToString())) : null;
                bool kept = keepPath != null && File.Exists(keepPath) && FileSha(keepPath) == rel.Sha;
                bool asked = File.Exists(UpdateFiles.OffNext(me));
                string keep = null, stay = null;
                var gone = new List<string>();
                foreach (var f in Siblings(dir, me))
                {
                    bool release = relV != null && FileVersion(f) == relV && FileSha(f) == rel.Sha;
                    if (release && asked && stay == null) stay = f;                 // critic 3: the build the launcher's request leaves to run
                    else if (release && !kept && keep == null) keep = f;
                    else gone.Add(f);
                }
                foreach (var f in gone)
                {
                    try { File.Delete(f); done.Add("removed " + Path.GetFileName(f)); Info("[update] removed older build " + Path.GetFileName(f)); }
                    catch (Exception e) { Warn("[update] could not remove " + Path.GetFileName(f) + ": " + e.Message); }
                }
                if (keep != null)
                {
                    try
                    {
                        if (File.Exists(keepPath)) File.Delete(keepPath);
                        File.Move(keep, keepPath); kept = true;
                        done.Add("kept " + Path.GetFileName(keep) + " as " + Path.GetFileName(keepPath));
                        Info("[update] kept " + Path.GetFileName(keep) + " as " + Path.GetFileName(keepPath) + " (the release " + relV + ", the way back from this build)");
                    }
                    catch (Exception e) { Warn("[update] could not keep " + Path.GetFileName(keep) + ": " + e.Message); }
                }
                if (stay != null) { done.Add("stays " + Path.GetFileName(stay)); Info("[update] the release " + Path.GetFileName(stay) + " stays beside this build: " + Path.GetFileName(UpdateFiles.OffNext(me)) + " asks a launcher to set this build aside"); }
                foreach (var f in Directory.GetFiles(dir, FilePrefix + "*"))
                {
                    string name = Path.GetFileName(f);
                    if (name.EndsWith(".dll.off", StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(f); done.Add("removed " + name); Info("[update] removed the set-aside build " + name); }
                        catch (Exception e) { Warn("[update] could not remove " + name + ": " + e.Message); }
                    }
                    else if (name.EndsWith(".dll.offnext", StringComparison.OrdinalIgnoreCase))
                    {
                        string dll = f.Substring(0, f.Length - ".offnext".Length);
                        if (File.Exists(dll)) { done.Add("waits " + name); Info("[update] " + name + " still asks a launcher to set " + Path.GetFileName(dll) + " aside"); continue; }
                        try { File.Delete(f); done.Add("removed " + name); } catch { }
                    }
                    else if (name.EndsWith(".dll.keep", StringComparison.OrdinalIgnoreCase))
                    {
                        if (kept && string.Equals(Path.GetFullPath(f), Path.GetFullPath(keepPath), StringComparison.OrdinalIgnoreCase)) continue;
                        string why = KeepWhy(f, rel, mine) ?? "not the way back from this build";
                        try { File.Delete(f); done.Add("removed " + name); Info("[update] removed the kept build " + name + " (" + why + ")"); } catch { }
                    }
                    else if (name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)) { try { File.Delete(f); done.Add("removed " + name); } catch { } }
                }
            }
            catch (Exception e) { Warn("[update] cleanup: " + e.Message); }
            return done;
        }

        /// <summary>Every YazsCompanionMod*.dll in the folder but the running one.</summary>
        static List<string> Siblings(string dir, string me)
        {
            var list = new List<string>();
            string mine = Path.GetFullPath(me);
            foreach (var f in Directory.GetFiles(dir, FilePrefix + "*"))
                if (f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !string.Equals(Path.GetFullPath(f), mine, StringComparison.OrdinalIgnoreCase)) list.Add(f);
            return list;
        }

        /// <summary>A DLL's version: its InformationalVersion, else the version in its file name, else null.</summary>
        public static Version FileVersion(string path)
        {
            return UpdateRules.ParseVersion(BuildIdentity(path)) ?? UpdateFiles.VersionInName(Path.GetFileName(path));
        }

        /// <summary>The InformationalVersion a DLL carries ("0.16.2+abc1234", the build stamps it: YazsCompanion.Mod.csproj), read
        /// from its metadata without loading it - any platform, no Win32 version resource needed; FileVersionInfo's
        /// ProductVersion as the fallback; null when neither says.</summary>
        public static string BuildIdentity(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var pe = new PEReader(fs))
                {
                    if (pe.HasMetadata)
                    {
                        var md = pe.GetMetadataReader();
                        foreach (var h in md.GetAssemblyDefinition().GetCustomAttributes())
                        {
                            var ca = md.GetCustomAttribute(h);
                            if (AttributeName(md, ca) != "AssemblyInformationalVersionAttribute") continue;
                            var blob = md.GetBlobReader(ca.Value);
                            if (blob.Length < 3 || blob.ReadUInt16() != 1) continue;
                            return blob.ReadSerializedString();
                        }
                    }
                }
            }
            catch { }
            try { string pv = FileVersionInfo.GetVersionInfo(path).ProductVersion; return string.IsNullOrEmpty(pv) ? null : pv; }
            catch { return null; }
        }

        static string AttributeName(MetadataReader md, CustomAttribute ca)
        {
            if (ca.Constructor.Kind == HandleKind.MemberReference)
            {
                var mr = md.GetMemberReference((MemberReferenceHandle)ca.Constructor);
                if (mr.Parent.Kind == HandleKind.TypeReference) return md.GetString(md.GetTypeReference((TypeReferenceHandle)mr.Parent).Name);
            }
            else if (ca.Constructor.Kind == HandleKind.MethodDefinition)
                return md.GetString(md.GetTypeDefinition(md.GetMethodDefinition((MethodDefinitionHandle)ca.Constructor).GetDeclaringType()).Name);
            return null;
        }

        // ================================================================ the check

        static Setup _setup;
        static int _busy, _again, _retried;
        static string _probed;          // review fix: the probe's answer, logged once a session (not at every recheck and retry)

        /// <summary>A check is running (the bench waits on it instead of sleeping).</summary>
        internal static bool Busy { get { return Volatile.Read(ref _busy) == 1; } }

        /// <summary>Starts a check on a worker and returns it (the game never waits on it). One check at a time: asked while one
        /// runs, it runs once more right after.</summary>
        public static Task CheckInBackground(Setup s)
        {
            _setup = s;
            if (Interlocked.Exchange(ref _busy, 1) == 1) { Interlocked.Exchange(ref _again, 1); return Task.CompletedTask; }
            return Task.Run(() => Loop(s));
        }

        /// <summary>The switch changed (Config.SettingChanged): check again with it, so the download is done before the restart.
        /// Review fix: a change earns its own retry of a failed beta feed (critic 13's one retry was once a session, and the Off
        /// recheck is the one that matters).</summary>
        public static void Recheck(Setup s = null) { s = s ?? _setup; if (s == null) return; Interlocked.Exchange(ref _retried, 0); CheckInBackground(s); }

        static async Task Loop(Setup s)
        {
            while (true)
            {
                Interlocked.Exchange(ref _again, 0);
                try { await CheckOnce(_setup ?? s); }           // review fix: the "once more" pass runs with the newest setup (a changed UpdateUrl)
                catch (Exception e) { Warn("[update] check failed: " + e.GetType().Name + ": " + e.Message); }
                if (Interlocked.CompareExchange(ref _again, 0, 1) == 1) continue;
                Interlocked.Exchange(ref _busy, 0);
                if (Volatile.Read(ref _again) == 1 && Interlocked.CompareExchange(ref _busy, 1, 0) == 0) continue;     // asked for in between
                return;
            }
        }

        /// <summary>The bench: the process-wide flags back to a fresh start.</summary>
        internal static void ResetForTests() { _busy = 0; _again = 0; _retried = 0; _setup = null; _probed = null; Pending = null; PendingNotes = ""; PendingKind = ""; State = new UpdateState { Checking = true }; }

        static void ForgetPending() { Pending = null; PendingKind = ""; PendingNotes = ""; }

        static void Publish(UpdateState st) { st.Changes = State.Changes + 1; State = st; }

        static async Task CheckOnce(Setup s)
        {
            var st = State.Copy();
            st.Running = s.Version ?? ""; st.Commit = s.Commit; st.RunningFile = Path.GetFileName(s.Running ?? "") ?? ""; st.Wine = Wine;
            st.SingleFeed = s.BetaUrl == null; st.AutoUpdateOff = false;
            var sw = UpdateRules.Switch(s.Env != null ? s.Env() : null, s.CfgBeta != null ? (bool?)s.CfgBeta() : null);
            st.Switch = sw;
            Version run = UpdateRules.ParseVersion(s.Version);
            if (s.AutoUpdate != null && !s.AutoUpdate())
            {
                st.AutoUpdateOff = true; st.Checking = false;
                Publish(st);
                Info("[update] channel " + sw.Channel + " (" + sw.Why + "): auto-update is off ([Update] AutoUpdate) - nothing checked");
                return;
            }
            if (s.Probe) { st.Probe = _probed ?? (_probed = ProbeOnce(s.Running, s.Info)); Publish(st); st = st.Copy(); }

            // the two feeds, side by side (a missing beta feed = no test build published yet)
            var ts = FetchFeed(s, s.StableUrl);
            var tb = s.BetaUrl != null ? FetchFeed(s, s.BetaUrl) : Task.FromResult(Feed.Of(FeedState.NotAsked, null));
            await Task.WhenAll(ts, tb);
            Feed fs = ts.Result, fb = tb.Result;
            bool single = s.BetaUrl == null;
            string runSha = FileSha(s.Running);

            // review fix (critic 4): the release as this answered check sees it - the only build a .keep may be (not for a hand-set feed,
            // not a withdrawn one: the pull window can still serve it)
            if (fs.Ok && !single && !WithdrawnSet(fs, fb).Contains(fs.Version.ToString())) RecordRelease(s.Dir, fs);
            bool marker = DevMarker(s, runSha, fs, fb);
            string dev = UpdateRules.DevWhy(s.Commit, marker, run, fs, fb);
            var d = UpdateRules.Decide(run, dev, sw.Beta, fs, fb, single);
            string result, setAside = null;
            try { var r = await Act(s, d); result = r.Key; setAside = r.Value; }
            catch (Exception e) { result = "failed: " + e.GetType().Name + ": " + e.Message; Warn("[update] " + result); }
            // critic 4a: a test build is on its way while the release runs - the release goes to its .keep now, before the test build's first start
            if (d.Act == UpdateAct.Download && d.Kind == "test build" && Pending == d.Target.ToString()) KeepRunningRelease(s, fs, run, runSha);
            if (d.Act != UpdateAct.Nothing) SetAsideUnwanted(s, d.Target, fs, fb);
            else if (dev == null && run != null && !single && !sw.Beta)
            {   // review fix (critic 2 with 13): Off, and this check could not decide (a feed failed, ...) - a newer download must still
                // not win the next start: everything above the release (when known), else the test build this session downloaded
                if (fs.Ok) SetAsideUnwanted(s, fs.Version, fs, fb);
                else SetAsidePendingTest(s);
            }
            // put back only on a decision that wants this build to go on (not on a check that could not tell: a feed that failed)
            if ((d.Act == UpdateAct.UpToDate || d.Act == UpdateAct.Download) && dev == null) Undo(s.Running);
            if (fs.Ok && !single && dev == null) TidyKeep(s.Dir, ReadRelease(s.Dir));
            // the notes of builds older than the one that runs next (a step back: the lower build's own notes stay)
            var notesGone = PruneNotes(s.Dir, d.Act == UpdateAct.StepDown && d.Target != null ? d.Target.ToString() : s.Version);
            if (notesGone.Count > 0) Info("[update] removed the release notes of older builds: " + string.Join(", ", notesGone));

            st = st.Copy();
            st.Checking = false; st.Checked = DateTime.Now;
            st.Offline = !fs.Ok && !(fb.Ok || fb.State == FeedState.Absent) && fs.State != FeedState.Absent;
            st.Stable = fs.Ok ? fs.Version.ToString() : null; st.StableState = fs.State;
            st.Newest = fb.Ok ? fb.Version.ToString() : null; st.NewestState = fb.State;
            // review fix: no feed named a build - the menu shows no kind (the line keeps the spec's label; the header no TEST BUILD on a release)
            st.Label = dev == null && !fs.Ok && !fb.Ok ? "" : d.Label; st.Dev = dev; st.Act = d.Act;
            st.Target = d.Target != null ? d.Target.ToString() : null; st.TargetKind = d.Kind;
            st.Result = result; st.SetAside = setAside;
            st.Pending = Pending; st.PendingKind = PendingKind;
            Publish(st);
            Info(UpdateRules.ChannelLine(sw, fs, fb, s.Version, d.Label, result));

            // critic 13: a failed beta feed is asked once more, a minute later (once a session, and once more after each switch change)
            if (fb.State == FeedState.Failed && s.RetryMs > 0 && Interlocked.Exchange(ref _retried, 1) == 0)
            {
                Info("[update] the test-builds feed did not answer (" + fb.Error + ") - asking again in " + (s.RetryMs / 1000) + " s");
                _ = Task.Delay(s.RetryMs).ContinueWith(_ => CheckInBackground(_setup ?? s));
            }
        }

        /// <summary>Carries a decision out; returns the result in words and, for StepDown, the Retire outcome.</summary>
        static async Task<KeyValuePair<string, string>> Act(Setup s, Decision d)
        {
            string ver = d.Target != null ? d.Target.ToString() : null;
            switch (d.Act)
            {
                case UpdateAct.UpToDate:
                    KeepNotes(s.Dir, ver, d.From != null ? d.From.Notes : "");      // the running version's notes, for the what's-new card
                    return new KeyValuePair<string, string>("up to date", null);
                case UpdateAct.Download:
                    {
                        string got = await Install(s, d, false);
                        return new KeyValuePair<string, string>(got ?? ("downloaded " + ver + " (" + d.Kind + ") - it runs from the next start"), null);
                    }
                case UpdateAct.StepDown:
                    {
                        string got = await Install(s, d, true);
                        if (got != null && !got.StartsWith("ok", StringComparison.Ordinal)) return new KeyValuePair<string, string>(got, null);
                        string how = got == "ok keep" ? "the kept " + d.Kind + " " + ver + " put back" : "the " + d.Kind + " " + ver + " downloaded and checked";
                        string r = Retire(s.Running);
                        if (r == "done" || r == "already")
                        {
                            Info("[update] the " + d.Label + " " + s.Version + " is set aside (" + Path.GetFileName(UpdateFiles.Off(s.Running ?? "")) + "); " + ver + " runs from the next start");
                            return new KeyValuePair<string, string>("stepped down: " + how + ", this build set aside - " + ver + " runs from the next start", "done");
                        }
                        Warn("[update] could not set the test build aside (" + r.Substring(Math.Min(r.Length, 9)) + ") - it keeps running; a request was left for the launcher (" + Path.GetFileName(UpdateFiles.OffNext(s.Running ?? "")) + ")");
                        // review fix (critic 3): BepInEx loads this higher build again at the next start unless a launcher completes the
                        // request - the strip must not promise a restart that may not apply it (the row's help says what to do)
                        ForgetPending();
                        return new KeyValuePair<string, string>("step down: " + how + ", but this build could not be set aside - a request was left for a launcher", r);
                    }
                default:
                    return new KeyValuePair<string, string>(d.Why, null);
            }
        }

        /// <summary>Puts the target build next to the running one: already there with the feed's SHA-256, or (stepping down) the
        /// kept release with that SHA-256 renamed back, or downloaded (https only, SHA-256, a managed assembly) via a .part file.
        /// Stepping down, the file must also say it is that version (its InformationalVersion) before anything is touched. Returns
        /// null after a download, "ok there" / "ok keep" for a file found, or the reason it did not install.</summary>
        static async Task<string> Install(Setup s, Decision d, bool down)
        {
            Feed f = d.From; string ver = d.Target.ToString();
            if (!UpdateRules.UrlAllowed(f.Url, f.Dll)) { Warn("[update] refusing non-https download url " + f.Dll); return "not installed: a non-https download url"; }
            string target = Path.Combine(s.Dir, UpdateFiles.Versioned(ver));
            if (File.Exists(target) && Sha256(File.ReadAllBytes(target)) == f.Sha && (!down || IdentityIs(target, ver)))
            {
                SetPending(ver, d.Kind, f.Notes);
                Info("[update] " + ver + " is already downloaded" + (down ? "" : "; restart the game to apply"));     // stepping down, the set-aside decides that
                KeepNotes(s.Dir, ver, f.Notes);
                return down ? "ok there" : ver + " (" + d.Kind + ") already downloaded - it runs from the next start";
            }
            string keep = Path.Combine(s.Dir, UpdateFiles.Keep(ver));
            if (down && File.Exists(keep) && Sha256(File.ReadAllBytes(keep)) == f.Sha && IdentityIs(keep, ver))
            {
                if (File.Exists(target)) File.Delete(target);
                File.Move(keep, target);
                SetPending(ver, d.Kind, f.Notes); RemoveDevMarker(s.Dir);
                Info("[update] the kept " + d.Kind + " " + ver + " is back as " + Path.GetFileName(target) + " (its SHA-256 is the feed's) - no download");
                KeepNotes(s.Dir, ver, f.Notes);
                return "ok keep";
            }
            Info("[update] " + ver + " (" + d.Kind + ") " + (down ? "to go back to" : "available") + " (running " + s.Version + "), downloading " + f.Dll);
            var got = await Get(s, f.Dll);
            if (got.Status != 200 || got.Body == null) { Warn("[update] download of " + ver + " failed (" + (got.Error ?? "HTTP " + got.Status) + "), not installed"); return "not installed: the download failed"; }
            byte[] data = got.Body;
            string sha = Sha256(data);
            if (sha != f.Sha) { Warn("[update] sha256 mismatch for " + ver + " (expected " + f.Sha + ", got " + sha + "), not installed"); return "not installed: sha256 mismatch"; }
            string part = target + ".part";
            File.WriteAllBytes(part, data);
            try { AssemblyName.GetAssemblyName(part); }
            catch (Exception e) { File.Delete(part); Warn("[update] download is not a valid assembly (" + e.Message + "), not installed"); return "not installed: not an assembly"; }
            if (down && !IdentityIs(part, ver))
            {
                string says = BuildIdentity(part) ?? "nothing";
                File.Delete(part);
                Warn("[update] the download for " + ver + " says it is " + says + " - not installed, nothing set aside");
                return "not installed: the file is not " + ver;
            }
            if (File.Exists(target)) File.Delete(target);
            File.Move(part, target);
            SetPending(ver, d.Kind, f.Notes); RemoveDevMarker(s.Dir);
            Info("[update] downloaded " + ver + " (" + data.Length + " bytes) to " + Path.GetFileName(target) + "; restart the game to apply" + (f.Notes.Length > 0 ? " - " + f.Notes : ""));
            KeepNotes(s.Dir, ver, f.Notes);
            return down ? "ok downloaded" : null;
        }

        static void SetPending(string ver, string kind, string notes) { PendingNotes = notes ?? ""; PendingKind = kind ?? ""; Pending = ver; }

        /// <summary>The file says it is that version (InformationalVersion without its +commit).</summary>
        static bool IdentityIs(string path, string ver) { var v = UpdateRules.ParseVersion(BuildIdentity(path)); return v != null && v.ToString() == ver; }

        static async Task<Feed> FetchFeed(Setup s, string url)
        {
            var r = await Get(s, url);
            if (r.Status == 404) return Feed.Of(FeedState.Absent, url, "HTTP 404");
            if (r.Status != 200 || r.Body == null) return Feed.Of(FeedState.Failed, url, r.Error ?? "HTTP " + r.Status);
            var f = UpdateRules.ParseFeed(Encoding.UTF8.GetString(r.Body).TrimStart('\uFEFF'), url);
            if (f.State == FeedState.Failed) Warn("[update] " + f.Error);
            return f;
        }

        static async Task<Fetched> Get(Setup s, string url)
        {
            try
            {
                if (s.Fetch != null) return await s.Fetch(url) ?? new Fetched { Error = "no answer" };
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(40);
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("YazsCompanion/" + s.Version);
                    http.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                    using (var resp = await http.GetAsync(url))
                    {
                        var r = new Fetched { Status = (int)resp.StatusCode };
                        if (resp.IsSuccessStatusCode) r.Body = await resp.Content.ReadAsByteArrayAsync();
                        else r.Error = "HTTP " + (int)resp.StatusCode;
                        return r;
                    }
                }
            }
            catch (Exception e) { return new Fetched { Error = e.GetType().Name + ": " + e.Message }; }
        }

        // ================================================================ setting aside, putting back, the kept release

        /// <summary>Sets the running build aside: renames it to &lt;file&gt;.off (an older .off of that name deleted first) - only after
        /// the build that replaces it is verified on disk. Idempotent: no file = already set aside (the Steam relaunch runs Load()
        /// twice). A refused rename (R1: renaming a loaded DLL is unproven under Proton) leaves &lt;file&gt;.offnext, an empty request a
        /// launcher can complete before the next start. Returns "done", "already" or "refused: &lt;why&gt;".</summary>
        public static string Retire(string running)
        {
            if (string.IsNullOrEmpty(running) || !File.Exists(running)) return "already";
            string off = UpdateFiles.Off(running);
            try
            {
                if (File.Exists(off)) File.Delete(off);
                File.Move(running, off);
                try { string next = UpdateFiles.OffNext(running); if (File.Exists(next)) File.Delete(next); } catch { }
                return "done";
            }
            catch (Exception e)
            {
                try { File.WriteAllBytes(UpdateFiles.OffNext(running), new byte[0]); } catch { }
                return "refused: " + e.GetType().Name + ": " + Cut(e.Message, 90);
            }
        }

        /// <summary>The switch turned back before the restart: the set-aside running file is put back under its own name, and a
        /// launcher request for it withdrawn. True when something was put back.</summary>
        public static bool Undo(string running)
        {
            if (string.IsNullOrEmpty(running)) return false;
            bool back = false;
            try
            {
                string off = UpdateFiles.Off(running), next = UpdateFiles.OffNext(running);
                if (!File.Exists(running) && File.Exists(off))
                {
                    File.Move(off, running); back = true;
                    // the lower build fetched to go back to no longer runs next: the strip forgets it (CleanupSiblings keeps it as .keep)
                    var mine = FileVersion(running); var pend = UpdateRules.ParseVersion(Pending);
                    if (pend != null && mine != null && pend < mine) { Pending = null; PendingKind = ""; PendingNotes = ""; }
                    Info("[update] " + Path.GetFileName(running) + " put back: it runs again from the next start (the lower build beside it is kept as the way back)");
                }
                if (File.Exists(next) && File.Exists(running)) { File.Delete(next); Info("[update] the request to set " + Path.GetFileName(running) + " aside is withdrawn"); }
            }
            catch (Exception e) { Warn("[update] could not put " + Path.GetFileName(running) + " back: " + e.Message); }
            return back;
        }

        /// <summary>Critic 2: after a decision, a sibling build newer than its target - a test build downloaded while On, Off since
        /// - or a withdrawn one would still win the next start (BepInEx loads the highest): it is set aside (.off), and the
        /// restart strip forgets it. Review fix: also when Off could not decide, with the release as the ceiling.</summary>
        static void SetAsideUnwanted(Setup s, Version ceiling, Feed fs, Feed fb)
        {
            if (ceiling == null || string.IsNullOrEmpty(s.Running)) return;
            var w = WithdrawnSet(fs, fb);
            try
            {
                foreach (var f in Siblings(Path.GetDirectoryName(s.Running), s.Running))
                {
                    var v = FileVersion(f);
                    if (v == null || !(v > ceiling || w.Contains(v.ToString()))) continue;
                    SetAside(f, w.Contains(v.ToString()) ? "a withdrawn build" : "newer than " + ceiling + ", which this check wants", v.ToString());
                }
            }
            catch (Exception e) { Warn("[update] siblings not checked: " + e.Message); }
        }

        /// <summary>The versions the answered feeds list as withdrawn.</summary>
        static HashSet<string> WithdrawnSet(Feed fs, Feed fb)
        {
            var w = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in new[] { fs, fb }) if (f != null && f.Ok) foreach (var v in f.Withdrawn) w.Add(v);
            return w;
        }

        /// <summary>Review fix (critic 2 with 13): Off, and the release feed did not answer either - the test build this session
        /// downloaded (the strip's) is set aside all the same; a pending release stays.</summary>
        static void SetAsidePendingTest(Setup s)
        {
            string p = Pending;
            if (p == null || PendingKind != "test build" || string.IsNullOrEmpty(s.Dir)) return;
            string f = Path.Combine(s.Dir, UpdateFiles.Versioned(p));
            if (File.Exists(f) && !string.Equals(Path.GetFullPath(f), Path.GetFullPath(s.Running ?? ""), StringComparison.OrdinalIgnoreCase))
                SetAside(f, "the test build downloaded this session; Beta updates are off", p);
            else ForgetPending();
        }

        static void SetAside(string f, string why, string version)
        {
            string off = UpdateFiles.Off(f);
            try
            {
                if (File.Exists(off)) File.Delete(off);
                File.Move(f, off);
                Info("[update] " + Path.GetFileName(f) + " set aside (" + why + ")");
                if (Pending == version) ForgetPending();
            }
            catch (Exception e) { Warn("[update] could not set " + Path.GetFileName(f) + " aside: " + e.Message); }
        }

        /// <summary>The release an answered check saw (update_release.txt): a .keep is only ever this build.</summary>
        internal sealed class ReleaseRecord { public Version Version; public string Sha; }

        internal static ReleaseRecord ReadRelease(string dir)
        {
            try
            {
                string p = Path.Combine(dir ?? "", UpdateFiles.ReleaseFile);
                if (!File.Exists(p)) return null;
                var lines = File.ReadAllLines(p);
                var v = lines.Length > 0 ? UpdateRules.ParseVersion(lines[0]) : null;
                string sha = null;
                foreach (var l in lines) if (l.StartsWith("sha256 ", StringComparison.OrdinalIgnoreCase)) sha = l.Substring(7).Trim().ToLowerInvariant();
                return v != null && !string.IsNullOrEmpty(sha) ? new ReleaseRecord { Version = v, Sha = sha } : null;
            }
            catch { return null; }
        }

        /// <summary>Writes update_release.txt when the release feed names another build (a .part file, then moved). A record that
        /// cannot be brought up to date is removed: no record keeps no .keep, an old one could keep the wrong build. Line 1 the
        /// version, line 2 "sha256 &lt;hex&gt;": a launcher reads them too (UpdateFiles; integration fix 2026-10-12).</summary>
        static void RecordRelease(string dir, Feed fs)
        {
            string p = Path.Combine(dir ?? "", UpdateFiles.ReleaseFile);
            try
            {
                var old = ReadRelease(dir);
                if (old != null && old.Version == fs.Version && old.Sha == fs.Sha) return;
                string part = p + ".part";
                File.WriteAllText(part, fs.Version + "\nsha256 " + fs.Sha + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                    + " - the release feed's build: a " + UpdateFiles.Keep(fs.Version.ToString()) + " beside a test build is kept only while it is this build\n", new UTF8Encoding(false));
                if (File.Exists(p)) File.Delete(p);
                File.Move(part, p);
                Info("[update] the release is " + fs.Version + " (kept in " + UpdateFiles.ReleaseFile + ": the only build a test build keeps as its way back)");
            }
            catch (Exception e)
            {
                try { if (File.Exists(p)) File.Delete(p); } catch { }
                Warn("[update] " + UpdateFiles.ReleaseFile + " not written (" + e.Message + ") - no build is kept as the way back until it is");
            }
        }

        /// <summary>Why a .keep is not the way back, or null when it is: the recorded release's version and SHA-256, at or above the
        /// floor, and (at load, <paramref name="below"/> = the running build) below the build that runs.</summary>
        static string KeepWhy(string file, ReleaseRecord rel, Version below)
        {
            if (rel == null) return "no release recorded yet";
            var v = UpdateFiles.VersionInName(Path.GetFileName(file));
            if (v == null) return "no version in its name";
            if (v != rel.Version) return "the release is " + rel.Version;
            if (v < UpdateRules.Floor) return "below " + UpdateRules.Floor;
            if (below != null && v >= below) return v == below ? "this build is the release" : "above this build";
            if (FileSha(file) != rel.Sha) return "not the release's SHA-256";
            return null;
        }

        /// <summary>Critic 4, in a check: every .keep that is not the recorded release goes (at most one stays). The release running
        /// keeps its own .keep here - a test build just downloaded wins the next start; CleanupSiblings drops it if not.</summary>
        static void TidyKeep(string dir, ReleaseRecord rel)
        {
            try
            {
                foreach (var f in Directory.GetFiles(dir, FilePrefix + "*"))
                {
                    string name = Path.GetFileName(f);
                    if (!name.EndsWith(".dll.keep", StringComparison.OrdinalIgnoreCase)) continue;
                    string why = KeepWhy(f, rel, null);
                    if (why == null) continue;
                    try { File.Delete(f); Info("[update] removed the kept build " + name + " (" + why + ")"); } catch { }
                }
            }
            catch (Exception e) { Warn("[update] kept builds not checked: " + e.Message); }
        }

        /// <summary>Critic 4a (review fix): a test build was just downloaded while the release runs (its version and SHA-256 are the
        /// release feed's) - the release is copied to its .keep now (a .part file, checked, then moved), so the way back is there
        /// before the test build's first start, even for one that dies before its own load tidies anything.</summary>
        static void KeepRunningRelease(Setup s, Feed fs, Version run, string runSha)
        {
            if (!fs.Ok || run == null || run != fs.Version || run < UpdateRules.Floor || runSha == null || runSha != fs.Sha || string.IsNullOrEmpty(s.Dir)) return;
            string keep = Path.Combine(s.Dir, UpdateFiles.Keep(run.ToString())), part = keep + ".part";
            try
            {
                if (File.Exists(keep) && FileSha(keep) == fs.Sha) return;
                File.Copy(s.Running, part, true);
                if (FileSha(part) != fs.Sha) { File.Delete(part); Warn("[update] the copy of the release " + run + " is not its SHA-256 - not kept"); return; }
                if (File.Exists(keep)) File.Delete(keep);
                File.Move(part, keep);
                Info("[update] the release " + run + " is kept as " + Path.GetFileName(keep) + " (the way back from the test build, there before its first start)");
            }
            catch (Exception e) { try { if (File.Exists(part)) File.Delete(part); } catch { } Warn("[update] the release " + run + " was not kept: " + e.Message); }
        }

        /// <summary>Critic 7: dev_build.txt (the csproj's deploy writes it: the build's InformationalVersion, then "sha256 &lt;hex&gt;")
        /// names the running build - by its InformationalVersion or by the SHA-256 of the running file. One that names another build
        /// is stale (a release installed over a development deploy) and goes. Review fix: one that names this build goes too when an
        /// answered feed publishes these very bytes (this version, this SHA-256, the commit when it names one) - the deploy was the
        /// build then released, and release.ps1's local deploy finds "the same DLL" and copies nothing.</summary>
        static bool DevMarker(Setup s, string runSha, Feed fs, Feed fb)
        {
            try
            {
                string p = Path.Combine(s.Dir ?? "", UpdateFiles.DevMarker);
                if (!File.Exists(p)) return false;
                var lines = File.ReadAllLines(p);
                string info = lines.Length > 0 ? lines[0].Trim() : "", sha = null;
                foreach (var l in lines) if (l.StartsWith("sha256 ", StringComparison.OrdinalIgnoreCase)) sha = l.Substring(7).Trim().ToLowerInvariant();
                bool names = (!string.IsNullOrEmpty(s.Info) && info == s.Info) || (sha != null && runSha != null && runSha == sha);
                if (names)
                {
                    var run = UpdateRules.ParseVersion(s.Version);
                    Feed pub = null;
                    foreach (var f in new[] { fs, fb })
                        if (pub == null && f != null && f.Ok && run != null && f.Version == run && runSha != null && f.Sha == runSha && (string.IsNullOrEmpty(f.Commit) || UpdateRules.SameCommit(f.Commit, s.Commit))) pub = f;
                    if (pub == null) return true;
                    File.Delete(p);
                    Info("[update] removed " + UpdateFiles.DevMarker + ": this build is the published " + run + " (its SHA-256 is the feed's)");
                    return false;
                }
                File.Delete(p);
                Info("[update] removed a stale " + UpdateFiles.DevMarker + " (it names " + (info.Length > 0 ? info : "nothing") + ", this build is " + s.Info + ")");
            }
            catch (Exception e) { Warn("[update] " + UpdateFiles.DevMarker + " not read: " + e.Message); }
            return false;
        }

        /// <summary>A file's SHA-256, or null when it cannot be read.</summary>
        static string FileSha(string path)
        {
            try { return !string.IsNullOrEmpty(path) && File.Exists(path) ? Sha256(File.ReadAllBytes(path)) : null; }
            catch { return null; }
        }

        /// <summary>The updater's own installs end a development deploy's marker (critic 7).</summary>
        static void RemoveDevMarker(string dir)
        {
            try { string p = Path.Combine(dir ?? "", UpdateFiles.DevMarker); if (File.Exists(p)) { File.Delete(p); Info("[update] removed " + UpdateFiles.DevMarker + " (an updater install replaces the development build)"); } }
            catch { }
        }

        // ================================================================ the rename probe

        /// <summary>Once per build (update_probe.txt names the build it answered for): can the running DLL be renamed while loaded?
        /// The answer is logged at every check: "[update] setting this build aside works here" / "... is refused here (why)".
        /// Returns "works" or "refused: why".</summary>
        public static string ProbeOnce(string running, string info)
        {
            string result = null;
            string file = null;
            try
            {
                file = Path.Combine(Path.GetDirectoryName(running) ?? "", UpdateFiles.ProbeFile);
                if (File.Exists(file))
                {
                    var lines = File.ReadAllLines(file);
                    if (lines.Length >= 2 && lines[0].Trim() == (info ?? "")) result = lines[1].Trim();
                }
            }
            catch { }
            if (result == null)
            {
                result = Probe(running);
                try { if (file != null) File.WriteAllText(file, (info ?? "") + "\n" + result + "\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n", new UTF8Encoding(false)); } catch { }
            }
            if (result == "works") Info("[update] setting this build aside works here");
            else Info("[update] setting this build aside is refused here (" + (result.StartsWith("refused: ", StringComparison.Ordinal) ? result.Substring(9) : result) + ")");
            return result;
        }

        /// <summary>The probe itself: the running file renamed to a sibling name that still ends in .dll, and straight back (a cut
        /// between the two leaves a loadable file).</summary>
        public static string Probe(string running)
        {
            if (string.IsNullOrEmpty(running) || !File.Exists(running)) return "refused: the running file is not there";
            string other = Path.Combine(Path.GetDirectoryName(running), UpdateFiles.ProbeName(Path.GetFileName(running)));
            bool moved = false;
            try
            {
                if (File.Exists(other)) File.Delete(other);
                File.Move(running, other); moved = true;
                File.Move(other, running); moved = false;
                return "works";
            }
            catch (Exception e) { return "refused: " + (moved ? "the rename back failed - " : "") + e.GetType().Name + ": " + Cut(e.Message, 90); }
        }

        // ================================================================ Wine (critic 8: the rescue words)

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = false)] static extern IntPtr GetModuleHandleW(string name);
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = false)] static extern IntPtr GetProcAddress(IntPtr module, string name);

        /// <summary>Running under Wine / Proton: ntdll exports wine_get_version there (and Steam's compatibility tool sets
        /// STEAM_COMPAT_DATA_PATH). False on Windows, and whenever the test cannot run.</summary>
        public static bool DetectWine()
        {
            try { if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STEAM_COMPAT_DATA_PATH"))) return true; } catch { }
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
                var h = GetModuleHandleW("ntdll.dll");
                return h != IntPtr.Zero && GetProcAddress(h, "wine_get_version") != IntPtr.Zero;
            }
            catch { return false; }
        }

        // ---- 0.15.0: the release notes kept on disk for a what's-new card (pure .NET file calls, any thread) ----

        public const string NotesPrefix = "notes_";

        /// <summary>notes_&lt;version&gt;.txt for a version string, the version written as it parses ("0.15.0"), or null when it
        /// is not a version (nothing from the feed reaches a file name unparsed).</summary>
        public static string NotesFile(string version)
        {
            Version v;
            return Version.TryParse((version ?? "").Trim(), out v) ? NotesPrefix + v + ".txt" : null;
        }

        /// <summary>Keeps a version's release notes in dir as notes_&lt;version&gt;.txt (UTF-8, written to a .part file and moved
        /// into place, so a reader never sees half of it). Returns "written", "kept" (the file already holds them), "empty"
        /// (no notes, nothing written), "not a version" or "failed: ..."; never throws.</summary>
        public static string CacheNotes(string dir, string version, string notes)
        {
            string name = NotesFile(version);
            if (name == null) return "not a version";
            string text = (notes ?? "").Replace("\r\n", "\n").Trim();
            if (text.Length == 0 || string.IsNullOrEmpty(dir)) return "empty";
            text += "\n";
            try
            {
                string path = Path.Combine(dir, name), part = path + ".part";
                if (File.Exists(path) && File.ReadAllText(path) == text) return "kept";
                File.WriteAllText(part, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(part, path);
                return "written";
            }
            catch (Exception e) { return "failed: " + e.GetType().Name + ": " + e.Message; }
        }

        /// <summary>Removes the notes_&lt;version&gt;.txt files of versions older than the running one (they went with their
        /// builds) and any stray .part; the running and newer versions' notes and every other file stay. Returns the names
        /// removed; never throws.</summary>
        public static List<string> PruneNotes(string dir, string runningVersion)
        {
            var removed = new List<string>();
            Version mine;
            if (string.IsNullOrEmpty(dir) || !Version.TryParse((runningVersion ?? "").Trim(), out mine)) return removed;
            try
            {
                if (!Directory.Exists(dir)) return removed;
                foreach (var f in Directory.GetFiles(dir, NotesPrefix + "*.txt*"))
                {
                    string file = Path.GetFileName(f);
                    bool part = file.EndsWith(".txt.part", StringComparison.OrdinalIgnoreCase);
                    if (!part && !file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) continue;
                    string ver = file.Substring(NotesPrefix.Length, file.Length - NotesPrefix.Length - (part ? 9 : 4));
                    Version v;
                    if (!part && (!Version.TryParse(ver, out v) || v >= mine)) continue;
                    try { File.Delete(f); removed.Add(file); } catch { }
                }
            }
            catch { }
            return removed;
        }

        static void KeepNotes(string dir, string version, string notes)
        {
            string r = CacheNotes(dir, version, notes);
            if (r == "written") Info("[update] release notes of " + version + " kept in " + NotesFile(version) + " (" + (notes ?? "").Trim().Length + " characters)");
            else if (r.StartsWith("failed", StringComparison.Ordinal)) Info("[update] release notes of " + version + " not kept: " + r);
        }

        public static string Sha256(byte[] data)
        {
            using (var h = SHA256.Create())
            {
                var sb = new StringBuilder();
                foreach (var b in h.ComputeHash(data)) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
        public static string Sha256(string text) { return Sha256(Encoding.UTF8.GetBytes((text ?? "").Replace("\r\n", "\n"))); }

        static string Cut(string s, int n) { s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim(); return s.Length > n ? s.Substring(0, n - 3) + "..." : s; }
    }
}
