// The badge advice's game reader (0.13.0): everything Loadout.cs needs from the live game, read through one-line accessors
// that each touch a single game member and are never inlined - a game patch that renames or removes a member then breaks
// that accessor alone (its caller catches), not the whole reader (SPEC 6.2). Per capability there is a fallback:
//   registry   PowerupReferences.Get.badges            -> the grid buttons' badgeReference -> nothing (level E)
//   grid       UIViewRunSetup.availableBadgesButtons   -> no markers (level C / D)
//   slots      GamePermanentData.Get.GetMaxNumBadges() -> the active slot buttons -> max(equipped, 2), "slots unknown"
//   selected   UIViewRunSetup._selectedBadges          -> the slot buttons' badgeReference -> UIStartGameBar.selectedBadges
//   forced     UIViewRunSetup._forcedBadges            -> view.IsForcedBadge per equipped badge -> none
//   leader     UIStartGameBar.selectedCharacter        -> UIViewChooseHero.chosenClassProperties -> "leader unknown" (level D)
//   mode       UIStartGameBar.selectedGameplayMode / selectedDifficulty -> the view's selectedGameMode / difficulty button
//   levels     the badge's tree node (GetRuntimeInstance -> IsUnlocked ? GetCurrentLevel : 0), checked against
//              GetSkillTreeNodeLevel() (which adds Mushroom Mushroom through a static that can be left over from the last run)
// What a badge does is asset data: read once per session (cached by badgeBaseId, checked against the registry list), with the
// game's own values - the release changed 24 badge values between 1.0.1 and 1.0.2, so nothing is copied into code. The levels,
// the grid mapping and the slots are read once per visit of the screen; the selection, the leader, the mode and the difficulty
// on a change of the cheap signature (FNV-1a over pointers and ints: about ten reads every 0.25 s).
//
// Read-only: no call here changes the game. The calls that write the profile are named only in LoadoutEquip.cs (the optional
// one-click button, off by default) - the bench's safety scan fails on any of them anywhere else in Loadout*.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using TMPro;
using CT = GamePlayer.CharacterType;
using BadgeList = Il2CppSystem.Collections.Generic.List<GameplayBadgeBase>;
using ButtonList = Il2CppSystem.Collections.Generic.List<UIViewRunSetupBadgeButton>;
using BonusList = Il2CppSystem.Collections.Generic.List<PowerupBase.StatisticBonus>;

namespace YazsCompanion
{
    /// <summary>A grid or slot button of the run setup screen, as one visit reads it.</summary>
    internal sealed class LoadoutCell
    {
        public UIViewRunSetupBadgeButton Button;
        public IntPtr ButtonPtr, ObjectPtr, BadgePtr;
        public BadgeFacts Facts;
        public int Index;
        public bool Locked, Slot;
        public int Id { get { return Facts != null ? Facts.Id : -1; } }
    }

    /// <summary>One visit of the run setup screen: the grid, the slots and the levels (read once), the selection and the run
    /// (read again on every change of the signature).</summary>
    internal sealed class LoadoutVisit
    {
        public UIViewRunSetup View;
        public IntPtr ViewPtr;
        public int Number;
        public float At;
        public readonly List<LoadoutCell> Grid = new List<LoadoutCell>();
        public readonly List<LoadoutCell> SlotCells = new List<LoadoutCell>();
        public bool GridOk;
        public int Slots = -1, GameSlots = -1, BaseSlots = -1, SlotButtons;
        public string SlotsNote = "";
        public readonly Dictionary<int, int> Levels = new Dictionary<int, int>();
        public readonly Dictionary<int, bool> RankOpen = new Dictionary<int, bool>();
        public readonly HashSet<int> OffGrid = new HashSet<int>();
        public readonly List<int> Equipped = new List<int>(), Forced = new List<int>();
        public string EquippedFrom = "", ForcedFrom = "";
        public string Leader = "", LeaderFrom = "";
        public string Mode = "Normal", ModeFrom = "";
        public int Difficulty = 1;
        public string Quest = "-";
        public int Raised;                                            // badges GetSkillTreeNodeLevel() reads above their tree node
        public readonly List<string> Missing = new List<string>();    // capabilities that fell back, for the visit line

        public LoadoutCell CellOf(int id) { return Grid.FirstOrDefault(c => c.Id == id); }
        public int Unlocked { get { return Levels.Count(kv => kv.Value >= 1); } }
    }

    internal static class LoadoutState
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------------------------------------ the accessors
        // One game member each, never inlined: a member a game patch took away fails here alone. The callers catch.
        [MethodImpl(MethodImplOptions.NoInlining)] static PowerupReferences Refs() { return PowerupReferences.Get; }
        [MethodImpl(MethodImplOptions.NoInlining)] static BadgeList RegistryOf(PowerupReferences r) { return r.badges; }
        [MethodImpl(MethodImplOptions.NoInlining)] static ButtonList GridOf(UIViewRunSetup v) { return v.availableBadgesButtons; }
        [MethodImpl(MethodImplOptions.NoInlining)] static ButtonList SlotButtonsOf(UIViewRunSetup v) { return v.selectedBadgesButtons; }
        [MethodImpl(MethodImplOptions.NoInlining)] static BadgeList SelectedOf(UIViewRunSetup v) { return v._selectedBadges; }
        [MethodImpl(MethodImplOptions.NoInlining)] static BadgeList ForcedOf(UIViewRunSetup v) { return v._forcedBadges; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool IsForced(UIViewRunSetup v, GameplayBadgeBase b) { return v.IsForcedBadge(b); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static bool CanInteract(UIViewRunSetup v) { return v._canInteract; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static bool CanSelectMore(UIViewRunSetup v) { return v.CanSelectMoreBadges(); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static GameplayBadgeBase BadgeOf(UIViewRunSetupBadgeButton b) { return b.badgeReference; }
        [MethodImpl(MethodImplOptions.NoInlining)] static GameObject LockedOf(UIViewRunSetupBadgeButton b) { return b.lockedContainer; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static bool Interactable(UIViewRunSetupBadgeButton b) { return b._isInteractable; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int MaxSlots() { return GamePermanentData.Get.GetMaxNumBadges(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static int BaseSlotsConst() { return GamePermanentData.BASE_NUM_BADGES; }
        [MethodImpl(MethodImplOptions.NoInlining)] static UIMainMenu MainMenuUi() { return UIMainMenu.s_instance; }
        [MethodImpl(MethodImplOptions.NoInlining)] static UIStartGameBar BarField(UIMainMenu m) { return m._uiStartGameBar; }
        [MethodImpl(MethodImplOptions.NoInlining)] static UIViewChooseHero HeroField(UIMainMenu m) { return m._uiViewChooseHero; }
        [MethodImpl(MethodImplOptions.NoInlining)] static UIViewRunSetup SetupField(UIMainMenu m) { return m._uiViewRunSetup; }
        [MethodImpl(MethodImplOptions.NoInlining)] static ClassProperties BarLeader(UIStartGameBar b) { return b.selectedCharacter; }
        [MethodImpl(MethodImplOptions.NoInlining)] static GameMode.GameplayMode BarMode(UIStartGameBar b) { return b.selectedGameplayMode; }
        [MethodImpl(MethodImplOptions.NoInlining)] static GameMode.Difficulty BarDifficulty(UIStartGameBar b) { return b.selectedDifficulty; }
        [MethodImpl(MethodImplOptions.NoInlining)] static BadgeList BarBadges(UIStartGameBar b) { return b.selectedBadges; }
        [MethodImpl(MethodImplOptions.NoInlining)] static ClassProperties HeroLeader(UIViewChooseHero h) { return h.chosenClassProperties; }
        [MethodImpl(MethodImplOptions.NoInlining)] static GameMode.GameplayMode ViewMode(UIViewRunSetup v) { return v.selectedGameMode; }
        [MethodImpl(MethodImplOptions.NoInlining)] static GameMode.Difficulty ViewDifficulty(UIViewRunSetup v) { return v.selectedDifficultyButton.GetDifficulty().difficulty; }
        [MethodImpl(MethodImplOptions.NoInlining)] static GameHubQuestBase ActiveQuest() { return GameQuestManager.Get.ActiveQuest; }
        [MethodImpl(MethodImplOptions.NoInlining)] static SkillTreeUpgradeBase NodeOf(GameplayBadgeBase b) { return b.skillTreeRequirement; }
        [MethodImpl(MethodImplOptions.NoInlining)] static SkillTreeUpgradeBase Runtime(SkillTreeUpgradeBase n) { return n.GetRuntimeInstance(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool NodeUnlocked(SkillTreeUpgradeBase n) { return n.IsUnlocked(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static int NodeLevel(SkillTreeUpgradeBase n) { return n.GetCurrentLevel(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static int NodeRank(SkillTreeUpgradeBase n) { return n.rankRequirement; }
        [MethodImpl(MethodImplOptions.NoInlining)] static ClassProperties NodeOwner(SkillTreeUpgradeBase n) { return n.targetClassProperties; }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static int GameLevel(GameplayBadgeBase b) { return b.GetSkillTreeNodeLevel(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static int GameMax(GameplayBadgeBase b) { return b.GetSkillTreeNodeMaxLevel(); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static string NameText(GameplayBadgeBase b) { return b.GetNameText(); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static string LevelText(GameplayBadgeBase b) { return b.GetCurrentLevelDescriptionText(); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static Sprite IconOf(GameplayBadgeBase b) { return b.icon; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int IdOf(GameplayBadgeBase b) { return b.badgeBaseId; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int SortOf(GameplayBadgeBase b) { return b.badgeSortOrder; }
        [MethodImpl(MethodImplOptions.NoInlining)] static BadgeList RunList() { return GameplayMaster.s_instance.selectedBadges; }
        [MethodImpl(MethodImplOptions.NoInlining)] static int SpecialAt() { return HashtagSystem.NumRequiredForSpecial; }
        // the badge classes and their fields: Read() never names a badge subclass or one of their fields itself (the JIT compiles a
        // method whole - a class or a field a game patch took away would fail the whole reader, not one line of it), so a missing
        // one fails its accessor alone and that badge is read without it (not rated, at worst)
        [MethodImpl(MethodImplOptions.NoInlining)] static bool IsTagBadge(GameplayBadgeBase b) { return b.TryCast<GameplayBadgeHashtagBoost>() != null; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool IsPhysicalBadge(GameplayBadgeBase b) { return b.TryCast<GameplayBadgeHashtagPhysicalBoost>() != null; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool IsElementalBadge(GameplayBadgeBase b) { return b.TryCast<GameplayBadgeHashtagElementalBoost>() != null; }
        [MethodImpl(MethodImplOptions.NoInlining)] static bool IsStatBadge(GameplayBadgeBase b) { return b.TryCast<GameplayBadgeStatBoost>() != null; }
        [MethodImpl(MethodImplOptions.NoInlining)] static BonusList TagBonuses(GameplayBadgeBase b) { return b.TryCast<GameplayBadgeHashtagBoost>().bonusesPerLevel; }
        [MethodImpl(MethodImplOptions.NoInlining)] static BonusList PhysicalBonuses(GameplayBadgeBase b) { return b.TryCast<GameplayBadgeHashtagPhysicalBoost>().bonusesPerLevel; }
        [MethodImpl(MethodImplOptions.NoInlining)] static BonusList ElementalBonuses(GameplayBadgeBase b) { return b.TryCast<GameplayBadgeHashtagElementalBoost>().bonusesPerLevel; }
        [MethodImpl(MethodImplOptions.NoInlining)] static BonusList StatBonuses(GameplayBadgeBase b) { return b.TryCast<GameplayBadgeStatBoost>().bonusesPerLevel; }
        [MethodImpl(MethodImplOptions.NoInlining)] static string TagTypeOf(GameplayBadgeBase b) { return G.TagName(b.TryCast<GameplayBadgeHashtagBoost>().type); }
        [MethodImpl(MethodImplOptions.NoInlining)] static int[] TagLevelsOf(GameplayBadgeBase b) { return G.Each(b.TryCast<GameplayBadgeHashtagBoost>().hashtagsOnLevel).ToArray(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static int[] PhysicalLevelsOf(GameplayBadgeBase b) { return Ints(b.TryCast<GameplayBadgeHashtagPhysicalBoost>().hashtagsPerLevel); }
        [MethodImpl(MethodImplOptions.NoInlining)] static int[] ElementalLevelsOf(GameplayBadgeBase b) { return Ints(b.TryCast<GameplayBadgeHashtagElementalBoost>().hashtagsPerLevel); }
        [MethodImpl(MethodImplOptions.NoInlining)] static PlayerStatistic TargetOf(PowerupBase.StatisticBonus bo) { return bo.targetStatistic; }
        [MethodImpl(MethodImplOptions.NoInlining)] static string StatTypeOf(PlayerStatistic st) { return st.statisticType.ToString(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static string BonusTypeOf(PowerupBase.StatisticBonus bo) { return bo.bonusType.ToString(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static float ValueOf(PowerupBase.StatisticBonus bo) { return bo.value; }
        [MethodImpl(MethodImplOptions.NoInlining)] static string TemplateOf(PowerupBase.StatisticBonus bo) { return bo.valueTemplate; }

        [MethodImpl(MethodImplOptions.NoInlining)] internal static bool IsForcedBadge(UIViewRunSetup v, GameplayBadgeBase b) { return v.IsForcedBadge(b); }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static bool IsLocked(UIViewRunSetupBadgeButton b) { var lk = b.lockedContainer; return lk != null && lk.activeSelf; }
        [MethodImpl(MethodImplOptions.NoInlining)] static TextMeshProUGUI NameLabel(UIViewRunSetup v) { return v.currentBadgeName; }
        [MethodImpl(MethodImplOptions.NoInlining)] static TextMeshProUGUI DescLabel(UIViewRunSetup v) { return v.currentBadgeDescription; }
        [MethodImpl(MethodImplOptions.NoInlining)] static TextMeshProUGUI BonusLabel(UIViewRunSetup v) { return v.currentBadgeBonus; }
        [MethodImpl(MethodImplOptions.NoInlining)] static Il2CppSystem.Collections.Generic.List<UIViewRunSetupDifficultyButton> DifficultyButtons(UIViewRunSetup v) { return v.difficultyButtons; }
        [MethodImpl(MethodImplOptions.NoInlining)] static UnityEngine.UI.Button StartButton(UIStartGameBar b) { return b.startGameButton; }
        [MethodImpl(MethodImplOptions.NoInlining)] static GameObject QuestBox(UIStartGameBar b) { return b.questContainerObject; }
        /// <summary>The debug walk only: what the mouse does when it moves over a badge - the game's info panel shows it. It writes
        /// nothing (the call graph: GetSkillTreeNodeLevel and TMP_Text.SetText).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void MoveCursor(UIViewRunSetup v, UIViewRunSetupBadgeButton b) { v.OnBadgeHighlight(b); }

        static IntPtr Ptr(Il2CppObjectBase o) { try { return o == null ? IntPtr.Zero : o.Pointer; } catch { return IntPtr.Zero; } }

        /// <summary>The info panel's three labels (name, description, bonus); null where unreadable.</summary>
        public static void Labels(UIViewRunSetup v, out TextMeshProUGUI name, out TextMeshProUGUI desc, out TextMeshProUGUI bonus)
        {
            name = null; desc = null; bonus = null;
            try { name = NameLabel(v); } catch { }
            try { desc = DescLabel(v); } catch { }
            try { bonus = BonusLabel(v); } catch { }
        }

        /// <summary>The active difficulty buttons' rects.</summary>
        public static List<RectTransform> DifficultyRects(UIViewRunSetup v)
        {
            var o = new List<RectTransform>();
            try { foreach (var d in G.Each(DifficultyButtons(v))) { if (d == null || !d.gameObject.activeInHierarchy) continue; var rt = d.transform.TryCast<RectTransform>(); if (rt != null) o.Add(rt); } } catch { }
            return o;
        }

        /// <summary>The START bar's button and quest line (the bar itself may span the screen; nothing is ever parented to it).</summary>
        public static List<RectTransform> StartBarRects()
        {
            var o = new List<RectTransform>();
            UIStartGameBar bar = null; try { bar = Bar(); } catch { }
            if (bar == null) return o;
            try { var b = StartButton(bar); if (b != null && b.gameObject.activeInHierarchy) { var rt = b.transform.TryCast<RectTransform>(); if (rt != null) o.Add(rt); } } catch { }
            try { var q = QuestBox(bar); if (q != null && q.activeInHierarchy) { var rt = q.transform.TryCast<RectTransform>(); if (rt != null) o.Add(rt); } } catch { }
            return o;
        }

        static readonly HashSet<string> _said = new HashSet<string>();
        /// <summary>A line written once per session (keyed).</summary>
        internal static void Once(string key, string line, bool warn = false)
        {
            if (!_said.Add(key)) return;
            if (warn) Plugin.Logger.LogWarning("[loadout] " + line); else Plugin.Logger.LogInfo("[loadout] " + line);
        }

        // ------------------------------------------------------------------------------------------------ the badges (session)
        static List<BadgeFacts> _all;
        static readonly Dictionary<IntPtr, BadgeFacts> _byPtr = new Dictionary<IntPtr, BadgeFacts>();
        static IntPtr _listPtr; static int _listCount = -1;
        static string _hashSaid = "";
        public static string Source = "";                 // "registry" | "grid" | ""
        public static string Hash = "", Short = "";
        public static bool IsReference;

        /// <summary>The badges as the advice reads them (sorted by badgeSortOrder, each id once); null = none readable.</summary>
        public static List<BadgeFacts> All { get { return _all; } }

        /// <summary>The facts of every badge: from the registry (once a session, again only when the game swaps its list), else
        /// from the grid buttons of <paramref name="view"/>. False = no badge readable at all (level E).</summary>
        public static bool EnsureFacts(UIViewRunSetup view = null)
        {
            BadgeList list = null; int count = -1;
            try { var r = Refs(); if (r != null) { list = RegistryOf(r); if (list != null) count = list.Count; } }
            catch (Exception e) { Once("registry", "the badge registry (PowerupReferences.badges) is unreadable: " + e.GetBaseException().Message + " - the grid buttons are read instead", true); }
            if (list != null && count > 0)
            {
                if (_all != null && Source == "registry" && Ptr(list) == _listPtr && count == _listCount) return true;
                var read = new List<BadgeFacts>();
                try
                {
                    _byPtr.Clear();
                    foreach (var b in G.Each(list)) { var f = Read(b); if (f != null) read.Add(f); }
                }
                catch (Exception e) { read.Clear(); Once("registryread", "the badge registry could not be read through (" + e.GetBaseException().Message + ") - the grid buttons are read instead", true); }
                if (read.Count > 0) { Adopt(read, "registry"); _listPtr = Ptr(list); _listCount = count; return true; }
            }
            if (_all != null) return true;                 // read before (the registry may be away in this scene)
            if (view == null) return false;
            try
            {
                var read = new List<BadgeFacts>(); _byPtr.Clear();
                foreach (var btn in G.Each(GridOf(view))) { if (btn == null) continue; var f = Read(BadgeOf(btn)); if (f != null) read.Add(f); }
                if (read.Count > 0) { Adopt(read, "grid"); return true; }
            }
            catch (Exception e) { Once("gridfacts", "no badge readable from the grid either: " + e.GetBaseException().Message, true); }
            return false;
        }

        static void Adopt(List<BadgeFacts> read, string source)
        {
            _all = Loadout.Clean(read, w => Once("dup:" + w, w, true));
            Source = source;
            Hash = Loadout.Hash(_all); Short = Loadout.ShortHash(Hash);
            IsReference = Hash == Loadout.ReferenceHash;
            if (Hash == _hashSaid) return;               // the same inventory again (a new scene): said already
            _hashSaid = Hash;
            Plugin.Logger.LogInfo("[loadout] inventory " + _all.Count + " badges, hash " + Short + (IsReference ? " = the " + Loadout.ReferenceGame + " reference"
                : " differs from the " + Loadout.ReferenceGame + " reference (" + Loadout.ShortHash(Loadout.ReferenceHash) + ") - the advice uses the live values (the inventory line lists them)")
                + (source == "grid" ? " (read from the grid buttons: the registry was unreadable)" : ""));
            Plugin.Logger.LogInfo("[loadout] inventory " + Loadout.InventoryJson(_all, GameBuild()));
            var k = Knowledge.Current;
            foreach (var w in Loadout.KnowledgeWarnings(_all, k)) Once("kw:" + w, w, true);
            foreach (var b in _all)
            {
                if (!Loadout.Rated(b, k)) { Once("unrated:" + b.Id, "badge " + b.Id + " " + b.Short + " (" + (b.KindName.Length > 0 ? b.KindName : b.Kind.ToString()) + ") is not rated: " + (b.Kind == BadgeKind.Unknown ? "its class is unknown" : "none of its stats has a weight") + " - never advised (worth a look)", true); continue; }
                foreach (var s in Loadout.UnknownStats(b, k)) Once("stat:" + b.Id + ":" + s, "stat '" + s + "' (badge " + b.Id + " " + b.Short + ") has no weight: estimated at " + Loadout.Fixed(k.BadgeRules.UnknownStatPerLevel, 1) + " per level (worth a look)");
            }
        }

        static string _build;
        static string GameBuild()
        {
            if (_build != null) return _build;
            try { _build = Application.version ?? ""; } catch { _build = ""; }
            return _build;
        }

        /// <summary>The facts of a live badge (by its object, else by its id).</summary>
        public static BadgeFacts FactsOf(GameplayBadgeBase b)
        {
            if (b == null || _all == null) return null;
            BadgeFacts f; var p = Ptr(b);
            if (p != IntPtr.Zero && _byPtr.TryGetValue(p, out f)) return f;
            int id; try { id = IdOf(b); } catch { return null; }
            f = _all.FirstOrDefault(x => x.Id == id);
            if (f != null && p != IntPtr.Zero) _byPtr[p] = f;
            return f;
        }

        static BadgeFacts Read(GameplayBadgeBase b)
        {
            if (b == null) return null;
            var f = new BadgeFacts { Ref = b };
            try { f.Id = IdOf(b); } catch (Exception e) { Once("noid", "a badge without a readable badgeBaseId: " + e.GetBaseException().Message, true); return null; }
            try { f.Sort = SortOf(b); } catch { f.Sort = 1000 + f.Id; }
            f.Asset = G.Asset(b);
            f.Short = Loadout.ShortName(f.Asset); f.Rank = Loadout.RankOf(f.Asset);
            if (f.Short == f.Asset) Once("asset:" + f.Id, "badge " + f.Id + ": the asset name '" + f.Asset + "' does not read as Badge_<Class><rank>_<Name> - its short name is the asset name");
            try { f.Name = NameText(b) ?? ""; } catch { }
            if (f.Name.Length == 0) f.Name = f.Short + " Badge";
            SkillTreeUpgradeBase node = null;
            try { node = NodeOf(b); } catch { }
            if (node != null)
            {
                try { var cp = NodeOwner(node); if (cp != null) f.Owner = G.ClassName(cp.characterType); } catch { }
                if (f.Rank == 0) { try { f.Rank = NodeRank(node); } catch { } }
            }
            try { int m = GameMax(b); if (m > 0) f.Max = m; } catch { }
            try { f.KindName = b.GetIl2CppType().Name; } catch { }
            string single = null;
            BonusList bonuses = null;
            // the class, one accessor per class (a class a game patch took away fails its own test alone), then its fields
            f.Kind = BadgeKind.Unknown;
            string failed = null;
            try { if (IsTagBadge(b)) f.Kind = BadgeKind.Tag; } catch (Exception e) { failed = e.GetBaseException().Message; }
            if (f.Kind == BadgeKind.Unknown) { try { if (IsPhysicalBadge(b)) f.Kind = BadgeKind.Physical; } catch (Exception e) { failed = failed ?? e.GetBaseException().Message; } }
            if (f.Kind == BadgeKind.Unknown) { try { if (IsElementalBadge(b)) f.Kind = BadgeKind.Elemental; } catch (Exception e) { failed = failed ?? e.GetBaseException().Message; } }
            if (f.Kind == BadgeKind.Unknown) { try { if (IsStatBadge(b)) f.Kind = BadgeKind.Stat; } catch (Exception e) { failed = failed ?? e.GetBaseException().Message; } }
            if (f.Kind == BadgeKind.Unknown && failed != null) Once("kind:" + f.Id, "badge " + f.Id + " " + f.Short + ": its class could not be read (" + failed + ") - not rated", true);
            try
            {
                switch (f.Kind)
                {
                    case BadgeKind.Tag:
                        bonuses = TagBonuses(b);
                        try { single = TagTypeOf(b); } catch { }
                        try { f.TagPoints = TagLevelsOf(b); } catch { }
                        break;
                    case BadgeKind.Physical: bonuses = PhysicalBonuses(b); try { f.TagPoints = PhysicalLevelsOf(b); } catch { } break;
                    case BadgeKind.Elemental: bonuses = ElementalBonuses(b); try { f.TagPoints = ElementalLevelsOf(b); } catch { } break;
                    case BadgeKind.Stat: bonuses = StatBonuses(b); break;
                }
            }
            catch (Exception e) { Once("bonuses:" + f.Id, "badge " + f.Id + " " + f.Short + ": its bonuses could not be read (" + e.GetBaseException().Message + ") - rated by its tag points at most", true); }
            var k = Knowledge.Current;
            foreach (var bo in G.Each(bonuses))
            {
                if (bo == null) continue;
                string asset = "", type = "", etype = "", template = ""; double per = 0;
                try { var st = TargetOf(bo); if (st != null) { asset = G.Asset(st); try { type = StatTypeOf(st); } catch { } } } catch { }
                try { etype = BonusTypeOf(bo); } catch { }
                try { per = Exact(ValueOf(bo)); } catch { }
                try { template = TemplateOf(bo) ?? ""; } catch { }
                if (type.Length == 0) type = etype;
                string key = asset.Length > 0 ? Loadout.StatKey(asset) : StatFromType(etype.Length > 0 ? etype : type, k);
                f.Bonuses.Add(new BadgeBonus { Stat = key, Type = type, PerLevel = per, Template = template });
            }
            f.TagTypes.AddRange(Loadout.TagTypesOf(f.Kind, f.Bonuses.Select(x => x.Stat), single));
            return f;
        }

        static int[] Ints(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int> a)
        {
            if (a == null) return new int[0];
            var o = new int[a.Length]; for (int i = 0; i < o.Length; i++) o[i] = a[i];
            return o;
        }

        /// <summary>A float of the game as the number it was typed as (0.1f -> 0.1, not 0.100000001490116): the shortest text that
        /// reads back to the same float, read as a double - so the live facts hash and score like the fixture.</summary>
        static double Exact(float v) { return double.Parse(v.ToString("R", IC), NumberStyles.Float, IC); }

        /// <summary>A stat without its asset (the bare EType of a bonus): the EType name minus Internal / Team / Player when that is
        /// a key knowledge.json knows ("InternalInstantWeaponReloadChance" -> "InstantWeaponReloadChance").</summary>
        static string StatFromType(string etype, Knowledge k)
        {
            if (string.IsNullOrEmpty(etype)) return "";
            if (k.BadgeStats.ContainsKey(etype)) return etype;
            foreach (var p in new[] { "Internal", "Team", "Player" })
                if (etype.StartsWith(p, StringComparison.Ordinal))
                {
                    string rest = etype.Substring(p.Length);
                    if (k.BadgeStats.ContainsKey(rest) || rest.StartsWith("Hashtag", StringComparison.Ordinal)) return rest;
                }
            return etype;
        }

        /// <summary>The badge as the screen names it: the game's localized name without a trailing " Badge".</summary>
        public static string DisplayName(BadgeFacts f)
        {
            if (f == null) return "";
            string n = (f.Name ?? "").Trim();
            if (n.EndsWith(" Badge", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 6).Trim();
            return n.Length > 0 ? n : f.Short;
        }

        // ------------------------------------------------------------------------------------------------ levels
        /// <summary>Every badge's level from its tree node (0 = locked), the rank check for the UNLOCK hint, and how many read higher
        /// through GetSkillTreeNodeLevel() (Mushroom Mushroom). A badge the game itself calls locked (GetSkillTreeNodeLevel() = 0)
        /// is locked here too: the advice never names a badge the grid greys.</summary>
        public static void ReadLevels(Dictionary<int, int> levels, Dictionary<int, bool> rankOpen, out int raised)
        {
            raised = 0;
            if (_all == null) return;
            foreach (var f in _all)
            {
                var b = f.Ref as GameplayBadgeBase; if (b == null) continue;
                int tree = -1, game = -1;
                SkillTreeUpgradeBase node = null; try { node = NodeOf(b); } catch { }
                if (node != null)
                {
                    try
                    {
                        SkillTreeUpgradeBase rt = null; try { rt = Runtime(node); } catch { }
                        if (rt == null) rt = node;
                        tree = NodeUnlocked(rt) ? NodeLevel(rt) : 0;
                    }
                    catch { tree = -1; }
                }
                try { game = GameLevel(b); } catch { }
                // the tree unreadable: the game's level alone, which a Mushroom Mushroom left active by the last run raises past the
                // node's maximum (+1, and +1 per empty slot) - capped at the maximum, the most a tree node gives
                int lvl = tree >= 0 ? tree : Math.Min(Math.Max(0, game), Math.Max(1, f.Max));
                if (game == 0) lvl = 0;
                else if (game > 0 && tree >= 0 && game > tree) raised++;
                else if (game > 0 && tree > game) lvl = game;
                levels[f.Id] = Math.Max(0, lvl);
                if (rankOpen != null && node != null)
                {
                    int rank = 0; try { rank = NodeRank(node); } catch { }
                    if (rank > 1)
                    {
                        int tl = 0; try { var cp = NodeOwner(node); if (cp != null) tl = G.TreeLevel(cp); } catch { }
                        rankOpen[f.Id] = tl >= (rank - 1) * 20;      // ranks open at class level 20 / 40 / 60 / 80 (Plan.RankClosed)
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ one visit
        public static LoadoutVisit BeginVisit(UIViewRunSetup view, int number)
        {
            var v = new LoadoutVisit { View = view, ViewPtr = Ptr(view), Number = number, At = Time.realtimeSinceStartup };
            if (!EnsureFacts(view)) v.Missing.Add("no badge readable");
            // the grid (set up in the view's OnEnable: it cannot change while the view stays enabled)
            try
            {
                int i = 0;
                foreach (var btn in G.Each(GridOf(view)))
                {
                    int index = i++;
                    if (btn == null) continue;
                    bool active = false; try { active = btn.gameObject.activeInHierarchy; } catch { }
                    if (!active) continue;
                    GameplayBadgeBase b = null; try { b = BadgeOf(btn); } catch { }
                    var cell = new LoadoutCell { Button = btn, ButtonPtr = Ptr(btn), BadgePtr = Ptr(b), Facts = FactsOf(b), Index = index };
                    try { cell.ObjectPtr = Ptr(btn.gameObject); } catch { }
                    try { var lk = LockedOf(btn); cell.Locked = lk != null && lk.activeSelf; } catch { }
                    if (cell.Facts != null) v.Grid.Add(cell);
                }
                v.GridOk = v.Grid.Count > 0;
                if (!v.GridOk) v.Missing.Add("grid empty");
            }
            catch (Exception e) { v.Missing.Add("grid unreadable (" + e.GetBaseException().Message + ")"); }
            // the slot buttons
            try
            {
                int i = 0;
                foreach (var btn in G.Each(SlotButtonsOf(view)))
                {
                    int index = i++;
                    if (btn == null) continue;
                    bool active = false; try { active = btn.gameObject.activeInHierarchy; } catch { }
                    if (!active) continue;
                    var cell = new LoadoutCell { Button = btn, ButtonPtr = Ptr(btn), Index = index, Slot = true };
                    try { cell.ObjectPtr = Ptr(btn.gameObject); } catch { }
                    v.SlotCells.Add(cell);
                }
            }
            catch (Exception e) { v.Missing.Add("slot buttons unreadable (" + e.GetBaseException().Message + ")"); }
            v.SlotButtons = v.SlotCells.Count;
            try { v.GameSlots = MaxSlots(); } catch (Exception e) { v.Missing.Add("GetMaxNumBadges unreadable (" + e.GetBaseException().Message + ")"); }
            try { v.BaseSlots = BaseSlotsConst(); } catch { }
            // levels: they change only in the Training Yard
            int raised; ReadLevels(v.Levels, v.RankOpen, out raised); v.Raised = raised;
            foreach (var c in v.Grid) if (c.Locked) v.Levels[c.Id] = 0;      // the game shows its padlock: locked, whatever the tree says
            if (v.GridOk && _all != null)
                foreach (var f in _all)
                    if (!v.Grid.Any(c => c.Facts == f)) { v.OffGrid.Add(f.Id); Once("offgrid:" + f.Id, "badge " + f.Id + " " + f.Short + " is not on the grid: never advised (it cannot be equipped)"); }
            try { var q = ActiveQuest(); v.Quest = q == null ? "-" : G.Asset(q); } catch { v.Quest = "?"; }
            return v;
        }

        static List<int> Ids(BadgeList list)
        {
            var o = new List<int>();
            foreach (var b in G.Each(list)) { var f = FactsOf(b); if (f != null && !o.Contains(f.Id)) o.Add(f.Id); }
            return o;
        }

        /// <summary>The equipped and the forced badges right now (the selection may change at any click).</summary>
        public static void ReadSelection(LoadoutVisit v)
        {
            v.Equipped.Clear(); v.Forced.Clear();
            var view = v.View;
            try { v.Equipped.AddRange(Ids(SelectedOf(view))); v.EquippedFrom = "view"; }
            catch
            {
                try
                {
                    foreach (var c in v.SlotCells) { GameplayBadgeBase b = null; try { b = BadgeOf(c.Button); } catch { } var f = FactsOf(b); if (f != null && !v.Equipped.Contains(f.Id)) v.Equipped.Add(f.Id); }
                    v.EquippedFrom = "slot buttons";
                }
                catch
                {
                    try { var bar = Bar(); if (bar != null) v.Equipped.AddRange(Ids(BarBadges(bar))); v.EquippedFrom = "start bar"; } catch { v.EquippedFrom = "unknown"; }
                }
            }
            try { v.Forced.AddRange(Ids(ForcedOf(view))); v.ForcedFrom = "view"; }
            catch
            {
                v.ForcedFrom = "IsForcedBadge";
                foreach (var id in v.Equipped)
                {
                    var f = _all == null ? null : _all.FirstOrDefault(x => x.Id == id);
                    var b = f == null ? null : f.Ref as GameplayBadgeBase;
                    try { if (b != null && IsForced(view, b)) v.Forced.Add(id); } catch { v.ForcedFrom = "unknown (none assumed)"; break; }
                }
            }
            // the slots: the game's number (0 included: the game itself then takes no badge), else the slot buttons, else at least
            // what is equipped
            if (v.GameSlots >= 0) { v.Slots = v.GameSlots; v.SlotsNote = ""; }
            else if (v.SlotButtons > 0) { v.Slots = v.SlotButtons; v.SlotsNote = "slots from the slot buttons"; }
            else { v.Slots = Math.Max(v.Equipped.Count, 2); v.SlotsNote = "slots unknown"; }
        }

        static UIStartGameBar Bar() { var m = MainMenuUi(); return m == null ? null : BarField(m); }

        /// <summary>The team leader, the mode and the difficulty the player picked.</summary>
        public static void ReadRun(LoadoutVisit v)
        {
            UIStartGameBar bar = null; try { bar = Bar(); } catch { }
            ClassProperties lead = null;
            try { if (bar != null) { lead = BarLeader(bar); v.LeaderFrom = "bar"; } } catch { }
            if (lead == null)
            {
                try { var m = MainMenuUi(); var h = m == null ? null : HeroField(m); if (h != null) { lead = HeroLeader(h); v.LeaderFrom = "hero screen"; } } catch { }
            }
            v.Leader = "";
            if (lead != null) { try { v.Leader = G.ClassName(lead.characterType); } catch { } }
            if (v.Leader.Length == 0) v.LeaderFrom = "unknown";
            bool modeOk = false;
            try { if (bar != null) { v.Mode = BarMode(bar).ToString(); v.Difficulty = (int)BarDifficulty(bar) + 1; v.ModeFrom = "bar"; modeOk = true; } } catch { }
            if (!modeOk)
            {
                try { v.Mode = ViewMode(v.View).ToString(); v.ModeFrom = "view"; modeOk = true; } catch { }
                try { v.Difficulty = (int)ViewDifficulty(v.View) + 1; } catch { }
            }
            if (!modeOk) { v.Mode = "Normal"; v.Difficulty = 1; v.ModeFrom = "unknown (Normal d1 assumed)"; }
            v.Difficulty = Math.Max(1, Math.Min(5, v.Difficulty));
        }

        // FNV-1a 64 over the pointers and ints that can change while the screen is up: the view, the leader, the mode, the
        // difficulty, the selected and the forced badges. No strings, no names.
        static ulong Mix(ulong h, long x) { for (int i = 0; i < 8; i++) { h ^= (ulong)(x & 0xFF); h *= 1099511628211UL; x >>= 8; } return h; }

        public static ulong Signature(UIViewRunSetup view)
        {
            ulong h = 14695981039346656037UL;
            h = Mix(h, (long)Ptr(view));
            try
            {
                var bar = Bar();
                if (bar != null)
                {
                    try { h = Mix(h, (long)Ptr(BarLeader(bar))); } catch { }
                    try { h = Mix(h, (long)BarMode(bar)); } catch { }
                    try { h = Mix(h, (long)BarDifficulty(bar)); } catch { }
                }
            }
            catch { }
            try { var l = SelectedOf(view); int n = l == null ? 0 : l.Count; for (int i = 0; i < n && i < 8; i++) h = Mix(h, (long)Ptr(l[i])); h = Mix(h, n); } catch { }
            h = Mix(h, -1);
            try { var l = ForcedOf(view); int n = l == null ? 0 : l.Count; for (int i = 0; i < n && i < 8; i++) h = Mix(h, (long)Ptr(l[i])); h = Mix(h, n); } catch { }
            return h;
        }

        /// <summary>The selection as ids, read straight from the view (LoadoutEquip checks it before and after every press).</summary>
        public static List<int> LiveSelection(UIViewRunSetup view)
        {
            try { return Ids(SelectedOf(view)); } catch { return null; }
        }

        // ------------------------------------------------------------------------------------------------ the advice's input
        static readonly Dictionary<string, PowerFacts> _powers = new Dictionary<string, PowerFacts>(StringComparer.Ordinal);
        static readonly Dictionary<string, bool> _clip = new Dictionary<string, bool>(StringComparer.Ordinal);
        static bool _powersRead;

        // what every weapon, ability and evolution of every class deals (asset data: once a session)
        static void ReadPowers()
        {
            if (_powersRead) return;
            _powersRead = true;
            try
            {
                using (G.Cache())
                {
                    Action<PowerupBase> add = p =>
                    {
                        if (p == null) return;
                        string n = G.Name(p); if (string.IsNullOrEmpty(n) || n == "?" || _powers.ContainsKey(n)) return;
                        _powers[n] = G.Facts(p);
                        try { var w = p.TryCast<WeaponUpgradePowerup>(); var wp = w == null ? null : w.attachedWeaponProperties; if (wp != null) _clip[n] = wp.HasClipBehavior && wp.ClipSize > 1; } catch { }
                    };
                    foreach (CT cls in Enum.GetValues(typeof(CT)))
                    {
                        if (cls == CT.None || cls == CT.NumCharacters) continue;
                        var cp = G.PropsOf(cls); if (cp == null) continue;
                        try { foreach (var w in G.Each(cp.weaponPowerups)) add(w); } catch { }
                        try
                        {
                            foreach (var a in G.Each(cp.abilityBasePowerups))
                            {
                                add(a);
                                try { if (a != null) { add(a.abilityEvolutionA); add(a.abilityEvolutionB); } } catch { }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e) { Once("powers", "the classes' weapons and abilities could not be read (" + e.GetBaseException().Message + "): the run shape has no damage types", true); }
            if (_powers.Count == 0) _powersRead = false;      // the class data was not there yet: ask again next time
        }

        /// <summary>A weapon, ability or evolution by its English name (the kits' spelling): what it deals and its tags.</summary>
        public static PowerFacts PowerFactsOf(string name)
        {
            ReadPowers();
            PowerFacts f; return name != null && _powers.TryGetValue(name, out f) ? f : null;
        }

        // the share of the weapon line's weight on clip weapons (the Dexterity badge's instant reloads): the line as RunShape
        // weighs it (tier 1 0.15, tier 2 0.25, the branch 0.60 or the three tier-3 weapons 0.20 each); -1 = unknown
        static double ClipShare(string leader, Build build)
        {
            var kit = Builds.KitOf(leader);
            if (kit == null || kit.Line == null || kit.Line.Length < 2) return -1;
            string branch = build != null ? build.Branch ?? "" : "";
            if (build == null) { string g; if (Knowledge.Current.WeaponBranch.TryGetValue(leader ?? "", out g)) branch = g ?? ""; }
            var parts = new List<KeyValuePair<string, double>> { new KeyValuePair<string, double>(kit.Line[0], 0.15), new KeyValuePair<string, double>(kit.Line[1], 0.25) };
            if (branch.Length > 0) parts.Add(new KeyValuePair<string, double>(branch, 0.60));
            else for (int i = 2; i < Math.Min(5, kit.Line.Length); i++) parts.Add(new KeyValuePair<string, double>(kit.Line[i], 0.20));
            double w = 0, clip = 0;
            foreach (var p in parts) { bool c; if (!_clip.TryGetValue(p.Key, out c)) return -1; w += p.Value; if (c) clip += p.Value; }
            return w > 0 ? clip / w : -1;
        }

        static string _recruitsFor; static List<KeyValuePair<string, Build>> _recruits;

        // the two survivors the leader would most likely recruit (Ranker.Recruitable on a stand-in squad of the leader alone):
        // only while knowledge.json's badgeRules.recruitWeight is above 0 (OD3: 0 = the leader alone)
        static List<KeyValuePair<string, Build>> Recruits(string leader)
        {
            if (_recruitsFor == leader && _recruits != null) return _recruits;
            _recruitsFor = leader; _recruits = new List<KeyValuePair<string, Build>>();
            try
            {
                CT t = CT.None; bool found = false;
                foreach (CT cls in Enum.GetValues(typeof(CT))) if (G.ClassName(cls) == leader) { t = cls; found = true; break; }
                if (!found) return _recruits;
                using (G.Cache())
                {
                    var s = new Snapshot();
                    var props = G.PropsOf(t);
                    s.Squad.Add(new Survivor { Type = t, Name = leader, Props = props, Leader = true, Snap = s, TreeLevel = props != null ? G.TreeLevel(props) : 0 });
                    var list = Ranker.Recruitable(s);
                    list.Sort(Recruit.Compare);
                    foreach (var r in list.Take(2)) _recruits.Add(new KeyValuePair<string, Build>(r.Name, Builds.For(r.Name)));
                }
            }
            catch (Exception e) { Once("recruits", "the likely recruits could not be worked out (" + e.GetBaseException().Message + "): leader alone", true); }
            return _recruits;
        }

        /// <summary>The input of one advice for a leader, build, mode and difficulty with the levels, slots and selection given.</summary>
        public static LoadoutInput Input(string leader, Build build, string mode, int difficulty, int slots, IDictionary<int, int> levels, IDictionary<int, bool> rankOpen,
            ICollection<int> offGrid, IEnumerable<int> forced, IEnumerable<int> equipped)
        {
            var k = Knowledge.Current; var d = Doctrine.Current;
            ReadPowers();
            var recruits = k.BadgeRules.RecruitWeight > 0 ? Recruits(leader) : null;
            var inp = Loadout.Prepare(leader, build, mode, difficulty, d, k, PowerFactsOf, recruits);
            // a build the player did not select (a lent pack default Auto follows) is named "the X build" in the reasons, never "your"
            // (F02, Builds.Your; 0.14.0: no "(Auto)" on screen): the player's own build is the one the selection names
            string sel = Builds.SelectedId(leader);
            inp.Shape.Auto = build != null && (sel == Builds.AutoId || !string.Equals(sel, build.Id, StringComparison.OrdinalIgnoreCase));
            double clip = ClipShare(leader, build);
            if (clip >= 0) inp.Shape.Clip = clip;
            try { int at = SpecialAt(); if (at > 0) inp.Ctx.SpecialAt = at; } catch { }
            inp.Slots = Math.Max(0, slots);
            if (levels != null) foreach (var kv in levels) inp.Levels[kv.Key] = kv.Value;
            if (rankOpen != null) foreach (var kv in rankOpen) inp.RankOpen[kv.Key] = kv.Value;
            if (offGrid != null) foreach (var id in offGrid) inp.OffGrid.Add(id);
            if (forced != null) inp.Forced.AddRange(forced);
            if (equipped != null) inp.Equipped.AddRange(equipped);
            inp.Inventory = Short; inp.KnowledgeHash = Loadout.KnowledgeHash(k);
            return inp;
        }

        public static LoadoutInput Input(LoadoutVisit v, Build build)
        {
            return Input(v.Leader, build, v.Mode, v.Difficulty, v.Slots, v.Levels, v.RankOpen, v.OffGrid, v.Forced, v.Equipped);
        }

        // ------------------------------------------------------------------------------------------------ outside the screen
        /// <summary>The slots the game gives right now (the BADGES page): GetMaxNumBadges(), else 4.</summary>
        public static int SlotsNow() { try { int n = MaxSlots(); return n > 0 ? n : 4; } catch { return 4; } }

        /// <summary>The badges the run setup screen holds selected (the BADGES page, before any visit this session): empty = none.</summary>
        public static List<int> EquippedNow()
        {
            try { var m = MainMenuUi(); var v = m == null ? null : SetupField(m); if (v != null) return Ids(SelectedOf(v)); } catch { }
            return new List<int>();
        }

        /// <summary>The run setup view the main menu holds (the field, filled once the screen was opened: no search); null = none.</summary>
        public static UIViewRunSetup SetupView() { try { var m = MainMenuUi(); return m == null ? null : SetupField(m); } catch { return null; } }

        /// <summary>This run's badges (GameplayMaster.selectedBadges) with their levels: the tree's "L2", or the raised level with a star
        /// ("L3*") when Mushroom Mushroom raises it (SPEC 7, Advisor.cs row).</summary>
        public static List<KeyValuePair<BadgeFacts, string>> RunBadges()
        {
            var o = new List<KeyValuePair<BadgeFacts, string>>();
            EnsureFacts();
            BadgeList list = null; try { list = RunList(); } catch { return null; }
            if (list == null) return null;
            var levels = new Dictionary<int, int>(); int raised; ReadLevels(levels, null, out raised);
            foreach (var b in G.Each(list))
            {
                var f = FactsOf(b); if (f == null) continue;
                int tree; levels.TryGetValue(f.Id, out tree);
                int game = -1; try { game = GameLevel(b); } catch { }
                o.Add(new KeyValuePair<BadgeFacts, string>(f, game > tree && tree > 0 ? "L" + game + "*" : "L" + Math.Max(tree, 1)));
            }
            return o;
        }

        // ------------------------------------------------------------------------------------------------ load-time checks
        static readonly string[][] Members =
        {
            new[] { "PowerupReferences", "Get", "badges", "GetBadgeById" },
            new[] { "UIViewRunSetup", "availableBadgesButtons", "selectedBadgesButtons", "_selectedBadges", "_forcedBadges", "IsForcedBadge", "CanSelectMoreBadges",
                "currentBadgeName", "currentBadgeDescription", "currentBadgeBonus", "difficultyButtons", "selectedDifficultyButton", "_canInteract", "selectedGameMode",
                "OnBadgeHighlight", "RefreshSelectedBadgeButtons", "OnDisable", "Update" },
            new[] { "UIViewRunSetupBadgeButton", "badgeReference", "lockedContainer", "selectedContainer", "iconImage", "_isInteractable", "OnHighlight" },
            new[] { "UIViewRunSetupDifficultyButton", "GetDifficulty" },
            new[] { "DifficultySettings", "difficulty" },
            new[] { "GamePermanentData", "Get", "GetMaxNumBadges", "BASE_NUM_BADGES" },
            new[] { "UIStartGameBar", "selectedCharacter", "selectedGameplayMode", "selectedDifficulty", "selectedBadges" },
            new[] { "UIMainMenu", "s_instance", "Get", "_uiViewRunSetup", "_uiStartGameBar", "_uiViewChooseHero" },
            new[] { "UIViewChooseHero", "chosenClassProperties" },
            new[] { "GameplayBadgeBase", "badgeSortOrder", "badgeBaseId", "icon", "skillTreeRequirement", "GetNameText", "GetCurrentLevelDescriptionText",
                "GetSkillTreeNodeLevel", "GetSkillTreeNodeMaxLevel" },
            new[] { "GameplayBadgeStatBoost", "bonusesPerLevel" },
            new[] { "GameplayBadgeHashtagBoost", "bonusesPerLevel", "hashtagsOnLevel", "type" },
            new[] { "GameplayBadgeHashtagPhysicalBoost", "bonusesPerLevel", "hashtagsPerLevel" },
            new[] { "GameplayBadgeHashtagElementalBoost", "bonusesPerLevel", "hashtagsPerLevel" },
            new[] { "PowerupBase+StatisticBonus", "targetStatistic", "bonusType", "value", "valueTemplate" },
            new[] { "PlayerStatistic", "statisticType" },
            new[] { "SkillTreeUpgradeBase", "GetRuntimeInstance", "IsUnlocked", "GetCurrentLevel", "targetClassProperties", "rankRequirement" },
            new[] { "GameplayMaster", "selectedBadges", "GetPlayerTypeStatisticFinalValue", "currentMainCharacterType" },
            new[] { "GameQuestManager", "Get", "ActiveQuest" },
            new[] { "HashtagSystem", "NumRequiredForSpecial" },
        };

        static readonly Dictionary<string, string> Fallbacks = new Dictionary<string, string>
        {
            { "UIViewRunSetup._forcedBadges", "forced badges read through IsForcedBadge" },
            { "UIViewRunSetup._selectedBadges", "the selection read from the slot buttons" },
            { "PowerupReferences.badges", "the badges read from the grid buttons" },
            { "GamePermanentData.GetMaxNumBadges", "slots from the slot buttons" },
            { "UIStartGameBar.selectedCharacter", "the leader read from the team leader screen" },
            { "UIViewRunSetup.OnBadgeHighlight", "the cursor followed through the button's OnHighlight and the selection poll" },
            { "UIViewRunSetupBadgeButton.OnHighlight", "the cursor followed through OnBadgeHighlight and the selection poll" },
            { "UIViewRunSetup.RefreshSelectedBadgeButtons", "selection changes caught by the 0.25 s signature poll" },
            { "UIViewRunSetup.OnDisable", "the close seen by the tick" },
            { "UIViewRunSetup.Update", "the fallback tick from GameMaster.Update" },
        };

        /// <summary>At load: every game member the badge advice reads, looked up in the interop ("[loadout] game members: n of n readable").</summary>
        public static void CheckMembers()
        {
            var asm = typeof(UIViewRunSetup).Assembly;
            int total = 0; var missing = new List<string>();
            foreach (var row in Members)
            {
                var t = asm.GetType(row[0]);
                for (int i = 1; i < row.Length; i++)
                {
                    total++;
                    // plain reflection (AccessTools would log every miss of its property / field / method tries)
                    bool ok = t != null && t.GetMember(row[i], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy).Length > 0;
                    if (!ok) missing.Add(row[0].Replace('+', '.') + "." + row[i]);
                }
            }
            if (missing.Count == 0) { Plugin.Logger.LogInfo("[loadout] game members: " + total + " of " + total + " readable"); return; }
            Plugin.Logger.LogWarning("[loadout] game members: " + (total - missing.Count) + " of " + total + " readable - missing " + string.Join(", ", missing.Select(m =>
            { string fb; return m + (Fallbacks.TryGetValue(m, out fb) ? " (" + fb + ")" : ""); })));
        }

        static readonly string[] KnownClasses = { "GameplayBadgeHashtagBoost", "GameplayBadgeHashtagElementalBoost", "GameplayBadgeHashtagPhysicalBoost", "GameplayBadgeStatBoost" };

        /// <summary>At load: the badge classes of this game build; a new one is said (its badges are not rated).</summary>
        public static void CheckClasses()
        {
            Type[] types;
            try { types = typeof(GameplayBadgeBase).Assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types; }
            var names = new List<string>();
            foreach (var t in types) if (t != null && t.IsSubclassOf(typeof(GameplayBadgeBase))) names.Add(t.Name);
            names.Sort(StringComparer.Ordinal);
            var unknown = names.Where(n => !KnownClasses.Contains(n)).ToList();
            var gone = KnownClasses.Where(n => !names.Contains(n)).ToList();
            Plugin.Logger.LogInfo("[loadout] badge classes: " + (names.Count > 0 ? string.Join(", ", names) : "none") + (unknown.Count == 0 && gone.Count == 0 ? " - all readable"
                : (gone.Count > 0 ? " - gone: " + string.Join(", ", gone) : "")));
            foreach (var u in unknown) Plugin.Logger.LogWarning("[loadout] badge class " + u + " is new: its badges are not rated (worth a look)");
        }

        // ------------------------------------------------------------------------------------------------ warm-up
        /// <summary>One advice on the main menu with a stand-in leader (Warmup.cs): the facts, the kits and the code warm before the
        /// first visit. Thrown away; nothing is drawn or logged but the [warmup] line.</summary>
        public static string Warm()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (!EnsureFacts()) return "no badge registry on the main menu";
            string leader = null;
            foreach (CT cls in Enum.GetValues(typeof(CT)))
            {
                if (cls == CT.None || cls == CT.NumCharacters) continue;
                var props = G.PropsOf(cls);
                if (props != null && G.Unlocked(props)) { leader = G.ClassName(cls); break; }
            }
            if (leader == null) return "no unlocked class";
            var levels = new Dictionary<int, int>(); var open = new Dictionary<int, bool>(); int raised;
            ReadLevels(levels, open, out raised);
            var inp = Input(leader, Builds.For(leader), "Normal", 1, SlotsNow(), levels, open, null, null, null);
            var a = Loadout.Recommend(inp, _all, Knowledge.Current, 0);
            LoadoutView.Of(a, new int[0], new int[0], inp.Slots, LoadoutDetail.Full, Knowledge.Current);
            return sw.Elapsed.TotalMilliseconds.ToString("0", IC) + " ms (stand-in " + leader + ", " + a.Picks.Count + " picks)";
        }
    }
}
