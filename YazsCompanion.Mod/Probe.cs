// A one-off data probe (config [Debug] Probe, off by default): about six seconds after launch, on the main menu,
// write what the rules and the menu are built against into BepInEx\plugins\YazsCompanion\probe.json and the log -
// every item and powerup with the exact fields the game itself uses (powerup tags, damage type tags per level,
// highlighted statistics, mode availability), the team passives that boost a powerup tag, the input actions the
// game registered, and the layout of the main menu's button column; since 0.13.0 also every badge as the run setup screen's
// advice reads it (and as the game itself reports it) and the badge slots. For development; it changes nothing.
// MeasureBadges() is the [Debug] PreviewSetup walk's one line 3 s into a run: the badge statistics the advice only estimates.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    internal static class Probe
    {
        static bool _done; static float _at = -1f;

        public static void Tick()
        {
            bool on = false; try { on = Plugin.ProbeFlag != null && Plugin.ProbeFlag.Value; } catch { }
            if (_done || !on) return;
            float now = Time.realtimeSinceStartup;
            if (_at < 0) { _at = now + 6f; return; }
            if (now < _at) return;
            _done = true;
            var root = new Dictionary<string, object>();
            Section(root, "items", Items);
            Section(root, "powerups", Powerups);
            Section(root, "taggedBoosts", TaggedBoosts);
            Section(root, "actions", Actions);
            Section(root, "mainMenu", MainMenu);
            Section(root, "badges", Badges);
            Section(root, "badgeSlots", BadgeSlots);
            try
            {
                string path = Path.Combine(Plugin.PluginDir, "probe.json");
                File.WriteAllText(path, JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
                Plugin.Logger.LogInfo("[probe] written " + path);
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[probe] write: " + e.Message); }
        }

        static void Section(Dictionary<string, object> root, string key, Func<object> read)
        {
            try { root[key] = read(); }
            catch (Exception e) { root[key] = "failed: " + e.Message; Plugin.Logger.LogWarning("[probe] " + key + ": " + e); }
        }

        static List<string> Stats(Il2CppSystem.Collections.Generic.List<PlayerStatistic> list)
        {
            var o = new List<string>();
            foreach (var st in G.Each(list)) { if (st == null) continue; try { o.Add(st.statisticType.ToString()); } catch { } }
            return o;
        }

        static object Items()
        {
            var o = new List<object>();
            var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<ItemBase>());
            for (int i = 0; i < all.Length; i++)
            {
                var it = all[i].TryCast<ItemBase>(); if (it == null) continue;
                var d = new Dictionary<string, object>();
                d["asset"] = G.Asset(it); d["name"] = G.Name(it);
                try { d["id"] = it.itemBaseId; } catch { }
                try { var tags = new List<string>(); foreach (var t in G.Each(it.stringTags)) tags.Add(t); d["stringTags"] = tags; } catch { }
                try { d["stats"] = Stats(it.highlightedStatistics); } catch { }
                try { d["modes"] = (int)it.modeAvailability; } catch { }
                try { d["healing"] = it.isHealingItem; } catch { }
                try { d["animal"] = it.isAnimalItem; } catch { }
                try { d["parca"] = it.isParcaItem; } catch { }
                try { d["max"] = it.numMaxCanCarry; } catch { }
                try { d["swappable"] = it.isSwappable; } catch { }
                try { d["inPool"] = it.isVisibleInItemsPool; } catch { }
                try { d["desc"] = ItemRules.RichTag.Replace(it.EnglishDescription ?? "", ""); } catch { }
                o.Add(d);
            }
            Plugin.Logger.LogInfo("[probe] items: " + o.Count);
            return o;
        }

        static readonly HashtagSystem.EHashtagType[] Types =
        {
            HashtagSystem.EHashtagType.Fire, HashtagSystem.EHashtagType.Electric, HashtagSystem.EHashtagType.Toxic, HashtagSystem.EHashtagType.Ice,
            HashtagSystem.EHashtagType.Explosive, HashtagSystem.EHashtagType.Kinetic, HashtagSystem.EHashtagType.Slashing
        };

        static object Powerups()
        {
            var o = new List<object>();
            var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<PowerupBase>());
            for (int i = 0; i < all.Length; i++)
            {
                var p = all[i].TryCast<PowerupBase>(); if (p == null) continue;
                var d = new Dictionary<string, object>();
                d["asset"] = G.Asset(p); d["name"] = G.Name(p);
                try { d["type"] = p.GetIl2CppType().Name; } catch { }
                try { var cp = p.targetClassProperties; if (cp != null) d["class"] = G.ClassName(cp.characterType); } catch { }
                try { d["ability"] = p.isAbility; } catch { }
                try { d["healing"] = p.isHealingAbility; } catch { }
                try { d["modes"] = (int)p.modeAvailability; } catch { }
                try { d["max"] = p.MaxLevel; } catch { }
                try { var tags = new List<string>(); foreach (var t in G.Each(p.powerupTags)) tags.Add(t.ToString()); d["tags"] = tags; } catch { }
                try { var ht = new List<string>(); foreach (var t in G.Each(p.hashtagTypes)) ht.Add(G.TagName(t) ?? t.ToString()); d["damage"] = ht; } catch { }
                try
                {
                    var per = new Dictionary<string, List<int>>();
                    int max = G.MaxLevel(p);
                    foreach (var t in Types)
                    {
                        var row = new List<int>(); int sum = 0;
                        for (int l = 1; l <= max; l++) { int n = 0; try { n = p.GetNumHashtagsForLevel(t, l); } catch { } row.Add(n); sum += n; }
                        if (sum > 0) per[G.TagName(t)] = row;
                    }
                    if (per.Count > 0) d["tagPointsPerLevel"] = per;
                }
                catch { }
                try { d["stats"] = Stats(p.highlightedStatistics); } catch { }
                try { if (p.evolutionBaseAbility != null) d["evolutionOf"] = G.Name(p.evolutionBaseAbility); } catch { }
                try { if (p.abilityEvolutionA != null) d["evoA"] = G.Name(p.abilityEvolutionA); } catch { }
                try { if (p.abilityEvolutionB != null) d["evoB"] = G.Name(p.abilityEvolutionB); } catch { }
                try { if (p.previousLevelWeapon != null) d["previousWeapon"] = G.Name(p.previousLevelWeapon); } catch { }
                try { var w = p.TryCast<WeaponUpgradePowerup>(); if (w != null) { d["weaponTier"] = w.weaponTier.ToString(); d["weaponIndex"] = w.weaponIndex; } } catch { }
                try
                {
                    var w = p.TryCast<WeaponUpgradePowerup>(); var wp = w == null ? null : w.attachedWeaponProperties;
                    if (wp != null)
                    {
                        d["range"] = wp.Range; d["clip"] = wp.ClipSize; d["hasClip"] = wp.HasClipBehavior;
                        var rm = wp.rangeMod; d["rangeMod"] = rm.Close.ToString("0.##") + "/" + rm.Medium.ToString("0.##") + "/" + rm.Long.ToString("0.##");
                        d["overrideClose"] = wp.overrideCloseRange ? wp.overrideCloseRangeValue : -1f; d["overrideLong"] = wp.overrideLongRange ? wp.overrideLongRangeValue : -1f;
                    }
                }
                catch { }
                try { d["sprite"] = p.powerupSprite != null ? p.powerupSprite.name : null; } catch { }
                try { d["desc"] = ItemRules.RichTag.Replace(p.EnglishDescription ?? "", ""); } catch { }
                o.Add(d);
            }
            Plugin.Logger.LogInfo("[probe] powerups: " + o.Count);
            return o;
        }

        // ---- 0.13.0: the badges, as LoadoutState reads them and as the game reports them ----
        static object Badges()
        {
            if (!LoadoutState.EnsureFacts()) return "no badge registry";
            var levels = new Dictionary<int, int>(); var open = new Dictionary<int, bool>(); int raised;
            LoadoutState.ReadLevels(levels, open, out raised);
            var grid = new Dictionary<IntPtr, int>();
            try
            {
                var view = LoadoutState.SetupView();
                if (view != null) { int i = 0; foreach (var btn in G.Each(view.availableBadgesButtons)) { try { var b = btn == null ? null : btn.badgeReference; if (b != null) grid[b.Pointer] = i; } catch { } i++; } }
            }
            catch { }
            var o = new List<object>();
            foreach (var f in LoadoutState.All)
            {
                var d = new Dictionary<string, object>();
                d["sortOrder"] = f.Sort; d["baseId"] = f.Id; d["asset"] = f.Asset; d["class"] = f.KindName; d["kind"] = f.Kind.ToString();
                d["name"] = f.Name; d["owner"] = f.Owner; d["rank"] = f.Rank; d["max"] = f.Max;
                var bon = new List<object>();
                foreach (var b in f.Bonuses) bon.Add(new Dictionary<string, object> { { "stat", b.Stat }, { "type", b.Type }, { "value", b.PerLevel }, { "template", b.Template } });
                d["bonuses"] = bon; d["tags"] = f.TagTypes; d["points"] = f.TagPoints;
                int lvl; levels.TryGetValue(f.Id, out lvl); d["treeLevel"] = lvl;
                bool rank; if (open.TryGetValue(f.Id, out rank)) d["rankOpen"] = rank;
                var g = f.Ref as GameplayBadgeBase;
                if (g != null)
                {
                    try { d["gameLevel"] = g.GetSkillTreeNodeLevel(); } catch { }
                    try { d["unlocked"] = g.IsUnlocked(); } catch { }
                    try { int gi; if (grid.TryGetValue(g.Pointer, out gi)) d["gridIndex"] = gi; } catch { }
                }
                o.Add(d);
            }
            Plugin.Logger.LogInfo("[probe] badges: " + o.Count + " (" + LoadoutState.Source + ", hash " + LoadoutState.Short + (LoadoutState.IsReference ? " = the reference" : "") + ")");
            return o;
        }

        static object BadgeSlots()
        {
            var d = new Dictionary<string, object>();
            try { d["maxNumBadges"] = GamePermanentData.Get.GetMaxNumBadges(); } catch (Exception e) { d["maxNumBadges"] = "failed: " + e.Message; }
            try { d["baseNumBadges"] = GamePermanentData.BASE_NUM_BADGES; } catch { }
            try
            {
                var nodes = new List<object>();
                foreach (var n in G.Each(GamePermanentData.Get.badgeUnlockUpgrades))
                {
                    if (n == null) continue;
                    nodes.Add(new Dictionary<string, object> { { "asset", G.Asset(n) }, { "level", G.NodeLevel(n) }, { "max", G.NodeMax(n) } });
                }
                d["slotNodes"] = nodes;
            }
            catch { }
            try
            {
                var byId = new Dictionary<string, object>();
                var refs = PowerupReferences.Get;
                for (int i = 0; i <= 35; i++) { try { var b = refs.GetBadgeById(i); byId[i.ToString()] = b == null ? null : G.Asset(b); } catch (Exception e) { byId[i.ToString()] = "failed: " + e.GetType().Name; } }
                d["badgeById"] = byId;
            }
            catch { }
            return d;
        }

        /// <summary>The [Debug] PreviewSetup walk, 3 s into a run (the player's own badges; no badge is clicked): the statistics the
        /// badge advice only estimates (SPEC 11: U1 tag points vs the Hashtag multiplier, U2 player stats on another class, U3 the
        /// crit pools), as one line.</summary>
        public static void MeasureBadges()
        {
            try
            {
                var gm = GameplayMaster.s_instance;
                if (gm == null) { Plugin.Logger.LogInfo("[loadout] measure: no run"); return; }
                var lead = gm.currentMainCharacterType;
                var badges = LoadoutState.RunBadges();
                string names = badges == null ? "unreadable" : badges.Count == 0 ? "none" : string.Join(", ", badges.ConvertAll(x => x.Key.Short + " " + x.Value));
                int elec = -1; try { elec = gm.hashtagSystem.GetNumType(HashtagSystem.EHashtagType.Electric); } catch { }
                float he = float.NaN; try { he = gm.teamStatistics.GetStatisticValue(PlayerStatistic.EType.TeamHashtagElectric); } catch { }
                float frLead = float.NaN, frOther = float.NaN, cc = float.NaN, cd = float.NaN; string other = "?";
                try { frLead = gm.GetPlayerTypeStatisticFinalValue(lead, PlayerStatistic.EType.PlayerWeaponFireRate); } catch { }
                try
                {
                    foreach (GamePlayer.CharacterType cls in Enum.GetValues(typeof(GamePlayer.CharacterType)))
                    {
                        if (cls == GamePlayer.CharacterType.None || cls == GamePlayer.CharacterType.NumCharacters || cls == lead) continue;
                        other = G.ClassName(cls);
                        try { frOther = gm.GetPlayerTypeStatisticFinalValue(cls, PlayerStatistic.EType.PlayerWeaponFireRate); } catch (Exception e) { other += " (" + e.GetType().Name + ")"; }
                        break;
                    }
                }
                catch { }
                try { cc = gm.GetPlayerTypeStatisticFinalValue(lead, PlayerStatistic.EType.PlayerWeaponCritChance); } catch { }
                try { cd = gm.GetPlayerTypeStatisticFinalValue(lead, PlayerStatistic.EType.PlayerWeaponCritDamage); } catch { }
                Func<float, string> n3 = x => float.IsNaN(x) ? "?" : x.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
                Plugin.Logger.LogInfo("[loadout] measure: leader " + G.ClassName(lead) + " | badges " + names + " | Electric points " + (elec >= 0 ? elec.ToString() : "?")
                    + " | TeamHashtagElectric " + n3(he) + " (1 + the badge's % alone; + 0.02 per point would mean the tag points share this stat)"
                    + " | PlayerWeaponFireRate leader " + n3(frLead) + ", " + other + " " + n3(frOther) + " | PlayerWeaponCritChance " + n3(cc) + ", PlayerWeaponCritDamage " + n3(cd));
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[loadout] measure: failed - " + e.Message); }
        }

        static object TaggedBoosts()
        {
            var o = new List<object>();
            foreach (var n in G.AllNodes())
            {
                var t = n.TryCast<SkillTreeUpgradeTaggedPowerupBoost>(); if (t == null) continue;
                var d = new Dictionary<string, object>();
                d["node"] = G.Asset(n);
                try { var cp = n.targetClassProperties; if (cp != null) d["class"] = G.ClassName(cp.characterType); } catch { }
                try { d["requiredTag"] = t.requiredTag.ToString(); } catch { }
                try { d["excludeTag"] = t.excludeTag; d["excludedTag"] = t.excludedTag.ToString(); } catch { }
                try { d["abilitiesOnly"] = t.abilitiesOnly; } catch { }
                try { d["level"] = G.NodeLevel(n); } catch { }
                o.Add(d);
            }
            return o;
        }

        static object Actions()
        {
            var o = new List<string>();
            for (int id = 0; id < 120; id++)
            {
                try
                {
                    var a = Rewired.ReInput.mapping.GetAction(id);
                    if (a == null) continue;
                    o.Add(id + " " + a.name + " (" + a.type + ")");
                }
                catch { }
            }
            Plugin.Logger.LogInfo("[probe] actions: " + string.Join(", ", o));
            return o;
        }

        static object MainMenu()
        {
            var o = new Dictionary<string, object>();
            UIViewMainMenu menu = null;
            var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<UIViewMainMenu>());
            for (int i = 0; i < all.Length; i++) { var m = all[i].TryCast<UIViewMainMenu>(); if (m != null && m.gameObject.activeInHierarchy) { menu = m; break; } }
            if (menu == null) return "no active main menu";
            try { var rt = menu.transform.TryCast<RectTransform>(); o["viewRect"] = rt.rect.width + "x" + rt.rect.height; } catch { }
            try { o["canvas"] = CanvasInfo(menu.transform); } catch { }
            try
            {
                var vlg = menu.buttonsVerticalLayoutGroup;
                o["layout"] = "spacing " + vlg.spacing + ", align " + vlg.childAlignment + ", controlW " + vlg.childControlWidth + ", controlH " + vlg.childControlHeight
                    + ", expandW " + vlg.childForceExpandWidth + ", expandH " + vlg.childForceExpandHeight + ", padding " + vlg.padding.left + "/" + vlg.padding.top;
                var rows = new List<object>();
                var tr = vlg.transform;
                for (int i = 0; i < tr.childCount; i++) rows.Add(Tree(tr.GetChild(i), 0, 1));
                o["column"] = rows;
            }
            catch (Exception e) { o["layoutError"] = e.Message; }
            try { o["achievementsButton"] = Tree(menu.achievementsButton.transform, 0, 6); } catch (Exception e) { o["buttonError"] = e.Message; }
            try
            {
                var fix = menu.buttonsFixNavigationVertical; var names = new List<string>();
                if (fix != null) for (int i = 0; i < fix.Length; i++) names.Add(fix[i] == null ? "null" : fix[i].name);
                o["fixNavigationVertical"] = names;
            }
            catch { }
            try
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                o["eventSystem"] = es == null ? "none" : es.name + ", selected " + (es.currentSelectedGameObject == null ? "none" : es.currentSelectedGameObject.name)
                    + ", module " + (es.currentInputModule == null ? "none" : es.currentInputModule.GetIl2CppType().Name);
            }
            catch { }
            return o;
        }

        static string CanvasInfo(Transform t)
        {
            var c = t.GetComponentInParent<Canvas>(); if (c == null) return "none";
            var root = c.rootCanvas; var rt = root.transform.TryCast<RectTransform>();
            string s = root.name + " " + root.renderMode + " order " + root.sortingOrder + " rect " + rt.rect.width + "x" + rt.rect.height + " scale " + rt.localScale.x.ToString("0.0000");
            var sc = root.GetComponent<CanvasScaler>();
            if (sc != null) s += " scaler " + sc.uiScaleMode + " ref " + sc.referenceResolution.x + "x" + sc.referenceResolution.y + " match " + sc.screenMatchMode + " " + sc.matchWidthOrHeight;
            return s;
        }

        static object Tree(Transform t, int depth, int maxDepth)
        {
            var d = new Dictionary<string, object>();
            d["name"] = t.name + (t.gameObject.activeSelf ? "" : " (inactive)");
            try
            {
                var rt = t.TryCast<RectTransform>();
                if (rt != null) d["rect"] = rt.rect.width.ToString("0") + "x" + rt.rect.height.ToString("0") + " at " + rt.anchoredPosition.x.ToString("0") + "," + rt.anchoredPosition.y.ToString("0")
                    + " anchors " + rt.anchorMin.x + "," + rt.anchorMin.y + "-" + rt.anchorMax.x + "," + rt.anchorMax.y + " pivot " + rt.pivot.x + "," + rt.pivot.y;
            }
            catch { }
            var comps = new List<string>();
            try
            {
                var cs = t.GetComponents<Component>();
                for (int i = 0; i < cs.Length; i++)
                {
                    var c = cs[i]; if (c == null) continue;
                    string n = c.GetIl2CppType().Name;
                    if (n == "RectTransform" || n == "CanvasRenderer") continue;
                    var img = c.TryCast<Image>();
                    if (img != null) n += "[" + (img.sprite != null ? img.sprite.name : "no sprite") + " " + img.type + " " + Theme.Hex(img.color) + " a" + img.color.a.ToString("0.00") + "]";
                    var tmp = c.TryCast<TextMeshProUGUI>();
                    if (tmp != null) n += "['" + tmp.text + "' " + (tmp.font != null ? tmp.font.name : "?") + " " + tmp.fontSize + " " + tmp.fontStyle + " " + Theme.Hex(tmp.color) + " align " + tmp.alignment + "]";
                    var btn = c.TryCast<Button>();
                    if (btn != null) n += "[transition " + btn.transition + ", nav " + btn.navigation.mode + ", target " + (btn.targetGraphic != null ? btn.targetGraphic.name : "none") + "]";
                    var le = c.TryCast<LayoutElement>();
                    if (le != null) n += "[min " + le.minWidth + "x" + le.minHeight + " pref " + le.preferredWidth + "x" + le.preferredHeight + "]";
                    comps.Add(n);
                }
            }
            catch { }
            d["components"] = comps;
            if (depth < maxDepth && t.childCount > 0)
            {
                var kids = new List<object>();
                for (int i = 0; i < t.childCount; i++) kids.Add(Tree(t.GetChild(i), depth + 1, maxDepth));
                d["children"] = kids;
            }
            return d;
        }
    }
}
