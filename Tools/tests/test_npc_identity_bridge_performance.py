import unittest

from Tools import simulate_npc_identity_bridge_load as load_sim


class NpcIdentityBridgePerformanceTests(unittest.TestCase):
    def setUp(self):
        self.result = load_sim.simulate()

    def test_load_has_at_least_38_npcs(self):
        self.assertGreaterEqual(self.result["npcs"], 38)

    def test_load_has_thousands_of_observation_opportunities(self):
        self.assertGreaterEqual(self.result["observation_opportunities"], 2000)

    def test_snapshot_work_does_not_run_away(self):
        self.assertLess(self.result["snapshots_emitted"], 100)

    def test_duplicate_observations_are_suppressed(self):
        self.assertGreater(self.result["redundant_suppressed"], 3000)

    def test_incomplete_retry_is_bounded(self):
        self.assertTrue(self.result["bounded_retry_pass"])
        self.assertEqual(3, self.result["retries_total"])

    def test_sparse_scfu_attaches(self):
        self.assertTrue(self.result["late_scfu_link_pass"])
        self.assertEqual(7, self.result["scfu_packets"])

    def test_sparse_stat_attaches(self):
        self.assertTrue(self.result["late_stat_link_pass"])
        self.assertEqual(5, self.result["stat_packets"])

    def test_delayed_model_identity_attaches(self):
        self.assertTrue(self.result["model_delay_pass"])
        self.assertEqual(4, self.result["delayed_playfield_model_first_valid_round"])

    def test_raw_capture_path_is_lossless(self):
        self.assertTrue(self.result["lossless_raw_pass"])
        self.assertEqual(0, self.result["raw_packet_loss"])

    def test_stat_workload_is_materially_reduced(self):
        self.assertLess(
            self.result["new_estimated_getstat_calls"],
            self.result["old_estimated_getstat_calls"] // 100,
        )

    def test_simulation_is_deterministic(self):
        self.assertEqual(self.result["digest"], load_sim.simulate()["digest"])

    def test_31_npcs_without_scfu_are_not_decode_failures(self):
        self.assertEqual(31, self.result["packet_not_received_scfu_npcs"])
        self.assertEqual(0, self.result["scfu_decode_failures"])

    def test_complete_npc_stops_expensive_retries(self):
        self.assertEqual(0, self.result["complete_npc_retry_count"])

    def test_no_enrichment_queue_means_no_queue_backpressure(self):
        self.assertEqual(0, self.result["enrichment_queue_depth_high_water"])
        self.assertEqual(0, self.result["dropped_enrichment_work"])

    def test_wrong_resource_type_is_not_promoted(self):
        self.assertFalse(self.result["wrong_resource_type_promoted"])

    def test_new_epoch_does_not_inherit_old_npc_evidence(self):
        self.assertFalse(self.result["new_epoch_inherits_old_npc_evidence"])

    def test_all_38_client_identities_are_retained(self):
        self.assertEqual(38, self.result["npcs"])


if __name__ == "__main__":
    unittest.main()
