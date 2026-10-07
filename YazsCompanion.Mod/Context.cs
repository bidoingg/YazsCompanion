// Where the run stands: the game mode and difficulty, the clock against the mode's goal, how fast level-ups are
// coming, how the squad is holding up - and the curves the ranking reads from it. The same card is not worth the
// same at 02:00 and at 18:00 of a 20:00 run: what pays back over the rest of the run (XP, luck, pickup range, a
// fresh ability that still needs four levels and an evolution, a recruit who arrives without a weapon) is worth
// the most early and little at the end; what works the moment it is picked (a weapon tier, the level that
// completes an ability, a tag special within reach) keeps its value; survival weighs more as the horde grows.
// In the open-ended modes nothing ever stops paying back.
//
// Pure C# (no game types): filled from the live game in GameState.cs, and by hand in the offline bench.
// Mode facts come from the game's own data: TimeRequiredForSuccess is the goal of a timed mode, and the cards'
// mode masks show what the developers consider pointless where (no max health, armor, regeneration or dodge
// cards in One Hit; no XP, luck or pickup range cards in Extermination) - the game already withholds those, so
// the mode only has to steer what is left: the horizon, boss damage, crowd control, how much survival weighs.
using System;
using System.Collections.Generic;

namespace YazsCompanion
{
    internal enum ModeKind { Timed, Open, Waves, Boss }

    /// <summary>The player's standing orders for the advice, set in the mod menu (ADVICE tab) or the config file.</summary>
    internal sealed class Doctrine
    {
        /// <summary>How strongly the run clock moves the ranking: 0 off, 1 normal, 2 strong.</summary>
        public int Timing = 1;
        /// <summary>Let the game mode and difficulty steer the ranking.</summary>
        public bool ModeAware = true;
        /// <summary>Weight of the live squad synergy (shared damage types, tag thresholds, team passives): 0 off, 1 normal, 2 strong.</summary>
        public int Synergy = 1;
        /// <summary>What the run is for: 0 = win it (cash and XP bonuses fade with the clock), 1 = balanced, 2 = farm
        /// (cash, XP and luck keep their value to the end: the Training Yard is the goal).</summary>
        public int Farming = 0;
        /// <summary>How much survival picks weigh: 0 = glass cannon, 1 = normal, 2 = cautious.</summary>
        public int Caution = 1;
        /// <summary>Damage type tags: "Auto" = stack what the squad deals most, "Spread" = no stacking bonus, or a type name to force.</summary>
        public string TagPlan = "Auto";
        /// <summary>SOS late in a timed run: 0 = by the clock (Liberate once a recruit can no longer be built), 1 = always recruit, 2 = Liberate from the halfway mark.</summary>
        public int Recruit = 0;
        /// <summary>How level-ups are split between the weapon and the abilities for survivors on plain Auto (a selected build
        /// brings its own style; a lent build Auto follows too, unless <see cref="LentStyleMine"/>). The human guides disagree on
        /// "weapon first", so the default sits between them.</summary>
        public BuildStyle Style = BuildStyle.Balanced;
        /// <summary>0.15.0 (C15-07): [Advice] LentBuildStyle = Mine - <see cref="Style"/> also for a build another mod lends while Auto
        /// follows it (false, the default: that build's own style, said on the cards; Builds.StyleFor).</summary>
        public bool LentStyleMine;

        public static Doctrine Current = new Doctrine();
        public double SynergyWeight { get { return Synergy <= 0 ? 0 : Synergy == 1 ? 1.0 : 1.6; } }
    }

    internal sealed class RunContext
    {
        public string Mode = "Normal";          // the game's enum name: Normal, Endless, Hardcore, Extermination, OneHit, Infinite, BossRush
        public int Difficulty = 1;              // 1..5
        public double Seconds;                  // play time so far
        public double Goal;                     // seconds to survive for the win (0 = the mode has none)
        public int Wave, Waves;                 // Extermination: the wave now and how many there are (0 = unknown)
        public int Horde;                       // horde level
        public int LevelUps;                    // level-up screens seen this run
        public double LevelRate;                // level-ups per minute: the mode's measured pace blended with this run's, smoothed (LevelPace; 0 = not known)
        public double Health = 1;               // the squad's health, 0..1
        public Doctrine D = Doctrine.Current;

        /// <summary>Seconds of play the open-ended modes are judged over: far enough that everything still pays back.</summary>
        const double OpenHorizon = 600;

        public ModeKind Kind
        {
            get
            {
                switch (Mode)
                {
                    case "Endless": case "Infinite": return ModeKind.Open;
                    case "Extermination": return ModeKind.Waves;
                    case "BossRush": return ModeKind.Boss;
                    default: return Goal > 0 ? ModeKind.Timed : ModeKind.Open;
                }
            }
        }
        bool Aware { get { return D == null || D.ModeAware; } }
        int TimingLevel { get { return D == null ? 1 : D.Timing; } }

        /// <summary>0 at the start, 1 at the goal. Waves: by wave. Open-ended: stays early (0.15), whatever the clock says.</summary>
        public double Progress
        {
            get
            {
                if (!Aware) return Goal > 0 ? Clamp01(Seconds / Goal) : Clamp01(Seconds / 1200.0);
                switch (Kind)
                {
                    case ModeKind.Open: return 0.15;
                    case ModeKind.Waves: return Waves > 0 ? Clamp01((double)Wave / Waves) : Clamp01(Seconds / 1200.0);
                    default: return Goal > 0 ? Clamp01(Seconds / Goal) : Clamp01(Seconds / 1200.0);
                }
            }
        }

        /// <summary>Seconds of play left to profit from a pick.</summary>
        public double Remaining
        {
            get
            {
                if (Aware && Kind == ModeKind.Open) return OpenHorizon;
                double goal = Goal > 0 ? Goal : 1200.0;
                if (Aware && Kind == ModeKind.Waves && Waves > 0) return Math.Max(0, (1 - Progress) * goal);
                return Math.Max(0, goal - Seconds);
            }
        }

        public string Phase { get { double p = Progress; return p < 0.3 ? "early" : p < 0.7 ? "mid" : "late"; } }

        /// <summary>Level-ups still to come, from the run's pace (LevelPace: the mode's measured pace blended with this run's own, smoothed).
        /// 0.15.0 (C15-04): with no pace handed in, the mode's measured pace blended with the level-ups seen - up to 0.14.0 a flat 2.5 a
        /// minute, whatever the mode (Hardcore runs at 4.7).</summary>
        public double ExpectedLevelUps
        {
            get
            {
                double rate = LevelRate > 0 ? LevelRate
                    : LevelPace.Blend(LevelPace.Prior(Mode), LevelUps > 0 && Seconds > 0 ? LevelUps / Math.Max(LevelPace.MinWindow, Seconds) * 60.0 : 0, LevelUps);
                rate = Math.Max(0.6, Math.Min(5.0, rate));
                return rate * Remaining / 60.0;
            }
        }

        /// <summary>0..1: how likely a plan that needs <paramref name="picks"/> more picks of ONE powerup still completes.
        /// A given powerup shows up in roughly one offer in three, so it takes about three level-ups per pick.</summary>
        public double Reach(int picks)
        {
            if (picks <= 0) return 1;
            if (TimingLevel == 0) return 1;
            double r = Clamp01(0.35 * ExpectedLevelUps / picks);
            return TimingLevel == 2 ? r * r : r;
        }

        /// <summary>Multiplier for what pays back over the rest of the run: XP, luck, pickup range, magnets, chest and
        /// upgrade quality. 1.5 at the start, 1 a third in, 0.3 near the end; never fades in the open-ended modes.</summary>
        public double Economy
        {
            get
            {
                if (TimingLevel == 0) return 1;
                double v = Curve(Progress, 1.5, 1.0, 0.3, 0.15);
                if (TimingLevel == 2) v = Math.Max(0.05, 1 + (v - 1) * 1.5);
                if (D != null && D.Farming == 2) v = Math.Max(v, 1.0);
                else if (D != null && D.Farming == 1) v = Math.Max(v, 0.6);
                return v;
            }
        }

        /// <summary>Multiplier for cash bonuses: cash only buys Training Yard levels, it never helps this run.</summary>
        public double Cash
        {
            get
            {
                int f = D == null ? 0 : D.Farming;
                double left = Kind == ModeKind.Open && Aware ? 1.0 : 1 - Progress;
                return f == 2 ? 1.2 : f == 1 ? 0.4 + 0.5 * left : 0.15 + 0.35 * left;
            }
        }

        /// <summary>Multiplier for max health, armor, regeneration and healing. Grows with the clock, the difficulty and a
        /// squad that is hurting; nothing in One Hit, where a single hit ends the run whatever the health bar says.</summary>
        public double Survival
        {
            get
            {
                int c = D == null ? 1 : D.Caution;
                double v = TimingLevel == 0 ? 1.0 : 0.8 + 0.5 * (Kind == ModeKind.Open && Aware ? Clamp01(Seconds / 1200.0) : Progress);
                if (Aware)
                {
                    if (Mode == "OneHit") return 0;
                    v *= 1 + 0.08 * Math.Max(0, Difficulty - 1);
                    if (Mode == "Hardcore") v *= 1.25;
                }
                if (Health < 0.5) v += 0.4 * (1 - Health * 2);
                return v * (c == 0 ? 0.6 : c == 2 ? 1.4 : 1.0);
            }
        }

        /// <summary>Multiplier for crowd control (freeze, slow, stun, taunt, fear) and for not being where the horde is.</summary>
        public double Control
        {
            get
            {
                if (!Aware) return 1;
                return Mode == "OneHit" ? 1.6 : Mode == "Hardcore" ? 1.2 : 1.0;
            }
        }

        /// <summary>Multiplier for damage against elites and bosses.</summary>
        public double Boss
        {
            get
            {
                if (!Aware) return 1;
                switch (Kind)
                {
                    case ModeKind.Boss: return 2.0;
                    case ModeKind.Open: return 1.3;          // super elites keep coming
                    default: return Progress > 0.6 ? 1.2 : 1.0;
                }
            }
        }

        /// <summary>The value left in recruiting a survivor now, 0..1: a recruit arrives without a weapon and needs
        /// level-ups of their own before they carry their weight.</summary>
        public double RecruitValue
        {
            get
            {
                int r = D == null ? 0 : D.Recruit;
                if (r == 1 || TimingLevel == 0) return 1;
                if (r == 2) return Progress < 0.5 ? 1 : 0.2;
                return Clamp01(ExpectedLevelUps / 12.0);     // about a dozen level-ups make a recruit worth the slot
            }
        }

        public string ClockText
        {
            get
            {
                if (Kind == ModeKind.Waves && Waves > 0) return "wave " + Wave + "/" + Waves;
                if (Kind == ModeKind.Open || Goal <= 0) return "open-ended";
                int left = (int)Math.Round(Remaining);
                return (left / 60) + ":" + (left % 60).ToString("00") + " left";
            }
        }

        public override string ToString()
        {
            return Mode + " d" + Difficulty + ", " + ClockText + ", " + Phase + " (economy x" + Economy.ToString("0.00") + ", survival x" + Survival.ToString("0.00")
                + ", boss x" + Boss.ToString("0.00") + ", ~" + ExpectedLevelUps.ToString("0") + " level-ups to come)";
        }

        static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }

        // piecewise linear through (0, a) (0.35, b) (0.85, c) (1, d)
        static double Curve(double p, double a, double b, double c, double d)
        {
            if (p <= 0.35) return a + (b - a) * (p / 0.35);
            if (p <= 0.85) return b + (c - b) * ((p - 0.35) / 0.5);
            return c + (d - c) * ((p - 0.85) / 0.15);
        }
    }

    /// <summary>0.15.0 (C15-04): the run's level-up pace, which '~N level-ups to come' (RunContext.ExpectedLevelUps) and every score
    /// scaled by the clock read. Up to 0.14.0 it was the level-ups of the last three minutes per minute, nothing before 0:45 (a flat
    /// 2.5 then) - in the user's 10-06 run '~N level-ups to come' read 49, 48, 58, 65, 76 in the first two minutes and 66 - 77 at 3:00,
    /// a 55 % swing that moved every clock-scaled card with it. Now the mode's measured pace (Prior) counts as <see cref="PriorWeight"/>
    /// level-ups seen, blended with the windowed rate by the level-ups this run has seen (rate = (prior * k + observed * n) / (k + n)),
    /// and the blend is smoothed at every level-up with a half-life of <see cref="HalfLife"/> seconds of play, starting from the prior:
    /// on the same run 54 - 58 from 0:59 to 3:00. Fed at the level-up screens only (GameState's Pace), so a replay of the level-up
    /// clocks gives exactly what the live run gave. Pure: the bench replays the logged runs (RankCases.cs, N1 - N4).</summary>
    internal sealed class LevelPace
    {
        public const int PriorWeight = 4;           // k: the mode's measured pace counts as four level-ups seen
        public const double HalfLife = 60;          // seconds of play: the smoothing's half-life
        public const double Window = 180;           // seconds: the observed rate's window, the last three minutes (as up to 0.14.0)
        public const double MinWindow = 20;         // seconds: a level-up in the first seconds of a run is not read as 3 a second

        /// <summary>Level-ups per minute a run of the mode brings after its first minute, measured over the user's runs of three minutes
        /// or more in companion.log.1 + companion.log (2026-09-15 to 10-06; the 30-second scripted series runs left out - they only see the
        /// fast start): Normal 2.9 (14 runs, 478 level-ups in 161.8 min after 1:00 = 2.95; the 7 runs of 15 min or more: 373 in 129.9 min
        /// = 2.87 - e.g. 10-04 20:14 60 in 19.9 min, 10-03 12:02 60 in 19.5, 09-19 16:53 52 in 19.8); Hardcore 4.7 (4 runs: 10-04 09:36,
        /// 10-05 18:00, 10-06 20:33, 09-22 23:13 - 141 in 29.9 min = 4.72); Endless 3.2 (4 runs: 09-19 13:36, 09-20 19:34, 09-22 23:29,
        /// 10-04 16:04 - 206 in 65.4 min = 3.15, 3.26 over whole runs); BossRush 4.6 (one run, 10-04 19:52: 44 in 9.5 min = 4.62). A mode
        /// with no measured run (Extermination, OneHit, Infinite): Normal's. mod\tools\advice_audit.py prints the pace per mode; its table
        /// counts every run, the scripted ones too (Normal 4.31 a minute over 335 runs).</summary>
        public static double Prior(string mode)
        {
            switch (mode)
            {
                case "Hardcore": return 4.7;
                case "Endless": return 3.2;
                case "BossRush": return 4.6;
                default: return 2.9;
            }
        }

        /// <summary>The prior counted as <see cref="PriorWeight"/> level-ups seen, the observed rate as <paramref name="n"/>.</summary>
        public static double Blend(double prior, double observed, int n)
        {
            if (n <= 0) return prior;
            return (prior * PriorWeight + observed * n) / (PriorWeight + n);
        }

        readonly List<double> _at = new List<double>();        // the run clock of every level-up screen seen this run
        string _mode;                                           // the mode the smoothing below was replayed under (null: start over)
        int _done;                                              // level-ups already smoothed in
        double _smooth, _last;                                  // the smoothed rate, the clock of the last level-up smoothed in

        public int Count { get { return _at.Count; } }
        /// <summary>A new run (the clock went back).</summary>
        public void Clear() { _at.Clear(); _mode = null; }
        /// <summary>A level-up screen at <paramref name="seconds"/> of play (a reroll or a replaced offer is the same level-up: not added).</summary>
        public void Add(double seconds) { _at.Add(seconds); }

        /// <summary>The level-ups per minute in the window up to the <paramref name="i"/>-th level-up, that one counted.</summary>
        public double Observed(int i)
        {
            if (i < 0 || i >= _at.Count) return 0;
            double t = _at[i], w = Math.Max(MinWindow, Math.Min(Window, t));
            int n = 0;
            for (int j = 0; j <= i; j++) if (_at[j] >= t - w) n++;
            return n / (w / 60.0);
        }

        /// <summary>Level-ups per minute for the rest of the run under <paramref name="mode"/>: the prior before the first level-up, then
        /// the blend smoothed at every level-up. Never 0.</summary>
        public double Rate(string mode)
        {
            mode = mode ?? "";
            double prior = Prior(mode);
            if (_mode != mode) { _mode = mode; _done = 0; _smooth = prior; _last = 0; }
            for (; _done < _at.Count; _done++)
            {
                double t = _at[_done];
                double blend = Blend(prior, Observed(_done), _done + 1);
                double dt = Math.Max(0, t - _last);
                _smooth += (1 - Math.Pow(0.5, dt / HalfLife)) * (blend - _smooth);
                _last = Math.Max(_last, t);
            }
            return _smooth;
        }
    }
}
