"""Reconcile reviewed RK dyna captures against the current, private GameData.

This offline tool never writes GameData. Inputs are capture-derived CSVs and
the exports of the existing items.dat reader. A compatible source is a possible
explanation for a packet, not proof of the original server's selection or odds.
"""

import argparse
from collections import Counter, defaultdict
import csv
from dataclasses import dataclass
from functools import cache
import hashlib
import json
import math
from pathlib import Path
import struct


BOSS_COHORT = "RK_DYNA_SIGNATURE"
PACKET_KEY = ("Session", "CapturedUtc", "InventorySequence", "CorpseIdentity")
BOSS_FIELDS = (
    "Playfield", "CorpseFullUpdateSequence", "NpcIdentity", "NpcName",
    "MonsterData", "NpcLevel", "LiveScfuSequence", "LiveScfuFlags",
    "LiveScfuNanoIds", "Cohort",
)
ITEM_FIELDS = ("Slot", "LowId", "HighId", "Ql", "Count")
SPAWN_ITEM = 53064
CAST_NANO = 53051
SPECIAL_ITEM = 1 << 5


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def read_csv(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def provenance(path):
    return {"path": str(path.resolve()), "sha256": sha256(path)}


def json_cell(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def write_csv(path, fields, rows):
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def values(row, fields):
    return tuple(row[field] for field in fields)


def key(row):
    return values(row, PACKET_KEY)


def add_unique(target, identity, row, fields):
    current = target.setdefault(identity, row)
    if values(current, fields) != values(row, fields):
        raise ValueError(f"Conflicting evidence for {identity}")


def load_captures(batch_dirs):
    corpses = {}
    slots = {}
    inputs = []
    for batch in batch_dirs:
        corpse_path = batch / "rk-corpse-inventories.csv"
        item_path = batch / "rk-dyna-item-observations.csv"
        inputs.extend(provenance(p) for p in (corpse_path, item_path))
        for row in read_csv(corpse_path):
            add_unique(corpses, key(row), row, BOSS_FIELDS + ("ItemSlots", "Descriptors"))
        for row in read_csv(item_path):
            if row["Cohort"] != BOSS_COHORT:
                raise ValueError(f"Unreviewed row in confirmed item input: {key(row)}")
            add_unique(slots, (*key(row), int(row["Slot"])), row, BOSS_FIELDS + ITEM_FIELDS)

    grouped = defaultdict(list)
    for identity, row in sorted(slots.items()):
        corpse = corpses.get(identity[:-1])
        if corpse is None or values(corpse, BOSS_FIELDS) != values(row, BOSS_FIELDS):
            raise ValueError(f"Item has no matching confirmed corpse: {identity}")
        grouped[identity[:-1]].append(row)

    for identity, corpse in corpses.items():
        if corpse["Cohort"] != BOSS_COHORT:
            continue
        if not all(corpse[field] for field in ("NpcIdentity", "CorpseFullUpdateSequence", "LiveScfuSequence")):
            raise ValueError(f"Confirmed corpse lacks identity provenance: {identity}")
        items = grouped[identity]
        expected = corpse["Descriptors"].split(";") if corpse["Descriptors"] else []
        actual = [":".join(values(row, ITEM_FIELDS[1:])) for row in items]
        if actual != expected or len(items) != int(corpse["ItemSlots"]):
            raise ValueError(f"Item descriptors do not match original inventory: {identity}")
        if any(int(row["Slot"]) < 0 for row in items):
            raise ValueError(f"Invalid negative slot: {identity}")
    return corpses, grouped, inputs


def load_item_metadata(directory, items_dat, equipment_ids, spawn_args=None, equipment_rows=None):
    source = read_json(directory / "item-metadata-source.json")
    if sha256(items_dat) != source["SourceSha256"].lower():
        raise ValueError("Item metadata was exported from a different items.dat")

    qualities, flags, names, casts = {}, {}, {}, {}
    digest = hashlib.sha256()
    with (directory / "item-metadata.jsonl").open("rb") as stream:
        for line in stream:
            digest.update(line)
            row = json.loads(line)
            template = row["Id"]
            if template in qualities:
                raise ValueError(f"Duplicate template ID in item metadata: {template}")
            qualities[template] = max(1, row["Quality"])
            flags[template] = row["Flags"]
            names[template] = row.get("Name") or ""
            if template in equipment_ids or (equipment_rows is not None and
                                              names[template].startswith(("Boss Modifier Item", "Lootgiver"))):
                casts[template] = frozenset(
                    fn["IntegerArguments"][0] for fn in row["FunctionDetails"]
                    if fn["Event"] == "OnWear" and fn["Function"] == CAST_NANO
                )
                if spawn_args is not None:
                    spawn_args[template] = [
                        tuple(fn["IntegerArguments"][1:]) for fn in row["FunctionDetails"]
                        if fn["Event"] == "OnWear" and fn["Function"] == SPAWN_ITEM
                    ]
                if equipment_rows is not None:
                    on_wear = [fn for fn in row["FunctionDetails"] if fn["Event"] == "OnWear"]
                    for position, fn in enumerate(on_wear, 1):
                        if fn["Function"] != SPAWN_ITEM or "StringArguments" not in fn:
                            continue
                        arguments = [string if string is not None else integer for integer, string in
                                     zip(fn["IntegerArguments"], fn["StringArguments"], strict=True)]
                        equipment_rows.append({"Id": template, "ItemName": row["Name"], "StoredQl": row["Quality"],
                                               "Position": position, "Function": SPAWN_ITEM,
                                               "Arguments": arguments, "Requirements": []})
    if digest.hexdigest() != source["OutputSha256"].lower() or len(qualities) != source["RecordCount"]:
        raise ValueError("Item metadata digest or record count does not match its receipt")
    return qualities, flags, names, casts


def quality_window(level, percent):
    minimum = max(1, level * (100 - percent) // 100)
    return minimum, max(minimum, level * (100 + percent) // 100)


@dataclass(frozen=True)
class Leaf:
    path: tuple
    bands: tuple
    special: bool

    @property
    def minimum(self):
        return self.bands[0][0]

    @property
    def maximum(self):
        return self.bands[-1][0]

    def ids_at(self, quality):
        if len(self.bands) == 1:
            template = self.bands[0][1]
            return template, template
        for (low_ql, low_id), (high_ql, high_id) in zip(self.bands, self.bands[1:]):
            if low_ql <= quality <= high_ql:
                return low_id, high_id
        raise ValueError(f"QL {quality} is outside {'/'.join(self.path)}")


class HashPools:
    """Enumerate possible outcomes of existing hash selection without random draws."""

    def __init__(self, records, qualities, flags):
        self.records = records
        self.qualities = qualities
        self.flags = flags

    @cache
    def inspect(self, root):
        leaves = []
        issues = set()

        def visit(node, trail):
            path = (*trail, node)
            if node in trail:
                issues.add("cycle:" + "/".join(path))
                return
            entry = self.records.get(node)
            if not entry:
                issues.add("missing:" + "/".join(path))
                return
            children = entry.get("Children") or []
            if children:
                for child in children:
                    visit(child, path)
                return
            templates = [template for template in (entry.get("Templates") or []) if template > 0]
            if not templates:
                issues.add("empty:" + "/".join(path))
                return
            bands = tuple(sorted(((self.qualities.get(t, 1), t) for t in templates), key=lambda band: band[0]))
            for template in templates:
                if template not in self.qualities:
                    issues.add(f"unknown-template:{'/'.join(path)}:{template}")
            if len(bands) > 16 and len({ql for ql, _ in bands}) != len(bands):
                issues.add("equal-ql-sort-unverified:" + "/".join(path))
            leaves.append(Leaf(path, bands, any(self.flags.get(t, 0) & SPECIAL_ITEM for t in templates)))

        visit(root, ())
        return tuple(leaves), tuple(sorted(issues))

    def pool(self, root):
        return self.inspect(root)[0]

    @cache
    def highest(self, root, desired_quality):
        wanted = max(1, desired_quality)
        leaves = self.pool(root)
        best = max((min(wanted, leaf.maximum) for leaf in leaves if leaf.minimum <= wanted), default=0)
        reachable = {}
        for leaf in leaves:
            if leaf.minimum <= best and min(wanted, leaf.maximum) == best:
                reachable.setdefault(leaf.path[-1], (leaf, best))
        return tuple(reachable.values())

    @cache
    def ladder(self, parent):
        children = self.records[parent].get("Children") or []
        tiers = set()
        for child in children:
            leaves = self.pool(child)
            if len(leaves) != 1 or leaves[0].path != (child,) or not leaves[0].special:
                return ()
            leaf = leaves[0]
            if leaf.minimum != leaf.maximum:
                return ()
            tiers.add(leaf.minimum)
        return tuple(sorted(tiers)) if len(tiers) >= 3 else ()

    def table_reaches(self, leaf, window, quality):
        minimum, maximum = window
        if not leaf.special and (leaf.maximum < minimum or leaf.minimum > maximum):
            return False
        for parent, child in zip(leaf.path, leaf.path[1:]):
            # SpawnAll recurses through every child without the category's random-pick filter.
            if self.records[parent].get("SpawnAll"):
                continue
            tiers = self.ladder(parent)
            if tiers:
                low = max((tier for tier in tiers if tier < minimum), default=tiers[0] if tiers[0] > maximum else minimum)
                child_ql = self.pool(child)[0].minimum
                if not low <= child_ql <= max(low, maximum):
                    return False
        return min(leaf.maximum, max(leaf.minimum, minimum)) <= quality <= max(leaf.minimum, min(leaf.maximum, maximum))


def verified_source(row, manifest):
    source = manifest.parent / row["sourceFile"]
    receipt = provenance(source)
    locator = row["sourceLocator"]
    if receipt["sha256"] != row["sourceSha256"].lower() or not locator.startswith("/"):
        raise ValueError(f"Reviewed evidence source mismatch: {source}")
    entry = read_json(source)
    for token in locator[1:].split("/"):
        token = token.replace("~1", "/").replace("~0", "~")
        try:
            entry = entry[int(token) if isinstance(entry, list) else token]
        except (IndexError, KeyError, TypeError, ValueError) as error:
            raise ValueError(f"Invalid evidence pointer {locator}: {source}") from error
    return {**receipt, "locator": locator}, entry


def apply_reviewed_definitions(records, manifest, qualities, flags):
    proposals = {}
    if not manifest:
        return proposals
    for row in read_json(manifest):
        root = row["hash"]
        if root in records or root in proposals:
            raise ValueError(f"Reviewed definition would replace an existing hash: {root}")
        record = row["definition"]
        if bool(record.get("Children")) == bool(record.get("Templates")) or set(record) - {"Children", "Templates", "SpawnAll"}:
            raise ValueError(f"Invalid ItemTemplates definition: {root}")
        receipt, source_record = verified_source(row, manifest)
        source_hash = receipt["locator"].rsplit("/", 1)[1].replace("~1", "/").replace("~0", "~")
        if source_hash != root or source_record != record:
            raise ValueError(f"Reviewed definition does not match the exact evidence record: {root}")
        proposals[root] = {"definition": record, "evidence": receipt}
    definitions = {root: row["definition"] for root, row in proposals.items()}
    pools = HashPools(records | definitions, qualities, flags)
    for root in proposals:
        leaves, issues = pools.inspect(root)
        if not leaves or issues:
            raise ValueError(f"Reviewed definition {root} does not completely resolve: {issues}")
    records.update(definitions)
    return proposals


def lerp_int(low, high, fraction):
    value = low + (high - low) * fraction
    return math.floor(value + 0.5) if value >= 0 else math.ceil(value - 0.5)


def npc_band(bands, level):
    bands = sorted(bands, key=lambda band: band["Level"])
    if not bands or not bands[0]["Level"] <= level <= bands[-1]["Level"]:
        return None
    for index, high in enumerate(bands):
        if high["Level"] == level:
            return high, high["Stats"].get("359")
        if high["Level"] > level:
            low = bands[index - 1]
            fraction = (level - low["Level"]) / (high["Level"] - low["Level"])
            monster_data = low["Stats"].get("359")
            if monster_data is None:
                monster_data = high["Stats"].get("359")
            else:
                monster_data = lerp_int(monster_data, high["Stats"].get("359", monster_data), fraction)
            return (low if fraction <= 0.5 else high), monster_data
    return None


class Equipment:
    def __init__(self, rows, qualities, names, casts, spawn_args, hash_records):
        self.qualities = qualities
        self.names = names
        self.functions = defaultdict(dict)
        self.modifier_names = set()
        self.aura_owners = defaultdict(set)
        self.families = defaultdict(set)
        for row in rows:
            template = row["Id"]
            arguments = row["Arguments"]
            if row["Function"] != SPAWN_ITEM or not arguments[0] or int(arguments[1]) <= 0:
                raise ValueError(f"Invalid SpawnItem function in export: {row}")
            if template not in spawn_args or qualities[template] != row["StoredQl"] or names[template] != row["ItemName"]:
                raise ValueError(f"Export equipment endpoint disagrees with items.dat: {template}")
            add_unique(self.functions[template], row["Position"], row, ("Arguments", "Requirements"))
            for aura in casts[template]:
                self.aura_owners[aura].add(names[template])
                self.modifier_names.add(names[template])
        for template, functions in self.functions.items():
            exported = Counter(tuple(row["Arguments"][1:]) for row in functions.values())
            if exported != Counter(spawn_args[template]):
                raise ValueError(f"Export SpawnItem functions disagree with items.dat: {template}")
        for item_hash, record in hash_records.items():
            if record.get("Children"):
                continue
            for template in record.get("Templates") or []:
                if template in self.functions:
                    self.families[names[template]].add(item_hash)

    def match_auras(self, nano_ids):
        owners = [self.aura_owners[nano] for nano in nano_ids if nano in self.aura_owners]
        if not owners:
            return set(), "NO_MATCHED_AURA"
        candidates = set.intersection(*owners)
        if not candidates:
            return set.union(*owners), "CONFLICTING_AURAS"
        return candidates, "AURA_CANDIDATE" if len(candidates) == 1 else "AMBIGUOUS_AURA"

    def pair(self, row):
        if not row or row[0] not in self.functions:
            return None
        low = row[0]
        high = row[1] if len(row) > 1 and row[1] > 0 else low
        if high not in self.functions:
            raise ValueError(f"Missing high equipment endpoint in export: {row}")
        return low, high

    def aura_pairs(self, modifier_names):
        for name in sorted(modifier_names):
            endpoints = sorted((t for t in self.functions if self.names[t] == name), key=self.qualities.__getitem__)
            yield endpoints[0], endpoints[-1]

    def functions_at(self, pair, level):
        low, high = pair
        low_ql, high_ql = self.qualities[low], self.qualities[high]
        level = min(max(level, min(low_ql, high_ql)), max(low_ql, high_ql))
        a, b = self.functions[low], self.functions[high]
        if level == high_ql and high_ql != low_ql:
            a = b
        if level in (low_ql, high_ql):
            for pos, row in sorted(a.items()):
                root, center, *_ = row["Arguments"]
                yield pos, root, center
            return

        if a.keys() != b.keys():
            raise ValueError(f"Cannot interpolate differing equipment layouts: {pair}")
        # ItemBuilder computes the ratio as float32, then rounds the interpolated value to even.
        fraction = struct.unpack("<f", struct.pack("<f", (level - low_ql) / (high_ql - low_ql)))[0]
        for pos, row in sorted(a.items()):
            root, center, *_ = row["Arguments"]
            high_root, high_center, *_ = b[pos]["Arguments"]
            if root != high_root:
                raise ValueError(f"Cannot interpolate differing equipment hashes at position {pos}: {pair}")
            yield pos, root, round(center + (high_center - center) * fraction)


def load_bindings(path):
    result = {}
    if path is None:
        return result
    for row in read_json(path):
        identity = key(row)
        if identity in result:
            raise ValueError(f"Duplicate reviewed NPC binding: {identity}")
        receipt, source_record = verified_source(row, path)
        fields = PACKET_KEY + ("NpcIdentity", "NpcHash", "EquipmentPairs")
        if values(source_record, fields) != values(row, fields):
            raise ValueError(f"Reviewed binding does not match the exact evidence record: {identity}")
        result[identity] = {**row, "evidence": receipt}
    return result


class NpcSources:
    def __init__(self, templates, equipment, playfields=None):
        self.templates = templates
        self.equipment = equipment
        self.playfields = playfields
        self.placement_inputs = set()
        self.boss_leaves = {
            npc_hash: row["Templates"] for npc_hash, row in templates.items()
            if any(equipment.names.get(pair[0]) in equipment.modifier_names
                   for band in row.get("Templates") or []
                   for pair in band.get("Equipment") or [] if pair)
        }

    def candidates(self, corpse, modifier_names, binding):
        level = int(corpse["NpcLevel"])
        if binding:
            if binding["NpcIdentity"] != corpse["NpcIdentity"]:
                raise ValueError(f"Reviewed NPC identity conflicts with corpse: {key(corpse)}")
            npc_hash = binding["NpcHash"]
            band = npc_band(self.templates[npc_hash]["Templates"], level)
            if band is None or band[1] != int(corpse["MonsterData"]):
                raise ValueError(f"Reviewed NPC binding conflicts with canonical level or MonsterData: {key(corpse)}")
            yield npc_hash, band[0], binding["EquipmentPairs"], "REVIEWED_EQUIPMENT"
            return

        for npc_hash, bands in sorted(self.boss_leaves.items()):
            matched = npc_band(bands, level)
            if matched is None or matched[1] != int(corpse["MonsterData"]):
                continue
            band = matched[0]
            gear = [pair for pair in band.get("Equipment") or [] if pair]
            if not any(self.equipment.names.get(pair[0]) in modifier_names for pair in gear):
                continue
            pairs = [pair for pair in gear if self.equipment.names.get(pair[0]) not in self.equipment.modifier_names
                     or self.equipment.names.get(pair[0]) in modifier_names]
            yield npc_hash, band, pairs, "GLOBAL_MONSTERDATA_AND_AURA_CANDIDATE"

    @cache
    def playfield_roots(self, playfield):
        if self.playfields is None:
            return {}, False
        folder = self.playfields / str(int(playfield))
        spawns, districts = folder / "Spawns.json", folder / "Districts.json"
        roots = set()
        if spawns.is_file():
            self.placement_inputs.add(spawns)
            roots.update(row["hashText"] for row in read_json(spawns)["spawns"])
        if districts.is_file():
            self.placement_inputs.add(districts)
            for district in read_json(districts)["districts"]:
                if district.get("spawnPoints"):
                    roots.update(info["hashText"] for info in district.get("spawnInfos", []) if info.get("count", 0) > 0)

        placed = defaultdict(set)
        for root in sorted(roots):
            pending = [root]
            seen = set()
            while pending:
                npc_hash = pending.pop()
                if npc_hash in seen or npc_hash not in self.templates:
                    continue
                seen.add(npc_hash)
                placed[npc_hash].add(root)
                pending.extend(self.templates[npc_hash].get("Children") or [])
        return placed, spawns in self.placement_inputs and districts in self.placement_inputs


def sources_for(corpse, npcs, equipment, binding, variance):
    level = int(corpse["NpcLevel"])
    nanos = {int(nano) for nano in corpse["LiveScfuNanoIds"].split(";") if nano}
    modifier_names, aura_status = equipment.match_auras(nanos)
    templates = []
    sources = []
    placed, checked = npcs.playfield_roots(corpse["Playfield"])

    def add_equipment(pair, npc_hash, certainty, placement_status):
        name = equipment.names[pair[0]]
        for position, root, center in equipment.functions_at(pair, level):
            sources.append({"hash": root, "mechanism": "equipment", "confidence": certainty,
                            "npcHash": npc_hash, "equipmentIds": pair, "equipmentName": name,
                            "equipmentFamilies": sorted(equipment.families[name]), "functionPosition": position,
                            "center": center, "window": quality_window(center, variance),
                            "playfieldPlacementStatus": placement_status})

    for npc_hash, band, pairs, certainty in npcs.candidates(corpse, modifier_names, binding):
        roots = sorted(placed.get(npc_hash, ()))
        if roots:
            placement_status = "REFERENCED_BY_CANONICAL_PLAYFIELD_HASH_GRAPH"
        else:
            placement_status = "NO_CANONICAL_PLAYFIELD_REFERENCE" if checked else "PLAYFIELD_DATA_INCOMPLETE"
        templates.append({"hash": npc_hash, "bandLevel": band["Level"], "name": band["Name"],
                          "templateId": band.get("TemplateId"), "confidence": certainty,
                          "playfieldPlacementStatus": placement_status, "playfieldSpawnRoots": roots})
        for pair in pairs:
            endpoints = equipment.pair(pair)
            if endpoints:
                add_equipment(endpoints, npc_hash, certainty, placement_status)
        for index, entry in enumerate(band.get("LootTable") or []):
            if entry.get("Hash") and entry.get("Chance", 0) > 0 and entry.get("Repeats", 0) > 0:
                sources.append({"hash": entry["Hash"], "mechanism": "item-table", "confidence": certainty,
                                "npcHash": npc_hash, "entryIndex": index,
                                "playfieldPlacementStatus": placement_status,
                                "center": level, "window": quality_window(level, entry.get("LevelMod") or variance)})

    if not templates and not binding:
        for pair in equipment.aura_pairs(modifier_names):
            add_equipment(pair, None, "AURA_ONLY_CANDIDATE", "NPC_TEMPLATE_UNRESOLVED")
    return templates, sources, aura_status


def slot_explanations(pools, sources, low_id, high_id, quality):
    eligible = []
    members = []
    unresolved = set()
    for source in sources:
        root = source["hash"]
        leaves, issues = pools.inspect(root)
        if not leaves or issues:
            unresolved.add(root)
        for leaf in leaves:
            ids = {template for _, template in leaf.bands}
            if low_id not in ids or high_id not in ids:
                continue
            minimum, maximum = source["window"]
            result = {**source, "path": leaf.path, "familyQl": (leaf.minimum, leaf.maximum),
                      "fixedQl": leaf.minimum == leaf.maximum}
            if not leaf.minimum <= quality <= leaf.maximum:
                result["qlStatus"] = "OUTSIDE_FAMILY_RANGE"
            elif leaf.ids_at(quality) != (low_id, high_id):
                result["qlStatus"] = ("UPPER_ENDPOINT_WIRE_ALTERNATIVE" if low_id == high_id and
                                      leaf.ids_at(quality)[1] == high_id and any(band == (quality, high_id) for band in leaf.bands)
                                      else "ENDPOINT_PAIR_NOT_SELECTED_AT_QL")
            elif source["mechanism"] == "item-table" and pools.table_reaches(leaf, source["window"], quality):
                result["qlStatus"] = "ELIGIBLE_ITEM_TABLE"
            elif source["mechanism"] == "equipment":
                desired = [target for target in range(max(minimum, quality), maximum + 1)
                           if any(other.path[-1] == leaf.path[-1] and reached == quality
                                  for other, reached in pools.highest(root, target))]
                if desired:
                    result["qlStatus"] = "ELIGIBLE_HIGHEST_UP_TO"
                    result["desiredQl"] = desired
                elif quality > maximum:
                    result["qlStatus"] = "ABOVE_EQUIPMENT_WINDOW"
                else:
                    result["qlStatus"] = "NOT_HIGHEST_ELIGIBLE_IN_WINDOW"
            else:
                result["qlStatus"] = "NOT_ITEM_TABLE_ELIGIBLE_IN_WINDOW"
            bad_leaf = any(template not in pools.qualities for _, template in leaf.bands) or (
                "equal-ql-sort-unverified:" + "/".join(leaf.path) in issues)
            if result["qlStatus"].startswith("ELIGIBLE_") and not bad_leaf:
                eligible.append(result)
            else:
                members.append(result)
    return eligible, members, sorted(unresolved)


def reconcile(corpses, slots, pools, npcs, equipment, bindings, variance):
    if bindings.keys() - {identity for identity, row in corpses.items() if row["Cohort"] == BOSS_COHORT}:
        raise ValueError("Reviewed binding refers to a corpse outside the confirmed input")

    family_index = defaultdict(set)
    for family, record in pools.records.items():
        if not record.get("Children"):
            for item_id in record.get("Templates") or []:
                family_index[item_id].add(family)

    boss_rows = []
    item_rows = []
    excluded_rows = []
    root_corpses = Counter()
    root_slots = Counter()
    for identity, corpse in sorted(corpses.items()):
        shared = {field: corpse[field] for field in PACKET_KEY + BOSS_FIELDS}
        if corpse["Cohort"] != BOSS_COHORT:
            excluded_rows.append({**shared, "ItemSlots": corpse["ItemSlots"]})
            continue
        binding = bindings.get(identity)
        templates, sources, aura_status = sources_for(corpse, npcs, equipment, binding, variance)
        referenced = sorted({source["hash"] for source in sources})
        root_corpses.update(referenced)
        boss_rows.append({**shared, "ItemSlots": corpse["ItemSlots"], "AuraStatus": aura_status,
                          "NpcBindingStatus": "REVIEWED" if binding else "CANDIDATE_ONLY" if templates else "UNRESOLVED",
                          "ReviewedBindingEvidence": json_cell(binding["evidence"] if binding else None),
                          "NpcTemplateCandidates": json_cell(templates), "LootSources": json_cell(sources),
                          "ReferencedHashes": ";".join(referenced)})
        for row in slots[identity]:
            low, high, quality = (int(row[field]) for field in ("LowId", "HighId", "Ql"))
            eligible, members, unresolved = slot_explanations(pools, sources, low, high, quality)
            families = sorted(family_index[low] & family_index[high])
            compatible_hashes = sorted({source["hash"] for source in eligible})
            root_slots.update(compatible_hashes)
            if len(eligible) > 1:
                status = "AMBIGUOUS_SOURCES"
            elif eligible:
                status = "COMPATIBLE_REVIEWED_SOURCE" if binding else "COMPATIBLE_CANDIDATE_SOURCE"
            elif members:
                status = "KNOWN_MEMBERSHIP_NOT_RUNTIME_ELIGIBLE"
            else:
                status = "UNEXPLAINED"
            item_rows.append({**shared, **{field: row[field] for field in ITEM_FIELDS},
                              "ItemName": equipment.names.get(low, ""), "ItemIdsInItemsDat": low in pools.qualities and high in pools.qualities,
                              "CatalogLowQl": pools.qualities.get(low, ""), "CatalogHighQl": pools.qualities.get(high, ""),
                              "ObservedFixedQl": low == high and pools.qualities.get(low) == quality,
                              "ItemFamilies": ";".join(families), "Status": status,
                              "RuntimeEligibleSources": json_cell(eligible), "KnownMembershipOnly": json_cell(members),
                              "UnresolvedCandidateHashes": ";".join(unresolved)})
    return boss_rows, item_rows, excluded_rows, root_corpses, root_slots


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture-dir", action="append", type=Path, required=True,
                        help="Private batch containing reviewed corpse and item CSVs. Repeat to reconcile multiple batches.")
    parser.add_argument("--game-data", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True, help="Private report destination outside GameData.")
    parser.add_argument("--equipment-functions", type=Path, help="Exact OnWear SpawnItem export; defaults to first batch.")
    parser.add_argument("--item-metadata", type=Path, help="items.dat metadata export directory; defaults to first batch.")
    parser.add_argument("--authoritative-definitions", type=Path,
                        help="Reviewed JSON rows (hash, definition, sourceFile, sourceSha256, sourceLocator). Offline overlay only.")
    parser.add_argument("--npc-bindings", type=Path,
                        help="Reviewed JSON rows (corpse packet identity, NpcIdentity, NpcHash, EquipmentPairs, sourceFile, sourceSha256, sourceLocator).")
    parser.add_argument("--gameplay-config", type=Path,
                        default=Path(__file__).resolve().parents[1] / "AORebirth" / "Config" / "Gameplay.json")
    args = parser.parse_args(argv)
    batch = args.capture_dir[0]
    game_data = args.game_data.resolve()
    output = args.output_dir.resolve()
    if output.is_relative_to(game_data):
        raise ValueError("Report destination must be outside GameData")
    item_templates = game_data / "ItemTemplates.json"
    npc_templates = game_data / "NpcTemplates.json"
    items_dat = game_data / "items.dat"
    equipment_path = args.equipment_functions
    legacy_equipment_path = batch / "equipment-functions.json"
    item_metadata = args.item_metadata or batch / "item-metadata"

    corpses, slots, inputs = load_captures(args.capture_dir)
    records = read_json(item_templates)
    equipment_rows = read_json(equipment_path) if equipment_path else []
    spawn_args = {}
    qualities, flags, names, casts = load_item_metadata(item_metadata, items_dat,
                                                       {row["Id"] for row in equipment_rows}, spawn_args,
                                                       equipment_rows if not equipment_path else None)
    if not equipment_rows and not equipment_path and legacy_equipment_path.is_file():
        equipment_path = legacy_equipment_path
        equipment_rows = read_json(equipment_path)
    if not equipment_rows:
        raise ValueError("Export StringArguments or supply --equipment-functions for legacy metadata")
    proposals = apply_reviewed_definitions(records, args.authoritative_definitions, qualities, flags)
    pools = HashPools(records, qualities, flags)
    equipment = Equipment(equipment_rows, qualities, names, casts, spawn_args, records)
    npcs = NpcSources(read_json(npc_templates), equipment, game_data / "Playfields")
    bindings = load_bindings(args.npc_bindings)
    variance = read_json(args.gameplay_config)["loot"]["qualityVariancePercent"]
    if not isinstance(variance, int) or not 0 <= variance <= 100:
        raise ValueError("Expected loot.qualityVariancePercent from 0 to 100")
    bosses, items, excluded, root_corpses, root_slots = reconcile(corpses, slots, pools, npcs, equipment, bindings, variance)

    coverage = []
    for root in sorted({row["Arguments"][0] for row in equipment_rows} | root_corpses.keys()):
        leaves, issues = pools.inspect(root)
        coverage.append({"Hash": root, "DefinitionStatus": ("REVIEWED_PROPOSAL" if root in proposals else
                                                           "UNRESOLVED" if not leaves else "PARTIALLY_RESOLVED" if issues else "RESOLVED"),
                         "ReferencedCorpseCount": root_corpses[root], "CompatibleSlotCount": root_slots[root],
                         "DirectChildren": json_cell((records.get(root) or {}).get("Children") or []),
                         "LeafCount": len({leaf.path[-1] for leaf in leaves}),
                         "LeafPaths": json_cell([leaf.path for leaf in leaves]),
                         "Issues": json_cell(issues)})

    equipment_ids = {row["Id"] for row in equipment_rows}
    source_paths = [item_templates, npc_templates, items_dat, args.gameplay_config,
                    item_metadata / "item-metadata-source.json", item_metadata / "item-metadata.jsonl"]
    source_paths.extend(sorted(npcs.placement_inputs))
    source_paths.extend(path for path in (equipment_path, args.authoritative_definitions, args.npc_bindings) if path)
    inputs.extend(provenance(path) for path in source_paths)
    ambiguities = [row for row in items if row["Status"] == "AMBIGUOUS_SOURCES"]
    unexplained = [row for row in items if row["Status"] in ("UNEXPLAINED", "KNOWN_MEMBERSHIP_NOT_RUNTIME_ELIGIBLE")]
    observed = {}
    for row in sorted(items, key=lambda row: (row["CapturedUtc"], key(row), int(row["Slot"]))):
        item_pair = (row["LowId"], row["HighId"])
        first = observed.setdefault(item_pair, {"LowId": item_pair[0], "HighId": item_pair[1],
                                                "ItemName": row["ItemName"], "ItemFamilies": row["ItemFamilies"],
                                                "FirstCaptureUtc": row["CapturedUtc"], "FirstCaptureSession": row["Session"],
                                                "Slots": 0, "ObservedQls": set()})
        first["Slots"] += 1
        first["ObservedQls"].add(int(row["Ql"]))
    observed = [{**row, "ObservedQls": ";".join(map(str, sorted(row["ObservedQls"])))} for row in observed.values()]
    cohort_roots = defaultdict(set)
    for row in bosses:
        for source in json.loads(row["LootSources"]):
            cohort_roots[source["mechanism"]].add(source["hash"])
    summary = {
        "schemaVersion": 1, "inputs": inputs, "corpseCohorts": dict(Counter(row["Cohort"] for row in corpses.values())),
        "confirmedCorpses": len(bosses), "confirmedItemSlots": len(items), "excludedInventories": len(excluded),
        "unclassifiedInventories": sum(row["Cohort"] in ("UNLINKED", "NO_PRIOR_SCFU") for row in excluded),
        "confirmedItemCountDistribution": dict(Counter(int(row["ItemSlots"]) for row in bosses)),
        "slotStatuses": dict(Counter(row["Status"] for row in items)), "distinctObservedEndpointPairs": len(observed),
        "equipmentTemplateIdsInExport": len(equipment_ids), "lootQualityVariancePercent": variance,
        "referencedCohortHashes": sorted(root_corpses),
        "referencedCohortEquipmentHashes": sorted(cohort_roots["equipment"]),
        "referencedCohortItemTableHashes": sorted(cohort_roots["item-table"]),
        "unresolvedCohortHashes": sorted(row["Hash"] for row in coverage if row["ReferencedCorpseCount"] and row["Issues"] != "[]"),
        "proposedItemTemplatesDefinitions": proposals,
    }

    report_names = ("bosses.csv", "items.csv", "ambiguities.csv", "unexplained.csv", "excluded-inventories.csv",
                    "observed-item-families.csv", "hash-coverage.csv", "summary.json")
    for name in report_names:
        if (output / name).resolve() != output / name:
            raise ValueError(f"Report path leaves report destination: {name}")
    output.mkdir(parents=True, exist_ok=True)
    boss_fields = PACKET_KEY + BOSS_FIELDS + ("ItemSlots", "AuraStatus", "NpcBindingStatus", "ReviewedBindingEvidence",
                                             "NpcTemplateCandidates", "LootSources", "ReferencedHashes")
    item_fields = PACKET_KEY + BOSS_FIELDS + ITEM_FIELDS + ("ItemName", "ItemIdsInItemsDat", "CatalogLowQl", "CatalogHighQl",
                                                          "ObservedFixedQl", "ItemFamilies", "Status", "RuntimeEligibleSources",
                                                          "KnownMembershipOnly", "UnresolvedCandidateHashes")
    write_csv(output / "bosses.csv", boss_fields, bosses)
    write_csv(output / "items.csv", item_fields, items)
    write_csv(output / "ambiguities.csv", item_fields, ambiguities)
    write_csv(output / "unexplained.csv", item_fields, unexplained)
    write_csv(output / "excluded-inventories.csv", PACKET_KEY + BOSS_FIELDS + ("ItemSlots",), excluded)
    write_csv(output / "observed-item-families.csv", ("LowId", "HighId", "ItemName", "ItemFamilies", "FirstCaptureUtc",
                                                     "FirstCaptureSession", "Slots", "ObservedQls"), observed)
    write_csv(output / "hash-coverage.csv", ("Hash", "DefinitionStatus", "ReferencedCorpseCount", "CompatibleSlotCount",
                                              "DirectChildren", "LeafCount", "LeafPaths", "Issues"), coverage)
    (output / "summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Reconciled {len(items)} slots on {len(bosses)} confirmed corpses from {len(corpses)} inventories")


if __name__ == "__main__":
    main()
