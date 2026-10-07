// 0.15.0 (C15-07, the user's decision Q3 of 2026-10-06): a lent build's level-up style - the build's own stays the default, the cards and
// the PLAN readout say so, an ADVICE row "Lent builds' level-up style: the build's own / mine" switches it, the cfg text says when
// LevelUpStyle applies. Up to 0.14.0 a build another mod lends, followed through Auto, brought its own style silently while the cfg
// said LevelUpStyle was for survivors on Auto (live 10-06: '[ctx] ... (Ability, Auto)' under the user's Balanced).
//   R1  Builds.StyleFor: plain Auto follows LevelUpStyle, a build the player selected its own, a lent build Auto follows its own
//       (LentBuildStyle = BuildsOwn, the default) or the player's (Mine); Builds.LentStyleSaid: the words say so where the two differ.
//   R2  BuildsOwn reproduces 0.14.0's card order exactly: every build source x build style x the player's style gets the old rule's
//       style and weapon floor, and the live 10-06 19:18:27 hand orders as logged (Handgun 3.65 first).
//   R3  Mine with a Balanced LevelUpStyle orders the lent "abilities first" hand exactly as a Balanced survivor's (the weapon on the
//       balanced floor, a missing ability's "once early" lift: Synergy.OnceEarly / WithOnce, Ranker.ScoreAbility's own).
//   R4  the words: the cards ("abilities first, the build's style"), the WHY band ("..., the <B> build's own style"), the player's own
//       ("your style levels ..."), the rails at the card's room; the readout, the [ctx] line, the ADVICE row and the cfg text (sources).
//   R5  Builds.Styled: the badge advice weighs the style the cards follow.
// 0.15.0 (C15-04): the early level-up pace - PaceCases below (section N, after R5): the mode's measured pace blended with the run's own
// and smoothed (LevelPace, Context.cs), replayed over the user's logged runs.
// Generic names only (the repository is public): the lent build is "Bench Anchor"; every card name is the game's own.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YazsCompanion;

namespace YazsCompanion.Bench
{
    static class RankCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }
        static string F(double x) { return x.ToString("0.00", CultureInfo.InvariantCulture); }

        // one card of a hand, scored as Ranker scores it by style: a held weapon's level (Synergy.HeldWeapon on the style's floor - not
        // lifted here), or an ability (a missing one takes Balanced's "once early" lift; Base = its score without it, as logged under
        // "abilities first")
        sealed class HandCard { public string Name; public bool Weapon; public int Level, Max = 4; public double Syn, Base; public bool New; public int Owned = 2; }

        static double Score(HandCard c, BuildStyle style, bool lentAuto, BuildStyle doctrine, double reach10)
        {
            if (c.Weapon) return Math.Round(Synergy.HeldWeapon(new WeaponLift().Floor(style, lentAuto, doctrine), 1.0, c.Level, c.Max, c.Syn), 2);
            return Math.Round(c.New ? Synergy.WithOnce(c.Base, Synergy.OnceEarly(style, c.Owned, false, reach10)) : c.Base, 2);
        }

        // the hand in the order the cards would rank it (an exact tie: the card further left, as Ranker.Rank)
        static string Order(IList<HandCard> hand, BuildStyle style, bool lentAuto, BuildStyle doctrine, double reach10 = 1.0)
        {
            return string.Join(", ", hand.Select((c, i) => Tuple.Create(c.Name, Score(c, style, lentAuto, doctrine, reach10), i)).OrderByDescending(t => t.Item2).ThenBy(t => t.Item3)
                .Select(t => t.Item1 + " " + F(t.Item2)));
        }

        static readonly BuildStyle[] Styles = { BuildStyle.Weapon, BuildStyle.Balanced, BuildStyle.Ability };

        public static int Run()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.15.0: a lent build's level-up style shown and switchable (C15-07, decision Q3) - R1 whose style, R2 BuildsOwn = 0.14.0, R3 Mine orders as Balanced, R4 the words, R5 the badge advice");
            StyleSource from;

            // ---- R1: whose style
            var lent = new Build { Id = "ext:bench:anchor", Survivor = "Medic", Name = "Bench Anchor", Pack = "Bench Pack", Style = BuildStyle.Ability };
            var auto = Builds.StyleFor(null, false, BuildStyle.Balanced, false, out from); var fAuto = from;
            var chosen = Builds.StyleFor(lent, false, BuildStyle.Balanced, true, out from); var fChosen = from;
            var own = Builds.StyleFor(lent, true, BuildStyle.Balanced, false, out from); var fOwn = from;
            var mine = Builds.StyleFor(lent, true, BuildStyle.Balanced, true, out from); var fMine = from;
            Check("R1", "plain Auto: LevelUpStyle; a selected build: its own (Mine changes nothing there); a lent build on Auto: its own, or LevelUpStyle with Mine",
                auto == BuildStyle.Balanced && fAuto == StyleSource.Auto && chosen == BuildStyle.Ability && fChosen == StyleSource.Chosen && own == BuildStyle.Ability && fOwn == StyleSource.Lent
                && mine == BuildStyle.Balanced && fMine == StyleSource.Mine, auto + "/" + fAuto + ", " + chosen + "/" + fChosen + ", " + own + "/" + fOwn + ", " + mine + "/" + fMine);
            Check("R1", "the words say whose only where a lent build's own decides against LevelUpStyle",
                Builds.LentStyleSaid(BuildStyle.Ability, StyleSource.Lent, BuildStyle.Balanced) && !Builds.LentStyleSaid(BuildStyle.Ability, StyleSource.Lent, BuildStyle.Ability)
                && !Builds.LentStyleSaid(BuildStyle.Balanced, StyleSource.Mine, BuildStyle.Balanced) && !Builds.LentStyleSaid(BuildStyle.Weapon, StyleSource.Chosen, BuildStyle.Balanced)
                && !Builds.LentStyleSaid(BuildStyle.Balanced, StyleSource.Auto, BuildStyle.Weapon));

            // ---- R2: BuildsOwn = 0.14.0, exactly. The live 10-06 19:18:27 hand: the leader's Handgun (level 2 of 4, synergy 0.15: 3.65 on the
            // "abilities first" floor), Resuscitation (a missing ability, 4th in the lent build: 3.45), Stimpack (owned: 3.20)
            var hand = new List<HandCard>
            {
                new HandCard { Name = "Handgun", Weapon = true, Level = 2, Syn = 0.15 },
                new HandCard { Name = "Resuscitation", New = true, Base = 3.45 },
                new HandCard { Name = "Stimpack", Base = 3.20 },
            };
            string logged = Order(hand, BuildStyle.Ability, true, BuildStyle.Balanced);
            Check("R2", "BuildsOwn: the live 19:18:27 hand orders as logged (Handgun 3.65, Resuscitation 3.45, Stimpack 3.20)", logged == "Handgun 3.65, Resuscitation 3.45, Stimpack 3.20", logged);
            int combos = 0, same = 0; var differ = new List<string>();
            foreach (int source in new[] { 0, 1, 2 })              // plain Auto, a selected build, a lent build Auto follows
                foreach (var buildStyle in Styles)
                    foreach (var doctrine in Styles)
                        foreach (double reach10 in new[] { 1.0, 0.7, 0.3 })
                        {
                            combos++;
                            var b = source == 0 ? null : new Build { Id = "b", Name = "Bench Anchor", Style = buildStyle, Pack = source == 2 ? "Bench Pack" : "" };
                            bool lentAuto = source == 2;
                            var oldStyle = b != null ? b.Style : doctrine;                      // 0.14.0's Ranker.StyleOf
                            bool oldLentAuto = b != null && lentAuto;                           // 0.14.0's ScoreWeapon: owner.Build != null && OnAuto
                            var st = Builds.StyleFor(b, lentAuto, doctrine, false, out from);
                            bool newLentAuto = from == StyleSource.Lent || from == StyleSource.Mine;
                            string o0 = Order(hand, oldStyle, oldLentAuto, doctrine, reach10), o1 = Order(hand, st, newLentAuto, doctrine, reach10);
                            if (st == oldStyle && newLentAuto == oldLentAuto && o0 == o1) same++;
                            else if (differ.Count < 6) differ.Add(source + "/" + buildStyle + "/" + doctrine + ": " + o0 + " -> " + o1);
                        }
            Check("R2", "BuildsOwn reproduces 0.14.0 exactly: the same style, weapon floor and card order for every build source x build style x LevelUpStyle x clock (" + combos + ")",
                same == combos, same + " of " + combos + (differ.Count > 0 ? " | " + string.Join(" || ", differ) : ""));

            // ---- R3: Mine with a Balanced LevelUpStyle - the lent "abilities first" hand orders as a Balanced survivor's
            var stMine = Builds.StyleFor(lent, true, BuildStyle.Balanced, true, out from);
            string asMine = Order(hand, stMine, from == StyleSource.Lent || from == StyleSource.Mine, BuildStyle.Balanced);
            string asBalanced = Order(hand, BuildStyle.Balanced, false, BuildStyle.Balanced);
            Console.WriteLine("    the 19:18:27 hand: BuildsOwn '" + logged + "' -> Mine (Balanced) '" + asMine + "'");
            Check("R3", "Mine + Balanced: the lent abilities-first hand reorders as balanced - the Handgun on the 4.3 floor, Resuscitation lifted once early - exactly a Balanced survivor's order",
                asMine == asBalanced && asMine != logged && asMine == "Resuscitation 4.95, Handgun 4.65, Stimpack 3.20", asMine + " | Balanced: " + asBalanced);
            var late = Order(hand, stMine, true, BuildStyle.Balanced, 0.3);
            Check("R3", "... late in the run the once-early lift is gone (Reach(10) 0.3): the weapon level goes first under Mine", late == "Handgun 4.65, Resuscitation 3.45, Stimpack 3.20", late);
            var stWeapon = Builds.StyleFor(lent, true, BuildStyle.Weapon, true, out from);
            string asWeapon = Order(hand, stWeapon, true, BuildStyle.Weapon);
            Check("R3", "Mine + WeaponFirst: the weapon level on the 6.0 floor goes first, no once-early lift", stWeapon == BuildStyle.Weapon && asWeapon.StartsWith("Handgun 6.35", StringComparison.Ordinal), asWeapon);
            Check("R3", "the once-early lift is Ranker's (pure since 0.15.0): 1.5 from Reach(10) 1.0, none at 0.4 or under, none with four abilities, skipped or another style; squeezed above 5.6",
                Synergy.OnceEarly(BuildStyle.Balanced, 2, false, 1.0) == 1.5 && Synergy.OnceEarly(BuildStyle.Balanced, 2, false, 0.4) == 0 && Synergy.OnceEarly(BuildStyle.Balanced, 4, false, 1.0) == 0
                && Synergy.OnceEarly(BuildStyle.Balanced, 2, true, 1.0) == 0 && Synergy.OnceEarly(BuildStyle.Ability, 2, false, 1.0) == 0 && Math.Abs(Synergy.OnceEarly(BuildStyle.Balanced, 1, false, 0.7) - 0.75) < 1e-9
                && Math.Abs(Synergy.WithOnce(3.45, 1.5) - 4.95) < 1e-9 && Math.Abs(Synergy.WithOnce(5.0, 1.5) - 5.825) < 1e-9 && Synergy.WithOnce(3.0, 0) == 3.0);

            // ---- R4: the words
            Func<BuildStyle, int, bool, bool, string, int, string> card = (style, rank, lentSaid, mineSaid, build, room) =>
                Wording.Card(new CardWords { Kind = SayKind.Weapon, Level = 2, Max = 4, Style = style, Build = build, Reach = 1, Clock = "12:05 left", LentStyle = lentSaid, MineStyle = mineSaid }, rank, "Medical Drone", "Stimpack", room, null);
            string wl = card(BuildStyle.Ability, 2, true, false, "Bench Anchor", 50), ww = card(BuildStyle.Weapon, 2, true, false, "Bench Anchor", 50), wb = card(BuildStyle.Balanced, 2, true, false, "Bench Anchor", 50);
            string wm = card(BuildStyle.Ability, 2, false, true, "Bench Anchor", 50), wmb = card(BuildStyle.Balanced, 2, false, true, "Bench Anchor", 50), wo = card(BuildStyle.Ability, 2, false, false, "Bench Anchor", 50);
            string w1 = card(BuildStyle.Ability, 1, true, false, "Bench Anchor", 50), wAvoid = card(BuildStyle.Ability, 2, true, false, "Bench Anchor", 48);
            Check("R4", "the cards: a lent build's own style as the build's, the player's standing in as theirs, the rest as in 0.14.0",
                wl == "Level 3 of 4 - abilities first, the build's style" && ww == "Level 3 of 4 - weapons first, the build's style" && wb == "Level 3 of 4 - balanced, the build's style"
                && wm == "Level 3 of 4 - your style levels abilities first" && wmb == "Level 3 of 4 - weapon and abilities side by side" && wo == "Level 3 of 4 - this build levels abilities first"
                && w1 == "Level 3 of 4 - the best of this offer" && wAvoid == "Level 3 of 4 - this build levels abilities first",
                string.Join(" | ", new[] { wl, ww, wb, wm, wmb, wo, w1, wAvoid }));
            var whyLent = WhyText.Reasons(new CardWords { Kind = SayKind.Weapon, Level = 2, Max = 4, Style = BuildStyle.Ability, Build = "Rifleman", Reach = 1, LentStyle = true }, null, wl);
            var whyLong = WhyText.Reasons(new CardWords { Kind = SayKind.Weapon, Level = 2, Max = 4, Style = BuildStyle.Ability, Build = "Bench Anchor", Reach = 1, LentStyle = true }, null, wl);
            var whyMine = WhyText.Reasons(new CardWords { Kind = SayKind.Weapon, Level = 2, Max = 4, Style = BuildStyle.Ability, Build = "Bench Anchor", Reach = 1, MineStyle = true }, null, null);
            var whyAuto = WhyText.Reasons(new CardWords { Kind = SayKind.Weapon, Level = 2, Max = 4, Style = BuildStyle.Ability, Reach = 1 }, null, null);
            // an ability over its own survivor's weapon level because the lent build's own style says abilities first
            var abil = new CardWords { Kind = SayKind.Ability, Level = 1, Max = 4, Build = "Rifleman", Priority = 0, HeadRank = 4, Head = Wording.Role("Rifleman", 0), Style = BuildStyle.Ability, LentStyle = true, EvoExists = true };
            var gun = new CardWords { Kind = SayKind.Weapon, Level = 2, Max = 4, Style = BuildStyle.Ability, Build = "Rifleman", Reach = 1, LentStyle = true };
            Wording.Ranks(new List<CardWords> { abil, gun }, new List<string> { "Electric Turret", "Taser" });
            var whyAbil = WhyText.Reasons(abil, null, Wording.Card(abil, 1, "Electric Turret", "Taser"));
            // 0.15.x (the 10-07 review): one phrase for the idea everywhere - the WHY band said "the <B> build's own style" beside cards saying
            // "the build's style"; now the cards' phrase, and where the name does not fit it is the card's own line, which the band never repeats
            string wlong = card(BuildStyle.Ability, 2, true, false, "A Build Named Twenty1", 50);
            var whyLongest = WhyText.Reasons(new CardWords { Kind = SayKind.Weapon, Level = 2, Max = 4, Style = BuildStyle.Ability, Build = "A Build Named Twenty1", Reach = 1, LentStyle = true }, null, wlong);
            Check("R4", "the WHY band: '..., the <B> build's style' (the cards' phrase; the name dropped past 50 - then it is the card's own line and left out), on the ability above its weapon level too; the player's own: 'your style levels ...' (0.14.0's 'your level-up style: ...' broke the rails and never showed)",
                whyLent.Contains("Abilities first, the Rifleman build's style") && whyLong.Contains("Abilities first, the Bench Anchor build's style") && whyMine.Contains("Your style levels abilities first")
                && whyAuto.Contains("Your style levels abilities first") && whyAbil.Contains("Abilities first, the Rifleman build's style")
                && wlong == "Level 3 of 4 - abilities first, the build's style" && !whyLongest.Any(r => r.IndexOf("abilities first", StringComparison.OrdinalIgnoreCase) >= 0)
                && !whyLent.Concat(whyLong).Concat(whyAbil).Any(r => r.Contains("build's own")),
                string.Join(" / ", whyLent) + " | " + string.Join(" / ", whyLong) + " | " + string.Join(" / ", whyMine) + " | " + string.Join(" / ", whyAbil) + " | " + string.Join(" / ", whyLongest));
            // the rails: every form for every build name x style x rank x level, at the card's room (50, and 48 beside "AVOID")
            var railBad = new List<string>(); int swept = 0;
            foreach (var build in new[] { "Rifleman", "Shield Anchor", "Bombardier", "A Build Named Twenty1", "Bench Anchor" })
                foreach (var style in Styles)
                    foreach (int rank in new[] { 1, 2, 4 })
                        foreach (int level in new[] { 1, 2, 3 })
                            foreach (bool lentSaid in new[] { true, false })
                                foreach (int room in new[] { 50, 48 })               // beside "2ND" / "AVOID" (Wording.RoomBeside)
                                    foreach (double reach in new[] { 1.0, 0.3 })
                                    {
                                        var w = new CardWords { Kind = SayKind.Weapon, Level = level, Max = 4, Style = style, Build = build, Reach = reach, Clock = "2:10 left", LentStyle = lentSaid, MineStyle = !lentSaid, ShareType = level == 2 ? "Kinetic" : null, SharePct = 61 };
                                        string line = Wording.Card(w, rank, "Medical Drone", "Stimpack", room, null); swept++;
                                        var off = Wording.Rails(line, room); if (off.Count > 0) railBad.Add("'" + line + "' (" + string.Join(", ", off) + ")");
                                        foreach (var r in WhyText.Reasons(w, null, line)) if (!WhyText.Fits(r) || r.Contains("level-up style:")) railBad.Add("WHY '" + r + "'");
                                    }
            Check("R4", "every new form within the rails at the card's room (" + swept + " lines)", railBad.Count == 0, railBad.Count == 0 ? "" : railBad.Count + " off: " + string.Join(" || ", railBad.Take(8)));
            // 0.15.x (C-m2 of the 10-07 review): the Steam Deck's line holds 42 characters at Auto (CardTextSize.LineChars at x1.56); a lent
            // build's style had no form that short - its #1 weapon card said "Level 2 of 4 - this build levels weapons first" (46) and shrank
            // under 16 px. Every lent-style line at LineChars 42 beside the #1 / 2ND / AVOID prefix fits its room, the bare style last.
            var deckBad = new List<string>(); int deckSwept = 0; string deckWeapon = "";
            foreach (var build in new[] { "Rifleman", "Shield Anchor", "A Build Named Twenty1", "Bench Anchor" })
                foreach (var style in Styles)
                    foreach (var rs in new[] { new KeyValuePair<int, double>(1, 6.0), new KeyValuePair<int, double>(2, 4.0), new KeyValuePair<int, double>(3, 0.5) })
                        foreach (int level in new[] { 1, 2, 3 })
                            foreach (double reach in new[] { 1.0 })                 // (a late weapon's "too late to finish it" is no style line)
                                foreach (string share in new[] { null, "Kinetic" })
                                {
                                    int room = Wording.RoomBeside(Synergy.ReasonPrefixWidth(rs.Key, rs.Value), 42);
                                    var w = new CardWords { Kind = SayKind.Weapon, Level = level, Max = 4, Style = style, Build = build, Reach = reach, Clock = "2:10 left", LentStyle = true, ShareType = share, SharePct = 61 };
                                    string line = Wording.Card(w, rs.Key, "Medical Drone", "Stimpack", room, null); deckSwept++;
                                    var off = Wording.Rails(line, room); if (off.Count > 0) deckBad.Add("'" + line + "' in " + room + " (" + string.Join(", ", off) + ")");
                                    if (rs.Key == 1 && style == BuildStyle.Weapon && level == 1 && reach == 1.0 && share == null && build == "Bench Anchor") deckWeapon = line;
                                }
            Check("R4", "the Steam Deck (LineChars 42): every lent-style line fits beside its prefix - the #1 weapons-first card says 'Level 2 of 4 - weapons first' (C-m2; " + deckSwept + " lines)",
                deckBad.Count == 0 && deckWeapon == "Level 2 of 4 - weapons first" && CardTextSize.LineChars(CardTextSize.Scale(0f, CardTextSize.ReasonPx1(1280, 800), CardTextSize.CanvasUnits(1280, 800)), CardTextSize.ReasonPx1(1280, 800)) == 42,
                "#1 weapons first: '" + deckWeapon + "'" + (deckBad.Count == 0 ? "" : " | " + deckBad.Count + " off: " + string.Join(" || ", deckBad.Take(6))));
            // the readout, the [ctx] line, the ADVICE row, the cfg text, the ranking (sources)
            string plan = Src("Plan.cs"), advisor = Src("Advisor.cs"), menu = Src("Menu.cs"), plugin = Src("Plugin.cs"), ranker = Src("Ranker.cs"), loadout = Src("LoadoutState.cs");
            Check("R4", "the readout: 'Handgun later - the build's style' (compact and full) where a lent build's own style holds the weapon back",
                plan != null && plan.Contains("C(Dim, \" later\" + (Ranker.LentStyleSays(style, from) ? LentNote : \"\"))") && plan.Contains("const string LentNote = \" - the build's style\";") && plan.Contains("w += C(Dim, \"  later\" + LentNote);"));
            Check("R4", "the [ctx] line names the style's source: '(Ability, Auto, the build's own)', '(Balanced, Auto, mine)'",
                advisor != null && advisor.Contains("\", Auto, the build's own\"") && advisor.Contains("\", Auto, mine\"") && advisor.Contains("Ranker.StyleOf(sv, out from)"));
            Check("R4", "the ADVICE row 'Lent builds' level-up style' (the build's own / mine), right after the level-up style, with SAVED feedback through the config's SettingChanged",
                menu != null && menu.Contains("Cycler(rows, \"ad:lent\"") && menu.Contains("\"Lent builds' level-up style\"") && menu.Contains("Plugin.AdviceLentStyle.Value = Next(Plugin.AdviceLentStyle.Value, d)")
                && menu.Contains("\"The build's style\"") && !menu.Contains("\"The build's own\"") && !menu.Contains("\" (its own)\"") && menu.IndexOf("\"ad:style\"", StringComparison.Ordinal) < menu.IndexOf("\"ad:lent\"", StringComparison.Ordinal)
                && menu.Contains("\"Level-up style (on Auto; lent builds: next row)\"") && menu.Contains("a lent build too, unless the second row says Mine.")
                && plugin != null && plugin.Contains("Menu.SettingSaved(written)"));
            Check("R4", "the cfg: LentBuildStyle (BuildsOwn by default) bound and applied; LevelUpStyle says exactly when it applies (no more 'for survivors on Auto (a build selected ... brings its own style)')",
                plugin != null && plugin.Contains("Config.Bind(\"Advice\", \"LentBuildStyle\", LentBuildStyle.BuildsOwn,") && plugin.Contains("d.LentStyleMine = AdviceLentStyle != null && AdviceLentStyle.Value == LentBuildStyle.Mine;")
                && !plugin.Contains("for survivors on Auto (a build selected in the mod menu brings its own style)") && plugin.Contains("only with LentBuildStyle = Mine") && plugin.Contains("public enum LentBuildStyle { BuildsOwn, Mine }"));
            Check("R4", "the ranking: StyleOf asks Builds.StyleFor with the doctrine's switch; the weapon floor's lent test unchanged; the once-early lift through Synergy",
                ranker != null && ranker.Contains("Builds.StyleFor(b, b != null && Builds.OnAuto(owner.Name), d.Style, d.LentStyleMine, out from)") && ranker.Contains("lift.Floor(style, styleFrom == StyleSource.Lent || styleFrom == StyleSource.Mine, Doctrine.Current.Style)")
                && ranker.Contains("once = Synergy.OnceEarly(style, owned.Count, a.Skipped, ctx.Reach(10));") && ranker.Contains("c.Score = Synergy.WithOnce(c.Score, once);"));

            // ---- R5: the badge advice weighs the style the cards follow
            var d0 = new Doctrine { Style = BuildStyle.Balanced, LentStyleMine = false };
            var d1 = new Doctrine { Style = BuildStyle.Balanced, LentStyleMine = true };
            var d2 = new Doctrine { Style = BuildStyle.Ability, LentStyleMine = true };
            bool onAuto = Builds.OnAuto("Medic");
            var s0 = Builds.Styled("Medic", lent, d0); var s1 = Builds.Styled("Medic", lent, d1); var s2 = Builds.Styled("Medic", lent, d2);
            Check("R5", "Builds.Styled: BuildsOwn hands the build itself back; Mine a copy with LevelUpStyle (the lent build untouched); the same style: the build itself",
                ReferenceEquals(s0, lent) && ReferenceEquals(s2, lent) && Builds.Styled("Medic", null, d1) == null && (!onAuto || (!ReferenceEquals(s1, lent) && s1.Style == BuildStyle.Balanced && s1.Id == lent.Id && lent.Style == BuildStyle.Ability))
                && loadout != null && loadout.Contains("Loadout.Prepare(leader, Builds.Styled(leader, build, d), mode, difficulty, d, k, PowerFactsOf, recruits)"),
                "Medic on Auto in the bench: " + onAuto + (onAuto ? "; Mine: " + s1.Style + " (the lent build: " + lent.Style + ")" : " (the copy is not checked)"));

            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad + PaceCases.Run();      // 0.15.0 (C15-04)
        }
    }

    // 0.15.0 (C15-04): '~N level-ups to come' early in a run (RunContext.ExpectedLevelUps over LevelPace). Up to 0.14.0 the pace was the
    // last three minutes' level-ups per minute, nothing before 0:45 (a flat 2.5 then): on the user's 10-06 run '~N' read 49, 48, 58, 62,
    // 65, 70, 76 in the first two minutes and 66 - 77 around 3:00, and every clock-scaled score moved with it.
    //   N1  the replay is the live run: the old rule over the logged level-up clocks gives the logged '~N' of that run (22 of 23 exactly, 1 off by one);
    //   N2  the new rule on the same run: from the first minute on, every '~N' within 10 % of the one at 3:00 (the spec's sequence:
    //       00:34 ~49, 00:47 ~48, 00:59 ~58, 01:25 ~65, 01:41 ~76, 03:00 ~66-77), the first two minutes' swing under 10 % once three
    //       level-ups are in, and no offer after the first minute moving it by more than 10 %;
    //   N3  against what really came: a whole 20-minute Normal run (10-04 20:14) and the 10-06 Hardcore run - the new '~N' is nearer the
    //       level-ups that came after each offer than the old one, and Hardcore no longer starts from 2.5 a minute;
    //   N4  the parts: the measured priors per mode, the blend, the smoothing from the prior, a new run, a mode change, the fallback with
    //       no pace handed in (the bench's own RunContexts set LevelRate, so no card order of this bench moves), and the sources.
    // Level-up clocks are the log's '[offer] LevelUp mm:ss' (new level-ups only: a reroll or a replaced offer is the same one), + 0.5 s
    // (the clock shows whole seconds) - with that the old rule reproduces the logged values.
    static class PaceCases
    {
        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }
        static string Src(string file)
        {
            string p = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod", file));
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }
        static string F(double x) { return x.ToString("0.00", CultureInfo.InvariantCulture); }
        static string Pct(double x) { return (x * 100).ToString("0.0", CultureInfo.InvariantCulture) + " %"; }

        // 2026-10-06 19:16, Normal d2, the Medic leading (the session of the user's decisions): 23 level-ups, defeat at 6:27
        static readonly int[] Today = { 34, 47, 59, 72, 85, 94, 101, 115, 140, 155, 170, 180, 193, 208, 229, 249, 249, 271, 282, 298, 321, 348, 376 };
        static readonly int[] TodayLogged = { 49, 48, 58, 62, 65, 70, 76, 75, 68, 67, 66, 68, 73, 77, 70, 69, 74, 67, 61, 60, 59, 57, 46 };   // its '[ctx] ... ~N level-ups to come'
        // 2026-10-04 20:14, Normal: the whole 20 minutes, 60 level-ups
        static readonly int[] FullNormal = { 19, 32, 52, 61, 68, 72, 89, 98, 107, 115, 125, 140, 152, 168, 186, 205, 224, 237, 237, 273, 298, 308, 328, 348, 382, 419, 458, 458, 471, 496,
            522, 564, 608, 636, 642, 666, 682, 683, 696, 717, 764, 800, 807, 809, 871, 908, 912, 933, 941, 982, 995, 1013, 1056, 1074, 1088, 1124, 1150, 1154, 1168, 1193 };
        // 2026-10-06 20:33, Hardcore d1 (goal 10:00), the Tank leading: 44 level-ups to 9:49
        static readonly int[] Hardcore = { 39, 49, 54, 63, 67, 79, 85, 99, 110, 120, 125, 132, 144, 146, 148, 154, 162, 187, 187, 215, 234, 274, 285, 285, 285, 305, 320, 355, 377, 388,
            404, 423, 428, 444, 461, 474, 474, 478, 516, 530, 548, 579, 579, 589 };

        sealed class Offer { public double T; public int N; public int Old, New; public double Rate; }

        // '~N' as the [ctx] line prints it (RunContext.ToString: ExpectedLevelUps.ToString("0"))
        static int Shown(double v) { return int.Parse(v.ToString("0", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture); }

        // the rule up to 0.14.0: the last three minutes' level-ups per minute, 0 before 0:45; then 2.5, or the run's average past 1:00
        static double OldRate(List<double> at, double t)
        {
            double r = 0;
            if (t >= 45) { double w = Math.Min(180, t); r = at.Count(x => x >= t - w) / (w / 60.0); }
            if (r <= 0) r = t > 60 && at.Count > 0 ? at.Count / (t / 60.0) : 2.5;
            return Math.Max(0.6, Math.Min(5.0, r));
        }

        static List<Offer> Replay(int[] clocks, string mode, double goal)
        {
            var pace = new LevelPace(); var at = new List<double>(); var list = new List<Offer>();
            foreach (int c in clocks)
            {
                double t = c + 0.5;
                pace.Add(t); at.Add(t);
                var ctx = new RunContext { Mode = mode, Goal = goal, Seconds = t, LevelUps = pace.Count, LevelRate = pace.Rate(mode), D = new Doctrine() };
                list.Add(new Offer { T = t, N = pace.Count, Rate = ctx.LevelRate, New = Shown(ctx.ExpectedLevelUps), Old = Shown(OldRate(at, t) * ctx.Remaining / 60.0) });
            }
            return list;
        }
        static string Clock(double t) { int s = (int)t; return (s / 60).ToString("00") + ":" + (s % 60).ToString("00"); }
        static string Seq(IEnumerable<Offer> o, Func<Offer, int> v) { return string.Join(" ", o.Select(x => Clock(x.T) + " " + v(x))); }

        public static int Run()
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.15.0: the early level-up pace (C15-04) - N1 the 10-06 run replayed, N2 '~N level-ups to come' steady from the first minute, N3 against what came, N4 the parts");

            // ---- N1: the replay is the live run
            var today = Replay(Today, "Normal", 1200);
            var miss = new List<string>(); int off = 0;
            for (int i = 0; i < today.Count; i++) if (today[i].Old != TodayLogged[i]) { miss.Add(Clock(today[i].T) + " " + today[i].Old + " (logged " + TodayLogged[i] + ")"); off = Math.Max(off, Math.Abs(today[i].Old - TodayLogged[i])); }
            Check("N1", "the 10-06 run (Normal, 23 level-ups): the rule up to 0.14.0 over the logged clocks gives the logged '~N level-ups to come' - " + (today.Count - miss.Count) + " of " + today.Count
                + " exactly, the rest within 1 (the clock hides the fraction of a second)", off <= 1 && miss.Count <= 2 && today.Count == TodayLogged.Length, (miss.Count > 0 ? string.Join(", ", miss) + "; " : "") + Seq(today.Take(8), o => o.Old));

            // ---- N2: steady from the first minute
            var after = today.Where(o => o.T >= 59 && o.T <= 181).ToList();                 // 00:59 - 03:00, the spec's sequence
            int at3 = today.Last(o => o.T <= 181).New;
            double worst = after.Max(o => Math.Abs(o.New - at3) / (double)at3), worstOld = after.Max(o => Math.Abs(o.Old - today.Last(x => x.T <= 181).Old) / (double)today.Last(x => x.T <= 181).Old);
            Check("N2", "from the first minute to 3:00 every '~N' is within 10 % of the one at 3:00 (" + at3 + "): " + Pct(worst) + " at most (the old rule: " + Pct(worstOld) + ")",
                worst <= 0.10, "new " + Seq(today.Where(o => o.T <= 181), o => o.New) + " | old " + Seq(today.Where(o => o.T <= 181), o => o.Old));
            var early = today.Where(o => o.N >= 3 && o.T <= 120.5).ToList();
            double swing = early.Max(o => o.New) / (double)early.Min(o => o.New) - 1, swingOld = early.Max(o => o.Old) / (double)early.Min(o => o.Old) - 1;
            Check("N2", "the first two minutes, once three level-ups are in: the swing is under 10 % (" + Pct(swing) + "; the old rule " + Pct(swingOld) + ", 58 to 76)",
                swing < 0.10 && swingOld > 0.25, Seq(early, o => o.New));
            double step = 0, stepOld = 0; string where = "";
            for (int i = 1; i < today.Count; i++)
            {
                if (today[i].T < 60) continue;
                double s = Math.Abs(today[i].New - today[i - 1].New) / (double)today[i - 1].New, so = Math.Abs(today[i].Old - today[i - 1].Old) / (double)today[i - 1].Old;
                if (s > step) { step = s; where = Clock(today[i - 1].T) + " " + today[i - 1].New + " > " + Clock(today[i].T) + " " + today[i].New; }
                stepOld = Math.Max(stepOld, so);
            }
            Check("N2", "after the first minute no level-up moves '~N' by more than 10 % (" + Pct(step) + " at most, " + where + "; the old rule " + Pct(stepOld) + ")", step <= 0.10);
            Check("N2", "the pace behind it climbs with the run's evidence instead of jumping: " + F(today[2].Rate) + " a minute at 00:59, " + F(today.Last(o => o.T <= 181).Rate) + " at 3:00, "
                + F(today.Last().Rate) + " at the defeat (the run measured " + F(Today.Length / (387 / 60.0)) + " over its 6:27)",
                today.Zip(today.Skip(1), (a, b) => Math.Abs(b.Rate - a.Rate) / a.Rate).Max() <= 0.10 && today[2].Rate < today.Last(o => o.T <= 181).Rate);

            // ---- N3: against what came
            foreach (var run in new[] { Tuple.Create("the whole 10-04 20:14 Normal run (60 level-ups in 19:53)", FullNormal, "Normal", 1200.0), Tuple.Create("the 10-06 20:33 Hardcore run (44 in 9:49)", Hardcore, "Hardcore", 600.0) })
            {
                var r = Replay(run.Item2, run.Item3, run.Item4);
                double eNew = 0, eOld = 0; int n = 0, nearer = 0, farther = 0;
                for (int i = 0; i < r.Count; i++)
                {
                    if (r[i].T < 30 || r[i].T > run.Item4 - 60) continue;
                    int came = r.Count - (i + 1);
                    eNew += Math.Abs(r[i].New - came); eOld += Math.Abs(r[i].Old - came); n++;
                    if (Math.Abs(r[i].New - came) < Math.Abs(r[i].Old - came)) nearer++; else if (Math.Abs(r[i].New - came) > Math.Abs(r[i].Old - came)) farther++;
                }
                Check("N3", run.Item1 + ": the new '~N' is nearer the level-ups that came after each offer - off by " + F(eNew / n) + " on average against " + F(eOld / n) + " (nearer at " + nearer + " offers, farther at " + farther + ")",
                    eNew < eOld && nearer > farther, "first offers new " + Seq(r.Take(6), o => o.New) + " | old " + Seq(r.Take(6), o => o.Old) + " | came " + string.Join(" ", Enumerable.Range(0, 6).Select(i => r.Count - (i + 1))));
            }
            var hc = Replay(Hardcore, "Hardcore", 600);
            Check("N3", "Hardcore no longer starts from 2.5 a minute: '~N' " + hc[0].New + " at 00:39 with " + (Hardcore.Length - 1) + " to come (the old rule: " + hc[0].Old + ")",
                Math.Abs(hc[0].New - (Hardcore.Length - 1)) <= 0.1 * (Hardcore.Length - 1) && hc[0].Old < 25);

            // ---- N4: the parts
            Check("N4", "the measured priors (level-ups a minute after the first minute, runs of 3 minutes or more): Normal 2.9, Hardcore 4.7, Endless 3.2, BossRush 4.6; a mode with no run measured: Normal's",
                LevelPace.Prior("Normal") == 2.9 && LevelPace.Prior("Hardcore") == 4.7 && LevelPace.Prior("Endless") == 3.2 && LevelPace.Prior("BossRush") == 4.6 && LevelPace.Prior("OneHit") == 2.9 && LevelPace.Prior(null) == 2.9);
            Check("N4", "the blend: (prior x 4 + observed x n) / (4 + n) - the prior alone before a level-up, half and half at four",
                LevelPace.Blend(2.9, 6, 0) == 2.9 && Math.Abs(LevelPace.Blend(2.9, 4.1, 4) - 3.5) < 1e-9 && Math.Abs(LevelPace.Blend(4.7, 2.7, 16) - 3.1) < 1e-9);
            var p = new LevelPace();
            double r0 = p.Rate("Hardcore");
            p.Add(30); double r1 = p.Rate("Hardcore"); double again = p.Rate("Hardcore");
            double asNormal = p.Rate("Normal");
            p.Clear(); double fresh = p.Rate("Normal");
            Check("N4", "the smoothing starts from the prior and moves at the level-ups only (asked twice: the same); a mode change replays from that mode's prior; a new run starts over",
                r0 == 4.7 && r1 < 4.7 && r1 > 4.0 && again == r1 && asNormal < 2.9 && fresh == 2.9 && p.Count == 0, F(r0) + " > " + F(r1) + " / Normal " + F(asNormal) + " / new run " + F(fresh));
            var none = new RunContext { Mode = "Hardcore", Goal = 600, Seconds = 0, D = new Doctrine() };
            var some = new RunContext { Mode = "Normal", Goal = 1200, Seconds = 120, LevelUps = 8, D = new Doctrine() };
            Check("N4", "no pace handed in (LevelRate 0): the mode's prior blended with the level-ups seen - Hardcore at 0:00 " + Shown(none.ExpectedLevelUps) + " to come (up to 0.14.0: 25 at 2.5 a minute); Normal at 2:00 with 8 seen " + Shown(some.ExpectedLevelUps),
                Shown(none.ExpectedLevelUps) == 47 && Math.Abs(some.ExpectedLevelUps - LevelPace.Blend(2.9, 4.0, 8) * 18) < 1e-9);
            string state = Src("GameState.cs"), ctxSrc = Src("Context.cs"), advisor = Src("Advisor.cs");
            Check("N4", "sources: the readout's and the cards' pace is LevelPace fed at new level-ups only (Advisor), read under the run's mode; no 'seconds < 45' and no flat 2.5 a minute left",
                state != null && state.Contains("static readonly LevelPace _pace = new LevelPace();") && state.Contains("c.LevelRate = Pace.Rate(s.Mode);") && !state.Contains("if (seconds < 45f) return 0;")
                && ctxSrc != null && !ctxSrc.Contains("LevelUps / (Seconds / 60.0) : 2.5") && ctxSrc.Contains("LevelPace.Blend(LevelPace.Prior(Mode)") && advisor != null && advisor.Contains("if (!reroll0) G.Pace.LevelUp(t);"));

            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }
    }
}
