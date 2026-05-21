#!/usr/bin/env python3
"""Parse a saved AO pcap into packet, flow, and summary files.

This is the non-interactive half of ao_traffic_watcher.py. It lets us run
tshark in the background, stop it later, and then decode the saved pcap
without replaying the capture manually.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import sys
from pathlib import Path
from typing import Any


AO_LOGGER_ROOT = Path(r"C:\Users\Mike\Documents\AO Live Logger")
WATCHER_PATH = AO_LOGGER_ROOT / "ao_traffic_watcher.py"
DEFAULT_TSHARK = r"C:\Program Files\Wireshark\tshark.exe"


def load_watcher() -> Any:
    spec = importlib.util.spec_from_file_location("ao_traffic_watcher", WATCHER_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {WATCHER_PATH}")
    module = importlib.util.module_from_spec(spec)
    sys.modules["ao_traffic_watcher"] = module
    spec.loader.exec_module(module)
    return module


def read_meta(capture_dir: Path) -> dict[str, Any]:
    path = capture_dir / "capture_meta.json"
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_meta(capture_dir: Path, meta: dict[str, Any]) -> None:
    (capture_dir / "capture_meta.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture_dir", type=Path)
    parser.add_argument("--tshark", default=DEFAULT_TSHARK)
    parser.add_argument("--payload-bytes", type=int, default=65535)
    args = parser.parse_args()

    capture_dir = args.capture_dir
    raw_path = capture_dir / "raw.pcapng"
    if not raw_path.exists():
        raise FileNotFoundError(raw_path)

    watcher = load_watcher()
    tshark_path = watcher.ensure_tshark(args.tshark)
    local_ips = watcher.parse_ipconfig_addresses()
    meta = read_meta(capture_dir)
    interface_name = meta.get("interface_name") or meta.get("interface") or "Ethernet"

    packets = watcher.read_packets_from_pcap(
        tshark_path=tshark_path,
        pcap_path=raw_path,
        interface_name=interface_name,
        local_ips=local_ips,
        payload_preview_bytes=args.payload_bytes,
    )
    watcher.write_jsonl(capture_dir / "packets.jsonl", packets)
    watcher.write_packets_csv(capture_dir / "packets.csv", packets)

    flow_rows = watcher.compute_flow_rows(packets)
    watcher.write_flows_json(capture_dir / "flows.json", flow_rows)

    session_meta = {
        "session_dir": str(capture_dir),
        "start_time": meta.get("started_utc") or meta.get("start_time") or "",
        "end_time": meta.get("stopped_utc") or meta.get("end_time") or "",
        "interface_name": interface_name,
        "filter_mode_used": meta.get("filter_mode_used") or "host",
        "capture_filter": meta.get("capture_filter") or "",
    }
    summary = watcher.summarize(
        packets=packets,
        label_counts={},
        top_n=20,
        session_meta=session_meta,
    )
    (capture_dir / "summary.md").write_text(summary, encoding="utf-8")

    meta["packet_decode"] = {
        "packets": len(packets),
        "flows": len(flow_rows),
        "payload_preview_bytes": args.payload_bytes,
    }
    write_meta(capture_dir, meta)

    print(f"packets={len(packets)} flows={len(flow_rows)}")
    print(capture_dir / "summary.md")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
