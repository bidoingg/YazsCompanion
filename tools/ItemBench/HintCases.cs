// 0.14.0 (roadmap items 3 and 5 of the 10-05 review): WHY on highlight, CLOSE CALL and the action hints, section 13 of the bench:
//   H1  the user's logged chests replayed through ScreenCall: the hint speaks on the best 1.98 (10-05 18:19:31, rerolled by
//       hand), stays silent on 2.51 / 2.76 / 2.78 (10-04 21:19:22 / 21:15:30 / 21:01:16, rerolled by hand - under the 0.14.0
//       chest floor of 2.5 the choice stays the player's: at the plan's 3.0 the hint spoke on 70 of the 147 logged chests, 65 of
//       them taken as offered) and on the two 3.82 chests.
//   H2  the floors per screen (a chest 2.5, a level-up 3.5 / 2.5 over the run, a training and a Research Pod 2.0), "(2 rerolls
//       left)" when few are, nothing without a reroll; SKIP when every card would hurt the squad or the skip bonus beats weak
//       cards, a quest that wants no item; BANISH off by default, never a card the game will not banish or the quest names; one
//       hint at most, never a BANISH on the recommended card; the rescue screen's verdict carried unchanged; every line in the
//       card font's glyphs.
//   W1  CLOSE CALL under 0.20 (the 10-05 offer at 00:18: Medical Drone 4.75, Experiment 21 4.72).
//   W2  that offer's three WHY bands ("Either works - ..." on the close call), a quest-lifted weapon and a quest AVOID (the
//       10-05 'Heroic Theory' run), a build order the first card stands lower in, and a sweep of every kind of card: the reasons
//       never repeat the card's own line, at most four, each within the rails (64 characters), the "vs #1" sentence for every
//       card but a lone one.
//   W3  the packing of the band's lines.
//   B1  the band manager (ScreenBand): the hint's place on the PC's rescue screen as 0.12.2 drew it (band 111 units), on the
//       Steam Deck, crowded; a SKIP line on the level-up screen grows left from the Skip button, a BANISH line stops short of
//       the team panel; the WHY band on the PC's level-up screen as measured on the user's mark of 10-05 19:12 (four buttons,
//       the divider rule and its diamond) and on the Steam Deck modelled from it - never over a card's lines, a button, the
//       divider, the hint's line or the team panel, text 15 px or more, and nowhere when there is no room.
//   S   the sources: the hooks (each card class's OnSelected, ONE OnDeselected, not the shared empty bodies), the config, the
//       menu row, the offer path, the csproj links.
// 0.16.0 (pure, also in --no-data): H3 the revive guard (C16-13); H4 the short words of a line drawn under its button at 1280 x 800
//   (C16-15, DK-C04): every ScreenCall.Short set where Words is, shorter than it (BANISH keeps its words), within the rails; the spec's
//   forms ('a weak offer (2 left)', 'Quest: no items', 'every card hurts', 'the heal and cash are worth more', 'the cash is worth more');
//   Wording.RerollShort and FromRescue's short words; the line's width on the hint's model (956 -> 484 units on a chest); the sources
//   (RerollHint.Place draws the short words only under the button - the PC's line over it keeps the long ones).
// Generic names only (the repository is public): the game's card and class names, the bench's own build names.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class HintCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string F(double v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }
        static string Proj() { return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ItemBench.csproj")); }

        /// <summary>0.16.0: the pure hint cases (C16-13 H3 the revive guard; C16-15 H4 the short words under the button) - in the full bench
        /// (from Run) and in --no-data (Verdict.DataFree).</summary>
        public static int RunPure()
        {
            int was = _bad; _bad = 0;
            Console.WriteLine("\n=== 0.16.0: the action hints' pure cases (C16-13 the revive guard, C16-15 the short words under the button)");
            ReviveGuard();
            ReviveSources();
            ShortWords();
            ShortSources();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            int bad = _bad; _bad = was;
            return bad;
        }

        public static int Run(List<ProbeItem> items)
        {
            int pure = RunPure();
            _bad = 0;
            Console.WriteLine("\n=== 0.14.0: the action hints (ScreenCall), WHY on highlight and CLOSE CALL (WhyText), the band manager (ScreenBand)");
            Chests();
            Floors();
            Skips();
            Banishes();
            Rescue();
            Close();
            Offer0018();
            Sweep(items);
            Packing();
            HintPlace();
            WhyPlace();
            Sources();
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad + pure;
        }

        // ---------------------------------------------------------------- H3: the revive guard (C16-13, DK-C01)
        // The 10-07 Deck chest (companion.log 20261007_195304 :796-801) on its LOGGED 0.15.0 scores: REROLL under the 2.5 floor, the user
        // rerolled and Jewel of Life went away about 2 minutes before the defeat on a Survive quest. The guard holds the REROLL and every
        // SKIP back while a second life is on the cards and the quest asks to survive the run or the squad is under 60 % health; a BANISH
        // never names one; the no-item quest's SKIP keeps its place. The inputs are research's prototype's (proto_rv\Gap.cs R5).
        static HintCard Kr(string name, double score, int rank, bool revive = false, string kind = "item") { return new HintCard { Name = name, Score = score, Rank = rank, Kind = kind, Revive = revive }; }
        static ScreenCallIn DeckChest(double progress, double health, bool quest, int rerolls, params HintCard[] cards)
        {
            var x = new ScreenCallIn { Screen = "Chest", Progress = progress, CanReroll = rerolls > 0, Rerolls = rerolls, RerollsText = rerolls + " (bench)", CanSkip = true, SkipCash = 705, SkipHeal = 320,
                Health = health, Survival = 0.96, Cash = 0.4, SurviveQuest = quest };
            x.Cards.AddRange(cards);
            return x;
        }
        static string Said(ScreenCall c) { return (c.Show ? ScreenCall.Name(c.Action) + " '" + c.Words + "'" : "silent") + " | " + c.Why; }

        static void ReviveGuard()
        {
            Func<bool, HintCard[]> live = revive => new[] { Kr("Frozen Heart", 2.45, 1), Kr("Last Unicorn", 2.30, 2), Kr("Pickup Pick", 2.27, 3), Kr("Jewel of Life", 1.27, 4, revive) };
            var a = ScreenCall.Decide(DeckChest(0.326, 0.55, true, 7, live(true)));
            Check("H3", "the 10-07 Deck chest as live (squad health 55 %, a survive quest): silent - 'held back: Jewel of Life is a second life (a survive quest, squad health 55%)'",
                !a.Show && a.Why == "best Frozen Heart 2.45 under the chest floor 2.50 - held back: Jewel of Life is a second life (a survive quest, squad health 55%)", Said(a));
            var b = ScreenCall.Decide(DeckChest(0.326, 0.55, false, 7, live(true)));
            var q = ScreenCall.Decide(DeckChest(0.326, 1.0, true, 7, live(true)));
            Check("H3", "health 55 % without the quest, the quest at full health: silent, held back for the one reason",
                !b.Show && b.Why.EndsWith("held back: Jewel of Life is a second life (squad health 55%)") && !q.Show && q.Why.EndsWith("held back: Jewel of Life is a second life (a survive quest)"), Said(b) + " || " + Said(q));
            var full = ScreenCall.Decide(DeckChest(0.326, 1.0, false, 7, live(true)));
            var hp62 = ScreenCall.Decide(DeckChest(0.326, 0.62, false, 7, live(true)));
            var old = ScreenCall.Decide(DeckChest(0.326, 0.55, true, 7, live(false)));
            Check("H3", "full health and no quest, 62 % and no quest, and the 0.15.0 inputs (no Revive flag): REROLL as live :801",
                full.Show && full.Action == HintAction.Reroll && hp62.Show && hp62.Action == HintAction.Reroll && old.Show && old.Action == HintAction.Reroll
                && full.Why == "best Frozen Heart 2.45 under the chest floor 2.50 | rerolls 7 (bench)", Said(full));
            var replaced = ScreenCall.Decide(DeckChest(0.326, 0.55, true, 6, Kr("Biofuel Energy", 1.85, 1), Kr("Skip Rope", 1.59, 2), Kr("Teddy Bear", 1.50, 3), Kr("Hit Tracks", 1.18, 4)));
            Check("H3", "the replaced chest (:809-812, no revive on it) at 55 % with the quest: REROLL as live :814 - the guard is narrow", replaced.Show && replaced.Action == HintAction.Reroll && !replaced.Why.Contains("revive"), Said(replaced));
            var wk = DeckChest(0.6, 0.5, false, 0, Kr("A Cookie", 1.30, 1), Kr("Jewel of Life", 1.20, 2, true)); wk.Survival = 1.3;
            var w = ScreenCall.Decide(wk);
            var wk2 = DeckChest(0.6, 0.5, false, 0, Kr("A Cookie", 1.30, 1), Kr("Glass of Milk", 1.20, 2)); wk2.Survival = 1.3;
            var w2 = ScreenCall.Decide(wk2);
            Check("H3", "weak cards with Jewel of Life, no reroll, 50 %: no SKIP ('| revive guard: ...'); the same without the revive: SKIP for the heal and the cash (worth 2.15)",
                !w.Show && w.Why.EndsWith(" | revive guard: Jewel of Life is a second life (squad health 50%)") && w2.Show && w2.Action == HintAction.Skip && w2.Words == "the heal and the cash are worth more than these cards" && Math.Abs(w2.SkipValue - 2.15) < 1e-9,
                Said(w) + " || " + Said(w2));
            var avoid = DeckChest(0.6, 0.5, false, 0, Kr("Potato", 0.80, 1), Kr("Jewel of Life", 0.70, 2, true));
            var av = ScreenCall.Decide(avoid);
            Check("H3", "every card AVOID with a revive among them at 50 %: no SKIP either", !av.Show && av.Why.Contains("revive guard"), Said(av));
            var ni = DeckChest(0.3, 0.5, true, 8, Kr("Jewel of Life", 0.4, 1, true), Kr("Pills", 0.4, 2)); ni.NoItems = "take no items";
            var n = ScreenCall.Decide(ni);
            Check("H3", "a no-item quest at 50 % with Jewel of Life: SKIP 'Quest: taking any item fails it' (kept: any item fails that quest for good)", n.Show && n.Action == HintAction.Skip && n.Words == "Quest: taking any item fails it", Said(n));
            var ban = DeckChest(0.5, 0.5, false, 0, Kr("Bloody Axe", 3.2, 1), Kr("Jewel of Life", 0.9, 2, true), Kr("Potato", 0.8, 3)); ban.Banish = true; ban.CanBanish = true; ban.Banishes = 2; ban.CanSkip = false;
            var bn = ScreenCall.Decide(ban);
            var ban2 = DeckChest(0.5, 1.0, false, 0, Kr("Bloody Axe", 3.2, 1), Kr("Jewel of Life", 0.7, 2, true), Kr("Potato", 0.8, 3)); ban2.Banish = true; ban2.CanBanish = true; ban2.Banishes = 2; ban2.CanSkip = false;
            var bn2 = ScreenCall.Decide(ban2);
            Check("H3", "BANISH on: Potato, never the revive (at 50 %, and at full health with the revive the lowest card)",
                bn.Show && bn.Action == HintAction.Banish && bn.Target.Name == "Potato" && bn2.Show && bn2.Target.Name == "Potato", Said(bn) + " || " + Said(bn2));
            Func<double, ScreenCallIn> lv = hp =>
            {
                var x = new ScreenCallIn { Screen = "LevelUp", Progress = 0.3, CanReroll = true, Rerolls = 6, RerollsText = "6 (bench)", CanSkip = true, SkipCash = 300, SkipHeal = 200, Health = hp, Survival = 0.95, Cash = 0.3 };
                x.Cards.Add(Kr("Handgun", 3.30, 1, false, "weapon")); x.Cards.Add(Kr("Resuscitation", 3.20, 2, true, "ability")); x.Cards.Add(Kr("Stimpack", 3.00, 3, false, "ability"));
                return x;
            };
            var l50 = ScreenCall.Decide(lv(0.5)); var l100 = ScreenCall.Decide(lv(1.0));
            Check("H3", "a level-up early with a new Resuscitation (3.20 under the 3.5 floor): silent at 50 % ('held back: Resuscitation is a second life (squad health 50%)'), REROLL at full health",
                !l50.Show && l50.Why.EndsWith("held back: Resuscitation is a second life (squad health 50%)") && l100.Show && l100.Action == HintAction.Reroll, Said(l50) + " || " + Said(l100));
            // H1's 10-04 21:19:22 chest: Jewel of Life 1.41 at full health stays silent (best 2.51 over the floor), and with the quest too
            var h1 = new ScreenCallIn { Screen = "Chest", Progress = 0.90, CanReroll = true, Rerolls = 8, RerollsText = "8 (bench)", CanSkip = true, SkipCash = 120, SkipHeal = 40, Health = 1, Survival = 1, Cash = 0.3 };
            h1.Cards.AddRange(new[] { Kr("Ruby Gem", 2.51, 1), Kr("Mouse Trap", 1.78, 2), Kr("Jewel of Life", 1.41, 3, true), Kr("Icon of Tempest", 1.30, 4) });
            var h1a = ScreenCall.Decide(h1); h1.SurviveQuest = true; var h1b = ScreenCall.Decide(h1);
            Check("H3", "H1's 21:19:22 chest with Jewel of Life flagged: silent as before, with or without the quest", !h1a.Show && !h1b.Show && !h1a.Why.Contains("revive") && h1b.Why.Contains("revive guard"), Said(h1b));
            // Q03 (no answer yet: the chest floor stays 2.5): the guard holds whatever the floor - a best of 2.44 under a 2.45 floor would still speak
            Check("H3", "the chest floor is 2.5 until Q03 is answered (2.45 proposed); the guard's threshold is the revive need's (ItemRules.ReviveHurt 0.6)",
                ScreenCall.ChestFloor == 2.5 && ScreenCall.ReviveHealth == ItemRules.ReviveHurt && ScreenCall.ReviveHealth == 0.6);
            // the held-item SKIPs (C16-01b / f) are held back the same way
            var rb = DeckChest(0.3, 0.5, false, 0, Kr("Frozen Heart", 2.37, 1), Kr("Jewel of Life", 2.10, 2, true)); rb.SkipLevelUps = 1;
            var rbc = ScreenCall.Decide(rb); rb.Cards[1].Revive = false; var rbn = ScreenCall.Decide(rb);
            var sr = DeckChest(0.3, 1.0, true, 0, Kr("Teddy Bear", 1.40, 1), Kr("Jewel of Life", 1.20, 2, true)); sr.SkipBonus = 1.39; sr.RopePoints = 5;
            var src = ScreenCall.Decide(sr); sr.SurviveQuest = false; var srn = ScreenCall.Decide(sr);
            Check("H3", "Reserve Bench's level-up SKIP and Skip Rope's SKIP are held back the same way (and show without the revive / the state)",
                !rbc.Show && rbc.Why.Contains("revive guard") && rbn.Show && rbn.Action == HintAction.Skip && !src.Show && src.Why.Contains("revive guard") && srn.Show && srn.Action == HintAction.Skip,
                Said(rbc) + " || " + Said(src));
            Check("H3", "the guard's words: game names only, the health floored like C16-12's ('squad health 55%')", ScreenCall.Guard(DeckChest(0, 0.5999, false, 0, Kr("Plot Armor", 1, 1, true))) == "Plot Armor is a second life (squad health 59%)"
                && ScreenCall.Guard(DeckChest(0, 0.6, false, 0, Kr("Plot Armor", 1, 1, true))) == null && ScreenCall.Guard(DeckChest(0, 0.3, true, 0, Kr("Plot Armor", 1, 1))) == null);
        }

        static void ReviveSources()
        {
            string hint = Src("RerollHint.cs"), call = Src("ScreenCall.cs");
            Check("H3", "RerollHint.Inputs0 sets Revive from ItemBook.Revive / ItemBook.ReviveAbilities (a new ability only) and SurviveQuest from the run context",
                hint != null && hint.Contains("k.Revive = c.Item != null ? ItemBook.Revive(c.Name)") && hint.Contains("w.Level == 0 && ItemBook.ReviveAbilities.Contains(c.Name") && hint.Contains("x.SurviveQuest = s.Ctx.SurviveQuest;"));
            Check("H3", "ScreenCall: the REROLL held back, both weak-card SKIPs and the held-item SKIPs gated by the guard, '|| k.Revive' in the banish skip list",
                call != null && call.Contains("held != null) c.Why = under + \" - held back: \" + held;") && Regex.Matches(call, @"if \(held == null && x\.Skip && x\.CanSkip").Count == 3 && call.Contains("|| k.Revive ||"));
        }

        // ---------------------------------------------------------------- H4: the short words under the button (C16-15, DK-C04)
        // At 1280 x 800 the band over the button is 63 units (the 10-07 Deck round, companion.log 20261007_195304 :431 / :471 / :803): the line
        // goes under its button, into the WHY band's row - 961 x 74 units on the chests, 934 on the rescue screen - and the band beside it
        // lost its room (a rescue card had no WHY at all). There the line says its short words (GeometryCases B3: the band's piece widens by
        // as much); over the button (the PC since 0.12.2) the long words stay.
        static void ShortWords()
        {
            var said = new List<Tuple<string, ScreenCall>>();
            Func<string, ScreenCallIn, ScreenCall> run = (name, x) => { var c = ScreenCall.Decide(x); said.Add(Tuple.Create(name, c)); return c; };
            Func<ScreenCall, string> say = c => (c.Show ? ScreenCall.Name(c.Action) + " '" + c.Words + "' / '" + c.Short + "'" : "silent");
            // REROLL: 'a weak offer', the count as the long words give it
            Func<int, ScreenCallIn> pipe = n => In("Chest", 0.32, n, K("Detective's Pipe", 1.98, 1), K("Frying Pan", 1.92, 2), K("Teddy Bear", 1.50, 3));
            var r8 = run("REROLL, 8 rerolls", pipe(8)); var r2 = run("REROLL, 2 left", pipe(2)); var r1 = run("REROLL, 1 left", pipe(1));
            var free = In("Chest", 0.5, 0, K("Frying Pan", 2.0, 1)); free.CanReroll = true; free.FreeReroll = true; var rf = run("REROLL, a FREE reroll", free);
            Check("H4", "REROLL's short words: 'a weak offer', ' (2 left)' / ' (1 left)' where the long words count the rerolls left, nothing for a FREE reroll",
                r8.Action == HintAction.Reroll && r8.Short == "a weak offer" && r2.Short == "a weak offer (2 left)" && r2.Words.EndsWith("(2 rerolls left)") && r1.Short == "a weak offer (1 left)" && rf.Short == "a weak offer",
                say(r8) + " | " + say(r2) + " | " + say(r1) + " | free " + say(rf));
            // SKIP: the no-item quest, every card AVOID (C16-01c: for an empty slot), the weak cards
            var nq = In("Chest", 0.3, 8, K("Pills", 0.4, 1), K("Potato", 0.4, 2)); nq.NoItems = "take no items";
            var q = run("SKIP, a quest that wants no item", nq);
            var av = run("SKIP, every card AVOID", In("LevelUp", 0.5, 0, K("Medical Drone", 0.85, 1, "ability"), K("Stimpack", 0.6, 2, "ability")));
            var slot = In("Chest", 0.3, 0, K("Emerald Gem", 0.35, 1), K("Ruby Gem", 0.30, 2)); slot.SlotBy = "Wooden Stick"; foreach (var k in slot.Cards) k.HeldCost = 2.17;
            var sl = run("SKIP, every card AVOID for an empty slot (Wooden Stick)", slot);
            var hurt = In("Chest", 0.6, 0, K("Teddy Bear", 1.3, 1), K("Golden Key", 1.1, 2)); hurt.Health = 0.6; hurt.Survival = 1.3;
            var wh = run("SKIP, the heal and the cash", hurt);
            var rich = In("Chest", 0.6, 0, K("Teddy Bear", 1.2, 1), K("Golden Key", 1.1, 2)); rich.Cash = 2.5;
            var wc = run("SKIP, the cash", rich);
            Check("H4", "SKIP's short words: 'Quest: no items', 'every card hurts' ('an empty slot is worth more' with Wooden Stick), 'the heal and cash are worth more', 'the cash is worth more'",
                q.Action == HintAction.Skip && q.Short == "Quest: no items" && av.Action == HintAction.Skip && av.Short == "every card hurts" && sl.Action == HintAction.Skip && sl.Short == "an empty slot is worth more"
                && wh.Action == HintAction.Skip && wh.Short == "the heal and cash are worth more" && wc.Action == HintAction.Skip && wc.Short == "the cash is worth more",
                say(q) + " | " + say(av) + " | " + say(sl) + " | " + say(wh) + " | " + say(wc));
            // the held items' SKIPs (C16-01b / e / f)
            var rb = In("Chest", 0.3, 0, K("Frozen Heart", 2.37, 1), K("Pickup Pick", 2.27, 2)); rb.SkipLevelUps = 1;
            var b = run("SKIP, Reserve Bench's level-up", rb);
            var ls = In("Chest", 0.6, 0, K("Teddy Bear", 1.3, 1), K("Golden Key", 1.1, 2)); ls.Health = 0.6; ls.Survival = 1.3; ls.CashHeals = true;
            var l = run("SKIP, Life Savings: the cash heals", ls);
            var ck = In("Chest", 0.6, 0, K("Teddy Bear", 1.40, 1), K("Golden Key", 1.1, 2)); ck.Health = 0.7; ck.Survival = 0.8; ck.ExtraHeal = 500;
            var co = run("SKIP, A Cookie's heal", ck);
            var rp = In("Chest", 0.6, 0, K("Teddy Bear", 1.40, 1), K("Golden Key", 1.1, 2)); rp.SkipBonus = 1.39; rp.RopePoints = 5;
            var ro = run("SKIP, Skip Rope's training point (a chest)", rp);
            var rpl = In("LevelUp", 0.6, 0, K("Handgun", 1.40, 1, "weapon"), K("Stimpack", 1.1, 2, "ability")); rpl.SkipBonus = 1.39; rpl.RopePoints = 1;
            var rol = run("SKIP, Skip Rope's point (a level-up)", rpl);
            Check("H4", "the held items' SKIPs: 'the skip is a level-up' (Reserve Bench), 'the heal is worth more' (Life Savings; A Cookie), 'a training point is worth more' / 'a Skip Rope point is worth more' (Skip Rope)",
                b.Action == HintAction.Skip && b.Short == "the skip is a level-up" && l.Short == "the heal is worth more" && co.Words.Contains("(A Cookie)") && co.Short == "the heal is worth more"
                && ro.Words.Contains("training point") && ro.Short == "a training point is worth more" && rol.Words.Contains("Skip Rope point") && rol.Short == "a Skip Rope point is worth more",
                say(b) + " | " + say(l) + " | " + say(co) + " | " + say(ro) + " | " + say(rol));
            // BANISH (off by default) keeps its words; a silent screen has neither
            var bn = In("LevelUp", 0.4, 8, K("Experiment 21", 4.7, 1, "ability"), new HintCard { Name = "Medical Drone", Score = 3.9, Rank = 2, Kind = "ability", Class = "Medic", Skipped = true, Build = "Bench Anchor" });
            bn.Banish = true; bn.CanBanish = true; bn.Banishes = 4;
            var ba = run("BANISH (switched on)", bn);
            var silent = run("silent (best 3.82 over the chest floor)", In("Chest", 0.93, 8, K("Bloody Axe", 3.82, 1), K("Acoustic Guitar", 2.51, 2)));
            Check("H4", "BANISH keeps its words as its short form; a silent screen has neither", ba.Action == HintAction.Banish && ba.Short == ba.Words && !silent.Show && silent.Words == null && silent.Short == null, say(ba));
            // every screen above: the short words set where the words are, shorter (BANISH's are its words), within the rails
            var bad = new List<string>();
            foreach (var s in said)
            {
                var c = s.Item2;
                if ((c.Words == null) != (c.Short == null)) { bad.Add(s.Item1 + ": the words and the short words not set together"); continue; }
                if (c.Short == null) continue;
                if (c.Action != HintAction.Banish && c.Short.Length >= c.Words.Length) bad.Add(s.Item1 + ": '" + c.Short + "' not shorter than '" + c.Words + "'");
                var rails = Wording.Rails(c.Short); if (rails.Count > 0) bad.Add(s.Item1 + ": '" + c.Short + "' (" + string.Join(", ", rails) + ")");
            }
            Check("H4", "every Short set where Words is (" + said.Count + " screens, " + said.Count(s => s.Item2.Short != null) + " with a line), shorter than Words but BANISH's, within the rails (" + Wording.Budget + " characters, the card font's glyphs, no score)",
                bad.Count == 0 && said.Count(s => s.Item2.Short != null) == said.Count - 1, string.Join(" | ", bad));
            // the rescue screen (RerollHint.LineText: the long and the short form; the quest's form keeps its words)
            Func<string, double, Recruit> r = (n, sc) => new Recruit { Name = n, Score = sc, Class = n.Length };
            var call = RerollCall.Decide(new[] { r("Ranger", 3.66) }, 1.0, new[] { r("Huntress", 6.08), r("SWAT", 5.9) }, true, "4", 1.0);
            var both = ScreenCall.FromRescue(call, "REROLL  -  " + Wording.Reroll(new[] { "Huntress", "SWAT" }, 0), "REROLL  -  " + Wording.RerollShort(new[] { "Huntress", "SWAT" }));
            var quest = ScreenCall.FromRescue(call, "REROLL  -  the quest needs Huntress");
            var quiet = ScreenCall.FromRescue(new RerollCall { Show = false, Why = "the best survivor is on the cards" }, "REROLL  -  x", "REROLL  -  y");
            Check("H4", "the rescue screen: Wording.RerollShort 'Huntress fits better' (the first survivor alone, 'another survivor' for none); FromRescue carries the short words, the quest's form (no short words given) its own; nothing when silent",
                Wording.RerollShort(new[] { "Huntress", "SWAT" }) == "Huntress fits better" && Wording.RerollShort(new[] { "Tank" }) == "Tank fits better" && Wording.RerollShort(new string[0]) == "another survivor fits better" && Wording.RerollShort(null) == "another survivor fits better"
                && call.Show && both.Short == "REROLL  -  Huntress fits better" && both.Words == "REROLL  -  Huntress or SWAT would fit this squad better" && quest.Short == quest.Words
                && !quiet.Show && quiet.Words == null && quiet.Short == null && Wording.Rails("Huntress fits better").Count == 0 && Wording.Rails("another survivor fits better").Count == 0,
                "'" + both.Words + "' / '" + both.Short + "' | quest '" + quest.Short + "'");
            // the line's width on the hint's model (research's why_sim: its characters x 0.339 em x 48 units - the line's font at x1.20 - plus
            // the tips and pads 1.2 x (36 + 2 x 26) + 4; the 10-07 Deck logs: 961 x 74 on the chests, 934 on the rescue screen)
            Func<string, double> width = t => t.Length * 0.339 * 48 + 1.2 * (36 + 2 * 26) + 4;
            string chestL = "REROLL  -  " + r8.Words, chestS = "REROLL  -  " + r8.Short, sosL = "REROLL  -  " + Wording.Reroll(new[] { "Huntress" }, 0), sosS = "REROLL  -  " + Wording.RerollShort(new[] { "Huntress" });
            Check("H4", "the line's width on the hint's model: a chest " + F0(width(chestL)) + " -> " + F0(width(chestS)) + " units, the rescue screen " + F0(width(sosL)) + " -> " + F0(width(sosS)) + " (the spec's 956 -> 484 and 874 -> 614; the WHY band's piece widens by as much: GeometryCases B3)",
                F0(width(chestL)) == "956" && F0(width(chestS)) == "484" && F0(width(sosL)) == "874" && F0(width(sosS)) == "614", "'" + chestL + "' / '" + chestS + "' | '" + sosL + "' / '" + sosS + "'");
        }
        static string F0(double v) { return Math.Round(v).ToString("0", System.Globalization.CultureInfo.InvariantCulture); }

        static void ShortSources()
        {
            string hint = Src("RerollHint.cs"), call = Src("ScreenCall.cs");
            int words = call == null ? -1 : Regex.Matches(call, @"\bc\.Words = ").Count, shorts = call == null ? -2 : Regex.Matches(call, @"\bc\.Short = ").Count;
            Check("H4", "ScreenCall sets a Short beside every Words (" + words + " / " + shorts + ")", call != null && words >= 10 && words == shorts && call.Contains("public string Short;"));
            Check("H4", "RerollHint keeps both texts (_text, _short) and Place draws the short words only when the line goes under its button (the PC's over it keeps the long), the first draw's log line adding ', short form'; the frame on the button untouched",
                hint != null && hint.Contains("_text = text; _short = brief;") && hint.Contains("string drawn = spot.Where == \"under\" && _short != null ? _short : _text;") && hint.Contains("text.text = drawn;") && !hint.Contains("text.text = _text;")
                && hint.Contains("(brief ? \", short form\" : \"\")") && hint.Contains("var frame = Frame(brt);"));
            Check("H4", "the rescue line in both forms: LineText(r, false) / LineText(r, true) into FromRescue, Wording.RerollShort for the short one, the quest's form unchanged",
                hint != null && hint.Contains("ScreenCall.FromRescue(r, r.Show ? LineText(r, false) : null, r.Show ? LineText(r, true) : null)") && hint.Contains("(brief ? Wording.RerollShort(who) : Wording.Reroll(who, ")
                && hint.Contains("if (c.ForQuest) return head + \"the quest needs \""));
        }

        // ---------------------------------------------------------------- H1: the logged chests
        static HintCard K(string name, double score, int rank, string kind = "item") { return new HintCard { Name = name, Score = score, Rank = rank, Kind = kind }; }
        static ScreenCallIn In(string screen, double progress, int rerolls, params HintCard[] cards)
        {
            var x = new ScreenCallIn { Screen = screen, Progress = progress, CanReroll = rerolls > 0, Rerolls = rerolls, RerollsText = rerolls + " (bench)", CanSkip = true, SkipCash = 120, SkipHeal = 40, Health = 1, Survival = 1, Cash = 0.3 };
            x.Cards.AddRange(cards);
            return x;
        }

        static void Chests()
        {
            var logged = new[]
            {
                Tuple.Create("10-05 18:19:31 Hardcore 03:09", 0.32, In("Chest", 0.32, 8, K("Detective's Pipe", 1.98, 1), K("Frying Pan", 1.92, 2), K("Teddy Bear", 1.50, 3), K("Golden Key", 1.19, 4)), true),
                Tuple.Create("10-04 21:19:22 Normal 17:54", 0.90, In("Chest", 0.90, 8, K("Ruby Gem", 2.51, 1), K("Mouse Trap", 1.78, 2), K("Jewel of Life", 1.41, 3), K("Icon of Tempest", 1.30, 4)), false),
                Tuple.Create("10-04 21:15:30 Normal 15:41", 0.78, In("Chest", 0.78, 8, K("The Bomb", 2.76, 1), K("Bleeding Edge", 2.54, 2), K("Acoustic Guitar", 2.51, 3), K("Farming Tools", 1.30, 4)), false),
                Tuple.Create("10-04 21:01:16 Normal 04:48", 0.24, In("Chest", 0.24, 8, K("Solar Panel", 2.78, 1), K("Bloody Axe", 2.52, 2), K("Broken Glass", 2.08, 3), K("Ultra Instinct", 1.20, 4)), false),
                Tuple.Create("10-05 18:27:36 Hardcore 09:16", 0.93, In("Chest", 0.93, 8, K("Bloody Axe", 3.82, 1), K("Acoustic Guitar", 2.51, 2), K("Pickup Pick", 1.86, 3), K("Torchlight", 1.84, 4)), false),
                Tuple.Create("10-04 16:26:37 Normal 15:22", 0.77, In("Chest", 0.77, 8, K("Nine Inch Nails", 3.82, 1), K("Frying Pan", 2.00, 2), K("Buckshot Roulette", 1.33, 3), K("Rock And Roll", 1.07, 4)), false),
            };
            foreach (var l in logged)
            {
                var c = ScreenCall.Decide(l.Item3);
                bool ok = l.Item4 ? c.Show && c.Action == HintAction.Reroll : !c.Show;
                Check("H1", "chest " + l.Item1 + " best " + F(c.Best.Score) + (l.Item4 ? " -> REROLL" : " -> silent"), ok,
                    (c.Show ? ScreenCall.Name(c.Action) + " '" + c.Words + "'" : "silent") + " | " + c.Why);
            }
        }

        // ---------------------------------------------------------------- H2: floors, counts, skip, banish, rescue
        static void Floors()
        {
            Func<string, double, double, int, ScreenCall> one = (screen, progress, best, rerolls) => ScreenCall.Decide(In(screen, progress, rerolls, K("A", best, 1, "x"), K("B", Math.Max(1.2, best - 0.4), 2, "x")));
            Check("H2", "level-up: 3.0 speaks early (floor 3.5), not late (2.5)", one("LevelUp", 0.3, 3.0, 8).Show && !one("LevelUp", 0.7, 3.0, 8).Show,
                "early floor " + F(ScreenCall.FloorOf("LevelUp", 0.3)) + ", late " + F(ScreenCall.FloorOf("LevelUp", 0.7)));
            Check("H2", "military 1.9 speaks, 2.1 not (floor 2.0); a Research Pod the same; the rescue screen has no floor here",
                one("Military", 0.5, 1.9, 8).Show && !one("Military", 0.5, 2.1, 8).Show && one("Hashtag", 0.5, 1.9, 8).Show && !one("Hashtag", 0.5, 2.1, 8).Show && ScreenCall.FloorOf("SOS", 0.5) == 0);
            var two = one("Chest", 0.5, 2.0, 2); var once = one("Chest", 0.5, 2.0, 1); var three = one("Chest", 0.5, 2.0, 3);
            var free = In("Chest", 0.5, 0, K("A", 2.0, 1), K("B", 1.6, 2)); free.CanReroll = true; free.FreeReroll = true; var fc = ScreenCall.Decide(free);
            Check("H2", "'(2 rerolls left)' / '(1 reroll left)' when few are; nothing for 3; a FREE reroll counts nothing",
                two.Words.EndsWith("(2 rerolls left)") && once.Words.EndsWith("(1 reroll left)") && !three.Words.Contains("left") && fc.Show && !fc.Words.Contains("left"),
                "'" + two.Words + "' | '" + once.Words + "' | '" + three.Words + "' | free '" + fc.Words + "'");
            var none = one("Chest", 0.5, 2.0, 0);
            Check("H2", "no reroll left: no REROLL (and 2.0 is no skip)", !none.Show, none.Why);
            var off = In("Chest", 0.5, 8, K("A", 2.0, 1)); off.Reroll = false;
            Check("H2", "the Reroll action switched off: silent", !ScreenCall.Decide(off).Show);
        }

        static void Skips()
        {
            var avoid = In("LevelUp", 0.5, 0, K("A", 0.85, 1, "ability"), K("B", 0.6, 2, "ability"), K("C", 0.4, 3, "ability"));
            var a = ScreenCall.Decide(avoid);
            Check("H2", "every card AVOID, no reroll: SKIP", a.Show && a.Action == HintAction.Skip && a.Words.StartsWith("every card here would hurt"), "'" + a.Words + "'");
            var avoidR = In("LevelUp", 0.5, 4, K("A", 0.85, 1, "ability"), K("B", 0.6, 2, "ability"));
            var ar = ScreenCall.Decide(avoidR);
            Check("H2", "every card AVOID with a reroll left: REROLL first", ar.Show && ar.Action == HintAction.Reroll, ScreenCall.Name(ar.Action) + " " + ar.Why);
            var hurt = In("Chest", 0.6, 0, K("A", 1.3, 1), K("B", 1.1, 2)); hurt.Health = 0.6; hurt.Survival = 1.3;
            var h = ScreenCall.Decide(hurt);
            Check("H2", "weak cards, the squad at 60 %: SKIP for the heal and the cash", h.Show && h.Action == HintAction.Skip && h.Words.Contains("heal") && h.SkipValue > 1.3, "worth " + F(h.SkipValue) + " '" + h.Words + "'");
            var full = In("Chest", 0.6, 0, K("A", 1.3, 1), K("B", 1.1, 2)); full.Cash = 0.15;
            var f = ScreenCall.Decide(full);
            Check("H2", "weak cards at full health on a run to win (cash weighs 0.15): silent", !f.Show, "skip worth " + F(f.SkipValue));
            var skipOff = In("LevelUp", 0.5, 0, K("A", 0.5, 1)); skipOff.Skip = false;
            Check("H2", "the Skip action switched off: silent", !ScreenCall.Decide(skipOff).Show);
            var noItems = In("Chest", 0.3, 8, K("A", 0.4, 1), K("B", 0.4, 2)); noItems.NoItems = "take no items";
            var ni = ScreenCall.Decide(noItems);
            Check("H2", "a quest that wants no item: SKIP the chest (even with rerolls) - 'Quest: taking any item fails it'", ni.Show && ni.Action == HintAction.Skip && ni.Words == "Quest: taking any item fails it", "'" + ni.Words + "'");
            Check("H2", "no skip button: no SKIP", !ScreenCall.Decide(new Func<ScreenCallIn>(() => { var x = In("LevelUp", 0.5, 0, K("A", 0.5, 1)); x.CanSkip = false; return x; })()).Show);
        }

        static void Banishes()
        {
            Func<bool, ScreenCallIn> mk = on =>
            {
                var x = In("LevelUp", 0.4, 8, K("Experiment 21", 4.7, 1, "ability"), new HintCard { Name = "Medical Drone", Score = 3.9, Rank = 2, Kind = "ability", Class = "Medic", Skipped = true, Build = "Bench Anchor" },
                    new HintCard { Name = "Handgun", Score = 3.7, Rank = 3, Kind = "weapon", Class = "Medic" });
                x.Banish = on; x.CanBanish = true; x.Banishes = 4; return x;
            };
            var off = ScreenCall.Decide(mk(false));
            Check("H2", "BANISH off by default ([Advice] ActionHints = Reroll, Skip)", !off.Show);
            var on = ScreenCall.Decide(mk(true));
            Check("H2", "BANISH on: the card the build skips", on.Show && on.Action == HintAction.Banish && on.Target != null && on.Target.Name == "Medical Drone", "'" + on.Words + "'");
            var notB = mk(true); notB.Cards[1].Banishable = false;
            Check("H2", "never a card the game will not banish", !ScreenCall.Decide(notB).Show);
            var q = new QuestRules(); var full = new QuestRule { Ask = QuestAsk.FullClass, Class = "Medic" }; q.List.Add(full);
            var prot = mk(true); prot.Quest = q;
            Check("H2", "never a card the active quest names (FullyUpgradeClass of its class)", !ScreenCall.Decide(prot).Show && ScreenCall.Protected(q, prot.Cards[1]));
            var head = mk(true); head.Cards[1].Head = "quest: max the tier-3 weapon";
            Check("H2", "never a card a quest line decided", !ScreenCall.Decide(head).Show);
            var weak = In("LevelUp", 0.4, 8, K("A", 2.0, 1), K("B", 1.5, 2)); weak.Banish = true; weak.CanBanish = true; weak.Banishes = 4;
            var w = ScreenCall.Decide(weak);
            Check("H2", "one hint at most: a REROLL screen says nothing of a banish", w.Show && w.Action == HintAction.Reroll && w.Target == null);
            // the integration review (R5): a build-skipped ability can still lead a weak offer - never the card framed RECOMMENDED
            var lead = In("LevelUp", 0.7, 0, new HintCard { Name = "Medical Drone", Score = 2.9, Rank = 1, Kind = "ability", Class = "Medic", Skipped = true, Build = "Bench Anchor" },
                K("Handgun", 2.6, 2, "weapon"), K("Shotgun", 2.4, 3, "weapon"));
            lead.Banish = true; lead.CanBanish = true; lead.Banishes = 3;
            var ld = ScreenCall.Decide(lead);
            Check("H2", "BANISH never names the recommended card (a build-skipped #1 on a weak offer): silent here", !ld.Show || (ld.Target != null && ld.Target.Rank != 1), ld.Show ? ScreenCall.Name(ld.Action) + " " + (ld.Target != null ? ld.Target.Name : "") : ld.Why);
        }

        static void Rescue()
        {
            Func<string, double, Recruit> r = (n, s) => new Recruit { Name = n, Score = s, Class = n.Length };
            var call = RerollCall.Decide(new[] { r("Ranger", 3.66) }, 1.0, new[] { r("Huntress", 6.08), r("SWAT", 5.9) }, true, "4", 1.0);
            var sc = ScreenCall.FromRescue(call, "REROLL - words");
            Check("H2", "the rescue screen: RerollCall carried as it is (shown, its log reason, its words)", sc.Show == call.Show && sc.Why == call.Why && sc.Words == "REROLL - words" && sc.Action == HintAction.Reroll && sc.Rescue == call);
            var lines = new List<string>();
            foreach (var x in new[] { In("Chest", 0.3, 2, K("A", 2.0, 1)), In("LevelUp", 0.5, 0, K("A", 0.5, 1)), In("Chest", 0.3, 0, K("A", 1.2, 1)) })
            {
                x.Health = 0.5; x.Survival = 1.4; x.Banish = true; x.CanBanish = true; x.Banishes = 2;
                var c = ScreenCall.Decide(x); if (c.Words != null) lines.Add(c.Words);
            }
            var nq = In("Chest", 0.3, 2, K("A", 0.2, 1)); nq.NoItems = "take no items"; lines.Add(ScreenCall.Decide(nq).Words);
            // 0.15.0: the longest evolution name of 1.0.2 (1.0.1's longest, "Hunter's Whistle: Panic Whistle", is "Animal Whistle: Panic" now)
            var bn = In("LevelUp", 0.4, 0, K("A", 4.0, 1), new HintCard { Name = "Automatic Turret: Provocation", Score = 0.5, Rank = 2, Skipped = true, Build = "A Build Named Twenty1" }); bn.Banish = true; bn.CanBanish = true; bn.Banishes = 1; lines.Add(ScreenCall.Decide(bn).Words);
            var bad = lines.Where(l => l == null || l.Length > ScreenCall.Budget || Wording.Rails(l).Any(b => !b.StartsWith("length"))).ToList();
            Check("H2", "every action line within the rails (" + ScreenCall.Budget + " characters, the card font's glyphs, no score)", bad.Count == 0 && lines.Count == 5, string.Join(" | ", lines));
        }

        // ---------------------------------------------------------------- W1 / W2: CLOSE CALL, the 00:18 offer
        static void Close()
        {
            Check("W1", "CLOSE CALL: 4.75 vs 4.72 (the 10-05 offer at 00:18) yes, 4.75 vs 4.55 no (0.20 apart), 4.75 vs 4.56 yes, a missing score no",
                WhyText.Close(4.75, 4.72) && !WhyText.Close(4.75, 4.55) && WhyText.Close(4.75, 4.56) && !WhyText.Close(4.75, double.NaN));
        }

        const string A = "Bench Anchor";
        static void Offer0018()
        {
            // 18:02:59 LevelUp 00:18 (Hardcore, Medic leader, Kinetic 3/10 from the Handgun): #1 Medical Drone 4.75 (new, #2 in the build,
            // Kinetic 100 % of the damage), #2 Experiment 21 4.72 (new, #1 in the build), #3 Handgun 3.72 (abilities first)
            var drone = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Build = A, Priority = 1, HeadRank = 4, Head = Wording.Role(A, 1), EvoExists = true, Reach = 1, Owned = 0, Clock = "9:41 left", ShareType = "Kinetic", SharePct = 100 };
            var exp = new CardWords { Kind = SayKind.Ability, Level = 0, Max = 4, Build = A, Priority = 0, HeadRank = 4, Head = Wording.Role(A, 0), EvoExists = true, Reach = 1, Owned = 0, Clock = "9:41 left" };
            var gun = new CardWords { Kind = SayKind.Weapon, Level = 1, Max = 4, Style = BuildStyle.Ability, Build = A, Reach = 1, Clock = "9:41 left", ShareType = "Kinetic", SharePct = 100 };
            string sDrone = Wording.Card(drone, 1, "Medical Drone", "Experiment 21"), sExp = Wording.Card(exp, 2, "Medical Drone", "Experiment 21"), sGun = Wording.Card(gun, 3, "Medical Drone", "Experiment 21");
            Func<string, int, double, string, CardWords, List<string>, WhyBlock> block = (name, rank, score, shown, w, why) => WhyText.Block(new WhyIn
            {
                Name = name, Rank = rank, Score = score, Shown = shown, Say = w, Why = why,
                FirstName = "Medical Drone", FirstScore = 4.75, FirstShown = sDrone, SecondName = "Experiment 21", SecondScore = 4.72,
            });
            var b1 = block("Medical Drone", 1, 4.75, sDrone, drone, new List<string> { "new ability - #2 in " + A, "take each ability once early", "Skill Tree 3/5", "+1 Kinetic tag: 100% of the squad's damage (Handgun)" });
            var b2 = block("Experiment 21", 2, 4.72, sExp, exp, new List<string> { "new ability - #1 in " + A, "take each ability once early", "Skill Tree 3/5" });
            var b3 = block("Handgun", 3, 3.72, sGun, gun, new List<string> { "weapon level: 1 to 2 of 4", "style: abilities first", "+1 Kinetic tag: 100% of the squad's damage (Handgun)", "Skill Tree 3/3" });
            foreach (var b in new[] { Tuple.Create("#1 Medical Drone", sDrone, b1), Tuple.Create("#2 Experiment 21", sExp, b2), Tuple.Create("#3 Handgun", sGun, b3) })
                Console.WriteLine("    " + b.Item1 + " under the card '" + b.Item2 + "' -> " + b.Item3.Lead + "  " + string.Join("  /  ", b.Item3.Items));
            Check("W2", "00:18: CLOSE CALL on #1 and #2, not on #3", b1.Close && b2.Close && !b3.Close && b1.Lead == "CLOSE CALL" && b3.Lead == "WHY");
            Check("W2", "00:18: #1 'Either works - a hair ahead of Experiment 21'; #2 'Either works - Medical Drone is a hair ahead' (no argument for an order the scores barely make); #3 'Medical Drone goes first: <its line>'",
                b1.Versus == "Either works - a hair ahead of Experiment 21" && b2.Versus == "Either works - Medical Drone is a hair ahead" && b3.Versus == "Medical Drone goes first: " + sDrone,
                "'" + b1.Versus + "' | '" + b2.Versus + "' | '" + b3.Versus + "'");
            Check("W2", "00:18: the reasons never repeat the card's own line; the weapon names the build's style and the Kinetic share",
                !b1.Reasons.Contains(sDrone) && !b2.Reasons.Contains(sExp) && !b3.Reasons.Contains(sGun) && b3.Reasons.Contains("The " + A + " build levels abilities first") && b3.Reasons.Contains("Kinetic is 100% of your damage") && b1.Reasons.Contains("Kinetic is 100% of your damage"),
                string.Join(" | ", b3.Reasons));
            Check("W2", "00:18: the log-only 'Skill Tree 3/5' stays out (the game's own label shows it on the selected card)", !b1.Items.Concat(b2.Items).Concat(b3.Items).Any(i => i.Contains("Skill Tree 3")));
        }

        // ---------------------------------------------------------------- W2: every kind of card
        static void Sweep(List<ProbeItem> items)
        {
            var bad = new List<string>(); int cards = 0, withReason = 0, blocks = 0; var samples = new Dictionary<string, string>();
            Action<string, CardWords, List<string>, int> one = (kind, w, why, rank) =>
            {
                cards++;
                string shown = Wording.Card(w, rank, "Medical Drone", "Experiment 21") ?? "";
                var b = WhyText.Block(new WhyIn { Name = "Card", Rank = rank, Score = rank == 1 ? 5.0 : 4.0, Shown = shown, Say = w, Why = why, FirstName = "Medical Drone", FirstScore = 5.0, FirstShown = "The Rifleman build's main ability", SecondName = "Experiment 21", SecondScore = 4.0 });
                blocks++;
                if (b.Reasons.Count > 0) withReason++;
                if (b.Reasons.Count > WhyText.MaxReasons) bad.Add(kind + ": " + b.Reasons.Count + " reasons");
                if (b.Versus == null) bad.Add(kind + ": no vs sentence");
                foreach (var it in b.Items)
                {
                    if (!WhyText.Fits(it, it == b.Versus ? WhyText.VersusBudget : WhyText.Budget)) bad.Add(kind + ": '" + it + "' (" + string.Join(", ", Wording.Rails(it)) + ")");
                    if (it == shown) bad.Add(kind + ": repeats its own line '" + it + "'");
                }
                if (!samples.ContainsKey(kind) && b.Reasons.Count >= 2) samples[kind] = "'" + shown + "' -> " + string.Join(" / ", b.Items);
            };
            foreach (var build in new[] { null, "Rifleman", "A Build Named Twenty1" })
                foreach (int lvl in new[] { 0, 1, 3 })
                    foreach (int evo in new[] { 0, 1, 2 })
                        foreach (int head in new[] { 0, 1, 3, 4, 5, 6 })
                            foreach (double reach in new[] { 0.3, 1.0 })
                            {
                                string[] hd = head == 0 ? null : head == 6 ? Wording.Skips(build ?? "Rifleman") : head == 5 ? Wording.Special("Kinetic") : head == 4 ? Wording.Role(build ?? "Rifleman", 0) : head == 3 ? Wording.Synergy("Tank") : Wording.Tier("A");
                                var w = new CardWords { Kind = SayKind.Ability, Level = lvl, Max = 4, Build = build, Priority = build == null ? -1 : 2, HeadRank = head, Head = hd, EvoExists = evo > 0, EvoOwned = evo == 2, Reach = reach, Clock = "2:10 left", Owned = lvl == 0 ? 2 : 3, Focus = lvl == 3,
                                    ShareType = "Kinetic", SharePct = 44, ShortType = head == 1 ? "Ice" : null, Short = 2, Boost = head == 3 ? "Trap Expertise" : null };
                                one("ability", w, new List<string> { "synergy if you recruit Pyro", "healing: survival matters now", "Skill Tree 2/5" }, lvl == 3 ? 1 : 2);
                            }
            foreach (var style in new[] { BuildStyle.Ability, BuildStyle.Balanced, BuildStyle.Weapon })
                foreach (int lvl in new[] { 1, 3 })
                    foreach (bool lift in new[] { false, true })
                        one("weapon", new CardWords { Kind = SayKind.Weapon, Level = lvl, Max = 4, Style = style, Build = "Bombardier", Lifted = lift, Next = lvl == 3 ? "Pump-Action Shotgun" : null, Reach = 0.4, Clock = "4:20 left", ShareType = "Kinetic", SharePct = 61, Special = lvl == 1 ? "Kinetic" : null },
                            new List<string> { "abilities first: after Medical Drone, ahead of the rest" }, lift ? 2 : 3);
            foreach (bool mine in new[] { true, false })
                one("evolution", new CardWords { Kind = SayKind.Evolution, Base = "Bombing Strike", Build = "Bombardier", Pick = "Bombing Strike: Bioweapon", Mine = mine, PickOffered = !mine, Adds = "Chemical", AddsNew = true, ShareType = "Explosive", SharePct = 55 }, new List<string>(), 1);
            one("next tier", new CardWords { Kind = SayKind.NextTier, Branch = "type", BranchType = "Kinetic", BranchWith = "Handgun", Special = "Kinetic" }, new List<string>(), 1);
            one("first weapon", new CardWords { Kind = SayKind.FirstWeapon }, new List<string>(), 1);
            // items: the real item rules over two squads
            var k = Knowledge.FromJson(Knowledge.DefaultJson);
            var t1 = new TagProfile { SpecialAt = 10 }; t1.Source("Assault Rifle", 1.0, new[] { "Kinetic" }); t1.Source("Tesla", 0.7, new[] { "Electric" }); t1.Points["Kinetic"] = 8; t1.Points["Electric"] = 4;
            var t2 = new TagProfile { SpecialAt = 10 }; t2.Source("Experiment 21", 0.5, new[] { "Explosive", "Chemical", "Ice" }); t2.Source("Handgun", 0.4, new[] { "Kinetic" }); t2.Points["Kinetic"] = 16; t2.Points["Explosive"] = 9;
            var ctxs = new[]
            {
                new ItemContext { Squad = new[] { "SWAT", "Engineer" }, Tags = t1, K = k, Ctx = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 120, LevelRate = 3, D = new Doctrine() }, OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Deployable" }, Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase), CloseShare = 0.5, ClipShare = 1, AbilityLean = 0.4, CritSquad = true },
                new ItemContext { Squad = new[] { "Medic", "Tank" }, Tags = t2, K = k, Ctx = new RunContext { Mode = "Hardcore", Goal = 600, Seconds = 274, LevelRate = 3, Health = 0.4, D = new Doctrine() }, OwnedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Grenade" }, Held = new HashSet<string>(StringComparer.OrdinalIgnoreCase), LongShare = 0.5, AbilityLean = 0.6 },
            };
            ctxs[0].Wants.Add(new KeyValuePair<string, string>("your Rifleman build", "critical"));
            foreach (var c in ctxs)
                foreach (var it in items)
                {
                    var say = new ItemSay(); c.Stats = it.Stats; c.Healing = it.Healing; c.Say = say;
                    var why = new List<string>(); ItemRules.Evaluate(it.Name, it.Desc, c, why); c.Say = null;
                    one("item", new CardWords { Kind = SayKind.Item, Item = say }, why, 2);
                }
            foreach (var t in TagProfile.Names) one("research pod", new CardWords { Kind = SayKind.Tags, Type = t, Points = 2, Tags = t2 }, new List<string>(), 2);
            one("stat", new CardWords { Kind = SayKind.Stat, Stat = "ability area", Team = false, StatWhy = Wording.StatNote("abilities", null), WantBuild = "Shield Anchor" }, new List<string>(), 2);
            one("rescue", new CardWords { Kind = SayKind.Recruit, Recruit = new Func<RecruitSay>(() => { var r = new RecruitSay { Tier = "A", Bought = 1, SharedType = "Kinetic", SharedWith = "Handgun", TeamBonus = "armor", Unbought = 1 }; r.Partners.Add("Medic"); return r; })() }, new List<string>(), 2);
            one("liberate", new CardWords { Kind = SayKind.Recruit, Recruit = new RecruitSay { Liberate = true, Late = "1:10 left" } }, new List<string>(), 3);
            foreach (var s in samples.OrderBy(kv => kv.Key)) Console.WriteLine("    " + s.Key + ": " + s.Value);
            Check("W2", "the sweep: " + cards + " cards of every kind, " + withReason + " with a reason beyond their own line; every item within the rails, none repeats the line, at most four, a vs sentence each",
                bad.Count == 0 && withReason * 2 > cards, bad.Count > 0 ? string.Join(" | ", bad.Take(6)) : null);
            var lone = WhyText.Block(new WhyIn { Name = "A", Rank = 1, Score = 3, Shown = "x" });
            Check("W2", "a lone card: no vs sentence", lone.Versus == null);
            Check("W2", "the decisive part of a line: after a level or a kind, else up to its dash",
                WhyText.Decisive("Level 3 of 4 - evolution unlocks at level 4") == "evolution unlocks at level 4" && WhyText.Decisive("Mixed: boosts Ice, weakens Kinetic") == "Mixed: boosts Ice, weakens Kinetic"
                && WhyText.Decisive("Boosts Explosive - your Rocket Launcher deals it") == "Boosts Explosive" && WhyText.Decisive("Max level - next tier: Tesla") == "next tier: Tesla");
            Check("W2", "the log reasons that read plainly translated, the rest left out",
                WhyText.FromLog("synergy if you recruit Pyro") == "a synergy unlocks if you recruit Pyro" && WhyText.FromLog("A-tier ability in the guides") == "A-tier in the guides"
                && WhyText.FromLog("Skill Tree 3/5") == null && WhyText.FromLog("style: abilities first") == null && WhyText.FromLog("+1 Kinetic tag: 100% of the squad's damage (Handgun)") == null);
        }

        // ---------------------------------------------------------------- W3: the packing
        static void Packing()
        {
            var w = new List<float> { 300, 300, 300, 900, 200 };
            var one = WhyText.Pack(w, 100, 40, 1500, 1);
            var two = WhyText.Pack(w, 100, 40, 1500, 2);
            var tooWide = WhyText.Pack(new List<float> { 2000, 300 }, 0, 40, 1500, 2);
            Check("W3", "Pack: in order, a separator between items, the lead on the first line, overflow to the next line, then dropped",
                one.Count == 1 && one[0].SequenceEqual(new[] { 0, 1, 2 }) && two.Count == 2 && two[1].SequenceEqual(new[] { 3, 4 }) && tooWide.Count == 1 && tooWide[0].SequenceEqual(new[] { 1 }),
                "1 line: " + string.Join(",", one[0]) + " | 2 lines: " + string.Join(" ; ", two.Select(l => string.Join(",", l))) + " | too wide: " + string.Join(" ; ", tooWide.Select(l => string.Join(",", l))));
        }

        // ---------------------------------------------------------------- B1: the band manager
        // The PC's level-up screen in view units (3840 x 2160, y down; 0.667 px a unit, the view 660 units in from the canvas's left),
        // as measured on the user's mark of 10-05 19:12 (20261005_191231_m5_1.png, 3440 x 1440, three cards, the 0.13.0 badges at
        // x1): the cards 640 x 1126 (the 832 x 1462 root at 0.77) centred at y 1140 and x 582 / 1527 / 2472 (the game's Powerups
        // row sits 390 units left of the view's centre); the hovered card's reason line down to 1857 (1833 at rest: the 0.12.2 logs'
        // 111-unit band); the game's divider rule over the buttons at 1902 - 1906, x 127 - 2965, its diamond centred at 1528 (the
        // Decor rect: 128 units at the Buttons row's x1.47, about 188 square - the visible diamond is 54); four action buttons at
        // 1977 - 2078 (Reroll 196 - 774, Lockdown 892 - 1471, Banish 1593 - 2170, Skip 2293 - 2872), the Reroll and Banish count
        // plates down to 2109; the team panel at x 3003 - 3790, y 92 - 2026. The Steam Deck (1280 x 800: canvas 3840 x 2400, 0.333 px
        // a unit) is modelled from it: the badges at x1.3 push the reason lines about 40 units lower.
        sealed class Geo { public string Name; public R4 View; public float Bottom, CardsBottom, UnitPx; public R4[] Cards, Buttons, Rules, Decor; public R4 Panel; public float Anchor; }
        static readonly R4 Reroll = R4.Box(196, 1977, 578, 101), Banish = R4.Box(1593, 1977, 577, 101), Skip = R4.Box(2293, 1977, 579, 101);
        static Geo Pc(bool hover)
        {
            var cards = new[] { R4.Box(262, 577, 640, 1126), R4.Box(1207, 577, 640, 1126), R4.Box(2152, 577, 640, 1126) };
            float lowest = hover ? 1857f : 1833f;                // the selected card's lines, grown with its hover animation
            var buttons = new[] { Reroll, R4.Box(892, 1977, 579, 101), Banish, Skip, R4.Box(280, 2050, 56, 59), R4.Box(1676, 2050, 56, 59) };
            return new Geo { Name = "3440x1440", View = new R4(0, 0, 3840, 2160), Bottom = 2160, CardsBottom = lowest, UnitPx = 1440f / 2160f, Cards = cards, Buttons = buttons,
                Rules = new[] { new R4(127, 1902, 2965, 1906) }, Decor = new[] { R4.Box(1434, 1810, 188, 188) }, Panel = new R4(3003, 92, 3790, 2026), Anchor = 1527 };
        }
        static Geo Deck(bool hover, bool frameHidden)
        {
            var s = Pc(hover);
            s.Name = "1280x800" + (frameHidden ? "" : " (the game's frame shown)");
            s.CardsBottom = hover ? 1903f : 1872f;               // the badge lines at x1.3
            s.UnitPx = 800f / 2400f;
            s.Bottom = frameHidden ? 2280f : 2160f;               // the canvas reaches 120 units under the view
            return s;
        }

        static void HintPlace()
        {
            var pc = Pc(false); var reroll = R4.Box(196, 1944, 578, 100);         // the rescue screen's Reroll button (the 0.12.2 logs: 111 units under the cards)
            var a = ScreenBand.Hint(pc.CardsBottom, reroll, reroll.Y0, 2160, 62f);
            Check("B1", "the hint on the PC's rescue screen as 0.12.2 drew it: over the Reroll button, full size, centred in the 111-unit band",
                a.Where == "over" && Math.Abs(a.K - 1f) < 1e-4 && Math.Abs(a.Top - (1833f + (111f - 62f) / 2f)) < 0.01f && Math.Abs(a.Band - 111f) < 0.01f, "top " + a.Top + ", band " + a.Band);
            var deck = Deck(false, true); var d = ScreenBand.Hint(1865f, reroll, reroll.Y0, 2280, 62f * 1.3f);      // the rescue screen's badge lines at x1.3: 79 units over its Reroll button
            Check("B1", "the Steam Deck (x1.3, band 79): over the button, shrunk to fit", d.Where == "over" && d.K < 1f && d.K >= 0.72f && d.Top >= 1865f && d.Top + 62f * 1.3f * d.K <= reroll.Y0 + 0.01f, "k " + d.K.ToString("0.00"));
            var crowded = ScreenBand.Hint(1930f, reroll, reroll.Y0, 2160, 62f);
            Check("B1", "the cards down to the button: under the button (it fits the 116 units below)", crowded.Where == "under" && crowded.Top >= reroll.Y1, "below " + crowded.Below);
            var none = ScreenBand.Hint(2080f, R4.Box(600, 2100, 560, 50), 2100, 2160, 62f);
            Check("B1", "no room over or under: the frame alone", none.Where == null);

            // R1 (the integration review): the line's side. On the level-up screen Skip is the right-most button, next to the team panel:
            // up to then the line grew right from the button's left edge, into the panel's Danger Level box
            var lv = Pc(true); var panels = new List<R4> { lv.Panel };
            var sk = ScreenBand.Hint(lv.CardsBottom, Skip, Skip.Y0, 2160, 62f);
            float w; float x0 = ScreenBand.HintX(Skip, 1400f, 1920f, sk.Top, sk.Top + 62f * sk.K, panels, out w);
            var skLine = new R4(x0, sk.Top, x0 + w, sk.Top + 62f * sk.K);
            Check("B1", "a 1400-unit SKIP line on the level-up screen grows left from the Skip button's right edge, clear of the team panel (up to then: right, to x 3693, 690 units into it)",
                sk.Where == "over" && Math.Abs(x0 + w - Skip.X1) < 0.01f && Math.Abs(w - 1400f) < 0.01f && !skLine.Overlaps(lv.Panel) && !R4.Box(Skip.X0, sk.Top, 1400f, 62f).Empty && Skip.X0 + 1400f > lv.Panel.X0,
                skLine.ToString());
            float wb; float xb = ScreenBand.HintX(Banish, 1600f, 1920f, sk.Top, sk.Top + 62f, panels, out wb);
            Check("B1", "a 1600-unit BANISH line (the button left of the centre) grows right and stops short of the team panel", xb == Banish.X0 && wb < 1600f && xb + wb <= lv.Panel.X0 - ScreenBand.Gap + 0.01f, "x " + xb + " w " + wb);
            float wr; float xr = ScreenBand.HintX(reroll, 900f, 1920f, a.Top, a.Top + 62f, panels, out wr);
            Check("B1", "the rescue screen's Reroll line as since 0.12.2: from the button's left edge, its full width", xr == reroll.X0 && Math.Abs(wr - 900f) < 0.01f);
        }

        static WhyBandIn Band(Geo s, R4 hint, float prefPx, int want)
        {
            var obstacles = new List<R4> { s.Panel }; obstacles.AddRange(s.Decor); if (!hint.Empty) obstacles.Add(hint);
            float x0 = Math.Min(s.Cards.Min(c => c.X0), s.Buttons.Min(b => b.X0)), x1 = Math.Max(s.Cards.Max(c => c.X1), s.Buttons.Max(b => b.X1));
            return new WhyBandIn { View = s.View, Bottom = s.Bottom, CardsBottom = s.CardsBottom, SpanX0 = x0, SpanX1 = x1, Buttons = s.Buttons, Obstacles = obstacles.ToArray(), Rules = s.Rules, AnchorX = s.Anchor,
                FontUnits = Math.Max(ScreenBand.MinPx, prefPx) / s.UnitPx, MinFontUnits = ScreenBand.MinPx / s.UnitPx, Want = want };
        }

        static void WhyPlace()
        {
            var hintLine = new R4(196, 1886, 1100, 1948);                 // a REROLL line over the Reroll button, across the divider as 0.12.2 drew it
            var cases = new[]
            {
                Tuple.Create(Pc(false), new R4(), 20.5f, 1, "the PC, nothing selected yet"),
                Tuple.Create(Pc(true), new R4(), 20.5f, 1, "the PC, a card hovered"),
                Tuple.Create(Pc(true), hintLine, 20.5f, 2, "the PC with a REROLL line, two lines wanted"),
                Tuple.Create(Deck(true, true), new R4(), 10.3f, 1, "the Steam Deck (frame hidden)"),
                Tuple.Create(Deck(true, true), hintLine, 10.3f, 2, "the Steam Deck with a REROLL line, two lines wanted"),
            };
            foreach (var c in cases)
            {
                var x = Band(c.Item1, c.Item2, c.Item3, c.Item4);
                var spot = ScreenBand.Why(x);
                bool ok = spot.At != "none" && Clear(spot, x, c.Item1) && spot.Font * c.Item1.UnitPx >= ScreenBand.MinPx - 0.01f && spot.Lines >= 1;
                Check("B1", "WHY band, " + c.Item5 + ": " + spot.At + ", " + spot.Lines + " line(s) at " + (spot.Font * c.Item1.UnitPx).ToString("0.#") + " px, clear of the cards, the buttons, the divider, the hint and the panel", ok,
                    spot.Seg.ToString() + " " + spot.Seg.Size + " | " + spot.Bands);
            }
            var shown = ScreenBand.Why(Band(Deck(true, false), new R4(), 10.3f, 1));
            Check("B1", "the Steam Deck with the game's frame shown (WideMenus off) and a hovered card: nowhere rather than over something", shown.At == "none" || Clear(shown, Band(Deck(true, false), new R4(), 10.3f, 1), Deck(true, false)), shown.At + " | " + shown.Bands);
            Check("B1", "the block's left edge stays inside its stretch", ScreenBand.Left(new R4(100, 0, 900, 50), 50, 400) == 100 && ScreenBand.Left(new R4(100, 0, 900, 50), 880, 400) == 500 && ScreenBand.Left(new R4(100, 0, 900, 50), 500, 400) == 300);
        }

        // the band's block (its height at the lines laid out) is clear of the cards' lines, every button and every obstacle, inside the view's visible part
        static bool Clear(WhySpot spot, WhyBandIn x, Geo s)
        {
            float h = ScreenBand.BlockHeight(spot.Lines, spot.Font);
            float top = spot.Seg.Y0 + Math.Max(0f, (spot.Seg.H - h) / 2f);
            var block = new R4(spot.Seg.X0, top, spot.Seg.X1, top + h);
            if (block.Y0 < x.CardsBottom || block.Y1 > x.Bottom || block.X0 < 0 || block.X1 > s.View.X1) return false;
            foreach (var b in x.Buttons) if (block.Overlaps(b)) return false;
            foreach (var o in x.Obstacles) if (block.Overlaps(o)) return false;
            foreach (var r in x.Rules ?? new R4[0]) if (block.Overlaps(r)) return false;
            return h <= spot.Seg.H + 0.01f;
        }

        // ---------------------------------------------------------------- S: the sources
        static void Sources()
        {
            string why = Src("WhyUi.cs"), hint = Src("RerollHint.cs"), advisor = Src("Advisor.cs"), plugin = Src("Plugin.cs"), menu = Src("Menu.cs"), ranker = Src("Ranker.cs"), proj = File.Exists(Proj()) ? File.ReadAllText(Proj()) : null;
            Check("S", "the WHY band follows each card class's own OnSelected and exactly one OnDeselected (the skill card's: the body every card class shares)",
                why != null && why.Contains("AccessTools.DeclaredMethod(t, \"OnSelected\", Type.EmptyTypes)") && why.Contains("AccessTools.DeclaredMethod(typeof(UIPowerupButtonSkill), \"OnDeselected\", Type.EmptyTypes)")
                && Regex.Matches(why, "OnDeselected\", Type.EmptyTypes").Count == 1 && why.Contains("static MethodBase TargetMethod() { return WhyUi.DeselectTarget(); }"));
            Check("S", "the deselect post-fix reads the card's pointer only", why != null && Regex.IsMatch(why, @"public static void Deselected\(UIPowerupButtonBase b\)\s*\{\s*var p = Ptr\(b\);"));
            Check("S", "no detour on the shared empty bodies: the base SetActionButtonsInteractivity, the Research Pod screen's ProcessBanish",
                hint != null && !hint.Contains("typeof(UIGameplayUpgradeSelection), nameof(UIGameplayUpgradeSelection.SetActionButtonsInteractivity)") && !Regex.IsMatch(hint, @"typeof\(UIGameplayHashtagEvent\)[^\n]*ProcessBanish")
                && hint.Contains("typeof(UIGameplayLevelUp), nameof(UIGameplayLevelUp.SetActionButtonsInteractivity)"));
            Check("S", "the rescue screen keeps its words (Wording.Reroll) and its own switch; every screen goes through the hint and the WHY band",
                hint != null && hint.Contains("Wording.Reroll(") && advisor != null && advisor.Contains("RerollHint.OnOffer(sel, screen, cards, snap, before != null);") && !advisor.Contains("if (screen == Screen.SOS) RerollHint.OnOffer")
                && advisor.Contains("WhyUi.OnOffer(") && advisor.Contains("WhyUi.Close();") && advisor.Contains("WhyUi.Forget();") && advisor.Contains("WhyUi.Tick();"));
            Check("S", "[Advice] ActionHints (Reroll, Skip by default - no banish), [General] ShowWhy (on), RerollHint kept",
                plugin != null && plugin.Contains("Config.Bind(\"Advice\", \"ActionHints\", HintActions.Reroll | HintActions.Skip,") && plugin.Contains("Config.Bind(\"General\", \"ShowWhy\", true,") && plugin.Contains("Config.Bind(\"Advice\", \"RerollHint\", true,"));
            Check("S", "the ADVICE tab's hints row (one row for the rescue screen and the others; the rows in the tab's scroller)", menu != null && menu.Contains("\"ad:hints\"") && menu.Contains("Scroll(\"advice\", x, y, w, RowsRoom(y, rh, step), false, 40f") && menu.Contains("static void HintsStep(int d)"));
            Check("S", "the ability cards carry their tag facts for the band (the card's line unchanged: it reads the head)", ranker != null && ranker.Contains("Wording.Tags(say, G.Facts(p), s.Tags, s.Boosts, ctx);"));
            Check("S", "WhyText.cs, ScreenCall.cs and ScreenBand.cs are compiled into the bench (pure: no game types)",
                proj != null && proj.Contains("WhyText.cs") && proj.Contains("ScreenCall.cs") && proj.Contains("ScreenBand.cs"));
        }
    }
}
