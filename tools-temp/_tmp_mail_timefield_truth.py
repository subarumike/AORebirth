#!/usr/bin/env python3
from __future__ import annotations

import csv
import pathlib
import sys
from datetime import date, datetime, timezone

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

CAP = pathlib.Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture"
    r"\bin\Debug\captures\20260715-Recive-mail-datetime-stamp"
)
OUT = CAP / "_mail_timefield_truth.txt"
MAIL = 0x333B2867
X3F1 = 0x03F1


def bhex(s: str) -> bytes:
    s = "".join(c for c in s if c in "0123456789abcdefABCDEF")
    return bytes.fromhex(s if len(s) % 2 == 0 else s[:-1])


def find_type(data: bytes):
    be = MAIL.to_bytes(4, "big")
    le = MAIL.to_bytes(4, "little")
    offs = []
    for endian, needle in (("be", be), ("le", le)):
        i = 0
        while True:
            j = data.find(needle, i)
            if j < 0:
                break
            offs.append((j, endian))
            i = j + 1
    return offs


def ri16(b, o):
    return int.from_bytes(b[o : o + 2], "little", signed=True), o + 2


def ru16(b, o):
    return int.from_bytes(b[o : o + 2], "little", signed=False), o + 2


def ri32(b, o):
    return int.from_bytes(b[o : o + 4], "little", signed=True), o + 4


def ru32(b, o):
    return int.from_bytes(b[o : o + 4], "little", signed=False), o + 4


def ri32be(b, o):
    return int.from_bytes(b[o : o + 4], "big", signed=True), o + 4


def ru32be(b, o):
    return int.from_bytes(b[o : o + 4], "big", signed=False), o + 4


def ri64(b, o):
    return int.from_bytes(b[o : o + 8], "little", signed=True), o + 8


def ri64be(b, o):
    return int.from_bytes(b[o : o + 8], "big", signed=True), o + 8


def lp(b, o, endian="le"):
    if endian == "le":
        n, o = ri16(b, o)
    else:
        n = int.from_bytes(b[o : o + 2], "big", signed=True)
        o += 2
    if n < 0 or o + n > len(b):
        return f"<bad {n}>", o
    return b[o : o + n].decode("latin1", errors="replace"), o + n


def interp(tf: int, lines):
    lines.append(f"    TimeField={tf} (0x{tf & 0xffffffff:08x})")
    tries = []
    # days since 1970
    try:
        tries.append(("days_since_1970", date.fromordinal(date(1970, 1, 1).toordinal() + tf).isoformat()))
    except Exception as e:
        tries.append(("days_since_1970", str(e)))
    for name, sec in [
        ("unix_s", tf),
        ("unix_u32", tf & 0xFFFFFFFF),
        ("minutes", tf * 60),
        ("hours", tf * 3600),
        ("days_as_secs", tf * 86400),
    ]:
        try:
            tries.append((name, datetime.fromtimestamp(sec, tz=timezone.utc).isoformat()))
        except Exception as e:
            tries.append((name, f"FAIL {e}"))
    # AO / Windows FILETIME mid tricks: sometimes packed date
    # boost date as (yyyy<<16)|(mm<<8)|dd ?
    y = (tf >> 16) & 0xFFFF
    m = (tf >> 8) & 0xFF
    d = tf & 0xFF
    tries.append((f"packed_ymd {y}-{m}-{d}", ""))
    for name, val in tries:
        lines.append(f"      {name}: {val}")


def decode_entry(b, o, endian, tag, lines):
    r64 = ri64 if endian == "le" else ri64be
    r32 = ri32 if endian == "le" else ri32be
    mail_id, o = r64(b, o)
    tf, o = r32(b, o)
    frm, o = lp(b, o, endian)
    subj, o = lp(b, o, endian)
    credits, o = r32(b, o)
    cod, o = r32(b, o)
    flags, o = r32(b, o)
    summary = b[o]
    o += 1
    lines.append(
        f"  ENTRY[{tag}/{endian}] id={mail_id & 0xffffffffffffffff:016x} from={frm!r} subj={subj!r} "
        f"cr={credits} cod={cod} flags={flags} summary={summary}"
    )
    interp(tf, lines)
    if summary == 0:
        ext64, o = r32(b, o)
        a, o = r32(b, o)
        c, o = r32(b, o)
        d, o = r32(b, o)
        pad, o = r32(b, o)
        e74, o = r32(b, o)
        body, o = lp(b, o, endian)
        lines.append(f"    detail ext64={ext64} acg={a}/{c}/{d} pad={pad} e74={e74} body={body!r}")
    return o


def decode_payload(data: bytes, off: int, endian: str, meta: str, lines: list):
    # After type dword: Identity type+inst, unknown byte, action i16
    o = off + 4
    if endian == "le":
        idt, o = ru32(data, o)
        idi, o = ru32(data, o)
        unk = data[o]
        o += 1
        action = int.from_bytes(data[o : o + 2], "little", signed=True)
        o += 2
    else:
        idt, o = ru32be(data, o)
        idi, o = ru32be(data, o)
        unk = data[o]
        o += 1
        action = int.from_bytes(data[o : o + 2], "big", signed=True)
        o += 2
    lines.append(f"\n== {meta} endian={endian} action={action} id={idt:x}/{idi:x} unk={unk}")
    if action == 0:
        if endian == "le":
            enc, o = ri32(data, o)
        else:
            enc, o = ri32be(data, o)
        count = (enc // X3F1) - 1 if enc else -1
        # also try opposite if nonsense
        lines.append(f"  list enc=0x{enc & 0xffffffff:08x} count={count}")
        for i in range(max(0, min(count if count < 50 else 0, 10))):
            o = decode_entry(data, o, endian, f"L{i}", lines)
    elif action == 2:
        o = decode_entry(data, o, endian, "D", lines)
    elif action in (1, 3, 5, 7):
        if endian == "le":
            req, o = ri64(data, o)
        else:
            req, o = ri64be(data, o)
        lines.append(f"  reqId={req}")


lines: list[str] = []
with (CAP / "raw-packets.csv").open(newline="", encoding="utf-8-sig") as f:
    for row in csv.DictReader(f):
        if row.get("N3TypeName") != "Mail":
            continue
        data = bhex(row["RawHex"])
        meta = f"{row['Direction']} {row['CapturedUtc']} len={row['PacketLength']}"
        lines.append(f"\n##### {meta}")
        lines.append(f"RawHex={row['RawHex']}")
        offs = find_type(data)
        if not offs:
            lines.append("NO TYPE MARKER")
            continue
        for off, endian in offs:
            decode_payload(data, off, endian, meta, lines)
            # also try flipping endianness for action parse if list count looks wrong
            other = "be" if endian == "le" else "le"
            decode_payload(data, off, other, meta + " (forced flip)", lines)

# expected day numbers
for d in [date(2026, 7, 15), date(2026, 7, 13), date(2026, 7, 17)]:
    lines.append(f"days_since_1970({d})={(d - date(1970,1,1)).days}")

OUT.write_text("\n".join(lines), encoding="utf-8")
print("wrote", OUT)
