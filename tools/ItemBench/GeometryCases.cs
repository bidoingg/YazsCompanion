// 0.15.0 (roadmap 2026-10-06, Companion items 5 and 6: C15-05, C15-06): the size of the text on the selection screens and the
// menu's room, section 15 of the bench:
//   G1  Auto on the screens that matter (CardTextSize, ScreenBand.cs): 3440 x 1440 stays at s = 1, 20.5 px (the live line of
//       10-06 19:16:59); the Steam Deck (1280 x 800) reaches 16 px (s = 1.56; 0.14.0 stopped at x1.3, 13.3 px); 1024 x 768 stays at
//       or above the 15 px floor (s = 1.83, past the x1.6 cap); 1080p, 4K, 16:10 and 21:9 desktops at 16 px or more; 16:9 under
//       1080 px (1280 x 720) keeps the x1.3 cap of a 2160-unit canvas and reads the floor's 15 px (the review of 10-06).
//   G2  the floor everywhere from 1024 x 768 up, at every step of the menu row and at the ends of the cfg's range: the reason line
//       15 px or more, the WHY band 15 px or more and never larger than the reason line (the inverted hierarchy of 0.14.0 on the
//       Deck), the hint 15 px or more; under 1024 x 768 the scale stops at x2.
//   G3  a size picked is a share of Auto on that screen (the review of 10-06: as the line's own s every Deck step sat on or near the
//       floor, smaller than Auto): on the PC it scales the reason line, the WHY band (its 22 px ceiling moves with it) and the hint
//       alike; on the Deck 115 % reads 18.4 px; the room (x1.85) above, the floor below; Auto leaves the WHY band and the hint as
//       0.14.0 drew them.
//   G4  the menu row: Auto, 90 %, 115 %, 130 %, 150 % round the list (100 % is Auto), a value set by hand goes to its neighbour, the
//       labels; on a screen where two steps draw the same, the row passes over the second (the Deck's 150 % = 130 %).
//   G5  the canvas model against the canvases the logs name (2160 on 16:9 and wider, 2400 on the Deck, 2880 at 4:3).
//   G9  the reason line under the card against the game's divider rule (y 1902 in the view, the 10-05 mark): the hovered card's glyphs
//       stay above it at every screen and step - the ribbon held at x1.3; the divider's diamond is reported (a watch, not a check).
//   G10 the line's room follows the size (CardTextSize.LineChars): 56 characters at the PC's x1, 42 at the Deck's x1.56; a sweep of the
//       ability and weapon lines at every screen and step, as Ranker sizes them, measured with the glyph model against the reason rect
//       (800 x s units, at most 900) - at its size or shrunk to the 15 px floor (Badge.Fit), never cut.
//   S   the sources: the cards, the hint and the WHY band size through CardTextSize; [General] BadgeScale 0 or 0.5 - 1.6 with its
//       description; the DISPLAY row; the ADVICE and DISPLAY rows in the menu's scroller with the walks' check (C15-05).
// 0.15.0 item 12 (C15-12, the user's decision Q4 of 10-06): the WHY panel in the side wings, and the band's end diamond:
//   G6  the panel's wing on the selection screens laid out from the bench's PC geometry (HintCases: the user's mark of 10-05 19:12)
//       and the 10-06 live run (the canvas 5161 x 2160 at 3440 x 1440, 19:16:22.774; the four cards of the level-up at 19:16:59.704
//       spanning x 141 - 2811 by its 'under the cards 1285' and 'under the buttons 2670' stretches; the rescue screen's reroll hint
//       at 19:18:56.839, 741 x 62 units in the 111-unit band): at 21:9 the three reasons of 19:16:59.704 fully in the left wing at
//       the cards' 20.5 px, beside the left-most card, the right-most card's past the team panel, the nearer wing for a card
//       between; the rescue screen's three with the hint's line where 0.12.2 drew it; 2560 x 1080 at its 16 px; 150 % in the wing's
//       room; nowhere at 18:9 (wings of 240 units), with WideMenus off, at 16:9 and on the Steam Deck - the band's choice there as
//       0.14.0 made it. Every panel clear of the cards, the buttons, the divider, the hint, the team panel, inside the canvas. The lead
//       (WHY / CLOSE CALL) on its own line (the review of 10-06: as a hanging indent it took five ems off every line), CLOSE CALL and
//       130 % / 150 % cases; 1920 x 1080's band now one 15 px line with the tight pad (no band there since 0.14.0).
//   G7  the panel's lines (Wrap): one reason a line, a long one wrapped further in, at most four lines, the order kept.
//   G8  the end diamond: the 10-06 capture's band line (four reasons at 15 px, the last glyph under the right diamond) with the
//       pieces summed as 0.14.0 did and TMP's trailing spaces left out - and measured as drawn (FitLines): clear of the diamond.
//   S   the sources: WhyUi's side path, the wings' query in WideMenus, the separator measured between two glyphs.
// 0.15.x (C-M1 / C-m8 of the 10-07 review: 24 of 104 live side panels at 21:9 dropped a reason, 17 to a wrapped one):
//   G11 the LIVE geometry of the series (21.6 px - the hovered card's reason line -, wings of 609 x 225 units, 16.1 em of text room at
//       3440 x 1440): 0.15.0's cap of four wrapped lines reproduced on the live WHYs (a 68-character "goes first" showing 2 of 3, three
//       of four reasons, 4 of 5); now four REASONS, the panel growing upward up to the cards' top - all shown, a fifth left out; the
//       side panel's "vs #1" sentence without the first card's own line ("X goes first: weapons first", "X goes first"); the rail: every
//       side item within WhyText.Budget, never the first card's line, two lines at most at the live geometry, every block's reasons
//       shown (up to four); the third of four cards - about 1000 px from the left wing's panel - keeps the left wing (the user's Q4:
//       beside the selected card; C-m8: since 0.16.0 the header names the card, the placement is the user's answer C16-10b), its distance reported.
// 0.16.0 (C16-10, the build-now half of the 10-07 review's C-m8: a panel that could not be told from its neighbour's):
//   G11 also: WhySide.Pos / Of - which card of the row the panel speaks for (the [why] line's ", card 2 of 3"); the wings unchanged.
//   G12 the side panel's header: the lead and the card's name on one line exactly when that line fits, else the name on rows of its own
//       (two at most, the second ending in "..." past two), the lead alone for a word wider than a row; per name at 3440 x 1440 (Auto,
//       the live 21.6 px, 130 %, 150 %), 2560 x 1080 and 5120 x 1440; every card of the 3- and 4-card level-ups and of the rescue screen
//       headed with a long game name and a 38-character one: clear, every reason shown (up to four).
//   S   SidePanel's header (Names.Name, ScreenBand.Header), the Wrap and the block's height counting its rows, the [why] line's count.
// 0.16.0 (C16-11g, C-m9: the recommend frame's bottom line ran through the card's TIER / NEW / RECRUIT plate):
//   G13 EdgeCut: a centred plate cuts the edge symmetrically, an off-centre one off-centre; a plate too wide or outside: the edge whole.
//       r1 fix (10-08): PlateCut / PlateLine on the game's own card geometry (the level2 scene, read offline): the plate
//       'Content/AddonDescription' cuts the edge -211 .. 209 with the line the live redo greps; r1's powerupLevelObject ('PowerupLevel',
//       the portrait's level diamond) is 'apart' - the r1 live line reproduced; the drawn plate (its sprite's opaque part) clears the cut.
//   S   the frame still the card's last child, Cut only on the recommended card; the plate read as className's parent.
// 0.16.0 (C16-14, DK-C03 of the 10-07 Deck round: 41 of 97 WHY bands at 1280 x 800 dropped reasons although two 15 px lines fit):
//   B2  the WHY band at 1280 x 800 with the live Deck numbers: two wanted lines at 15 px where one fitted at 16 - 16.8 px, the tight
//       pad in a 139-unit band, the lines wanted per band where the hint's line cut it, 17.5 px and one wanted line unchanged; the short
//       "goes first" (WhyBlock.SideItems) in the band when an item still drops.
//   S   WhyUi passes the words' width and chrome, and falls back to the short form only when FitLines dropped an item.
// 0.16.0 (C16-15, DK-C04 of the 10-07 Deck round: the REROLL line under its button cut the band; a rescue card's piece of 640 units held
//   no item at all):
//   B3  the live geometry of B2 with the line's rects: the chest's 961-unit line leaves 1745 units at x 1165, its short words 2255 at 655;
//       the rescue screen's #2 card (left of the 934-unit line, its first item needing 785 units at 15 px) takes the widest piece, 1028 at
//       1837 (was 640 at 247), 1348 at 1517 with the short words; #1 1028 / 1348; a first item no piece holds: the band as before; the
//       PC unchanged; stacking the WHY over the line does not fit the 153-unit band; no WHY block over the line in any B2 / B3 case.
//   S   WhyUi measures the first item at 15 px before the preferred size and passes NeedW; Seg and the passes need max(MinWidth, NeedW).
// The glyph widths are a model of the card font (average widths per glyph class, calibrated on the live band lines of 10-06 -
// 910 units at 19:16:59.704, 781 at 19:18:44.438 - trailing spaces left out as TMP leaves them).
// Generic names only (the repository is public).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class GeometryCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string F(double v, string f = "0.00") { return v.ToString(f, CultureInfo.InvariantCulture); }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        const float Eps = 0.001f;

        /// <summary>A screen: the reason line's pixels at s = 1, the s and the pixels at a setting, and what follows them.</summary>
        sealed class Shot
        {
            public float W, H, C, Setting, Px1, S, Px, Factor, WhyPx, HintPx;
            public Shot(float w, float h, float setting)
            {
                W = w; H = h; Setting = setting;
                C = CardTextSize.CanvasUnits(w, h);
                Px1 = CardTextSize.ReasonPx1(w, h);
                S = CardTextSize.Scale(setting, Px1, C); Px = Px1 * S;
                Factor = CardTextSize.Factor(setting, Px1, C);
                WhyPx = CardTextSize.WhyPx(Px, Factor);
                float hint1 = 40f * h / C;                                      // the hint: 40 units in the screen's view, unscaled
                HintPx = hint1 * CardTextSize.Follow(hint1, C, Factor);
            }
            public string Name { get { return W + "x" + H; } }
            public override string ToString() { return Name + " " + CardTextSize.Label(Setting) + ": s " + F(S) + ", reason " + F(Px, "0.0") + " px, WHY " + F(WhyPx, "0.0") + " px, hint " + F(HintPx, "0.0") + " px"; }
        }

        public static int Run()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.15.0: the card text size (CardTextSize: the reason lines, the WHY band, the hint) and the menu's room");
            Auto();
            Floors();
            Picks();
            Row();
            Canvas();
            Divider();
            Room();
            Sources();
            Console.WriteLine("\n=== 0.15.0: the WHY panel in the side wings (C15-12, the user's Q4: ScreenBand.Side / Wrap) and the band's end diamond (FitLines)");
            SideWings();
            SideWrap();
            Caps();
            SideLive();
            SideHeader();
            SideSources();
            Console.WriteLine("\n=== 0.16.0: the recommend frame's edge round the card's plate (C16-11g: ScreenBand.EdgeCut) and the WHY band at 1280 x 800 (C16-14, C16-15)");
            EdgeCuts();
            DeckBand();
            HintBand();
            Sources016();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        // ---------------------------------------------------------------- G1: Auto
        static void Auto()
        {
            var pc = new Shot(3440, 1440, 0);
            Check("G1", "3440 x 1440 on Auto: s = 1, the reason line stays at 20.5 px (the live line of 10-06 19:16:59)", Math.Abs(pc.S - 1f) < Eps && Math.Abs(pc.Px - 20.5f) < 0.05f, pc.ToString());
            var deck = new Shot(1280, 800, 0);
            float old = Math.Min(1.3f, Math.Max(1f, 16f / deck.Px1)) * deck.Px1;          // 0.14.0's rule: Auto capped at x1.3 everywhere
            Check("G1", "the Steam Deck (1280 x 800) on Auto: 16 px or more (the cap is x1.6 on a canvas taller than 16:9: 2400 units)", deck.Px >= 16f - Eps && deck.S <= CardTextSize.TallAutoCap + Eps && CardTextSize.Tall(deck.C), deck + " | 0.14.0: " + F(old, "0.0") + " px");
            var small = new Shot(1024, 768, 0);
            Check("G1", "1024 x 768 on Auto: at or above the 15 px floor, past the x1.6 cap", small.Px >= ScreenBand.MinPx - Eps && small.S > CardTextSize.TallAutoCap, small.ToString());
            var hd720 = new Shot(1280, 720, 0); var hd768 = new Shot(1366, 768, 0);
            Check("G1", "16:9 under 1080 px (1280 x 720, 1366 x 768) on Auto: the x1.3 cap of a 2160-unit canvas, the floor lifting the line to 15 px (the review of 10-06: the x1.6 cap was keyed on the screen's height - x1.56 there, on a band under the cards no taller than at 1080p)",
                Math.Abs(hd720.Px - 15f) < 0.01f && Math.Abs(hd768.Px - 15f) < 0.01f && !CardTextSize.Tall(hd720.C) && !CardTextSize.Tall(hd768.C), hd720 + " | " + hd768);
            foreach (var s in new[] { new Shot(1920, 1080, 0), new Shot(1920, 1200, 0), new Shot(2560, 1080, 0), new Shot(2560, 1440, 0), new Shot(3840, 2160, 0) })
                Check("G1", s.Name + " on Auto: 16 px or more, enlarged no further than its cap", s.Px >= 16f - Eps && s.S <= CardTextSize.Cap(s.C) + Eps, s.ToString());
        }

        // ---------------------------------------------------------------- G2: the floor
        static readonly float[][] Screens =
        {
            new[] { 1024f, 768f }, new[] { 1280f, 720f }, new[] { 1280f, 800f }, new[] { 1280f, 1024f }, new[] { 1366f, 768f }, new[] { 1600f, 900f },
            new[] { 1920f, 1080f }, new[] { 1920f, 1200f }, new[] { 2560f, 1080f }, new[] { 2560f, 1440f }, new[] { 2560f, 1600f }, new[] { 3440f, 1440f }, new[] { 3840f, 2160f },
        };

        static void Floors()
        {
            var settings = CardTextSize.Steps.Concat(new[] { CardTextSize.MinPick, CardTextSize.MaxPick, 0.3f }).ToArray();
            int n = 0; var bad = new List<string>();
            float lowReason = float.MaxValue, lowWhy = float.MaxValue, lowHint = float.MaxValue;
            foreach (var sc in Screens)
                foreach (var v in settings)
                {
                    var s = new Shot(sc[0], sc[1], v); n++;
                    lowReason = Math.Min(lowReason, s.Px); lowWhy = Math.Min(lowWhy, s.WhyPx); lowHint = Math.Min(lowHint, s.HintPx);
                    if (s.Px < ScreenBand.MinPx - Eps || s.WhyPx < ScreenBand.MinPx - Eps || s.WhyPx > s.Px + Eps || s.HintPx < ScreenBand.MinPx - Eps) bad.Add(s.ToString());
                }
            Check("G2", "from 1024 x 768 up, at every step and at the cfg's ends (0.5, 1.6; 0.3 reads as 0.5): reason line, WHY band and hint at 15 px or more, the WHY band never larger than the reason line",
                bad.Count == 0, n + " screens x settings; lowest reason " + F(lowReason, "0.0") + " px, WHY " + F(lowWhy, "0.0") + " px, hint " + F(lowHint, "0.0") + " px" + (bad.Count > 0 ? " | " + string.Join(" | ", bad.Take(4)) : ""));
            var tiny = new Shot(800, 600, 0);
            Check("G2", "under 1024 x 768 (800 x 600) the scale stops at x2", Math.Abs(tiny.S - CardTextSize.FloorCap) < Eps, tiny.ToString());
        }

        // ---------------------------------------------------------------- G3: a picked size
        static void Picks()
        {
            var auto = new Shot(3440, 1440, 0);
            Check("G3", "3440 x 1440 on Auto: the WHY band at the reason line's 20.5 px and the hint at 26.7 px, as 0.14.0 drew them (WHY: the reason line within 15 - 22 px; hint: 40 units at x1)",
                Math.Abs(auto.WhyPx - Math.Max(15f, Math.Min(22f, auto.Px))) < Eps && Math.Abs(auto.HintPx - 40f * 1440f / 2160f) < 0.01f && Math.Abs(auto.Factor - 1f) < Eps, auto.ToString());
            var want = new Dictionary<float, float> { { 0.9f, 18.5f }, { 1f, 20.5f }, { 1.15f, 23.6f }, { 1.3f, 26.7f }, { 1.5f, 30.8f } };
            var lines = new List<string>(); bool ok = true;
            foreach (var kv in want)
            {
                var s = new Shot(3440, 1440, kv.Key);
                ok &= Math.Abs(s.Px - kv.Value) < 0.06f && Math.Abs(s.Factor - kv.Key) < Eps && Math.Abs(s.WhyPx - Math.Min(s.Px, 22f * kv.Key)) < Eps && Math.Abs(s.HintPx - auto.HintPx * kv.Key) < 0.01f;
                lines.Add(CardTextSize.Label(kv.Key) + " " + F(s.Px, "0.0") + "/" + F(s.WhyPx, "0.0") + "/" + F(s.HintPx, "0.0"));
            }
            Check("G3", "3440 x 1440, each step: the reason line at that share of its own size, the WHY band's 22 px ceiling and the hint scaled alike (reason / WHY / hint px)", ok, string.Join(", ", lines));
            var big = new Shot(3440, 1440, 1.5f);
            Check("G3", "150 % on the PC: the WHY band grows past its Auto ceiling with the cards (30.8 px, not stopped at 22)", big.WhyPx > 22f + 1f && Math.Abs(big.WhyPx - big.Px) < Eps, big.ToString());
            var deck = new Shot(1280, 800, 0);
            Check("G3", "the Deck on Auto: the WHY band and the hint at 16 px with the cards (the hint's x1.2 as in 0.14.0)", Math.Abs(deck.WhyPx - deck.Px) < Eps && Math.Abs(deck.HintPx - 16f) < 0.01f, deck.ToString());
            var d90 = new Shot(1280, 800, 0.9f); var d100 = new Shot(1280, 800, 1f); var d115 = new Shot(1280, 800, 1.15f); var d130 = new Shot(1280, 800, 1.3f); var d150 = new Shot(1280, 800, 1.5f);
            Check("G3", "the Deck's steps are shares of its Auto (the review of 10-06: as the line's own s, 90 % - 130 % all sat on the 15 px floor and 150 % at 15.4 px, each smaller than Auto's 16): 90 % at the floor, 100 % = Auto, 115 % 18.4 px, 130 % and 150 % at the room's x1.85 (19.0 px), the WHY band and the hint growing with them",
                Math.Abs(d90.Px - 15f) < 0.01f && Math.Abs(d100.S - deck.S) < Eps && Math.Abs(d115.Px - 18.4f) < 0.06f && Math.Abs(d130.S - CardTextSize.RoomCap) < Eps && Math.Abs(d150.S - CardTextSize.RoomCap) < Eps
                && d115.Px > deck.Px && d115.WhyPx > deck.WhyPx && d115.HintPx > deck.HintPx && d130.HintPx > d115.HintPx,
                d90 + " | " + d115 + " | " + d130 + " | " + d150);
            // every step means the same share on every screen: Auto x the share, held by the floor below and the room above
            int n = 0; var off = new List<string>(); float above = float.MaxValue; string aboveAt = "";
            foreach (var sc in Screens)
            {
                var a = new Shot(sc[0], sc[1], 0);
                foreach (var v in CardTextSize.Steps.Where(x => x > 0))
                {
                    var t = new Shot(sc[0], sc[1], v); n++;
                    float expect = Math.Max(Math.Min(a.S * v, Math.Max(CardTextSize.RoomCap, a.S)), ScreenBand.MinPx / t.Px1);
                    if (Math.Abs(t.S - expect) > Eps) off.Add(t.ToString());
                    if (v > 1f && t.Px - a.Px < above) { above = t.Px - a.Px; aboveAt = t.Name + " " + CardTextSize.Label(v); }
                    if (v > 1f && t.Px < a.Px - Eps) off.Add(t + " under Auto");
                }
            }
            Check("G3", "every step is the same share of Auto on every screen (" + n + " screens x steps): s = Auto x the share, held by the 15 px floor below and the room (x1.85) above; no step over 100 % draws smaller than Auto",
                off.Count == 0, "the least a step over 100 % adds: " + F(above, "0.0") + " px (" + aboveAt + ")" + (off.Count > 0 ? " | " + string.Join(" | ", off.Take(4)) : ""));
            var hd = new Shot(1920, 1080, 0); var hd100 = new Shot(1920, 1080, 1f); var hd150 = new Shot(1920, 1080, 1.5f);
            Check("G3", "1920 x 1080: a hand-set 1.0 is Auto itself (16 px; up to the review it read 15.4 px, smaller than Auto), 150 % 24 px",
                Math.Abs(hd100.Px - hd.Px) < Eps && Math.Abs(hd.Px - 16f) < 0.01f && Math.Abs(hd150.Px - 24f) < 0.01f, hd + " | " + hd100 + " | " + hd150);
        }

        // ---------------------------------------------------------------- G4: the menu row
        static void Row()
        {
            var up = new List<string>(); float v = 0f;
            for (int i = 0; i <= CardTextSize.Steps.Length; i++) { up.Add(CardTextSize.Label(v)); v = CardTextSize.Step(v, 1); }
            var down = new List<string>(); v = 0f;
            for (int i = 0; i <= CardTextSize.Steps.Length; i++) { down.Add(CardTextSize.Label(v)); v = CardTextSize.Step(v, -1); }
            Check("G4", "the row steps Auto, 90%, 115%, 130%, 150% round the list, both ways (100 % is Auto itself now)",
                string.Join(" ", up) == "Auto 90% 115% 130% 150% Auto" && string.Join(" ", down) == "Auto 150% 130% 115% 90% Auto", string.Join(" > ", up) + " | " + string.Join(" > ", down));
            Check("G4", "a value set by hand goes to its neighbour: 1.2 -> 130% / 115%, 1.6 -> Auto / 150%, 0.5 -> 90% / Auto, 1.0 -> 115% / 90%",
                Math.Abs(CardTextSize.Step(1.2f, 1) - 1.3f) < Eps && Math.Abs(CardTextSize.Step(1.2f, -1) - 1.15f) < Eps && CardTextSize.Step(1.6f, 1) == 0f && Math.Abs(CardTextSize.Step(1.6f, -1) - 1.5f) < Eps
                && Math.Abs(CardTextSize.Step(0.5f, 1) - 0.9f) < Eps && CardTextSize.Step(0.5f, -1) == 0f && Math.Abs(CardTextSize.Step(1f, 1) - 1.15f) < Eps && Math.Abs(CardTextSize.Step(1f, -1) - 0.9f) < Eps);
            // on a screen: a step that draws what the row shows now, or what Auto draws, is passed over (Auto never is)
            Func<float, float, int, string> cycle = (w, h, d) =>
            {
                float px1 = CardTextSize.ReasonPx1(w, h), c = CardTextSize.CanvasUnits(w, h), x = 0f; var seen = new List<string>();
                for (int i = 0; i <= CardTextSize.Steps.Length; i++) { seen.Add(CardTextSize.Label(x)); x = CardTextSize.Step(x, d, px1, c); if (!(x > 0)) { seen.Add("Auto"); break; } }
                return string.Join(" ", seen);
            };
            string pcUp = cycle(3440, 1440, 1), pcDown = cycle(3440, 1440, -1), dkUp = cycle(1280, 800, 1), dkDown = cycle(1280, 800, -1), hdUp = cycle(1280, 720, 1), hdDown = cycle(1280, 720, -1);
            // 0.15.x (C-m4 of the 10-07 review): a step that draws a smaller step's size is left out both ways round - up to 0.15.0 the Deck
            // offered "150% (19 px)" going left from Auto and "130% (19 px)" going right, two labels for one size
            float dkPx1 = CardTextSize.ReasonPx1(1280, 800), dkC = CardTextSize.CanvasUnits(1280, 800);
            Check("G4", "on the screen: every press changes the size and the row is the same both ways round - the PC keeps all five; the Deck leaves out 150 % (the same x1.85 as 130 %); 1280 x 720 leaves out 90 % (Auto's own floor) and 150 % (C-m4)",
                pcUp == "Auto 90% 115% 130% 150% Auto" && pcDown == "Auto 150% 130% 115% 90% Auto" && dkUp == "Auto 90% 115% 130% Auto" && dkDown == "Auto 130% 115% 90% Auto"
                && hdUp == "Auto 115% 130% Auto" && hdDown == "Auto 130% 115% Auto"
                && CardTextSize.SameAsSmaller(1.5f, dkPx1, dkC) && !CardTextSize.SameAsSmaller(1.3f, dkPx1, dkC) && !CardTextSize.SameAsSmaller(0f, dkPx1, dkC) && !CardTextSize.SameAsSmaller(1.4f, dkPx1, dkC)
                && Math.Abs(CardTextSize.Step(1.5f, -1, dkPx1, dkC) - 1.15f) < Eps && CardTextSize.Step(1.5f, 1, dkPx1, dkC) == 0f,
                "PC " + pcUp + " / " + pcDown + " | Deck " + dkUp + " / " + dkDown + " | 1280x720 " + hdUp + " / " + hdDown);
            Check("G4", "the labels: Auto for 0 (and below), whole percents otherwise", CardTextSize.Label(0f) == "Auto" && CardTextSize.Label(-1f) == "Auto" && CardTextSize.Label(1.15f) == "115%" && CardTextSize.Label(1.6f) == "160%");
        }

        // ---------------------------------------------------------------- G5: the canvas model
        static void Canvas()
        {
            Check("G5", "the HUD canvas (3840 x 2160, Expand): 2160 units on 16:9 and 21:9, 2400 on the Deck's 16:10, 2880 at 4:3 (the [badge] / [wide] lines' canvases)",
                CardTextSize.CanvasUnits(1920, 1080) == 2160f && CardTextSize.CanvasUnits(3440, 1440) == 2160f && Math.Abs(CardTextSize.CanvasUnits(1280, 800) - 2400f) < 0.01f && Math.Abs(CardTextSize.CanvasUnits(1024, 768) - 2880f) < 0.01f);
            Check("G5", "the reason line at s = 1: 20.5 px on 3440 x 1440 (measured live), 10.3 px on the Deck",
                Math.Abs(CardTextSize.ReasonPx1(3440, 1440) - 20.52f) < 0.01f && Math.Abs(CardTextSize.ReasonPx1(1280, 800) - 10.26f) < 0.01f,
                F(CardTextSize.ReasonPx1(3440, 1440)) + " / " + F(CardTextSize.ReasonPx1(1280, 800)));
        }

        // ---------------------------------------------------------------- G9: the reason line against the divider
        // the card's reason line in the view (y down): its middle hangs 66 x the ribbon's s + 33 + 35 x s card units under the card's
        // bottom (Badge.Reason), its glyphs half its font (20 x s) round that middle; the card is drawn at 0.77 of the view, and a hovered
        // card grows about its centre by HoverK. The Deck's selection view is modelled as the PC's 3840 x 2160 (its Training Yard view
        // logs 3840x2160 inside the 2400-unit canvas), so its band under the cards is the PC's.
        const float RuleTop = 1902f;
        static float GlyphLow(float s, float rs) { return CardBottom + CardTextSize.CardChain * (66f * rs + 33f + 35f * s + 20f * s); }
        static float Hovered(float y) { float mid = (CardTop + CardBottom) / 2f; return mid + HoverK * (y - mid); }

        static void Divider()
        {
            var settings = CardTextSize.Steps.Concat(new[] { CardTextSize.MinPick, CardTextSize.MaxPick }).ToArray();
            int n = 0; var bad = new List<string>(); float closest = float.MaxValue; string closestAt = "";
            foreach (var sc in Screens)
                foreach (var v in settings)
                {
                    var t = new Shot(sc[0], sc[1], v); n++;
                    float y = Hovered(GlyphLow(t.S, CardTextSize.RibbonScale(t.S)));
                    if (RuleTop - y < closest) { closest = RuleTop - y; closestAt = t.ToString(); }
                    if (y > RuleTop - 0.01f) bad.Add(t + ": the line's foot at " + F(y, "0"));
                }
            Check("G9", "the hovered card's reason line above the game's divider rule (y 1902; the glyphs half the font round the line's middle) at every screen from 1024 x 768 up and every step - the ribbon held at x1.3, a pick at most x1.85",
                bad.Count == 0, n + " screens x settings; the closest " + F(closest, "0.0") + " units above it (" + closestAt + ")" + (bad.Count > 0 ? " | " + string.Join(" | ", bad.Take(4)) : ""));
            float grown = Hovered(GlyphLow(CardTextSize.RoomCap, CardTextSize.RoomCap)), held = Hovered(GlyphLow(CardTextSize.RoomCap, CardTextSize.RibbonScale(CardTextSize.RoomCap)));
            Check("G9", "the ribbon held at x1.3 makes that room: at the room's x1.85 a ribbon grown with the line (as up to the review) puts the hovered line's foot at y " + F(grown, "0") + ", over the rule; held, at " + F(held, "0"),
                grown > RuleTop && held < RuleTop);
            // the divider's diamond (the 10-05 mark: R4.Box(1434, 1810, 188, 188), its top corner at x 1528, y 1810) lies inside the third
            // card's span of a four-card offer (x 1494 - 2134, its middle 1814): a long line there meets the diamond's upper edges at rest.
            // A watch for the captures, not a check - the PC's accepted look of 10-06 reaches it with a line of the full 56 characters.
            var watch = new List<string>();
            foreach (var sc in new[] { new[] { 3440f, 1440f, 0f }, new[] { 1920f, 1080f, 0f }, new[] { 1280f, 800f, 0f }, new[] { 1280f, 800f, 1.3f }, new[] { 3440f, 1440f, 1.5f } })
            {
                var t = new Shot(sc[0], sc[1], sc[2]);
                float foot = GlyphLow(t.S, CardTextSize.RibbonScale(t.S)), edge = 1528f + Math.Max(0f, foot - 1810f);
                float clearW = Math.Max(0f, (1814.33f - edge) / (CardTextSize.CardChain / 2f));          // the widest line (card units) that stays clear of it
                int chars = CardTextSize.LineChars(t.S), clearChars = (int)Math.Floor(clearW / (CardTextSize.EmPerChar * CardTextSize.ReasonUnits * t.S));
                watch.Add(t.Name + " " + CardTextSize.Label(t.Setting) + " " + Math.Min(clearChars, chars) + " of " + chars);
            }
            Console.WriteLine("    watch (no check): the characters the third card of a four-card offer shows at rest before its line meets the divider's diamond - " + string.Join(", ", watch));
        }

        // ---------------------------------------------------------------- G10: the line's room follows the size
        static string Plain(string s) { return (s ?? "").Replace("<b>", "").Replace("</b>", ""); }

        static void Room()
        {
            var at = new[] { 1f, 1.15f, 1.3f, 1.5f, 1.56f, 1.85f, 2f };
            var hd768 = new Shot(1366, 768, 0); var dk = new Shot(1280, 800, 0);
            Check("G10", "the line's room follows the size (the rect is 900 units at most): 56 characters at x1, 51 at x1.3, 43 at x1.56, 36 at the room's x1.85 - 0.14.0 gave 56 at every size; the less room a line has to shrink to the 15 px floor, the more it counts a dense line's glyphs (the Deck's 16 px: 42; 1366 x 768 at the floor: 45)",
                CardTextSize.LineChars(1f) == 56 && CardTextSize.LineChars(1.3f) == 51 && CardTextSize.LineChars(1.56f) == 43 && CardTextSize.LineChars(1.85f) == 36
                && CardTextSize.LineChars(dk.S, dk.Px1) == 42 && CardTextSize.LineChars(hd768.S, hd768.Px1) == 45,
                string.Join(", ", at.Select(x => "x" + F(x) + " " + CardTextSize.LineChars(x))) + "; the Deck " + CardTextSize.LineChars(dk.S, dk.Px1) + ", 1366x768 " + CardTextSize.LineChars(hd768.S, hd768.Px1));
            // the review's line at the Deck's Auto (s 1.56): sized for 56 characters it ran past the 900-unit rect; sized to the room it fits
            var deck = new Shot(1280, 800, 0);
            float rect = Math.Min(CardTextSize.ReasonW * deck.S, CardTextSize.ReasonMaxW), font = CardTextSize.ReasonUnits * deck.S;
            var abil = new CardWords { Kind = SayKind.Ability, Level = 1, Max = 4, Build = "Bombardier", Priority = 0, EvoExists = true, Reach = 1, Owned = 1, Clock = "4:20 left", HeadRank = 4, Head = Wording.Role("Bombardier", 0) };
            string wide = Synergy.ReasonPrefix(2, 4.0) + Wording.Card(abil, 2, "Medical Drone", "Handgun", Wording.RoomBeside(6), null);
            string sized = Synergy.ReasonPrefix(2, 4.0) + Wording.Card(abil, 2, "Medical Drone", "Handgun", Wording.RoomBeside(6, CardTextSize.LineChars(deck.S, deck.Px1)), null);
            float wW = Tmp(wide, font), wS = Tmp(sized, font);
            Check("G10", "the Deck on Auto (x1.56, a 900-unit rect): a level line sized for 56 characters runs past it; sized to the room it fits",
                wW > rect && wS <= rect, "'" + Plain(wide) + "' " + F(wW, "0") + " units; '" + Plain(sized) + "' " + F(wS, "0") + " in " + F(rect, "0"));

            // the sweep: ability and weapon lines as Ranker sizes them, for the top card, a 2ND and an AVOID, at every screen and step
            var builds = new[] { null, "Rifleman", "Shield Anchor", "Bombardier", "A Build Named Twenty1" };
            var words = new List<CardWords>();
            foreach (int level in new[] { 0, 1, 2, 3 })
                foreach (var b in builds)
                    foreach (int pri in new[] { 0, 1, 2, 4 })
                        foreach (int evo in new[] { 0, 1, 2 })
                            foreach (double reach in new[] { 1.0, 0.3 })
                                foreach (bool focus in new[] { false, true })
                                {
                                    var w = new CardWords { Kind = SayKind.Ability, Level = level, Max = 4, Build = b, Priority = pri, EvoExists = evo > 0, EvoOwned = evo == 2, Reach = reach, Focus = focus, Owned = 1, Clock = "4:20 left" };
                                    if (b != null) { w.HeadRank = pri <= 1 ? 4 : 1; w.Head = Wording.Role(b, pri); }
                                    words.Add(w);
                                }
            foreach (int level in new[] { 0, 1, 2, 3 })
                foreach (BuildStyle st in Enum.GetValues(typeof(BuildStyle)))
                    foreach (var b in builds)
                        foreach (double reach in new[] { 1.0, 0.3 })
                            words.Add(new CardWords { Kind = SayKind.Weapon, Level = level, Max = 4, Style = st, Build = b, Reach = reach, Clock = "18:19 left" });
            var cards = new[] { new KeyValuePair<int, double>(1, 6.0), new KeyValuePair<int, double>(2, 4.0), new KeyValuePair<int, double>(3, 0.5) };
            var seen = new HashSet<string>(); int sizes = 0, lines = 0, shrunk = 0; var cut = new List<string>(); float widest = 0f; string widestAt = "";
            var per = new List<string>(); int smallCut = 0, smallLines = 0;
            foreach (var sc in Screens)
                foreach (var v in CardTextSize.Steps.Concat(new[] { CardTextSize.MaxPick }))
                {
                    var t = new Shot(sc[0], sc[1], v);
                    float least = Math.Min(t.S, ScreenBand.MinPx / t.Px1) / t.S;            // Badge.Fit shrinks a line this far at most: the 15 px floor
                    if (!seen.Add(F(t.S, "0.000") + "/" + F(least, "0.000"))) continue;
                    bool small = sc[0] < 1280f;                                              // 1024 x 768 (4:3, the floor's x1.83): reported, not held to it
                    sizes++; int cutHere = 0, linesHere = 0;
                    int chars = CardTextSize.LineChars(t.S, t.Px1);
                    float rect2 = Math.Min(CardTextSize.ReasonW * t.S, CardTextSize.ReasonMaxW), font2 = CardTextSize.ReasonUnits * t.S;
                    foreach (var c in cards)
                    {
                        string prefix = Synergy.ReasonPrefix(c.Key, c.Value);
                        int room = Wording.RoomBeside(Synergy.ReasonPrefixWidth(c.Key, c.Value), chars);
                        foreach (var w in words)
                        {
                            string line = Wording.Card(w, c.Key, "Medical Drone", "Handgun", room, null);
                            if (string.IsNullOrEmpty(line)) continue;
                            lines++; linesHere++;
                            string drawn = prefix + Wording.Safe(line);
                            float width = Tmp(drawn, font2);
                            if (!small && width / rect2 > widest) { widest = width / rect2; widestAt = t.Name + " " + CardTextSize.Label(v) + ": '" + Plain(drawn) + "'"; }
                            if (width <= rect2 + 0.01f) continue;
                            if (width * least <= rect2 + 0.01f) { shrunk++; continue; }
                            cutHere++;
                            if (small) { smallCut++; continue; }
                            if (cut.Count < 6) cut.Add(t.Name + " " + CardTextSize.Label(v) + ": '" + Plain(drawn) + "' " + F(width, "0") + " in " + F(rect2, "0"));
                            else cut.Add("");
                        }
                    }
                    if (small) smallLines += linesHere;
                    if (cutHere > 0 && !small) per.Add(t.Name + " " + CardTextSize.Label(v) + " (x" + F(t.S) + ", " + chars + " characters) " + cutHere + " of " + linesHere);
                }
            Check("G10", "a sweep of the ability and weapon lines as Ranker sizes them (the top card, a 2ND, an AVOID) at every screen from 1280 x 720 up and every step: each fits the reason rect at its size or shrunk to the 15 px floor (Badge.Fit), none cut by the ellipsis (1024 x 768 reported: at the floor's x1.83 a line holds 34 characters)",
                cut.Count == 0, sizes + " sizes, " + lines + " lines, " + shrunk + " shrunk; the widest " + F(widest * 100f, "0") + "% of its rect (" + widestAt + "); 1024 x 768: " + smallCut + " of " + smallLines + " cut"
                + (cut.Count > 0 ? " | cut " + cut.Count + " - " + string.Join("; ", per) + ": " + string.Join(" || ", cut.Where(x => x.Length > 0)) : ""));
        }

        // ---------------------------------------------------------------- S: the sources
        static void Sources()
        {
            string badge = Src("Badge.cs"), hint = Src("RerollHint.cs"), why = Src("WhyUi.cs"), plugin = Src("Plugin.cs"), menu = Src("Menu.cs");
            Check("S", "the cards, the hint and the WHY band take their size from CardTextSize (no cap of their own left)",
                badge != null && badge.Contains("CardTextSize.Scale(setting, px1, canvasH)") && !badge.Contains("MaxScale")
                && hint != null && hint.Contains("CardTextSize.Follow(Font * screenH / h, h, factor)") && !hint.Contains("MaxScale")
                && why != null && why.Contains("CardTextSize.WhyPx(reasonPx, SizeFactor())") && !why.Contains("PreferPx = 22f"));
            Check("S", "[General] BadgeScale: 0 (Auto) or up to 1.6, its description names the menu row, the WHY band, the hint and the 15 px floor",
                plugin != null && plugin.Contains("Config.Bind(\"General\", \"BadgeScale\", 0f, new ConfigDescription(") && plugin.Contains("new AcceptableValueRange<float>(0f, 1.6f)")
                && plugin.Contains("\\\"Card text size\\\"") && plugin.Contains("the WHY band and the REROLL / SKIP hint") && plugin.Contains("under 15 px") && !plugin.Contains("up to 1.3).")
                && plugin.Contains("that share of the automatic size on this screen") && !plugin.Contains("under 1080 px tall"));
            Check("S", "the ribbon held at x1.3 with the reason line under it, a line too wide shrinking to the floor before the ellipsis, Ranker sizing the words to the room (the review of 10-06)",
                badge != null && badge.Contains("Ribbon(root, c.Button, CardTextSize.RibbonScale(s))") && badge.Contains("-(RibbonH * rs + 33f + ReasonH * s / 2)") && badge.Contains("Fit(reason, s);")
                && badge.Contains("ScreenBand.MinPx / _px1") && badge.Contains("CardTextSize.LineChars(CardTextSize.Scale(Plugin.BadgeScale.Value,"));
            Check("S", "the DISPLAY tab's Card text size row, bound to BadgeScale, with the preview's sample",
                menu != null && menu.Contains("\"di:cardtext\"") && menu.Contains("Plugin.BadgeScale.Value = CardStep(Plugin.BadgeScale.Value, d); LayCardSample(sample);") && menu.Contains("sample = BuildCardSample(window, ww)")
                && menu.Contains("CardTextSize.Step(setting, d, CardTextSize.ReasonPx1(sw, sh), CardTextSize.CanvasUnits(sw, sh))") && menu.Contains("(the same here, skipped)")
                && menu.Contains("CardTextSize.SameAsSmaller(CardTextSize.Steps[i], px1, canvasH)") && !menu.Contains("scales all three alike"));
            // 0.15.x (C-m1 of the 10-07 review): the inset above the rows stops at the tab's lead line (a row scrolled out above the area cut
            // the lead's descenders); the area's top, and so the rows' tops (448 + 124 n, 444 + 124 n), unchanged
            Check("S", "C15-05: the ADVICE and DISPLAY rows in the menu's scroller (the inset above them stopping at the lead: C-m1), the preview window as tall as the area, the walks' scroll check",
                menu != null && menu.Contains("Scroll(\"advice\", x, y, w, RowsRoom(y, rh, step), false, 40f, TopInset(lead, y))") && menu.Contains("Scroll(\"display\", x, top, w, room, false, 40f, TopInset(lead, top))")
                && menu.Contains("return bottom > 0f ? Mathf.Clamp(rowsTop - bottom, 0f, 40f) : 40f;") && menu.Contains("float origin = -s.View.anchoredPosition.y + s.Top;")
                && menu.Contains("content.anchoredPosition = across ? new Vector2(inset - p, -top) : new Vector2(inset, p - top);")
                && System.Text.RegularExpressions.Regex.Matches(menu, @"EndRows\(area, y - \(step - rh\)\);").Count == 2 && menu.Contains("wh = room - 96f")
                && menu.Contains("ScrollWalk(\"ADVICE\", \"menu4b_advice\")") && menu.Contains("ScrollWalk(\"DISPLAY\", \"menu5b_display\")")
                && menu.Contains("ScrollWalk(\"ADVICE\", \"pause1b_advice\")") && menu.Contains("ScrollWalk(\"DISPLAY\", \"pause3b_display\")") && menu.Contains("\" rows \" + rows.Count + \", visible \" + visible + \", scroll \""));
        }

        // ================================================================ 0.15.0 item 12: the WHY panel in the side wings, the band's end diamond
        // the card font's glyphs as TMP measures them: average widths per glyph class (in ems), x Calib = the live band lines of 10-06
        const float Calib = 1.135f;
        static float Glyph(char ch)
        {
            if (ch == ' ') return 0.22f;
            if (char.IsUpper(ch) || char.IsDigit(ch)) return 0.40f;
            switch (ch)
            {
                case '%': return 0.55f;
                case '.': case ',': case ':': case ';': case '\'': case '!': case '|': case 'i': case 'l': return 0.17f;
                case '-': case 'r': return 0.24f;
                case 'j': case 'f': case 't': return 0.22f;
                case '/': return 0.30f;
                case 'm': case 'w': return 0.50f;
            }
            return 0.36f;
        }

        /// <summary>A text's width as TMP measures it at <paramref name="font"/> units: tags left out, bold 8 % wider, trailing spaces left out.</summary>
        static float Tmp(string s, float font)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            var glyphs = new List<KeyValuePair<char, bool>>(); bool bold = false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '<')
                {
                    int e = s.IndexOf('>', i);
                    if (e > i) { string tag = s.Substring(i, e - i + 1); if (tag == "<b>") bold = true; else if (tag == "</b>") bold = false; i = e; continue; }
                }
                glyphs.Add(new KeyValuePair<char, bool>(s[i], bold));
            }
            int n = glyphs.Count;
            while (n > 0 && glyphs[n - 1].Key == ' ') n--;
            float w = 0f;
            for (int i = 0; i < n; i++) w += Glyph(glyphs[i].Key) * (glyphs[i].Value ? 1.08f : 1f);
            return w * Calib * font;
        }

        // WhyUi's chrome at a font (view units): the end diamond (36 x 1.2 at the card's 40), the pad inside it (22 at 30)
        static float TipU(float font) { return 36f * font / 40f * 1.2f; }
        static float PadU(float font) { return 22f * font / 30f; }
        static float ChromeU(float font) { return 2f * (TipU(font) / 2f + PadU(font)); }

        // one selection screen in view units (3840 x 2160, y down), the canvas round it; three cards as on the 10-05 mark (x 262 / 1207 /
        // 2152, 640 wide) or four as the 10-06 level-up (x 141 - 2811); the cards 577 - 1703 with their reason lines down to 1833, the
        // selected one grown about its centre until its line reaches 1857 (the mark's hovered card)
        const float CardTop = 577f, CardBottom = 1703f, RestLow = 1833f, HoverLow = 1857f;
        const float HoverK = (HoverLow - (CardTop + CardBottom) / 2f) / (RestLow - (CardTop + CardBottom) / 2f);
        sealed class Sel { public string Name; public float W, H, UnitPx; public R4 View, Canvas, Panel, Hint, Reroll; public R4[] Rest; public readonly List<R4> Obstacles = new List<R4>(); }

        static Sel Screen(float w, float h, float canvasW, int cards, bool rescue)
        {
            bool wide = w / h >= 16f / 9f - 1e-4f;
            float cw = canvasW > 0 ? canvasW : wide ? 2160f * w / h : 3840f, ch = wide ? 2160f : 3840f * h / w;
            var s = new Sel { Name = w + "x" + h + (rescue ? " rescue" : " level-up"), W = w, H = h, UnitPx = h / ch, View = new R4(0, 0, 3840, 2160) };
            s.Canvas = new R4(-(cw - 3840f) / 2f, -(ch - 2160f) / 2f, 3840f + (cw - 3840f) / 2f, 2160f + (ch - 2160f) / 2f);
            float x0 = cards == 4 ? 141f : 262f, pitch = cards == 4 ? (2670f - 640f) / 3f : 945f;
            s.Rest = Enumerable.Range(0, cards).Select(i => new R4(x0 + i * pitch, CardTop, x0 + i * pitch + 640f, RestLow)).ToArray();
            s.Panel = new R4(3003, 92, 3790, 2026);
            if (rescue)
            {
                s.Reroll = R4.Box(196, 1944, 578, 100);
                s.Hint = R4.Box(196, RestLow + (111f - 62f) / 2f, 741f, 62f);         // 19:18:56.839: 741 x 62 units over the Reroll button, band 111
                s.Obstacles.AddRange(new[] { s.Reroll, R4.Box(2293, 1944, 579, 100), R4.Box(280, 2017, 56, 59), s.Hint });
            }
            else
            {
                s.Reroll = R4.Box(196, 1977, 578, 101);
                s.Obstacles.AddRange(new[] { s.Reroll, R4.Box(892, 1977, 579, 101), R4.Box(1593, 1977, 577, 101), R4.Box(2293, 1977, 579, 101), R4.Box(280, 2050, 56, 59), R4.Box(1676, 2050, 56, 59) });
            }
            s.Obstacles.AddRange(new[] { s.Panel, R4.Box(1434, 1810, 188, 188), new R4(127, 1902, 2965, 1906) });
            return s;
        }

        /// <summary>The side panel's input for card <paramref name="selected"/> at a "Card text size" <paramref name="setting"/>; <paramref name="px"/>
        /// = the panel's text in pixels (the cards' reason size, 15 px at least). <paramref name="wings"/> false: the frame shown (WideMenus off).</summary>
        static WhySideIn SideOf(Sel s, int selected, float setting, bool wings, out float px, float livePx = 0f)
        {
            float px1 = CardTextSize.ReasonPx1(s.W, s.H);
            px = livePx > 0 ? livePx : ScreenBand.SidePx(px1 * CardTextSize.Scale(setting, px1, CardTextSize.CanvasUnits(s.W, s.H)));      // livePx: as WhyUi measured it (G11)
            float font = px / s.UnitPx;
            var cards = s.Rest.ToArray();
            var r = cards[selected]; float cx = (r.X0 + r.X1) / 2f, cy = (CardTop + CardBottom) / 2f;
            cards[selected] = new R4(cx + (r.X0 - cx) * HoverK, cy + (r.Y0 - cy) * HoverK, cx + (r.X1 - cx) * HoverK, cy + (r.Y1 - cy) * HoverK);
            return new WhySideIn { View = s.View, Canvas = wings ? s.Canvas : s.View, Cards = cards, AnchorX = cx, ReasonBottom = cards[selected].Y1, Obstacles = s.Obstacles.ToArray(), FontUnits = font, TipUnits = TipU(font), ChromeUnits = ChromeU(font) };
        }

        sealed class Panel { public WhySide Side; public List<SideLine> Head = new List<SideLine>(), Lines = new List<SideLine>(); public R4 Block, Outer; public float Px, Widest, Chrome, Slack; public int Shown; public string Text = ""; }

        // WhyUi.SidePanel's layout with the glyph model: the header (the lead; 0.16.0, C16-10: with <name> the card's name on the lead's line
        // or on rows of its own - ScreenBand.Header), Wrap at the panel's whole width in the lines left under it, the block beside the card
        // with its bottom on the reason line. <old>: 0.15.0's rule (four wrapped lines at most, every item a candidate) - G11 reproduces it
        static Panel Lay(WhySideIn x, float px, string lead, IList<string> items, bool old = false, string name = null)
        {
            var d = new Panel { Side = ScreenBand.Side(x), Px = px };
            if (d.Side.At == "none") return d;
            float font = x.FontUnits, cont = ScreenBand.ContEms * font;
            Func<string, float> m = t => Tmp(t, font);
            d.Chrome = ChromeU(font); d.Slack = ScreenBand.Slack(font);
            float room = d.Side.Width - d.Chrome - d.Slack;
            d.Head = ScreenBand.Header(lead, old ? null : name, m, room, cont);
            d.Lines = old ? ScreenBand.Wrap(items, m, room, cont, ScreenBand.SideBaseLines)
                : ScreenBand.Wrap(items, m, room, cont, d.Side.Lines - (d.Head.Count - 1), ScreenBand.SideMaxItems);
            d.Shown = d.Lines.Select(l => l.Item).Distinct().Count();
            d.Widest = Math.Max(ScreenBand.Widest(d.Head, 0f, cont), ScreenBand.Widest(d.Lines, 0f, cont));
            float width = ScreenBand.BlockWidth(d.Widest, d.Chrome, font, d.Side.Width), height = ScreenBand.BlockHeight(d.Lines.Count + d.Head.Count, font);
            float left = d.Side.Left ? d.Side.Room.X1 - width : d.Side.Room.X0, top = d.Side.Room.Y1 - height;
            d.Block = R4.Box(left, top, width, height);
            d.Outer = new R4(left - TipU(font) / 2f, top, left + width + TipU(font) / 2f, top + height);
            d.Text = (name == null ? "" : HeadText(d.Head) + " || ") + string.Join(" | ", d.Lines.Select(l => (l.Cont ? "~ " : "") + l.Text));
            return d;
        }

        static string HeadText(IList<SideLine> head) { return string.Join(" | ", head.Select(h => h.Item == -1 ? "[lead]" + (h.Text.Length > 0 ? "  " + h.Text : "") : (h.Cont ? "~ " : "") + h.Text)); }

        // the panel with its diamonds inside the canvas and in a wing (past the game's 16:9 frame), clear of every card and obstacle, its
        // bottom on the reason line, its text inside the diamonds and pads, four reasons at most, its top no higher than the cards' top
        static bool Clear(Panel d, WhySideIn x, out string why)
        {
            why = "clear";
            var o = d.Outer;
            if (d.Lines.Count == 0) { why = "no line"; return false; }
            if (o.X0 < x.Canvas.X0 - 0.01f || o.X1 > x.Canvas.X1 + 0.01f || o.Y0 < x.Canvas.Y0 || o.Y1 > x.Canvas.Y1) { why = "outside the canvas"; return false; }
            if (!(o.X1 <= x.View.X0 + 0.01f || o.X0 >= x.View.X1 - 0.01f)) { why = "inside the game's frame"; return false; }
            foreach (var c in x.Cards) if (o.Overlaps(c)) { why = "over a card " + c; return false; }
            foreach (var b in x.Obstacles) if (o.Overlaps(b)) { why = "over " + b; return false; }
            if (Math.Abs(d.Block.Y1 - x.ReasonBottom) > 0.01f) { why = "its bottom is not on the reason line"; return false; }
            if (d.Widest + d.Chrome + d.Slack > d.Block.W + 0.01f) { why = "its text runs into the diamond"; return false; }
            if (d.Shown > ScreenBand.SideMaxItems) { why = d.Shown + " reasons"; return false; }
            float cardsTop = x.Cards.Length > 0 ? x.Cards.Min(c => c.Y0) : x.Canvas.Y0;
            if (o.Y0 < cardsTop - 0.01f) { why = "above the cards' top (" + F(o.Y0, "0") + " over " + F(cardsTop, "0") + ")"; return false; }
            return true;
        }

        static string Say(Panel d) { return d.Side.At + ", the lead's line and " + d.Lines.Count + " at " + F(d.Px, "0.0") + " px, " + d.Shown + " shown, " + d.Block + " " + d.Block.Size + " [" + d.Text + "] | " + d.Side.Wings; }

        // the 10-06 offers with generic names (the repository is public): the level-up of 19:16:59.704 (its third reason's card renamed),
        // the third card of that offer at 19:17:00.289 (a 59-character "goes first"), the rescue screen of 19:18:56.842 (0.14.0 showed 1 of 3)
        static readonly string[] LevelUp = { "Fills an empty ability slot early", "Kinetic is 100% of your damage", "Just ahead of Electric Personality" };
        static readonly string[] Third = { "Medical Drone goes first: A core ability of the Medic build", "The Medic Healer build levels abilities first", "Kinetic is 100% of your damage" };
        static readonly string[] Rescue = { "S-tier recruit in the guides", "2 synergies with your squad, not unlocked yet", "Just ahead of Ranger" };
        static readonly string[] CloseCall = { "Either works - Medical Drone is a hair ahead", "Fills an empty ability slot early", "Kinetic is 100% of your damage" };

        // ---------------------------------------------------------------- G6: the panel's wing
        static void SideWings()
        {
            string why; float px;
            var pc = Screen(3440, 1440, 5161, 4, false);
            var x = SideOf(pc, 0, 0, true, out px); var d = Lay(x, px, "WHY", LevelUp);
            bool clear = Clear(d, x, out why);
            bool ok = d.Side.At == "side wing left" && d.Shown == 3 && clear && Math.Abs(px - 20.5f) < 0.05f && d.Block.W <= ScreenBand.SideWidth + 0.01f && d.Outer.X1 <= pc.View.X0 - ScreenBand.Gap + 0.01f;
            Check("G6", "3440 x 1440 (canvas 5161 x 2160: wings of 660 units, 19:16:22.774), the level-up of 19:16:59.704 with card 1 selected: its three reasons fully in the left wing at the cards' 20.5 px (0.14.0: one 15 px line over the buttons), at most 600 units wide, beside the card, its bottom on the reason line",
                ok, why + "; " + Say(d));
            var x4 = SideOf(pc, 3, 0, true, out px); var d4 = Lay(x4, px, "WHY", LevelUp);
            clear = Clear(d4, x4, out why);
            ok = d4.Side.At == "side wing right" && d4.Shown == 3 && clear && d4.Outer.X0 >= Math.Max(pc.View.X1, pc.Panel.X1) + ScreenBand.Gap - 0.01f;
            Check("G6", "the same screen, the right-most card selected: the right wing, past the team panel, inside the screen", ok, why + "; " + Say(d4));

            // the side by the selected card: left of the left-most, right of the right-most, the nearer wing between
            var three = Screen(3440, 1440, 5161, 3, false);
            var picks3 = Enumerable.Range(0, 3).Select(i => { float p; return ScreenBand.Side(SideOf(three, i, 0, true, out p)).At.Replace("side wing ", ""); }).ToList();
            var picks4 = Enumerable.Range(0, 4).Select(i => { float p; return ScreenBand.Side(SideOf(pc, i, 0, true, out p)).At.Replace("side wing ", ""); }).ToList();
            Check("G6", "the wing by the selected card: three cards (the 10-05 mark) left / left / right - left of card 1, right of card 3, the nearer for card 2; four cards (10-06) left / left / left / right (the third keeps the nearer wing - C-m8 of the 10-07 review: the header names the card since 0.16.0, the placement is the user's answer C16-10b)",
                string.Join(" ", picks3) == "left left right" && string.Join(" ", picks4) == "left left left right", "three: " + string.Join(" / ", picks3) + "; four: " + string.Join(" / ", picks4));

            // the rescue screen with the reroll hint: the hint keeps its line, the WHY no longer shares the band with it
            var sos = Screen(3440, 1440, 5161, 3, true);
            var xs = SideOf(sos, 0, 0, true, out px); var ds = Lay(xs, px, "WHY", Rescue);
            var hint = ScreenBand.Hint(RestLow, sos.Reroll, sos.Reroll.Y0, 2160, 62f);
            clear = Clear(ds, xs, out why);
            ok = ds.Side.At == "side wing left" && ds.Shown == 3 && clear && hint.Where == "over" && Math.Abs(hint.Band - 111f) < 0.01f && Math.Abs(hint.Top - sos.Hint.Y0) < 0.01f && !ds.Outer.Overlaps(sos.Hint);
            Check("G6", "the rescue screen of 19:18:56.842 with the reroll hint (741 x 62 units over the Reroll button, band 111): the hint where 0.12.2 drew it, the WHY's three reasons in the left wing (0.14.0: 1 of 3 beside the hint)",
                ok, why + "; hint " + hint.Where + " at " + F(hint.Top, "0") + ", band " + F(hint.Band, "0") + "; " + Say(ds));

            // a 59-character "goes first" wraps: its next line further in, the three still in four lines
            var x3 = SideOf(pc, 2, 0, true, out px); var d3 = Lay(x3, px, "WHY", Third);
            clear = Clear(d3, x3, out why);
            ok = d3.Shown == 3 && d3.Lines.Any(l => l.Cont && l.Item == 0) && clear;
            Check("G6", "the third card of that offer (19:17:00.289): its 59-character first reason wraps, the next line further in; all three reasons (0.15.x: the panel grows upward)", ok, why + "; " + Say(d3));

            // 2560 x 1080: wings of 640 units, the reasons at 16 px
            var uw = Screen(2560, 1080, 0, 4, false);
            var xu = SideOf(uw, 0, 0, true, out px); var du = Lay(xu, px, "WHY", LevelUp);
            clear = Clear(du, xu, out why);
            ok = du.Side.At == "side wing left" && du.Shown == 3 && clear && Math.Abs(px - 16f) < 0.05f && du.Block.W <= (uw.View.X0 - uw.Canvas.X0) - 2f * (ScreenBand.Gap + TipU(xu.FontUnits) / 2f) + 0.01f;
            Check("G6", "2560 x 1080 (canvas 5120 x 2160, wings of 640 units): the three reasons in the left wing at the cards' 16 px, the panel inside the wing", ok, why + "; " + Say(du));

            // a larger "Card text size": 150 % on the PC - the panel at the cards' 30.8 px takes the wing's room (19.5 ems would be 901 units)
            var xb = SideOf(pc, 0, 1.5f, true, out px); var db = Lay(xb, px, "WHY", LevelUp);
            clear = Clear(db, xb, out why);
            ok = db.Side.At == "side wing left" && Math.Abs(px - 30.8f) < 0.06f && Math.Abs(db.Side.Width - db.Side.Room.W) < 0.01f && db.Shown == 3 && clear;
            Check("G6", "150 % on the PC: the panel at the cards' 30.8 px, as wide as the wing allows, still clear, all three reasons (0.15.x: up to 0.15.0 two of three in four lines)", ok, why + "; " + Say(db));

            // the lead on its own line (the review of 10-06: as a hanging indent CLOSE CALL took five ems off every line - at 150 % one
            // reason of three, in lines of twelve characters)
            var xc = SideOf(pc, 0, 0, true, out px); var dc = Lay(xc, px, "CLOSE CALL", CloseCall);
            clear = Clear(dc, xc, out why);
            ok = dc.Side.At == "side wing left" && dc.Shown == 3 && clear && dc.Lines.Count(l => l.Item == 0) == 1;
            Check("G6", "CLOSE CALL at 3440 x 1440, the lead on its own line: the first reason whole on one line, three of three", ok, why + "; " + Say(dc));
            var sizes = new List<string>(); ok = true;
            foreach (float set in new[] { 1.3f, 1.5f })
                foreach (var lead in new[] { "WHY", "CLOSE CALL" })
                {
                    var xz = SideOf(pc, 0, set, true, out px); var dz = Lay(xz, px, lead, lead == "WHY" ? LevelUp : CloseCall);
                    bool cz = Clear(dz, xz, out why);
                    ok &= dz.Side.At == "side wing left" && cz && dz.Shown == 3 && dz.Lines.Count(l => l.Item == 0) <= 2;
                    sizes.Add(CardTextSize.Label(set) + " " + lead + ": " + dz.Shown + " of 3 in " + dz.Lines.Count + " (" + F(px, "0.#") + " px, " + why + ")");
                }
            var xcu = SideOf(uw, 0, 0, true, out px); var dcu = Lay(xcu, px, "CLOSE CALL", CloseCall);
            bool ccu = Clear(dcu, xcu, out why);
            ok &= dcu.Side.At == "side wing left" && ccu && dcu.Shown == 3;
            sizes.Add("2560x1080 CLOSE CALL: " + dcu.Shown + " of 3 in " + dcu.Lines.Count + " (" + why + ")");
            Check("G6", "130 % and 150 % on the PC with WHY and CLOSE CALL, and CLOSE CALL at 2560 x 1080: the panel clear, three reasons of three (0.15.x), the first in two lines at most", ok, string.Join("; ", sizes));

            // nowhere: 18:9 (wings of 240 units), WideMenus off at 21:9, 16:9, the Steam Deck (16:10: taller, no side wings)
            var nine = Screen(2160, 1080, 0, 4, false);
            var n18 = ScreenBand.Side(SideOf(nine, 0, 0, true, out px));
            var off = ScreenBand.Side(SideOf(pc, 0, 0, false, out px));
            Check("G6", "no panel - the band - at 18:9 (2160 x 1080: wings of 240 units, their text room under nine ems) and with WideMenus off at 21:9 (the frame shown)",
                n18.At == "none" && n18.Wings.Contains("too narrow") && off.At == "none", "18:9: " + n18.Wings + " | off: " + off.Wings);
            var hd = Screen(1920, 1080, 0, 4, false); var deck = Screen(1280, 800, 0, 4, false);
            var shd = ScreenBand.Side(SideOf(hd, 0, 0, true, out px)); float pxHd = px;
            var sdk = ScreenBand.Side(SideOf(deck, 0, 0, true, out px)); float pxDk = px;
            Check("G6", "16:9 (1920 x 1080, canvas 3840 x 2160) and the Steam Deck (1280 x 800, canvas 3840 x 2400): no wings, so no panel - the band as 0.14.0 chose it",
                shd.At == "none" && sdk.At == "none" && shd.Wings.StartsWith("no wings") && sdk.Wings.StartsWith("no wings"), Band(hd, pxHd) + "; " + Band(deck, pxDk));
            // the band's last resort (the review of 10-06): 1920 x 1080's four-button level-up has 55 units over the buttons, a 15 px line
            // needs 58.5 at the full pad - since 0.14.0 no WHY there at all; one line with the tight pad now, every other choice unchanged
            var hdBand = BandSpot(hd, pxHd); var dkBand = BandSpot(deck, pxDk);
            Check("G6", "1920 x 1080's four-button level-up: one 15 px WHY line with the tight pad (0.25 of the font) in the band over the buttons, where no band held one at the full pad; the Deck's band as before",
                hdBand.At == "over the buttons" && hdBand.Pad == ScreenBand.TightPad && hdBand.Lines == 1 && ScreenBand.BlockHeight(1, hdBand.Font, hdBand.Pad) <= hdBand.Seg.H + 0.01f && dkBand.Pad == ScreenBand.Pad,
                Band(hd, pxHd) + " | " + Band(deck, pxDk));
        }

        // the band manager's choice on a screen (one line wanted, card 1 hovered), as WhyUi asks for it when there is no panel
        static string Band(Sel s, float reasonPx)
        {
            var spot = BandSpot(s, reasonPx);
            return s.Name.Replace(" level-up", "") + " band: " + (spot.At == "none" ? "nowhere (no band holds a 15 px line: " + spot.Bands + ")" : spot.At + ", " + spot.Lines + " line at " + F(spot.Font * s.UnitPx, "0.#") + " px" + (spot.Pad < ScreenBand.Pad ? " with the tight pad" : ""));
        }

        static WhySpot BandSpot(Sel s, float reasonPx)
        {
            var rule = new R4(127, 1902, 2965, 1906);
            var buttons = s.Obstacles.Where(o => o.Y0 >= 1940f && !o.Equals(s.Hint)).ToList();
            var obstacles = s.Obstacles.Where(o => !buttons.Contains(o) && !o.Equals(rule)).ToList();
            float x0 = Math.Min(s.Rest.Min(c => c.X0), buttons.Min(b => b.X0)), x1 = Math.Max(s.Rest.Max(c => c.X1), buttons.Max(b => b.X1));
            var spot = ScreenBand.Why(new WhyBandIn
            {
                View = s.View, Bottom = Math.Max(s.View.Y1, s.Canvas.Y1), CardsBottom = HoverLow, SpanX0 = x0, SpanX1 = x1, Buttons = buttons.ToArray(), Obstacles = obstacles.ToArray(), Rules = new[] { rule },
                AnchorX = (s.Rest[0].X0 + s.Rest[0].X1) / 2f, FontUnits = CardTextSize.WhyPx(reasonPx, 1f) / s.UnitPx, MinFontUnits = ScreenBand.MinPx / s.UnitPx, Want = 1,
            });
            return spot;
        }

        // ---------------------------------------------------------------- G7: the panel's lines
        static void SideWrap()
        {
            Func<string, float> w = t => t.Length * 10f;                       // ten units a character
            var five = ScreenBand.Wrap(new[] { "aaaa", "bbbb", "cccc", "dddd", "eeee" }, w, 100f, 10f, 4);
            Check("G7", "one reason a line, in order, four lines at most: the fifth of five short reasons left out",
                five.Count == 4 && five.Select(l => l.Item).SequenceEqual(new[] { 0, 1, 2, 3 }) && five.All(l => !l.Cont), string.Join(" | ", five.Select(l => l.Item + ":" + l.Text)));
            var four = ScreenBand.Wrap(new[] { "aaaa", "bbbb bbbb bbbb", "cccc", "dddd", "eeee" }, w, 100f, 10f, 30, ScreenBand.SideMaxItems);
            Check("G7", "0.15.x (C-M1): four REASONS in the lines the room holds - a wrapped one takes another line, not a reason's place; the fifth left out",
                four.Select(l => l.Item).Distinct().SequenceEqual(new[] { 0, 1, 2, 3 }) && four.Count == 5 && four.Count(l => l.Cont) == 1, string.Join(" | ", four.Select(l => l.Item + ":" + (l.Cont ? "~" : "") + l.Text)));
            var wrap = ScreenBand.Wrap(new[] { "one two three four five", "six" }, w, 100f, 20f, 4);
            Check("G7", "a reason wider than its line wraps at its spaces, its next lines narrower by the hanging indent (100 units, then 80)",
                wrap.Count == 4 && wrap[0].Text == "one two" && wrap[1].Cont && wrap[1].Text == "three" && wrap[2].Cont && wrap[2].Text == "four" && wrap.Where(l => l.Cont).All(l => l.Width <= 80f) && wrap[3].Item == 0 && wrap[3].Text == "five",
                string.Join(" | ", wrap.Select(l => (l.Cont ? "~" : "") + l.Text + " " + l.Width)));
            var even = ScreenBand.Wrap(new[] { "aa bb cc dd ee ff gg hh" }, w, 200f, 0f, 4);
            Check("G7", "a wrapped reason is balanced: the shortest right end for as few lines - 'aa bb cc dd' / 'ee ff gg hh', not seven words and a lone 'hh'",
                even.Count == 2 && even[0].Text == "aa bb cc dd" && even[1].Text == "ee ff gg hh", string.Join(" | ", even.Select(l => l.Text)));
            var tail = ScreenBand.Wrap(new[] { "aaaa", "bbbbbbb bbbbbbb bbbbbbb bbbbbbb", "cc" }, w, 80f, 10f, 4);
            var wide = ScreenBand.Wrap(new[] { "aaaa", "an extraordinarilylongword", "cc" }, w, 100f, 10f, 4);
            Check("G7", "a reason that does not fit the lines left - or holds a word wider than a line - is left out with every reason after it (the verdict's order)",
                tail.Select(l => l.Item).Distinct().SequenceEqual(new[] { 0 }) && wide.Select(l => l.Item).Distinct().SequenceEqual(new[] { 0 }),
                "tail: " + string.Join(" | ", tail.Select(l => l.Text)) + "; wide: " + string.Join(" | ", wide.Select(l => l.Text)));
            var none = ScreenBand.Wrap(new[] { "abcdefghijklmnop" }, w, 100f, 10f, 4);
            Check("G7", "nothing fits: no line (WhyUi then lays the band out), and empty input gives none",
                none.Count == 0 && ScreenBand.Wrap(new string[0], w, 100f, 10f, 4).Count == 0 && ScreenBand.Wrap(new[] { "aa" }, w, 0f, 10f, 4).Count == 0);
        }

        // ---------------------------------------------------------------- G8: the band's end diamond
        static void Caps()
        {
            // the capture of 10-06 at 3440 x 1440: four reasons on the band over the buttons (1285 units, 19:16:59.704), 15 px = 22.5 units
            string[] items = { "The Medic Healer build levels weapons first", "Kinetic is 2 tags from its 10-tag effect", "Kinetic is 80% of your damage", "Ahead of Electric Personality" };
            const string sep = "  /  ";
            float font = 15f / (1440f / 2160f), tip = TipU(font), pad = PadU(font), chrome = ChromeU(font), seg = 1285f;
            float leadW = Tmp("<b>WHY</b>", font) + font * 0.6f;
            // 0.14.0: the pieces summed, the separator measured alone (TMP leaves its two trailing spaces out)
            float oldW = leadW + items.Sum(i => Tmp(i, font)) + 3f * Tmp(sep, font), oldBlock = Math.Min(seg, oldW + chrome + 4f);
            float drawn = leadW + Tmp(string.Join(sep, items), font);           // the line as TMP draws it
            float oldEnd = tip / 2f + pad + drawn, capIn = oldBlock - tip / 2f;
            Check("G8", "0.14.0's band reproduced: the four-reason line of the 10-06 capture at 15 px ran past the right diamond's inner edge (the last glyph under the diamond)",
                oldEnd > capIn, F(oldEnd - capIn, "0.0") + " units past; summed " + F(oldW, "0") + " units, drawn " + F(drawn, "0") + "; block " + F(oldBlock, "0") + ", diamond from " + F(capIn, "0"));
            // 0.15.0: the separator measured between two glyphs for the packing, every line measured as drawn - on that 1285-unit stretch the
            // four reasons as drawn need 1286 units with the diamonds and the pads, so the fourth goes to keep the diamond clear; on a stretch
            // of 1400 all four stay
            var says = new List<string>(); bool fit = true;
            foreach (float seg2 in new[] { seg, 1400f })
            {
                float sepW = Tmp("x" + sep + "x", font) - Tmp("xx", font), room = seg2 - chrome - ScreenBand.Slack(font);
                var lines = WhyText.Pack(items.Select(i => Tmp(i, font)).ToList(), 0f, sepW, room - leadW, 1);
                float widest = ScreenBand.FitLines(lines, items, sep, t => Tmp(t, font), leadW, room), block = ScreenBand.BlockWidth(widest, chrome, font, seg2);
                float end = tip / 2f + pad + widest, inner = block - tip / 2f;
                int kept = lines.Count == 1 ? lines[0].Count : 0;
                fit &= lines.Count == 1 && end <= inner - pad + 0.01f && block <= seg2 + 0.01f && kept == (seg2 > seg ? 4 : 3);
                says.Add(F(seg2, "0") + " units: " + kept + " of 4 kept, end " + F(end, "0") + ", diamond from " + F(inner, "0") + ", block " + F(block, "0"));
            }
            Check("G8", "0.15.0: the same line measured as drawn - the last glyph a pad (" + F(pad, "0.#") + " units) and more clear of the right diamond, the reasons that fit kept in order",
                fit, string.Join("; ", says));
            // a line the packing let through that is wider as drawn loses its last reason rather than running under the diamond
            var tight = new List<List<int>> { new List<int> { 0, 1, 2, 3 } };
            float tw = ScreenBand.FitLines(tight, items, sep, t => Tmp(t, font), leadW, drawn - 1f);
            Check("G8", "a line wider as drawn than its room loses reasons from its end (4 -> 3) instead of running under the diamond",
                tight.Count == 1 && tight[0].Count == 3 && tw <= drawn - 1f, "room " + F(drawn - 1f, "0") + ", kept " + tight[0].Count + ", end " + F(tw, "0"));
            var gone = new List<List<int>> { new List<int> { 0 } };
            ScreenBand.FitLines(gone, items, sep, t => Tmp(t, font), leadW, leadW + 10f);
            Check("G8", "a line whose one reason cannot fit goes", gone.Count == 0);
        }

        // ---------------------------------------------------------------- G11: the live geometry, four reasons, the side panel's words (0.15.x)
        // the live WHYs of the 10-06/07 series at 3440 x 1440 (left wing, 21.6 px - the hovered card's reason line at its hover size -,
        // 609 x 225 units, 16.1 em) with generic names (the repository is public), each as long as the live one: a 68-character "goes
        // first" of a weapons-first build with two more reasons, 2 of 3 shown (B_C_Pyro 00:03 / 00:12 / 00:13; the third not logged - the
        // bench's); three of four (B_C_Tank 00:02, B_C_Engineer 00:03 - its fourth the bench's); the five-reason WHY, 4 of 5 (B_C_Tank #1)
        const float LivePx = 21.6f;
        static readonly string[] LivePyro = { "Whirlpin goes first: Level 2 of 4 - weapons first, the build's style", "A core ability of the Bench Storm Whirlpool build", "Fire is 33% of your damage" };
        static readonly string[] LiveTank = { "Spinning Blade goes first: Level 2 of 4 - weapons first, the build's style", "The Bench Lotus build's main ability", "Your top ability - keep feeding it", "Kinetic is 44% of your damage" };
        static readonly string[] LiveEngineer = { "Bench Turrets goes first: Level 2 of 4 - evolution unlocks at level 4", "Abilities first, the build's own style", "Electric is 3 tags from its 10-tag effect", "Kinetic is 2 tags from its 10-tag effect" };
        static readonly string[] LiveFive = { "Evolution locked in the Skill Tree", "The Bench Storm Whirlpool build's main ability", "Your top ability - keep feeding it", "Fire is 33% of your damage", "Just ahead of Medical Drone" };

        static void SideLive()
        {
            string why; float px;
            // the live screen: the canvas 5160 x 2160 (3440 / 1440 x 2160: wings of 660 units), card 1 selected
            var pc = Screen(3440, 1440, 5160, 4, false);
            var x = SideOf(pc, 0, 0, true, out px, LivePx);
            var probe = ScreenBand.Side(x);
            Check("G11", "the live geometry (the series' [why] lines: 21.6 px, wings of 660 units, the left one 609 x 225 with 16.1 em of text room)",
                probe.At == "side wing left" && probe.Wings.Contains("left 609x225 (text 16.1 em)"), probe.Wings);
            Func<string[], int> live = a => a == LivePyro ? 2 : a == LiveFive ? 4 : 3;
            var cases = new[] { Tuple.Create("68-character goes-first, 3 reasons", LivePyro), Tuple.Create("74-character goes-first, 4 reasons", LiveTank), Tuple.Create("69-character goes-first, 4 reasons", LiveEngineer), Tuple.Create("5 reasons", LiveFive) };
            var olds = new List<string>(); bool oldOk = true;
            var news = new List<string>(); bool newOk = true;
            foreach (var cs in cases)
            {
                var o = Lay(x, px, "WHY", cs.Item2, true);
                oldOk &= o.Shown == live(cs.Item2);
                olds.Add(cs.Item1 + ": " + o.Shown + " of " + cs.Item2.Length + " in " + o.Lines.Count + " lines");
                var d = Lay(x, px, "WHY", cs.Item2);
                bool clear = Clear(d, x, out why);
                newOk &= clear && d.Shown == Math.Min(ScreenBand.SideMaxItems, cs.Item2.Length) && d.Lines.Count <= d.Side.Lines;
                news.Add(cs.Item1 + ": " + d.Shown + " of " + cs.Item2.Length + " in " + d.Lines.Count + " lines (room " + d.Side.Lines + "), " + F(d.Block.H, "0") + " units high, " + why);
            }
            Check("G11", "0.15.0's rule reproduced on the live WHYs: four wrapped lines at most - the 68-character goes-first 2 of 3, three of four, 4 of 5 (as the series logged them)", oldOk, string.Join("; ", olds));
            Check("G11", "0.15.x (C-M1): four REASONS, a wrapped one growing the panel upward (up to the cards' top) - every reason shown up to four, the fifth left out, the panel clear", newOk, string.Join("; ", news));
            Console.WriteLine("    G11 the 68-character case now: " + Say(Lay(x, px, "WHY", LivePyro)));

            // the side panel's words: the "vs #1" sentence without the first card's own line (it stands under that card)
            const string B = "Bench Storm Whirlpool", FirstLine = "Level 2 of 4 - weapons first, the build's style";
            var molotov = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Build = B, Priority = 1, HeadRank = 4, Head = Wording.Role(B, 1), Owned = 4, Reach = 1, Clock = "9:41 left", ShareType = "Fire", SharePct = 33 };
            Func<string, string, WhyBlock> pyro = (firstShown, mine) => WhyText.Block(new WhyIn { Name = "Molotov Cocktail", Rank = 3, Score = 4.66, Shown = mine ?? "New to the squad", Say = molotov,
                FirstName = "Whirlpin", FirstScore = 5.20, FirstShown = firstShown, SecondName = "Bench Drone", SecondScore = 4.90 });
            var b = pyro(FirstLine, null);
            var bandD = Lay(x, px, b.Lead, b.Items); var sideD = Lay(x, px, b.Lead, b.SideItems);
            bool okWords = b.Versus == "Whirlpin goes first: " + FirstLine && b.Versus.Length == 68 && b.SideVersus == "Whirlpin goes first: weapons first" && b.SideItems.Count == b.Items.Count
                && b.SideItems[0] == b.SideVersus && b.SideItems.Skip(1).SequenceEqual(b.Reasons) && sideD.Shown == b.SideItems.Count && sideD.Lines.Count < bandD.Lines.Count;
            Check("G11", "the 68-character case through WhyText: the band keeps 'Whirlpin goes first: <its line>', the side panel says 'Whirlpin goes first: weapons first' (the line stands under that card), its reasons the same and in order, all shown in fewer lines",
                okWords, "band '" + b.Versus + "' (" + bandD.Lines.Count + " lines); side [" + string.Join(" | ", b.SideItems) + "] (" + sideD.Lines.Count + " lines, " + sideD.Shown + " of " + b.SideItems.Count + ")");
            var plain = pyro("Level 2 of 4 - the build's main ability", null);
            // the first card's line is this card's too, so the edge is one of the first card's reasons - not on screen: said whole
            string same = Wording.Card(molotov, 3, "Whirlpin", "Bench Drone");
            var off = WhyText.Block(new WhyIn { Name = "Molotov Cocktail", Rank = 3, Score = 4.66, Shown = same, Say = molotov, FirstName = "Whirlpin", FirstScore = 5.20, FirstShown = same,
                FirstSay = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Owned = 4, Reach = 1, ShareType = "Kinetic", SharePct = 44 }, SecondName = "Bench Drone", SecondScore = 4.90 });
            Check("G11", "a first card's line naming no style: the bare 'X goes first'; a sentence that does not carry the first card's line (its edge one of the first card's reasons, not on screen) stays as the band says it",
                plain.SideVersus == "Whirlpin goes first" && plain.Versus == "Whirlpin goes first: Level 2 of 4 - the build's main ability" && off.Versus == "Whirlpin goes first: Kinetic is 44% of your damage" && off.SideVersus == off.Versus,
                "'" + plain.SideVersus + "' | band '" + off.Versus + "', side '" + off.SideVersus + "'");
            Check("G11", "Brief: the style a line names in two words, else none",
                WhyText.Brief(FirstLine) == "weapons first" && WhyText.Brief("Level 3 of 4 - abilities first, the build's own style") == "abilities first" && WhyText.Brief("Level 2 of 4 - the Rifleman build levels weapons first") == "weapons first"
                && WhyText.Brief("Level 2 of 4 - evolution unlocks at level 4") == null && WhyText.Brief("A core ability of the Medic build") == null && WhyText.Brief(null) == null);

            // the rail: a sweep of first cards (weapons in every style, lent or not, abilities by their place) and selected cards at 2ND and
            // 3RD - every side item within WhyText.Budget, the "vs #1" never the first card's line, two lines at most at the live geometry,
            // every reason shown (up to four)
            var firsts = new List<KeyValuePair<string, CardWords>>();
            foreach (var name in new[] { "Taser", "Medical Drone", "Spinning Blade: Fire and Ice" })
            {
                foreach (int lvl in new[] { 0, 1, 2 })
                    foreach (BuildStyle st in Enum.GetValues(typeof(BuildStyle)))
                        foreach (var bd in new[] { null, "Rifleman", B, "A Build Named Twenty1" })
                            foreach (bool lent in new[] { false, true })
                                firsts.Add(new KeyValuePair<string, CardWords>(name, new CardWords { Kind = SayKind.Weapon, Level = lvl, Max = 4, Style = st, Build = bd, LentStyle = lent && bd != null, Reach = 1, Clock = "9:41 left" }));
                foreach (int lvl in new[] { 0, 1, 2 })
                    foreach (var bd in new[] { "Rifleman", B, "A Build Named Twenty1" })
                        foreach (int pri in new[] { 0, 1, 2 })
                            firsts.Add(new KeyValuePair<string, CardWords>(name, new CardWords { Kind = SayKind.Ability, Level = lvl, Max = 4, Build = bd, Priority = pri, HeadRank = 4, Head = Wording.Role(bd, pri), EvoExists = true, EvoOwned = lvl > 0, Reach = 1, Owned = 2, Clock = "9:41 left" }));
            }
            var picks = new List<CardWords>
            {
                molotov,
                new CardWords { Kind = SayKind.Ability, Level = 2, Max = 4, Build = B, Priority = 2, HeadRank = 4, Head = Wording.Role(B, 2), EvoExists = true, Reach = 1, Owned = 3, Focus = true, Clock = "9:41 left", ShareType = "Kinetic", SharePct = 44, ShortType = "Electric", Short = 3 },
                new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Build = B, LentStyle = true, Reach = 1, Clock = "9:41 left", ShareType = "Kinetic", SharePct = 44 },
                new CardWords { Kind = SayKind.Weapon, Level = 2, Max = 4, Style = BuildStyle.Weapon, Build = "Rifleman", Reach = 0.3, Clock = "1:10 left" },
            };
            int blocks = 0, items = 0, shortened = 0, maxLines = 0, longest = 0; string longestAt = ""; var bad = new List<string>();
            float room = probe.Width - ChromeU(x.FontUnits) - ScreenBand.Slack(x.FontUnits), cont = ScreenBand.ContEms * x.FontUnits;
            foreach (var f in firsts)
            {
                string fs = Wording.Card(f.Value, 1, f.Key, "Experiment 21") ?? "";
                foreach (var w in picks)
                    foreach (int rank in new[] { 2, 3 })
                    {
                        var k = WhyText.Block(new WhyIn { Name = "Molotov Cocktail", Rank = rank, Score = 4.10, Shown = Wording.Card(w, rank, f.Key, "Experiment 21"), Say = w, FirstName = f.Key, FirstScore = 5.0, FirstShown = fs, FirstSay = f.Value, SecondName = "Experiment 21", SecondScore = 4.5 });
                        blocks++;
                        if (k.Versus != null && k.SideVersus != k.Versus) shortened++;
                        string d = WhyText.Decisive(fs), brief = WhyText.Brief(fs);
                        if (k.SideVersus != null && fs.Length > 0 && (k.SideVersus.IndexOf(fs, StringComparison.OrdinalIgnoreCase) >= 0 || (d.Length > 0 && d != brief && k.SideVersus.IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0)))
                            bad.Add("repeats the first card's line: '" + k.SideVersus + "'");
                        foreach (var it in k.SideItems)
                        {
                            items++;
                            if (it.Length > longest) { longest = it.Length; longestAt = it; }
                            if (!WhyText.Fits(it, WhyText.Budget)) bad.Add("over " + WhyText.Budget + " or off the rails: '" + it + "'");
                            int n = ScreenBand.Wrap(new[] { it }, t => Tmp(t, x.FontUnits), room, cont, 99).Count;
                            maxLines = Math.Max(maxLines, n);
                            if (n == 0 || n > 2) bad.Add(n + " lines at the live geometry: '" + it + "'");
                        }
                        var lay = Lay(x, px, k.Lead, k.SideItems);
                        bool layClear = Clear(lay, x, out why);
                        if (lay.Shown != Math.Min(ScreenBand.SideMaxItems, k.SideItems.Count) || !layClear) bad.Add(lay.Shown + " of " + k.SideItems.Count + " shown (" + why + "): " + string.Join(" | ", k.SideItems));
                    }
            }
            Check("G11", "the rail on the side panel's items (" + blocks + " WHYs, " + items + " items, " + shortened + " 'vs #1' sentences shortened): each within " + WhyText.Budget + " characters and the rails, the 'vs #1' never the first card's line, two lines at most at the live 16.1 em, every reason shown up to four",
                bad.Count == 0, "the longest " + longest + " ('" + longestAt + "'), at most " + maxLines + " lines" + (bad.Count > 0 ? " | " + bad.Count + " bad: " + string.Join(" || ", bad.Distinct().Take(5)) : ""));

            // C-m8 (the placement is the user's answer, C16-10b; the header names the card since 0.16.0): the third of four cards - about 1000 px from the left wing's panel - keeps the left
            // wing, as the user's Q4 put it (beside the selected card, no distance rule); the right-most past the team panel, about 700 px
            // from it; at 2560 x 1080 the same (view units). The distances are reported for the user's call.
            var far = new List<string>(); var at = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var xi = SideOf(pc, i, 0, true, out px, LivePx); var si = ScreenBand.Side(xi);
                at.Add(si.At == "none" ? "band" : si.At.Replace("side wing ", ""));
                var c = xi.Cards[i]; float gapL = c.X0 - (pc.View.X0 - ScreenBand.Gap - TipU(xi.FontUnits) / 2f), gapR = (pc.View.X1 + ScreenBand.Gap + TipU(xi.FontUnits) / 2f) - c.X1;
                far.Add("card " + (i + 1) + " " + F(Math.Min(gapL, gapR) * pc.UnitPx, "0") + " px");
            }
            var uw = Screen(2560, 1080, 0, 4, false);
            var atUw = Enumerable.Range(0, 4).Select(i => { float p; var si = ScreenBand.Side(SideOf(uw, i, 0, true, out p)); return si.At == "none" ? "band" : si.At.Replace("side wing ", ""); }).ToList();
            var s3 = ScreenBand.Side(SideOf(pc, 2, 0, true, out px, LivePx));
            Check("G11", "C-m8 (the placement is the user's answer C16-10b): no distance rule - every card keeps a wing at the live geometry, four cards left / left / left / right at 3440 x 1440 and 2560 x 1080 (the third about 1000 px from the left wing's panel)",
                string.Join(" ", at) == "left left left right" && string.Join(" ", atUw) == "left left left right" && s3.At == "side wing left",
                string.Join(" / ", at) + " (" + string.Join(", ", far) + "); 2560x1080 " + string.Join(" / ", atUw) + "; " + s3.Wings);

            // 0.16.0 (C16-10): which card of the row the panel speaks for - the [why] line's ", card P of N" - on the four-card level-up (the
            // live geometry) and on the three-card one (the 10-05 mark); the wings themselves unchanged
            var posOf = new List<string>(); bool posOk = true;
            for (int i = 0; i < 4; i++)
            {
                var si = ScreenBand.Side(SideOf(pc, i, 0, true, out px, LivePx));
                posOk &= si.Pos == i + 1 && si.Of == 4; posOf.Add(si.Pos + " of " + si.Of);
            }
            var three = Screen(3440, 1440, 5161, 3, false);
            for (int i = 0; i < 3; i++)
            {
                var si = ScreenBand.Side(SideOf(three, i, 0, true, out px));
                posOk &= si.Pos == i + 1 && si.Of == 3; posOf.Add(si.Pos + " of " + si.Of);
            }
            var none = ScreenBand.Side(new WhySideIn { FontUnits = 30f });
            Check("G11", "C16-10: WhySide.Pos / Of - card 1 .. 4 of 4 on the four-card level-up, 1 .. 3 of 3 on the three-card one (the card whose centre is nearest the selected one's); 0 of 0 when nothing was measured",
                posOk && none.Pos == 0 && none.Of == 0, "four: " + string.Join(", ", posOf.Take(4)) + "; three: " + string.Join(", ", posOf.Skip(4)));
        }

        // ---------------------------------------------------------------- G12: the side panel's header names its card (0.16.0, C16-10)
        static readonly string[] HeadNames = { "Grenade Trail", "Medical Drone", "Remote Control Car: Oil Spill", "Helicopter Strike: Chemtrails", "Automatic Turret: Provocation", "Ice Turret: Bermuda Triangle" };
        const string Name38 = "Bench Twinblade Tempest: Rising Embers";                                                  // as long as the longest name another mod lends (38)
        static readonly string Name140 = ("Bench Long Name " + string.Concat(Enumerable.Repeat("Abcdefgh ", 20))).Substring(0, 140).TrimEnd();      // ordinary words, 140 characters
        static readonly string Word60 = ("Bench" + string.Concat(Enumerable.Repeat("longword", 8))).Substring(0, 60);            // one word of 60 characters

        // the screens of G12: (label, screen, card count, setting, the live px or 0)
        static List<Tuple<string, Func<int, Sel>, float, float>> HeadScreens()
        {
            return new List<Tuple<string, Func<int, Sel>, float, float>>
            {
                Tuple.Create<string, Func<int, Sel>, float, float>("3440x1440 Auto", n => Screen(3440, 1440, 5161, n, false), 0f, 0f),
                Tuple.Create<string, Func<int, Sel>, float, float>("3440x1440 21.6 px", n => Screen(3440, 1440, 5160, n, false), 0f, LivePx),
                Tuple.Create<string, Func<int, Sel>, float, float>("3440x1440 130%", n => Screen(3440, 1440, 5161, n, false), 1.3f, 0f),
                Tuple.Create<string, Func<int, Sel>, float, float>("3440x1440 150%", n => Screen(3440, 1440, 5161, n, false), 1.5f, 0f),
                Tuple.Create<string, Func<int, Sel>, float, float>("2560x1080 Auto", n => Screen(2560, 1080, 0, n, false), 0f, 0f),
                Tuple.Create<string, Func<int, Sel>, float, float>("5120x1440 Auto", n => Screen(5120, 1440, 0, n, false), 0f, 0f),
            };
        }

        static void SideHeader()
        {
            // a. the header's forms, per name (no pinned counts): one line exactly when the lead and the name fit the room together, measured
            // whole; else the lead alone and the name's own rows - two at most for a real name (38 characters or less, no "..."), two ending in
            // "..." for a name needing more, none for a word wider than its row; every row within its room
            var names = HeadNames.Concat(new[] { Name38, Name140 }).ToList();
            var watch = new List<string>(); var bad = new List<string>(); int forms = 0, oneLines = 0, ownRows = 0, cut = 0, alone = 0;
            int widest140 = int.MaxValue, most140 = 0;
            foreach (var sc in HeadScreens())
            {
                float px; var x = SideOf(sc.Item2(4), 0, sc.Item3, true, out px, sc.Item4);
                var side = ScreenBand.Side(x);
                if (side.At == "none") { bad.Add(sc.Item1 + ": no panel"); continue; }
                float font = x.FontUnits, cont = ScreenBand.ContEms * font, room = side.Width - ChromeU(font) - ScreenBand.Slack(font);
                Func<string, float> m = t => Tmp(t, font);
                var counts = new List<string>();
                foreach (var lead in new[] { "WHY", "CLOSE CALL" })
                {
                    int one = 0;
                    foreach (var name in names.Concat(new[] { Word60 }))
                    {
                        forms++;
                        var head = ScreenBand.Header(lead, name, m, room, cont);
                        string at = sc.Item1 + " " + lead + " '" + (name.Length > 40 ? name.Substring(0, 24) + "..(" + name.Length + ")" : name) + "'";
                        bool fits = m("<b>" + lead + "</b>  " + name) <= room;
                        var rows = head.Skip(1).ToList();
                        // every row within its room: the lead's line the room, the name's first row the room, a carried-on row the room less the indent
                        if (head.Count == 0 || head[0].Item != -1 || head[0].Width > room + 0.01f) bad.Add(at + ": the lead's line " + (head.Count == 0 ? "missing" : F(head[0].Width, "0") + " in " + F(room, "0")));
                        foreach (var r in rows) if (r.Item != -2 || r.Width > (r.Cont ? room - cont : room) + 0.01f || Math.Abs(r.Width - m(r.Text)) > 0.01f || r.Cont != (r != rows[0])) bad.Add(at + ": row '" + r.Text + "' " + F(r.Width, "0") + " in " + F(r.Cont ? room - cont : room, "0"));
                        if (name == Word60)
                        {
                            if (head.Count != 1 || head[0].Text.Length != 0) bad.Add(at + ": a 60-character word gives " + head.Count + " lines, not the lead alone");
                            else alone++;
                            continue;
                        }
                        if (fits)
                        {
                            one++; if (name != Name140) oneLines++;
                            if (head.Count != 1 || head[0].Text != name || Math.Abs(head[0].Width - m("<b>" + lead + "</b>  " + name)) > 0.01f) bad.Add(at + ": fits one line, got " + HeadText(head));
                            continue;
                        }
                        if (head[0].Text.Length != 0 || rows.Count == 0) { bad.Add(at + ": does not fit one line, got " + HeadText(head)); continue; }
                        if (name.Length <= 38)
                        {
                            ownRows++;
                            if (rows.Count > 2 || rows.Any(r => r.Text.Contains(ScreenBand.Ellipsis)) || string.Join(" ", rows.Select(r => r.Text)) != name) bad.Add(at + ": " + rows.Count + " rows " + HeadText(head));
                        }
                        else
                        {
                            int need = ScreenBand.Wrap(new[] { name }, m, room, cont, int.MaxValue, 1).Count;
                            widest140 = Math.Min(widest140, need); most140 = Math.Max(most140, need);
                            if (need <= 2 || rows.Count != 2 || !rows[1].Text.EndsWith(ScreenBand.Ellipsis) || rows[0].Text.Contains(ScreenBand.Ellipsis) || !name.StartsWith(rows[0].Text + " ")) bad.Add(at + ": needs " + need + " rows, got " + HeadText(head));
                            else cut++;
                        }
                    }
                    counts.Add(lead + " " + one + "/" + names.Count);
                }
                watch.Add(sc.Item1 + " " + string.Join(", ", counts) + " (room " + F(room, "0") + ")");
            }
            Check("G12", "C16-10 a: the header per name (" + names.Count + " names + a 60-character word, WHY and CLOSE CALL, at 3440 x 1440 Auto / 21.6 px / 130 % / 150 %, 2560 x 1080, 5120 x 1440): one line exactly when the lead and the name fit together (measured whole); else the lead alone and the name's rows - two at most and whole for every name of 38 characters or less; the 140-character name in two rows ending in '...'; the 60-character word: the lead alone; every row within its room",
                bad.Count == 0, forms + " forms: " + oneLines + " one-line, " + ownRows + " on rows of their own, " + cut + " cut, " + alone + " lead alone; the 140-character name needs " + widest140 + " - " + most140 + " rows" + (bad.Count > 0 ? " | " + bad.Count + " bad: " + string.Join(" || ", bad.Take(5)) : ""));
            Console.WriteLine("    watch (no check): the names on the lead's line - " + string.Join("; ", watch));

            // b. every card of the 3- and 4-card level-ups (and of the rescue screen with the reroll hint at 3440 x 1440), headed with a long
            // game name and with the 38-character one: the panel clear and every reason shown, up to four - a name row never costs a reason
            var sets = new[] { Tuple.Create("LevelUp", LevelUp), Tuple.Create("Third", Third), Tuple.Create("LiveTank", LiveTank), Tuple.Create("LiveFive", LiveFive) };
            int panels = 0, twoRow = 0, maxRows = 0; var off = new List<string>(); var tallest = "";
            float most = 0f;
            foreach (var sc in HeadScreens())
            {
                var screens = new List<Sel> { sc.Item2(3), sc.Item2(4) };
                if (sc.Item1.StartsWith("3440", StringComparison.Ordinal)) screens.Add(Screen(3440, 1440, sc.Item4 > 0 ? 5160 : 5161, 3, true));
                foreach (var s in screens)
                    for (int card = 0; card < s.Rest.Length; card++)
                    {
                        float px; var x = SideOf(s, card, sc.Item3, true, out px, sc.Item4);
                        foreach (var name in new[] { "Automatic Turret: Provocation", Name38 })
                            foreach (var lead in new[] { "WHY", "CLOSE CALL" })
                                foreach (var set in sets)
                                {
                                    panels++;
                                    string why = "no panel"; var d = Lay(x, px, lead, set.Item2, false, name);
                                    bool clear = d.Side.At != "none" && Clear(d, x, out why);
                                    int rows = d.Head.Count;
                                    if (rows > 1) twoRow++;
                                    maxRows = Math.Max(maxRows, rows);
                                    if (d.Block.H > most) { most = d.Block.H; tallest = sc.Item1 + " " + s.Name + " card " + (card + 1) + " " + lead + " '" + name + "' " + set.Item1 + ": " + F(d.Block.H, "0") + " units, " + d.Lines.Count + " lines under a header of " + rows + " (room " + d.Side.Lines + ")"; }
                                    if (!clear || d.Shown != Math.Min(ScreenBand.SideMaxItems, set.Item2.Length))
                                        off.Add(sc.Item1 + " " + s.Name + " card " + (card + 1) + " " + lead + " '" + name + "' " + set.Item1 + ": " + d.Shown + " of " + set.Item2.Length + " (" + why + ")");
                                }
                    }
            }
            Check("G12", "C16-10 b: every card of the 3- and 4-card level-ups and of the rescue screen (3440 x 1440, the reroll hint) at those sizes, headed with 'Automatic Turret: Provocation' and with the 38-character name: the panel clear (Clear: in the wing, over nothing, under the cards' top) and every reason shown up to four",
                off.Count == 0, panels + " panels, " + twoRow + " with the name on rows of its own (a header of " + maxRows + " rows at most); the tallest: " + tallest + (off.Count > 0 ? " | " + off.Count + " off: " + string.Join(" || ", off.Take(5)) : ""));
        }

        // ---------------------------------------------------------------- S: the sources of item 12
        static void SideSources()
        {
            string why = Src("WhyUi.cs"), wide = Src("WideMenus.cs");
            Check("S", "the WHY tries the wing first: WideMenus.RunWings, ScreenBand.Side over the cards, the buttons, the obstacles and the divider, the cards' own reason size, Wrap into four reasons in the lines the room holds (0.15.x, C-M1), the side panel's own words (WhyBlock.SideItems), the block beside the card",
                why != null && why.Contains("if (SidePanel(c, view, block, buttons, obstacles, reasonPx, unitPx)) return;") && why.Contains("if (!WideMenus.RunWings(out wing)) return false;")
                && why.Contains("var side = ScreenBand.Side(new WhySideIn") && why.Contains("float font = ScreenBand.SidePx(reasonPx) / unitPx")
                && why.Contains("foreach (var it in block.SideItems) { string s = Wording.Safe(Names.Text(it)); if (s.Length > 0) items.Add(s); }")
                && why.Contains("ScreenBand.Wrap(items, measure, side.Width - chrome - ScreenBand.Slack(font), cont, side.Lines - (head.Count - 1), ScreenBand.SideMaxItems)") && why.Contains("ScreenBand.BlockHeight(lines.Count + head.Count, font)")
                && why.Contains("ChromeUnits = chrome")
                && why.Contains("float left = side.Left ? side.Room.X1 - width : side.Room.X0, top = side.Room.Y1 - height;"));
            // 0.16.0 (C16-10): the header names the card as the card shows it, the [why] line counts the reasons' lines and says the card of the row
            Check("S", "C16-10: SidePanel heads the panel with the card's shown name (Names.Name(c.Name) through ScreenBand.Header, in the room of a reason's first line), the reasons in the lines left under the header; the side [why] line passes the reasons' lines alone (no '+ 1'), LinesSaid no longer subtracts one, ', card P of N' and ', header in R rows'",
                why != null && why.Contains("string name = Wording.Safe(Names.Name(c.Name));") && why.Contains("var head = ScreenBand.Header(block.Lead, name, measure, side.Width - chrome - ScreenBand.Slack(font), cont);")
                && why.Contains("Log(c, block, drawn, side.At, lines.Count, font * unitPx, items.Count, width, height, left, top, ") && why.Contains("side.Pos, side.Of, head.Count);")
                && !why.Contains("lines.Count + 1,") && !why.Contains("lines.Count + 1, font") && !why.Contains("lines - 1")
                && why.Contains("(of > 0 ? \", card \" + pos + \" of \" + of : \"\")") && why.Contains("(headRows > 1 ? \", header in \" + headRows + \" rows\" : \"\")")
                && why.Contains("spot.Bands, 0, 0, 0, shortForm, spot.Widest);"));      // the band's Log call (C16-15 added spot.Widest)
            Check("S", "the band's lines measured as drawn and the separator between two glyphs (Q4)",
                why != null && why.Contains("float widest = ScreenBand.FitLines(lines, items, Separator, s => Width(s, font), indent, room);") && why.Contains("sepW = Width(\"x\" + Separator + \"x\", font) - Width(\"xx\", font);")
                && why.Contains("float width = ScreenBand.BlockWidth(widest, chrome, font, spot.Seg.W)") && !why.Contains("widest + chrome + 4f"));
            Check("S", "WideMenus tells whether the run's wings are open (the frame's pieces hidden on a canvas wider than 3840 units, none shown again)",
                wide != null && wide.Contains("public static bool RunWings(out float wing)") && wide.Contains("if (!s.Applied || s.PiecesShown || s.W - RefW < 1f) return false;"));
        }

        // ================================================================ 0.16.0 (C16-11g, C16-14)
        // ---------------------------------------------------------------- G13: the recommend frame's bottom edge round the card's plate
        // the frame is the card root (832 x 1462, centred) inset 5: its bottom edge runs x -411 .. 411 in the root's units
        static void EdgeCuts()
        {
            const float X0 = -411f, X1 = 411f, Gap = 10f;
            float a1, b0;
            bool mid = ScreenBand.EdgeCut(X0, X1, -100f, 100f, Gap, out a1, out b0);
            Check("G13", "C16-11g: a plate centred on the edge (200 units wide) cuts it symmetrically, " + F(Gap, "0") + " units clear either side - the left piece " + F(X0, "0") + " .. " + F(a1, "0") + ", the right " + F(b0, "0") + " .. " + F(X1, "0"),
                mid && Math.Abs(a1 - -110f) < Eps && Math.Abs(b0 - 110f) < Eps && Math.Abs((a1 - X0) - (X1 - b0)) < Eps);
            float c1, d0;
            bool off = ScreenBand.EdgeCut(X0, X1, 100f, 250f, Gap, out c1, out d0);
            Check("G13", "an off-centre plate cuts the edge off-centre (x 100 .. 250: the pieces " + F(X0, "0") + " .. " + F(c1, "0") + " and " + F(d0, "0") + " .. " + F(X1, "0") + ")",
                off && Math.Abs(c1 - 90f) < Eps && Math.Abs(d0 - 260f) < Eps && (c1 - X0) > (X1 - d0));
            // the widest plate that still cuts leaves gap + 4 units of edge on each side
            float limit = (X1 - X0) - 2f * (Gap + 4f), e1, f0, g1, h0;
            bool atLimit = ScreenBand.EdgeCut(X0, X1, -limit / 2f, limit / 2f, Gap, out e1, out f0);
            bool wider = ScreenBand.EdgeCut(X0, X1, -limit / 2f - 1f, limit / 2f + 1f, Gap, out g1, out h0);
            Check("G13", "a plate wider than the edge less 2 x (gap + 4) - " + F(limit, "0") + " units - draws the edge whole (false, a1 = b0 = the edge's end); at exactly that width it still cuts, 4 units of edge each side",
                atLimit && Math.Abs(e1 - X0 - 4f) < Eps && Math.Abs(X1 - f0 - 4f) < Eps && !wider && g1 == X1 && h0 == X1);
            var outside = new[] { new[] { 420f, 500f }, new[] { -600f, -500f }, new[] { -430f, -300f }, new[] { 300f, 420f }, new[] { 50f, 40f } };
            bool none = true; var says = new List<string>();
            foreach (var o in outside) { float p, q; bool cut = ScreenBand.EdgeCut(X0, X1, o[0], o[1], Gap, out p, out q); none &= !cut && p == X1 && q == X1; says.Add(F(o[0], "0") + ".." + F(o[1], "0") + (cut ? " CUT" : " whole")); }
            Check("G13", "a plate outside the edge, reaching past one of its ends, or too near one, and an empty plate: the edge whole", none, string.Join(", ", says));
            PrefabPlates();
        }

        // r1 fix (10-08): the game's own card geometry, read offline from the level2 scene with UnityPy (every PowerupButton_LevelUp and
        // PowerupButton_CharacterRescue the same; the hierarchy computed to the root's units - PowerupLevel reproduces r1's live
        // '544x224 units, y 212..436' exactly). The card root 832 x 1462 centred; the frame inset 5, its bottom bar 6 high: -726 .. -720.
        //   plate 'Content/AddonDescription' (Content centred, 800 x 1430, scale 1): anchoredPosition (-0.8987, -707), 400 x 150 - the image
        //         GenericStatBoost_Rarity_Base_01 whose label 'Content/AddonDescription/Text' is the cards' className field (TIER I / NEW / RECRUIT)
        //   r1's  'Content/PowerupLevel' (powerupLevelObject): (0, 324.3), 543.5475 x 224 - the level diamond on the portrait
        //   the sprite's opaque part: textureRect 381.87 x 74.85 at (9.05, 38.08) of its 400 x 150 rect (the Image draws Simple: that
        //         part only) - the plate as seen, x -191.85 .. 190.02, y -743.92 .. -669.08: the edge's bar crosses its lower half
        static void PrefabPlates()
        {
            const float RX0 = -416f, RX1 = 416f, RY0 = -731f, Inset = 5f, Thick = 6f, Gap = 10f;
            float ax = -0.8986969f, ay = -707f, px0 = ax - 200f, px1 = ax + 200f, py0 = ay - 75f, py1 = ay + 75f, a1, b0;
            string got = ScreenBand.PlateCut(RX0, RX1, RY0, Inset, Thick, px0, px1, py0, py1, Gap, out a1, out b0);
            string line = ScreenBand.PlateLine("UIPowerupButtonSOS", "AddonDescription", got, RX0, RX1, RY0, Inset, Thick, px0, px1, py0, py1, a1, b0);
            const string Want = "[badge] plate: UIPowerupButtonSOS AddonDescription 400x150 units, y -782..-632 (the frame's bottom edge -726..-720) - edge cut -211..209";
            Check("G13", "r1 fix: the RECRUIT / TIER / NEW plate as the game places it ('Content/AddonDescription', 400 x 150 at y -782 .. -632) cuts the frame's edge "
                + F(a1, "0.0") + " .. " + F(b0, "0.0") + ", 10 units clear of its rect, symmetric within the plate's 0.9-unit offset, and says the line the live redo greps",
                got == "cut" && Math.Abs(a1 - (px0 - Gap)) < Eps && Math.Abs(b0 - (px1 + Gap)) < Eps && line == Want, got + " / " + line);
            // the plate as drawn: inside the cut, and astride the bar (the strike-through r1's C_rescue shot shows)
            float vx0 = px0 + 9.051316f, vx1 = vx0 + 381.87256f, vy0 = py0 + 38.07612f, vy1 = vy0 + 74.84776f, e0 = RY0 + Inset, e1 = e0 + Thick;
            Check("G13", "r1 fix: the plate as drawn (its sprite's opaque part, x " + F(vx0, "0.0") + " .. " + F(vx1, "0.0") + ", y " + F(vy0, "0.0") + " .. " + F(vy1, "0.0")
                + ") lies astride the edge's bar " + F(e0, "0") + " .. " + F(e1, "0") + " and inside the cut, " + F(vx0 - a1, "0.0") + " / " + F(b0 - vx1, "0.0") + " units clear either side",
                vy0 < e0 && vy1 > e1 && a1 < vx0 - Gap && b0 > vx1 + Gap);
            // r1's object: the r1 live line reproduced - 'apart', the edge whole (20 runs; the C_rescue shot: the line through RECRUIT)
            float lx0 = -543.5475f / 2f, lx1 = 543.5475f / 2f, ly0 = 324.3f - 112f, ly1 = 324.3f + 112f, c1, d0;
            string r1 = ScreenBand.PlateCut(RX0, RX1, RY0, Inset, Thick, lx0, lx1, ly0, ly1, Gap, out c1, out d0);
            string r1Line = ScreenBand.PlateLine("UIPowerupButtonSkill", "PowerupLevel", r1, RX0, RX1, RY0, Inset, Thick, lx0, lx1, ly0, ly1, c1, d0);
            Check("G13", "r1 fix: r1's powerupLevelObject ('PowerupLevel', the portrait's level diamond) misses the edge - 'apart', the edge whole - and says r1's live line",
                r1 == "apart" && c1 == RX1 - Inset && d0 == RX1 - Inset
                && r1Line == "[badge] plate: UIPowerupButtonSkill PowerupLevel 544x224 units, y 212..436 (the frame's bottom edge -726..-720) - no overlap, edge whole", r1 + " / " + r1Line);
            // on the bar with no room either side: 'wide' and its words; the bar touched only at its edge (y1 = e0): 'apart'
            float w1, w0, t1, t0;
            string wide = ScreenBand.PlateCut(RX0, RX1, RY0, Inset, Thick, -405f, 405f, -760f, -700f, Gap, out w1, out w0);
            string touch = ScreenBand.PlateCut(RX0, RX1, RY0, Inset, Thick, -100f, 100f, -800f, -726f, Gap, out t1, out t0);
            string wideLine = ScreenBand.PlateLine("UIPowerupButtonSkill", "AddonDescription", wide, RX0, RX1, RY0, Inset, Thick, -405f, 405f, -760f, -700f, w1, w0);
            Check("G13", "r1 fix: a plate on the bar too wide for room either side is 'wide' (the edge whole, said so), one that only touches the bar's bottom is 'apart'",
                wide == "wide" && w1 == RX1 - Inset && w0 == RX1 - Inset && wideLine.EndsWith(" - x -405..405 leaves no room either side of it on the edge -411..411, edge whole", StringComparison.Ordinal) && touch == "apart",
                wide + " / " + touch + " / " + wideLine);
        }

        // ---------------------------------------------------------------- B2: the WHY band at 1280 x 800 with the live Deck numbers
        // the 10-07 Deck round (companion.log 20261007_195304): the band under the buttons y 2119 - 2272 (153 units) on the level-ups -
        // 2670 wide at 00:15 (:213), 2787 at 02:25 (:451) -, 139 units where a count plate reached 2125 (:725, :815); one px is 3 units
        // (a 2400-unit canvas on 800 px); the preferred size the reason line's 16.0 - 16.8 px; every view drew one line at it (41 of 97 dropped
        // reasons) - two at 15 px fit
        static WhyBandIn DeckIn(float x0, float x1, float anchor, float prefPx, int want, R4 hint, float plateBottom = 2111f, float text = 0f)
        {
            var obstacles = new List<R4>(); if (!hint.Empty) obstacles.Add(hint);
            obstacles.Add(new R4(3003, 92, 3790, 2026));                       // the team panel (it ends above the band)
            float font = prefPx * 3f;
            return new WhyBandIn
            {
                View = new R4(0, 0, 3840, 2160), Bottom = 2280, CardsBottom = 1930, SpanX0 = x0, SpanX1 = x1,
                Buttons = new[] { new R4(196, 1977, 774, 2078), new R4(892, 1977, 1471, 2078), new R4(1593, 1977, 2170, 2078), new R4(2293, 1977, 2866, 2078), new R4(280, 2050, 336, plateBottom) },
                Obstacles = obstacles.ToArray(), Rules = new R4[0], AnchorX = anchor, FontUnits = font, MinFontUnits = 45f, Want = want,
                TextUnits = text, ChromeUnits = text > 0 ? ChromeU(font) : 0f,          // as WhyUi passes them (0: Want as given)
            };
        }

        static string Spot(WhySpot s) { return s.At + ", " + s.Lines + " line(s) at " + F(s.Font / 3f, "0.0") + " px, pad " + F(s.Pad, "0.00") + ", in " + s.Seg.Size + " at x " + F(s.Seg.X0, "0"); }
        static bool Is(WhySpot s, string at, float segW, float segH, int lines, float px, float pad)
        {
            return s.At == at && Math.Abs(s.Seg.W - segW) < 0.5f && Math.Abs(s.Seg.H - segH) < 0.5f && s.Lines == lines && Math.Abs(s.Font / 3f - px) < 0.05f && Math.Abs(s.Pad - pad) < 1e-4f;
        }

        static void DeckBand()
        {
            var none = new R4();
            const string Under = "under the buttons";
            var l015 = ScreenBand.Why(DeckIn(196, 2866, 1527, 16.0f, 2, none));
            Check("B2", "C16-14: the level-up of 00:15 at 16.0 px, two lines wanted (0.15.0: one line at 16 px in 2670 x 153, reasons dropped): two lines at 15 px with the full pad",
                Is(l015, Under, 2670, 153, 2, 15f, ScreenBand.Pad) && ScreenBand.Near(DeckIn(196, 2866, 1527, 16.0f, 2, none)), Spot(l015));
            var l225 = ScreenBand.Why(DeckIn(123, 2910, 582, 16.8f, 2, none));
            Check("B2", "the level-up of 02:25 at 16.8 px (the offer's first WHY, DK-C12), two lines wanted (0.15.0: one line at 16.8 px in 2787 x 153): two lines at 15 px with the full pad",
                Is(l225, Under, 2787, 153, 2, 15f, ScreenBand.Pad), Spot(l225));
            var one = ScreenBand.Why(DeckIn(196, 2866, 1527, 16.0f, 1, none));
            var oneText = ScreenBand.Why(DeckIn(196, 2866, 1527, 16.0f, 1, none, 2111f, 1000f));
            Check("B2", "one line wanted - by Want, or by the words' width in the band (1000 units in 2670) -: unchanged, one line at 16 px (the 15 px passes skipped)",
                Is(one, Under, 2670, 153, 1, 16f, ScreenBand.Pad) && Is(oneText, Under, 2670, 153, 1, 16f, ScreenBand.Pad), Spot(one) + " | " + Spot(oneText));
            var far = ScreenBand.Why(DeckIn(196, 2866, 1527, 17.5f, 2, none));
            Check("B2", "17.5 px (2.5 px over the floor, not near it): unchanged, one line at 17.5 px",
                Is(far, Under, 2670, 153, 1, 17.5f, ScreenBand.Pad) && !ScreenBand.Near(DeckIn(196, 2866, 1527, 17.5f, 2, none)) && ScreenBand.Near(DeckIn(196, 2866, 1527, 17.0f, 2, none)), Spot(far));
            var b139 = ScreenBand.Why(DeckIn(263, 2865, 1527, 16.0f, 2, none, 2125f));
            Check("B2", "the 139-unit band (the rescue screen of 05:42, :725 - 2602 x 139, a plate down to 2125): two 15 px lines only with the tight pad - " + F(ScreenBand.BlockHeight(2, 45f, ScreenBand.TightPad), "0") + " units in " + F(b139.Seg.H, "0"),
                Is(b139, Under, 2602, 139, 2, 15f, ScreenBand.TightPad) && ScreenBand.BlockHeight(2, 45f, ScreenBand.TightPad) <= b139.Seg.H + 0.01f && ScreenBand.LinesIn(139f, 45f) == 1, Spot(b139));
            var chest = ScreenBand.Why(DeckIn(123, 2910, 1527, 16.0f, 1, new R4(196, 2133, 1157, 2207), 2125f, 2400f));
            Check("B2", "the replaced chest of 06:31 (:815): the band cut by the REROLL line to 1745 x 139, one line wanted over the cards' whole span but 2400 units of words: the lines wanted per band - two at 15 px (the tight pad)",
                Is(chest, Under, 1745, 139, 2, 15f, ScreenBand.TightPad) && Math.Abs(chest.Seg.X0 - 1165f) < 0.5f && ScreenBand.WantIn(DeckIn(123, 2910, 1527, 16.0f, 1, none, 2125f, 2400f), chest.Seg, 48f) == 2, Spot(chest));
            var withText = ScreenBand.Why(DeckIn(196, 2866, 1527, 16.0f, 1, none, 2111f, 3000f));
            Check("B2", "the 00:15 level-up as WhyUi asks for it (the words' width given, 3000 units): two lines at 15 px",
                Is(withText, Under, 2670, 153, 2, 15f, ScreenBand.Pad), Spot(withText));

            // the PC keeps every layout: at 20.5 px (5.5 px over the floor) the words' width changes nothing
            var same = new List<string>(); bool pcOk = true;
            foreach (var sc in new[] { Screen(3440, 1440, 5161, 4, false), Screen(3440, 1440, 5161, 3, false), Screen(3440, 1440, 5161, 3, true) })
                foreach (int want in new[] { 1, 2, 3 })
                {
                    var a = PcBand(sc, 20.5f, want, 0f); var b = PcBand(sc, 20.5f, want, 4000f);
                    bool eq = a.At == b.At && a.Lines == b.Lines && Math.Abs(a.Font - b.Font) < 0.001f && a.Seg.ToString() == b.Seg.ToString() && a.Pad == b.Pad;
                    pcOk &= eq; same.Add(sc.Name + " want " + want + ": " + a.At + " " + a.Lines + (eq ? "" : " -> " + b.At + " " + b.Lines));
                }
            Check("B2", "3440 x 1440 (20.5 px): with or without the words' width every band as before - the PC is not near the floor", pcOk, string.Join("; ", same));

            // the short "goes first": the side panel's form of the "vs #1" sentence, drawn in the band when the long form drops an item and the
            // short one shows more
            var mol = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Build = "Rifleman", Priority = 1, HeadRank = 4, Head = Wording.Role("Rifleman", 1), Owned = 4, Reach = 1, Clock = "9:41 left", ShareType = "Fire", SharePct = 33 };
            const string FirstLine = "Level 2 of 4 - the build's main ability";
            var k = WhyText.Block(new WhyIn { Name = "Molotov Cocktail", Rank = 2, Score = 4.66, Shown = "New to the squad", Say = mol, FirstName = "Medical Drone", FirstScore = 5.20, FirstShown = FirstLine, SecondName = "Molotov Cocktail", SecondScore = 4.66 });
            Check("B2", "the short form: a #2 card whose edge is the #1 card's own line - Items[0] 'Medical Drone goes first: <its line>', SideItems[0] 'Medical Drone goes first', the reasons the same",
                k.Items.Count > 0 && k.Items[0] == "Medical Drone goes first: " + FirstLine && k.SideItems.Count == k.Items.Count && k.SideItems[0] == "Medical Drone goes first" && k.SideItems.Skip(1).SequenceEqual(k.Items.Skip(1)),
                "[" + string.Join(" | ", k.Items) + "] / [" + string.Join(" | ", k.SideItems) + "]");
            // WhyUi.Place's choice on the glyph model at 15 px: one line in a 1400-unit stretch (the long sentence alone fits, the short one
            // leaves room for a reason: the short drawn); two lines across the level-up's 2670 (all three fit: nothing to fall back from); one
            // line in the rescue band the hint cut to 1028 units (C16-15 widens it: one item either way - a tie keeps the long form)
            var picks = new List<string>(); bool pickOk = true;
            foreach (var band in new[] { Tuple.Create(1400f, 1, "short"), Tuple.Create(2670f, 2, "all"), Tuple.Create(1028f, 1, "tie") })
            {
                int longN, shortN; bool usedShort = BandPick(k, 45f, band.Item1, band.Item2, out longN, out shortN);
                if (band.Item3 == "short") pickOk &= usedShort && longN < k.Items.Count && shortN > longN;
                else if (band.Item3 == "all") pickOk &= !usedShort && longN == k.Items.Count && shortN == longN;
                else pickOk &= !usedShort && longN < k.Items.Count && shortN == longN;
                picks.Add(F(band.Item1, "0") + " units, " + band.Item2 + " line(s): the long form " + longN + " of " + k.Items.Count + ", the short " + shortN + (usedShort ? " - the short drawn" : " - the long kept"));
            }
            Check("B2", "the band falls back to the short 'goes first' only when an item dropped and the short form shows more (all shown: no fallback; a tie keeps the long form)",
                pickOk, string.Join("; ", picks));
        }

        static WhySpot PcBand(Sel s, float px, int want, float text, float needW = 0f)
        {
            var rule = new R4(127, 1902, 2965, 1906);
            var buttons = s.Obstacles.Where(o => o.Y0 >= 1940f && !o.Equals(s.Hint)).ToList();
            var obstacles = s.Obstacles.Where(o => !buttons.Contains(o) && !o.Equals(rule)).ToList();
            float x0 = Math.Min(s.Rest.Min(c => c.X0), buttons.Min(b => b.X0)), x1 = Math.Max(s.Rest.Max(c => c.X1), buttons.Max(b => b.X1));
            float font = CardTextSize.WhyPx(px, 1f) / s.UnitPx;
            return ScreenBand.Why(new WhyBandIn
            {
                View = s.View, Bottom = Math.Max(s.View.Y1, s.Canvas.Y1), CardsBottom = HoverLow, SpanX0 = x0, SpanX1 = x1, Buttons = buttons.ToArray(), Obstacles = obstacles.ToArray(), Rules = new[] { rule },
                AnchorX = (s.Rest[0].X0 + s.Rest[0].X1) / 2f, FontUnits = font, MinFontUnits = ScreenBand.MinPx / s.UnitPx, Want = want, TextUnits = text, ChromeUnits = text > 0 ? ChromeU(font) : 0f,
                NeedW = needW,
            });
        }

        // ---------------------------------------------------------------- B3: the REROLL / SKIP line under its button vs the WHY band (C16-15)
        // the 10-07 Deck round (DK-C04, companion.log 20261007_195304): at 1280 x 800 the band over the button is 63 units, so the line goes
        // under it, into the band under the buttons (153 units): the chests' 961 x 74 line left the band 1745 of 2787 units (:472 1 of 2
        // shown, :815 2 of 4), the rescue screen's 934 x 74 line left its #2 card 640 units left of it (:432 'no item fits the band
        // (640x153)') and #1 1028 right of it (:433 1 of 3). (a) the line's short words (HintCases H4); (b) the selected card's own piece only
        // when it holds the first item at 15 px - NeedW, 785 units for a 40-character first item and the lead (671 on research's model)
        // with the chrome and the slack -, else the band's widest
        static void HintBand()
        {
            const string Under = "under the buttons";
            const float Need = 785f;
            var chestLong = new R4(196, 2119, 1157, 2193); var chestShort = new R4(196, 2119, 647, 2193);
            var sosLong = new R4(895, 2119, 1829, 2193); var sosShort = new R4(895, 2119, 1509, 2193);
            Func<WhyBandIn, float, WhyBandIn> needs = (x, w) => { x.NeedW = w; return x; };
            Func<float, float, bool> at = (v, want) => Math.Abs(v - want) < 0.5f;
            var all = new List<Tuple<string, WhyBandIn>>();
            Func<string, WhyBandIn, WhySpot> lay = (name, x) => { all.Add(Tuple.Create(name, x)); return ScreenBand.Why(x); };

            // the chest of 06:31 (span 123 - 2910, the selected card at 1527, 16.8 px)
            var cl = lay("chest, the long line", needs(DeckIn(123, 2910, 1527, 16.8f, 2, chestLong), Need));
            var cs = lay("chest, the short line", needs(DeckIn(123, 2910, 1527, 16.8f, 2, chestShort), Need));
            var cl0 = ScreenBand.Why(DeckIn(123, 2910, 1527, 16.8f, 2, chestLong));
            Check("B3", "C16-15 the chest of 06:31 (:803 - :815; span 123 - 2910, the card at 1527, 16.8 px): the long line (961 units) leaves the band 1745 units at x 1165, the short one (451) 2255 at x 655 - two lines at 15 px (C16-14); the card's own piece either way",
                Is(cl, Under, 1745, 153, 2, 15f, ScreenBand.Pad) && at(cl.Seg.X0, 1165) && Is(cs, Under, 2255, 153, 2, 15f, ScreenBand.Pad) && at(cs.Seg.X0, 655) && !cl.Widest && !cs.Widest
                && cl0.Seg.ToString() == cl.Seg.ToString() && cl0.Lines == cl.Lines, Spot(cl) + " | " + Spot(cs));

            // the rescue screen of 02:12 (span 247 - 2865, 16.0 px; #2 at 560 left of the line, #1 at 2100 right of it)
            var s2old = ScreenBand.Why(DeckIn(247, 2865, 560, 16.0f, 2, sosLong));
            var s2 = lay("rescue #2, the long line", needs(DeckIn(247, 2865, 560, 16.0f, 2, sosLong), Need));
            var s2s = lay("rescue #2, the short line", needs(DeckIn(247, 2865, 560, 16.0f, 2, sosShort), Need));
            Check("B3", "the rescue screen's #2 card (:432, left of the 934-unit line; its first item needs 785 units at 15 px): its own piece 640 at x 247 (0.15.0: 'no item fits the band (640x153)') gives way to the widest, 1028 at x 1837 - 1348 at x 1517 with the short line (614)",
                Is(s2old, Under, 640, 153, 2, 15f, ScreenBand.Pad) && at(s2old.Seg.X0, 247) && Is(s2, Under, 1028, 153, 2, 15f, ScreenBand.Pad) && at(s2.Seg.X0, 1837) && s2.Widest
                && Is(s2s, Under, 1348, 153, 2, 15f, ScreenBand.Pad) && at(s2s.Seg.X0, 1517) && s2s.Widest && s2.Bands.Contains("the selected card's own 640x153 holds no first item (785 units at 15 px)"),
                "0.15.0 " + Spot(s2old) + " | " + Spot(s2) + " | " + Spot(s2s));
            float w800 = 800f;
            Check("B3", "the block in a piece that does not hold the card's centre sits at its end nearest the card (Left): x 1837 / 1517 for the card at 560",
                at(ScreenBand.Left(s2.Seg, 560, w800), 1837) && at(ScreenBand.Left(s2s.Seg, 560, w800), 1517) && at(ScreenBand.Left(s2.Seg, 560, 2000f), 1837));
            var s1 = lay("rescue #1, the long line", needs(DeckIn(247, 2865, 2100, 16.0f, 2, sosLong), Need));
            var s1s = lay("rescue #1, the short line", needs(DeckIn(247, 2865, 2100, 16.0f, 2, sosShort), Need));
            var s1old = ScreenBand.Why(DeckIn(247, 2865, 2100, 16.0f, 2, sosLong));
            Check("B3", "the rescue screen's #1 card (:433, right of the line): 1028 at x 1837 as before - (b) changes nothing there -, 1348 at x 1517 with the short line",
                Is(s1, Under, 1028, 153, 2, 15f, ScreenBand.Pad) && at(s1.Seg.X0, 1837) && !s1.Widest && s1old.Seg.ToString() == s1.Seg.ToString() && Is(s1s, Under, 1348, 153, 2, 15f, ScreenBand.Pad) && at(s1s.Seg.X0, 1517),
                Spot(s1) + " | " + Spot(s1s));

            // no piece holds the first item: the band as before (a later item may still fit); a need under MinWidth: nothing changes
            var huge = ScreenBand.Why(needs(DeckIn(247, 2865, 560, 16.0f, 2, sosLong), 3000f));
            var small = ScreenBand.Why(needs(DeckIn(247, 2865, 560, 16.0f, 2, sosLong), 450f));
            Check("B3", "a first item no piece of any band holds (3000 units): the band as before C16-15 (640 at x 247), said in the log; a need under MinWidth (450): nothing changes",
                huge.Seg.ToString() == s2old.Seg.ToString() && huge.Lines == s2old.Lines && huge.Font == s2old.Font && huge.Pad == s2old.Pad && !huge.Widest && huge.Bands.EndsWith("; no stretch holds the first item (3000 units at 15 px)")
                && small.Seg.ToString() == s2old.Seg.ToString() && small.Bands == s2old.Bands && ScreenBand.Need(needs(new WhyBandIn(), 450f)) == ScreenBand.MinWidth && ScreenBand.Need(needs(new WhyBandIn(), 785f)) == 785f && ScreenBand.Need(needs(new WhyBandIn(), float.NaN)) == ScreenBand.MinWidth,
                Spot(huge) + " | " + Spot(small));

            // NeedW as WhyUi.FirstNeed measures it: the chrome, the slack, the lead with its gap and the first item at 15 px - the short "goes
            // first" when the block has one (the form the band falls back to, C16-14)
            const string FirstLine = "Level 2 of 4 - the build's main ability";
            var mol = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Build = "Rifleman", Priority = 1, HeadRank = 4, Head = Wording.Role("Rifleman", 1), Owned = 4, Reach = 1, Clock = "9:41 left", ShareType = "Fire", SharePct = 33 };
            var k = WhyText.Block(new WhyIn { Name = "Molotov Cocktail", Rank = 2, Score = 4.66, Shown = "New to the squad", Say = mol, FirstName = "Medical Drone", FirstScore = 5.20, FirstShown = FirstLine, SecondName = "Molotov Cocktail", SecondScore = 4.66 });
            float f15 = 45f, lead = Tmp("<b>" + k.Lead + "</b>", f15) + f15 * 0.6f;
            float needShort = ScreenBand.NeedWidth(lead, Tmp(k.SideItems[0], f15), ChromeU(f15), f15), needLong = ScreenBand.NeedWidth(lead, Tmp(k.Items[0], f15), ChromeU(f15), f15);
            Check("B3", "NeedWidth = the chrome + the slack + the lead + the item (at 15 px on the Deck: the short '" + k.SideItems[0] + "' " + F(needShort, "0") + " units, the long '" + k.Items[0] + "' " + F(needLong, "0") + ")",
                Math.Abs(ScreenBand.NeedWidth(93f, 600f, ChromeU(f15), f15) - (93f + 600f + ChromeU(f15) + ScreenBand.Slack(f15))) < Eps && needShort < needLong && ScreenBand.NeedWidth(-5f, -5f, -5f, f15) == ScreenBand.Slack(f15),
                "chrome " + F(ChromeU(f15), "0.#") + " + slack " + F(ScreenBand.Slack(f15), "0.#") + " + the lead " + F(lead, "0") + " (research's model of the chrome and slack: 114)");

            // stacking the WHY over the line under the button does not fit the 153-unit band (recorded and rejected in the spec)
            float lineDeck = (float)Math.Round(62f * 48f / 40f), line15 = (float)Math.Round(62f * 45f / 40f), why1 = ScreenBand.BlockHeight(1, 45f, ScreenBand.TightPad);
            Check("B3", "stacking the WHY over the line does not fit the band: " + F(lineDeck, "0") + " + " + F(ScreenBand.Gap, "0") + " + " + F(why1) + " = " + F(lineDeck + ScreenBand.Gap + why1) + " > 153 (at 15 px: " + F(line15 + ScreenBand.Gap + why1) + ")",
                lineDeck == 74f && line15 == 70f && Math.Abs(why1 - 78.75f) < Eps && lineDeck + ScreenBand.Gap + why1 > 153f && line15 + ScreenBand.Gap + why1 > 153f);

            // the PC: with the need of the longest first item there is (the "vs #1" sentence's budget, WhyText.VersusBudget characters) on its
            // 15 px floor (22.5 units at 0.667 px a unit), every band as before
            var same = new List<string>(); bool pcOk = true;
            float pcFont = ScreenBand.MinPx / (1440f / 2160f);
            string longest = (k.Items[0] + " " + k.Items[0]).Substring(0, WhyText.VersusBudget);
            float pcNeed = ScreenBand.NeedWidth(Tmp("<b>WHY</b>", pcFont) + pcFont * 0.6f, Tmp(longest, pcFont), ChromeU(pcFont), pcFont);
            foreach (var sc in new[] { Screen(3440, 1440, 5161, 4, false), Screen(3440, 1440, 5161, 3, false), Screen(3440, 1440, 5161, 3, true) })
                foreach (int want in new[] { 1, 2, 3 })
                {
                    var a = PcBand(sc, 20.5f, want, 0f); var b = PcBand(sc, 20.5f, want, 0f, pcNeed);
                    bool eq = a.At == b.At && a.Lines == b.Lines && Math.Abs(a.Font - b.Font) < 0.001f && a.Seg.ToString() == b.Seg.ToString() && a.Pad == b.Pad && !b.Widest;
                    pcOk &= eq; same.Add(sc.Name + " want " + want + ": " + a.At + " " + a.Seg.Size + (eq ? "" : " -> " + b.At + " " + b.Seg.Size));
                }
            Check("B3", "3440 x 1440 (20.5 px): with the need of a " + longest.Length + "-character first item (" + F(pcNeed, "0") + " units, over MinWidth) every band as before - the PC's line stands over its button", pcOk && pcNeed > ScreenBand.MinWidth, string.Join("; ", same));

            // no WHY block over the hint's line (or a button, the team panel, the plates) in any B2 / B3 case: the whole stretch at the lines laid out
            all.Add(Tuple.Create("B2 00:15", DeckIn(196, 2866, 1527, 16.0f, 2, new R4())));
            all.Add(Tuple.Create("B2 02:25", DeckIn(123, 2910, 582, 16.8f, 2, new R4())));
            all.Add(Tuple.Create("B2 139-unit band", DeckIn(263, 2865, 1527, 16.0f, 2, new R4(), 2125f)));
            all.Add(Tuple.Create("B2 the replaced chest", DeckIn(123, 2910, 1527, 16.0f, 1, new R4(196, 2133, 1157, 2207), 2125f, 2400f)));
            all.Add(Tuple.Create("B2 the replaced chest, the short line", DeckIn(123, 2910, 1527, 16.0f, 1, new R4(196, 2133, 647, 2207), 2125f, 2400f)));
            all.Add(Tuple.Create("B3 rescue #2, no need given (0.15.0's piece)", DeckIn(247, 2865, 560, 16.0f, 2, sosLong)));
            var over = new List<string>(); int lined = 0;
            foreach (var c in all)
            {
                var sp = ScreenBand.Why(c.Item2);
                if (sp.At == "none") { over.Add(c.Item1 + ": no band"); continue; }
                if (c.Item2.Obstacles.Any(o => o.Y0 >= 2100f)) lined++;
                string why; if (!BandClear(sp, c.Item2, out why)) over.Add(c.Item1 + ": " + why);
            }
            Check("B3", "no WHY block over the hint's line, a button, a plate or the team panel in any of the " + all.Count + " B2 / B3 layouts (" + lined + " with the line under the button)", over.Count == 0 && lined >= 7, string.Join(" | ", over));
        }

        // the band's block as WhyUi draws it at most (the stretch's whole width, its height at the lines laid out, centred in the stretch): clear
        // of the buttons and their plates, every obstacle (the hint's line, the team panel) and the rules, between the cards and the bottom
        static bool BandClear(WhySpot spot, WhyBandIn x, out string why)
        {
            why = null;
            float h = ScreenBand.BlockHeight(spot.Lines, spot.Font, spot.Pad);
            float top = spot.Seg.Y0 + Math.Max(0f, (spot.Seg.H - h) / 2f);
            var block = new R4(spot.Seg.X0, top, spot.Seg.X1, top + h);
            if (h > spot.Seg.H + 0.01f) { why = "taller than its stretch"; return false; }
            if (block.Y0 < x.CardsBottom || block.Y1 > x.Bottom) { why = "outside the band"; return false; }
            foreach (var b in x.Buttons) if (block.Overlaps(b)) { why = "over a button " + b; return false; }
            foreach (var o in x.Obstacles) if (block.Overlaps(o)) { why = "over " + o; return false; }
            foreach (var r in x.Rules ?? new R4[0]) if (block.Overlaps(r)) { why = "over the rule " + r; return false; }
            return true;
        }

        // WhyUi.Place's packing on the glyph model (the lead inline as a hanging indent, the items packed and measured as drawn) for the long
        // form and the short one in a stretch <segW> wide holding <lines> lines at <font>: true when the short form is drawn
        static bool BandPick(WhyBlock block, float font, float segW, int lines, out int longN, out int shortN)
        {
            const string Sep = "  /  ";
            Func<string, float> m = t => Tmp(t, font);
            float leadW = m("<b>" + block.Lead + "</b>") + font * 0.6f, sepW = m("x" + Sep + "x") - m("xx");
            float room = segW - ChromeU(font) - ScreenBand.Slack(font), indent = leadW;
            Func<IList<string>, int> shown = items =>
            {
                var packed = WhyText.Pack(items.Select(m).ToList(), 0f, sepW, room - indent, lines);
                ScreenBand.FitLines(packed, items, Sep, m, indent, room);
                return packed.Sum(l => l.Count);
            };
            longN = shown(block.Items); shortN = longN;
            if (longN < block.Items.Count && !block.SideItems.SequenceEqual(block.Items)) shortN = shown(block.SideItems);
            return shortN > longN;
        }

        // ---------------------------------------------------------------- S: the sources of 0.16.0's C16-11g and C16-14
        static void Sources016()
        {
            string badge = Src("Badge.cs"), why = Src("WhyUi.cs"), band = Src("ScreenBand.cs");
            // r1 fix (10-08): the plate is className's parent ('Content/AddonDescription'), not powerupLevelObject (the portrait's level diamond)
            int plateAt = badge == null ? -1 : badge.IndexOf("static RectTransform PlateOf(UIPowerupButtonBase b)", StringComparison.Ordinal);
            int plateEnd = plateAt < 0 ? -1 : badge.IndexOf("\n        }", plateAt, StringComparison.Ordinal);
            string plateOf = plateAt < 0 || plateEnd < 0 ? "" : badge.Substring(plateAt, plateEnd - plateAt);
            Check("S", "C16-11g: the frame stays the card root's last child (drawn over the card art), Show cuts its edge on the recommended card only (and again 0.5 s later, Fx key 'badge:cut:'); r1 fix: the plate read as the className label's parent on the skill and rescue cards (the rescue class named only in a NoInlining helper), never powerupLevelObject, the cut decided and said by ScreenBand.PlateCut / PlateLine",
                badge != null && badge.Contains("if (existing != null) { var e = existing.TryCast<RectTransform>(); e.SetAsLastSibling(); return e; }") && badge.Contains("f.SetAsLastSibling();")
                && badge.Contains("if (best) { Cut(frame, root, c.Button); CutAgain(frame, root, c.Button); }") && System.Text.RegularExpressions.Regex.Matches(badge, @"\bCut\(frame, root, c\.Button\)").Count == 1
                && badge.Contains("CutKey = \"badge:cut:\"") && badge.Contains("CutSettle = 0.5f") && badge.Contains("CutGap = 10f")
                && badge.Contains("string outcome = ScreenBand.PlateCut(rr.xMin, rr.xMax, rr.yMin, FrameInset, FrameThick, px0, px1, py0, py1, CutGap, out a1, out b0);")
                && badge.Contains("ScreenBand.PlateLine(cls, plate.name, outcome, rr.xMin, rr.xMax, rr.yMin, FrameInset, FrameThick, px0, px1, py0, py1, a1, b0)")
                && plateOf.Contains("if (skill != null) label = skill.className;") && plateOf.Contains("label = SosPlateLabel(b);") && plateOf.Contains("label.transform.parent") && !plateOf.Contains("powerupLevelObject")
                && System.Text.RegularExpressions.Regex.IsMatch(badge, @"MethodImplOptions\.NoInlining\)\]\s*static TextMeshProUGUI SosPlateLabel\(UIPowerupButtonBase b\)\s*\{\s*var sos = b\.TryCast<UIPowerupButtonSOS>\(\);\s*return sos == null \? null : sos\.className;")
                && band != null && band.Contains("public static string PlateCut(") && band.Contains("return EdgeCut(x0, x1, px0, px1, gap, out a1, out b0) ? \"cut\" : \"wide\";"));
            int fit = why == null ? -1 : why.IndexOf("float widest = ScreenBand.FitLines(lines, items, Separator, s => Width(s, font), indent, room);", StringComparison.Ordinal);
            int fall = why == null ? -1 : why.IndexOf("if (shownLong < items.Count && !block.SideItems.SequenceEqual(block.Items))", StringComparison.Ordinal);
            Check("S", "C16-14: WhyUi passes the words' width and the chrome (TextUnits / ChromeUnits) and falls back to WhyBlock.SideItems only after FitLines dropped an item, drawing it only when it shows more; ScreenBand's passes near the floor (the wanted lines at the preferred size, at 15 px, at 15 px with the tight pad, then one line)",
                why != null && why.Contains("TextUnits = total, ChromeUnits = chrome,") && fit > 0 && fall > fit && why.Contains("if (sideLines.Sum(l => l.Count) > shownLong) { items = sideItems; lines = sideLines; widest = sideWidest; shortForm = true; }")
                && why.Contains(", the short \\\"goes first\\\"")
                && band != null && band.Contains("NearPasses = { 0, 3, 4, 1, 2 }") && band.Contains("public const float NearPx = 2f;") && band.Contains("foreach (var pass in near ? NearPasses : Passes)"));
            // C16-15 (b): the first item's need measured at 15 px BEFORE the preferred size's widths (Width reads the label's size as Widths last
            // set it), passed as NeedW; Seg's own piece and the passes need max(MinWidth, NeedW); the [why] line's ', the widest stretch'
            string hint = Src("RerollHint.cs");
            int need = why == null ? -1 : why.IndexOf("float needW = FirstNeed(block, items, minFont);", StringComparison.Ordinal);
            int pref = why == null ? -1 : why.IndexOf("var widths = Widths(items, font, out float leadW, out float sepW, block.Lead);", StringComparison.Ordinal);
            Check("S", "C16-15: WhyUi measures the first item at 15 px (FirstNeed: the short \"goes first\" when the block has one, ScreenBand.NeedWidth) before the preferred size's widths and passes NeedW; ScreenBand's Seg and passes need max(MinWidth, NeedW), the band as before when no piece holds it; the line's height constants (RerollHint: Font 40, LineH 62) the stacking check assumes",
                why != null && need > 0 && pref > need && why.Contains("TextUnits = total, ChromeUnits = chrome, NeedW = needW,") && why.Contains("return ScreenBand.NeedWidth(leadW, w[0], chrome, minFont);")
                && why.Contains("if (!block.SideItems.SequenceEqual(block.Items))") && why.Contains("(widest ? \", the widest stretch\" : \"\")") && why.Contains("spot.Bands, 0, 0, 0, shortForm, spot.Widest);")
                && band != null && band.Contains("if (b.Value.W < need) continue;") && band.Contains("p.Value - p.Key >= need).ToList();") && band.Contains("if (need <= MinWidth) return Lay(x, MinWidth);")
                && !band.Contains("b.Value.W < MinWidth") && hint != null && hint.Contains("const float Font = 40f, LineH = 62f,"));
        }
    }
}
