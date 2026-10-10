#!/usr/bin/env python3
"""Advice audit: what the player did with the Companion's advice, read offline from companion.log.

For every session of the given logs (oldest file first) it reports:
  - picks and overrides by screen, and by the advice's reason head (the #1 card's headline, its numbers and names
    folded away, so weeks of logs group the same way);
  - CLOSE CALL screens and which card was taken there;
  - action hints shown (REROLL / SKIP / BANISH, and the rescue screen's reroll hint) and whether the player acted on
    them within 10 s (a reroll, the skip, the Banish button), later on the same screen, or not at all;
  - WHY views (the band under a highlighted card) per pick, and the bands that dropped reasons ('N of M shown');
  - the active quest's objectives and the run clock each one was met at;
  - the level-up pace per game mode (level-ups per run minute, by phase of the run: the priors of the pace blend);
  - the exact ties for the first place by the rule that settled them, and the stat-card weights the Companion could not
    read from the card (0.16.0; those lines count as warnings);
  - the Training Yard: purchases against the advice shown (which place was bought), refunds, and badge levels bought
    while the badge advice held them (0.16.0) - also for a session that only visited the Training Yard;
  - the warning and error lines.
Then the same numbers over every selected session together.

Standard library only; reads the log as it is (any version from 0.12 on; older lines are simply not counted).

  python advice_audit.py companion.log.1 companion.log                      every session in both files
  python advice_audit.py companion.log.1 companion.log --since 2026-10-06   sessions started on or after that day
  python advice_audit.py companion.log --since last --until 19:35:35        the last session, up to a wall time
  python advice_audit.py companion.log --brief --json audit.json            totals only, and every number as JSON
  python advice_audit.py --self-test                                        the parser against a made-up log
"""
import argparse
import json
import re
import sys
from collections import Counter, OrderedDict, defaultdict

ACT_WINDOW = 10.0                    # seconds: a hint counts as acted on when the player answers within this
PHASES = [(0, 2), (2, 5), (5, 10), (10, 20), (20, None)]      # run minutes, for the pace by phase

# The screen names of the log: [offer] / hint lines use the Companion's names, [pick] lines the game's view classes.
SCREEN_OF = {"LevelUp": "LevelUp", "Chest": "Chest", "ChestOpened": "Chest", "Military": "Military",
             "MilitaryTraining": "Military", "SOS": "SOS", "CharacterRescue": "SOS", "Hashtag": "Hashtag",
             "HashtagEvent": "Hashtag"}
SCREEN_ORDER = ["LevelUp", "Chest", "SOS", "Military", "Hashtag"]
SCREEN_LABEL = {"LevelUp": "level-up", "Chest": "chest", "SOS": "rescue", "Military": "military",
                "Hashtag": "research pod"}

RX_SESSION = re.compile(r"^==== (\d{4}-\d\d-\d\d \d\d:\d\d:\d\d) session start ====")
RX_LINE = re.compile(r"^(\d\d):(\d\d):(\d\d)(?:\.(\d{1,3}))? \[(\w+)\s*\] (.*)$")
RX_LOADED = re.compile(r"^YAZS Companion (\S+)(?: \(([^)]*)\))? loaded")
RX_OFFER = re.compile(r"^\[offer\] (\w+) (\d+:\d\d) \((\S+) horde (\d+)\)(.*)$")
RX_CARD = re.compile(r"^\[card\] #(\d+) (?:PICK|    ) (.+?) \(([^)]*)\) (-?\d+\.\d\d) - (.*)$")
RX_PICK = re.compile(r"^\[pick\] (\w+|\?) (\d+:\d\d|\S*): (.*)$")
RX_PICKED = re.compile(r"^(.*) \(#(\d+), (?:the pick|pick was (.*))\)$")
RX_BANISH_ON = re.compile(r"^banish on (.*?)(?: \(#(\d+), (-?\d+\.\d\d)(, the pick)?\))?$")
RX_HINT = re.compile(r"^\[squad\] (reroll|action) hint: (?:(\w+) (\d+:\d\d): )?(?:\((.*?)\) )?(SHOWN(?: (REROLL|SKIP|BANISH))?|not shown)")
RX_WHY = re.compile(r"^\[why\] (\w+) (\d+:\d\d): #(\d+) (.+?) (-?\d+\.\d\d) - (WHY|CLOSE CALL) '(.*)$")
RX_WHY_SHOWN = re.compile(r"\((?:([a-z ]+?), )?(?:header \+ )?(\d+) line\(?s?\)?, ([\d.]+) px, (\d+) of (\d+) shown")     # px "20.5" (the side panel, 1440p); 0.15.x: the panel says "header + 3 lines"
RX_CLOCK = re.compile(r"^\[(?:plan|offer|pick|why|shown)\] (?:\w+ )?(\d+:\d\d)[: ]")
RX_PLAN_CLOCK = re.compile(r"^\[plan\] (\d+:\d\d):")
RX_QUEST_START = re.compile(r'^\[quest\] (\S+) "([^"]*)" - (\d+) objective')
RX_QUEST_RULES = re.compile(r"^\[quest\] (.*?) -> (.*)$")
RX_QUEST_NONE = re.compile(r"^\[quest\] no active quest")
# 0.16.0: the Training Yard. The advice line (TreeUi) follows the purchases of the same read (TreeState.LogChanges logs them
# first), so a purchase belongs to the tree of the last advice line; '[yard] strip at' / '[yard] nodes of' lines do not match.
RX_YARD_ADVICE = re.compile(r"^\[yard\] (\w+): (\d+) points; buy (.*?)(?:; save for (.*?) \((\d+)\))?(?:; later (.*))?$")
RX_YARD_CHANGE = re.compile(r"^\[yard\] (bought|refunded) (.+?) (\d+)>(\d+)(?: \((?:advice #(\d+)|not advised)\))?$")
RX_YARD_BADGES = re.compile(r"^\[yard\] badges of (\w+) \(([^,]+), (\d+) survivors?, floor ([\d.]+), reach (\d+)\): (.*)$")
RX_YARD_COMPUTE = re.compile(r"^\[yard\] badge advice for (\d+) survivors? \(([^)]*)\): ([\d.]+) ms$")
RX_YARD_PART = re.compile(r"^(.+?) (?:\d+>\d+ (?:stage|levels) |0>1 pinned$|held \()")      # the badge name of one '; ' part
# 0.16.0: an exact tie for the first place (Ranker.SayTie; the rule is the text after the last ' - ') and the stat-card weights
# (Ranker.OwnRatio: 'the card's values not read' / 'read from the rarity's field' are counted as warnings).
RX_TIE = re.compile(r"^\[rank\] tie for #1 at (-?\d+\.\d\d): (.*) - (.*)$")
RX_STAT_WEIGHT = re.compile(r"^\[rank\] (\w+) stat cards: weight x(-?[\d.]+) for (.*)$")
TIE_RULES = ["build_order", "own_value", "tag_points", "further_left", "other"]
TIE_LABEL = {"build_order": "the build's order", "own_value": "own value", "tag_points": "tag points",
             "further_left": "further left", "other": "other"}


def clock_s(c):
    """'06:27' -> 387 (also '1:02:03')."""
    parts = [int(p) for p in c.split(":")]
    s = 0
    for p in parts:
        s = s * 60 + p
    return s


def mmss(sec):
    sec = int(round(sec))
    return "%02d:%02d" % (sec // 60, sec % 60)


def tie_rule(rule):
    """The rule of a '[rank] tie for #1 at' line (the text after ' - ') as a key of TIE_RULES."""
    if rule.startswith("the build's order"):
        return "build_order"
    if rule.startswith("its own value") or rule.startswith("the stat card's own value"):
        return "own_value"
    if rule.startswith("tag points"):
        return "tag_points"
    if rule.startswith("the card further left"):
        return "further_left"
    return "other"


def head_kind(why):
    """The #1 card's headline (its Why[0]) folded to a reason kind: numbers, names and the build tail left out."""
    h = why.split(";")[0].split(" - ")[0]
    h = re.sub(r", then .*$", "", h)
    h = re.sub(r"\d+", "N", h).strip()
    low = h.lower()
    m = re.match(r"^([SABCD])-tier (rescue|item)", h)
    if m:
        return m.group(1) + "-tier " + m.group(2)
    m = re.match(r"^(common|rare|legendary|endless)(, team-wide)?:", low)
    if m:
        return "training: " + m.group(1) + (" team-wide" if m.group(2) else "")
    rules = [
        (r"^(weapon level|leveling your current weapon)", "weapon level"),
        (r"^(finish|completes) the weapon", "finish the weapon"),
        (r"^(next weapon (tier|step)|next step of the weapon)", "next weapon tier"),
        (r"^first weapon", "first weapon"),
        (r"^new ability", "new ability"),
        (r"^evolution of", "evolution"),
        (r"^reaches the \w+ special", "tag special effect"),
        (r"^quest", "quest target"),
        (r"^only .* is offered", "the only one offered"),
        (r"^(some|no) \w+ damage", "damage type on the squad"),
        (r"^\w+: n% of the squad", "damage type share"),
    ]
    for rx, kind in rules:
        if re.search(rx, low):
            return kind
    if re.match(r"^(focus\b|leveling\b|level n|n>n of n|n to n of n|toward its evolution)", low):
        if "out of reach" in low:
            return "ability level (evolution out of reach)"
        return "ability toward its evolution" if "evolution" in low else "ability level"
    if re.match(r"^[^:]+: [a-z][a-z ]*$", h):
        return "fits what the squad has"
    words = h.split()
    return " ".join(words[:4])[:40] or "?"


class Hint:
    def __init__(self, t, screen, clock, action, when):
        self.t, self.screen, self.clock, self.action, self.when = t, screen, clock, action, when
        self.acted = None            # seconds to the answer, None: not acted
        self.closed = False          # the screen closed (a card taken) before any answer


class Run:
    def __init__(self, idx, session):
        self.idx = idx
        self.session = session       # the stamp of the session the run belongs to
        self.mode = None
        self.max_clock = 0
        self.levelups = []           # run clock (s) of every new level-up offer
        self.quest = None            # (id, name)
        self.quest_none = False
        self.met = OrderedDict()     # objective text -> (run clock s, wall s)
        self.quest_open = OrderedDict()


class Session:
    def __init__(self, stamp, source):
        self.stamp, self.source = stamp, source
        self.version = None
        self.commit = None
        self.lines = 0
        self.warnings, self.errors = [], []
        self.offers = Counter()          # screen -> new offers
        self.replaced = Counter()        # 'reroll' / 'banish' / '' -> replaced offers
        self.picks = defaultdict(Counter)    # screen -> {'first','other','skip','unmatched'}
        self.banishes = Counter()            # screen -> banishes of a card
        self.banish_armed = Counter()
        self.heads = defaultdict(Counter)    # head kind -> {'picks','followed','overridden','skipped'}
        self.overrides = []                  # (wall, screen, clock, taken, rank, best, head)
        self.close_calls = []                # (screen, clock, taken rank or None)
        self.hints = []
        self.why = []                        # (screen, clock, rank, name, kind, shown, total, where)
        self.why_screens = set()
        self.picks_with_why = 0
        self.runs = []
        self.ties = Counter()                # 0.16.0: tie rule (TIE_RULES) -> '[rank] tie for #1' lines
        self.ties_vs_common = 0              # of own_value: the stat card's own value against a non-stat card
        self.stat_weights = 0                # '[rank] <Rarity> stat cards: weight x..' lines
        self.stat_not_read = 0               # ... with the card's values not read (the fallback)
        self.stat_via_field = 0              # ... read from the rarity's field (GetMyRarityBonusList gave nothing)
        self.yard_advice = Counter()         # 0.16.0: Training Yard tree -> advice lines
        self.yard_purchases = self.yard_advised = self.yard_not_advised = 0
        self.yard_place = Counter()          # the advice place a purchase followed: '1', '2', '3', '4+'
        self.yard_refunds = self.yard_levels = 0
        self.yard_badge_levels = self.yard_badge_advised = self.yard_badge_held = self.yard_badge_other = 0
        self.yard_badge_lines = Counter()    # tree -> '[yard] badges of' lines
        self.yard_held = {}                  # tree -> the sorted held badge names of its last badges line
        self.yard_computes = 0
        self.yard_compute_ms = 0.0
        self.raw = []                    # the lines while reading (dropped once counted)
        self.first_t = None
        self.last_t = None


def parse(files, since=None, until=None):
    """Every session of the files (in order), as Session objects; since: 'all', 'last', or a stamp prefix
    ('2026-10-06', '2026-10-06 19:02'); until: a wall time 'HH:MM:SS' that ends each session's reading."""
    sessions = []
    cur = None
    for path in files:
        with open(path, encoding="utf-8", errors="replace") as fh:
            for raw in fh:
                line = raw.rstrip("\r\n")
                m = RX_SESSION.match(line)
                if m:
                    cur = Session(m.group(1), path)
                    sessions.append(cur)
                    continue
                if cur is None:
                    cur = Session("(before the first session header of %s)" % path, path)
                    sessions.append(cur)
                cur.lines += 1
                cur.raw.append(line)
    if since and since not in ("all", ""):
        if since == "last":
            sessions = sessions[-1:]
        else:
            sessions = [s for s in sessions if not s.stamp.startswith("(") and s.stamp >= since]
    until_s = None
    if until:
        until_s = clock_s(until)
    for s in sessions:
        Reader(s, until_s).read()
        s.raw = None
    return sessions


class Reader:
    """One session's lines, in order, into its counts."""

    def __init__(self, s, until_s):
        self.s = s
        self.until = until_s
        self.day = 0
        self.prev_t = None
        self.clock = 0               # the run clock last seen (s)
        self.run = None
        self.screen = None           # the open selection screen: (screen, clock) of its last offer
        self.cards = {}              # rank -> (name, score, head) of the open screen
        self.close_call = False
        self.why_on_screen = 0
        self.hints_open = []         # hints of the open screen not yet answered
        self.yard_tree = None        # the Training Yard tree of the last advice line
        self.yard_badges = {}        # tree -> the badge names of its last '[yard] badges of' line
        self.yard_held_now = {}      # tree -> the badges held there (by the badge advice)
        self.cur_line = ""           # the whole log line being read (a counted warning keeps it)

    def wall(self, m):
        t = int(m.group(1)) * 3600 + int(m.group(2)) * 60 + int(m.group(3)) + (int((m.group(4) or "0").ljust(3, "0")) / 1000.0)
        t += self.day * 86400
        if self.prev_t is not None and t < self.prev_t - 43200:
            self.day += 1
            t += 86400
        self.prev_t = t
        return t

    def new_run(self):
        self.run = Run(len(self.s.runs) + 1, self.s.stamp)
        self.s.runs.append(self.run)

    def run_starts(self):
        """The quest lines come first in a run (before its '[plan] 00:00'): a run already under way is over."""
        if self.run is None or self.clock > 5 or self.run.levelups:
            self.new_run()
            self.clock = 0

    def see_clock(self, c):
        sec = clock_s(c)
        if self.run is None or sec + 5 < self.clock:
            self.new_run()
        self.clock = sec
        self.run.max_clock = max(self.run.max_clock, sec)

    def answer(self, t, action):
        for h in self.hints_open:
            if h.acted is None and h.action == action:
                h.acted = t - h.t
        self.hints_open = [h for h in self.hints_open if h.acted is None]

    def close_screen(self):
        for h in self.hints_open:
            h.closed = True
        self.hints_open = []
        self.screen = None
        self.cards = {}
        self.close_call = False
        self.why_on_screen = 0

    def read(self):
        s = self.s
        for line in s.raw:
            m = RX_LINE.match(line)
            if not m:
                continue
            t = self.wall(m)
            if self.until is not None and t - self.day * 86400 > self.until:
                break
            level, msg = m.group(5), m.group(6)
            if s.first_t is None:
                s.first_t = t
            s.last_t = t
            if level in ("Warning",):
                s.warnings.append(line)
            elif level in ("Error", "Fatal"):
                s.errors.append(line)
            if not msg.startswith("["):
                lm = RX_LOADED.match(msg)
                if lm:
                    s.version, s.commit = lm.group(1), lm.group(2)
                continue
            self.cur_line = line
            self.line(t, msg)
        for h in self.hints_open:
            h.closed = True

    def line(self, t, msg):
        s = self.s
        if msg.startswith("[yard] "):
            self.yard(msg)
            return
        if msg.startswith("[rank] "):
            self.rank(msg)
            return
        pm = RX_PLAN_CLOCK.match(msg)
        if pm:
            self.see_clock(pm.group(1))
            return
        om = RX_OFFER.match(msg)
        if om:
            screen = SCREEN_OF.get(om.group(1), om.group(1))
            self.see_clock(om.group(2))
            tail = om.group(5)
            if self.run.mode is None:
                self.run.mode = om.group(3)
            if tail.startswith(" replaced"):
                by = re.match(r" replaced(?: \((\w+)\))?", tail).group(1) or ""
                s.replaced[by] += 1
                if by in ("reroll", ""):
                    self.answer(t, "REROLL")
                self.cards = {}
                self.close_call = False
            else:
                if self.screen is not None:
                    self.close_screen()
                if tail.strip() == "reroll":
                    s.replaced["reroll"] += 1
                    self.answer(t, "REROLL")
                else:
                    s.offers[screen] += 1
                    if screen == "LevelUp":
                        self.run.levelups.append(clock_s(om.group(2)))
            self.screen = (screen, om.group(2))
            return
        cm = RX_CARD.match(msg)
        if cm:
            self.cards[int(cm.group(1))] = (cm.group(2), float(cm.group(4)), head_kind(cm.group(5)))
            return
        hm = RX_HINT.match(msg)
        if hm:
            if hm.group(5).startswith("SHOWN"):
                action = hm.group(6) or "REROLL"         # the rescue screen's hint is a reroll
                scr = SCREEN_OF.get(hm.group(2), hm.group(2)) if hm.group(2) else (self.screen[0] if self.screen else "SOS")
                clk = hm.group(3) or (self.screen[1] if self.screen else "?")
                h = Hint(t, scr, clk, action, hm.group(4))
                s.hints.append(h)
                self.hints_open.append(h)
            return
        wm = RX_WHY.match(msg)
        if wm:
            sm = RX_WHY_SHOWN.search(msg)
            shown = total = None
            where = ""
            if sm:
                where, shown, total = (sm.group(1) or ""), int(sm.group(4)), int(sm.group(5))
            screen = SCREEN_OF.get(wm.group(1), wm.group(1))
            s.why.append((screen, wm.group(2), int(wm.group(3)), wm.group(4), wm.group(6), shown, total, where))
            s.why_screens.add((screen, wm.group(2)))
            self.why_on_screen += 1
            if wm.group(6) == "CLOSE CALL":
                self.close_call = True
            return
        km = RX_PICK.match(msg)
        if km:
            self.pick(t, km)
            return
        qm = RX_QUEST_START.match(msg)
        if qm:
            self.run_starts()
            self.run.quest = (qm.group(1), qm.group(2))
            return
        if RX_QUEST_NONE.match(msg):
            self.run_starts()
            self.run.quest_none = True
            return
        rm = RX_QUEST_RULES.match(msg)
        if rm and self.run is not None:
            for part in rm.group(2).split("; "):
                part = re.sub(r" - not followed \(.*\)$| \(info only: no card moves\)$", "", part)
                if part.endswith(" met"):
                    key = part[:-4]
                    if key not in self.run.met:
                        self.run.met[key] = (self.clock, t)
                else:
                    key = part.split(": ")[0].split(" out of reach")[0]
                    self.run.quest_open.setdefault(key, (self.clock, t))
            return
        cl = RX_CLOCK.match(msg)
        if cl:
            self.see_clock(cl.group(1))

    def yard(self, msg):
        """A '[yard] ' line (0.15.0 advice and purchases, 0.16.0 badge plans): the Training Yard counts."""
        s = self.s
        am = RX_YARD_ADVICE.match(msg)
        if am:
            s.yard_advice[am.group(1)] += 1
            self.yard_tree = am.group(1)
            return
        ch = RX_YARD_CHANGE.match(msg)
        if ch:
            tree = self.yard_tree or "?"
            if ch.group(1) == "refunded":
                s.yard_refunds += 1
                return
            name = ch.group(2)
            lv = int(ch.group(4)) - int(ch.group(3))
            s.yard_purchases += 1
            s.yard_levels += lv
            advised = ch.group(5) is not None
            if advised:
                n = int(ch.group(5))
                s.yard_advised += 1
                s.yard_place[str(n) if n < 4 else "4+"] += 1
            else:
                s.yard_not_advised += 1
            if name.endswith(" Badge") or name in self.yard_badges.get(tree, ()):
                s.yard_badge_levels += lv
                if advised:
                    s.yard_badge_advised += lv
                elif name in self.yard_held_now.get(tree, ()):
                    s.yard_badge_held += lv
                else:
                    s.yard_badge_other += lv
            return
        bm = RX_YARD_BADGES.match(msg)
        if bm:
            tree = bm.group(1)
            s.yard_badge_lines[tree] += 1
            names, held = set(), set()
            for part in bm.group(6).split("; "):
                pm = RX_YARD_PART.match(part)
                if not pm:
                    continue                 # 'nothing to plan'
                names.add(pm.group(1))
                if " held (" in part:
                    held.add(pm.group(1))
            self.yard_badges[tree] = names
            self.yard_held_now[tree] = held
            s.yard_held[tree] = sorted(held)
            return
        cm = RX_YARD_COMPUTE.match(msg)
        if cm:
            s.yard_computes += 1
            s.yard_compute_ms = max(s.yard_compute_ms, float(cm.group(3)))

    def rank(self, msg):
        """A '[rank] ' line (0.16.0): the exact ties for the first place, and the stat-card weights."""
        s = self.s
        tm = RX_TIE.match(msg)
        if tm:
            key = tie_rule(tm.group(3))
            s.ties[key] += 1
            if tm.group(3).startswith("the stat card's own value"):
                s.ties_vs_common += 1
            return
        wm = RX_STAT_WEIGHT.match(msg)
        if wm:
            s.stat_weights += 1
            if "the card's values not read" in msg:
                s.stat_not_read += 1
            if "read from the rarity's field" in msg:
                s.stat_via_field += 1
            if ("the card's values not read" in msg or "read from the rarity's field" in msg) and not (s.warnings and s.warnings[-1] is self.cur_line):
                s.warnings.append(self.cur_line)         # an Info line, counted as a warning (once, were it ever logged as one)

    def pick(self, t, km):
        s = self.s
        screen = SCREEN_OF.get(km.group(1), km.group(1))
        what = km.group(3)
        if km.group(2):
            try:
                self.see_clock(km.group(2))
            except ValueError:
                pass
        if what.startswith("the Banish button pressed"):
            s.banish_armed[screen] += 1
            self.answer(t, "BANISH")
            return
        bm = RX_BANISH_ON.match(what)
        if bm and what.startswith("banish on "):
            s.banishes[screen] += 1
            self.answer(t, "BANISH")
            return
        best = self.cards.get(1)
        head = best[2] if best else None
        if what == "skip / nothing":
            s.picks[screen]["skip"] += 1
            self.answer(t, "SKIP")
            if head:
                s.heads[head]["picks"] += 1
                s.heads[head]["skipped"] += 1
            rank = None
        else:
            pk = RX_PICKED.match(what)
            if pk:
                rank = int(pk.group(2))
                if rank == 1:
                    s.picks[screen]["first"] += 1
                else:
                    s.picks[screen]["other"] += 1
                    s.overrides.append((t, screen, km.group(2), pk.group(1), rank, pk.group(3), head))
                if head:
                    s.heads[head]["picks"] += 1
                    s.heads[head]["followed" if rank == 1 else "overridden"] += 1
            else:
                rank = None
                s.picks[screen]["unmatched"] += 1
        if self.close_call:
            s.close_calls.append((screen, km.group(2), rank))
        if self.why_on_screen > 0:
            s.picks_with_why += 1
        self.close_screen()


# ------------------------------------------------------------------------------------------------ the report
def merge(sessions):
    """The selected sessions as one (for the totals)."""
    tot = Session("all", "")
    for s in sessions:
        tot.lines += s.lines
        tot.warnings += s.warnings
        tot.errors += s.errors
        tot.offers.update(s.offers)
        tot.replaced.update(s.replaced)
        for k, v in s.picks.items():
            tot.picks[k].update(v)
        tot.banishes.update(s.banishes)
        tot.banish_armed.update(s.banish_armed)
        for k, v in s.heads.items():
            tot.heads[k].update(v)
        tot.overrides += s.overrides
        tot.close_calls += s.close_calls
        tot.hints += s.hints
        tot.why += s.why
        tot.why_screens |= {(s.stamp,) + k for k in s.why_screens}
        tot.picks_with_why += s.picks_with_why
        tot.runs += s.runs
        tot.ties.update(s.ties)
        tot.ties_vs_common += s.ties_vs_common
        tot.stat_weights += s.stat_weights
        tot.stat_not_read += s.stat_not_read
        tot.stat_via_field += s.stat_via_field
        tot.yard_advice.update(s.yard_advice)
        tot.yard_purchases += s.yard_purchases
        tot.yard_advised += s.yard_advised
        tot.yard_not_advised += s.yard_not_advised
        tot.yard_place.update(s.yard_place)
        tot.yard_refunds += s.yard_refunds
        tot.yard_levels += s.yard_levels
        tot.yard_badge_levels += s.yard_badge_levels
        tot.yard_badge_advised += s.yard_badge_advised
        tot.yard_badge_held += s.yard_badge_held
        tot.yard_badge_other += s.yard_badge_other
        tot.yard_badge_lines.update(s.yard_badge_lines)
        tot.yard_held.update(s.yard_held)                # the last seen per tree
        tot.yard_computes += s.yard_computes
        tot.yard_compute_ms = max(tot.yard_compute_ms, s.yard_compute_ms)
    return tot


def has_yard(s):
    """The session logged a Training Yard advice, purchase, refund or badge plan."""
    return bool(s.yard_advice or s.yard_purchases or s.yard_refunds or s.yard_badge_lines)


def pct(a, b):
    return "-" if b == 0 else "%d%%" % round(100.0 * a / b)


def numbers(s):
    """Every reported number of a session (or of the merged totals), as plain data (the JSON and the self-test)."""
    picks = {}
    for scr in SCREEN_ORDER + sorted(k for k in s.picks if k not in SCREEN_ORDER):
        p = s.picks.get(scr)
        if not p and not s.offers.get(scr) and not s.banishes.get(scr):
            continue
        p = p or Counter()
        cards = p["first"] + p["other"]
        picks[scr] = {"offers": s.offers.get(scr, 0), "first": p["first"], "other": p["other"], "skip": p["skip"],
                      "unmatched": p["unmatched"], "banish": s.banishes.get(scr, 0),
                      "follow": None if cards == 0 else round(p["first"] / float(cards), 3)}
    heads = {k: dict(v) for k, v in sorted(s.heads.items(), key=lambda kv: (-kv[1]["picks"], kv[0]))}
    cc = {"screens": len(s.close_calls), "took_first": sum(1 for c in s.close_calls if c[2] == 1),
          "took_second": sum(1 for c in s.close_calls if c[2] == 2),
          "other": sum(1 for c in s.close_calls if c[2] not in (1, 2))}
    hints = {"shown": len(s.hints), "by_action": dict(Counter(h.action for h in s.hints)),
             "acted_10s": sum(1 for h in s.hints if h.acted is not None and h.acted <= ACT_WINDOW),
             "acted_later": sum(1 for h in s.hints if h.acted is not None and h.acted > ACT_WINDOW),
             "not_acted": sum(1 for h in s.hints if h.acted is None)}
    drops = [w for w in s.why if w[5] is not None and w[5] < w[6]]
    why = {"bands": len(s.why), "screens": len(s.why_screens), "cards": len({(w[0], w[1], w[2], w[3]) for w in s.why}),
           "close_call_bands": sum(1 for w in s.why if w[4] == "CLOSE CALL"),
           "picks": sum(p["first"] + p["other"] + p["skip"] + p["unmatched"] for p in s.picks.values()),
           "picks_with_why": s.picks_with_why, "drops": len(drops),
           "drop_where": dict(Counter(w[7] or "?" for w in drops))}
    why["per_pick"] = None if why["picks"] == 0 else round(len(s.why) / float(why["picks"]), 2)
    pace = pace_of(s.runs)
    quests = []
    for r in s.runs:
        if r.quest:
            quests.append({"session": r.session, "run": r.idx, "quest": r.quest[1], "id": r.quest[0], "run_length": mmss(r.max_clock),
                           "met": [{"objective": k, "clock": mmss(v[0])} for k, v in r.met.items()],
                           "open": [k for k in r.quest_open if k not in r.met]})
    ties = OrderedDict([("count", sum(s.ties.values()))] + [(k, s.ties.get(k, 0)) for k in TIE_RULES]
                       + [("own_value_vs_common", s.ties_vs_common)])
    stat_weights = OrderedDict([("lines", s.stat_weights), ("not_read", s.stat_not_read), ("via_field", s.stat_via_field)])
    yard = OrderedDict([("advice_lines", dict(s.yard_advice)), ("purchases", s.yard_purchases), ("advised", s.yard_advised),
                        ("not_advised", s.yard_not_advised), ("advice_place", dict(s.yard_place)), ("refunds", s.yard_refunds),
                        ("levels_bought", s.yard_levels), ("badge_levels_bought", s.yard_badge_levels),
                        ("badge_advised", s.yard_badge_advised), ("badge_while_held", s.yard_badge_held),
                        ("badge_not_in_advice", s.yard_badge_other), ("badge_lines", dict(s.yard_badge_lines)),
                        ("held", dict(s.yard_held)), ("computes", s.yard_computes), ("compute_ms_max", s.yard_compute_ms)])
    return OrderedDict([("picks", picks), ("heads", heads), ("close_calls", cc), ("hints", hints), ("why", why),
                        ("quests", quests), ("pace", pace), ("runs", len(s.runs)),
                        ("replaced", dict(s.replaced)), ("banish_armed", sum(s.banish_armed.values())),
                        ("ties", ties), ("stat_weights", stat_weights), ("yard", yard),
                        ("warnings", len(s.warnings)), ("errors", len(s.errors))])


def pace_of(runs):
    by = OrderedDict()
    for r in runs:
        if not r.levelups or r.mode is None:
            continue
        d = by.setdefault(r.mode, {"runs": 0, "levelups": 0, "minutes": 0.0, "phases": OrderedDict()})
        d["runs"] += 1
        d["levelups"] += len(r.levelups)
        length = max(r.max_clock, r.levelups[-1]) / 60.0
        d["minutes"] += length
        for lo, hi in PHASES:
            key = "%d-%s" % (lo, hi if hi is not None else "")
            ph = d["phases"].setdefault(key, {"levelups": 0, "minutes": 0.0})
            top = length if hi is None else min(length, hi)
            if top > lo:
                ph["minutes"] += top - lo
            ph["levelups"] += sum(1 for c in r.levelups if c / 60.0 >= lo and (hi is None or c / 60.0 < hi))
    for d in by.values():
        d["per_minute"] = None if d["minutes"] <= 0 else round(d["levelups"] / d["minutes"], 2)
        d["minutes"] = round(d["minutes"], 2)
        for ph in d["phases"].values():
            ph["per_minute"] = None if ph["minutes"] <= 0.05 else round(ph["levelups"] / ph["minutes"], 2)
            ph["minutes"] = round(ph["minutes"], 2)
    return by


def yard_block(n, w):
    """The Training Yard line of a session block (nothing when the session had no yard line)."""
    y = n["yard"]
    if not (y["advice_lines"] or y["purchases"] or y["refunds"] or y["badge_lines"]):
        return
    trees = sorted(y["advice_lines"].items(), key=lambda kv: (-kv[1], kv[0]))
    parts = ["advice lines %d%s" % (sum(y["advice_lines"].values()), " (%s)" % ", ".join("%s %d" % kv for kv in trees) if trees else "")]
    if y["purchases"]:
        places = sorted(y["advice_place"].items(), key=lambda kv: (kv[0] == "4+", kv[0]))
        parts.append("purchases %d (%d advised%s; %d not advised)" % (
            y["purchases"], y["advised"], (": " + ", ".join("#%s x%d" % kv for kv in places)) if places else "", y["not_advised"]))
    else:
        parts.append("purchases 0")
    parts.append("refunds %d" % y["refunds"])
    parts.append("levels bought %d" % y["levels_bought"])
    parts.append("badge levels bought %d%s" % (y["badge_levels_bought"], " (%d advised, %d not advised while held, %d other)" % (
        y["badge_advised"], y["badge_while_held"], y["badge_not_in_advice"]) if y["badge_levels_bought"] else ""))
    if y["badge_lines"]:
        held = "; ".join("%s %s" % (t, ", ".join(names)) for t, names in sorted(y["held"].items()) if names)
        parts.append("badge plans %d (held: %s)" % (sum(y["badge_lines"].values()), held or "none"))
    if y["computes"]:
        parts.append("badge advice computed %dx (max %.1f ms)" % (y["computes"], y["compute_ms_max"]))
    w("  Training Yard: " + " | ".join(parts) + "\n")


def ties_block(n, w):
    """The tie and stat-card weight lines of a session block (0.16.0; nothing when the session logged neither)."""
    t, sw = n["ties"], n["stat_weights"]
    if t["count"]:
        w("  ties for #1: %d (%s)\n" % (t["count"], ", ".join("%s %d" % (TIE_LABEL[k], t[k]) for k in TIE_RULES if k != "other" or t[k])))
    if sw["lines"]:
        w("  stat-card weights logged: %d (the card's values not read %d, read from the rarity's field %d)\n" % (
            sw["lines"], sw["not_read"], sw["via_field"]))


def report(s, title, brief=False, out=None, total=False):
    n = numbers(s)
    w = (out or sys.stdout).write
    w(title + "\n")
    if s.lines == 0:
        w("  (no lines)\n")
        return n
    total_picks = n["why"]["picks"]
    if total_picks == 0 and not s.offers:
        w("  no selection screen in this session; %d warning(s), %d error(s)\n" % (n["warnings"], n["errors"]))
        for l in (s.warnings + s.errors)[:5]:
            w("    " + l[:200] + "\n")
        yard_block(n, w)
        return n
    w("  picks by screen       offers  #1 taken  other  skip  banish  followed\n")
    for scr, p in n["picks"].items():
        w("    %-18s %6d  %8d  %5d  %4d  %6d  %8s%s\n" % (SCREEN_LABEL.get(scr, scr), p["offers"], p["first"], p["other"],
                                                    p["skip"], p["banish"], pct(p["first"], p["first"] + p["other"]),
                                                    "" if not p["unmatched"] else "   (%d card(s) not matched)" % p["unmatched"]))
    rr = n["replaced"]
    if rr:
        w("    replaced offers: " + ", ".join("%d by %s" % (v, k or "a refill") for k, v in sorted(rr.items())) + "\n")
    w("  advice by reason head (the #1 card's)    picks  followed  other  skip\n")
    heads = list(n["heads"].items())
    for k, v in heads[: (8 if brief else 14)]:
        w("    %-38s %5d  %8s  %5d  %4d\n" % (k[:38], v.get("picks", 0), pct(v.get("followed", 0), v.get("followed", 0) + v.get("overridden", 0)),
                                           v.get("overridden", 0), v.get("skipped", 0)))
    if len(heads) > (8 if brief else 14):
        rest = heads[(8 if brief else 14):]
        w("    (%d more kinds: %d picks, %d other)\n" % (len(rest), sum(v.get("picks", 0) for _, v in rest), sum(v.get("overridden", 0) for _, v in rest)))
    if not brief and s.overrides:
        w("    overridden: " + "; ".join("%s %s took %s (#%d) over %s" % (SCREEN_LABEL.get(o[1], o[1]), o[2], o[3], o[4], o[5]) for o in s.overrides[:6])
          + ("; ..." if len(s.overrides) > 6 else "") + "\n")
    cc = n["close_calls"]
    w("  CLOSE CALL screens: %d - took the #1 %d, the close second %d, another or none %d\n" % (cc["screens"], cc["took_first"], cc["took_second"], cc["other"]))
    ties_block(n, w)
    h = n["hints"]
    acts = ", ".join("%s %d" % (k, v) for k, v in sorted(h["by_action"].items())) or "none"
    w("  hints shown: %d (%s) - acted on within %ds: %d, later on the same screen: %d, not acted on: %d\n" % (h["shown"], acts, ACT_WINDOW, h["acted_10s"], h["acted_later"], h["not_acted"]))
    if not brief:
        for x in s.hints[:8]:
            what = ("answered after %.1f s" % x.acted) if x.acted is not None else ("screen closed with a card" if x.closed else "no answer")
            w("    %s %s %s%s: %s\n" % (SCREEN_LABEL.get(x.screen, x.screen), x.clock, x.action, " (%s)" % x.when if x.when else "", what))
        if len(s.hints) > 8:
            w("    ... %d more\n" % (len(s.hints) - 8))
    yard_block(n, w)
    y = n["why"]
    w("  WHY: %d band(s) on %d screen(s), %d card(s) looked at (%d CLOSE CALL); %s per pick, %d of %d picks after a WHY view\n" % (
        y["bands"], y["screens"], y["cards"], y["close_call_bands"], "-" if y["per_pick"] is None else "%.2f" % y["per_pick"], y["picks_with_why"], y["picks"]))
    if y["drops"]:
        w("    reasons dropped ('N of M shown') on %d band(s): %s\n" % (y["drops"], ", ".join("%s %d" % (k, v) for k, v in sorted(y["drop_where"].items()))))
        if not brief:
            for d in [x for x in s.why if x[5] is not None and x[5] < x[6]][:8]:
                w("      %s %s #%d %s: %d of %d shown (%s)\n" % (SCREEN_LABEL.get(d[0], d[0]), d[1], d[2], d[3], d[5], d[6], d[7] or "?"))
    for q in n["quests"]:
        met = "; ".join("%s at %s" % (m["objective"], m["clock"]) for m in q["met"])
        w('  quest "%s" (%srun %d, %s of run clock): %s\n' % (q["quest"], ("session %s, " % q["session"]) if total else "", q["run"], q["run_length"],
                                                          ("met: " + met) if met else "no objective logged as met" + (" (open: %s)" % ", ".join(q["open"]) if q["open"] else "")))
    for mode, d in n["pace"].items():
        ph = ", ".join("%s min %s" % (k.rstrip("-") + ("+" if k.endswith("-") else ""), "%.2f" % p["per_minute"]) for k, p in d["phases"].items() if p["per_minute"] is not None)
        w("  level-up pace %s: %d level-up(s) in %.1f run minute(s) over %d run(s) = %s per minute (%s)\n" % (
            mode, d["levelups"], d["minutes"], d["runs"], "-" if d["per_minute"] is None else "%.2f" % d["per_minute"], ph or "-"))
    w("  warnings: %d, errors: %d\n" % (n["warnings"], n["errors"]))
    for l in (s.errors + s.warnings)[: (3 if brief else 6)]:
        w("    " + l[:200] + "\n")
    return n


def main(argv):
    ap = argparse.ArgumentParser(description="Advice audit over companion.log files (oldest first).")
    ap.add_argument("logs", nargs="*", help="companion.log.1 companion.log ... (oldest first)")
    ap.add_argument("--since", default="all", help="'all' (default), 'last', or a session stamp prefix: 2026-10-06 / '2026-10-06 19:02'")
    ap.add_argument("--until", default=None, help="stop each session's reading at this wall time (HH:MM:SS)")
    ap.add_argument("--brief", action="store_true", help="the totals only")
    ap.add_argument("--json", default=None, help="write every number (per session and the totals) to this file")
    ap.add_argument("--self-test", action="store_true", help="check the parser against a made-up log and stop")
    a = ap.parse_args(argv)
    if a.self_test:
        return self_test()
    if not a.logs:
        ap.print_usage()
        print("advice_audit: name the logs (BepInEx/plugins/YazsCompanion/companion.log.1 companion.log)")
        return 2
    try:
        sessions = parse(a.logs, a.since, a.until)
    except OSError as e:
        print("advice_audit: %s" % e)
        return 2
    if not sessions:
        print("advice_audit: no session in %s matches --since %s" % (", ".join(a.logs), a.since))
        return 2
    played = [s for s in sessions if sum(s.offers.values()) > 0 or any(s.picks.values())]
    yard = [s for s in sessions if has_yard(s)]
    shown = [s for s in sessions if s in played or s in yard]          # in log order; a Training Yard visit alone counts too
    out = {"sessions": []}
    if not a.brief:
        for s in shown:
            v = s.version + (" (%s)" % s.commit if s.commit else "") if s.version else "version not logged"
            out["sessions"].append(dict(stamp=s.stamp, version=s.version, commit=s.commit,
                                        numbers=report(s, "==== session %s - %s - %d run(s), %d lines" % (s.stamp, v, len(s.runs), s.lines))))
            print()
    quiet = len(sessions) - len(shown)
    tot = merge(sessions)
    title = "==== TOTAL over %d session(s) with a selection screen or Training Yard lines (of %d selected%s)" % (
        len(shown), len(sessions), "; %d without either" % quiet if quiet else "")
    if a.brief or len(shown) != 1:
        out["total"] = report(tot, title, brief=a.brief, total=True)
    else:
        out["total"] = numbers(tot)          # one session: its block above is the total
    out["total"]["sessions"] = len(sessions)
    out["total"]["played"] = len(played)
    out["total"]["yard_sessions"] = len(yard)
    if a.json:
        with open(a.json, "w", encoding="utf-8") as fh:
            json.dump(out, fh, indent=1)
        print("numbers written to %s" % a.json)
    return 0


# ------------------------------------------------------------------------------------------------ the self-test
SAMPLE = """==== 2030-01-02 10:00:00 session start ====
10:00:01.000 [Info] [hooks] 38 methods patched (class by class; 0 patch classes failed)
10:00:01.001 [Info] YAZS Companion 9.9.9 (abc1234) loaded from X; 38 methods patched
10:00:01.002 [Info] [why] hooks: OnSelected Hashtag ok, Item ok, Military ok, Skill ok, SOS ok | OnDeselected (one body for every card class) ok
10:01:00.000 [Info] [quest] GameHubQuest_Test_01 "Test Quest" - 1 objective (objectives), all must hold: 1. FinishWithWeapons (Live)
10:01:00.100 [Info] [plan] 00:00: TANK  next  Shotgun
10:01:10.000 [Info] [offer] LevelUp 00:30 (Normal horde 1)
10:01:10.001 [Info] [card] #1 PICK Medical Drone (ability, Medic) 4.84 - new ability - #2 in Rifleman; take each ability once early
10:01:10.002 [Info] [card] #2      Handgun (weapon, Medic) 3.72 - weapon level: 1 to 2 of 4; style: abilities first
10:01:10.003 [Info] [shown] LevelUp 00:30: 1 'Fills an empty ability slot early' | 2 'Level 2 of 4'
10:01:11.000 [Info] [why] LevelUp 00:30: #1 Medical Drone 4.84 - WHY 'Fills an empty ability slot early' | 'Ahead of Handgun' (over the buttons, 1 line, 15 px, 2 of 2 shown; 900 x 44 units at 1,1)
10:01:12.000 [Info] [why] LevelUp 00:30: #2 Handgun 3.72 - WHY 'Medical Drone goes first' | 'a' | 'b' (over the buttons, 1 line, 15 px, 2 of 3 shown)
10:01:13.000 [Info] [pick] LevelUp 00:30: Handgun (#2, pick was Medical Drone)
10:01:40.000 [Info] [offer] LevelUp 01:00 (Normal horde 1)
10:01:40.001 [Info] [card] #1 PICK Handgun (weapon, Medic) 4.00 - weapon level: 2 to 3 of 4
10:01:40.002 [Info] [card] #2      Stimpack (ability, Medic) 3.99 - new ability
10:01:41.000 [Info] [why] LevelUp 01:00: #1 Handgun 4.00 - CLOSE CALL 'Either works - a hair ahead of Stimpack' (over the buttons, 1 line, 15 px, 1 of 1 shown)
10:01:41.500 [Info] [why] LevelUp 01:00: #2 Stimpack 3.99 - WHY 'Fills an empty ability slot early' | 'Kinetic is 80% of your damage' (side wing left, 4 lines, 20.5 px, 2 of 3 shown; 548 x 214 units at -572,1643; wings 660 units a side: left 611x214 (text 16.8 em); right 611x214 (text 16.8 em))
10:01:42.000 [Info] [pick] LevelUp 01:00: Handgun (#1, the pick)
10:02:00.000 [Info] [offer] SOS 01:20 (Normal horde 1)
10:02:00.001 [Info] [card] #1 PICK Huntress (survivor) 5.07 - S-tier rescue in the guides
10:02:00.002 [Info] [squad] reroll hint: SHOWN - Tank would rate higher (6.17 vs Huntress 5.07, +1.10) | rerolls 8
10:02:03.000 [Info] [offer] SOS 01:20 (Normal horde 1) replaced (reroll): gone Huntress; new Tank
10:02:03.001 [Info] [card] #1 PICK Tank (survivor) 6.17 - A-tier rescue in the guides
10:02:03.002 [Info] [squad] reroll hint: (the cards were replaced) not shown - the best survivor is on the cards
10:02:05.000 [Info] [pick] CharacterRescue 01:20: Tank (#1, the pick)
10:02:30.000 [Info] [offer] Chest 01:45 (Normal horde 1)
10:02:30.001 [Info] [card] #1 PICK Potato (item) 1.31 - economy
10:02:30.002 [Info] [squad] action hint: Chest 01:45: SHOWN SKIP - every card here would hurt this squad | skip +675 cash | actions Reroll, Skip | 'every card here would hurt this squad - take the skip bonus'
10:02:50.000 [Info] [pick] ChestOpened 01:45: skip / nothing
10:03:00.000 [Info] [offer] LevelUp 02:10 (Normal horde 1)
10:03:00.001 [Info] [card] #1 PICK Handgun (weapon, Medic) 4.00 - weapon level: 3 to 4 of 4
10:03:01.000 [Warning] [badge] something odd
10:03:02.000 [Info] [quest] Test Quest -> max Handgun met
10:03:03.000 [Info] [pick] LevelUp 02:10: Handgun (#1, the pick)
10:04:00.000 [Info] [plan] 03:00: TANK  Shotgun 4/4
==== 2030-01-02 10:20:00 session start ====
10:20:01.000 [Info] [yard] badge advice for 9 survivors (Normal I): 4.2 ms
10:20:01.001 [Info] [yard] badges of Medic (Normal I, 9 survivors, floor 0.15, reach 2): Growth Badge 1>3 stage 9/9 0.41/pt; Soul Badge held (next levels 0.12/pt); Healing Badge held (from level 4)
10:20:01.002 [Info] [yard] Medic: 9 points; buy 1 Experiment 21 3>5 (9); later Growth Badge 1>3
10:20:05.000 [Info] [yard] bought Experiment 21 3>4 (advice #1)
10:20:05.001 [Info] [yard] Medic: 5 points; buy 1 Experiment 21 4>5 (5); later Growth Badge 1>3
10:20:07.000 [Info] [yard] bought Healing Badge 0>1 (not advised)
10:20:07.001 [Info] [yard] Medic: 4 points; buy nothing; save for Experiment 21 4>5 (5)
10:20:09.000 [Info] [yard] refunded Healing Badge 1>0
10:20:09.001 [Info] [yard] Medic: 5 points; buy 1 Experiment 21 4>5 (5)
10:20:11.000 [Info] [yard] bought Experiment 21 4>5 (advice #1)
10:20:11.001 [Info] [yard] Medic: 0 points; buy nothing; save for Growth Badge 1>3 (5)
==== 2030-01-02 11:00:00 session start ====
11:00:01.000 [Info] [hooks] 38 methods patched (class by class; 0 patch classes failed)
11:00:05.000 [Info] [offer] LevelUp 00:20 (Endless horde 1)
11:00:05.001 [Info] [rank] Endless stat cards: weight x0.40 for Pickup Range (the card's values not read: NullReferenceException - the fallback)
11:00:05.001 [Info] [rank] tie for #1 at 5.00: Shotgun before Multishot - the card further left
11:00:05.001 [Info] [card] #1 PICK Shotgun (weapon, Tank) 5.00 - next weapon tier
11:00:05.002 [Info] [card] #2      Multishot (weapon, Tank) 5.00 - weapon level: 1 to 2 of 4
11:00:06.000 [Info] [pick] LevelUp 00:20: Shotgun (#1, the pick)
11:00:30.000 [Info] [offer] LevelUp 00:40 (Endless horde 1)
11:00:30.001 [Info] [card] #1 PICK Shotgun (weapon, Tank) 5.00 - finish the weapon: 3 to 4 of 4, then Pump-Action Shotgun
11:00:31.000 [Info] [pick] LevelUp 00:40: banish on Shotgun (#1, 5.00, the pick)
11:00:31.500 [Info] [offer] LevelUp 00:40 (Endless horde 1) replaced (banish): gone Shotgun; new Minefield
11:00:32.000 [Info] [pick] LevelUp 00:40: Minefield
11:01:00.000 [Info] [plan] 01:00: TANK
"""


def self_test():
    import contextlib
    import io
    import os
    import tempfile
    fd, path = tempfile.mkstemp(suffix=".log")
    os.close(fd)
    try:
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(SAMPLE)
        ss = parse([path])
        bad = []

        def want(what, got, wanted):
            if got != wanted:
                bad.append("%s: got %r, wanted %r" % (what, got, wanted))

        want("sessions", len(ss), 3)
        a, ys, b = ss
        n = numbers(a)
        want("version", (a.version, a.commit), ("9.9.9", "abc1234"))
        want("level-up picks", (n["picks"]["LevelUp"]["first"], n["picks"]["LevelUp"]["other"]), (2, 1))
        want("level-up offers", n["picks"]["LevelUp"]["offers"], 3)
        want("rescue offers (the reroll is no new offer)", n["picks"]["SOS"]["offers"], 1)
        want("chest skip", n["picks"]["Chest"]["skip"], 1)
        want("heads", sorted((k, v.get("followed", 0), v.get("overridden", 0), v.get("skipped", 0)) for k, v in n["heads"].items()),
             [("A-tier rescue", 1, 0, 0), ("economy", 0, 0, 1), ("new ability", 0, 1, 0), ("weapon level", 2, 0, 0)])
        want("override", [(o[3], o[4], o[5], o[6]) for o in a.overrides], [("Handgun", 2, "Medical Drone", "new ability")])
        want("close calls", n["close_calls"], {"screens": 1, "took_first": 1, "took_second": 0, "other": 0})
        want("hints", (n["hints"]["shown"], n["hints"]["acted_10s"], n["hints"]["acted_later"], n["hints"]["not_acted"]), (2, 1, 1, 0))
        want("hint actions", n["hints"]["by_action"], {"REROLL": 1, "SKIP": 1})
        want("why bands", (n["why"]["bands"], n["why"]["drops"], n["why"]["cards"], n["why"]["close_call_bands"]), (4, 2, 4, 1))
        want("why in the side wing (a fractional px)", [(w[7], w[5], w[6]) for w in a.why if w[7].startswith("side")], [("side wing left", 2, 3)])
        want("why per pick", (n["why"]["picks"], n["why"]["picks_with_why"]), (5, 2))
        want("quest met", [(q["quest"], [(m["objective"], m["clock"]) for m in q["met"]]) for q in n["quests"]], [("Test Quest", [("max Handgun", "02:10")])])
        want("warnings", (n["warnings"], n["errors"]), (1, 0))
        want("pace Normal", (n["pace"]["Normal"]["levelups"], n["pace"]["Normal"]["minutes"], n["pace"]["Normal"]["phases"]["2-5"]["levelups"]), (3, 3.0, 1))
        nb = numbers(b)
        want("banish", (nb["picks"]["LevelUp"]["banish"], nb["picks"]["LevelUp"]["unmatched"], nb["replaced"]), (1, 1, {"banish": 1}))
        want("pace Endless", (nb["pace"]["Endless"]["levelups"], nb["pace"]["Endless"]["per_minute"]), (2, 2.0))
        want("heads b (a card not matched to the offer counts for no head)", sorted(nb["heads"]), ["next weapon tier"])
        want("since", [s.stamp for s in parse([path], "2030-01-02 10:30")], ["2030-01-02 11:00:00"])
        want("since last", [s.stamp for s in parse([path], "last")], ["2030-01-02 11:00:00"])
        want("until", numbers(parse([path], "2030-01-02 10:00", "10:01:30")[0])["why"]["bands"], 2)
        for h, k in [("weapon level: 1 to 2 of 4; style: x", "weapon level"), ("focus 2>3 of 4 toward its evolution - #2 in X", "ability toward its evolution"),
                     ("focus 2>3 of 4", "ability level"), ("3>4 of 4 toward its evolution - #1 in X", "ability toward its evolution"),
                     ("completes the weapon: 3 to 4 of 4, then SMG", "finish the weapon"), ("Common, team-wide: XP", "training: common team-wide"),
                     ("B-tier item, +8% against elites", "B-tier item"), ("Sawblade Drone, Fireaxe: slashing", "fits what the squad has"),
                     ("evolution of Pulsar", "evolution"), ("focus 2>3 of 4, evolution out of reach", "ability level (evolution out of reach)")]:
            want("head '%s'" % h, head_kind(h), k)

        # 0.16.0 (C16-07b): the Training Yard - a session with yard lines only
        ny = numbers(ys)
        want("yard numbers", ny["yard"], {"advice_lines": {"Medic": 5}, "purchases": 3, "advised": 2, "not_advised": 1,
                                          "advice_place": {"1": 2}, "refunds": 1, "levels_bought": 3, "badge_levels_bought": 1,
                                          "badge_advised": 0, "badge_while_held": 1, "badge_not_in_advice": 0,
                                          "badge_lines": {"Medic": 1}, "held": {"Medic": ["Healing Badge", "Soul Badge"]},
                                          "computes": 1, "compute_ms_max": 4.2})
        want("yard keys (the --json schema)", list(ny["yard"]), ["advice_lines", "purchases", "advised", "not_advised", "advice_place",
                                                                 "refunds", "levels_bought", "badge_levels_bought", "badge_advised",
                                                                 "badge_while_held", "badge_not_in_advice", "badge_lines", "held",
                                                                 "computes", "compute_ms_max"])
        want("yard in the total", numbers(merge(ss))["yard"]["purchases"], 3)
        want("no yard in a session without yard lines", (numbers(a)["yard"]["purchases"], numbers(a)["yard"]["advice_lines"], has_yard(a)), (0, {}, False))
        buf = io.StringIO()
        report(ys, "==== yard", out=buf)
        text = buf.getvalue()
        want("yard-only session: no selection screen", "no selection screen in this session" in text, True)
        want("yard-only session: the Training Yard line", [l for l in text.splitlines() if l.startswith("  Training Yard: ")],
             ["  Training Yard: advice lines 5 (Medic 5) | purchases 3 (2 advised: #1 x2; 1 not advised) | refunds 1 | levels bought 3"
              " | badge levels bought 1 (0 advised, 1 not advised while held, 0 other) | badge plans 1 (held: Medic Healing Badge, Soul Badge)"
              " | badge advice computed 1x (max 4.2 ms)"])
        buf = io.StringIO()
        report(a, "==== a", out=buf)
        want("no Training Yard line without yard lines", "Training Yard" in buf.getvalue(), False)
        for part, name in [("Growth Badge 1>3 stage 9/9 0.41/pt", "Growth Badge"), ("Glacier Badge 0>2 levels 2/9 0.30/pt", "Glacier Badge"),
                           ("Chance Badge 0>1 pinned", "Chance Badge"), ("Soul Badge held (next levels 0.12/pt)", "Soul Badge"),
                           ("nothing to plan", None)]:
            pm = RX_YARD_PART.match(part)
            want("badge part '%s'" % part, pm.group(1) if pm else None, name)
        for line, tree in [("[yard] General: 9 points; buy 1 Damage Against Elites 3>5 (9); later Weapon Damage 3>5", "General"),
                           ("[yard] General: 0 points; buy nothing; save for Banish Amount 0>1 (1); later Damage Against Elites 3>5", "General"),
                           ("[yard] strip at (12, 340) x0.80; nodes span x 12..900", None), ("[yard] nodes of General: x", None)]:
            am = RX_YARD_ADVICE.match(line)
            want("advice line '%s'" % line[:40], am.group(1) if am else None, tree)
        # a badge bought outside the advice, not held: 'other'; a node lent a name without ' Badge' known from the badges line
        yr = Reader(Session("y", ""), None)
        for msg in ["[yard] badges of Pyro (Normal I, 1 survivor, floor 0.15, reach 2): Renamed Node 0>1 pinned; Soul Badge held (from level 2)",
                    "[yard] Pyro: 3 points; buy 1 Renamed Node 0>1 (1)", "[yard] bought Renamed Node 0>1 (advice #1)",
                    "[yard] bought Chance Badge 0>2 (not advised)", "[yard] bought Armor 4>5 (advice #5)"]:
            yr.line(0.0, msg)
        yn = numbers(yr.s)["yard"]
        want("yard badge counts (lent name, other, place 4+)", (yn["badge_levels_bought"], yn["badge_advised"], yn["badge_not_in_advice"], yn["advice_place"]),
             (3, 1, 2, {"1": 1, "4+": 1}))

        # 0.16.0 (C16-08b part 6): the exact ties for the first place, and the stat-card weights (warnings when not read)
        nb2 = numbers(b)
        want("ties b", dict(nb2["ties"]), {"count": 1, "build_order": 0, "own_value": 0, "tag_points": 0, "further_left": 1, "other": 0,
                                         "own_value_vs_common": 0})
        want("stat weights b (a line not read counts as a warning)", (dict(nb2["stat_weights"]), nb2["warnings"]),
             ({"lines": 1, "not_read": 1, "via_field": 0}, 1))
        buf = io.StringIO()
        report(b, "==== b", out=buf)
        want("ties line", [l for l in buf.getvalue().splitlines() if l.startswith("  ties for #1")],
             ["  ties for #1: 1 (the build's order 0, own value 0, tag points 0, further left 1)"])
        for rule, key in [("the build's order (#1 before #2)", "build_order"), ("its own value (x2.00 before x1.00)", "own_value"),
                          ("the stat card's own value (x0.25 against a Common card's x1.00)", "own_value"),
                          ("tag points (5 before 1)", "tag_points"), ("tag points (1 before 5, Spread)", "tag_points"),
                          ("the card further left", "further_left"), ("something new", "other")]:
            want("tie rule '%s'" % rule, tie_rule(rule), key)
        rr = Reader(Session("r", ""), None)
        for msg in ["[rank] tie for #1 at 4.81: Bombing Strike before Minefield - the build's order (#1 before #2)",
                    "[rank] tie for #1 at 3.00: Fast Mover before Dear John - its own value (x2.00 before x1.00)",
                    "[rank] tie for #1 at 2.10: Shotgun before Pickup Range - the stat card's own value (x0.25 against a Common card's x1.00)",
                    "[rank] tie for #1 at 4.87: #Slashing +4 before #Electric +4 - tag points (5 before 1)",
                    "[rank] Rare stat cards: weight x2.50 for Ability Cooldown (the card's own +0.25 vs Common +0.1)",
                    "[rank] Legendary stat cards: weight x4.00 for Max Health (the card's own +0.4 vs Common +0.1; GetMyRarityBonusList gave nothing - read from the rarity's field)",
                    "[rank] Rare stat cards: weight x2.00 for Armor (the card's values not read: MissingFieldException - the fallback)"]:
            rr.cur_line = "00:00:00.000 [Info] " + msg
            rr.line(0.0, msg)
        rn = numbers(rr.s)
        want("ties by rule", dict(rn["ties"]), {"count": 4, "build_order": 1, "own_value": 2, "tag_points": 1, "further_left": 0, "other": 0,
                                              "own_value_vs_common": 1})
        want("stat weights", (dict(rn["stat_weights"]), rn["warnings"]), ({"lines": 3, "not_read": 1, "via_field": 1}, 2))
        want("ties in the total", numbers(merge(ss + [rr.s]))["ties"]["count"], 5)

        # main(): every session with a selection screen or yard lines is reported; --json carries the yard numbers
        fd2, jpath = tempfile.mkstemp(suffix=".json")
        os.close(fd2)
        try:
            with contextlib.redirect_stdout(io.StringIO()) as printed:
                rc = main([path, "--json", jpath])
            with open(jpath, encoding="utf-8") as fh:
                js = json.load(fh)
            want("main rc", rc, 0)
            want("main: the sessions reported", [x["stamp"] for x in js["sessions"]],
                 ["2030-01-02 10:00:00", "2030-01-02 10:20:00", "2030-01-02 11:00:00"])
            want("main: total.yard.purchases", js["total"]["yard"]["purchases"], 3)
            want("main: played / yard_sessions / sessions", (js["total"]["played"], js["total"]["yard_sessions"], js["total"]["sessions"]), (2, 1, 3))
            want("main: sessions[].numbers.yard", js["sessions"][1]["numbers"]["yard"]["badge_while_held"], 1)
            want("main: total.ties", js["total"]["ties"]["further_left"], 1)
            want("main: the TOTAL title", [l for l in printed.getvalue().splitlines() if l.startswith("==== TOTAL")],
                 ["==== TOTAL over 3 session(s) with a selection screen or Training Yard lines (of 3 selected)"])
            with contextlib.redirect_stdout(io.StringIO()) as printed:
                main([path, "--since", "2030-01-02 10:20", "--until", "10:30:00"])
            want("main: a yard-only session alone is shown (no TOTAL block)",
                 ([l for l in printed.getvalue().splitlines() if l.startswith("====")],
                  sum(1 for l in printed.getvalue().splitlines() if l.startswith("  Training Yard: "))),
                 (["==== session 2030-01-02 10:20:00 - version not logged - 0 run(s), 11 lines"], 1))
        finally:
            os.remove(jpath)
        for l in bad:
            print("FAIL " + l)
        print("advice_audit self-test: " + ("all as wanted" if not bad else "%d not as wanted" % len(bad)))
        return 0 if not bad else 1
    finally:
        os.remove(path)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
