# In-game session helper: writes the dev server's events.json for foundation's step 5 tests.
# Modes: boot (test events, schedules parked on Mon 04:00), go (schedules relative to now), d23a (boot's events plus one
# with an unknown unit), d23b (the same file with a comma removed), restore-valid (d23a again), cool (the last valid file
# with t-cool due every minute from now+2 to now+13, so a purge right after it has due times inside its cooldown). State in
# %TEMP%/nyar-session. s17a/s17b: session 17; rac2: raphael-api-core session 2; fe1 and show: faction-empowerment
# session 1 (see each mode's comment). `python session-events.py --help` lists the modes and writes nothing.
import json, sys, datetime, io, os, shutil
SERVER = r"C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer"
CFG = os.path.join(SERVER, "BepInEx", "config", "Nyarlathotep", "events.json")
HERE = os.environ.get("NYAR_SESSION_DIR") or os.path.join(os.environ["TEMP"], "nyar-session")
USAGE = """usage: python tools/ingame/session-events.py <mode> [args]
  boot | go | d23a | d23b | cool | restore-valid | d22 | perf | drain | a21 | a22 | rac2   (foundation, raphael-api-core)
  s17a HH:MM | s17b                                                                         (foundation session 17)
  fe1 [--delay M] [--dry-run] [--server DIR]   faction-empowerment session 1: events.json gets only fe-short (Empower
        Faction_Bandits, 60 s, due 2 min after now + M) and fe-long (1200 s, due 5 min after now + M), each rounded up
        to the next whole minute; kdpen.Nyarlathotep.cfg gets Pillars.FactionEmpowerment = true and
        Debug.VerboseLogging = true. The previous events.json and cfg are copied to %TEMP%/nyar-session first; each
        file is written through a .tmp and a replace. --dry-run prints what it would write and writes nothing.
  show [--server DIR]   prints the server's events.json and the cfg keys fe1 sets; writes nothing.
The server defaults to """ + SERVER + "."
def here():
    # Created on first use, so --help, show and fe1 --dry-run leave no %TEMP%/nyar-session behind.
    os.makedirs(HERE, exist_ok=True)
    return HERE
if len(sys.argv) < 2 or sys.argv[1] in ("-h", "--help", "help"):
    print(USAGE)
    sys.exit(0)
mode = sys.argv[1]
def unit(n): return [{"prefab": "CHAR_Bandit_Deadeye", "count": n}]
def point(units, waves=1, interval=30, radius=6): return {"type": "SpawnWaves", "units": units, "waves": waves, "intervalSeconds": interval, "radius": radius, "location": {"type": "Point", "x": -1200, "z": -800}}
def hhmm(t): return t.strftime("%H:%M")
def build(sched_day, sched_times, cool_times):
    ev = [
      {"id": "t-manual", "name": "Test manual raid", "enabled": True, "pillar": "spawns", "trigger": {"type": "Manual"}, "durationSeconds": 180,
       "action": {"type": "SpawnWaves", "units": unit(15), "waves": 2, "intervalSeconds": 20, "radius": 8, "location": {"type": "Admin"}}},
      {"id": "t-sched", "name": "Test schedule", "enabled": True, "pillar": "sieges", "trigger": {"type": "Schedule", "days": [sched_day], "times": sched_times}, "durationSeconds": 60, "action": point(unit(2))},
      {"id": "t-sched-twin", "name": "Test schedule twin", "enabled": False, "pillar": "sieges", "trigger": {"type": "Schedule", "days": [sched_day], "times": sched_times}, "durationSeconds": 60, "action": point(unit(2))},
      {"id": "t-night", "name": "Test night", "enabled": True, "pillar": "zones", "trigger": {"type": "GameTime", "phase": "night"}, "durationSeconds": 60, "action": point(unit(2))},
      {"id": "t-vblood", "name": "Test V Blood", "enabled": True, "pillar": "boss", "trigger": {"type": "VBloodKilled", "bosses": ["any"]}, "durationSeconds": 60, "action": point(unit(2))},
      {"id": "t-cool", "name": "Test purge cooldown", "enabled": True, "pillar": "sieges", "trigger": {"type": "Schedule", "days": [sched_day], "times": cool_times}, "durationSeconds": 30, "action": point(unit(1))},
    ]
    return {"SchemaVersion": 1, "events": ev}
def write(doc, text=None):
    text = text if text is not None else json.dumps(doc, indent=2)
    io.open(CFG, "w", encoding="utf-8", newline="\n").write(text)
if mode == "boot":
    write(build("Mon", ["04:00"], ["04:30"]))
elif mode == "go":
    now = datetime.datetime.now().replace(second=0, microsecond=0)
    day = now.strftime("%a")
    sched = now + datetime.timedelta(minutes=5)
    cool = [hhmm(now + datetime.timedelta(minutes=12 + 3 * k)) for k in range(12)]
    doc = build(day, [hhmm(sched)], cool)
    write(doc)
    json.dump(doc, open(os.path.join(here(), "go.json"), "w"), indent=2)
    print("go: t-sched at", hhmm(sched), "t-cool", cool)
elif mode == "d23a":
    doc = build("Mon", ["04:00"], ["04:30"])
    doc["events"].append({"id": "t-badunit", "name": "Test unknown unit", "enabled": True, "pillar": "spawns", "trigger": {"type": "Manual"}, "durationSeconds": 60, "action": point([{"prefab": "CHAR_Not_A_Real_Unit", "count": 1}])})
    json.dump(doc, open(os.path.join(here(), "d23a.json"), "w"), indent=2)
    write(doc)
    print("d23a written")
elif mode == "d23b":
    text = json.dumps(json.load(open(os.path.join(here(), "d23a.json"))), indent=2)
    lines = text.split("\n")
    # remove the comma after the first event's "name" value (line 5 of the pretty text)
    i = next(n for n, l in enumerate(lines) if '"name": "Test manual raid",' in l)
    lines[i] = lines[i].rstrip(",")
    write(None, "\n".join(lines))
    print("d23b written: comma removed on line", i + 1)
elif mode == "cool":
    src = os.path.join(here(), "d23a.json")
    doc = json.load(open(src)) if os.path.exists(src) else build("Mon", ["04:00"], ["04:30"])
    now = datetime.datetime.now().replace(second=0, microsecond=0)
    cool = [hhmm(now + datetime.timedelta(minutes=2 + k)) for k in range(12)]
    ev = next(e for e in doc["events"] if e["id"] == "t-cool")
    ev["trigger"] = {"type": "Schedule", "days": [now.strftime("%a")], "times": cool}
    write(doc)
    json.dump(doc, open(os.path.join(here(), "cool.json"), "w"), indent=2)
    print("cool: t-cool", cool)
elif mode == "d22":
    # caps test (D22): only manual events enabled, so MaxConcurrentEvents 1 is taken by nothing automatic
    doc = build("Mon", ["04:00"], ["04:30"])
    for e in doc["events"]:
        if e["trigger"]["type"] != "Manual": e["enabled"] = False
    doc["events"].append({"id": "t-caps", "name": "Test caps", "enabled": True, "pillar": "spawns", "trigger": {"type": "Manual"}, "durationSeconds": 120,
        "action": {"type": "SpawnWaves", "units": unit(20), "waves": 2, "intervalSeconds": 20, "radius": 8, "location": {"type": "Admin"}}})
    write(doc)
    print("d22 written")
elif mode == "perf":
    # session 15 (Debug build, D24 + D25), unattended: idle until now+8, t-150 (10 waves of 15) at now+8 for 8 min,
    # t-500 (10 waves of 50; needs MaxTrackedUnits 500, MaxUnitsPerWave 50) at now+18 for 8 min, then t-fault
    # (Debug.FaultInjection = t-fault) and t-other at now+30. Every other event disabled.
    now = datetime.datetime.now().replace(second=0, microsecond=0)
    day = now.strftime("%a")
    at = lambda m: [hhmm(now + datetime.timedelta(minutes=m))]
    def sched(id, pillar, m, dur, per, waves):
        a = point(unit(per), waves=waves, interval=10, radius=30)
        return {"id": id, "name": id, "enabled": True, "pillar": pillar, "trigger": {"type": "Schedule", "days": [day], "times": at(m)}, "durationSeconds": dur, "action": a}
    doc = build("Mon", ["04:00"], ["04:30"])
    for e in doc["events"]: e["enabled"] = False
    doc["events"] += [sched("t-150", "spawns", 8, 480, 15, 10), sched("t-500", "spawns", 18, 480, 50, 10),
                      sched("t-fault", "zones", 30, 120, 2, 1), sched("t-other", "sieges", 30, 120, 2, 1)]
    write(doc)
    print("perf: t-150", at(8), "t-500", at(18), "t-fault/t-other", at(30))
elif mode == "drain":
    # session 16 (A16), unattended: t-150 (10 waves of 15 at a Point) at now+3 for 3 min; at end + grace its 150 units
    # must drain 5 a tick to "0 left", well before their LifeTime (due + 90 s at the default caps).
    now = datetime.datetime.now().replace(second=0, microsecond=0)
    doc = build("Mon", ["04:00"], ["04:30"])
    for e in doc["events"]: e["enabled"] = False
    a = point(unit(15), waves=10, interval=10, radius=30)
    doc["events"].append({"id": "t-150", "name": "t-150", "enabled": True, "pillar": "spawns",
        "trigger": {"type": "Schedule", "days": [now.strftime("%a")], "times": [hhmm(now + datetime.timedelta(minutes=3))]},
        "durationSeconds": 180, "action": a})
    write(doc)
    print("drain: t-150 at", hhmm(now + datetime.timedelta(minutes=3)))
elif mode in ("s17a", "s17b"):
    # session 17 (foundation step 6/7): t-warn is 3 waves of 1, 90 s apart, 240 s, warnings on, at a Point. s17a fires it
    # by Schedule at argv[2] (HH:MM, today) and adds t-tonight at 23:55 for the daily banner to name; s17b makes it
    # Manual (an admin starts it) after the first-run seed has been read.
    now = datetime.datetime.now()
    warn = {"id": "t-warn", "name": "Test warnings", "enabled": True, "pillar": "spawns", "durationSeconds": 240,
            "announce": {"warnings": True}, "action": point(unit(1), waves=3, interval=90, radius=6)}
    if mode == "s17a":
        warn["trigger"] = {"type": "Schedule", "days": [now.strftime("%a")], "times": [sys.argv[2]]}
        tonight = {"id": "t-tonight", "name": "Test tonight", "enabled": True, "pillar": "spawns", "durationSeconds": 60,
                   "trigger": {"type": "Schedule", "days": [now.strftime("%a")], "times": ["23:55"]}, "action": point(unit(1))}
        doc = {"SchemaVersion": 1, "events": [warn, tonight]}
    else:
        warn["trigger"] = {"type": "Manual"}
        doc = {"SchemaVersion": 1, "events": [warn]}
    write(doc)
    print(mode, "written", sys.argv[2] if len(sys.argv) > 2 else "")
elif mode == "a21":
    # foundation A21 (step 9): t-own is 10 units in one wave, each with unitLifetimeSeconds 30 inside a 300 s event, so
    # their own lifetime decides the due time; with MaxDespawnsPerTick 1 they must leave 1 a tick ("10 units due for
    # despawn", then batches of 1 to "0 left"), not all in one frame by LifeTime. t-end (6 units, 60 s) is the event-end
    # control: queued at end + grace and drained the same way.
    now = datetime.datetime.now().replace(second=0, microsecond=0)
    at = [hhmm(now + datetime.timedelta(minutes=3))]
    own = point(unit(10), radius=6); own["unitLifetimeSeconds"] = 30
    end = point(unit(6), radius=6); end["location"] = {"type": "Point", "x": -1180, "z": -800}
    day = [now.strftime("%a")]
    doc = {"SchemaVersion": 1, "events": [
        {"id": "t-own", "name": "t-own", "enabled": True, "pillar": "spawns", "durationSeconds": 300,
         "trigger": {"type": "Schedule", "days": day, "times": at}, "action": own},
        {"id": "t-end", "name": "t-end", "enabled": True, "pillar": "spawns", "durationSeconds": 60,
         "trigger": {"type": "Schedule", "days": day, "times": at}, "action": end}]}
    write(doc)
    print("a21: t-own and t-end at", at[0])
elif mode == "a22":
    # foundation A22 (step 9): t-mark is 5 units for 600 s at a Point, spawned with the marker set first; the server is
    # killed right after the next "Finished Saving" with them alive, and the next boot must log "boot marker sweep: 5
    # found, 5 queued" and drain them (the marker still makes every unit findable).
    now = datetime.datetime.now().replace(second=0, microsecond=0)
    at = [hhmm(now + datetime.timedelta(minutes=3))]
    doc = {"SchemaVersion": 1, "events": [
        {"id": "t-mark", "name": "t-mark", "enabled": True, "pillar": "spawns", "durationSeconds": 600,
         "trigger": {"type": "Schedule", "days": [now.strftime("%a")], "times": at}, "action": point(unit(5), radius=6)}]}
    write(doc)
    print("a22: t-mark at", at[0])
elif mode == "rac2":
    # raphael-api-core session 2 (step 5, owner): the 5 seeded examples with example-spawns enabled and shortened to 2
    # waves 40 s apart (the D13 event, at the admin); t-150 (10 waves of 15, 20 s apart, warnings on, at a Point) for D22;
    # t-spare (disabled) for the enable, disable and set pushes; t-fill-1..4 (disabled) so `api events 2` has a row
    # (11 definitions). Every trigger is Manual or parked, so nothing starts on its own.
    seed = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Nyarlathotep", "Nyarlathotep", "Resources", "events.default.json")
    doc = json.load(io.open(os.path.normpath(seed), encoding="utf-8-sig"))
    for e in doc["events"]:
        if e["id"] == "example-spawns":
            e["enabled"] = True; e["durationSeconds"] = 120; e["action"]["waves"] = 2; e["action"]["intervalSeconds"] = 40
    t150 = point(unit(15), waves=10, interval=20, radius=30)
    doc["events"].append({"id": "t-150", "name": "Test 150", "enabled": True, "pillar": "spawns", "trigger": {"type": "Manual"},
                          "durationSeconds": 300, "announce": {"warnings": True}, "action": t150})
    for id in ["t-spare"] + [f"t-fill-{k}" for k in range(1, 5)]:
        doc["events"].append({"id": id, "name": id, "enabled": False, "pillar": "spawns", "trigger": {"type": "Manual"},
                              "durationSeconds": 60, "action": point(unit(1))})
    write(doc)
    print("rac2 written:", len(doc["events"]), "definitions")
elif mode in ("fe1", "show"):
    # faction-empowerment session 1 (step 4, unattended, D17): fe-short ends by expiry and its sample line reverts one
    # tick later; fe-long is still running when the server is stopped mid-window, so the next boot's carrier sweep finds
    # k > 0. Only these two definitions are written (nothing else starts on its own); the -Save snapshot of
    # tools/dev-snapshot.ps1 puts the previous files back after the session.
    opts = sys.argv[2:]
    def opt(name, default=None):
        if name in opts:
            i = opts.index(name)
            if i + 1 >= len(opts): sys.exit(f"{name} needs a value")
            return opts[i + 1]
        return default
    server = opt("--server", SERVER)
    dry = "--dry-run" in opts
    events_path = os.path.join(server, "BepInEx", "config", "Nyarlathotep", "events.json")
    cfg_path = os.path.join(server, "BepInEx", "config", "kdpen.Nyarlathotep.cfg")
    CFG_KEYS = [("Pillars", "FactionEmpowerment", "true"), ("Debug", "VerboseLogging", "true")]
    DAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]   # the validator's names, whatever the locale
    def cfg_get(text, section, key):
        cur = None
        for line in text.splitlines():
            t = line.strip()
            if t.startswith("[") and t.endswith("]"): cur = t[1:-1].strip()
            elif cur == section and "=" in t and not t.startswith("#") and t.split("=", 1)[0].strip() == key:
                return t.split("=", 1)[1].strip()
        return None
    def cfg_set(text, section, key, value):
        # Sets one BepInEx key in place (comments and every other line kept); a missing key is added at the end of its
        # section, a missing section at the end of the file (BepInEx fills in the rest on the next boot).
        nl = "\r\n" if "\r\n" in text else "\n"
        lines = text.splitlines()
        cur, sec_end, found = None, None, False
        for i, line in enumerate(lines):
            t = line.strip()
            if t.startswith("[") and t.endswith("]"):
                cur = t[1:-1].strip()
                if cur == section: sec_end = i + 1
            elif cur == section:
                if t: sec_end = i + 1
                if "=" in t and not t.startswith("#") and t.split("=", 1)[0].strip() == key:
                    lines[i] = f"{key} = {value}"; found = True
        if not found:
            if sec_end is None: lines += ([""] if lines and lines[-1].strip() else []) + [f"[{section}]", f"{key} = {value}"]
            else: lines.insert(sec_end, f"{key} = {value}")
        return nl.join(lines) + nl
    def read(path):
        return io.open(path, encoding="utf-8-sig", newline="").read() if os.path.exists(path) else None
    def atomic_write(path, text):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        tmp = path + ".tmp"
        with io.open(tmp, "w", encoding="utf-8", newline="") as f: f.write(text)
        os.replace(tmp, path)
    def show(events_text, cfg_text, heading):
        print(heading)
        print(f"  {events_path}:")
        if events_text is None: print("    (absent)")
        else:
            try:
                for e in json.loads(events_text).get("events", []):
                    t, a = e.get("trigger", {}), e.get("action", {})
                    when = f"{','.join(t.get('days', []))} {','.join(t.get('times', []))}".strip()
                    what = f"Empower {','.join(a.get('factions', []))} {json.dumps(a.get('stats', {}))}" if a.get("type") == "Empower" else a.get("type")
                    print(f"    {e.get('id')}: enabled={e.get('enabled')} pillar={e.get('pillar')} trigger={t.get('type')} {when} durationSeconds={e.get('durationSeconds')} action={what}")
            except ValueError as x: print(f"    (not valid JSON: {x})")
        print(f"  {cfg_path}:")
        for section, key, _ in CFG_KEYS + [("General", "Enabled", None)]:
            v = cfg_get(cfg_text, section, key) if cfg_text is not None else None
            print(f"    {section}.{key} = {v if v is not None else '(absent: the default applies)'}")
    if mode == "show":
        show(read(events_path), read(cfg_path), "current files:")
        sys.exit(0)
    delay = int(opt("--delay", "0"))
    now = datetime.datetime.now()
    def due(minutes):
        t = now + datetime.timedelta(minutes=minutes + delay)
        if t.second or t.microsecond: t = t.replace(second=0, microsecond=0) + datetime.timedelta(minutes=1)
        return t
    def empower(id, name, minutes, duration):
        t = due(minutes)   # each event names its own day, so a due time past midnight is still met
        return {"id": id, "name": name, "enabled": True, "pillar": "empowerment",
                "trigger": {"type": "Schedule", "days": [DAYS[t.weekday()]], "times": [hhmm(t)]},
                "durationSeconds": duration,
                "action": {"type": "Empower", "factions": ["Faction_Bandits"], "stats": {"physicalPower": 1.5, "maxHealth": 1.5}}}
    doc = {"SchemaVersion": 1, "events": [empower("fe-short", "FE short (expires)", 2, 60),
                                          empower("fe-long", "FE long (stopped mid-window)", 5, 1200)]}
    events_text = json.dumps(doc, indent=2) + "\n"
    old_cfg = read(cfg_path)
    cfg_text = old_cfg if old_cfg is not None else ""
    for section, key, value in CFG_KEYS: cfg_text = cfg_set(cfg_text, section, key, value)
    if (cfg_get(cfg_text, "General", "Enabled") or "true").lower() != "true":
        print("warning: General.Enabled is false in the cfg; the mod will not run")
    if dry:
        show(events_text, cfg_text, f"fe1 --dry-run (run at {now:%H:%M:%S}, nothing written):")
        sys.exit(0)
    for path in (events_path, cfg_path):
        if os.path.exists(path):
            shutil.copy2(path, os.path.join(here(), "fe1-" + os.path.basename(path) + ".bak"))
    atomic_write(events_path, events_text)
    atomic_write(cfg_path, cfg_text)
    show(read(events_path), read(cfg_path), f"fe1 written (run at {now:%H:%M:%S}; previous files copied to {HERE}):")
elif mode == "restore-valid":
    write(json.load(open(os.path.join(here(), "d23a.json"))))
    print("valid d23a restored")
else:
    sys.exit(f"unknown mode {mode}\n{USAGE}")
