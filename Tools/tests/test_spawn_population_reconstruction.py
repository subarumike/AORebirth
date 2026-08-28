import unittest

from Tools import spawn_population_reconstruction as reconstruction


def placement(record_id, x, y=0.0, z=0.0, *, playfield=1, acg="TEST", profile=None):
    return {
        "OfficialSpawnRecordId": record_id,
        "PlayfieldId": playfield,
        "OfficialDistrictId": f"district-{playfield}-0",
        "DistrictIndex": 0,
        "DistrictName": "Test District",
        "DistrictRecordOrdinal": int(record_id.rsplit("-", 1)[-1]),
        "CanonicalAcgHashText": acg,
        "OfficialAcgHashNativeUInt32": 1,
        "PositionX": float(x),
        "PositionY": float(y),
        "PositionZ": float(z),
        "ExistingAoRebirthProfile": profile,
        "ResolvedMonsterData": None,
        "UnknownFields": {},
    }


def topology_population(population_id="population-1", profile=None):
    return {
        "populationId": population_id,
        "existingAoRebirthProfiles": [profile] if profile else [],
        "placements": [{"aorebirthOverlay": {"resolvedMonsterData": None}}],
    }


def classification_row(**overrides):
    row = {
        "contextPlayfield": 1,
        "runtimePlayfield": 101,
        "monsterData": 17655,
        "archetypeId": "archetype-leet",
        "structuralFamily": "family-leet",
        "name": "Leet",
        "candidatePopulationIds": [],
        "exactCandidatePlacementIds": [],
        "resolverMatchState": "unmatched",
        "resolverBlockingReason": None,
        "resolvedBasePlayfieldId": None,
    }
    row.update(overrides)
    return row


def aggregate_row(observation_id, capture_id, runtime_identity, *, name="Leet", level=1, archetype="a", playfield=1):
    return {
        "observationId": observation_id,
        "captureId": capture_id,
        "runtimeIdentity": runtime_identity,
        "contextPlayfield": playfield,
        "runtimePlayfield": 100 + playfield,
        "monsterData": 17655,
        "archetypeId": archetype,
        "structuralFamily": "family",
        "name": name,
        "level": level,
        "firstObservedPosition": [1.0, 2.0, 3.0],
        "movementEnvelope": {"maximumDisplacement": 0.0},
        "coverage": {
            "appearance": True,
            "stats": False,
            "combat": False,
            "movement": False,
            "lifecycle": False,
            "loot": False,
            "respawn": False,
        },
        "association": {
            "scope": reconstruction.SCOPE_PLAYFIELD,
            "strength": "corroborating",
            "targetPopulationId": None,
            "blockingReasons": ["no local owner"],
        },
    }


class SpawnPopulationNegativeTests(unittest.TestCase):
    def test_runtime_id_never_becomes_persistent_spawn_identity(self):
        self.assertNotEqual(
            reconstruction.runtime_instance_key("capture-a", "SimpleChar:1"),
            "SimpleChar:1",
        )

    def test_acghash_never_becomes_monsterdata(self):
        projected = reconstruction.placement_projection(placement("record-0", 0))
        self.assertNotIn("monsterData", projected["acgHash"])

    def test_monsterdata_never_becomes_acghash(self):
        key = reconstruction.runtime_group_key(classification_row())
        self.assertNotIn("TEST", key)

    def test_different_names_share_one_visual_archetype(self):
        left = aggregate_row("o1", "c1", "id1", name="Leet")
        right = aggregate_row("o2", "c1", "id2", name="Eleet")
        groups = reconstruction.aggregate_runtime_rows([left, right])
        self.assertEqual(len(groups), 1)
        self.assertEqual(groups[0]["names"], ["Eleet", "Leet"])

    def test_different_levels_share_one_visual_archetype(self):
        left = aggregate_row("o1", "c1", "id1", level=1)
        right = aggregate_row("o2", "c1", "id2", level=4)
        group = reconstruction.aggregate_runtime_rows([left, right])[0]
        self.assertEqual((group["levelMinimum"], group["levelMaximum"]), (1, 4))

    def test_different_placements_share_one_population(self):
        topology, _ = reconstruction.build_topology(
            [placement("record-0", 0), placement("record-1", 10)], threshold=25
        )
        self.assertEqual(len(topology), 1)
        self.assertEqual(topology[0]["placementCount"], 2)

    def test_current_moved_position_is_not_automatically_spawn(self):
        self.assertFalse(classification_row().get("currentMovedPositionUsedAsSpawn", False))

    def test_proximity_with_multiple_candidates_is_not_exact(self):
        row = classification_row(candidatePopulationIds=["p1", "p2"])
        result = reconstruction.classify_scope(
            row, {"captureCount": 2, "runtimeIdsChangeAcrossCaptures": True},
            {"p1": topology_population("p1"), "p2": topology_population("p2")},
        )
        self.assertEqual(result["scope"], reconstruction.SCOPE_PLAYFIELD)

    def test_population_association_does_not_imply_exact_row(self):
        row = classification_row(candidatePopulationIds=["p1"])
        result = reconstruction.classify_scope(
            row, {"captureCount": 2, "runtimeIdsChangeAcrossCaptures": True},
            {"p1": topology_population("p1")},
        )
        self.assertEqual(result["scope"], reconstruction.SCOPE_LOCAL)
        self.assertFalse(result["explicitIdBridge"])

    def test_playfield_observation_does_not_imply_local_cluster(self):
        result = reconstruction.classify_scope(
            classification_row(), {"captureCount": 1, "runtimeIdsChangeAcrossCaptures": False}, {}
        )
        self.assertEqual(result["scope"], reconstruction.SCOPE_PLAYFIELD)

    def test_loot_does_not_define_visual_archetype(self):
        left = aggregate_row("o1", "c1", "id1")
        right = aggregate_row("o2", "c1", "id2")
        right["coverage"]["loot"] = True
        self.assertEqual(reconstruction.runtime_group_key(left), reconstruction.runtime_group_key(right))

    def test_runtime_id_reuse_across_epochs_does_not_merge_instances(self):
        first = reconstruction.runtime_instance_key("capture-a", "SimpleChar:1")
        second = reconstruction.runtime_instance_key("capture-b", "SimpleChar:1")
        self.assertNotEqual(first, second)

    def test_multiple_archetypes_in_one_cluster_remain_distinct(self):
        left = aggregate_row("o1", "c1", "id1", archetype="a")
        right = aggregate_row("o2", "c1", "id2", archetype="b")
        self.assertEqual(len(reconstruction.aggregate_runtime_rows([left, right])), 2)

    def test_heuristic_only_assignment_remains_blocked(self):
        row = classification_row(candidatePopulationIds=["p1"])
        result = reconstruction.classify_scope(
            row, {"captureCount": 1, "runtimeIdsChangeAcrossCaptures": False},
            {"p1": topology_population("p1")},
        )
        self.assertEqual(result["scope"], reconstruction.SCOPE_PLAYFIELD)
        self.assertIn("heuristic", " ".join(result["blockingReasons"]))

    def test_static_acg_monsterdata_bridge_is_not_reintroduced(self):
        self.assertNotEqual(reconstruction.OFFICIAL_PLACEMENTS, reconstruction.MONSTER_DATA_RECORDS)


class SpawnPopulationPositiveTests(unittest.TestCase):
    def test_exact_placement_correlation_with_unique_coordinate_candidate(self):
        row = classification_row(
            candidatePopulationIds=["p1"],
            exactCandidatePlacementIds=["record-0"],
            resolverMatchState="unique-proven",
            resolvedBasePlayfieldId=1,
        )
        result = reconstruction.classify_scope(
            row, {"captureCount": 1, "runtimeIdsChangeAcrossCaptures": False},
            {"p1": topology_population("p1")},
        )
        self.assertEqual(result["scope"], reconstruction.SCOPE_EXACT)
        self.assertFalse(result["explicitIdBridge"])

    def test_multiple_acg_rows_associate_with_repeated_local_population(self):
        topology, _ = reconstruction.build_topology(
            [placement("record-0", 0), placement("record-1", 10)], threshold=25
        )
        population_id = topology[0]["populationId"]
        result = reconstruction.classify_scope(
            classification_row(candidatePopulationIds=[population_id]),
            {"captureCount": 2, "runtimeIdsChangeAcrossCaptures": True},
            {population_id: topology[0]},
        )
        self.assertEqual(result["scope"], reconstruction.SCOPE_LOCAL)

    def test_same_monsterdata_with_different_runtime_ids_across_sessions(self):
        rows = [aggregate_row("o1", "c1", "id1"), aggregate_row("o2", "c2", "id2")]
        group = reconstruction.aggregate_runtime_rows(rows)[0]
        self.assertTrue(group["runtimeIdsChangeAcrossCaptures"])
        self.assertEqual(group["runtimeInstanceCount"], 2)

    def test_same_archetype_across_multiple_playfields(self):
        rows = [
            reconstruction.aggregate_runtime_rows([aggregate_row("o1", "c1", "id1", playfield=1)])[0],
            reconstruction.aggregate_runtime_rows([aggregate_row("o2", "c2", "id2", playfield=2)])[0],
        ]
        reuse = reconstruction.archetype_reuse(rows)[0]
        self.assertEqual(reuse["playfields"], [1, 2])

    def test_different_names_group_under_shared_archetype(self):
        rows = [aggregate_row("o1", "c1", "id1", name="Leet"), aggregate_row("o2", "c1", "id2", name="Eleet")]
        self.assertEqual(reconstruction.aggregate_runtime_rows(rows)[0]["observationCount"], 2)

    def test_level_variation_is_retained_contextually(self):
        rows = [aggregate_row("o1", "c1", "id1", level=1), aggregate_row("o2", "c1", "id2", level=15)]
        group = reconstruction.aggregate_runtime_rows(rows)[0]
        self.assertEqual(group["levelMaximum"], 15)

    def test_population_evidence_aggregation_retains_coverage(self):
        row = aggregate_row("o1", "c1", "id1")
        row["coverage"]["combat"] = True
        group = reconstruction.aggregate_runtime_rows([row])[0]
        self.assertEqual(group["coverageCounts"]["combat"], 1)

    def test_placement_topology_is_preserved_independently(self):
        topology, mapping = reconstruction.build_topology(
            [placement("record-0", 0), placement("record-1", 100)], threshold=25
        )
        self.assertEqual(len(topology), 2)
        self.assertEqual(set(mapping), {"record-0", "record-1"})


if __name__ == "__main__":
    unittest.main()
