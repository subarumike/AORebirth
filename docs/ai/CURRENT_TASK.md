# Current Task

Connected position persistence acceptance complete on `codex/delmus-npc-content-001`.
Input: `29fde403`; control runtime: `cb12160c`.
The height mismatch is a proven test expectation defect: the motor simulates
ground-stick movement after the requested landing. The fixture now verifies the
runtime snapshot against stored float bits, DAO reload and reconnect separately.
Tested source: `ead455fd047d28f7835174dbce9261f0632d6079`.
Corrected control and candidate connected lifecycles, exact-source Windows,
all mandatory gates, Linux publication, and disposable/DAO validation PASS.
No active implementation work remains for this focused acceptance task.
Runtime movement code, NPC data, production, master and Delmus's branch are unchanged.
Legacy retirement and gameplay completeness are not acceptance prerequisites.
See `docs/reports/NEWENGINE_CONNECTED_POSITION_PERSISTENCE_RECONCILIATION.md`.
