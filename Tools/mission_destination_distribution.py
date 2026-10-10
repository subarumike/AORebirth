"""Offline observed zone distributions; never changes runtime catalogs or capture inputs."""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import csv
import gzip
import hashlib
import io
import itertools
import json
from pathlib import Path
import subprocess

from mission_destination_distribution_sources import extract

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs/generated/missions/destination-zone-distribution"
EVIDENCE = "f07bb3c1a99218433e7d459c95ba29ccb22c36b2"
ZONE_SOURCE = "docs/generated/missions/arpa3/clicksaver-playfield-catalog.json"
LEVEL_SOURCE = "AORebirth/GameData/Missions/Source/MissionLevels.csv"
LEVELS = (2, 7, 13, 25, 35, 37)
SLIDERS = ("good_bad", "order_chaos", "open_hidden", "physical_mystical", "headon_stealth", "credits_xp")


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=True, separators=(",", ":"), allow_nan=False)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def require(ok, message):
    if not ok:
        raise ValueError(message)


def identity(value):
    return None if value is None else f"{value['type']:08X}:{value['instance_uint32']:08X}"


def csv_bytes(rows):
    stream = io.StringIO(newline="")
    if rows:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]), lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow({k: canonical(v) if isinstance(v, (list, dict)) else v for k, v in row.items()})
    return stream.getvalue().encode("utf-8")


def gzip_rows(rows):
    data = "".join(canonical(row) + "\n" for row in rows).encode("utf-8")
    return gzip.compress(data, mtime=0)


def md_table(headers, rows):
    def cell(value):
        if value is None:
            return "-"
        if isinstance(value, list):
            value = ", ".join(map(str, value))
        return str(value).replace("|", "\\|").replace("\n", " ")
    return "\n".join(["| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |"] +
                     ["| " + " | ".join(cell(c) for c in row) + " |" for row in rows]) + "\n"


def percent(count, total):
    return round(100 * count / total, 6) if total else None


def group_rows(rows, key):
    groups = defaultdict(list)
    for row in rows:
        groups[key(row)].append(row)
    return groups


def project():
    rows, metadata = extract(ROOT)
    rows.sort(key=lambda r: (r["session_id"], r["source_line"], r["request_id"] or "", r["cohort_id"] or "", r["offer_index"]))
    zone_bytes = subprocess.check_output(["git", "show", f"{EVIDENCE}:{ZONE_SOURCE}"], cwd=ROOT)
    zone_catalog = {r["playfield_id"]: r for r in json.loads(zone_bytes)}
    needed_pfs = {r["destination_playfield"] for r in rows if r["destination_identity"] is not None}
    needed_pfs.update(r["terminal_playfield"] for r in rows)
    zones = {}
    for pf in sorted(needed_pfs):
        entry = zone_catalog[pf]
        zones[pf] = entry["all_name"] or entry["tiny_name"]
        require(zones[pf], f"Missing zone name for {pf}")
    level_bytes = (ROOT / LEVEL_SOURCE).read_bytes()
    lookup = {int(r["Level"]): r for r in csv.DictReader(io.StringIO(level_bytes.decode("utf-8-sig")))}
    for index, row in enumerate(rows, 1):
        row["offer_row_id"] = index
        row["terminal_zone_name"] = zones[row["terminal_playfield"]]
        row["destination_zone_name"] = zones.get(row["destination_playfield"]) if row["destination_identity"] else None
        row["entrance_identity_hex"] = identity(row["destination_identity"])
        row["terminal_identity_hex"] = identity(row["terminal_identity"])
        require(row["current_expected_ql"] == int(lookup[row["character_level"]][f"Q{row['difficulty_detent'] - 1}"]), "Current QL lookup mismatch")
    resolved = [r for r in rows if r["destination_identity"] is not None]
    unresolved = [r for r in rows if r["destination_identity"] is None]
    keys = [(r["session_id"], r["source_line"], r["request_id"], r["cohort_id"], r["offer_index"]) for r in rows]
    require(len(keys) == len(set(keys)) == 93185, "Retained offer ownership mismatch")
    require(len(resolved) == 92830 and len(unresolved) == 355, "Resolution totals mismatch")
    require(len({r["entrance_identity_hex"] for r in resolved}) == 812, "Identity total mismatch")
    require(len({r["destination_playfield"] for r in resolved}) == 22, "Playfield total mismatch")

    def distribution(population, dimensions, group_id):
        exact = [r for r in population if r["destination_identity"] is not None]
        by_zone = group_rows(population, lambda r: r["destination_playfield"] if r["destination_identity"] else None)
        if not population:
            by_zone[None] = []
        result = []
        for pf, members in sorted(by_zone.items(), key=lambda kv: -1 if kv[0] is None else kv[0]):
            result.append({"group_id": group_id, **dimensions,
                           "status": "UNOBSERVED" if not members else "UNRESOLVED_RAW_MISSING" if pf is None else "OBSERVED",
                           "destination_playfield": pf, "destination_zone_name": zones.get(pf),
                           "offer_count": len(members), "retained_group_offers": len(population),
                           "resolved_group_offers": len(exact), "unresolved_group_offers": len(population) - len(exact),
                           "percent_of_resolved_offers": percent(len(members), len(exact)) if pf is not None else None,
                           "percent_of_retained_offers": percent(len(members), len(population)),
                           "unique_entrance_count": len({r["entrance_identity_hex"] for r in members if r["destination_identity"]}),
                           "entrance_identities": sorted({r["entrance_identity_hex"] for r in members if r["destination_identity"]}),
                           "source_offer_row_ids": [r["offer_row_id"] for r in members]})
        return result

    a_rows = []
    terminal_keys = sorted({(r["terminal_playfield"], r["terminal_identity_hex"]) for r in rows})
    by_a = group_rows(rows, lambda r: (r["character_level"], r["difficulty_detent"], r["terminal_playfield"], r["terminal_identity_hex"]))
    coverage = []
    for level, detent, (pf, terminal) in itertools.product(LEVELS, range(1, 12), terminal_keys):
        members = by_a[(level, detent, pf, terminal)]
        dimensions = {"character_level": level, "difficulty_detent": detent,
                      "slider_position_label": "EASY" if detent == 1 else "HARD" if detent == 11 else "CENTER" if detent == 6 else f"POSITION_{detent}",
                      "expected_ql": int(lookup[level][f"Q{detent - 1}"]),
                      "terminal_playfield": pf, "terminal_zone_name": zones[pf], "terminal_identity": terminal}
        gid = f"L{level}-D{detent}-T{pf}-{terminal}"
        a_rows.extend(distribution(members, dimensions, gid))
        coverage.append({"group_id": gid, **dimensions, "status": "OBSERVED" if members else "UNOBSERVED",
                         "retained_offers": len(members), "resolved_offers": sum(r["destination_identity"] is not None for r in members),
                         "unresolved_offers": sum(r["destination_identity"] is None for r in members)})
    b_rows, b_terminal_rows = [], []
    for ql, members in sorted(group_rows(rows, lambda r: r["current_expected_ql"]).items()):
        b_rows.extend(distribution(members, {"expected_ql": ql, "terminal_stratum": "ALL_OBSERVED_TERMINALS"}, f"Q{ql}"))
        for (pf, terminal), subset in sorted(group_rows(members, lambda r: (r["terminal_playfield"], r["terminal_identity_hex"])).items()):
            b_terminal_rows.extend(distribution(subset, {"expected_ql": ql, "terminal_playfield": pf,
                                                       "terminal_zone_name": zones[pf], "terminal_identity": terminal}, f"Q{ql}-T{pf}-{terminal}"))
    c_rows = sorted((dict(r) for r in a_rows if r["status"] == "OBSERVED"),
                    key=lambda r: (r["destination_playfield"], r["character_level"], r["difficulty_detent"], r["terminal_playfield"]))
    zone_rows = []
    for pf, members in sorted(group_rows(resolved, lambda r: r["destination_playfield"]).items()):
        qls = sorted({r["current_expected_ql"] for r in members})
        zone_rows.append({"destination_playfield": pf, "destination_zone_name": zones[pf],
                          "offer_count": len(members), "percent_of_all_resolved_offers": percent(len(members), len(resolved)),
                          "unique_entrance_count": len({r["entrance_identity_hex"] for r in members}),
                          "entrance_identities": sorted({r["entrance_identity_hex"] for r in members}),
                          "character_levels": sorted({r["character_level"] for r in members}),
                          "observed_expected_ql_min": min(qls), "observed_expected_ql_max": max(qls),
                          "observed_expected_ql_values": qls,
                          "original_ql_labels": sorted({r["original_expected_ql"] for r in members}),
                          "difficulty_detents": sorted({r["difficulty_detent"] for r in members}),
                          "terminal_playfields": sorted({r["terminal_playfield"] for r in members})})

    level_rows = []
    for level in LEVELS:
        members = [r for r in rows if r["character_level"] == level]
        exact = [r for r in members if r["destination_identity"] is not None]
        pfs = sorted({r["destination_playfield"] for r in exact})
        level_rows.append({"character_level": level, "retained_offers": len(members), "resolved_offers": len(exact),
                           "unresolved_offers": len(members) - len(exact), "terminal_playfields": sorted({r["terminal_playfield"] for r in members}),
                           "observed_difficulty_detents": sorted({r["difficulty_detent"] for r in members}),
                           "expected_ql_values": sorted({r["current_expected_ql"] for r in members}),
                           "destination_playfields": pfs, "destination_zone_names": [zones[pf] for pf in pfs]})

    # Pairwise set comparisons are descriptive, including shared-condition strata.
    overlaps = []
    def compare(ql, label, left, right, condition):
        def values(members, field):
            return {r[field] for r in members}
        lp, rp = values(left, "destination_playfield"), values(right, "destination_playfield")
        li, ri = values(left, "entrance_identity_hex"), values(right, "entrance_identity_hex")
        same_terminal = values(left, "terminal_identity_hex") == values(right, "terminal_identity_hex") and values(left, "terminal_playfield") == values(right, "terminal_playfield")
        overlaps.append({"expected_ql": ql, "comparison": label, "condition": condition,
                         "left_level": left[0]["character_level"], "right_level": right[0]["character_level"],
                         "left_offers": len(left), "right_offers": len(right),
                         "left_terminal_playfields": sorted(values(left, "terminal_playfield")),
                         "right_terminal_playfields": sorted(values(right, "terminal_playfield")),
                         "left_difficulty_detents": sorted(values(left, "difficulty_detent")),
                         "right_difficulty_detents": sorted(values(right, "difficulty_detent")),
                         "left_playfields": sorted(lp), "right_playfields": sorted(rp), "shared_playfields": sorted(lp & rp),
                         "left_only_playfields": sorted(lp - rp), "right_only_playfields": sorted(rp - lp),
                         "left_entrance_count": len(li), "right_entrance_count": len(ri), "shared_entrance_count": len(li & ri),
                         "left_only_entrance_count": len(li - ri), "right_only_entrance_count": len(ri - li),
                         "shared_entrance_identities": sorted(li & ri), "left_only_entrance_identities": sorted(li - ri),
                         "right_only_entrance_identities": sorted(ri - li),
                         "terminal_geography": "SAME_OBSERVED_TERMINAL" if same_terminal else "DIFFERENT_TERMINALS_CONFOUNDED",
                         "interpretation": "OBSERVED_SET_COMPARISON_NOT_PROVEN_LEVEL_EFFECT_OR_EXCLUSION",
                         "left_source_offer_row_ids": [r["offer_row_id"] for r in left],
                         "right_source_offer_row_ids": [r["offer_row_id"] for r in right]})
    for ql, members in sorted(group_rows(resolved, lambda r: r["current_expected_ql"]).items()):
        by_level = group_rows(members, lambda r: r["character_level"])
        for llevel, rlevel in itertools.combinations(sorted(by_level), 2):
            left, right = by_level[llevel], by_level[rlevel]
            compare(ql, "ALL_SECONDARY_SETTINGS", left, right, {})
            def strata(pop):
                return group_rows(pop, lambda r: (r["terminal_playfield"], r["terminal_identity_hex"], tuple(r["secondary_slider_bytes"])))
            ls, rs = strata(left), strata(right)
            for key in sorted(ls.keys() & rs.keys()):
                compare(ql, "SAME_TERMINAL_AND_SECONDARY_SETTINGS", ls[key], rs[key],
                        {"terminal_playfield": key[0], "terminal_identity": key[1], "secondary_slider_bytes": list(key[2])})

    entrance_rows = []
    for key, members in sorted(group_rows(resolved, lambda r: (r["character_level"], r["difficulty_detent"], r["current_expected_ql"],
                                                               r["terminal_playfield"], r["terminal_identity_hex"], r["destination_playfield"], r["entrance_identity_hex"])).items()):
        level, detent, ql, terminal_pf, terminal, dest_pf, entrance = key
        entrance_rows.append({"character_level": level, "difficulty_detent": detent, "expected_ql": ql,
                              "terminal_playfield": terminal_pf, "terminal_identity": terminal, "destination_playfield": dest_pf,
                              "destination_zone_name": zones[dest_pf], "entrance_identity": entrance, "offer_count": len(members),
                              "source_offer_row_ids": [r["offer_row_id"] for r in members]})

    # Every distribution view is independently a partition, never added to another view.
    for name, table, expected in (("A", a_rows, rows), ("B", b_rows, rows), ("B_terminal", b_terminal_rows, rows),
                                  ("C", c_rows, resolved), ("entrances", entrance_rows, resolved)):
        observed = [n for row in table for n in row["source_offer_row_ids"]]
        require(len(observed) == len(set(observed)) == len(expected), f"{name} duplicates or omits offers")
        require(set(observed) == {r["offer_row_id"] for r in expected}, f"{name} ownership mismatch")
    mismatch = [r for r in rows if r["original_expected_ql"] is not None and r["original_expected_ql"] != r["current_expected_ql"]]
    validation = {"result": "PASS", "retained_offers": len(rows), "resolved_offers": len(resolved), "unresolved_offers": len(unresolved),
                  "exact_entrance_identities": 812, "destination_playfields": 22, "source_files": len(metadata["sources"]),
                  "character_levels": list(LEVELS), "expected_ql_count": len({r["current_expected_ql"] for r in resolved}),
                  "observed_level_slider_terminal_combinations": sum(r["status"] == "OBSERVED" for r in coverage),
                  "unobserved_level_slider_terminal_combinations": sum(r["status"] == "UNOBSERVED" for r in coverage),
                  "ql_label_mismatch_offers": len(mismatch),
                  "original_static_ql_unrecorded_offers": sum(r["original_expected_ql"] is None for r in rows),
                  "source_validation": metadata["counts"],
                  "offer_ownership_per_view": {"A": "EXACT_ONCE_ALL_RETAINED", "B": "EXACT_ONCE_ALL_RETAINED",
                                              "B_terminal": "EXACT_ONCE_ALL_RETAINED", "C": "EXACT_ONCE_ALL_RESOLVED", "entrances": "EXACT_ONCE_ALL_RESOLVED"},
                  "runtime_generation_changed": False, "dao_changed": False, "captures_started": False,
                  "probability_or_eligibility_model_created": False}
    datasets = {"schema": "aorebirth.observed-mission-destination-distribution.v1", "evidence_classification": "OBSERVED",
                "expected_ql_basis": "CURRENT_ESTABLISHED_LOOKUP_NOT_DECODED_RESPONSE_QL", "validation": validation,
                "level_slider_zone_distribution": a_rows, "ql_zone_distribution": b_rows,
                "ql_zone_distribution_by_terminal": b_terminal_rows, "zone_level_slider_distribution": c_rows,
                "zone_summary": zone_rows, "level_summary": level_rows, "coverage": coverage, "overlap_comparisons": overlaps}
    manifest = {k: v for k, v in metadata.items() if k != "contexts"}
    manifest["zone_name_source"] = {"commit": EVIDENCE, "path": ZONE_SOURCE, "sha256": digest(zone_bytes)}
    manifest["zone_names"] = [{"playfield_id": pf, "display_name": zones[pf], "all_name": zone_catalog[pf]["all_name"],
                               "tiny_name": zone_catalog[pf]["tiny_name"], "name_conflict": zone_catalog[pf]["name_conflict"],
                               "selection": "PUBLIC_ALL_NAME"} for pf in sorted(zones)]
    manifest["expected_ql_lookup"] = {"path": LEVEL_SOURCE, "sha256": digest(level_bytes)}
    manifest["generator_sources"] = [{"path": f"Tools/{name}", "sha256": digest((ROOT / "Tools" / name).read_bytes())}
                                     for name in ("mission_destination_distribution.py", "mission_destination_distribution_sources.py")]

    files = {"distribution-data.json": (json.dumps(datasets, indent=2, ensure_ascii=True, allow_nan=False) + "\n").encode(),
             "source-manifest.json": (json.dumps(manifest, indent=2, ensure_ascii=True, allow_nan=False) + "\n").encode(),
             "validation.json": (json.dumps(validation, indent=2, sort_keys=True) + "\n").encode(),
             "offer-records.jsonl.gz": gzip_rows(rows),
             "offer-records.csv.gz": gzip.compress(csv_bytes(rows), mtime=0),
             "capture-contexts.jsonl.gz": gzip_rows([{"context_id": k, **v} for k, v in sorted(metadata["contexts"].items())]),
             "unresolved-offers.csv": csv_bytes(unresolved), "ql-label-differences.csv": csv_bytes(mismatch)}
    for name, table in (("level-slider-zones", a_rows), ("ql-zones", b_rows), ("ql-zones-by-terminal", b_terminal_rows),
                        ("zone-level-sliders", c_rows), ("zone-summary", zone_rows), ("level-summary", level_rows), ("coverage", coverage),
                        ("overlap-comparisons", overlaps), ("entrance-observations", entrance_rows)):
        files[name + ".csv"] = csv_bytes(table)

    intro = ("All tables describe retained observations, not eligibility rules or destination weights. "
             "Expected QL is calculated from the current established lookup; original capture labels remain in the offer ledger. "
             "Percentages use resolved offers in the stated group; CSV/JSON also provide retained-offer denominators including unresolved rows. "
             "Repeated destinations are counted as separate offers. Missing combinations are UNOBSERVED. "
             "A/B/C pool captured secondary-slider settings; those settings remain in the per-offer/context data, "
             "and matched-setting comparisons are separate. They are not fully controlled character-level comparisons. "
             "Exact identity lists and source offer row IDs are complete in the linked CSV and distribution-data.json.\n\n")
    a_md = ["# A. Character level -> slider -> destination zones\n\n", intro,
            "[Complete CSV](level-slider-zones.csv) | [Coverage grid](coverage.csv) | [Exact entrance observations](entrance-observations.csv)\n\n"]
    for level in LEVELS:
        a_md.append(f"## Character level {level}\n\n")
        for pf, terminal in terminal_keys:
            a_md.append(f"### Terminal: {zones[pf]} (PF{pf}), `{terminal}`\n\n")
            table = [r for r in a_rows if r["character_level"] == level and r["terminal_playfield"] == pf and r["terminal_identity"] == terminal]
            a_md.append(md_table(["Easy/Hard position (1-11)", "Expected QL", "Destination PF", "Destination zone / status", "Offers", "% resolved", "Exact entrances"],
                                 [[r["difficulty_detent"], r["expected_ql"], r["destination_playfield"], r["destination_zone_name"] or r["status"],
                                   r["offer_count"], r["percent_of_resolved_offers"], r["unique_entrance_count"]] for r in table]) + "\n")
    b_md = ["# B. Expected mission QL -> destination zone distribution\n\n", intro,
            "[Complete CSV with exact IDs](ql-zones.csv) | [Terminal-stratified CSV](ql-zones-by-terminal.csv)\n\n"]
    for ql in sorted({r["expected_ql"] for r in b_rows}):
        b_md.append(f"## Expected QL {ql}\n\n")
        b_md.append(md_table(["Destination PF", "Destination zone / status", "Offers", "% resolved", "% retained", "Exact entrances"],
                             [[r["destination_playfield"], r["destination_zone_name"] or r["status"], r["offer_count"],
                               r["percent_of_resolved_offers"], r["percent_of_retained_offers"], r["unique_entrance_count"]]
                              for r in b_rows if r["expected_ql"] == ql]) + "\n")
    c_md = ["# C. Destination zone -> observed character levels\n\n", intro,
            "[Complete CSV](zone-level-sliders.csv) | [Zone summaries and exact observed QL sets](zone-summary.csv)\n\n"]
    for zone in zone_rows:
        pf = zone["destination_playfield"]
        c_md.append(f"## {zone['destination_zone_name']} (PF{pf})\n\n")
        c_md.append(f"Observed expected QLs: {', '.join(map(str, zone['observed_expected_ql_values']))}. "
                    f"The min/max {zone['observed_expected_ql_min']}-{zone['observed_expected_ql_max']} is an observed envelope, not a proven eligible interval.\n\n")
        c_md.append(md_table(["Character level", "Easy/Hard position", "Expected QL", "Terminal zone (PF)", "Offers", "% of level/slider/terminal", "Exact entrances"],
                             [[r["character_level"], r["difficulty_detent"], r["expected_ql"], f"{r['terminal_zone_name']} ({r['terminal_playfield']})",
                               r["offer_count"], r["percent_of_resolved_offers"], r["unique_entrance_count"]] for r in c_rows if r["destination_playfield"] == pf]) + "\n")
    overlap_md = ["# Same expected QL across different character levels\n\n", intro,
                  "[Complete comparison CSV, including every shared and one-sided exact entrance identity](overlap-comparisons.csv)\n\n",
                  "Each row compares observed sets. A one-sided identity is UNOBSERVED in the other sample, not proven excluded. "
                  "Matching terminal and secondary settings still does not control breed/profession or prove level causation. "
                  "All captures are Omni; no faction comparison is available. "
                  "Terminal PF655 is Andromeda; PF800 is Borealis. Destination PF names are listed in the complete zone table in README.md.\n\n"]
    for kind in ("ALL_SECONDARY_SETTINGS", "SAME_TERMINAL_AND_SECONDARY_SETTINGS"):
        overlap_md.append(f"## {kind}\n\n")
        overlap_md.append(md_table(["QL", "Levels", "Offers left/right", "Terminal PF left/right", "Secondary bytes", "Shared PFs", "Left-only PFs", "Right-only PFs", "Entrances left/right/shared", "Geography"],
                                  [[r["expected_ql"], f"{r['left_level']}/{r['right_level']}", f"{r['left_offers']}/{r['right_offers']}",
                                    f"{r['left_terminal_playfields']}/{r['right_terminal_playfields']}", r["condition"].get("secondary_slider_bytes", "MIXED"),
                                    r["shared_playfields"], r["left_only_playfields"], r["right_only_playfields"],
                                    f"{r['left_entrance_count']}/{r['right_entrance_count']}/{r['shared_entrance_count']}", r["terminal_geography"]]
                                   for r in overlaps if r["comparison"] == kind]) + "\n")
    terminal_locations = []
    for (pf, terminal, coordinates), members in sorted(group_rows(rows, lambda r: (r["terminal_playfield"], r["terminal_identity_hex"], canonical(r["terminal_coordinates"]))).items()):
        terminal_locations.append([zones[pf], pf, terminal, coordinates, sorted({r["character_level"] for r in members}), len(members)])
    overview = ["# Observed mission destination-zone distribution\n\n",
                "Evidence reconstruction on `codex/mission-destination-integration`. Runtime repair `73fc97a75` is preserved.\n\n", intro,
                "## Reports and datasets\n\n",
                "- [A. Character level -> slider -> destination zones](A-character-level-slider-zones.md)\n",
                "- [B. Expected mission QL -> destination zone distribution](B-mission-ql-zone-distribution.md)\n",
                "- [C. Destination zone -> observed character levels](C-zone-character-levels.md)\n",
                "- [Overlapping-level comparisons](overlapping-levels.md)\n",
                "- [Complete aggregate JSON](distribution-data.json), [exact entrance observations CSV](entrance-observations.csv), [source manifest](source-manifest.json)\n",
                "- All 93,185 offers: [CSV gzip](offer-records.csv.gz), [JSON Lines gzip](offer-records.jsonl.gz). Both are UTF-8 and losslessly compressed.\n",
                "- [Original condition metadata JSON Lines gzip](capture-contexts.jsonl.gz), [unresolved offers](unresolved-offers.csv), [QL label differences](ql-label-differences.csv), [validation](validation.json)\n\n",
                "## Method and traceability\n\n",
                "Read all 77 original event journals, verify their pinned source hashes, pair each captured request/cohort, "
                "and validate original response packet bytes and exact offer boundaries with the existing offline decoder. "
                "The retained audit inventories are reconciliation controls, not the aggregation inputs. Destination PF/XYZ comes "
                "from raw WorldPos and joins the exact placement by PF and all three binary32 coordinate bits; the full entrance "
                "identity is the result of that proven placement join, not a guessed wire field. Missing raw packets stay unresolved.\n\n",
                "`source_offer_row_ids` indexes `offer_row_id` in either offer ledger. Each ledger row retains session, source line, "
                "request/cohort/offer index, original source-row and packet hashes, offer byte range, WorldPos offset and context ID. "
                "The source manifest identifies each logical capture and original file hash. `context_id` links complete sanitized "
                "original condition metadata; raw packet blobs and local absolute filesystem paths are not copied into this report. "
                "Exact entrance IDs use eight hexadecimal digits for both type and unsigned instance. Terminal location and "
                "destination location are separate fields throughout.\n\n",
                "Easy/Hard is the captured detent 1-11 (1 easiest, 6 center, 11 hardest); no invented physical slider percentage is assigned. "
                "Secondary slider bytes are preserved as captured, including raw center 255. Expected QL is not decoded live mission QL.\n\n",
                "Zone display names use the public Clicksaver `all_name`; original aliases remain in the manifest. "
                "PF635 also has the `tiny_name` alias Stret East Bank GOLD; that naming difference does not split its identity.\n\n",
                "## Original mission terminal locations\n\n",
                md_table(["Terminal zone", "Terminal PF", "Terminal identity", "Captured terminal XYZ", "Character levels", "Retained offers"], terminal_locations),
                "\nThese are original captured terminal snapshots, not destination coordinates or the later local Shade test. "
                "Request-time snapshots are used where recorded; older records retain their session snapshot provenance in the context data.\n\n",
                "## Complete observed level-to-zone summary\n\n",
                md_table(["Character level", "Resolved / unresolved offers", "Terminal PF", "Captured positions", "Destination zones"],
                         [[r["character_level"], f"{r['resolved_offers']} / {r['unresolved_offers']}", r["terminal_playfields"],
                           r["observed_difficulty_detents"], r["destination_zone_names"]] for r in level_rows]),
                "\n[Level summary CSV](level-summary.csv). Report A expands every level into every observed detent and zone.\n\n",
                "## Complete observed zone summary\n\n",
                md_table(["PF", "Destination zone", "Offers", "Entrances", "Character levels", "Observed QL min-max", "Exact observed QLs"],
                         [[r["destination_playfield"], r["destination_zone_name"], r["offer_count"], r["unique_entrance_count"], r["character_levels"],
                           f"{r['observed_expected_ql_min']}-{r['observed_expected_ql_max']}", r["observed_expected_ql_values"]] for r in zone_rows]),
                "\n## Validation and evidence gaps\n\n",
                f"- Retained offers: {len(rows):,}; raw-backed exact destinations: {len(resolved):,}; unresolved missing-raw offers: {len(unresolved):,}.\n",
                "- Exactly 812 observed entrance identities and 22 destination playfields. Every resolved offer occurs once in each relevant view; unresolved rows occur once in A/B and their ledger. Views are not added together.\n",
                "- Original source manifest: 77 logical sessions, 18,642 requests, 18,638 cohorts; one empty cohort and four requests without cohorts are retained in source/context evidence, not fabricated into offers.\n",
                "- Repeated entrances within or across cohorts remain distinct observations; source copies are not additional experiments.\n",
                f"- {len(mismatch)} offers have differing original/current expected-QL labels. Both labels are retained; see the complete difference CSV. This changes only report grouping, never runtime catalogs or historical records.\n",
                f"- {validation['original_static_ql_unrecorded_offers']} offers lack an original static-QL label. An operator planner target is retained separately and is not promoted to a decoded or static QL. Current expected QL is calculated only from the original level/detent and established lookup.\n",
                "- The 355 missing-raw offers have level2 in their original session headers. The old eligibility inventory lost that fallback because it inserted a null request-level key. This new extraction preserves the original level and records each historical metadata discrepancy in source-manifest.json; all 355 destinations remain unresolved.\n",
                "- Level35 is captured at Borealis (PF800); levels2/7/13/25/37 at Andromeda (PF655). Cross-terminal comparisons cannot isolate character level. Other metadata differences also remain confounders.\n",
                "- UNOBSERVED cells and one-sided sets are missing coverage, not exclusions. QL min/max does not establish eligibility of every intervening QL. Frequencies reflect this deliberately sampled corpus, not Funcom probabilities.\n",
                "- Original live response QL, independent faction controls and complete level/terminal/secondary controls remain absent. No additional captures were performed.\n",
                "- Runtime mission generation, DAO/schema, GameData, services and the AO client were not changed or restarted for this task.\n\n",
                "## Reproduce\n\n",
                "From the repository root, with the original retained captures available:\n\n",
                "```cmd\ncmd /d /c Tools\\mission_destination_distribution.cmd\ncmd /d /c Tools\\mission_destination_distribution.cmd --check\n```\n\n",
                "The check repeats original-source verification and requires every output byte to match. Source paths are resolved from the pinned manifest and retained repository journals; no live capture is started.\n"]
    for name, pieces in (("README.md", overview), ("A-character-level-slider-zones.md", a_md),
                         ("B-mission-ql-zone-distribution.md", b_md), ("C-zone-character-levels.md", c_md), ("overlapping-levels.md", overlap_md)):
        files[name] = ("".join(pieces).rstrip() + "\n").encode("utf-8")
    files["artifact-manifest.json"] = (json.dumps({"files": [{"path": name, "bytes": len(data), "sha256": digest(data)}
                                                              for name, data in sorted(files.items())]}, indent=2) + "\n").encode()
    return files, validation


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    files, validation = project()
    if not args.check:
        OUT.mkdir(parents=True, exist_ok=True)
    for name, data in files.items():
        path = OUT / name
        if args.check:
            require(path.is_file() and path.read_bytes() == data, f"Stale output: {name}")
        else:
            path.write_bytes(data)
    print(json.dumps({"mode": "check" if args.check else "generate", "output_files": len(files), "validation": validation}, sort_keys=True))


if __name__ == "__main__":
    main()
