// 0.15.0 (C15-01): the bench's verdict and exit code. Every line the bench prints is watched: a line with BAD, FAIL, MISSING,
// WRONG CLASS, NO KIT, DUPLICATE ABILITY or 'unexpected' (what a failing check prints) counts against it, whether or not its
// section counted it. The last line says "bench: all as wanted" or "bench: N line(s) not as wanted", and the exit code follows:
//   0 all as wanted, 1 a failing line that no section counted, 2 the game data missing, 3 a section counted a failure.
// release.ps1 stops on any non-zero exit; the GitHub workflow (.github/workflows/bench.yml) on the last line as well.
//   ItemBench --no-data    the part that needs no game data (data\gamedata.json and data\probe.json stay out of the public
//                          repository): the 10-05 run fixes, the integration review, --check-log, the API contract,
//                          the README / CHANGELOG checks and the release notes cache (DocCases.cs); 0.16.0: the card text
//                          size and the WHY panel (GeometryCases.cs) and the pure parts of the 0.16.0 sections (DataFree).
//   ItemBench --strict     the release gate: the game data and the built DLL must be there (a skip is a failure).
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace YazsCompanion.Bench
{
    sealed class VerdictWriter : TextWriter
    {
        static readonly Regex Failing = new Regex(@"\bBAD\b|\bFAIL\b|\bMISSING\b|WRONG CLASS|NO KIT|DUPLICATE ABILITY|unexpected|not as wanted");
        readonly TextWriter _inner;
        readonly StringBuilder _line = new StringBuilder();
        public int Failed;              // lines that read as a failing check
        public string First;            // the first of them
        public bool Paused;             // the verdict line itself is not counted

        public VerdictWriter(TextWriter inner) { _inner = inner; }
        public override Encoding Encoding { get { return _inner.Encoding; } }

        public override void Write(char value)
        {
            _inner.Write(value);
            if (value == '\n') Flushed();
            else if (value != '\r') _line.Append(value);
        }

        public override void Write(string value)
        {
            if (value == null) return;
            _inner.Write(value);
            foreach (char ch in value)
            {
                if (ch == '\n') Flushed();
                else if (ch != '\r') _line.Append(ch);
            }
        }

        public override void WriteLine(string value) { Write(value); Write(Environment.NewLine); }
        public override void WriteLine() { Write(Environment.NewLine); }
        public override void Flush() { _inner.Flush(); }

        void Flushed()
        {
            if (!Paused && _line.Length > 0 && Failing.IsMatch(_line.ToString()))
            {
                Failed++;
                if (First == null) First = _line.ToString().Trim();
            }
            _line.Clear();
        }
    }

    static class Verdict
    {
        /// <summary>From here on every printed line is watched.</summary>
        public static VerdictWriter Install()
        {
            var w = new VerdictWriter(Console.Out);
            Console.SetOut(w);
            return w;
        }

        /// <summary>The verdict line and the exit code: <paramref name="code"/> as the checks returned it (0, 2, 3), raised to 1
        /// when a failing line was printed that no section counted.</summary>
        public static int Finish(VerdictWriter w, int code)
        {
            code = Code(code, w.Failed);
            w.Paused = true;
            Console.WriteLine();
            Console.WriteLine(code == 0 && w.Failed == 0 ? "bench: all as wanted"
                : "bench: " + Math.Max(w.Failed, 1) + " line" + (w.Failed == 1 ? "" : "s") + " not as wanted (exit " + code + ")" + (w.First != null ? " - first: " + w.First : ""));
            w.Flush();
            return code;
        }

        /// <summary>The cases that need no game data, after the data checks (or alone with --no-data).</summary>
        static int Code(int code, int failedLines) { return code == 0 && failedLines > 0 ? 1 : code; }

        /// <summary>The verdict itself, over made-up output (V1).</summary>
        static int Cases()
        {
            Console.WriteLine("\n=== 0.15.0: the bench's verdict - the lines a failing check prints, the exit code (C15-01)");
            var sw = new StringWriter();
            var w = new VerdictWriter(sw);
            w.WriteLine("  ok   X1 a passing case");
            w.WriteLine("  all as wanted");
            w.Write("  BA"); w.Write("D  X2 a failing case,"); w.Write(" written in pieces\n");
            w.WriteLine("  MISS" + "ING  Tank weapon: 'Shotgun'");
            w.WriteLine("  ok   X3 a quest line 'Tank would fail it' and 'unexpected' in lower case");
            w.Paused = true; w.WriteLine("bench: 3 lines not as wanted"); w.Paused = false;
            bool counted = w.Failed == 3 && w.First == "BA" + "D  X2 a failing case, written in pieces" && sw.ToString().Contains("written in pieces");
            bool codes = Code(0, 0) == 0 && Code(0, 2) == 1 && Code(3, 2) == 3 && Code(2, 0) == 2;
            bool ok = counted && codes;
            // the third failing line above is X3: 'unexpected' is one of the words; a lower-case 'fail' is not. The first one is shown
            // with its word spelt out, or this very line would count against the real run
            string first = (w.First ?? "-").Replace("BA" + "D", "B-A-D");
            Console.WriteLine("  " + (ok ? "ok  " : "BA" + "D ") + " V1 every printed line is read (pieces too, the verdict line left out); the failure words count, a lower-case 'fail it' does not;"
                + " exit 0 / 1 when only printed / the section's own 2 or 3: " + w.Failed + " line(s), first '" + first + "'");
            Console.WriteLine("  " + (ok ? "all as wanted" : "1 BA" + "D"));
            return ok ? 0 : 1;
        }

        public static int Tail(bool strict)
        {
            int bad = Cases();                          // 0.15.0 C15-01: the verdict itself
            bad += LogCheck.Cases();                    // 0.15.0 C15-11: --check-log over made-up logs
            bad += ApiContract.Run(strict);             // 0.15.0 C15-01: the public API frozen (api_v3.txt)
            bad += DocCases.Run();                      // 0.15.0 C15-10: README / CHANGELOG split, the release notes cache
            return bad;
        }

        /// <summary>ItemBench --no-data: every case that runs without data\gamedata.json and data\probe.json.</summary>
        public static int DataFree(bool strict)
        {
            Console.WriteLine("(--no-data: the item scenarios and every check over the game's data skipped; the cases below need none)");
            int bad = RunFixes.Run();
            bad += ReviewCases.Run();
            // 0.16.0 (C16-00): every new pure case reachable here, so the public CI runs it (the full run reaches them from Checks.Run)
            bad += ItemBookCases.RunPure();             // C16-02 / C16-12: the item book's rows, the fallback, the revive need (ItemBookCases.cs)
            bad += RankSmall.Run();                     // C16-05 / C16-08 / C16-17: all data-free (RankSmallCases.cs)
            bad += HeldCases.RunPure();                 // C16-01: the held-item rules' pure part (HeldCases.cs)
            bad += YardBadgeCases.RunDataFree();        // C16-07: the Training Yard's badge steering without gamedata (YardBadgeCases.cs)
            bad += HintCases.RunPure();                 // C16-13 / C16-15: the revive guard, the short hint words (HintCases.cs)
            bad += Loadouts.RunPure();                  // C16-16: the summary row's join forms, the marker digits (LoadoutCases.cs)
            bad += GeometryCases.Run();                 // 0.15.0 C15-05 / C15-06 / C15-12 and 0.16.0 C16-10 / C16-14 / C16-15: all data-free (GeometryCases.cs)
            bad += Tail(strict);
            return bad == 0 ? 0 : 3;
        }
    }
}
