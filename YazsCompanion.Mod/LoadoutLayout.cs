// Where the badge advice goes on the run setup screen (0.13.0), from measured rects: the WHY line (the reason for the
// badge under the cursor) and the summary rows (EQUIP / SWAP OUT / YARD under CHOSEN BADGES), and how big the text and the
// diamonds are. LoadoutUi.cs measures the screen once per visit (GetWorldCorners into the view's space, so the canvas mode
// does not matter) and hands the rects in; the bench replays mocks of the PC, the Deck and crowded / empty screens.
//
// Coordinates: view units with the origin at the view's TOP-LEFT and y growing DOWN (the measured table of SPEC section 0).
// The canvas rect is in the same space (a 21:9 screen has pillars: x < 0 and x > view width; a 16:10 screen letterboxes:
// y < 0 and y > view height).
//
// The rules (SPEC 2.1 / 2.4): text px = max(15, 1.875 % of the screen height) x LoadoutSize, never under 15 px; a line is
// 1.55 x the font (the TMP vanishing-line pitfall); a band must be 600 units wide; the WHY line takes the first band of
// grid-gap -> under-info -> over-name that holds 2 lines (or 1 at the smallest shrink), else it goes inline into the game's
// bonus label; the summary takes under-slots -> grid-gap (if free) -> the pillar, shrinking to max(0.87x, 15 px) and then
// dropping YARD, then SWAP. Nothing sits over an obstacle, a button, the info labels or the START bar, nor at the bottom
// centre of a grid button (the game's green "selected" diamond). Every band is first cut free of what overlaps it (Trim):
// in the game (1.0.2, walk of 2026-10-04) the grid buttons' rects are the whole 200-unit cells, touching, so the selected
// zones of the row above reach 44 units into the grid gap - rejecting an overlapped band outright left the WHY line nowhere.
//
// Pure C# (no game types), linked into the offline bench.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YazsCompanion
{
    /// <summary>A rect in view units, top-left origin, y down.</summary>
    internal struct R4
    {
        public float X0, Y0, X1, Y1;
        public R4(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
        public static R4 Box(float x, float y, float w, float h) { return new R4(x, y, x + w, y + h); }
        public float W { get { return X1 - X0; } }
        public float H { get { return Y1 - Y0; } }
        public bool Empty { get { return W <= 0 || H <= 0; } }
        public bool Overlaps(R4 o) { return !Empty && !o.Empty && X0 < o.X1 && o.X0 < X1 && Y0 < o.Y1 && o.Y0 < Y1; }
        public bool Contains(R4 o) { return !o.Empty && o.X0 >= X0 && o.X1 <= X1 && o.Y0 >= Y0 && o.Y1 <= Y1; }
        public bool Contains(float x, float y) { return x >= X0 && x <= X1 && y >= Y0 && y <= Y1; }
        public R4 Inset(float d) { return new R4(X0 + d, Y0 + d, X1 - d, Y1 - d); }
        public R4 Clip(R4 o) { return new R4(Math.Max(X0, o.X0), Math.Max(Y0, o.Y0), Math.Min(X1, o.X1), Math.Min(Y1, o.Y1)); }
        public static R4 Union(IEnumerable<R4> rs)
        {
            var l = rs.Where(r => !r.Empty).ToList();
            return l.Count == 0 ? new R4() : new R4(l.Min(r => r.X0), l.Min(r => r.Y0), l.Max(r => r.X1), l.Max(r => r.Y1));
        }
        static string N(float v) { return Math.Round(v).ToString(CultureInfo.InvariantCulture); }
        public string Size { get { return N(W) + "x" + N(H); } }
        public override string ToString() { return "(" + N(X0) + "," + N(Y0) + ")-(" + N(X1) + "," + N(Y1) + ")"; }
    }

    internal sealed class LayoutIn
    {
        public R4 View, Canvas;
        public R4[] Grid = new R4[0];               // the grid buttons in the grid's order (badgeSortOrder, rows left to right)
        public R4[] Slots = new R4[0];
        public R4[] Difficulty = new R4[0];
        public R4[] Obstacles = new R4[0];          // active graphics under the view: lines, labels, boxes (backgrounds and frames excluded)
        public R4[] Containers = new R4[0];         // the excluded frames (a band that starts inside one stays inside it, inset 24)
        public R4 Name, Desc, Bonus, StartBar;      // the info labels' rendered text bounds; Empty = unknown
        public float UnitPx = 1, ScreenH = 1080, Size = 1;
        public LoadoutDetail Detail = LoadoutDetail.NumbersAndReason;
        public int SumRows = 2;                     // rows the summary has (3 = with the YARD / UNLOCK row)
    }

    internal sealed class LayoutOut
    {
        public string WhyAt = "none", SumAt = "none";
        public R4 Why, Sum;
        public int WhyLines, SumRows;
        public float FontUnits, WhyFontUnits, SumFontUnits, LineUnits, MarkerUnits, NumberUnits, FontPx;
        public readonly List<string> Dropped = new List<string>();     // summary rows left out: "yard", "swap"
        public readonly List<string> Notes = new List<string>();
        public string Bands = "";                                      // every band measured (the `[loadout] layout` line)
        public string Note { get { return string.Join("; ", Notes); } }
    }

    internal static class LoadoutLayout
    {
        public const float LineFactor = 1.55f, MinShrink = 0.87f, MinPx = 15f, Pad = 10f, FrameInset = 24f, MinBandWidth = 600f, PillarMin = 560f, PillarWidth = 600f;

        /// <summary>The text size in px: the readout rule, 1.875 % of the screen height, never under 15 px, times LoadoutSize.</summary>
        public static float FontPx(float screenH, float size) { return Math.Max(MinPx, 0.01875f * screenH) * Math.Max(0.1f, size); }

        /// <summary>The diamond size in units: clamp(max(0.40 w, 13 px / (0.52 unitPx)), 40, 0.60 w) x LoadoutSize, capped at 0.60 w.</summary>
        public static float MarkerUnits(float buttonW, float unitPx, float size)
        {
            float cap = 0.60f * buttonW;
            float m = Math.Max(0.40f * buttonW, 13f / (0.52f * Math.Max(0.0001f, unitPx)));
            m = Math.Min(Math.Max(m, 40f), cap) * Math.Max(0.1f, size);
            return Math.Min(m, cap);
        }

        /// <summary>The geometric half of the obstacle rule (SPEC 2.4): of the graphics measured under the view (the caller has already
        /// left out our own Yazs* pieces, near-transparent ones and anything under a grid or slot button), a rect that contains
        /// every grid button, or a slot button, or that covers 40 % of the view or more is a CONTAINER (a background, the BADGES
        /// COLLECTION frame) - not an obstacle, but a band starting inside one stays inside it; everything else (lines, labels,
        /// the difficulty stats box) is an obstacle.</summary>
        public static void Split(IEnumerable<R4> graphics, R4 view, R4[] grid, R4[] slots, out List<R4> obstacles, out List<R4> containers)
        {
            obstacles = new List<R4>(); containers = new List<R4>();
            var gridAll = R4.Union(grid ?? new R4[0]);
            float viewArea = Math.Max(1f, view.W * view.H);
            foreach (var r in graphics ?? Enumerable.Empty<R4>())
            {
                if (r.Empty) continue;
                bool holdsGrid = !gridAll.Empty && r.Contains(gridAll);
                bool holdsSlot = (slots ?? new R4[0]).Any(s => r.Contains(s));
                bool big = r.W * r.H >= 0.40f * viewArea;
                if (holdsGrid || holdsSlot || big) containers.Add(r); else obstacles.Add(r);
            }
        }

        /// <summary>The zone a grid button's own "selected" diamond takes (bottom centre): nothing of ours may cover it.</summary>
        public static R4 SelectedZone(R4 button)
        {
            float cx = (button.X0 + button.X1) / 2, w = button.W;
            return new R4(cx - 0.15f * w, button.Y1 - 0.12f * w, cx + 0.15f * w, button.Y1 + 0.22f * w);
        }

        /// <summary>Everything a band must stay off: obstacles, buttons, the info labels, the difficulty buttons, the START bar and the
        /// grid buttons' selected diamonds.</summary>
        public static List<R4> Blocked(LayoutIn i)
        {
            var l = new List<R4>();
            l.AddRange(i.Obstacles); l.AddRange(i.Grid); l.AddRange(i.Slots); l.AddRange(i.Difficulty);
            l.Add(i.Name); l.Add(i.Desc); l.Add(i.Bonus); l.Add(i.StartBar);
            l.AddRange(i.Grid.Select(SelectedZone));
            return l.Where(r => !r.Empty).ToList();
        }

        // the frame a band must stay inside: the SMALLEST container that holds the point (the graphics come in hierarchy order, so
        // "the first" would often be a full-screen background and let a band run past the gold frame); skip = containers that are
        // no frame for this band (the slot row's own backing for the band under the slots)
        static R4 FrameOf(LayoutIn i, float x, float y, Func<R4, bool> skip = null)
        {
            R4 best = new R4(); float area = float.MaxValue;
            foreach (var c in i.Containers)
            {
                if (c.Empty || !c.Contains(x, y) || (skip != null && skip(c))) continue;
                float a = c.W * c.H;
                if (a < area) { area = a; best = c; }
            }
            return best;
        }

        static R4 FrameClip(LayoutIn i, R4 band, Func<R4, bool> skip = null)
        {
            var c = FrameOf(i, band.X0 + 1, band.Y0 + 1, skip);
            return c.Empty ? band : band.Clip(c.Inset(FrameInset));
        }

        static bool Free(R4 band, List<R4> blocked) { return !band.Empty && !blocked.Any(b => b.Overlaps(band)); }

        /// <summary>A band cut free of what overlaps it, one blocker at a time: of the four pieces left around the blocker (below,
        /// above, right, left of it) the one that holds the most text wins - lines (at most <paramref name="maxLines"/>, at
        /// <paramref name="lineU"/> units a line) times width, a piece under the minimum band width holding none; the larger area
        /// breaks a tie. So a row of small blockers along one edge (the selected diamonds of the grid row above the grid gap -
        /// the game's grid buttons are the whole 200-unit cells, touching, so their zones reach into the gap) costs the band a
        /// strip of its height, not its width, and a neighbour that only touches an edge costs nothing. A band already free comes
        /// back unchanged; one that cannot be freed comes back empty. <paramref name="cut"/> names the first blocker that cut it.</summary>
        public static R4 Trim(R4 band, List<R4> blocked, float lineU, int maxLines, out string cut)
        {
            cut = "";
            if (band.Empty || blocked == null) return band;
            float lu = Math.Max(1f, lineU); int ml = Math.Max(1, maxLines);
            Func<R4, double> holds = r => r.Empty || r.W < MinBandWidth ? 0 : Math.Min(ml, Math.Floor(r.H / lu + 1e-4)) * (double)r.W;
            for (int n = 0; n < 32; n++)
            {
                int at = blocked.FindIndex(b => b.Overlaps(band));
                if (at < 0) return band;
                var hit = blocked[at];
                if (cut.Length == 0) cut = hit.ToString();
                var pieces = new[] { new R4(band.X0, hit.Y1, band.X1, band.Y1), new R4(band.X0, band.Y0, band.X1, hit.Y0), new R4(hit.X1, band.Y0, band.X1, band.Y1), new R4(band.X0, band.Y0, hit.X0, band.Y1) };
                R4 keep = new R4(); double best = -1, bestArea = -1;
                foreach (var p in pieces)
                {
                    if (p.Empty) continue;
                    double h = holds(p), area = (double)p.W * p.H;
                    if (h > best || (h == best && area > bestArea)) { best = h; bestArea = area; keep = p; }
                }
                band = keep;
                if (band.Empty) return band;
            }
            return Free(band, blocked) ? band : new R4();
        }

        /// <summary>Trim is greedy: a piece kept because it holds more lines can lose its width to the next blocker (a bigger text,
        /// LoadoutSize 1.3: the 200-unit grid gap holds 2 lines, the 156 left under the row above's selected diamonds only 1 - the
        /// greedy cut kept the 2-line right-hand piece, the next diamond took 330 units of it, and 670 x 156 came back where
        /// 1000 x 156 holds the same 1 line). So the band is cut for every line count from <paramref name="maxLines"/> down to 1, at
        /// the full size and at the smallest shrink, and the best result wins: more lines at the full size, then more at the
        /// shrink, then the wider; a tie keeps the first (the plain cut for maxLines at the full size).</summary>
        public static R4 TrimBest(R4 band, List<R4> blocked, float fontU, float minU, int maxLines, out string cut)
        {
            int ml = Math.Max(1, maxLines);
            var best = Trim(band, blocked, LineFactor * fontU, ml, out cut);
            if (cut.Length == 0) return best;                    // already free (or empty): nothing to choose
            foreach (float lu in new[] { fontU, minU })
                for (int m = ml; m >= 1; m--)
                {
                    if (lu == fontU && m == ml) continue;
                    string c; var t = Trim(band, blocked, LineFactor * lu, m, out c);
                    if (Better(t, best, fontU, minU, ml)) { best = t; cut = c; }
                }
            return best;
        }

        // a beats b: usable (600 units wide) first, then lines at the full size, then lines at the smallest shrink, then width
        static bool Better(R4 a, R4 b, float fontU, float minU, int ml)
        {
            Func<R4, bool> ok = r => !r.Empty && r.W >= MinBandWidth;
            if (!ok(a)) return false;
            if (!ok(b)) return true;
            int fa = Math.Min(ml, Lines(a, fontU)), fb = Math.Min(ml, Lines(b, fontU));
            if (fa != fb) return fa > fb;
            int sa = Math.Min(ml, Lines(a, minU)), sb = Math.Min(ml, Lines(b, minU));
            if (sa != sb) return sa > sb;
            return a.W > b.W + 0.5f;
        }

        // the empty cells after the last grid button in its row (3 or more), the row's height
        static R4 GridGap(LayoutIn i)
        {
            if (i.Grid.Length == 0) return new R4();
            var xs = Cluster(i.Grid.Select(r => r.X0)); var ys = Cluster(i.Grid.Select(r => r.Y0));
            var last = i.Grid[i.Grid.Length - 1];
            int inRow = i.Grid.Count(r => Math.Abs(r.Y0 - last.Y0) < 5);
            int empty = xs.Count - inRow;
            if (empty < 3 || inRow >= xs.Count) return new R4();
            float bw = last.W;
            return new R4(xs[inRow], last.Y0, xs[xs.Count - 1] + bw, last.Y1);
        }
        static List<float> Cluster(IEnumerable<float> vs)
        {
            var o = new List<float>();
            foreach (var v in vs.OrderBy(x => x)) if (o.Count == 0 || v - o[o.Count - 1] > 5) o.Add(v);
            return o;
        }

        // the info panel's width: the smallest frame around the labels (inset), else the grid's
        static void PanelX(LayoutIn i, R4 at, out float x0, out float x1)
        {
            var c = FrameOf(i, at.X0 + 1, at.Y0 + 1, f => !f.Contains(at));
            if (!c.Empty) { var r = c.Inset(FrameInset); x0 = r.X0; x1 = r.X1; return; }
            var g = R4.Union(i.Grid);
            x0 = g.Empty ? at.X0 : g.X0; x1 = g.Empty ? at.X1 : g.X1;
        }

        static R4 UnderInfo(LayoutIn i, List<R4> blocked)
        {
            var anchor = !i.Bonus.Empty ? i.Bonus : !i.Desc.Empty ? i.Desc : i.Name;
            if (anchor.Empty) return new R4();
            float x0, x1; PanelX(i, anchor, out x0, out x1);
            float top = anchor.Y1 + Pad, bottom = i.View.Y1;
            foreach (var b in blocked) if (b.Y0 >= anchor.Y1 && b.X0 < x1 && b.X1 > x0) bottom = Math.Min(bottom, b.Y0);
            return FrameClip(i, new R4(x0, top, x1, bottom - Pad), f => !f.Contains(anchor));
        }

        static R4 OverName(LayoutIn i, List<R4> blocked)
        {
            if (i.Name.Empty) return new R4();
            float x0, x1; PanelX(i, i.Name, out x0, out x1);
            float bottom = i.Name.Y0 - Pad, top = i.View.Y0;
            foreach (var b in blocked) if (b.Y1 <= i.Name.Y0 && b.X0 < x1 && b.X1 > x0) top = Math.Max(top, b.Y1);
            return FrameClip(i, new R4(x0, top + Pad, x1, bottom), f => !f.Contains(i.Name));
        }

        static R4 UnderSlots(LayoutIn i, List<R4> blocked)
        {
            var row = R4.Union(i.Slots);
            if (row.Empty) return new R4();
            float x0 = row.X0, x1 = row.X1, top = row.Y1 + Pad;
            // the right column under the slots (the difficulty header, its buttons, the stats box) sets the right edge
            foreach (var b in blocked)
                if (b.Y0 >= row.Y1 && b.X0 >= row.X0 - 0.1f * row.W && b.X1 <= i.View.X1) x1 = Math.Max(x1, b.X1);
            float bottom = i.View.Y1;
            foreach (var b in blocked) if (b.Y0 >= row.Y1 && b.X0 < x1 && b.X1 > x0) bottom = Math.Min(bottom, b.Y0);
            // a backing that holds a slot button is the slot row's own plate, not a frame for the band under it (clipping to it
            // left nothing: the summary fell to the pillar on the PC and to nothing on the Deck)
            return FrameClip(i, new R4(x0, top, x1, bottom - Pad), f => i.Slots.Any(s => f.Contains(s)));
        }

        static R4 Pillar(LayoutIn i, int rows, float lineU)
        {
            float side = i.Canvas.X1 - i.View.X1;
            if (side < PillarMin) return new R4();
            var row = R4.Union(i.Slots);
            float top = row.Empty ? i.View.Y0 + 300 : row.Y0;
            float x0 = i.View.X1 + 30;
            return new R4(x0, top, Math.Min(x0 + PillarWidth, i.Canvas.X1 - 20), top + rows * lineU + Pad);
        }

        // lines of text a band holds at a font (units)
        static int Lines(R4 band, float fontU) { return band.Empty ? 0 : (int)Math.Floor(band.H / (LineFactor * fontU) + 1e-4); }

        /// <summary>The placement of the WHY line and the summary on one screen.</summary>
        public static LayoutOut Choose(LayoutIn i)
        {
            var o = new LayoutOut();
            float unit = Math.Max(0.0001f, i.UnitPx);
            o.FontPx = FontPx(i.ScreenH, i.Size);
            float fontU = o.FontPx / unit;
            float minU = Math.Max(MinShrink * o.FontPx, MinPx) / unit;
            if (minU > fontU) minU = fontU;
            o.FontUnits = o.WhyFontUnits = o.SumFontUnits = fontU;
            o.LineUnits = LineFactor * fontU;
            float bw = i.Grid.Length > 0 ? i.Grid[0].W : 143;
            o.MarkerUnits = MarkerUnits(bw, unit, i.Size);
            // 0.16.0 (C16-16, DK-C05): the digits at the 15 px floor where the diamond allows it (0.52 x the diamond gave 14 px on the Deck's
            // 200-unit cells), never over 0.60 x the diamond; the PC keeps 0.52 (57 u diamond, 19.8 px digits). The diamond keeps its size
            o.NumberUnits = Math.Max(0.52f * o.MarkerUnits, Math.Min(MinPx / unit, 0.60f * o.MarkerUnits));
            var blocked = Blocked(i);

            // the grid gap is made of the grid's own cells, inside every frame around the grid: no clip (the smallest container
            // there may be the grid's own backing, whose inset would cut the cells' height)
            var gap = GridGap(i); var under = UnderInfo(i, blocked); var over = OverName(i, blocked); var slots = UnderSlots(i, blocked);
            float pillarSide = Math.Min(i.View.X0 - i.Canvas.X0, i.Canvas.X1 - i.View.X1);
            o.Bands = "grid-gap " + (gap.Empty ? "-" : gap.Size) + ", under-info " + (under.Empty ? "-" : ((int)Math.Round(under.H)).ToString(CultureInfo.InvariantCulture))
                + ", over-name " + (over.Empty ? "-" : ((int)Math.Round(over.H)).ToString(CultureInfo.InvariantCulture)) + ", under-slots " + (slots.Empty ? "-" : slots.Size)
                + ", pillar " + (pillarSide > 0 ? Math.Round(i.View.X0 - i.Canvas.X0).ToString(CultureInfo.InvariantCulture) + "/" + Math.Round(i.Canvas.X1 - i.View.X1).ToString(CultureInfo.InvariantCulture) : "-");

            if (i.Detail < LoadoutDetail.NumbersAndReason) { o.WhyAt = "none"; o.SumAt = "none"; return o; }

            // the WHY line: 2 lines at the full size, or 1 at the smallest shrink; each band is first cut free of what overlaps it
            // (Trim) - a band is rejected only when what is left is too narrow or too low (every try is named in the notes then)
            var tries = new List<string>();
            foreach (var cand in new[] { new KeyValuePair<string, R4>("grid-gap", gap), new KeyValuePair<string, R4>("under-info", under), new KeyValuePair<string, R4>("over-name", over) })
            {
                var raw = cand.Value;
                if (raw.Empty) { tries.Add(cand.Key + " - (" + (cand.Key == "grid-gap" ? "fewer than 3 empty cells" : cand.Key == "under-info" ? "no info label measured" : "no name label measured") + ")"); continue; }
                string cut; var b = TrimBest(raw, blocked, fontU, minU, 2, out cut);
                string was = raw.Size + (cut.Length > 0 ? " cut to " + (b.Empty ? "nothing" : b.Size) + " by " + cut : "");
                if (b.Empty || b.W < MinBandWidth) { tries.Add(cand.Key + " " + was + " (too narrow)"); continue; }
                if (Lines(b, fontU) >= 2) { o.WhyAt = cand.Key; o.Why = b; o.WhyLines = 2; o.WhyFontUnits = fontU; if (cut.Length > 0) o.Notes.Add("why: " + cand.Key + " " + was); break; }
                if (Lines(b, minU) >= 1) { o.WhyAt = cand.Key; o.Why = b; o.WhyLines = 1; o.WhyFontUnits = Math.Min(fontU, b.H / LineFactor); if (cut.Length > 0) o.Notes.Add("why: " + cand.Key + " " + was); break; }
                tries.Add(cand.Key + " " + was + " (too low for a line)");
            }
            if (o.WhyAt == "none")
            {
                string tried = tries.Count > 0 ? " [" + string.Join("; ", tries) + "]" : "";
                if (!i.Bonus.Empty) { o.WhyAt = "inline"; o.WhyLines = 1; o.Notes.Add("why: inline in the game's bonus label (no band" + tried + ")"); }
                else o.Notes.Add("why: no band and no bonus label - markers alone" + tried);
            }

            // the summary: under the slots, else the grid gap (when the WHY line left it), else the pillar
            int wanted = Math.Max(1, Math.Min(3, i.SumRows));
            var cands = new List<KeyValuePair<string, R4>> { new KeyValuePair<string, R4>("under-slots", slots) };
            if (o.WhyAt != "grid-gap") cands.Add(new KeyValuePair<string, R4>("grid-gap", gap));
            foreach (var cand in cands)
            {
                string scut; var b = TrimBest(cand.Value, blocked, fontU, minU, wanted, out scut);
                if (b.Empty || b.W < MinBandWidth || !Free(b, blocked)) continue;
                for (int r = wanted; r >= 1; r--)
                {
                    float f = 0;
                    if (Lines(b, fontU) >= r) f = fontU;
                    else if (Lines(b, minU) >= r) f = Math.Max(minU, Math.Min(fontU, b.H / (r * LineFactor)));
                    if (f <= 0) continue;
                    o.SumAt = cand.Key; o.Sum = b; o.SumRows = r; o.SumFontUnits = f;
                    break;
                }
                if (o.SumAt != "none") { if (scut.Length > 0) o.Notes.Add("summary: " + cand.Key + " " + cand.Value.Size + " cut to " + b.Size + " by " + scut); break; }
            }
            if (o.SumAt == "none")
            {
                var p = Pillar(i, wanted, o.LineUnits);
                if (!p.Empty && p.W >= MinBandWidth * 0.9f && Free(p, blocked)) { o.SumAt = "pillar"; o.Sum = p; o.SumRows = wanted; o.SumFontUnits = fontU; }
            }
            if (o.SumAt == "none") o.Notes.Add("summary: no band - the WHY line shows row 1 while no badge is highlighted");
            else
            {
                if (o.SumRows < wanted && wanted >= 3) o.Dropped.Add("yard");
                if (o.SumRows < Math.Min(wanted, 2)) o.Dropped.Add("swap");
            }
            return o;
        }

        /// <summary>"[loadout] layout" text: the screen, the canvas, the measured bands.</summary>
        public static string Describe(LayoutIn i, LayoutOut o, int screenW, int screenH)
        {
            return screenW + "x" + screenH + ": canvas " + Math.Round(i.Canvas.W) + "x" + Math.Round(i.Canvas.H) + " (unit " + i.UnitPx.ToString("0.000", CultureInfo.InvariantCulture) + " px) | view " + i.View
                + " | grid " + i.Grid.Length + " buttons | slots " + i.Slots.Length + " " + R4.Union(i.Slots) + " | obstacles " + i.Obstacles.Length + " | bands " + o.Bands;
        }

        /// <summary>"why grid-gap 943x143 (2 lines) | summary under-slots 1090x205 (2 rows) | font 40.5 u = 27 px | marker 57 u (number 20 px)"</summary>
        public static string Drawn(LayoutOut o, float unitPx)
        {
            var ic = CultureInfo.InvariantCulture;
            return "why " + o.WhyAt + (o.WhyAt == "none" || o.WhyAt == "inline" ? "" : " " + o.Why.Size + " (" + o.WhyLines + " line" + (o.WhyLines == 1 ? "" : "s") + ")")
                + " | summary " + o.SumAt + (o.SumAt == "none" ? "" : " " + o.Sum.Size + " (" + o.SumRows + " row" + (o.SumRows == 1 ? "" : "s") + ")")
                + " | font " + o.FontUnits.ToString("0.0", ic) + " u = " + Math.Round(o.FontPx).ToString(ic) + " px | marker " + Math.Round(o.MarkerUnits).ToString(ic)
                + " u (number " + Math.Round(o.NumberUnits * unitPx).ToString(ic) + " px)";
        }
    }
}
