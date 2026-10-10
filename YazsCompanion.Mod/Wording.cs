// The words under the cards (0.14.0, A1 stage 2): what a card's reason line SAYS, built from the verdict the ranking already
// holds. Up to 0.13.0 the card drew the ranking's own headline (Why[0]), which is written for the log: "focus 2>3 of 4 toward
// its evolution - #2 in Rifleman" names the ranking's internals and counts nothing a player can see (and its '>' was dropped
// on screen, "focus 23 of 4"). The log keeps that headline; the card says the same verdict plainly:
//     Level 3 of 4 - evolution unlocks at level 4          Max level - next tier: Pump-Action Shotgun
//     The Rifleman build's main ability                    Mixed: boosts Ice, weakens Kinetic and Explosive
//     Unlocked synergy with Medic - A-tier in the guides   Turns on the Ice 10-tag effect
// Writing rules (the bench's rails hold every builder to them, WordingCases.cs):
//  - the decisive reason in player terms, "<what this pick does> - <why>"; the game's card already shows its NEW plate, its
//    level diamond and its rarity, so the line does not say them again;
//  - every number says what it counts ("Level 3 of 4", "Kinetic is 44% of your damage", "2:10 left"); no Companion score;
//  - the game's words: Skill Tree, Training Yard, recruit, unlocked synergy, damage type tag, "10-tag effect", the game's own
//    stat labels; never the ranking's (focus, style:, (Auto), #2 in, special, stack, tree boost, pts, weighs, x1.31);
//  - "the <B> build" always, and a build's order in words (its main ability / a core ability of it / part of it / skips it);
//  - 50 visible characters at most (56 with the "2ND   " before it: what a card shows up to a card text size of about x1.2; 48 on
//    a card that shows "AVOID   "; fewer at a larger size - 42 in all at the Deck's x1.56, CardTextSize.LineChars), counted on the line as drawn - with the names another mod lends (Names.Text), which run up to 19
//    characters longer than the game's. A form that is too long loses its parenthesis first, then the next, shorter form is tried;
//  - printable ASCII without | \ ^ ` and <, plus » and ×; '-' is the one dash (the card font has none of › · • ≥ →).
// Pure (no game types): Ranker fills a CardWords per card while it scores it and asks here once the ranks are final (a lifted
// weapon's line names the cards above it); ItemRules fills an ItemSay as it reasons; the bench sweeps every builder.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace YazsCompanion
{
    /// <summary>What a card is, for its reason line.</summary>
    internal enum SayKind { Other, Ability, Weapon, FirstWeapon, NextTier, OtherBranch, Evolution, Item, Tags, Stat, Recruit }

    /// <summary>The verdict data a card's reason line is built from (Wording.Card): filled by the ranking while it scores the
    /// card, read once the ranks are final. Every field is optional; what a kind does not use stays at its default.</summary>
    internal sealed class CardWords
    {
        public SayKind Kind;
        public string Text;                  // Other: the line itself
        // ---- abilities and weapons
        public int Level, Max;               // the level now (before the pick) and the last level
        public string Build;                 // the owner's build (null: none - Auto without a lent build)
        public int Priority = -1;            // the build's order, 0 = its first ability (-1: not in it)
        public int HeadRank; public string[] Head;    // the verdict's headline in display words (Ranker.AbilityVerdict.Mark: 6 the build
                                             // skips it, 5 a 10-tag effect, 4 the build's main / core ability, 3 an unlocked synergy or a
                                             // team passive, 2 the guides' tier, 1 the rest of the build, a share, a low tier), longest form first
        public bool Focus;                   // the ability the survivor should be feeding (Ranker.FocusAbility)
        public bool EvoExists, EvoOwned;     // the ability has an evolution / its Skill Tree node is bought
        public double Reach = 1;             // the share of the picks still needed that the clock allows (an evolution, a new ability, a weapon)
        public string Clock = "";            // "2:10 left"
        public int Owned;                    // abilities the survivor holds (a new ability)
        public BuildStyle Style;             // weapons and abilities: the level-up style followed
        // 0.15.0 (C15-07, the user's decision Q3): where that style comes from, when it is not plainly the build's or the player's -
        // LentStyle: a build another mod lends, followed through Auto, brings its own and the player's LevelUpStyle says otherwise
        // ([Advice] LentBuildStyle = BuildsOwn: the cards say "abilities first, the <B> build's style"); MineStyle: the player's
        // LevelUpStyle stands in for that build's own (LentBuildStyle = Mine: "your style ...", never "the <B> build levels ...")
        public bool LentStyle, MineStyle;
        public bool Lifted; public string LiftLeft;   // "abilities first" with the survivor's own abilities as good as done (WeaponLift)
        public string Next;                  // the weapon this one leads to once maxed / the branch a tier-3 card locks out
        public bool NextOffered = true;      // that branch can be offered (its Skill Tree node bought)
        public string Branch;                // why the next tier is this weapon: "build", "type", "guides", "yard"; a branch card: "build", "squad"
        public string BranchType, BranchWith;     // "type": the damage type it shares, the first powerup of the squad dealing it
        // ---- what the squad's damage type tags and team passives say (Wording.Tags)
        public string Special;               // the type this level carries to its 10-tag effect
        public string ShortType; public int Short;   // the type 2 - 3 tags short of its 10-tag effect
        public string ShareType, ShareFrom; public int SharePct;   // the squad's biggest share among its types, the first powerup dealing it
        public string Boost;                 // a team passive on the squad that boosts it
        // ---- evolutions
        public string Base, Pick; public bool Mine, PickOffered = true;    // the base ability, the build's pick and whether this is it / offered
        public int BasePriority = -1;        // 0.15.0 (C15-03 c): the base ability's place in the build's order (Priority's scale; -1: none, or skipped)
        public string Adds, AddsWith; public bool AddsNew;   // the damage type it adds over its base, who on the squad deals it (new: nobody)
        public bool Pair; public string OtherAdds;           // the other evolution of the same base is on the offer too, and what that one adds
        public string Closes;                // 0.16.0 (C16-17): the other evolution of the base, which the PLAN prefers and this pick closes (null: none)
        // ---- items, Research Pods, stat cards, rescues
        public ItemSay Item;
        public string Type; public int Points; public TagProfile Tags;      // a Research Pod card: the type, its points, the squad's profile
        public string Stat; public bool Team; public string[] StatWhy; public string WantBuild;     // a stat card: the game's label, team-wide,
                                             // the note in display words, the build that wants it
        public RecruitSay Recruit;
        public string Quest;                 // 0.14.0 (C3): the active quest's rule line when it decides the card ("quest: ..."; QuestRules) - said first
        public string[] Held;                // 0.16.0 (C16-01): what an item the squad holds does to this card, longest form first ("does nothing while you
                                             // hold Mana Potion") - said right after the quest (HeldRules)
        // ---- 0.15.0 (C15-03 a): once the ranks are final (Wording.Ranks) - the card's game name and place on the offer (0: not known,
        // a line as before), and for a weapon level the best ability card on the offer, for an ability the best weapon level card
        // (null: none) - the WHY band says what decided between them instead of a style the order contradicts
        public string Name;
        public int Rank;
        public CardWords Rival;
    }

    /// <summary>What the item rules reasoned (ItemRules.Evaluate fills it beside the reason lines when the context carries one).</summary>
    internal sealed class ItemSay
    {
        public bool Quest, Held;             // the active quest's target item; held already and it does not stack
        public string QuestLine;             // a quest rule's own reason line ("quest: ..."), when one decided
        public string Lead;                  // what the rules put first, in display words (a pair held, a tag item's verdict)
        public string Tier, Note;            // the guides' tier, the knowledge note (or a rule's own note: RuleNote)
        public bool RuleNote;                // the note is a rule's verdict on this squad ("no short-range weapon on the squad"), not a guide's
        public string NoteSay;               // a rule's note in the card's words, when they differ from the log's ("all your weapons are short-range")
        public readonly List<ItemFit> Boosts = new List<ItemFit>(), Hurts = new List<ItemFit>();
        public readonly List<string> Nobody = new List<string>();        // damage types it is about that nobody on the squad deals
        public string Lacks;                 // what it needs that the squad does not have, in display words
        public string Want, WantBuild;       // the leaning of a build it fits, and that build
        public string Scaling;               // a growing item: "early", "late"
        public string Clock = "";
        public string Keyword; public string KeywordAxis; public double KeywordValue = 1;     // the first keyword scored that names nobody
        public string Survival;              // "onehit", "hurting", "now", "plain"
        // 0.16.0 (C16-01): a held-item rule's words for the card, longest form first, said before every other reason but the quest's and
        // 'already held' (null: none), and what filling an empty item slot costs this card (C16-01c, the action hints judge the merit)
        public string[] LeadForms;
        public double HeldCost;
    }

    /// <summary>One thing an item boosts or weakens: a damage type (<see cref="Typed"/>), a powerup tag the squad owns
    /// (<see cref="Power"/>) or a keyword a class favours; <see cref="Who"/> = the first powerup dealing the type, or the class.</summary>
    internal sealed class ItemFit { public string What, Who; public bool Typed, Power; public int Pct; public double Weight; }

    /// <summary>What speaks for a rescue card (Ranker.RecruitScore fills it beside the reason lines).</summary>
    internal sealed class RecruitSay
    {
        public bool Liberate, Full;          // the Liberate card; the squad is full
        public string Quest;                 // the active quest's line when it decides the card ("quest: ...")
        public string Late;                  // the clock when a recruit no longer has the time to grow
        public string Tier;                  // the guides' rescue tier
        public int Bought, Unbought;         // synergy nodes with the squad: unlocked in the Skill Tree / not
        public readonly List<string> Partners = new List<string>();     // the survivors those unlocked synergies are with
        public string SharedType, SharedWith;      // a damage type the squad deals that the recruit deals too, the first powerup dealing it
        public string TeamBonus;             // the stat of the recruit's own team passive, in the game's words ("armor")
        public string Boosts; public int Covered;  // a team passive of the recruit's that would boost <Covered> of the squad's powerups
        public int RankLevels, Rank;         // class levels to the recruit's next rank (Farm), that rank
        // 0.16.0 (C16-01): the held items' counts - two counts, two meanings: the level-ups a RECRUIT brings (C16-01b, Reserve Bench) and the
        // level-ups a LIBERATE gives (C16-01d, Hijacked Signal: 2); cash that heals instead (C16-01e, Life Savings)
        public int JoinLevelUps;
        public int LiberateLevelUps = 1;
        public bool CashHeals;
    }

    internal static class Wording
    {
        /// <summary>The visible characters a line may have (the "2ND   " before it makes 56: the Deck's width at its card text size).</summary>
        public const int Budget = 50;
        /// <summary>What a card's reason line shows, its "2ND" / "AVOID" included, up to a card text size of about x1.2 (the PC's x1);
        /// a larger size holds fewer (CardTextSize.LineChars: 42 at the Steam Deck's x1.56 - the review of 10-06; 0.14.0 took 56 there).</summary>
        public const int DeckWidth = 56;

        // the line being built (Card sets them for one call): its room in visible characters, and the names another mod lends -
        // a form is measured as it will be drawn. Defaults: Budget, the game's names (the bench's sweep)
        [ThreadStatic] static int _room;
        [ThreadStatic] static Func<string, string> _lend;
        static int Room { get { return _room > 0 ? _room : Budget; } }
        static int Len(string s)
        {
            if (s == null) return 0;
            if (_lend == null) return s.Length;
            try { return Safe(_lend(s) ?? s).Length; } catch { return s.Length; }
        }
        /// <summary>A name as it will be drawn (another mod's name for it), before a short form is taken of it.</summary>
        static string Lend(string name)
        {
            if (_lend == null || string.IsNullOrEmpty(name)) return name;
            try { return _lend(name) ?? name; } catch { return name; }
        }
        /// <summary>An evolution's own part of the name it will be drawn with ("Bioweapon"; another mod's name's own part when it lends one) -
        /// 0.15.0 (C15-03 b): only where <paramref name="named"/> (the card's base, or the line's other name) names its base already;
        /// else the name whole, as the card shows it (lent as drawn).</summary>
        static string ShortOf(string name, string named) { return BaseNamed(name, named) ? Short(Lend(name)) : name; }

        // ================================================================ the glyphs the card font has
        /// <summary>A line as the card's font can draw it: no '&lt;' (it would open a rich-text tag), the arrows to '»', the bullets
        /// to '-', '≥' to "&gt;=", the long dashes to '-', and none of | \ ^ ` (missing from the card font).</summary>
        public static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = null;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i]; string to = null;
                switch (ch)
                {
                    case '<': case '\\': case '^': case '`': to = ""; break;
                    case '→': case '›': to = "»"; break;
                    case '·': case '•': case '—': case '–': to = "-"; break;
                    case '≥': to = ">="; break;
                    case '|': to = "/"; break;
                }
                if (to == null) { if (sb != null) sb.Append(ch); continue; }
                if (sb == null) { sb = new StringBuilder(s.Length + 4); sb.Append(s, 0, i); }
                sb.Append(to);
            }
            return sb == null ? s : sb.ToString();
        }

        // ================================================================ composing a line
        /// <summary>The first form that fits the budget: "&lt;lead&gt; - &lt;form&gt;", or the form alone, capitalised (no lead). Each
        /// form is tried as it is, then without its parenthesis; the last one tried when none fits.</summary>
        public static string Say(string lead, params string[] forms)
        {
            string last = null;
            if (forms != null)
                foreach (var f in forms)
                {
                    if (string.IsNullOrEmpty(f)) continue;
                    last = Join(lead, f);
                    if (Len(last) <= Room) return last;
                    string bare = NoParen(f);
                    if (bare == null) continue;
                    last = Join(lead, bare);
                    if (Len(last) <= Room) return last;
                }
            return last ?? (lead ?? "");
        }
        static string Join(string lead, string form) { return lead == null ? Cap(form) : lead + " - " + form; }
        static string[] F(params string[] forms) { return forms; }
        static string[] Concat(string[] a, params string[] b) { return a == null ? b : a.Concat(b).ToArray(); }

        /// <summary>The text without its (last) parenthesis, or null when it has none.</summary>
        static string NoParen(string s)
        {
            int o = s.LastIndexOf(" (", StringComparison.Ordinal);
            if (o < 0) return null;
            int c = s.IndexOf(')', o);
            return (s.Substring(0, o) + (c >= 0 && c + 1 < s.Length ? s.Substring(c + 1) : "")).TrimEnd();
        }

        public static string Cap(string s) { return string.IsNullOrEmpty(s) || !char.IsLower(s[0]) ? s ?? "" : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        // the last resort of a form that names something another mod renames longer: whole words off its end until it fits as drawn,
        // never ending on a word that leads into the cut part ("keep the slot for" -> "keep the slot"); the form itself when even
        // two words do not fit
        static readonly HashSet<string> Dangling = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "for", "with", "of", "the", "and", "or", "to", "a", "an", "your", "like", "on", "in", "by", "at", "+", "-", "is" };
        static string Cut(string s)
        {
            if (string.IsNullOrEmpty(s) || Len(s) <= Room) return s;
            var words = s.Split(' ').ToList();
            while (words.Count > 2)
            {
                words.RemoveAt(words.Count - 1);
                while (words.Count > 2 && Dangling.Contains(words[words.Count - 1].TrimEnd(',', ':', ';'))) words.RemoveAt(words.Count - 1);
                string t = string.Join(" ", words).TrimEnd(',', ':', ';', '-', '+', ' ');
                if (Len(t) <= Room) return t;
            }
            return s;
        }

        /// <summary>"Level 3 of 4", "Max level".</summary>
        public static string LevelText(int level, int max) { return max > 0 && level >= max ? "Max level" : max > 0 ? "Level " + level + " of " + max : "Level " + level; }

        /// <summary>An evolution's own part of its name ("Bioweapon" of "Bombing Strike: Bioweapon") - the whole name when that part
        /// says too little on its own (a number, a short word, a damage type: "Experiment 21: 73", "Bear Trap: Fire").</summary>
        public static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return name ?? "";
            int c = name.IndexOf(':');
            if (c <= 0 || c + 1 >= name.Length) return name;
            string own = name.Substring(c + 1).Trim();
            if (own.Length < 5 || own.All(ch => char.IsDigit(ch) || ch == ' ') || TagProfile.Names.Any(t => string.Equals(t, own, StringComparison.OrdinalIgnoreCase))) return name;
            return own;
        }

        /// <summary>0.15.0 (C15-03 b): <see cref="Short(string)"/> only where the base is named already - in the same line or on the same
        /// card (<paramref name="named"/>: that card's name or base, or the line's other name): "Bioweapon" on a card of Bombing Strike,
        /// the name whole anywhere else. The live 10-06 offer (19:33:05) drew "Frost goes first: ..." on a Sawblade Drone card for the
        /// first card's evolution "...: Frost" - a damage type's word, not a card the player could find.</summary>
        public static string ShortBeside(string name, string named) { return BaseNamed(name, named) ? Short(name) : name ?? ""; }

        /// <summary>The base of an evolution's name ("Bombing Strike" of "Bombing Strike: Bioweapon") appears in <paramref name="named"/>.</summary>
        public static bool BaseNamed(string name, string named)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(named)) return false;
            int c = name.IndexOf(':');
            if (c <= 0) return false;
            string head = name.Substring(0, c).Trim();
            return head.Length > 0 && named.IndexOf(head, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        /// <summary>The first powerup of a source list ("Handgun" of "Handgun, Medical Drone +1").</summary>
        public static string FirstSource(string src)
        {
            if (string.IsNullOrEmpty(src)) return null;
            int c = src.IndexOf(", ", StringComparison.Ordinal); string one = c >= 0 ? src.Substring(0, c) : src;
            int p = one.LastIndexOf(" +", StringComparison.Ordinal); if (p > 0 && p + 2 < one.Length && char.IsDigit(one[p + 2])) one = one.Substring(0, p);
            return one.Trim();
        }
        /// <summary>The build a reason names: "Rifleman" of "your Rifleman build" or "Rifleman (Auto)" (Builds.Your).</summary>
        public static string BuildOf(string whose)
        {
            if (string.IsNullOrEmpty(whose)) return null;
            string s = whose;
            if (s.StartsWith("your ", StringComparison.Ordinal)) s = s.Substring(5);
            if (s.EndsWith(" build", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 6);
            if (s.EndsWith(" (Auto)", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 7);
            return s.Trim();
        }
        static string Or(IList<string> names) { return names.Count <= 1 ? (names.Count == 1 ? names[0] : "") : string.Join(", ", names.Take(names.Count - 1)) + " or " + names[names.Count - 1]; }
        static string And(IList<string> names) { return names.Count <= 1 ? (names.Count == 1 ? names[0] : "") : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1]; }

        // ================================================================ the reasons, in the game's words (longest form first)
        /// <summary>A build's order in words: its main ability, a core ability of it (2nd and 3rd), part of it.</summary>
        public static string[] Role(string build, int priority)
        {
            if (string.IsNullOrEmpty(build) || priority < 0) return null;
            // the last, shortest forms (0.15.0, the review of 10-06): a larger card text size - the Deck's x1.56 - holds 42 characters in all
            if (priority == 0) return F("the " + build + " build's main ability", "the build's main ability", "the main ability");
            if (priority <= 2) return F("a core ability of the " + build + " build", "core to the " + build + " build", "a core ability of the build", "a core ability");
            return F("part of the " + build + " build", "part of the build");
        }
        public static string[] Skips(string build) { return F("the " + build + " build skips it", "the build skips it"); }
        public static string[] Tier(string tier) { return F(tier.ToUpperInvariant() + "-tier in the guides"); }
        public static string[] Synergy(string partner) { return F("unlocked synergy with " + partner, "synergy with " + partner, "synergy with your squad"); }
        public static string[] Boosted(string node, string owner) { return F("boosted by " + node + (string.IsNullOrEmpty(owner) ? "" : " (" + owner + ")"), "boosted by a team passive"); }
        /// <summary>A level that carries the type to its 10 tag points, where its effect switches on.</summary>
        public static string[] Special(string type) { return F("turns on the " + type + " 10-tag effect", type + " 10-tag effect turns on", "turns on a 10-tag effect"); }
        public static string[] Share(string type, int pct) { return F(type + " is " + pct + "% of your damage"); }

        // ================================================================ a card
        /// <summary>The line under a card, from its verdict data; <paramref name="rank"/> = its place on the offer, <paramref name="first"/>
        /// / <paramref name="second"/> = the names of the cards ranked first and second (a lifted weapon names them). Null: no words.</summary>
        public static string Card(CardWords w, int rank, string first, string second)
        {
            if (w == null) return null;
            return Line(w, rank, first, second);
        }

        /// <summary>The line under a card in <paramref name="room"/> visible characters (Budget, less on a card whose "AVOID   " is
        /// wider than the "2ND   " the budget leaves room for), measured with the names <paramref name="lend"/> swaps in - the names
        /// another mod lends, as the card will draw them (Names.Text; null: the game's names).</summary>
        public static string Card(CardWords w, int rank, string first, string second, int room, Func<string, string> lend)
        {
            if (w == null) return null;
            int was = _room; var lent = _lend;
            _room = room; _lend = lend;
            try { return Line(w, rank, first, second); }
            finally { _room = was; _lend = lent; }
        }

        /// <summary>The offline bench's sweep as drawn: every line built from here on gets <paramref name="room"/> and
        /// <paramref name="lend"/> (0, null: the defaults again). The game passes them per card (the overload above).</summary>
        internal static void Ambient(int room, Func<string, string> lend) { _room = room; _lend = lend; }

        /// <summary>The room a card's line has next to its "2ND" / "AVOID" (<paramref name="prefix"/> visible characters).</summary>
        public static int RoomBeside(int prefix) { return RoomBeside(prefix, DeckWidth); }

        /// <summary>The room a card's line has next to its "2ND" / "AVOID" when the line holds <paramref name="width"/> visible characters
        /// in all (CardTextSize.LineChars at the card text's size on this screen: 56 at the PC's, 42 at the Deck's x1.56); 30 at least.</summary>
        public static int RoomBeside(int prefix, int width) { return Math.Max(30, Math.Min(Budget, Math.Min(DeckWidth, width) - prefix)); }

        static string Line(CardWords w, int rank, string first, string second)
        {
            if (w.Quest != null) return Quest(w.Quest);
            if (w.Held != null) return Say(null, w.Held);           // 0.16.0 (C16-01): what a held item does to this card
            switch (w.Kind)
            {
                case SayKind.Ability: return w.Level <= 0 ? NewAbility(w) : Ability(w);
                case SayKind.Weapon: return Weapon(w, rank, first, second);
                case SayKind.FirstWeapon: return Say(null, "first weapon - the recruit can't attack without it", "first weapon - needed to attack");
                case SayKind.NextTier: return NextTier(w);
                case SayKind.OtherBranch: return OtherBranch(w);
                case SayKind.Evolution: return Evolution(w);
                case SayKind.Item: return Item(w.Item);
                case SayKind.Tags: return Tag(w.Type, w.Points, w.Tags);
                case SayKind.Stat: return Stat(w);
                case SayKind.Recruit: return Recruit(w.Recruit);
                default: return string.IsNullOrEmpty(w.Text) ? null : Say(null, w.Text);
            }
        }

        // ---- abilities: the level after the pick, then the reason
        static string Ability(CardWords w)
        {
            int next = w.Level + 1;
            bool last = w.Max > 0 && next >= w.Max;
            string lead = LevelText(next, w.Max);
            if (w.HeadRank >= 5 && w.Head != null) return Say(lead, w.Head);            // the build skips it, or a 10-tag effect switches on
            if (w.EvoOwned)
            {
                if (w.Reach >= 0.5) return last ? Say(lead, "its evolution comes next", "evolution next") : Say(lead, "evolution unlocks at level " + w.Max, "evolves at level " + w.Max);
                return Say(lead, "too late to evolve it (" + w.Clock + ")");
            }
            if (w.HeadRank >= 3 && w.Head != null) return Say(lead, w.Head);            // the build's main / core ability, a synergy, a team passive
            if (w.EvoExists) return Say(lead, "evolution locked in the Skill Tree", "evolution still locked");
            if (w.HeadRank > 0 && w.Head != null) return Say(lead, w.Head);
            if (w.Focus) return Say(lead, "keep feeding your top ability", "feed your top ability", "your top ability");
            return Say(lead, last ? "its full strength" : "a steady upgrade");
        }

        // ---- a new ability: the game's NEW plate says that much, the line says why
        static string NewAbility(CardWords w)
        {
            if (w.HeadRank >= 6 && w.Head != null) return Say(null, w.Head);
            if (w.Reach < 0.6) return Say(null, "little time left to level it (" + w.Clock + ")");
            if (w.HeadRank > 0 && w.Head != null) return Say(null, w.Head);
            return Say(null, w.Owned < 4 ? "fills an empty ability slot early" : "fills an ability slot");
        }

        // ---- a level of the weapon in hand
        static string Weapon(CardWords w, int rank, string first, string second)
        {
            int next = w.Level + 1;
            bool last = w.Max > 0 && next >= w.Max;
            string lead = LevelText(next, w.Max);
            if (w.Special != null) return Say(lead, Special(w.Special));
            // 0.15.0 (C15-03 b): another card's name whole - its evolution part only where this line names its base already
            if (last && w.Next != null) return Say(lead, "next tier: " + w.Next, "the next tier follows");
            if (w.Lifted)
            {
                // "abilities first" with the survivor's own abilities as good as done: the weapon goes ahead of the others' levels
                if (rank <= 1) return Say(lead, w.LiftLeft == null ? "your abilities are done" : "your abilities are nearly done");
                if (rank == 2 && first != null) return Say(lead, "next, after " + first, "next, after the top card");
                if (first != null && second != null)
                {
                    string both = "after " + first + " and " + second, cut = "after " + first + " and " + ShortOf(second, first);
                    return Say(lead, both, cut != both ? cut : null, "after the two cards above it");
                }
                return Say(lead, "after the cards above it");
            }
            bool late = w.Reach < 0.6;
            string[] share = w.ShareType != null ? Share(w.ShareType, w.SharePct) : null;
            // 0.15.0 (C15-07, decision Q3): the style of a lent build Auto follows is said as the build's ("abilities first, the build's
            // style"), the player's own standing in for it as theirs ("your style ..."), never as the build's
            bool build = w.Build != null && !w.MineStyle;
            switch (w.Style)
            {
                case BuildStyle.Ability:
                    if (rank <= 1) return Say(lead, Concat(share, "the best of this offer"));
                    if (w.LentStyle && build) return Say(lead, LentForms("abilities first", w.Build, "this build levels abilities first"));
                    // the weapons-first sibling's words (0.14.0 review: "after the abilities (build style)" read compressed)
                    return build ? Say(lead, "the " + w.Build + " build levels abilities first", "this build levels abilities first", "abilities first")
                        : Say(lead, "your style levels abilities first", "abilities first (your style)", "abilities first");
                case BuildStyle.Weapon:
                    if (late) return Say(lead, "too late to finish it (" + w.Clock + ")");
                    if (w.LentStyle && build) return Say(lead, LentForms("weapons first", w.Build, "this build levels weapons first"));
                    return build ? Say(lead, "the " + w.Build + " build levels weapons first", "this build levels weapons first", "weapons first")
                        : Say(lead, "your style levels weapons first", "weapons first (your style)", "weapons first");
                default:
                    if (late) return Say(lead, "too late to finish it (" + w.Clock + ")");
                    if (w.LentStyle && build) return Say(lead, Concat(LentForms("balanced", w.Build, null), Concat(share, "weapon and abilities side by side", "balanced")));
                    return Say(lead, Concat(share, "weapon and abilities side by side", "balanced with abilities", "balanced"));
            }
        }

        /// <summary>0.15.0 (C15-07): a lent build's own style, said as the build's - "abilities first, the Medic build's style", then
        /// without the build's name, then <paramref name="fallback"/> (null: none, and no bare form - the caller ends the list), then the
        /// bare style: 0.15.x (C-m2 of the 10-07 review) the Steam Deck's 42-character line held none of the longer forms, so a lent
        /// build's #1 weapon card said "Level 2 of 4 - this build levels weapons first" (46) and shrank under its 16 px; now
        /// "Level 2 of 4 - weapons first" (28).</summary>
        public static string[] LentForms(string style, string build, string fallback)
        {
            var forms = new List<string> { style + ", the " + build + " build's style", style + ", the build's style" };
            if (fallback != null) { forms.Add(fallback); forms.Add(style); }
            return forms.ToArray();
        }

        // ---- the next weapon tier
        static string NextTier(CardWords w)
        {
            const string lead = "Next weapon tier";
            switch (w.Branch)
            {
                case "build": if (w.Build != null) return Say(lead, "the " + w.Build + " build's branch", "the build's branch"); break;
                case "type": if (w.BranchType != null) return Say(lead, "deals " + w.BranchType + (w.BranchWith != null ? ", like your " + w.BranchWith : " like your squad"), "deals " + w.BranchType + " like your squad", "deals " + w.BranchType + " too"); break;
                case "guides": return Say(lead, "the guides' pick of the three");
                case "yard": return Say(lead, "your Training Yard pick");
            }
            return Say(lead, "a big step up");
        }

        // ---- a tier-3 weapon of another branch than the favoured one (the three branches exclude each other)
        static string OtherBranch(CardWords w)
        {
            if (w.Next == null) return Say("Next weapon tier", "a big step up");
            // 0.15.0 (C15-03 b): the other branch's name whole, as its card shows it
            if (!w.NextOffered) return Say("Next weapon tier", w.Next + " is locked, take this one", "the other branch is locked");
            if (w.Branch == "build") return Say(null, "locks out " + w.Next + ", the build's branch", "locks out the build's branch");
            return Say(null, "locks out " + w.Next + ", the better fit here", "locks out " + w.Next, "locks out the better branch");
        }

        // ---- evolutions: never "the biggest upgrade" - two evolutions of different bases can be on one offer
        static string Evolution(CardWords w)
        {
            if (w.Pick != null)
            {
                string pick = ShortOf(w.Pick, w.Base);          // the card is the base's evolution: its title names the base
                if (w.Mine) return Say(null, "the " + w.Build + " build's evolution", "the build's evolution");
                if (!w.PickOffered) return Say(null, pick + " isn't offered - this one still evolves it", pick + " isn't offered - this evolves it too", "the build's pick isn't offered - take this");
                return Say(null, "the " + w.Build + " build takes " + pick + " instead", "the build takes " + pick + " instead", "the build takes the other one");
            }
            // two evolutions of one base on the offer: the line that sets them apart
            if (w.Pair && !string.Equals(w.Adds, w.OtherAdds, StringComparison.OrdinalIgnoreCase))
                return w.Adds == null ? Say(null, "adds no new damage type") : Say(null, Adds(w));
            const string lead = "Evolution";
            if (w.Special != null) return Say(lead, Special(w.Special));
            // 0.15.0 (C15-03 c): a build that names no evolution for the base still ranks the base - its main or a core ability evolving
            // is the reason (live 10-06 19:33:04: the lent build's main ability evolved under "Evolution - a big step up")
            var role = Evolves(w);
            if (role != null) return Say(null, role);
            if (w.Boost != null) return Say(lead, Boosted(w.Boost, null));
            if (w.ShareType != null) return Say(lead, Share(w.ShareType, w.SharePct));
            if (w.Adds != null) return Say(lead, w.AddsNew || string.IsNullOrWhiteSpace(w.AddsWith) ? F("adds " + w.Adds + " to the squad") : F("adds " + w.Adds + ", like your " + w.AddsWith, "adds " + w.Adds));
            if (w.ShortType != null) return Say(lead, w.ShortType + " is " + w.Short + " tags from its 10-tag effect", w.Short + " tags from a 10-tag effect");
            return Say(lead, "a big step up");
        }
        /// <summary>0.15.0 (C15-03 c): an evolution whose base is the build's main ability ("evolves the Rifleman build's main ability") or a
        /// core one (2nd, 3rd) while the build names no evolution for it; null otherwise (a pick, a skipped base, the rest of the order).</summary>
        public static string[] Evolves(CardWords w)
        {
            if (w == null || w.Kind != SayKind.Evolution || w.Pick != null || string.IsNullOrEmpty(w.Build) || w.BasePriority < 0 || w.BasePriority > 2) return null;
            return w.BasePriority == 0 ? F("evolves the " + w.Build + " build's main ability", "evolves the build's main ability")
                : F("evolves a core ability of the " + w.Build + " build", "evolves a core ability of the build");
        }

        /// <summary>0.15.0 (C15-03 a): once the ranks are final - each card's place on the offer and game name (<paramref name="names"/>, in
        /// the same order), and for a weapon level the best ability card on the offer, for an ability the best weapon level card (the
        /// first of that kind in rank order). WhyText then says what decided between the two.</summary>
        public static void Ranks(IList<CardWords> order, IList<string> names)
        {
            if (order == null) return;
            for (int i = 0; i < order.Count; i++)
            {
                var w = order[i]; if (w == null) continue;
                w.Rank = i + 1; w.Rival = null;
                if (names != null && i < names.Count) w.Name = names[i];
            }
            foreach (var w in order)
            {
                if (w == null || (w.Kind != SayKind.Weapon && w.Kind != SayKind.Ability)) continue;
                var other = w.Kind == SayKind.Weapon ? SayKind.Ability : SayKind.Weapon;
                foreach (var o in order) if (o != null && !ReferenceEquals(o, w) && o.Kind == other) { w.Rival = o; break; }
            }
        }

        /// <summary>Two evolutions of one base on the same offer: each learns the other is there, and what the other adds - its line then
        /// says what sets it apart (Ranker runs it once the ranks are final).</summary>
        public static void Pair(IList<CardWords> offer)
        {
            if (offer == null) return;
            foreach (var w in offer)
            {
                if (w == null || w.Kind != SayKind.Evolution || w.Base == null) continue;
                foreach (var o in offer)
                    if (o != null && !ReferenceEquals(o, w) && o.Kind == SayKind.Evolution && o.Base == w.Base) { w.Pair = true; w.OtherAdds = o.Adds; }
            }
        }

        static string[] Adds(CardWords w)
        {
            if (w.AddsNew || string.IsNullOrWhiteSpace(w.AddsWith)) return F("adds " + w.Adds + " - new to the squad", "adds " + w.Adds);
            return F("adds " + w.Adds + ", which your " + w.AddsWith + " deals", "adds " + w.Adds + ", like your " + w.AddsWith, "adds " + w.Adds + " - the squad deals it");
        }

        // ================================================================ items (chests)
        /// <summary>An item's line, the strongest reason first: the quest, held already, what the rules put first (a pair, a tag
        /// item), a downside, a damage type the squad deals, a build's leaning, a type nobody deals, what it needs, a class it suits,
        /// growing, the knowledge note, the guides' tier, survival, a keyword in words.</summary>
        public static string Item(ItemSay s)
        {
            if (s == null) return null;
            if (s.QuestLine != null) return Quest(s.QuestLine);
            if (s.Quest) return Say(null, "quest: the quest asks for this item", "quest: the quest asks for it");
            if (s.Held) return Say(null, "already held by the squad");
            // 0.16.0 (C16-01): a held-item rule's words (C16-01a Mana Potion held, C16-01k the Mana Potion card, C16-01c a slot's cost), the
            // last form cut to the line as drawn
            if (s.LeadForms != null && s.LeadForms.Length > 0) return Say(null, s.LeadForms.Concat(new[] { Cut(s.LeadForms[s.LeadForms.Length - 1]) }).ToArray());
            if (s.Lead != null) return Say(null, s.Lead, s.Lead.StartsWith("pairs with ", StringComparison.Ordinal) ? "pairs with an item you hold" : s.Lead.StartsWith("clashes with ", StringComparison.Ordinal) ? "clashes with an item you hold" : null, Cut(s.Lead));
            if (s.Hurts.Count > 0)
            {
                var hurt = s.Hurts.OrderByDescending(h => h.Weight).Select(Noun).Distinct().Take(2).ToList();
                if (s.Boosts.Count > 0)
                {
                    var gain = s.Boosts.OrderByDescending(b => b.Weight).Select(Noun).Distinct().Take(2).ToList();
                    return Say(null, "mixed: boosts " + And(gain) + ", weakens " + And(hurt), "mixed: boosts " + gain[0] + ", weakens " + hurt[0], "mixed: helps and hurts this squad");
                }
                var worst = s.Hurts.OrderByDescending(h => h.Weight).First();
                if (worst.Typed && worst.Who != null) return Say(null, "weakens " + worst.What + " - your " + worst.Who + " deals it", "weakens " + worst.What);
                return Say(null, "a downside: less " + And(hurt), "a downside: less " + hurt[0]);
            }
            // an S- or A-tier item says so in front of a fit (a B or C tier is no reason of its own there)
            string top = s.Tier != null && (s.Tier.Trim().ToUpperInvariant() == "S" || s.Tier.Trim().ToUpperInvariant() == "A") ? s.Tier.Trim().ToUpperInvariant() + "-tier, " : null;
            var typed = s.Boosts.Where(b => b.Typed).OrderByDescending(b => b.Weight).FirstOrDefault();
            if (typed != null)
            {
                string boosts = "boosts " + typed.What + (typed.Who != null ? " - your " + typed.Who + " deals it" : "");
                return Say(null, top != null ? top + boosts : null, boosts, "boosts " + typed.What + " (" + typed.Pct + "% of your damage)");
            }
            if (s.Want != null)
            {
                string fits = "fits the " + s.WantBuild + " build (" + WantNoun(s.Want) + ")";
                return Say(null, top != null ? top + fits : null, fits, "fits what the " + s.WantBuild + " build wants", "fits the build (" + WantNoun(s.Want) + ")");
            }
            if (s.Nobody.Count > 0) { var nobody = s.Nobody.Distinct().ToList(); return Say(null, "nobody on the squad deals " + Or(nobody), "nobody deals " + Or(nobody), "nobody deals " + nobody[0]); }
            if (s.Lacks != null) return Say(null, s.Lacks);
            var other = s.Boosts.OrderByDescending(b => b.Weight).FirstOrDefault();
            if (other != null) return other.Power ? Say(null, "boosts your " + PowerNoun(other.What)) : Say(null, Verb(other.What) + (other.Who != null ? " - suits " + other.Who : ""), Verb(other.What));
            if (s.Scaling != null) return s.Scaling == "early" ? Say(null, "grows all run - take it early") : Say(null, "grows too slowly now (" + s.Clock + ")");
            if (!string.IsNullOrEmpty(s.Note)) return Say(null, NoteForms(s.RuleNote ? null : s.Tier, s.NoteSay ?? s.Note, s.RuleNote));
            if (s.Tier != null) return Say(null, s.Tier.ToUpperInvariant() + "-tier in the guides");
            if (s.Survival != null)
                return Say(null, s.Survival == "onehit" ? "health means nothing in One Hit" : s.Survival == "hurting" ? "keeps the squad alive - you are low on health"
                    : s.Survival == "now" ? "keeps the squad alive - survival matters now" : "keeps the squad alive");
            if (s.Keyword != null) return Say(null, Keyword(s.Keyword, s.KeywordAxis, s.KeywordValue, s.Clock));
            return Say(null, "nothing in it for this squad");
        }

        // a knowledge note (editable in knowledge.json, so it may be long): with the tier, alone, cut at its colon or comma, cut short;
        // a rule's own note (it already says this squad's case in full) is never cut at a colon - "with 1 survivor: ..." would lose it all
        static string[] NoteForms(string tier, string note, bool rule = false)
        {
            var forms = new List<string>();
            if (tier != null) forms.Add(tier.ToUpperInvariant() + "-tier - " + note);        // "S-tier - boosts short-range weapons"
            forms.Add(note);
            if (!rule) foreach (var cut in new[] { ':', ',', ';' }) { int i = note.IndexOf(cut); if (i > 8) forms.Add(note.Substring(0, i)); }
            int room = Room;
            if (note.Length > room) { int sp = note.LastIndexOf(' ', room - 1); forms.Add(sp > 8 ? note.Substring(0, sp).TrimEnd(',', ':', ';', '-', ' ') : note.Substring(0, room)); }
            forms.Add(Cut(note));         // as drawn: a note that names a powerup another mod renames longer
            return forms.ToArray();
        }

        static string Noun(ItemFit f) { return f.Typed ? f.What : f.Power ? PowerNoun(f.What) : KeywordNoun(f.What); }
        static string WantNoun(string want) { foreach (var t in TagProfile.Names) if (string.Equals(t, want, StringComparison.OrdinalIgnoreCase)) return t; return KeywordNoun(want); }

        /// <summary>A keyword of the item rules as a noun: "crits", "XP and luck", "damage to elites".</summary>
        public static string KeywordNoun(string tag)
        {
            switch ((tag ?? "").ToLowerInvariant())
            {
                case "critical": return "crits";
                case "economy": return "XP and luck";
                case "upgrade quality": return "rerolls and rarity";
                case "elites/bosses": return "damage to elites";
                case "pickups": return "pickup range";
                case "slow": return "slows";
                case "marked": return "marked enemies";
                case "taunt": return "taunts";
                case "status effects": return "status effects";
                case "survival": return "health";                         // 0.16.0 (C16-02): the item book's own tags
                case "control": return "crowd control";
                case "move": return "speed";
                default: foreach (var t in TagProfile.Names) if (string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)) return t; return tag ?? "";
            }
        }
        static string PowerNoun(string tag)
        {
            switch ((tag ?? "").ToLowerInvariant())
            {
                case "turret": return "turrets";
                case "grenade": return "grenades";
                case "deployable": return "deployables";
                case "taunt": return "taunts";
                case "melee": return "melee attacks";
                default: return (tag ?? "").ToLowerInvariant();
            }
        }
        static string Verb(string tag)
        {
            switch ((tag ?? "").ToLowerInvariant())
            {
                case "healing": return "more healing";
                case "armor": return "more armor";
                case "critical": return "more crits";
                case "dodge": return "more dodge";
                case "slow": return "slows enemies";
                case "marked": return "marks enemies";
                case "status effects": return "stronger status effects";
                case "taunt": return "taunts enemies";
                case "control": return "holds the horde back";            // 0.16.0 (C16-02): the item book's own tags
                case "move": return "faster movement";
                case "survival": return "keeps the squad alive";
                default: return "boosts " + KeywordNoun(tag);
            }
        }
        /// <summary>What an item without a squad-specific fit is scored for, in words, with the run clock's say.</summary>
        static string[] Keyword(string tag, string axis, double value, string clock)
        {
            string what;
            switch ((tag ?? "").ToLowerInvariant())
            {
                case "weapons": what = "boosts weapons"; break;
                case "abilities": what = "boosts abilities"; break;
                case "economy": what = "more XP and luck over the run"; break;
                case "cash": what = "more cash for the Training Yard"; break;
                case "elites/bosses": what = "more damage to elites and bosses"; break;
                case "pickups": what = "a bigger pickup range"; break;
                case "upgrade quality": what = "better offers: rerolls and rarity"; break;
                case "damage": what = "more damage"; break;
                default: what = Verb(tag); break;
            }
            // the clock's say needs a short name for what it is about: "More XP and luck - pays off all run (18:00 left)"
            string brief = tag == "upgrade quality" ? "better offers" : tag == "pickups" ? "pickup range" : Short(what, "over the run");
            if (axis == "economy" && value >= 1.25) return F(brief + " - pays off all run (" + clock + ")", what);
            if (axis == "economy" && value <= 0.5) return F(brief + " - too late to pay off (" + clock + ")", "too late to pay off (" + clock + ")");
            if (axis == "cash" && value <= 0.3) return F("cash only helps the Training Yard", "only helps the Training Yard");
            // 0.16.0 (C16-01e): Life Savings held - the cash rule is weighed as healing
            if (tag == "cash" && axis == "heal") return F("its cash heals you instead (Life Savings)", "cash heals you (Life Savings)", "cash heals you");
            if (axis == "boss" && value >= 1.5) return F("boss damage - this mode is about the boss", what);
            return F(what);
        }
        static string Short(string what, string tail) { return what.EndsWith(" " + tail, StringComparison.Ordinal) ? what.Substring(0, what.Length - tail.Length - 1) : what; }

        /// <summary>What an item needs that the squad does not have: a powerup tag nobody owns, or a class.</summary>
        public static string Lacks(string powerTag, IList<string> needs)
        {
            if (!string.IsNullOrEmpty(powerTag))
                switch (powerTag.ToLowerInvariant())
                {
                    case "taunt": return "nothing on the squad taunts";
                    case "deployable": return "nothing on the squad deploys";
                    case "melee": return "no melee attack on the squad";
                    default: return "no " + PowerNoun(powerTag) + " on the squad";
                }
            return needs != null && needs.Count > 0 ? "needs " + Or(needs) + " on the squad" : "nothing on the squad uses it";
        }

        // ================================================================ Research Pod cards: n tag points of one type
        public static string Tag(string type, int n, TagProfile p)
        {
            if (string.IsNullOrEmpty(type)) return Say(null, "an unknown damage type");
            string pts = "+" + n + " " + type + (n == 1 ? " tag" : " tags");
            if (p == null || !p.Known) return Say(null, pts + " - no damage data yet this run", pts);
            double fit = p.Fit(type); int cur = p.PointsOf(type);
            int pct = (int)Math.Round(p.Share(type) * 100);
            if (p.SpecialAt > 0 && cur < p.SpecialAt && cur + n >= p.SpecialAt)
                return fit > 0 ? Say(null, Special(type)) : Say(null, type + " 10-tag effect - but nobody deals " + type, type + " 10-tag effect - nobody deals it", "a 10-tag effect nobody would use");
            if (fit <= 0) return Say(null, "nobody on the squad deals " + type, "nobody deals " + type);
            string focus = p.Focus();
            if (cur > 0 && string.Equals(focus, type, StringComparison.OrdinalIgnoreCase))
                return Say(null, type + " is " + pct + "% of your damage, your highest tag", type + " is " + pct + "% of your damage");
            if (pct >= 25) return Say(null, Share(type, pct));
            return Say(null, "only " + pct + "% of your damage is " + type);
        }

        // ================================================================ stat cards (military training, Endless)
        static string Stat(CardWords w)
        {
            string label = string.IsNullOrEmpty(w.Stat) ? "this stat" : w.Stat;
            if (w.WantBuild != null) return Say(null, "the " + w.WantBuild + " build wants " + label, "the build wants " + label);
            if (w.StatWhy != null) return Say(null, w.StatWhy.Select(n => Cap(label) + " - " + n).Concat(new[] { Cap(label) }).ToArray());
            return Say(null, w.Team ? Cap(label) + " for the whole team" : "more " + label, "more " + label);
        }

        /// <summary>A stat card's note in words (after the stat's label): "weapons" / "abilities" (most of the squad's damage),
        /// "early" / "late" (an economy stat by the clock), "now" / "hurting" (survival).</summary>
        public static string[] StatNote(string kind, string clock)
        {
            switch (kind)
            {
                case "weapons": case "abilities": return F("most of your damage is " + kind, "you lean on " + kind);
                case "early": return F("pays off all run (" + clock + ")");
                case "late": return F("too late to pay off (" + clock + ")");
                case "hurting": return F("the squad is hurting");
                case "now": return F("survival matters now");
                default: return null;
            }
        }

        // ================================================================ rescue cards
        static string Recruit(RecruitSay r)
        {
            if (r == null) return null;
            if (r.Quest != null) return Quest(r.Quest);
            if (r.Liberate)
            {
                // 0.16.0 (C16-01d / e): Hijacked Signal held - two level-ups; Life Savings held - the cash heals ('a heal')
                int k = r.LiberateLevelUps; bool heal = r.CashHeals;
                if (k <= 1 && !heal)
                {
                    if (r.Full) return Say(null, "squad is full - take the level-up and cash");
                    if (r.Late != null) return Say(null, "too late for a recruit to grow (" + r.Late + ")");
                    return Say(null, "a level-up and cash instead of a recruit");
                }
                string gives = HeldRules.LiberateWhat(k, heal, "a level-up"), take = HeldRules.LiberateWhat(k, heal);
                if (r.Full) return Say(null, "squad is full - take " + take, take);
                if (r.Late != null) return Say(null, "too late for a recruit - " + gives, gives);
                if (k <= 1) return Say(null, gives + " instead of a recruit", gives);
                return heal ? Say(null, gives + " (Hijacked Signal)", gives)
                            : Say(null, gives + " (Hijacked Signal)", gives + " instead of a recruit", gives);
            }
            // 0.16.0 (C16-01b): Reserve Bench held - late in the run the recruit's own level-ups are why it still beats Liberate (earlier
            // every recruit would draw the same line: it stays a WHY reason there)
            if (r.Late != null && r.JoinLevelUps >= 1)
            {
                string lu = r.JoinLevelUps + (r.JoinLevelUps == 1 ? " level-up" : " level-ups");
                return Say(null, "recruiting gives " + lu + " (Reserve Bench)", lu + " for recruiting (Reserve Bench)", lu + " for recruiting");
            }
            if (r.Late != null) return Say(null, "little time left for a recruit to grow (" + r.Late + ")", "too late for a recruit to grow (" + r.Late + ")");
            string tier = string.IsNullOrEmpty(r.Tier) ? null : r.Tier.ToUpperInvariant() + "-tier";
            if (r.Bought > 0)
            {
                string with = And(r.Partners.Distinct().ToList()), syn = r.Bought == 1 ? "unlocked synergy with " : r.Bought + " unlocked synergies with ";
                return Say(null, tier != null ? syn + with + " - " + tier + " in the guides" : null, syn + with, syn + "your squad");
            }
            if (r.SharedType != null)
            {
                string deals = "deals " + r.SharedType + (r.SharedWith != null ? " like your " + r.SharedWith : " like your squad");
                return Say(null, tier != null ? tier + ", also " + deals : null, "also " + deals, "also deals " + r.SharedType);
            }
            if (r.TeamBonus != null) return Say(null, tier != null ? tier + ", team bonus: " + r.TeamBonus : null, "team bonus: " + r.TeamBonus);
            if (r.Boosts != null) return Say(null, r.Boosts + " would boost " + r.Covered + " of your powerups", "would boost " + r.Covered + " of your powerups");
            if (tier != null) return Say(null, tier + " recruit in the guides");
            if (r.Unbought > 0) return Say(null, r.Unbought == 1 ? "1 synergy with your squad, not unlocked yet" : r.Unbought + " synergies with your squad, none unlocked yet");
            if (r.RankLevels > 0) return Say(null, RankAway(r.RankLevels, r.Rank));
            return Say(null, "no synergy with this squad");
        }

        /// <summary>A recruit close to its class's next rank (Farm): "4 more class levels to rank 3" (0.15.x, the 10-07 review: "4 class levels
        /// from rank 3" read as cryptic). The card and the WHY band say it alike.</summary>
        public static string RankAway(int levels, int rank) { return levels + " more class level" + (levels > 1 ? "s" : "") + " to rank " + rank; }

        /// <summary>A quest rule's reason line ("quest: stay solo - take the level-up and cash") as the card says it.</summary>
        public static string Quest(string line)
        {
            string rest = line ?? "";
            if (rest.StartsWith("quest", StringComparison.OrdinalIgnoreCase)) { int c = rest.IndexOf(':'); rest = c >= 0 ? rest.Substring(c + 1).Trim() : ""; }
            if (rest.Length == 0) return "Quest: your active quest decides it";
            int dash = rest.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0 && ClassList(rest.Substring(0, dash)))
            {
                // the rule is a class list ("Huntress", "Ghost + Huntress"): it NEEDS them - and a line that names them again says it alone
                string who = rest.Substring(0, dash), then = rest.Substring(dash + 3);
                rest = then.EndsWith(" " + who, StringComparison.Ordinal) ? then : "needs " + who + " - " + then;
                dash = rest.IndexOf(" - ", StringComparison.Ordinal);
            }
            int sp = rest.Length > Room - 7 ? rest.LastIndexOf(' ', Room - 8) : -1;
            return Say(null, "Quest: " + rest, dash > 0 ? "Quest: " + rest.Substring(dash + 3) : null, "Quest: " + (sp > 0 ? rest.Substring(0, sp) : rest),
                dash > 0 ? Cut("Quest: " + rest.Substring(dash + 3)) : null, Cut("Quest: " + rest));
        }

        // "Huntress", "Ghost + Huntress": capitalised single words joined by " + "
        static bool ClassList(string s)
        {
            foreach (var part in s.Split(new[] { " + " }, StringSplitOptions.None))
                if (part.Length == 0 || !char.IsUpper(part[0]) || part.IndexOf(' ') >= 0) return false;
            return true;
        }

        // ================================================================ the reroll hint (RerollHint.cs draws it after "REROLL  -  ")
        /// <summary>"Tank or SWAT would fit this squad better (+1 more)": the first two survivors that clear the margin, and how many
        /// more do.</summary>
        public static string Reroll(IList<string> names, int more)
        {
            string who = names == null || names.Count == 0 ? "another survivor" : names.Count == 1 ? names[0] : names[0] + " or " + names[1];
            return who + " would fit this squad better" + (more > 0 ? " (+" + more + " more)" : "");
        }

        /// <summary>0.16.0 (C16-15): the short form of <see cref="Reroll"/> for the line drawn UNDER the Reroll button (1280 x 800, where it
        /// stands in the WHY band's row): "Tank fits better" - the first survivor alone, no count of the others.</summary>
        public static string RerollShort(IList<string> names)
        {
            string who = names == null || names.Count == 0 || string.IsNullOrEmpty(names[0]) ? "another survivor" : names[0];
            return who + " fits better";
        }

        // ================================================================ facts the tags and the team passives give
        /// <summary>What the squad's damage type tags and team passives say for a weapon, an ability or an evolution - the facts
        /// Synergy.TagValue / BoostValue / EvolutionFit score: the type this level carries to its 10-tag effect (the last type that
        /// close, as TagValue puts it first), one 2-3 tags short, the squad's biggest share among its types (from
        /// <paramref name="minShare"/>), a team passive that boosts it, and for an evolution (<paramref name="baseFacts"/>) the
        /// first type it adds over its base.</summary>
        public static void Tags(CardWords w, PowerFacts p, TagProfile tags, IList<TeamBoost> boosts, RunContext ctx, PowerFacts baseFacts = null, double minShare = 0.25)
        {
            if (w == null || p == null) return;
            double weight = ctx == null || ctx.D == null ? 1.0 : ctx.D.SynergyWeight;
            if (weight <= 0) return;
            if (tags != null && p.Damage.Count > 0)
            {
                bool spread = string.Equals(tags.Plan, "Spread", StringComparison.OrdinalIgnoreCase);
                string best = null; double bestShare = 0;
                foreach (var t in p.Damage.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    double share = tags.Share(t); int cur = tags.PointsOf(t);
                    if (share > bestShare) { bestShare = share; best = t; }
                    if (tags.SpecialAt <= 0 || cur >= tags.SpecialAt) continue;
                    int left = tags.SpecialAt - cur;
                    if (left <= 1) w.Special = t;
                    else if (left <= 3 && (share > 0 || spread) && w.ShortType == null) { w.ShortType = t; w.Short = left; }
                }
                if (best != null && bestShare >= minShare) { w.ShareType = best; w.SharePct = (int)Math.Round(bestShare * 100); w.ShareFrom = FirstSource(tags.SourceText(best)); }
                if (baseFacts != null)
                    foreach (var t in p.Damage)
                    {
                        if (baseFacts.Damage.Contains(t, StringComparer.OrdinalIgnoreCase)) continue;
                        w.Adds = t; w.AddsNew = tags.Share(t) <= 0; w.AddsWith = FirstSource(tags.SourceText(t));
                        if (w.AddsWith == null) w.AddsNew = true;
                        break;
                    }
            }
            if (boosts != null) foreach (var b in boosts) if (b.Covers(p)) { w.Boost = b.Name; break; }
        }

        // ================================================================ the rails (the bench holds every builder to them)
        static readonly string[] Banned = { "focus", "style:", "(auto)", "tree boost", "stack", " pts", "weighs", "new ability" };

        /// <summary>What breaks the writing rules in a line under a card (empty: none): its length, a glyph the card font lacks or
        /// that reads as markup, the ranking's own words, a bare score, a head that repeats the card's plate.</summary>
        public static List<string> Rails(string line) { return Rails(line, Budget); }

        /// <summary>The rails with <paramref name="budget"/> visible characters (a card showing "AVOID   ": <see cref="RoomBeside"/>).</summary>
        public static List<string> Rails(string line, int budget)
        {
            var bad = new List<string>();
            if (line == null) { bad.Add("no text"); return bad; }
            if (line.Length > budget) bad.Add("length " + line.Length);
            foreach (char ch in line)
                if (ch == '<' || ch == '>' || ch == '|' || ch == '\\' || ch == '^' || ch == '`' || ch < ' ' || ch > '~' && ch != '»' && ch != '×')
                { bad.Add("glyph '" + ch + "'"); break; }
            string low = line.ToLowerInvariant();
            foreach (var b in Banned) if (low.Contains(b)) bad.Add("'" + b.Trim() + "'");
            if (low.Contains("special") && !low.Contains("10-tag")) bad.Add("'special'");
            for (int i = 0; i + 1 < line.Length; i++) if (line[i] == '#' && char.IsDigit(line[i + 1])) { bad.Add("'#" + line[i + 1] + "'"); break; }
            for (int i = 1; i + 1 < line.Length; i++) if (line[i] == 'x' && char.IsDigit(line[i + 1]) && !char.IsLetter(line[i - 1])) { bad.Add("a multiplier"); break; }
            if (low.StartsWith("endless", StringComparison.Ordinal) || low.StartsWith("common", StringComparison.Ordinal) || low.StartsWith("rare", StringComparison.Ordinal) || low.StartsWith("legendary", StringComparison.Ordinal))
                bad.Add("starts with the card's rarity");
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\d\.\d\d|\d(\.\d+)? vs \d")) bad.Add("a bare score");
            return bad;
        }
    }
}
