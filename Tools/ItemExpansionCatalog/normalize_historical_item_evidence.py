"""Join cached historical research by exact IDs, preserving independent evidence axes."""
from __future__ import annotations

import argparse
import csv
import hashlib
import html
import io
import json
from pathlib import Path
import re
import urllib.error
import urllib.parse
import urllib.request

GUIDES = {
    "pocketboss-mechanism": "https://www.ao-universe.com/guides/shadowlands/tradeskill-guides-5/general-crafting-4/pocket-boss-guide",
    "ofab-content-origin": "https://www.ao-universe.com/guides/lost-eden/tradeskill-guides/armor/upgrading-ofab-armor",
}


def load(path: Path, default=None):
    return json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else default


def digest(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write(path: Path, value) -> None:
    temporary = path.with_name(path.name + ".partial")
    temporary.write_text(json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def lines(path: Path):
    if path.exists():
        with path.open(encoding="utf-8-sig") as stream:
            for line in stream:
                if line.strip():
                    yield json.loads(line)


def csv_rows(path: Path):
    raw = path.read_text(encoding="utf-8-sig")
    return list(csv.DictReader(io.StringIO("\n".join(x for x in raw.splitlines() if not x.startswith("#")))))


def version_key(version: str):
    return tuple(int(x) for x in re.findall(r"\d+", version))


def patch_timeline_key(row):
    date = row.get("BuildDate")
    if not date and len(row.get("ReleaseDates", [])) == 1:
        date = row["ReleaseDates"][0]
    return (not bool(date), date or "", version_key(row["Version"]), row["Version"])


def normalize(root: Path, fetch_guides: bool) -> None:
    baseline = load(root.parent.parent / "ClassificationSummary.json")
    if not baseline:
        raise ValueError("The previous classification baseline summary must exist and remains read-only.")
    sources: dict[str, dict] = {}
    assertions = []
    gaps = []
    observations: dict[str, dict] = {}

    def add_source(identifier, url, title="", sha=None, cache=None, status="AVAILABLE", date=None, scope=None):
        if not url:
            return None
        identifier = str(identifier or hashlib.sha256(url.encode()).hexdigest()[:20])
        value = {"Id": identifier, "Url": url, "Title": title or url, "Sha256": sha,
                 "CachePath": cache, "Status": status, "SourceDate": date, "Scope": scope}
        if identifier not in sources or (sha and not sources[identifier].get("Sha256")):
            sources[identifier] = value
        elif sha == sources[identifier].get("Sha256") and cache and not sources[identifier].get("CachePath"):
            sources[identifier]["CachePath"] = cache
        return identifier

    def add_observation(row):
        if not row.get("TemplateId") or not row.get("PatchVersion"):
            return
        key = json.dumps(row, sort_keys=True, separators=(",", ":"))
        observations[key] = row

    # All original source routes remain visible, including failures later recovered by another route.
    for row in load(root / "chronology/source-index.json", []):
        identifier = row.get("source_id")
        cache_file = row.get("cache_file")
        cache_path = "chronology/" + cache_file if cache_file else None
        status = "AVAILABLE" if row.get("sha256") and row.get("http_status") == 200 else row.get("status", "UNKNOWN")
        add_source(identifier, row.get("url"), row.get("title", row.get("source_kind", "")),
                   row.get("sha256"), cache_path, status,
                   scope=row.get("source_kind"))
        if row.get("status") == "UNAVAILABLE":
            gaps.append("Chronology source route unavailable: " + row.get("url", "") + "; " + str(row.get("error", "")))

    patches = {}
    for row in load(root / "chronology/patch-chronology.json", []):
        version = row["patch_version"]
        publisher = row.get("database_publisher_observations", [])
        urls = sorted({x["url"] for x in row.get("evidence", []) if x.get("url")})
        dates = sorted({str(x.get("date") or x.get("release_date")) for x in row.get("release_date_claims", [])
                        if x.get("date") or x.get("release_date")})
        if row.get("release_date"):
            dates = sorted(set(dates + [row["release_date"]]))
        counts = publisher[0] if publisher else {}
        patches[version] = {"Version": version, "DatabaseBuild": counts.get("database_build"),
                            "BuildDate": counts.get("database_build_date"), "ReleaseDates": dates,
                            "SourceUrls": urls, "Availability": "AVAILABLE" if row.get("notes_available") or publisher else "INDEX_ONLY",
                            "ItemChangesStatus": row.get("actual_item_diff_status", "UNRECOVERED"),
                            "NewTemplateCount": counts.get("templates_new"),
                            "ModifiedTemplateCount": counts.get("templates_modified"),
                            "DeletedTemplateCount": counts.get("templates_deleted"),
                            "ExplicitNoTemplateChanges": bool(row.get("zero_template_changes_reported") or row.get("zero_item_changes_proven")),
                            "Notes": ["Raw observed label may be an alias or series heading; not automatically a distinct release.",
                                      "Database build dates are not release dates.",
                                      "RELEASE_DATE_STATUS=" + row.get("release_date_status", "UNKNOWN"),
                                      "RELEASE_DATE_CLAIMS=" + json.dumps(row.get("release_date_claims", []), sort_keys=True),
                                      "EVIDENCE_SCOPES=" + json.dumps(sorted({x.get("source_kind", "UNKNOWN") for x in row.get("evidence", [])}), sort_keys=True)]}
        if len(dates) > 1 or "CONFLICT" in row.get("release_date_status", ""):
            gaps.append("Conflicting release-date evidence retained for patch " + version + ": " + json.dumps(dates))
    chronology_summary = load(root / "chronology/research-summary.json", {})
    if isinstance(chronology_summary, dict):
        for gap in chronology_summary.get("gaps", []):
            gaps.append("Chronology gap: " + json.dumps(gap, sort_keys=True))
    date_updates = load(root / "chronology/date-only-updates.json", [])
    if date_updates:
        gaps.append("Date-only update observations retained separately; not invented patch versions: " + json.dumps(date_updates, sort_keys=True))

    database = root / "databases"
    publisher_rows = load(database / "aoitems_patch_index.json", []) + load(database / "aoitems_patch_index_2023.json", [])
    for row in publisher_rows:
        identifier = add_source(None, row["sourceUrl"], "Archived AOItems publisher patch index",
                                row.get("sourceSha256"))
        version = row["patch"]
        patch = patches.setdefault(version, {"Version": version, "ReleaseDates": [], "SourceUrls": [], "Notes": []})
        patch.update(DatabaseBuild=row.get("build"), BuildDate=row.get("buildDate"),
                     NewTemplateCount=row.get("templateNew"), ModifiedTemplateCount=row.get("templateModified"),
                     DeletedTemplateCount=row.get("templateDeleted"), Availability="AVAILABLE",
                     ItemChangesStatus="PUBLISHER_SUMMARY_COUNTS")
        patch["SourceUrls"] = sorted(set(patch.get("SourceUrls", []) + [row["sourceUrl"]]))

    for filename in ("auno_item_patch_history.jsonl", "database_item_presence.jsonl", "database_item_diffs.jsonl",
                     "aoitems_item_patch_history.jsonl", "aoitems_item_changes.jsonl", "aoitems_patch_changes.jsonl", "aoitems_archive_item_changes.jsonl"):
        for row in lines(database / filename):
            if filename == "auno_item_patch_history.jsonl":
                row = dict(row, sourcePatchSemantics="DATABASE_ADDED_OR_IMPORT_TAG_NOT_PROVEN_EXACT_GAME_INTRODUCTION_PATCH")
            template = row.get("templateId", row.get("id"))
            patch = row.get("patch", row.get("laterSnapshot", row.get("version", "")))
            kind = row.get("kind", "UNKNOWN")
            if kind == "SNAPSHOT_FIELD_CHANGED":
                kind = "FIELD_CHANGED_BETWEEN_SNAPSHOTS"
            urls = [row["sourceUrl"]] if row.get("sourceUrl") else [x["sourceUrl"] for x in row.get("sources", [])]
            hashes = [row["sourceSha256"]] if row.get("sourceSha256") else [x["sourceSha256"] for x in row.get("sources", [])]
            for url, sha in zip(urls, hashes):
                add_source(None, url, "Historical item database evidence", sha, scope=filename)
            add_observation({"TemplateId": template, "PatchVersion": patch, "DatabaseBuild": row.get("build"),
                             "Kind": kind, "SourceUrls": sorted(set(urls)), "SourceHashes": sorted(set(hashes)),
                             "Confidence": row.get("confidence", "UNVERIFIED"), "Detail": row})

    for receipt_path in sorted((database / "cache").glob("*.receipt.json")):
        receipt = load(receipt_path, {})
        cache_file = receipt.get("cache_file")
        add_source(None, receipt.get("url"), "Historical database cached response", receipt.get("sha256"),
                   "databases/" + cache_file.replace("\\", "/") if cache_file else None,
                   "AVAILABLE" if receipt.get("sha256") and receipt.get("http_status") == 200 else "UNAVAILABLE",
                   scope="Exact HTTP response receipt; stable original URL retained instead of temporary signed redirect")

    checkpoint = load(database / "aoitems-detail-checkpoint.json", {})
    for row in checkpoint.get("patches", []):
        version = row["patch"]
        patch = patches.setdefault(version, {"Version": version, "ReleaseDates": [], "SourceUrls": [], "Notes": []})
        patch["ItemChangesStatus"] = "ARCHIVE_" + row.get("status", "UNKNOWN")
        missing = row.get("missingPages", [])
        recovered = {kind: value for kind, value in row.get("recovered", {}).items()}
        patch["Notes"].append("RECOVERED_ID_COUNTS=" + json.dumps(recovered, sort_keys=True))
        patch["Notes"].append("EXPECTED_SOURCE_COUNTS=" + json.dumps(row.get("expected", {}), sort_keys=True))
        if row.get("status") not in {"COMPLETE", "NO_TEMPLATE_CHANGES", "SKIPPED_ZERO_TEMPLATE_CHANGES"}:
            gaps.append("Archive patch " + version + ": status=" + row.get("status", "UNKNOWN")
                        + "; advertisedPages=" + str(row.get("totalPages"))
                        + "; missingPages=" + json.dumps(sorted({x["page"] for x in missing}))
                        + "; actual introduction/change values not inferred from unavailable pages.")
        errors = [row["error"]] if row.get("error") else []
        errors.extend(x.get("error", {}) for x in missing)
        for error in errors:
            if error.get("url"):
                add_source(None, error["url"], "Unavailable archived patch-detail route", status="UNAVAILABLE",
                           scope="Request result only, not proof the original patch had no changes")

    acquisition = {"sources": [], "records": [], "gaps": []}
    for folder in ("acquisition", "acquisition-extra"):
        study = load(root / folder / "research.json", {})
        acquisition["sources"].extend(dict(row, normalizedCachePath=folder + "/" + row.get("cache", ""))
                                      for row in study.get("sources", []))
        acquisition["records"].extend(study.get("records", []))
        acquisition["gaps"].extend(study.get("gaps", []))
    acquisition_sources = acquisition.get("sources", [])
    urls_to_ids = {}
    for row in acquisition_sources:
        identifier = add_source(row["id"], row["url"], row.get("title", ""), row.get("sha256"),
                                row["normalizedCachePath"], row.get("status", "UNKNOWN"), row.get("sourceUpdated"))
        urls_to_ids[row["url"]] = identifier
    for row in acquisition.get("records", []):
        urls = sorted(set(row.get("evidenceUrls", []) + ([row["identityUrl"]] if row.get("identityUrl") else [])))
        ids = {urls_to_ids[url] for url in urls if url in urls_to_ids}
        for source in row.get("acquisitionSources", []):
            ids.update(source.get("sourceIds", []))
        acq = []
        for source in row.get("acquisitionSources", []):
            source_urls = [sources[x]["Url"] for x in source.get("sourceIds", []) if x in sources]
            acq.append({"Kind": source.get("kind", "UNKNOWN"), "Location": source.get("location"),
                        "TemporalScope": source.get("temporalScope") or ("HISTORICAL_PLAYER_REPORT" if "HISTORICAL" in row.get("chestStatus", "")
                                         else "GUIDE_DOCUMENTATION_OBSERVATION"),
                        "Exclusive": source.get("exclusive", False), "SourceUrls": source_urls,
                        "Confidence": row.get("confidence", "UNVERIFIED")})
        assertions.append({"TemplateId": row["templateId"], "ContentAssociations": row.get("contentAssociations", []),
                           "AcquisitionSources": acq, "Roles": [row["role"]] if row.get("role") else [],
                           "ChestStatus": row.get("chestStatus", "NO_VERIFIED_CHEST_SOURCE"),
                           "Confidence": row.get("confidence", "UNVERIFIED"), "IdentityMatch": row.get("identityMatch", "UNVERIFIED"),
                           "SourceUrls": urls, "SourceIds": sorted(ids), "Reason": row.get("reason") or row.get("assertion") or row.get("chestEvidenceScope"),
                           "DocumentedQuality": row.get("documentedQuality"), "DocumentedMinimumQL": row.get("documentedMinimumQL"),
                           "DocumentedMaximumQL": row.get("documentedMaximumQL"),
                           "RetailChestAbsenceProven": row.get("retailChestAbsenceProven", False),
                           "DocumentedIntroductionPatch": row.get("documentedIntroductionPatch"),
                           "DocumentedIntroductionScope": row.get("documentedIntroductionScope")})
    gaps.extend(json.dumps(row, sort_keys=True) for row in acquisition.get("gaps", []))

    additional = load(database / "additional-snapshots/source-index.json", {})
    for row in additional.get("sources", []):
        add_source(None, row.get("url"), "Additional historical export discovery route", row.get("sha256"),
                   "databases/additional-snapshots/" + row["cache_file"] if row.get("cache_file") else None,
                   "AVAILABLE_NON_DATABASE_RESEARCH" if row.get("status") == "FETCHED" else "UNAVAILABLE",
                   scope="Archive/tool inspection only; no additional direct SQLite dataset was recovered")
    gaps.extend("Additional snapshot discovery: " + str(gap) for gap in additional.get("gaps", []))
    static_findings = database / "additional-snapshots/static-installer-content/FINDINGS.md"
    if static_findings.exists():
        gaps.append("Later static installer inspection qualification: " + static_findings.read_text(encoding="utf-8"))

    # Context guides validate mechanisms/categories; the linked CSVs supply exact identities, not name guesses.
    community = root / "community"
    for identifier, url in GUIDES.items():
        path = community / (identifier + ".html")
        if fetch_guides and not path.exists():
            try:
                request = urllib.request.Request(url, headers={"User-Agent": "AORebirth-Offline-Historical-Research/1.0"})
                with urllib.request.urlopen(request, timeout=25) as response:
                    raw = response.read(2_000_001)
                if len(raw) > 2_000_000:
                    raise ValueError("guide exceeds bounded cache size")
                path.write_bytes(raw)
            except (urllib.error.URLError, TimeoutError, ValueError) as error:
                gaps.append(f"Community mechanism guide not cached: {url}: {error}")
        readable = html.unescape(re.sub(r"<[^>]+>", " ", path.read_text(encoding="utf-8", errors="replace"))).lower() if path.exists() else ""
        markers = ("pocket boss guide", "shadowlands") if identifier == "pocketboss-mechanism" else ("upgrading ofab", "lost eden")
        relevant = path.exists() and all(marker in readable for marker in markers)
        if not relevant:
            gaps.append("Cached guide did not establish the expected acquisition mechanism: " + url)
        add_source(identifier, url, "Original acquisition mechanism guide", digest(path) if path.exists() else None,
                   str(path.relative_to(root)), "AVAILABLE" if relevant else "UNAVAILABLE_OR_IRRELEVANT")
    public_index = load(community / "source-index.json", {}).get("sources", [])
    tables = {}
    for source in public_index:
        if source.get("status") != "AVAILABLE":
            continue
        add_source(source["id"], source["browseUrl"], "Pinned Nadybot community data", source["sha256"],
                   "community/" + source["file"], "AVAILABLE", scope="Community-maintained table, not complete retail server loot")
        tables[source["id"]] = source
    aodb = tables.get("Modules_ITEMS_MODULE_aodb.csv")
    if aodb:
        for row in csv_rows(community / aodb["file"]):
            for template in sorted({int(row["lowid"]), int(row["highid"])}):
                add_observation({"TemplateId": template, "PatchVersion": "18.8.58.1", "DatabaseBuild": "18.08.58.01",
                                 "Kind": "COMMUNITY_DATABASE_PRESENCE", "SourceUrls": [aodb["browseUrl"]],
                                 "SourceHashes": [aodb["sha256"]], "Confidence": "CORROBORATED_COMMUNITY_DATABASE_MEMBERSHIP",
                                 "Detail": row})
    pocket = tables.get("Modules_IMPLANT_MODULE_pocketboss.csv")
    if pocket and sources["pocketboss-mechanism"]["Status"] == "AVAILABLE":
        for row in csv_rows(community / pocket["file"]):
            if not row.get("itemid", "").isdigit() or not row.get("pb"):
                continue
            urls = [pocket["browseUrl"], GUIDES["pocketboss-mechanism"]]
            assertions.append({"TemplateId": int(row["itemid"]), "ContentAssociations": ["Shadowlands"],
                "AcquisitionSources": [{"Kind": "ShadowlandsPocketBoss", "Location": row["pb"] + "; " + row.get("pb_location", ""),
                    "TemporalScope": "COMMUNITY_DROP_TABLE_SNAPSHOT", "Exclusive": False, "SourceUrls": urls,
                    "Confidence": "CORROBORATED_EXACT_ID_DROP_TABLE"}], "Roles": [], "ChestStatus": "NO_VERIFIED_CHEST_SOURCE",
                "Confidence": "CORROBORATED_EXACT_ID_DROP_TABLE", "IdentityMatch": "EXACT_TEMPLATE_ID_IN_SOURCE_TABLE",
                "SourceUrls": urls, "SourceIds": [pocket["id"], "pocketboss-mechanism"],
                "Reason": "Explicit pocket-boss itemid plus original Shadowlands pocket-boss mechanism guide",
                "DocumentedQuality": int(row["ql"]) if row.get("ql", "").isdigit() else None})
    ofab = tables.get("Modules_ALIEN_MODULE_ofabarmor.csv")
    if ofab and sources["ofab-content-origin"]["Status"] == "AVAILABLE":
        for row in csv_rows(community / ofab["file"]):
            urls = [ofab["browseUrl"], GUIDES["ofab-content-origin"]]
            for template in sorted({int(row["lowid"]), int(row["highid"])}):
                assertions.append({"TemplateId": template, "ContentAssociations": ["LostEden"],
                    "AcquisitionSources": [{"Kind": "LostEdenVendor" if row["upgrade"] == "0" else "TradeskillUpgrade",
                        "Location": "Ofab armor catalog; upgrade stage " + row["upgrade"], "TemporalScope": "COMMUNITY_CATALOG_SNAPSHOT",
                        "Exclusive": False, "SourceUrls": urls, "Confidence": "CORROBORATED_EXACT_ID_CONTENT_CATALOG"}],
                    "Roles": [], "ChestStatus": "NO_VERIFIED_CHEST_SOURCE", "Confidence": "CORROBORATED_EXACT_ID_CONTENT_CATALOG",
                    "IdentityMatch": "EXACT_TEMPLATE_ID_IN_SOURCE_TABLE", "SourceUrls": urls,
                    "SourceIds": [ofab["id"], "ofab-content-origin"],
                    "Reason": "Typed Ofab armor catalog membership and explicit Lost Eden armor origin guide"})
    for table_name in ("Modules_ITEMS_MODULE_boss_lootdb.csv", "Modules_LOOT_MODULE_raid_loot.csv"):
        table = tables.get(table_name)
        if not table:
            continue
        unresolved = 0
        for row in csv_rows(community / table["file"]):
            if not row.get("aoid", "").isdigit():
                unresolved += 1
                continue
            assertions.append({"TemplateId": int(row["aoid"]), "ContentAssociations": [],
                "AcquisitionSources": [{"Kind": "DocumentedBossOrRaidDrop", "Location": row.get("raid", "bossid=" + row.get("bossid", "")),
                    "TemporalScope": "COMMUNITY_DROP_TABLE_SNAPSHOT", "Exclusive": False, "SourceUrls": [table["browseUrl"]],
                    "Confidence": "CORROBORATED_EXACT_ID_DROP_TABLE"}], "Roles": [], "ChestStatus": "NO_VERIFIED_CHEST_SOURCE",
                "Confidence": "CORROBORATED_EXACT_ID_DROP_TABLE", "IdentityMatch": "EXACT_TEMPLATE_ID_IN_SOURCE_TABLE",
                "SourceUrls": [table["browseUrl"]], "SourceIds": [table["id"]],
                "Reason": "Explicit aoid only; rows lacking identities are not joined by name"})
        gaps.append(f"{table_name}: {unresolved} rows lack exact aoid; not promoted through ambiguous name joins.")

    # Use only explicit source build mappings (including documented trailing-zero label aliases)
    # to order legacy labels such as 13.21/build13.2.1 correctly.
    builds_by_alias = {}
    def alias(version):
        parts = version_key(version)
        while len(parts) > 1 and parts[-1] == 0:
            parts = parts[:-1]
        return parts
    for patch in patches.values():
        if patch.get("DatabaseBuild"):
            builds_by_alias.setdefault(alias(patch["Version"]), set()).add(patch["DatabaseBuild"])
    for observation in observations.values():
        known_builds = builds_by_alias.get(alias(observation["PatchVersion"]), set())
        if not observation.get("DatabaseBuild") and len(known_builds) == 1:
            observation["DatabaseBuild"] = next(iter(known_builds))

    # An exact primary initial-database tag supports limited base-client membership.
    # Independent roster matches strengthen confidence; they are not an invented universal two-source gate.
    initial_auno = {row["templateId"] for row in lines(database / "auno_item_patch_history.jsonl")
                    if row.get("patch") == "11.0" and row.get("kind") == "ADDED_IN_DATABASE_PATCH"}
    def is_aoitems_initial_roster(url):
        decoded = urllib.parse.unquote(url)
        if urllib.parse.urlsplit(decoded).hostname != "web.archive.org" or "/http" not in decoded:
            return False
        target = urllib.parse.urlsplit("http" + decoded.split("/http", 1)[1])
        return target.hostname in {"aoitems.com", "www.aoitems.com"} and target.path.rstrip("/") == "/history/info/11.0"
    initial_archive = {row["TemplateId"] for row in observations.values()
                       if row["Kind"] == "ADDED_IN_DATABASE_PATCH" and row["PatchVersion"] == "11.0"
                       and any(is_aoitems_initial_roster(url) for url in row["SourceUrls"])}
    initial_by_template = {}
    for row in observations.values():
        if row["Kind"] == "ADDED_IN_DATABASE_PATCH" and row["PatchVersion"] == "11.0":
            initial_by_template.setdefault(row["TemplateId"], []).append(row)
    for template in sorted(initial_auno):
        related = initial_by_template[template]
        urls = sorted({url for row in related for url in row["SourceUrls"]})
        ids = [add_source(None, url, "Independent initial base-client database roster") for url in urls]
        independently_matched = template in initial_archive
        assertions.append({"TemplateId": template, "ContentAssociations": ["RubiKa"], "AcquisitionSources": [], "Roles": [],
            "ChestStatus": "NO_VERIFIED_CHEST_SOURCE",
            "Confidence": "CORROBORATED_INDEPENDENT_INITIAL_ROSTER_INTERSECTION" if independently_matched else "CORROBORATED_PRIMARY_INITIAL_BASE_DATABASE_TAG",
            "IdentityMatch": "EXACT_TEMPLATE_ID_INTERSECTION_OF_INDEPENDENT_INITIAL_ROSTERS" if independently_matched else "EXACT_TEMPLATE_ID_IN_PRIMARY_INITIAL_DATABASE_ROSTER",
            "SourceUrls": urls, "SourceIds": ids,
            "Reason": "Limited initial base-client dataset association from the primary11.0roster; stronger independent identity corroboration where available. NOT authenticated retail-release build, first introduction date, current unchanged identity, obtainable RK content or chest eligibility"})
    if len(initial_archive) < 26502:
        gaps.append(f"Initial AOItems11.0 exact roster incomplete: {len(initial_archive)}/26502; the complete primaryAuno26502roster retains primary-source confidence, while only exact intersections receive stronger independent-identity confidence. Neither establishes retail obtainability.")
    gaps.extend(["Retail original CD build identity remains unverified; build11.0 date2001-06-01 is not the retail release date.",
                 "Auno18.8.55 Added342 versus AOItems18.8.55 New0 and18.8.54 New343 demonstrates coarse database-import patch tags; first-recorded tags are not automatically actual game introduction patches.",
                 "Dates and post-expansion patch membership never determine expansion origin.",
                 "Snapshot absence/deletion of a client template is not proof all live copies were retired or unavailable.",
                 "Historical requirement/stat values, replacement-ID relationships and acquisition transfers require explicit per-change evidence; index modification counts alone cannot reconstruct them.",
                 "Community froob-friendly/in_game flags are availability observations, not content origin or random-chest eligibility."])
    assertion_keys = {json.dumps(row, sort_keys=True): row for row in assertions}
    bundle = {"FormatVersion": 1, "ItemsDatSha256": baseline["ItemsDatSha256"],
              "Sources": sorted(sources.values(), key=lambda row: row["Id"]),
              "Patches": sorted(patches.values(), key=patch_timeline_key),
              "Assertions": sorted(assertion_keys.values(), key=lambda row: (row["TemplateId"], json.dumps(row, sort_keys=True))),
              "Gaps": sorted(set(gaps))}
    for source in bundle["Sources"]:
        if source.get("CachePath") and source.get("Sha256"):
            path = (root / source["CachePath"]).resolve()
            if not path.is_relative_to(root) or not path.is_file() or digest(path) != source["Sha256"]:
                raise ValueError("Cached source path/hash does not match its research receipt: " + source["Id"])
    write(root / "HistoricalEvidence.json", bundle)
    observation_path = root / "ItemObservations.jsonl"
    temporary = observation_path.with_name(observation_path.name + ".partial")
    with temporary.open("w", encoding="utf-8", newline="\n") as stream:
        for row in sorted(observations.values(), key=lambda row: (row["TemplateId"], version_key(row["PatchVersion"]), row["Kind"], json.dumps(row, sort_keys=True))):
            stream.write(json.dumps(row, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n")
    temporary.replace(observation_path)
    write(root / "normalization-checkpoint.json", {"status": "COMPLETE_FOR_FROZEN_AVAILABLE_INPUTS", "sources": len(sources),
          "patchLabels": len(patches), "assertions": len(assertion_keys), "observations": len(observations),
          "evidenceSha256": digest(root / "HistoricalEvidence.json"), "observationsSha256": digest(observation_path)})
    print(json.dumps({"sources": len(sources), "patchLabels": len(patches), "assertions": len(assertion_keys),
                      "observations": len(observations), "initialRosterIntersection": len(initial_auno & initial_archive)}, sort_keys=True))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--research-root", type=Path, required=True)
    parser.add_argument("--fetch-guides", action="store_true")
    arguments = parser.parse_args()
    normalize(arguments.research_root.resolve(), arguments.fetch_guides)
