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
