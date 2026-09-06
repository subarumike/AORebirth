import unittest

from Tools import mission_capture_wave_planner as planner


class MissionCaptureWavePlannerTests(unittest.TestCase):
    def test_discovery_window_counts_only_new_destinations(self):
        values = [1, 2, 1, 3, 2, 4]
        self.assertEqual(planner.new_in_tail(values, 3), 2)
        self.assertEqual(planner.new_in_tail(values, 6), 4)

    def test_ranges_are_canonical(self):
        self.assertEqual(planner.compact_ranges([1, 2, 3, 5, 7, 8]), "1-3, 5, 7-8")
        self.assertEqual(planner.compact_ranges([]), "none")

    def test_assignment_covers_every_target_once(self):
        table = {2: (1, 2, 3), 3: (2, 3, 4), 4: (3, 4, 5)}
        assignments = planner.assign_targets({1, 2, 3, 4, 5}, [2, 3, 4], table, {})
        self.assertEqual({row["mission_ql"] for row in assignments}, {1, 2, 3, 4, 5})
        self.assertEqual(len(assignments), 5)

    def test_saturation_is_not_probability(self):
        self.assertEqual(planner.saturation_label(0, 0, 0), "NOT_CAPTURED")
        self.assertEqual(planner.saturation_label(90, 30, 20), "LOW_SAMPLE")
        self.assertEqual(planner.saturation_label(1250, 0, 0), "SATURATED_FOR_DISCOVERY")
        self.assertEqual(planner.saturation_label(1250, 1, 3), "STABILIZING")
        self.assertEqual(planner.saturation_label(1250, 4, 9), "EXPANDING")


if __name__ == "__main__":
    unittest.main()
