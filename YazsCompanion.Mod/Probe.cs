// A one-off data probe (config [Debug] Probe, off by default): about six seconds after launch, on the main menu,
// write what the rules and the menu are built against into BepInEx\plugins\YazsCompanion\probe.json and the log -
// every item and powerup with the exact fields the game itself uses (powerup tags, damage type tags per level,
// highlighted statistics, mode availability), the team passives that boost a powerup tag, the input actions the
// game registered, and the layout of the main menu's button column. For development; it changes nothing.
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
