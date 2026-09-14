// The run plan shown in the sidebar: per survivor the weapon line and its next step, the ability to
// keep feeding (and what it evolves into), the next ability worth taking; then who to rescue next and
// which items are worth a chest slot. Same rules as the card verdicts (Ranker), read live.
using System;
using System.Collections.Generic;
using System.Linq;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal sealed class Plan
    {
        public const string Gold = "#F5C752", Dim = "#A6A6A6", White = "#EDEDED";
        public string Signature = "";
        public readonly List<string> Lines = new List<string>();      // rich text, one per row

        static string C(string hex, string s) { return "<color=" + hex + ">" + s + "</color>"; }
        static string EvoShort(PowerupBase evo, PowerupBase baseAbility)
        {
            string n = G.Name(evo), b = G.Name(baseAbility);
            if (n.StartsWith(b + ":")) n = n.Substring(b.Length + 1).Trim();
            else if (n.StartsWith(b + " ")) n = n.Substring(b.Length + 1).Trim();
            return n;
        }

        public static Plan Build(Snapshot s)
        {
            var p = new Plan();
            foreach (var sv in s.Squad)
            {
                try { p.Survivor(sv, s); }
                catch (Exception e) { p.Lines.Add(C(Dim, sv.Name + ": " + e.GetType().Name)); }
            }
            try { p.Recruits(s); } catch { }
            try { p.Items(s); } catch { }
            p.Signature = string.Join("|", p.Lines);
            return p;
        }

        void Survivor(Survivor sv, Snapshot s)
        {
            int gained = 0; try { gained = sv.TreeLevel - sv.Player.skillTreeLevelWhenSpawned; } catch { }
            Lines.Add("<b>" + sv.Name.ToUpperInvariant() + "</b>  " + C(Dim, "L" + sv.TreeLevel + (gained > 0 ? "  +" + gained : "")));

            // weapon line
            var path = Ranker.WeaponPath(sv);
            var current = path.Where(x => x.Level >= 1).OrderByDescending(x => x.Depth).FirstOrDefault();
            var next = path.FirstOrDefault(x => x.Recommended && x.Available && x.Level < 1 && x.Depth > 0 && (current == null || x.Depth > current.Depth));
            if (current == null)
            {
                var first = path.FirstOrDefault(x => x.Depth == 0);
                Lines.Add(C(Dim, "no weapon yet") + (first != null ? C(Gold, "  take " + G.Name(first.W)) : ""));
            }
            else
            {
                // a deeper step that exists but is not unlocked in the Training Yard: the line is not complete, just locked
                bool locked = next == null && path.Any(x => !x.Available && x.Level < 1 && x.Depth > current.Depth);
                int max = G.MaxLevel(current.W);
                string line = G.Name(current.W) + " " + current.Level + "/" + max;
                if (current.Level < max) line += C(Dim, "  finish it");
                else if (next != null) line += C(Gold, "  then " + G.Name(next.W));
                else line += C(Dim, locked ? "  next tier locked in the Training Yard" : "  line complete");
                if (current.Level < max && next != null) line += C(Dim, ", then " + G.Name(next.W));
                Lines.Add(line);
            }

            var owned = sv.Powerups.Where(kv => kv.Value >= 1 && kv.Key != null && G.IsAbility(kv.Key)).ToList();

            // a maxed ability whose evolution is unlocked but not taken yet: the card to wait for
            foreach (var kv in owned)
            {
                if (kv.Value < G.MaxLevel(kv.Key)) continue;
                bool isEvo = false; try { isEvo = kv.Key.evolutionBaseAbility != null; } catch { }
                if (isEvo) continue;
                var v = Ranker.AbilityScore(kv.Key, sv, s);
                if (!v.EvoOwned) continue;
                if ((v.EvoA != null && sv.Owns(v.EvoA)) || (v.EvoB != null && sv.Owns(v.EvoB))) continue;
                var evos = new List<string>();
                if (v.EvoA != null) evos.Add(EvoShort(v.EvoA, kv.Key));
                if (v.EvoB != null) evos.Add(EvoShort(v.EvoB, kv.Key));
                Lines.Add(G.Name(kv.Key) + " " + kv.Value + "/" + G.MaxLevel(kv.Key) + C(Gold, "  evolve: " + string.Join(" / ", evos)));
            }

            // the ability to keep feeding
            var open = owned.Where(kv => kv.Value < G.MaxLevel(kv.Key)).OrderByDescending(kv => kv.Value).ToList();
            if (open.Count > 0)
            {
                var f = open[0];
                var v = Ranker.AbilityScore(f.Key, sv, s);
                string line = G.Name(f.Key) + " " + f.Value + "/" + G.MaxLevel(f.Key);
                if (v.EvoOwned)
                {
                    var evos = new List<string>();
                    if (v.EvoA != null) evos.Add(EvoShort(v.EvoA, f.Key));
                    if (v.EvoB != null) evos.Add(EvoShort(v.EvoB, f.Key));
                    line += C(Gold, "  evolves: " + string.Join(" / ", evos));
                }
                else line += C(Dim, "  feed it");
                Lines.Add(line);
            }
            int evolved = owned.Count(kv => { try { return kv.Key.evolutionBaseAbility != null; } catch { return false; } });
            int baseCount = owned.Count - evolved;

            // the next ability worth taking (only ones the Training Yard actually offers)
            if (baseCount < 4 && sv.Props != null)
            {
                PowerupBase best = null; double bestScore = double.MinValue;
                foreach (var a in G.Each(sv.Props.abilityBasePowerups))
                {
                    if (a == null || sv.LevelOf(a) >= 1) continue;
                    SkillTreeUpgradeBase node = null; try { node = a.skillTreeAbilityBoost; } catch { }
                    if (node == null) { try { node = a.skillTreeRequirement; } catch { } }
                    if (node != null && !G.NodeOwned(node)) continue;
                    double sc = Ranker.AbilityScore(a, sv, s).Score;
                    if (sc > bestScore) { bestScore = sc; best = a; }
                }
                if (best != null) Lines.Add(C(Dim, "next ability  ") + G.Name(best));
            }
        }

        void Recruits(Snapshot s)
        {
            if (s.SquadFull || s.Squad.Count == 0) return;
            var ranked = new List<KeyValuePair<string, double>>();
            foreach (CT cls in Enum.GetValues(typeof(CT)))
            {
                if (cls == CT.None || cls == CT.NumCharacters || s.OnSquad(cls)) continue;
                var props = G.PropsOf(cls);
                if (props == null || !G.Unlocked(props)) continue;
                var why = new List<string>();
                double sc = Ranker.RecruitScore(cls, props, s, why);
                ranked.Add(new KeyValuePair<string, double>(G.ClassName(cls), sc));
            }
            if (ranked.Count == 0) return;
            var top = ranked.OrderByDescending(kv => kv.Value).Take(2).Select(kv => kv.Key);
            Lines.Add(C(Gold, "SOS  ") + string.Join(", ", top));
        }

        void Items(Snapshot s)
        {
            PowerupReferences refs = null; try { refs = PowerupReferences.Get; } catch { }
            if (refs == null) return;
            var ranked = new List<KeyValuePair<string, double>>();
            foreach (var it in G.Each(refs.items))
            {
                if (it == null) continue;
                try { if (refs.IsItemDisabled(it)) continue; } catch { }
                if (s.AnyoneHas(it)) continue;
                var why = new List<string>();
                double sc = Ranker.ItemScore(it, s, why);
                if (sc >= 3.0) ranked.Add(new KeyValuePair<string, double>(G.Name(it), sc));
            }
            if (ranked.Count == 0) return;
            var top = ranked.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key);
            Lines.Add(C(Gold, "GRAB  ") + string.Join(", ", top));
        }

        public string PlainText() { return ItemRules.RichTag.Replace(string.Join(" | ", Lines), ""); }
    }
}
