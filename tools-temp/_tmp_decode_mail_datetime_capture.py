#!/usr/bin/env python3
"""Decode Mail (0x333B2867) packets from receive-datetime capture."""
from __future__ import annotations

import csv
import pathlib
import re
from datetime import date, datetime, timezone

CAP = pathlib.Path(
    r"C:\Users\nermi\source\repos\AORebirth\tools-temp\AOSharpLiveCapture"
    r"\bin\Debug\captures\20260715-Recive-mail-datetime-stamp"
)
MAIL = 0x333B2867
X3F1 = 0x03F1


def parse_hex_blob(s: str) -> bytes:
    s = re.sub(r"[^0-9A-Fa-f]", "", s or "")
    if len(s) % 2:
        s = s[:-1]
    return bytes.fromhex(s)


def find_mail_offsets(payload: bytes):
    # little-endian type dword
    needle = MAIL.to_bytes(4, "little")
    offs = []
    start = 0
    while True:
        i = payload.find(needle)
        if i < 0:
            break
        offs.append(i)
        payload = payload  # keep searching in original via start
        # advance
        # actually mutate carefully
        break
    # redo with loop over original
    data = payload
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


def read_u16(b, o):
    return int.from_bytes(b[o : o + 2], "little", signed=False), o + 2


def read_i32(b, o):
    return int.from_bytes(b[o : o + 4], "little", signed=True), o + 4


def read_u32(b, o):
    return int.from_bytes(b[o : o + 4], "little", signed=False), o + 4


def read_i64(b, o):
    return int.from_bytes(b[o : o + 8], "little", signed=True), o + 8


def read_lp_str(b, o):
    n, o = read_i16(b, o)
    if n < 0 or n > 2048 or o + n > len(b):
        return f"<bad len {n}>", o
    s = b[o : o + n].decode("latin1", errors="replace")
    return s, o + n


def decode_entry(b, o, tag):
    mail_id, o = read_i64(b, o)
    time_field, o = read_i32(b, o)
    frm, o = read_lp_str(b, o)
    subject, o = read_lp_str(b, o)
    credits, o = read_i32(b, o)
    cod, o = read_i32(b, o)
    flags, o = read_i32(b, o)
    summary = b[o]
    o += 1

    days_epoch = time_field
    try:
        d = date(1970, 1, 1).fromordinal(date(1970, 1, 1).toordinal() + days_epoch)
        days_as_date = d.isoformat()
    except Exception as e:
        days_as_date = f"err:{e}"

    unix_as_date = "?"
    try:
        unix_as_date = datetime.fromtimestamp(time_field, tz=timezone.utc).isoformat()
    except Exception:
        pass

    print(f"  ENTRY[{tag}] id={mail_id & 0xffffffffffffffff:016x} time={time_field} (0x{time_field & 0xffffffff:08x})")
    print(f"    as_days_since_1970 -> {days_as_date}")
    print(f"    as_unix_seconds_utc -> {unix_as_date}")
    print(f"    from={frm!r} subject={subject!r} credits={credits} cod={cod} flags={flags} summary={summary}")

    if summary == 0:
        ext64, o = read_i32(b, o)
        acg_lo, o = read_i32(b, o)
        acg_hi, o = read_i32(b, o)
        acg_lv, o = read_i32(b, o)
        pad, o = read_i32(b, o)
        ext74, o = read_i32(b, o)
        body, o = read_lp_str(b, o)
        print(f"    detail ext64={ext64} acg={acg_lo}/{acg_hi}/{acg_lv} pad={pad} ext74={ext74} body={body!r}")
    return o


def decode_mail_at(payload: bytes, off: int, direction: str, line_hint: str):
    # payload starts at type
    b = payload[off:]
    if len(b) < 15:
        return
    n3type, o = read_u32(b, 0)
    # identity: type+instance typically 8 bytes? N3 identity is usually 2*u32 or Identity 8
    # Stream: Int32 type, Identity (4+4), byte unknown, Int16 action
    id_type, o = read_u32(b, o)
    id_inst, o = read_u32(b, o)
    unknown = b[o]
    o += 1
    action, o = read_i16(b, o)
    print(f"\n== {direction} {line_hint} action={action} identity={id_type:x}/{id_inst:x} unk={unknown} paylen={len(b)}")

    if action == 0:
        enc, o = read_i32(b, o)
        count = (enc // X3F1) - 1
        print(f"  MailboxList enc=0x{enc:08x} count={count}")
        for i in range(max(0, count)):
            o = decode_entry(b, o, f"list{i}")
    elif action == 2:
        o = decode_entry(b, o, "detail")
    elif action == 1:
        if o + 8 <= len(b):
            req, o = read_i64(b, o)
            print(f"  OpenOrRequest id={req}")
    elif action in (3, 5, 7):
        if o + 8 <= len(b):
            req, o = read_i64(b, o)
            print(f"  action id={req}")
    elif action == 6:
        recip, o = read_lp_str(b, o)
        subj, o = read_lp_str(b, o)
        body, o = read_lp_str(b, o)
        i1, o = read_i32(b, o)
        i2, o = read_i32(b, o)
        cr, o = read_i32(b, o)
        express = b[o] if o < len(b) else None
        print(f"  Send to={recip!r} subj={subj!r} body={body!r} item={i1}/{i2} credits={cr} express={express}")
    elif action == 8:
        echo, o = read_i16(b, o)
        u1, o = read_i32(b, o)
        mid, o = read_i32(b, o)
        u2, o = read_i32(b, o)
        print(f"  SendAccepted echo={echo} mailId={mid} u1={u1} u2={u2}")


def scan_csv(path: pathlib.Path):
    if not path.exists():
        print("missing", path)
        return
    with path.open(newline="", encoding="utf-8", errors="replace") as f:
        reader = csv.DictReader(f)
        for row in reader:
            blob = None
            for key in ("PayloadHex", "Hex", "payload", "Payload", "DataHex", "data"):
                if key in row and row[key]:
                    blob = row[key]
                    break
            if blob is None:
                # try any long hex-ish column
                for k, v in row.items():
                    if v and len(v) > 40 and re.fullmatch(r"[0-9A-Fa-f\\s]+", v.replace(" ", "")):
                        blob = v
                        break
            if not blob:
                continue
            data = parse_hex_blob(blob)
            if MAIL.to_bytes(4, "little") not in data and MAIL.to_bytes(4, "big") not in data:
                continue
            direction = row.get("Direction") or row.get("Dir") or row.get("direction") or "?"
            hint = row.get("Timestamp") or row.get("Time") or row.get("Seq") or ""
            for off in find_mail_offsets(data):
                decode_mail_at(data, off, direction, str(hint))


def scan_hex_log(path: pathlib.Path):
    if not path.exists():
        return
    # lines may contain hex dumps
    for i, line in enumerate(path.open(encoding="utf-8", errors="replace"), 1):
        if "333B2867" not in line.upper() and "67283B33" not in line.upper():
            # also raw hex without markers
            if "mail" not in line.lower():
                # still scan if long hex
                if len(line) < 80:
                    continue
        hx = re.findall(r"(?:[0-9A-Fa-f]{2}[ :]?){20,}", line)
        for chunk in hx:
            data = parse_hex_blob(chunk)
            if MAIL.to_bytes(4, "little") not in data:
                continue
            for off in find_mail_offsets(data):
                decode_mail_at(data, off, "hexlog", f"L{i}")


def scan_events(path: pathlib.Path):
    if not path.exists():
        return
    for i, line in enumerate(path.open(encoding="utf-8", errors="replace"), 1):
        if "Mail" in line or "333B2867" in line or "mail" in line.lower():
            print(f"EVENT L{i}: {line.rstrip()[:300]}")


print("=== events with Mail ===")
scan_events(CAP / "events.log")
print("\n=== capture_info ===")
print((CAP / "capture_info.json").read_text(encoding="utf-8", errors="replace")[:800])
print("\n=== raw-packets.csv headers ===")
with (CAP / "raw-packets.csv").open(encoding="utf-8", errors="replace") as f:
    print(f.readline().rstrip())
print("\n=== Mail from raw-packets.csv ===")
scan_csv(CAP / "raw-packets.csv")
print("\n=== Mail from packets.hex.log ===")
scan_hex_log(CAP / "packets.hex.log")

# compare our encoding for today
today = date.today()
days = (today - date(1970, 1, 1)).days
print(f"\n=== local today {today} as days_since_1970 = {days} ===")
