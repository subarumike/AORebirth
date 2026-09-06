import gzip
import json
import unittest
from collections import Counter

import mission_destination_eligibility_analysis as analysis


class MissionDestinationEligibilityTests(unittest.TestCase):
    def test_side_mapping_is_repository_side_enum(self):
        self.assertEqual(analysis.SIDE_NAMES[0], "Neutral")
        self.assertEqual(analysis.SIDE_NAMES[1], "Clan")
        self.assertEqual(analysis.SIDE_NAMES[2], "Omni")

    def test_wilson_interval_contains_observed_proportion(self):
        low, high = analysis.wilson(20, 100)
        self.assertLess(low, 0.2)
        self.assertGreater(high, 0.2)

    def test_independent_duplicate_model_bounds(self):
        probability = analysis.independent_duplicate_probability({1: 50, 2: 50})
        self.assertGreaterEqual(probability, 0.0)
        self.assertLessEqual(probability, 1.0)

    def test_signed_slider_values_remain_distinct(self):
        self.assertEqual(analysis.slider_label({"semantic_state": "SIGNED_VALUE", "semantic_value": -50}), "SIGNED_VALUE_-50")
        self.assertEqual(analysis.slider_label({"semantic_state": "SIGNED_VALUE", "semantic_value": 50}), "SIGNED_VALUE_+50")

    def test_generated_summary_boundaries(self):
        path = analysis.OUT / "mission-destination-eligibility-summary.json"
        summary = json.loads(path.read_text(encoding="utf-8"))
        self.assertEqual(summary["populations"]["RAW_BACKED_EXACT_DESTINATION"], 92830)
        self.assertEqual(summary["populations"]["NO_RAW_DESTINATION_UNRESOLVED"], 355)
        self.assertEqual(summary["populations"]["LEVEL2_CONTROLLED_SLIDER_CORPUS"], 270)
        self.assertEqual(summary["universe_coverage"]["total_client_placements"], 2242)
        self.assertEqual(summary["LIVE_MISSION_CAPTURE_PERFORMED"], "NO")
        self.assertEqual(summary["RUNTIME_MISSION_LOGIC_CHANGED"], "NO")
        self.assertEqual(summary["DESTINATION_SELECTION_IMPLEMENTED"], "NO")
        self.assertEqual(summary["DESTINATION_PROBABILITIES_INFERRED"], "NO")

    def test_ql_matrix_uses_three_state_evidence_classification(self):
        path = analysis.OUT / "destination-ql-evidence-matrix.jsonl.gz"
        counts = Counter()
        with gzip.open(path, "rt", encoding="utf-8") as stream:
            for line in stream:
                counts[json.loads(line)["classification"]] += 1
        self.assertEqual(sum(counts.values()), 2242 * 250)
        self.assertEqual(set(counts), {"OBSERVED", "NOT_YET_OBSERVED", "NO_CAPTURE_COVERAGE"})

    def test_unresolved_offers_are_not_assigned_destinations(self):
        path = analysis.OUT / "mission-offer-analysis-inventory.jsonl.gz"
        populations = Counter()
        with gzip.open(path, "rt", encoding="utf-8") as stream:
            for line in stream:
                row = json.loads(line)
                populations[row["population"]] += 1
                if row["population"] == "NO_RAW_DESTINATION_UNRESOLVED":
                    self.assertIsNone(row["destination_identity"])
        self.assertEqual(populations["RAW_BACKED_EXACT_DESTINATION"], 92830)
        self.assertEqual(populations["NO_RAW_DESTINATION_UNRESOLVED"], 355)


def run_tests():
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(MissionDestinationEligibilityTests)
    result = unittest.TextTestRunner().run(suite)
    if not result.wasSuccessful():
        raise SystemExit(1)
    print(f"MISSION_DESTINATION_ELIGIBILITY_TESTS=PASS ({result.testsRun} tests)")
