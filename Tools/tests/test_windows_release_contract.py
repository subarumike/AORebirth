"""Focused offline contract regressions; no engines, clients or remote services."""
import copy
from concurrent.futures import ThreadPoolExecutor
from contextlib import redirect_stderr, redirect_stdout
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("windows_release_contract", ROOT / "tools/windows_release_contract.py")
gate = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(gate)
PUBLIC, DUMP = "a" * 40, "b" * 40


def make_generated_fixture(root, data=b"new generated fixture"):
    """Synthetic receipt exercises metadata guards; no real RDB/exporter is used."""
    root = Path(root)
    stage = root / "ignored-stage"
    stage.mkdir(exist_ok=True)
    (stage / "items.dat").write_bytes(data)
    database = root / "client/cd_image/data/db"
    database.mkdir(parents=True, exist_ok=True)
    inputs = []
    for name in ("ResourceDatabase.idx", "ResourceDatabase.dat", "ResourceDatabase.dat.001"):
        path = database / name
        path.write_bytes(("synthetic " + name).encode())
        state = gate.file_state(path)
        inputs.append({"path": name, "size": state[2], "BEFORE_STATE": state, "AFTER_STATE": state})
    inputs[0]["sha256"] = gate.digest((database / inputs[0]["path"]).read_bytes())
    output = root / gate.EXPORTER_OUTPUT
    output.mkdir(parents=True, exist_ok=True)
    for name in ("RDBDataExtractor.exe", "RDBDataExtractor.dll", "AODB.dll", "MsgPack.dll", "RDBDataExtractor.deps.json", "RDBDataExtractor.runtimeconfig.json", "FunctionSets.cfg"):
        (output / name).write_bytes(("synthetic output " + name).encode())
    binaries, _ = gate.inventory(output)
    binaries = [{**row, "path": gate.EXPORTER_OUTPUT + "/" + row["path"]} for row in binaries]
    assembly = next(row["sha256"] for row in binaries if row["path"].endswith("/RDBDataExtractor.dll"))
    validation = {"CURRENT_ITEMS_DAT_READER": "PASS", "ITEM_TEMPLATE_CATALOG": "DEFERRED_TO_REAL_WINDOWS_STARTUP",
        "EXPORTER_ASSEMBLY_SHA256": assembly, "ITEMS_DAT_SHA256": gate.digest(data), "ITEMS_DAT_SIZE": len(data),
        "HEADER": {"version": "RDBDataExtractor", "packCount": 10000, "templateCount": 3, "slices": 1,
                   "sliceStates": [{"index": 0, "compressedBytes": 10, "uncompressedBytes": 20}]},
        "SOURCE_POPULATION": {"itemRecords": 2, "nanoRecords": 1, "expected": 3}, "DECODED_POPULATION": 3,
        "DISTINCT_IDS": 3, "NAMED_TEMPLATES": 2, "UNNAMED_IDS": [3], "BOSS_MODIFIER_COUNT": 1,
        "BOSS_MODIFIERS": [{"id": 1, "name": "Boss Modifier Item Fixture"}],
        "CURRENT_FIELDS": {key: 3 if key == "fullRdb" else 0 if key == "positiveMultipleCount" else 1 for key in ("fullRdb", "stats", "attack", "defend",
            "eventsOrActions", "shops", "shopLines", "rawFallback", "nanoFields", "positiveAmmoType", "positiveMaxEnergy",
            "positiveMultipleCount", "negativeOneTickFunctions", "periodicFunctions")},
        "AURA_OF_REVIVAL_212161": [{"eventType": 1, "functionType": 1, "tickCount": -1, "tickInterval": 100}]}
    receipt = {"FORMAT_VERSION": 1, "PUBLIC_MASTER_SHA": PUBLIC, "CREATED_UTC": "2025-12-31T23:59:59Z",
        "EXPORTER_SOURCE_CLOSURE": [gate.content_identity(b"synthetic project", gate.EXPORTER_PROJECT)],
        "TOOL_BUILD": {"COMMAND": ["dotnet", *gate.EXPORTER_BUILD_ARGS], "STARTED_UTC": "2025-12-31T23:59:50Z",
            "FINISHED_UTC": "2025-12-31T23:59:52Z", "EXIT_CODE": 0, "BINARIES": binaries},
        "RDB_INPUTS": {"ROOT": str(database), "INDEX": inputs[0], "SHARDS": inputs[1:]},
        "EXPORT": {"COMMAND": [str(output / "RDBDataExtractor.exe"), "extract", "--ao-path", str(root / "client"),
            "--gamedata-dir", str(stage), *gate.EXPORTER_SKIP_ARGS], "STARTED_UTC": "2025-12-31T23:59:53Z",
            "FINISHED_UTC": "2025-12-31T23:59:58Z", "EXIT_CODE": 0, "OUTPUT_ABSENT_BEFORE": True},
        "OUTPUT": {"STAGED_PATH": str(stage / "items.dat"), **gate.content_identity(data)}, "VALIDATION": validation}
    path = root / "generated-receipt.json"
    gate.write_json(path, receipt)
    profile = {"CANONICAL_GAMEDATA_ROOT": str(root / "canonical")}
    with mock.patch.object(gate, "git", return_value=b"ignored"), mock.patch.object(gate, "exporter_source_closure", return_value=receipt["EXPORTER_SOURCE_CLOSURE"]):
        proof, _, _ = gate.generated_items_receipt(root, profile, path, PUBLIC)
    return path, receipt, proof


def make_portable_fixture(root, generated=False):
    """Synthetic two-file dataset and measured-looking fixture; never a real release."""
    root = Path(root)
    canonical = root / "canonical"
    canonical.mkdir(parents=True)
    (canonical / "catalog.json").write_bytes(b'{"upstream":1,"private":2}\n')
    (canonical / "items.dat").write_bytes(b"generated-fixture")
    rows, _ = gate.inventory(canonical)
    index = gate.validate_manifest(rows)
    bundle = root / "bundle"
    bundle.mkdir()
    gate.write_json(bundle / "before-manifest.json", rows)
    gate.write_json(bundle / "manifest.json", rows)
    snapshots = {authority: {"BASE_SHA": sha, "CURRENT_SHA": sha, "BEFORE": {}, "AFTER": {}}
                 for authority, sha in (("PRIVATE_CONTENT_UPSTREAM", DUMP), ("PUBLIC_PRODUCT_DATA", PUBLIC))}
    reconciliation = {"FORMAT_VERSION": 1, "PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP,
                      "CANONICAL_BEFORE_MANIFEST": index, "WINDOWS_GAMEDATA_MANIFEST": index,
                      "SOURCE_SNAPSHOTS": snapshots, "CHANGES": [], "PROTECTED_GENERATED_FILES": ["items.dat"],
                      "BEFORE_MANIFEST": gate.descriptor(bundle, bundle / "before-manifest.json"),
                      "CANONICAL_MANIFEST": gate.descriptor(bundle, bundle / "manifest.json")}
    gate.write_json(bundle / "reconciliation.json", reconciliation)
    summary = gate.summarize_reconciliation(bundle, reconciliation)
    if generated:
        _, _, proof = make_generated_fixture(root, (canonical / "items.dat").read_bytes())
        summary["GENERATED_ITEMS_PROVENANCE"] = proof
    gate.write_json(bundle / "reconciliation-summary.json", summary)
    binaries = []
    for path in gate.OUTPUTS:
        target = root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        data = ("synthetic output " + path).encode()
        target.write_bytes(data)
        binaries.append({"path": path, "bytes": len(data), "sha256": gate.digest(data)})
    attempt_id = "c" * 32
    attempt = {"FORMAT_VERSION": 1, "PUBLIC_MASTER_SHA": PUBLIC, "LOCAL_WINDOWS_SOURCE_SHA": PUBLIC,
               "CONTENT_DUMP_SHA": DUMP, "WINDOWS_GAMEDATA_MANIFEST": index, "ATTEMPT_ID": attempt_id,
               "ATTEMPT_STARTED_UTC": "2026-01-01T00:00:00Z", "CREATED_UTC": "2026-01-01T00:00:03Z",
               "PUBLIC_FETCH": {"ROLE": "PUBLIC_PRODUCT", "METHOD": "GIT_FETCH", "RESULT": "SUCCESS",
                                "TIP_SHA": PUBLIC, "ATTEMPT_ID": attempt_id, "FETCHED_AT_UTC": "2026-01-01T00:00:01Z"},
               "CONTENT_DUMP_FETCH": {"ROLE": "PRIVATE_CONTENT_UPSTREAM", "METHOD": "AUTHENTICATED_GITHUB_API",
                                      "RESULT": "SUCCESS", "TIP_SHA": DUMP, "ATTEMPT_ID": attempt_id,
                                      "FETCHED_AT_UTC": "2026-01-01T00:00:02Z"},
               "BUILD_STARTED_UTC": "2026-01-01T00:00:04Z", "BUILD_FINISHED_UTC": "2026-01-01T00:00:05Z",
               "WINDOWS_BUILD": "PASS", "WINDOWS_BUILD_EXIT_CODE": 0, "WINDOWS_USER_ACCEPTED": "NO",
               "APPROVED_BUILD_ENTRYPOINT": "NewZoneEngineBuild/build.cmd",
               "PRODUCT_COMPILER_HOOKS": ["ChatEngine.WinNet10", "LoginEngine.WinNet10", "WorldEngine", "ZoneEngine_New"],
               "BINARIES": binaries, "LAUNCHER_SHA256": gate.digest(b"synthetic launcher"),
               "RECONCILIATION": gate.descriptor(bundle, bundle / "reconciliation-summary.json"),
               "WINDOWS_CONTENT_VALIDATION": {"METHOD": "LOCAL_GUARDED_REPLAY_AND_WHOLE_TREE_MEASUREMENT",
                   "SUMMARY_SHA256": gate.digest(gate.encoded(summary)), "LOCAL_REPLAY_SHA256": summary["LOCAL_REPLAY_SHA256"],
                   "CANONICAL_MANIFEST": index, "VALIDATED_AT_UTC": "2026-01-01T00:00:05Z"},
               "CANONICAL_MANIFEST": gate.descriptor(bundle, bundle / "manifest.json")}
    gate.write_json(bundle / "build-attempt.json", attempt)
    held = []
    selected_root = "C:/canonical-fixture/GameData"
    for number, engine in enumerate(("ChatEngine", "LoginEngine", "WorldEngine", "ZoneEngine_New"), 100):
        exe = next(path for path in gate.OUTPUTS if path.endswith("/" + engine + ".exe"))
        held.append({"engine": engine, "pid": number, "executable": "C:/product-fixture/" + exe,
                     "expected_executable": "C:/product-fixture/" + exe, "handle_opened": True,
                     "start_time_utc": "2026-01-01T00:00:06Z"})
    ready = {"PUBLIC_MASTER_SHA": PUBLIC, "FAILURE": None, "HELD_PROCESSES": held,
             "COMMANDS": {"startup": {"exit_code": 0, "started_utc": "2026-01-01T00:00:06Z"},
                          "status": {"exit_code": 0}}}
    world = {"world": {"epoch": 123, "recovering": False,
                       "instances": [{"role": role, "connected": True, "heartbeatAgeMs": 1}
                                     for role in ("RoleChat", "RoleZone")]}}
    capture = bundle / "capture"
    capture.mkdir()
    gate.write_json(capture / "startup-ready.json", ready)
    gate.write_json(capture / "world-status.json", world)
    gate.write_json(capture / "world-status-observation.json", {"source": PUBLIC,
                    "requested_utc": "2026-01-01T00:00:07Z", "captured_utc": "2026-01-01T00:00:08Z",
                    "sha256": gate.digest((capture / "world-status.json").read_bytes())})
    (capture / "status.log").write_text("\n".join(f"engine={row['engine']} processPid={row['pid']} port=1 ownership=PASS" for row in held))
    (capture / "LoginEngine.out.full.log").write_text("[Info] LoginEngine GameData root: " + selected_root + " \n")
    (capture / "ZoneEngine_New.out.delta.log").write_text("[Info] GameData ready root=" + selected_root + " mobs=1 hashTemplates=1\n" +
        ("[Info] Loaded 3 item templates from " + selected_root + "/items.dat\n[Info] ItemTemplateCatalog ready with 3 templates\n" if generated else ""))
    evidence = {path.relative_to(bundle).as_posix(): gate.digest(path.read_bytes()) for path in capture.iterdir()}
    raw = {"FORMAT_VERSION": 1, "CREATED_UTC": "2026-01-01T00:00:09Z", "PUBLIC_MASTER_SHA": PUBLIC,
           "WINDOWS_PRODUCT_SOURCE_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP,
           "WINDOWS_GAMEDATA_MANIFEST": index, "WINDOWS_BUILD": "PASS", "WINDOWS_STARTUP": "PASS",
           "WORLD_RECOVERY_COMPLETE": True, "WORLD_EPOCH": 123, "WORLD_LEASE_TTL_MS": 15000,
           "WINDOWS_SOURCE_CLEAN": True, "WINDOWS_RUNTIME_DIFFS": 0, "WINDOWS_LOCAL_CHANGES": [],
           "BINARIES": binaries, "EVIDENCE": evidence}
    gate.write_json(bundle / "raw-windows-receipt.json", raw)
    windows = {**raw, "BUILD_ATTEMPT_RECEIPT": gate.descriptor(bundle, bundle / "build-attempt.json"),
               "RAW_WINDOWS_RECEIPT": gate.descriptor(bundle, bundle / "raw-windows-receipt.json"),
               "WINDOWS_USER_ACCEPTED": "NO", "CANONICAL_GAMEDATA_ROOT_AT_STARTUP": selected_root,
               "STARTUP_GAMEDATA_MANIFEST": attempt["CANONICAL_MANIFEST"], "GAMEDATA_SELECTION_EVIDENCE": []}
    for row in held:
        selection = {"Engine": row["engine"], "Pid": row["pid"], "Path": row["executable"],
                     "ProcessStartUtc": row["start_time_utc"], "CapturedUtc": "2026-01-01T00:00:06Z",
                     "InheritedGameData": selected_root, "InheritedOverride": "", "ResolvedGameDataRoot": selected_root,
                     "BuildAttemptSha256": windows["BUILD_ATTEMPT_RECEIPT"]["SHA256"], "PublicMasterSha": PUBLIC,
                     "GameDataManifest": index, "LauncherSha256": attempt["LAUNCHER_SHA256"]}
        path = bundle / "startup-selection" / (row["engine"] + ".json")
        gate.write_json(path, selection)
        windows["GAMEDATA_SELECTION_EVIDENCE"].append(gate.descriptor(bundle, path))
    gate.write_json(bundle / "windows-receipt.json", windows)
    expected = {**{key: attempt[key] for key in gate.TUPLE},
                "WINDOWS_RECEIPT_SHA256": gate.digest((bundle / "windows-receipt.json").read_bytes())}
    human = {**expected, "AUTHOR": "Mike", "ROLE": "user", "MESSAGE_ID": "synthetic-fixture-user-message",
             "RECORDED_UTC": "2026-01-01T00:00:10Z", "TEXT": "I accept this Windows build for the frozen inputs.",
             "EXPLICIT_ACCEPTANCE": True, "FIXTURE_ONLY": True}
    gate.write_json(bundle / "human.json", human)
    acceptance = {"FORMAT_VERSION": 1, **expected, "WINDOWS_USER_ACCEPTED": "YES", "ACCEPTED_BY": "Mike",
                  "ACCEPTED_UTC": "2026-01-01T00:00:11Z", "EXPLICIT_ACCEPTANCE_EVIDENCE": gate.descriptor(bundle, bundle / "human.json"),
                  "BUILD_ATTEMPT_RECEIPT": windows["BUILD_ATTEMPT_RECEIPT"],
                  "WINDOWS_RECEIPT": gate.descriptor(bundle, bundle / "windows-receipt.json")}
    gate.write_json(bundle / "acceptance.json", acceptance)
    return bundle, {"CANONICAL_GAMEDATA_ROOT": str(canonical), "LOCAL_CONFIG_OVERRIDES": []}


class ReleaseContractTests(unittest.TestCase):
    def test_admits_typed_mission_content_and_excludes_recorded_packets(self):
        for path in ("Missions/MissionOffers.json", "Missions/Keys.json"):
            self.assertTrue(gate.runtime_content_path(path), path)
        for path in ("Missions/RollBodies.json", "Missions/RollTemplate.json"):
            self.assertFalse(gate.runtime_content_path(path), path)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def test_explicit_developer_compile_needs_no_private_profile_or_receipt(self):
        environment = {key: value for key, value in os.environ.items()
                       if key not in ("AO_REBIRTH_BUILD_CHILD", "AO_REBIRTH_BUILD_TOKEN", "AO_REBIRTH_BUILD_STATE")}
        output = io.StringIO()
        with mock.patch.dict(os.environ, environment, clear=True), redirect_stdout(output), \
                mock.patch.object(gate, "profile_for") as profile, \
                mock.patch.object(gate, "verify_build_token") as verify:
            result = gate.main(["verify-build-token", "--repository-root", str(self.root), "--developer-build"])
        self.assertEqual(result, 0)
        profile.assert_not_called()
        verify.assert_not_called()
        self.assertIn("WINDOWS_BUILD_SCOPE=DEVELOPER", output.getvalue())
        self.assertIn("PRIVATE_RELEASE_PROFILE=NOT_REQUIRED", output.getvalue())
        self.assertIn("DEPLOYABLE=NO", output.getvalue())
        self.assertIn("WINDOWS_USER_ACCEPTED=NO", output.getvalue())
        self.assertEqual(list(self.root.iterdir()), [])

    def test_developer_compile_cannot_claim_release_actions_or_active_capability(self):
        environment = {key: value for key, value in os.environ.items()
                       if key not in ("AO_REBIRTH_BUILD_CHILD", "AO_REBIRTH_BUILD_TOKEN", "AO_REBIRTH_BUILD_STATE")}
        invalid = [
            ["verify-build-token", "--publishing"],
            ["verify-build-token", "--entrypoint", "NewZoneEngineBuild/build.cmd"],
            ["verify-build-token", "--profile", str(self.root / "private.json")],
            ["prepare-build"], ["run-build"], ["record-startup"], ["accept-windows"],
        ]
        for arguments in invalid:
            with self.subTest(arguments=arguments), mock.patch.dict(os.environ, environment, clear=True), \
                    redirect_stderr(io.StringIO()), mock.patch.object(gate, "profile_for") as profile:
                self.assertEqual(gate.main([*arguments, "--repository-root", str(self.root), "--developer-build"]), 1)
                profile.assert_not_called()
        for marker in ("AO_REBIRTH_BUILD_CHILD", "AO_REBIRTH_BUILD_TOKEN", "AO_REBIRTH_BUILD_STATE"):
            for value in ("1", "0"):
                with self.subTest(marker=marker, value=value), \
                        mock.patch.dict(os.environ, {**environment, marker: value}, clear=True), \
                        redirect_stderr(io.StringIO()), mock.patch.object(gate, "profile_for") as profile:
                    self.assertEqual(gate.main(["verify-build-token", "--repository-root", str(self.root), "--developer-build"]), 1)
                    profile.assert_not_called()
        self.assertEqual(list(self.root.iterdir()), [])

    def test_default_token_check_still_requires_private_profile(self):
        with redirect_stderr(io.StringIO()), \
                mock.patch.object(gate, "profile_for", side_effect=FileNotFoundError("missing private fixture")) as profile, \
                mock.patch.object(gate, "verify_build_token") as verify:
            self.assertEqual(gate.main(["verify-build-token", "--repository-root", str(self.root)]), 1)
        profile.assert_called_once_with(self.root, None)
        verify.assert_not_called()

    def private_catalog_fixture(self):
        canonical = self.root / "canonical"
        canonical.mkdir()
        (canonical / "private.json").write_bytes(b'{"repair":true}')
        (canonical / "items.dat").write_bytes(b"old generated fixture")
        stage = self.root / "ignored-policy-inputs"
        stage.mkdir()
        text = "Stage the reviewed private policies: " + ", ".join(Path(name).name for name in sorted(gate.PRIVATE_CHEST_CATALOGS))
        instruction = stage / "authorization.txt"
        instruction.write_bytes(text.encode())
        mappings = []
        for name in sorted(gate.PRIVATE_CHEST_CATALOGS):
            path = stage / Path(name).name
            path.write_bytes(gate.encoded({"Version": 1, "Entries": [{"fixture": name}]}))
            mappings.append({"PATH": name, "STAGED_PATH": str(path), "SHA256": gate.digest(path.read_bytes()),
                "APPROVAL": {"BY": "Mike", "INSTRUCTION_PATH": str(instruction),
                             "INSTRUCTION_SHA256": gate.digest(instruction.read_bytes()), "TEXT": text}})
        profile = {"CANONICAL_GAMEDATA_ROOT": str(canonical), "STATE_ROOT": str(self.root),
                   "PROTECTED_GENERATED_FILES": ["items.dat"], "PRIVATE_CATALOG_ADOPTIONS": mappings}
        placeholder = gate.encoded({"Version": 1, "Entries": []})
        source = {name: {"size": len(placeholder), "blob": gate.blob(placeholder)} for name in gate.PRIVATE_CHEST_CATALOGS}
        def trees(root, selected, authority, sha):
            return source if authority == "PUBLIC_PRODUCT_DATA" and sha == PUBLIC else {}
        return canonical, profile, placeholder, trees

    def test_private_catalog_adoption_replays_public_placeholders_and_preserves_applied_chain(self):
        canonical, profile, placeholder, trees = self.private_catalog_fixture()
        receipt_path, receipt, _ = make_generated_fixture(self.root)
        profile["REQUIRE_GENERATED_ITEMS"] = True
        rows, states = gate.inventory(canonical)
        bundle = self.root / "catalog-plan"
        bundle.mkdir()
        gate.write_json(bundle / "before-manifest.json", rows)
        baseline = {"PUBLIC_MASTER_SHA": "c" * 40, "CONTENT_DUMP_SHA": DUMP}
        with mock.patch.object(gate, "tree_files", side_effect=trees), mock.patch.object(gate, "source_blob", return_value=placeholder), \
                mock.patch.object(gate, "git", return_value=b"ignored"), \
                mock.patch.object(gate, "exporter_source_closure", return_value=receipt["EXPORTER_SOURCE_CLOSURE"]):
            plan, projected = gate.reconcile_plan(self.root, profile, bundle, PUBLIC, DUMP, baseline, rows, generated_receipt=receipt_path)
            gate.validate_reconciliation(bundle, plan)
            summary = gate.summarize_reconciliation(bundle, plan)
            self.assertEqual(len(summary["PRIVATE_CATALOG_ADOPTIONS"]), 3)
            self.assertNotIn("STAGED_PATH", gate.encoded(summary).decode())
            self.assertNotIn(str(self.root), gate.encoded(summary).decode())
            attempt = {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP, "WINDOWS_GAMEDATA_MANIFEST": plan["WINDOWS_GAMEDATA_MANIFEST"]}
            with mock.patch.object(gate, "intake", return_value=(bundle, attempt, plan, projected, states)):
                gate.apply_content(self.root, profile, gate.digest(gate.encoded(plan)))
            self.assertEqual((canonical / "private.json").read_bytes(), b'{"repair":true}')
            for mapping in profile["PRIVATE_CATALOG_ADOPTIONS"]:
                self.assertEqual(gate.digest((canonical / mapping["PATH"]).read_bytes()), mapping["SHA256"])
            next_bundle = self.root / "next-catalog-plan"
            next_bundle.mkdir()
            current, _ = gate.inventory(canonical)
            gate.write_json(next_bundle / "before-manifest.json", current)
            carried, projected = gate.reconcile_plan(self.root, profile, next_bundle, PUBLIC, DUMP,
                gate.read_json(self.root / "canonical-state.json"), current)
            self.assertEqual(projected, {})
            self.assertIn("PRIOR_LOCAL_RECONCILIATION", carried)
            gate.summarize_reconciliation(next_bundle, carried)
        for mutate in (lambda value: value["PRIVATE_CATALOG_ADOPTIONS"].pop(),
                       lambda value: value["PRIVATE_CATALOG_ADOPTIONS"][0].update(PATH="items.dat"),
                       lambda value: value["PRIVATE_CATALOG_ADOPTIONS"][0]["APPROVAL"].update(BY="Agent"),
                       lambda value: value["PRIVATE_CATALOG_ADOPTIONS"][0]["APPROVAL"].update(INSTRUCTION_SHA256="f" * 64),
                       lambda value: value["PRIVATE_CATALOG_ADOPTIONS"][0]["CANONICAL_AFTER"].update(sha256="f" * 64),
                       lambda value: value["PRIVATE_CATALOG_ADOPTIONS"][0].update(CANONICAL_BEFORE=None)):
            bad = copy.deepcopy(summary)
            mutate(bad)
            with self.assertRaises(ValueError):
                gate.validate_reconciliation_summary(bundle, bad)

    def test_private_catalog_adoption_rejects_tampered_source_and_instruction(self):
        canonical, profile, placeholder, trees = self.private_catalog_fixture()
        rows, _ = gate.inventory(canonical)
        for number, mutation in enumerate((lambda value: value["PRIVATE_CATALOG_ADOPTIONS"][0].update(SHA256="f" * 64),
                lambda value: value["PRIVATE_CATALOG_ADOPTIONS"][0]["APPROVAL"].update(TEXT="Not the actual instruction for MissionChestRKAllowlist.json"),
                lambda value: value["PRIVATE_CATALOG_ADOPTIONS"][0].update(PATH="items.dat"))):
            selected = copy.deepcopy(profile)
            mutation(selected)
            bundle = self.root / ("invalid-policy-" + str(number))
            bundle.mkdir()
            gate.write_json(bundle / "before-manifest.json", rows)
            with mock.patch.object(gate, "tree_files", side_effect=trees), mock.patch.object(gate, "source_blob", return_value=placeholder), \
                    mock.patch.object(gate, "git", return_value=b"ignored"), self.assertRaises(ValueError):
                plan, _ = gate.reconcile_plan(self.root, selected, bundle, PUBLIC, DUMP,
                    {"PUBLIC_MASTER_SHA": "c" * 40, "CONTENT_DUMP_SHA": DUMP}, rows)
                gate.validate_reconciliation(bundle, plan)
        self.assertEqual(gate.inventory(canonical)[0], rows)

    def test_private_catalog_adoption_cannot_overwrite_different_private_entries(self):
        canonical, profile, placeholder, trees = self.private_catalog_fixture()
        existing = canonical / sorted(gate.PRIVATE_CHEST_CATALOGS)[0]
        existing.parent.mkdir()
        existing.write_bytes(gate.encoded({"Version": 1, "Entries": [{"private": "preserve"}]}))
        rows, _ = gate.inventory(canonical)
        bundle = self.root / "conflicting-private-policy"
        bundle.mkdir()
        gate.write_json(bundle / "before-manifest.json", rows)
        with mock.patch.object(gate, "tree_files", return_value={}), mock.patch.object(gate, "git", return_value=b"ignored"), self.assertRaises(ValueError):
            plan, _ = gate.reconcile_plan(self.root, profile, bundle, PUBLIC, DUMP,
                {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP}, rows)
            gate.validate_reconciliation(bundle, plan)
        self.assertEqual(gate.inventory(canonical)[0], rows)

    @staticmethod
    def object_state_machine_catalog():
        return {"SchemaVersion": 1, "Machines": [{"Id": 1, "InitialState": 0,
            "InitialStateCompatibility": "INFERRED: reviewed synthetic initial-state binding",
            "States": [{"Id": 0, "EntrySpells": [], "SignalTimers": [], "Transitions": []}]}],
            "SignalPolicies": [{"SignalMask": 1, "DoorOperations": ["Unlock", "Open"],
                "Compatibility": "INFERRED: reviewed synthetic server signal policy"}]}

    def test_object_state_machine_adoption_uses_existing_hash_approval_and_portable_chain(self):
        canonical = self.root / "canonical"
        canonical.mkdir()
        (canonical / "items.dat").write_bytes(b"existing generated fixture")
        before_rows, _ = gate.inventory(canonical)
        stage = self.root / "ignored-object-inputs"
        stage.mkdir()
        data = stage / "ObjectStateMachines.json"
        data.write_bytes(gate.encoded(self.object_state_machine_catalog()))
        text = "Adopt the reviewed ObjectStateMachines.json runtime catalog."
        instruction = stage / "authorization.txt"
        instruction.write_bytes(text.encode())
        mapping = {"PATH": data.name, "STAGED_PATH": str(data), "SHA256": gate.digest(data.read_bytes()),
            "APPROVAL": {"BY": "Mike", "INSTRUCTION_PATH": str(instruction),
                "INSTRUCTION_SHA256": gate.digest(instruction.read_bytes()), "TEXT": text}}
        profile = {"CANONICAL_GAMEDATA_ROOT": str(canonical), "STATE_ROOT": str(self.root),
            "PROTECTED_GENERATED_FILES": [], "PRIVATE_CATALOG_ADOPTIONS": [mapping]}
        bundle = self.root / "object-plan"
        bundle.mkdir()
        gate.write_json(bundle / "before-manifest.json", before_rows)
        with mock.patch.object(gate, "tree_files", return_value={}), mock.patch.object(gate, "git", return_value=b"ignored"):
            plan, projected = gate.reconcile_plan(self.root, profile, bundle, PUBLIC, DUMP,
                {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP}, before_rows)
            gate.validate_reconciliation(bundle, plan)
            summary = gate.summarize_reconciliation(bundle, plan)
        self.assertEqual(projected, {data.name: data.read_bytes()})
        self.assertEqual(summary["PRIVATE_CATALOG_ADOPTIONS"][0]["SHA256"], mapping["SHA256"])
        self.assertNotIn("STAGED_PATH", gate.encoded(summary).decode())
        self.assertNotIn(str(self.root), gate.encoded(summary).decode())
        for mutation in (lambda row: row.update(SHA256="f" * 64),
                         lambda row: row["APPROVAL"].update(BY="Agent"),
                         lambda row: row["APPROVAL"].update(TEXT="Invented approval for ObjectStateMachines.json"),
                         lambda row: row["APPROVAL"].update(INSTRUCTION_SHA256="f" * 64)):
            changed = copy.deepcopy(plan)
            mutation(changed["PRIVATE_CATALOG_ADOPTIONS"][0])
            with self.assertRaises(ValueError):
                gate.validate_reconciliation(bundle, changed)
        # A second intake may recognize identical bytes, but cannot overwrite private edits.
        identical = {data.name: gate.content_identity(data.read_bytes(), data.name)}
        row = copy.deepcopy(plan["PRIVATE_CATALOG_ADOPTIONS"][0])
        row["CANONICAL_BEFORE"] = row["CANONICAL_AFTER"]
        replay = {**plan, "PRIVATE_CATALOG_ADOPTIONS": [row]}
        gate.validate_private_catalog_adoptions(bundle, replay, dict(identical))
        altered = copy.deepcopy(replay)
        different = gate.encoded({**self.object_state_machine_catalog(), "SchemaVersion": 2})
        altered["PRIVATE_CATALOG_ADOPTIONS"][0]["CANONICAL_BEFORE"] = gate.stash(bundle, "different-private", different)
        with self.assertRaisesRegex(ValueError, "overwrite unexplained private content"):
            gate.validate_private_catalog_adoptions(bundle, altered,
                {data.name: gate.content_identity(different, data.name)})

    def test_object_state_machine_adoption_requires_nonempty_classified_valid_states(self):
        invalid = (
            lambda row: row.update(SchemaVersion=2),
            lambda row: row.update(SchemaVersion=True),
            lambda row: row.update(Machines=[]),
            lambda row: row.update(SignalPolicies=None),
            lambda row: row["Machines"][0].update(States=[]),
            lambda row: row["Machines"][0].update(InitialState=99),
            lambda row: row["Machines"][0].update(InitialStateCompatibility=" "),
            lambda row: row["Machines"].append(copy.deepcopy(row["Machines"][0])),
            lambda row: row["Machines"][0]["States"].append(copy.deepcopy(row["Machines"][0]["States"][0])),
            lambda row: row["Machines"][0]["States"][0].update(Transitions=[{"TargetState": 2, "Criteria": []}]),
            lambda row: row["SignalPolicies"][0].update(Compatibility=""),
            lambda row: row["SignalPolicies"][0].update(SignalMask=0),
            lambda row: row["SignalPolicies"][0].update(DoorOperations=["Invented"]),
            lambda row: row["SignalPolicies"][0].update(DoorOperations=[]),
            lambda row: row["SignalPolicies"][0].update(UseEnabled=1),
            lambda row: row["SignalPolicies"].append(copy.deepcopy(row["SignalPolicies"][0])),
        )
        for index, mutate in enumerate(invalid):
            content = self.object_state_machine_catalog()
            mutate(content)
            with self.subTest(case=index), self.assertRaises(ValueError):
                gate.validate_object_state_machine_adoption(content)
        valid = self.object_state_machine_catalog()
        valid["SignalPolicies"][0].update(DoorOperations=[], UseEnabled=False)
        gate.validate_object_state_machine_adoption(valid)
        gate.validate_object_state_machine_adoption(json.loads(json.dumps(valid).replace('"SchemaVersion"', '"schemaVersion"')))

    def test_private_catalog_adoption_paths_preserve_chest_group_and_independent_catalogs(self):
        for paths in ([], ["ObjectStateMachines.json"], ["QuestRewards.json"],
                      ["Playfields/17/Spawns.json"], ["Playfields/17/Spawns.json", "ObjectStateMachines.json"],
                      sorted(gate.INDEPENDENT_PRIVATE_CATALOGS), sorted(gate.PRIVATE_CHEST_CATALOGS),
                      sorted(gate.PRIVATE_CHEST_CATALOGS | gate.INDEPENDENT_PRIVATE_CATALOGS)):
            gate.validate_private_catalog_paths([{"PATH": path} for path in paths])
        for paths in (["ObjectStateMachines.json", "ObjectStateMachines.json"], ["items.dat"],
                      ["Playfields/17/Spawns.json", "Playfields/17/Spawns.json"],
                      ["Playfields/0/Spawns.json"], ["Playfields/17/../Spawns.json"],
                      ["Playfields/17/Spawns.json/extra"], ["Playfields/17/Rooms.json"],
                      ["Playfields/../17/Spawns.json"], ["/Playfields/17/Spawns.json"],
                      ["Playfields\\17\\Spawns.json"], ["Playfields/non-numeric/Spawns.json"],
                      ["ObjectStateMachines.json", sorted(gate.PRIVATE_CHEST_CATALOGS)[0]]):
            with self.subTest(paths=paths), self.assertRaises(ValueError):
                gate.validate_private_catalog_paths([{"PATH": path} for path in paths])

    def spawn_catalog_fixture(self):
        path = "Playfields/17/Spawns.json"
        before = {"SchemaVersion": 1, "PlayfieldId": 17, "Spawns": [
            {"Hash": 1, "HashText": "fixture-a", "Position": [1, 2, 3],
             "Extensions": {"Events": [{"Name": "fixture", "Active": True}]}},
            {"Hash": 2, "HashText": "fixture-b", "Position": [4, 5, 6]}]}
        after = copy.deepcopy(before)
        for entry in after["Spawns"]:
            entry.update(MinLevel=1, MaxLevel=2, RespawnTime=30)
        after["Spawns"][0]["Extensions"]["Events"][0]["Unknown1"] = 0
        canonical = self.root / "canonical"
        target = canonical / path
        target.parent.mkdir(parents=True)
        target.write_bytes(gate.encoded(before))
        stage = self.root / "ignored-spawn-inputs"
        stage.mkdir()
        data = stage / "Spawns.json"
        data.write_bytes(gate.encoded(after))
        text = "Approve " + path
        instruction = stage / "authorization.txt"
        instruction.write_bytes(text.encode())
        mapping = {"PATH": path, "STAGED_PATH": str(data), "SHA256": gate.digest(data.read_bytes()),
            "APPROVAL": {"BY": "Mike", "INSTRUCTION_PATH": str(instruction),
                "INSTRUCTION_SHA256": gate.digest(instruction.read_bytes()), "TEXT": text}}
        profile = {"CANONICAL_GAMEDATA_ROOT": str(canonical), "STATE_ROOT": str(self.root),
            "PROTECTED_GENERATED_FILES": [], "PRIVATE_CATALOG_ADOPTIONS": [mapping]}
        rows, _ = gate.inventory(canonical)
        bundle = self.root / "spawn-plan"
        bundle.mkdir()
        gate.write_json(bundle / "before-manifest.json", rows)
        with mock.patch.object(gate, "tree_files", return_value={}), mock.patch.object(gate, "git", return_value=b"ignored"):
            plan, projected = gate.reconcile_plan(self.root, profile, bundle, PUBLIC, DUMP,
                {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP}, rows)
        return path, bundle, plan, projected, before, after, target

    def test_spawn_adoption_preserves_existing_fields_and_portable_approval_chain(self):
        path, bundle, plan, projected, before, after, target = self.spawn_catalog_fixture()
        gate.validate_reconciliation(bundle, plan)
        summary = gate.summarize_reconciliation(bundle, plan)
        gate.validate_reconciliation_summary(bundle, summary)
        self.assertEqual(projected, {path: gate.encoded(after)})
        self.assertEqual(target.read_bytes(), gate.encoded(before))
        self.assertEqual(summary["PRIVATE_CATALOG_ADOPTIONS"][0]["APPROVAL"]["TEXT"], "Approve " + path)
        self.assertNotIn("STAGED_PATH", gate.encoded(summary).decode())
        self.assertNotIn(str(self.root), gate.encoded(summary).decode())
        lower = json.loads(json.dumps(after).replace('"SchemaVersion"', '"schemaVersion"')
                           .replace('"PlayfieldId"', '"playfieldId"').replace('"Spawns"', '"spawns"'))
        gate.validate_spawn_catalog_adoption(path, lower, copy.deepcopy(lower))

    def test_spawn_adoption_rejects_destructive_and_malformed_approved_bytes(self):
        path, bundle, plan, _, before, after, _ = self.spawn_catalog_fixture()
        mutations = (
            lambda value: value["Spawns"][0].pop("Hash"),
            lambda value: value["Spawns"][0].update(Hash=9),
            lambda value: value["Spawns"][0].update(Hash=True),
            lambda value: value["Spawns"][0].update(Hash=1.0),
            lambda value: value["Spawns"].reverse(),
            lambda value: value["Spawns"].append(copy.deepcopy(value["Spawns"][0])),
            lambda value: value["Spawns"].pop(),
            lambda value: value["Spawns"][0]["Position"].reverse(),
            lambda value: value["Spawns"][0]["Extensions"]["Events"][0].update(Active=False),
            lambda value: value["Spawns"][0].update(hash=9),
            lambda value: value.update(Spawns=[]),
            lambda value: value.update(Spawns=[None]),
            lambda value: value.update(Spawns=[{}]),
            lambda value: value.update(SchemaVersion=True),
            lambda value: value.update(PlayfieldId=18),
        )
        actual = {path: gate.content_identity(gate.encoded(before), path)}
        for index, mutate in enumerate(mutations):
            changed, payload = copy.deepcopy(plan), copy.deepcopy(after)
            mutate(payload)
            data = gate.encoded(payload)
            row = changed["PRIVATE_CATALOG_ADOPTIONS"][0]
            row.update(SHA256=gate.digest(data), CANONICAL_AFTER=gate.stash(bundle, "rejected-" + str(index), data))
            with self.subTest(case=index), self.assertRaises(ValueError):
                gate.validate_private_catalog_adoptions(bundle, changed, dict(actual))
        for malformed in (None, {}, [], {"SchemaVersion": 1, "Spawns": []}):
            with self.subTest(existing=malformed), self.assertRaises(ValueError):
                gate.validate_spawn_catalog_adoption(path, malformed, after)

    def test_spawn_adoption_retains_hash_bound_actual_path_approval(self):
        path, bundle, plan, _, before, _, _ = self.spawn_catalog_fixture()
        actual = {path: gate.content_identity(gate.encoded(before), path)}
        mutations = (
            lambda row: row.update(SHA256="f" * 64),
            lambda row: row["APPROVAL"].update(BY="Agent"),
            lambda row: row["APPROVAL"].update(TEXT="Approve Spawns.json"),
            lambda row: row["APPROVAL"].update(TEXT="Invented approval for " + path),
            lambda row: row["APPROVAL"].update(INSTRUCTION_SHA256="f" * 64),
            lambda row: row.update(CANONICAL_BEFORE=None),
        )
        for index, mutate in enumerate(mutations):
            changed = copy.deepcopy(plan)
            mutate(changed["PRIVATE_CATALOG_ADOPTIONS"][0])
            with self.subTest(case=index), self.assertRaises(ValueError):
                gate.validate_private_catalog_adoptions(bundle, changed, dict(actual))

    def test_portable_acceptance_survives_location_change_without_windows_binaries(self):
        bundle, _ = make_portable_fixture(self.root)
        relocated = self.root / "private-source" / "evidence"
        shutil.copytree(bundle, relocated)
        loaded = gate.load_accepted_release(self.root / "private-source", "evidence/acceptance.json")
        self.assertEqual(loaded["acceptance"]["WINDOWS_USER_ACCEPTED"], "YES")
        self.assertEqual(loaded["build_attempt"]["PUBLIC_MASTER_SHA"], PUBLIC)
        self.assertFalse((relocated / "binaries").exists())

    def test_portable_export_omits_local_payload_history_and_requires_one_summary_schema(self):
        bundle, _ = make_portable_fixture(self.root)
        local = bundle / "reconciliation-payload" / "large-catalog"
        local.parent.mkdir()
        with local.open("wb") as stream:
            stream.truncate(87359781)
        prior = bundle / "prior-history" / "catalog.json"
        prior.parent.mkdir()
        prior.write_bytes(b"ignored old local replay")
        exported = gate.export_accepted_bundle(self.root, bundle, bundle / "acceptance.json")
        self.assertLess(sum(path.stat().st_size for path in exported.parent.rglob("*") if path.is_file()), 100000)
        self.assertFalse((exported.parent / "reconciliation-payload").exists())
        self.assertFalse((exported.parent / "prior-history").exists())
        self.assertFalse((exported.parent / "reconciliation.json").exists())
        loaded = gate.load_accepted_release(self.root, exported)
        self.assertEqual(loaded["reconciliation"]["PROOF_KIND"], "PORTABLE_MEASURED_RECONCILIATION_V1")
        original = gate.read_json(bundle / "build-attempt.json")
        original["RECONCILIATION"] = gate.descriptor(bundle, bundle / "reconciliation.json")
        with self.assertRaises(ValueError):
            gate.require_successful_attempt(bundle, original)

    def test_hash_or_frozen_tuple_tampering_fails(self):
        bundle, _ = make_portable_fixture(self.root)
        for target in ("windows-receipt.json", "build-attempt.json", "manifest.json", "human.json", "capture/status.log"):
            path, original = bundle / target, (bundle / target).read_bytes()
            path.write_bytes(original + b" ")
            with self.assertRaises(ValueError, msg=target):
                gate.load_accepted_release(self.root, "bundle/acceptance.json")
            path.write_bytes(original)
        accepted = gate.read_json(bundle / "acceptance.json")
        accepted["CONTENT_DUMP_SHA"] = "f" * 40
        gate.write_json(bundle / "acceptance.json", accepted)
        with self.assertRaises(ValueError):
            gate.load_accepted_release(self.root, "bundle/acceptance.json")

    def test_failed_prepared_missing_hooks_or_stale_fetch_cannot_be_accepted(self):
        bundle, _ = make_portable_fixture(self.root)
        original = gate.read_json(bundle / "build-attempt.json")
        for update in ({"WINDOWS_BUILD": "NOT_RUN"}, {"WINDOWS_BUILD": "FAIL"},
                       {"WINDOWS_BUILD_EXIT_CODE": 7}, {"BUILD_FINISHED_UTC": None},
                       {"PRODUCT_COMPILER_HOOKS": []}, {"APPROVED_BUILD_ENTRYPOINT": "other.cmd"}):
            with self.assertRaises(ValueError):
                gate.require_successful_attempt(bundle, {**original, **update})
        for key in ("PUBLIC_FETCH", "CONTENT_DUMP_FETCH"):
            for value in ({"RESULT": "FAIL"}, {"ATTEMPT_ID": "d" * 32},
                          {"FETCHED_AT_UTC": "2025-01-01T00:00:00Z"}, {"TIP_SHA": "f" * 40}):
                bad = copy.deepcopy(original)
                bad[key].update(value)
                with self.assertRaises(ValueError):
                    gate.require_successful_attempt(bundle, bad)

    def test_actual_binary_and_data_changes_invalidate_pre_acceptance(self):
        bundle, profile = make_portable_fixture(self.root)
        attempt = gate.read_json(bundle / "build-attempt.json")
        with mock.patch.object(gate, "source_exact"):
            gate.current_matches(self.root, profile, bundle, attempt)
            output = self.root / gate.OUTPUTS[0]
            old = output.read_bytes()
            output.write_bytes(b"modified")
            with self.assertRaises(ValueError):
                gate.current_matches(self.root, profile, bundle, attempt)
            output.write_bytes(old)
            (Path(profile["CANONICAL_GAMEDATA_ROOT"]) / "catalog.json").write_bytes(b'{"upstream":9,"private":2}\n')
            with self.assertRaises(ValueError):
                gate.current_matches(self.root, profile, bundle, attempt)

    def test_yes_boolean_and_unrelated_or_negative_human_text_are_not_acceptance(self):
        bundle, _ = make_portable_fixture(self.root)
        human = gate.read_json(bundle / "human.json")
        expected = {key: human[key] for key in (*gate.TUPLE, "WINDOWS_RECEIPT_SHA256")}
        for text in ("", "hello", "not accepted", "I do not accept", "yes", "Is this approved?",
                     "I haven't approved this", "I have not accepted this", "I approve this?", "looks good?"):
            with self.assertRaises(ValueError):
                gate.explicit_mike_acceptance({**human, "TEXT": text}, expected)
        gate.explicit_mike_acceptance(human, expected)
        gate.explicit_mike_acceptance({**human, "TEXT": "works on Windows, update Linux, rebuild and deploy to live"}, expected)
        gate.explicit_mike_acceptance({**human, "TEXT": "I accept this Windows build. Do not deploy yet."}, expected)
        bad = {**human, "WINDOWS_GAMEDATA_MANIFEST": "e" * 64}
        with self.assertRaises(ValueError):
            gate.explicit_mike_acceptance(bad, expected)
        human["RECORDED_UTC"] = "2025-01-01T00:00:00Z"
        gate.write_json(bundle / "human.json", human)
        accepted = gate.read_json(bundle / "acceptance.json")
        accepted["EXPLICIT_ACCEPTANCE_EVIDENCE"] = gate.descriptor(bundle, bundle / "human.json")
        gate.write_json(bundle / "acceptance.json", accepted)
        with self.assertRaises(ValueError):
            gate.load_accepted_release(self.root, "bundle/acceptance.json")

    def test_declared_pass_without_actual_generation_or_selection_fails(self):
        bundle, _ = make_portable_fixture(self.root)
        windows, attempt = gate.read_json(bundle / "windows-receipt.json"), gate.read_json(bundle / "build-attempt.json")
        for update in ({"EVIDENCE": {}}, {"GAMEDATA_SELECTION_EVIDENCE": []}, {"WORLD_EPOCH": 999}):
            with self.assertRaises(ValueError):
                gate.validate_startup(bundle, {**windows, **update}, attempt)
        selection_path = gate.load_descriptor(bundle, windows["GAMEDATA_SELECTION_EVIDENCE"][0])
        selection = gate.read_json(selection_path)
        selection["InheritedOverride"] = "C:/different-overlay"
        gate.write_json(selection_path, selection)
        windows["GAMEDATA_SELECTION_EVIDENCE"][0] = gate.descriptor(bundle, selection_path)
        with self.assertRaises(ValueError):
            gate.validate_startup(bundle, windows, attempt)

    def test_approved_whole_document_removal_preserves_private_metadata_and_shops(self):
        base = {"SchemaVersion": 1, "PlayfieldId": 99, "Npcs": [{"Key": "synthetic"}],
                "StandaloneShops": [{"Key": "synthetic-shop"}]}
        private = copy.deepcopy(base)
        private["Npcs"][0]["HasDialogue"] = True
        resolution = {"": {"INPUT_HASHES": {"BASE": gate.value_hash(base),
            "UPSTREAM": "MISSING", "PRIVATE": gate.value_hash(private)},
            "ACTION": "PRESERVE_PRIVATE", "APPROVAL": "Synthetic test approval"}}
        original = json.dumps(private).encode()
        self.assertEqual(gate.merge_bytes(json.dumps(base).encode(), None, original, resolution), original)
        with self.assertRaises(gate.ContentConflict):
            gate.merge_value(base, gate.MISSING, private, {})

    def test_whole_document_resolution_rejects_changed_input_hashes(self):
        base, private = {"a": 1}, {"a": 2}
        resolution = {"": {"INPUT_HASHES": {"BASE": gate.value_hash(base),
            "UPSTREAM": "MISSING", "PRIVATE": gate.value_hash(private)},
            "ACTION": "PRESERVE_PRIVATE", "APPROVAL": "Synthetic test approval"}}
        for key in ("BASE", "UPSTREAM", "PRIVATE"):
            invalid = copy.deepcopy(resolution)
            invalid[""]["INPUT_HASHES"][key] = "f" * 64
            with self.subTest(key=key), self.assertRaises(gate.ContentConflict):
                gate.merge_value(base, gate.MISSING, private, invalid)

    def test_whole_document_resolution_requires_approval_and_known_action(self):
        base, private = {"a": 1}, {"a": 2}
        resolution = {"": {"INPUT_HASHES": {"BASE": gate.value_hash(base),
            "UPSTREAM": "MISSING", "PRIVATE": gate.value_hash(private)},
            "ACTION": "PRESERVE_PRIVATE", "APPROVAL": "Synthetic test approval"}}
        for key, value in (("APPROVAL", ""), ("ACTION", "UNKNOWN")):
            invalid = copy.deepcopy(resolution)
            invalid[""][key] = value
            with self.subTest(key=key), self.assertRaises(gate.ContentConflict):
                gate.merge_value(base, gate.MISSING, private, invalid)

    def test_reconcile_add_modify_delete_preserves_private_and_detects_conflicts(self):
        base = {"a": 1, "remove": 2, "repaired": 3}
        private = {"a": 1, "remove": 2, "repaired": 7, "private": {"x": 8}}
        incoming = {"a": 4, "repaired": 3, "new": 5}
        self.assertEqual(gate.merge_value(base, incoming, private, {}),
                         {"a": 4, "repaired": 7, "private": {"x": 8}, "new": 5})
        self.assertEqual(gate.merge_value({"a": 1}, gate.MISSING, {"a": 1, "private": 2}, {}), {"private": 2})
        self.assertEqual(gate.merge_value({"a": 1, "b": 2}, {}, {"a": 1}, {}), {})
        self.assertIs(gate.merge_value(1, True, 1, {}), True)
        with self.assertRaises(ValueError):
            gate.merge_value(1, 2, True, {})
        self.assertEqual(gate.merge_bytes(b'{"n":1}', b'{"n":true}', b'{"n":1,"private":2}', {}), b'{\n  "n": true,\n  "private": 2\n}\n')
        with self.assertRaises(ValueError):
            gate.merge_value({"repair": 1}, {"repair": 2}, {"repair": 3}, {})
        with self.assertRaises(ValueError):
            gate.merge_value([1], [1, 2], [1, 3], {})
        hashes = {"BASE": gate.value_hash(1), "UPSTREAM": gate.value_hash(2), "PRIVATE": gate.value_hash(3)}
        resolution = {"/repair": {"ACTION": "PRESERVE_PRIVATE", "INPUT_HASHES": hashes, "APPROVAL": "approved scope"}}
        self.assertEqual(gate.merge_value({"repair": 1}, {"repair": 2}, {"repair": 3}, resolution), {"repair": 3})
        with self.assertRaises(ValueError):
            gate.merge_value({"repair": 1}, {"repair": 2}, {"repair": 4}, resolution)

    def test_report_bool_or_missing_upstream_transition_cannot_authorize_stale_data(self):
        bundle, _ = make_portable_fixture(self.root)
        report = gate.read_json(bundle / "reconciliation.json")
        report["SOURCE_SNAPSHOTS"]["PRIVATE_CONTENT_UPSTREAM"]["AFTER"]["new.json"] = {"blob": "c" * 40, "size": 2}
        report["ALL_CHANGES_RECONCILED"] = True
        with self.assertRaises(ValueError):
            gate.validate_reconciliation(bundle, report)

    def test_private_authority_rebinding_requires_applied_baseline_projection(self):
        canonical = self.root / "canonical"
        canonical.mkdir()
        catalog = "ItemTemplates.json"
        prior_upstream = b'{"drop":1}'
        new_upstream = b'{"drop":2}'
        prior_canonical = b'{"drop":1,"private":true}'
        quest = b'{"unchanged":true}'
        (canonical / catalog).write_bytes(prior_canonical)
        (canonical / "Quests.json").write_bytes(quest)
        (canonical / "items.dat").write_bytes(b"independently generated")
        rows, _ = gate.inventory(canonical)
        authority = "PRIVATE_CONTENT_UPSTREAM"
        profile = {"CANONICAL_GAMEDATA_ROOT": str(canonical), "STATE_ROOT": str(self.root),
                   "PRIVATE_CONTENT_REPOSITORY": "fixture/replacement", "PRIVATE_CONTENT_PREFIX": "Output",
                   "PROTECTED_GENERATED_FILES": ["items.dat"]}
        old_files = {catalog: prior_upstream, "Quests.json": quest}
        old_projection = {path: {"blob": gate.blob(data), "size": len(data)} for path, data in old_files.items()}
        baseline = {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP,
                    "WINDOWS_GAMEDATA_MANIFEST": gate.validate_manifest(rows)}
        prior_bundle = self.root / "applied"
        prior_bundle.mkdir()
        gate.write_json(prior_bundle / "before-manifest.json", rows)
        with mock.patch.object(gate, "tree_files", side_effect=[old_projection, old_projection, {}, {}]):
            gate.reconcile_plan(self.root, profile, prior_bundle, PUBLIC, DUMP, baseline, rows)
        baseline.update(APPLIED_RECONCILIATION_BUNDLE=str(prior_bundle),
                        APPLIED_RECONCILIATION_SHA256=gate.digest((prior_bundle / "reconciliation.json").read_bytes()))
        target = "d" * 40

        def github_tree(selected, endpoint):
            self.assertEqual(selected["PRIVATE_CONTENT_REPOSITORY"], "fixture/replacement")
            if endpoint == f"git/trees/{DUMP}?recursive=1":
                files = old_files
            else:
                self.assertEqual(endpoint, f"git/trees/{target}?recursive=1")
                files = {**old_files, catalog: new_upstream}
            entries = {**{"Output/" + path: data for path, data in files.items()}, "Raw/" + catalog: b'{"unrelated":true}'}
            return {"truncated": False, "tree": [{"path": path, "type": "blob", "mode": "100644",
                    "sha": gate.blob(data), "size": len(data)} for path, data in entries.items()]}

        rejected = self.root / "rejected-plan"
        rejected.mkdir()
        gate.write_json(rejected / "before-manifest.json", rows)
        wrong_prefix = {**profile, "PRIVATE_CONTENT_PREFIX": "Raw"}
        with mock.patch.object(gate, "gh", side_effect=github_tree) as gh, mock.patch.object(gate, "source_blob") as fetch_blob:
            with self.assertRaisesRegex(ValueError, "baseline projection differs from the applied checkpoint"):
                gate.reconcile_plan(self.root, wrong_prefix, rejected, PUBLIC, target, baseline, rows)
            gh.assert_called_once_with(wrong_prefix, f"git/trees/{DUMP}?recursive=1")
            fetch_blob.assert_not_called()
            self.assertFalse((rejected / "reconciliation.json").exists())

        accepted = self.root / "replacement-plan"
        accepted.mkdir()
        gate.write_json(accepted / "before-manifest.json", rows)
        blobs = {gate.blob(data): data for data in (prior_upstream, new_upstream)}
        with mock.patch.object(gate, "gh", side_effect=github_tree), mock.patch.object(gate, "git", return_value=b""), \
                mock.patch.object(gate, "source_blob", side_effect=lambda root, profile, role, identity: blobs[identity]) as fetch_blob:
            plan, projected = gate.reconcile_plan(self.root, profile, accepted, PUBLIC, target, baseline, rows)
            self.assertEqual(fetch_blob.call_args_list, [mock.call(self.root, profile, authority, identity) for identity in blobs])
        self.assertEqual(set(projected), {catalog})
        self.assertEqual(json.loads(projected[catalog]), {"drop": 2, "private": True})
        self.assertEqual(gate.inventory(canonical)[0], rows)
        gate.summarize_reconciliation(accepted, plan)

    def test_approved_physical_add_modify_remove_replays_and_preserves_coexisting_content(self):
        canonical = self.root / "canonical"
        canonical.mkdir()
        (canonical / "catalog.json").write_bytes(b'{"a":1,"remove":3,"repaired":8,"private":2}')
        (canonical / "gone.json").write_bytes(b'{"upstream":1,"private":2}')
        (canonical / "items.dat").write_bytes(b"independently generated")
        before_rows, states = gate.inventory(canonical)
        old_data = {"catalog.json": b'{"a":1,"remove":3,"repaired":1}', "gone.json": b'{"upstream":1}'}
        new_data = {"catalog.json": b'{"a":4,"repaired":1,"new":5}', "added.json": b'{"fresh":6}'}
        source = {gate.blob(raw): raw for raw in [*old_data.values(), *new_data.values()]}
        old_tree = {path: {"blob": gate.blob(raw), "size": len(raw)} for path, raw in old_data.items()}
        new_tree = {path: {"blob": gate.blob(raw), "size": len(raw)} for path, raw in new_data.items()}
        baseline = {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP}
        bundle = self.root / "plan"
        bundle.mkdir()
        gate.write_json(bundle / "before-manifest.json", before_rows)
        profile = {"CANONICAL_GAMEDATA_ROOT": str(canonical), "STATE_ROOT": str(self.root), "PROTECTED_GENERATED_FILES": ["items.dat"]}
        def tree(root, profile, authority, sha):
            return {} if authority == "PUBLIC_PRODUCT_DATA" else old_tree if sha == DUMP else new_tree
        with mock.patch.object(gate, "tree_files", tree), mock.patch.object(gate, "source_blob", side_effect=lambda r,p,a,b: source[b]):
            plan, projected = gate.reconcile_plan(self.root, profile, bundle, PUBLIC, "d" * 40, baseline, before_rows)
        gate.validate_reconciliation(bundle, plan)
        summary = gate.summarize_reconciliation(bundle, plan)
        self.assertEqual(gate.validate_reconciliation_summary(bundle, summary), gate.read_json(bundle / "manifest.json"))
        missing_removal = copy.deepcopy(summary)
        missing_removal["CHANGES"] = [row for row in summary["CHANGES"] if row["STATUS"] != "D"]
        with self.assertRaises(ValueError):
            gate.validate_reconciliation_summary(bundle, missing_removal)
        wrong_size = copy.deepcopy(summary)
        next(row for row in wrong_size["CHANGES"] if row["UPSTREAM_AFTER"] is not None)["UPSTREAM_AFTER"]["size"] += 1
        with self.assertRaises(ValueError):
            gate.validate_reconciliation_summary(bundle, wrong_size)
        raw_payload = copy.deepcopy(summary)
        raw_payload["RAW_PAYLOAD"] = "unexpected catalog bytes"
        with self.assertRaises(ValueError):
            gate.validate_reconciliation_summary(bundle, raw_payload)
        attempt = {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": "d" * 40, "WINDOWS_GAMEDATA_MANIFEST": plan["WINDOWS_GAMEDATA_MANIFEST"]}
        with mock.patch.object(gate, "intake", return_value=(bundle, attempt, plan, projected, states)):
            gate.apply_content(self.root, profile, gate.digest(gate.encoded(plan)))
        self.assertEqual(gate.read_json(canonical / "catalog.json"), {"a": 4, "repaired": 8, "private": 2, "new": 5})
        self.assertEqual(gate.read_json(canonical / "gone.json"), {"private": 2})
        self.assertEqual(gate.read_json(canonical / "added.json"), {"fresh": 6})
        self.assertEqual((canonical / "items.dat").read_bytes(), b"independently generated")
        state = gate.read_json(self.root / "canonical-state.json")
        next_bundle = self.root / "next-plan"
        next_bundle.mkdir()
        current_rows, _ = gate.inventory(canonical)
        gate.write_json(next_bundle / "before-manifest.json", current_rows)
        with mock.patch.object(gate, "tree_files", tree), mock.patch.object(gate, "source_blob", side_effect=lambda r,p,a,b: source[b]):
            next_plan, _ = gate.reconcile_plan(self.root, profile, next_bundle, PUBLIC, "d" * 40, state, current_rows)
        next_summary = gate.summarize_reconciliation(next_bundle, next_plan)
        self.assertEqual(next_summary["PRIOR_CHECKPOINT"]["WINDOWS_GAMEDATA_MANIFEST"], plan["WINDOWS_GAMEDATA_MANIFEST"])
        self.assertFalse((next_bundle / "prior-reconciliation").exists())
        self.assertFalse((next_bundle / "reconciliation-payload").exists())
        forged = copy.deepcopy(plan)
        forged["CHANGES"] = forged["CHANGES"][:-1]
        with self.assertRaises(ValueError):
            gate.validate_reconciliation(bundle, forged)

    def test_inventory_rejects_lfs_pointer_and_full_path_set_races(self):
        data = self.root / "data"
        data.mkdir()
        target = data / "x.json"
        target.write_bytes(b"version https://git-lfs.github.com/spec/v1\n")
        with self.assertRaises(ValueError):
            gate.inventory(data)
        target.write_bytes(b"{}")
        original_open, changed = Path.open, []
        def changing_open(path, *args, **kwargs):
            if path == target and args and args[0] == "rb" and not changed:
                changed.append(True)
                (data / "new-directory").mkdir()
                with original_open(data / "added.json", "wb") as stream:
                    stream.write(b"{}")
            return original_open(path, *args, **kwargs)
        with mock.patch.object(Path, "open", changing_open), self.assertRaises(ValueError):
            gate.inventory(data)

    def test_generated_receipt_rejects_stale_source_output_tool_and_rdb_states(self):
        path, receipt, proof = make_generated_fixture(self.root)
        profile = {"CANONICAL_GAMEDATA_ROOT": str(self.root / "canonical")}
        closure = receipt["EXPORTER_SOURCE_CLOSURE"]
        with mock.patch.object(gate, "git", return_value=b"ignored"), mock.patch.object(gate, "exporter_source_closure", return_value=closure):
            gate.generated_items_receipt(self.root, profile, path, PUBLIC)
            for mutate in (lambda value: value.update(PUBLIC_MASTER_SHA="f" * 40),
                           lambda value: value["EXPORT"].update(OUTPUT_ABSENT_BEFORE=False),
                           lambda value: value["TOOL_BUILD"].update(EXIT_CODE=1),
                           lambda value: value["VALIDATION"].update(CURRENT_ITEMS_DAT_READER="FAIL"),
                           lambda value: value["VALIDATION"]["CURRENT_FIELDS"].update(fullRdb=0),
                           lambda value: value["VALIDATION"]["CURRENT_FIELDS"].update(positiveMultipleCount=-1),
                           lambda value: value["VALIDATION"]["CURRENT_FIELDS"].update(positiveMultipleCount=True),
                           lambda value: value["VALIDATION"]["CURRENT_FIELDS"].pop("positiveMultipleCount"),
                           lambda value: value["VALIDATION"]["CURRENT_FIELDS"].update(rawFallback=-1),
                           lambda value: value["VALIDATION"]["CURRENT_FIELDS"].update(rawFallback=True),
                           lambda value: value["EXPORTER_SOURCE_CLOSURE"][0].update(sha256="f" * 64),
                           lambda value: value["RDB_INPUTS"]["SHARDS"][0]["BEFORE_STATE"].__setitem__(3, 0)):
                bad = copy.deepcopy(receipt)
                mutate(bad)
                gate.write_json(path, bad)
                with self.assertRaises(ValueError):
                    gate.generated_items_receipt(self.root, profile, path, PUBLIC)
            gate.write_json(path, receipt)
            for target in (Path(receipt["OUTPUT"]["STAGED_PATH"]), self.root / gate.EXPORTER_OUTPUT / "RDBDataExtractor.dll",
                           Path(receipt["RDB_INPUTS"]["ROOT"]) / "ResourceDatabase.dat.001"):
                original = target.read_bytes()
                target.write_bytes(original + b"changed")
                with self.assertRaises(ValueError):
                    gate.generated_items_receipt(self.root, profile, path, PUBLIC)
                target.write_bytes(original)
            extra = Path(receipt["RDB_INPUTS"]["ROOT"]) / "ResourceDatabase.dat.002"
            extra.write_bytes(b"unexpected shard")
            with self.assertRaises(ValueError):
                gate.generated_items_receipt(self.root, profile, path, PUBLIC)
        bad_proof = copy.deepcopy(proof)
        bad_proof["VALIDATION"]["RAW_CATALOG"] = "raw bytes are forbidden"
        with self.assertRaises(ValueError):
            gate.validate_generated_items_provenance(bad_proof, PUBLIC)

    def test_generated_adoption_and_exact_current_missing_catalogs_share_existing_plan(self):
        canonical = self.root / "canonical"
        canonical.mkdir()
        (canonical / "items.dat").write_bytes(b"previous generated fixture")
        (canonical / "private.json").write_bytes(b'{"repair":true}')
        (canonical / "present.json").write_bytes(b'{"private":true}')
        receipt_path, receipt, proof = make_generated_fixture(self.root)
        rows, states = gate.inventory(canonical)
        bundle = self.root / "plan"
        bundle.mkdir()
        gate.write_json(bundle / "before-manifest.json", rows)
        data = {"Research.json": b'{"research":true}', "DefaultNpc.json": b'{"npc":true}', "present.json": b'{"upstream":true}'}
        source = {gate.blob(raw): raw for raw in data.values()}
        def tree(root, profile, authority, sha):
            return {path: {"blob": gate.blob(raw), "size": len(raw)} for path, raw in data.items()
                    if (path == "Research.json") == (authority == "PUBLIC_PRODUCT_DATA")}
        mappings = [{"AUTHORITY": "PUBLIC_PRODUCT_DATA" if path == "Research.json" else "PRIVATE_CONTENT_UPSTREAM",
                     "PATH": path, "APPROVAL": "Explicit synthetic required-catalog approval"} for path in data]
        profile = {"CANONICAL_GAMEDATA_ROOT": str(canonical), "STATE_ROOT": str(self.root),
                   "PROTECTED_GENERATED_FILES": ["items.dat"], "REQUIRE_GENERATED_ITEMS": True, "REQUIRED_CURRENT_CATALOGS": mappings}
        baseline = {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP}
        with mock.patch.object(gate, "tree_files", tree), mock.patch.object(gate, "source_blob", side_effect=lambda r,p,a,b: source[b]), \
                mock.patch.object(gate, "git", return_value=b"ignored"), mock.patch.object(gate, "exporter_source_closure", return_value=receipt["EXPORTER_SOURCE_CLOSURE"]):
            plan, projected = gate.reconcile_plan(self.root, profile, bundle, PUBLIC, DUMP, baseline, rows, generated_receipt=receipt_path)
            gate.validate_reconciliation(bundle, plan)
            summary = gate.summarize_reconciliation(bundle, plan)
            self.assertEqual(gate.validate_reconciliation_summary(bundle, summary), gate.read_json(bundle / "manifest.json"))
            self.assertEqual({row["PATH"] for row in plan["CATALOG_RESTORATIONS"]}, {"Research.json", "DefaultNpc.json"})
            self.assertEqual(set(projected), {"Research.json", "DefaultNpc.json", "items.dat"})
            self.assertNotIn("STAGED_PATH", gate.encoded(summary).decode())
            self.assertNotIn(str(self.root / "client"), gate.encoded(summary).decode())
            attempt = {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP, "WINDOWS_GAMEDATA_MANIFEST": plan["WINDOWS_GAMEDATA_MANIFEST"]}
            with mock.patch.object(gate, "intake", return_value=(bundle, attempt, plan, projected, states)):
                gate.apply_content(self.root, profile, gate.digest(gate.encoded(plan)))
            self.assertEqual((canonical / "items.dat").read_bytes(), b"new generated fixture")
            self.assertEqual((canonical / "present.json").read_bytes(), b'{"private":true}')
            self.assertEqual((canonical / "private.json").read_bytes(), b'{"repair":true}')
            next_bundle = self.root / "next-plan"
            next_bundle.mkdir()
            current, _ = gate.inventory(canonical)
            gate.write_json(next_bundle / "before-manifest.json", current)
            carried, projected = gate.reconcile_plan(self.root, profile, next_bundle, PUBLIC, DUMP,
                gate.read_json(self.root / "canonical-state.json"), current)
            self.assertEqual(carried["GENERATED_ITEMS_PROVENANCE"], proof)
            self.assertEqual(projected, {})
            gate.summarize_reconciliation(next_bundle, carried)
        for mutate in (lambda value: value["CATALOG_RESTORATIONS"].pop(),
                       lambda value: value["REQUIRED_CURRENT_CATALOGS"][0].update(PATH="unapproved.json"),
                       lambda value: value["GENERATED_ITEMS_ADOPTION"]["CANONICAL_AFTER"].update(size=1),
                       lambda value: value["GENERATED_ITEMS_PROVENANCE"].update(PUBLIC_MASTER_SHA="f" * 40)):
            bad = copy.deepcopy(summary)
            mutate(bad)
            with self.assertRaises(ValueError):
                gate.validate_reconciliation_summary(bundle, bad)

    def test_fresh_byte_identical_generation_is_valid_and_missing_current_receipt_blocks(self):
        canonical = self.root / "canonical"
        canonical.mkdir()
        (canonical / "items.dat").write_bytes(b"new generated fixture")
        receipt_path, receipt, _ = make_generated_fixture(self.root)
        rows, _ = gate.inventory(canonical)
        profile = {"CANONICAL_GAMEDATA_ROOT": str(canonical), "STATE_ROOT": str(self.root),
                   "PROTECTED_GENERATED_FILES": ["items.dat"], "REQUIRE_GENERATED_ITEMS": True}
        bundle = self.root / "plan"
        bundle.mkdir()
        gate.write_json(bundle / "before-manifest.json", rows)
        with mock.patch.object(gate, "tree_files", return_value={}), mock.patch.object(gate, "git", return_value=b"ignored"), \
                mock.patch.object(gate, "exporter_source_closure", return_value=receipt["EXPORTER_SOURCE_CLOSURE"]):
            with self.assertRaises(ValueError):
                gate.reconcile_plan(self.root, profile, bundle, PUBLIC, DUMP, {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP}, rows)
            plan, projected = gate.reconcile_plan(self.root, profile, bundle, PUBLIC, DUMP,
                {"PUBLIC_MASTER_SHA": PUBLIC, "CONTENT_DUMP_SHA": DUMP}, rows, generated_receipt=receipt_path)
        self.assertNotIn("GENERATED_ITEMS_ADOPTION", plan)
        self.assertEqual(projected, {})
        self.assertEqual(plan["CANONICAL_BEFORE_MANIFEST"], plan["WINDOWS_GAMEDATA_MANIFEST"])
        gate.summarize_reconciliation(bundle, plan)

    def test_generated_items_require_real_current_catalog_startup_and_remain_portable(self):
        bundle, _ = make_portable_fixture(self.root, generated=True)
        loaded = gate.load_accepted_release(self.root, "bundle/acceptance.json")
        self.assertEqual(loaded["reconciliation"]["GENERATED_ITEMS_PROVENANCE"]["VALIDATION"]["DECODED_POPULATION"], 3)
        exported = gate.export_accepted_bundle(self.root, bundle, bundle / "acceptance.json")
        self.assertFalse((exported.parent / "ignored-stage").exists())
        self.assertFalse(any(path.suffix in (".dat", ".dll", ".exe") for path in exported.parent.rglob("*")))
        windows, attempt = gate.read_json(bundle / "windows-receipt.json"), gate.read_json(bundle / "build-attempt.json")
        zone = bundle / "capture/ZoneEngine_New.out.delta.log"
        original = zone.read_text()
        for text in (original.replace("Loaded 3", "Loaded 2"), original.replace("ready with 3", "ready with 0"),
                     original.replace("/items.dat", "/stale-items.dat"), original + "Failed to load GameData items.dat\n"):
            zone.write_text(text)
            bad_windows = copy.deepcopy(windows)
            bad_windows["EVIDENCE"][zone.relative_to(bundle).as_posix()] = gate.digest(zone.read_bytes())
            raw = gate.read_json(bundle / "raw-windows-receipt.json")
            raw["EVIDENCE"] = bad_windows["EVIDENCE"]
            gate.write_json(bundle / "raw-windows-receipt.json", raw)
            bad_windows["RAW_WINDOWS_RECEIPT"] = gate.descriptor(bundle, bundle / "raw-windows-receipt.json")
            with self.assertRaises(ValueError):
                gate.validate_startup(bundle, bad_windows, attempt)

    def test_direct_product_build_cannot_use_prepared_receipt_or_arbitrary_child(self):
        profile = {"STATE_ROOT": str(self.root)}
        with mock.patch.dict(os.environ, {"AO_REBIRTH_BUILD_STATE": "", "AO_REBIRTH_BUILD_TOKEN": ""}):
            with self.assertRaises(ValueError):
                gate.verify_build_token(self.root, profile)
        with mock.patch.object(gate, "intake") as intake:
            with self.assertRaises(ValueError):
                gate.run_build(self.root, profile, ["cmd", "/c", "exit", "0"])
            intake.assert_not_called()

    def test_parallel_product_hooks_keep_immutable_state_and_all_observations(self):
        canonical = self.root / "canonical"
        canonical.mkdir()
        (canonical / "one.json").write_bytes(b"{}")
        files, directories = gate.content_states(canonical)
        active = self.root / "active.json"
        token = "synthetic-live-capability"
        gate.write_json(active, {"TOKEN_SHA256": gate.digest(token.encode()), "OWNER_PID": os.getpid(),
                        "PUBLIC_MASTER_SHA": PUBLIC, "FILE_STATES": files, "DIRECTORY_STATES": directories,
                        "CREATED_UTC": gate.now()})
        original = active.read_bytes()
        profile = {"STATE_ROOT": str(self.root), "CANONICAL_GAMEDATA_ROOT": str(canonical)}
        names = ["ChatEngine.WinNet10", "LoginEngine.WinNet10", "WorldEngine", "ZoneEngine_New"] * 8
        with mock.patch.dict(os.environ, {"AO_REBIRTH_BUILD_STATE": str(active), "AO_REBIRTH_BUILD_TOKEN": token}), \
                mock.patch.object(gate, "source_exact"):
            with ThreadPoolExecutor(max_workers=12) as workers:
                list(workers.map(lambda name: gate.verify_build_token(self.root, profile, product=name), names))
            gate.verify_build_token(self.root, profile, entrypoint="NewZoneEngineBuild/build.cmd")
        self.assertEqual(active.read_bytes(), original)
        observed = gate.collect_build_observations(active)
        self.assertTrue(observed["WRAPPER_ENTERED"])
        self.assertEqual(set(observed["PRODUCT_HOOKS"]), set(names))
        self.assertEqual(len(list((self.root / "build-observations").glob("*.json"))), len(names) + 1)

    def test_feature_command_cannot_publish_escape_or_execute_an_engine_or_migration(self):
        project = self.root / "AORebirth/Server/Fixture.Tests/Fixture.Tests.csproj"
        project.parent.mkdir(parents=True)
        project.write_text("<Project />")
        accepted = ["dotnet", "test", "AORebirth/Server/Fixture.Tests/Fixture.Tests.csproj"]
        self.assertEqual(gate.feature_command(self.root, accepted), project)
        for child in (["cmd", "/c", "exit", "0"], ["dotnet", "publish", accepted[2]],
                      [*accepted, "--no-build"], ["dotnet", "build", "../outside.csproj"],
                      ["dotnet", "run", "--project", accepted[2]]):
            with self.assertRaises(ValueError):
                gate.feature_command(self.root, child)

    def test_feature_source_records_dirty_changes_and_requires_feature_branch(self):
        (self.root / "NewSource.cs").write_bytes(b"feature source")
        answers = [PUBLIC.encode(), b"codex/feature", b"tracked feature diff", b"NewSource.cs\0"]
        with mock.patch.object(gate, "git", side_effect=answers):
            source = gate.feature_source(self.root)
        self.assertEqual(source["HEAD"], PUBLIC)
        self.assertEqual(source["TRACKED_DIFF_SHA256"], gate.digest(b"tracked feature diff"))
        self.assertEqual(source["UNTRACKED_SOURCE"][0]["sha256"], gate.digest(b"feature source"))
        with mock.patch.object(gate, "git", side_effect=[PUBLIC.encode(), b"master"]), self.assertRaises(ValueError):
            gate.feature_source(self.root)

    def test_feature_existing_chest_wrappers_are_exact_and_own_the_engine_paths(self):
        paths = ("Tools/run_mission_dao_validation.cmd", "Tools/run_newengine_connected_acceptance.cmd",
                 "Tools/MissionDaoValidation/MissionDaoValidation.csproj", "Tools/ZoneEngineSchemaValidation/ZoneEngineSchemaValidation.csproj",
                 "AORebirth/Built/Debug/ZoneEngine_New/ZoneEngine_New.dll", "AORebirth/Built/Debug/LoginEngine.exe",
                 "AORebirth/Built/Debug/WorldEngine/WorldEngine.dll")
        for name in paths:
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"synthetic wrapper/project/output fixture")
        prefix = [os.environ.get("ComSpec", r"C:\Windows\System32\cmd.exe"), "/d", "/c"]
        dao = [*prefix, str(self.root / paths[0]), "--chests-only"]
        self.assertEqual(gate.feature_command(self.root, dao), self.root / paths[2])
        connected = [*prefix, str(self.root / paths[1]), "--engine", str(self.root / paths[4]),
                     "--login-engine", str(self.root / paths[5]), "--chests-only", "--world-engine", str(self.root / paths[6])]
        self.assertEqual(gate.feature_command(self.root, connected), self.root / paths[3])
        for child in ([*dao, "extra"], [*prefix, str(self.root / paths[0]), "--isolated-sources"],
                      [*connected[:5], str(self.root / paths[5]), *connected[6:]], [*connected, "extra"]):
            with self.assertRaises(ValueError):
                gate.feature_command(self.root, child)

    def test_source_only_dao_wrapper_records_real_output_without_fabricating_product_hooks(self):
        canonical = self.root / "canonical"
        canonical.mkdir()
        (canonical / "one.json").write_bytes(b"{}")
        for name in ("Tools/run_mission_dao_validation.cmd", "Tools/run_newengine_connected_acceptance.cmd",
                     "Tools/MissionDaoValidation/MissionDaoValidation.csproj"):
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"synthetic source-only validation fixture")
        profile = {"STATE_ROOT": str(self.root / "ignored"), "CANONICAL_GAMEDATA_ROOT": str(canonical)}
        source = {"HEAD": PUBLIC, "BRANCH": "codex/fixture", "TRACKED_DIFF_SHA256": gate.digest(b"draft"), "UNTRACKED_SOURCE": []}
        output = self.root / "Tools/MissionDaoValidation/bin/Release/net10.0/MissionDaoValidation.dll"
        def run_child(child, cwd, env, check):
            self.assertEqual(env["MSBUILDDISABLENODEREUSE"], "1")
            self.assertEqual(env["DOTNET_CLI_USE_MSBUILD_SERVER"], "0")
            output.parent.mkdir(parents=True)
            output.write_bytes(b"synthetic compiled assembly for offline receipt test")
            return subprocess.CompletedProcess(child, 0)
        child = [os.environ.get("ComSpec", r"C:\Windows\System32\cmd.exe"), "/d", "/c",
                 str(self.root / "Tools/run_mission_dao_validation.cmd"), "--chests-only"]
        with mock.patch.object(gate, "feature_source", return_value=source), mock.patch.object(gate.subprocess, "run", side_effect=run_child):
            self.assertEqual(gate.run_feature_validation(self.root, profile, child), 0)
        attempt = gate.read_json(next((self.root / "ignored").glob("feature-validation-*/feature-validation.json")))
        self.assertEqual(attempt["PRODUCT_COMPILER_HOOKS"], [])
        self.assertEqual(attempt["SOURCE_ONLY_VALIDATION_TOOL"]["sha256"], gate.digest(output.read_bytes()))
        self.assertFalse(attempt["DEPLOYABLE"])

    def test_feature_validation_owns_real_capability_and_cannot_be_release_acceptance(self):
        canonical = self.root / "canonical"
        canonical.mkdir()
        (canonical / "one.json").write_bytes(b"{}")
        project = self.root / "AORebirth/Server/Fixture.Tests/Fixture.Tests.csproj"
        project.parent.mkdir(parents=True)
        project.write_text("<Project />")
        profile = {"STATE_ROOT": str(self.root / "ignored"), "CANONICAL_GAMEDATA_ROOT": str(canonical)}
        source = {"HEAD": PUBLIC, "BRANCH": "codex/fixture", "TRACKED_DIFF_SHA256": gate.digest(b"draft"), "UNTRACKED_SOURCE": []}
        def run_child(child, cwd, env, check):
            self.assertEqual(env["MSBUILDDISABLENODEREUSE"], "1")
            self.assertEqual(env["DOTNET_CLI_USE_MSBUILD_SERVER"], "0")
            with mock.patch.dict(os.environ, env):
                gate.verify_build_token(self.root, profile, product="Fixture.Tests")
                with self.assertRaisesRegex(ValueError, "cannot publish"):
                    gate.verify_build_token(self.root, profile, product="Fixture.Tests", publishing=True)
                with self.assertRaisesRegex(ValueError, "another checkout"):
                    gate.verify_build_token(self.root / "another-checkout", profile)
                with mock.patch.object(gate, "feature_source", return_value={**source, "HEAD": "f" * 40}):
                    with self.assertRaisesRegex(ValueError, "source changed"):
                        gate.verify_build_token(self.root, profile)
            return subprocess.CompletedProcess(child, 0)
        with mock.patch.object(gate, "feature_source", return_value=source), mock.patch.object(gate.subprocess, "run", side_effect=run_child):
            self.assertEqual(gate.run_feature_validation(self.root, profile,
                ["dotnet", "test", "AORebirth/Server/Fixture.Tests/Fixture.Tests.csproj"]), 0)
        receipt_path = next((self.root / "ignored").glob("feature-validation-*/feature-validation.json"))
        attempt = gate.read_json(receipt_path)
        self.assertEqual(attempt["VALIDATION_RESULT"], "PASS")
        self.assertEqual(attempt["WINDOWS_USER_ACCEPTED"], "NO")
        self.assertFalse(attempt["DEPLOYABLE"])
        self.assertFalse((receipt_path.parent / "active.json").exists())
        self.assertFalse((receipt_path.parent / "build-attempt.json").exists())
        with self.assertRaisesRegex(ValueError, "not a production build"):
            gate.require_successful_attempt(receipt_path.parent, attempt)

    def test_fresh_authority_fetch_failures_do_not_fall_back_to_cached_state(self):
        profile = {"PUBLIC_PRODUCT_REMOTE": "origin", "PRIVATE_CONTENT_BRANCH": "main"}
        def public_git(root, *args):
            if args[:2] == ("remote", "get-url"):
                return b"https://github.com/subarumike/AORebirth.git\n"
            if args[0] == "fetch":
                raise ValueError("fetch failed")
            return (PUBLIC + "\n").encode()
        with mock.patch.object(gate, "git", public_git), mock.patch.object(gate, "gh") as gh:
            with self.assertRaises(ValueError):
                gate.fresh_sources(self.root, profile)
            gh.assert_not_called()
        with mock.patch.object(gate, "git", side_effect=[b"https://github.com/subarumike/AORebirth.git", b"", PUBLIC.encode()]), \
                mock.patch.object(gate, "source_exact"), mock.patch.object(gate, "gh", side_effect=ValueError("unavailable")):
            with self.assertRaises(ValueError):
                gate.fresh_sources(self.root, profile)

    def test_source_head_or_runtime_drift_is_rejected(self):
        for answers in ((b"f" * 40,), (PUBLIC.encode(), b"AORebirth/Server/ZoneEngine_New/Program.cs\n", b""),
                        (PUBLIC.encode(), b"", b"AORebirth/Server/ZoneEngine_New/Added.cs\n")):
            with mock.patch.object(gate, "git", side_effect=answers), self.assertRaises(ValueError):
                gate.source_exact(self.root, PUBLIC)

    def test_portable_descriptor_escape_is_denied(self):
        bundle, _ = make_portable_fixture(self.root)
        for path in ("../outside", "/absolute", "C:/absolute", "folder\\file", "a/../file"):
            with self.assertRaises(ValueError):
                gate.checked(bundle, path)

    def test_public_hook_scopes_real_products_and_blocks_linux_publish_before_copy(self):
        xml = ET.parse(ROOT / "Directory.Build.targets").getroot()
        self.assertEqual(set(xml.attrib["TreatAsLocalProperty"].split(";")), {"AORebirthProductEntry", "AORebirthNormalizedProjectDirectory", "AORebirthServerOwner", "AORebirthLibraryOwner"})
        targets = {item.attrib["Name"]: item for item in xml.findall("Target")}
        for platform in ("Windows", "OnlineLinux"):
            compile_target = targets["RequireGoverned" + platform + "ProductBuild"]
            publish_target = targets["RequireGoverned" + platform + "ProductPublish"]
            self.assertIn("AORebirthProductEntry", compile_target.attrib["Condition"])
            self.assertIn("SkipCompilerExecution", compile_target.attrib["Condition"])
            self.assertEqual(compile_target.attrib["BeforeTargets"], "PrepareForBuild;CoreCompile")
            self.assertIn("AORebirthProductEntry", publish_target.attrib["Condition"])
            self.assertNotIn("SkipCompilerExecution", publish_target.attrib["Condition"])
            self.assertNotIn("DesignTimeBuild", publish_target.attrib["Condition"])
            self.assertEqual(publish_target.attrib["BeforeTargets"], "PrepareForPublish;ComputeFilesToPublish;Publish")
        self.assertIsNotNone(targets["RequireGovernedOnlineLinuxProductBuild"].find("Error"))
        self.assertIsNotNone(targets["RequireGovernedOnlineLinuxProductPublish"].find("Error"))
        self.assertNotIn("ChatEngine.Linux", (ROOT / "Directory.Build.targets").read_text())
        self.assertNotIn("LoginEngine.Linux", (ROOT / "Directory.Build.targets").read_text())
        self.assertIn("AORebirthLibraryOwner", (ROOT / "Directory.Build.targets").read_text())
        script = (ROOT / "NewZoneEngineBuild/build.cmd").read_text()
        self.assertLess(script.index("run-build"), script.index("call :RestorePackagesConfig"))
        self.assertLess(script.index("verify-build-token"), script.index("stop-engines.cmd"))


@unittest.skipUnless(os.name == "nt", "Real Windows MSBuild regression requires Windows")
class DeveloperMSBuildTests(unittest.TestCase):
    """Compile one harmless temporary class; never build or start an AORebirth engine."""

    @classmethod
    def setUpClass(cls):
        locator = Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)")) / "Microsoft Visual Studio/Installer/vswhere.exe"
        if not locator.is_file():
            raise RuntimeError("Windows build-contract regression requires Visual Studio MSBuild and vswhere.exe")
        result = subprocess.run([str(locator), "-latest", "-products", "*", "-requires", "Microsoft.Component.MSBuild",
                                 "-find", r"MSBuild\**\Bin\MSBuild.exe"],
                                capture_output=True, text=True, check=True, timeout=30)
        candidates = [Path(line.strip()) for line in result.stdout.splitlines() if line.strip()]
        cls.msbuild = next((path for path in candidates if path.is_file()), None)
        if cls.msbuild is None:
            raise RuntimeError("Windows build-contract regression could not locate installed Visual Studio MSBuild")

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="aorebirth-build-contract-")
        self.root = Path(self.temp.name)
        self.addCleanup(self.temp.cleanup)
        for name in ("Directory.Build.targets", "tools/windows_release_contract.py", "tools/windows_release_contract.cmd",
                     "tools/select_python_runtime.cmd"):
            destination = self.root / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, destination)
        self.project = self.root / "AORebirth/Server/DeveloperProbe/DeveloperProbe.csproj"
        self.project.parent.mkdir(parents=True)
        self.project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                                '<TargetFramework>net10.0</TargetFramework><OutputType>Library</OutputType>'
                                '<ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable>'
                                '<UseSharedCompilation>false</UseSharedCompilation><NuGetAudit>false</NuGetAudit>'
                                '</PropertyGroup></Project>')
        (self.project.parent / "Probe.cs").write_text("internal static class DeveloperProbe { internal static int Value => 42; }\n")
        (self.root / "NuGet.Config").write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        (self.root / ".gitignore").write_text(".local/\ncanonical/\n**/bin/\n**/obj/\n")
        self.environment = dict(os.environ)
        for name in ("AO_REBIRTH_BUILD_CHILD", "AO_REBIRTH_BUILD_TOKEN", "AO_REBIRTH_BUILD_STATE", "AO_REBIRTH_RELEASE_CONFIG"):
            self.environment.pop(name, None)
        self.environment.update(AO_REBIRTH_PYTHON_EXE=sys.executable, MSBUILDDISABLENODEREUSE="1",
                                DOTNET_CLI_USE_MSBUILD_SERVER="0", DOTNET_CLI_TELEMETRY_OPTOUT="1",
                                DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull)

    def msbuild_probe(self, target="Restore;Build", configuration="Debug", properties=(), environment=None):
        command = [str(self.msbuild), str(self.project), "/t:" + target, "/p:Configuration=" + configuration,
                   "/m:1", "/nr:false", "/nologo", "/v:minimal", *properties]
        return subprocess.run(command, cwd=self.root, env={**self.environment, **(environment or {})},
                              stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8",
                              errors="replace", check=False, timeout=120)

    def assert_compiled(self, result, configuration):
        self.assertEqual(result.returncode, 0, result.stdout[-9000:])
        assembly = self.project.parent / "bin" / configuration / "net10.0/DeveloperProbe.dll"
        self.assertTrue(assembly.is_file(), result.stdout[-9000:])
        self.assertEqual(assembly.read_bytes()[:2], b"MZ")

    def test_public_clone_compiles_debug_without_private_profile(self):
        self.assert_compiled(self.msbuild_probe(), "Debug")
        self.assertFalse((self.root / ".local").exists())

    def test_visual_studio_and_ordinary_release_compilation_need_no_profile(self):
        for configuration, properties in (("Debug", ("/p:BuildingInsideVisualStudio=true",)), ("Release", ())):
            with self.subTest(configuration=configuration):
                self.assert_compiled(self.msbuild_probe(configuration=configuration,
                                                       properties=properties), configuration)
        self.assertFalse((self.root / ".local").exists())

    def test_explicit_governed_build_and_windows_publish_still_require_profile(self):
        for target, properties in (("Restore;Build", ("/p:AORebirthGovernedRelease=true",)),
                                   ("Restore;Publish", ("/p:AORebirthGovernedRelease=false",))):
            with self.subTest(target=target):
                result = self.msbuild_probe(target=target, properties=properties)
                self.assertNotEqual(result.returncode, 0, result.stdout[-9000:])
                self.assertIn("WINDOWS_RELEASE_CONTRACT=FAIL", result.stdout)
                self.assertIn("release-contract.json", result.stdout)
        self.assertFalse((self.root / ".local").exists())

    def test_each_active_build_marker_cannot_opt_out_with_false_release_property(self):
        for name in ("AO_REBIRTH_BUILD_CHILD", "AO_REBIRTH_BUILD_TOKEN", "AO_REBIRTH_BUILD_STATE"):
            with self.subTest(marker=name):
                result = self.msbuild_probe(properties=("/p:AORebirthGovernedRelease=false",), environment={name: "fixture-invalid"})
                self.assertNotEqual(result.returncode, 0, result.stdout[-9000:])
                self.assertIn("WINDOWS_RELEASE_CONTRACT=FAIL", result.stdout)
        self.assertFalse((self.root / ".local").exists())

    def test_developer_verify_build_token_tolerates_active_build_markers(self):
        for name in ("AO_REBIRTH_BUILD_CHILD", "AO_REBIRTH_BUILD_TOKEN", "AO_REBIRTH_BUILD_STATE"):
            with self.subTest(marker=name):
                command = [sys.executable, str(TOOLS / "windows_release_contract.py"),
                           "verify-build-token", "--repository-root", str(self.root), "--developer-build",
                           "--entrypoint", "NewZoneEngineBuild/build.cmd"]
                result = subprocess.run(command, cwd=self.root, env={**self.environment, name: "fixture-invalid"},
                                        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8",
                                        errors="replace", check=False, timeout=120)
                self.assertEqual(result.returncode, 0, result.stdout[-9000:])
                self.assertIn("WINDOWS_BUILD_SCOPE=DEVELOPER", result.stdout)
                self.assertIn("DEPLOYABLE=NO", result.stdout)
        self.assertFalse((self.root / ".local").exists())

    def test_live_synthetic_governed_capability_still_validates_and_observes_compiler(self):
        def local_git(*arguments):
            return subprocess.run(["git", *arguments], cwd=self.root, env=self.environment,
                                  capture_output=True, text=True, check=True, timeout=30).stdout.strip()
        local_git("init", "--initial-branch=main")
        local_git("add", ".")
        local_git("-c", "user.name=AORebirth Contract Fixture", "-c", "user.email=fixture@example.invalid",
                  "commit", "-m", "Synthetic offline compiler guard fixture")
        public = local_git("rev-parse", "HEAD")
        canonical = self.root / "canonical"
        canonical.mkdir()
        (canonical / "fixture.json").write_bytes(b'{"FIXTURE_ONLY":true}\n')
        state_root = self.root / ".local/state"
        state_root.mkdir(parents=True)
        provenance = self.root / ".local/initial.json"
        provenance.write_bytes(b'{"FIXTURE_ONLY":true}\n')
        gate.write_json(self.root / ".local/release-contract.json", {
            "FORMAT_VERSION": 1, "PRIVATE_CONTENT_REPOSITORY": "fixture/not-a-real-release",
            "PRIVATE_CONTENT_BRANCH": "main", "PRIVATE_CONTENT_PREFIX": "Output",
            "CANONICAL_GAMEDATA_ROOT": str(canonical), "STATE_ROOT": str(state_root),
            "INITIAL_PROVENANCE": str(provenance), "INITIAL_PROVENANCE_SHA256": gate.digest(provenance.read_bytes())})
        files, directories = gate.content_states(canonical)
        token = "synthetic-test-capability-" + os.urandom(16).hex()
        active = state_root / "active.json"
        gate.write_json(active, {"TOKEN_SHA256": gate.digest(token.encode()), "OWNER_PID": os.getpid(),
                        "PUBLIC_MASTER_SHA": public, "FILE_STATES": files, "DIRECTORY_STATES": directories,
                        "CREATED_UTC": gate.now(), "FIXTURE_ONLY": True})
        before = active.read_bytes()
        result = self.msbuild_probe(properties=("/p:AORebirthGovernedRelease=false",), environment={
            "AO_REBIRTH_BUILD_CHILD": "1", "AO_REBIRTH_BUILD_TOKEN": token, "AO_REBIRTH_BUILD_STATE": str(active)})
        self.assert_compiled(result, "Debug")
        self.assertEqual(active.read_bytes(), before)
        self.assertEqual(gate.collect_build_observations(active)["PRODUCT_HOOKS"], ["DeveloperProbe"])
        self.assertFalse((state_root / "acceptance.json").exists())


class ExporterReuseQualificationTests(unittest.TestCase):
    """Only inert, payload-bound changes may reuse a measured item export."""

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.request = "d" * 40
        self.paths = "AORebirth/Libraries/Source/AORebirth.Core/GameData/GameDataPaths.cs"
        self.declaration = ('        /// <summary>Hash spawn points that are not loaded (ZoneEngine_New SpawnExclusionCatalog).</summary>\n'
                            '        public const string SpawnExclusionsFileName = "SpawnExclusions.json";\n\n')
        condition = "'$(AORebirthProductEntry)' == 'true' And !('$(DesignTimeBuild)' == 'true')"
        self.clause = ("And ('$(AORebirthGovernedRelease)' == 'true' Or '$(AO_REBIRTH_BUILD_CHILD)' != '' "
                       "Or '$(AO_REBIRTH_BUILD_TOKEN)' != '' Or '$(AO_REBIRTH_BUILD_STATE)' != '') ")
        old_xml = ('<Project><Target Name="RequireGovernedWindowsProductBuild" Condition="' + condition
                   + '"><Exec Command="verify-build-token" /></Target></Project>')
        self.before = {gate.EXPORTER_PROJECT: b"synthetic project", "Tools/RDBDataExtractor/Exporter.cs": b"class Exporter {}",
                       "Directory.Build.targets": old_xml.encode(), self.paths: b"class GameDataPaths {\n}\n"}
        self.after = dict(self.before)
        self.after["Directory.Build.targets"] = old_xml.replace("And !(", self.clause + "And !(").encode()
        self.after[self.paths] = ("class GameDataPaths {\n" + self.declaration + "}\n").encode()

    @staticmethod
    def rows(values):
        return [gate.content_identity(data, path) for path, data in sorted(values.items())]

    def qualify(self):
        def source(root, *args):
            self.assertEqual(args[0], "show")
            sha, path = args[1].split(":", 1)
            return (self.before if sha == PUBLIC else self.after)[path]
        with mock.patch.object(gate, "git", side_effect=source):
            return gate.qualify_exporter_sources(self.root, PUBLIC, self.request, self.rows(self.before), self.rows(self.after))

    def test_accepts_only_the_two_reviewed_inert_changes_and_portable_replay(self):
        value = self.qualify()
        self.assertEqual(value["CURRENT_SOURCE_CLOSURE"], self.rows(self.after))
        self.assertEqual(len(value["INERT_CHANGES"]), 2)
        gate.validate_exporter_source_qualification(self.rows(self.before), value)

    def test_rejects_algorithm_project_package_or_added_source_changes(self):
        for path in ("Tools/RDBDataExtractor/Exporter.cs", gate.EXPORTER_PROJECT):
            with self.subTest(path=path):
                original = self.after[path]
                self.after[path] += b" changed algorithm or package"
                with self.assertRaises(ValueError):
                    self.qualify()
                self.after[path] = original
        self.after["Tools/RDBDataExtractor/Added.cs"] = b"new source"
        with self.assertRaisesRegex(ValueError, "path set"):
            self.qualify()

    def test_rejects_msbuild_command_or_compiler_input_changes(self):
        for transform in (lambda b: b.replace(b"verify-build-token", b"change-inputs"),
                          lambda b: b.replace(b"</Project>", b'<ItemGroup><Compile Include="injected.cs" /></ItemGroup></Project>')):
            with self.subTest(transform=transform):
                value = transform(self.after["Directory.Build.targets"])
                with self.assertRaises(ValueError):
                    gate.inert_exporter_source_change("Directory.Build.targets", self.before["Directory.Build.targets"], value)

    def test_rejects_used_constant_type_reflection_and_changed_shared_code(self):
        path = "Tools/RDBDataExtractor/Exporter.cs"
        for source in (b"GameDataPaths.SpawnExclusionsFileName", b"typeof(GameDataPaths)", b"assembly.GetTypes()"):
            with self.subTest(source=source):
                original = self.before[path]
                self.before[path] = self.after[path] = source
                with self.assertRaisesRegex(ValueError, "referenced|reflection"):
                    self.qualify()
                self.before[path] = self.after[path] = original
        for changed in (self.after[self.paths].replace(b"SpawnExclusions.json", b"items.dat"), self.after[self.paths] + b"new method"):
            with self.assertRaises(ValueError):
                gate.inert_exporter_source_change(self.paths, self.before[self.paths], changed)

    def test_rejects_tampered_or_omitted_portable_qualification(self):
        valid = self.qualify()
        for mutate in (lambda v: v["INERT_CHANGES"].pop(),
                       lambda v: v["INERT_CHANGES"][0].update(AFTER="eA=="),
                       lambda v: v["INERT_CHANGES"][0].update(CLASSIFICATION="ignored"),
                       lambda v: v["CURRENT_SOURCE_CLOSURE"][0].update(sha256="f" * 64)):
            value = copy.deepcopy(valid)
            mutate(value)
            with self.assertRaises(ValueError):
                gate.validate_exporter_source_qualification(self.rows(self.before), value)

    def test_generated_reuse_preserves_original_receipt_and_output_identities(self):
        path, receipt, _ = make_generated_fixture(self.root)
        receipt["EXPORTER_SOURCE_CLOSURE"] = self.rows(self.before)
        gate.write_json(path, receipt)
        raw = path.read_bytes()
        def source(root, *args):
            if args[0] == "check-ignore":
                return b"ignored"
            sha, relative = args[1].split(":", 1)
            return (self.before if sha == PUBLIC else self.after)[relative]
        with mock.patch.object(gate, "git", side_effect=source), \
                mock.patch.object(gate, "exporter_source_closure", side_effect=[self.rows(self.before), self.rows(self.after)]):
            proof, data, observed = gate.generated_items_receipt(self.root, {"CANONICAL_GAMEDATA_ROOT": str(self.root / "canonical")}, path, self.request)
        self.assertEqual(path.read_bytes(), raw)
        self.assertEqual(observed, raw)
        self.assertEqual(proof["PUBLIC_MASTER_SHA"], PUBLIC)
        self.assertEqual(proof["EXPORTER_SOURCE_CLOSURE"], receipt["EXPORTER_SOURCE_CLOSURE"])
        self.assertEqual(proof["OUTPUT"]["sha256"], gate.digest(data))
        self.assertIn("SOURCE_QUALIFICATION", proof["QUALIFIED_REUSE"])
        gate.validate_generated_items_provenance(proof, self.request)

    def test_historical_git_closure_does_not_relax_current_checkout_validation(self):
        self.before[gate.EXPORTER_PROJECT] = self.after[gate.EXPORTER_PROJECT] = b'<Project Sdk="Microsoft.NET.Sdk" />'
        for relative, data in self.after.items():
            target = self.root / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        def source(root, *args):
            if args[0] == "show":
                sha, relative = args[1].split(":", 1)
                return (self.before if sha == PUBLIC else self.after)[relative]
            if args[0] == "hash-object":
                return gate.blob(self.after[args[-1]]).encode()
            if args[:2] == ("ls-tree", "-r"):
                return "\n".join(sorted(self.before)).encode()
            if args[0] == "ls-tree":
                return args[-1].encode() if args[-1] in self.before else b""
            return b""
        with mock.patch.object(gate, "git", side_effect=source):
            with self.assertRaisesRegex(ValueError, "Current exporter source differs"):
                gate.exporter_source_closure(self.root, PUBLIC)
            self.assertEqual(gate.exporter_source_closure(self.root, PUBLIC, verify_working_tree=False), self.rows(self.before))
            self.assertEqual(gate.exporter_source_closure(self.root, self.request), self.rows(self.after))


if __name__ == "__main__":
    unittest.main()
