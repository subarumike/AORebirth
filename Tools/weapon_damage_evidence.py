from __future__ import print_function

import datetime
import json
import os
import shutil
import subprocess
import sys


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
BASE = os.path.join(ROOT, ".local", "weapon-damage-evidence")
SCHEMA_VERSION = "1.0"


PRIMARY_WEAPON = {
    "templateIdentity": "121567",
    "name": "Solar-Powered Pistol",
    "qualityLevel": 1,
    "minimum": 2,
    "maximum": 18,
    "legacyDamageBonus": 18,
    "rawDamageType": 90,
    "damageType": "Projectile",
    "attackSkills": [{"statId": 112, "percentage": 100}],
    "amsCapDependency": "absent in audited template; not part of this campaign",
}

OPTIONAL_WEAPON = {
    "templateIdentity": "121565",
    "name": "Worn Oak Bo",
    "qualityLevel": 1,
    "minimum": 6,
    "maximum": 24,
    "legacyDamageBonus": 18,
    "rawDamageType": 91,
    "damageType": "Melee",
    "attackSkills": [{"statId": 100, "percentage": 100}],
    "amsCapDependency": "absent in audited template; not part of this campaign",
}

TARGET_FIXTURE = {
    "identity": "Malfunctioning Cleaning Robot",
    "playfield": "Arete / private-server controlled low-level area",
    "requirements": [
        "single isolated target",
        "no other attackers on target",
        "no reflect, absorb, damage shield, proc, nano, DoT, environmental damage, healing, or regeneration during the hit interval",
        "diagnostic row must include targetMatchingArmor",
    ],
}

MATRIX = [
    {
        "id": "A",
        "purpose": "base-roll distribution and observed min/max boundaries",
        "weapon": PRIMARY_WEAPON["templateIdentity"],
        "attackerStats": "same character, same weapon skill below 1000, no buffs, no Add All Off changes",
        "targetAC": "same isolated target and diagnostic targetMatchingArmor",
        "uncontrolled": "baseRoll",
        "minimumObservations": 12,
        "logs": ["raw/server-weapon-damage-events.jsonl", "ZoneEngineLog.txt reference"],
        "distinguishes": ["base roll range", "hidden fixed modifiers"],
        "acceptance": "all rows KnownNormal, health delta matches damage, no target health discontinuity",
    },
    {
        "id": "B",
        "purpose": "AR scaling and integer truncation below 1000",
        "weapon": PRIMARY_WEAPON["templateIdentity"],
        "attackerStats": "same character with one controlled weapon-skill value change below 1000",
        "targetAC": "same isolated target AC",
        "uncontrolled": "baseRoll",
        "minimumObservations": 6,
        "logs": ["diagnostic attackSkillValues and effectiveAttackRating"],
        "distinguishes": ["AR-A", "AR-B", "AR-C stage truncation"],
        "acceptance": "effectiveAttackRating differs and every row is otherwise complete",
    },
    {
        "id": "C",
        "purpose": "AC divisor and subtraction order",
        "weapon": PRIMARY_WEAPON["templateIdentity"],
        "attackerStats": "same character and weapon skill",
        "targetAC": "two known targetMatchingArmor values from diagnostics",
        "uncontrolled": "baseRoll",
        "minimumObservations": 6,
        "logs": ["diagnostic targetMatchingArmor"],
        "distinguishes": ["AC-A", "AC-B", "AC-C", "AC-D"],
        "acceptance": "target AC changes while weapon/AR remain stable",
    },
    {
        "id": "D",
        "purpose": "minimum-damage floor behavior",
        "weapon": PRIMARY_WEAPON["templateIdentity"],
        "attackerStats": "same character and weapon skill",
        "targetAC": "high enough diagnostic targetMatchingArmor to force floor candidates",
        "uncontrolled": "baseRoll",
        "minimumObservations": 8,
        "logs": ["diagnostic baseRoll, health before/after"],
        "distinguishes": ["floor before AC", "floor after AC"],
        "acceptance": "repeated low-end rows without health discontinuity",
    },
    {
        "id": "E",
        "purpose": "integer division boundary rows",
        "weapon": PRIMARY_WEAPON["templateIdentity"],
        "attackerStats": "weapon skill values chosen near AR/400 truncation boundaries",
        "targetAC": "same isolated target AC",
        "uncontrolled": "baseRoll",
        "minimumObservations": 6,
        "logs": ["diagnostic baseRoll and effectiveAttackRating"],
        "distinguishes": ["stage truncation before/after multiply-add"],
        "acceptance": "rows land near candidate divergence values",
    },
]


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def run_git(args):
    p = subprocess.Popen(["git"] + args, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    out, err = p.communicate()
    if p.returncode != 0:
        raise RuntimeError((err or out).decode("utf-8", "replace").strip())
    return out.decode("utf-8", "replace").strip()


def session_path(session_id):
    if not session_id or any(c in session_id for c in "\\/:*?\"<>|"):
        raise SystemExit("Invalid --session-id. Use a simple id such as first-normal-hit-001.")
    return os.path.join(BASE, session_id)


def ensure_dirs(path):
    for child in ["raw", "observations", "reports", "commands"]:
        d = os.path.join(path, child)
        if not os.path.isdir(d):
            os.makedirs(d)


def write_json(path, data):
    with open(path, "w") as f:
        json.dump(data, f, indent=2, sort_keys=True)
        f.write("\n")


def read_json(path):
    with open(path, "r") as f:
        return json.load(f)


def parse_args(argv):
    if len(argv) < 2:
        raise SystemExit("Usage: weapon_damage_evidence.cmd <prepare|status|finish|analyze|self-test> --session-id <ID>")
    command = argv[1]
    session_id = None
    force = False
    i = 2
    while i < len(argv):
        if argv[i] == "--session-id" and i + 1 < len(argv):
            session_id = argv[i + 1]
            i += 2
        elif argv[i] == "--force":
            force = True
            i += 1
        else:
            raise SystemExit("Unknown argument: " + argv[i])
    return command, session_id, force


def make_manifest(session_id):
    return {
        "schemaVersion": SCHEMA_VERSION,
        "sessionId": session_id,
        "createdUtc": utc_now(),
        "commit": run_git(["rev-parse", "HEAD"]),
        "branch": run_git(["branch", "--show-current"]),
        "environmentClassification": "PrivateServerControlled",
        "outcome": "operator-pending",
        "primaryWeapon": PRIMARY_WEAPON,
        "optionalConfirmationWeapon": OPTIONAL_WEAPON,
        "targetFixture": TARGET_FIXTURE,
        "normalHitProofMethod": "AORebirth server-side AttackInfoHitType must equal NormalAttackInfoHitType and diagnostic hitKind must be KnownNormal; low damage alone is not proof.",
        "excludedVariables": [
            "critical hits",
            "Add All Off ordering",
            "type-specific add damage",
            "universal add damage",
            "post-1000 AR scaling",
            "AMSCap behavior",
            "special attacks",
            "PvP",
            "reflect",
            "absorbs",
            "damage shields",
            "nanos",
            "perks",
            "procs",
        ],
        "diagnosticFields": [
            "attackerIdentity",
            "targetIdentity",
            "weaponTemplateIdentity",
            "weaponMinimum",
            "weaponMaximum",
            "rawDamageType",
            "attackSkillDefinitions",
            "attackSkillValues",
            "effectiveAttackRating",
            "addAllOff",
            "targetMatchingArmor",
            "selectedProductionStrategy",
            "baseRoll",
            "observedDamage",
            "targetHealthBefore",
            "targetHealthAfter",
            "candidate predictions generated by analyze",
        ],
        "missingDiagnosticFields": [
            "independent live-client critical flag outside server AttackInfoHitType",
            "external packet capture path until Mike runs AOSharp capture",
        ],
        "matrix": MATRIX,
    }


def write_operator_steps(path, session_id):
    text = """# First Ordinary Weapon-Hit Campaign Operator Steps

Session id: `{0}`

## Codex preparation

1. Run `cmd /d /c tools\\weapon_damage_evidence.cmd prepare --session-id {0}`.
2. Start diagnostics and engines with `cmd /d /c .local\\weapon-damage-evidence\\{0}\\commands\\start-session-engines.cmd`.
3. Check status with `cmd /d /c tools\\weapon_damage_evidence.cmd status --session-id {0}`.

## Mike client-side actions

1. Use one low-level private-server test character with no outside buffs, no damage modifiers, no reflect, no absorb, no damage shield, no proc, no nano damage, no DoT, and no pet.
2. Equip only QL1 Solar-Powered Pistol `121567` in the right hand for the primary matrix. Do not use Burst, Fling Shot, perks, nanos, or any special attack.
3. Go to an isolated `Malfunctioning Cleaning Robot` target. Stand so only that target is selected and no other player, pet, or NPC is attacking it.
4. Use ordinary auto attack only. Press attack once, then let single normal hits occur. Do not spam actions.
5. Collect 12 primary hits or stop when the target dies, whichever happens first.
6. If a critical, reflect/absorb/shield/proc/nano/DoT/environmental effect, heal, regeneration, second attacker, or ambiguous target switch occurs, stop and tell Codex. The analyzer will reject ambiguous rows; do not interpret packets manually.
7. Note only visible anomalies in plain language: wrong weapon, unexpected crit text, another attacker, target healed, target died early, or wrong target.

## Codex import and analysis

1. Run `cmd /d /c tools\\weapon_damage_evidence.cmd finish --session-id {0}`.
2. Disable diagnostics by restarting engines without session env if needed: `cmd /d /c .local\\weapon-damage-evidence\\{0}\\commands\\disable-session-engines.cmd`.
3. Run `cmd /d /c tools\\weapon_damage_evidence.cmd analyze --session-id {0}`.
4. Review `.local\\weapon-damage-evidence\\{0}\\reports\\parity-report.md`.
""".format(session_id)
    with open(os.path.join(path, "OPERATOR_STEPS.md"), "w") as f:
        f.write(text)


def write_command_files(path, session_id):
    start_path = os.path.join(path, "commands", "start-session-engines.cmd")
    disable_path = os.path.join(path, "commands", "disable-session-engines.cmd")
    with open(start_path, "w") as f:
        f.write("@echo off\r\n")
        f.write("set AO_REBIRTH_WEAPON_DAMAGE_EVIDENCE_SESSION={0}\r\n".format(session_id))
        f.write("set AO_REBIRTH_WEAPON_DAMAGE_EVIDENCE_DIR={0}\r\n".format(path))
        f.write("cmd /d /c restart-engines.cmd\r\n")
    with open(disable_path, "w") as f:
        f.write("@echo off\r\n")
        f.write("set AO_REBIRTH_WEAPON_DAMAGE_EVIDENCE_SESSION=\r\n")
        f.write("set AO_REBIRTH_WEAPON_DAMAGE_EVIDENCE_DIR=\r\n")
        f.write("cmd /d /c restart-engines.cmd\r\n")


def prepare(session_id, force):
    path = session_path(session_id)
    status = run_git(["status", "--short"])
    if status:
        raise SystemExit("Working tree is not clean; refusing to prepare evidence session.")
    if os.path.exists(path):
        if not force:
            raise SystemExit("Session already exists: " + path)
        shutil.rmtree(path)
    ensure_dirs(path)
    write_json(os.path.join(path, "manifest.json"), make_manifest(session_id))
    write_operator_steps(path, session_id)
    write_command_files(path, session_id)
    open(os.path.join(path, "raw", "server-weapon-damage-events.jsonl"), "a").close()
    print("prepared session " + session_id)
    print("start command: cmd /d /c .local\\weapon-damage-evidence\\{0}\\commands\\start-session-engines.cmd".format(session_id))


def status(session_id):
    path = session_path(session_id)
    manifest = read_json(os.path.join(path, "manifest.json"))
    raw_file = os.path.join(path, "raw", "server-weapon-damage-events.jsonl")
    observation_dir = os.path.join(path, "observations")
    raw_count = count_jsonl(raw_file)
    obs_count = len([x for x in os.listdir(observation_dir) if x.endswith(".json")]) if os.path.isdir(observation_dir) else 0
    print("session: " + manifest["sessionId"])
    print("commit: " + manifest["commit"])
    print("rawEvents: " + str(raw_count))
    print("observations: " + str(obs_count))


def finish(session_id):
    path = session_path(session_id)
    manifest_path = os.path.join(path, "manifest.json")
    manifest = read_json(manifest_path)
    manifest["finishedUtc"] = utc_now()
    write_json(manifest_path, manifest)
    print("finished session " + session_id)
    print("disable command: cmd /d /c .local\\weapon-damage-evidence\\{0}\\commands\\disable-session-engines.cmd".format(session_id))


def count_jsonl(path):
    if not os.path.exists(path):
        return 0
    count = 0
    with open(path, "r") as f:
        for line in f:
            if line.strip():
                count += 1
    return count


def load_events(path):
    events = []
    if not os.path.exists(path):
        return events
    with open(path, "r") as f:
        for index, line in enumerate(f, 1):
            if line.strip():
                event = json.loads(line)
                event["_line"] = index
                events.append(event)
    return events


def validate_event(event, previous_by_target):
    issues = []
    rejected = []
    if event.get("hitKind") != "KnownNormal":
        issues.append("hit kind is not KnownNormal")
    if event.get("attackInfoHitType") != 3:
        issues.append("attackInfoHitType is not normal")
    for field in ["weaponTemplateIdentity", "weaponMinimum", "weaponMaximum", "rawDamageType", "effectiveAttackRating", "targetMatchingArmor", "targetHealthBefore", "targetHealthAfter", "observedDamage", "baseRoll"]:
        if event.get(field) is None or event.get(field) == "":
            issues.append("missing " + field)
    if event.get("multipleDamageSourcesPossible") is True:
        issues.append("multiple damage sources possible")
    if event.get("externalDamagePossible") is True:
        issues.append("external damage possible")
    if isinstance(event.get("targetHealthBefore"), int) and isinstance(event.get("targetHealthAfter"), int) and isinstance(event.get("observedDamage"), int):
        if event["targetHealthBefore"] - event["targetHealthAfter"] != event["observedDamage"]:
            rejected.append("health delta mismatch")
    target = event.get("targetIdentity", "")
    if target in previous_by_target and previous_by_target[target] != event.get("targetHealthBefore"):
        issues.append("target health discontinuity or overlapping event")
    if isinstance(event.get("targetHealthAfter"), int):
        previous_by_target[target] = event["targetHealthAfter"]
    if rejected:
        return "rejected", rejected
    if issues:
        return "incomplete", issues
    return "valid", []


def to_observation(event, observation_id):
    return {
        "schemaVersion": SCHEMA_VERSION,
        "source": {
            "observationId": observation_id,
            "sourceKind": "PrivateServerControlled",
            "captureDate": event.get("timestampUtc", ""),
            "environment": "AORebirth controlled evidence session",
            "classification": "CONTROLLED_TEST_CONFIRMED",
        },
        "attacker": {
            "identity": event.get("attackerIdentity", ""),
            "category": "Player",
            "attackRating": event.get("effectiveAttackRating"),
            "addAllOff": event.get("addAllOff"),
            "temporaryOffensiveModifiers": 0,
            "typeSpecificAddDamage": 0,
            "universalAddDamage": 0,
            "attackSkills": parse_skill_values(event.get("attackSkillDefinitions", ""), event.get("attackSkillValues", "")),
        },
        "weapon": {
            "templateIdentity": str(event.get("weaponTemplateIdentity", "")),
            "instanceIdentity": "",
            "qualityLevel": event.get("weaponQualityLevel"),
            "minimum": event.get("weaponMinimum"),
            "maximum": event.get("weaponMaximum"),
            "legacyDamageBonus": event.get("legacyDamageBonus"),
            "criticalBonus": None,
            "rawDamageType": event.get("rawDamageType"),
            "damageType": event.get("mappedDamageType", ""),
            "amsCapPresent": False,
            "amsCap": None,
            "attackTime": None,
            "rechargeTime": None,
        },
        "target": {
            "identity": event.get("targetIdentity", ""),
            "category": "Npc",
            "matchingArmor": event.get("targetMatchingArmor"),
        },
        "hit": {
            "hitKind": event.get("hitKind"),
            "baseRoll": event.get("baseRoll"),
            "packetOrderComplete": event.get("packetOrderComplete", False),
            "criticalStateEvidencePresent": event.get("criticalStateEvidencePresent", False),
            "multipleDamageSourcesPossible": event.get("multipleDamageSourcesPossible", True),
            "externalDamagePossible": event.get("externalDamagePossible", True),
        },
        "result": {
            "observedDamage": event.get("observedDamage"),
            "targetHealthBefore": event.get("targetHealthBefore"),
            "targetHealthAfter": event.get("targetHealthAfter"),
        },
        "evidence": {
            "packetReferences": [event.get("evidenceReference", "server diagnostic")],
            "logReferences": ["raw/server-weapon-damage-events.jsonl line " + str(event.get("_line", ""))],
            "timingReference": event.get("timestampUtc", ""),
            "uncertainties": ["private-server controlled diagnostic; not live AO formula proof"],
        },
    }


def parse_skill_values(definitions, values):
    value_map = {}
    for part in (values or "").split(","):
        if ":" in part:
            k, v = part.split(":", 1)
            try:
                value_map[int(k)] = int(v)
            except ValueError:
                pass
    skills = []
    for part in (definitions or "").split(","):
        if ":" in part:
            k, pct = part.split(":", 1)
            try:
                stat_id = int(k)
                skills.append({"statId": stat_id, "percentage": int(pct), "value": value_map.get(stat_id)})
            except ValueError:
                pass
    return skills


def schema_check(observation):
    for key in ["schemaVersion", "source", "attacker", "weapon", "target", "hit", "result", "evidence"]:
        if key not in observation:
            return False
    return observation["schemaVersion"] == SCHEMA_VERSION and bool(observation["source"].get("observationId"))


def evaluate_candidates(observations):
    candidate_names = [
        ("AR-A_AC-A", "A", "A"),
        ("AR-B_AC-A", "B", "A"),
        ("AR-A_AC-B", "A", "B"),
        ("AR-B_AC-B", "B", "B"),
        ("AR-A_AC-D", "A", "D"),
        ("AR-B_AC-D", "B", "D"),
    ]
    results = {}
    for name, ar_kind, ac_kind in candidate_names:
        matches = []
        for observation in observations:
            predicted = predict(observation, ar_kind, ac_kind)
            observed = observation["result"]["observedDamage"]
            matches.append(predicted == observed)
        results[name] = {"matchesEveryObservation": bool(matches) and all(matches), "matchCount": sum(1 for m in matches if m)}
    return results


def predict(observation, ar_kind, ac_kind):
    base = observation["hit"]["baseRoll"]
    ar = observation["attacker"]["attackRating"]
    ac = observation["target"]["matchingArmor"]
    min_damage = observation["weapon"]["minimum"]
    if ar_kind == "B":
        scaled = (base * (400 + ar)) // 400
        scaled_min = (min_damage * (400 + ar)) // 400
    else:
        scaled = base + ((base * ar) // 400)
        scaled_min = min_damage + ((min_damage * ar) // 400)
    reduction = ac // 10
    if ac_kind == "A":
        return max(scaled - reduction, scaled_min)
    if ac_kind == "B":
        return max(scaled, scaled_min) - reduction
    return scaled


def analyze(session_id):
    path = session_path(session_id)
    raw_path = os.path.join(path, "raw", "server-weapon-damage-events.jsonl")
    events = load_events(raw_path)
    previous_by_target = {}
    valid = []
    incomplete = []
    rejected = []
    for index, event in enumerate(events, 1):
        status, issues = validate_event(event, previous_by_target)
        if status == "valid":
            observation = to_observation(event, "session-{0}-hit-{1:03d}".format(session_id, index))
            if not schema_check(observation):
                incomplete.append({"line": event.get("_line"), "issues": ["schema check failed"]})
            else:
                valid.append(observation)
                write_json(os.path.join(path, "observations", observation["source"]["observationId"] + ".json"), observation)
        elif status == "rejected":
            rejected.append({"line": event.get("_line"), "issues": issues})
        else:
            incomplete.append({"line": event.get("_line"), "issues": issues})
    candidate_results = evaluate_candidates(valid)
    write_json(os.path.join(path, "reports", "analysis.json"), {
        "validObservationCount": len(valid),
        "incompleteObservationCount": len(incomplete),
        "rejectedObservationCount": len(rejected),
        "incomplete": incomplete,
        "rejected": rejected,
        "candidateResults": candidate_results,
    })
    write_report(path, valid, incomplete, rejected, candidate_results)
    print("valid: " + str(len(valid)))
    print("incomplete: " + str(len(incomplete)))
    print("rejected: " + str(len(rejected)))
    print("report: " + os.path.join(path, "reports", "parity-report.md"))


def write_report(path, valid, incomplete, rejected, candidate_results):
    unique = [name for name, value in candidate_results.items() if value["matchesEveryObservation"]]
    with open(os.path.join(path, "reports", "parity-report.md"), "w") as f:
        f.write("# Weapon Damage Evidence Session Report\n\n")
        f.write("- valid observations: {0}\n".format(len(valid)))
        f.write("- incomplete observations: {0}\n".format(len(incomplete)))
        f.write("- rejected observations: {0}\n\n".format(len(rejected)))
        f.write("## Candidate results\n\n")
        for name in sorted(candidate_results):
            f.write("- {0}: matchCount={1}, matchesEveryObservation={2}\n".format(name, candidate_results[name]["matchCount"], candidate_results[name]["matchesEveryObservation"]))
        f.write("\n## Conclusion\n\n")
        if len(valid) == 0:
            f.write("No valid proof observations exist yet. Outcome B remains operator-pending.\n")
        elif len(unique) == 1:
            f.write("Exactly one report-only candidate currently matches every valid observation: `{0}`. Do not activate it.\n".format(unique[0]))
        elif len(unique) > 1:
            f.write("Multiple report-only candidates still match. More distinguishing observations are required.\n")
        else:
            f.write("No report-only candidate matches every valid observation. Hidden modifiers or invalid assumptions are likely.\n")


def self_test():
    session_id = "selftest"
    path = session_path(session_id)
    if os.path.exists(path):
        shutil.rmtree(path)
    ensure_dirs(path)
    write_json(os.path.join(path, "manifest.json"), make_manifest(session_id))
    valid_event = {
        "schemaVersion": "1.0",
        "sessionId": session_id,
        "timestampUtc": utc_now(),
        "sourceKind": "PrivateServerControlled",
        "eventKind": "ordinary-weapon-hit",
        "attackerIdentity": "Player:1",
        "targetIdentity": "NPC:1",
        "weaponTemplateIdentity": "121567",
        "weaponHighId": 121567,
        "weaponQualityLevel": 1,
        "weaponMinimum": 2,
        "weaponMaximum": 18,
        "legacyDamageBonus": 18,
        "rawDamageType": 90,
        "mappedDamageType": "Projectile",
        "attackSkillDefinitions": "112:100",
        "attackSkillValues": "112:400",
        "effectiveAttackRating": 400,
        "addAllOff": 0,
        "targetMatchingArmor": 20,
        "hitKind": "KnownNormal",
        "attackInfoHitType": 3,
        "baseRoll": 10,
        "selectedProductionStrategy": "LegacyFallback",
        "observedDamage": 38,
        "targetHealthBefore": 100,
        "targetHealthAfter": 62,
        "multipleDamageSourcesPossible": False,
        "externalDamagePossible": False,
        "packetOrderComplete": True,
        "criticalStateEvidencePresent": True,
        "evidenceReference": "self-test",
    }
    cases = [
        valid_event,
        dict(valid_event, targetIdentity="NPC:2", hitKind="KnownCritical", targetHealthBefore=100, targetHealthAfter=62),
        dict(valid_event, targetIdentity="NPC:3", hitKind="UnknownHitKind", targetHealthBefore=100, targetHealthAfter=62),
        dict(valid_event, targetIdentity="NPC:4", effectiveAttackRating=None, targetHealthBefore=100, targetHealthAfter=62),
        dict(valid_event, targetIdentity="NPC:5", targetMatchingArmor=None, targetHealthBefore=100, targetHealthAfter=62),
        dict(valid_event, targetIdentity="NPC:6", observedDamage=99, targetHealthBefore=100, targetHealthAfter=62),
        dict(valid_event, targetIdentity="NPC:1", targetHealthBefore=70, targetHealthAfter=32),
    ]
    raw_path = os.path.join(path, "raw", "server-weapon-damage-events.jsonl")
    with open(raw_path, "w") as f:
        for case in cases:
            f.write(json.dumps(case, sort_keys=True) + "\n")
    analyze(session_id)
    analysis = read_json(os.path.join(path, "reports", "analysis.json"))
    assert analysis["validObservationCount"] == 1
    assert analysis["incompleteObservationCount"] == 5
    assert analysis["rejectedObservationCount"] == 1
    assert "AR-A_AC-A" in analysis["candidateResults"]
    shutil.rmtree(path)
    print("self-test PASS")


def main(argv):
    command, session_id, force = parse_args(argv)
    if command == "self-test":
        self_test()
        return 0
    if not session_id:
        raise SystemExit("--session-id is required")
    if command == "prepare":
        prepare(session_id, force)
    elif command == "status":
        status(session_id)
    elif command == "finish":
        finish(session_id)
    elif command == "analyze":
        analyze(session_id)
    else:
        raise SystemExit("Unknown command: " + command)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv))
    except Exception as exc:
        print("ERROR: " + str(exc), file=sys.stderr)
        sys.exit(1)
