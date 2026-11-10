# AORebirth bug investigation worker

Run `python -B Tools/bug_investigation/worker.py --db <private-queue> run --api-url <https-feed> --token-file <private-bearer-file> --poll-seconds 60` after the website feed is deployed. Omit `--poll-seconds` for a single sweep. Use `show <ID>` or `requeue <ID>` with the same `--db`. Repeat `--evidence-dir` for approved local evidence folders.

For the first real report, use `--report-id <website-ID>` in place of `--poll-seconds 60`. This selects the authenticated detail route and limits investigation and optional draft repair to that ID. Rerunning the same ID preserves an unchanged completed result.

Use `--next-new 3` for a one-shot batch of up to three open reports that have never been queued. An interrupted report remains in SQLite and can be resumed with `--report-id` or the regular worker. This batch limits optional repair to its own reports.

The worker splits distinct symptoms and checkpoints each against the pinned source commit. Codex can search approved tracked source and local evidence without shell access. Before escalating, it must search every approved evidence root. The worker discovers checkout GameData and Captures and a local `RDBEvidence` or `RDB` directory when present. Use `--evidence-dir` to point to other approved folders of decoded text evidence. Binary RDB shards and raw sensitive captures are not passed to Codex. Searches distinguish missing or bounded sources from a negative finding.

`show <ID>` lists the effective diagnosis and original checkpoint. A source investigation gets up to three passes. Record a completed operator review with `record-followup <ID> --symptom-id <N> --source-commit <PIN> --classification <RESULT> --finding <TEXT> --evidence <REFERENCE> --next-action <TEXT>`. It updates that symptom's effective classification without granting automatic repair eligibility or modifying the website. Use `requeue <ID> --symptom-id <N>` to revisit only that symptom with its history.

`--symptom-workers 3` lets Codex investigate up to three symptoms of the same report concurrently. Reports and repair attempts still run one at a time.

For product repairs, add `--repair-plan <private-json> --report-id <ID> --supervised-validation` to stage a candidate without executing it. Keep the plan, queue, and token in an operator-private directory outside the source and evidence trees.

```json
{
  "mode": "aorebirth",
  "repository": "subarumike/AORebirth",
  "base": "master",
  "allowed_files": ["AORebirth/Server/ZoneEngine_New/<approved-source>.cs"],
  "build_project": "AORebirth/Server/ZoneEngine_New/ZoneEngine_New.csproj",
  "test_project": "AORebirth/Server/ZoneEngine_New.Tests/ZoneEngine_New.Tests.csproj",
  "test_filter": "FullyQualifiedName~ApprovedFocusedTests",
  "validation_timeout": 300
}
```

Review the staged diff at the path printed by the worker. In an interactive Windows terminal, rerun the same command with `--approve-tree <printed-tree>` and type the requested confirmation. The worker rechecks the candidate and runs the existing Windows feature-validation wrapper with the attempt's own private development profile. Approval covers the displayed build and focused test and applies only to that execution. This mode runs with the operator's local rights; it does not isolate files or networks. Use a development session without production access. Unattended product validation is disabled. Git and `gh` need separate authorization to push and create drafts.

For the ZoneEngine test project, optionally set `test_scope` to `NpcAiFocusedTests` or `MissionDestinationFocusedTests` to use the existing focused compilation target as well as the test filter.

Product validation requires a fresh VSTest result with a passing test that matches the approved filter.

On Windows, the worker runs the operator's storage preflight before each new product repair worktree and before validation. It saves bounded build and regression logs, the verified TRX, and recognized feature-validation evidence locally. After verified draft publication it removes only an eligible worker-owned worktree. Unknown files, private profiles and active Git operations cause retention with a recorded reason.

Repair attempts operate on existing approved C# files. Optional `report_id` and `symptom_id` plan fields narrow the scope further. A diagnosis informed by private evidence requires an independent public-source review before the worker can attempt a repair. A validated symptom can produce its own draft while other symptoms remain open. The worker retains attempts, validation results, and draft links in SQLite. An interrupted publication can reconcile the exact commit and draft on the next sweep.

Fixture mode requires a loopback synthetic feed and a separate GitHub repository whose name ends in `-Fixture`. Use `mode: fixture`, `fixture_test_arg`, and `fixture_failure_marker` in the plan. The fixture build and regression must reproduce the expected baseline failure and pass with the proposed edit.
