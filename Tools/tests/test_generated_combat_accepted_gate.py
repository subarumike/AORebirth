import sys
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import generated_combat_pipeline as pipeline


class AcceptedCombatGateTests(unittest.TestCase):
    def test_check_validates_committed_cohort_without_raw_inputs(self):
        repo_root = Path(__file__).resolve().parents[2]
        with mock.patch.object(
            pipeline,
            "validate_cohort",
            return_value={"generationIdentity": "test"},
        ) as validate, mock.patch.object(
            pipeline,
            "capture_auxiliary_inputs",
            side_effect=AssertionError("accepted check must not read raw inputs"),
        ):
            result = pipeline.run_pipeline(
                repo_root=repo_root, mode="check", max_rounds=8
            )

        self.assertEqual(result, 0)
        validate.assert_called_once_with(repo_root.resolve(), verify_toolchain=False)


if __name__ == "__main__":
    unittest.main()
