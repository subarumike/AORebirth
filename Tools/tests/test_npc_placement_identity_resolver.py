import json
import tempfile
import unittest
from pathlib import Path

from Tools import npc_observation_harvester as harvester
from Tools import npc_placement_identity_resolver as resolver


def placement(
    placement_id: str,
    position=(1.0, 2.0, 3.0),
    *,
    radius=0.0,
    playfield=7,
    district_centre=None,
    corroborating=None,
):
    return {
        "placementId": placement_id,
        "playfieldId": playfield,
        "districtId": "district-0",
        "districtName": "Fixture",
        "sourcePosition": list(position),
        "worldPosition": None,
        "districtCentre": list(district_centre) if district_centre is not None else None,
        "spawnMetadata": {"radius": radius, "levelMinimum": 1, "levelMaximum": 220},
        "provenIdentifiers": {"recordOffsetInResource": 1},
        "unprovenIndirection": {"templateId": None, "monsterData": None},
        "acgHash": {"text": "TEST", "semanticState": "never runtime identity"},
        "provenCorroborating": corroborating or {},
    }


def observation(position=(1.0, 2.0, 3.0), **corroborating):
    return {
        "position": list(position),
        "resourcePlayfieldId": 7,
        "runtimePlayfieldId": 1000007,
        "corroborating": corroborating,
    }


def proven_mapping():
    return {
        "mappingStatus": "proven-capture-scoped",
        "mappingProven": True,
        "basePlayfieldResourceId": 7,
        "conflicts": [],
    }


def proven_transform(**overrides):
    values = {
        "name": "fixture-proven-transform",
        "evidence_class": resolver.EVIDENCE_PROVEN,
        "proven": True,
        "proof": "isolated decoder fixture",
    }
    values.update(overrides)
    return resolver.CoordinateTransform(**values)


def npc(capture_id: str, identity: str, position=(1.0, 2.0, 3.0)):
    provenance = {"captureId": capture_id}
    fields = {
        "monsterData": harvester.evidence(123, "packet-observed", provenance),
        "headMesh": harvester.evidence(456, "packet-observed", provenance),
        "textures": harvester.evidence([{"place": 0, "id": 789, "unknown": 0}], "packet-observed", provenance),
        "meshes": harvester.evidence([], "packet-observed", provenance),
        "breed": harvester.evidence("Human", "packet-observed", provenance),
        "gender": harvester.evidence("Male", "packet-observed", provenance),
        "race": harvester.evidence("Solitus", "packet-observed", provenance),
    }
    return harvester.NpcObservation(
        observation_id=capture_id + "|" + identity,
        capture_id=capture_id,
        capture_path="Captures/fixture-" + capture_id,
        identity=identity,
        resource_playfield_id=7,
        runtime_playfield_id=1000007,
        name="Fixture NPC",
        position=position,
        fields=fields,
    )


class NpcPlacementIdentityResolverNegativeTests(unittest.TestCase):
    def test_capture_info_values_without_same_epoch_pair_are_not_a_mapping(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary)
            (path / "capture_info.json").write_text(
                json.dumps({"playfieldId": "1000007", "resourcePlayfieldId": "7"}),
                encoding="utf-8",
            )
            record = harvester.CaptureRecord("20260101-000000", path, True, "fixture", 7, False)
            payload, mappings = resolver.build_runtime_playfield_mapping(
                [record], [npc(record.capture_id, "(SimpleChar:1)")], {7}
            )
            self.assertEqual(0, payload["provenCaptureMappings"])
            self.assertFalse(mappings[record.capture_id]["mappingProven"])

    def test_teleport_destination_proxy_is_corroborating_not_proven(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary)
            (path / "events.log").write_text(
                "2026-01-01T00:00:00Z [MISSION-FLOW] IN-N3-TELEPORT "
                "destPf=(51102:3BA) changePf=(Playfield2:2080A4)\n",
                encoding="utf-8",
            )
            record = harvester.CaptureRecord("20260101-000000", path, True, "fixture", 3081, False)
            pairs = resolver.capture_runtime_proxy_pairs(record)
            self.assertEqual(954, pairs[0]["destinationPlayfieldProxyId"])
            self.assertEqual(2130084, pairs[0]["runtimePlayfieldId"])
            self.assertEqual(resolver.EVIDENCE_CORROBORATING, pairs[0]["evidenceClass"])

    def test_same_epoch_instance_with_wrong_model_type_is_not_proven(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary)
            (path / "capture_info.json").write_text(
                json.dumps({"playfieldId": "1000007", "resourcePlayfieldId": "7"}),
                encoding="utf-8",
            )
            record = harvester.CaptureRecord("20260101-000000", path, True, "fixture", 7, False)
            evidence = {
                record.capture_id: {
                    "runtimePlayfieldId": 1000007,
                    "modelIdentityType": 999,
                    "modelIdentityInstance": 7,
                    "zoneEpoch": "fixture-epoch",
                }
            }
            payload, mappings = resolver.build_runtime_playfield_mapping(
                [record], [npc(record.capture_id, "(SimpleChar:1)")], {7}, evidence
            )
            self.assertEqual(0, payload["provenCaptureMappings"])
            self.assertFalse(mappings[record.capture_id]["mappingProven"])

    def test_same_epoch_pair_for_unobserved_runtime_is_not_proven(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary)
            record = harvester.CaptureRecord("20260101-000000", path, True, "fixture", 7, False)
            evidence = {
                record.capture_id: {
                    "runtimePlayfieldId": 1000008,
                    "modelIdentityType": 1000014,
                    "modelIdentityInstance": 7,
                    "zoneEpoch": "fixture-epoch-b",
                }
            }
            payload, mappings = resolver.build_runtime_playfield_mapping(
                [record], [npc(record.capture_id, "(SimpleChar:1)")], {7}, evidence
            )
            self.assertEqual(0, payload["provenCaptureMappings"])
            self.assertFalse(mappings[record.capture_id]["mappingProven"])
            self.assertEqual([], mappings[record.capture_id]["sameEpochProvenMappings"])

    def test_same_epoch_pair_applies_only_to_matching_runtime_in_multizone_capture(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary)
            record = harvester.CaptureRecord("20260101-000000", path, True, "fixture", 7, False)
            epoch_a = npc(record.capture_id, "(SimpleChar:1)")
            epoch_b = npc(record.capture_id, "(SimpleChar:2)")
            epoch_b.runtime_playfield_id = 1000008
            evidence = {
                record.capture_id: {
                    "runtimePlayfieldId": 1000008,
                    "modelIdentityType": 1000014,
                    "modelIdentityInstance": 7,
                    "zoneEpoch": "fixture-epoch-b",
                }
            }
            payload, mappings = resolver.build_runtime_playfield_mapping(
                [record], [epoch_a, epoch_b], {7}, evidence
            )
            capture_mapping = mappings[record.capture_id]
            self.assertEqual(1, payload["provenCaptureMappings"])
            self.assertEqual(0, payload["fullyProvenCaptureMappings"])
            self.assertEqual("partial-same-epoch-proven", capture_mapping["mappingStatus"])
            self.assertFalse(
                resolver.mapping_for_observation_epoch(capture_mapping, 1000007)["mappingProven"]
            )
            bound = resolver.mapping_for_observation_epoch(capture_mapping, 1000008)
            self.assertTrue(bound["mappingProven"])
            self.assertEqual(7, bound["basePlayfieldResourceId"])
            self.assertEqual("proven-observation-zone-epoch", bound["mappingStatus"])

    def test_refuses_two_placements_at_effectively_same_location(self):
        result = resolver.resolve_candidate_set(
            observation(),
            [placement("one"), placement("two")],
            proven_mapping(),
            proven_transform(),
        )
        self.assertEqual(resolver.MATCH_AMBIGUOUS, result["matchState"])
        self.assertEqual(2, len(result["exactCandidates"]))

    def test_refuses_same_name_on_multiple_placements(self):
        result = resolver.resolve_candidate_set(
            observation((50.0, 50.0, 50.0), name="Same"),
            [placement("one", corroborating={"name": "Same"}), placement("two", (5, 5, 5), corroborating={"name": "Same"})],
            proven_mapping(),
            proven_transform(),
        )
        self.assertEqual(resolver.MATCH_UNMATCHED, result["matchState"])

    def test_refuses_same_monster_data_on_multiple_placements(self):
        result = resolver.resolve_candidate_set(
            observation((50, 50, 50), monsterData=42),
            [placement("one", corroborating={"monsterData": 42}), placement("two", (5, 5, 5), corroborating={"monsterData": 42})],
            proven_mapping(),
            proven_transform(),
        )
        self.assertEqual(resolver.MATCH_UNMATCHED, result["matchState"])

    def test_refuses_same_appearance_signature_on_multiple_placements(self):
        result = resolver.resolve_candidate_set(
            observation((50, 50, 50), appearance="signature"),
            [placement("one", corroborating={"appearance": "signature"}), placement("two", (5, 5, 5), corroborating={"appearance": "signature"})],
            proven_mapping(),
            proven_transform(),
        )
        self.assertEqual(resolver.MATCH_UNMATCHED, result["matchState"])

    def test_refuses_exact_coordinate_with_metadata_contradiction(self):
        result = resolver.resolve_candidate_set(
            observation(monsterData=42),
            [placement("one", corroborating={"monsterData": 43})],
            proven_mapping(),
            proven_transform(),
        )
        self.assertEqual(resolver.MATCH_CONFLICT, result["matchState"])
        self.assertTrue(result["candidateEliminations"])

    def test_refuses_acg_hash_only_match(self):
        value = observation((50, 50, 50))
        value["acgHash"] = "TEST"
        result = resolver.resolve_candidate_set(
            value, [placement("one")], proven_mapping(), proven_transform()
        )
        self.assertEqual(resolver.MATCH_UNMATCHED, result["matchState"])
        self.assertFalse(result["acgHashUsedAsRuntimeIdentity"])

    def test_refuses_name_plus_acg_hash_match(self):
        value = observation((50, 50, 50), name="Fixture NPC")
        value["acgHash"] = "TEST"
        result = resolver.resolve_candidate_set(
            value,
            [placement("one", corroborating={"name": "Fixture NPC"})],
            proven_mapping(),
            proven_transform(),
        )
        self.assertEqual(resolver.MATCH_UNMATCHED, result["matchState"])
        self.assertFalse(result["acgHashUsedAsRuntimeIdentity"])

    def test_refuses_nearest_neighbor_without_proven_transform(self):
        unproven = resolver.CoordinateTransform(name="nearest-only", proven=False)
        result = resolver.resolve_candidate_set(
            observation((1.2, 2.0, 3.0)), [placement("one")], proven_mapping(), unproven
        )
        self.assertEqual(resolver.MATCH_UNMATCHED, result["matchState"])
        self.assertTrue(result["nearestCandidates"])

    def test_refuses_unknown_district_transform(self):
        unproven = resolver.CoordinateTransform(
            name="unknown-district-origin", district_centre_mode="add-all", proven=False
        )
        with self.assertRaises(resolver.ResolverError):
            resolver.apply_coordinate_transform(
                (1, 2, 3), unproven, district_centre=(10, 10, 10)
            )
        result = resolver.resolve_candidate_set(
            observation((11, 12, 13)),
            [placement("one", district_centre=(10, 10, 10))],
            proven_mapping(),
            unproven,
        )
        self.assertEqual(resolver.MATCH_AMBIGUOUS, result["matchState"])

    def test_refuses_conflicting_playfield_evidence(self):
        mapping = {
            "mappingStatus": "conflict",
            "mappingProven": False,
            "conflicts": ["runtime/base disagreement"],
        }
        result = resolver.resolve_candidate_set(
            observation(), [placement("one")], mapping, proven_transform()
        )
        self.assertEqual(resolver.MATCH_CONFLICT, result["matchState"])

    def test_refuses_observation_outside_proven_regions(self):
        result = resolver.resolve_candidate_set(
            observation((20, 20, 20)),
            [placement("one", radius=1.0)],
            proven_mapping(),
            proven_transform(),
        )
        self.assertEqual(resolver.MATCH_UNMATCHED, result["matchState"])

    def test_refuses_spawn_region_ambiguity(self):
        result = resolver.resolve_candidate_set(
            observation((0.5, 0, 0)),
            [placement("one", (0, 0, 0), radius=2), placement("two", (1, 0, 0), radius=2)],
            proven_mapping(),
            proven_transform(),
        )
        self.assertEqual(resolver.MATCH_AMBIGUOUS, result["matchState"])
        self.assertEqual(2, len(result["regionCandidates"]))

    def test_refuses_repeated_cluster_metadata_contradiction(self):
        result = resolver.resolve_candidate_set(
            observation(),
            [placement("one")],
            proven_mapping(),
            proven_transform(),
            cluster_conflict=True,
        )
        self.assertEqual(resolver.MATCH_CONFLICT, result["matchState"])

    def test_detects_repeated_lineage_metadata_contradiction_end_to_end(self):
        first = npc("20260101-000000", "(SimpleChar:1)")
        second = npc("20260101-000100", "(SimpleChar:1)")
        second.fields["monsterData"] = harvester.evidence(
            999, "packet-observed", {"captureId": second.capture_id}
        )
        clusters, mapping = resolver.build_observation_clusters([first, second], {})
        self.assertEqual(1, len(clusters))
        self.assertIn("MonsterData changes inside conservative runtime lineage", clusters[0]["conflicts"])
        self.assertEqual(mapping[first.observation_id], mapping[second.observation_id])


class NpcPlacementIdentityResolverPositiveFixtureTests(unittest.TestCase):
    def test_official_district_transform_decoding_fixture(self):
        transform = proven_transform(
            axis_order=(2, 1, 0),
            signs=(1, 1, -1),
            scale=2.0,
            offset=(1.0, 2.0, 3.0),
            district_centre_mode="add-xz",
        )
        self.assertEqual(
            (27.0, 6.0, 27.0),
            resolver.apply_coordinate_transform(
                (3.0, 2.0, 4.0), transform, district_centre=(18.0, 9.0, 30.0)
            ),
        )

    def test_runtime_to_base_playfield_mapping_fixture(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary)
            (path / "capture_info.json").write_text(
                json.dumps({"playfieldId": "1000007", "resourcePlayfieldId": "7"}),
                encoding="utf-8",
            )
            (path / "capture-session.json").write_text(
                json.dumps({"resourcePlayfieldId": "7"}), encoding="utf-8"
            )
            record = harvester.CaptureRecord("20260101-000000", path, True, "fixture", 7, False)
            observed = npc("20260101-000000", "(SimpleChar:1)")
            evidence = {
                record.capture_id: {
                    "runtimePlayfieldId": 1000007,
                    "modelIdentityType": 1000014,
                    "modelIdentityInstance": 7,
                    "zoneEpoch": "fixture-epoch",
                }
            }
            payload, mappings = resolver.build_runtime_playfield_mapping(
                [record], [observed], {7}, evidence
            )
            self.assertEqual(1, payload["provenCaptureMappings"])
            self.assertTrue(mappings[record.capture_id]["mappingProven"])

    def test_exact_transformed_placement_candidate_fixture(self):
        transform = proven_transform(offset=(10.0, 0.0, -5.0))
        result = resolver.resolve_candidate_set(
            observation((11.0, 2.0, -2.0)),
            [placement("one")],
            proven_mapping(),
            transform,
        )
        self.assertEqual(resolver.MATCH_UNIQUE, result["matchState"])
        self.assertEqual("one", result["exactCandidates"][0]["placementId"])
        exact_evidence = [
            row
            for row in result["identityEvidence"]
            if row.get("type") == "transformed-exact-three-dimensional-coordinate"
        ]
        self.assertEqual(resolver.EVIDENCE_PROVEN, exact_evidence[0]["class"])

    def test_candidate_elimination_by_independent_corroboration_fixture(self):
        survivors, eliminations = resolver.independent_corroborating_elimination(
            {"corroborating": {"monsterData": 42, "headMesh": 100}},
            [
                placement("one", corroborating={"monsterData": 42, "headMesh": 100}),
                placement("two", corroborating={"monsterData": 43, "headMesh": 100}),
            ],
        )
        self.assertEqual(["one"], [row["placementId"] for row in survivors])
        self.assertEqual(["monsterData"], eliminations[0]["contradictingFields"])

    def test_stable_repeated_observation_cluster_fixture(self):
        first = npc("20260101-000000", "(SimpleChar:1)", (1.00, 2.0, 3.0))
        second = npc("20260101-000100", "(SimpleChar:1)", (1.02, 2.0, 3.0))
        history = {
            first.observation_id: [{"position": [1.00, 2.0, 3.0]}],
            second.observation_id: [{"position": [1.02, 2.0, 3.0]}],
        }
        clusters, mapping = resolver.build_observation_clusters([first, second], history)
        self.assertEqual(1, len(clusters))
        self.assertEqual(2, clusters[0]["observationCount"])
        self.assertEqual(2, clusters[0]["captureCount"])
        self.assertIsNotNone(clusters[0]["stablePosition"])
        self.assertEqual(mapping[first.observation_id], mapping[second.observation_id])

    def test_unique_proven_resolution_fixture(self):
        result = resolver.resolve_candidate_set(
            observation((1, 2, 3), monsterData=42),
            [
                placement("exact", (1, 2, 3), corroborating={"monsterData": 42}),
                placement("other", (10, 20, 30), corroborating={"monsterData": 42}),
            ],
            proven_mapping(),
            proven_transform(),
        )
        self.assertEqual(resolver.MATCH_UNIQUE, result["matchState"])
        self.assertEqual(1, len(result["exactCandidates"]))


if __name__ == "__main__":
    unittest.main()
