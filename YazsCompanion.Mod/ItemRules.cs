// Item scoring rules, ported from lib/engine.js ITEM_KEYWORDS. Class weights use the game's display names.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YazsCompanion
{
    internal sealed class ItemRule
    {
        public string Tag;
        public Regex Re;
        public Dictionary<string, double> Cls;   // class display name -> weight
        public bool Exclusive;
        public double Generic, Ability, Weapon, Survival;

        public ItemRule(string tag, string pattern, string cls = null, bool exclusive = false, double generic = 0, double ability = 0, double weapon = 0, double survival = 0)
        {
            Tag = tag; Re = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            Exclusive = exclusive; Generic = generic; Ability = ability; Weapon = weapon; Survival = survival;
            if (cls != null)
            {
                Cls = new Dictionary<string, double>();
                foreach (var part in cls.Split(','))
                {
                    var kv = part.Trim().Split(':');
                    Cls[kv[0]] = double.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
    }

    internal static class ItemRules
    {
        public static readonly Regex Negative = new Regex(@"(^|[^+\d])-\s?\d+\s?%|halves|halved|cannot|no longer", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static readonly Regex RichTag = new Regex(@"<[^>]+>", RegexOptions.Compiled);

        public static readonly ItemRule[] All =
        {
            new ItemRule("fire", @"\bburn|\bfire\b|flame|hellfire|ignit", "Pyro:1,SWAT:.3,Tank:.3,Engineer:.3,Huntress:.3,Ghost:.3,Mechanic:.3"),
            new ItemRule("electric", @"electri|\bshock|lightning", "Engineer:1,Tank:.3,Ghost:.3,Ranger:.3,Pyro:.3,Mechanic:.3"),
            new ItemRule("freeze/slow", @"freez|frost|frozen|\bslow|movement impaired", "Mechanic:1,Huntress:.8,Medic:.5,Engineer:.3,Ghost:.3,Ranger:.3"),
            new ItemRule("toxic", @"toxi|poison|chemical|acid|plague", "Medic:1,Huntress:.6,Tank:.3,Ghost:.3,Pyro:.3"),
            new ItemRule("explosive", @"explos|grenade|\bbomb|\bmine", "SWAT:1,Tank:1,Pyro:.5,Medic:.4,Engineer:.4"),
            new ItemRule("bleed", @"bleed", "Ghost:1,Huntress:.4,Mechanic:.4"),
            new ItemRule("slashing", @"slashing", "Ghost:1,Mechanic:.5,Pyro:.3"),
            new ItemRule("kinetic", @"kinetic", "SWAT:.8,Tank:.8,Huntress:.6,Ranger:.6,Medic:.4,Engineer:.3"),
            new ItemRule("marked", @"\bmark", "Ranger:1,Huntress:.3", exclusive: true),
            new ItemRule("critical", @"critical", "Huntress:1,Ghost:.6,Ranger:.6,SWAT:.4"),
            new ItemRule("turret", @"turret", "Engineer:1,SWAT:.6,Mechanic:.6", exclusive: true),
            new ItemRule("melee", @"melee", "Ghost:1,Mechanic:.8,Pyro:.5,Tank:.3", exclusive: true),
            new ItemRule("taunt", @"taunt", "Huntress:.6,Tank:.5,Mechanic:.4"),
            new ItemRule("deployable", @"deployable|\btrap", "Mechanic:.8,Engineer:.6,Huntress:.6,Tank:.4"),
            new ItemRule("dodge", @"dodge|parry", "Ghost:.8,SWAT:.3"),
            new ItemRule("armor", @"armor", "Tank:.8", survival: .5),
            new ItemRule("healing", @"heal|healthpak|health pack|\bhp\b|max health|regenerat", "Medic:.8", survival: .8),
            new ItemRule("status effects", @"status effect", "Pyro:.4,Engineer:.4,Medic:.4,Huntress:.4,Mechanic:.4,Ghost:.3"),
            new ItemRule("weapons", @"weapon|reload|clip|bullet|projectile", weapon: .6, generic: .2),
            new ItemRule("abilities", @"abilit|cooldown", ability: .6, generic: .2),
            new ItemRule("economy", @"\bcash|money|\bxp\b|experience|specializ|\bluck|chest", generic: .5),
            new ItemRule("elites/bosses", @"\belite|\bboss", generic: .4),
            new ItemRule("pickups", @"magnet|pickup|pick-up|collect", generic: .3),
            new ItemRule("upgrade quality", @"military training|lockdown|reroll|banish|\bskip|rarity|quality", generic: .3),
            new ItemRule("damage", @"\bdamage", generic: .3),
        };

        static readonly string[] Elemental = { "Pyro", "Engineer", "Medic", "Huntress", "Mechanic" };

        /// <summary>Score an item description against the squad. Mirrors runAdvice() item scoring without run history.</summary>
        public static double Score(string description, IList<string> squad, List<string> why)
        {
            string text = RichTag.Replace(description ?? "", "");
            double score = 0;
            // clauses with a malus ("-10% Fire, Ice, Electric damage") count against the squad instead of for it
            var clauses = new List<string>();
            foreach (var c in Regex.Split(text, @"(?<=[.!])\s+|\s(?=-\s?\d+\s?%)")) if (!string.IsNullOrWhiteSpace(c)) clauses.Add(c);
            var posParts = new List<string>(); var negParts = new List<string>();
            foreach (var c in clauses) (Negative.IsMatch(c) ? negParts : posParts).Add(c);
            string pos = posParts.Count > 0 ? string.Join(" ", posParts) : text;
            string neg = string.Join(" ", negParts);
            bool hasElemental = false; foreach (var c in squad) if (Array.IndexOf(Elemental, c) >= 0) hasElemental = true;

            foreach (var kw in All)
            {
                bool inPos = kw.Re.IsMatch(pos), inNeg = !inPos && kw.Re.IsMatch(neg);
                if (!inPos && !inNeg) continue;
                if (kw.Cls != null)
                {
                    double best = 0; string who = null;
                    foreach (var c in squad) { double v; if (kw.Cls.TryGetValue(c, out v) && v > best) { best = v; who = c; } }
                    if (inNeg) { if (best > 0) { score -= 0.7 * best; why.Add("hurts " + who + ": " + kw.Tag); } continue; }
                    if (best > 0) { score += best; why.Add(who + ": " + kw.Tag); }
                    else if (kw.Exclusive)
                    {
                        var needs = new List<string>(); foreach (var kv in kw.Cls) if (kv.Value >= 1) needs.Add(kv.Key);
                        score -= 1; why.Add("needs " + string.Join("/", needs));
                    }
                    else if (kw.Tag == "status effects" && hasElemental) score += 0.3;
                }
                if (inNeg) continue;
                if (kw.Generic > 0) { score += kw.Generic; if (kw.Cls == null) why.Add(kw.Tag); }
                // ability-heavy / weapon-heavy / survival-need weights need run history; not available in-process yet
            }
            return score;
        }
    }
}
