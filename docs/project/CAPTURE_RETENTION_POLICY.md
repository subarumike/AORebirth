# Capture Retention and Authority Policy

## Authority model

`RAW_CAPTURES_ARE_RUNTIME_AUTHORITY=NO`

`ACCEPTED_CODE_AND_TESTS_ARE_CURRENT_MECHANIC_AUTHORITY=YES`

`NEW_CAPTURE_WORK_REQUIRES_EVIDENCE_UNTIL_ACCEPTED=YES`

Historical raw captures are development evidence. Once behavior is accepted,
the accepted source, committed generated data, regression tests, and acceptance
evidence become the current implementation authority. Raw captures are not a
normal runtime, build, Windows acceptance, or Linux build dependency.

## Lifecycle states

`ACTIVE_EVIDENCE`: raw capture is required for current research, replay, or
capture-backed generation and must be retained until that work is accepted.

`ACCEPTED_DIGESTED`: behavior has been promoted into accepted source and
generated artifacts with tests and acceptance evidence. Raw replay may still be
available, but normal checks do not require it.

`ARCHIVABLE`: raw capture is no longer required by normal development or
acceptance because its required behavior is durably represented in accepted
artifacts. Retain it when practical for provenance or future investigation.

`HISTORICAL_OPTIONAL`: deletion does not invalidate the accepted mechanics.
Strict historical replay must report missing evidence rather than silently
substitute accepted artifacts.

PF 4582 remains `ACTIVE_EVIDENCE` for any new behavior not yet accepted. The
existing accepted combat cohort is `ACCEPTED_DIGESTED`; its deleted historical
raw inputs affect strict replay only.

## Command policy

`--check` validates committed accepted generated-artifact integrity without
reading raw captures.

`--write` is raw-dependent generation and promotion.

`--validate-current` is strict historical replay/reproducibility and may fail
closed with `FAIL_MISSING_RAW`.

Scoped audits for new capture-backed content remain strict and must not promote
behavior from incomplete or missing evidence.
