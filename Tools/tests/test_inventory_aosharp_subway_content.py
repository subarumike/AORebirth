#!/usr/bin/env python3

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path


TOOLS = Path(__file__).resolve().parents[1]
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

import inventory_aosharp_subway_content as content


class CaptureRealmTests(unittest.TestCase):
    def test_runtime_127_is_private(self) -> None:
        realm, basis = content.capture_realm(
            {
                "capture_playfield_id": 127,
                "event_playfield_ids": "127",
                "runtime_playfield_ids": "(Playfield2:007F)",
            }
        )

        self.assertEqual("aorebirth_private", realm)
        self.assertIn("runtime-playfield-127", basis)

    def test_mapped_runtime_instance_is_official_live(self) -> None:
        realm, basis = content.capture_realm(
            {
                "capture_playfield_id": 1407006,
                "event_playfield_ids": "1407006",
                "runtime_playfield_ids": "(Playfield2:15781E)",
            }
        )

        self.assertEqual("official_live", realm)
        self.assertIn("mapped-official-runtime", basis)

    def test_conflicting_realm_signals_remain_unknown(self) -> None:
        realm, basis = content.capture_realm(
            {
                "capture_playfield_id": 127,
                "event_playfield_ids": "1407006",
                "runtime_playfield_ids": "",
            }
        )

        self.assertEqual("unknown", realm)
        self.assertIn("conflicting-private-and-official-signals", basis)

    def test_projected_runtime_127_refines_geometry_session_to_private(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "pf127-line-of-sight.csv"
            path.write_text(
                "RuntimePlayfieldId,TargetIdentity\n(Playfield2:007F),(SimpleChar:00000001)\n",
                encoding="utf-8",
            )

            realm, basis = content.refine_realm_from_projected_runtime(
                Path(directory),
                "unknown",
                "no-explicit-runtime-playfield",
            )

        self.assertEqual("aorebirth_private", realm)
        self.assertIn("pf127-line-of-sight.csv:RuntimePlayfieldId=127", basis)


class ScopeTests(unittest.TestCase):
    def test_mixed_session_never_blanket_scopes_to_subway(self) -> None:
        index = content.IdentityScopeIndex()

        self.assertEqual(
            "unscoped_mixed",
            index.resolve("(SimpleChar:00000001)", "", "MIXED"),
        )

    def test_unique_exact_identity_scope_can_be_joined(self) -> None:
        index = content.IdentityScopeIndex()
        index.register("(SimpleChar:00000001)", "subway_exact")

        self.assertEqual(
            "subway_joined",
            index.resolve("(SimpleChar:00000001)", "", "MIXED"),
        )

    def test_conflicting_identity_scope_is_not_promoted(self) -> None:
        index = content.IdentityScopeIndex()
        identity = "(SimpleChar:00000001)"
        index.register(identity, "subway_exact")
        index.register(identity, "elsewhere_exact")

        self.assertEqual("scope_conflict", index.resolve(identity, "", "MIXED"))


class IdentityTests(unittest.TestCase):
    def test_identity_normalization_is_typed_and_zero_padded(self) -> None:
        self.assertEqual(
            "(Corpse:00F69020)",
            content.normalize_identity("(Corpse:F69020)"),
        )

    def test_vendor_owner_numeric_identity_joins_to_simple_char(self) -> None:
        self.assertEqual(
            "(SimpleChar:79135F51)",
            content.identity_from_numeric(50000, 0x79135F51),
        )


class CorpseEvidenceTests(unittest.TestCase):
    def test_reused_corpse_identity_keeps_observation_names_separate(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            capture_path = root / "captures" / "20260708-143600"
            capture_path.mkdir(parents=True)
            (capture_path / "corpse-full-updates.csv").write_text(
                "CapturedUtc,CorpseIdentity,CorpseName,PlayfieldId,DeadNpcIdentity,"
                "DeadNpcName,CorpseCatMesh,CorpseCredits,CorpseMonsterData\n"
                "2026-07-08T19:38:22Z,(Corpse:00F6E002),Remains of Filth Flea,"
                "1187842,(SimpleChar:794DF17B),Filth Flea,15231,29,17657\n"
                "2026-07-08T20:00:09Z,(Corpse:00F6E002),Remains of Killer,"
                "1187842,(SimpleChar:794DF23C),Killer,96177,0,96195\n",
                encoding="utf-8",
            )
            references = {
                category: set() for category in content.REFERENCE_CATEGORIES
            }
            analyzer = content.CaptureAnalyzer(
                root,
                {
                    "capture_id": "20260708-143600",
                    "capture_path": "captures/20260708-143600",
                    "classification": "SUBWAY",
                    "confidence": "high",
                    "capture_playfield_id": 1187842,
                },
                references,
            )
            rows = {
                record.related_identity: content.evidence_to_row(record, references)
                for record in analyzer.analyze()
                if record.evidence_kind == "corpse_full_update"
            }

        flea = rows["(SimpleChar:794DF17B)"]
        killer = rows["(SimpleChar:794DF23C)"]
        self.assertEqual("Remains of Filth Flea", flea["subject_name"])
        self.assertEqual("Remains of Killer", killer["subject_name"])
        self.assertEqual("17657", flea["monster_data"])
        self.assertEqual("96195", killer["monster_data"])
        self.assertIn('"dead_npc_name":"Filth Flea"', flea["observed_values_json"])
        self.assertIn('"dead_npc_name":"Killer"', killer["observed_values_json"])


class PendingProjectionTests(unittest.TestCase):
    @staticmethod
    def _analyzer(root: Path, capture_id: str = "20260709-220439") -> content.CaptureAnalyzer:
        references = {
            category: set() for category in content.REFERENCE_CATEGORIES
        }
        return content.CaptureAnalyzer(
            root,
            {
                "capture_id": capture_id,
                "capture_path": "captures/" + capture_id,
                "classification": "SUBWAY",
                "confidence": "high",
                "capture_playfield_id": 1187842,
            },
            references,
        )

    def test_complete_scfu_and_exact_corpse_are_inventoried_from_pending(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            capture_path = root / "captures" / "20260709-220439"
            capture_path.mkdir(parents=True)
            (capture_path / "scfu-appearance.pending.csv").write_text(
                "CapturedUtc,Identity,Name,PlayfieldId,Level,Health,MonsterData,"
                "DecodeStatus,DecodeFullyConsumed\n"
                "2026-07-10T03:06:19Z,(SimpleChar:7953AD69),Disobedient Bot,"
                "1187842,5,73,17649,decoded_complete,true\n",
                encoding="utf-8",
            )
            (capture_path / "corpse-full-updates.pending.csv").write_text(
                "CapturedUtc,CorpseIdentity,CorpseName,PlayfieldId,DeadNpcIdentity,"
                "DeadNpcName,CorpseCatMesh,CorpseCredits,CorpseMonsterData\n"
                "2026-07-10T03:06:20Z,(Corpse:00F6E009),Remains of Disobedient Bot,"
                "1187842,(SimpleChar:7953AD69),Disobedient Bot,15215,11,17649\n",
                encoding="utf-8",
            )
            analyzer = self._analyzer(root)
            rows = [
                content.evidence_to_row(record, analyzer.references)
                for record in analyzer.analyze()
            ]

        scfu = next(row for row in rows if row["evidence_kind"] == "scfu_appearance")
        corpse = next(row for row in rows if row["evidence_kind"] == "corpse_full_update")
        self.assertEqual("scfu-appearance.pending.csv", scfu["source_artifact"])
        self.assertEqual("projection-pending-observed", scfu["evidence_status"])
        self.assertEqual("projection-pending", scfu["issues"])
        self.assertIn('"decode_status":"decoded_complete"', scfu["observed_values_json"])
        self.assertEqual("corpse-full-updates.pending.csv", corpse["source_artifact"])
        self.assertEqual("projection-pending-observed", corpse["evidence_status"])
        self.assertEqual("Remains of Disobedient Bot", corpse["subject_name"])
        self.assertEqual("11", corpse["numeric_min"])

    def test_final_projection_wins_without_merging_stale_pending_rows(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            capture_path = root / "captures" / "20260709-220439"
            capture_path.mkdir(parents=True)
            header = "CapturedUtc,Identity,Name,Level,Health,MonsterData\n"
            (capture_path / "scfu-appearance.csv").write_text(
                header
                + "2026-07-10T03:06:19Z,(SimpleChar:7953AD69),Final Bot,5,73,17649\n",
                encoding="utf-8",
            )
            (capture_path / "scfu-appearance.pending.csv").write_text(
                header
                + "2026-07-10T03:06:20Z,(SimpleChar:7953AD70),Pending Bot,6,80,17650\n",
                encoding="utf-8",
            )
            analyzer = self._analyzer(root)
            rows = [
                content.evidence_to_row(record, analyzer.references)
                for record in analyzer.analyze()
                if record.evidence_kind == "scfu_appearance"
            ]

        self.assertEqual(1, len(rows))
        self.assertEqual("(SimpleChar:7953AD69)", rows[0]["subject_identity"])
        self.assertEqual("scfu-appearance.csv", rows[0]["source_artifact"])
        self.assertEqual("observed", rows[0]["evidence_status"])
        self.assertEqual("", rows[0]["issues"])

    def test_pending_incomplete_scfu_decode_cannot_imply_complete_projection(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            capture_path = root / "captures" / "20260709-220439"
            capture_path.mkdir(parents=True)
            (capture_path / "scfu-appearance.pending.csv").write_text(
                "CapturedUtc,Identity,Name,Level,Health,MonsterData,DecodeStatus,"
                "DecodeFullyConsumed\n"
                "2026-07-10T03:06:19Z,(SimpleChar:7953AD69),Disobedient Bot,"
                "5,73,17649,raw_complete_decode_pending,false\n",
                encoding="utf-8",
            )
            analyzer = self._analyzer(root)
            row = next(
                content.evidence_to_row(record, analyzer.references)
                for record in analyzer.analyze()
                if record.evidence_kind == "scfu_appearance"
            )

        self.assertEqual("projection-pending-incomplete", row["evidence_status"])
        self.assertEqual(
            "incomplete-decode-not-absence;projection-pending",
            row["issues"],
        )
        self.assertIn(
            '"decode_status":"raw_complete_decode_pending"',
            row["observed_values_json"],
        )

    def test_pending_schema_is_validated_against_final_projection_contract(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            capture_path = root / "captures" / "20260709-220439"
            capture_path.mkdir(parents=True)
            (capture_path / "corpse-full-updates.pending.csv").write_text(
                "CapturedUtc,CorpseIdentity,CorpseName\n"
                "2026-07-10T03:06:20Z,(Corpse:00F6E009),Remains of Bot\n",
                encoding="utf-8",
            )
            analyzer = self._analyzer(root)
            records = analyzer.analyze()

        self.assertFalse(any(record.evidence_kind == "corpse_full_update" for record in records))
        self.assertIn(
            "corpse-full-updates.pending.csv:schema-missing="
            "CorpseCatMesh,CorpseCredits,CorpseMonsterData,DeadNpcIdentity,PlayfieldId",
            analyzer.issues,
        )

    def test_pending_incomplete_respawn_is_not_promoted_to_absence_or_complete(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            capture_path = root / "captures" / "20260709-220439"
            capture_path.mkdir(parents=True)
            (capture_path / "enemy-respawns.pending.csv").write_text(
                "Status,DeathIdentity,Name,MonsterData,DeathUtc,CorpseIdentity,"
                "RespawnIdentity,RespawnDelaySeconds,CandidateCount,Detail\n"
                "incomplete,(SimpleChar:7953AD69),Disobedient Bot,17649,"
                "2026-07-10T03:06:20Z,(Corpse:00F6E009),,,0,"
                "No later candidate was observed before capture stop.\n",
                encoding="utf-8",
            )
            analyzer = self._analyzer(root)
            rows = [
                content.evidence_to_row(record, analyzer.references)
                for record in analyzer.analyze()
                if record.evidence_kind.startswith("respawn_")
            ]

        self.assertEqual(1, len(rows))
        self.assertEqual("respawn_incomplete", rows[0]["evidence_kind"])
        self.assertEqual("enemy-respawns.pending.csv", rows[0]["source_artifact"])
        self.assertEqual("projection-pending-incomplete", rows[0]["evidence_status"])
        self.assertEqual(
            "incomplete-correlation-not-absence;projection-pending",
            rows[0]["issues"],
        )
        self.assertNotEqual("respawn_complete", rows[0]["evidence_kind"])


class LocationReferenceTests(unittest.TestCase):
    def test_inventory_outputs_do_not_self_satisfy_implementation_references(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            generated = root / "docs" / "generated"
            generated.mkdir(parents=True)
            (generated / "aosharp_capture_inventory.md").write_text(
                "20260708-004038\n",
                encoding="utf-8",
            )
            (generated / "aosharp_subway_capture_content.csv").write_text(
                "20260709-210452\n",
                encoding="utf-8",
            )
            (generated / "subway_enemy_combat_contracts.json").write_text(
                '"capture": "20260710-205400"\n',
                encoding="utf-8",
            )
            runtime = root / "AORebirth" / "Server" / "ZoneEngine"
            runtime.mkdir(parents=True)
            (runtime / "CapturedEvidence.cs").write_text(
                '// 20260712-161506\n',
                encoding="utf-8",
            )

            documented, indexed = (
                content.location_inventory.collect_repository_references(root)
            )

        self.assertNotIn("20260708-004038", documented)
        self.assertNotIn("20260709-210452", documented)
        self.assertIn(
            "docs/generated/subway_enemy_combat_contracts.json",
            indexed["20260710-205400"],
        )
        self.assertIn(
            "AORebirth/Server/ZoneEngine/CapturedEvidence.cs",
            indexed["20260712-161506"],
        )


if __name__ == "__main__":
    unittest.main()
