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
        static string Evos(Ranker.AbilityVerdict v, PowerupBase baseAbility, Survivor sv = null, Snapshot s = null)
        {
            var pick = PickEvolution(v, baseAbility, sv, s);
            if (pick != null) return N(EvoShort(pick, baseAbility));
            var evos = new List<string>();
            if (v.EvoA != null) evos.Add(N(EvoShort(v.EvoA, baseAbility)));
            if (v.EvoB != null) evos.Add(N(EvoShort(v.EvoB, baseAbility)));
            return string.Join(" / ", evos);
        }

        // the evolution to take: the build's pick, else the one that clearly fits the squad better (damage types, team
        // passives); null while the two are a toss-up
        static PowerupBase PickEvolution(Ranker.AbilityVerdict v, PowerupBase baseAbility, Survivor sv, Snapshot s)
        {
            if (sv == null || s == null || v.EvoA == null || v.EvoB == null) return null;
            var build = sv.Build; string want = build == null ? null : build.EvolutionOf(G.Name(baseAbility));
            if (want != null)
            {
                if (Ranker.SameName(want, G.Name(v.EvoA))) return v.EvoA;
                if (Ranker.SameName(want, G.Name(v.EvoB))) return v.EvoB;
            }
            var bf = G.Facts(baseAbility);
            double a = Synergy.EvolutionFit(G.Facts(v.EvoA), bf, s.Tags, s.Boosts, s.Ctx, null);
            double b = Synergy.EvolutionFit(G.Facts(v.EvoB), bf, s.Tags, s.Boosts, s.Ctx, null);
            if (Math.Abs(a - b) < 0.25) return null;
            return a > b ? v.EvoA : v.EvoB;
        }

        /// <summary>The compact plan (the default during play): one row per survivor holding only what to pick next - the
        /// weapon level to finish or the next tier once it is maxed, and the ability to evolve, feed or take - and the run
        /// rows cut to their head. The full plan keeps two rows per survivor with the next steps and evolution names.</summary>
        public bool Compact;

        public static Plan Build(Snapshot s, bool compact = false)
        {
            var p = new Plan { Compact = compact };
            foreach (var sv in s.Squad)
            {
                try { if (compact) p.SurvivorCompact(sv, s); else p.Survivor(sv, s); }
                catch (Exception e) { p.Add(compact ? "squad" : sv.Name, sv.Name + (compact ? ".plan" : ".weapon"), sv.Name.ToUpperInvariant(), C(Dim, e.GetType().Name), true); }
            }
            try { p.TagsLine(s); } catch { }
            try { p.Recruits(s); } catch { }
            try { p.Items(s); } catch { }
            p.Signature = string.Join("|", p.Rows);
            return p;
        }

        // one row: "Pump-Action Shotgun 3/4 · Sawblade Drone 2/4"; gold marks a step that is ready now (the next weapon
        // tier once the current one is maxed, an evolution whose base ability is maxed)
        void SurvivorCompact(Survivor sv, Snapshot s)
        {
            var items = new List<string>();
            var path = Ranker.WeaponPath(sv);
            var current = Ranker.CurrentStep(sv, path);
            if (current == null)
            {
                var first = path.FirstOrDefault(x => x.Depth == 0);
                if (first != null) items.Add(C(Gold, N("take " + G.Name(first.W))));
            }
            else
            {
                int max = G.MaxLevel(current.W);
                if (current.Level < max) items.Add(N(G.Name(current.W) + " " + current.Level + "/" + max));
                else
                {
                    var next = path.FirstOrDefault(x => x.Recommended && x.Available && x.Level < 1 && x.Depth > current.Depth);
                    if (next != null) items.Add(C(Gold, N(Arrow.TrimStart() + G.Name(next.W))));
                }
            }

            var owned = sv.Abilities();
            string ability = null;
            foreach (var kv in owned)
            {
                if (kv.Value < G.MaxLevel(kv.Key)) continue;
                var v = Ranker.AbilityScore(kv.Key, sv, s);
                if (!v.EvoOwned || sv.EvolutionOf(kv.Key) != null) continue;
                var pick = PickEvolution(v, kv.Key, sv, s);
                ability = C(Gold, N(pick != null ? "evolve " + G.Name(pick) : "evolve " + G.Name(kv.Key)));
                break;
            }
            // "take each ability once": while abilities are still missing and there is time for them, the next one comes first
            if (ability == null && owned.Count < 4 && sv.Props != null && s.Ctx.Reach(3) >= 0.6)
            {
                var best = NextAbility(sv, s);
                if (best != null) ability = Nx(G.Name(best));
            }
            if (ability == null)
            {
                var focus = Ranker.FocusAbility(sv, s);
                if (focus != null) ability = N(G.Name(focus) + " " + sv.LevelOf(focus) + "/" + G.MaxLevel(focus));
            }
            if (ability != null) items.Add(ability);
            Add("squad", sv.Name + ".plan", sv.Name.ToUpperInvariant(), items.Count > 0 ? Join(items) : C(Dim, "build complete"), true);
        }

        // the best ability this survivor does not own yet and whose tree node is bought
        static PowerupBase NextAbility(Survivor sv, Snapshot s)
        {
            PowerupBase best = null; double bestScore = double.MinValue;
            foreach (var a in G.Each(sv.Props.abilityBasePowerups))
            {
                if (a == null || sv.LevelOf(a) >= 1) continue;
                SkillTreeUpgradeBase node = null; try { node = a.skillTreeAbilityBoost; } catch { }
                if (node == null) { try { node = a.skillTreeRequirement; } catch { } }
                if (node != null && !G.NodeOwned(node)) continue;
                var v = Ranker.AbilityScore(a, sv, s);
                if (v.Skipped) continue;
                double sc = v.Score;
                if (sc > bestScore) { bestScore = sc; best = a; }
            }
            return best;
        }

        void Survivor(Survivor sv, Snapshot s)
        {
            // weapon line: "Pump-Action Shotgun 3/4 › Rocket Launcher" (the next step gold once the weapon is maxed)
            var path = Ranker.WeaponPath(sv);
            var current = Ranker.CurrentStep(sv, path);
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
            var owned = sv.Abilities();
            var items = new List<string>();
            foreach (var kv in owned)
            {
                if (kv.Value < G.MaxLevel(kv.Key)) continue;
                var v = Ranker.AbilityScore(kv.Key, sv, s);
                if (!v.EvoOwned || sv.EvolutionOf(kv.Key) != null) continue;
                items.Add(N(G.Name(kv.Key) + " " + kv.Value + "/" + G.MaxLevel(kv.Key)) + C(Gold, Arrow + Evos(v, kv.Key, sv, s)));
                break;
            }
            var focusAbility = Ranker.FocusAbility(sv, s);
            if (focusAbility != null)
            {
                int fl = sv.LevelOf(focusAbility);
                var v = Ranker.AbilityScore(focusAbility, sv, s);
                // the evolution names only from one level below max: earlier they just make the line wrap
                bool nearMax = fl >= G.MaxLevel(focusAbility) - 1;
                items.Add(N(G.Name(focusAbility) + " " + fl + "/" + G.MaxLevel(focusAbility)) + (v.EvoOwned && nearMax ? C(Dim, Arrow + Evos(v, focusAbility, sv, s)) : ""));
            }
            if (owned.Count < 4 && sv.Props != null && items.Count < 2)
            {
                var best = NextAbility(sv, s);
                if (best != null) items.Add(Nx(G.Name(best)));
            }
            if (items.Count > 0) Add(sv.Name, sv.Name + ".ability", "", Join(items.Take(2).ToList()));
        }

        // the run's damage type tag points (two highest types) and, when it is not the first one, the type to stack
        void TagsLine(Snapshot s)
        {
            string pts = s.Tags.PointsText(Compact ? 1 : 2);
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
            if (s.Ctx.RecruitValue < 0.45) { Add("run", "sos", "SOS", C(Dim, "Liberate") + C(Dim, Sep + s.Ctx.ClockText)); return; }
            var top = ranked.OrderByDescending(kv => kv.Value).Take(2).Select(kv => kv.Key);
            Add("run", "sos", "SOS", string.Join(", ", top.Select(N)));
        }

        void Items(Snapshot s)
        {
            PowerupReferences refs = null; try { refs = PowerupReferences.Get; } catch { }
            if (refs == null) return;
            var ranked = new List<KeyValuePair<string, double>>();
            // the game keeps the list of what can still drop this run (its mode masks, the item pool, what was banished)
            var pool = refs.items; bool live = false;
            try { if (refs.stillAvailableItems != null && refs.stillAvailableItems.Count > 0) { pool = refs.stillAvailableItems; live = true; } } catch { }
            var shared = Ranker.ItemContextOf(s);
            foreach (var it in G.Each(pool))
            {
                if (it == null) continue;
                if (!live)
                {
                    try { if (refs.IsItemDisabled(it)) continue; } catch { }
                    try { if (!it.IsAvailable()) continue; } catch { }
                }
                if (s.AnyoneHas(it)) continue;
                var why = new List<string>();
                double sc = Ranker.ItemScore(it, s, why, shared);
                if (sc >= 3.0) ranked.Add(new KeyValuePair<string, double>(G.Name(it), sc));
            }
            if (ranked.Count == 0) return;
            var top = ranked.OrderByDescending(kv => kv.Value).Take(2).Select(kv => kv.Key);
            Add("run", "grab", "GRAB", string.Join(", ", top.Select(N)));
        }

        public string PlainText() { return ItemRules.RichTag.Replace(string.Join(" | ", Rows), ""); }

        /// <summary>Sample plans for the design preview on the main menu (no game state needed): 0 = two survivors,
        /// 1 = the same after a pick (the weapon maxed, tags up: two changed lines), 2 = a full squad, nine lines.</summary>
        public static Plan Sample(int variant, bool compact = false)
        {
            var p = new Plan { Compact = compact };
            string a = Arrow;
            if (compact)
            {
                if (variant <= 1)
                {
                    bool after = variant == 1;
                    p.Add("squad", "Tank.plan", "TANK", Join(new List<string> { after ? C(Gold, N(a.TrimStart() + "Rocket Launcher")) : N("Pump-Action Shotgun 3/4"), N("Sawblade Drone 2/4") }), true);
                    p.Add("squad", "Pyro.plan", "PYRO", Join(new List<string> { N("Fireaxe 1/4"), N("Molotov 3/4") }), true);
                    p.Add("run", "tags", "TAGS", after ? "Kinetic 9/10" : "Kinetic 8/10");
                    p.Add("run", "sos", "SOS", "SWAT, Huntress");
                    p.Add("run", "grab", "GRAB", "Accumulator, Bleeding Edge");
                }
                else
                {
                    p.Add("squad", "SWAT.plan", "SWAT", N("Automatic Turret 3/4"), true);
                    p.Add("squad", "Engineer.plan", "ENGINEER", Join(new List<string> { N("Tesla 3/4"), N("Electric Turret 2/4") }), true);
                    p.Add("squad", "Huntress.plan", "HUNTRESS", Join(new List<string> { N("Multishot 1/4"), C(Gold, N("evolve Arrow Rain")) }), true);
                    p.Add("run", "tags", "TAGS", "Kinetic 31" + C(Dim, Sep + "stack Explosive"));
                    p.Add("run", "grab", "GRAB", "Accumulator, Bloody Axe");
                }
                p.Signature = string.Join("|", p.Rows);
                return p;
            }
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
