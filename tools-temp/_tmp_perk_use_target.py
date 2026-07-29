import csv
import struct
from collections import Counter
from pathlib import Path

CAPTURE = Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260715-194155\raw-packets.csv"
)

NAMES = {
    179: "UsePerk",
    180: "AddPerkAction",
    187: "TrainPerk",
    80: "QueuePerk",
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
    return action, tgt_type, tgt_inst, p1, p2


def main() -> None:
    counts = Counter()
    with CAPTURE.open(newline="", encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            counts[row.get("N3TypeName", "")] += 1
            if row.get("N3TypeName") != "CharacterAction":
                continue
            parsed = parse_ca(row["RawHex"])
            if not parsed:
                continue
            action, tgt_type, tgt_inst, p1, p2 = parsed
            if action not in NAMES:
                continue
            name = NAMES[action]
            print(
                f"{row['CapturedUtc']} {row['Direction']:3} {name:16} "
                f"tgt={tgt_type:X}:{tgt_inst:X} p1={p1} p2={p2}"
            )
    print("N3 top:", counts.most_common(12))


if __name__ == "__main__":
    main()
