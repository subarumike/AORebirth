# AORebirth bug investigation worker

Run `python -B Tools/bug_investigation/worker.py --db <private-queue> run --api-url <https-feed> --token-file <private-bearer-file> --poll-seconds 60` after the website feed is deployed. Omit `--poll-seconds` for a single sweep. Use `show <ID>` or `requeue <ID>` with the same `--db`. Repeat `--evidence-dir` for approved local evidence folders.

For the first real report, use `--report-id <website-ID>` in place of `--poll-seconds 60`. This selects the authenticated detail route and limits investigation and optional draft repair to that ID. Rerunning the same ID preserves an unchanged completed result.

The worker splits distinct symptoms and checkpoints each against the pinned source commit. Codex can search and page through approved tracked source and evidence using the worker's read-only source tool. It cannot run commands, open attachments, read credentials, or write source during investigation. `show <ID>` lists the trace, reproduction reasoning, outstanding evidence, and repair progress for each symptom. A source investigation that needs more reads gets up to three passes and remains `SOURCE_INVESTIGATION_INCOMPLETE` if that budget is exhausted. Use `requeue <ID> --symptom-id <N>` to revisit only that symptom.

`--symptom-workers 3` lets Codex investigate up to three symptoms of the same report concurrently. Reports and repair attempts still run one at a time.

Add `--repair-plan <private-json>` to enable draft repairs. Keep the plan, queue, and token in an operator-private directory outside the source and evidence trees.

```json
{
  "mode": "aorebirth",
  "repository": "subarumike/AORebirth",
  "base": "master",
  "allowed_files": ["AORebirth/Server/ZoneEngine_New/<approved-source>.cs"],
  "build_project": "AORebirth/Server/ZoneEngine_New/ZoneEngine_New.csproj",
  "test_project": "AORebirth/Server/ZoneEngine_New.Tests/ZoneEngine_New.Tests.csproj",
  "test_filter": "FullyQualifiedName~ApprovedFocusedTests",
  "validation_timeout": 300,
  "validation_runner": ["C:/private/approved-isolated-validation.exe"]
}
```

Set exact paths and a focused test filter for the authorized scope. The worker supplies the documented Windows feature-validation command as argv with the attempt worktree as the current directory. The trusted runner must enforce isolation from production networks and credentials, use the owning worktree’s private validation profile, propagate the wrapper exit code, and stop its descendants on timeout. Git and `gh` need separate authorization to push and create drafts.

For the ZoneEngine test project, optionally set `test_scope` to `NpcAiFocusedTests` or `MissionDestinationFocusedTests` to use the existing focused compilation target as well as the test filter.

Repair attempts operate on existing approved C# files. Optional `report_id` and `symptom_id` plan fields narrow the scope further. A diagnosis informed by private evidence requires an independent public-source review before the worker can attempt a repair. A validated symptom can produce its own draft while other symptoms remain open. The worker retains attempts, validation results, and draft links in SQLite. An interrupted publication can reconcile the exact commit and draft on the next sweep.

Fixture mode requires a loopback synthetic feed and a separate GitHub repository whose name ends in `-Fixture`. Use `mode: fixture`, `fixture_test_arg`, and `fixture_failure_marker` in the plan. The fixture build and regression must reproduce the expected baseline failure and pass with the proposed edit.
