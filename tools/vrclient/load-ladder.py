"""The undead-hordes load ladder (docs/dod/undead-hordes.md D2, D3): fixtures, the report, and its selftest.

  python tools/vrclient/load-ladder.py fixtures            write scenarios/fixtures/load-<n>.json (D3's mix)
  python tools/vrclient/load-ladder.py install <events>    put the load-<n> events into a dev events.json (others kept)
  python tools/vrclient/load-ladder.py report <log>...     the Load test rows and "load ladder: … · ceiling <n>"
  python tools/vrclient/load-ladder.py selftest            "load ladder selftest: pass"

The logs carry no timestamps, so scenarios/load-ladder.vrs marks each step's phases with NyarDevTools' `.devnote`:
  [DEV NOTE] step=<n> phase=start                 before the event starts
  [DEV NOTE] step=<n> phase=quiet s=<spawn s> tracked=<k>   every unit spawned; nobody within 200 m
  [DEV NOTE] step=<n> phase=fight                 Chaos among them
  [DEV NOTE] step=<n> phase=drain                 `.nyar purge confirm` sent
  [DEV NOTE] step=<n> phase=end s=<drain s> purged=<k>   tracked units 0 (k: units the purge found)
and reads the mod's "[nyar] tick timing: avg <a> ms, max <m> ms over <k> ticks" and "[nyar] slow tick:" lines (Debug.TimingLog)
and the meter's "[DEV FRAME] avg=<ms> p95=<ms> max=<ms> frames=<n> mem=<MB>" lines, each once a minute. The first window
of each kind after a phase mark straddles the mark and is not counted.

A step passes when it spawned at least 98 % of its n and still held 95 % at the purge (natives at the pond kill a
few; Session 0's first step 100 lost all of them to unit-watch's stall rule), has at least 10 quiet and 5 fight windows of each kind, every one with a tick average under 5 ms,
a frame average under 36 ms and a frame p95 under 50 ms, no slow tick in its quiet or fight phase, and its drain within
ceil(n / MaxDespawnsPerTick) + 60 s. The ladder stops at the first failing step; the ceiling is the last step that passed.
scenarios/hostility.vrs marks each native's 60 s with "[DEV NOTE] hostility-start native=<prefab>" and "[DEV NOTE] hostility
native=<prefab>"; report counts the "[nyar] <prefab> of <event> died (killer <prefab>)" lines between them (VerboseLogging)
into the Hostility table.
"""
import json
import math
import os
import re
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
FIXTURES = os.path.join(HERE, "scenarios", "fixtures")
STEPS = [100, 250, 500, 750, 1000]
QUIET_WINDOWS, FIGHT_WINDOWS = 10, 5
TICK_BAR, FRAME_AVG_BAR, FRAME_P95_BAR = 5.0, 36.0, 50.0
DESPAWNS_PER_TICK = 20          # the Session 0 dev cfg's MaxDespawnsPerTick
MAX_WAVE = 500                  # MaxUnitsPerWave's ceiling

WEAK = ["CHAR_Undead_SkeletonSoldier_Armored_Farbane", "CHAR_Undead_ArmoredSkeletonCrossbow_Farbane",
        "CHAR_Undead_SkeletonApprentice", "CHAR_Undead_UndyingGhoul"]
MID = ["CHAR_Undead_Guardian", "CHAR_Undead_SkeletonMage", "CHAR_Undead_SkeletonGolem"]
STRONG = ["CHAR_Undead_GhostBanshee", "CHAR_Undead_GhostGuardian", "CHAR_Undead_GhostMilitia_Light"]
BOSS = "CHAR_Undead_BishopOfDeath_VBlood"
LEVELS = {"weak": 20, "mid": 40, "strong": 60, "boss": 50}
POND = {"type": "Point", "x": -1880, "z": -1640, "y": 0}


# ---------------------------------------------------------------------------------------------------- fixtures
def split(total: int, parts: int) -> list[int]:
    """total over parts as evenly as integers allow, the remainder to the first."""
    base, extra = divmod(total, parts)
    return [base + (1 if i < extra else 0) for i in range(parts)]


def mix(n: int) -> dict[str, int]:
    """n tracked units: the boss, 80 % weak, 5 % strong and the rest mid (D3), per prefab. The boss is one of the n, so the
    mid share is one short of 15 % (14 of 100; Codex step 1 F1, plan Log)."""
    weak, strong = round(n * 0.8), round(n * 0.05)
    mid = n - 1 - weak - strong
    out = {}
    for tier, prefabs, count in (("weak", WEAK, weak), ("mid", MID, mid), ("strong", STRONG, strong)):
        for p, c in zip(prefabs, split(count, len(prefabs))):
            out[p] = c
    return out


def tier_of(prefab: str) -> str:
    return "weak" if prefab in WEAK else "mid" if prefab in MID else "strong"


def fixture(n: int) -> dict:
    """The load-<n> event: n - 1 units over ceil((n - 1) / 500) waves 10 s apart, and the boss in wave 1."""
    counts = mix(n)
    waves = math.ceil((n - 1) / MAX_WAVE)
    per = {p: [0] * waves for p in counts}           # dealt round-robin, so the waves differ by one unit at most
    k = 0
    for p, c in counts.items():
        for _ in range(c):
            per[p][k % waves] += 1
            k += 1
    wave_list = []
    for w in range(waves):
        units = [{"prefab": p, "count": per[p][w], "modifiers": {"level": LEVELS[tier_of(p)]}} for p in counts if per[p][w] > 0]
        spec = {"units": units}
        if w == 0:
            spec["bosses"] = [{"prefab": BOSS, "count": 1, "modifiers": {"level": LEVELS["boss"]}}]
        else:
            spec["afterSeconds"] = 10
        wave_list.append(spec)
    return {"id": f"load-{n}", "name": f"Load {n}", "enabled": True, "pillar": "spawns", "trigger": {"type": "Manual"},
            "durationSeconds": 3600,
            "action": {"type": "SpawnWaves", "waveList": wave_list, "radius": 30, "location": dict(POND),
                       "behaviour": {"type": "Hunt", "range": 40}}}


def cmd_fixtures() -> int:
    os.makedirs(FIXTURES, exist_ok=True)
    for n in STEPS:
        f = fixture(n)
        total = sum(u["count"] for w in f["action"]["waveList"] for u in w["units"]) + 1
        path = os.path.join(FIXTURES, f"load-{n}.json")
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            json.dump(f, fh, indent=2)
            fh.write("\n")
        print(f"{path}: {total} units")
    return 0


def cmd_install(events_path: str) -> int:
    """Replace the dev events.json's load-<n> events with the fixtures; every other event stays as it was."""
    with open(events_path, encoding="utf-8") as fh:
        doc = json.load(fh)
    events = [e for e in doc["events"] if not str(e.get("id", "")).startswith("load-")]
    for n in STEPS:
        with open(os.path.join(FIXTURES, f"load-{n}.json"), encoding="utf-8") as fh:
            events.append(json.load(fh))
    doc["events"] = events
    with open(events_path, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=2)
    print(f"{events_path}: {len(STEPS)} load events, {len(events)} events in all")
    return 0


# ---------------------------------------------------------------------------------------------------- report
NOTE = re.compile(r"\[DEV NOTE\] step=(\d+) phase=(start|quiet|fight|drain|end)(?: s=(\d+))?(?: tracked=(\d+))?(?: purged=(\d+))?")
H_START = re.compile(r"\[DEV NOTE\] hostility-start native=(\S+)")
H_END = re.compile(r"\[DEV NOTE\] hostility native=(\S+)")
DIED = re.compile(r"\[nyar\] (CHAR_\S+) of \S+ died \(killer (CHAR_[^\s,)]+)")
UNDEAD_PROBE = "CHAR_Undead_SkeletonSoldier_Armored_Farbane"
PHASES = ["start", "quiet", "fight", "drain", "end"]
TICK = re.compile(r"\[nyar\] tick timing: avg ([\d.]+) ms, max ([\d.]+) ms over (\d+) ticks")
SLOW = re.compile(r"\[nyar\] slow tick: ([\d.]+) ms")
FRAME = re.compile(r"\[DEV FRAME\] avg=([\d.]+) p95=([\d.]+) max=([\d.]+) frames=(\d+) mem=(\d+)")


class Step:
    def __init__(self, n: int):
        self.n = n
        self.spawn_s = None
        self.tracked = None
        self.purged = None
        self.drain_s = None
        self.phases: list[str] = []
        self.ticks = {"quiet": [], "fight": []}       # (avg, max)
        self.frames = {"quiet": [], "fight": []}      # (avg, p95, mem)
        self.slow = {"quiet": 0, "fight": 0, "other": 0}

    def problem(self) -> str | None:
        """None when the step passes, else the first bar it misses."""
        if self.phases != PHASES:                 # each phase once, in order (Codex step 1 F2)
            return "not finished" if self.phases == PHASES[:len(self.phases)] else "phases out of order: " + " ".join(self.phases)
        if self.tracked is None or self.tracked < math.ceil(self.n * 0.98):
            return f"spawned {self.tracked} of {self.n}"
        if self.purged is None or self.purged < math.ceil(self.n * 0.95):
            return f"held {self.purged} of {self.n} at the purge"
        for phase, need in (("quiet", QUIET_WINDOWS), ("fight", FIGHT_WINDOWS)):
            if len(self.ticks[phase]) < need:
                return f"{len(self.ticks[phase])} of {need} {phase} tick windows"
            if len(self.frames[phase]) < need:
                return f"{len(self.frames[phase])} of {need} {phase} frame windows"
            for i, (avg, _) in enumerate(self.ticks[phase], 1):
                if avg >= TICK_BAR:
                    return f"{phase} tick window {i} avg {avg:g} ms"
            for i, (avg, p95, _) in enumerate(self.frames[phase], 1):
                if avg >= FRAME_AVG_BAR:
                    return f"{phase} frame window {i} avg {avg:g} ms"
                if p95 >= FRAME_P95_BAR:
                    return f"{phase} frame window {i} p95 {p95:g} ms"
            if self.slow[phase]:
                return f"slow ticks while {phase}: {self.slow[phase]}"
        margin = math.ceil(self.n / DESPAWNS_PER_TICK) + 60
        if self.drain_s is None or self.drain_s > margin:
            return f"drain {self.drain_s} s over {margin} s"
        return None

    def row(self) -> str:
        def tick(phase):
            t = self.ticks[phase]
            return f"{sum(a for a, _ in t) / len(t):.2f} / {max(m for _, m in t):.1f}" if t else "-"
        f = self.frames["quiet"] + self.frames["fight"]
        frame = f"{sum(a for a, _, _ in f) / len(f):.1f} / {max(p for _, p, _ in f):.1f}" if f else "-"
        mem = f"{max(m for _, _, m in f)}" if f else "-"
        slow = sum(self.slow.values())
        result = self.problem()
        return (f"| {self.n} | {self.spawn_s if self.spawn_s is not None else '-'} | {tick('quiet')} | {tick('fight')} | {slow} "
                f"| {frame} | {mem} | {self.drain_s if self.drain_s is not None else '-'} | {'pass' if result is None else 'fail: ' + result} |")


def parse(lines: list[str], reruns: list[str] | None = None) -> list[Step]:
    """The steps in log order. A step started again right after its own run (a rerun after a fix) replaces that run;
    the replaced run's result goes to reruns."""
    steps: list[Step] = []
    cur, phase, skip = None, None, set()
    for line in lines:
        if m := NOTE.search(line):
            n, p = int(m[1]), m[2]
            if p == "start" or cur is None or cur.n != n:
                if steps and steps[-1].n == n and p == "start":
                    old = steps.pop()
                    if reruns is not None:
                        reruns.append(f"step {n} rerun; the earlier run: {old.problem() or 'pass'}")
                cur = Step(n)
                steps.append(cur)
            cur.phases.append(p)
            if p == "quiet" and m[3]:
                cur.spawn_s = int(m[3])
            if p == "quiet" and m[4]:
                cur.tracked = int(m[4])
            if p == "end" and m[3]:
                cur.drain_s = int(m[3])
            if p == "end" and m[5]:
                cur.purged = int(m[5])
            phase = p if p in ("quiet", "fight") else None
            skip = {"tick", "frame"}
            continue
        if cur is None or cur.phases[-1:] == ["end"]:
            continue
        if m := TICK.search(line):
            if phase and "tick" in skip:
                skip.discard("tick")
            elif phase:
                cur.ticks[phase].append((float(m[1]), float(m[2])))
        elif m := FRAME.search(line):
            if phase and "frame" in skip:
                skip.discard("frame")
            elif phase:
                cur.frames[phase].append((float(m[1]), float(m[2]), int(m[5])))
        elif SLOW.search(line):
            cur.slow[phase or "other"] += 1
    return steps


def hostility(lines: list[str]) -> list[tuple[str, int, int]]:
    """Per probed native: the undead it killed and the natives the undead killed, between its two marks."""
    rows, native, undead, natives = [], None, 0, 0
    for line in lines:
        if m := H_START.search(line):
            native, undead, natives = m[1], 0, 0
        elif (m := H_END.search(line)) and native == m[1]:
            rows.append((native, undead, natives))
            native = None
        elif native and (m := DIED.search(line)):
            if m[1] == UNDEAD_PROBE and m[2] == native:
                undead += 1
            elif m[1] == native and m[2].startswith("CHAR_Undead_"):
                natives += 1
    return rows


def verdict(steps: list[Step]) -> str:
    parts, ceiling, failed = [], 0, None
    for s in steps:
        if failed is not None:
            return f"load ladder: {', '.join(parts)} · ceiling {ceiling}; step {s.n} was run after a failure"
        expected = STEPS[len(parts)] if len(parts) < len(STEPS) else None
        if s.n != expected:
            return f"load ladder: {', '.join(parts) or 'no step'} · ceiling {ceiling}; step {s.n} out of order"
        problem = s.problem()
        if problem is None:
            parts.append(f"{s.n} pass")
            ceiling = s.n
        else:
            parts.append(f"{s.n} fail ({problem})")
            failed = s.n
    if not parts:
        return "load ladder: no step · ceiling 0"
    return f"load ladder: {', '.join(parts)} · ceiling {ceiling}"


def read_logs(paths: list[str]) -> list[str]:
    lines = []
    for p in paths:
        with open(p, encoding="utf-8", errors="replace") as fh:
            lines.extend(fh.read().splitlines())
    return lines


def cmd_report(paths: list[str]) -> int:
    lines = read_logs(paths)
    reruns: list[str] = []
    steps = parse(lines, reruns)
    for r in reruns:
        print(r)
    print("| Step | Spawn s | Tick avg / max ms (quiet) | Tick avg / max ms (fight) | Slow ticks | Frame avg / p95 ms | Memory MB | Drain s | Result |")
    print("|---|---|---|---|---|---|---|---|---|")
    for s in steps:
        print(s.row())
    rows = hostility(lines)
    if rows:
        print()
        print("| Native | Fights the undead | Deaths in 60 s |")
        print("|---|---|---|")
        for native, undead, natives in rows:
            print(f"| {native} | {'yes' if undead + natives else 'no'} | {undead} undead killed by it, {natives} killed by the undead |")
        print()
    line = verdict(steps)
    print(line)
    return 0 if line.endswith(f"ceiling {STEPS[-1]}") else 1


# ---------------------------------------------------------------------------------------------------- selftest
def synthetic(n: int, quiet=QUIET_WINDOWS + 1, fight=FIGHT_WINDOWS + 1, tick=1.2, frame=(33.4, 35.0), mem=4100,
              drain=None, slow_in=None, frames=True, tracked=None, purged=None) -> list[str]:
    """One step's log as Session 0 writes it (each phase holds one more window than it needs: the first is skipped)."""
    i = "[Info   :Nyarlathotep] [nyar] "
    d = "[Info   :NyarDevTools] "
    out = [f"{d}[DEV NOTE] step={n} phase=start", f"{i}spawn queued", f"{d}[DEV NOTE] step={n} phase=quiet s=48 tracked={n if tracked is None else tracked}"]
    for phase, count in (("quiet", quiet), ("fight", fight)):
        if phase == "fight":
            out.append(f"{d}[DEV NOTE] step={n} phase=fight")
        for _ in range(count):
            out.append(f"{i}tick timing: avg {tick:.3f} ms, max 9.100 ms over 60 ticks")
            if frames:
                out.append(f"{d}[DEV FRAME] avg={frame[0]:.1f} p95={frame[1]:.1f} max=61.0 frames=1790 mem={mem}")
            if slow_in == phase:
                out.append(f"{i}slow tick: 260.0 ms (spawn 250.0 ms; outside phases 10.0 ms)")
                slow_in = None
    out += [f"{d}[DEV NOTE] step={n} phase=drain", f"{d}[DEV NOTE] step={n} phase=end s={drain if drain is not None else 50} purged={n if purged is None else purged}"]
    return out


def cmd_selftest() -> int:
    ok_ladder = [l for n in STEPS for l in synthetic(n)]
    cases = [
        ("a passing ladder", ok_ladder, "load ladder: 100 pass, 250 pass, 500 pass, 750 pass, 1000 pass · ceiling 1000"),
        ("no [DEV FRAME] lines", synthetic(100, frames=False), "load ladder: 100 fail (0 of 10 quiet frame windows) · ceiling 0"),
        ("one quiet window short", synthetic(100, quiet=QUIET_WINDOWS), "load ladder: 100 fail (9 of 10 quiet tick windows) · ceiling 0"),
        ("a tick window at the bar", synthetic(100, tick=5.0), "load ladder: 100 fail (quiet tick window 1 avg 5 ms) · ceiling 0"),
        ("a frame average at the bar", synthetic(100, frame=(36.0, 40.0)), "load ladder: 100 fail (quiet frame window 1 avg 36 ms) · ceiling 0"),
        ("a frame p95 at the bar", synthetic(100, frame=(34.0, 50.0)), "load ladder: 100 fail (quiet frame window 1 p95 50 ms) · ceiling 0"),
        ("a p95 just under the bar", synthetic(100, frame=(35.9, 49.9)), "load ladder: 100 pass · ceiling 100"),
        ("a slow tick while fighting", synthetic(100, slow_in="fight"), "load ladder: 100 fail (slow ticks while fight: 1) · ceiling 0"),
        ("a drain over its margin", synthetic(100, drain=66), "load ladder: 100 fail (drain 66 s over 65 s) · ceiling 0"),
        ("a step after a failure", synthetic(100) + synthetic(250, tick=6.0) + synthetic(500),
         "load ladder: 100 pass, 250 fail (quiet tick window 1 avg 6 ms) · ceiling 100; step 500 was run after a failure"),
        ("a spawn short of its count", synthetic(100, tracked=97), "load ladder: 100 fail (spawned 97 of 100) · ceiling 0"),
        ("a spawn within 2 %", synthetic(100, tracked=98), "load ladder: 100 pass · ceiling 100"),
        ("units lost before the purge", synthetic(100, purged=0), "load ladder: 100 fail (held 0 of 100 at the purge) · ceiling 0"),
        ("a few units lost before the purge", synthetic(100, purged=95), "load ladder: 100 pass · ceiling 100"),
        ("a rerun of the same step", synthetic(100, tick=6.0) + synthetic(100) + synthetic(250),
         "load ladder: 100 pass, 250 pass · ceiling 250"),
        ("a step out of order", synthetic(250), "load ladder: no step · ceiling 0; step 250 out of order"),
        ("phases swapped", [l.replace("phase=quiet", "phase=@").replace("phase=fight", "phase=quiet").replace("phase=@", "phase=fight")
                            for l in synthetic(100)],
         "load ladder: 100 fail (phases out of order: start fight quiet drain end) · ceiling 0"),
        ("a phase marked twice", synthetic(100)[:3] + synthetic(100)[2:],
         "load ladder: 100 fail (phases out of order: start quiet quiet fight drain end) · ceiling 0"),
        ("a step not finished", synthetic(100)[:-1], "load ladder: 100 fail (not finished) · ceiling 0"),
    ]
    bad = []
    for name, lines, want in cases:
        got = verdict(parse(lines))
        if got != want:
            bad.append(f"{name}: got {got!r}")
    # the figures: the row reads the frame and memory numbers as written, and skips the straddling first window
    lines = synthetic(100, frame=(33.4, 35.0), mem=4321)
    lines.insert(3, "[Info   :NyarDevTools] [DEV FRAME] avg=90.0 p95=99.0 max=120.0 frames=600 mem=9999")
    row = parse(lines)[0].row()
    want_row = "| 100 | 48 | 1.20 / 9.1 | 1.20 / 9.1 | 0 | 33.4 / 35.0 | 4321 | 50 | pass |"
    if row != want_row:
        bad.append(f"figures: got {row!r}")
    with tempfile.TemporaryDirectory() as tmp:          # report reads files: two log copies make one ladder
        a, b = os.path.join(tmp, "a.log"), os.path.join(tmp, "b.log")
        open(a, "w", encoding="utf-8").write("\n".join(ok_ladder[:len(ok_ladder) // 2]))
        open(b, "w", encoding="utf-8").write("\n".join(ok_ladder[len(ok_ladder) // 2:]))
        if verdict(parse(read_logs([a, b]))) != cases[0][2]:
            bad.append("two log copies do not read as one ladder")
    probe = ["[DEV NOTE] hostility-start native=CHAR_Bandit_Thug",
             "[nyar] CHAR_Bandit_Thug of spawn died (killer CHAR_Undead_SkeletonSoldier_Armored_Farbane)",
             "[nyar] CHAR_Undead_SkeletonSoldier_Armored_Farbane of spawn died (killer CHAR_Bandit_Thug)",
             "[nyar] CHAR_Bandit_Thug of spawn died (killer CHAR_Undead_SkeletonSoldier_Armored_Farbane)",
             "[nyar] CHAR_Bandit_Thug of spawn died (killer CHAR_Militia_Light)",
             "[DEV NOTE] hostility native=CHAR_Bandit_Thug",
             "[nyar] CHAR_Bandit_Thug of spawn died (killer CHAR_Undead_SkeletonSoldier_Armored_Farbane)",
             "[DEV NOTE] hostility-start native=CHAR_Forest_Wolf", "[DEV NOTE] hostility native=CHAR_Forest_Wolf"]
    if hostility(probe) != [("CHAR_Bandit_Thug", 1, 2), ("CHAR_Forest_Wolf", 0, 0)]:
        bad.append(f"hostility: got {hostility(probe)}")
    for n in STEPS:                                   # the fixtures hold D3's mix
        f = fixture(n)
        units = [u for w in f["action"]["waveList"] for u in w["units"]]
        total = sum(u["count"] for u in units) + 1
        weak = sum(u["count"] for u in units if u["prefab"] in WEAK)
        strong = sum(u["count"] for u in units if u["prefab"] in STRONG)
        mid = sum(u["count"] for u in units if u["prefab"] in MID)
        sizes = [sum(u["count"] for u in w["units"]) for w in f["action"]["waveList"]]
        if total != n or weak != round(n * 0.8) or strong != round(n * 0.05) or mid != n - 1 - weak - strong or max(sizes) > MAX_WAVE:
            bad.append(f"fixture {n}: total {total}, weak {weak}, mid {mid}, strong {strong}, waves {sizes}")
        if any(u["modifiers"]["level"] != LEVELS[tier_of(u["prefab"])] for u in units):
            bad.append(f"fixture {n}: a level is off")
    if bad:
        print("load ladder selftest: FAIL")
        for b_ in bad:
            print("  " + b_)
        return 1
    print("load ladder selftest: pass")
    return 0


def main(argv: list[str]) -> int:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    if len(argv) >= 1 and argv[0] == "fixtures":
        return cmd_fixtures()
    if len(argv) == 2 and argv[0] == "install":
        return cmd_install(argv[1])
    if len(argv) >= 2 and argv[0] == "report":
        return cmd_report(argv[1:])
    if argv[:1] == ["selftest"]:
        return cmd_selftest()
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
