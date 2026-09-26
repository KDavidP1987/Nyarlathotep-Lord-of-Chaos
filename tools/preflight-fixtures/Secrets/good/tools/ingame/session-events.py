# In-game session helper: writes the dev server's events.json for foundation's step 5 tests.
# Modes: boot (test events, schedules parked on Mon 04:00), go (schedules relative to now), d23a (boot's events plus one
# with an unknown unit), d23b (the same file with a comma removed), restore-valid (d23a again), cool (the last valid file
# with t-cool due every minute from now+2 to now+13, so a purge right after it has due times inside its cooldown). State in
# %TEMP%/nyar-session. s17a/s17b: session 17 (see the mode's comment).
import json, sys, datetime, io, os
CFG = r"C:\Program Files (x86)\Steam\steamapps\common\VRisingDedicatedServer\BepInEx\config\Nyarlathotep\events.json"
HERE = os.environ.get("NYAR_SESSION_DIR") or os.path.join(os.environ["TEMP"], "nyar-session")
os.makedirs(HERE, exist_ok=True)
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
    json.dump(doc, open(os.path.join(HERE, "go.json"), "w"), indent=2)
    print("go: t-sched at", hhmm(sched), "t-cool", cool)
elif mode == "d23a":
    doc = build("Mon", ["04:00"], ["04:30"])
    doc["events"].append({"id": "t-badunit", "name": "Test unknown unit", "enabled": True, "pillar": "spawns", "trigger": {"type": "Manual"}, "durationSeconds": 60, "action": point([{"prefab": "CHAR_Not_A_Real_Unit", "count": 1}])})
    json.dump(doc, open(os.path.join(HERE, "d23a.json"), "w"), indent=2)
    write(doc)
    print("d23a written")
elif mode == "d23b":
    text = json.dumps(json.load(open(os.path.join(HERE, "d23a.json"))), indent=2)
    lines = text.split("\n")
    # remove the comma after the first event's "name" value (line 5 of the pretty text)
    i = next(n for n, l in enumerate(lines) if '"name": "Test manual raid",' in l)
    lines[i] = lines[i].rstrip(",")
    write(None, "\n".join(lines))
    print("d23b written: comma removed on line", i + 1)
elif mode == "cool":
    src = os.path.join(HERE, "d23a.json")
    doc = json.load(open(src)) if os.path.exists(src) else build("Mon", ["04:00"], ["04:30"])
    now = datetime.datetime.now().replace(second=0, microsecond=0)
    cool = [hhmm(now + datetime.timedelta(minutes=2 + k)) for k in range(12)]
    ev = next(e for e in doc["events"] if e["id"] == "t-cool")
    ev["trigger"] = {"type": "Schedule", "days": [now.strftime("%a")], "times": cool}
    write(doc)
    json.dump(doc, open(os.path.join(HERE, "cool.json"), "w"), indent=2)
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
elif mode == "restore-valid":
    write(json.load(open(os.path.join(HERE, "d23a.json"))))
    print("valid d23a restored")
