// The run plan shown in the sidebar, compact: two lines per survivor (the weapon line and its next step; the
// ability to feed with its evolution, or the evolution card to wait for, and the next ability), then the tag
// points, who to rescue next and which items are worth a chest slot. Same rules as the card verdicts (Ranker),
// read live. Every line carries a stable key so the sidebar can highlight the lines that changed after a pick,
// a label for the sidebar's label column (the survivor's name on its first line, TAGS / SOS / GRAB) and the group
// it belongs to (one per survivor, plus the run lines) so the sidebar can rule between them.
using System;
using System.Collections.Generic;
using System.Linq;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal sealed class PlanLine
    {
        public string Key;      // stable id: Tank.weapon, Tank.ability, tags, sos, grab
        public string Group;    // survivor name, or "run" for the TAGS / SOS / GRAB lines
        public string Label;    // the label column: the survivor's name (first line of the group), TAGS, SOS, GRAB, or ""
        public string Text;     // rich text of the value column
        public bool Head;       // the first line of a survivor group (the label is the survivor's name)
        public string Row { get { return Label.Length > 0 ? Label + "  " + Text : Text; } }
    }

    internal sealed class Plan
    {
        public const string Gold = Theme.GoldHex, Dim = Theme.DimHex, White = Theme.WhiteHex;
        /// <summary>Glyphs between a state and its next step / between two items; the sidebar swaps in ASCII when
        /// the game's font lacks them.</summary>
        public static string Arrow = " › ", Sep = "  ·  ";
        public string Signature = "";
        public readonly List<PlanLine> Lines = new List<PlanLine>();      // one per row, in display order
        public IEnumerable<string> Rows { get { return Lines.Select(l => l.Row); } }

        static string C(string hex, string s) { return "<color=" + hex + ">" + s + "</color>"; }
        static string N(string name) { return "<nobr>" + name + "</nobr>"; }   // a name never breaks across lines
        static string Nx(string name) { return N(C(Dim, "next  ") + name); }     // "next  Name", kept together
        /// <summary>Two items on one row: the separator travels with the second item when the row wraps.</summary>
        static string Join(List<string> items)
        {
            if (items.Count == 0) return "";
            if (items.Count == 1) return items[0];
            string second = items[1];
            if (second.StartsWith("<nobr>")) second = "<nobr>" + C(Dim, Sep.TrimStart()) + second.Substring(6);
            else second = C(Dim, Sep.TrimStart()) + second;
            return items[0] + " " + second;
        }
        void Add(string group, string key, string label, string text, bool head = false)
        {
            Lines.Add(new PlanLine { Group = group, Key = key, Label = label ?? "", Text = text ?? "", Head = head });
        }
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
            if (v.EvoA != null) evos.Add(N(EvoShort(v.EvoA, baseAbility)));
            if (v.EvoB != null) evos.Add(N(EvoShort(v.EvoB, baseAbility)));
            return string.Join(" / ", evos);
        }

        public static Plan Build(Snapshot s)
        {
            var p = new Plan();
            foreach (var sv in s.Squad)
            {
                try { p.Survivor(sv, s); }
                catch (Exception e) { p.Add(sv.Name, sv.Name + ".weapon", sv.Name.ToUpperInvariant(), C(Dim, e.GetType().Name), true); }
            }
            try { p.TagsLine(s); } catch { }
            try { p.Recruits(s); } catch { }
            try { p.Items(s); } catch { }
            p.Signature = string.Join("|", p.Rows);
            return p;
        }

        void Survivor(Survivor sv, Snapshot s)
        {
            // weapon line: "Pump-Action Shotgun 3/4 › Rocket Launcher" (the next step gold once the weapon is maxed)
            var path = Ranker.WeaponPath(sv);
            var current = path.Where(x => x.Level >= 1).OrderByDescending(x => x.Depth).FirstOrDefault();
            var next = path.FirstOrDefault(x => x.Recommended && x.Available && x.Level < 1 && x.Depth > 0 && (current == null || x.Depth > current.Depth));
            string w;
            if (current == null)
            {
                var first = path.FirstOrDefault(x => x.Depth == 0);
                w = first != null ? C(Gold, "take " + N(G.Name(first.W))) : C(Dim, "no weapon yet");
            }
            else
            {
                bool locked = next == null && path.Any(x => !x.Available && x.Level < 1 && x.Depth > current.Depth);
                int max = G.MaxLevel(current.W);
                w = N(G.Name(current.W) + " " + current.Level + "/" + max);
                if (next != null) w += C(current.Level < max ? Dim : Gold, N(Arrow + G.Name(next.W)));
                else if (current.Level >= max) w += C(Dim, locked ? "  next tier locked" : "  line complete");
            }
            Add(sv.Name, sv.Name + ".weapon", sv.Name.ToUpperInvariant(), w, true);

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
                items.Add(N(G.Name(kv.Key) + " " + kv.Value + "/" + G.MaxLevel(kv.Key)) + C(Gold, Arrow + Evos(v, kv.Key)));
                break;
            }
            var open = owned.Where(kv => kv.Value < G.MaxLevel(kv.Key)).OrderByDescending(kv => kv.Value).ToList();
            if (open.Count > 0)
            {
                var f = open[0];
                var v = Ranker.AbilityScore(f.Key, sv, s);
                // the evolution names only from one level below max: earlier they just make the line wrap
                bool nearMax = f.Value >= G.MaxLevel(f.Key) - 1;
                items.Add(N(G.Name(f.Key) + " " + f.Value + "/" + G.MaxLevel(f.Key)) + (v.EvoOwned && nearMax ? C(Dim, Arrow + Evos(v, f.Key)) : ""));
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
                if (best != null) items.Add(N(C(Dim, "next  ") + G.Name(best)));
            }
            if (items.Count > 0) Add(sv.Name, sv.Name + ".ability", "", Join(items.Take(2).ToList()));
        }

        // the run's damage type tag points (two highest types) and, when it is not the first one, the type to stack
        void TagsLine(Snapshot s)
        {
            string pts = s.Tags.PointsText(2);
            if (pts.Length == 0) return;
            string focus = s.Tags.Focus();
            bool first = focus != null && pts.StartsWith(focus + " ", StringComparison.OrdinalIgnoreCase);
            Add("run", "tags", "TAGS", pts + (focus != null && !first ? C(Dim, Sep + "stack " + focus) : ""));
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
            Add("run", "sos", "SOS", string.Join(", ", top.Select(N)));
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
            Add("run", "grab", "GRAB", string.Join(", ", top.Select(N)));
        }

        public string PlainText() { return ItemRules.RichTag.Replace(string.Join(" | ", Rows), ""); }

        /// <summary>Sample plans for the design preview on the main menu (no game state needed): 0 = two survivors,
        /// 1 = the same after a pick (the weapon maxed, tags up: two changed lines), 2 = a full squad, nine lines.</summary>
        public static Plan Sample(int variant)
        {
            var p = new Plan();
            string a = Arrow;
            if (variant <= 1)
            {
                bool after = variant == 1;
                p.Add("Tank", "Tank.weapon", "TANK", after ? N("Pump-Action Shotgun 4/4") + C(Gold, N(a + "Rocket Launcher")) : N("Pump-Action Shotgun 3/4") + C(Dim, N(a + "Rocket Launcher")), true);
                p.Add("Tank", "Tank.ability", "", Join(new List<string> { N("Sawblade Drone 2/4"), Nx("Minefield") }));
                p.Add("Pyro", "Pyro.weapon", "PYRO", N("Fireaxe 1/4") + C(Dim, N(a + "Blowtorch")), true);
                p.Add("Pyro", "Pyro.ability", "", Join(new List<string> { N("Molotov 3/4") + C(Dim, a + N("Napalm") + " / " + N("Cocktail Party")), Nx("No Pain, No Gain") }));
                p.Add("run", "tags", "TAGS", (after ? "Kinetic 9/10" : "Kinetic 8/10") + ", Slashing 4/10");
                p.Add("run", "sos", "SOS", "SWAT, Huntress");
                p.Add("run", "grab", "GRAB", "Accumulator, Bleeding Edge");
            }
            else
            {
                p.Add("SWAT", "SWAT.weapon", "SWAT", N("Assault Rifle 4/4") + C(Dim, "  line complete"), true);
                p.Add("SWAT", "SWAT.ability", "", N("Automatic Turret 3/4") + C(Dim, a + N("Sentry") + " / " + N("Twin Barrels")));
                p.Add("Engineer", "Engineer.weapon", "ENGINEER", N("Tesla 3/4") + C(Dim, N(a + "Blaster")), true);
                p.Add("Engineer", "Engineer.ability", "", Join(new List<string> { N("Electric Turret 2/4"), Nx("Energy Shield") }));
                p.Add("Huntress", "Huntress.weapon", "HUNTRESS", N("Multishot 1/4") + C(Dim, N(a + "Explosive Arrows")), true);
                p.Add("Huntress", "Huntress.ability", "", Join(new List<string> { N("Arrow Rain 4/4") + C(Gold, a + N("Storm") + " / " + N("Barrage")), Nx("Bear Trap") }));
                p.Add("run", "tags", "TAGS", "Kinetic 31, Electric 22" + C(Dim, Sep + "stack Explosive"));
                p.Add("run", "grab", "GRAB", "Accumulator, Bloody Axe");
            }
            p.Signature = string.Join("|", p.Rows);
            return p;
        }
    }
}
