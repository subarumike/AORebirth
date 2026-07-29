"""Correct CharacterAction wire layout for perk capture packets."""
from __future__ import annotations

import csv
import struct
from pathlib import Path

CAPTURE = Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260715-194155"
)

NAMES = {
    179: "UsePerk",
    180: "AddPerkAction",
    187: "TrainPerk",
    80: "QueuePerk?",
    206: "PerkAvailable",
    207: "PerkUnavailable",
}


def parse_ca(hx: str):
    hx = hx.upper()
    idx = hx.find("5E477770")
    if idx < 0:
        return None
    body = bytes.fromhex(hx[idx + 8 :])
    if len(body) < 38:
        return None
    id_type, id_inst, n3unk = struct.unpack_from(">III", body, 0)
    action = struct.unpack_from("<I", body, 12)[0]
    ca_unk1, tgt_type, tgt_inst, p1, p2 = struct.unpack_from(">IIIII", body, 16)
    try:
        p2a = struct.pack(">I", p2).decode("ascii")
        if not p2a.isprintable():
            p2a = ""
    except Exception:
        p2a = ""
    return {
        "id": f"{id_type:X}:{id_inst:X}",
        "n3unk": n3unk,
        "action": action,
        "ca_unk1": ca_unk1,
        "tgt": f"{tgt_type:X}:{tgt_inst:X}",
        "p1": p1,
        "p2": p2,
        "p2a": p2a,
    }


def main() -> None:
    with (CAPTURE / "raw-packets.csv").open(newline="", encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            if row["N3TypeName"] != "CharacterAction":
                continue
            d = parse_ca(row["RawHex"])
            if not d:
                continue
            a = d["action"]
            if a not in NAMES and not (0xB0 <= a <= 0xD0):
                continue
            name = NAMES.get(a, f"0x{a:02X}")
            p2s = f" '{d['p2a']}'" if d["p2a"] else ""
            print(
                f"{row['CapturedUtc']} {row['Direction']:3} {name:16} "
                f"act={a} tgt={d['tgt']} p1={d['p1']} p2={d['p2']}{p2s} caUnk1={d['ca_unk1']}"
            )


if __name__ == "__main__":
    main()
