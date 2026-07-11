#!/usr/bin/env python3
"""Validate checked-in captured CorpseFullUpdate template hex lengths."""

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "AORebirth" / "Server" / "ZoneEngine" / "Core" / "Packets" / "CorpseFullUpdate.cs"


def main():
    text = SOURCE.read_text(encoding="utf-8-sig")
    lengths = {
        name: int(value)
        for name, value in re.findall(
            r"private const int (Captured\w+)PacketLength\s*=\s*(\d+)\s*;",
            text,
        )
    }
    templates = re.findall(
        r"private static readonly byte\[\] (Captured\w+)Template\s*=\s*HexToBytes\((.*?)\);",
        text,
        re.DOTALL,
    )
    if not templates:
        raise SystemExit("no captured corpse templates found")

    failures = []
    for name, expression in templates:
        hex_text = "".join(re.findall(r'"([0-9A-Fa-f]+)"', expression))
        expected = lengths.get(name)
        if expected is None:
            failures.append(f"{name}: missing {name}PacketLength constant")
            continue
        if len(hex_text) % 2:
            failures.append(f"{name}: odd hex length {len(hex_text)}")
            continue
        actual = len(bytes.fromhex(hex_text))
        if actual != expected:
            failures.append(f"{name}: expected {expected} bytes, found {actual}")
            continue
        print(f"PASS {name} length={actual}")

    if failures:
        raise SystemExit("\n".join(failures))


if __name__ == "__main__":
    main()
