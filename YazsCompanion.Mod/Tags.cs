// Damage type tags ("hashtags"): Fire, Electric, Chemical, Ice, Explosive, Kinetic, Slashing. Every weapon
// and ability deals one or more of them (PowerupBase.hashtagTypes); tag points come from levels, items
// ("+2 to Fire damage type tag") and the Research Pod event, whose reward screen (UIGameplayHashtagEvent)
// offers cards of "+N points of type X". Points raise the damage of everything dealing that type, and at
// HashtagSystem.NumRequiredForSpecial points the type's special effect switches on.
//
// This file is pure C# (no game types) so the offline bench in tools/ItemBench can run the same rules; the
// profile is filled from the live game in GameState.cs.
using System;
using System.Collections.Generic;
using System.Linq;

namespace YazsCompanion
{
    /// <summary>The squad's damage types (what it deals, with the powerups dealing it) and the run's tag points.</summary>
    internal sealed class TagProfile
    {
        public static readonly string[] Names = { "Fire", "Electric", "Chemical", "Ice", "Explosive", "Kinetic", "Slashing" };

        /// <summary>type -> how much of the squad's damage carries it: a weapon counts 1, an ability 0.5, each scaled by how
        /// far it is levelled (a recruit's level-1 weapon is not the leader's maxed one).</summary>
        public readonly Dictionary<string, double> Weight = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The weight of every damage source, each counted once however many types it deals.</summary>
        public double Total;
        /// <summary>type -> the powerups dealing it, for the reason lines ("Shotgun, Minefield").</summary>
        public readonly Dictionary<string, List<string>> Sources = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        /// <summary>type -> tag points the run has right now.</summary>
        public readonly Dictionary<string, int> Points = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Points needed for a type's special effect (0 = unknown).</summary>
        public int SpecialAt;
        /// <summary>"Auto" = stack what the squad deals most, "Spread" = no stacking, or a type name to force (Doctrine.TagPlan).</summary>
        public string Plan = "Auto";
        public bool Known { get { return Weight.Count > 0; } }

        /// <summary>One damage source with all the types it deals (the live reader): counted once in <see cref="Total"/>.</summary>
        public void Source(string source, double weight, IEnumerable<string> types)
        {
            bool any = false;
            foreach (var t in types) { if (string.IsNullOrEmpty(t)) continue; Add(t, source, weight); any = true; }
            if (any) Total += weight;
        }

        /// <summary>One type of one source (the bench's scenarios; a source dealing two types is counted twice in the total).</summary>
        public void Deals(string type, string source, double weight)
        {
            if (string.IsNullOrEmpty(type)) return;
            Add(type, source, weight); Total += weight;
        }

        void Add(string type, string source, double weight)
        {
            double w; Weight.TryGetValue(type, out w); Weight[type] = w + weight;
            List<string> list; if (!Sources.TryGetValue(type, out list)) Sources[type] = list = new List<string>();
            if (!string.IsNullOrEmpty(source) && !list.Contains(source)) list.Add(source);
        }

        /// <summary>0..1: the share of the squad's damage that carries the type. One fire weapon among three is a third,
        /// not "fully fire"; a solo Pyro is 1.</summary>
        public double Share(string type) { double w; return Total > 0 && Weight.TryGetValue(type ?? "", out w) ? Math.Min(1.0, w / Total) : 0; }
        /// <summary>How well a bonus to the type fits the squad, 0..1 (the share, lifted a little: half the squad's damage
        /// is already a full fit for an item).</summary>
        public double Fit(string type) { return Math.Min(1.0, Share(type) * 1.6); }
        public int PointsOf(string type) { int n; return Points.TryGetValue(type, out n) ? n : 0; }
        public string SourceText(string type)
        {
            List<string> list; if (!Sources.TryGetValue(type, out list) || list.Count == 0) return "";
            return list.Count <= 2 ? string.Join(", ", list) : list[0] + ", " + list[1] + " +" + (list.Count - 2);
        }

        /// <summary>The type with the most points (ties: the one the squad deals more of); null when no points yet.</summary>
        public string Best()
        {
            string best = null; int bestN = 0; double bestW = -1;
            foreach (var kv in Points)
            {
                double w = Fit(kv.Key);
                if (kv.Value > bestN || (kv.Value == bestN && kv.Value > 0 && w > bestW)) { best = kv.Key; bestN = kv.Value; bestW = w; }
            }
            return bestN > 0 ? best : null;
        }

        /// <summary>The type worth stacking: most points among the types the squad deals, else the type it deals most.</summary>
        public string Focus()
        {
            // the player's standing order (mod menu, ADVICE tab): a fixed type, or no stacking at all
            if (string.Equals(Plan, "Spread", StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.IsNullOrEmpty(Plan) && !string.Equals(Plan, "Auto", StringComparison.OrdinalIgnoreCase))
                foreach (var n in Names) if (string.Equals(n, Plan, StringComparison.OrdinalIgnoreCase)) return n;
            string focus = null; double key = double.MinValue;
            foreach (var t in Names)
            {
                double w = Fit(t); if (w <= 0) continue;
                double k = PointsOf(t) * 10 + w;
                if (k > key) { key = k; focus = t; }
            }
            return focus ?? Best();
        }

        /// <summary>"Explosive 6/10, Kinetic 3" (types with points, most first, at most max when max > 0; /N once the special
        /// threshold is known).</summary>
        public string PointsText(int max = 0)
        {
            var parts = Points.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => Array.IndexOf(Names, kv.Key))
                .Take(max > 0 ? max : int.MaxValue)
                .Select(kv => kv.Key + " " + kv.Value + (SpecialAt > 0 && kv.Value < SpecialAt ? "/" + SpecialAt : ""));
            return string.Join(", ", parts);
        }
        /// <summary>0.15.0 (C15-03 d): the PLAN readout's TAGS row - <see cref="PointsText"/> with a met 10-tag effect said: "Kinetic 31 -
        /// effect on", "Kinetic 31, Electric 22 - effects on", "Kinetic 12 - effect on, Electric 4/10" (types with points, most first, at
        /// most <paramref name="max"/> when max &gt; 0; the met ones come first, the count sorts them). Up to 0.14.0 a met type read "Kinetic
        /// 31", the same as a count with no threshold known.</summary>
        public string PlanText(int max = 0)
        {
            var parts = Points.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => Array.IndexOf(Names, kv.Key))
                .Take(max > 0 ? max : int.MaxValue).ToList();
            var met = SpecialAt > 0 ? parts.Where(kv => kv.Value >= SpecialAt).Select(kv => kv.Key + " " + kv.Value).ToList() : new List<string>();
            var rest = parts.Where(kv => SpecialAt <= 0 || kv.Value < SpecialAt).Select(kv => kv.Key + " " + kv.Value + (SpecialAt > 0 ? "/" + SpecialAt : "")).ToList();
            string on = met.Count == 0 ? "" : string.Join(", ", met) + (met.Count == 1 ? " - effect on" : " - effects on");
            return on.Length == 0 ? string.Join(", ", rest) : rest.Count == 0 ? on : on + ", " + string.Join(", ", rest);
        }
        public string DealsText()
        {
            var parts = Weight.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => Array.IndexOf(Names, kv.Key))
                .Select(kv => kv.Key + " (" + SourceText(kv.Key) + ")");
            return string.Join(", ", parts);
        }
        /// <summary>Cheap change key for the sidebar (points only: the weights follow the squad text).</summary>
        public string Key() { return string.Join(",", Points.OrderBy(kv => kv.Key).Select(kv => kv.Key[0].ToString() + kv.Value)); }
    }

    /// <summary>Ranking of a Research Pod reward card: n tag points of one type.</summary>
    internal static class Tags
    {
        /// <summary>0.16.0 (C16-08): a card that reaches the type's 10-tag effect, at a full fit (1.5 x fit; see <see cref="Score"/>).</summary>
        public const double SpecialBonus = 1.5;

        public static double Score(string type, int n, TagProfile p, List<string> why)
        {
            double fit = p == null ? 0 : p.Fit(type);
            int cur = p == null ? 0 : p.PointsOf(type);
            string focus = p == null ? null : p.Focus();
            double score = 1.0 + 0.15 * Math.Max(1, n);
            if (fit > 0)
            {
                score += 2.5 * fit;
                string src = p.SourceText(type);
                // say the share: "some Slashing damage" was the text for half of the squad's damage
                int pct = (int)Math.Round(p.Share(type) * 100);
                why.Add(fit >= 1 ? type + " is what " + (src.Length > 0 ? src + " deals" : "the squad deals")
                    : (pct >= 25 ? type + ": " + pct + "% of the squad's damage" : "little " + type + " damage: " + pct + "% of the squad's") + (src.Length > 0 ? " (" + src + ")" : ""));
            }
            else why.Add(p == null || !p.Known ? "squad damage types unknown" : "nothing on the squad deals " + type);
            if (focus != null && string.Equals(focus, type, StringComparison.OrdinalIgnoreCase) && cur > 0)
            {
                score += 1.0; why.Add("your most stacked tag (" + cur + ")");
            }
            if (p != null && p.SpecialAt > 0 && cur < p.SpecialAt)
            {
                // 0.16.0 (C16-08): a 10-tag effect acts on its own type's status or explosions only (the game checks it in the Burn / Bleed /
                // Frozen / Electrocuted / Injured statuses, the Toxify settings and ExplosionSource.Explode - the callers of
                // HashtagSystem.HasSpecialUnlocked / GetNumType in 1.0.2) - worth as much as the squad deals that type: 1.5 x fit, nothing
                // for a type nobody deals (the card says "but nobody deals X"); up to 0.15.0 the full +1.5 whatever the fit
                if (cur + n >= p.SpecialAt) { score += SpecialBonus * fit; why.Insert(0, "reaches the " + type + " special effect (" + p.SpecialAt + ")"); }
                else if (fit > 0) why.Add((p.SpecialAt - cur - n) + " more to the special after this");
            }
            // the other line a card can cross: Ultra Instinct needs one tag at 30 (said, not scored: whether that item is
            // worth taking depends on what it would wipe)
            if (fit > 0 && cur < 30 && cur + n >= 30) why.Add("takes " + type + " to " + (cur + n) + ", past the 30 Ultra Instinct needs");
            return score;
        }
    }
}
