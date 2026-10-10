// 0.16.0 (C16-02b): the item book's live needs, read once per snapshot - the free item slots, luck, pickup range and movement
// speed (Snapshot.FreeSlots / Luck / Pickup / MoveSpeed / FreeFrom) from the team's statistics - and their proof in the log: the
// '[items] item slot reads: ...' line once a session and the '[items] held: ...' line of every chest offer (Advisor.Offer).
// Called from GameState.Read (after ReadBoosts, before HeldRead.Fill: HeldFacts.FreeSlots reads Snapshot.FreeSlots).
//
// The units are the badge catalogue's statBase (research\badges_1004\catalogue.json), taken as read - no magnitude guessing:
// TeamLuck (EType 8) in points (0 at the start, +4 a badge level), TeamMagnetRange (9) a multiplier (1.0 = 100 %, +0.1 a level),
// TeamMovementSpeedMultiplier (12) in points (100 at the start, +6 a level).
// The free slots are ONE read, the game's own formula: GamePlayer.AddItem adds 1 to TEAM statistic 49 InternalNumEquippedItems
// and then sets team statistic 50 InternalNumFreeItemSlots = (int)(statistic 43 InternalMaxItems - statistic 49) (a stack skips
// both; RemoveItem the same backwards), and Wooden Stick / Empty Chest read statistic 50 from the team statistics. So
// FreeSlots = max(0, stat 43 - stat 49), computed - right also before the run's first AddItem has set statistic 50 - and the
// raw statistic 50 (team and the leader's) is only logged, in the once-a-session line, for the live step to compare. When
// statistic 49 is unreadable the distinct items held stand in for it ('max - held').
// Plausibility gates (a value outside -> NaN, so the book's need leaves the item's worth as it is, and one Warning per session
// per statistic once the run clock has passed 2 s): pickup range 0.5 - 6, movement speed 30 - 400, luck -50 - 1000, free slots
// a whole number 0 - 16. No read outside a run (no squad or no game mode: the menus keep NaN, and no line is written).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YazsCompanion
{
    internal static class ItemStats
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // the plausibility gates (C16-02b)
        internal const double PickupMin = 0.5, PickupMax = 6, SpeedMin = 30, SpeedMax = 400, LuckMin = -50, LuckMax = 1000, SlotsMax = 16;
        // where Snapshot.FreeSlots came from (Snapshot.FreeFrom; '?' = not read)
        internal const string FromMaxEquipped = "max - equipped", FromMaxHeld = "max - held";
        // a gate that fails in the first seconds of a run (the statistics may still be filling in) gives NaN without a Warning
        const float WarnAfterSeconds = 2f;

        static bool _readsSaid;                                                              // the '[items] item slot reads' line, once a session
        static readonly HashSet<string> _gateSaid = new HashSet<string>(StringComparer.Ordinal);   // a failed gate, once a session per statistic

        /// <summary>Read the run's item statistics into <paramref name="s"/> (GameState.Read, inside its own try; a throw leaves NaN and
        /// is said once a session there: '[items] statistics not read: ...').</summary>
        public static void Fill(GameplayMaster master, Snapshot s)
        {
            if (master == null || s == null || s.Squad.Count == 0) return;          // no run: the menus keep NaN / '?'
            bool inMode = false; try { inMode = master.currentGameMode != null; } catch { }
            if (!inMode) return;
            var ts = master.teamStatistics;
            if (ts == null) return;
            bool warn = s.Seconds >= WarnAfterSeconds;

            // luck, pickup range, movement speed: each as read (a throw reaches GameState's once-a-session Warning)
            s.Luck = Gate("luck", ts.GetStatisticValue(PlayerStatistic.EType.TeamLuck), LuckMin, LuckMax, false, warn);
            s.Pickup = Gate("pickup range", ts.GetStatisticValue(PlayerStatistic.EType.TeamMagnetRange), PickupMin, PickupMax, false, warn);
            s.MoveSpeed = Gate("movement speed", ts.GetStatisticValue(PlayerStatistic.EType.TeamMovementSpeedMultiplier), SpeedMin, SpeedMax, false, warn);

            // the free item slots: the game's own formula over the team statistics 43 and 49
            double max = ts.GetStatisticValue(PlayerStatistic.EType.InternalMaxItems);
            double equipped = ts.GetStatisticValue(PlayerStatistic.EType.InternalNumEquippedItems);
            double equippedRaw = equipped;
            string from = FromMaxEquipped;
            if (double.IsNaN(equipped) || equipped < 0) { equipped = DistinctHeld(s); from = FromMaxHeld; }
            s.FreeSlots = Gate("free item slots", Slots(max, equipped), 0, SlotsMax, true, warn);
            s.FreeFrom = double.IsNaN(s.FreeSlots) ? "?" : from;

            // once a session, at the first read with a squad: the raw statistics the live step compares with the HUD's empty slots
            if (!_readsSaid)
            {
                _readsSaid = true;
                double team = double.NaN, leader = double.NaN;
                try { team = ts.GetStatisticValue(PlayerStatistic.EType.InternalNumFreeItemSlots); } catch { }
                try
                {
                    Survivor lead = null;
                    foreach (var sv in s.Squad) if (sv.Leader) { lead = sv; break; }
                    if (lead != null && lead.Player != null) leader = lead.Player.GetStatisticFinalValue(PlayerStatistic.EType.InternalNumFreeItemSlots);
                }
                catch { }
                Plugin.Logger.LogInfo(ReadsLine(team, leader, max, equippedRaw));
            }
        }

        /// <summary>The '[items] held: &lt;names&gt; | free slots N (&lt;from&gt;) | luck L | pickup P% | speed S' line of a chest offer (not behind
        /// [Logging] LogSquad); null = nothing to write.</summary>
        public static string HeldLine(Snapshot s)
        {
            if (s == null) return null;
            var names = new List<string>();
            foreach (var sv in s.Squad)
                foreach (var kv in sv.Items)
                {
                    if (kv.Key == null) continue;
                    string n = G.Name(kv.Key);
                    names.Add(kv.Value > 1 ? n + " x" + kv.Value.ToString(IC) : n);
                }
            return HeldText(names, s.FreeSlots, s.FreeFrom, s.Luck, s.Pickup, s.MoveSpeed);
        }

        // ---------------------------------------------------------------- the pure part (the formats and the gates)
        /// <summary>'[items] held: Homing Pigeon, Black Box | free slots 3 (max - equipped) | luck 10 | pickup 130% | speed 100'; a value
        /// not read is '?'.</summary>
        internal static string HeldText(IList<string> names, double free, string from, double luck, double pickup, double speed)
        {
            var sb = new StringBuilder("[items] held: ");
            sb.Append(names == null || names.Count == 0 ? "none" : string.Join(", ", names));
            sb.Append(" | free slots ").Append(Num(free)).Append(" (").Append(string.IsNullOrEmpty(from) ? "?" : from).Append(')');
            sb.Append(" | luck ").Append(Num(luck));
            sb.Append(" | pickup ").Append(double.IsNaN(pickup) || double.IsInfinity(pickup) ? "?" : Math.Round(pickup * 100, MidpointRounding.AwayFromZero).ToString("0", IC) + "%");
            sb.Append(" | speed ").Append(Num(speed));
            return sb.ToString();
        }

        /// <summary>'[items] item slot reads: team T | leader L | max M - equipped E' - the raw values, '?' where a read threw.</summary>
        internal static string ReadsLine(double team, double leader, double max, double equipped)
        {
            return "[items] item slot reads: team " + Num(team) + " | leader " + Num(leader) + " | max " + Num(max) + " - equipped " + Num(equipped);
        }

        /// <summary>The free slots by the game's formula: max(0, max - equipped); NaN when the maximum is not read.</summary>
        internal static double Slots(double max, double equipped)
        {
            if (double.IsNaN(max) || double.IsInfinity(max) || max < 0 || double.IsNaN(equipped) || double.IsInfinity(equipped)) return double.NaN;
            return Math.Max(0, max - equipped);
        }

        /// <summary>A value inside [min, max] (and whole, when asked) passes as it is; outside: NaN.</summary>
        internal static bool Plausible(double v, double min, double max, bool whole)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || v < min || v > max) return false;
            return !whole || Math.Abs(v - Math.Round(v)) < 1e-3;
        }

        static double Gate(string what, double v, double min, double max, bool whole, bool warn)
        {
            if (Plausible(v, min, max, whole)) return whole ? Math.Round(v) : v;
            if (warn && _gateSaid.Add(what))
                Plugin.Logger.LogWarning("[items] " + what + " read as " + Num(v) + " - outside " + Num(min) + " .. " + Num(max) + (whole ? " (a whole number)" : "")
                    + ", not used: the item book leaves that need out (said once a session)");
            return double.NaN;
        }

        static int DistinctHeld(Snapshot s)
        {
            int n = 0;
            foreach (var sv in s.Squad) foreach (var kv in sv.Items) if (kv.Key != null) n++;
            return n;
        }

        static string Num(double v) { return double.IsNaN(v) || double.IsInfinity(v) ? "?" : v.ToString("0.##", IC); }
    }
}
