#!/usr/bin/env python3
from __future__ import annotations

import csv
import pathlib
import re
import sys
from datetime import date, datetime, timezone

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

CAP = pathlib.Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture"
    r"\bin\Debug\captures\20260715-Recive-mail-datetime-stamp"
)
MAIL = 0x333B2867
X3F1 = 0x03F1
OUT = CAP / "_mail_datetime_decode.txt"


def parse_hex_blob(s: str) -> bytes:
    s = re.sub(r"[^0-9A-Fa-f]", "", s or "")
    if len(s) % 2:
        s = s[:-1]
    return bytes.fromhex(s) if s else b""


def find_mail_offsets(data: bytes):
    needle = MAIL.to_bytes(4, "little")
    offs = []
    i = 0
    while True:
        j = data.find(needle, i)
        if j < 0:
            break
        offs.append(j)
        i = j + 1
    return offs


def read_i16(b, o):
    return int.from_bytes(b[o : o + 2], "little", signed=True), o + 2


def read_u32(b, o):
    return int.from_bytes(b[o : o + 4], "little", signed=False), o + 4


def read_i32(b, o):
    return int.from_bytes(b[o : o + 4], "little", signed=True), o + 4


def read_i64(b, o):
    return int.from_bytes(b[o : o + 8], "little", signed=True), o + 8


def read_lp_str(b, o):
    n, o = read_i16(b, o)
    if n < 0 or n > 4096 or o + n > len(b):
        return f"<bad {n}>", o
    return b[o : o + n].decode("latin1", errors="replace"), o + n


def interpret_time(time_field: int, lines: list):
    lines.append(f"    time_field signed={time_field} unsigned={time_field & 0xffffffff} hex=0x{time_field & 0xffffffff:08x}")
    # days since 1970
    try:
        d = date.fromordinal(date(1970, 1, 1).toordinal() + time_field)
        lines.append(f"    interp days_since_1970 -> {d.isoformat()}")
    except Exception as e:
        lines.append(f"    interp days_since_1970 FAIL {e}")
    # unix seconds
    for label, ts in (("unix_s", time_field), ("unix_s_u32", time_field & 0xffffffff)):
        try:
            dt = datetime.fromtimestamp(ts, tz=timezone.utc)
            lines.append(f"    interp {label}_utc -> {dt.isoformat()}")
        except Exception as e:
            lines.append(f"    interp {label} FAIL {e}")
    # FILETIME-ish? 100ns since 1601 - too big for i32
    # minutes since epoch?
    try:
        dt = datetime.fromtimestamp(time_field * 60, tz=timezone.utc)
        lines.append(f"    interp minutes*60_utc -> {dt.isoformat()}")
    except Exception:
        pass
    # hours
    try:
        dt = datetime.fromtimestamp(time_field * 3600, tz=timezone.utc)
        lines.append(f"    interp hours*3600_utc -> {dt.isoformat()}")
    except Exception:
        pass
    # days *something
    for mult, name in ((86400, "days_as_unix_seconds"),):
        try:
            dt = datetime.fromtimestamp(time_field * mult, tz=timezone.utc)
            lines.append(f"    interp {name} -> {dt.isoformat()}")
        except Exception:
            pass


def decode_entry(b, o, tag, lines):
    mail_id, o = read_i64(b, o)
    time_field, o = read_i32(b, o)
    frm, o = read_lp_str(b, o)
    subject, o = read_lp_str(b, o)
    credits, o = read_i32(b, o)
    cod, o = read_i32(b, o)
    flags, o = read_i32(b, o)
    summary = b[o]
    o += 1
    lines.append(
        f"  ENTRY[{tag}] id=0x{mail_id & 0xffffffffffffffff:016x} from={frm!r} subject={subject!r} "
        f"credits={credits} cod={cod} flags={flags} summary={summary}"
    )
    interpret_time(time_field, lines)
    if summary == 0:
        ext64, o = read_i32(b, o)
        acg_lo, o = read_i32(b, o)
        acg_hi, o = read_i32(b, o)
        acg_lv, o = read_i32(b, o)
        pad, o = read_i32(b, o)
        ext74, o = read_i32(b, o)
        body, o = read_lp_str(b, o)
        lines.append(
            f"    detail ext64={ext64} acg={acg_lo}/{acg_hi}/{acg_lv} pad={pad} ext74={ext74} body={body!r}"
        )
    return o


def decode_mail_at(payload: bytes, off: int, direction: str, hint: str, lines: list):
    b = payload[off:]
    if len(b) < 15:
        return
    n3type, o = read_u32(b, 0)
    id_type, o = read_u32(b, o)
    id_inst, o = read_u32(b, o)
    unknown = b[o]
    o += 1
    action, o = read_i16(b, o)
    lines.append(
        f"\n== {direction} {hint} action={action} identity={id_type:x}/{id_inst:x} unk={unknown} len={len(b)}"
    )
    if action == 0:
        enc, o = read_i32(b, o)
        count = (enc // X3F1) - 1
        lines.append(f"  MailboxList enc=0x{enc:08x} count={count}")
        for i in range(max(0, min(count, 30))):
            if o >= len(b):
                lines.append("  TRUNCATED")
                break
            o = decode_entry(b, o, f"list{i}", lines)
    elif action == 2:
        o = decode_entry(b, o, "detail", lines)
    elif action == 1 and o + 8 <= len(b):
        req, o = read_i64(b, o)
        lines.append(f"  OpenOrRequest id={req}")
    elif action in (3, 5, 7) and o + 8 <= len(b):
        req, o = read_i64(b, o)
        lines.append(f"  id={req}")
    elif action == 6:
        recip, o = read_lp_str(b, o)
        subj, o = read_lp_str(b, o)
        body, o = read_lp_str(b, o)
        i1, o = read_i32(b, o)
        i2, o = read_i32(b, o)
        cr, o = read_i32(b, o)
        express = b[o] if o < len(b) else None
        lines.append(f"  Send to={recip!r} subj={subj!r} item={i1}/{i2} credits={cr} express={express}")
    elif action == 8:
        echo, o = read_i16(b, o)
        u1, o = read_i32(b, o)
        mid, o = read_i32(b, o)
        u2, o = read_i32(b, o)
        lines.append(f"  SendAccepted echo={echo} mailId={mid}")


lines: list[str] = []
# headers
with (CAP / "raw-packets.csv").open(encoding="utf-8-sig", errors="replace") as f:
    header = f.readline().rstrip()
    lines.append("CSV header: " + header)

mail_hits = 0
with (CAP / "raw-packets.csv").open(newline="", encoding="utf-8-sig", errors="replace") as f:
    reader = csv.DictReader(f)
    for idx, row in enumerate(reader):
        # gather possible hex columns
        candidates = []
        for k, v in row.items():
            if not v:
                continue
            if "hex" in k.lower() or k.lower() in ("payload", "data", "body"):
                candidates.append(v)
            elif len(v) > 60 and re.fullmatch(r"[0-9A-Fa-f\s]+", v.strip()):
                candidates.append(v)
        direction = row.get("Direction") or row.get("direction") or "?"
        hint = row.get("Timestamp") or row.get("MonoTime") or str(idx)
        msgtype = row.get("MessageType") or row.get("N3MessageType") or row.get("Type") or ""
        for blob in candidates:
            data = parse_hex_blob(blob)
            offs = find_mail_offsets(data)
            if not offs and "Mail" in msgtype:
                lines.append(f"row {idx} labeled {msgtype} but no 333B2867 in hex dirs={direction}")
            for off in offs:
                mail_hits += 1
                decode_mail_at(data, off, direction, f"{hint} type={msgtype}", lines)

lines.append(f"\nraw-packets mail frames decoded: {mail_hits}")

# packets.hex.log brute
hex_hits = 0
hexlog = CAP / "packets.hex.log"
if hexlog.exists():
    text = hexlog.read_text(encoding="utf-8", errors="replace")
    # find occurrences of type dword little endian hex bytes
    # 67 28 3B 33
    for m in re.finditer(r"(?i)67283b33", text.replace(" ", "").replace("\n", "")):
        hex_hits += 1
    lines.append(f"packets.hex.log spaced-stripped 67283b33 count={hex_hits}")

    # also unpack continuous hex lines containing the marker
    for i, line in enumerate(text.splitlines(), 1):
        compact = re.sub(r"[^0-9A-Fa-f]", "", line)
        if "67283b33" not in compact.lower():
            continue
        data = bytes.fromhex(compact if len(compact) % 2 == 0 else compact[:-1])
        for off in find_mail_offsets(data):
            decode_mail_at(data, off, "hexlog", f"L{i}", lines)

# system messages
sysp = CAP / "system-messages.log"
if sysp.exists():
    for i, line in enumerate(sysp.open(encoding="utf-8", errors="replace"), 1):
        if "Mail" in line or "333B" in line:
            lines.append(f"SYS L{i}: {line.rstrip()[:400]}")

# events Mentions of Mail message type beyond GenericCmd
for i, line in enumerate((CAP / "events.log").open(encoding="utf-8", errors="replace"), 1):
    if "MailMessage" in line or "N3MessageType=Mail" in line or "0x333B2867" in line or "333B2867" in line:
        lines.append(f"EVT L{i}: {line.rstrip()[:400]}")

today = date.today()
days = (today - date(1970, 1, 1)).days
lines.append(f"\nlocal today {today} days_since_1970={days}")
lines.append(f"capture wall clock from events around 2026-07-15T10:00Z")

OUT.write_text("\n".join(lines), encoding="utf-8")
print("wrote", OUT, "lines", len(lines), "mail_hits", mail_hits)
