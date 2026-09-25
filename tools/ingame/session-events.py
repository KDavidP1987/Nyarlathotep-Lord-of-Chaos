# In-game session helper: writes the dev server's events.json for foundation's step 5 tests.
# Modes: boot (test events, schedules parked on Mon 04:00), go (schedules relative to now), d23a (adds an event with an
# unknown unit), d23b (the same file with a comma removed), restore-valid (d23a again). State in %TEMP%
yar-session.
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
    doc = json.load(open(os.path.join(HERE, "go.json")))
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
elif mode == "restore-valid":
    write(json.load(open(os.path.join(HERE, "d23a.json"))))
    print("valid d23a restored")
