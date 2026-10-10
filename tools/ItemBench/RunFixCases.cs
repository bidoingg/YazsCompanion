// 0.14.0: the fix-now items of the review of the user's 2026-10-05 run, replayed offline (section 10 of the bench):
//   B1  an Endless stat card is weighed by its own value against the Common card of its stat (Synergy.EndlessWeight,
//       RarityWeight, StatCard): the flat 2.6 put an Endless +2.5 % ability area over the build's main ability (6.07 vs 4.72);
//       the four logged cards are replayed with their stat weight taken back out of the logged score.
//   B3  a survivor's class level is the one of its first sight in the run (RankGate, Plan.RankClosed): a rank III ability
//       first looked at after the live class level crossed 40 stood as "next" on the readout to the end of the run.
//   B4  "AVOID" before the reason of a card scored under 1, in place of its place (Synergy.ReasonPrefix) - not the dull red
//       alone; 8 visible characters, so its line has 48 of the Deck's 56 (the integration review: "2ND   AVOID  " left 43).
//   A1  the reason line keeps its '>' (Synergy.ReasonText) and the ability level step is written "2 to 3 of 4": up to 0.13.0
//       "focus 2>3 of 4" was drawn "focus 23 of 4" on a quarter of the cards.
//   B5  companion.log moves to .1 over 4 MB at load (LogFile.Rotate), in a test folder; nothing but the oldest copy is lost.
//   B6  the quest's badge-objective note is a [Logging] Verbose line (a source check).
// Generic names only (the repository is public): the game's own card names.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class RunFixes
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string F(double v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        public static int Run()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.14.0: the 10-05 run - B1 Endless stat cards, B3 the class rank of a run, B4 AVOID in words, A1 the '>' kept, B5 the log rotated, B6 a verbose note");
            EndlessWeights();
            EndlessCards();
            RankGates();
            Prefixes();
            ReasonTexts();
            Rotation();
            Sources();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ---------------------------------------------------------------- B1
        static void EndlessWeights()
        {
            Check("B1", "EndlessWeight: the card's own ratio (0.25, 0.5)", Synergy.EndlessWeight(0.25) == 0.25 && Synergy.EndlessWeight(0.5) == 0.5,
                "0.25 -> " + F(Synergy.EndlessWeight(0.25)) + ", 0.5 -> " + F(Synergy.EndlessWeight(0.5)));
            Check("B1", "EndlessWeight: not readable (NaN, 0, below 0, infinite) -> 0.4",
                new[] { double.NaN, 0, -0.3, double.PositiveInfinity }.All(x => Synergy.EndlessWeight(x) == 0.4),
                "NaN " + F(Synergy.EndlessWeight(double.NaN)) + ", 0 " + F(Synergy.EndlessWeight(0)) + ", -0.3 " + F(Synergy.EndlessWeight(-0.3)) + ", inf " + F(Synergy.EndlessWeight(double.PositiveInfinity)));
            Check("B1", "EndlessWeight: held to 0.1 - 1.0", Synergy.EndlessWeight(0.02) == 0.1 && Synergy.EndlessWeight(3.0) == 1.0,
                "0.02 -> " + F(Synergy.EndlessWeight(0.02)) + ", 3 -> " + F(Synergy.EndlessWeight(3.0)));
            // 0.16.0 (C16-05): Rare and Legendary by their own value too (RankSmallCases.cs); unreadable, they keep the flat 2 / 3
            Check("B1", "Common 1 whatever the ratio; Rare / Legendary unreadable -> 2 / 3",
                Synergy.RarityWeight("Common", 0.25) == 1.0 && Synergy.RarityWeight("Rare", double.NaN) == 2.0 && Synergy.RarityWeight("Legendary", double.NaN) == 3.0 && Synergy.RarityWeight("Unknown", 0.25) == 1.0
                && Synergy.RarityWeight("Endless", 0.25) == 0.25 && Synergy.RarityWeight("Endless", double.NaN) == 0.4);
        }

        // the logged Endless cards: (time, card, logged score, the game's Endless and Common values, team-wide, the card it should go under, its score, the replay wanted)
        sealed class Logged { public string At, Card, Rival; public double Old, RivalScore, Want; public float Own, Common; public bool Team; }
        static void EndlessCards()
        {
            var cards = new[]
            {
                new Logged { At = "10-05 18:15:41", Card = "Zone of Action", Old = 6.07, Own = 0.025f, Common = 0.1f, Team = false, Rival = "Experiment 21", RivalScore = 4.72, Want = 1.49 },
                new Logged { At = "10-05 18:16:32", Card = "Kevlar Plating", Old = 4.91, Own = 5f, Common = 10f, Team = true, Rival = "Handgun", RivalScore = 4.79, Want = 1.91 },
                new Logged { At = "10-04 20:29:29", Card = "Master Blaster", Old = 8.36, Own = 0.05f, Common = 0.1f, Team = false, Rival = "Transmitter", RivalScore = 4.68, Want = 2.42 },
                new Logged { At = "10-04 21:20:44", Card = "UAV System", Old = 8.05, Own = 0.05f, Common = 0.1f, Team = false, Rival = "Nitro-Gun", RivalScore = 4.53, Want = 2.36 },
            };
            var now = new Dictionary<string, double>();
            foreach (var c in cards)
            {
                double w = (c.Old - 1.0 - (c.Team ? 0.2 : 0)) / (2.0 * 2.6);                        // the stat weight the logged score was made of
                double ratio = Math.Abs(c.Own) / Math.Abs(c.Common);                                 // as Ranker.OwnRatio reads it
                double before = Math.Round(Synergy.StatCard(2.6, w, c.Team), 2);
                double after = Math.Round(Synergy.StatCard(Synergy.RarityWeight("Endless", ratio), w, c.Team), 2);
                now[c.Card] = after;
                Check("B1", c.At + " " + c.Card + " (Endless +" + c.Own + " vs Common +" + c.Common + "): under " + c.Rival + " now",
                    before == c.Old && after == c.Want && after < c.RivalScore,
                    "up to 0.13.0 " + F(before) + " -> " + F(after) + " (" + c.Rival + " " + F(c.RivalScore) + ")");
            }
            // CORRECTED in the plan: the ratios differ per stat, so an offer of Endless cards alone can reorder - on purpose
            Check("B1", "two Endless cards may swap places (their own ratios differ): intended", now["Kevlar Plating"] > now["Zone of Action"],
                "Zone of Action 6.07 over Kevlar Plating 4.91 before; now " + F(now["Zone of Action"]) + " vs " + F(now["Kevlar Plating"]));
            // a Common card of the same stat, logged the same day: the formula of the other rarities is the old one
            double wc = (2.91 - 1.0) / 2.0;
            Check("B1", "10-05 18:20:54 the Common Zone of Action (+10%) keeps its 2.91", Math.Round(Synergy.StatCard(Synergy.RarityWeight("Common", double.NaN), wc, false), 2) == 2.91);
        }

        // ---------------------------------------------------------------- B3
        // the 0.13.0 rule, for the detail: closed at first sight with the LIVE class level
        sealed class OldGate
        {
            readonly HashSet<string> _closed = new HashSet<string>(); double _clock = -1;
            public bool Closed(string sv, string a, int rank, int level, double t)
            {
                if (t < _clock - 5) _closed.Clear(); _clock = t;
                if (_closed.Contains(sv + "/" + a)) return true;
                if (rank <= 1 || level >= (rank - 1) * 20) return false;
                _closed.Add(sv + "/" + a); return true;
            }
        }
        static void RankGates()
        {
            // the 10-05 run: the Medic led at class level 37, reached 40 mid-run and 41 by the end; Resuscitation is rank III (40)
            var g = new RankGate(); var old = new OldGate();
            bool a1 = g.Closed("Medic", "Experiment 21", 1, 37, 5), o1 = old.Closed("Medic", "Experiment 21", 1, 37, 5);
            bool a2 = g.Closed("Medic", "Resuscitation", 3, 41, 406), o2 = old.Closed("Medic", "Resuscitation", 3, 41, 406);
            Check("B3", "a rank III ability first looked at after the class level passed 40 mid-run stays closed (the level of the run's start counts)",
                !a1 && !o1 && a2 && g.RunLevel("Medic") == 37, "06:46 at L41: " + (a2 ? "closed" : "OPEN") + " | up to 0.13.0: " + (o2 ? "closed" : "open - 'next Resuscitation' on the row to the end"));
            Check("B3", "... and stays closed for the rest of the run", g.Closed("Medic", "Resuscitation", 3, 41, 590) && g.Closed("Medic", "Resuscitation", 3, 42, 600));
            Check("B3", "rank II at L37: open; no rank (0 / 1): open", !g.Closed("Medic", "Stimpack", 2, 41, 601) && !g.Closed("Medic", "Medical Drone", 0, 41, 602));
            Check("B3", "a recruit joining mid-run is judged by its level at its first sight", !g.Closed("Tank", "Sawblade Drone", 3, 45, 300) && g.RunLevel("Tank") == 45 && g.Closed("Pyro", "Fire Walk", 3, 39, 300));
            bool next = g.Closed("Medic", "Resuscitation", 3, 41, 3);
            Check("B3", "a new run (the clock went back): the level of its start, 41 - rank III open", !next && g.RunLevel("Medic") == 41);
        }

        // ---------------------------------------------------------------- B4
        static void Prefixes()
        {
            string p2 = Synergy.ReasonPrefix(2, 0.99), p3 = Synergy.ReasonPrefix(3, 1.0), p4 = Synergy.ReasonPrefix(4, 0.2), p7 = Synergy.ReasonPrefix(7, 0.5), best = Synergy.ReasonPrefix(1, 0.3);
            Check("B4", "a card under 1 says AVOID in place of its place", p2 == "<b>AVOID</b>   " && p4 == p2 && p7 == p2,
                "'" + p2 + "' | '" + p4 + "' | '" + p7 + "'");
            Check("B4", "the prefix widths: none on the recommended card, 6 for '2ND   ', 8 for 'AVOID   ' - its line gets 48 of the Deck's 56",
                Synergy.ReasonPrefixWidth(1, 0.3) == 0 && Synergy.ReasonPrefixWidth(2, 4.5) == 6 && Synergy.ReasonPrefixWidth(3, 0.5) == 8
                && Wording.RoomBeside(Synergy.ReasonPrefixWidth(3, 0.5)) == 48 && Wording.RoomBeside(Synergy.ReasonPrefixWidth(2, 4.5)) == Wording.Budget && Wording.RoomBeside(0) == Wording.Budget);
            Check("B4", "1.00 and over: the place only (as up to 0.13.0)", p3 == "<b>3RD</b>   " && Synergy.ReasonPrefix(2, 4.5) == "<b>2ND</b>   ", "'" + p3 + "'");
            Check("B4", "the recommended card: no prefix, whatever its score", best == "");
            Console.WriteLine("       e.g. a rescue screen late in a run: 'Tank 0.30' #1 | '" + Regex.Replace(Synergy.ReasonPrefix(2, 0.20), "</?b>", "") + "level-up and cash instead of a recruit' (Liberate 0.20)");
        }

        // ---------------------------------------------------------------- A1
        static void ReasonTexts()
        {
            string line = "focus 2>3 of 4 toward its evolution";
            Check("A1", "the reason line keeps '>' (\"focus 2>3 of 4\" was drawn \"focus 23 of 4\")", Synergy.ReasonText(line) == line, Synergy.ReasonText(line));
            Check("A1", "... and still drops '<' (a rich-text tag would open)", Synergy.ReasonText("a <color=#f00>red</color> word") == "a color=#f00>red/color> word" && Synergy.ReasonText(null) == "");
        }

        // ---------------------------------------------------------------- B5
        static void Rotation()
        {
            string dir = Path.Combine(Path.GetTempPath(), "yazs_bench_log_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            try
            {
                string log = Path.Combine(dir, "companion.log");
                Action<string, string, long> write = (path, first, size) =>
                {
                    using (var w = new StreamWriter(path, false)) { w.WriteLine(first); long n = first.Length + 2; var pad = new string('x', 1000); while (n < size) { w.WriteLine(pad); n += 1002; } }
                };
                Func<string, string> head = path => { using (var r = new StreamReader(path)) return r.ReadLine(); };

                write(log, "OLD", 1024 * 1024);
                string r0 = LogFile.Rotate(log);
                Check("B5", "a 1 MB log is left alone", r0 == null && File.Exists(log) && !File.Exists(log + ".1"));
                write(log, "OLD", LogFile.RotateAt - 2048);
                Check("B5", "... and one just under 4 MB", LogFile.Rotate(log) == null && !File.Exists(log + ".1"));

                write(log, "OLD", 8 * 1024 * 1024 + 4096);      // 8 MB, like the user's 8.4 MB
                long oldLen = new FileInfo(log).Length;
                write(log + ".1", "ONE", 10); write(log + ".2", "TWO", 10); write(log + ".3", "THREE", 10);
                string r1 = LogFile.Rotate(log);
                // what FileListener does next: a fresh file, the session header first
                using (var w = new StreamWriter(log, true)) { w.WriteLine(LogFile.Header(new DateTime(2026, 10, 5, 19, 40, 0))); if (r1 != null) w.WriteLine("19:40:00.000 [Info] [log] " + r1); }
                string first = head(log);
                Check("B5", "an 8 MB log moves to .1, the copies one step up, the oldest .3 dropped",
                    head(log + ".1") == "OLD" && new FileInfo(log + ".1").Length == oldLen && head(log + ".2") == "ONE" && head(log + ".3") == "TWO" && r1 != null && r1.Contains("moved to companion.log.1"),
                    r1);
                Check("B5", "the new log starts with the session header (the overhaul's marks find a session by it)", first == "==== 2026-10-05 19:40:00 session start ====" && first.EndsWith(" session start ====", StringComparison.Ordinal), first);

                // a copy that cannot make room: nothing is overwritten, the log keeps growing
                foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
                write(log, "OLD", 5 * 1024 * 1024); write(log + ".1", "ONE", 10); write(log + ".2", "TWO", 10);
                Directory.CreateDirectory(log + ".3");                               // a folder in the oldest copy's place: no step can move
                string r2 = LogFile.Rotate(log);
                Check("B5", "a step that cannot run overwrites nothing: the log stays and keeps appending",
                    r2 != null && r2.Contains("kept appending") && head(log) == "OLD" && head(log + ".1") == "ONE" && head(log + ".2") == "TWO", r2);
            }
            catch (Exception e) { Check("B5", "the rotation in a test folder", false, e.GetType().Name + " " + e.Message); }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        // ---------------------------------------------------------------- sources: A1 / B4 / B6 and the version
        static void Sources()
        {
            string ranker = Src("Ranker.cs"), badge = Src("Badge.cs"), ui = Src("LoadoutUi.cs"), plugin = Src("Plugin.cs");
            Check("A1", "Ranker writes the ability level step in words ('2 to 3 of 4'), no 'N>M' left",
                ranker != null && !Regex.IsMatch(ranker, "lvl \\+ \">\" \\+ \\(lvl \\+ 1\\)") && Regex.Matches(ranker, "lvl \\+ \" to \" \\+ \\(lvl \\+ 1\\)").Count >= 3);
            // (the plain-words layer may draw its own text in place of Synergy.ReasonText - the '>' must survive either way)
            Check("A1/B4", "Badge draws Synergy.ReasonPrefix, and nothing there strips '>'",
                badge != null && badge.Contains("Synergy.ReasonPrefix(c.Rank, c.Score)") && !badge.Contains("Replace(\">\", \"\")"));
            var note = ui == null ? null : ui.Split('\n').FirstOrDefault(l => l.Contains("\"questnoforced:\""));
            Check("B6", "the quest's badge-objective note only with [Logging] Verbose", note != null && Regex.IsMatch(note, "if \\(Verbose && "), note == null ? "line not found" : note.Trim().Substring(0, Math.Min(60, note.Trim().Length)) + "...");
            var v = plugin == null ? null : Regex.Match(plugin, "VERSION = \"([^\"]+)\"");
            Version ver = null; if (v != null && v.Success) Version.TryParse(v.Groups[1].Value, out ver);
            Check("0.14.0", "Plugin.VERSION is 0.14.0 or later", ver != null && ver >= new Version(0, 14, 0), v != null && v.Success ? v.Groups[1].Value : "?");
            Check("B5", "FileListener rotates before it opens the log and writes the header first",
                plugin != null && plugin.IndexOf("LogFile.Rotate(path)", StringComparison.Ordinal) >= 0 && plugin.IndexOf("LogFile.Rotate(path)", StringComparison.Ordinal) < plugin.IndexOf("new StreamWriter(path, true)", StringComparison.Ordinal)
                && plugin.IndexOf("_w.WriteLine(LogFile.Header(", StringComparison.Ordinal) > plugin.IndexOf("new StreamWriter(path, true)", StringComparison.Ordinal));
        }
    }
}
