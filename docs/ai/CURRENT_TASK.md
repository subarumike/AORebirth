# Current Task

Checkpoint `codex/mission-typed-content` and run complete required Windows
acceptance against the committed SHA with isolated normalized mission GameData.
The reviewed implementation and connected matrix passed before this checkpoint;
no candidate-specific defect was found. Preserve that implementation unchanged.

Include only scoped source, normalized public mission GameData, regression and
acceptance fixtures, and associated documentation. Mike explicitly approved
`Tests/Fixtures/Gameplay/Missions/HistoricalLayouts.json`, a byte-for-byte copy
of the original public Layouts.json, as an intentional test-only regression
fixture. No external capture corpus, private GameData or machine artifacts belong
in this checkpoint.

Keep the two NewEngine, two AOtomation and connected nano-expiry baseline
failures separate. Do not repair unrelated production code or relax assertions.
Push this branch only after exact-SHA validation succeeds under the authorized
baseline distinction. Do not merge, deploy, change Linux or modify private data.
Acceptance receipts remain under ignored `build-verify/checkpoint-acceptance` so
the tested source stays clean. Historical reports describe earlier phases.
