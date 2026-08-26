from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path


TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import import_official_playfield_placements as importer


ADDITIONAL_ISLAND_REET_NPC_IDS = {
    1007852,
    1007853,
    1007854,
    1007855,
    1007856,
    1007857,
    1007859,
    1007860,
    1007861,
    1007987,
}
ALL_ISLAND_REET_NPC_IDS = ADDITIONAL_ISLAND_REET_NPC_IDS | {1007858}


class OfficialPlayfieldRuntimeAuthorizationRefreshTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.artifacts, _ = importer._load_pf4582_artifacts(
            importer.REPOSITORY_ROOT
        )
        cls.authorizations = importer._build_pf4582_runtime_authorizations(
            cls.artifacts
        )
        explicit_ids = {
            row["npcId"]
            for row in cls.artifacts["RuntimeEvidenceMap"]["runtimeMappings"]
        }
        cls.previous_authorized_ids = explicit_ids - ADDITIONAL_ISLAND_REET_NPC_IDS

    def test_bounded_transition_changes_only_runtime_authorization_fields(self) -> None:
        tracked = importer._load_json(
            importer.REPOSITORY_ROOT
            / "docs"
            / "generated"
            / "playfields"
            / "placements"
            / "pf_4582.json"
        )
        prior = copy.deepcopy(tracked)
        for record in prior["Records"]:
            if record.get("SourceNpcId") in self.previous_authorized_ids:
                continue
            record.update(
                {
                    "BehaviorReady": False,
                    "BehaviorReadiness": "UNPROVEN",
                    "CurrentRuntimeActive": False,
                    "ExistingAoRebirthProfile": None,
                    "IdentityResolved": False,
                    "IdentityResolutionStatus": (
                        "SOURCE_PLACEMENT_RECONCILED_IDENTITY_UNRESOLVED"
                    ),
                    "RuntimeActivationAuthorized": False,
                }
            )

        self.assertEqual(
            importer.PF4582_PREVIOUS_RUNTIME_ACTIVE_COUNT,
            sum(
                record["RuntimeActivationAuthorized"] is True
                for record in prior["Records"]
            ),
        )
        refreshed, newly_authorized = importer.refresh_pf4582_runtime_authorization(
            prior, self.artifacts
        )
        self.assertEqual(importer.PF4582_NEWLY_AUTHORIZED_COUNT, newly_authorized)
        self.assertEqual(
            [
                importer._pf4582_static_record_projection(record)
                for record in prior["Records"]
            ],
            [
                importer._pf4582_static_record_projection(record)
                for record in refreshed["Records"]
            ],
        )
        for before, after in zip(prior["Records"], refreshed["Records"]):
            changed_fields = {
                key for key in before if before.get(key) != after.get(key)
            }
            self.assertTrue(
                changed_fields.issubset(
                    importer.PF4582_RUNTIME_AUTHORIZATION_FIELDS
                )
            )

    def test_exact_official_island_reet_set_reuses_one_profile(self) -> None:
        model = importer.build_runtime_authorization_refresh_model()
        records = model.placement_shards[4582]["Records"]
        active = [
            record
            for record in records
            if record["RuntimeActivationAuthorized"] is True
        ]
        blocked = [
            record
            for record in records
            if record["RuntimeActivationAuthorized"] is False
        ]
        self.assertEqual(importer.PF4582_RUNTIME_ACTIVE_COUNT, len(active))
        self.assertEqual(importer.PF4582_OFFICIAL_RUNTIME_BLOCKED_COUNT, len(blocked))
        self.assertEqual(
            set(importer.PF4582_BLOCKED_SOURCE_NPC_TAGS.values()) | {"NCNN"},
            {record["CanonicalAcgHashText"] for record in blocked},
        )
        reets = {
            record["SourceNpcId"]: record
            for record in records
            if record.get("ExistingAoRebirthProfile")
            == "IccShuttleportSpawn:Island Reet"
        }
        self.assertEqual(ALL_ISLAND_REET_NPC_IDS, set(reets))
        self.assertTrue(
            all(
                record["CanonicalAcgHashText"] == "ISRE"
                and record["CurrentRuntimeActive"] is True
                and record["RuntimeActivationAuthorized"] is True
                for record in reets.values()
            )
        )
        ncnn = next(
            record
            for record in records
            if record["CanonicalAcgHashText"] == "NCNN"
        )
        self.assertIsNone(ncnn["SourceNpcId"])
        self.assertIsNone(ncnn["ExistingAoRebirthProfile"])
        self.assertIs(ncnn["CurrentRuntimeActive"], False)
        self.assertIs(ncnn["RuntimeActivationAuthorized"], False)

    def test_exact_authorization_map_contains_199_and_only_seven_source_ids_blocked(self) -> None:
        self.assertEqual(importer.PF4582_RUNTIME_ACTIVE_COUNT, len(self.authorizations))
        self.assertEqual(
            importer.PF4582_PREVIOUS_RUNTIME_ACTIVE_COUNT,
            len(self.previous_authorized_ids),
        )
        overlay_ids = {
            row["SourceNpcId"]
            for row in self.artifacts["OfficialOverlay"]["Records"]
            if row["SourceNpcId"] is not None
        }
        self.assertEqual(
            set(importer.PF4582_BLOCKED_SOURCE_NPC_TAGS),
            overlay_ids - set(self.authorizations),
        )

    def test_fabricated_known_profile_is_rejected(self) -> None:
        mutated = copy.deepcopy(self.artifacts)
        fdqo = next(
            row
            for row in mutated["RuntimeEvidenceMap"]["templateProfileMappings"]
            if row["templateHash"] == 1330725958
        )
        fdqo["runtimeProfile"] = "IccShuttleportSpawn:Fabricated FDQO"
        with self.assertRaisesRegex(
            importer.PlacementImportError,
            "template-profile identity bridge is invalid|profile crosswalk drift",
        ):
            importer._build_pf4582_runtime_authorizations(mutated)

    def test_every_unresolved_and_ncnn_activation_injection_is_rejected(self) -> None:
        tracked = importer._load_json(
            importer.REPOSITORY_ROOT
            / "docs"
            / "generated"
            / "playfields"
            / "placements"
            / "pf_4582.json"
        )
        blocked_ids = set(importer.PF4582_BLOCKED_SOURCE_NPC_TAGS) | {None}
        for source_npc_id in blocked_ids:
            mutated = copy.deepcopy(tracked)
            record = next(
                row
                for row in mutated["Records"]
                if row.get("SourceNpcId") == source_npc_id
                and (
                    source_npc_id is not None
                    or row.get("CanonicalAcgHashText") == "NCNN"
                )
            )
            record.update(
                {
                    "BehaviorReady": True,
                    "CurrentRuntimeActive": True,
                    "ExistingAoRebirthProfile": "IccShuttleportSpawn:Injected",
                    "IdentityResolved": True,
                    "RuntimeActivationAuthorized": True,
                }
            )
            with self.subTest(source_npc_id=source_npc_id):
                with self.assertRaisesRegex(
                    importer.PlacementImportError, "unmapped PF4582 record is active"
                ):
                    importer.refresh_pf4582_runtime_authorization(
                        mutated, self.artifacts
                    )

    def test_local_refresh_preserves_every_non_pf4582_payload(self) -> None:
        model = importer.build_runtime_authorization_refresh_model()
        outputs = importer.build_candidate_outputs(model)
        changed = importer.runtime_authorization_changed_outputs(outputs)
        allowed = {
            importer.REPOSITORY_ROOT.joinpath(*relative.parts)
            for relative in (
                importer.OUTPUT_SOURCE_MANIFEST,
                importer.OUTPUT_INDEX,
                importer.OUTPUT_SUMMARY,
                importer.OUTPUT_CORPUS_MANIFEST,
                importer.OUTPUT_PLACEMENT_ROOT / "pf_4582.json",
            )
        }
        self.assertTrue(set(changed).issubset(allowed))
        self.assertEqual(
            importer.PF4582_RUNTIME_ACTIVE_COUNT,
            model.corpus_manifest["Metrics"]["RuntimeActivationAuthorizedCount"],
        )
        self.assertEqual(
            importer.PF4582_NEWLY_AUTHORIZED_COUNT,
            model.summary["Metrics"]["NewRuntimeSpawnsActivated"],
        )
        acghash_path = importer.REPOSITORY_ROOT.joinpath(
            *importer.OUTPUT_ACGHASH.parts
        )
        self.assertEqual(acghash_path.read_bytes(), outputs[acghash_path])
        subway_path = importer.REPOSITORY_ROOT.joinpath(
            *(importer.OUTPUT_PLACEMENT_ROOT / "pf_127.json").parts
        )
        self.assertEqual(subway_path.read_bytes(), outputs[subway_path])


if __name__ == "__main__":
    unittest.main()
