// Auto-update. On every launch a background task fetches latest.json from the GitHub release feed
// (config UpdateUrl); if it names a newer version, the DLL is downloaded next to the running one as
// YazsCompanionMod-<version>.dll and verified against the published SHA-256. Nothing running is ever
// replaced: BepInEx loads only the highest [BepInPlugin] version of a GUID ("... because a newer version
// exists"), so the next launch runs the new file, which then deletes the older copies (CleanupSiblings).
// Since 0.15.0 the feed's release notes are kept next to the DLL as notes_<version>.txt for a later what's-new
// card: the pending version's when it is downloaded (or found downloaded), the running version's when the feed
// names it as the latest; the notes of versions older than the running one are removed (CacheNotes / PruneNotes).
// Only .NET file and network calls happen off the main thread; the game objects are never touched here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace YazsCompanion
{
    internal static class Updater
    {
        public const string FilePrefix = "YazsCompanionMod";
        /// <summary>Version downloaded and waiting for a restart, or null. Read on the main thread by Notice.</summary>
        public static volatile string Pending;
        public static volatile string PendingNotes = "";

        /// <summary>Delete every other YazsCompanionMod*.dll next to the running one: BepInEx chose us as the newest.</summary>
        public static void CleanupSiblings()
        {
            try
            {
                string me = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(me)) return;
                string dir = Path.GetDirectoryName(me);
                foreach (var f in Directory.GetFiles(dir, FilePrefix + "*.dll"))
                {
                    if (string.Equals(Path.GetFullPath(f), Path.GetFullPath(me), StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(f); Plugin.Logger.LogInfo("[update] removed older build " + Path.GetFileName(f)); }
                    catch (Exception e) { Plugin.Logger.LogWarning("[update] could not remove " + Path.GetFileName(f) + ": " + e.Message); }
                }
                foreach (var f in Directory.GetFiles(dir, FilePrefix + "*.part")) { try { File.Delete(f); } catch { } }
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[update] cleanup: " + e.Message); }
        }

        public static void CheckInBackground(string url, string currentVersion, string dir)
        {
            Task.Run(async () =>
            {
                try { await Check(url, currentVersion, dir); }
                catch (Exception e) { Plugin.Logger.LogWarning("[update] check failed: " + e.GetType().Name + ": " + e.Message); }
            });
        }

        static async Task Check(string url, string currentVersion, string dir)
        {
            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromSeconds(40);
                http.DefaultRequestHeaders.UserAgent.ParseAdd("YazsCompanion/" + currentVersion);
                http.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

                string json = await http.GetStringAsync(url);
                string latestStr = null, dllUrl = null, sha = null, notes = "";
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement; JsonElement e;
                    if (root.TryGetProperty("version", out e)) latestStr = e.GetString();
                    if (root.TryGetProperty("dll", out e)) dllUrl = e.GetString();
                    if (root.TryGetProperty("sha256", out e)) sha = e.GetString();
                    if (root.TryGetProperty("notes", out e)) notes = e.GetString() ?? "";
                }
                Version latest, current;
                if (!Version.TryParse(latestStr, out latest) || !Version.TryParse(currentVersion, out current) || string.IsNullOrEmpty(dllUrl) || string.IsNullOrEmpty(sha))
                {
                    Plugin.Logger.LogWarning("[update] latest.json unusable: " + Trim(json)); return;
                }
                var gone = PruneNotes(dir, currentVersion);
                if (gone.Count > 0) Plugin.Logger.LogInfo("[update] removed the release notes of older builds: " + string.Join(", ", gone));
                if (latest <= current)
                {
                    Plugin.Logger.LogInfo("[update] up to date (latest " + latestStr + ")");
                    if (latest == current) KeepNotes(dir, latestStr, notes);   // the running version's notes, for the what's-new card
                    return;
                }
                if (!dllUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Logger.LogWarning("[update] refusing non-https download url " + dllUrl); return;
                }

                sha = sha.Trim().ToLowerInvariant();
                string target = Path.Combine(dir, FilePrefix + "-" + latestStr + ".dll");
                if (File.Exists(target) && Sha256(File.ReadAllBytes(target)) == sha)
                {
                    Pending = latestStr; PendingNotes = notes;
                    Plugin.Logger.LogInfo("[update] " + latestStr + " is already downloaded; restart the game to apply");
                    KeepNotes(dir, latestStr, notes); return;
                }
                Plugin.Logger.LogInfo("[update] " + latestStr + " available (running " + currentVersion + "), downloading " + dllUrl);
                byte[] data = await http.GetByteArrayAsync(dllUrl);
                string got = Sha256(data);
                if (got != sha) { Plugin.Logger.LogWarning("[update] sha256 mismatch for " + latestStr + " (expected " + sha + ", got " + got + "), not installed"); return; }
                string part = target + ".part";
                File.WriteAllBytes(part, data);
                try { AssemblyName.GetAssemblyName(part); }
                catch (Exception e) { File.Delete(part); Plugin.Logger.LogWarning("[update] download is not a valid assembly (" + e.Message + "), not installed"); return; }
                if (File.Exists(target)) File.Delete(target);
                File.Move(part, target);
                Pending = latestStr; PendingNotes = notes;
                Plugin.Logger.LogInfo("[update] downloaded " + latestStr + " (" + data.Length + " bytes) to " + Path.GetFileName(target) + "; restart the game to apply" + (notes.Length > 0 ? " - " + notes : ""));
                KeepNotes(dir, latestStr, notes);
            }
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
            if (r == "written") Plugin.Logger.LogInfo("[update] release notes of " + version + " kept in " + NotesFile(version) + " (" + notes.Trim().Length + " characters)");
            else if (r.StartsWith("failed", StringComparison.Ordinal)) Plugin.Logger.LogInfo("[update] release notes of " + version + " not kept: " + r);
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

        static string Trim(string s) { s = (s ?? "").Replace("\n", " "); return s.Length > 160 ? s.Substring(0, 157) + "..." : s; }
    }
}
