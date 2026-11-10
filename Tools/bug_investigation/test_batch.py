import io
import sqlite3
from contextlib import redirect_stderr, redirect_stdout
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

import repair
import worker


def record(report_id, fingerprint="original"):
    return {"id": report_id}, fingerprint


class NextNewReportsTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.addCleanup(self.db.close)
        worker.initialize(self.db)
        self.args = SimpleNamespace(next_new=2, repair_plan="trusted plan", source="source")
        self.propose = Mock()
        self.enterContext(redirect_stdout(io.StringIO()))
        self.errors = self.enterContext(redirect_stderr(io.StringIO()))

    def test_skips_known_and_closed_reports_and_bounds_repairs(self):
        with self.db:
            worker.enqueue(self.db, *record(1))
            worker.enqueue(self.db, *record(2))
            self.db.execute("UPDATE jobs SET state = 'done' WHERE id = 1")
        feed = Mock()

        def pages():
            yield [record(1), record(2, "changed"), record(3), record(4)]
            yield [record(5), record(6)]
            self.fail("Batch fetched another page after reaching its limit")

        feed.pages.side_effect = pages
        feed.detail.side_effect = [None, record(4, "detail fingerprint"), record(5)]

        def prepare(args):
            args.plan = {}
            return "pinned"

        with (patch.object(repair, "prepare", side_effect=prepare),
              patch.object(worker, "investigate", return_value={"classification": "CAPTURE_REQUIRED"}) as inspect,
              patch.object(repair, "process_repairs", return_value=False) as repairs):
            self.assertFalse(worker.process_next_new(self.db, feed, self.args, self.propose))

        self.assertEqual([call.args for call in feed.detail.call_args_list], [(3,), (4,), (5,)])
        self.assertEqual([call.args[2]["id"] for call in inspect.call_args_list], [4, 5])
        self.assertEqual([call.kwargs["selected_id"] for call in repairs.call_args_list], [4, 5])
        self.assertEqual([tuple(row) for row in self.db.execute(
            "SELECT id, fingerprint, state FROM jobs ORDER BY id"
        )], [(1, "original", "done"), (2, "original", "pending"),
             (4, "detail fingerprint", "done"), (5, "original", "done")])

    def test_plan_failure_and_investigation_failure_are_independent(self):
        feed = Mock()
        feed.pages.return_value = [[record(1), record(2)]]
        feed.detail.side_effect = [record(1), record(2)]
        results = [worker.InvestigationError("Source trace failed."), {"classification": "CAPTURE_REQUIRED"}]

        with (patch.object(repair, "prepare", side_effect=repair.RepairError("Runner unavailable.")),
              patch.object(worker, "source_head", return_value="pinned"),
              patch.object(worker, "investigate", side_effect=results),
              patch.object(repair, "process_repairs") as repairs):
            self.assertTrue(worker.process_next_new(self.db, feed, self.args, self.propose))

        repairs.assert_not_called()
        self.assertEqual(self.args.repair_base, "pinned")
        self.assertIn("Repair plan unavailable. Runner unavailable.", self.errors.getvalue())
        self.assertIn("Bug 1 remains queued. Source trace failed.", self.errors.getvalue())
        self.assertEqual([tuple(row) for row in self.db.execute(
            "SELECT id, state, failures FROM jobs ORDER BY id"
        )], [(1, "pending", 1), (2, "done", 0)])

    def test_stalled_investigation_stops_automatically_retrying_after_three_failures(self):
        selected = record(1)
        with self.db:
            worker.enqueue(self.db, *selected)
        with (patch.object(worker.time, "time", return_value=100) as now,
              patch.object(worker, "investigate", side_effect=worker.InvestigationError("Stalled")) as inspect):
            for when, retry in ((100, 115), (115, 145), (145, worker.PARKED)):
                now.return_value = when
                self.assertTrue(worker.process_pending(self.db, Mock(), self.args, selected=selected))
                self.assertEqual(self.db.execute("SELECT retry_at FROM jobs").fetchone()[0], retry)
            now.return_value = 10000
            self.assertFalse(worker.process_pending(self.db, Mock(), self.args, selected=selected))
            self.assertEqual(inspect.call_count, 3)
        self.assertIn("Bug 1 parked for review.", self.errors.getvalue())


if __name__ == "__main__":
    unittest.main()
