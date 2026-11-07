"""Evidence-only projections for RK outdoor dynaboss captures."""

from collections import Counter, defaultdict
import csv
import hashlib
import json
from pathlib import Path
import os
import re
import shutil
import subprocess
import sys


ITEM_CLASSES = ("Misc", "Weapon", "Armor", "Implant", "Npc", "Spirit", "Utility", "Tower")
PROFESSIONS = (
    "None", "Soldier", "MartialArtist", "Engineer", "Fixer", "Agent", "Adventurer", "Trader",
    "Bureaucrat", "Enforcer", "Doctor", "Nanotechnician", "Metaphysicist", "Monster", "Keeper", "Shade",
)
RK_DYNA_WIRE_BIT = 0x01000000
BOSS_COHORT = "RK_DYNA_SIGNATURE"
NANO_ID = re.compile(r"\(53019:([0-9a-fA-F]+)\)")


def capture_sessions(path, marker):
    if (path / marker).is_file():
        return [path]
    nested = path / "Recordings" / "Live" / "captures"
    if nested.is_dir():
        path = nested
    sessions = [folder for folder in sorted(path.iterdir()) if folder.is_dir() and (folder / marker).is_file()]
    if not sessions:
        raise ValueError(f"No capture sessions with {marker}: {path}")
    return sessions


def projection_path(folder, name):
    path = folder / f"{name}.csv"
    pending = folder / f"{name}.pending.csv"
    return pending if pending.is_file() else path


def projected_sessions(path):
    marker = "corpse-loot-observations.csv"
    if (path / f"corpse-loot-observations.pending.csv").is_file():
        return [path]
    return capture_sessions(path, marker)


def ao_identity(value):
    if not value:
        return ""
    kind, instance = value.strip("()").split(":")
    return f"{kind}:{int(instance, 16):08X}"


def choose_prior(rows, cutoff):
    return max((row for row in rows if int(row["Sequence"]) <= cutoff),
               key=lambda row: int(row["Sequence"]), default=None)


def project_session(folder, match_auras):
    """Join inbound inventory, corpse and NPC identities within one recording."""
    paths = [projection_path(folder, name) for name in ("corpse-loot-observations", "corpse-full-updates", "scfu-appearance")]
    paths.append(folder / "inventory-updates.csv")
    capture_info = folder / "capture_info.json"
    session_playfield = ""
    if capture_info.is_file():
        paths.append(capture_info)
        session_playfield = str(json.loads(capture_info.read_text(encoding="utf-8-sig")).get("sessionPlayfieldId") or "")
    corpse_packets = defaultdict(list)
    for row in iter_csv(paths[1]):
        if row["Direction"] == "IN":
            corpse_packets[(ao_identity(row["CorpseIdentity"]), ao_identity(row["DeadNpcIdentity"]))].append(row)
    spawns = defaultdict(list)
    for row in iter_csv(paths[2]):
        if row["Direction"] == "IN" and row["DecodeStatus"] == "decoded_complete" and row["DecodeFullyConsumed"].lower() == "true":
            spawns[ao_identity(row["Identity"])].append(row)
    inventories = defaultdict(dict)
    for row in iter_csv(paths[3]):
        identity = ao_identity(row["InventoryIdentity"])
        if row["Direction"] != "IN" or not identity.startswith("Corpse:") or not row["LowId"]:
            continue
        packet = (int(row["Sequence"]), identity)
        slot = int(row["Slot"])
        item = tuple(row[field] for field in ("LowId", "HighId", "Quality", "Count"))
        if inventories[packet].setdefault(slot, item) != item:
            raise ValueError(f"Conflicting inventory slot {folder.name}, packet {packet}, slot {slot}")

    seen = {}
    corpses, items = [], []
    for row in iter_csv(paths[0]):
        if row["Direction"] != "IN" or row["InitialSnapshot"].lower() != "true":
            continue
        seq = int(row["Sequence"])
        corpse_id = ao_identity(row["CorpseIdentity"])
        npc_id = ao_identity(row["DeadNpcIdentity"])
        duplicate_key = (seq, corpse_id)
        if duplicate_key in seen:
            if seen[duplicate_key] != row:
                raise ValueError(f"Conflicting first inventory projection: {folder.name}, {duplicate_key}")
            continue
        seen[duplicate_key] = row
        corpse = choose_prior(corpse_packets[(corpse_id, npc_id)], seq) if npc_id else None
        spawn = choose_prior(spawns[npc_id], int(corpse["Sequence"])) if corpse else None
        flags = int(spawn["FlagsNumeric"]) if spawn else 0
        auras = {int(value, 16) for value in NANO_ID.findall(spawn["ActiveNanos"])} if spawn else set()
        modifiers, _ = match_auras(auras)
        if corpse is None:
            cohort = "UNLINKED"
        elif spawn is None:
            cohort = "NO_PRIOR_SCFU"
        elif not flags & RK_DYNA_WIRE_BIT:
            cohort = "SIGNATURE_CLEAR"
        elif not modifiers:
            cohort = "UNVERIFIED_MODIFIER_AURA"
        else:
            cohort = BOSS_COHORT
        descriptors = row["Items"].split(";") if row["Items"] else []
        slots = sorted(inventories[duplicate_key].items())
        if [":".join(item) for _, item in slots] != descriptors or len(slots) != int(row["ItemCount"]):
            raise ValueError(f"Wire slots disagree with first inventory projection: {folder.name}, {duplicate_key}")
        packet = {
            "Session": folder.name, "CapturedUtc": row["CapturedUtc"], "InventorySequence": row["Sequence"],
            "CorpseIdentity": corpse_id, "Playfield": corpse["PlayfieldId"] if corpse else row["PlayfieldId"] or session_playfield,
            "CorpseFullUpdateSequence": corpse["Sequence"] if corpse else "", "NpcIdentity": npc_id,
            "NpcName": spawn["Name"] if spawn else row["EnemyName"],
            "MonsterData": spawn["MonsterData"] if spawn else row["MonsterData"],
            "NpcLevel": spawn["Level"] if spawn else row["EnemyLevel"],
            "LiveScfuSequence": spawn["Sequence"] if spawn else "", "LiveScfuFlags": f"0x{flags:08X}" if spawn else "",
            "LiveScfuNanoIds": ";".join(map(str, sorted(auras))), "Cohort": cohort,
        }
        corpses.append({**packet, "ItemSlots": row["ItemCount"], "Descriptors": row["Items"]})
        if cohort == BOSS_COHORT:
            items.extend({**packet, **dict(zip(("Slot", "LowId", "HighId", "Ql", "Count"), (str(slot), *item)))}
                         for slot, item in slots)
    return corpses, items, paths


def run_decoder(command, repo, timeout):
    with subprocess.Popen(command, cwd=repo, stdout=subprocess.PIPE, stderr=subprocess.STDOUT) as proc:
        try:
            log, _ = proc.communicate(timeout=timeout)
        except subprocess.TimeoutExpired as error:
            if os.name == "nt":
                subprocess.run(["taskkill.exe", "/PID", str(proc.pid), "/T", "/F"], capture_output=True, timeout=10, check=False)
            if proc.poll() is None:
                proc.kill()
            log, _ = proc.communicate(timeout=10)
            raise RuntimeError(f"Offline decoder exceeded {timeout} seconds: {command[0]}\n{log.decode(errors='replace')}") from error
        if proc.returncode:
            raise RuntimeError(f"Offline decoder exited {proc.returncode}: {command[0]}\n{log.decode(errors='replace')}")


def stage_and_decode(path, output, timeout):
    """Replay the approved offline analyzers against private working copies."""
    repo = Path(__file__).resolve().parents[1]
    analyzer = repo / "tools-temp" / "AOSharpCaptureAnalyzer" / "bin" / "Debug" / "AOSharpCaptureAnalyzer.exe"
    lifecycle = repo / "tools-temp" / "AOSharpLiveCapture" / "decode_npc_lifecycle_capture.py"
    if not analyzer.is_file():
        raise ValueError("The documented AOSharpCaptureAnalyzer executable is missing. Build it through docs/ai/WORKFLOW.md first.")
    if (path / "packets.hex.log").is_file() and not (path / "raw-packets.csv").is_file():
        sessions = [path]
    else:
        sessions = capture_sessions(path, "raw-packets.csv")
    source_inputs = [analyzer, lifecycle]
    batch_id = hashlib.sha256(str(path.resolve()).encode()).hexdigest()[:16]
    projection_root = output / "capture-projections" / batch_id
    if not projection_root.resolve().is_relative_to(output):
        raise ValueError("Capture projection path leaves report directory")
    decoded = []
    for session in sessions:
        target = projection_root / session.name
        if target.resolve() != target:
            raise ValueError(f"Capture projection redirects outside its working path: {target}")
        target.mkdir(parents=True, exist_ok=True)
        for name in ("raw-packets.csv", "packets.hex.log", "capture_info.json"):
            source = session / name
            if source.is_file():
                destination = target / name
                if destination.resolve() != destination:
                    raise ValueError(f"Capture input destination redirects: {destination}")
                shutil.copyfile(source, destination)
                source_inputs.append(source)
        run_decoder([str(analyzer), str(target)], repo, timeout)
        run_decoder([str(analyzer), "--decode-loot", str(target)], repo, timeout)
        run_decoder([sys.executable, str(lifecycle), str(target)], repo, timeout)
        decoded.append(target)
    return decoded, source_inputs


def json_cell(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def iter_csv(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        yield from csv.DictReader(stream)


def item_details(directory, ids):
    receipt = json.loads((directory / "item-metadata-source.json").read_text(encoding="utf-8-sig"))
    metadata = directory / "item-metadata.jsonl"
    digest = hashlib.sha256()
    details = {}
    with metadata.open("rb") as stream:
        for line in stream:
            digest.update(line)
            # Export records are ordered by Id and begin with that JSON property.
            prefix = line.split(b",", 1)[0]
            if prefix.startswith(b'{"Id":') and int(prefix[6:]) in ids:
                row = json.loads(line)
                details[row["Id"]] = row
    if digest.hexdigest() != receipt["OutputSha256"].lower():
        raise ValueError("Item metadata changed while resolving observed template details")
    enum_path = directory / "metadata-enums.json"
    enums = json.loads(enum_path.read_text(encoding="utf-8-sig")) if enum_path.is_file() else {}
    return details, enums.get("ItemFlags", {})


def enum_name(names, value):
    return names[value] if isinstance(value, int) and 0 <= value < len(names) else None


def endpoint_metadata(template, flag_values):
    if template is None:
        return None
    stats = template.get("NamedStats") or {}
    item_class = template.get("LiveItemClass")
    if item_class is None:
        item_class = stats.get("ItemClass")
    flags = template.get("Flags", 0) & 0xFFFFFFFF
    return {
        "id": template["Id"], "name": template.get("Name"), "quality": template.get("Quality"),
        "itemType": template.get("ItemType"), "dynelType": template.get("DynelType"),
        "itemClass": item_class, "itemClassName": enum_name(ITEM_CLASSES, item_class),
        "attack": template.get("Attack", {}), "defend": template.get("Defend", {}),
        "flags": flags, "flagNames": sorted(name for name, value in flag_values.items()
                                           if value and flags & (value & 0xFFFFFFFF) == (value & 0xFFFFFFFF)),
        "can": stats.get("Can"), "slotMask": stats.get("Slot"), "nanoUploadIds": template.get("UploadNanoIds", []),
        "rdbPresent": template.get("RdbPresent"), "rdbReadComplete": template.get("ReadComplete"),
        "professionCriteria": [
            {**criterion, "ProfessionName": enum_name(PROFESSIONS, criterion["Value"])}
            for criterion in template.get("Criteria", []) if criterion.get("Stat") in (60, 368)
        ],
    }


def corpus_item_catalog(items, pools, details, flag_values):
    """Keep every exact wire pair and separate catalog resolution from drop attribution."""
    observations = defaultdict(list)
    for row in items:
        observations[(int(row["LowId"]), int(row["HighId"]))].append(row)

    catalog = []
    by_pair = {}
    discrepancies = []
    for pair, rows in sorted(observations.items()):
        low, high = pair
        qls = sorted({int(row["Ql"]) for row in rows})
        candidates = []
        exact_families = set()
        alternative_families = set()
        for root in sorted(set(rows[0]["ItemFamilies"].split(";")) - {""}):
            leaves = pools.pool(root)
            if len(leaves) != 1:
                continue
            leaf = leaves[0]
            exact = []
            alternatives = []
            for quality in qls:
                if not leaf.minimum <= quality <= leaf.maximum:
                    continue
                selected = leaf.ids_at(quality)
                if selected == pair:
                    exact.append(quality)
                elif low == high and selected[1] == high and (quality, high) in leaf.bands:
                    alternatives.append(quality)
            template_ids = tuple(template for _, template in leaf.bands)
            candidates.append({"hash": root, "templateIds": template_ids, "minQl": leaf.minimum,
                               "maxQl": leaf.maximum, "exactWireQls": exact, "upperEndpointQls": alternatives})
            if exact == qls:
                exact_families.add(template_ids)
            if alternatives == qls:
                alternative_families.add(template_ids)

        endpoints = [endpoint_metadata(details.get(item_id), flag_values) for item_id in pair]
        resolved = all(endpoints)
        valid_range = resolved and min(endpoints[0]["quality"], endpoints[1]["quality"]) <= qls[0] <= qls[-1] <= max(
            endpoints[0]["quality"], endpoints[1]["quality"])
        family_key = f"wire:{low}:{high}"
        if len(exact_families) == 1:
            family_key = "templates:" + ":".join(map(str, next(iter(exact_families))))
            status = "CANONICAL_FAMILY_EXACT_WIRE_PAIR"
        elif len(exact_families) > 1:
            status = "MULTIPLE_CANONICAL_FAMILIES"
        elif alternative_families:
            status = "CANONICAL_UPPER_ENDPOINT_ALTERNATIVE"
        elif valid_range and all(endpoint["rdbPresent"] and endpoint["rdbReadComplete"] for endpoint in endpoints):
            status = "RDB_WIRE_PAIR_ONLY"
        elif valid_range:
            status = "ITEMS_DAT_WIRE_PAIR_ONLY"
        elif resolved:
            status = "OBSERVED_QL_OUTSIDE_ENDPOINT_RANGE"
        else:
            status = "ENDPOINT_MISSING_FROM_ITEMS_DAT"

        first = min(rows, key=lambda row: (row["CapturedUtc"], row["Session"], int(row["InventorySequence"]), int(row["Slot"])))
        entry = {"WireLowId": low, "WireHighId": high, "FamilyKey": family_key,
                 "FamilyEvidence": status, "Endpoints": json_cell(endpoints),
                 "CanonicalFamilyCandidates": json_cell(candidates), "ObservedQls": json_cell(qls),
                 "ObservedSlots": len(rows), "ObservedUnits": sum(int(row["Count"]) for row in rows),
                 "FirstCapturedUtc": first["CapturedUtc"], "FirstSession": first["Session"]}
        by_pair[pair] = entry
        catalog.append(entry)
        for row in rows:
            known = json.loads(row["KnownMembershipOnly"])
            for alternative in known:
                status = alternative["qlStatus"]
                reconstructed = status == "RECONSTRUCTED_FAMILY_QL_EQUIVALENCE"
                if not reconstructed and status != "UPPER_ENDPOINT_WIRE_ALTERNATIVE":
                    continue
                quality = int(row["Ql"])
                leaves, _ = pools.inspect(alternative["hash"], reconstructed)
                leaf = next(leaf for leaf in leaves if tuple(alternative["path"]) == leaf.path)
                selector = leaf.ids_at(quality)
                discrepancies.append({"Session": row["Session"], "CapturedUtc": row["CapturedUtc"],
                                      "InventorySequence": row["InventorySequence"], "CorpseIdentity": row["CorpseIdentity"],
                                      "Slot": row["Slot"], "WireLowId": low, "WireHighId": high, "Ql": quality,
                                      "CandidatePool": alternative["hash"], "CandidatePath": json_cell(leaf.path),
                                      "SelectorLowId": selector[0], "SelectorHighId": selector[1],
                                      "EndpointTemplateVerified": high in details and details[high]["Quality"] == quality,
                                      "SelectionEvidence": status, "ScopedCenter": alternative.get("center", "") if reconstructed else "",
                                      "DesiredQls": json_cell(alternative.get("desiredQl", [])) if reconstructed else "",
                                      "Classification": ("RECONSTRUCTED_FAMILY_QL_EQUIVALENT_ITEM_TEMPLATE_WIRE_PAIR_DIFFERS" if reconstructed
                                                         else "UPPER_ENDPOINT_EQUIVALENT_ITEM_TEMPLATE_WIRE_CAUSE_UNVERIFIED")})
    return catalog, by_pair, discrepancies


def packet_key(row):
    return tuple(row[field] for field in ("Session", "CapturedUtc", "InventorySequence", "CorpseIdentity"))


def equipment_signature(sources):
    """Strip duplicate candidate NPC paths and level-specific centers, retaining function multiplicity."""
    return tuple(sorted({(tuple(source["equipmentIds"]), source["functionPosition"], source["hash"])
                         for source in sources if source["mechanism"] == "equipment"}))


def wilson_interval(events, population):
    rate = events / population
    z = 1.96
    denominator = 1 + z ** 2 / population
    center = (rate + z ** 2 / (2 * population)) / denominator
    radius = z * ((rate * (1 - rate) + z ** 2 / (4 * population)) / population) ** 0.5 / denominator
    return max(0, center - radius), min(1, center + radius)


def occurrence_evidence(bosses, items, item_catalog, pools):
    """Project observed support and denominators without fitting conditional drop weights."""
    source_rows = {packet_key(boss): json.loads(boss["LootSources"]) for boss in bosses}
    signatures = {key: equipment_signature(sources) for key, sources in source_rows.items()}
    signature_names = {signature: hashlib.sha256(json_cell(signature).encode()).hexdigest()[:12]
                       for signature in set(signatures.values())}
    population = Counter(signatures.values())
    populations_by_root = Counter()
    for sources in source_rows.values():
        populations_by_root.update({source["hash"] for source in sources})

    slots_by_signature = Counter()
    frequency_counts = defaultdict(Counter)
    occurrence_rows = []
    slot_rows_by_family = defaultdict(list)
    for row in items:
        identity = packet_key(row)
        pair = (int(row["LowId"]), int(row["HighId"]))
        quality = int(row["Ql"])
        catalog = item_catalog[pair]
        family = catalog["FamilyKey"]
        endpoints = json.loads(catalog["Endpoints"])
        maximum = max((endpoint["quality"] for endpoint in endpoints if endpoint), default=0)
        signature = signatures[identity]
        slots_by_signature[signature] += 1
        for group in (None, signature):
            frequency_counts[(family, group)][identity] += 1

        eligible = json.loads(row["RuntimeEligibleSources"])
        known = json.loads(row["KnownMembershipOnly"])
        equivalent = [source for source in known
                      if source["qlStatus"] == "UPPER_ENDPOINT_WIRE_ALTERNATIVE"]
        reconstructed_equivalent = [source for source in known
                                    if source["qlStatus"] == "RECONSTRUCTED_FAMILY_QL_EQUIVALENCE"]
        candidate_roots = defaultdict(set)
        for source in source_rows[identity]:
            root = source["hash"]
            reconstructed = source["mechanism"] == "equipment" and bool((pools.records.get(root) or {}).get("EquipmentLoot"))
            leaves, issues = pools.inspect(root, reconstructed)
            if leaves:
                if issues:
                    candidate_roots[root].add("PARTIAL_POOL_UNASSIGNED_CHILD")
                continue
            if not all(endpoints):
                candidate_roots[root].add("RDB_ENDPOINT_MISSING")
                continue
            if source["mechanism"] == "item-table":
                candidate_roots[root].add("UNRESOLVED_ORDINARY_ITEM_TABLE")
                continue
            lower, upper = source["window"]
            if pair[0] == pair[1]:
                candidate_roots[root].add("FIXED_OR_ENDPOINT_WIRE_ID_REQUIRES_POOL_EVIDENCE")
            elif quality <= upper and (quality >= lower or quality == maximum):
                candidate_roots[root].add("SCALING_QL_COMPATIBLE_IF_MEMBERSHIP_PROVEN")
            else:
                candidate_roots[root].add("SCALING_QL_CONFLICT_WITH_CURRENT_CENTER")
        unassigned = {root for root, classes in candidate_roots.items()
                      if classes & {"FIXED_OR_ENDPOINT_WIRE_ID_REQUIRES_POOL_EVIDENCE", "SCALING_QL_COMPATIBLE_IF_MEMBERSHIP_PROVEN"}}
        slot_rows_by_family[family].append((identity, {source["hash"] for source in eligible}, unassigned))
        occurrence_rows.append({
            **dict(zip(("Session", "CapturedUtc", "InventorySequence", "CorpseIdentity"), identity)),
            "NpcIdentity": row["NpcIdentity"], "NpcLevel": row["NpcLevel"], "Playfield": row["Playfield"],
            "Slot": row["Slot"], "WireLowId": pair[0], "WireHighId": pair[1], "Ql": quality,
            "FamilyKey": family, "FamilyEvidence": catalog["FamilyEvidence"], "EquipmentSignature": signature_names[signature],
            "EquipmentFunctions": json_cell(signature), "ExactEligibleRoots": json_cell(sorted({s["hash"] for s in eligible
                                                                                                  if not s["qlStatus"].startswith("ELIGIBLE_RECONSTRUCTED_")})),
            "ReconstructedEligibleRoots": json_cell(sorted({s["hash"] for s in eligible if s["qlStatus"].startswith("ELIGIBLE_RECONSTRUCTED_")})),
            "UpperEndpointEquivalentRoots": json_cell(sorted({s["hash"] for s in equivalent})),
            "ReconstructedFamilyEquivalentRoots": json_cell(sorted({s["hash"] for s in reconstructed_equivalent})),
            "UnresolvedPoolEvidence": json_cell({root: sorted(values) for root, values in candidate_roots.items()}),
        })

    frequencies = []
    for (family, signature), counts in sorted(frequency_counts.items(), key=lambda pair: (pair[0][0], pair[0][1] is not None, pair[0][1] or ())):
        corpses = len(bosses) if signature is None else population[signature]
        total_slots = len(items) if signature is None else slots_by_signature[signature]
        observed_slots = sum(counts.values())
        low, high = wilson_interval(len(counts), corpses)
        frequencies.append({"FamilyKey": family, "EquipmentSignature": "ALL" if signature is None else signature_names[signature],
                            "ObservedSlots": observed_slots, "ObservedCorpses": len(counts), "CohortSlots": total_slots,
                            "CohortCorpses": corpses, "SlotShare": f"{observed_slots / total_slots:.8f}",
                            "ObservedCorpseFrequency": f"{len(counts) / corpses:.8f}",
                            "ObservedCorpseWilson95Low": f"{low:.8f}", "ObservedCorpseWilson95High": f"{high:.8f}",
                            "ConditionalWeight": "", "WeightStatus": "UNOBSERVED_INDEPENDENT_POOL_TRIALS"})

    membership = []
    for family, rows in sorted(slot_rows_by_family.items()):
        roots = [exact | candidate for _, exact, candidate in rows]
        union = set.union(*roots)
        shared = set.intersection(*roots)
        evidence_class = ("UNIQUE_COMMON_CANDIDATE" if len(shared) == 1 else
                          "MULTIPLE_COMMON_CANDIDATES" if shared else
                          "NO_COMMON_CANDIDATE" if union else "NO_COMPATIBLE_EQUIPMENT_SOURCE")
        for root in sorted(union) or [""]:
            exact_count = sum(root in exact for _, exact, _ in rows)
            candidate_count = sum(root in candidates for _, _, candidates in rows)
            membership.append({"FamilyKey": family, "CandidateHash": root, "ObservedFamilySlots": len(rows),
                               "CandidateRootOnCorpses": populations_by_root[root], "ExactEligibleSlots": exact_count,
                               "PotentialUnresolvedSlots": candidate_count, "CommonAcrossObservedSlots": root in shared,
                               "EvidenceClass": evidence_class, "AssignmentStatus": "CANDIDATE_ONLY"})
    return occurrence_rows, frequencies, membership
