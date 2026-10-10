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
//    with a pad of 0.35 x the font above and below - 0.25 for the last resort of 0.15.0, one 15 px line where no band held one at the
//    full pad (1920 x 1080's four-button level-up) - (wrapped text is laid out with Overflow, so a line never vanishes -
//    the TMP pitfall of single-line rects lower than 1.3 x their font).
// 0.15.0 (C15-12, the user's decision Q4 of 10-06): on a screen wider than 16:9 whose wings WideMenus opened (the game's frame
// hidden, the canvas past the view on both sides: 660 units a side at 3440 x 1440), the WHY goes in a PANEL in the wing beside
// the selected card instead (Side): the left wing for the left-most card, the right one - past the team panel - for the
// right-most, the nearer one for a card between; at most 600 units wide (19.5 ems at a larger "Card text size"), up to four
// reasons at the cards' own reason size (20.5 px on the PC, never under 15) under the header (the lead - WHY / CLOSE CALL - and,
// 0.16.0 (C16-10), the name of the card it explains, on one line or the name on a line or two of its own: Header), one reason
// a line - a long one wraps at its spaces, its next line hanging a little further in (Wrap), the panel growing upward for it up to
// the cards' top (0.15.x, C-M1 of the 10-07 review: "up to 4 lines" counts reasons) -, its bottom on the selected card's
// reason line; clear of every card, button, the divider, the team panel and the skip reward. A wing whose text room is under nine
// ems leaves the WHY to the band, and 16:9, 16:10 (the Steam Deck) and WideMenus off keep the band exactly as 0.14.0 laid it
// (the third of four cards keeps its wing - beside the selected card, as Q4 chose; the 10-07 review's C-m8: the header names the
// card since 0.16.0, and where the panel goes on four-card screens is the user's answer, C16-10b). The action hint keeps its line.
// 0.16.0 (C16-14, DK-C03 of the 10-07 Deck round): a preferred size within NearPx of the 15 px floor (the Deck's 16 - 16.8 px) tries
// the wanted lines at 15 px - the full pad, then the tight one - before one line at the preferred size; the lines wanted per band.
// 0.16.0 (C16-15, DK-C04): a stretch must hold the block's first item at 15 px (WhyBandIn.NeedW) - the selected card's own piece when it
// does, else the band's widest (at 1280 x 800 the hint's line under its button cut the band, and a rescue card's piece held no item at all).
// 0.16.0 (C16-11g): EdgeCut - the recommend frame's bottom edge either side of the card's TIER / NEW / RECRUIT plate (Badge.Cut); r1 fix
// (10-08): PlateCut / PlateLine - Badge.Cut's decision and proof line (in the card ROOT's units: y grows UP there, unlike below).
// Also Q4: the band's lines are measured as drawn (FitLines) - the end diamond stood over the last glyph at 3440 x 1440.
// Coordinates: view units with the origin at the view's TOP-LEFT and y growing DOWN (LoadoutLayout's R4).
// Pure (no game types): the bench lays the PC (3440 x 1440) and the Steam Deck (1280 x 800) screens out (the PC's level-up
// screen as measured on the user's mark of 10-05 19:12, the Deck's modelled from it) and checks that nothing covers a card
// line, a button or the divider; the side panel at 3440 x 1440, 2560 x 1080, 1920 x 1080 and 1280 x 800 (GeometryCases.cs).
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
        public float TextUnits, ChromeUnits;    // 0.16.0 (C16-14): the words' width at the preferred size (the lead, the items, the separators) and the block's chrome; 0: Want as given
        public float NeedW;                     // 0.16.0 (C16-15): the width the block needs for its first item at the 15 px floor - the chrome, the slack, the lead and
                                                // the first item (NeedWidth); a stretch narrower than max(MinWidth, NeedW) is passed over while a wider one exists. 0: MinWidth
    }

    /// <summary>Where the WHY band goes: a stretch of one band, the text size and the lines it holds there.</summary>
    internal sealed class WhySpot
    {
        public string At = "none";              // "under the cards", "over the buttons" (under the divider rule), "under the buttons", "none"
        public R4 Seg;                          // the stretch (the band's height, the width clear of obstacles)
        public float Font; public int Lines;
        public float Pad = ScreenBand.Pad;      // the block's pad above and below its lines, in fonts (ScreenBand.TightPad: the last resort)
        public string Bands = "";               // every band measured, for the log
        public bool Widest;                     // 0.16.0 (C16-15): the stretch is the band's widest - the selected card's own could not hold the first item
    }

    /// <summary>What the WHY panel in a side wing is laid out from (0.15.0, C15-12; view units, y down).</summary>
    internal sealed class WhySideIn
    {
        public R4 View;                         // the game's 3840-unit frame (the selection screen's view)
        public R4 Canvas;                       // what the player sees: past the view on both sides when WideMenus hid the frame on a screen wider than 16:9, else the view
        public R4[] Cards = new R4[0];          // each card with the ribbons and the reason line under it (the selected one at its hover size)
        public float AnchorX = float.NaN;       // the selected card's centre
        public float ReasonBottom = float.NaN;  // the selected card's lowest line (its reason line at its hover size): the panel's bottom
        public R4[] Obstacles = new R4[0];      // the action buttons and their plates, the divider rule and diamond, the hint's line, the team panel, the skip reward, the FREE labels
        public float FontUnits;                 // the cards' reason size (15 px at least), in view units
        public float TipUnits;                  // the end diamonds' width: each stands half outside the block
        public float ChromeUnits;               // round the text inside the block: the diamonds' inner halves and the pads, both sides (0: unknown)
    }

    /// <summary>Where the WHY panel goes in a wing (At "none": the band under the cards does the job).</summary>
    internal sealed class WhySide
    {
        public string At = "none";              // "side wing left", "side wing right", "none"
        public bool Left;
        public R4 Room;                         // the block's room: the wing clear of everything (the diamonds inside it), its bottom on the reason line, its top
                                                // the cards' top (0.15.x, C-M1: the panel grows upward for a wrapped reason)
        public int Lines;                       // the reasons' lines the room holds under the lead's own line
        public float Width;                     // the widest the block may be
        public string Wings = "";               // both wings as measured, for the log
        public int Pos, Of;                     // 0.16.0 (C16-10): the selected card is card Pos of Of in the row, left to right (0 / 0: not measured) - the [why] line's ", card 2 of 3"
    }

    /// <summary>One line of the side panel: the item it shows (all of it or a part), its words and their width, whether it carries on
    /// the item's line above.</summary>
    internal sealed class SideLine { public int Item; public string Text = ""; public bool Cont; public float Width; }

    internal static class ScreenBand
    {
        public const float Gap = 8f;            // canvas units between two things
        public const float LinePitch = 1.25f, Pad = 0.35f, MinWidth = 600f, MinPx = 15f;
        public const float NearPx = 2f;         // 0.16.0 (C16-14): a preferred size within this of the 15 px floor tries the wanted lines at the floor first
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
        public static int LinesIn(float h, float font) { return LinesIn(h, font, Pad); }

        /// <summary>The lines a band of height <paramref name="h"/> holds at the font <paramref name="font"/> with a pad of <paramref name="pad"/> fonts.</summary>
        public static int LinesIn(float h, float font, float pad)
        {
            if (!(font > 0) || !(h > 0)) return 0;
            int n = (int)Math.Floor((h - 2 * pad * font) / (LinePitch * font));
            return Math.Max(0, Math.Min(MaxLines, n));
        }

        /// <summary>The height of a block of <paramref name="lines"/> lines at <paramref name="font"/>.</summary>
        public static float BlockHeight(int lines, float font) { return BlockHeight(lines, font, Pad); }
        public static float BlockHeight(int lines, float font, float pad) { return lines * LinePitch * font + 2 * pad * font; }

        /// <summary>The pad of the last resort: one 15 px line in a band that holds it with a quarter of the font above and below, not the
        /// 0.35 of every other band (the review of 10-06: at 1920 x 1080 a four-button level-up had 55 units over the buttons for the
        /// 58.5 a 15 px line needs, and since 0.14.0 never showed the WHY at all - the most common screen there is).</summary>
        public const float TightPad = 0.25f;

        /// <summary>The WHY band's place: the band under the cards, else the band under the buttons, whichever holds the lines
        /// wanted at the preferred size (the one under the cards first), then one line at the preferred size, then one at 15 px.
        /// 0.16.0 (C16-14, DK-C03 of the 10-07 Deck round: at 1280 x 800 the one-line band dropped reasons in 41 of 97 views although two
        /// lines at 15 px fit): when the preferred size is within <see cref="NearPx"/> of the 15 px floor (the Deck's 16 - 16.8 px; the
        /// PC's 20.5 px is 5.5 px over it and keeps every pass as it was), the passes are the wanted lines at the preferred size, the wanted
        /// lines at 15 px with the full pad, the same with <see cref="TightPad"/>, then one line at the preferred size and one at 15 px; the
        /// lines wanted are counted per band from <see cref="WhyBandIn.TextUnits"/> (a band the hint's line cut asks for more than the
        /// cards' whole span), and a band that wants one line skips the two 15 px passes.
        /// 0.16.0 (C16-15, DK-C04 of the 10-07 Deck round: at 1280 x 800 the hint's line under its button cut the band, and a rescue card's
        /// piece of it - 640 units left of the line - held no item at all): a stretch must be at least <see cref="Need"/> wide - the block's
        /// first item at 15 px (<see cref="WhyBandIn.NeedW"/>) - so the selected card's own piece is taken only when it holds that, else the
        /// band's widest piece (the block then sits at its end nearest the card: <see cref="Left"/>), and the passes skip narrower pieces.
        /// Where no piece of any band is that wide, the band is laid out as before (from <see cref="MinWidth"/>).</summary>
        public static WhySpot Why(WhyBandIn x)
        {
            if (x == null) return new WhySpot();
            float need = Need(x);
            if (need <= MinWidth) return Lay(x, MinWidth);
            var spot = Lay(x, need);
            if (spot.At != "none") return spot;
            // no stretch holds the first item at 15 px: as C16-14 laid it out - a later item may still fit
            var old = Lay(x, MinWidth);
            old.Bands += "; no stretch holds the first item (" + need.ToString("0", CultureInfo.InvariantCulture) + " units at 15 px)";
            return old;
        }

        /// <summary>0.16.0 (C16-15): the width a stretch must have - <see cref="MinWidth"/>, or <see cref="WhyBandIn.NeedW"/> when that is more.</summary>
        public static float Need(WhyBandIn x) { return x != null && x.NeedW > MinWidth ? x.NeedW : MinWidth; }

        /// <summary>0.16.0 (C16-15): the width a block needs to show its first item - its chrome (the end diamonds' inner halves and the pads),
        /// the slack, the lead (with its gap) and the item, all at <paramref name="font"/> (WhyUi measures them at the 15 px floor).</summary>
        public static float NeedWidth(float leadW, float itemW, float chrome, float font) { return Math.Max(0f, chrome) + Slack(font) + Math.Max(0f, leadW) + Math.Max(0f, itemW); }

        // Why's layout with stretches at least <need> wide (MinWidth up to 0.15.0)
        static WhySpot Lay(WhyBandIn x, float need)
        {
            var spot = new WhySpot();
            bool buttons = x.Buttons != null && x.Buttons.Any(b => !b.Empty);
            float buttonsTop = buttons ? x.Buttons.Where(b => !b.Empty).Min(b => b.Y0) : x.Bottom;
            float buttonsBottom = buttons ? x.Buttons.Where(b => !b.Empty).Max(b => b.Y1) : x.Bottom;
            var bands = new List<KeyValuePair<string, R4>>();
            if (!float.IsNaN(x.CardsBottom)) Split(x, bands, "under the cards", "over the buttons", x.CardsBottom + Gap, buttonsTop - Gap, need);
            if (buttons) Split(x, bands, "under the buttons", "under the buttons", buttonsBottom + Gap, x.Bottom - Gap, need);
            int want = Math.Max(1, Math.Min(MaxLines, x.Want));
            var notes = new List<string>();
            foreach (var b in bands) notes.Add(b.Key + " " + b.Value.Size + " (" + LinesIn(b.Value.H, x.FontUnits) + " line(s) at the preferred size, " + LinesIn(b.Value.H, x.MinFontUnits) + " at 15 px)");
            spot.Bands = string.Join("; ", notes);
            // the lines wanted at the preferred size; 0.16.0 (C16-14) near the floor: the lines wanted at 15 px (the full pad, then the tight
            // one); one line at the preferred size; one line at 15 px
            bool near = Near(x);
            foreach (var pass in near ? NearPasses : Passes)
                foreach (var b in bands)
                {
                    if (b.Value.W < need) continue;                                      // 0.16.0 (C16-15): need = MinWidth, or the first item's NeedW
                    bool many = pass == 0 || pass == 3 || pass == 4;                     // the passes of the lines wanted
                    float font = pass == 2 || pass == 3 || pass == 4 ? x.MinFontUnits : x.FontUnits;
                    float pad = pass == 4 ? TightPad : Pad;
                    int wantB = many && near && x.TextUnits > 0 ? WantIn(x, b.Value, font) : want;
                    if ((pass == 3 || pass == 4) && wantB < 2) continue;                 // one line wanted here: the 15 px passes add nothing
                    int n = LinesIn(b.Value.H, font, pad);
                    if (n < (many ? wantB : 1)) continue;
                    spot.At = b.Key; spot.Seg = b.Value; spot.Font = font; spot.Lines = Math.Min(n, many ? wantB : want); spot.Pad = pad;
                    if (pass == 3 || pass == 4)
                        spot.Bands += "; " + spot.Lines + " lines at 15 px" + (pass == 4 ? " with the tight pad (" + TightPad.ToString("0.##", CultureInfo.InvariantCulture) + " of the font)" : "")
                            + " - the preferred size is within " + NearPx.ToString("0.#", CultureInfo.InvariantCulture) + " px of it";
                    MarkWidest(x, spot, need);
                    return spot;
                }
            // the last resort (0.15.0): one line at 15 px with the tight pad - only where no band held one at the full pad, so every
            // band 0.14.0 chose stays as it was
            foreach (var b in bands)
            {
                if (b.Value.W < need || LinesIn(b.Value.H, x.MinFontUnits, TightPad) < 1) continue;
                spot.At = b.Key; spot.Seg = b.Value; spot.Font = x.MinFontUnits; spot.Lines = 1; spot.Pad = TightPad;
                spot.Bands += "; one 15 px line with the tight pad (" + TightPad.ToString("0.##", CultureInfo.InvariantCulture) + " of the font)";
                MarkWidest(x, spot, need);
                return spot;
            }
            return spot;
        }

        // 0.16.0 (C16-15): the stretch chosen is not the selected card's own piece because that one could not hold the first item (it is at
        // least MinWidth but under need): said in the log (WhySpot.Widest, and the offer's first [why] line)
        static void MarkWidest(WhyBandIn x, WhySpot spot, float need)
        {
            if (need <= MinWidth || (x.AnchorX >= spot.Seg.X0 && x.AnchorX <= spot.Seg.X1)) return;
            var own = Seg(x, spot.Seg.Y0, spot.Seg.Y1, MinWidth);
            if (!(x.AnchorX >= own.X0 && x.AnchorX <= own.X1) || own.W < MinWidth || own.W >= need) return;
            spot.Widest = true;
            spot.Bands += "; the widest stretch " + spot.Seg.Size + " - the selected card's own " + own.Size + " holds no first item ("
                + need.ToString("0", CultureInfo.InvariantCulture) + " units at 15 px)";
        }

        // the passes of Why: 0 = the lines wanted at the preferred size, 1 = one line at it, 2 = one line at 15 px; near the floor (0.16.0,
        // C16-14) 3 = the lines wanted at 15 px, 4 = the same with the tight pad, between 0 and 1
        static readonly int[] Passes = { 0, 1, 2 }, NearPasses = { 0, 3, 4, 1, 2 };

        /// <summary>0.16.0 (C16-14): the preferred size within <see cref="NearPx"/> of the 15 px floor (in the band's units), and lines wanted
        /// (Want over one, or the words' width given): the wanted lines are tried at 15 px before one line at the preferred size.</summary>
        public static bool Near(WhyBandIn x)
        {
            if (x == null || !(x.MinFontUnits > 0) || !(x.FontUnits > 0)) return false;
            if (!(x.Want > 1 || x.TextUnits > 0)) return false;
            return x.FontUnits - x.MinFontUnits <= NearPx * x.MinFontUnits / MinPx + 1e-3f;
        }

        /// <summary>0.16.0 (C16-14): the lines the words want in the band <paramref name="seg"/> at <paramref name="font"/> - their width at the
        /// preferred size scaled to the font, over the band's width less the chrome - 1 .. <see cref="MaxLines"/>.</summary>
        public static int WantIn(WhyBandIn x, R4 seg, float font)
        {
            if (x == null || !(x.TextUnits > 0) || !(x.FontUnits > 0)) return Math.Max(1, Math.Min(MaxLines, x == null ? 1 : x.Want));
            double lines = Math.Ceiling(x.TextUnits * (font / x.FontUnits) / Math.Max(1f, seg.W - Math.Max(0f, x.ChromeUnits)) - 1e-6);
            return (int)Math.Max(1, Math.Min(MaxLines, lines));
        }

        // a band from y0 to y1, cut at every divider rule that crosses it (0.14.0 review: on the PC's level-up screen the rule lies
        // between the cards' lowest line and the buttons - a block centred in that band sat across it and its diamond): the part
        // above the first rule is <first>, the parts below it <rest>
        static void Split(WhyBandIn x, List<KeyValuePair<string, R4>> bands, string first, string rest, float y0, float y1, float need)
        {
            float cur = y0; bool cut = false;
            foreach (var r in (x.Rules ?? new R4[0]).Where(r => !r.Empty && r.Y1 > y0 && r.Y0 < y1).OrderBy(r => r.Y0))
            {
                bands.Add(new KeyValuePair<string, R4>(cut ? rest : first, Seg(x, cur, r.Y0 - Gap, need)));
                cur = Math.Max(cur, r.Y1 + Gap); cut = true;
            }
            bands.Add(new KeyValuePair<string, R4>(cut ? rest : first, Seg(x, cur, y1, need)));
        }

        // one band's stretch: the span between y0 and y1, cut clear of every obstacle that reaches into it, the piece nearest the
        // selected card (the one holding it when it is at least <need> wide - MinWidth, or 0.16.0's C16-15 NeedW: the first item at 15 px -,
        // else the widest)
        static R4 Seg(WhyBandIn x, float y0, float y1, float need)
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
            var holding = pieces.Where(p => x.AnchorX >= p.Key && x.AnchorX <= p.Value && p.Value - p.Key >= need).ToList();
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

        // ---------------------------------------------------------------- the band's lines as drawn (0.15.0, Q4)
        /// <summary>The slack a block keeps past its widest line: a sixth of the font, 4 units at least.</summary>
        public static float Slack(float font) { return Math.Max(4f, font / 6f); }

        /// <summary>The block's width round its widest line (<paramref name="widest"/>: the text's right end, its indent included): that
        /// plus the chrome (the diamonds' halves and the pads both sides) and the slack, at most <paramref name="max"/>.</summary>
        public static float BlockWidth(float widest, float chrome, float font, float max) { return Math.Min(max, widest + chrome + Slack(font)); }

        /// <summary>The items of one band line as drawn: joined by <paramref name="sep"/>.</summary>
        public static string Join(IList<string> items, IList<int> line, string sep)
        {
            var parts = new List<string>(line.Count);
            foreach (int i in line) if (i >= 0 && i < items.Count) parts.Add(items[i]);
            return string.Join(sep, parts);
        }

        /// <summary>The band's lines measured as drawn (0.15.0, Q4): each line's items joined by <paramref name="sep"/> and measured whole
        /// by <paramref name="width"/>; a line whose end - after the <paramref name="indent"/> - passes <paramref name="room"/> loses items
        /// from its end until it fits, and a line left empty goes. Returns the widest end (the indent included). Up to 0.14.0 the band
        /// summed its pieces, and TMP leaves a text's trailing spaces out of its preferred width: each "  /  " came out two spaces short,
        /// and a line of four items ran past its room into the end diamond (the last letter of the last reason under the
        /// diamond, 3440 x 1440, 10-06).</summary>
        public static float FitLines(List<List<int>> lines, IList<string> items, string sep, Func<string, float> width, float indent, float room)
        {
            float widest = 0f;
            if (lines == null || items == null || width == null) return widest;
            foreach (var l in lines)
            {
                float w = 0f;
                while (l.Count > 0 && indent + (w = width(Join(items, l, sep))) > room) l.RemoveAt(l.Count - 1);
                if (l.Count > 0) widest = Math.Max(widest, indent + w);
            }
            lines.RemoveAll(l => l.Count == 0);
            return widest;
        }

        // ---------------------------------------------------------------- the recommend frame's bottom edge (0.16.0, C16-11g)
        /// <summary>The gold frame's bottom edge cut round the plate the game hangs on the card's bottom edge (TIER I on a weapon card, NEW on
        /// an ability, RECRUIT on a rescue card), as the game's own highlight passes behind it (C-m9 of the 10-07 review: the frame's bottom
        /// line ran through the plate since 0.13.0). The edge runs <paramref name="x0"/> .. <paramref name="x1"/>, the plate
        /// <paramref name="c0"/> .. <paramref name="c1"/> (one unit, e.g. the card root's). True when the plate lies inside the edge with
        /// <paramref name="gap"/> + 4 units or more left on each side: the edge is then drawn as <paramref name="x0"/> .. <paramref name="a1"/>
        /// (= c0 - gap) and <paramref name="b0"/> (= c1 + gap) .. <paramref name="x1"/>. False (a plate wider than that, outside the edge, or
        /// no plate): the edge whole (a1 = b0 = x1).</summary>
        public static bool EdgeCut(float x0, float x1, float c0, float c1, float gap, out float a1, out float b0)
        {
            a1 = x1; b0 = x1;
            if (float.IsNaN(x0) || float.IsNaN(x1) || float.IsNaN(c0) || float.IsNaN(c1) || !(c1 >= c0) || !(x1 > x0)) return false;
            gap = Math.Max(0f, gap);
            if (c0 - x0 < gap + 4f || x1 - c1 < gap + 4f) return false;
            a1 = c0 - gap; b0 = c1 + gap;
            return true;
        }

        /// <summary>r1 fix (10-08): Badge.Cut's decision, in the card root's units - the root's rect <paramref name="rx0"/> .. <paramref name="rx1"/>
        /// across, its bottom at <paramref name="ry0"/>; the frame is that rect inset <paramref name="inset"/>, its bottom bar
        /// <paramref name="thick"/> units high; the plate <paramref name="px0"/> .. <paramref name="px1"/> x <paramref name="py0"/> ..
        /// <paramref name="py1"/>. "cut" when the plate's y range reaches into the bar and EdgeCut leaves room either side (a1 / b0 the
        /// pieces' inner ends); "apart" when it misses the bar (r1's powerupLevelObject, the portrait's level diamond at y 212 .. 436);
        /// "wide" when it is on the bar with no room either side. The edge is whole for the last two (a1 = b0 = the bar's right end).</summary>
        public static string PlateCut(float rx0, float rx1, float ry0, float inset, float thick, float px0, float px1, float py0, float py1, float gap, out float a1, out float b0)
        {
            float x0 = rx0 + inset, x1 = rx1 - inset, e0 = ry0 + inset, e1 = e0 + thick;
            a1 = x1; b0 = x1;
            if (!(py0 < e1 && py1 > e0)) return "apart";
            return EdgeCut(x0, x1, px0, px1, gap, out a1, out b0) ? "cut" : "wide";
        }

        /// <summary>r1 fix (10-08): Badge.Cut's proof line for a PlateCut outcome, naming the object measured (<paramref name="obj"/>):
        /// '[badge] plate: &lt;class&gt; &lt;object&gt; &lt;w&gt;x&lt;h&gt; units, y &lt;y0&gt;..&lt;y1&gt; (the frame's bottom edge &lt;e0&gt;..&lt;e1&gt;) - edge cut &lt;a1&gt;..&lt;b0&gt;'
        /// (or '- no overlap, edge whole', '- x .. leaves no room either side of it on the edge .., edge whole').</summary>
        public static string PlateLine(string cls, string obj, string outcome, float rx0, float rx1, float ry0, float inset, float thick, float px0, float px1, float py0, float py1, float a1, float b0)
        {
            var inv = CultureInfo.InvariantCulture;
            float x0 = rx0 + inset, x1 = rx1 - inset, e0 = ry0 + inset, e1 = e0 + thick;
            string head = "[badge] plate: " + cls + " " + obj + " " + (px1 - px0).ToString("0", inv) + "x" + (py1 - py0).ToString("0", inv) + " units, y " + py0.ToString("0", inv) + ".." + py1.ToString("0", inv)
                + " (the frame's bottom edge " + e0.ToString("0", inv) + ".." + e1.ToString("0", inv) + ")";
            if (outcome == "cut") return head + " - edge cut " + a1.ToString("0", inv) + ".." + b0.ToString("0", inv);
            if (outcome == "apart") return head + " - no overlap, edge whole";
            return head + " - x " + px0.ToString("0", inv) + ".." + px1.ToString("0", inv) + " leaves no room either side of it on the edge " + x0.ToString("0", inv) + ".." + x1.ToString("0", inv) + ", edge whole";
        }

        // ---------------------------------------------------------------- the WHY panel in a side wing (0.15.0, C15-12)
        public const int SideMaxItems = 4;      // the reasons the panel shows, each on its own line(s), under the lead's own line (WHY / CLOSE CALL - the review
                                                // of 10-06: as a hanging indent the lead took five ems off every line). 0.15.x (C-M1 of the 10-07 review): the
                                                // user's "up to 4 lines" counts REASONS - up to 0.15.0 it counted wrapped lines, and live at 21:9 24 of 104 panels
                                                // dropped a reason (17 of them to a reason that wrapped); a wrapped reason now grows the panel upward instead
        public const int SideBaseLines = 4;     // the reasons' lines of the wing's measure (the width test: the lead's line and four more, 225 units at 21.6 px)
        public const float SideWidth = 600f;    // the panel's widest: 600 units - at the PC's 20.5 px reasons (30.75 units) about 45 characters a line -
        public const float SideEms = 19.5f;     // and as many ems at a larger "Card text size" (19.5 x 30.75 = 600)
        public const float SideMinEms = 9f;     // a wing whose TEXT room (the diamonds, pads and slack left out) is under nine ems - about 27 characters a line - leaves the WHY to the band
        public const float ContEms = 0.75f;     // a reason's next line hangs this much further in than its first

        /// <summary>The panel's text in pixels: the cards' own reason size, 15 px at least - the "Card text size" row moves both.</summary>
        public static float SidePx(float reasonPx) { return Math.Max(MinPx, reasonPx); }

        /// <summary>The WHY panel's wing: the free stretch between the canvas's edge and the game's 16:9 frame (or anything reaching past
        /// it), measured five lines high (the lead's and four reasons') with its bottom on the selected card's reason line, both sides
        /// kept <see cref="Gap"/> plus half a diamond clear; the left wing for a card nearer to it than to the right one (the left-most
        /// card), else the right one (past the team panel); the other when the nearer leaves its text under <see cref="SideMinEms"/>;
        /// "none" - the band - when both do, and on a screen whose canvas is no wider than the view (16:9; 16:10 like the Steam Deck;
        /// WideMenus off). A card between keeps its wing however far it stands from it (the user's Q4: beside the selected card; the
        /// 10-07 review's C-m8 - the third of four cards about 1000 px from the left wing's panel: since 0.16.0 (C16-10) the panel's
        /// header names its card, and where it goes on four-card screens is the user's answer, C16-10b). The room then reaches up to the
        /// cards' top (0.15.x, C-M1), clear of anything in the wing above the measure: a wrapped reason, or a name on rows of its own
        /// (<see cref="Header"/>), grows the panel upward (<see cref="WhySide.Lines"/>: the lines under the lead's own line).
        /// <see cref="WhySide.Pos"/> / <see cref="WhySide.Of"/>: which card of the row, left to right, the panel speaks for.</summary>
        public static WhySide Side(WhySideIn x)
        {
            var s = new WhySide();
            if (x == null || !(x.FontUnits > 0) || float.IsNaN(x.ReasonBottom)) { s.Wings = "no card measured"; return s; }
            if (x.View.X0 - x.Canvas.X0 < 1f && x.Canvas.X1 - x.View.X1 < 1f) { s.Wings = "no wings (the screen is no wider than the game's frame)"; return s; }
            float pad = Gap + Math.Max(0f, x.TipUnits) / 2f;
            float y1 = Math.Min(x.ReasonBottom, x.Canvas.Y1 - Gap), y0 = Math.Max(x.Canvas.Y0 + Gap, y1 - BlockHeight(SideBaseLines + 1, x.FontUnits));
            // a wing starts where the game's frame ended, or where anything at the panel's height reaches past it
            float inL = x.View.X0, inR = x.View.X1;
            foreach (var r in (x.Cards ?? new R4[0]).Concat(x.Obstacles ?? new R4[0]))
            {
                if (r.Empty || r.Y1 <= y0 || r.Y0 >= y1) continue;
                inL = Math.Min(inL, r.X0); inR = Math.Max(inR, r.X1);
            }
            var left = new R4(x.Canvas.X0 + pad, y0, inL - pad, y1);
            var right = new R4(inR + pad, y0, x.Canvas.X1 - pad, y1);
            // the test is the TEXT's room (the review of 10-06: the wing's own width passed while the lead and the chrome took five of its ems)
            float cap = Math.Max(SideWidth, SideEms * x.FontUnits), chrome = Math.Max(0f, x.ChromeUnits) + Slack(x.FontUnits);
            float textL = Math.Min(left.W, cap) - chrome, textR = Math.Min(right.W, cap) - chrome;
            float need = SideMinEms * x.FontUnits, twoLines = BlockHeight(2, x.FontUnits);
            bool okL = textL >= need && left.H >= twoLines, okR = textR >= need && right.H >= twoLines;
            float anchor = float.IsNaN(x.AnchorX) ? (x.View.X0 + x.View.X1) / 2f : x.AnchorX;
            // 0.16.0 (C16-10): which card of the row the panel speaks for (the card whose centre is nearest the anchor), for the [why] line
            var centres = (x.Cards ?? new R4[0]).Where(c => !c.Empty).Select(c => (c.X0 + c.X1) / 2f).OrderBy(c => c).ToList();
            if (centres.Count > 0)
            {
                int near = 0;
                for (int i = 1; i < centres.Count; i++) if (Math.Abs(centres[i] - anchor) < Math.Abs(centres[near] - anchor)) near = i;
                s.Pos = near + 1; s.Of = centres.Count;
            }
            s.Wings = "left " + Wing(left, textL, okL, x.FontUnits) + "; right " + Wing(right, textR, okR, x.FontUnits);
            if (!okL && !okR) return s;
            bool toLeft = okL && (!okR || anchor - left.X1 <= right.X0 - anchor);
            var room = toLeft ? left : right;
            s.Left = toLeft;
            s.Room = new R4(room.X0, Ceiling(x, room), room.X1, room.Y1);
            s.Lines = Math.Max(0, (int)Math.Floor((s.Room.H - 2 * Pad * x.FontUnits) / (LinePitch * x.FontUnits) + 1e-4) - 1);
            s.At = s.Left ? "side wing left" : "side wing right";
            s.Width = Math.Min(s.Room.W, cap);
            return s;
        }

        // 0.15.x (C-M1): how high the panel may grow in its wing - up to the cards' top (beside the cards, under anything the screen shows over
        // them), below anything else reaching into the wing above the measured room; never lower than the measure's own top
        static float Ceiling(WhySideIn x, R4 room)
        {
            float top = x.Canvas.Y0 + Gap, cardsTop = float.MaxValue;
            foreach (var c in x.Cards ?? new R4[0]) if (!c.Empty) cardsTop = Math.Min(cardsTop, c.Y0);
            if (cardsTop < float.MaxValue) top = Math.Max(top, cardsTop);
            foreach (var r in (x.Cards ?? new R4[0]).Concat(x.Obstacles ?? new R4[0]))
            {
                if (r.Empty || r.Y0 >= room.Y0 || r.X1 <= room.X0 - Gap || r.X0 >= room.X1 + Gap) continue;
                top = Math.Max(top, r.Y1 + Gap);
            }
            return Math.Min(top, room.Y0);
        }

        static string Wing(R4 r, float text, bool ok, float font)
        {
            if (r.W <= 0) return "none";
            return r.Size + " (text " + (Math.Max(0f, text) / font).ToString("0.#", CultureInfo.InvariantCulture) + " em)" + (ok ? "" : " too narrow");
        }

        /// <summary>The side panel's lines: one item a line, in order; an item wider than its line wraps at its spaces, its next lines
        /// <paramref name="cont"/> further in (narrower by as much), balanced - the shortest right end that keeps the item to as few
        /// lines, so no lone word hangs under a full line; an item that does not fit the lines left - or holds a word wider than a line
        /// - is left out with every item after it (the verdict's order: the first items say the most). <paramref name="width"/> measures
        /// a text as drawn; <paramref name="lineWidth"/> is the room of an item's first line.</summary>
        public static List<SideLine> Wrap(IList<string> items, Func<string, float> width, float lineWidth, float cont, int maxLines) { return Wrap(items, width, lineWidth, cont, maxLines, int.MaxValue); }

        /// <summary>As above, <paramref name="maxItems"/> items at most (0.15.x, C-M1: the panel's four reasons, however many lines they take).</summary>
        public static List<SideLine> Wrap(IList<string> items, Func<string, float> width, float lineWidth, float cont, int maxLines, int maxItems)
        {
            var lines = new List<SideLine>();
            if (items == null || width == null || maxLines <= 0 || maxItems <= 0 || !(lineWidth > 0)) return lines;
            cont = Math.Max(0f, cont);
            int shown = 0;
            for (int i = 0; i < items.Count && shown < maxItems; i++)
            {
                var words = (items[i] ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0) continue;
                var mine = Break(i, words, width, lineWidth, cont, lineWidth);
                if (mine != null && mine.Count > 1)
                {
                    float lo = 0f, hi = lineWidth;
                    for (int k = 0; k < 16 && hi - lo > 1f; k++)
                    {
                        float t = (lo + hi) / 2f;
                        var b = Break(i, words, width, lineWidth, cont, t);
                        if (b != null && b.Count <= mine.Count) hi = t; else lo = t;
                    }
                    mine = Break(i, words, width, lineWidth, cont, hi) ?? mine;
                }
                if (mine == null || lines.Count + mine.Count > maxLines) break;
                lines.AddRange(mine); shown++;
            }
            return lines;
        }

        // one item's words in lines whose right ends stay within min(lineWidth, end) - a carried-on line starts cont further in - filled
        // greedily; null: a word wider than its line
        static List<SideLine> Break(int item, string[] words, Func<string, float> width, float lineWidth, float cont, float end)
        {
            float first = Math.Min(lineWidth, end), next = first - cont;
            var mine = new List<SideLine>();
            string cur = null; float curW = 0f;
            foreach (var word in words)
            {
                string cand = cur == null ? word : cur + " " + word;
                float w = width(cand);
                if (w <= (mine.Count == 0 ? first : next)) { cur = cand; curW = w; continue; }
                if (cur == null) return null;
                mine.Add(new SideLine { Item = item, Text = cur, Cont = mine.Count > 0, Width = curW });
                curW = width(word);
                if (curW > next) return null;
                cur = word;
            }
            if (cur != null) mine.Add(new SideLine { Item = item, Text = cur, Cont = mine.Count > 0, Width = curW });
            return mine;
        }

        public const string Ellipsis = "...";

        /// <summary>The side panel's header (0.16.0, C16-10 - the build-now half of the 10-07 review's C-m8: a panel that cannot be told apart
        /// from its neighbour's): the lead (WHY / CLOSE CALL) and the name of the card it explains, as the card shows it. Line 0 is the lead's
        /// line (Item -1; its Text the name when the name shares it, else ""); the name's own rows follow (Item -2, Cont from the second).
        /// One line when <c>width("&lt;b&gt;" + lead + "&lt;/b&gt;  " + name)</c> fits <paramref name="room"/> - the line measured whole, as drawn
        /// (TMP leaves trailing spaces out of a width, so the pieces are never summed); else the lead alone and the name broken as
        /// <see cref="Wrap(IList{string}, Func{string, float}, float, float, int, int)"/> breaks a reason (balanced, a next row
        /// <paramref name="cont"/> further in), two rows at most: past two the greedy first row, then the next words while they fit with
        /// "..." after them in the second. An empty name, or a word wider than its row: the lead alone (as up to 0.15.x). The game's names
        /// are 29 characters at most and the names other mods lend 38, so neither of the last two happens to a real name.</summary>
        public static List<SideLine> Header(string lead, string name, Func<string, float> width, float room, float cont)
        {
            var head = new List<SideLine>();
            string bold = "<b>" + (lead ?? "") + "</b>";
            if (width == null) { head.Add(new SideLine { Item = -1 }); return head; }
            cont = Math.Max(0f, cont);
            string n = (name ?? "").Trim();
            if (n.Length > 0)
            {
                float one = width(bold + "  " + n);
                if (one <= room) { head.Add(new SideLine { Item = -1, Text = n, Width = one }); return head; }
            }
            head.Add(new SideLine { Item = -1, Text = "", Width = width(bold) });
            if (n.Length == 0) return head;
            var rows = Wrap(new[] { n }, width, room, cont, int.MaxValue, 1);
            if (rows.Count == 0) return head;                                   // a word wider than its row: the lead alone
            if (rows.Count > 2)
            {
                // two rows: the greedy first, then the words that fit before the ellipsis
                var words = n.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int k = 0; string r1 = null;
                while (k < words.Length) { string cand = r1 == null ? words[k] : r1 + " " + words[k]; if (r1 != null && width(cand) > room) break; r1 = cand; k++; }
                string r2 = null;
                while (k < words.Length) { string cand = r2 == null ? words[k] : r2 + " " + words[k]; if (width(cand + Ellipsis) > room - cont) break; r2 = cand; k++; }
                string t2 = k >= words.Length && r2 != null ? r2 : (r2 ?? "") + Ellipsis;
                rows = new List<SideLine> { new SideLine { Text = r1, Width = width(r1) }, new SideLine { Text = t2, Cont = true, Width = width(t2) } };
            }
            foreach (var r in rows) head.Add(new SideLine { Item = -2, Text = r.Text, Cont = r.Cont, Width = r.Width });
            return head;
        }

        /// <summary>The side panel's widest line end: the lead's indent, a carried-on line's further indent, its words.</summary>
        public static float Widest(IList<SideLine> lines, float indent, float cont)
        {
            float w = 0f;
            if (lines != null) foreach (var l in lines) w = Math.Max(w, indent + (l.Cont ? cont : 0f) + l.Width);
            return w;
        }

        /// <summary>The height of the diamonds' centre from the block's top: the first line's middle (a block of one line: its middle).</summary>
        public static float TipFromTop(float font) { return Pad * font + LinePitch * font / 2f; }
    }

    /// <summary>The size of the text on the selection screens (0.15.0, C15-06): the reason line under each card (with the RECOMMENDED
    /// ribbon), and following it the WHY band and the action hint's line - one setting, [General] BadgeScale, the mod menu's "Card
    /// text size". The reason line's font is 40 units at s = 1 on the card, which the game draws at 0.95 x 0.9 x 0.9 = 0.77 of its
    /// HUD canvas (a CanvasScaler of 3840 x 2160, Expand: 2160 units tall on 16:9 and wider screens, taller on taller ones - 2400 on
    /// the Steam Deck's 16:10, 2880 at 4:3). Measured: 20.5 px at s = 1 on 3440 x 1440 (10-06 19:16:59); hence 10.26 px on the Deck.
    ///  - Auto: enlarged until the reason line is 16 px, up to x1.3 - x1.6 on a canvas taller than 16:9 (the Deck: x1.56, 16.0 px;
    ///    0.14.0 stopped at x1.3 there, 13.3 px - under the WHY band's 15 px floor). A 16:9 screen under 1080 px (1280 x 720) keeps
    ///    the x1.3 cap and the floor lifts it to 15 px (the review of 10-06: the cap was keyed on the screen's height);
    ///  - a size picked in the menu (BadgeScale 0.5 - 1.6) is a share of Auto ON THIS SCREEN: 115 % draws 115 % of what Auto draws,
    ///    on the PC (20.5 -> 23.6 px) as on the Deck (16 -> 18.4 px). The review of 10-06: a pick was the line's own s, and on the
    ///    Deck (Auto x1.56) every step landed on or near the floor, each one smaller than Auto. A pick goes no further than x1.85
    ///    (RoomCap): with the ribbon held at x1.3 (RibbonCap) the hovered card's reason line stays above the game's divider rule;
    ///  - either way never under ScreenBand.MinPx (15 px, the floor of the WHY band and of the readout): a small screen gets the s
    ///    that reads 15 px even past the caps (1024 x 768: x1.83), up to x2.
    /// The WHY band and the hint keep their own automatic sizes and follow the pick by its ratio to Auto (Factor). The line's room in
    /// characters follows the size (LineChars: the reason rect is at most 900 units wide on the card). Pure: the bench checks the PC,
    /// the Deck and 1024 x 768, the divider and the room (GeometryCases.cs).</summary>
    internal static class CardTextSize
    {
        public const float ReasonUnits = 40f;           // the reason line's font at s = 1, on the card
        public const float CardChain = 0.7695f;         // the card on the HUD canvas: 0.95 x 0.9 x 0.9
        public const float RefW = 3840f, RefH = 2160f;  // the HUD canvas's reference (CanvasScaler, Expand)
        public const float TargetPx = 16f;              // what Auto enlarges to
        public const float AutoCap = 1.3f, TallAutoCap = 1.6f;      // Auto's most: x1.3 on a 16:9 or wider canvas, x1.6 on a taller one (the Deck's 2400 units)
        public const float RoomCap = 1.85f;             // a pick's most: the hovered card's reason line stays above the divider rule (the ribbon at RibbonCap)
        public const float RibbonCap = 1.3f;            // the RECOMMENDED ribbon grows no further, so the reason line under it keeps its room
        public const float MinPick = 0.5f, MaxPick = 1.6f, FloorCap = 2f;
        public const float WhyPreferPx = 22f;           // the WHY band's own ceiling on Auto (it takes the reason line's size, 15 - 22 px)
        public const float ReasonW = 800f, ReasonMaxW = 900f;       // the reason rect on the card: 800 x s units wide, at most 900 (Badge.Reason)
        public const float EmPerChar = 0.335f;          // the card font's average glyph on the builders' lines, in ems (the bench's glyph model)
        public const float DenseEmPerChar = 0.36f;      // a dense line's (capitals, m and w): 7.5 % wider - the room of a line drawn at the 15 px floor
        /// <summary>The menu row's steps: Auto (0), then 90 % .. 150 % of Auto (100 % is Auto itself).</summary>
        public static readonly float[] Steps = { 0f, 0.9f, 1.15f, 1.3f, 1.5f };

        /// <summary>The HUD canvas's height in units on a screen of <paramref name="w"/> x <paramref name="h"/> pixels.</summary>
        public static float CanvasUnits(float w, float h)
        {
            if (!(w > 0) || !(h > 0)) return RefH;
            return w / h >= RefW / RefH ? RefH : RefW * h / w;
        }

        /// <summary>The reason line's height in pixels at s = 1 on a screen whose HUD canvas is <paramref name="canvasUnits"/> tall
        /// (the model of what Badge measures on the card).</summary>
        public static float ReasonPx1OnCanvas(float screenH, float canvasUnits) { return canvasUnits > 0 ? ReasonUnits * CardChain * screenH / canvasUnits : 0f; }
        public static float ReasonPx1(float w, float h) { return ReasonPx1OnCanvas(h, CanvasUnits(w, h)); }

        /// <summary>A HUD canvas taller than 16:9's 2160 units (the Deck's 16:10: 2400; 4:3: 2880).</summary>
        public static bool Tall(float canvasUnits) { return canvasUnits > RefH + 0.5f; }

        /// <summary>How far Auto may enlarge on a HUD canvas <paramref name="canvasUnits"/> tall: x1.6 on a taller one than 16:9, else x1.3.</summary>
        public static float Cap(float canvasUnits) { return Tall(canvasUnits) ? TallAutoCap : AutoCap; }

        /// <summary>The s that draws a text whose size at s = 1 is <paramref name="px1"/> pixels at <paramref name="px"/> pixels or
        /// more: at most x2 (a screen smaller than 1024 x 768 stays there).</summary>
        static float AtLeast(float px1, float px) { return px1 > 0 ? Math.Min(FloorCap, px / px1) : 1f; }

        /// <summary>The automatic s of a text whose size at s = 1 is <paramref name="px1"/> pixels on a canvas <paramref name="canvasUnits"/>
        /// tall: 16 px where the cap allows it, never under 15 px.</summary>
        public static float Auto(float px1, float canvasUnits)
        {
            if (!(px1 > 0)) return 1f;
            float s = Math.Max(1f, Math.Min(Cap(canvasUnits), TargetPx / px1));
            return Math.Max(s, AtLeast(px1, ScreenBand.MinPx));
        }

        /// <summary>A picked share of Auto, within the cfg's range (0.5 - 1.6).</summary>
        public static float Pick(float setting) { return Math.Max(MinPick, Math.Min(MaxPick, setting)); }

        /// <summary>The s the reason line is drawn at: <paramref name="setting"/> 0 (or less) = Auto, else that share of Auto on this
        /// screen, at most x1.85 (RoomCap; Auto's own s when the floor took it past that); never under 15 px.</summary>
        public static float Scale(float setting, float px1, float canvasUnits)
        {
            float auto = Auto(px1, canvasUnits);
            if (!(setting > 0) || !(px1 > 0)) return auto;
            float s = Math.Min(Math.Max(RoomCap, auto), auto * Pick(setting));
            return Math.Max(s, AtLeast(px1, ScreenBand.MinPx));
        }

        /// <summary>The pick's size against Auto's on this screen (1 on Auto): what the WHY band and the hint follow.</summary>
        public static float Factor(float setting, float px1, float canvasUnits)
        {
            if (!(setting > 0) || !(px1 > 0)) return 1f;
            return Scale(setting, px1, canvasUnits) / Auto(px1, canvasUnits);
        }

        /// <summary>The s of a text that follows the pick (the action hint: 40 units in the screen's view, <paramref name="px1"/> pixels
        /// at s = 1): its own Auto times <paramref name="factor"/>, never under 15 px.</summary>
        public static float Follow(float px1, float canvasUnits, float factor)
        {
            float s = Auto(px1, canvasUnits) * (factor > 0 ? factor : 1f);
            return Math.Max(s, AtLeast(px1, ScreenBand.MinPx));
        }

        /// <summary>The WHY band's text in pixels: the reason line's size, at least 15 px, at most 22 px times the pick's factor.</summary>
        public static float WhyPx(float reasonPx, float factor)
        {
            float top = Math.Max(ScreenBand.MinPx, WhyPreferPx * (factor > 0 ? factor : 1f));
            return Math.Max(ScreenBand.MinPx, Math.Min(top, reasonPx));
        }

        /// <summary>The RECOMMENDED ribbon's s at the reason line's <paramref name="s"/>: the same up to x1.3, no larger past it - the
        /// reason line hangs under the ribbon, and a larger pick goes to the words.</summary>
        public static float RibbonScale(float s) { return Math.Min(s, RibbonCap); }

        /// <summary>The visible characters a reason line holds at <paramref name="s"/>, its "2ND" / "AVOID" included: the rect's room in
        /// ems (800 x s units, at most 900, over a 40 x s font) at the card font's average glyph - Wording.DeckWidth (56) up to about
        /// x1.2, 51 at x1.3, 43 at x1.56, 36 at x1.85. Ranker gives each card's words that room (Wording.RoomBeside).</summary>
        public static int LineChars(float s) { return LineChars(s, 0f); }

        /// <summary>As <see cref="LineChars(float)"/> on a screen whose reason line reads <paramref name="px1"/> pixels at s = 1: where the
        /// line is drawn at the 15 px floor already (1366 x 768, 1280 x 720 on Auto) Badge.Fit cannot shrink a dense line any further,
        /// so the room counts a dense line's glyphs (7.5 % wider than the average) - 45 characters at 1366 x 768's x1.37 instead of 49; a line
        /// with room to shrink between the two (the Deck's 16 px: 42 instead of 43), and the average from 7.5 % above the floor up.</summary>
        public static int LineChars(float s, float px1)
        {
            if (!(s > 0)) s = 1f;
            float em = Math.Min(ReasonW * s, ReasonMaxW) / (ReasonUnits * s);
            float margin = px1 > 0 ? Math.Max(1f, s * px1 / ScreenBand.MinPx) : 2f;
            float perChar = Math.Max(EmPerChar, DenseEmPerChar / margin);
            return Math.Max(1, Math.Min(Wording.DeckWidth, (int)Math.Floor(em / perChar + 1e-4f)));
        }

        /// <summary>The next step of the menu row from <paramref name="setting"/> (<paramref name="d"/> = +1 / -1), round the list;
        /// a value set by hand between two steps goes to the neighbour on that side.</summary>
        public static float Step(float setting, int d)
        {
            float v = setting > 0 ? setting : 0f;
            int at = Array.FindIndex(Steps, x => Math.Abs(x - v) < 0.005f);
            if (at >= 0) return Steps[(at + (d >= 0 ? 1 : -1) + Steps.Length) % Steps.Length];
            if (d >= 0) { foreach (var x in Steps) if (x > v) return x; return Steps[0]; }
            for (int i = Steps.Length - 1; i >= 0; i--) if (Steps[i] < v) return Steps[i];
            return Steps[Steps.Length - 1];
        }

        /// <summary>The menu row's next step on this screen: as <see cref="Step(float, int)"/>, passing over a step that draws the line at a
        /// smaller step's size here (<see cref="SameAsSmaller"/>: on the Deck 150 % stops at the room's x1.85 as 130 % does; on 1280 x 720,
        /// 90 % is the floor Auto already reads), or at the size it has now - every press changes what the screen shows, the same steps
        /// both ways round, and Auto is never passed over. 0.15.x (C-m4 of the 10-07 review): up to 0.15.0 only a step at the size shown
        /// NOW or at Auto's was passed over, so the Deck offered "150% (19 px)" going left from Auto and "130% (19 px)" going right.</summary>
        public static float Step(float setting, int d, float px1, float canvasUnits)
        {
            float from = Scale(setting, px1, canvasUnits), v = setting;
            for (int i = 0; i < Steps.Length; i++)
            {
                v = Step(v, d);
                if (!(v > 0)) return v;
                float s = Scale(v, px1, canvasUnits);
                if (Math.Abs(s - from) > 0.005f && !SameAsSmaller(v, px1, canvasUnits)) return v;
            }
            return v;
        }

        /// <summary>A step of the row that draws the line here at the size of a step before it in <see cref="Steps"/> (Auto first): the row
        /// leaves it out on this screen (the Deck: 150 % = 130 %; 1280 x 720: 90 % = Auto). False for Auto and for a value set by hand.</summary>
        public static bool SameAsSmaller(float setting, float px1, float canvasUnits)
        {
            int at = Array.FindIndex(Steps, x => setting > 0 && Math.Abs(x - setting) < 0.005f);
            if (at <= 0) return false;
            float s = Scale(setting, px1, canvasUnits);
            for (int i = 0; i < at; i++) if (Math.Abs(Scale(Steps[i], px1, canvasUnits) - s) <= 0.005f) return true;
            return false;
        }

        /// <summary>The menu's words for a setting: "Auto" or "115%".</summary>
        public static string Label(float setting) { return setting > 0 ? ((int)Math.Round(setting * 100f)).ToString(CultureInfo.InvariantCulture) + "%" : "Auto"; }
    }
}
