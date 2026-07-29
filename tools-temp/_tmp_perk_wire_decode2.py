"""Decode perk CharacterAction packets from capture 20260715-194155."""
from __future__ import annotations

import csv
import struct
from pathlib import Path

CAPTURE = Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260715-194155"
)

# Seed names from events.log; fill others from discovered action ints.
ACTION_NAMES = {
    0xB4: "AddPerkAction",
    0xBB: "TrainPerk",
}


def decode_after_marker(hexblob: str) -> dict | None:
    data = bytes.fromhex(hexblob)
    marker = bytes.fromhex("5E477770")
    idx = data.find(marker)
    if idx < 0:
        return None
    body = data[idx + 4 :]
    if len(body) < 38:
        return None
    # 0000C350 | idInst | unk1 | action | tgtType | tgtInst | p1 | p2 | unk2
    # Note: identity type often omitted in this packing after C350; C350 may be type+flags.
    hdr, id_inst, unk1, action, tgt_type, tgt_inst, p1, p2 = struct.unpack_from(
        ">IIIIIIII", body, 0
    )
    unk2 = struct.unpack_from(">H", body, 32)[0] if len(body) >= 34 else None
    p2_ascii = ""
    try:
        s = struct.pack(">I", p2).decode("ascii")
        if s.isprintable():
            p2_ascii = s
    except Exception:
        pass
    return {
        "hdr": hdr,
        "id_inst": id_inst,
        "unk1": unk1,
        "action": action,
        "tgt_type": tgt_type,
        "tgt_inst": tgt_inst,
        "p1": p1,
        "p2": p2,
        "p2_ascii": p2_ascii,
        "unk2": unk2,
    }


def main() -> None:
    path = CAPTURE / "raw-packets.csv"
    with path.open(newline="", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)
        print("fields", reader.fieldnames)
        count = 0
        for row in reader:
            if row.get("N3TypeName") != "CharacterAction":
                continue
            hexblob = row["RawHex"].strip()
            d = decode_after_marker(hexblob)
            if not d:
                continue
            a = d["action"]
            # perk-related window from capture evidence
            if a < 0xB0 or a > 0xC8:
                continue
            count += 1
            name = ACTION_NAMES.get(a, f"0x{a:02X}")
            p2s = f" '{d['p2_ascii']}'" if d["p2_ascii"] else ""
            print(
                f"{row['CapturedUtc']} {row['Direction']} act={a}({name}) "
                f"tgt={d['tgt_type']:X}:{d['tgt_inst']:X} p1={d['p1']} p2={d['p2']}{p2s}"
            )
        print("count", count)

    # Cross-check action codes named in events.log by matching timestamps for UsePerk
    print("\nKnown hashes:")
    for v, label in [
        (1129206341, "CNRE"),
        (1398034242, "STOB"),
        (1146771273, "DZWI"),
        (1162630213, "ELTE"),
    ]:
        print(label, hex(v), struct.pack(">I", v))


if __name__ == "__main__":
    main()
