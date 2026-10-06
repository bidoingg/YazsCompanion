// The band manager of the selection screens (0.14.0): where the action hint's line (RerollHint.cs) and the WHY band
// (WhyUi.cs) go, from rects measured on the screen. One manager for both, so they never stand on each other:
//  - the hint keeps the place it has had since 0.12.2 (the player liked it): over its button when the band from the cards'
//    lowest line down to the button holds at least 0.72 of the line (measured on the PC's rescue screens: 111 units at
//    3440 x 1440), else under the button, else the button's gold frame alone; on a button right of the view's centre (Skip)
//    the line grows left from the button's right edge, and it never runs into the team panel or the skip reward (HintX);
//  - the WHY band takes the band under the cards (from the cards' lowest line - the selected card's grown by its hover
//    animation, about 7 % - down to the action buttons), cut in two by the game's divider rule over the buttons (above it
//    "under the cards", below it "over the buttons"), or the band under the buttons, whichever holds more of it, in that
//    order; in each, the widest stretch that stays clear of the hint's line, the team panel, the divider's diamond and the
//    labels the screen shows, nearest to the selected card. Text at least 15 px (the readout's rule), at most 3 lines, each line 1.25 x the font
//    with a pad of 0.35 x the font above and below (wrapped text is laid out with Overflow, so a line never vanishes -
//    the TMP pitfall of single-line rects lower than 1.3 x their font).
// Coordinates: view units with the origin at the view's TOP-LEFT and y growing DOWN (LoadoutLayout's R4).
// Pure (no game types): the bench lays the PC (3440 x 1440) and the Steam Deck (1280 x 800) screens out (the PC's level-up
// screen as measured on the user's mark of 10-05 19:12, the Deck's modelled from it) and checks that nothing covers a card
// line, a button or the divider.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace YazsCompanion
{
    /// <summary>Where the action hint's line goes (Where null: nowhere - the button's frame alone).</summary>
    internal sealed class HintSpot
    {
        public string Where;                    // "over" the button, "under" it, null
        public float Top, K = 1f;               // the line's top (y down) and its shrink (0.72 .. 1)
        public float Band, Below, Need;         // the band over the button, the room under it, what the line needs
    }

    /// <summary>What the WHY band is laid out from (view units, y down).</summary>
    internal sealed class WhyBandIn
    {
        public R4 View;
        public float Bottom;                    // the lowest visible y: the view's bottom, or the screen's when the game's frame is hidden
        public float CardsBottom = float.NaN;   // the cards' lowest line (the selected card grown by its hover); NaN: no card measured
        public float SpanX0, SpanX1;            // the cards' and the action buttons' horizontal extent
        public R4[] Buttons = new R4[0];        // the action buttons shown (and their count plates)
        public R4[] Obstacles = new R4[0];      // the hint's line, the team panel, the FREE labels, the skip reward, the divider's diamond
        public R4[] Rules = new R4[0];          // the game's divider rule over the buttons: a band is above it or below it, never across it
        public float AnchorX;                   // the selected card's centre
        public float FontUnits, MinFontUnits;   // the preferred text size and the 15 px floor, in view units
        public int Want = 1;                    // the lines the words would take over the widest stretch (1 .. 3)
    }

    /// <summary>Where the WHY band goes: a stretch of one band, the text size and the lines it holds there.</summary>
    internal sealed class WhySpot
    {
        public string At = "none";              // "under the cards", "over the buttons" (under the divider rule), "under the buttons", "none"
        public R4 Seg;                          // the stretch (the band's height, the width clear of obstacles)
        public float Font; public int Lines;
        public string Bands = "";               // every band measured, for the log
    }

    internal static class ScreenBand
    {
        public const float Gap = 8f;            // canvas units between two things
        public const float LinePitch = 1.25f, Pad = 0.35f, MinWidth = 600f, MinPx = 15f;
        public const int MaxLines = 3;
        public const float HintNeed = 0.72f;    // the share of the hint's line a band must hold (0.12.2)

        /// <summary>The action hint's line (RerollHint's rule since 0.12.2): <paramref name="lowest"/> = the cards' lowest line (y down,
        /// NaN: none), <paramref name="floorTop"/> = the button's top or the top of the FREE label over it, <paramref name="canvasBottom"/>
        /// = the screen's bottom, <paramref name="lineH"/> = the line's full height.</summary>
        public static HintSpot Hint(float lowest, R4 button, float floorTop, float canvasBottom, float lineH)
        {
            var s = new HintSpot { Need = HintNeed * lineH + 2 * Gap };
            s.Band = float.IsNaN(lowest) ? lineH + 2 * Gap : floorTop - lowest;
            if (s.Band >= s.Need)
            {
                s.K = Math.Min(1f, (s.Band - 2 * Gap) / lineH);
                s.Top = floorTop - s.Band + (s.Band - lineH * s.K) / 2f;
                s.Where = "over";
                return s;
            }
            s.Below = canvasBottom - button.Y1;
            if (s.Below >= s.Need)
            {
                s.K = Math.Min(1f, (s.Below - 2 * Gap) / lineH);
                s.Top = button.Y1 + Gap;
                s.Where = "under";
            }
            return s;
        }

        /// <summary>The hint line's left edge, and its width in <paramref name="w"/>. On a button left of the view's centre the line
        /// starts at the button's left edge and grows right (the rescue screen's Reroll button, as since 0.12.2); on a button right
        /// of it - the Skip button, next to the team panel - it ends at the button's right edge and grows left. Either way it stops
        /// short of an obstacle in its way (the team panel, the skip reward): the text then ends in an ellipsis instead of covering
        /// it. Up to the 10-05 review the line always grew right, and on a level-up a SKIP line would have run 300 - 500 px into the
        /// team panel's Danger Level box (3440 x 1440).</summary>
        public static float HintX(R4 button, float width, float viewCentre, float top, float bottom, IList<R4> obstacles, out float w)
        {
            bool right = (button.X0 + button.X1) / 2f > viewCentre;
            w = width;
            if (obstacles != null)
                foreach (var o in obstacles)
                {
                    if (o.Empty || o.Y1 <= top || o.Y0 >= bottom) continue;
                    if (right) { if (o.X1 <= button.X1 && o.X1 + Gap > button.X1 - w) w = Math.Max(0f, button.X1 - o.X1 - Gap); }
                    else if (o.X0 >= button.X0 && o.X0 - Gap < button.X0 + w) w = Math.Max(0f, o.X0 - Gap - button.X0);
                }
            return right ? button.X1 - w : button.X0;
        }

        /// <summary>The lines a band of height <paramref name="h"/> holds at the font <paramref name="font"/> (0..MaxLines).</summary>
        public static int LinesIn(float h, float font)
        {
            if (!(font > 0) || !(h > 0)) return 0;
            int n = (int)Math.Floor((h - 2 * Pad * font) / (LinePitch * font));
            return Math.Max(0, Math.Min(MaxLines, n));
        }

        /// <summary>The height of a block of <paramref name="lines"/> lines at <paramref name="font"/>.</summary>
        public static float BlockHeight(int lines, float font) { return lines * LinePitch * font + 2 * Pad * font; }

        /// <summary>The WHY band's place: the band under the cards, else the band under the buttons, whichever holds the lines
        /// wanted at the preferred size (the one under the cards first), then one line at the preferred size, then one at 15 px.</summary>
        public static WhySpot Why(WhyBandIn x)
        {
            var spot = new WhySpot();
            if (x == null) return spot;
            bool buttons = x.Buttons != null && x.Buttons.Any(b => !b.Empty);
            float buttonsTop = buttons ? x.Buttons.Where(b => !b.Empty).Min(b => b.Y0) : x.Bottom;
            float buttonsBottom = buttons ? x.Buttons.Where(b => !b.Empty).Max(b => b.Y1) : x.Bottom;
            var bands = new List<KeyValuePair<string, R4>>();
            if (!float.IsNaN(x.CardsBottom)) Split(x, bands, "under the cards", "over the buttons", x.CardsBottom + Gap, buttonsTop - Gap);
            if (buttons) Split(x, bands, "under the buttons", "under the buttons", buttonsBottom + Gap, x.Bottom - Gap);
            int want = Math.Max(1, Math.Min(MaxLines, x.Want));
            var notes = new List<string>();
            foreach (var b in bands) notes.Add(b.Key + " " + b.Value.Size + " (" + LinesIn(b.Value.H, x.FontUnits) + " line(s) at the preferred size, " + LinesIn(b.Value.H, x.MinFontUnits) + " at 15 px)");
            spot.Bands = string.Join("; ", notes);
            // the lines wanted at the preferred size; one line at it; one line at 15 px
            foreach (var pass in new[] { 0, 1, 2 })
                foreach (var b in bands)
                {
                    if (b.Value.W < MinWidth) continue;
                    float font = pass == 2 ? x.MinFontUnits : x.FontUnits;
                    int n = LinesIn(b.Value.H, font);
                    if (n < (pass == 0 ? want : 1)) continue;
                    spot.At = b.Key; spot.Seg = b.Value; spot.Font = font; spot.Lines = Math.Min(n, want);
                    return spot;
                }
            return spot;
        }

        // a band from y0 to y1, cut at every divider rule that crosses it (0.14.0 review: on the PC's level-up screen the rule lies
        // between the cards' lowest line and the buttons - a block centred in that band sat across it and its diamond): the part
        // above the first rule is <first>, the parts below it <rest>
        static void Split(WhyBandIn x, List<KeyValuePair<string, R4>> bands, string first, string rest, float y0, float y1)
        {
            float cur = y0; bool cut = false;
            foreach (var r in (x.Rules ?? new R4[0]).Where(r => !r.Empty && r.Y1 > y0 && r.Y0 < y1).OrderBy(r => r.Y0))
            {
                bands.Add(new KeyValuePair<string, R4>(cut ? rest : first, Seg(x, cur, r.Y0 - Gap)));
                cur = Math.Max(cur, r.Y1 + Gap); cut = true;
            }
            bands.Add(new KeyValuePair<string, R4>(cut ? rest : first, Seg(x, cur, y1)));
        }

        // one band's stretch: the span between y0 and y1, cut clear of every obstacle that reaches into it, the piece nearest the
        // selected card (the one holding it, else the widest)
        static R4 Seg(WhyBandIn x, float y0, float y1)
        {
            if (y1 <= y0) return new R4(x.SpanX0, y0, x.SpanX0, y0);
            var pieces = new List<KeyValuePair<float, float>> { new KeyValuePair<float, float>(Math.Max(x.SpanX0, x.View.X0), Math.Min(x.SpanX1, x.View.X1)) };
            foreach (var o in x.Obstacles ?? new R4[0])
            {
                if (o.Empty || o.Y1 <= y0 || o.Y0 >= y1) continue;
                float c0 = o.X0 - Gap, c1 = o.X1 + Gap;
                var next = new List<KeyValuePair<float, float>>();
                foreach (var p in pieces)
                {
                    if (c1 <= p.Key || c0 >= p.Value) { next.Add(p); continue; }
                    if (c0 > p.Key) next.Add(new KeyValuePair<float, float>(p.Key, c0));
                    if (c1 < p.Value) next.Add(new KeyValuePair<float, float>(c1, p.Value));
                }
                pieces = next;
            }
            pieces = pieces.Where(p => p.Value > p.Key).ToList();
            if (pieces.Count == 0) return new R4(x.SpanX0, y0, x.SpanX0, y1);
            var holding = pieces.Where(p => x.AnchorX >= p.Key && x.AnchorX <= p.Value && p.Value - p.Key >= MinWidth).ToList();
            var pick = holding.Count > 0 ? holding[0] : pieces.OrderByDescending(p => p.Value - p.Key).First();
            return new R4(pick.Key, y0, pick.Value, y1);
        }

        /// <summary>The block's left edge: <paramref name="width"/> wide, centred on <paramref name="anchor"/>, kept inside the stretch.</summary>
        public static float Left(R4 seg, float anchor, float width)
        {
            float w = Math.Min(width, seg.W);
            float x0 = anchor - w / 2f;
            if (x0 < seg.X0) x0 = seg.X0;
            if (x0 + w > seg.X1) x0 = seg.X1 - w;
            return x0;
        }

        public static string Text(R4 r) { return r.ToString() + " " + r.Size; }
    }
}
