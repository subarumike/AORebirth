import copy
import csv
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from Tools import npc_identity_bridge_replay as replay
from Tools import npc_observation_harvester as harvester
from Tools import npc_placement_identity_resolver as resolver


SCFU_HEADERS = [
    "Direction",
    "Sequence",
    "GlobalOrdinal",
    "RawPacketHex",
    "DecodeStatus",
    "DecodeFullyConsumed",
    "FlagsNumeric",
    "PlayfieldId",
    "PositionX",
    "PositionY",
    "PositionZ",
    "HeadingX",
    "HeadingY",
    "HeadingZ",
    "HeadingW",
    "MonsterData",
    "VisualFlags",
    "HeadMesh",
    "Textures",
    "Meshes",
    "Breed",
    "Gender",
    "Level",
    "Owner",
    "AppearanceValue",
]
STAT_HEADERS = [
    "Direction",
    "Sequence",
    "GlobalOrdinal",
    "RawPacketHex",
    "DecodeStatus",
    "DecodeFullyConsumed",
    "StatId",
    "Value",
]


def direct(value, classification="client-state-observed", provenance=None):
    return {
        "value": value,
        "classification": classification,
        "provenance": provenance or {"source": "synthetic bridge fixture"},
    }


def raw_packet_hex(instance=0x1A2B):
    packet = bytearray(28)
    packet[6:8] = len(packet).to_bytes(2, "big")
    packet[20:24] = (50000).to_bytes(4, "big")
    packet[24:28] = instance.to_bytes(4, "big")
    return packet.hex().upper()


def packet_sha256(instance=0x1A2B):
    return hashlib.sha256(bytes.fromhex(raw_packet_hex(instance))).hexdigest()


def bridge_scfu_provenance(sequence=1, ordinal=5):
    return {
        "source": "scfu-appearance.csv",
        "kind": "scfu",
        "direction": "IN",
        "sequence": sequence,
        "global_ordinal": ordinal,
        "raw_packet_sha256": packet_sha256(),
    }


def harvested_scfu_provenance(sequence=1, ordinal=5):
    return {
        "captureId": "fixture-capture",
        "artifact": "scfu-appearance.csv",
        "direction": "IN",
        "sequence": str(sequence),
        "globalOrdinal": str(ordinal),
        "rawPacketSha256": packet_sha256(),
    }


def npc():
    return harvester.NpcObservation(
        observation_id="fixture-capture|(SimpleChar:1A2B)",
        capture_id="fixture-capture",
        capture_path="Captures/fixture-capture",
        identity="(SimpleChar:1A2B)",
        resource_playfield_id=999,
        runtime_playfield_id=1000007,
        name="Fixture NPC",
        position=(1.0, 2.0, 3.0),
        fields={},
        source_rows=[
            harvested_scfu_provenance(1, 5),
            harvested_scfu_provenance(2, 6),
            harvested_scfu_provenance(3, 22),
            harvested_scfu_provenance(4, 15),
            harvested_scfu_provenance(5, 0),
        ],
    )


def placement(placement_id):
    return {
        "placementId": placement_id,
        "playfieldId": 7,
        "districtId": "district-0",
        "districtName": "Fixture",
        "sourcePosition": [1.0, 2.0, 3.0],
        "worldPosition": None,
        "districtCentre": None,
        "spawnMetadata": {"radius": 0.0, "levelMinimum": 1, "levelMaximum": 220},
        "provenIdentifiers": {"recordOffsetInResource": 1},
        "unprovenIndirection": {"templateId": None, "monsterData": None},
        "acgHash": {"text": "TEST", "semanticState": "never runtime identity"},
        "provenCorroborating": {},
    }


def proven_relation():
    return {
        "state": "proven",
        "proof": "isolated synthetic coordinate-relation fixture",
        "source_position": "official-placement.sourcePosition",
        "target_position": "positions.world",
        "transform": {
            "name": "synthetic-identity-transform",
            "axis_order": [0, 1, 2],
            "signs": [1, 1, 1],
            "scale": 1.0,
            "offset": [0.0, 0.0, 0.0],
            "quantization": None,
            "district_centre_mode": "none",
            "evidence_class": resolver.EVIDENCE_PROVEN,
            "proven": True,
            "proof": "isolated synthetic transform fixture",
        },
    }


def bridge_artifact(observation):
    return {
        "schema_version": resolver.BRIDGE_SCHEMA_VERSION,
        "capture_id": observation.capture_id,
        "epochs": [
            {
                "zone_epoch_id": "epoch-1",
                "start_global_ordinal": 1,
                "end_global_ordinal": 20,
                "trigger": "fixture",
                "runtime_playfield": direct(1000007),
                "base_playfield_direct": direct(7),
                "district_id_direct": direct(None, "not-observed"),
                "cell_id_direct": direct(None, "not-observed"),
                "valid": True,
            }
        ],
        "observations": [
            {
                "observation_id": observation.observation_id,
                "capture_id": observation.capture_id,
                "zone_epoch_id": "epoch-1",
                "observation_sequence": 1,
                "observation_global_ordinal": 10,
                "timestamp": "2026-01-01T00:00:00Z",
                "runtime_identity_type": direct(50000, "packet-observed"),
                "runtime_identity_instance": direct(0x1A2B, "packet-observed"),
                "runtime_playfield": direct(1000007),
                "base_playfield_direct": direct(7),
                "full_model_type_direct": direct(1000014),
                "full_model_instance_direct": direct(7),
                "positions": {
                    "world": direct([1.0, 2.0, 3.0]),
                    "packet_scfu": direct(
                        [1.0, 2.0, 3.0],
                        "packet-observed",
                        [bridge_scfu_provenance(1, 5)],
                    ),
                },
                "packet_provenance": [bridge_scfu_provenance(1, 5)],
                "client_state_provenance": [],
                "bridge_state": "candidate",
                "blockers": [],
                "coordinate_relation": proven_relation(),
                "acg_hash_used_as_runtime_identity": False,
            }
        ],
        "parity": {
            "packet_fields_match": True,
            "client_state_only_fields": [],
            "live_only_fields": [],
            "offline_only_fields": [],
            "conflicts": [],
        },
        "source_files": [],
        "digest": "synthetic-fixture",
    }


def load_bridge(artifact, observation):
    with tempfile.TemporaryDirectory() as temporary:
        path = Path(temporary) / "npc-identity-bridge.json"
        path.write_text(json.dumps(artifact), encoding="utf-8")
        return resolver.load_identity_bridge_artifacts([path], {7}, [observation])


def resolve_with_bridge(observation, bridge_by_observation, placements):
    clusters, observation_to_cluster = resolver.build_observation_clusters([observation], {})
    resolutions, _ = resolver.resolve_observations(
        [observation],
        placements,
        {},
        clusters,
        observation_to_cluster,
        bridge_by_observation,
    )
    return resolutions[0]


def write_csv(path, headers, rows=()):
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=headers)
        writer.writeheader()
        writer.writerows(rows)


def analyzer_scfu_row(sequence, ordinal, position):
    return {
        "Direction": "IN",
        "Sequence": str(sequence),
        "GlobalOrdinal": str(ordinal),
        "RawPacketHex": raw_packet_hex(),
        "DecodeStatus": "decoded_complete",
        "DecodeFullyConsumed": "true",
        "FlagsNumeric": str(replay.SCFU_FLAG_HAS_PLAYFIELD),
        "PlayfieldId": "1000007",
        "PositionX": str(position[0]),
        "PositionY": str(position[1]),
        "PositionZ": str(position[2]),
        "HeadingX": "",
        "HeadingY": "",
        "HeadingZ": "",
        "HeadingW": "",
        "MonsterData": "123",
        "VisualFlags": "7",
        "HeadMesh": "",
        "Textures": "",
        "Meshes": "",
        "Breed": "Solitus",
        "Gender": "Male",
        "Level": "5",
        "Owner": "",
        "AppearanceValue": "0",
    }


def live_scfu_record(capture_id, sequence, ordinal, position):
    return {
        "schema_version": 1,
        "record_type": "packet_scfu",
        "capture_id": capture_id,
        "zone_epoch_id": "epoch-1",
        "zone_epoch_valid": True,
        "direction": "IN",
        "global_ordinal": ordinal,
        "sequence": sequence,
        "decode_error": "",
        "decode_fully_consumed": True,
        "runtime_identity_type": 50000,
        "runtime_identity_instance": 0x1A2B,
        "runtime_playfield_id": 1000007,
        "position": {"x": position[0], "y": position[1], "z": position[2]},
        "monster_data": 123,
        "visual_flags": 7,
        "textures": "",
        "meshes": "",
        "level": 5,
        "breed": 0,
        "gender": 0,
    }


class NpcIdentityBridgeResolverTests(unittest.TestCase):
    def test_stale_epoch_is_rejected(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        artifact["epochs"][0]["valid"] = False

        payload, bridges = load_bridge(artifact, observation)

        self.assertEqual(0, payload["acceptedObservations"])
        self.assertFalse(bridges[observation.observation_id]["accepted"])
        self.assertIn(
            "zone epoch is invalid or stale",
            bridges[observation.observation_id]["public"]["blockers"],
        )
        result = resolve_with_bridge(observation, bridges, [placement("only")])
        self.assertNotEqual(resolver.MATCH_UNIQUE, result["matchState"])

    def test_acg_hash_identity_use_is_rejected_and_never_promoted(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        artifact["observations"][0]["acg_hash_used_as_runtime_identity"] = True

        _, bridges = load_bridge(artifact, observation)
        result = resolve_with_bridge(observation, bridges, [placement("only")])

        self.assertFalse(bridges[observation.observation_id]["accepted"])
        self.assertIn(
            "ACGHash identity exclusion is absent or false",
            bridges[observation.observation_id]["public"]["blockers"],
        )
        self.assertFalse(result["acgHashUsedAsRuntimeIdentity"])
        self.assertNotEqual(resolver.MATCH_UNIQUE, result["matchState"])

    def test_direct_model_instance_is_accepted_but_derived_is_rejected(self):
        observation = npc()
        direct_artifact = bridge_artifact(observation)
        direct_payload, direct_bridges = load_bridge(direct_artifact, observation)
        self.assertEqual(1, direct_payload["acceptedObservations"])
        self.assertTrue(direct_bridges[observation.observation_id]["accepted"])

        derived_artifact = bridge_artifact(observation)
        derived_artifact["observations"][0]["full_model_instance_direct"]["classification"] = "derived"
        derived_payload, derived_bridges = load_bridge(derived_artifact, observation)

        self.assertEqual(0, derived_payload["acceptedObservations"])
        self.assertIn(
            "observation.full_model_instance_direct is not directly observed (classification=derived)",
            derived_bridges[observation.observation_id]["public"]["blockers"],
        )

    def test_valid_synthetic_bridge_produces_one_unique_official_record(self):
        observation = npc()
        payload, bridges = load_bridge(bridge_artifact(observation), observation)

        result = resolve_with_bridge(observation, bridges, [placement("only")])

        self.assertEqual(1, payload["acceptedObservations"])
        self.assertEqual(resolver.MATCH_UNIQUE, result["matchState"])
        self.assertEqual(["only"], result["candidatePlacementIds"])
        self.assertTrue(result["promotionReady"])

    def test_proven_bridge_matches_official_placement_against_world_not_scfu_position(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        row = artifact["observations"][0]
        row["positions"]["world"] = direct({"x": 10.0, "y": 2.0, "z": 3.0})
        row["coordinate_relation"]["transform"]["offset"] = [9.0, 0.0, 0.0]

        payload, bridges = load_bridge(artifact, observation)
        result = resolve_with_bridge(observation, bridges, [placement("only")])

        self.assertEqual(1, payload["acceptedObservations"])
        self.assertEqual(resolver.MATCH_UNIQUE, result["matchState"])
        self.assertEqual("bridge-positions.world", result["candidatePositionSpace"])
        self.assertEqual([10.0, 2.0, 3.0], result["runtimePosition"])
        self.assertEqual(
            [1.0, 2.0, 3.0],
            result["identityBridge"]["directPacketScfuPosition"],
        )

    def test_later_exact_scfu_provenance_does_not_equal_consolidated_first_position(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        artifact["observations"][0]["positions"]["packet_scfu"] = direct(
            [9.0, 2.0, 3.0],
            "packet-observed",
            [bridge_scfu_provenance(2, 6)],
        )
        artifact["observations"][0]["packet_provenance"] = [
            bridge_scfu_provenance(2, 6)
        ]

        payload, bridges = load_bridge(artifact, observation)

        self.assertEqual(1, payload["acceptedObservations"])
        self.assertTrue(bridges[observation.observation_id]["accepted"])
        self.assertEqual(
            [9.0, 2.0, 3.0],
            bridges[observation.observation_id]["public"]["directPacketScfuPosition"],
        )

    def test_packet_scfu_requires_exact_harvested_packet_provenance(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        artifact["observations"][0]["positions"]["packet_scfu"]["provenance"] = [
            bridge_scfu_provenance(99, 99)
        ]

        payload, bridges = load_bridge(artifact, observation)

        self.assertEqual(0, payload["acceptedObservations"])
        self.assertIn(
            "harvested SCFU provenance conflicts with bridge packet_scfu provenance",
            bridges[observation.observation_id]["public"]["blockers"],
        )

    def test_packet_scfu_requires_declared_row_reference(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        artifact["observations"][0]["packet_provenance"] = []

        payload, bridges = load_bridge(artifact, observation)

        self.assertEqual(0, payload["acceptedObservations"])
        self.assertIn(
            "positions.packet_scfu is not bound to one exact declared SCFU packet_provenance entry",
            bridges[observation.observation_id]["public"]["blockers"],
        )

    def test_packet_scfu_future_reference_is_rejected(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        row = artifact["observations"][0]
        future = bridge_scfu_provenance(4, 15)
        row["positions"]["packet_scfu"]["provenance"] = [future]
        row["packet_provenance"] = [future]

        payload, bridges = load_bridge(artifact, observation)

        self.assertEqual(0, payload["acceptedObservations"])
        self.assertIn(
            "positions.packet_scfu packet ordinal is after observation ordinal",
            bridges[observation.observation_id]["public"]["blockers"],
        )

    def test_packet_scfu_out_of_epoch_reference_is_rejected(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        row = artifact["observations"][0]
        outside = bridge_scfu_provenance(5, 0)
        row["positions"]["packet_scfu"]["provenance"] = [outside]
        row["packet_provenance"] = [outside]

        payload, bridges = load_bridge(artifact, observation)

        self.assertEqual(0, payload["acceptedObservations"])
        self.assertIn(
            "positions.packet_scfu packet ordinal is outside selected zone epoch",
            bridges[observation.observation_id]["public"]["blockers"],
        )

    def test_single_legacy_row_accepts_not_observed_lineage_wrapper(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        artifact["observations"][0]["lifecycle_lineage"] = {
            "value": None,
            "classification": "not-observed",
            "provenance": [],
        }

        payload, bridges = load_bridge(artifact, observation)

        self.assertEqual(1, payload["acceptedObservations"])
        self.assertTrue(bridges[observation.observation_id]["accepted"])

    def test_absent_direct_base_playfield_is_blocked(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        del artifact["observations"][0]["base_playfield_direct"]

        _, bridges = load_bridge(artifact, observation)
        result = resolve_with_bridge(observation, bridges, [placement("only")])

        self.assertIn(
            "observation.base_playfield_direct direct evidence is absent",
            bridges[observation.observation_id]["public"]["blockers"],
        )
        self.assertNotEqual(resolver.MATCH_UNIQUE, result["matchState"])
        self.assertFalse(result["promotionReady"])

    def test_duplicate_exact_official_records_remain_ambiguous(self):
        observation = npc()
        _, bridges = load_bridge(bridge_artifact(observation), observation)

        result = resolve_with_bridge(
            observation,
            bridges,
            [placement("duplicate-a"), placement("duplicate-b")],
        )

        self.assertEqual(resolver.MATCH_AMBIGUOUS, result["matchState"])
        self.assertEqual(2, len(result["exactCandidates"]))
        self.assertFalse(result["promotionReady"])

    def test_open_epoch_is_rejected_even_when_marked_valid(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        artifact["epochs"][0]["end_global_ordinal"] = None

        _, bridges = load_bridge(artifact, observation)

        self.assertFalse(bridges[observation.observation_id]["accepted"])
        self.assertIn(
            "zone epoch finalized end boundary is absent or invalid",
            bridges[observation.observation_id]["public"]["blockers"],
        )

    def test_overlapping_valid_epochs_block_every_artifact_row(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        overlapping = copy.deepcopy(artifact["epochs"][0])
        overlapping["zone_epoch_id"] = "epoch-2"
        overlapping["start_global_ordinal"] = 5
        overlapping["end_global_ordinal"] = 15
        artifact["epochs"].append(overlapping)

        payload, bridges = load_bridge(artifact, observation)

        self.assertEqual(0, payload["acceptedObservations"])
        self.assertFalse(bridges[observation.observation_id]["accepted"])
        self.assertIn(
            "zone epoch ranges overlap or have non-strict starts",
            bridges[observation.observation_id]["public"]["blockers"],
        )

    def test_invalid_epoch_overlap_still_blocks_valid_epoch_promotion(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        overlapping = copy.deepcopy(artifact["epochs"][0])
        overlapping["zone_epoch_id"] = "epoch-invalid"
        overlapping["start_global_ordinal"] = 5
        overlapping["end_global_ordinal"] = 15
        overlapping["valid"] = False
        artifact["epochs"].append(overlapping)

        payload, bridges = load_bridge(artifact, observation)

        self.assertEqual(0, payload["acceptedObservations"])
        self.assertIn(
            "zone epoch ranges overlap or have non-strict starts",
            bridges[observation.observation_id]["public"]["blockers"],
        )

    def test_unfinalized_invalid_epoch_blocks_completed_artifact(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        pending = copy.deepcopy(artifact["epochs"][0])
        pending["zone_epoch_id"] = "epoch-pending"
        pending["start_global_ordinal"] = 21
        pending["end_global_ordinal"] = None
        pending["valid"] = False
        artifact["epochs"].append(pending)

        payload, bridges = load_bridge(artifact, observation)

        self.assertEqual(0, payload["acceptedObservations"])
        self.assertIn(
            "zone epoch range is absent, invalid, or not finalized",
            bridges[observation.observation_id]["public"]["blockers"],
        )

    def test_advisory_bridge_blockers_are_reported_without_rejecting_proof(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        row = artifact["observations"][0]
        row.pop("blockers")
        row["bridge_blockers"] = [
            "npc-specific-official-placement-identity-not-exposed"
        ]

        payload, bridges = load_bridge(artifact, observation)
        result = resolve_with_bridge(observation, bridges, [placement("only")])
        public = bridges[observation.observation_id]["public"]

        self.assertEqual(1, payload["acceptedObservations"])
        self.assertEqual([], public["blockers"])
        self.assertEqual(row["bridge_blockers"], public["bridgeBlockers"])
        self.assertEqual(resolver.MATCH_UNIQUE, result["matchState"])

    def test_malformed_bridge_blockers_are_rejected(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        artifact["observations"][0]["bridge_blockers"] = {"invalid": True}

        _, bridges = load_bridge(artifact, observation)

        self.assertFalse(bridges[observation.observation_id]["accepted"])
        self.assertIn(
            "bridge observation bridge_blockers field is invalid",
            bridges[observation.observation_id]["public"]["blockers"],
        )

    def test_repeated_snapshots_select_the_only_eligible_temporal_row(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        exact = artifact["observations"][0]
        exact["observation_id"] = "fixture-bridge-row-1"
        exact["harvested_observation_id"] = observation.observation_id
        exact["lifecycle_lineage"] = direct(
            "epoch-1|SimpleChar:1A2B|lineage:0001", "derived"
        )
        moved = copy.deepcopy(exact)
        moved["observation_id"] = "fixture-bridge-row-2"
        moved["observation_sequence"] = 2
        moved["observation_global_ordinal"] = 11
        moved["positions"]["world"] = direct(
            [9.0, 2.0, 3.0], "packet-observed"
        )
        moved["positions"]["packet_scfu"] = direct(
            [9.0, 2.0, 3.0],
            "packet-observed",
            [bridge_scfu_provenance(2, 6)],
        )
        moved["packet_provenance"] = [bridge_scfu_provenance(2, 6)]
        artifact["observations"].append(moved)

        payload, bridges = load_bridge(artifact, observation)
        bridge = bridges[observation.observation_id]
        result = resolve_with_bridge(observation, bridges, [placement("only")])

        self.assertEqual(1, payload["acceptedObservations"])
        self.assertTrue(bridge["accepted"])
        self.assertEqual(
            "one-eligible-temporal-row-selected",
            bridge["public"]["temporalAggregation"],
        )
        self.assertEqual(
            [True, False],
            [row["accepted"] for row in bridge["public"]["temporalRows"]],
        )
        self.assertIn(
            "observation.positions.world is not client-state-observed",
            bridge["public"]["temporalRows"][1]["blockers"],
        )
        self.assertEqual(resolver.MATCH_UNIQUE, result["matchState"])

    def test_identical_epoch_lineage_snapshots_deduplicate_deterministically(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        first = artifact["observations"][0]
        first["observation_id"] = "fixture-bridge-row-1"
        first["harvested_observation_id"] = observation.observation_id
        first["lifecycle_lineage"] = direct(
            "epoch-1|SimpleChar:1A2B|lineage:0001", "derived"
        )
        second = copy.deepcopy(first)
        second["observation_id"] = "fixture-bridge-row-2"
        second["observation_sequence"] = 2
        second["observation_global_ordinal"] = 11
        artifact["observations"].append(second)

        payload, bridges = load_bridge(artifact, observation)
        bridge = bridges[observation.observation_id]

        self.assertEqual(1, payload["acceptedObservations"])
        self.assertTrue(bridge["accepted"])
        self.assertEqual(
            "identical-eligible-temporal-rows-deduplicated",
            bridge["public"]["temporalAggregation"],
        )
        self.assertEqual("fixture-bridge-row-1", bridge["public"]["observationId"])

    def test_cross_epoch_identity_reuse_poisons_single_eligible_row(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        first = artifact["observations"][0]
        first["observation_id"] = "fixture-bridge-row-1"
        first["harvested_observation_id"] = observation.observation_id
        first["lifecycle_lineage"] = direct(
            "epoch-1|SimpleChar:1A2B|lineage:0001", "derived"
        )
        next_epoch = copy.deepcopy(artifact["epochs"][0])
        next_epoch["zone_epoch_id"] = "epoch-2"
        next_epoch["start_global_ordinal"] = 21
        next_epoch["end_global_ordinal"] = 40
        next_epoch["runtime_playfield"] = direct(1000008)
        next_epoch["base_playfield_direct"] = direct(8)
        artifact["epochs"].append(next_epoch)
        reused = copy.deepcopy(first)
        reused["observation_id"] = "fixture-bridge-row-2"
        reused["zone_epoch_id"] = "epoch-2"
        reused["observation_sequence"] = 2
        reused["observation_global_ordinal"] = 25
        reused["runtime_playfield"] = direct(1000008)
        reused["base_playfield_direct"] = direct(8)
        reused["full_model_instance_direct"] = direct(8)
        reused["lifecycle_lineage"] = direct(
            "epoch-2|SimpleChar:1A2B|lineage:0001", "derived"
        )
        reused["positions"]["packet_scfu"] = direct(
            [1.0, 2.0, 3.0],
            "packet-observed",
            [bridge_scfu_provenance(3, 22)],
        )
        reused["packet_provenance"] = [bridge_scfu_provenance(3, 22)]
        artifact["observations"].append(reused)

        payload, bridges = load_bridge(artifact, observation)
        bridge = bridges[observation.observation_id]

        self.assertEqual(0, payload["acceptedObservations"])
        self.assertFalse(bridge["accepted"])
        self.assertEqual(
            "conflicting-temporal-entity-scope",
            bridge["public"]["temporalAggregation"],
        )
        self.assertIn(
            "temporal bridge rows cross epoch, lineage, or direct identity scope",
            bridge["public"]["blockers"],
        )

    def test_conflicting_eligible_temporal_rows_fail_closed(self):
        observation = npc()
        artifact = bridge_artifact(observation)
        first = artifact["observations"][0]
        first["observation_id"] = "fixture-bridge-row-1"
        first["harvested_observation_id"] = observation.observation_id
        first["lifecycle_lineage"] = direct(
            "epoch-1|SimpleChar:1A2B|lineage:0001", "derived"
        )
        second = copy.deepcopy(first)
        second["observation_id"] = "fixture-bridge-row-2"
        second["observation_sequence"] = 2
        second["observation_global_ordinal"] = 11
        second["coordinate_relation"]["transform"]["name"] = "different-proven-transform"
        artifact["observations"].append(second)

        payload, bridges = load_bridge(artifact, observation)
        bridge = bridges[observation.observation_id]
        result = resolve_with_bridge(observation, bridges, [placement("only")])

        self.assertEqual(0, payload["acceptedObservations"])
        self.assertFalse(bridge["accepted"])
        self.assertEqual(
            "conflicting-eligible-temporal-rows",
            bridge["public"]["temporalAggregation"],
        )
        self.assertIn(
            "multiple eligible temporal bridge rows have conflicting or unscoped critical proof",
            bridge["public"]["blockers"],
        )
        self.assertNotEqual(resolver.MATCH_UNIQUE, result["matchState"])

    def test_real_replay_output_joins_harvested_id_and_parses_mapping_position(self):
        observation = npc()
        capture_id = observation.capture_id
        live_records = [
            {
                "schema_version": 1,
                "record_type": "zone_epoch",
                "capture_id": capture_id,
                "zone_epoch_id": "epoch-1",
                "start_global_ordinal": 1,
                "end_global_ordinal": 20,
                "validity": "valid",
                "runtime_playfield": direct(1000007),
                "base_playfield_direct": direct(7),
            },
            {
                "schema_version": 1,
                "record_type": "npc_snapshot",
                "capture_id": capture_id,
                "zone_epoch_id": "epoch-1",
                "zone_epoch_valid": True,
                "observation_sequence": 1,
                "observation_global_ordinal": 10,
                "timestamp": "2026-01-01T00:00:00Z",
                "runtime_identity_type": direct(50000),
                "runtime_identity_instance": direct(0x1A2B),
                "runtime_playfield": direct(1000007),
                "base_playfield_direct": direct(7),
                "full_model_type_direct": direct(1000014),
                "full_model_instance_direct": direct(7),
                "lifecycle_lineage": "epoch-1|SimpleChar:1A2B|lineage:0001",
                "positions": {
                    "world": direct({"x": 10.0, "y": 2.0, "z": 3.0})
                },
                "packet_provenance": [
                    {
                        "kind": "scfu",
                        "direction": "IN",
                        "global_ordinal": 5,
                        "sequence": 1,
                    }
                ],
                "client_state_provenance": [],
                "bridge_state": "direct-candidate",
                "acg_hash_used_as_runtime_identity": False,
            },
        ]
        periodic_snapshot = copy.deepcopy(live_records[1])
        periodic_snapshot["observation_sequence"] = 2
        periodic_snapshot["observation_global_ordinal"] = 11
        periodic_snapshot["positions"]["world"] = direct(
            {"x": 18.0, "y": 2.0, "z": 3.0}
        )
        periodic_snapshot["packet_provenance"][0]["global_ordinal"] = 6
        periodic_snapshot["packet_provenance"][0]["sequence"] = 2
        live_records.append(periodic_snapshot)
        live_records.extend(
            [
                live_scfu_record(capture_id, 1, 5, (1.0, 2.0, 3.0)),
                live_scfu_record(capture_id, 2, 6, (9.0, 2.0, 3.0)),
            ]
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            live_path = root / replay.DEFAULT_LIVE_JSONL
            scfu_path = root / replay.DEFAULT_SCFU_CSV
            stat_path = root / replay.DEFAULT_STAT_CSV
            artifact_path = root / replay.DEFAULT_OUTPUT_JSON
            live_path.write_text(
                "".join(json.dumps(record, sort_keys=True) + "\n" for record in live_records),
                encoding="utf-8",
            )
            write_csv(
                scfu_path,
                SCFU_HEADERS,
                [
                    analyzer_scfu_row(1, 5, (1.0, 2.0, 3.0)),
                    analyzer_scfu_row(2, 6, (9.0, 2.0, 3.0)),
                ],
            )
            write_csv(stat_path, STAT_HEADERS)

            artifact = replay.build_artifact(live_path, scfu_path, stat_path)
            replay.write_artifact(artifact_path, artifact, check=False)
            replay_rows = artifact["observations"]
            payload, bridges = resolver.load_identity_bridge_artifacts(
                [artifact_path], {7}, [observation]
            )

        self.assertEqual(2, len(replay_rows))
        self.assertEqual(2, len({row["observation_id"] for row in replay_rows}))
        self.assertTrue(
            all(
                row["harvested_observation_id"] == observation.observation_id
                for row in replay_rows
            )
        )
        self.assertEqual(
            {"x": 10.0, "y": 2.0, "z": 3.0},
            replay_rows[0]["positions"]["world"]["value"],
        )
        self.assertEqual(
            {"x": 1.0, "y": 2.0, "z": 3.0},
            replay_rows[0]["positions"]["packet_scfu"]["value"],
        )
        self.assertEqual(
            "epoch-1|SimpleChar:1A2B|lineage:0001",
            replay_rows[0]["lifecycle_lineage"]["value"],
        )
        self.assertEqual(
            "derived",
            replay_rows[0]["lifecycle_lineage"]["classification"],
        )
        self.assertIn(observation.observation_id, bridges)
        self.assertEqual(0, payload["acceptedObservations"])
        public = bridges[observation.observation_id]["public"]
        self.assertEqual("no-eligible-temporal-rows", public["temporalAggregation"])
        self.assertEqual(2, len(public["temporalRows"]))
        self.assertIn(
            "coordinate relation is not explicitly proven",
            public["blockers"],
        )
        self.assertEqual(
            ["coordinate relation is not explicitly proven"],
            public["blockers"],
        )
        self.assertFalse(
            any("lifecycle_lineage" in blocker for blocker in public["blockers"])
        )
        self.assertNotIn(
            "multiple eligible temporal bridge rows have conflicting or unscoped critical proof",
            public["blockers"],
        )


if __name__ == "__main__":
    unittest.main()
