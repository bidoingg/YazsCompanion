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
            self.line(t, msg)
        for h in self.hints_open:
            h.closed = True

    def line(self, t, msg):
        s = self.s
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
    return tot


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
    return OrderedDict([("picks", picks), ("heads", heads), ("close_calls", cc), ("hints", hints), ("why", why),
                        ("quests", quests), ("pace", pace), ("runs", len(s.runs)),
                        ("replaced", dict(s.replaced)), ("banish_armed", sum(s.banish_armed.values())),
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


def report(s, title, brief=False, out=sys.stdout, total=False):
    n = numbers(s)
    w = out.write
    w(title + "\n")
    if s.lines == 0:
        w("  (no lines)\n")
        return n
    total_picks = n["why"]["picks"]
    if total_picks == 0 and not s.offers:
        w("  no selection screen in this session; %d warning(s), %d error(s)\n" % (n["warnings"], n["errors"]))
        for l in (s.warnings + s.errors)[:5]:
            w("    " + l[:200] + "\n")
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
    h = n["hints"]
    acts = ", ".join("%s %d" % (k, v) for k, v in sorted(h["by_action"].items())) or "none"
    w("  hints shown: %d (%s) - acted on within %ds: %d, later on the same screen: %d, not acted on: %d\n" % (h["shown"], acts, ACT_WINDOW, h["acted_10s"], h["acted_later"], h["not_acted"]))
    if not brief:
        for x in s.hints[:8]:
            what = ("answered after %.1f s" % x.acted) if x.acted is not None else ("screen closed with a card" if x.closed else "no answer")
            w("    %s %s %s%s: %s\n" % (SCREEN_LABEL.get(x.screen, x.screen), x.clock, x.action, " (%s)" % x.when if x.when else "", what))
        if len(s.hints) > 8:
            w("    ... %d more\n" % (len(s.hints) - 8))
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
    out = {"sessions": []}
    if not a.brief:
        for s in played:
            v = s.version + (" (%s)" % s.commit if s.commit else "") if s.version else "version not logged"
            out["sessions"].append(dict(stamp=s.stamp, version=s.version, commit=s.commit,
                                        numbers=report(s, "==== session %s - %s - %d run(s), %d lines" % (s.stamp, v, len(s.runs), s.lines))))
            print()
    quiet = len(sessions) - len(played)
    tot = merge(sessions)
    title = "==== TOTAL over %d session(s) with a selection screen (of %d selected%s)" % (
        len(played), len(sessions), "; %d without one" % quiet if quiet else "")
    if a.brief or len(played) != 1:
        out["total"] = report(tot, title, brief=a.brief, total=True)
    else:
        out["total"] = numbers(tot)          # one session: its block above is the total
    out["total"]["sessions"] = len(sessions)
    out["total"]["played"] = len(played)
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
==== 2030-01-02 11:00:00 session start ====
11:00:01.000 [Info] [hooks] 38 methods patched (class by class; 0 patch classes failed)
11:00:05.000 [Info] [offer] LevelUp 00:20 (Endless horde 1)
11:00:05.001 [Info] [card] #1 PICK Shotgun (weapon, Tank) 5.00 - next weapon tier
11:00:06.000 [Info] [pick] LevelUp 00:20: Shotgun (#1, the pick)
11:00:30.000 [Info] [offer] LevelUp 00:40 (Endless horde 1)
11:00:30.001 [Info] [card] #1 PICK Shotgun (weapon, Tank) 5.00 - finish the weapon: 3 to 4 of 4, then Pump-Action Shotgun
11:00:31.000 [Info] [pick] LevelUp 00:40: banish on Shotgun (#1, 5.00, the pick)
11:00:31.500 [Info] [offer] LevelUp 00:40 (Endless horde 1) replaced (banish): gone Shotgun; new Minefield
11:00:32.000 [Info] [pick] LevelUp 00:40: Minefield
11:01:00.000 [Info] [plan] 01:00: TANK
"""


def self_test():
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

        want("sessions", len(ss), 2)
        a, b = ss
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
        for l in bad:
            print("FAIL " + l)
        print("advice_audit self-test: " + ("all as wanted" if not bad else "%d not as wanted" % len(bad)))
        return 0 if not bad else 1
    finally:
        os.remove(path)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
