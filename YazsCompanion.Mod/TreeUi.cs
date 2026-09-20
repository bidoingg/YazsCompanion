// Training Yard advice on the Training Yard itself ("Train your survivors"): a small gold diamond with the purchase
// order on the top-right corner of every node worth buying with the points on hand (the game's own green "can buy"
// diamond sits on the top-left), a hollow gold diamond on the node to save for next, and a PLAN strip in the empty
// band under the tree: SPEND (what to buy now, in order), THEN (what to save for, what follows) and WHY (the reason
// for the node under the cursor when it is part of the advice, else for the first purchase).
//
// Motion (Fx.cs): on a tab change the rule draws itself from the left, the strip types on and the diamonds stamp in
// one after the other (big to small with a half turn, then a ping); the first purchase keeps breathing with a ping
// every few seconds, the node to save for glows slowly, a glint runs along the rule now and then. After a purchase
// only the diamonds that changed stamp again.
//
// Ticked from UIViewSkillTree.Update; the advice is recomputed only when the tab, the points or a node level changes
// (a cheap signature). Read-only: it never buys anything. Markers are children of the game's node objects and the
// strip is a child of the view, so they show, hide and die with them.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace YazsCompanion
{
    internal static class TreeUi
    {
        const string StripName = "YazsYardPlan", MarkName = "YazsYardMark";
        const float Font = 34f, LabelSize = 30f, LabelW = 200f, LineGap = 5f;
        // the strip runs from the first column to just before the Reset points button, which shares its band (the
        // button's left edge is ~1510 view units right of the first column on every aspect: the view is 3840 wide)
        const float StripSpan = 1480f;
        const float HeadH = 14f, HeadRule = 3f, HeadGap = 8f, HeadDiamond = 14f;     // a fading gold rule with a diamond on its left end; the row labels do the talking
        const float GapUnderNodes = 16f, BottomMargin = 14f, ShrinkFloor = 0.87f;      // text never below 13 px where the automatic size is 15 px
        const float MinTextPx = 15f;
        const int MaxNow = 4;

        sealed class Mark { public RectTransform Root, Fill, Turn, Ring; public CanvasGroup Group, RingGroup; public TextMeshProUGUI Text; public string Label; public float Size; }

        static readonly Dictionary<IntPtr, Mark> _marks = new Dictionary<IntPtr, Mark>();
        static RectTransform _strip, _rule, _tip, _glint; static Image _glintImage; static TextMeshProUGUI _text;
        static IntPtr _viewPtr, _containerPtr;
        static bool _fresh;                            // the tab changed (or the view opened): everything makes its entrance
        static string _typed = "";
        static float _next, _width = StripSpan, _scale = 1f, _room;
        static string _sig = "", _sep = "  ·  ", _dash = " — ";
        static List<TNode> _nodes; static TAdvice _advice; static List<TStep> _steps;
        static TNode _highlighted; static string _tree = "";
        static bool _logged;

        public static bool Enabled { get { try { return Plugin.ShowYard.Value; } catch { return true; } } }

        public static void Tick(UIViewSkillTree view)
        {
            if (view == null) return;
            try
            {
                float now = Time.realtimeSinceStartup;
                var container = view.currentContainer;
                if (container == null) return;
                bool tabChanged = container.Pointer != _containerPtr;
                if (now < _next && !tabChanged) return;
                _next = now + 0.25f;
                if (!Enabled) { HideAll(); return; }
                if (view.Pointer != _viewPtr) { _viewPtr = view.Pointer; _strip = null; _text = null; _rule = null; _tip = null; _glint = null; _marks.Clear(); _sig = ""; Fx.Cancel("yard"); }
                if (tabChanged) { _containerPtr = container.Pointer; _fresh = true; _sig = ""; }
                bool isTeam = false; try { var g = view.containerTabGeneral; isTeam = g != null && g.Pointer == container.Pointer; } catch { }
                string tree;
                long perf = Perf.Begin();
                var nodes = TreeState.Read(container, isTeam, out tree);
                int points = TreeState.Points(view, container);
                Perf.End("yard.read", perf);
                var sig = new StringBuilder().Append(container.Pointer).Append('|').Append(points);
                foreach (var n in nodes) sig.Append('|').Append(n.Level).Append(n.RankOpen ? "" : "L");
                string s = sig.ToString();
                if (s == _sig && Alive()) return;
                _sig = s;

                _nodes = nodes; _tree = tree; _highlighted = null;
                perf = Perf.Begin();
                _steps = isTeam ? TreePlan.TeamSteps(nodes) : TreePlan.ClassSteps(nodes);
                _advice = TreePlan.Advise(nodes, _steps, points);
                Perf.End("yard.advise", perf);
                Plugin.Logger.LogInfo("[yard] " + tree + ": " + points + " points; buy " + (_advice.Now.Count == 0 ? "nothing" : string.Join(", ", _advice.Now.Select(b => b.Order + " " + b.Label + " (" + b.Cost + ")")))
                    + (_advice.SaveFor != null ? "; save for " + _advice.SaveFor.Label + " (" + _advice.SaveFor.Cost + ")" : "") + (_advice.Later.Count > 0 ? "; later " + string.Join(", ", _advice.Later.Select(b => b.Label)) : ""));
                if (Plugin.Verbose.Value || !_logged) { _logged = true; Plugin.Logger.LogInfo("[yard] nodes of " + tree + ":" + TreeState.Describe(nodes)); }

                perf = Perf.Begin();
                DrawMarks();
                Perf.End("yard.marks", perf);
                perf = Perf.Begin();
                if (EnsureStrip(view)) { Place(view); Fit(); Entrance(); }
                Perf.End("yard.strip", perf);
                _fresh = false;
                Shots.Later(0.4f, "yard");
            }
            catch (Exception e) { Plugin.Logger.LogWarning("[yard] " + e); _next = Time.realtimeSinceStartup + 5f; }
        }

        /// <summary>The game moved its cursor to <paramref name="node"/>: the WHY row follows it.</summary>
        public static void Highlighted(UISkillTreeNode node)
        {
            try
            {
                if (node == null || _nodes == null || !Enabled) return;
                var n = _nodes.FirstOrDefault(x => { var ui = x.Ui as UISkillTreeNode; return ui != null && ui.Pointer == node.Pointer; });
                if (n == _highlighted) return;
                _highlighted = n;
                if (Alive()) Fit();
            }
            catch { }
        }

        static bool Alive() { try { return _strip != null && _text != null && _strip.gameObject != null; } catch { return false; } }

        static void HideAll()
        {
            try { if (Alive()) _strip.gameObject.SetActive(false); } catch { }
            foreach (var m in _marks.Values) { try { m.Root.gameObject.SetActive(false); m.Label = null; } catch { } }
            Fx.Cancel("yard");
            _sig = ""; _containerPtr = IntPtr.Zero;
        }

        // ---- the markers on the nodes ----
        static void DrawMarks()
        {
            var wanted = new Dictionary<IntPtr, string>();
            if (_advice != null)
            {
                foreach (var b in _advice.Now) { var ui = b.Node.Ui as UISkillTreeNode; if (ui != null) wanted[ui.Pointer] = b.Order.ToString(); }
                if (_advice.SaveFor != null && !_advice.Now.Any(b => b.Node == _advice.SaveFor.Node)) { var ui = _advice.SaveFor.Node.Ui as UISkillTreeNode; if (ui != null) wanted[ui.Pointer] = ""; }
            }
            foreach (var kv in _marks)
            {
                if (wanted.ContainsKey(kv.Key)) continue;
                try { kv.Value.Root.gameObject.SetActive(false); kv.Value.Label = null; } catch { }
                Fx.Cancel("yardmark:" + kv.Key);
            }
            if (_advice == null) return;
            int i = 0;
            foreach (var b in _advice.Now) Show(b.Node, b.Order.ToString(), true, i++);
            if (_advice.SaveFor != null && !_advice.Now.Any(b => b.Node == _advice.SaveFor.Node)) Show(_advice.SaveFor.Node, "", false, i);
        }

        static void Show(TNode n, string label, bool solid, int index)
        {
            var ui = n.Ui as UISkillTreeNode; if (ui == null) return;
            try
            {
                Mark m;
                if (!_marks.TryGetValue(ui.Pointer, out m) || !MarkAlive(m)) { m = Build(ui); if (m == null) return; _marks[ui.Pointer] = m; }
                bool same = !_fresh && m.Label == label && m.Root.gameObject.activeSelf;
                m.Fill.gameObject.SetActive(!solid);
                if (m.Text != null) m.Text.text = label;
                m.Label = label;
                m.Root.gameObject.SetActive(true);
                m.Root.SetAsLastSibling();
                if (!same) Stamp(m, "yardmark:" + ui.Pointer, index, solid && label == "1", !solid);
            }
            catch (Exception e) { Plugin.Logger.LogInfo("[yard] marker on " + n.Key + ": " + e.Message); }
        }

        // big to small with a half turn and an overshoot, a ping as it lands; then the first purchase breathes and pings
        // every few seconds, the node to save for glows slowly, the others rest
        static void Stamp(Mark m, string key, int index, bool first, bool hollow)
        {
            Fx.Cancel(key);
            var root = m.Root; var turn = m.Turn; var group = m.Group; var ring = m.Ring; var ringGroup = m.RingGroup;
            if (!Fx.On) { root.localScale = Vector3.one; if (group != null) group.alpha = 1f; if (turn != null) turn.localRotation = Quaternion.identity; if (ringGroup != null) ringGroup.alpha = 0f; return; }
            Fx.Run(key + ":in", 0.10f + 0.085f * index, 0.36f, k =>
            {
                float s = Mathf.LerpUnclamped(2.3f, 1f, Fx.OutBack(k));
                root.localScale = new Vector3(s, s, 1f);
                if (group != null) group.alpha = Mathf.Clamp01(k * 3.5f);
                if (turn != null) turn.localRotation = Quaternion.Euler(0, 0, 180f * (1f - Fx.OutCubic(k)));
            }, () =>
            {
                if (ring != null) Fx.Run(key + ":ping", 0f, 0.55f, k => Fx.PingPose(ring, ringGroup, k, 2.5f));
                if (first) Fx.Loop(key + ":idle", 2.8f, k =>
                {
                    float s = 1f + 0.055f * Mathf.Sin(k * Mathf.PI * 2f);
                    root.localScale = new Vector3(s, s, 1f);
                    if (ring != null && k > 0.45f) Fx.PingPose(ring, ringGroup, (k - 0.45f) / 0.24f > 1f ? 1f : (k - 0.45f) / 0.24f, 2.3f);
                });
                else if (hollow && group != null) Fx.Loop(key + ":idle", 2.4f, k => { group.alpha = 0.74f + 0.26f * Mathf.Cos(k * Mathf.PI * 2f); });
            });
        }

        static bool MarkAlive(Mark m) { try { return m != null && m.Root != null && m.Root.gameObject != null; } catch { return false; } }

        static Mark Build(UISkillTreeNode ui)
        {
            var nodeRt = ui.transform.TryCast<RectTransform>(); if (nodeRt == null) return null;
            float w = 0; try { w = nodeRt.rect.width; } catch { }
            if (w <= 1f) w = 144f;
            float size = Mathf.Clamp(w * 0.40f, 40f, 84f);
            var root = Ui.NewRect(MarkName, nodeRt);
            root.anchorMin = root.anchorMax = new Vector2(1f, 1f); root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(-size * 0.18f, -size * 0.18f); root.sizeDelta = new Vector2(size, size);
            CanvasGroup group = null, ringGroup = null; RectTransform ring = null;
            try { group = root.gameObject.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; } catch { }
            try { ring = Fx.Ring(root, size, 4f, Theme.Gold, true, out ringGroup); } catch { }
            var turn = Ui.NewRect("Turn", root); Ui.Stretch(turn, 0, 0, 0, 0);                              // the diamonds turn, the number stays upright
            Ui.Diamond(turn, "Edge", 0.5f, 0.5f, size + 8f, new Color(0.04f, 0.035f, 0.03f, 0.95f));     // a dark rim keeps it readable on bright icons
            Ui.Diamond(turn, "Gold", 0.5f, 0.5f, size, Theme.Gold);
            var fill = Ui.Diamond(turn, "Hollow", 0.5f, 0.5f, size - 12f, new Color(0.05f, 0.045f, 0.04f, 1f));
            TextMeshProUGUI text = null;
            TextMeshProUGUI template = null; try { template = ui.levelText; } catch { }
            if (template == null) template = _text;
            if (template != null)
            {
                text = Ui.CloneText(template, root, "N");
                if (text != null)
                {
                    Ui.Stretch(text.rectTransform, -10, -10, -10, -10);
                    text.alignment = TextAlignmentOptions.Center; text.fontSize = size * 0.52f; text.fontStyle = FontStyles.Bold;
                    text.color = new Color(0.07f, 0.055f, 0.03f, 1f);
                }
            }
            return new Mark { Root = root, Fill = fill, Text = text, Turn = turn, Ring = ring, Group = group, RingGroup = ringGroup, Size = size };
        }

        // ---- the strip under the tree ----
        static bool EnsureStrip(UIViewSkillTree view)
        {
            if (Alive()) { _strip.gameObject.SetActive(true); return true; }
            var viewRt = view.transform.TryCast<RectTransform>(); if (viewRt == null) return false;
            TextMeshProUGUI template = null; try { template = view.currentUpgradeDescriptionText; } catch { }
            if (template == null) template = Ui.FindLabel(viewRt, null);
            if (template == null) return false;

            var root = Ui.NewRect(StripName, viewRt);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f); root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(StripSpan, 200f);

            var head = Ui.NewRect("Header", root);
            head.anchorMin = new Vector2(0, 1); head.anchorMax = new Vector2(1, 1); head.pivot = new Vector2(0.5f, 1f);
            head.anchoredPosition = Vector2.zero; head.sizeDelta = new Vector2(0, HeadH);
            var rule = Ui.FadeRule(head, "Rule", Theme.GoldLine); Ui.LeftPivot(rule, 0.5f, HeadRule);
            var glint = Ui.Image(head, "Glint", new Color(1f, 0.95f, 0.75f, 0f));
            glint.anchorMin = glint.anchorMax = new Vector2(0, 0.5f); glint.pivot = new Vector2(0.5f, 0.5f); glint.sizeDelta = new Vector2(260f, HeadRule * 3f);
            try { var gi = glint.GetComponent<Image>(); var sp = Ui.GlowSprite(); if (sp != null) gi.sprite = sp; _glintImage = gi; } catch { }
            var tip = Ui.Diamond(head, "Tip", 0, 0.5f, HeadDiamond, Theme.Gold); tip.anchoredPosition = new Vector2(HeadDiamond / 2, 0f);
            _rule = rule; _tip = tip; _glint = glint;

            var text = Ui.CloneText(template, root, "Text");
            if (text == null) { UnityEngine.Object.Destroy(root.gameObject); return false; }
            Ui.Stretch(text.rectTransform, 0, 0, 0, HeadH + HeadGap);
            text.fontSize = Font; text.fontStyle = FontStyles.Normal; text.alignment = TextAlignmentOptions.TopLeft; text.color = Theme.White;
            try { text.enableWordWrapping = true; } catch { }
            try { text.lineSpacing = LineGap; } catch { }
            try { text.margin = Vector4.zero; text.paragraphSpacing = 0f; } catch { }     // the description label it was cloned from pads its top
            try
            {
                var font = text.font;
                if (font != null)
                {
                    if (!font.HasCharacter('·', true, true)) _sep = "  |  ";
                    if (!font.HasCharacter('—', true, true)) _dash = " - ";
                }
            }
            catch { _sep = "  |  "; _dash = " - "; }
            _strip = root; _text = text;
            return true;
        }

        // under the lowest node of the tab, left-aligned with the first column; enlarged on small screens
        static void Place(UIViewSkillTree view)
        {
            var viewRt = view.transform.TryCast<RectTransform>();
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, nodeH = 0f;
            var corners = new Il2CppStructArray<Vector3>(4);
            foreach (var n in _nodes)
            {
                var ui = n.Ui as UISkillTreeNode; if (ui == null) continue;
                try
                {
                    if (!ui.gameObject.activeInHierarchy) continue;
                    var rt = ui.transform.TryCast<RectTransform>(); if (rt == null) continue;
                    rt.GetWorldCorners(corners);
                    var bl = viewRt.InverseTransformPoint(corners[0]); var tr = viewRt.InverseTransformPoint(corners[2]);
                    minX = Mathf.Min(minX, bl.x); minY = Mathf.Min(minY, bl.y); maxX = Mathf.Max(maxX, tr.x);
                    nodeH = Mathf.Max(nodeH, tr.y - bl.y);
                }
                catch { }
            }
            if (minX == float.MaxValue) return;
            float scale = 1f;
            try
            {
                var canvas = view.GetComponentInParent<Canvas>();
                var canvasRt = canvas == null ? null : canvas.rootCanvas.transform.TryCast<RectTransform>();
                float h = canvasRt == null ? 0 : canvasRt.rect.height;
                if (h > 0) scale = Mathf.Clamp(MinTextPx / (Font * UnityEngine.Screen.height / h), 1f, 1.6f);
            }
            catch { }
            _auto = scale; _room = (minY - GapUnderNodes) - (viewRt.rect.yMin + BottomMargin);      // the band between the nodes and the bottom edge of the view
            SetScale(scale);
            _strip.localPosition = new Vector3(minX, minY - GapUnderNodes, 0f);
            Plugin.Logger.LogInfo("[yard] strip at (" + minX.ToString("0") + ", " + (minY - GapUnderNodes).ToString("0") + ") x" + scale.ToString("0.00") + "; nodes span x " + minX.ToString("0") + ".." + maxX.ToString("0")
                + ", lowest y " + minY.ToString("0") + ", node height " + nodeH.ToString("0") + "; view rect " + viewRt.rect.width.ToString("0") + "x" + viewRt.rect.height.ToString("0"));
        }

        // the strip's entrance on a tab change: the rule draws, its diamond spins in, the rows type on; after a purchase
        // (same tab) only the rows type again, and only when SPEND / THEN changed (the WHY row follows the cursor silently)
        static void Entrance()
        {
            try
            {
                string text = _text.text ?? "";
                int why = text.LastIndexOf("WHY", StringComparison.Ordinal);
                string head = why > 0 ? text.Substring(0, why) : text;
                if (_fresh)
                {
                    Fx.Draw("yardrule", _rule, 0f, 0.45f);
                    Fx.Spin("yardtip", _tip, 0f, 0.5f, 0.5f);
                    Fx.Cancel("yardglint");
                    var glint = _glint; var image = _glintImage;
                    if (glint != null && image != null) Fx.Loop("yardglint", 6f, k =>
                    {
                        float kk = (k - 0.5f) / 0.13f;                       // one pass every six seconds
                        if (kk < 0f || kk > 1f) { if (image.color.a > 0f) image.color = new Color(1f, 0.95f, 0.75f, 0f); return; }
                        glint.anchoredPosition = new Vector2(_width * 0.7f * kk, 0f);
                        image.color = new Color(1f, 0.95f, 0.75f, 0.85f * Mathf.Sin(kk * Mathf.PI));
                    });
                }
                if (_fresh || head != _typed) Fx.Type("yardtype", _text, _fresh ? 0.12f : 0f, 240f);
                _typed = head;
            }
            catch { }
        }

        static float _auto = 1f;
        static void SetScale(float scale)
        {
            _scale = scale;
            _strip.localScale = new Vector3(scale, scale, 1f);
            _width = StripSpan / scale; _strip.sizeDelta = new Vector2(_width, _strip.sizeDelta.y);
        }

        // the band under the tree is short on a 16:10 screen (the view is letterboxed to 16:9, the text is enlarged):
        // when the rows do not fit, first shrink a little (a smaller scale is also a wider strip, fewer wraps), then say
        // less - drop the "after that" tail of THEN, then the WHY row - rather than let the text get small
        static void Fit()
        {
            try
            {
                for (int terse = 0; terse <= 2; terse++)
                {
                    SetScale(_auto); Render(terse);
                    if (_room <= 0 || Fits()) return;
                    float floor = Mathf.Max(1f, _auto * ShrinkFloor);
                    for (int pass = 0; pass < 2 && _scale > floor; pass++)
                    {
                        SetScale(Mathf.Max(floor, _scale * _room / (_strip.sizeDelta.y * _scale) * 0.98f));
                        Render(terse);
                        if (Fits()) return;
                    }
                }
            }
            catch { }
        }
        static bool Fits() { return _strip.sizeDelta.y * _scale <= _room; }

        static string Gold(string s) { return "<color=" + Theme.GoldHex + ">" + s + "</color>"; }
        static string Dim(string s) { return "<color=" + Theme.DimHex + ">" + s + "</color>"; }
        static string Row(string label, string value)
        {
            return "<size=" + LabelSize + "><b>" + Gold(label) + "</b></size><indent=" + LabelW + ">" + value + "</indent>";
        }
        static string Item(TBuy b) { return "<nobr>" + (b.Order > 0 ? Gold("<b>" + b.Order + "</b>") + " " : "") + b.Node.Name + " " + Dim(b.From + ">" + b.To) + "</nobr>"; }

        static void Render(int terse)
        {
            if (_advice == null || _text == null) return;
            var rows = new List<string>();
            if (_advice.Now.Count > 0)
            {
                var items = _advice.Now.Take(MaxNow).Select(Item).ToList();
                if (_advice.Now.Count > MaxNow) items.Add(Dim("+" + (_advice.Now.Count - MaxNow) + " more"));
                rows.Add(Row("SPEND " + (_advice.Points - _advice.Left), string.Join(Dim(_sep), items)));
            }
            else rows.Add(Row("SPEND", Dim(_advice.Complete ? "every open node of this tree is maxed" : _advice.Points > 0 ? "nothing in the plan fits " + _advice.Points + (_advice.Points == 1 ? " point" : " points") + " yet" : "no points to spend")));

            var then = new List<string>();
            if (_advice.SaveFor != null)
            {
                int need = Math.Max(1, _advice.SaveFor.Cost - _advice.Left);
                then.Add("<nobr>save " + need + " more for " + _advice.SaveFor.Node.Name + " " + Dim(_advice.SaveFor.From + ">" + _advice.SaveFor.To) + "</nobr>");
            }
            var later = _advice.Later.Where(b => _advice.SaveFor == null || b.Node != _advice.SaveFor.Node).Take(2).Select(b => "<nobr>" + b.Node.Name + "</nobr>").ToList();
            if (later.Count > 0 && (terse < 1 || then.Count == 0)) then.Add(Dim((then.Count > 0 ? "after that " : "next ") + string.Join(", ", later)));
            if (then.Count > 0) rows.Add(Row("THEN", string.Join(Dim(_sep), then)));

            if (terse >= 2 && _highlighted == null) { Finish(rows); return; }
            TBuy about = _highlighted != null ? _advice.Find(_highlighted) : null;
            if (about == null && _highlighted == null) about = _advice.Now.FirstOrDefault() ?? _advice.SaveFor;
            if (about != null && about.Why.Length > 0) rows.Add(Row("WHY", Gold(about.Node.Name) + Dim(_dash) + about.Why));
            else if (_highlighted != null)
            {
                bool maxed = _highlighted.Level >= _highlighted.Max;
                int pos = _steps == null ? -1 : _steps.FindIndex(s => s.Node == _highlighted && s.To > _highlighted.Level);
                string note = maxed ? "maxed" : !_highlighted.RankOpen ? "its rank is still locked" : pos >= 0 ? "later in the plan (step " + (pos + 1) + " of " + _steps.Count + ")" : "not in the plan";
                rows.Add(Row("WHY", _highlighted.Name + Dim(_dash + note)));
            }
            Finish(rows);
        }

        static void Finish(List<string> rows)
        {
            _text.text = string.Join("\n", rows);
            try { _text.ForceMeshUpdate(); float h = _text.preferredHeight; if (h > 0) _strip.sizeDelta = new Vector2(_width, HeadH + HeadGap + h); } catch { }
        }
    }
}
