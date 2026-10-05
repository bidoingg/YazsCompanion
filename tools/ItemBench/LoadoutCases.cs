// The badge advice of the run setup screen (0.13.0): Loadout.cs / LoadoutView.cs / LoadoutLayout.cs against
//   P1  the reference model (research\badges_1004\tools\value_model.py, regenerated for 1.0.2, leader only): sections A, B1-B10
//       (+B3a), C1-C6 and D printed in its format and compared with loadout_expected.txt (text equal, numbers within 0.01),
//   M   the model's rules (RunGoal, One Hit, Boss Rush, locked / forced badges, the prefix property, ties, the special pull,
//       doctrine, packs, pins, slots 0-5, a fresh profile, recruits),
//   D   identity and drift (the fixture's hash, references, unknown stats / classes / modes, a renamed asset),
//   V   the view (the truth table of the marks, numbers, the slot mirror, swaps and the keep margin, the EQUIP plan, texts,
//       detail levels),
//   L   the placement (the PC frame, the Deck, 1080p, a crowded grid, no room, a big size; nothing over an obstacle),
//   S   safety (no call that writes the profile outside LoadoutEquip.cs's guarded click; builds.json unchanged without pins),
//   X1  the replay of logged advices (ItemBench --replay-loadout <companion log>).
// The fixture = the 27 badges of 1.0.2 (generated once from research\badges_1004\catalogue.json; game names only) and the
// badge levels of the PC save of 2026-10-04.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class Loadouts
    {
        public const string FixtureBuild = "2026/10/2/11/19#1.0.2";
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------------------------------------ the fixture
        static BadgeBonus B(string stat, string type, double per, string template) { return new BadgeBonus { Stat = stat, Type = type, PerLevel = per, Template = template }; }
        static BadgeFacts F(int id, int sort, string asset, string name, string owner, BadgeKind kind, string kindName, int rank, string[] tags, int[] points, params BadgeBonus[] bonuses)
        {
            var b = new BadgeFacts { Id = id, Sort = sort, Asset = asset, Short = Loadout.ShortName(asset), Name = name, Owner = owner, Kind = kind, KindName = kindName, Rank = rank, Max = 5, TagPoints = points };
            b.TagTypes.AddRange(tags); b.Bonuses.AddRange(bonuses);
            return b;
        }

        /// <summary>The 27 badges of 1.0.2 (catalogue.json, extracted 2026-10-04).</summary>
        public static List<BadgeFacts> Fixture()
        {
            return new List<BadgeFacts>
            {
                F(15, 1, "Badge_SWAT2_Leveling", "Leveling Badge", "SWAT", BadgeKind.Stat, "GameplayBadgeStatBoost", 2, new string[] { }, new int[] { }, B("MoneyMultiplier", "TeamMoneyMultiplier", 0.05, "+{value*100}%"), B("SpecializationPointsMod", "InternalSpecializationPointsMod", 0.02, "+{value*100}%")),
                F(16, 2, "Badge_SWAT3_Gunner", "Gunner Badge", "SWAT", BadgeKind.Tag, "GameplayBadgeHashtagBoost", 3, new string[] { "Kinetic" }, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, B("HashtagKinetic", "TeamHashtagKinetic", 0.1, "+{value*100}%")),
                F(17, 3, "Badge_SWAT4_CriticalBadge", "Critical Badge", "SWAT", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[] { }, new int[] { }, B("AbilityCriticalDamage", "PlayerAbilityCritDamage", 0.12, "+{value*100}%"), B("WeaponCriticalDamage", "PlayerWeaponCritDamage", 0.12, "+{value*100}%")),
                F(18, 11, "Badge_Tank2_Tough", "Tough Badge", "Tank", BadgeKind.Stat, "GameplayBadgeStatBoost", 2, new string[] { }, new int[] { }, B("MaxHealth", "TeamMaxHealth", 80, "+{value}"), B("Armor", "TeamArmor", 4, "+{value}")),
                F(19, 12, "Badge_Tank3_Bomber", "Bomber Badge", "Tank", BadgeKind.Tag, "GameplayBadgeHashtagBoost", 3, new string[] { "Explosive" }, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, B("HashtagExplosive", "TeamHashtagExplosive", 0.1, "+{value*100}%")),
                F(20, 13, "Badge_Tank4_Power", "Power Badge", "Tank", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[] { }, new int[] { }, B("WeaponDamage", "PlayerWeaponDamage", 0.04, "+{value*100}%"), B("AbilityDamage", "PlayerAbilityDamage", 0.04, "+{value*100}%")),
                F(0, 21, "Badge_Engineer2_Coverage", "Coverage Badge", "Engineer", BadgeKind.Stat, "GameplayBadgeStatBoost", 2, new string[] { }, new int[] { }, B("AbilitySize", "PlayerAbilitySize", 0.05, "+{value*100}%"), B("AbilityDuration", "PlayerAbilityDuration", 0.05, "+{value*100}%")),
                F(1, 22, "Badge_Engineer3_Thunder", "Thunder Badge", "Engineer", BadgeKind.Tag, "GameplayBadgeHashtagBoost", 3, new string[] { "Electric" }, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, B("HashtagElectric", "TeamHashtagElectric", 0.1, "+{value*100}%")),
                F(2, 23, "Badge_Engineer4_Reload", "Reload Badge", "Engineer", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[] { }, new int[] { }, B("WeaponCooldownReduction", "PlayerWeaponCDRed", 0.04, "+{value*100}%"), B("AbilityCooldownReduction", "PlayerAbilityCDRed", 0.03, "+{value*100}%")),
                F(6, 31, "Badge_Huntress2_Speed", "Speed Badge", "Huntress", BadgeKind.Stat, "GameplayBadgeStatBoost", 2, new string[] { }, new int[] { }, B("WeaponFireRate", "PlayerWeaponFireRate", 0.04, "+{value*100}%"), B("MovementSpeed", "TeamMovementSpeedMultiplier", 6, "+{value}")),
                F(7, 32, "Badge_Huntress3_Physical", "Physical Badge", "Huntress", BadgeKind.Physical, "GameplayBadgeHashtagPhysicalBoost", 3, new string[] { "Kinetic", "Slashing", "Explosive" }, new int[] { 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 }, B("HashtagKinetic", "TeamHashtagKinetic", 0.04, "+{value*100}%"), B("HashtagSlashing", "TeamHashtagSlashing", 0.04, "+{value*100}%"), B("HashtagExplosive", "TeamHashtagExplosive", 0.04, "+{value*100}%")),
                F(8, 33, "Badge_Huntress4_Boss", "Boss Badge", "Huntress", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[] { }, new int[] { }, B("DamageToElites", "InternalDamageToElites", 0.1, "+{value*100}%"), B("DamageToBosses", "InternalDamageToBosses", 0.05, "+{value*100}%")),
                F(3, 41, "Badge_Ghost2_Ninja", "Ninja Badge", "Ghost", BadgeKind.Stat, "GameplayBadgeStatBoost", 2, new string[] { }, new int[] { }, B("DodgeChance", "TeamDodgeChance", 0.03, "+{value*100}%"), B("InviFrames", "TeamInviFrames", 0.05, "+{value}s")),
                F(4, 42, "Badge_Ghost3_Carver", "Carver Badge", "Ghost", BadgeKind.Tag, "GameplayBadgeHashtagBoost", 3, new string[] { "Slashing" }, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, B("HashtagSlashing", "TeamHashtagSlashing", 0.1, "+{value*100}%")),
                F(5, 43, "Badge_Ghost4_Chance", "Chance Badge", "Ghost", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[] { }, new int[] { }, B("WeaponCriticalChance", "PlayerWeaponCritChance", 0.04, "+{value*100}%"), B("AbilityCriticalChance", "PlayerAbilityCritChance", 0.04, "+{value*100}%")),
                F(9, 51, "Badge_Medic2_Growth", "Growth Badge", "Medic", BadgeKind.Stat, "GameplayBadgeStatBoost", 2, new string[] { }, new int[] { }, B("XPMultiplier", "TeamXPMultiplier", 0.04, "+{value*100}%"), B("Luck", "TeamLuck", 4, "+{value}")),
                F(10, 52, "Badge_Medic3_Soul", "Soul Badge", "Medic", BadgeKind.Tag, "GameplayBadgeHashtagBoost", 3, new string[] { "Chemical" }, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, B("HashtagToxic", "TeamHashtagToxic", 0.1, "+{value*100}%")),
                F(11, 53, "Badge_Medic4_Healing", "Healing Badge", "Medic", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[] { }, new int[] { }, B("HealthRegen", "TeamHealthRegen", 1, "+{value}/s"), B("HealthBonusesMod", "TeamHealthBonuses", 0.05, "+{value*100}%")),
                F(12, 61, "Badge_Pyro2_Gamble", "Gamble Badge", "Pyro", BadgeKind.Stat, "GameplayBadgeStatBoost", 2, new string[] { }, new int[] { }, B("NumRerolls", "TeamNumRerolls", 2, "+{value}"), B("NumBanishes", "TeamNumBanishes", 1, "+{value}")),
                F(13, 62, "Badge_Pyro3_Heat", "Heat Badge", "Pyro", BadgeKind.Tag, "GameplayBadgeHashtagBoost", 3, new string[] { "Fire" }, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, B("HashtagFire", "TeamHashtagFire", 0.1, "+{value*100}%")),
                F(14, 63, "Badge_Pyro4_Elemental", "Elemental Badge", "Pyro", BadgeKind.Elemental, "GameplayBadgeHashtagElementalBoost", 4, new string[] { "Ice", "Fire", "Electric", "Chemical" }, new int[] { 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 }, B("HashtagFire", "TeamHashtagFire", 0.04, "+{value*100}%"), B("HashtagIce", "TeamHashtagIce", 0.04, "+{value*100}%"), B("HashtagElectric", "TeamHashtagElectric", 0.04, "+{value*100}%"), B("HashtagToxic", "TeamHashtagToxic", 0.04, "+{value*100}%")),
                F(27, 71, "Badge_Mechanic2_Gatherer", "Gatherer Badge", "Mechanic", BadgeKind.Stat, "GameplayBadgeStatBoost", 2, new string[] { }, new int[] { }, B("MagnetRange", "TeamMagnetRange", 0.1, "+{value*100}%"), B("PowerupDurationExtension", "InternalPowerupDurationExtension", 0.1, "+{value*100}%")),
                F(28, 72, "Badge_Mechanic3_Glacier", "Glacier Badge", "Mechanic", BadgeKind.Tag, "GameplayBadgeHashtagBoost", 3, new string[] { "Ice" }, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, B("HashtagIce", "TeamHashtagIce", 0.1, "+{value*100}%")),
                F(29, 73, "Badge_Mechanic4_Thief", "Thief Badge", "Mechanic", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[] { }, new int[] { }, B("XPGemRarity", "InternalXPGemRarity", 0.1, "+{value*100}%")),
                F(30, 81, "Badge_Ranger2_Dexterity", "Dexterity Badge", "Ranger", BadgeKind.Stat, "GameplayBadgeStatBoost", 2, new string[] { }, new int[] { }, B("InstantWeaponReloadChance", "InternalInstantWeaponReloadChance", 0.05, "+{value*100}")),
                F(31, 82, "Badge_Ranger3_Refresh", "Refresh Badge", "Ranger", BadgeKind.Stat, "GameplayBadgeStatBoost", 3, new string[] { }, new int[] { }, B("InstantAbilityReloadChance", "InternalInstantAbilityReloadChance", 0.03, "+{value*100}")),
                F(32, 83, "Badge_Ranger4_Training", "Training Badge", "Ranger", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[] { }, new int[] { }, B("InternalMilitaryTrainingRarityBonus", "InternalMilitaryTrainingRarityBonus", 0.08, "+{value*100}%")),
            };
        }

        /// <summary>The PC save of 2026-10-04: badge levels (the BadgeBoost nodes) and the badges whose tree rank is not reached.</summary>
        static readonly Dictionary<int, int> SaveLevels = new Dictionary<int, int>
        {
            { 15, 2 }, { 16, 2 }, { 17, 2 }, { 18, 2 }, { 19, 2 }, { 20, 2 }, { 0, 2 }, { 1, 2 }, { 2, 1 }, { 6, 1 }, { 7, 1 }, { 8, 1 }, { 3, 1 }, { 4, 1 },
            { 5, 0 }, { 9, 1 }, { 10, 0 }, { 11, 0 }, { 12, 1 }, { 13, 1 }, { 14, 1 }, { 27, 0 }, { 28, 0 }, { 29, 0 }, { 30, 0 }, { 31, 0 }, { 32, 0 }
        };
        static readonly int[] SaveClosed = { 10, 11, 28, 29, 30, 31, 32 };
        static readonly int[] SaveEquipped = { 15, 6, 9, 1 };
        static Dictionary<int, int> Fresh() { return new Dictionary<int, int> { { 15, 1 }, { 18, 1 }, { 0, 1 }, { 6, 1 }, { 3, 1 }, { 9, 1 }, { 12, 1 } }; }
        static Dictionary<int, int> AllAt(int level) { return Fixture().ToDictionary(b => b.Id, b => level); }

        static readonly Dictionary<string, PowerFacts> Powers = new Dictionary<string, PowerFacts>(StringComparer.Ordinal);
        static PowerFacts Fact(string name) { PowerFacts f; return name != null && Powers.TryGetValue(name, out f) ? f : null; }

        static void LoadProbe(string probePath)
        {
            Powers.Clear();
            using (var doc = JsonDocument.Parse(File.ReadAllText(probePath)))
                foreach (var p in doc.RootElement.GetProperty("powerups").EnumerateArray())
                {
                    var f = new PowerFacts { Name = p.GetProperty("name").GetString() };
                    JsonElement e;
                    if (p.TryGetProperty("damage", out e)) foreach (var t in e.EnumerateArray()) f.Damage.Add(t.GetString());
                    if (p.TryGetProperty("tags", out e)) foreach (var t in e.EnumerateArray()) f.Tags.Add(t.GetString());
                    Powers[f.Name] = f;
                }
        }

        // ------------------------------------------------------------------------------------------------ scenarios
        sealed class Sc
        {
            public string Title = "", Leader = "", Preset;
            public string[] Recruits = new string[0];
            public string Mode = "Normal", TagPlan = "Auto";
            public int Diff = 1, Farming, Caution = 1, Slots = 4;
            public Dictionary<int, int> Levels;
            public int[] Forced = new int[0], Equipped = new int[0];
            public bool SaveRanks, ShowAll;
            public Sc With(Action<Sc> change) { var c = (Sc)MemberwiseClone(); change(c); return c; }
        }

        static Knowledge K() { return Knowledge.FromJson(Knowledge.DefaultJson); }
        static Knowledge KRecruits() { var k = K(); k.BadgeRules.RecruitWeight = 0.3; return k; }
        static Build Preset(string id) { return id == null ? null : Builds.Presets.First(b => b.Id == id); }

        static LoadoutInput Input(Sc sc, Knowledge k, int slots = -1, Build build = null)
        {
            var d = new Doctrine { Farming = sc.Farming, Caution = sc.Caution, TagPlan = sc.TagPlan };
            var rec = sc.Recruits.Select(r => new KeyValuePair<string, Build>(r, Builds.PresetsOf(r).First())).ToList();
            var inp = Loadout.Prepare(sc.Leader, build ?? Preset(sc.Preset), sc.Mode, sc.Diff, d, k, Fact, rec);
            inp.Slots = slots >= 0 ? slots : sc.Slots;
            foreach (var kv in sc.Levels) inp.Levels[kv.Key] = kv.Value;
            if (sc.SaveRanks) foreach (var id in SaveClosed) inp.RankOpen[id] = false;
            inp.Forced.AddRange(sc.Forced); inp.Equipped.AddRange(sc.Equipped);
            inp.Inventory = Loadout.ShortHash(Loadout.Hash(Fixture())); inp.KnowledgeHash = Loadout.KnowledgeHash(k);
            return inp;
        }
        static LoadoutAdvice Advise(Sc sc, Knowledge k = null, int slots = -1, List<BadgeFacts> fixture = null, Build build = null)
        {
            k = k ?? (sc.Recruits.Length > 0 ? KRecruits() : K());
            return Loadout.Recommend(Input(sc, k, slots, build), fixture ?? Fixture(), k, 7);
        }

        static List<Sc> Scenarios()
        {
            var b1 = new Sc { Title = "B1. The user's profile: SWAT Rifleman, Normal II, WinTheRun, solo start (4 slots, levels from the save)", Leader = "SWAT", Preset = "swat-rifleman", Diff = 2, Levels = SaveLevels, Equipped = SaveEquipped, SaveRanks = true, ShowAll = true };
            return new List<Sc>
            {
                b1,
                b1.With(s => { s.Title = "B2. The same with RunGoal FarmProgress"; s.Farming = 2; s.ShowAll = false; }),
                b1.With(s => { s.Title = "B3. The user's last win: Huntress (Auto), Normal II, recruits Engineer + Tank expected (recruitWeight 0.3)"; s.Leader = "Huntress"; s.Preset = null; s.Recruits = new[] { "Engineer", "Tank" }; }),
                b1.With(s => { s.Title = "B3a. The same, leader only (recruitWeight 0, the default: OD3)"; s.Leader = "Huntress"; s.Preset = null; s.ShowAll = false; }),
                b1.With(s => { s.Title = "B4. Huntress Bombardier (Explosive Arrows), Normal II, solo"; s.Leader = "Huntress"; s.Preset = "huntress-bombardier"; s.ShowAll = false; }),
                b1.With(s => { s.Title = "B5. Engineer Shield Anchor, Hardcore I"; s.Leader = "Engineer"; s.Preset = "engineer-anchor"; s.Mode = "Hardcore"; s.Diff = 1; s.ShowAll = false; }),
                b1.With(s => { s.Title = "B6. Tank Demolition, One Hit"; s.Leader = "Tank"; s.Preset = "tank-demolition"; s.Mode = "OneHit"; s.Diff = 1; s.Equipped = new int[0]; }),
                b1.With(s => { s.Title = "B7. Ghost Blademaster, Boss Rush"; s.Leader = "Ghost"; s.Preset = "ghost-blademaster"; s.Mode = "BossRush"; s.Diff = 1; s.Equipped = new int[0]; s.ShowAll = false; }),
                b1.With(s => { s.Title = "B8. Pyro Inferno (Flamethrower), Endless"; s.Leader = "Pyro"; s.Preset = "pyro-inferno"; s.Mode = "Endless"; s.Diff = 1; s.Equipped = new int[0]; s.ShowAll = false; }),
                b1.With(s => { s.Title = "B9. Medic Field Medic, Extermination"; s.Leader = "Medic"; s.Preset = "medic-field"; s.Mode = "Extermination"; s.Diff = 1; s.Equipped = new int[0]; s.ShowAll = false; }),
                b1.With(s => { s.Title = "B10. Engineer Live Wire, Normal I, Cautious"; s.Leader = "Engineer"; s.Preset = "engineer-livewire"; s.Diff = 1; s.Caution = 2; s.Equipped = new int[0]; s.ShowAll = false; }),
                new Sc { Title = "C1. Fresh profile, 2 slots: SWAT Rifleman, Normal I (only the rank-2 badges, L1)", Leader = "SWAT", Preset = "swat-rifleman", Levels = Fresh(), Slots = 2, ShowAll = true },
                new Sc { Title = "C2. Every badge at L5, 4 slots: Pyro Inferno, Normal I (Heat vs Elemental)", Leader = "Pyro", Preset = "pyro-inferno", Levels = AllAt(5) },
                new Sc { Title = "C3. Every badge at L5: Pyro Inferno with Engineer + Medic + Mechanic expected (spread elements)", Leader = "Pyro", Preset = "pyro-inferno", Recruits = new[] { "Engineer", "Medic", "Mechanic" }, Levels = AllAt(5) },
                new Sc { Title = "C4. Every badge at L5: Ranger Beastmaster (abilities, crit), Normal I", Leader = "Ranger", Preset = "ranger-beastmaster", Levels = AllAt(5) },
                new Sc { Title = "C5. Forced by the Ranger mission: Gatherer + Thief forced, SWAT Rifleman, all L5", Leader = "SWAT", Preset = "swat-rifleman", Levels = AllAt(5), Forced = new[] { 27, 29 } },
                new Sc { Title = "C6. Every badge at L5: Mechanic Cold Chain, Normal I (Glacier special pull)", Leader = "Mechanic", Preset = "mechanic-coldchain", Levels = AllAt(5) },
            };
        }
        static Sc Find(string prefix) { return Scenarios().First(s => s.Title.StartsWith(prefix + ".", StringComparison.Ordinal)); }

        // ------------------------------------------------------------------------------------------------ P1: the reference model's report
        static string P(double x, int dec = 2) { return Loadout.Fixed(x, dec); }
        static string Pct(double x) { return ((long)Math.Round(x, MidpointRounding.ToEven)).ToString(IC); }
        static string Rule { get { return new string('=', 118); } }

        static string FmtTerms(List<BadgeTerm> terms)
        {
            var parts = terms.Select((t, i) => new { t, i }).OrderByDescending(x => x.t.Points).ThenBy(x => x.i).Where(x => x.t.Points > 0.004)
                .Select(x => (string.IsNullOrEmpty(x.t.Text) || x.t.Text == x.t.Kind ? x.t.Kind : x.t.Text) + " " + P(x.t.Points)).ToList();
            return parts.Count == 0 ? "-" : string.Join(", ", parts);
        }

        static void Report(StringBuilder o, Sc sc)
        {
            var k = sc.Recruits.Length > 0 ? KRecruits() : K();
            var inp = Input(sc, k);
            var a = Loadout.Recommend(inp, Fixture(), k, 0);
            var s = inp.Shape; var c = inp.Ctx;
            o.Append("\n" + Rule + "\n" + sc.Title + "\n");
            o.Append("leader " + sc.Leader + ", build " + s.BuildName + ", recruits " + (s.Recruits.Count > 0 ? string.Join("+", s.Recruits) : "-") + " | " + c.Text() + " | slots " + inp.Slots + "\n");
            o.Append("deals " + string.Join(", ", s.Dealt().Select(x => x.Key + " " + Pct(x.Value * 100) + "%")) + " | weapon " + P(s.WeaponShare * 100, 0) + "% / ability " + P(s.AbilityShare * 100, 0)
                + "% | crit lean " + P(s.Crit, 1) + " | deployable " + P(s.Deploy * 100, 0) + "% | projected tags " + string.Join(", ", s.ProjectedList().Select(x => x.Key + " " + x.Value)) + "\n");
            o.Append("ADVICE: " + string.Join("  ", a.Picks.Select((p, i) => (i + 1) + " " + p.Badge.Short + " L" + Math.Max(p.Level, 1) + " " + P(p.Score)
                + (p.Forced ? " (forced)" : p.Close ? " (close: " + p.CloseTo.Short + ")" : ""))) + "\n");
            for (int i = 0; i < a.Picks.Count; i++) { var p = a.Picks[i]; o.Append("   " + (i + 1) + " " + p.Badge.Short.PadRight(10) + " " + p.Effect + " - " + p.Why + "\n"); }
            if (a.Next.Count > 0) o.Append("   next: " + string.Join("; ", a.Next.Select(r => r.Badge.Short + " " + P(r.Score) + " (" + Loadout.WhyOf(r, a, k) + ")")) + "\n");
            if (a.Never.Count > 0) o.Append("   never: " + string.Join("; ", a.Never.Take(3).Select(r => r.Badge.Short + " (" + Loadout.WhyOf(r, a, k) + ")")) + "\n");
            if (a.Level != null) o.Append("LEVEL: " + a.Level.Badge.Short + " " + a.Level.From + ">" + a.Level.To + " costs " + a.Level.Cost + " " + a.Level.Badge.Owner + " points: +" + P(a.Level.Gain) + " (" + P(a.Level.PerPoint) + " per point)\n");
            if (sc.Equipped.Length > 0)
            {
                var eq = sc.Equipped.Select(id => a.RowOf(id)).ToList();
                var adv = a.Picks.Select(p => p.Badge.Id).ToList();
                var outs = eq.Select((r, i) => new { r, i }).Where(x => !adv.Contains(x.r.Badge.Id)).OrderBy(x => x.r.Score).ThenBy(x => x.i).Select(x => x.r).ToList();
                var ins = a.Picks.Where(p => !sc.Equipped.Contains(p.Badge.Id) && !p.Forced).ToList();
                var swaps = new List<string>();
                for (int i = 0; i < Math.Min(outs.Count, ins.Count); i++)
                {
                    double gain = (Loadout.Centi(ins[i].Score) - Loadout.Centi(outs[i].Score)) / 100.0;
                    swaps.Add(outs[i].Badge.Short + " > " + ins[i].Badge.Short + " (+" + P(gain) + (gain >= Math.Max(1.0, 0.15 * outs[i].Score) ? "" : ", close: keep yours") + ")");
                }
                o.Append("EQUIPPED: " + string.Join(", ", eq.Select(r => r.Badge.Short + " " + P(r.Score))) + (swaps.Count > 0 ? " | SWAP " + string.Join("; ", swaps) : " | matches the advice") + "\n");
            }
            var unlock = a.Unlocks.Where(u => !u.Pinned).ToList();
            if (unlock.Count > 0) o.Append("UNLOCK: " + string.Join("; ", unlock.Take(3).Select(u => u.Row.Badge.Short + " (" + u.Row.Badge.Owner + " tree, rank " + u.Row.Badge.Rank + ") " + P(u.Row.Score) + " at L1")) + "\n");
            if (sc.ShowAll)
            {
                o.Append("   " + "badge".PadRight(10) + " " + "class".PadRight(8) + " " + "lvl".PadLeft(3) + " " + "score".PadLeft(7) + " " + "per-L".PadLeft(7) + "  terms at its level (points)\n");
                foreach (var r in a.All)
                    o.Append("   " + r.Badge.Short.PadRight(10) + " " + r.Badge.Owner.PadRight(8) + " " + r.Level.ToString(IC).PadLeft(3) + " " + P(r.Level >= 1 ? r.Score : 0).PadLeft(7) + " " + P(r.Per).PadLeft(7) + "  " + FmtTerms(r.Terms) + "\n");
            }
        }

        static RunShape Synthetic(double weapon = 0.5, double crit = 0.3, string[] wants = null, double deploy = 0.5, double heal = 0.3, IList<string> full = null, bool selected = false)
        {
            var s = new RunShape { BuildSelected = selected, BuildName = "test", WeaponW = weapon, AbilityW = 1 - weapon, Crit = crit, Deploy = deploy, Heal = heal };
            if (wants != null) s.Wants.AddRange(wants);
            foreach (var t in (full != null && full.Count > 0 ? full : (IList<string>)TagProfile.Names)) s.Add("src-" + t, 1.0, new[] { t });
            return s;
        }
        static LoadoutCtx Ctx(string mode, int diff = 1, int farming = 0, Knowledge k = null)
        {
            k = k ?? K();
            return Loadout.Averaged(Loadout.ContextFor(mode, diff, new Doctrine { Farming = farming }, k), k);
        }
        static double Sc1(BadgeFacts b, int level, RunShape s, LoadoutCtx c, Knowledge k = null, List<BadgeTerm> terms = null)
        {
            return Loadout.Score(b, level, s, c, new Dictionary<string, int>(), k ?? K(), terms);
        }

        static string ReferenceReport()
        {
            var o = new StringBuilder();
            var k = K();
            o.Append("Badge value model - reference run (" + FixtureBuild + ")\n");
            o.Append("POINTS = %% more squad damage over the whole run, or its equivalent. Defaults: value_weights.json.\n");      // the prototype writes "%%" as it stands
            var neutral = Ctx("Normal");
            var sh = new RunShape { Lead = "SWAT", WeaponW = 0.5, AbilityW = 0.5, Crit = 0.3, Heal = 0.3, Deploy = 0.5 };
            foreach (var t in TagProfile.Names) sh.Add("any", 1.0, new[] { t });
            o.Append("\n" + Rule + "\nA. POINTS PER BADGE LEVEL, neutral run (Normal d1, WinTheRun, weapon 50 / ability 50, each damage type 1/7, no special pull)\n");
            o.Append("   for a tag badge also shown: per level when ALL of the run's damage is of its type(s), split evenly\n");
            foreach (var b in Fixture().OrderBy(x => x.Sort))
            {
                var terms = new List<BadgeTerm>();
                double s1 = Sc1(b, 1, sh, neutral, k, terms);
                string full = "";
                if (b.TagTypes.Count > 0)
                {
                    var sh2 = new RunShape { Lead = "SWAT", WeaponW = 0.5, AbilityW = 0.5 };
                    foreach (var t in b.TagTypes) sh2.Add("src-" + t, 1.0, new[] { t });
                    full = "   all-" + string.Join("/", b.TagTypes) + ": L1 " + P(Sc1(b, 1, sh2, neutral, k)) + ", L5 " + P(Sc1(b, 5, sh2, neutral, k));
                }
                o.Append("   " + b.Id.ToString(IC).PadLeft(2) + " " + b.Short.PadRight(10) + " " + b.Owner.PadRight(8) + " L1 " + P(s1).PadLeft(5) + "  L5 " + P(Sc1(b, 5, sh, neutral, k)).PadLeft(5) + "  " + FmtTerms(terms) + full + "\n");
            }
            foreach (var sc in Scenarios()) Report(o, sc);
            // D. the matrix
            var cols = new List<Tuple<string, LoadoutCtx, Func<BadgeFacts, RunShape>>>
            {
                Tuple.Create<string, LoadoutCtx, Func<BadgeFacts, RunShape>>("neutral", Ctx("Normal"), b => Synthetic()),
                Tuple.Create<string, LoadoutCtx, Func<BadgeFacts, RunShape>>("fit", Ctx("Normal"), b => b.TagTypes.Count > 0 ? Synthetic(full: b.TagTypes) : Synthetic()),
                Tuple.Create<string, LoadoutCtx, Func<BadgeFacts, RunShape>>("wpn+crit", Ctx("Normal"), b => Synthetic(weapon: 0.65, crit: 1.0, wants: new[] { "weapons", "critical" }, selected: true)),
                Tuple.Create<string, LoadoutCtx, Func<BadgeFacts, RunShape>>("ability", Ctx("Normal"), b => Synthetic(weapon: 0.35, wants: new[] { "abilities" }, deploy: 0.8, selected: true)),
                Tuple.Create<string, LoadoutCtx, Func<BadgeFacts, RunShape>>("Hardcore", Ctx("Hardcore"), b => Synthetic()),
                Tuple.Create<string, LoadoutCtx, Func<BadgeFacts, RunShape>>("Farm", Ctx("Normal", 1, 2), b => Synthetic()),
                Tuple.Create<string, LoadoutCtx, Func<BadgeFacts, RunShape>>("BossRush", Ctx("BossRush"), b => Synthetic()),
                Tuple.Create<string, LoadoutCtx, Func<BadgeFacts, RunShape>>("Endless", Ctx("Endless"), b => Synthetic()),
                Tuple.Create<string, LoadoutCtx, Func<BadgeFacts, RunShape>>("OneHit", Ctx("OneHit"), b => Synthetic()),
                Tuple.Create<string, LoadoutCtx, Func<BadgeFacts, RunShape>>("Extermin.", Ctx("Extermination"), b => Synthetic()),
            };
            o.Append("\n" + Rule + "\nD. POINTS PER BADGE LEVEL by context (fit = all of the run's damage is of its type(s), split evenly; the others: every type 1/7)\n");
            o.Append("   " + "badge".PadRight(10) + " " + "class".PadRight(8) + string.Concat(cols.Select(x => x.Item1.PadLeft(10))) + "\n");
            foreach (var b in Fixture().OrderBy(x => x.Sort))
                o.Append("   " + b.Short.PadRight(10) + " " + b.Owner.PadRight(8) + string.Concat(cols.Select(x => P(Sc1(b, 1, x.Item3(b), x.Item2, k)).PadLeft(10))) + "\n");
            return o.ToString();
        }

        static readonly Regex NumRx = new Regex(@"-?\d+(?:\.\d+)?");

        /// <summary>Line by line: the text between the numbers equal, every number within 0.01.</summary>
        static int CompareReport(string mine, string expected, out int same, out int near, List<string> bad)
        {
            var a = mine.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'); var b = expected.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            same = 0; near = 0; int n = Math.Max(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                string x = i < a.Length ? a[i] : "<missing>", y = i < b.Length ? b[i] : "<missing>";
                if (x == y) { same++; continue; }
                var nx = NumRx.Matches(x).Cast<Match>().ToList(); var ny = NumRx.Matches(y).Cast<Match>().ToList();
                bool ok = NumRx.Replace(x, "#") == NumRx.Replace(y, "#") && nx.Count == ny.Count;
                for (int j = 0; ok && j < nx.Count; j++)
                    ok = Math.Abs(double.Parse(nx[j].Value, IC) - double.Parse(ny[j].Value, IC)) <= 0.0100001;
                if (ok) near++;
                else bad.Add("line " + (i + 1) + "\n      mine:     " + x + "\n      expected: " + y);
            }
            return n;
        }

        // ------------------------------------------------------------------------------------------------ the run
        static int _bad;
        static void Check(string label, bool ok, string detail = "")
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok   " : "BAD  ") + label + (detail.Length > 0 ? ": " + detail : ""));
        }
        static string Ids(IEnumerable<int> ids) { var l = ids.ToList(); return l.Count == 0 ? "-" : string.Join(",", l); }
        static string Names(LoadoutAdvice a) { return string.Join(", ", a.Picks.Select(p => p.Badge.Short + " " + P(p.Score))); }

        public static int Run(string probePath, string gamedataPath)
        {
            _bad = 0;
            LoadProbe(probePath);
            string root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(probePath))) ?? ".";
            Console.WriteLine("\n\n################ 0.13.0 badge advice (Loadout.cs, LoadoutView.cs, LoadoutLayout.cs; 27 badges of " + Loadout.ReferenceGame + ")");
            Parity();
            ProbeDrift(probePath);
            Model();
            Data(root);
            View();
            Layout();
            Safety();
            Replay();
            Console.WriteLine("\n  badge advice: " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        static string ExpectedPath()
        {
            foreach (var p in new[] { Path.Combine(AppContext.BaseDirectory, "loadout_expected.txt"), Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "loadout_expected.txt")) })
                if (File.Exists(p)) return p;
            return null;
        }

        static void Parity()
        {
            Console.WriteLine("\n=== P1 the reference model's report, recomputed (research\\badges_1004\\tools\\value_model.py, 1.0.2, leader only)");
            string mine = ReferenceReport();
            foreach (var line in mine.Replace("\r\n", "\n").TrimEnd('\n').Split('\n')) Console.WriteLine("  | " + line);
            string path = ExpectedPath();
            if (path == null) { Check("P1 parity file loadout_expected.txt", false, "not found"); return; }
            var bad = new List<string>(); int same, near;
            int n = CompareReport(mine, File.ReadAllText(path), out same, out near, bad);
            foreach (var b in bad.Take(20)) Console.WriteLine("    DIFF " + b);
            Check("P1 parity with loadout_expected.txt", bad.Count == 0, n + " lines: " + same + " identical, " + near + " within 0.01, " + bad.Count + " different");
        }

        // once the game's [Debug] Probe writes a "badges" section (the badge objects of Loadout.InventoryJson), the drift against the fixture
        static void ProbeDrift(string probePath)
        {
            try
            {
                string json = File.ReadAllText(probePath);
                using (var doc = JsonDocument.Parse(json))
                {
                    JsonElement e;
                    if (!doc.RootElement.TryGetProperty("badges", out e) || e.ValueKind != JsonValueKind.Array) { Console.WriteLine("\n  (probe.json has no badges section yet: no drift against the live game to show)"); return; }
                }
                var live = Loadout.ReadInventory(json);
                string drift = Loadout.Drift(Fixture(), live);
                Console.WriteLine("\n  probe.json badges: " + live.Count + ", hash " + Loadout.ShortHash(Loadout.Hash(live)) + (drift.Length == 0 ? " = the fixture" : " - drift: " + drift));
            }
            catch (Exception ex) { Console.WriteLine("\n  probe.json badges unreadable: " + ex.Message); }
        }

        // ------------------------------------------------------------------------------------------------ M: the model
        static void Model()
        {
            Console.WriteLine("\n=== M the model");
            var k = K();
            var b1 = Find("B1"); var a1 = Advise(b1);
            // M1
            var v1 = LoadoutView.Of(a1, b1.Equipped, b1.Forced, 4, LoadoutDetail.NumbersAndReason, k);
            Func<int, string> whyOf = id => Loadout.WhyOf(a1.RowOf(id), a1, k);
            Check("M1 B1 SWAT Rifleman: Gunner #1", a1.Picks[0].Badge.Id == 16, Names(a1));
            Check("M1 Thunder, Carver, Heat = 0 with 'nothing in this run deals ...'", new[] { 1, 4, 13 }.All(id => a1.RowOf(id).Score == 0 && whyOf(id).StartsWith("nothing in this run deals ", StringComparison.Ordinal)),
                string.Join("; ", new[] { 1, 4, 13 }.Select(id => a1.RowOf(id).Badge.Short + " " + P(a1.RowOf(id).Score) + " " + whyOf(id))));
            Check("M1 first swap Thunder>Gunner", v1.Swaps.Count > 0 && v1.Swaps[0].Key == 1 && v1.Swaps[0].Value == 16, v1.EquippedLine());
            // M2 RunGoal (OD2)
            var a2 = Advise(Find("B2"));
            var aBal = Advise(b1.With(s => s.Farming = 1));
            double lw = a1.RowOf(15).Score, lf = a2.RowOf(15).Score, lb = aBal.RowOf(15).Score;
            Check("M2 RunGoal: Leveling not in the WinTheRun 4 (2.10), #2 on FarmProgress (8.40), Balanced between", !a1.Equip.Contains(15) && P(lw) == "2.10" && a2.Picks.Count > 1 && a2.Picks[1].Badge.Id == 15 && P(lf) == "8.40" && lb > lw && lb < lf,
                "win " + P(lw) + ", balanced " + P(lb) + ", farm " + P(lf) + " (#" + (a2.Equip.IndexOf(15) + 1) + ")");
            // M3 One Hit
            var b6 = Find("B6"); var a6 = Advise(b6);
            var a6max = Advise(b6.With(s => s.Levels = AllAt(5)));
            Func<LoadoutAdvice, int, bool> oneHit = (a, id) => a.RowOf(id).Score == 0 && Loadout.WhyOf(a.RowOf(id), a, k) == "One Hit: a single hit ends the run";
            Check("M3 One Hit (B6): Tough, Ninja = 0.00 with 'One Hit: a single hit ends the run'", oneHit(a6, 18) && oneHit(a6, 3));
            Check("M3 One Hit, every badge L5: Tough, Ninja, Healing = 0.00 with the One Hit reason", oneHit(a6max, 18) && oneHit(a6max, 3) && oneHit(a6max, 11));
            var a6n = Advise(b6.With(s => { s.Mode = "Normal"; }));
            Check("M3 Speed in One Hit >= its Normal value", a6.RowOf(6).Score >= a6n.RowOf(6).Score, "One Hit " + P(a6.RowOf(6).Score) + ", Normal " + P(a6n.RowOf(6).Score));
            // M4 - M6 from the D matrix
            var fx = Fixture(); Func<int, BadgeFacts> bf = id => fx.First(b => b.Id == id);
            double bossBR = Sc1(bf(8), 1, Synthetic(), Ctx("BossRush")), bossN = Sc1(bf(8), 1, Synthetic(), Ctx("Normal"));
            Check("M4 Boss Rush: Boss per level >= 1.9x Normal", bossBR >= 1.9 * bossN, P(bossBR) + " vs " + P(bossN));
            var ex = Ctx("Extermination");
            Check("M5 Extermination: Growth, Thief = 0; Gatherer 0.24", Sc1(bf(9), 1, Synthetic(), ex) == 0 && Sc1(bf(29), 1, Synthetic(), ex) == 0 && P(Sc1(bf(27), 1, Synthetic(), ex)) == "0.24");
            double gE = Sc1(bf(9), 1, Synthetic(), Ctx("Endless")), gN = Sc1(bf(9), 1, Synthetic(), Ctx("Normal"));
            Check("M6 Endless: Growth 3.86 > 2.39", P(gE) == "3.86" && P(gN) == "2.39", P(gE) + " vs " + P(gN));
            // M7 Heat vs Elemental
            var c2 = Advise(Find("C2"));
            var four = Synthetic(full: new[] { "Fire", "Ice", "Electric", "Chemical" });
            double heat4 = Sc1(bf(13), 5, four, Ctx("Normal")), elem4 = Sc1(bf(14), 5, four, Ctx("Normal"));
            Check("M7 C2: Heat > Elemental; four elements at 25 % each: Elemental > Heat", c2.RowOf(13).Score > c2.RowOf(14).Score && elem4 > heat4,
                "C2 Heat " + P(c2.RowOf(13).Score) + " / Elemental " + P(c2.RowOf(14).Score) + "; spread Heat " + P(heat4) + " / Elemental " + P(elem4));
            // M8 locked
            bool lockedAdvised = false;
            foreach (var sc in Scenarios()) { var a = Advise(sc); if (a.Picks.Any(p => !p.Forced && p.Level < 1)) lockedAdvised = true; }
            var c1 = Advise(Find("C1")); var b9 = Advise(Find("B9"));
            Check("M8 a locked badge is never advised (every scenario)", !lockedAdvised);
            Check("M8 C1: Gunner only as UNLOCK", !c1.Equip.Contains(16) && c1.Unlock != null && c1.Unlock.Row.Badge.Id == 16, "unlock " + (c1.Unlock == null ? "-" : c1.Unlock.Row.Badge.Short));
            Check("M8 B9: Soul (rank closed) gets no UNLOCK", !b9.Unlocks.Any(u => u.Row.Badge.Id == 10), "unlocks " + string.Join(", ", b9.Unlocks.Select(u => u.Row.Badge.Short)));
            // M9 forced
            var c5sc = Find("C5"); var c5 = Advise(c5sc);
            var c5v = LoadoutView.Of(c5, new[] { 27, 29, 15, 6 }, c5sc.Forced, 4, LoadoutDetail.Full, k);
            Check("M9 C5: Gatherer, Thief forced (Rank 0), the mission reason, never in a swap", c5.Picks[0].Badge.Id == 27 && c5.Picks[1].Badge.Id == 29 && c5.Picks.Take(2).All(p => p.Rank == 0 && p.Why == "the active mission requires it")
                && !c5v.Swaps.Any(s => s.Key == 27 || s.Key == 29 || s.Value == 27 || s.Value == 29) && c5v.MarkOf(27).Kind == MarkKind.Forced, Names(c5) + " | " + c5v.EquippedLine());
            var allForced = Advise(c5sc.With(s => s.Slots = 2));
            Check("M9 forced = slots: no free picks + 'the quest fixes every badge'", allForced.Picks.Count == 2 && allForced.Picks.All(p => p.Forced) && allForced.Notes.Contains("the quest fixes every badge"), string.Join("; ", allForced.Notes));
            var three = Advise(c5sc.With(s => { s.Slots = 2; s.Forced = new[] { 27, 29, 32 }; }));
            Check("M9 3 forced with 2 slots: 2", three.Picks.Count == 2 && three.Picks.All(p => p.Forced), Names(three));
            // M10 prefix property
            var prefixBad = new List<string>();
            foreach (var sc in Scenarios())
            {
                var a4 = Advise(sc, null, 4).Equip;
                foreach (int n in new[] { 2, 3 }) { var an = Advise(sc, null, n).Equip; if (!an.SequenceEqual(a4.Take(an.Count)) || an.Count != Math.Min(n, a4.Count)) prefixBad.Add(sc.Title.Substring(0, 3) + " " + n); }
            }
            Check("M10 prefix: the 2- and 3-slot advice = the first 2 and 3 of the 4-slot advice (every scenario)", prefixBad.Count == 0, string.Join(", ", prefixBad));
            // M11 ties and determinism
            var tie = Fixture(); var power = tie.First(b => b.Id == 20);
            var copy = F(33, 90, "Badge_Tank9_Sprint", "Sprint Badge", "Tank", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[0], new int[0], B("WeaponDamage", "PlayerWeaponDamage", 0.04, ""), B("AbilityDamage", "PlayerAbilityDamage", 0.04, ""));
            tie.Add(copy);
            var tieIn = Input(b1, k); tieIn.Levels[33] = 2;
            var tieA = Loadout.Recommend(tieIn, tie, k, 7);
            int ip = tieA.All.FindIndex(r => r.Badge.Id == 20), ic = tieA.All.FindIndex(r => r.Badge.Id == 33);
            tieIn.Levels[33] = 3; var tieL = Loadout.Recommend(tieIn, tie, k, 7);
            Check("M11 equal scores: the higher level first, then badgeSortOrder, then id", ip < ic && tieL.All.FindIndex(r => r.Badge.Id == 33) < tieL.All.FindIndex(r => r.Badge.Id == 20),
                "Power " + P(tieA.RowOf(20).Score) + " at #" + (ip + 1) + ", its copy (sort 90) at #" + (ic + 1) + "; copy at L3 first: " + (tieL.All.FindIndex(r => r.Badge.Id == 33) < tieL.All.FindIndex(r => r.Badge.Id == 20)));
            var rnd = new Random(1004); string json0 = Loadout.InputJson(Input(b1, k), a1); bool same = true;
            for (int i = 0; i < 50; i++)
            {
                var sh = Fixture().OrderBy(x => rnd.Next()).ToList();
                var inp = Input(b1, k); var levels = inp.Levels.ToList(); inp.Levels.Clear(); foreach (var kv in levels.OrderBy(x => rnd.Next())) inp.Levels[kv.Key] = kv.Value;
                var a = Loadout.Recommend(inp, sh, k, 7);
                if (!a.Equip.SequenceEqual(a1.Equip) || a.Picks.Select(p => p.Score).Zip(a1.Picks.Select(p => p.Score), (x, y) => x != y).Any(d => d) || Loadout.InputJson(inp, a) != json0) same = false;
            }
            Check("M11 50 shuffles of the badges and levels: the same advice, byte-identical input JSON", same);
            // M12 special pull
            var sp = Synthetic(full: new[] { "Kinetic", "Slashing" }); sp.SetProjected("Kinetic", 8);
            var spIn = new LoadoutInput { Leader = "SWAT", Slots = 2, Shape = sp, Ctx = Ctx("Normal") };
            spIn.Levels[16] = 2; spIn.Levels[7] = 2;
            var spA = Loadout.Recommend(spIn, Fixture(), k, 1);
            int specials = spA.Picks.Sum(p => p.Terms.Count(t => t.Kind.StartsWith("special:Kinetic", StringComparison.Ordinal)));
            var gunnerTerms = new List<BadgeTerm>(); Loadout.Score(bf(16), 2, sp, Ctx("Normal"), new Dictionary<string, int> { { "Kinetic", 8 } }, k, gunnerTerms);
            Check("M12 projected 8 + an L2 badge: exactly one special term", gunnerTerms.Count(t => t.Kind.StartsWith("special:", StringComparison.Ordinal)) == 1, string.Join(" | ", gunnerTerms.Select(t => t.ToString())));
            Check("M12 two badges of that type: only the first pick takes the special", spA.Picks.Count == 2 && specials == 1 && spA.Picks[0].Terms.Any(t => t.Kind == "special:Kinetic"), Names(spA));
            // M13 doctrine
            var b10 = Advise(Find("B10"));
            Check("M13 Cautious lifts Tough to #2 (B10)", b10.Picks.Count > 1 && b10.Picks[1].Badge.Id == 18, Names(b10));
            var spread = Advise(Find("C6").With(s => s.TagPlan = "Spread"));
            Check("M13 TagPlan Spread: no special / early term anywhere", spread.Rows.All(r => !r.Terms.Any(t => t.Kind.StartsWith("special:", StringComparison.Ordinal) || t.Kind.StartsWith("early:", StringComparison.Ordinal)))
                && spread.Picks.All(p => !p.Terms.Any(t => t.Kind.StartsWith("special:", StringComparison.Ordinal) || t.Kind.StartsWith("early:", StringComparison.Ordinal))));
            var fire = Advise(b1.With(s => { s.TagPlan = "Fire"; s.Levels = AllAt(5); }));
            Check("M13 TagPlan Fire on SWAT: Heat 0", fire.RowOf(13).Score == 0, "Heat " + P(fire.RowOf(13).Score));
            // M14 the same focus as the run ranker
            var focusBad = new List<string>();
            foreach (var pr in Builds.Presets)
            {
                var a = Advise(new Sc { Leader = pr.Survivor, Preset = pr.Id, Levels = AllAt(5) });
                var prof = a.Input.Shape.ToProfile(); string focus = prof.Focus();
                var top = a.All.FirstOrDefault(r => r.Badge.Kind == BadgeKind.Tag);
                if (focus != null && prof.Share(focus) >= 0.5 && top != null && top.Badge.TagTypes[0] != focus) focusBad.Add(pr.Id + ": focus " + focus + " but " + top.Badge.Short);
            }
            Check("M14 per preset: TagProfile.Focus() = the type of the top single-type badge (share >= 0.5)", focusBad.Count == 0, focusBad.Count == 0 ? Builds.Presets.Count + " presets" : string.Join("; ", focusBad));
            // M15 build packs
            var pack = Builds.ParsePack("{ \"title\": \"Test pack\", \"builds\": [ { \"id\": \"frost\", \"name\": \"Frost Archer\", \"branch\": \"Freezing Arrows\", \"style\": \"Weapon\", \"abilities\": [\"Arrow Rain\", \"Bear Trap\"] } ] }", "bench", "Huntress");
            var frost = pack.Builds[0];
            var frostA = Advise(b1.With(s => { s.Leader = "Huntress"; s.Preset = null; s.Levels = new Dictionary<int, int>(SaveLevels) { [28] = 2 }; }), null, -1, null, frost);
            Check("M15 a pack build on Freezing Arrows: Ice in the profile, Glacier advised when unlocked", frostA.Input.Shape.Share("Ice") > 0 && frostA.Equip.Contains(28), "Ice " + Pct(frostA.Input.Shape.Share("Ice") * 100) + "% | " + Names(frostA));
            // M16 pins / skips
            var pinned = Preset("swat-rifleman").Clone(); pinned.Badges.AddRange(new[] { "Gunner Badge", "20", "Badge_Ghost4_Chance", "Nonexistent" });
            var pinA = Advise(b1, null, -1, null, pinned);
            Check("M16 pins 'Gunner Badge', '20', 'Badge_Ghost4_Chance', 'Nonexistent': Gunner, Power, then 2 scored",
                pinA.Picks.Count == 4 && pinA.Picks[0].Badge.Id == 16 && pinA.Picks[0].Source == "pin" && pinA.Picks[1].Badge.Id == 20 && pinA.Picks[1].Source == "pin" && pinA.Picks.Skip(2).All(p => p.Source == "score"), Names(pinA));
            Check("M16 Chance (locked) never advised + UNLOCK 'pinned but locked'; the unknown pin said once", !pinA.Equip.Contains(5) && pinA.Unlock != null && pinA.Unlock.Row.Badge.Id == 5 && pinA.Unlock.Pinned
                && pinA.Notes.Count(n => n.Contains("Nonexistent")) == 1, Loadout.HintLine(pinA) + " | " + string.Join("; ", pinA.Notes));
            var skipB = Preset("swat-rifleman").Clone(); skipB.SkipBadges.Add("Critical");
            var skipA = Advise(b1, null, -1, null, skipB);
            Check("M16 a skipped badge never appears", !skipA.Equip.Contains(17) && !skipA.Next.Any(r => r.Badge.Id == 17), Names(skipA));
            var many = Preset("swat-rifleman").Clone(); many.Badges.AddRange(new[] { "Gunner", "Power", "Critical", "Tough", "Speed", "Growth" });
            var manyA = Advise(b1, null, -1, null, many);
            Check("M16 more pins than slots: the extra ignored + a note", manyA.Picks.Count == 4 && manyA.Picks.All(p => p.Source == "pin") && manyA.Notes.Any(n => n.Contains("more pins than slots")), string.Join("; ", manyA.Notes));
            var numPack = Builds.ParsePack("{ \"title\": \"Numbers\", \"builds\": [ { \"id\": \"a\", \"name\": \"A\", \"abilities\": [\"Helicopter Strike\"], \"badges\": [16, \"Power\"] }, { \"id\": \"b\", \"name\": \"B\", \"abilities\": [\"Ricochet\"] } ] }", "bench", "SWAT");
            var numA = numPack.Builds.Count == 2 ? Advise(b1, null, -1, null, numPack.Builds[0]) : null;
            Check("M16 a pack with \"badges\": [16, \"Power\"] parses, its other builds survive, 16 = Gunner", numPack.Builds.Count == 2 && numA != null && numA.Picks[0].Badge.Id == 16 && numA.Picks[1].Badge.Id == 20,
                numPack.Builds.Count + " builds, pins " + string.Join(", ", numPack.Builds[0].Badges) + (numA != null ? " | " + Names(numA) : ""));
            // M17 slots 0-5
            var slotBad = new List<string>();
            for (int n = 0; n <= 5; n++)
            {
                var a = Advise(b1, null, n);
                int rated = a.Rows.Count(r => r.Unlocked && r.Rated);
                if (a.Picks.Count != Math.Min(n, rated)) slotBad.Add(n + " slots: " + a.Picks.Count);
                if (n == 0 && (!a.Notes.Contains("no badge slots") || LoadoutView.Of(a, SaveEquipped, new int[0], 0, LoadoutDetail.Full, k).Grid.Any(m => m.Drawn))) slotBad.Add("0 slots: note or marks");
            }
            Check("M17 slots 0-5: EQUIP length = min(slots, rated unlocked); 0 slots: 'no badge slots', no marks", slotBad.Count == 0, string.Join("; ", slotBad));
            var skipForced = Preset("swat-rifleman").Clone(); skipForced.SkipBadges.Add("Gatherer");
            var sfA = Advise(c5sc, null, -1, null, skipForced);
            Check("M17 a forced badge in SkipBadges stays forced", sfA.Picks[0].Badge.Id == 27 && sfA.Picks[0].Forced && sfA.Picks[0].Why2.Contains("never"), sfA.Picks[0].Why2);
            var off = Input(b1, k); off.OffGrid.Add(16);
            Check("M17 a registry badge that is not on the grid is never advised", !Loadout.Recommend(off, Fixture(), k, 7).Equip.Contains(16));
            // M18 fresh profile
            var fresh = Advise(b1.With(s => { s.Levels = new Dictionary<int, int>(); s.Equipped = new int[0]; }));
            Check("M18 every level 0: no advice, 'no badges unlocked yet'", fresh.Picks.Count == 0 && fresh.Notes.Contains("no badges unlocked yet"), string.Join("; ", fresh.Notes));
            // M19 recruits (OD3)
            var b3 = Advise(Find("B3")); var b3a = Advise(Find("B3a"));
            Check("M19 B3 recruitWeight 0.3: Gunner 9.22, Thunder 3.68; 0.0 (the default): Gunner 14.00", P(b3.PickOf(16).Score) == "9.22" && P(b3.RowOf(1).Score) == "3.68" && P(b3a.PickOf(16).Score) == "14.00" && b3a.Input.Shape.Recruits.Count == 0,
                "0.3: " + P(b3.PickOf(16).Score) + " / " + P(b3.RowOf(1).Score) + ", 0.0: " + P(b3a.PickOf(16).Score));
            Check("M19 knowledge default recruitWeight = 0.0 (OD3), cash 0.3 / 0.6 / 1.2 (OD2)", k.BadgeRules.RecruitWeight == 0 && k.BadgeRules.CashByRunGoal.SequenceEqual(new[] { 0.3, 0.6, 1.2 }));
            // M20 (review): every slot fixed by the quest - no UNLOCK hint either (nothing unlocked could be equipped)
            var fq = Advise(b1.With(s => { s.Slots = 2; s.Forced = new[] { 15, 6 }; }));
            Check("M20 every slot forced: no UNLOCK hint, 'the quest fixes every badge'", fq.Unlocks.Count == 0 && fq.Notes.Contains("the quest fixes every badge"),
                Loadout.HintLine(fq) + " | " + string.Join("; ", fq.Notes));
            // M21 (review, F02's wording): a lent build Auto follows is named "Rifleman (Auto)" in the reasons, never "your ... build";
            // the scores do not change
            var pinAuto = Preset("swat-rifleman").Clone(); pinAuto.Badges.Add("Power");
            var inAuto = Input(b1, k, -1, pinAuto); inAuto.Shape.Auto = true;
            var aAuto = Loadout.Recommend(inAuto, Fixture(), k, 7); var aMine = Advise(b1, null, -1, null, pinAuto);
            var vAuto = LoadoutView.Of(aAuto, b1.Equipped, b1.Forced, 4, LoadoutDetail.Full, k);
            var autoTexts = Loadout.WhyLines(aAuto).Concat(aAuto.Picks.Select(p => p.Why2)).Concat(vAuto.Grid.SelectMany(m => new[] { m.Why1, m.Why2 })).ToList();
            var autoJson = Loadout.InputJson(aAuto.Input, aAuto); List<KeyValuePair<int, double>> autoLogged;
            Check("M21 a lent build on Auto: 'Rifleman (Auto) wants crits', 'pinned on Rifleman (Auto)', no 'your ... build'; the same scores; the flag replays",
                autoTexts.Any(t => t.Contains("Rifleman (Auto) wants crits")) && aAuto.Picks.Any(p => p.Why2 == "pinned on Rifleman (Auto)") && !autoTexts.Any(t => t.Contains("your Rifleman") || t.Contains("your build"))
                && Names(aAuto) == Names(aMine) && Loadout.ReadInput(autoJson, out autoLogged).Shape.Auto && !Loadout.InputJson(aMine.Input, aMine).Contains("\"auto\""),
                string.Join(" | ", Loadout.WhyLines(aAuto)));
        }

        // ------------------------------------------------------------------------------------------------ D: data, identity, drift
        static void Data(string root)
        {
            Console.WriteLine("\n=== D data, identity, drift");
            var k = K(); var fx = Fixture();
            string hash = Loadout.Hash(fx);
            Check("D1 Hash(fixture) == Loadout.ReferenceHash", hash == Loadout.ReferenceHash, hash);
            string how;
            var resolved = new[] { "16", "Gunner", "gunner badge", "Badge_SWAT3_Gunner" }.Select(r => { var b = Loadout.Resolve(r, fx, out how); return b == null ? -1 : b.Id; }).ToList();
            var glacier = Loadout.Resolve("Glacier", fx, out how);
            var dup = Fixture(); dup.Add(F(16, 99, "Badge_SWAT9_Gunner", "Gunner Badge", "SWAT", BadgeKind.Tag, "GameplayBadgeHashtagBoost", 3, new[] { "Kinetic" }, new[] { 0, 1, 2 }, B("HashtagKinetic", "", 0.1, "")));
            var warns = new List<string>(); var clean = Loadout.Clean(dup, warns.Add);
            Check("D2 '16', 'Gunner', 'gunner badge', 'Badge_SWAT3_Gunner' -> 16; 'Glacier' -> 28; a duplicate id 16 -> the lower sort kept + one warning",
                resolved.All(x => x == 16) && glacier != null && glacier.Id == 28 && clean.Count(b => b.Id == 16) == 1 && clean.First(b => b.Id == 16).Sort == 2 && warns.Count == 1, string.Join(", ", resolved) + " | " + (glacier == null ? "-" : glacier.Id.ToString(IC)) + " | " + string.Join("; ", warns));
            var misses = fx.SelectMany(b => b.Bonuses).Select(x => x.Stat).Where(s => !s.StartsWith("Hashtag", StringComparison.Ordinal) && !k.BadgeStats.ContainsKey(s)).Distinct().ToList();
            var healTerms = new List<BadgeTerm>(); Sc1(fx.First(b => b.Id == 11), 1, Synthetic(), Ctx("Normal"), k, healTerms);
            Check("D3 every stat of the fixture has a weight; Healing's HealthBonusesMod term > 0", misses.Count == 0 && healTerms.Any(t => t.Stat == "HealthBonusesMod" && t.Points > 0), misses.Count == 0 ? "no misses" : string.Join(", ", misses));
            Check("D3 code defaults = knowledge.json defaults (badgeStats, badgeModes, badgeRules)", Loadout.KnowledgeIsDefault(k) && new Knowledge().BadgeStats.All(kv => kv.Value.Why == k.BadgeStats[kv.Key].Why), k.BadgeStats.Count + " stats, " + k.BadgeModes.Count + " modes");
            // D4 value drift
            var drift = Fixture(); var boss = drift.First(b => b.Id == 8); boss.Bonuses[0].PerLevel = 0.04;
            var t1 = new List<BadgeTerm>(); var t2 = new List<BadgeTerm>();
            Sc1(fx.First(b => b.Id == 8), 1, Synthetic(), Ctx("Normal"), k, t1); Sc1(boss, 1, Synthetic(), Ctx("Normal"), k, t2);
            double ratio = t1.First(t => t.Stat == "DamageToElites").Points / t2.First(t => t.Stat == "DamageToElites").Points;
            Check("D4 Boss elites 0.04 vs 0.10: the elite term x2.5, the hash differs, the drift is said", Math.Abs(ratio - 2.5) < 1e-9 && Loadout.Hash(drift) != hash && Loadout.Drift(fx, drift).Contains("8 Boss DamageToElites 0.1 -> 0.04"), Loadout.Drift(fx, drift));
            // D5 unknown stat
            var unk = Fixture(); unk.Add(F(33, 90, "Badge_Ranger9_Sprint", "Sprint Badge", "Ranger", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[0], new int[0], B("Foo", "Foo", 0.1, "")));
            var unIn = Input(Find("B1"), k); unIn.Levels[33] = 5;
            var unA = Loadout.Recommend(unIn, unk, k, 7);
            Check("D5 a badge with only an unknown stat: not rated (never in EQUIP / next), WHY 'not rated'", !unA.Equip.Contains(33) && !unA.Next.Any(r => r.Badge.Id == 33) && unA.Unrated.Any(r => r.Badge.Id == 33)
                && Loadout.WhyOf(unA.RowOf(33), unA, k).StartsWith("not rated", StringComparison.Ordinal));
            var est = Fixture(); est.Add(F(33, 90, "Badge_Ranger9_Sprint", "Sprint Badge", "Ranger", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[0], new int[0], B("WeaponDamage", "", 0.04, ""), B("Foo", "Foo", 0.1, "")));
            var estA = Loadout.Recommend(unIn, est, k, 7); var estRow = estA.RowOf(33);
            Check("D5 WeaponDamage + an unknown stat: rated, estimated, the unknown = 1.0 per level, one line", estRow.Rated && estRow.Estimated && estRow.Terms.Any(t => t.Stat == "Foo" && t.Points == 5.0) && estA.Estimated.Count == 1, string.Join("; ", estA.Estimated));
            // D6 unknown kind
            var kind = Fixture(); kind.Add(F(33, 90, "Badge_Ranger9_Odd", "Odd Badge", "Ranger", BadgeKind.Unknown, "GameplayBadgeXyz", 4, new string[0], new int[0], B("WeaponDamage", "", 0.04, "")));
            var kindA = Loadout.Recommend(unIn, kind, k, 7);
            Check("D6 a badge of an unknown class: not rated", !kindA.RowOf(33).Rated && !kindA.Equip.Contains(33));
            // D7 new rated badge
            var nw = Fixture(); nw.Add(F(33, 14, "Badge_Tank4_Sprint", "Sprint Badge", "Tank", BadgeKind.Stat, "GameplayBadgeStatBoost", 4, new string[0], new int[0], B("WeaponDamage", "", 0.04, ""), B("AbilityDamage", "", 0.04, "")));
            var nwIn = Input(Find("C2"), k); nwIn.Levels[33] = 5;
            var nwA = Loadout.Recommend(nwIn, nw, k, 7);
            int iPow = nwA.All.FindIndex(r => r.Badge.Id == 20), iNew = nwA.All.FindIndex(r => r.Badge.Id == 33);
            Check("D7 a new badge 33 = a copy of Power: can be advised; the tie goes by sort order, then id", nwA.RowOf(33).Rated && iPow + 1 == iNew, "Power #" + (iPow + 1) + ", the copy #" + (iNew + 1));
            // D8 renamed asset
            var ren = Fixture(); var gun = ren.First(b => b.Id == 16); gun.Asset = "Badge_SWAT3_Sharpshooter"; gun.Short = Loadout.ShortName(gun.Asset);
            var kn = K(); kn.BadgeNotes["Gunner"] = new BadgeNote { Bias = 5, Note = "my favourite" };
            var renTerms = new List<BadgeTerm>(); Sc1(gun, 2, Synthetic(), Ctx("Normal"), kn, renTerms);
            var kw = Loadout.KnowledgeWarnings(ren, kn);
            Check("D8 a renamed asset under id 16: the knowledge entry 'Gunner' is not applied, the warning text produced", !renTerms.Any(t => t.Kind == "bias") && kw.Count == 1 && kw[0].Contains("'Gunner' matches no badge"), string.Join("; ", kw));
            // D9 clamp
            var cl = F(34, 91, "Badge_Pyro9_Clamp", "Clamp Badge", "Pyro", BadgeKind.Tag, "GameplayBadgeHashtagBoost", 3, new[] { "Fire" }, new[] { 0, 1, 2 }, B("HashtagFire", "", 0.1, ""));
            bool clampOk = cl.PointsAt(9) == 2; try { Sc1(cl, 9, Synthetic(), Ctx("Normal"), k); } catch { clampOk = false; }
            Check("D9 level 9 with a 3-entry tag table: the last entry, no exception", clampOk);
            // D10 unknown mode
            LoadoutCtx g = null; LoadoutAdvice ga = null;
            try { g = Ctx("Gauntlet"); ga = Advise(Find("B1").With(s => s.Mode = "Gauntlet")); } catch (Exception e) { Console.WriteLine("    " + e); }
            Check("D10 unknown mode 'Gauntlet': open-ended curves, a note, no exception", g != null && g.Kind == ModeKind.Open && g.Note.Length > 0 && ga != null && ga.Notes.Any(n => n.Contains("Gauntlet")), g == null ? "-" : g.Text() + " | " + g.Note);
            var orderBad = fx.Where(b => !Loadout.TagTypesOf(b.Kind, b.Bonuses.Select(x => x.Stat), b.Kind == BadgeKind.Tag ? b.TagTypes.FirstOrDefault() : null).SequenceEqual(b.TagTypes)).Select(b => b.Short).ToList();
            var shuffledTags = Fixture(); shuffledTags.First(b => b.Id == 14).TagTypes.Reverse();
            Check("D12 the live reader's tag order (Loadout.TagTypesOf from the bonus stats) = the fixture's; the hash ignores the order", orderBad.Count == 0 && Loadout.Hash(shuffledTags) == hash, string.Join(", ", orderBad));
            // D11 against catalogue.json
            string cat = Path.Combine(root, "research", "badges_1004", "catalogue.json");
            if (!File.Exists(cat)) { Console.WriteLine("  (D11 skipped: no " + cat + ")"); return; }
            var problems = new List<string>(); int rows = 0;
            var kinds = new Dictionary<string, BadgeKind> { { "GameplayBadgeStatBoost", BadgeKind.Stat }, { "GameplayBadgeHashtagBoost", BadgeKind.Tag }, { "GameplayBadgeHashtagPhysicalBoost", BadgeKind.Physical }, { "GameplayBadgeHashtagElementalBoost", BadgeKind.Elemental } };
            using (var doc = JsonDocument.Parse(File.ReadAllText(cat)))
                foreach (var e in doc.RootElement.GetProperty("badges").EnumerateArray())
                {
                    rows++;
                    int id = e.GetProperty("id").GetInt32(); var b = fx.FirstOrDefault(x => x.Id == id);
                    if (b == null) { problems.Add(id + " missing"); continue; }
                    if (b.Sort != e.GetProperty("sortOrder").GetInt32()) problems.Add(id + " sort");
                    if (b.Owner != e.GetProperty("badgeClass").GetString()) problems.Add(id + " owner");
                    if (b.Kind != kinds[e.GetProperty("class").GetString()]) problems.Add(id + " kind");
                    if (b.Short + " Badge" != e.GetProperty("name").GetString()) problems.Add(id + " name " + b.Short);
                    var bon = e.GetProperty("bonuses").EnumerateArray().ToList();
                    if (bon.Count != b.Bonuses.Count) { problems.Add(id + " bonuses"); continue; }
                    for (int i = 0; i < bon.Count; i++)
                    {
                        if (Loadout.StatKey(bon[i].GetProperty("stat").GetString()) != b.Bonuses[i].Stat) problems.Add(id + " stat " + i);
                        if (bon[i].GetProperty("perLevel").GetDouble() != b.Bonuses[i].PerLevel) problems.Add(id + " value " + i);
                        var st = bon[i].GetProperty("statType");
                        if (st.ValueKind == JsonValueKind.Number && !b.Bonuses[i].Type.StartsWith("InternalInstant", StringComparison.Ordinal)) problems.Add(id + " EType " + st.GetRawText() + " unresolved");
                    }
                }
            Check("D11 the fixture against catalogue.json: 27 rows, ids, sort orders, owners, kinds, names, stats; 74 / 75 resolved to names", rows == 27 && problems.Count == 0, rows + " rows" + (problems.Count > 0 ? ": " + string.Join(", ", problems) : ""));
        }

        // ------------------------------------------------------------------------------------------------ V: the view and its texts
        static void View()
        {
            Console.WriteLine("\n=== V the view and its texts");
            var k = K();
            // V1 truth table
            var table = new[]
            {
                Tuple.Create("advised, equipped -> Keep", LoadoutView.StateOf(true, true, false, false, false, false, false, LoadoutDetail.Numbers), MarkKind.Keep),
                Tuple.Create("advised, not equipped -> Equip", LoadoutView.StateOf(true, false, false, false, false, false, false, LoadoutDetail.Numbers), MarkKind.Equip),
                Tuple.Create("equipped, not advised (swap) -> SwapOut", LoadoutView.StateOf(false, true, false, false, false, false, false, LoadoutDetail.Numbers), MarkKind.SwapOut),
                Tuple.Create("equipped, not advised, under the margin -> KeepClose", LoadoutView.StateOf(false, true, false, false, false, true, false, LoadoutDetail.Numbers), MarkKind.KeepClose),
                Tuple.Create("forced -> Forced", LoadoutView.StateOf(false, true, true, false, false, false, false, LoadoutDetail.Numbers), MarkKind.Forced),
                Tuple.Create("runner-up within the margin, Full -> Close", LoadoutView.StateOf(false, false, false, false, false, false, true, LoadoutDetail.Full), MarkKind.Close),
                Tuple.Create("runner-up within the margin, NumbersAndReason -> None", LoadoutView.StateOf(false, false, false, false, false, false, true, LoadoutDetail.NumbersAndReason), MarkKind.None),
                Tuple.Create("pinned (advised) -> Pinned", LoadoutView.StateOf(true, false, false, false, true, false, false, LoadoutDetail.Numbers), MarkKind.Pinned),
                Tuple.Create("locked -> None", LoadoutView.StateOf(false, false, false, true, false, false, false, LoadoutDetail.Full), MarkKind.None),
                Tuple.Create("neither -> None", LoadoutView.StateOf(false, false, false, false, false, false, false, LoadoutDetail.Full), MarkKind.None),
                Tuple.Create("detail Off -> None", LoadoutView.StateOf(true, false, false, false, false, false, false, LoadoutDetail.Off), MarkKind.None),
            };
            Check("V1 the truth table of SPEC 2.1", table.All(t => t.Item2 == t.Item3), string.Join("; ", table.Where(t => t.Item2 != t.Item3).Select(t => t.Item1 + " gave " + t.Item2)));
            bool lockedAdvised = false;
            foreach (var sc in Scenarios()) { var a = Advise(sc); var v = LoadoutView.Of(a, sc.Equipped, sc.Forced, sc.Slots, LoadoutDetail.Full, k); if (a.Picks.Any(p => !p.Forced && a.RowOf(p.Badge.Id).Level < 1) || v.Grid.Any(m => m.Locked && m.Drawn)) lockedAdvised = true; }
            Check("V1 locked + advised never occurs (every scenario), no mark on a locked badge", !lockedAdvised);
            // V2 numbers
            var numBad = new List<string>();
            foreach (var sc in Scenarios())
            {
                var a = Advise(sc); var v = LoadoutView.Of(a, sc.Equipped, sc.Forced, sc.Slots, LoadoutDetail.Numbers, k);
                var nums = v.Grid.Where(m => m.Number > 0).Select(m => m.Number).OrderBy(x => x).ToList();
                int n = a.Picks.Count(p => !p.Forced);
                if (!nums.SequenceEqual(Enumerable.Range(1, n))) numBad.Add(sc.Title.Substring(0, 3) + " " + string.Join(",", nums));
                if (a.Picks.Where(p => !p.Forced).Select((p, i) => p.Rank != i + 1).Any(x => x)) numBad.Add(sc.Title.Substring(0, 3) + " ranks");
                if (v.Grid.Any(m => v.Forced.Contains(m.Id) && (m.Kind != MarkKind.Forced || m.Tag != "QUEST"))) numBad.Add(sc.Title.Substring(0, 3) + " quest");
            }
            Check("V2 numbers 1..(slots - forced), contiguous, in the advice order; forced = QUEST", numBad.Count == 0, string.Join("; ", numBad));
            // V3 slot mirror + V4 swaps (B1)
            var b1 = Find("B1"); var a1 = Advise(b1); var v1 = LoadoutView.Of(a1, b1.Equipped, b1.Forced, 4, LoadoutDetail.Full, k);
            Check("V3 slot mirror (B1): all four swapped out -> rust", v1.Slots.Count == 4 && v1.Slots.All(m => m.Kind == MarkKind.SwapOut), string.Join(",", v1.Slots));
            string swaps = string.Join(", ", v1.Swaps.Select(s => a1.RowOf(s.Key).Badge.Short + ">" + a1.RowOf(s.Value).Badge.Short));
            Check("V4 B1: 4 swaps in the order of the log line", swaps == "Thunder>Gunner, Leveling>Critical, Growth>Power, Speed>Tough", v1.EquippedLine());
            var matched = LoadoutView.Of(a1, a1.Equip, new int[0], 4, LoadoutDetail.Full, k);
            Check("V4 equipped = the advice: Matches, no swaps, no frames, '4 of 4 as advised'", matched.Matches && matched.Swaps.Count == 0 && !matched.Grid.Any(m => m.Frame) && matched.EquippedLine().Contains("4 of 4 as advised")
                && matched.Slots.All(m => m.Kind == MarkKind.Keep && m.Number > 0) && matched.Rows.Count > 1 && matched.Rows[1].Kind == "match", matched.EquippedLine());
            var b3a = Advise(Find("B3a")); var close = LoadoutView.Of(b3a, new[] { 16, 20, 17, 7 }, new int[0], 4, LoadoutDetail.Full, k);
            var physical = close.MarkOf(7); var tough = close.MarkOf(18);
            Check("V4 gain under max(1.0, 15 %): KeepClose (with the advised badge's number) + Close, no frame",
                close.Kept.Count == 1 && physical.Kind == MarkKind.KeepClose && physical.Number == b3a.PickOf(18).Rank && tough.Kind == MarkKind.Close && !tough.Frame && close.Swaps.Count == 0 && close.Plan.Count == 0,
                close.EquippedLine() + " | " + close.DrawnMarks());
            // V5 the EQUIP plan
            Func<int, bool> unlocked = id => a1.RowOf(id).Unlocked;
            var plan = v1.Plan; var applied = LoadoutView.Apply(b1.Equipped, plan, new int[0], unlocked, 4);
            bool removesFirst = plan.SkipWhile(s => !s.Add).All(s => s.Add);
            var back = LoadoutView.Apply(applied, v1.Undo, new int[0], unlocked, 4);
            bool prefixes = true;
            for (int i = 0; i <= plan.Count; i++) { var cur = LoadoutView.Apply(b1.Equipped, plan.Take(i), new int[0], unlocked, 4); if (cur.Count > 4 || cur.Any(id => !unlocked(id))) prefixes = false; }
            Check("V5 B1 [15,6,9,1]: 8 clicks, removes first, the result = the advice, plan + UNDO = the original, every prefix valid",
                plan.Count == 8 && removesFirst && new HashSet<int>(applied).SetEquals(a1.Equip) && new HashSet<int>(back).SetEquals(b1.Equipped) && prefixes, string.Join(" ", plan) + " | undo " + string.Join(" ", v1.Undo));
            Check("V5 matched: an empty plan", matched.Plan.Count == 0);
            var c5sc = Find("C5"); var c5 = Advise(c5sc); var c5v = LoadoutView.Of(c5, new[] { 27, 15, 29, 6 }, c5sc.Forced, 4, LoadoutDetail.Full, k);
            var c5after = LoadoutView.Apply(c5v.Equipped, c5v.Plan, c5v.Forced, id => c5.RowOf(id).Unlocked, 4);
            Check("V5 never a forced badge, never past the slots (C5 with the quest's two badges)", !c5v.Plan.Any(s => s.Id == 27 || s.Id == 29) && c5after.Contains(27) && c5after.Contains(29) && c5after.Count <= 4 && new HashSet<int>(c5after).SetEquals(c5.Equip), string.Join(" ", c5v.Plan));
            var lockedTarget = LoadoutView.EquipPlan(new[] { 15 }, new[] { 15, 5 }, new int[0], unlocked, 4);
            var fullSlots = LoadoutView.EquipPlan(new[] { 15, 6 }, new[] { 15, 6, 16, 17, 20 }, new int[0], unlocked, 3);
            Check("V5 never adds a locked badge (Chance L0), never past the slots", lockedTarget.Count == 0 && fullSlots.Count == 1, string.Join(" ", lockedTarget) + " | " + string.Join(" ", fullSlots));
            // the one-click button's run (OD1): press by press against the live selection, a click by hand stops it, UNDO restores
            var run = LoadoutView.RunOf(v1, unlocked); var live = b1.Equipped.ToList(); var pressed = new List<string>();
            for (var st = run.Next(live); st != null; st = run.Next(live)) { live = LoadoutView.Apply(live, new[] { st }, new int[0], unlocked, 4); run.Pressed(live); pressed.Add(st.ToString()); }
            var undoRun = run.Undo(); var live2 = live.ToList();
            for (var st = undoRun.Next(live2); st != null; st = undoRun.Next(live2)) { live2 = LoadoutView.Apply(live2, new[] { st }, new int[0], unlocked, 4); undoRun.Pressed(live2); }
            Check("V5 the EQUIP run: 8 presses to the advice; its UNDO back to the save's set", run.Finished && pressed.Count == 8 && new HashSet<int>(live).SetEquals(a1.Equip) && undoRun.Finished && new HashSet<int>(live2).SetEquals(b1.Equipped),
                string.Join(" ", pressed) + " | undo " + string.Join(" ", undoRun.Steps));
            var hand = LoadoutView.RunOf(v1, unlocked); var lv = b1.Equipped.ToList();
            for (int n = 0; n < 3; n++) { var st = hand.Next(lv); lv = LoadoutView.Apply(lv, new[] { st }, new int[0], unlocked, 4); hand.Pressed(lv); }
            lv.Add(12);                                                                         // the player clicks Gamble in between
            var stop = hand.Next(lv); var partUndo = hand.Undo();
            Check("V5 a click by hand mid-run stops it, and the UNDO takes back only the presses made", stop == null && hand.Stopped.Contains("by hand") && partUndo.Steps.Count == 3 && partUndo.Steps.All(x => x.Add),
                hand.Stopped + " | undo " + string.Join(" ", partUndo.Steps));
            var forcedRun = new EquipRun(new[] { 27, 15 }, new[] { new EquipStep { Id = 27, Add = false } }, new[] { 27 }, unlocked, 4);
            Check("V5 a press that would remove a forced badge is refused", forcedRun.Next(new[] { 27, 15 }) == null && forcedRun.Stopped.Contains("forced"), forcedRun.Stopped);
            // V6 texts
            var textBad = new List<string>(); int texts = 0;
            string[] classes = Builds.Survivors; string[] spelling = { "abilitys", "recieve", "seperate", "occured", "dammage", "untill", "teh " };
            foreach (int level in new[] { 1, 5 })
                foreach (var sc in Scenarios())
                    foreach (string sep in new[] { "·", "-" })
                    {
                        var s = sc.With(x => x.Levels = AllAt(level));
                        var a = Advise(s); var v = LoadoutView.Of(a, SaveEquipped, s.Forced, s.Slots, LoadoutDetail.Full, k, null, null, sep);
                        foreach (var p in a.Picks) { texts++; if (p.Effect.Length > 36) textBad.Add("effect " + p.Effect.Length + ": " + p.Effect); if (p.Why.Length > 48) textBad.Add("why " + p.Why.Length + ": " + p.Why); }
                        foreach (var r in a.Rows) { var w = Loadout.WhyOf(r, a, k); if (w.Length > 48) textBad.Add("why " + w); var e = Loadout.Effect(r.Badge, Math.Max(1, r.Level)); if (e.Length > 36) textBad.Add("effect " + e); }
                        var all = new List<string>();
                        foreach (var m in v.Grid) { texts++; all.Add(m.Why1); all.Add(m.Why2); if (LoadoutView.Visible(m.Why1).Length > 60) textBad.Add("WHY " + LoadoutView.Visible(m.Why1).Length + ": " + LoadoutView.Visible(m.Why1)); }
                        foreach (var r in v.Rows) { all.Add(r.Text()); if (r.Text().Length > 64) textBad.Add("row " + r.Text().Length + ": " + r.Text()); }
                        all.AddRange(a.Picks.Select(p => p.Effect + " " + p.Why));
                        foreach (var t in all)
                        {
                            if (t.Contains("›") || t.Contains("—")) textBad.Add("glyph: " + t);
                            if (sep == "-" && t.Contains("·")) textBad.Add("'·' with the fallback: " + t);
                            if (spelling.Any(x => t.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) textBad.Add("spelling: " + t);
                            if (Regex.IsMatch(t, @"\d,\d")) textBad.Add("decimal comma: " + t);
                        }
                        foreach (var m in v.Grid)
                        {
                            var row = a.RowOf(m.Id);
                            foreach (var cls in classes)
                                if (Regex.IsMatch(m.Why1 + " " + m.Why2, @"\b" + cls + @"\b") && cls != s.Leader && cls != row.Badge.Owner) textBad.Add("class " + cls + " in the WHY of " + row.Badge.Short + ": " + m.Why2);
                            // (review) the dim line 2 never repeats line 1's reason (it read "never advised: <the same reason>")
                            string reason = LoadoutView.Visible(m.Why1); int cut = reason.IndexOf("  ", StringComparison.Ordinal); reason = cut >= 0 ? reason.Substring(cut + 2) : "";
                            if (reason.Length > 12 && m.Why2.Contains(reason)) textBad.Add("line 2 repeats line 1 for " + row.Badge.Short + ": " + m.Why2);
                        }
                    }
            Check("V6 texts at levels 1 and 5, every scenario, both separators: effect <= 36, why <= 48, WHY <= 60 visible, rows <= 64, no stray class, glyphs, spelling, decimals, line 2 never repeats line 1",
                textBad.Count == 0, texts + " texts" + (textBad.Count > 0 ? ": " + string.Join(" || ", textBad.Distinct().Take(8)) : ""));
            // V7 detail levels
            var vOff = LoadoutView.Of(a1, b1.Equipped, b1.Forced, 4, LoadoutDetail.Off, k);
            var vNum = LoadoutView.Of(a1, b1.Equipped, b1.Forced, 4, LoadoutDetail.Numbers, k);
            var vNR = LoadoutView.Of(b3a, new[] { 16, 20, 17, 7 }, new int[0], 4, LoadoutDetail.NumbersAndReason, k);
            var vFull = close;
            Check("V7 Off: no marks (the log line still has the swaps)", !vOff.Grid.Any(m => m.Drawn) && vOff.Rows.Count == 0 && vOff.Swaps.Count == 4 && vOff.DrawnMarks().StartsWith("marks - ", StringComparison.Ordinal));
            Check("V7 Numbers: marks only (no WHY, no rows)", vNum.Grid.Any(m => m.Drawn) && vNum.Grid.All(m => m.Why1.Length == 0) && vNum.Rows.Count == 0);
            Check("V7 NumbersAndReason: + WHY + rows 1-2, no CLOSE CALL marks", vNR.Grid.All(m => m.Why1.Length > 0) && vNR.Rows.Count == 2 && !vNR.Grid.Any(m => m.Kind == MarkKind.Close), string.Join(" / ", vNR.Rows));
            Check("V7 Full: + CLOSE CALL marks + row 3", vFull.Grid.Any(m => m.Kind == MarkKind.Close) && vFull.Rows.Count == 3, string.Join(" / ", vFull.Rows));
            Console.WriteLine("    B1 as drawn (NumbersAndReason): " + string.Join(" / ", LoadoutView.Of(a1, b1.Equipped, b1.Forced, 4, LoadoutDetail.NumbersAndReason, k).Rows));
            var vb1 = LoadoutView.Of(a1, b1.Equipped, b1.Forced, 4, LoadoutDetail.Full, k);
            foreach (int id in new[] { 16, 1, 5, 7 }) { var m = vb1.MarkOf(id); Console.WriteLine("    WHY " + a1.RowOf(id).Badge.Short.PadRight(9) + LoadoutView.Visible(m.Why1) + "  |  " + m.Why2); }
            Console.WriteLine("    " + Loadout.AdviseLine(a1)); Console.WriteLine("    " + Loadout.ShapeLine(a1)); Console.WriteLine("    " + Loadout.HintLine(a1)); Console.WriteLine("    " + vb1.EquippedLine()); Console.WriteLine("    " + vb1.DrawnMarks());
            foreach (var l in Loadout.WhyLines(a1).Concat(Loadout.TermLines(a1))) Console.WriteLine("    " + l);
        }

        // ------------------------------------------------------------------------------------------------ L: placement
        static LayoutIn Mock(float screenH, R4 canvas, int badges = 27, float size = 1, LoadoutDetail detail = LoadoutDetail.NumbersAndReason, int rows = 2)
        {
            var i = new LayoutIn { View = new R4(0, 0, 3840, 2160), Canvas = canvas, ScreenH = screenH, UnitPx = screenH / canvas.H, Size = size, Detail = detail, SumRows = rows };
            i.Grid = Enumerable.Range(0, badges).Select(n => R4.Box(438 + 200 * (n % 8), 445 + 200 * (n / 8), 143, 143)).ToArray();
            i.Slots = Enumerable.Range(0, 4).Select(n => R4.Box(2150 + 235 * n, 405, 186, 187)).ToArray();
            i.Difficulty = Enumerable.Range(0, 3).Select(n => R4.Box(2150 + 235 * n, 915, 198, 195)).ToArray();
            i.Name = new R4(1008, 1323, 1408, 1375); i.Desc = new R4(850, 1385, 1566, 1440); i.Bonus = new R4(900, 1445, 1516, 1515);
            i.StartBar = new R4(200, 1805, 3840, 2160);
            i.Obstacles = new[] { new R4(1500, 129, 2340, 226), new R4(420, 1250, 1995, 1254), new R4(2152, 300, 2560, 352), new R4(2152, 817, 2560, 870), new R4(2108, 1207, 3240, 1650), new R4(1300, 1730, 2540, 1780) };
            i.Containers = new[] { new R4(393, 297, 2025, 1642) };
            return i;
        }

        // the rects the game really measured on 2026-10-04 (game 1.0.2; '[loadout] layout' lines of companion_native.log and
        // companion_deck.log): the view is the whole canvas (PC 5161x2160 at 3440x1440, the Deck 3840x2400 at 1280x800, the
        // content 660 units right of the 3840-wide table of SPEC 0 on the PC, 120 down on the Deck), the grid buttons are the
        // 200-unit cells around the 143-unit icons, the slot rects 268 units tall, and the info labels empty (no badge
        // highlighted yet)
        static LayoutIn MockReal(bool deck)
        {
            float dx = deck ? 0f : 660f, dy = deck ? 120f : 0f;
            var view = deck ? new R4(0, 0, 3840, 2400) : new R4(0, 0, 5161, 2160);
            var i = new LayoutIn { View = view, Canvas = view, ScreenH = deck ? 800 : 1440, Detail = LoadoutDetail.NumbersAndReason, SumRows = 2 };
            i.UnitPx = i.ScreenH / view.H;
            i.Grid = Enumerable.Range(0, 27).Select(n => R4.Box(409.5f + dx + 200 * (n % 8), 416.5f + dy + 200 * (n / 8), 200, 200)).ToArray();
            i.Slots = Enumerable.Range(0, 4).Select(n => R4.Box(2127 + dx + 235 * n, 393 + dy, 271, 268)).ToArray();
            i.Difficulty = Enumerable.Range(0, 3).Select(n => R4.Box(2122 + dx + 235 * n, 904 + dy, 275, 278)).ToArray();
            i.StartBar = new R4(2689 + dx, 1641 + dy, 3277 + dx, 2229 + dy);
            i.Obstacles = new[] { new R4(1500, 129, 2340, 226), new R4(420, 1250, 1995, 1254), new R4(2152, 300, 2560, 352), new R4(2152, 801, 2560, 854), new R4(2108, 1207, 3240, 1650), new R4(1300, 1730, 2540, 1780) }
                .Select(r => new R4(r.X0 + dx, r.Y0 + dy, r.X1 + dx, r.Y1 + dy)).ToArray();
            i.Containers = new[] { new R4(393 + dx, 297 + dy, 2025 + dx, 1642 + dy) };
            return i;
        }

        static void L7(string label, LayoutIn i, LayoutOut o, List<string> bad)
        {
            var blocked = LoadoutLayout.Blocked(i);
            foreach (var r in new[] { o.WhyAt == "inline" || o.WhyAt == "none" ? new R4() : o.Why, o.SumAt == "none" ? new R4() : o.Sum })
            {
                if (r.Empty) continue;
                if (blocked.Any(b => b.Overlaps(r))) bad.Add(label + ": " + r + " overlaps " + blocked.First(b => b.Overlaps(r)));
                if (!i.Canvas.Contains(r)) bad.Add(label + ": " + r + " leaves the canvas");
                // a frame = a container that is neither the slot row's own backing nor a backing that hugs the grid's cells
                var grid = R4.Union(i.Grid);
                Func<R4, bool> frame = c => !i.Slots.Any(s => c.Contains(s)) && (!c.Contains(grid) || c.Inset(LoadoutLayout.FrameInset).Contains(grid));
                foreach (var c in i.Containers) if (frame(c) && c.Contains(r.X0 + 1, r.Y0 + 1) && !c.Inset(LoadoutLayout.FrameInset).Contains(r)) bad.Add(label + ": " + r + " crosses the frame's inset edge");
            }
        }

        static void Layout()
        {
            Console.WriteLine("\n=== L placement (LoadoutLayout.Choose)");
            var l7 = new List<string>();
            Func<LayoutOut, string> say = o => LoadoutLayout.Drawn(o, o.FontPx / Math.Max(0.001f, o.FontUnits)) +" | sum font " + o.SumFontUnits.ToString("0.0", IC) + " u" + (o.Dropped.Count > 0 ? " | dropped " + string.Join(", ", o.Dropped) : "") + (o.Note.Length > 0 ? " | " + o.Note : "");
            // L1 PC 3440x1440
            var pc = Mock(1440, new R4(-660, 0, 4500, 2160), rows: 3, detail: LoadoutDetail.Full); var opc = LoadoutLayout.Choose(pc); L7("PC", pc, opc, l7);
            Check("L1 PC 3440x1440: WHY grid-gap 2 lines at 40.5 u; summary under-slots 3 rows; marker 57 u, number >= 13 px; WHY >= 15 px",
                opc.WhyAt == "grid-gap" && opc.WhyLines == 2 && Math.Abs(opc.WhyFontUnits - 40.5) < 0.05 && opc.SumAt == "under-slots" && opc.SumRows == 3 && Math.Round(opc.MarkerUnits) == 57
                && opc.NumberUnits * pc.UnitPx >= 13 && opc.WhyFontUnits * pc.UnitPx >= 15 - 1e-3 && opc.Why.Size == "943x143" && opc.Sum.Size == "1090x205", say(opc) + " | bands " + opc.Bands);
            // L2 Deck 1280x800
            var deck = Mock(800, new R4(0, -120, 3840, 2280), rows: 3, detail: LoadoutDetail.Full); var odeck = LoadoutLayout.Choose(deck); L7("Deck", deck, odeck, l7);
            Check("L2 Deck 1280x800: WHY grid-gap 2 lines at 45 u; summary under-slots rows 1-2 at 15 px, Full's row 3 dropped (not shrunk); marker 75 u = 25 px, number 13 px",
                odeck.WhyAt == "grid-gap" && odeck.WhyLines == 2 && Math.Abs(odeck.WhyFontUnits - 45) < 0.05 && odeck.SumAt == "under-slots" && odeck.SumRows == 2 && Math.Abs(odeck.SumFontUnits * deck.UnitPx - 15) < 0.01
                && odeck.Dropped.Contains("yard") && Math.Round(odeck.MarkerUnits) == 75 && Math.Round(odeck.MarkerUnits * deck.UnitPx) == 25 && Math.Round(odeck.NumberUnits * deck.UnitPx) == 13, say(odeck));
            // L3 1920x1080
            var hd = Mock(1080, new R4(0, 0, 3840, 2160)); var ohd = LoadoutLayout.Choose(hd); L7("1080p", hd, ohd, l7);
            Check("L3 1920x1080: as L1, in the same view bands", ohd.WhyAt == "grid-gap" && ohd.WhyLines == 2 && ohd.SumAt == "under-slots" && ohd.SumRows == 2 && Math.Abs(ohd.FontUnits - 40.5) < 0.05, say(ohd));
            // L4 crowded
            var crowd = Mock(1440, new R4(-660, 0, 4500, 2160), 30); var ocrowd = LoadoutLayout.Choose(crowd); L7("crowded", crowd, ocrowd, l7);
            Check("L4 crowded (30 badges, a 2-cell gap): WHY under-info / over-name / inline; the summary unchanged", (ocrowd.WhyAt == "under-info" || ocrowd.WhyAt == "over-name" || ocrowd.WhyAt == "inline") && ocrowd.SumAt == "under-slots" && ocrowd.SumRows == 2, say(ocrowd) + " | bands " + ocrowd.Bands);
            // L5 no room
            var none = Mock(1440, new R4(0, 0, 3840, 2160), 30);
            none.Obstacles = none.Obstacles.Concat(new[] { new R4(420, 1525, 2000, 1540), new R4(420, 1290, 2000, 1310), new R4(2150, 600, 3240, 640) }).ToArray();
            var onone = LoadoutLayout.Choose(none); L7("no room", none, onone, l7);
            Check("L5 no room: WHY inline; summary none + a note", onone.WhyAt == "inline" && onone.SumAt == "none" && onone.Note.Contains("row 1"), say(onone));
            // L6 LoadoutSize 2.0
            var big = Mock(1440, new R4(-660, 0, 4500, 2160), size: 2.0f, rows: 3, detail: LoadoutDetail.Full); var obig = LoadoutLayout.Choose(big); L7("size 2.0", big, obig, l7);
            Check("L6 LoadoutSize 2.0: shrink, then YARD and SWAP dropped; markers capped at 0.60 w", obig.SumRows == 1 && obig.Dropped.Contains("yard") && obig.Dropped.Contains("swap") && obig.MarkerUnits <= 0.60f * 143 + 1e-3, say(obig));
            // the container rule of the measurement (2.4): backgrounds and the BADGES COLLECTION frame out, the stats box stays an obstacle
            List<R4> obs, cont;
            var measured = new[] { new R4(0, 0, 3840, 2160), new R4(393, 297, 2025, 1642), new R4(2108, 1207, 3240, 1650), new R4(420, 1250, 1995, 1254), new R4(2152, 817, 2560, 870), new R4(2100, 380, 3100, 620) };
            LoadoutLayout.Split(measured, pc.View, pc.Grid, pc.Slots, out obs, out cont);
            Check("L7 the measurement's container rule: the background, the frame and the slot row's backing are containers; the stats box, the line and the header stay obstacles",
                cont.Count == 3 && obs.Count == 3 && obs.Contains(new R4(2108, 1207, 3240, 1650)), "containers " + string.Join(" ", cont) + " | obstacles " + string.Join(" ", obs));
            // L9 the containers the measurement really meets: a full-screen background (hierarchy order puts it first), the slot
            // row's own backing (the Split case above) and the grid's own backing - the bands must not depend on them
            var real = Mock(1440, new R4(-660, 0, 4500, 2160), rows: 3, detail: LoadoutDetail.Full);
            real.Containers = new[] { new R4(0, 0, 3840, 2160), new R4(393, 297, 2025, 1642), new R4(2100, 380, 3100, 620), new R4(430, 437, 1989, 1196) };
            var oreal = LoadoutLayout.Choose(real); L7("real containers", real, oreal, l7);
            var slotOnly = Mock(1440, new R4(-660, 0, 4500, 2160), rows: 3, detail: LoadoutDetail.Full);
            slotOnly.Containers = new[] { new R4(393, 297, 2025, 1642), new R4(2100, 380, 3100, 620) };
            var oslot = LoadoutLayout.Choose(slotOnly); L7("slot backing", slotOnly, oslot, l7);
            var crowdBg = Mock(1440, new R4(-660, 0, 4500, 2160), 30);
            crowdBg.Containers = new[] { new R4(0, 0, 3840, 2160), new R4(393, 297, 2025, 1642) };
            var ocrowdBg = LoadoutLayout.Choose(crowdBg); L7("crowded + background", crowdBg, ocrowdBg, l7);
            Check("L9 a background, the slot row's backing and the grid's backing among the containers: the same bands as L1 / L4",
                oreal.WhyAt == "grid-gap" && oreal.Why.Size == "943x143" && oreal.SumAt == "under-slots" && oreal.Sum.Size == "1090x205" && oreal.SumRows == 3
                && oslot.SumAt == "under-slots" && oslot.Sum.Size == "1090x205" && ocrowdBg.WhyAt == ocrowd.WhyAt && ocrowdBg.Why.ToString() == ocrowd.Why.ToString(),
                say(oreal) + " | slot backing alone: summary " + oslot.SumAt + " | crowded + background: why " + ocrowdBg.WhyAt + " " + ocrowdBg.Why);
            // L10 the rects the game really hands over (in-game walk 2026-10-04, game 1.0.2, '[loadout] layout' lines of the PC and
            // the Deck): the grid buttons' rects are the whole 200-unit cells (touching; the 143-unit icon sits inside), so the
            // selected-diamond zones of the row above reach into the grid gap; and the info labels hold no text before the first
            // highlight (empty rects). The gap must still take the WHY line: cut under the row above, 2 lines, full width.
            foreach (bool onDeck in new[] { false, true })
            {
                var real10 = MockReal(onDeck); var o10 = LoadoutLayout.Choose(real10); string lab10 = onDeck ? "real Deck" : "real PC"; L7(lab10, real10, o10, l7);
                float f10 = onDeck ? 45f : 40.5f;
                Check("L10 " + lab10 + " (200-unit cells, labels empty before the first highlight): WHY grid-gap, 2 lines at " + f10.ToString("0.0", IC) + " u, the full 1000-unit width, cut under the row above's selected diamonds; summary under-slots",
                    o10.WhyAt == "grid-gap" && o10.WhyLines == 2 && Math.Abs(o10.WhyFontUnits - f10) < 0.05 && Math.Round(o10.Why.W) == 1000 && o10.Why.H >= 2 * LoadoutLayout.LineFactor * f10 && o10.Why.H < 200
                    && o10.SumAt == "under-slots", say(o10) + " | why " + o10.Why + " | bands " + o10.Bands);
            }
            // L11 Trim alone: a row of zones along the top edge costs height, not width; a neighbour that only touches costs nothing,
            // one a hair over the edge a hair; a wall through the middle leaves too little width; a free band comes back as it was
            {
                var band = new R4(0, 0, 1000, 200); string c1, c2, c3, c4, c5;
                var zones = Enumerable.Range(0, 5).Select(n => new R4(70 + 200 * n, -24, 130 + 200 * n, 44)).ToList();
                var t1 = LoadoutLayout.Trim(band, zones, 62.775f, 2, out c1);
                var t2 = LoadoutLayout.Trim(band, new List<R4> { new R4(-200, 0, 0, 200) }, 62.775f, 2, out c2);
                var t3 = LoadoutLayout.Trim(band, new List<R4> { new R4(-200, 0, 0.01f, 200), new R4(1000, 0, 1200, 200) }, 62.775f, 2, out c3);
                var t4 = LoadoutLayout.Trim(band, new List<R4> { new R4(450, -10, 550, 210) }, 62.775f, 2, out c4);
                var t5 = LoadoutLayout.Trim(band, new List<R4> { new R4(0, 300, 1000, 400) }, 62.775f, 2, out c5);
                Check("L11 Trim: zones along the top edge cut the top strip (1000x156), a touching neighbour nothing, a hair over the edge a hair, a wall through the middle leaves < 600 wide, a free band unchanged",
                    t1.ToString() == "(0,44)-(1000,200)" && c1.Length > 0 && t2.ToString() == band.ToString() && c2.Length == 0 && Math.Abs(t3.X0 - 0.01f) < 1e-4 && t3.X1 == 1000 && t3.H == 200
                    && t4.W < LoadoutLayout.MinBandWidth && t5.ToString() == band.ToString() && c5.Length == 0,
                    t1 + " by " + c1 + " | " + t2 + " | " + t3 + " | " + t4 + " | " + t5);
            }
            // L12 (review) the real screen once the labels are measured on the first highlight (their rendered rects from the
            // 10-04 shots: under the separator, PC and Deck): the WHY line stays in the grid gap, clear of them; and a bigger text
            // (LoadoutSize 1.3: 200 units hold 2 lines, the 156 left under the row above only 1) keeps the full 1000-unit width at
            // 1 line instead of a 670-unit strip (Trim alone is greedy: it took the 2-line right-hand piece, then lost it)
            foreach (bool onDeck in new[] { false, true })
            {
                string lab12 = onDeck ? "real Deck" : "real PC";
                var lab = MockReal(onDeck);
                if (onDeck) { lab.Name = new R4(1050, 1446, 1371, 1494); lab.Desc = new R4(969, 1521, 1449, 1569); lab.Bonus = new R4(1020, 1587, 1398, 1635); }
                else { lab.Name = new R4(1708, 1321, 2028, 1378); lab.Desc = new R4(1631, 1401, 2105, 1450); lab.Bonus = new R4(1677, 1465, 2059, 1514); }
                var olab = LoadoutLayout.Choose(lab); L7(lab12 + " + labels", lab, olab, l7);
                var big12 = MockReal(onDeck); big12.Size = 1.3f; var obig12 = LoadoutLayout.Choose(big12); L7(lab12 + " size 1.3", big12, obig12, l7);
                Check("L12 " + lab12 + ": labels measured -> WHY still grid-gap 1000x156, 2 lines; LoadoutSize 1.3 -> WHY grid-gap at the full 1000-unit width, 1 line",
                    olab.WhyAt == "grid-gap" && olab.WhyLines == 2 && olab.Why.Size == "1000x156" && obig12.WhyAt == "grid-gap" && obig12.WhyLines == 1 && Math.Round(obig12.Why.W) == 1000,
                    "labels: " + say(olab) + " | size 1.3: " + say(obig12) + " | why " + obig12.Why);
            }
            // L7
            Check("L7 every mock: no chosen rect over an obstacle, a button, the info labels, the START bar or a selected diamond; inside the frame's inset", l7.Count == 0, string.Join("; ", l7));
            Console.WriteLine("    " + LoadoutLayout.Describe(pc, opc, 3440, 1440));
            Console.WriteLine("    (L8: replay of logged '[loadout] layout' lines - none logged yet)");
        }

        // ------------------------------------------------------------------------------------------------ S: safety
        static readonly string[] Forbidden = { "OnClickBadge(", "SaveSelectedBadges(", "SetSelectedBadges(", "ApplyForcedBadges(", "RebuildSelectedBadges(", "LoadSelectedBadges(", "OnClickDifficulty(", "OnClickStartGame(", "RepeatRun(" };

        /// <summary>Source text without comments and string / char literals.</summary>
        static string Code(string src)
        {
            var sb = new StringBuilder(); int i = 0;
            while (i < src.Length)
            {
                if (src[i] == '/' && i + 1 < src.Length && src[i + 1] == '/') { while (i < src.Length && src[i] != '\n') i++; continue; }
                if (src[i] == '/' && i + 1 < src.Length && src[i + 1] == '*') { int e = src.IndexOf("*/", i + 2, StringComparison.Ordinal); i = e < 0 ? src.Length : e + 2; continue; }
                if (src[i] == '@' && i + 1 < src.Length && src[i + 1] == '"') { i += 2; while (i < src.Length) { if (src[i] == '"' && i + 1 < src.Length && src[i + 1] == '"') { i += 2; continue; } if (src[i] == '"') { i++; break; } i++; } sb.Append("\"\""); continue; }
                if (src[i] == '"' || src[i] == '\'') { char q = src[i++]; while (i < src.Length && src[i] != q) { if (src[i] == '\\') i++; i++; } i++; sb.Append(q).Append(q); continue; }
                sb.Append(src[i++]);
            }
            return sb.ToString();
        }

        static void Safety()
        {
            Console.WriteLine("\n=== S safety");
            string mod = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod"));
            var files = Directory.Exists(mod) ? Directory.GetFiles(mod, "Loadout*.cs").Concat(Directory.GetFiles(mod, "Menu.Badges.cs")).OrderBy(x => x).ToList() : new List<string>();
            var hits = new List<string>();
            foreach (var f in files)
            {
                string code = Code(File.ReadAllText(f)); string name = Path.GetFileName(f);
                foreach (var call in Forbidden)
                {
                    int at = 0;
                    while ((at = code.IndexOf(call, at, StringComparison.Ordinal)) >= 0)
                    {
                        bool allowed = false;
                        if (name == "LoadoutEquip.cs" && call == "OnClickBadge(")
                        {   // only inside GuardedClick: the nearest method header before the call
                            var m = Regex.Matches(code.Substring(0, at), @"\b(?:static\s+)?(?:void|bool)\s+(\w+)\s*\(").Cast<Match>().LastOrDefault();
                            allowed = m != null && m.Groups[1].Value == "GuardedClick";
                        }
                        if (!allowed) hits.Add(name + ": " + call);
                        at += call.Length;
                    }
                }
            }
            Check("S1 no call that writes the profile in Loadout*.cs / Menu.Badges.cs (OnClickBadge only in LoadoutEquip.cs's GuardedClick)", files.Count > 0 && hits.Count == 0,
                files.Count + " files (" + string.Join(", ", files.Select(Path.GetFileName)) + ")" + (hits.Count > 0 ? ": " + string.Join(", ", hits) : ""));
            // S2 builds.json
            string plain = "{\n  \"selected\": {},\n  \"custom\": {\n    \"SWAT\": {\n      \"id\": \"custom-swat\",\n      \"name\": \"My SWAT\",\n      \"summary\": \"\",\n      \"glyph\": \"\",\n      \"branch\": \"Assault Rifle\",\n      \"style\": \"Weapon\",\n      \"abilities\": [\n        \"Helicopter Strike\"\n      ],\n      \"skip\": [],\n      \"wants\": [],\n      \"evolution\": {}\n    }\n  }\n}";
            string withPins = plain.Replace("\"evolution\": {}\n", "\"evolution\": {},\n      \"badges\": [\n        \"Gunner\",\n        \"20\"\n      ],\n      \"skipBadges\": [\n        \"Leveling\"\n      ]\n");
            string tmp = Path.Combine(Path.GetTempPath(), "yazs-bench-builds-none.json");
            Builds.Load(tmp); Builds.Parse(plain); string out1 = Builds.ToJson().Replace("\r\n", "\n");
            Builds.Load(tmp); Builds.Parse(withPins); string out2 = Builds.ToJson().Replace("\r\n", "\n"); var c = Builds.CustomOf("SWAT");
            string old = "";
            using (var doc = JsonDocument.Parse(withPins)) old = string.Join(",", doc.RootElement.GetProperty("custom").GetProperty("SWAT").GetProperty("abilities").EnumerateArray().Select(x => x.GetString()));
            Builds.Load(tmp);
            Check("S2 builds.json: a build without badges writes byte-identically; with badges / skipBadges the round trip holds; the other fields read the same",
                out1 == plain && out2 == withPins && c != null && c.Badges.SequenceEqual(new[] { "Gunner", "20" }) && c.SkipBadges.SequenceEqual(new[] { "Leveling" }) && string.Join(",", c.Abilities) == old,
                out1 == plain ? (out2 == withPins ? "both round trips exact" : "with pins:\n" + out2) : "without pins:\n" + out1);
        }

        // ------------------------------------------------------------------------------------------------ X1: replay
        sealed class ReplayResult
        {
            public int Advices, Match, Diff, Drawn, DrawnAsAdvised, RunStarts;
            public readonly List<string> Lines = new List<string>();
            public string Summary()
            {
                return "loadout replay: " + Advices + " advice" + (Advices == 1 ? "" : "s") + ", " + Match + " MATCH, " + Diff + " DIFF; " + DrawnAsAdvised + " of " + Drawn + " drawn sets as advised; "
                    + RunStarts + " run" + (RunStarts == 1 ? "" : "s") + " started";
            }
        }

        static readonly Regex LoadoutLine = new Regex(@"\[loadout\] (inventory|knowledge|input #(\d+)|drawn #(\d+)|run start:)\s*(.*)$");

        static ReplayResult ReplayLines(IEnumerable<string> lines)
        {
            var r = new ReplayResult();
            List<BadgeFacts> inv = null; var k = K();
            var advices = new Dictionary<int, LoadoutAdvice>();
            foreach (var raw in lines)
            {
                var m = LoadoutLine.Match(raw ?? "");
                if (!m.Success) continue;
                string kind = m.Groups[1].Value, rest = m.Groups[4].Value.Trim();
                try
                {
                    if (kind == "inventory" && rest.StartsWith("{", StringComparison.Ordinal)) inv = Loadout.ReadInventory(rest);
                    else if (kind == "knowledge" && rest.StartsWith("{", StringComparison.Ordinal)) k = Knowledge.FromJson(rest);
                    else if (kind.StartsWith("input", StringComparison.Ordinal))
                    {
                        int n = int.Parse(m.Groups[2].Value, IC);
                        List<KeyValuePair<int, double>> logged;
                        var inp = Loadout.ReadInput(rest, out logged);
                        var badges = inv ?? (inp.Inventory == Loadout.ShortHash(Loadout.Hash(Fixture())) ? Fixture() : null);
                        r.Advices++;
                        if (badges == null) { r.Diff++; r.Lines.Add("#" + n + " DIFF: no inventory line before it"); continue; }
                        var a = Loadout.Recommend(inp, badges, k, n); advices[n] = a;
                        bool same = a.Picks.Count == logged.Count && a.Picks.Select((p, i) => p.Badge.Id == logged[i].Key && Math.Abs(p.Score - logged[i].Value) <= 0.005).All(x => x);
                        if (same) r.Match++; else r.Diff++;
                        r.Lines.Add("#" + n + " " + (same ? "MATCH" : "DIFF") + ": " + Names(a) + (same ? "" : " | logged " + string.Join(", ", logged.Select(x => x.Key + " " + P(x.Value)))));
                    }
                    else if (kind.StartsWith("drawn", StringComparison.Ordinal))
                    {
                        int n = int.Parse(m.Groups[3].Value, IC);
                        var mm = Regex.Match(rest, @"marks ([\d,\-]+)");
                        if (!mm.Success) continue;
                        r.Drawn++;
                        LoadoutAdvice a;
                        var marks = mm.Groups[1].Value == "-" ? new List<int>() : mm.Groups[1].Value.Split(',').Select(x => int.Parse(x, IC)).ToList();
                        bool ok = advices.TryGetValue(n, out a) && (marks.Count == 0 && rest.Contains("Off") || marks.SequenceEqual(a.Picks.Where(p => !p.Forced).Select(p => p.Badge.Id)));
                        if (ok) r.DrawnAsAdvised++;
                        else r.Lines.Add("#" + n + " drawn NOT as advised: marks " + Ids(marks) + (a != null ? ", advised " + Ids(a.Picks.Where(p => !p.Forced).Select(p => p.Badge.Id)) : ", no advice #" + n));
                    }
                    else if (kind == "run start:") { r.RunStarts++; r.Lines.Add("run start: " + rest); }
                }
                catch (Exception e) { r.Diff++; r.Lines.Add(kind + " unreadable: " + e.Message); }
            }
            return r;
        }

        /// <summary>ItemBench --replay-loadout &lt;companion.log | LogOutput.log&gt;: every logged advice recomputed.</summary>
        public static int ReplayFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { Console.Error.WriteLine("--replay-loadout: no log at " + (path ?? "(none)")); return 2; }
            var r = ReplayLines(File.ReadLines(path));
            foreach (var l in r.Lines) Console.WriteLine("  " + l);
            Console.WriteLine(r.Summary());
            return r.Diff == 0 && r.DrawnAsAdvised == r.Drawn ? 0 : 3;
        }

        static void Replay()
        {
            Console.WriteLine("\n=== X1 replay");
            var bad = new List<string>();
            foreach (var sc in Scenarios())
            {
                var k = sc.Recruits.Length > 0 ? KRecruits() : K();
                var inp = Input(sc, k); var a = Loadout.Recommend(inp, Fixture(), k, 3);
                List<KeyValuePair<int, double>> logged;
                var back = Loadout.ReadInput(Loadout.InputJson(inp, a), out logged);
                var b = Loadout.Recommend(back, Fixture(), k, 3);
                bool same = a.Equip.SequenceEqual(b.Equip) && a.Picks.Zip(b.Picks, (x, y) => Math.Abs(x.Score - y.Score) <= 0.005).All(x => x) && logged.Select(x => x.Key).SequenceEqual(a.Equip)
                    && Loadout.InputJson(back, b) == Loadout.InputJson(inp, a);
                if (!same) bad.Add(sc.Title.Substring(0, 3));
            }
            Check("X1 InputJson -> ReadInput -> Recommend: the same EQUIP and scores (every scenario), the JSON reproduced", bad.Count == 0, string.Join(", ", bad));
            // a hand-made log excerpt, as LoadoutUi writes it
            var kk = K(); var b1 = Find("B1"); var a1 = Loadout.Recommend(Input(b1, kk), Fixture(), kk, 7); var v1 = LoadoutView.Of(a1, b1.Equipped, b1.Forced, 4, LoadoutDetail.NumbersAndReason, kk);
            string pre = "[Info   :YAZS Companion] [loadout] ";
            var log = new List<string>
            {
                pre + "inventory 27 badges, hash " + Loadout.ShortHash(Loadout.Hash(Fixture())) + " = the 1.0.2 reference",
                pre + "inventory " + Loadout.InventoryJson(Fixture(), "1.0.2"),
                pre + Loadout.AdviseLine(a1),
                pre + "input #7 " + Loadout.InputJson(a1.Input, a1),
                pre + v1.EquippedLine(),
                pre + "drawn #7 level A (NumbersAndReason, size 1.00): " + v1.DrawnMarks() + " | why grid-gap 943x143 (2 lines)",
                pre + "run start: SWAT Normal d2 | badges Gunner L2, Critical L2, Power L2, Tough L2 (advice #7: 4 of 4)",
            };
            var r = ReplayLines(log);
            Check("X1 a log excerpt (inventory, advise, input, equipped, drawn, run start) -> MATCH", r.Advices == 1 && r.Match == 1 && r.Drawn == 1 && r.DrawnAsAdvised == 1 && r.RunStarts == 1, r.Summary());
            var tampered = log.Select(l => l.Contains("drawn #7") ? Regex.Replace(l, @"marks [\d,]+", "marks 16,17,20,7") : l).ToList();
            var rt = ReplayLines(tampered);
            Check("X1 a tampered drawn set is reported", rt.Drawn == 1 && rt.DrawnAsAdvised == 0 && rt.Lines.Any(l => l.Contains("NOT as advised")), rt.Summary());
            var knowledge = K(); knowledge.BadgeStats["MoneyMultiplier"].W = 2.0;
            var ak = Loadout.Recommend(Input(Find("B1"), knowledge), Fixture(), knowledge, 8);
            var rk = ReplayLines(new[] { pre + "knowledge " + Loadout.KnowledgeJson(knowledge), pre + "input #8 " + Loadout.InputJson(ak.Input, ak) });
            Check("X1 an edited knowledge.json in the log is used by the replay", rk.Match == 1 && !Loadout.KnowledgeIsDefault(knowledge), rk.Summary() + " | " + Names(ak));
        }
    }
}
