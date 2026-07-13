import csv
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
GENERATED = ROOT / "docs" / "generated" / "architecture"
ARCHITECTURE = ROOT / "docs" / "architecture"


def load_json(name):
    with (GENERATED / name).open("r", encoding="utf-8") as handle:
        return json.load(handle)


def load_csv(name):
    with (GENERATED / name).open("r", encoding="utf-8", newline="") as handle:
        return list(csv.DictReader(handle))


def require_paths(paths):
    missing = [path for path in paths if not (ROOT / path).exists()]
    if missing:
        raise AssertionError("Missing cited paths: " + ", ".join(missing))


def main():
    expected_docs = [
        "AO_REBIRTH_FULL_CODEBASE_AUDIT.md",
        "AO_REBIRTH_TARGET_ARCHITECTURE.md",
        "AO_REBIRTH_SUBSYSTEM_ROADMAP.md",
        "AO_REBIRTH_REMOVAL_PLAN.md",
        "AO_REBIRTH_LOOT_ARCHITECTURE.md",
        "AO_REBIRTH_WORLD_POPULATION_ARCHITECTURE.md",
        "AO_REBIRTH_DYNA_ARCHITECTURE.md",
        "AO_REBIRTH_TEST_GAP_ANALYSIS.md",
        "AO_REBIRTH_RISK_REGISTER.md",
    ]
    for name in expected_docs:
        if not (ARCHITECTURE / name).is_file():
            raise AssertionError("Missing architecture document: " + name)

    inventory = load_json("subsystem_inventory.json")
    weak_points = load_json("weak_points.json")
    graph = load_json("dependency_graph.json")
    backlog = load_csv("implementation_backlog.csv")
    removals = load_csv("removal_candidates.csv")

    subsystems = inventory["subsystems"]
    names = [row["name"] for row in subsystems]
    if len(names) != len(set(names)):
        raise AssertionError("Duplicate subsystem name")
    required_inventory = {
        "name", "owner", "primaryFiles", "responsibilities", "inputs", "outputs",
        "stateOwned", "persistenceOwned", "packetResponsibilities",
        "lifecycleResponsibilities", "tests", "knownEvidence", "knownWeaknesses",
        "duplicateOwners", "missingBehavior", "scalingRisk", "disposition", "priority"
    }
    for row in subsystems:
        missing = required_inventory.difference(row)
        if missing:
            raise AssertionError("Inventory fields missing for %s: %s" % (row["name"], sorted(missing)))
        require_paths(row["primaryFiles"])

    finding_ids = [row["id"] for row in weak_points["findings"]]
    if len(finding_ids) != len(set(finding_ids)):
        raise AssertionError("Duplicate finding ID")

    backlog_ids = [row["Backlog ID"] for row in backlog]
    if len(backlog_ids) != len(set(backlog_ids)):
        raise AssertionError("Duplicate backlog ID")
    for row in backlog:
        if not row["Subsystem"] or not row["Validation"]:
            raise AssertionError("Backlog item lacks subsystem or validation: " + row["Backlog ID"])

    for row in removals:
        if not row["Replacement owner"] and not row["Reason"]:
            raise AssertionError("Removal lacks replacement or reason: " + row["Candidate ID"])

    nodes = set(graph["nodes"])
    for edge in graph["edges"]:
        if edge["from"] not in nodes or edge["to"] not in nodes:
            raise AssertionError("Dependency graph edge references unknown node")

    require_paths([
        "AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs",
        "AORebirth/Server/ZoneEngine/Core/Playfields/OrdinaryEnemyRuntimeService.cs",
        "AORebirth/Server/ZoneEngine/Core/Playfields/PlayfieldVisibilityInterestRuntimeService.cs",
        "AORebirth/Server/ZoneEngine/Core/Playfields/CapturedAreteRobotSpawnOrchestrator.cs",
        "AORebirth/Libraries/Source/AORebirth.Database/Dao/MobDroptableDao.cs",
        "docs/generated/enemy_catalog/sources/dyna_boss_list_1.normalized.json",
    ])
    print("PASS")


if __name__ == "__main__":
    main()
