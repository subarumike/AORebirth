"""Decode perk CharacterAction using events.log + raw hex for action opcodes."""
from __future__ import annotations

import csv
import re
import struct
from pathlib import Path

CAPTURE = Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260715-194155"
)


def main() -> None:
    # From known TrainPerk OUT hex, action bytes are BB000000 => LE 0xBB
    # From known Action180 IN hex, action bytes are B4000000 => LE 0xB4
    path = CAPTURE / "raw-packets.csv"
    interesting = []
    with path.open(newline="", encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            if row["N3TypeName"] != "CharacterAction":
                continue
            hx = row["RawHex"].upper()
            idx = hx.find("5E477770")
            if idx < 0:
                continue
            body = bytes.fromhex(hx[idx + 8 :])
            # body: type(4) inst(4) unk1(4) action_le(4) tgtType(4) tgtInst(4) p1(4) p2(4) unk2(2)
            if len(body) < 34:
                continue
            id_type, id_inst, unk1 = struct.unpack_from(">III", body, 0)
            action = struct.unpack_from("<I", body, 12)[0]
            tgt_type, tgt_inst, p1, p2 = struct.unpack_from(">IIII", body, 16)
            if action < 0xB0 or action > 0xC8:
                continue
            p2a = ""
            try:
                s = struct.pack(">I", p2).decode("ascii")
                if s.isprintable():
                    p2a = s
            except Exception:
                pass
            interesting.append(
                (
                    row["CapturedUtc"],
                    row["Direction"],
                    action,
                    tgt_type,
                    tgt_inst,
                    p1,
                    p2,
                    p2a,
                )
            )

    print(f"count={len(interesting)}")
    for ts, d, a, tt, ti, p1, p2, p2a in interesting:
        extra = f" '{p2a}'" if p2a else ""
        print(
            f"{ts} {d} action=0x{a:02X}({a}) tgt={tt:X}:{ti:X} p1={p1} p2={p2}{extra}"
        )

    # Map action codes used in events by timestamp proximity to names
    events = (CAPTURE / "events.log").read_text(encoding="utf-8", errors="replace")
    for name in (
        "TrainPerk",
        "UsePerk",
        "QueuePerk",
        "PerkUnavailable",
        "PerkAvailable",
        "Action=180",
    ):
        print(name, "hits", events.count(name))


if __name__ == "__main__":
    main()
