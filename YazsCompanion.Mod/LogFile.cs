// companion.log's housekeeping (0.14.0, B5). The file is this plugin's own copy of its lines, appended across launches
// (BepInEx overwrites LogOutput.log every time) - and it never shrank: 8.4 MB and 1017 sessions after three weeks. At load,
// a file over 4 MB now moves to companion.log.1 (.1 to .2, .2 to .3, the oldest .3 goes) and a fresh one starts with the
// session header, by which the overhaul's marks and the test scripts find a session's lines.
// Plain .NET file calls, no game types: the offline bench rotates a test folder with it.
using System;
using System.IO;

namespace YazsCompanion
{
    internal static class LogFile
    {
        public const long RotateAt = 4L * 1024 * 1024;
        public const int Keep = 3;                  // companion.log.1 .. .3

        /// <summary>The line that opens a session in the log (Plugin's FileListener writes it first).</summary>
        public static string Header(DateTime now) { return "==== " + now.ToString("yyyy-MM-dd HH:mm:ss") + " session start ===="; }

        /// <summary>Moves <paramref name="path"/> to .1 when it is over <paramref name="limit"/> bytes, the older copies one step up
        /// and the oldest dropped. Each step in its own try, and nothing but the oldest copy is ever overwritten: a step that
        /// fails leaves its file where it is, and when the log itself cannot move it keeps growing as before. Returns what was
        /// done, for the log's first lines (null: under the limit, or no file yet).</summary>
        public static string Rotate(string path, long limit = RotateAt)
        {
            long len;
            try { var fi = new FileInfo(path); if (!fi.Exists || fi.Length <= limit) return null; len = fi.Length; }
            catch { return null; }
            string mb = (len / (1024.0 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
            try { if (File.Exists(path + "." + Keep)) File.Delete(path + "." + Keep); } catch { }
            for (int i = Keep - 1; i >= 1; i--)
            {
                string from = path + "." + i, to = path + "." + (i + 1);
                try { if (File.Exists(from) && !File.Exists(to)) File.Move(from, to); } catch { }
            }
            string name = Path.GetFileName(path);
            try
            {
                if (File.Exists(path + ".1")) return "the log was " + mb + " but " + name + ".1 could not make room: kept appending";
                File.Move(path, path + ".1");
                return "the log was " + mb + ": moved to " + name + ".1 (the older copies one step up, " + Keep + " kept)";
            }
            catch (Exception e) { return "the log was " + mb + " but could not move (" + e.GetType().Name + "): kept appending"; }
        }
    }
}
