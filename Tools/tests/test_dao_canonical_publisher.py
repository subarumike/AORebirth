import hashlib
import io
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import dao_canonical_publisher as publisher


BASE, CANDIDATE, UPSTREAM, TREE, WORKFLOW = (char * 40 for char in "abcde")
PROFILE = {"PRIVATE_CONTENT_REPOSITORY": "private/content", "PRIVATE_CONTENT_BRANCH": "main",
           "DAO_TRUSTED_VALIDATOR": {"REPOSITORY": "private/validator", "WORKFLOW_ID": 11,
                                     "WORKFLOW_SOURCE_SHA": WORKFLOW, "APP_ID": 12, "MASTER_RULESET_ID": 13}}
RUN = {"id": 14, "workflow_id": 11, "head_sha": WORKFLOW, "event": "workflow_dispatch", "status": "completed",
       "conclusion": "success", "repository": {"full_name": "private/validator"},
       "head_repository": {"full_name": "private/validator"}, "run_attempt": 2}


def evidence(**overrides):
    manifest = [{"path": "Tradeskills.json", "size": 50, "sha256": "1" * 64, "blob": "2" * 40}]
    receipt = {"FORMAT_VERSION": 1, "SOURCE_SHA": CANDIDATE, "BASE_SHA": BASE, "CONTENT_DUMP_SHA": UPSTREAM,
               "CONTENT_DUMP_TREE_SHA": TREE, "RUN_ID": 14, "RUN_ATTEMPT": 2,
               "GAMEDATA_MANIFEST_SHA256": publisher.validate_manifest(manifest),
               "CHECKS": {step: "PASS" for step in publisher.REQUIRED_STEPS}}
    receipt.update(overrides)
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w") as archive:
        archive.writestr("manifest.json", json.dumps(manifest))
        archive.writestr("receipt.json", json.dumps(receipt))
    data = stream.getvalue()
    artifact = {"id": 15, "name": "dao-canonical-evidence-2", "expired": False, "size_in_bytes": len(data),
                "digest": "sha256:" + hashlib.sha256(data).hexdigest(), "workflow_run": {"id": 14}}
    return data, artifact


class CanonicalPublisherTests(unittest.TestCase):
    def fake(self, *, run=None, steps=None, archive=None, app=12, upstream_moves=False):
        if steps is None:
            steps = [{"name": step, "status": "completed", "conclusion": "success"}
                     for step in sorted(publisher.REQUIRED_STEPS)]
        data, artifact = archive or evidence()
        self.writes = []
        private_reads = 0

        def api(endpoint, **args):
            nonlocal private_reads
            if endpoint.endswith("/branches/master") and "/rules/" not in endpoint:
                return {"commit": {"sha": BASE}}
            if "/compare/" in endpoint:
                return {"merge_base_commit": {"sha": BASE}, "status": "ahead"}
            if endpoint == "repos/private/content/commits/main":
                private_reads += 1
                return {"sha": "9" * 40 if upstream_moves and private_reads > 1 else UPSTREAM,
                        "commit": {"tree": {"sha": TREE}}}
            if endpoint == "repos/private/validator":
                return {"private": True}
            if endpoint.endswith("/rulesets/13"):
                return {"enforcement": "active", "bypass_actors": []}
            if endpoint.endswith("/rules/branches/master"):
                return [{"ruleset_id": 13, "type": kind} for kind in ("deletion", "non_fast_forward", "pull_request")] + [
                    {"ruleset_id": 13, "type": "required_status_checks", "parameters": {"strict_required_status_checks_policy": True,
                     "required_status_checks": [{"context": "dao-runtime-structure", "integration_id": 15368},
                                                {"context": publisher.CHECK, "integration_id": 12}]}}]
            if "?check_name=dao-runtime-structure" in endpoint:
                return {"total_count": 1, "check_runs": [{"app": {"id": 15368}, "head_sha": CANDIDATE,
                                                          "status": "completed", "conclusion": "success"}]}
            if endpoint.endswith("/runs/14"):
                return run or RUN
            if "/attempts/2/jobs?" in endpoint:
                return {"total_count": 1, "jobs": [{"name": publisher.CHECK, "conclusion": "success", "steps": steps}]}
            if "/runs/14/artifacts?" in endpoint:
                return {"total_count": 1, "artifacts": [artifact]}
            if endpoint.endswith("/artifacts/15/zip"):
                return data
            if args.get("method") in ("POST", "PATCH"):
                self.writes.append(args["method"])
                return {"id": 16, "head_sha": CANDIDATE, "name": publisher.CHECK, "app": {"id": app}, **args["payload"]}
            self.fail(f"Unexpected endpoint {endpoint}")

        return patch.object(publisher, "api", side_effect=api)

    def test_verified_evidence_publishes_only_after_app_identity(self):
        with self.fake():
            publisher.publish(PROFILE, CANDIDATE, 14, "ghs_fixture")
        self.assertEqual(self.writes, ["POST", "PATCH"])

    def test_inconclusive_or_wrong_workflow_never_writes(self):
        for field, value in (("conclusion", "skipped"), ("head_sha", "f" * 40), ("event", "pull_request")):
            with self.subTest(field=field), self.fake(run={**RUN, field: value}):
                with self.assertRaisesRegex(ValueError, "pinned workflow"):
                    publisher.publish(PROFILE, CANDIDATE, 14, "ghs_fixture")
                self.assertEqual(self.writes, [])

    def test_skipped_required_step_never_writes(self):
        steps = [{"name": step, "status": "completed", "conclusion": "skipped"} for step in publisher.REQUIRED_STEPS]
        with self.fake(steps=steps), self.assertRaisesRegex(ValueError, "skipped"):
            publisher.publish(PROFILE, CANDIDATE, 14, "ghs_fixture")
        self.assertEqual(self.writes, [])

    def test_wrong_source_or_manifest_never_writes(self):
        for overrides in ({"SOURCE_SHA": BASE}, {"GAMEDATA_MANIFEST_SHA256": "9" * 64}, {"RUN_ATTEMPT": 1}):
            with self.subTest(overrides=overrides), self.fake(archive=evidence(**overrides)):
                with self.assertRaisesRegex(ValueError, "(?i)receipt"):
                    publisher.publish(PROFILE, CANDIDATE, 14, "ghs_fixture")
                self.assertEqual(self.writes, [])

    def test_wrong_app_or_moved_authority_cannot_publish_success(self):
        for settings in ({"app": 99}, {"upstream_moves": True}):
            with self.subTest(settings=settings), self.fake(**settings), self.assertRaises(ValueError):
                publisher.publish(PROFILE, CANDIDATE, 14, "ghs_fixture")
            self.assertEqual(self.writes, ["POST"])

    def test_missing_trust_binding_or_token_never_uses_api(self):
        with patch.object(publisher, "api") as request:
            with self.assertRaisesRegex(ValueError, "workflow and App binding"):
                publisher.publish({}, CANDIDATE, 14, "ghs_fixture")
            with self.assertRaisesRegex(ValueError, "installation token"):
                publisher.publish(PROFILE, CANDIDATE, 14, None)
            request.assert_not_called()


if __name__ == "__main__":
    unittest.main()
