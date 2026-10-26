"""Cache bounded, pinned public bot database evidence; never treats names as item IDs."""
from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
from pathlib import Path
import urllib.error
import urllib.request

COMMIT = "de9e3b2c8d2f91df87c614a3d9f91bc16c2eacf2"
FILES = {
    "Modules/ITEMS_MODULE/aodb.csv": "f22331647439c1c93320cbbe4c5ddc7cdbc05b5f",
    "Modules/ITEMS_MODULE/boss_lootdb.csv": "9f30ba837c9d8f80e46ee8570e9866fd92366e7b",
    "Modules/ITEMS_MODULE/boss_namedb.csv": "e7145a94e4f5d0a612f64afcc18dd97a308c12f7",
    "Modules/LOOT_MODULE/raid_loot.csv": "009c208c2d5c6269ac498405cfc4596519810974",
    "Modules/IMPLANT_MODULE/Symbiant.csv": "6280bb45260c2db7200600a6ed04fa52d5016521",
    "Modules/IMPLANT_MODULE/pocketboss.csv": "961e7c65cb4f68408851b0dce759cd67e7b17828",
    "Modules/HELPBOT_MODULE/playfields.csv": "6e25076eaad3c13b8b710b9d5f59355c314344a5",
    "Modules/HELPBOT_MODULE/dynadb.csv": "b1fea622cbe7393e9b7de5bc9bd24abc0c390c72",
    "Modules/ALIEN_MODULE/ofabarmor.csv": "4b2dafe562c988fd431a318fbfb5301b095a25c9",
}


def write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.cache_root.resolve()
    root.mkdir(parents=True, exist_ok=True)
    sources = []
    for relative, expected_blob in FILES.items():
        name = relative.replace("/", "_")
        path = root / name
        url = f"https://raw.githubusercontent.com/Nadybot/Nadybot/{COMMIT}/src/{relative}"
        status = "CACHED"
        if not path.exists():
            try:
                request = urllib.request.Request(url, headers={"User-Agent": "AORebirth-Offline-Historical-Research/1.0"})
                with urllib.request.urlopen(request, timeout=25) as response:
                    content = response.read(8_000_001)
                if len(content) > 8_000_000:
                    raise ValueError("bounded source size exceeded")
                path.write_bytes(content)
                status = "FETCHED"
            except (urllib.error.URLError, TimeoutError, ValueError) as error:
                sources.append({"id": name, "url": url, "status": "UNAVAILABLE", "error": str(error)})
                continue
        raw = path.read_bytes()
        actual_blob = hashlib.sha1(b"blob " + str(len(raw)).encode() + b"\0" + raw).hexdigest()
        if actual_blob != expected_blob:
            raise ValueError(f"Pinned public Git blob identity differs: {relative}")
        csv_text = "\n".join(line for line in raw.decode("utf-8-sig").splitlines()
                             if not line.startswith("#"))
        reader = csv.DictReader(io.StringIO(csv_text))
        rows = list(reader)
        sources.append({"id": name, "url": url, "browseUrl":
                        f"https://github.com/Nadybot/Nadybot/blob/{COMMIT}/src/{relative}",
                        "file": name, "commit": COMMIT, "gitBlobSha1": actual_blob,
                        "sha256": hashlib.sha256(raw).hexdigest(), "bytes": len(raw),
                        "rows": len(rows), "fields": reader.fieldnames, "status": "AVAILABLE",
                        "retrieval": status, "sample": rows[:2]})
    write_json(root / "source-index.json", {"formatVersion": 1, "sources": sources,
               "policy": "Exact IDs only. Froob usability is not expansion origin. Names alone do not identify an acquisition record."})
    print(json.dumps({"sources": len(sources), "available": sum(x["status"] == "AVAILABLE" for x in sources),
                      "cache": str(root)}, sort_keys=True))


if __name__ == "__main__":
    main()
