// 0.16.0 (C16-01 / C16-01k): the live read behind the held-item rules - what the squad holds (names, assets, the animal flag),
// the item slots (team statistics 43 / 49 / 50), the leader's ability cooldown reduction, the seconds since the last survivor
// joined, the abilities' cooldowns and the [Debug] HeldPretend names - into Snapshot.Held (HeldFacts, HeldRules.cs), once per
// snapshot. Called from GameState.Read, after ItemStats.Fill (Snapshot.FreeSlots is the one read of the free slots), only while a
// game mode runs (the main menu's GameplayMaster has none). Each read in its own try through a NoInlining accessor; a failure is
// said once a session ('[held] <what> not read (<exception>)').
// The game's side (machine code of 1.0.2, research\roadmap_1007\scratch\c16_held_items):
//  - slots: GamePlayer.AddItem adds 1 to TEAM statistic 49 InternalNumEquippedItems, then sets team statistic 50
//    InternalNumFreeItemSlots = (int)(statistic 43 InternalMaxItems - statistic 49); RemoveItem the same backwards; a stack skips both.
//    Wooden Stick / Empty Chest read statistic 50 from the team statistics. The free slots are the game's formula, computed (right also
//    before the run's first AddItem has set statistic 50); statistic 50 is read for the log only.
//  - the join clock: Reserve Bench counts (play time - RunStats.lastCharacterSpawnTime) / 120; SpawnNewGamePlayer raises the spawn event
//    BEFORE it writes lastCharacterSpawnTime, so the value read before a recruit is what the item counts.
//  - cooldowns (C16-01k): Mana Potion's candidates are the main player's activePowerups (every survivor's powerups live on the leader)
//    that are not disabled, have a class, a base cooldown and take the cooldown reduction. They are walked here through the interop
//    getters - never through GamePlayer.GetCooldownReductionCandidates, which refills a list the game's own Mana Potion uses.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YazsCompanion
{
    internal static class HeldRead
    {
        const int MaxCandidates = 12;           // C16-01k: about 50 interop calls a snapshot at most
        static readonly HashSet<string> _said = new HashSet<string>(StringComparer.Ordinal);

        static void Said(string what, Exception e)
        {
            if (!_said.Add(what)) return;
            try { Plugin.Logger.LogInfo("[held] " + what + " not read (" + e.GetType().Name + ": " + e.Message + ")"); } catch { }
        }

        // ---- the accessors: a member a game patch took away fails its own accessor (caught at the call), not the whole read
        [MethodImpl(MethodImplOptions.NoInlining)] static bool IsAnimal(ItemBase it) { return it.isAnimalItem; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float TeamStat(GameplayMaster m, PlayerStatistic.EType t) { return m.teamStatistics.GetStatisticValue(t); }
        [MethodImpl(MethodImplOptions.NoInlining)] static float LeaderStat(GamePlayer gp, PlayerStatistic.EType t) { return gp.GetStatisticFinalValue(t); }
        [MethodImpl(MethodImplOptions.NoInlining)] static float SpawnTime(GameplayMaster m) { var rs = m.runStats; return rs == null ? float.NaN : rs.lastCharacterSpawnTime; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Il2CppSystem.Collections.Generic.List<PowerupBase> Active(GamePlayer gp) { return gp.activePowerups; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float BaseCooldown(PowerupBase p) { return p.baseCooldown; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool Disabled(PowerupBase p) { return p.isDisabled; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool HasClass(PowerupBase p) { return p.targetClassProperties != null; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool TakesReduction(PowerupBase p) { return p.canReceiveCooldownReduction; }
        [MethodImpl(MethodImplOptions.NoInlining)] static float Cooldown(PowerupBase p, bool withoutStat) { return p.GetAbilityCooldown(withoutStat); }

        static bool Ok(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }

        /// <summary>Fill <paramref name="s"/>.Held from the run (GameState.Read, inside its own try).</summary>
        public static void Fill(GameplayMaster master, Snapshot s)
        {
            if (master == null || s == null) return;
            var h = s.Held ?? (s.Held = new HeldFacts());

            // what the squad holds: names, assets ('(Clone)' cut), the game's animal flag (Last Unicorn's count, C16-01i)
            int distinct = 0;
            foreach (var sv in s.Squad)
                foreach (var kv in sv.Items)
                {
                    var it = kv.Key; if (it == null) continue;
                    distinct++;
                    string n = G.Name(it); if (!string.IsNullOrEmpty(n) && n != "?") h.Names.Add(n);
                    string a = HeldRules.CutClone(G.Asset(it)); if (a.Length > 0) h.Assets.Add(a);
                    try { if (IsAnimal(it)) h.AnimalHeld++; } catch (Exception e) { Said("the animal flag", e); }
                }

            // [Debug] HeldPretend: the names the advice treats as held (an animal one counts for Last Unicorn, once)
            try
            {
                foreach (var n in HeldRules.SplitNames(Plugin.HeldPretend == null ? "" : Plugin.HeldPretend.Value))
                {
                    if (!h.Pretend.Add(n)) continue;
                    var animal = HeldRules.AnimalOf(n);
                    if (animal != null && !h.Names.Contains(animal.Name) && !h.Assets.Contains(animal.Asset)) h.AnimalHeld++;
                }
            }
            catch (Exception e) { Said("[Debug] HeldPretend", e); }

            // the squad's abilities at level 1 or more (the reads line's 'N of M owned')
            foreach (var sv in s.Squad) foreach (var kv in sv.Powerups) if (kv.Key != null && kv.Value >= 1 && G.IsAbility(kv.Key)) h.AbilitiesOwned++;

            // the item slots: the team's maximum and filled (statistics 43 / 49), statistic 50 for the log
            double equipped = double.NaN;
            try
            {
                h.Max = HeldRules.CountOf(TeamStat(master, PlayerStatistic.EType.InternalMaxItems));
                equipped = TeamStat(master, PlayerStatistic.EType.InternalNumEquippedItems);
                h.FreeStat = HeldRules.CountOf(TeamStat(master, PlayerStatistic.EType.InternalNumFreeItemSlots));
            }
            catch (Exception e) { Said("the item slots", e); }
            h.Equipped = HeldRules.EquippedOf(equipped, distinct);     // statistic 49 unread: the DISTINCT items held (a stack takes one slot)
            // one read of the free slots for both groups (C16-02b's Snapshot.FreeSlots); the same formula when that one is not read
            h.FreeSlots = Ok(s.FreeSlots) && s.FreeSlots >= 0 ? (int)Math.Round(s.FreeSlots) : HeldRules.FreeOf(h.Max, h.Equipped);

            // the leader: the game's main player (every survivor's powerups live there)
            Survivor lead = null;
            foreach (var sv in s.Squad) if (sv.Leader) { lead = sv; break; }
            if (lead == null && s.Squad.Count > 0) lead = s.Squad[0];

            // the leader's ability cooldown reduction (0 while Mana Potion is really held, C16-01a)
            try { if (lead != null && lead.Player != null) { double cdr = LeaderStat(lead.Player, PlayerStatistic.EType.PlayerAbilityCDRed); if (Ok(cdr)) h.LeaderCdr = cdr; } }
            catch (Exception e) { Said("the leader's ability cooldown reduction", e); }

            // seconds since the last survivor joined (Reserve Bench, C16-01b)
            try
            {
                double spawn = SpawnTime(master);
                double since = Ok(spawn) ? s.Seconds - spawn : -1;
                h.SinceJoin = since >= 0 ? since : -1;
            }
            catch (Exception e) { Said("the time since the last join", e); }

            // C16-01k: (base, with the stat) per ability Mana Potion would reset - null when the read fails
            try { h.Cooldowns = lead != null && lead.Player != null ? Cooldowns(lead.Player) : null; }
            catch (Exception e) { h.Cooldowns = null; Said("the abilities' cooldowns", e); }
        }

        /// <summary>The leader's powerups that ItemManaPotion would pick from (the game's own filter: not disabled, a class, a base cooldown,
        /// takes the cooldown reduction), as (GetAbilityCooldown(true) = the base, GetAbilityCooldown(false) = with the statistic); a base of
        /// 0.05 s or less is no cooldown. The base cooldown is asked first: a weapon or a stat card leaves after one call.</summary>
        static List<KeyValuePair<double, double>> Cooldowns(GamePlayer gp)
        {
            var list = new List<KeyValuePair<double, double>>();
            foreach (var p in G.Each(Active(gp)))
            {
                if (p == null) continue;
                if (list.Count >= MaxCandidates) break;
                if (!(BaseCooldown(p) > 0)) continue;
                if (Disabled(p) || !HasClass(p) || !TakesReduction(p)) continue;
                double b = Cooldown(p, true);
                if (!Ok(b) || b <= 0.05) continue;
                double w = Cooldown(p, false);
                list.Add(new KeyValuePair<double, double>(b, Ok(w) ? w : b));
            }
            return list;
        }
    }
}
