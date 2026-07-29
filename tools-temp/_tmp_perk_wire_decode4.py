"""Extract CharacterAction opcodes near perk times; trust events.log field values."""
from __future__ import annotations

import csv
import struct
from pathlib import Path

CAPTURE = Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260715-194155"
)

# Windows of interest (UTC prefixes)
WINDOWS = (
    "2026-07-15T17:42:02",
    "2026-07-15T17:42:32",
    "2026-07-15T17:42:33",
    "2026-07-15T17:42:34",
    "2026-07-15T17:42:40",
    "2026-07-15T17:42:42",
    "2026-07-15T17:43:02",
    "2026-07-15T17:43:15",
)


def parse_ca(hx: str):
    hx = hx.upper()
    idx = hx.find("5E477770")
    if idx < 0:
        return None
    body = bytes.fromhex(hx[idx + 8 :])
    if len(body) < 34:
        return None
    # After N3 type: Identity(type,inst) then members per serializer.
    # Events prove: Action values small (<=200). Try both member orderings.
    id_type, id_inst = struct.unpack_from(">II", body, 0)
    # Ordering A (matches message class Action first): but class says Action is AoMember(0)
    # Observed bytes after identity: 00000000 B4000000 00000000 0003796D 0000280A 434E5245 0000
    # If Action is first and LE: unk? 
    # Actually class order: Action, Unknown1, Target, P1, P2, Unknown2
    # Bytes: B4000000 00000000 00000000 0003796D 0000280A 434E5245 0000
    # But we have leading 00000000 before B4... so either Unknown before Action on wire
    # or identity packing includes something else.
    # Leading after id_inst: 00000000 B4000000 ...
    rest = body[8:]
    # Try: Unknown1(BE) + Action(LE) + TargetType(BE) + TargetInst(BE) + P1(BE) + P2(BE)
    unk1 = struct.unpack_from(">I", rest, 0)[0]
    action = struct.unpack_from("<I", rest, 4)[0]
    tgt_type, tgt_inst, p1, p2 = struct.unpack_from(">IIII", rest, 8)
    # Validate against known Channel Rage action pack
    return {
        "id_type": id_type,
        "id_inst": id_inst,
        "unk1": unk1,
        "action": action,
        "tgt": (tgt_type, tgt_inst),
        "p1": p1,
        "p2": p2,
        "p2a": _ascii(p2),
    }


def _ascii(v: int) -> str:
    try:
        s = struct.pack(">I", v).decode("ascii")
        return s if s.isprintable() else ""
    except Exception:
        return ""


def main() -> None:
    with (CAPTURE / "raw-packets.csv").open(newline="", encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            ts = row["CapturedUtc"]
            if not any(ts.startswith(w) for w in WINDOWS):
                continue
            if row["N3TypeName"] != "CharacterAction":
                continue
            d = parse_ca(row["RawHex"])
            if not d:
                continue
            print(
                f"{ts} {row['Direction']:3} act=0x{d['action']:02X}({d['action']:3}) "
                f"tgt={d['tgt'][0]:X}:{d['tgt'][1]:X} p1={d['p1']} p2={d['p2']} "
                f"'{d['p2a']}' unk1={d['unk1']}"
            )

    print("\nCross-check Action180 expected: act=180 tgt=0:3796D p1=10250 p2=CNRE")


if __name__ == "__main__":
    main()
