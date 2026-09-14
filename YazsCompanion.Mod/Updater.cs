// Auto-update. On every launch a background task fetches latest.json from the GitHub release feed
// (config UpdateUrl); if it names a newer version, the DLL is downloaded next to the running one as
// YazsCompanionMod-<version>.dll and verified against the published SHA-256. Nothing running is ever
// replaced: BepInEx loads only the highest [BepInPlugin] version of a GUID ("... because a newer version
// exists"), so the next launch runs the new file, which then deletes the older copies (CleanupSiblings).
// Only .NET file and network calls happen off the main thread; the game objects are never touched here.
using System;
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
                if (latest <= current) { Plugin.Logger.LogInfo("[update] up to date (latest " + latestStr + ")"); return; }
                if (!dllUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Logger.LogWarning("[update] refusing non-https download url " + dllUrl); return;
                }

                sha = sha.Trim().ToLowerInvariant();
                string target = Path.Combine(dir, FilePrefix + "-" + latestStr + ".dll");
                if (File.Exists(target) && Sha256(File.ReadAllBytes(target)) == sha)
                {
                    Pending = latestStr; PendingNotes = notes;
                    Plugin.Logger.LogInfo("[update] " + latestStr + " is already downloaded; restart the game to apply"); return;
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
            }
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
