import math
from pathlib import Path
import sys
import unittest


sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from audit_rk_dyna_loot_weights import Choice, draw_exposure, predictive_folds


class AuditDynaLootWeightsTests(unittest.TestCase):
    def test_repeated_roll_incidence_uses_conditional_shared_ql(self):
        mass, expected, incidence = draw_exposure((("gear", "fixed"), ("fixed",)),
                                                  {"gear": 3, "fixed": 1}, copies=2)
        self.assertEqual(dict(mass), {"gear": 1.0, "fixed": 2.0})
        self.assertEqual(dict(expected), {"gear": 0.75, "fixed": 1.25})
        self.assertEqual(dict(incidence), {"gear": 0.46875, "fixed": 0.71875})

    def test_playfield_holdout_keeps_test_count_out_of_the_fit(self):
        choices = [Choice(("test",), "A", 40, "ROOT", 40, 40, "unseen")]
        choices += [Choice((index,), "B", 40, "ROOT", 40, 40, "seen") for index in range(3)]

        def risks(root, center, desired):
            return (("seen", "unseen"),)

        results = predictive_folds(choices, risks, {"ROOT"}, "neutral")
        fold = next(row for row in results if row["Playfield"] == "A")
        self.assertEqual(fold["SeenInTrainingSlots"], 0)
        self.assertAlmostEqual(fold["LogLoss"]["TrainingCount"], math.log(4))
        self.assertAlmostEqual(fold["LogLoss"]["Candidate"], math.log(2))

        results = predictive_folds(choices, risks, {"ROOT"}, "count-plus-one")
        fold = next(row for row in results if row["Playfield"] == "A")
        self.assertAlmostEqual(fold["LogLoss"]["AddOnePrior"], math.log(5))
        self.assertAlmostEqual(fold["LogLoss"]["Candidate"], math.log(3))


if __name__ == "__main__":
    unittest.main()
