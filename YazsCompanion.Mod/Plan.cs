// The run plan shown in the sidebar, compact: two lines per survivor (the weapon line and its next step; the
// ability to feed with its evolution, or the evolution card to wait for, and the next ability), then the tag
// points, who to rescue next and which items are worth a chest slot. Same rules as the card verdicts (Ranker),
// read live. Every line carries a stable key so the sidebar can highlight the lines that changed after a pick.
using System;
using System.Collections.Generic;
using System.Linq;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal sealed class PlanLine { public string Key; public string Text; }

    internal sealed class Plan
    {
        public const string Gold = "#F5C752", Dim = "#A6A6A6", White = "#EDEDED";
        /// <summary>Glyphs between a state and its next step / between two items; the sidebar swaps in ASCII when
        /// the game's font lacks them.</summary>
        public static string Arrow = " › ", Sep = "  ·  ";
        public string Signature = "";
        public readonly List<PlanLine> Lines = new List<PlanLine>();      // rich text, one per row
        public IEnumerable<string> Texts { get { return Lines.Select(l => l.Text); } }

        static string C(string hex, string s) { return "<color=" + hex + ">" + s + "</color>"; }
        void Add(string key, string text) { Lines.Add(new PlanLine { Key = key, Text = text }); }
        static string EvoShort(PowerupBase evo, PowerupBase baseAbility)
        {
            string n = G.Name(evo), b = G.Name(baseAbility);
            if (n.StartsWith(b + ":")) n = n.Substring(b.Length + 1).Trim();
            else if (n.StartsWith(b + " ")) n = n.Substring(b.Length + 1).Trim();
            return n;
        }
        static string Evos(Ranker.AbilityVerdict v, PowerupBase baseAbility)
        {
            var evos = new List<string>();
            if (v.EvoA != null) evos.Add(EvoShort(v.EvoA, baseAbility));
            if (v.EvoB != null) evos.Add(EvoShort(v.EvoB, baseAbility));
            return string.Join(" / ", evos);
        }

        public static Plan Build(Snapshot s)
        {
            var p = new Plan();
            foreach (var sv in s.Squad)
            {
                try { p.Survivor(sv, s); }
                catch (Exception e) { p.Add(sv.Name + ".weapon", "<b>" + sv.Name.ToUpperInvariant() + "</b>  " + C(Dim, e.GetType().Name)); }
            }
            try { p.TagsLine(s); } catch { }
            try { p.Recruits(s); } catch { }
            try { p.Items(s); } catch { }
            p.Signature = string.Join("|", p.Texts);
            return p;
        }

        void Survivor(Survivor sv, Snapshot s)
        {
            string name = "<b>" + sv.Name.ToUpperInvariant() + "</b>  ";

            // weapon line: "Pump-Action Shotgun 3/4 › Rocket Launcher" (the next step gold once the weapon is maxed)
            var path = Ranker.WeaponPath(sv);
            var current = path.Where(x => x.Level >= 1).OrderByDescending(x => x.Depth).FirstOrDefault();
            var next = path.FirstOrDefault(x => x.Recommended && x.Available && x.Level < 1 && x.Depth > 0 && (current == null || x.Depth > current.Depth));
            string w;
            if (current == null)
            {
                var first = path.FirstOrDefault(x => x.Depth == 0);
                w = first != null ? C(Gold, "take " + G.Name(first.W)) : C(Dim, "no weapon yet");
            }
            else
            {
                bool locked = next == null && path.Any(x => !x.Available && x.Level < 1 && x.Depth > current.Depth);
                int max = G.MaxLevel(current.W);
                w = G.Name(current.W) + " " + current.Level + "/" + max;
                if (next != null) w += C(current.Level < max ? Dim : Gold, Arrow + G.Name(next.W));
                else if (current.Level >= max) w += C(Dim, locked ? "  next tier locked" : "  line complete");
            }
            Add(sv.Name + ".weapon", name + w);

            // ability line, two items at most: the evolution card to wait for > the ability to feed > the next ability
            var owned = sv.Powerups.Where(kv => kv.Value >= 1 && kv.Key != null && G.IsAbility(kv.Key)).ToList();
            var items = new List<string>();
            foreach (var kv in owned)
            {
                if (kv.Value < G.MaxLevel(kv.Key)) continue;
                bool isEvo = false; try { isEvo = kv.Key.evolutionBaseAbility != null; } catch { }
                if (isEvo) continue;
                var v = Ranker.AbilityScore(kv.Key, sv, s);
                if (!v.EvoOwned) continue;
                if ((v.EvoA != null && sv.Owns(v.EvoA)) || (v.EvoB != null && sv.Owns(v.EvoB))) continue;
                items.Add(G.Name(kv.Key) + " " + kv.Value + "/" + G.MaxLevel(kv.Key) + C(Gold, Arrow + Evos(v, kv.Key)));
                break;
            }
            var open = owned.Where(kv => kv.Value < G.MaxLevel(kv.Key)).OrderByDescending(kv => kv.Value).ToList();
            if (open.Count > 0)
            {
                var f = open[0];
                var v = Ranker.AbilityScore(f.Key, sv, s);
                // the evolution names only from one level below max: earlier they just make the line wrap
                bool nearMax = f.Value >= G.MaxLevel(f.Key) - 1;
                items.Add(G.Name(f.Key) + " " + f.Value + "/" + G.MaxLevel(f.Key) + (v.EvoOwned && nearMax ? C(Dim, Arrow + Evos(v, f.Key)) : ""));
            }
            int evolved = owned.Count(kv => { try { return kv.Key.evolutionBaseAbility != null; } catch { return false; } });
            if (owned.Count - evolved < 4 && sv.Props != null && items.Count < 2)
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
                if (best != null) items.Add(C(Dim, "next  ") + G.Name(best));
            }
            if (items.Count > 0) Add(sv.Name + ".ability", string.Join(C(Dim, Sep), items.Take(2)));
        }

        // the run's damage type tag points (two highest types) and, when it is not the first one, the type to stack
        void TagsLine(Snapshot s)
        {
            string pts = s.Tags.PointsText(2);
            if (pts.Length == 0) return;
            string focus = s.Tags.Focus();
            bool first = focus != null && pts.StartsWith(focus + " ", StringComparison.OrdinalIgnoreCase);
            Add("tags", C(Gold, "TAGS  ") + pts + (focus != null && !first ? C(Dim, Sep + "stack " + focus) : ""));
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
            Add("sos", C(Gold, "SOS  ") + string.Join(", ", top));
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
            var top = ranked.OrderByDescending(kv => kv.Value).Take(2).Select(kv => kv.Key);
            Add("grab", C(Gold, "GRAB  ") + string.Join(", ", top));
        }

        public string PlainText() { return ItemRules.RichTag.Replace(string.Join(" | ", Texts), ""); }
    }
}
