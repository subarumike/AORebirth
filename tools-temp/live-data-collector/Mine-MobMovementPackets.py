import csv
import math
import struct
import sys
from collections import Counter, defaultdict
from pathlib import Path


def be_i32(buf, off):
    return struct.unpack_from(">i", buf, off)[0]


def be_u32(buf, off):
    return struct.unpack_from(">I", buf, off)[0]


def be_f32(buf, off):
    return struct.unpack_from(">f", buf, off)[0]


def identity(type_id, instance):
    return f"{type_id}:{instance:08X}"


def frame_body(hex_text):
    data = bytes.fromhex("".join(hex_text.split()))
    if len(data) < 28:
        return data, b""
    header = {
        "message_id": int.from_bytes(data[0:2], "big"),
        "packet_type": int.from_bytes(data[2:4], "big"),
        "size": int.from_bytes(data[6:8], "big"),
        "sender": be_u32(data, 8),
        "receiver": be_u32(data, 12),
        "n3": be_u32(data, 16),
        "identity_type": be_u32(data, 20),
        "identity_instance": be_u32(data, 24),
    }
    return header, data[28:]


def decode_follow(row):
    header, body = frame_body(row["frame_hex"])
    result = {
        "packet": int(row["packet_number"]),
        "time": float(row["relative_timestamp"]),
        "identity": identity(header["identity_type"], header["identity_instance"]),
        "size": int(row["size"]),
        "n3_unknown": body[0] if len(body) > 0 else None,
        "info_type": body[1] if len(body) > 1 else None,
    }

    if len(body) >= 4 and result["info_type"] == 1:
        result["kind"] = "coordinates"
        result["move_mode"] = body[2]
        result["coordinate_count"] = body[3]
        coords = []
        off = 4
        for _ in range(result["coordinate_count"]):
            if off + 12 > len(body):
                break
            coords.append((be_f32(body, off), be_f32(body, off + 4), be_f32(body, off + 8)))
            off += 12
        result["coords"] = coords
        result["remaining_bytes"] = len(body) - off
        if len(coords) >= 2:
            sx, sy, sz = coords[0]
            ex, ey, ez = coords[-1]
            result["delta_2d"] = math.dist((sx, sz), (ex, ez))
    elif len(body) >= 3 and result["info_type"] == 2:
        result["kind"] = "type2"
        result["move_type"] = body[2]
        result["raw_after_header"] = body[3:].hex()
        # Private captures show type2 packets as a target/stop style payload,
        # not the old AOtomation FollowTargetInfo layout. Decode the stable
        # coordinate tail shape when present without claiming names for the
        # unknown padding fields.
        if len(body) >= 40:
            result["zero_pad_hex"] = body[3:15].hex()
            result["coord_a"] = (be_f32(body, 15), be_f32(body, 19), be_f32(body, 23))
            result["flag"] = body[27]
            result["coord_b"] = (be_f32(body, 28), be_f32(body, 32), be_f32(body, 36))
    else:
        result["kind"] = "unknown"
        result["raw_body"] = body.hex()

    return result


def decode_char_move(row):
    header, body = frame_body(row["frame_hex"])
    result = {
        "packet": int(row["packet_number"]),
        "time": float(row["relative_timestamp"]),
        "identity": identity(header["identity_type"], header["identity_instance"]),
        "size": int(row["size"]),
        "n3_unknown": body[0] if len(body) > 0 else None,
        "move_type": body[1] if len(body) > 1 else None,
    }
    if len(body) >= 42:
        result["heading"] = tuple(be_f32(body, off) for off in (2, 6, 10, 14))
        result["coords"] = tuple(be_f32(body, off) for off in (18, 22, 26))
        result["unknown_tail"] = (be_i32(body, 30), be_i32(body, 34), be_i32(body, 38))
    return result


def decode_attack_info(row):
    header, body = frame_body(row["frame_hex"])
    result = {
        "packet": int(row["packet_number"]),
        "time": float(row["relative_timestamp"]),
        "identity": identity(header["identity_type"], header["identity_instance"]),
        "size": int(row["size"]),
        "n3_unknown": body[0] if len(body) > 0 else None,
    }
    if len(body) >= 33:
        result.update({
            "damage": be_i32(body, 1),
            "ammo_count": be_i32(body, 5),
            "weapon_slot": be_i32(body, 9),
            "target": identity(be_u32(body, 13), be_u32(body, 17)),
            "unknown4": be_i32(body, 21),
            "hit_type": be_i32(body, 25),
            "unknown6": be_i32(body, 29),
            "tail_hex": body[33:].hex(),
        })
    return result


def main():
    if len(sys.argv) != 2:
        print("usage: Mine-MobMovementPackets.py <capture_dir-or-s2c_frames.csv>", file=sys.stderr)
        return 2

    source = Path(sys.argv[1])
    csv_path = source if source.name == "s2c_frames.csv" else source / "s2c_frames.csv"
    rows = list(csv.DictReader(csv_path.open(newline="", encoding="utf-8-sig")))

    follows = [decode_follow(r) for r in rows if r["name"] == "FollowTarget"]
    moves = [decode_char_move(r) for r in rows if r["name"] == "CharDCMove"]
    attacks = [r for r in rows if r["name"] in {"Attack", "AttackInfo", "MissedAttackInfo", "StopFight", "CharacterAction"}]
    infos = [decode_attack_info(r) for r in rows if r["name"] == "AttackInfo"]

    print(f"# Mob Movement Packet Mining: {csv_path}")
    print()
    print(f"Total S2C frames: {len(rows)}")
    print(f"FollowTarget: {len(follows)}")
    print(f"CharDCMove: {len(moves)}")
    print(f"Combat/control frames: {len(attacks)}")
    print()

    print("## FollowTarget Shapes")
    for (size, kind, unknown, info_type, mode, count), n in sorted(Counter(
        (f["size"], f.get("kind"), f.get("n3_unknown"), f.get("info_type"), f.get("move_mode", f.get("move_type")), f.get("coordinate_count"))
        for f in follows
    ).items()):
        print(f"- count={n:4d} size={size:3d} kind={kind} n3_unknown={unknown} info_type={info_type} mode={mode} coord_count={count}")
    print()

    print("## FollowTarget Coordinate Samples")
    for f in [x for x in follows if x.get("kind") == "coordinates"][:20]:
        coords = f["coords"]
        start = coords[0] if coords else None
        end = coords[-1] if coords else None
        print(
            f"- t={f['time']:.3f} pkt={f['packet']} id={f['identity']} "
            f"size={f['size']} unk={f['n3_unknown']} mode={f['move_mode']} "
            f"count={f['coordinate_count']} start={fmt_vec(start)} end={fmt_vec(end)} "
            f"delta2d={f.get('delta_2d', 0):.3f} rem={f['remaining_bytes']}"
        )
    print()

    print("## FollowTarget Type2 Samples")
    for f in [x for x in follows if x.get("kind") == "type2"][:20]:
        print(
            f"- t={f['time']:.3f} pkt={f['packet']} id={f['identity']} "
            f"size={f['size']} unk={f['n3_unknown']} move_type={f['move_type']} "
            f"pad={f.get('zero_pad_hex')} flag={f.get('flag')} "
            f"coord_a={fmt_vec(f.get('coord_a'))} coord_b={fmt_vec(f.get('coord_b'))}"
        )
    print()

    print("## CharDCMove MoveType Counts")
    for (move_type, unknown), n in sorted(Counter((m.get("move_type"), m.get("n3_unknown")) for m in moves).items()):
        print(f"- count={n:4d} n3_unknown={unknown} move_type={move_type}")
    print()

    by_identity = defaultdict(list)
    for m in moves:
        by_identity[m["identity"]].append(m)
    print("## Most Active CharDCMove Identities")
    for ident, items in sorted(by_identity.items(), key=lambda kv: len(kv[1]), reverse=True)[:12]:
        times = [x["time"] for x in items]
        move_types = Counter(x.get("move_type") for x in items)
        print(f"- id={ident} frames={len(items)} t={min(times):.3f}-{max(times):.3f} move_types={dict(sorted(move_types.items()))}")
    print()

    print("## AttackInfo Field Samples")
    for info in infos[:30]:
        print(
            f"- t={info['time']:.3f} pkt={info['packet']} id={info['identity']} "
            f"target={info.get('target')} damage={info.get('damage')} "
            f"ammo={info.get('ammo_count')} slot={info.get('weapon_slot')} "
            f"u4={info.get('unknown4')} hit_type={info.get('hit_type')} u6={info.get('unknown6')} tail={info.get('tail_hex')}"
        )
    print()

    print("## Combat Windows With FollowTarget")
    combat_times = [float(r["relative_timestamp"]) for r in attacks if r["name"] in {"Attack", "AttackInfo", "MissedAttackInfo"}]
    windows = []
    for t in combat_times:
        if not windows or t - windows[-1][1] > 3:
            windows.append([t, t])
        else:
            windows[-1][1] = t
    for start, end in windows[:20]:
        nearby = [f for f in follows if start - 1 <= f["time"] <= end + 1]
        if not nearby:
            continue
        shape_counts = Counter((f["size"], f.get("kind"), f.get("n3_unknown"), f.get("info_type"), f.get("move_mode", f.get("move_type")), f.get("coordinate_count")) for f in nearby)
        print(f"- t={start:.3f}-{end:.3f} follow_packets={len(nearby)} shapes={dict(shape_counts)}")


def fmt_vec(vec):
    if vec is None:
        return "None"
    return f"({vec[0]:.3f},{vec[1]:.3f},{vec[2]:.3f})"


if __name__ == "__main__":
    raise SystemExit(main())
