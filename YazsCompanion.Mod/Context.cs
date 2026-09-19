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
        /// <summary>How level-ups are split between the weapon and the abilities for survivors on Auto (a selected build
        /// brings its own style). The human guides disagree on "weapon first", so the default sits between them.</summary>
        public BuildStyle Style = BuildStyle.Balanced;

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
        public double LevelRate;                // level-ups per minute, recent
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

        /// <summary>Level-ups still to come, from the recent pace (a run averages two to four a minute early, fewer later).</summary>
        public double ExpectedLevelUps
        {
            get
            {
                double rate = LevelRate > 0 ? LevelRate : (Seconds > 60 && LevelUps > 0 ? LevelUps / (Seconds / 60.0) : 2.5);
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
}
