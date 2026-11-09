# AORebirth bug investigation worker

Run `python -B Tools/bug_investigation/worker.py --db <private-queue> run --api-url <https-feed> --token-file <private-bearer-file> --poll-seconds 60` after the website feed is deployed. Omit `--poll-seconds` for a single sweep. Use `show <ID>` or `requeue <ID>` with the same `--db`. Repeat `--evidence-dir` for approved local evidence folders.

For the first real report, use `--report-id <website-ID>` in place of `--poll-seconds 60`. This selects the authenticated detail route and limits investigation and optional draft repair to that ID. Rerunning the same ID preserves an unchanged completed result.

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

Repair attempts operate on existing approved C# files and publish only when the diagnosis is supported by public source. Reports that depend on private evidence remain available for local review. The worker retains attempts, validation results, and draft links in SQLite. An interrupted publication can reconcile the exact commit and draft on the next sweep.

Fixture mode requires a loopback synthetic feed and a separate GitHub repository whose name ends in `-Fixture`. Use `mode: fixture`, `fixture_test_arg`, and `fixture_failure_marker` in the plan. The fixture build and regression must reproduce the expected baseline failure and pass with the proposed edit.
