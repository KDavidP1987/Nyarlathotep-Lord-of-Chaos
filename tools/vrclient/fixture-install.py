"""Put fixture events into a dev events.json, replacing any event with the same id; every other event stays.

  python tools/vrclient/fixture-install.py <events.json> <id> [<id> ...]

Each <id> is tools/vrclient/scenarios/fixtures/<id>.json. Dev server only."""
import json
import os
import sys

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "scenarios", "fixtures")


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print(__doc__)
        return 2
    events_path, ids = argv[0], argv[1:]
    with open(events_path, encoding="utf-8") as fh:
        doc = json.load(fh)
    events = [e for e in doc["events"] if e.get("id") not in ids]
    for i in ids:
        with open(os.path.join(FIXTURES, f"{i}.json"), encoding="utf-8") as fh:
            events.append(json.load(fh))
    doc["events"] = events
    with open(events_path, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=2)
    print(f"{events_path}: {', '.join(ids)} in, {len(events)} events in all")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
