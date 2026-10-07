// 0.15.0 (C15-10): the README and the CHANGELOG, and the release notes the updater keeps on disk for a what's-new card.
//   D1  the README describes the mod as it is: no release history (no status block, no "Next steps" list), a short
//       "Open checks" list, no promise to pick for the player (an "optional autoselect"), the extension API's version as
//       Api/Extensions.cs has it, and the CHANGELOG linked.
//   D2  CHANGELOG.md holds every release from 0.5.0 to the running VERSION (Plugin.cs), newest first, one dated heading
//       each; the running VERSION's entry is final, not "in progress" (release.ps1 runs this bench as its gate, so a
//       version bump without its entry, or with an unfinished one, stops the release).
//   D3  Updater's notes cache in a test folder: notes_<version>.txt written, kept when unchanged, written again when the
//       notes change; nothing for empty notes or a version that does not parse, a failure reported instead of thrown;
//       the notes of older versions and stray .part files removed, the running and newer versions' notes and every other
//       file kept.
// Runs with and without game data (Verdict.Tail). Generic names only (the repository is public).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class DocCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (ok || string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", file));
            return File.Exists(p) ? File.ReadAllText(p).Replace("\r\n", "\n") : null;
        }

        // every release published before the CHANGELOG existed; the running VERSION joins them
        static readonly string[] Released =
        {
            "0.5.0", "0.5.1", "0.5.2", "0.5.3", "0.5.4", "0.5.5", "0.6.0", "0.7.0", "0.8.0", "0.9.0",
            "0.10.0", "0.10.1", "0.10.2", "0.11.0", "0.12.0", "0.12.1", "0.12.2", "0.13.0", "0.14.0",
        };

        public static int Run()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.15.0: the README and the CHANGELOG, the release notes kept for a what's-new card (C15-10)");
            Readme();
            Changelog();
            NotesCache();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ---------------------------------------------------------------- D1
        static void Readme()
        {
            string readme = Src("README.md"), api = Src("YazsCompanion.Mod/Api/Extensions.cs");
            if (readme == null || api == null) { Check("D1", "the README describes the mod as it is", false, "README.md or Api/Extensions.cs not found"); return; }
            var why = new List<string>();
            var lines = readme.Split('\n');
            var status = lines.Where(l => l.StartsWith("Status (", StringComparison.Ordinal) || Regex.IsMatch(l, @"^\*\*0\.\d+\.\d+ — ")).ToList();
            if (status.Count > 0) why.Add("a status block: '" + Cut(status[0]) + "'");
            if (lines.Contains("## Next steps")) why.Add("a 'Next steps' list");
            int open = Array.IndexOf(lines, "## Open checks");
            int items = 0;
            if (open < 0) why.Add("no 'Open checks' section");
            else for (int i = open + 1; i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal); i++) if (Regex.IsMatch(lines[i], @"^\d+\. ")) items++;
            if (open >= 0 && (items < 1 || items > 12)) why.Add("'Open checks' holds " + items + " items (a short list: 1 - 12)");
            if (Regex.IsMatch(readme, @"\bautoselect\b", RegexOptions.IgnoreCase)) why.Add("it mentions an autoselect (the mod never picks for you)");
            var code = Regex.Match(api, @"ApiVersion\s*=>\s*(\d+)");
            var doc = Regex.Match(readme, @"ApiVersion \{ get; \}\s*//\s*(\d+)");
            if (!code.Success || !doc.Success || code.Groups[1].Value != doc.Groups[1].Value)
                why.Add("ApiVersion " + (doc.Success ? doc.Groups[1].Value : "?") + " in the README, " + (code.Success ? code.Groups[1].Value : "?") + " in the code");
            if (!readme.Contains("](CHANGELOG.md)")) why.Add("no link to CHANGELOG.md");
            // the review of 10-06 (C15-10's "under about 900 lines"): the internals live in docs/INTERNALS.md, linked
            if (lines.Length > 950) why.Add("README.md has " + lines.Length + " lines (about 900 wanted, 950 at most)");
            if (!readme.Contains("](docs/INTERNALS.md") || Src("docs/INTERNALS.md") == null) why.Add("docs/INTERNALS.md missing or not linked");
            Check("D1", "the README describes the mod as it is: no status block, no 'Next steps', a short 'Open checks' list, no autoselect, the API version the code has, the CHANGELOG linked; about 900 lines (950 at most), the internals in docs/INTERNALS.md",
                why.Count == 0, string.Join("; ", why));
        }

        // ---------------------------------------------------------------- D2
        static void Changelog()
        {
            string log = Src("CHANGELOG.md"), plugin = Src("YazsCompanion.Mod/Plugin.cs");
            if (log == null || plugin == null) { Check("D2", "the CHANGELOG holds every release", false, "CHANGELOG.md or Plugin.cs not found"); return; }
            var vm = Regex.Match(plugin, "VERSION = \"([^\"]+)\"");
            Version running;
            if (!vm.Success || !Version.TryParse(vm.Groups[1].Value, out running)) { Check("D2", "the CHANGELOG holds every release", false, "no VERSION in Plugin.cs"); return; }
            var heads = new List<KeyValuePair<Version, string>>();
            foreach (Match m in Regex.Matches(log, @"^## (\d+\.\d+\.\d+)\b(.*)$", RegexOptions.Multiline))
                heads.Add(new KeyValuePair<Version, string>(Version.Parse(m.Groups[1].Value), m.Value.TrimEnd()));
            var why = new List<string>();
            for (int i = 1; i < heads.Count; i++)
                if (heads[i].Key >= heads[i - 1].Key) why.Add("'" + Cut(heads[i].Value) + "' after '" + Cut(heads[i - 1].Value) + "' (newest first, one heading each)");
            var want = Released.Select(Version.Parse).Concat(new[] { running }).Distinct();
            foreach (var v in want)
            {
                var h = heads.Where(x => x.Key == v).Select(x => x.Value).FirstOrDefault();
                if (h == null) { why.Add("no entry for " + v); continue; }
                if (!Regex.IsMatch(h, @"\(\d{4}-\d\d-\d\d\)")) why.Add("'" + Cut(h) + "' has no release date (yyyy-mm-dd)");
                if (v == running && Regex.IsMatch(h, "in progress|not released", RegexOptions.IgnoreCase))
                    why.Add("the entry of the running VERSION " + v + " still says it is in progress - finish it before the release");
            }
            Check("D2", "the CHANGELOG holds every release from 0.5.0 to the running VERSION, newest first, each dated; the running version's entry is final",
                why.Count == 0, string.Join("; ", why));
        }

        // ---------------------------------------------------------------- D3
        static void NotesCache()
        {
            string dir = Path.Combine(Path.GetTempPath(), "yazs_bench_notes_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            var why = new List<string>();
            try
            {
                Directory.CreateDirectory(dir);
                string Read(string f) { string p = Path.Combine(dir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
                Action<string, string, string> expect = (got, wanted, what) => { if (got != wanted) why.Add(what + ": '" + got + "', wanted '" + wanted + "'"); };

                expect(Updater.NotesFile("0.15.0"), "notes_0.15.0.txt", "the file of 0.15.0");
                expect(Updater.NotesFile(" 0.15.0 "), "notes_0.15.0.txt", "the file of ' 0.15.0 '");
                expect(Updater.NotesFile("0.15.0/../x") ?? "null", "null", "a version with a path in it");
                expect(Updater.CacheNotes(dir, "0.15.0", "First line.\r\nSecond line.  "), "written", "new notes");
                expect(Read("notes_0.15.0.txt"), "First line.\nSecond line.\n", "the file's text");
                expect(Updater.CacheNotes(dir, "0.15.0", "First line.\nSecond line."), "kept", "the same notes again");
                expect(Updater.CacheNotes(dir, "0.15.0", "Changed notes."), "written", "changed notes");
                expect(Read("notes_0.15.0.txt"), "Changed notes.\n", "the file after the change");
                expect(Updater.CacheNotes(dir, "0.16.0", "   "), "empty", "empty notes");
                expect(Read("notes_0.16.0.txt") ?? "null", "null", "a file for empty notes");
                expect(Updater.CacheNotes(dir, "../0.16.0", "Notes."), "not a version", "a name that is not a version");
                string failed = Updater.CacheNotes(Path.Combine(dir, "missing"), "0.15.0", "Notes.");
                if (!failed.StartsWith("failed: ", StringComparison.Ordinal)) why.Add("a folder that is not there: '" + failed + "', wanted 'failed: ...'");

                foreach (var f in new[] { "notes_0.13.0.txt", "notes_0.14.0.txt", "notes_0.14.0.txt.part", "notes_0.16.0.txt", "notes_draft.txt", "other.txt", "YazsCompanionMod-0.13.0.dll" })
                    File.WriteAllText(Path.Combine(dir, f), "x");
                var removed = Updater.PruneNotes(dir, "0.15.0").OrderBy(s => s, StringComparer.Ordinal).ToList();
                expect(string.Join(", ", removed), "notes_0.13.0.txt, notes_0.14.0.txt, notes_0.14.0.txt.part", "removed under 0.15.0");
                var left = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(s => s, StringComparer.Ordinal).ToList();
                expect(string.Join(", ", left), "YazsCompanionMod-0.13.0.dll, notes_0.15.0.txt, notes_0.16.0.txt, notes_draft.txt, other.txt", "left under 0.15.0");
                expect(Updater.PruneNotes(dir, "not a version").Count.ToString(), "0", "removed under a version that does not parse");
            }
            catch (Exception e) { why.Add("threw " + e.GetType().Name + ": " + e.Message); }
            finally { try { Directory.Delete(dir, true); } catch { } }
            Check("D3", "the release notes kept for a what's-new card: notes_<version>.txt written, kept, written again on a change; nothing for empty notes or a non-version, a failure reported, never thrown; older notes and stray .part files removed, the running and newer ones kept",
                why.Count == 0, string.Join("; ", why));
        }

        static string Cut(string s) { s = (s ?? "").Trim(); return s.Length > 70 ? s.Substring(0, 67) + "..." : s; }
    }
}
