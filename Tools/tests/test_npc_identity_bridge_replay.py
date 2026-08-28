import csv
import json
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from pathlib import Path

from Tools import npc_identity_bridge_replay as replay


CAPTURE_ID = "20260827-120000"
SCFU_HEADERS = [
    "CapturedUtc",
    "Direction",
    "GlobalOrdinal",
    "Sequence",
    "DecodeStatus",
    "DecodeFullyConsumed",
    "RawPacketHex",
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
    "CapturedUtc",
    "Direction",
    "GlobalOrdinal",
    "Sequence",
    "DecodeStatus",
    "DecodeFullyConsumed",
    "RawPacketHex",
    "StatId",
    "Value",
]


def evidence(value, classification="client-state-observed", provenance=None):
    return {
        "value": value,
        "classification": classification,
        "provenance": provenance or [{"source": "fixture-live"}],
    }


def raw_packet(identity_type=0xC350, identity_instance=0x11223344):
    packet = bytearray(28)
    packet[6:8] = len(packet).to_bytes(2, "big")
    packet[20:24] = identity_type.to_bytes(4, "big")
    packet[24:28] = identity_instance.to_bytes(4, "big")
    return packet.hex().upper()


def scfu_row(
    ordinal,
    sequence,
    *,
    instance=0x11223344,
    playfield=4582,
    position=(1.25, 2.5, 3.75),
    monster_data=123,
    owner="",
):
    return {
        "CapturedUtc": "2026-08-27T12:00:00.0000000Z",
        "Direction": "IN",
        "GlobalOrdinal": str(ordinal),
        "Sequence": str(sequence),
        "DecodeStatus": "decoded_complete",
        "DecodeFullyConsumed": "true",
        "RawPacketHex": raw_packet(identity_instance=instance),
        "FlagsNumeric": str(
            replay.SCFU_FLAG_HAS_PLAYFIELD
            | replay.SCFU_FLAG_HAS_HEAD_MESH
            | replay.SCFU_FLAG_HAS_HEADING
        ),
        "PlayfieldId": str(playfield),
        "PositionX": str(position[0]),
        "PositionY": str(position[1]),
        "PositionZ": str(position[2]),
        "HeadingX": "0",
        "HeadingY": "0.5",
        "HeadingZ": "0",
        "HeadingW": "0.8660254",
        "MonsterData": str(monster_data),
        "VisualFlags": "7",
        "HeadMesh": "99",
        "Textures": "0:123:0",
        "Meshes": "0:456:0:0",
        "Breed": "Solitus",
        "Gender": "Male",
        "Level": "5",
        "Owner": owner,
        "AppearanceValue": str((1 << 5) | (1 << 8)),
    }


def stat_row(ordinal, sequence, *, instance=0x11223344, stat_id=521, value=0):
    return {
        "CapturedUtc": "2026-08-27T12:00:00.1000000Z",
        "Direction": "IN",
        "GlobalOrdinal": str(ordinal),
        "Sequence": str(sequence),
        "DecodeStatus": "decoded_complete",
        "DecodeFullyConsumed": "true",
        "RawPacketHex": raw_packet(identity_instance=instance),
        "StatId": str(stat_id),
        "Value": str(value),
    }


def epoch(epoch_id, start, end=1000, runtime=4582, base=None, valid=True):
    value = {
        "schema_version": 1,
        "record_type": "zone_epoch",
        "capture_id": CAPTURE_ID,
        "zone_epoch_id": epoch_id,
        "start_global_ordinal": start,
        "end_global_ordinal": end,
        "runtime_playfield": evidence(runtime),
        "valid": valid,
    }
    if base is not None:
        value["base_playfield_direct"] = evidence(base)
    return value


def packet_reference(kind, ordinal, sequence):
    return {
        "kind": kind,
        "direction": "IN",
        "global_ordinal": ordinal,
        "sequence": sequence,
    }


def snapshot(epoch_id, observation_sequence, ordinal, *, packet_refs=None, fields=None, positions=None):
    value = {
        "schema_version": 1,
        "record_type": "npc_snapshot",
        "capture_id": CAPTURE_ID,
        "zone_epoch_id": epoch_id,
        "observation_sequence": observation_sequence,
        "observation_global_ordinal": ordinal,
        "timestamp": "2026-08-27T12:00:01.0000000Z",
        "packet_provenance": packet_refs or [],
        "client_state_provenance": [{"source": "AOSharp-client-state"}],
        "positions": positions or {},
    }
    value.update(fields or {})
    return value


def write_csv(path, headers, rows):
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=headers)
        writer.writeheader()
        writer.writerows(rows)


def assigned_epoch(records, ordinal):
    epochs = sorted(
        [record for record in records if record.get("record_type") == "zone_epoch"],
        key=lambda record: record["start_global_ordinal"],
    )
    for index, item in enumerate(epochs):
        end = item.get("end_global_ordinal")
        if end is None and index + 1 < len(epochs):
            end = epochs[index + 1]["start_global_ordinal"] - 1
        if ordinal >= item["start_global_ordinal"] and (end is None or ordinal <= end):
            return item["zone_epoch_id"]
    return None


def live_scfu_record(row, records):
    appearance = int(row["AppearanceValue"])
    value = {
        "schema_version": 1,
        "record_type": "packet_scfu",
        "capture_id": CAPTURE_ID,
        "zone_epoch_id": assigned_epoch(records, int(row["GlobalOrdinal"])),
        "zone_epoch_valid": True,
        "bridge_link_eligible": True,
        "captured_utc": row["CapturedUtc"],
        "direction": row["Direction"],
        "global_ordinal": int(row["GlobalOrdinal"]),
        "sequence": int(row["Sequence"]),
        "decode_error": "",
        "decode_fully_consumed": True,
        "runtime_identity_type": 0xC350,
        "runtime_identity_instance": int.from_bytes(bytes.fromhex(row["RawPacketHex"])[24:28], "big"),
        "runtime_playfield_id": int(row["PlayfieldId"]),
        "position": {
            "x": float(row["PositionX"]),
            "y": float(row["PositionY"]),
            "z": float(row["PositionZ"]),
        },
        "heading": {
            "x": float(row["HeadingX"]),
            "y": float(row["HeadingY"]),
            "z": float(row["HeadingZ"]),
            "w": float(row["HeadingW"]),
        },
        "monster_data": int(row["MonsterData"]),
        "head_mesh": int(row["HeadMesh"]),
        "textures": row["Textures"],
        "meshes": row["Meshes"],
        "visual_flags": int(row["VisualFlags"]),
        "level": int(row["Level"]),
        "breed": (appearance & 255) >> 5,
        "gender": (appearance & 1023) >> 8,
    }
    owner = row["Owner"].strip()
    if owner:
        match = replay.IDENTITY_PATTERN.fullmatch(owner)
        identity_type = {"None": 0, "SimpleChar": 0xC350}.get(match.group("type"))
        if identity_type is None:
            identity_type = int(match.group("type"), 10)
        value["owner"] = {
            "type": identity_type,
            "instance": int(match.group("instance"), 16),
        }
    return value


def live_stat_record(row, records):
    value = int(row["Value"])
    sentinel = value == replay.UNSET_SENTINEL
    return {
        "schema_version": 1,
        "record_type": "packet_stat",
        "capture_id": CAPTURE_ID,
        "zone_epoch_id": assigned_epoch(records, int(row["GlobalOrdinal"])),
        "zone_epoch_valid": True,
        "bridge_link_eligible": True,
        "captured_utc": row["CapturedUtc"],
        "direction": row["Direction"],
        "global_ordinal": int(row["GlobalOrdinal"]),
        "sequence": int(row["Sequence"]),
        "decode_error": "",
        "decode_fully_consumed": True,
        "runtime_identity_type": 0xC350,
        "runtime_identity_instance": int.from_bytes(bytes.fromhex(row["RawPacketHex"])[24:28], "big"),
        "stats": [
            {
                "stat_ordinal": 0,
                "stat_id": int(row["StatId"]),
                "value": None if sentinel else value,
                "raw_value": value,
                "provenance": "sentinel/default" if sentinel else "packet-observed",
            }
        ],
    }


class CaptureFixture:
    def __init__(self, root, records, scfu_rows=None, stat_rows=None):
        self.root = Path(root)
        self.live = self.root / replay.DEFAULT_LIVE_JSONL
        self.scfu = self.root / replay.DEFAULT_SCFU_CSV
        self.stats = self.root / replay.DEFAULT_STAT_CSV
        scfu_rows = scfu_rows or []
        stat_rows = stat_rows or []
        live_records = list(records)
        live_records.extend(live_scfu_record(row, records) for row in scfu_rows)
        live_records.extend(live_stat_record(row, records) for row in stat_rows)
        self.live.write_text(
            "".join(json.dumps(record, sort_keys=True) + "\n" for record in live_records),
            encoding="utf-8",
        )
        write_csv(self.scfu, SCFU_HEADERS, scfu_rows)
        write_csv(self.stats, STAT_HEADERS, stat_rows)

    def build(self):
        return replay.build_artifact(self.live, self.scfu, self.stats)


def complete_packet_fields():
    return {
        "runtime_identity_type": evidence(0xC350, "packet-observed"),
        "runtime_identity_instance": evidence(0x11223344, "packet-observed"),
        "runtime_playfield": evidence(4582, "packet-observed"),
        "monster_data": evidence(123, "packet-observed"),
        "visual_flags": evidence(7, "packet-observed"),
        "head_mesh": evidence(99, "packet-observed"),
        "textures": evidence(["0:123:0"], "packet-observed"),
        "meshes": evidence(["0:456:0:0"], "packet-observed"),
        "packet_scfu_heading": evidence(
            {"x": 0.0, "y": 0.5, "z": 0.0, "w": 0.8660254}, "packet-observed"
        ),
        "packet_scfu_breed_derived": evidence(1, "derived"),
        "packet_scfu_gender_derived": evidence(1, "derived"),
        "packet_scfu_level": evidence(5, "packet-observed"),
        "packet_stat_observations": evidence(
            [{"stat_id": 521, "value": 0}], "packet-observed"
        ),
    }


def direct_identity_fields(runtime_type=0xC350, runtime_instance=0x11223344):
    return {
        "runtime_identity_type": evidence(runtime_type),
        "runtime_identity_instance": evidence(runtime_instance),
        "full_model_type_direct": evidence(replay.MODEL_RESOURCE_TYPE),
        "full_model_instance_direct": evidence(4582),
        "base_playfield_direct": evidence(4582),
    }


class NpcIdentityBridgeReplayTests(unittest.TestCase):
    def test_offline_replay_recovers_scfu_received_before_client_discovery(self):
        live_snapshot = snapshot(
            "epoch-0001",
            1,
            20,
            fields={
                "runtime_identity_type": 0xC350,
                "runtime_identity_instance": 0x11223344,
            },
        )
        live_snapshot["evidence_window_start_global_ordinal"] = 1
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(
                temporary,
                [epoch("epoch-0001", 1), live_snapshot],
                [scfu_row(5, 5)],
            ).build()
        observation = artifact["observations"][0]
        self.assertEqual(123, observation["monster_data"]["value"])
        self.assertEqual(["0:123:0"], observation["textures"]["value"])
        self.assertEqual(["0:456:0:0"], observation["meshes"]["value"])
        self.assertEqual(["scfu"], [item["kind"] for item in observation["packet_provenance"]])
        self.assertTrue(artifact["parity"]["packet_fields_match"], artifact["parity"])

    def test_offline_replay_recovers_direct_stat_identity_link(self):
        live_snapshot = snapshot(
            "epoch-0001",
            1,
            20,
            fields={
                "runtime_identity_type": 0xC350,
                "runtime_identity_instance": 0x11223344,
            },
        )
        live_snapshot["evidence_window_start_global_ordinal"] = 1
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(
                temporary,
                [epoch("epoch-0001", 1), live_snapshot],
                stat_rows=[stat_row(6, 6, stat_id=54, value=5)],
            ).build()
        observation = artifact["observations"][0]
        self.assertEqual(
            [{"stat_id": 54, "value": 5}],
            observation["packet_stat_observations"]["value"],
        )
        self.assertEqual(["stat"], [item["kind"] for item in observation["packet_provenance"]])
        self.assertTrue(artifact["parity"]["packet_fields_match"], artifact["parity"])

    def test_offline_replay_does_not_cross_lineage_evidence_floor(self):
        live_snapshot = snapshot(
            "epoch-0001",
            1,
            20,
            fields={
                "runtime_identity_type": 0xC350,
                "runtime_identity_instance": 0x11223344,
            },
        )
        live_snapshot["evidence_window_start_global_ordinal"] = 10
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(
                temporary,
                [epoch("epoch-0001", 1), live_snapshot],
                [scfu_row(5, 5)],
            ).build()
        self.assertEqual([], artifact["observations"][0]["packet_provenance"])
        self.assertEqual(
            "not-observed",
            artifact["observations"][0]["monster_data"]["classification"],
        )

    def test_zone_transition_creates_distinct_epochs_for_reused_runtime_identity(self):
        records = [
            epoch("epoch-0001", 1, 9, runtime=4582),
            epoch("epoch-0002", 10, 20, runtime=3081),
            snapshot(
                "epoch-0001",
                1,
                5,
                packet_refs=[packet_reference("scfu", 5, 5)],
            ),
            snapshot(
                "epoch-0002",
                2,
                12,
                packet_refs=[packet_reference("scfu", 12, 12)],
            ),
        ]
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(
                temporary,
                records,
                [
                    scfu_row(5, 5, playfield=4582, position=(1, 2, 3)),
                    scfu_row(12, 12, playfield=3081, position=(4, 5, 6)),
                ],
            ).build()
        self.assertEqual(2, len(artifact["epochs"]))
        self.assertEqual(2, len(artifact["observations"]))
        self.assertEqual("epoch-0001", artifact["observations"][0]["zone_epoch_id"])
        self.assertEqual("epoch-0002", artifact["observations"][1]["zone_epoch_id"])
        self.assertEqual(
            artifact["observations"][0]["runtime_identity_instance"]["value"],
            artifact["observations"][1]["runtime_identity_instance"]["value"],
        )
        self.assertEqual(4582, artifact["observations"][0]["runtime_playfield"]["value"])
        self.assertEqual(3081, artifact["observations"][1]["runtime_playfield"]["value"])

    def test_stale_epoch_packet_reference_is_rejected(self):
        records = [
            epoch("epoch-0001", 1, 9),
            epoch("epoch-0002", 10),
            snapshot(
                "epoch-0001",
                1,
                12,
                packet_refs=[packet_reference("scfu", 12, 12)],
            ),
        ]
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(temporary, records, [scfu_row(12, 12)]).build()
        observation = artifact["observations"][0]
        self.assertEqual("invalid-epoch", observation["bridge_state"])
        self.assertTrue(artifact["parity"]["conflicts"])
        self.assertIsNone(observation["runtime_identity_instance"]["value"])

    def test_old_direct_playfield_and_model_fields_do_not_leak_into_new_epoch(self):
        direct = {
            "runtime_identity_type": evidence(0xC350),
            "runtime_identity_instance": evidence(0x11223344),
            "full_model_type_direct": evidence(replay.MODEL_RESOURCE_TYPE),
            "full_model_instance_direct": evidence(4582),
            "base_playfield_direct": evidence(4582),
        }
        records = [
            epoch("epoch-0001", 1, 9, base=4582),
            epoch("epoch-0002", 10, runtime=3081),
            snapshot("epoch-0001", 1, 5, fields=direct),
            snapshot("epoch-0002", 2, 12),
        ]
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(temporary, records).build()
        first, second = artifact["observations"]
        self.assertEqual("direct-candidate", first["bridge_state"])
        self.assertEqual("not-exposed", second["bridge_state"])
        self.assertEqual("not-observed", second["full_model_instance_direct"]["classification"])
        self.assertEqual("not-observed", second["base_playfield_direct"]["classification"])

    def test_sentinel_is_non_authoritative_in_scfu_and_stats(self):
        records = [
            epoch("epoch-0001", 1),
            snapshot(
                "epoch-0001",
                1,
                6,
                packet_refs=[
                    packet_reference("scfu", 5, 5),
                    packet_reference("stat", 6, 6),
                ],
            ),
        ]
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(
                temporary,
                records,
                [scfu_row(5, 5, monster_data=replay.UNSET_SENTINEL)],
                [stat_row(6, 6, value=replay.UNSET_SENTINEL)],
            ).build()
        observation = artifact["observations"][0]
        self.assertEqual("sentinel/default", observation["monster_data"]["classification"])
        self.assertIsNone(observation["monster_data"]["value"])
        self.assertEqual([], observation["packet_stat_observations"]["value"])
        self.assertIn("sentinel/default evidence rejected", observation["bridge_blockers"])

    def test_direct_and_derived_model_identity_remain_distinct(self):
        direct = {
            "runtime_identity_type": evidence(0xC350),
            "runtime_identity_instance": evidence(0x11223344),
            "full_model_type_direct": evidence(replay.MODEL_RESOURCE_TYPE),
            "full_model_instance_direct": evidence(4582),
            "base_playfield_direct": evidence(4582),
        }
        derived = {
            "runtime_identity_type": evidence(0xC350),
            "runtime_identity_instance": evidence(0x11223344),
            "full_model_type_direct": evidence(replay.MODEL_RESOURCE_TYPE, "derived"),
            "full_model_instance_direct": evidence(4582, "derived"),
            "base_playfield_direct": evidence(4582),
        }
        records = [
            epoch("epoch-0001", 1),
            snapshot("epoch-0001", 1, 1, fields=direct),
            snapshot("epoch-0001", 2, 2, fields=derived),
        ]
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(temporary, records).build()
        self.assertEqual("direct-candidate", artifact["observations"][0]["bridge_state"])
        self.assertEqual("partial", artifact["observations"][1]["bridge_state"])
        self.assertEqual(
            "derived",
            artifact["observations"][1]["full_model_instance_direct"]["classification"],
        )

    def test_multiple_position_spaces_survive_serialization(self):
        positions = {
            "world": evidence({"x": 1, "y": 2, "z": 3}),
            "local": evidence({"x": 4, "y": 5, "z": 6}),
            "district": evidence({"x": 7, "y": 8, "z": 9}),
            "cell": evidence({"x": 10, "y": 11, "z": 12}),
        }
        records = [epoch("epoch-0001", 1), snapshot("epoch-0001", 1, 1, positions=positions)]
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(temporary, records).build()
        output = artifact["observations"][0]
        self.assertEqual(set(replay.POSITION_SPACES), set(output["positions"]))
        self.assertEqual({"x": 10, "y": 11, "z": 12}, output["positions"]["cell"]["value"])
        self.assertEqual({"state": "not-proven"}, output["coordinate_relation"])

    def test_live_and_offline_packet_fields_match_with_raw_scfu_provenance(self):
        fields = complete_packet_fields()
        positions = {
            "packet_scfu": evidence(
                {"x": 1.25, "y": 2.5, "z": 3.75}, "packet-observed"
            )
        }
        records = [
            epoch("epoch-0001", 1),
            snapshot(
                "epoch-0001",
                1,
                6,
                packet_refs=[
                    packet_reference("scfu", 5, 5),
                    packet_reference("stat", 6, 6),
                ],
                fields=fields,
                positions=positions,
            ),
        ]
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(
                temporary,
                records,
                [scfu_row(5, 5)],
                [stat_row(6, 6)],
            ).build()
        self.assertTrue(artifact["parity"]["packet_fields_match"], artifact["parity"])
        self.assertEqual([], artifact["parity"]["conflicts"])
        provenance = artifact["observations"][0]["packet_provenance"]
        self.assertEqual(["scfu", "stat"], sorted(item["kind"] for item in provenance))
        self.assertTrue(all(len(item["raw_packet_sha256"]) == 64 for item in provenance))

    def test_cached_scfu_motion_and_demographics_do_not_replace_client_state(self):
        fields = complete_packet_fields()
        fields.pop("packet_stat_observations")
        client_heading = {"x": 0.0, "y": 0.0, "z": 1.0, "w": 0.0}
        client_orientation = {"x": 0.0, "y": 1.0, "z": 0.0, "w": 0.0}
        fields.update(
            {
                "heading": evidence(client_heading),
                "orientation": evidence(client_orientation),
                "level": evidence(99),
                "breed": evidence(4),
                "gender": evidence(2),
            }
        )
        records = [
            epoch("epoch-0001", 1),
            snapshot(
                "epoch-0001",
                1,
                6,
                packet_refs=[packet_reference("scfu", 5, 5)],
                fields=fields,
                positions={
                    "world": evidence({"x": 9.0, "y": 8.0, "z": 7.0}),
                    "packet_scfu": evidence(
                        {"x": 1.25, "y": 2.5, "z": 3.75}, "packet-observed"
                    ),
                },
            ),
        ]
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(temporary, records, [scfu_row(5, 5)]).build()
        observation = artifact["observations"][0]
        self.assertEqual({"x": 9.0, "y": 8.0, "z": 7.0}, observation["positions"]["world"]["value"])
        self.assertEqual(
            {"x": 1.25, "y": 2.5, "z": 3.75},
            observation["positions"]["packet_scfu"]["value"],
        )
        self.assertEqual(client_heading, observation["heading"]["value"])
        self.assertEqual(client_orientation, observation["orientation"]["value"])
        self.assertEqual(99, observation["level"]["value"])
        self.assertEqual(4, observation["breed"]["value"])
        self.assertEqual(2, observation["gender"]["value"])
        self.assertEqual(5, observation["packet_scfu_level"]["value"])
        self.assertEqual("derived", observation["packet_scfu_breed_derived"]["classification"])
        self.assertEqual("derived", observation["packet_scfu_gender_derived"]["classification"])
        self.assertEqual([], artifact["parity"]["conflicts"])
        self.assertTrue(artifact["parity"]["packet_fields_match"], artifact["parity"])

    def test_owner_identity_has_matching_live_and_offline_shape(self):
        fields = complete_packet_fields()
        fields.pop("packet_stat_observations")
        fields["owner"] = evidence(
            {"type": 50000, "instance": 0x1A2B}, "packet-observed"
        )
        records = [
            epoch("epoch-0001", 1),
            snapshot(
                "epoch-0001",
                1,
                6,
                packet_refs=[packet_reference("scfu", 5, 5)],
                fields=fields,
                positions={
                    "packet_scfu": evidence(
                        {"x": 1.25, "y": 2.5, "z": 3.75}, "packet-observed"
                    )
                },
            ),
        ]
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(
                temporary,
                records,
                [scfu_row(5, 5, owner="(SimpleChar:1A2B)")],
            ).build()
        self.assertEqual(
            {"type": 50000, "instance": 0x1A2B},
            artifact["observations"][0]["owner"]["value"],
        )
        self.assertTrue(artifact["parity"]["packet_fields_match"], artifact["parity"])

    def test_client_state_only_fields_are_retained_but_not_fabricated(self):
        records = [
            epoch("epoch-0001", 1),
            snapshot(
                "epoch-0001",
                1,
                1,
                fields={"profession": evidence(7)},
                positions={"local": evidence({"x": 4, "y": 5, "z": 6})},
            ),
        ]
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(temporary, records).build()
        observation = artifact["observations"][0]
        self.assertEqual("client-state-observed", observation["profession"]["classification"])
        self.assertEqual("not-observed", observation["monster_data"]["classification"])
        self.assertEqual("not-observed", observation["positions"]["world"]["classification"])
        self.assertTrue(any(value.endswith(":profession") for value in artifact["parity"]["client_state_only_fields"]))

    def test_live_writer_evidence_shape_normalizes_to_stable_contract(self):
        live_epoch = epoch("epoch-0001", 1)
        live_epoch["runtime_playfield"] = {
            "value": {"type": replay.MODEL_RESOURCE_TYPE, "instance": 4582},
            "classification": "client-state-observed",
            "provenance": "Playfield.Identity",
        }
        live_epoch["base_playfield_direct"] = {
            "value": {"type": replay.MODEL_RESOURCE_TYPE, "instance": 4582},
            "classification": "client-state-observed",
            "provenance": "Playfield.ModelIdentity",
        }
        live_epoch["runtime_playfield_id_hint"] = None
        live_epoch["base_playfield_id_if_proven"] = None
        fields = complete_packet_fields()
        fields.update(
            {
                "runtime_identity_type": 0xC350,
                "runtime_identity_instance": 0x11223344,
                "runtime_playfield": live_epoch["runtime_playfield"],
                "runtime_playfield_id": 4582,
                "base_playfield_direct": live_epoch["base_playfield_direct"],
                "base_playfield_id_if_proven": 4582,
                "full_model_type_direct": replay.MODEL_RESOURCE_TYPE,
                "full_model_instance_direct": 4582,
                "monster_data": {
                    "state": "observed",
                    "provenance": "packet-observed",
                    "source": "raw SimpleCharFullUpdate.MonsterData",
                    "value": 123,
                },
                "textures": {
                    "state": "observed",
                    "provenance": "packet-observed",
                    "source": "raw SimpleCharFullUpdate.Textures",
                    "value": "0:123:0",
                },
                "meshes": {
                    "state": "observed",
                    "provenance": "packet-observed",
                    "source": "raw SimpleCharFullUpdate.Meshes",
                    "value": "0:456:0:0",
                },
                "heading": {
                    "state": "observed",
                    "provenance": "client-state-observed",
                    "source": "Dynel.Rotation",
                    "value": {"x": 0.0, "y": 0.5, "z": 0.0, "w": 0.8660254},
                },
                "client_visible_stats": [
                    {
                        "stat": "Level",
                        "stat_id": 54,
                        "value": 5,
                        "raw_value": 5,
                        "provenance": "client-state-observed",
                        "error": "",
                    }
                ],
            }
        )
        live_snapshot = snapshot(
            "epoch-0001",
            1,
            6,
            packet_refs=[packet_reference("scfu", 5, 5), packet_reference("stat", 6, 6)],
            fields=fields,
            positions={
                "world": {
                    "state": "observed",
                    "provenance": "client-state-observed",
                    "source": "Dynel.Position",
                    "value": {"x": 1.25, "y": 2.5, "z": 3.75},
                },
                "packet_scfu": evidence(
                    {"x": 1.25, "y": 2.5, "z": 3.75}, "packet-observed"
                ),
            },
        )
        live_snapshot["epoch_scoped_identity_key"] = "epoch-0001|(SimpleChar:11223344)"
        live_snapshot["lifecycle_lineage"] = "spawn:00000001"
        live_snapshot["client_object_pointer_diagnostic"] = {
            "value": "0x1234",
            "provenance": "client-state-observed",
            "authoritative": False,
            "stable_across_runs": False,
        }
        live_snapshot["client_state_provenance"] = {
            "runtime_identity": "Dynel.Identity",
            "position": "Dynel.Position",
        }
        live_snapshot["bridge_blockers"] = [
            "npc-specific-official-placement-identity-not-exposed"
        ]
        packet_event = {
            "schema_version": 1,
            "record_type": "packet_event",
            "capture_id": CAPTURE_ID,
            "zone_epoch_id": "epoch-0001",
            "zone_epoch_valid": True,
            "captured_utc": "2026-08-27T12:00:00Z",
            "direction": "IN",
            "global_ordinal": 4,
            "sequence": 4,
            "decode_error": "",
        }
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(
                temporary,
                [live_epoch, live_snapshot, packet_event],
                [scfu_row(5, 5)],
                [stat_row(6, 6)],
            ).build()
        observation = artifact["observations"][0]
        self.assertEqual(4582, artifact["epochs"][0]["runtime_playfield"]["value"])
        self.assertEqual(4582, observation["base_playfield_direct"]["value"])
        self.assertEqual(["0:123:0"], observation["textures"]["value"])
        self.assertEqual("direct-candidate", observation["bridge_state"])
        self.assertEqual(
            f"{CAPTURE_ID}|(SimpleChar:11223344)",
            observation["harvested_observation_id"],
        )
        self.assertNotEqual(observation["observation_id"], observation["harvested_observation_id"])
        self.assertIn(
            "npc-specific-official-placement-identity-not-exposed",
            observation["bridge_blockers"],
        )
        self.assertEqual(1, len(observation["client_state_provenance"]))
        self.assertEqual(
            "epoch-0001|(SimpleChar:11223344)",
            observation["epoch_scoped_identity_key"]["value"],
        )
        self.assertEqual("derived", observation["epoch_scoped_identity_key"]["classification"])
        self.assertEqual("spawn:00000001", observation["lifecycle_lineage"]["value"])
        self.assertEqual("derived", observation["lifecycle_lineage"]["classification"])
        self.assertEqual(
            "client-state-observed",
            observation["client_object_pointer_diagnostic"]["classification"],
        )
        self.assertFalse(
            observation["client_object_pointer_diagnostic"]["value"]["authoritative"]
        )
        self.assertEqual(
            {"value", "classification", "provenance"}, set(observation["monster_data"])
        )
        self.assertTrue(artifact["parity"]["packet_fields_match"], artifact["parity"])

    def test_explicitly_unassigned_transition_packet_is_not_a_packet_parity_conflict(self):
        def build(include_snapshot):
            live_epoch = epoch("epoch-0001", 1)
            row = scfu_row(5, 5)
            transition_packet = live_scfu_record(row, [live_epoch])
            transition_packet["zone_epoch_id"] = None
            transition_packet["zone_epoch_valid"] = False
            records = [live_epoch, transition_packet]
            if include_snapshot:
                records.append(
                    snapshot(
                        "epoch-0001",
                        1,
                        6,
                        packet_refs=[packet_reference("scfu", 5, 5)],
                    )
                )
            with tempfile.TemporaryDirectory() as temporary:
                fixture = CaptureFixture(temporary, records)
                write_csv(fixture.scfu, SCFU_HEADERS, [row])
                return fixture.build()

        unreferenced = build(include_snapshot=False)
        self.assertEqual([], unreferenced["parity"]["conflicts"])

        referenced = build(include_snapshot=True)
        reasons = [conflict["reason"] for conflict in referenced["parity"]["conflicts"]]
        self.assertNotIn("live-packet-epoch-assignment-conflict", reasons)
        self.assertIn("referenced-live-packet-record-invalid", reasons)
        self.assertIsNone(referenced["observations"][0]["runtime_identity_instance"]["value"])

    def test_failed_scfu_and_partial_stat_rows_are_audit_only_until_referenced(self):
        def build(kind, include_snapshot):
            live_epoch = epoch("epoch-0001", 1)
            if kind == "scfu":
                analyzer_row = scfu_row(5, 5)
                analyzer_row["DecodeStatus"] = "decoded_partial"
                analyzer_row["DecodeFullyConsumed"] = "false"
                packet = live_scfu_record(analyzer_row, [live_epoch])
                packet["decode_error"] = "truncated packet"
            else:
                analyzer_row = stat_row(5, 5)
                analyzer_row["DecodeStatus"] = "decoded_partial"
                analyzer_row["DecodeFullyConsumed"] = "false"
                packet = live_stat_record(analyzer_row, [live_epoch])
                packet["decode_fully_consumed"] = False
            records = [live_epoch, packet]
            if include_snapshot:
                records.append(
                    snapshot(
                        "epoch-0001",
                        1,
                        6,
                        packet_refs=[packet_reference(kind, 5, 5)],
                    )
                )
            with tempfile.TemporaryDirectory() as temporary:
                fixture = CaptureFixture(temporary, records)
                if kind == "scfu":
                    write_csv(fixture.scfu, SCFU_HEADERS, [analyzer_row])
                else:
                    write_csv(fixture.stats, STAT_HEADERS, [analyzer_row])
                return fixture.build()

        for kind in ("scfu", "stat"):
            with self.subTest(kind=kind, referenced=False):
                unreferenced = build(kind, include_snapshot=False)
                self.assertEqual([], unreferenced["parity"]["conflicts"])
                self.assertTrue(unreferenced["parity"]["packet_fields_match"])
            with self.subTest(kind=kind, referenced=True):
                referenced = build(kind, include_snapshot=True)
                reasons = [
                    conflict["reason"] for conflict in referenced["parity"]["conflicts"]
                ]
                self.assertEqual(["referenced-live-packet-record-invalid"], reasons)
                self.assertIsNone(
                    referenced["observations"][0]["runtime_identity_instance"]["value"]
                )

    def test_packet_zone_epoch_valid_requires_json_true(self):
        live_epoch = epoch("epoch-0001", 1)
        row = scfu_row(5, 5)
        packet = live_scfu_record(row, [live_epoch])
        packet["zone_epoch_valid"] = 1
        with tempfile.TemporaryDirectory() as temporary:
            fixture = CaptureFixture(temporary, [live_epoch, packet])
            write_csv(fixture.scfu, SCFU_HEADERS, [row])
            artifact = fixture.build()
        self.assertIn(
            "live-packet-epoch-assignment-conflict",
            [conflict["reason"] for conflict in artifact["parity"]["conflicts"]],
        )

    def test_snapshot_requires_finalized_and_live_epoch_validity(self):
        cases = (
            (epoch("epoch-0001", 1, valid=False), True, "snapshot-finalized-epoch-invalid"),
            (
                {**epoch("epoch-0001", 1, valid=True), "valid": "false"},
                True,
                "snapshot-finalized-epoch-invalid",
            ),
            (epoch("epoch-0001", 1, end=None, valid=True), True, "snapshot-finalized-epoch-invalid"),
            (epoch("epoch-0001", 1, valid=True), False, "snapshot-input-zone-epoch-invalid"),
        )
        for live_epoch, input_epoch_valid, expected_reason in cases:
            with self.subTest(expected_reason=expected_reason):
                live_snapshot = snapshot(
                    "epoch-0001", 1, 1, fields=direct_identity_fields()
                )
                live_snapshot["zone_epoch_valid"] = input_epoch_valid
                live_snapshot["bridge_state"] = "direct-candidate"
                with tempfile.TemporaryDirectory() as temporary:
                    artifact = CaptureFixture(temporary, [live_epoch, live_snapshot]).build()
                self.assertEqual("invalid-epoch", artifact["observations"][0]["bridge_state"])
                self.assertIn(
                    expected_reason,
                    [conflict["reason"] for conflict in artifact["parity"]["conflicts"]],
                )

    def test_input_conflict_and_stale_bridge_states_are_not_discarded(self):
        cases = (
            ("conflict", "conflict", "input-bridge-state-conflict"),
            ("invalid-epoch", "invalid-epoch", "input-bridge-state-invalid-epoch"),
            ("stale", "invalid-epoch", "input-bridge-state-stale-epoch"),
        )
        for input_state, output_state, expected_reason in cases:
            with self.subTest(input_state=input_state):
                live_snapshot = snapshot(
                    "epoch-0001", 1, 1, fields=direct_identity_fields()
                )
                live_snapshot["zone_epoch_valid"] = True
                live_snapshot["bridge_state"] = input_state
                with tempfile.TemporaryDirectory() as temporary:
                    artifact = CaptureFixture(
                        temporary, [epoch("epoch-0001", 1), live_snapshot]
                    ).build()
                self.assertEqual(output_state, artifact["observations"][0]["bridge_state"])
                self.assertIn(
                    expected_reason,
                    [conflict["reason"] for conflict in artifact["parity"]["conflicts"]],
                )

    def test_malformed_live_bridge_blockers_fail_closed(self):
        live_snapshot = snapshot("epoch-0001", 1, 1, fields=direct_identity_fields())
        live_snapshot["bridge_blockers"] = "not-a-string-list"
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(
                temporary, [epoch("epoch-0001", 1), live_snapshot]
            ).build()
        observation = artifact["observations"][0]
        self.assertEqual("conflict", observation["bridge_state"])
        self.assertIn(
            "input-bridge-blockers-malformed",
            [conflict["reason"] for conflict in artifact["parity"]["conflicts"]],
        )

    def test_harvested_observation_id_uses_harvester_hex_identity_format(self):
        live_snapshot = snapshot(
            "epoch-0001",
            1,
            1,
            fields=direct_identity_fields(runtime_instance=0x1A2B),
        )
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(
                temporary, [epoch("epoch-0001", 1), live_snapshot]
            ).build()
        observation = artifact["observations"][0]
        self.assertEqual(
            f"{CAPTURE_ID}|(SimpleChar:1A2B)", observation["harvested_observation_id"]
        )
        self.assertNotEqual(observation["observation_id"], observation["harvested_observation_id"])

    def test_acg_hash_identity_claim_is_forced_false_and_conflicts(self):
        record = snapshot("epoch-0001", 1, 1)
        record["acg_hash_used_as_runtime_identity"] = True
        with tempfile.TemporaryDirectory() as temporary:
            artifact = CaptureFixture(temporary, [epoch("epoch-0001", 1), record]).build()
        observation = artifact["observations"][0]
        self.assertFalse(observation["acg_hash_used_as_runtime_identity"])
        self.assertEqual("conflict", observation["bridge_state"])

    def test_artifact_digest_and_bytes_are_deterministic(self):
        records = [epoch("epoch-0001", 1), snapshot("epoch-0001", 1, 1)]
        with tempfile.TemporaryDirectory() as temporary:
            fixture = CaptureFixture(temporary, records)
            first = fixture.build()
            second = fixture.build()
            output = Path(temporary) / replay.DEFAULT_OUTPUT_JSON
            replay.write_artifact(output, first, check=False)
            first_bytes = output.read_bytes()
            replay.write_artifact(output, second, check=False)
            second_bytes = output.read_bytes()
        self.assertEqual(first["digest"], second["digest"])
        self.assertEqual(first_bytes, second_bytes)
        self.assertEqual(
            {
                "schema_version",
                "capture_id",
                "epochs",
                "observations",
                "parity",
                "source_files",
                "digest",
            },
            set(first),
        )

    def test_cli_rejects_output_and_pending_path_source_collisions(self):
        with tempfile.TemporaryDirectory() as temporary:
            fixture = CaptureFixture(temporary, [epoch("epoch-0001", 1)])
            live_before = fixture.live.read_bytes()
            with redirect_stdout(StringIO()), redirect_stderr(StringIO()):
                result = replay.main([temporary, "--output", str(fixture.live)])
            self.assertEqual(1, result)
            self.assertEqual(live_before, fixture.live.read_bytes())

            pending_live = Path(temporary) / "custom-output.pending"
            pending_live.write_bytes(live_before)
            output = Path(temporary) / "custom-output"
            with redirect_stdout(StringIO()), redirect_stderr(StringIO()):
                result = replay.main(
                    [
                        temporary,
                        "--live-jsonl",
                        str(pending_live),
                        "--output",
                        str(output),
                    ]
                )
            self.assertEqual(1, result)
            self.assertEqual(live_before, pending_live.read_bytes())
            self.assertFalse(output.exists())

    def test_overlapping_epoch_ranges_fail_closed(self):
        records = [epoch("epoch-0001", 1, 10), epoch("epoch-0002", 10)]
        with tempfile.TemporaryDirectory() as temporary:
            fixture = CaptureFixture(temporary, records)
            with self.assertRaises(replay.ReplayError):
                fixture.build()


if __name__ == "__main__":
    unittest.main()
