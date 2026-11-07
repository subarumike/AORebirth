"""Audit unresolved RK dyna observations against preserved packets and loot profiles."""

import argparse
from collections import Counter, defaultdict
from itertools import combinations
import json
from pathlib import Path
import re
import struct

from reconcile_rk_dyna_loot import json_cell, key, provenance, quality_window, read_csv, read_json, write_csv
from reconstruct_rk_dyna_loot import ordered_sources


INVENTORY_UPDATE = 0x4E536976
CORPSE_FULL_UPDATE = 0x4F474E05
GENERIC_CMD = 0x52526858
CORPSE_TYPE = 0xC76A
TRANSFER_TYPES = {525164414, 1196653092, 1416181567}
PACKET_LOG = re.compile(r"\S+ (IN|OUT) #(\d+) len=(\d+) n3=\d+ hex=([0-9A-Fa-f]+)")
REPORT_KEY = ("Session", "CapturedUtc", "InventorySequence", "CorpseIdentity", "NpcName")


def decode_inventory(packet):
    # N3 envelope, InventoryUpdateMessage members and InventoryEntry members.
    if len(packet) < 57 or struct.unpack_from(">I", packet, 16)[0] != INVENTORY_UPDATE:
        raise ValueError("Not a complete InventoryUpdate")
    capacity, _, encoded = struct.unpack_from(">III", packet, 29)
    count, remainder = divmod(encoded, 0x3F1)
    if remainder or count < 1 or len(packet) != 57 + 32 * (count - 1):
        raise ValueError("Inventory array count disagrees with packet length")
    entries = []
    for index in range(count - 1):
        slot, _, units, _, _, low, high, quality, _ = struct.unpack_from(">IHHIIIIII", packet, 41 + 32 * index)
        entries.append((slot, low, high, quality, units))
    bag_type, bag_id, _, _ = struct.unpack_from(">IIII", packet, 41 + 32 * (count - 1))
    return capacity, (bag_type, bag_id), entries


def audit_packet_window(raw, hex_rows, capture_info, boss, items):
    in_seq = int(boss["InventorySequence"])
    corpse_seq = int(boss["CorpseFullUpdateSequence"])
    identity = (CORPSE_TYPE, int(boss["CorpseIdentity"].split(":")[1], 16))
    typed_bytes = struct.pack(">II", *identity)
    indexes = {(row["Direction"], int(row["Sequence"])): row for row in raw}
    corpse = indexes[("IN", corpse_seq)]
    snapshot = indexes[("IN", in_seq)]
    if int(corpse["N3TypeValue"]) != CORPSE_FULL_UPDATE or bytes.fromhex(corpse["RawHex"])[20:28] != typed_bytes:
        raise ValueError("CorpseFullUpdate identity does not match the observed inventory")
    packet = bytes.fromhex(snapshot["RawHex"])
    capacity, observed_identity, entries = decode_inventory(packet)
    projected = [tuple(int(row[field]) for field in ("Slot", "LowId", "HighId", "Ql", "Count")) for row in items]
    if observed_identity != identity or entries != projected:
        raise ValueError("Raw inventory identity, slot order or item tuple disagrees with the observation")

    start, end = (int(row["GlobalOrdinal"]) for row in (corpse, snapshot))
    window = [row for row in raw if start <= int(row["GlobalOrdinal"]) <= end]
    uses, earlier_inventory, targeted_transfers = [], [], []
    for row in window:
        direction, sequence = row["Direction"], int(row["Sequence"])
        raw_bytes = bytes.fromhex(row["RawHex"])
        n3_type = int(row["N3TypeValue"]) if row["N3TypeValue"] else 0
        if n3_type == GENERIC_CMD and direction == "OUT" and raw_bytes[-8:] == typed_bytes and struct.unpack_from(">I", raw_bytes, 37)[0] == 3:
            uses.append(sequence)
        if n3_type == INVENTORY_UPDATE and direction == "IN" and sequence < in_seq and raw_bytes[-16:-8] == typed_bytes:
            earlier_inventory.append(sequence)
        if n3_type in TRANSFER_TYPES and typed_bytes in raw_bytes[16:]:
            targeted_transfers.append((direction, sequence))
    parity = all(hex_rows.get((row["Direction"], int(row["Sequence"]))) == (int(row["PacketLength"]), row["RawHex"].upper())
                 and len(bytes.fromhex(row["RawHex"])) == int(row["PacketLength"])
                 and row["PreservationStatus"] == "raw_complete" for row in window)
    contiguous = [int(row["GlobalOrdinal"]) for row in window] == list(range(start, end + 1))
    complete = (parity and contiguous and capture_info.get("finalized") is True and capture_info.get("rawMissingPackets") == 0
                and capture_info.get("rawPacketWriteErrors") == 0 and len(uses) == 1 and not earlier_inventory and not targeted_transfers)
    return {"CorpseFullUpdateSequence": corpse_seq, "InventorySequence": in_seq, "GenericCmdUseSequences": uses,
            "EarlierMatchingInventories": earlier_inventory, "PriorTypedCorpseTransferPackets": targeted_transfers,
            "ContiguousRawPacketWindow": contiguous, "RawSinksAgree": parity, "ArrayEntries": len(entries),
            "InventoryCapacity": capacity, "InventoryPacketBytes": len(packet), "CompleteFirstSnapshotInCapture": complete}


def pair(row):
    return int(row["LowId"]), int(row["HighId"])


def fingerprint(catalog):
    endpoints = json.loads(catalog["Endpoints"])
    low = endpoints[0]
    role = ("NANO_UPLOAD" if low["nanoUploadIds"] else "DISC_DISPLAY_CLASS" if (low["name"] or "").startswith("Instruction Disc (")
            else "OTHER")
    return low["itemClassName"], tuple(sorted(low["attack"])), role


def compatible_draw(row, catalog, source, variance):
    endpoints = json.loads(catalog["Endpoints"])
    low, high = (endpoint["quality"] for endpoint in endpoints)
    quality = int(row["Ql"])
    if low == high:
        return None if quality == low else set()
    minimum, maximum = quality_window(source.center, variance)
    return {wanted for wanted in range(max(minimum, low), maximum + 1) if min(wanted, high) == quality}


def enumerate_alignments(profile, items, anchors, catalogs, profiles, variance):
    output = []
    for order_name, order in (("LOOTGIVER_FIRST", profile), ("MODIFIER_FIRST", tuple(sorted(profile, key=lambda source: -source.role)))):
        for sources in combinations(order, len(items)):
            common = {}
            supports = []
            for source, row in zip(sources, items, strict=True):
                if anchors[int(row["Slot"])] and source.root not in anchors[int(row["Slot"])]:
                    break
                catalog = catalogs[pair(row)]
                desired = compatible_draw(row, catalog, source, variance)
                if desired is not None:
                    common.setdefault(source.center, desired.copy()).intersection_update(desired)
                    if not common[source.center]:
                        break
                supports.append(profiles[source.root][fingerprint(catalog)])
            else:
                output.append({"Order": order_name, "SlotSources": sources, "OmittedSources": tuple(s for s in order if s not in sources),
                               "CommonDesiredQl": {center: sorted(draws) for center, draws in sorted(common.items())},
                               "FingerprintObservationCounts": supports})
    return output


def source_record(source):
    return {"hash": source.root, "position": source.position, "center": source.center, "equipment": source.equipment}


def control_family_gap(records, catalog, source, quality):
    children = (records.get(source.root) or {}).get("Children", [])
    endpoints = json.loads(catalog["Endpoints"])
    details = {"equipmentFunction": source_record(source), "danglingChildren": [child for child in children if child not in records]}
    if not all(endpoint and endpoint["rdbPresent"] and endpoint["rdbReadComplete"] for endpoint in endpoints):
        return "ORDERED_ROOT_SUPPORTED_CHILD_UNRESOLVED", details
    low, high = endpoints
    known = [child for child in children if low["id"] in (records.get(child) or {}).get("Templates", [])]
    if (len(known) == 1 and low["quality"] <= quality <= high["quality"] and low["id"] != high["id"]
            and high["id"] not in records[known[0]]["Templates"]):
        child = known[0]
        details.update({"canonicalLeaf": child, "canonicalTemplates": records[child]["Templates"],
                        "wireTemplateQualities": [low["quality"], high["quality"]],
                        "otherCanonicalParents": sorted(root for root, entry in records.items()
                                                        if root != source.root and child in (entry.get("Children") or []))})
        return "PROVEN_EXISTING_FAMILY_ENDPOINT_GAP", details
    return "ORDERED_ROOT_SUPPORTED_CHILD_UNRESOLVED", details


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reconciliation", type=Path, required=True)
    parser.add_argument("--membership-evidence", type=Path, required=True)
    parser.add_argument("--raw-capture-root", type=Path, required=True)
    parser.add_argument("--item-templates", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args(argv)
    base = args.reconciliation
    inputs = [base / name for name in ("bosses.csv", "items.csv", "observed-item-catalog.csv", "summary.json")]
    inputs += [args.membership_evidence, args.item_templates]
    bosses = {key(row): row for row in read_csv(inputs[0])}
    grouped = defaultdict(list)
    for row in read_csv(inputs[1]):
        grouped[key(row)].append(row)
    catalogs = {(int(row["WireLowId"]), int(row["WireHighId"])): row for row in read_csv(inputs[2])}
    variance = read_json(inputs[3])["lootQualityVariancePercent"]
    records = read_json(args.item_templates)
    support_by_pair, support_by_family = defaultdict(set), defaultdict(set)
    root_profiles = defaultdict(Counter)
    for row in read_csv(args.membership_evidence):
        if row["Basis"] != "FULL_ORDERED_PROFILE" or not row["AssignedHash"]:
            continue
        root = row["AssignedHash"]
        catalog = catalogs[pair(row)]
        support_by_pair[pair(row)].add(root)
        support_by_family[catalog["FamilyKey"]].add(root)
        root_profiles[root][fingerprint(catalog)] += 1

    slots, alignment_rows, packets = [], [], []
    raw_by_session = {}
    for identity, items in sorted(grouped.items()):
        unresolved = [row for row in items if row["Status"] == "UNEXPLAINED"]
        if not unresolved:
            continue
        boss = bosses[identity]
        common = dict(zip(REPORT_KEY[:-1], identity))
        common["NpcName"] = boss["NpcName"]
        session = boss["Session"]
        if session not in raw_by_session:
            directory = args.raw_capture_root / session
            paths = [directory / name for name in ("raw-packets.csv", "packets.hex.log", "capture_info.json")]
            inputs.extend(paths)
            hex_rows = {}
            for match in PACKET_LOG.finditer(paths[1].read_text(encoding="utf-8-sig")):
                direction, seq, length, raw_hex = match.groups()
                hex_rows[(direction, int(seq))] = int(length), raw_hex.upper()
            raw_by_session[session] = read_csv(paths[0]), hex_rows, read_json(paths[2])
        raw, hex_rows, capture_info = raw_by_session[session]
        packet = audit_packet_window(raw, hex_rows, capture_info, boss, items)
        packet = {k: json_cell(v) if isinstance(v, (list, dict)) else v for k, v in packet.items()}
        packets.append({**common, **packet})
        profile = ordered_sources(boss)
        if not profile or len(profile) < len(items):
            raise ValueError(f"No compatible equipment profile for {identity}")
        if len(profile) == len(items):
            for row in unresolved:
                source = profile[int(row["Slot"])]
                low, high = pair(row)
                status, details = control_family_gap(records, catalogs[pair(row)], source, int(row["Ql"]))
                slots.append({**common, "Slot": row["Slot"], "LowId": low, "HighId": high, "Ql": row["Ql"], "Status": status,
                              "CandidateHashes": json_cell([source.root]), "PreferredHash": source.root, "Details": json_cell(details)})
            continue

        anchors = {}
        for row in items:
            known = support_by_pair[pair(row)] or support_by_family[catalogs[pair(row)]["FamilyKey"]]
            eligible = {source["hash"] for source in json.loads(row["RuntimeEligibleSources"]) if source["mechanism"] == "equipment"}
            anchors[int(row["Slot"])] = known or eligible
        alignments = enumerate_alignments(profile, items, anchors, catalogs, root_profiles, variance)
        if not alignments:
            raise ValueError(f"No packet, QL and existing-family compatible alignment for {identity}")
        best = max(sum(count > 0 for count in row["FingerprintObservationCounts"]) for row in alignments)
        preferred = [row for row in alignments if sum(count > 0 for count in row["FingerprintObservationCounts"]) == best]
        for row in alignments:
            alignment_rows.append({**common, "Order": row["Order"], "EquipmentAlignment": json_cell([source_record(s) for s in row["SlotSources"]]),
                                   "OmittedFunctions": json_cell([source_record(s) for s in row["OmittedSources"]]),
                                   "CommonDesiredQl": json_cell(row["CommonDesiredQl"]),
                                   "FingerprintObservationCounts": json_cell(row["FingerprintObservationCounts"]),
                                   "EmpiricallyPreferred": row in preferred})
        for row in unresolved:
            index = int(row["Slot"])
            candidates = sorted({a["SlotSources"][index].root for a in alignments})
            preferred_roots = sorted({a["SlotSources"][index].root for a in preferred})
            low, high = pair(row)
            slots.append({**common, "Slot": index, "LowId": low, "HighId": high, "Ql": row["Ql"],
                          "Status": "AMBIGUOUS_ALIGNMENT", "CandidateHashes": json_cell(candidates),
                          "PreferredHash": preferred_roots[0] if len(preferred_roots) == 1 else "", "Details": json_cell(preferred_roots)})

    output = args.output_dir.resolve()
    if output.is_relative_to(args.item_templates.resolve().parent) or output.is_relative_to(args.raw_capture_root.resolve()):
        raise ValueError("Report destination must be outside GameData and original captures")
    output.mkdir(parents=True, exist_ok=True)
    reports = (
        ("residual-slots.csv", slots, ("Slot", "LowId", "HighId", "Ql", "Status", "CandidateHashes", "PreferredHash", "Details")),
        ("short-inventory-alignments.csv", alignment_rows, ("Order", "EquipmentAlignment", "OmittedFunctions", "CommonDesiredQl",
                                                            "FingerprintObservationCounts", "EmpiricallyPreferred")),
        ("packet-completeness.csv", packets, ("CorpseFullUpdateSequence", "GenericCmdUseSequences", "EarlierMatchingInventories",
                                               "PriorTypedCorpseTransferPackets", "ContiguousRawPacketWindow", "RawSinksAgree",
                                               "ArrayEntries", "InventoryCapacity", "InventoryPacketBytes", "CompleteFirstSnapshotInCapture")),
    )
    for name, rows, fields in reports:
        destination = output / name
        if destination.resolve() != destination:
            raise ValueError(f"Report output redirects: {destination}")
        write_csv(destination, REPORT_KEY + fields, rows)
    summary = {"inputs": [provenance(path) for path in sorted(set(inputs))], "residualStatuses": dict(Counter(row["Status"] for row in slots)),
               "auditedInventories": len(packets), "admittedAlignments": len(alignment_rows),
               "allInventoriesCompleteInCapture": all(row["CompleteFirstSnapshotInCapture"] for row in packets)}
    summary_path = output / "summary.json"
    if summary_path.resolve() != summary_path:
        raise ValueError(f"Report output redirects: {summary_path}")
    summary_path.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Audited {len(slots)} unresolved slots in {len(packets)} preserved inventories")


if __name__ == "__main__":
    main()
