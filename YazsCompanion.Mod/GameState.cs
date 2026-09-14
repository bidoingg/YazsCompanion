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
        public bool Has(ItemBase it) { foreach (var kv in Items) if (G.Same(kv.Key, it)) return true; return false; }
    }

    internal sealed class Snapshot
    {
        public readonly List<Survivor> Squad = new List<Survivor>();
        public float Seconds;
        public string Clock = "?";
        public string Mode = "?";
        public int Horde;
        public bool SquadFull { get { return Squad.Count >= 3; } }
        public Survivor Find(CT t) { foreach (var s in Squad) if (s.Type == t) return s; return null; }
        public bool OnSquad(CT t) { return Find(t) != null; }
        public bool AnyoneHas(ItemBase it) { foreach (var s in Squad) if (s.Has(it)) return true; return false; }

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

        public static string Name(PowerupBase p)
        {
            if (p == null) return "?";
            try { var n = p.EnglishName; if (!string.IsNullOrEmpty(n)) return n; } catch { }
            try { return p.name; } catch { return "?"; }
        }
        public static string Name(ItemBase it)
        {
            if (it == null) return "?";
            try { var n = it.EnglishName; if (!string.IsNullOrEmpty(n)) return n; } catch { }
            try { return it.name; } catch { return "?"; }
        }
        public static string Asset(UnityEngine.Object o) { try { return o == null ? "" : o.name; } catch { return ""; } }

        public static bool Same(UnityEngine.Object a, UnityEngine.Object b)
        {
            if (a == null || b == null) return false;
            try { if (a.Pointer == b.Pointer) return true; } catch { }
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
        public static IEnumerable<SkillTreeUpgradeBase> AllNodes()
        {
            SkillTreeUpgrades tree = null;
            try { tree = SkillTreeUpgrades.Get; } catch { }
            if (tree == null) yield break;
            foreach (var n in Each(tree.skillTreeUpgrades)) if (n != null) yield return n;
        }
        public static IEnumerable<SkillTreeUpgradeBase> NodesOf(CT t)
        {
            foreach (var n in AllNodes())
            {
                ClassProperties cp = null; try { cp = n.targetClassProperties; } catch { }
                if (cp != null && cp.characterType == t) yield return n;
            }
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
            return s;
        }
    }
}
