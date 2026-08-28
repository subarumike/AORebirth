import copy
import unittest

from Tools import enemy_archetype_census as census


def state(value=None, kind="value"):
    return {"state": kind, "value": value if kind == "value" else None}


def cat(record_id, raw, joint, structure, texture):
    return {
        "recordId": record_id,
        "rawSha256": raw,
        "jointSha256": joint,
        "meshStructureSha256": structure,
        "textureSha256": texture,
    }


def monster(monster_data, name, mesh, head=None, animation="anim-a", features=32775):
    return {
        "monsterData": monster_data,
        "officialName": name,
        "mesh": state(mesh),
        "headMesh": state(kind="absent") if head is None else state(head),
        "features": state(features),
        "fabricType": state(0),
        "charRadius": state(3),
        "animationGroupMapSha256": animation,
    }


def observed(value, classification="packet-observed", status="captured"):
    return {"evidenceClassification": classification, "status": status, "value": value}


def observation(observation_id, monster_data, name="Mob", level=1, textures=None, meshes=None):
    return {
        "observationId": observation_id,
        "identity": observation_id.split("|")[-1],
        "name": name,
        "resourcePlayfieldId": 1,
        "runtimePlayfieldId": 1001,
        "categoryEvidence": {},
        "fields": {
            "monsterData": observed(monster_data),
            "catMesh": observed(None, "not-observed", "not observed"),
            "level": observed(level),
            "appearanceValue": observed(8),
            "headMesh": observed(0),
            "textures": observed([] if textures is None else textures),
            "meshes": observed([] if meshes is None else meshes),
            "breed": observed("Leet"),
            "gender": observed("Unknown"),
            "race": observed(0),
            "visualFlags": observed(31),
        },
    }


def fixture_source():
    return {
        "monsterDataRecords": [
            monster(1, "Leet", 100),
            monster(2, "Beach Leet", 100),
            monster(3, "Heckler of Stones", 200, animation="anim-h"),
            monster(4, "Heckler Boss", 200, animation="anim-h"),
            monster(5, "Leet", 101),
        ],
        "catMeshRecords": [
            cat(100, "raw-a", "joint-cute", "structure-cute", "texture-a"),
            cat(101, "raw-b", "joint-cute", "structure-cute", "texture-b"),
            cat(200, "raw-h", "joint-rock", "structure-rock", "texture-h"),
        ],
    }


class EnemyArchetypeCensusTests(unittest.TestCase):
    def setUp(self):
        self.source = fixture_source()
        self.official = census.build_official_archetypes(self.source)

    def test_01_different_names_share_visual_archetype(self):
        self.assertEqual(
            self.official["monsterToArchetype"][1],
            self.official["monsterToArchetype"][2],
        )

    def test_02_different_levels_share_visual_archetype(self):
        first = census.resolve_runtime_observation(
            observation("c|one", 1, level=1),
            self.official["monsterToArchetype"],
            self.official["catMeshToArchetypes"],
        )
        second = census.resolve_runtime_observation(
            observation("c|two", 1, level=200),
            self.official["monsterToArchetype"],
            self.official["catMeshToArchetypes"],
        )
        self.assertEqual(first["archetypeId"], second["archetypeId"])

    def test_03_different_acg_placements_share_visual_archetype(self):
        first = census.classify_placement(
            {"CanonicalAcgHashText": "AAAA", "OfficialModelReference": {"monsterData": 1, "provenance": "direct-official"}},
            self.official["monsterToArchetype"],
        )
        second = census.classify_placement(
            {"CanonicalAcgHashText": "BBBB", "OfficialModelReference": {"monsterData": 2, "provenance": "direct-official"}},
            self.official["monsterToArchetype"],
        )
        self.assertEqual(first["archetypeId"], second["archetypeId"])

    def test_04_same_name_with_different_visuals_stays_separate(self):
        self.assertNotEqual(
            self.official["monsterToArchetype"][1],
            self.official["monsterToArchetype"][5],
        )

    def test_05_same_monsterdata_runtime_visual_difference_is_preserved(self):
        first = observation("c|one", 1, textures=[{"place": 0, "id": 10, "unknown": 0}])
        second = observation("c|two", 1, textures=[{"place": 0, "id": 11, "unknown": 0}])
        archetype = self.official["monsterToArchetype"][1]
        self.assertNotEqual(
            census.stable_id("runtime-visual", census.runtime_visual_variant_payload(first, archetype)),
            census.stable_id("runtime-visual", census.runtime_visual_variant_payload(second, archetype)),
        )

    def test_06_sentinel_does_not_enter_signature_value(self):
        field = census.normalize_observed_field(observed(census.UNSET_SENTINEL))
        self.assertEqual(field, {"state": "sentinel/default", "value": None})

    def test_07_missing_field_does_not_become_zero(self):
        self.assertNotEqual(
            census.normalize_official_state(state(kind="absent")),
            census.normalize_official_state(state(0)),
        )

    def test_08_texture_array_differences_are_preserved(self):
        archetype = self.official["monsterToArchetype"][1]
        first = census.runtime_visual_variant_payload(
            observation("c|one", 1, textures=[{"place": 0, "id": 10, "unknown": 0}]), archetype
        )
        second = census.runtime_visual_variant_payload(
            observation("c|two", 1, textures=[{"place": 0, "id": 12, "unknown": 0}]), archetype
        )
        self.assertNotEqual(census.canonical_digest(first), census.canonical_digest(second))

    def test_09_mesh_array_differences_are_preserved(self):
        archetype = self.official["monsterToArchetype"][1]
        first = census.runtime_visual_variant_payload(
            observation("c|one", 1, meshes=[{"place": 0, "slot": 4, "id": 10, "unknown": 0}]), archetype
        )
        second = census.runtime_visual_variant_payload(
            observation("c|two", 1, meshes=[{"place": 0, "slot": 4, "id": 11, "unknown": 0}]), archetype
        )
        self.assertNotEqual(census.canonical_digest(first), census.canonical_digest(second))

    def test_10_humanoid_equipment_order_normalizes_by_explicit_slot(self):
        archetype = self.official["monsterToArchetype"][1]
        rows = [
            {"place": 1, "slot": 2, "id": 20, "unknown": 0},
            {"place": 0, "slot": 4, "id": 10, "unknown": 0},
        ]
        first = census.runtime_visual_variant_payload(observation("c|one", 1, meshes=rows), archetype)
        second = census.runtime_visual_variant_payload(observation("c|two", 1, meshes=list(reversed(rows))), archetype)
        self.assertEqual(census.canonical_digest(first), census.canonical_digest(second))

    def test_11_boss_context_can_share_visual_archetype(self):
        self.assertEqual(
            self.official["monsterToArchetype"][3],
            self.official["monsterToArchetype"][4],
        )

    def test_12_leet_case_study_expands_visual_family(self):
        case = census.build_case_study("Leet", "leet", self.official["archetypes"])
        self.assertIn(self.official["monsterToArchetype"][5], case["visualArchetypes"])
        self.assertEqual(len(case["baseModelFamilies"]), 1)

    def test_13_heckler_case_study_uses_shared_resources(self):
        case = census.build_case_study("Heckler", "heckler", self.official["archetypes"])
        self.assertEqual(set(case["monsterData"]), {3, 4})
        self.assertEqual(len(case["visualArchetypes"]), 1)

    def test_14_acghash_is_not_used_as_archetype_identity(self):
        unresolved = census.classify_placement(
            {"CanonicalAcgHashText": "LEET", "ResolvedMonsterData": 1},
            self.official["monsterToArchetype"],
        )
        self.assertEqual(unresolved["associationState"], "unresolved")
        self.assertIsNone(unresolved["archetypeId"])

    def test_15_runtime_identity_is_not_used_as_archetype_identity(self):
        first = census.resolve_runtime_observation(
            observation("c|runtime-a", 1), self.official["monsterToArchetype"], self.official["catMeshToArchetypes"]
        )
        second = census.resolve_runtime_observation(
            observation("c|runtime-b", 1), self.official["monsterToArchetype"], self.official["catMeshToArchetypes"]
        )
        self.assertEqual(first["archetypeId"], second["archetypeId"])

    def test_16_runtime_observation_resolves_without_exact_placement(self):
        result = census.resolve_runtime_observation(
            observation("c|runtime", 1), self.official["monsterToArchetype"], self.official["catMeshToArchetypes"]
        )
        self.assertEqual(result["associationState"], "unique")
        self.assertEqual(result["associationBasis"], "direct-official-monsterdata-resource-chain")

    def test_17_deterministic_repeated_build(self):
        first = census.build_official_archetypes(copy.deepcopy(self.source))
        second = census.build_official_archetypes(copy.deepcopy(self.source))
        self.assertEqual(census.canonical_digest(first["archetypes"]), census.canonical_digest(second["archetypes"]))

    def test_18_unproven_resolvedmonsterdata_is_rejected_for_placement(self):
        row = census.classify_placement(
            {"ResolvedMonsterData": 1, "MobTemplateEvidenceSource": "AORebirth runtime"},
            self.official["monsterToArchetype"],
        )
        self.assertEqual(row["associationState"], "unresolved")

    def test_19_name_is_excluded_from_visual_signature(self):
        changed = fixture_source()
        changed["monsterDataRecords"][0]["officialName"] = "Completely Different Name"
        rebuilt = census.build_official_archetypes(changed)
        self.assertEqual(
            self.official["monsterToArchetype"][1],
            rebuilt["monsterToArchetype"][1],
        )

    def test_20_loot_is_excluded_from_visual_signature(self):
        changed = fixture_source()
        changed["monsterDataRecords"][0]["loot"] = ["invented-test-only-item"]
        rebuilt = census.build_official_archetypes(changed)
        self.assertEqual(
            self.official["monsterToArchetype"][1],
            rebuilt["monsterToArchetype"][1],
        )


if __name__ == "__main__":
    unittest.main()
