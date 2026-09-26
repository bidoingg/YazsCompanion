// Live game state, read straight from the game's objects (no save file, no OCR).
// Note: the game keeps every survivor's powerups on the LEADER's player object; recruits' lists stay
// empty. Powerups are therefore regrouped here by the class they belong to.
using System;
using System.Collections.Generic;
using System.Text;
using CT = GamePlayer.CharacterType;

namespace YazsCompanion
{
    internal sealed class Survivor
    {
        public CT Type;
        public string Name;              // display name (Ninja -> Ghost, as the game shows it)
        public GamePlayer Player;
        public ClassProperties Props;
        public bool Leader;
        public int TreeLevel;            // Training Yard level of this survivor
        public PowerupBase Weapon;       // current weapon powerup (level 0 until a recruit picks it)
        public Snapshot Snap;
        public readonly List<KeyValuePair<PowerupBase, int>> Powerups = new List<KeyValuePair<PowerupBase, int>>();   // this class's powerups, wherever the game stores them
        public readonly List<KeyValuePair<ItemBase, int>> Items = new List<KeyValuePair<ItemBase, int>>();

        public int LevelOf(PowerupBase p)
        {
            if (p == null) return 0;
            foreach (var kv in Powerups) if (G.Same(kv.Key, p)) return kv.Value;
            if (Snap != null) foreach (var sv in Snap.Squad)
            {
                if (sv.Player == null) continue;
                try { int l = sv.Player.GetPowerupLevel(p); if (l > 0) return l; } catch { }
            }
            return 0;
        }
        public bool Owns(PowerupBase p) { return LevelOf(p) >= 1; }

        /// <summary>The build the advice follows for this survivor (mod menu); null = Auto.</summary>
        public Build Build { get { return Builds.For(Name); } }

        /// <summary>Owned base abilities with their levels. An evolution is its own level-1 powerup next to the maxed base
        /// ability, so it is left out here: it is not a fifth ability.</summary>
        public List<KeyValuePair<PowerupBase, int>> Abilities()
        {
            var list = new List<KeyValuePair<PowerupBase, int>>();
            foreach (var kv in Powerups) if (kv.Value >= 1 && kv.Key != null && G.IsAbility(kv.Key) && !G.IsEvolution(kv.Key)) list.Add(kv);
            return list;
        }
        /// <summary>The evolution this survivor took of a base ability, or null.</summary>
        public PowerupBase EvolutionOf(PowerupBase ability)
        {
            PowerupBase a = null, b = null; try { a = ability.abilityEvolutionA; b = ability.abilityEvolutionB; } catch { }
            if (a != null && Owns(a)) return a;
            if (b != null && Owns(b)) return b;
            return null;
        }
        public bool Has(ItemBase it) { foreach (var kv in Items) if (G.Same(kv.Key, it)) return true; return false; }
    }

    internal sealed class Snapshot
    {
        public readonly List<Survivor> Squad = new List<Survivor>();
        public float Seconds;
        public string Clock = "?";
        public string Mode = "?";
        public int Horde;
        public readonly TagProfile Tags = new TagProfile();   // what the squad deals + the run's damage type tag points
        public RunContext Ctx = new RunContext();             // mode, clock, pace: what the timing curves read
        public readonly List<TeamBoost> Boosts = new List<TeamBoost>();   // owned team passives whose owner is on the squad
        public int ItemsHeld;
        public bool SquadFull { get { return Squad.Count >= 3; } }
        public Survivor Find(CT t) { foreach (var s in Squad) if (s.Type == t) return s; return null; }
        public bool OnSquad(CT t) { return Find(t) != null; }
        public bool AnyoneHas(ItemBase it) { foreach (var s in Squad) if (s.Has(it)) return true; return false; }

        // the quest manager while a quest is active, else null - asked once per snapshot, not once per scored item
        GameQuestManager _quests; bool _questsRead;
        public GameQuestManager ActiveQuests
        {
            get
            {
                if (!_questsRead)
                {
                    _questsRead = true;
                    try { var qm = GameQuestManager.Get; if (qm != null && qm.ActiveQuest != null) _quests = qm; } catch { }
                }
                return _quests;
            }
        }

        public string SquadText()
        {
            var sb = new StringBuilder();
            foreach (var s in Squad)
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(s.Name).Append(s.Leader ? "*" : "").Append(" L").Append(s.TreeLevel);
                if (s.Weapon != null && s.LevelOf(s.Weapon) >= 1) sb.Append(' ').Append(G.Name(s.Weapon)).Append(':').Append(s.LevelOf(s.Weapon));
                else sb.Append(" (no weapon yet)");
                foreach (var kv in s.Powerups)
                {
                    bool ability = false; try { ability = kv.Key.isAbility; } catch { }
                    if (ability) sb.Append(' ').Append(G.Name(kv.Key)).Append(':').Append(kv.Value);
                }
                if (s.Items.Count > 0) { sb.Append(" ["); bool first = true; foreach (var kv in s.Items) { if (!first) sb.Append(','); first = false; sb.Append(G.Name(kv.Key)); if (kv.Value > 1) sb.Append('x').Append(kv.Value); } sb.Append(']'); }
            }
            return sb.ToString();
        }
    }

    /// <summary>Helpers over the IL2CPP objects.</summary>
    internal static class G
    {
        public static string ClassName(CT t) { return t == CT.Ninja ? "Ghost" : t.ToString(); }

        // ---- what one computation may remember ----
        // Every name is a call into the game that allocates a string on ITS heap and copies it over, and one ranking or
        // plan build asks for the same few dozen names thousands of times (Same() falls back to comparing names, LevelOf
        // walks the powerups with it). Inside a Cache() scope - one offer, one snapshot + plan - names and powerup facts
        // are remembered per object; the scope's end forgets them, so nothing outlives the objects it was read from.
        static int _scope;
        static readonly Dictionary<IntPtr, string> _names = new Dictionary<IntPtr, string>();
        static readonly Dictionary<IntPtr, string> _assets = new Dictionary<IntPtr, string>();
        static readonly Dictionary<IntPtr, PowerFacts> _facts = new Dictionary<IntPtr, PowerFacts>();

        internal struct Scope : IDisposable
        {
            bool _live;
            internal static Scope Enter() { _scope++; return new Scope { _live = true }; }
            public void Dispose()
            {
                if (!_live) return;
                _live = false;
                if (--_scope <= 0) { _scope = 0; _names.Clear(); _assets.Clear(); _facts.Clear(); }
            }
        }
        /// <summary>using (G.Cache()) { read, rank, plan }: names and powerup facts are fetched once per object inside.</summary>
        public static Scope Cache() { return Scope.Enter(); }

        // the object's address as the cache key; zero (= do not cache) when the wrapper has lost its object
        static IntPtr Ptr(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase o) { try { return o.Pointer; } catch { return IntPtr.Zero; } }

        public static string Name(PowerupBase p)
        {
            if (p == null) return "?";
            string n; IntPtr key = _scope > 0 ? Ptr(p) : IntPtr.Zero;
            if (key != IntPtr.Zero && _names.TryGetValue(key, out n)) return n;
            n = null;
            try { n = p.EnglishName; } catch { }
            if (string.IsNullOrEmpty(n)) { try { n = p.name; } catch { return "?"; } }
            if (key != IntPtr.Zero && n != null) _names[key] = n;
            return n;
        }
        public static string Name(ItemBase it)
        {
            if (it == null) return "?";
            string n; IntPtr key = _scope > 0 ? Ptr(it) : IntPtr.Zero;
            if (key != IntPtr.Zero && _names.TryGetValue(key, out n)) return n;
            n = null;
            try { n = it.EnglishName; } catch { }
            if (string.IsNullOrEmpty(n)) { try { n = it.name; } catch { return "?"; } }
            if (key != IntPtr.Zero && n != null) _names[key] = n;
            return n;
        }
        public static string Asset(UnityEngine.Object o)
        {
            try
            {
                if (o == null) return "";
                string n; IntPtr key = _scope > 0 ? Ptr(o) : IntPtr.Zero;
                if (key != IntPtr.Zero && _assets.TryGetValue(key, out n)) return n;
                n = o.name ?? "";
                if (key != IntPtr.Zero) _assets[key] = n;
                return n;
            }
            catch { return ""; }
        }

        public static bool Same(UnityEngine.Object a, UnityEngine.Object b)
        {
            if (a == null || b == null) return false;
            try { if (a.Pointer == b.Pointer) return true; } catch { }
            if (_scope > 0) { string na = Asset(a); return na.Length > 0 && na == Asset(b); }
            try { return a.name == b.name; } catch { return false; }
        }

        public static IEnumerable<T> Each<T>(Il2CppSystem.Collections.Generic.List<T> list)
        {
            if (list == null) yield break;
            int n; try { n = list.Count; } catch { yield break; }
            for (int i = 0; i < n; i++) { T v; try { v = list[i]; } catch { continue; } yield return v; }
        }

        // ---- skill tree nodes (Training Yard) ----
        public static int NodeLevel(SkillTreeUpgradeBase node)
        {
            if (node == null) return 0;
            try { var rt = node.GetRuntimeInstance(); if (rt != null) return rt.GetCurrentLevel(); } catch { }
            try { return node.GetCurrentLevel(); } catch { }
            try { return node.levelCurrent; } catch { return 0; }
        }
        public static bool NodeOwned(SkillTreeUpgradeBase node) { return NodeLevel(node) >= 1; }
        public static int NodeMax(SkillTreeUpgradeBase node)
        {
            if (node == null) return 0;
            try { return node.GetMaxLevel(); } catch { }
            try { return node.levelMax; } catch { return 0; }
        }
        public static string NodeDesc(SkillTreeUpgradeBase node)
        {
            if (node == null) return "";
            try { var rt = node.GetRuntimeInstance(); if (rt != null) return rt.GetDescription() ?? ""; } catch { }
            try { return node.GetDescription() ?? ""; } catch { return ""; }
        }
        // The Training Yard's node list (245 nodes) is asset data: which nodes exist, whose they are and which of them are
        // team passives never changes, only their levels do (always read live). Walking the list costs three calls into
        // the game per node, and one plan build used to walk it once per recruitable class plus once per snapshot - so
        // the list and its per-class split are kept until the run ends (ForgetRun) or the game swaps the list.
        static List<SkillTreeUpgradeBase> _allNodes;
        static IntPtr _nodeList; static int _nodeCount;
        static readonly Dictionary<CT, List<SkillTreeUpgradeBase>> _nodesOf = new Dictionary<CT, List<SkillTreeUpgradeBase>>();
        static List<TaggedNode> _taggedNodes;

        sealed class TaggedNode { public SkillTreeUpgradeBase Node; public CT Class; public string Tag, NotTag, Name; public bool AbilitiesOnly; }

        /// <summary>The HUD is gone (a run ended) or the mod menu closed: drop what was kept for the run.</summary>
        public static void ForgetRun() { _allNodes = null; _nodesOf.Clear(); _taggedNodes = null; _nodeList = IntPtr.Zero; _nodeCount = 0; _props.Clear(); Names.Forget(); }

        static List<SkillTreeUpgradeBase> NodeList()
        {
            SkillTreeUpgrades tree = null;
            try { tree = SkillTreeUpgrades.Get; } catch { }
            if (tree == null) return null;
            Il2CppSystem.Collections.Generic.List<SkillTreeUpgradeBase> list = null; int count = -1;
            try { list = tree.skillTreeUpgrades; if (list != null) count = list.Count; } catch { }
            if (list == null) return null;
            if (_allNodes != null && list.Pointer == _nodeList && count == _nodeCount) return _allNodes;
            var all = new List<SkillTreeUpgradeBase>(Math.Max(0, count));
            foreach (var n in Each(list)) if (n != null) all.Add(n);
            _allNodes = all; _nodeList = list.Pointer; _nodeCount = count; _nodesOf.Clear(); _taggedNodes = null;
            return all;
        }

        public static IEnumerable<SkillTreeUpgradeBase> AllNodes() { return (IEnumerable<SkillTreeUpgradeBase>)NodeList() ?? new SkillTreeUpgradeBase[0]; }

        public static IEnumerable<SkillTreeUpgradeBase> NodesOf(CT t)
        {
            var all = NodeList();
            if (all == null) return new SkillTreeUpgradeBase[0];
            List<SkillTreeUpgradeBase> mine;
            if (_nodesOf.TryGetValue(t, out mine)) return mine;
            mine = new List<SkillTreeUpgradeBase>();
            foreach (var n in all)
            {
                ClassProperties cp = null; try { cp = n.targetClassProperties; } catch { }
                try { if (cp != null && cp.characterType == t) mine.Add(n); } catch { }
            }
            _nodesOf[t] = mine;
            return mine;
        }

        static readonly Dictionary<CT, ClassProperties> _props = new Dictionary<CT, ClassProperties>();
        public static ClassProperties PropsOf(CT t)
        {
            ClassProperties cached;
            if (_props.TryGetValue(t, out cached) && cached != null) return cached;
            ClassProperties found = null;
            try
            {
                var master = GameplayMaster.s_instance;
                if (master != null) foreach (var cp in Each(master.availableClasses)) if (cp != null && cp.characterType == t) { found = cp; break; }
            }
            catch { }
            if (found == null) foreach (var n in NodesOf(t)) { try { found = n.targetClassProperties; } catch { } if (found != null) break; }
            if (found != null) _props[t] = found;
            return found;
        }
        public static int TreeLevel(ClassProperties cp)
        {
            try { var v = cp.skillTreeCurrentLevel; return v == null ? 0 : (int)v.value; } catch { return 0; }
        }
        public static int MaxLevel(PowerupBase p) { try { int m = p.MaxLevel; return m > 0 ? m : 4; } catch { return 4; } }
        public static bool Unlocked(ClassProperties cp) { try { return cp.isCharacterUnlocked || cp.isCharacterAlwaysUnlocked; } catch { return false; } }
        public static bool IsAbility(PowerupBase p) { try { return p.isAbility && p.TryCast<BasicLevelPowerup>() == null; } catch { return false; } }
        public static bool IsEvolution(PowerupBase p) { try { return p != null && p.evolutionBaseAbility != null; } catch { return false; } }

        /// <summary>The powerup in the game's own terms (damage types, powerup tags), for the pure synergy rules.</summary>
        public static PowerFacts Facts(PowerupBase p)
        {
            if (p == null) return new PowerFacts();
            PowerFacts f; IntPtr key = _scope > 0 ? Ptr(p) : IntPtr.Zero;
            if (key != IntPtr.Zero && _facts.TryGetValue(key, out f)) return f;      // nobody writes to the facts they are handed
            f = ReadFacts(p);
            if (key != IntPtr.Zero) _facts[key] = f;
            return f;
        }

        static PowerFacts ReadFacts(PowerupBase p)
        {
            var f = new PowerFacts();
            f.Name = Name(p);
            try { f.IsWeapon = p.TryCast<WeaponUpgradePowerup>() != null; } catch { }
            f.IsAbility = IsAbility(p);
            try { f.Healing = p.isHealingAbility; } catch { }
            try { foreach (var t in Each(p.hashtagTypes)) { string n = TagName(t); if (n != null && !f.Damage.Contains(n)) f.Damage.Add(n); } } catch { }
            try { foreach (var t in Each(p.powerupTags)) f.Tags.Add(t.ToString()); } catch { }
            return f;
        }

        // ---- items ----
        /// <summary>What the item rules read of an item: its English text, its highlighted statistics, whether it heals,
        /// how many can be carried. Asset data, so it is read once per item for the session (keyed by the item's id and
        /// checked against its name) - the GRAB row alone scores every item that can still drop, after every pick.</summary>
        internal sealed class ItemFacts { public string Name, Desc; public List<string> Stats; public bool Healing; public int MaxCarry = 1; }
        static readonly Dictionary<int, ItemFacts> _itemFacts = new Dictionary<int, ItemFacts>();

        public static ItemFacts FactsOf(ItemBase it)
        {
            string name = Name(it);
            int id = 0; bool keyed = false;
            try { id = it.itemBaseId; keyed = true; } catch { }
            ItemFacts f;
            if (keyed && _itemFacts.TryGetValue(id, out f) && f.Name == name) return f;
            f = new ItemFacts { Name = name, Stats = new List<string>() };
            bool english = false;
            try { f.Desc = it.EnglishDescription; english = !string.IsNullOrEmpty(f.Desc); } catch { }
            if (!english) { try { f.Desc = it.GetDescriptionText(); } catch { } }
            if (f.Desc == null) f.Desc = "";
            try { foreach (var st in Each(it.highlightedStatistics)) if (st != null) f.Stats.Add(st.statisticType.ToString()); } catch { }
            try { f.Healing = it.isHealingItem; } catch { }
            try { f.MaxCarry = it.numMaxCanCarry; } catch { }
            if (keyed && english) _itemFacts[id] = f;      // the localized fallback text follows the language: not kept
            return f;
        }

        // ---- damage type tags ----
        static readonly HashtagSystem.EHashtagType[] TagTypes =
        {
            HashtagSystem.EHashtagType.Fire, HashtagSystem.EHashtagType.Electric, HashtagSystem.EHashtagType.Toxic, HashtagSystem.EHashtagType.Ice,
            HashtagSystem.EHashtagType.Explosive, HashtagSystem.EHashtagType.Kinetic, HashtagSystem.EHashtagType.Slashing
        };
        /// <summary>The game's display name of a tag type (Toxic shows as Chemical); null for None.</summary>
        public static string TagName(HashtagSystem.EHashtagType t)
        {
            switch (t)
            {
                case HashtagSystem.EHashtagType.Fire: return "Fire";
                case HashtagSystem.EHashtagType.Electric: return "Electric";
                case HashtagSystem.EHashtagType.Toxic: return "Chemical";
                case HashtagSystem.EHashtagType.Ice: return "Ice";
                case HashtagSystem.EHashtagType.Explosive: return "Explosive";
                case HashtagSystem.EHashtagType.Kinetic: return "Kinetic";
                case HashtagSystem.EHashtagType.Slashing: return "Slashing";
                default: return null;
            }
        }

        /// <summary>What the squad deals (the current weapon of each survivor counts 1 per type, each owned ability 0.5)
        /// and the run's tag points per type from the game's HashtagSystem (plus the special-effect threshold).</summary>
        static void ReadTags(GameplayMaster master, Snapshot s)
        {
            var p = s.Tags;
            try { p.Plan = Doctrine.Current.TagPlan ?? "Auto"; } catch { }
            foreach (var sv in s.Squad)
            {
                if (sv.Weapon != null && sv.LevelOf(sv.Weapon) >= 1) Deals(p, sv.Weapon, 1.0 * Levelled(sv.LevelOf(sv.Weapon), MaxLevel(sv.Weapon)));
                foreach (var kv in sv.Powerups)
                {
                    if (kv.Value < 1 || kv.Key == null || !IsAbility(kv.Key)) continue;
                    if (!IsEvolution(kv.Key) && sv.EvolutionOf(kv.Key) != null) continue;      // the evolution stands in for its maxed base ability
                    Deals(p, kv.Key, 0.5 * (IsEvolution(kv.Key) ? 1.15 : Levelled(kv.Value, MaxLevel(kv.Key))));
                }
            }
            try
            {
                var hs = master.hashtagSystem;
                if (hs != null)
                {
                    try { p.SpecialAt = HashtagSystem.NumRequiredForSpecial; } catch { }
                    foreach (var t in TagTypes)
                    {
                        int n = 0; try { n = hs.GetNumType(t); } catch { continue; }
                        if (n > 0) p.Points[TagName(t)] = n;
                    }
                }
            }
            catch { }
        }
        /// <summary>What the squad deals when <paramref name="without"/>'s current weapon is left out: the weapon is about
        /// to be replaced by a tier-3 branch, so it must not vote for its own damage type. The run's tag points are kept.</summary>
        public static TagProfile SquadProfile(Snapshot s, Survivor without)
        {
            var p = new TagProfile { SpecialAt = s.Tags.SpecialAt, Plan = s.Tags.Plan };
            foreach (var kv in s.Tags.Points) p.Points[kv.Key] = kv.Value;
            foreach (var sv in s.Squad)
            {
                if (sv != without && sv.Weapon != null && sv.LevelOf(sv.Weapon) >= 1) Deals(p, sv.Weapon, 1.0 * Levelled(sv.LevelOf(sv.Weapon), MaxLevel(sv.Weapon)));
                foreach (var kv in sv.Powerups)
                {
                    if (kv.Value < 1 || kv.Key == null || !IsAbility(kv.Key)) continue;
                    if (!IsEvolution(kv.Key) && sv.EvolutionOf(kv.Key) != null) continue;
                    Deals(p, kv.Key, 0.5 * (IsEvolution(kv.Key) ? 1.15 : Levelled(kv.Value, MaxLevel(kv.Key))));
                }
            }
            return p;
        }

        static void Deals(TagProfile p, PowerupBase powerup, double weight)
        {
            string name = Name(powerup);
            var types = new List<string>();
            try { foreach (var t in Each(powerup.hashtagTypes)) { string n = TagName(t); if (n != null && !types.Contains(n)) types.Add(n); } } catch { }
            p.Source(name, weight, types);
        }
        // a level-1 powerup carries 40 % of what it will at its maximum
        static double Levelled(int level, int max) { return 0.4 + 0.6 * Math.Min(1.0, level / (double)Math.Max(1, max)); }

        /// <summary>The run's pace, kept across snapshots: level-up screens seen and when (Advisor feeds it).</summary>
        internal static class Pace
        {
            static readonly List<float> _at = new List<float>();
            static float _lastSeconds = -1f;
            public static int Count { get { return _at.Count; } }
            public static void Clock(float seconds) { if (seconds + 5f < _lastSeconds) _at.Clear(); _lastSeconds = seconds; }   // the clock jumped back: a new run
            public static void LevelUp(float seconds) { Clock(seconds); _at.Add(seconds); }
            /// <summary>Level-ups per minute over the last three minutes of play (0 = too early to say).</summary>
            public static double Rate(float seconds)
            {
                if (seconds < 45f) return 0;
                float window = Math.Min(180f, seconds); int n = 0;
                foreach (var t in _at) if (t >= seconds - window) n++;
                return n / (window / 60.0);
            }
        }

        static void ReadContext(GameplayMaster master, Snapshot s)
        {
            var c = s.Ctx;
            c.D = Doctrine.Current;
            c.Mode = s.Mode; c.Seconds = s.Seconds; c.Horde = s.Horde;
            try
            {
                var gm = master.currentGameMode;
                if (gm != null)
                {
                    try { c.Difficulty = (int)gm.difficulty + 1; } catch { }
                    try { c.Goal = gm.TimeRequiredForSuccess; } catch { }
                    if (s.Mode == "Extermination")
                    {
                        try { var ex = gm.TryCast<GameModeExtermination>(); if (ex != null) c.Wave = ex.CompletedWaveCount() + 1; } catch { }
                        try { var def = master.gameModeDefinition; if (def != null && def.waveDefinitions != null) c.Waves = def.waveDefinitions.Count; } catch { }
                    }
                }
            }
            catch { }
            Pace.Clock(s.Seconds);
            c.LevelUps = Pace.Count; c.LevelRate = Pace.Rate(s.Seconds);
            // recruits carry a sentinel health value, so the leader's health bar speaks for the squad
            foreach (var sv in s.Squad)
            {
                if (!sv.Leader || sv.Player == null) continue;
                try { var h = sv.Player.health; if (h != null && h.MaxHealth > 0 && h.MaxHealth < 1e7f) c.Health = Math.Max(0, Math.Min(1, h.CurrentHealth / h.MaxHealth)); } catch { }
            }
        }

        // the team passives (Grenade / Turret / Trap Expertise, Cold Chain) that are bought and whose owner is on the squad
        static void ReadBoosts(Snapshot s)
        {
            var tagged = TaggedNodes();
            if (tagged == null) return;
            foreach (var tn in tagged)
            {
                if (!NodeOwned(tn.Node) || !s.OnSquad(tn.Class)) continue;      // bought or not is read live
                var b = new TeamBoost { Owner = ClassName(tn.Class), Tag = tn.Tag, NotTag = tn.NotTag, AbilitiesOnly = tn.AbilitiesOnly };
                try { b.Name = tn.Node.GetName(); } catch { }
                if (string.IsNullOrEmpty(b.Name)) b.Name = b.Tag + " Expertise";
                s.Boosts.Add(b);
            }
        }

        // which of the 245 nodes are team passives, and what they boost: asset data, found once per run
        static List<TaggedNode> TaggedNodes()
        {
            var all = NodeList();
            if (all == null) return null;
            if (_taggedNodes != null) return _taggedNodes;
            var list = new List<TaggedNode>();
            foreach (var n in all)
            {
                SkillTreeUpgradeTaggedPowerupBoost t = null; try { t = n.TryCast<SkillTreeUpgradeTaggedPowerupBoost>(); } catch { }
                if (t == null) continue;
                ClassProperties cp = null; try { cp = n.targetClassProperties; } catch { }
                if (cp == null) continue;
                var tn = new TaggedNode { Node = n };
                try { tn.Class = cp.characterType; } catch { continue; }
                try { tn.Tag = t.requiredTag.ToString(); } catch { continue; }
                try { if (t.excludeTag) tn.NotTag = t.excludedTag.ToString(); } catch { }
                try { tn.AbilitiesOnly = t.abilitiesOnly; } catch { }
                list.Add(tn);
            }
            _taggedNodes = list;
            return list;
        }

        /// <summary>A cheap fingerprint of everything the plan reads from the run: who is on the squad, every powerup and
        /// item with its level or count, the tag points, the active quest. The readout polls this every two seconds and
        /// takes the full snapshot (names, the tree, the tags profile: some thousand calls into the game) only when it
        /// moved. 0 = could not be read (the caller then takes the snapshot as before).</summary>
        public static long QuickKey()
        {
            try
            {
                var master = GameplayMaster.s_instance;
                if (master == null) return 0;
                var players = master.gamePlayers;
                if (players == null) return 0;
                unchecked
                {
                    long h = 1469598103934665603L;
                    int n = players.Count;
                    h = h * 1099511628211L + n;
                    for (int i = 0; i < n; i++)
                    {
                        var gp = players[i]; if (gp == null) continue;
                        h = h * 1099511628211L + gp.Pointer.ToInt64();
                        h = h * 1099511628211L + (int)gp.characterType;
                        var w = gp.currentWeaponPowerup;
                        h = h * 1099511628211L + (w == null ? 0L : w.Pointer.ToInt64());
                        var ps = gp.activePowerups; int pn = ps == null ? 0 : ps.Count;
                        h = h * 1099511628211L + pn;
                        for (int j = 0; j < pn; j++)
                        {
                            var p = ps[j]; if (p == null) continue;
                            h = h * 1099511628211L + p.Pointer.ToInt64();
                            h = h * 1099511628211L + gp.GetPowerupLevel(p);
                        }
                        var its = gp.activeItems; int itn = its == null ? 0 : its.Count;
                        h = h * 1099511628211L + itn;
                        for (int j = 0; j < itn; j++)
                        {
                            var it = its[j]; if (it == null) continue;
                            h = h * 1099511628211L + it.Pointer.ToInt64();
                            h = h * 1099511628211L + gp.GetItemCount(it);
                        }
                    }
                    var hs = master.hashtagSystem;
                    if (hs != null) foreach (var t in TagTypes) h = h * 1099511628211L + hs.GetNumType(t);
                    var qm = GameQuestManager.Get; var q = qm == null ? null : qm.ActiveQuest;
                    h = h * 1099511628211L + (q == null ? 0L : q.Pointer.ToInt64());
                    return h == 0 ? 1 : h;
                }
            }
            catch { return 0; }
        }

        // ---- the run right now ----
        public static Snapshot Read()
        {
            var s = new Snapshot();
            GameplayMaster master = null;
            try { master = GameplayMaster.s_instance; } catch { }
            if (master == null) return s;
            try
            {
                var gm = master.currentGameMode;
                if (gm != null)
                {
                    s.Seconds = gm.CurrentModePlayTime;
                    try { s.Clock = gm.GetCurrentRunTimeMMSS(); } catch { s.Clock = TimeSpan.FromSeconds(s.Seconds).ToString(@"mm\:ss"); }
                    s.Mode = gm.gameplayMode.ToString();
                    s.Horde = gm.CurrentHordeLevel;
                }
            }
            catch { }
            var held = new List<KeyValuePair<Survivor, KeyValuePair<PowerupBase, int>>>();
            foreach (var gp in Each(master.gamePlayers))
            {
                if (gp == null) continue;
                var sv = new Survivor { Player = gp, Snap = s };
                try { sv.Type = gp.characterType; } catch { }
                sv.Name = ClassName(sv.Type);
                try { sv.Leader = gp.isMainPlayer; } catch { }
                try { sv.Props = gp.properties; } catch { }
                if (sv.Props == null) sv.Props = PropsOf(sv.Type);
                if (sv.Props != null) sv.TreeLevel = TreeLevel(sv.Props);
                try { sv.Weapon = gp.currentWeaponPowerup; } catch { }
                foreach (var p in Each(gp.activePowerups)) { if (p == null) continue; int lvl = 0; try { lvl = gp.GetPowerupLevel(p); } catch { } held.Add(new KeyValuePair<Survivor, KeyValuePair<PowerupBase, int>>(sv, new KeyValuePair<PowerupBase, int>(p, lvl))); }
                foreach (var it in Each(gp.activeItems)) { if (it == null) continue; int n = 1; try { n = gp.GetItemCount(it); } catch { } sv.Items.Add(new KeyValuePair<ItemBase, int>(it, n)); }
                s.Squad.Add(sv);
            }
            // leader first, as the PC app orders the squad
            s.Squad.Sort((a, b) => (b.Leader ? 1 : 0) - (a.Leader ? 1 : 0));
            // regroup every powerup under the survivor whose class it belongs to
            foreach (var h in held)
            {
                Survivor owner = null;
                try { var cp = h.Value.Key.targetClassProperties; if (cp != null) owner = s.Find(cp.characterType); } catch { }
                (owner ?? h.Key).Powerups.Add(h.Value);
            }
            try { ReadTags(master, s); } catch (Exception e) { Plugin.Logger.LogWarning("[tags] " + e.Message); }
            try { ReadContext(master, s); } catch (Exception e) { Plugin.Logger.LogWarning("[ctx] " + e.Message); }
            try { ReadBoosts(s); } catch (Exception e) { Plugin.Logger.LogWarning("[boosts] " + e.Message); }
            foreach (var sv in s.Squad) foreach (var kv in sv.Items) s.ItemsHeld += Math.Max(1, kv.Value);
            return s;
        }
    }
}
