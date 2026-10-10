# Current Task

Repair mission-roll destination rejection on codex/mission-destination-integration,
starting at 30ab16d0de180d48f2323ede4811602b0e21ab7c.

PROVEN: the baseline rejects an exact captured centered Borealis request
(level35, expectedQL35, difficulty6, Omni, breed3, profession12,
terminal0xDAC1:0xC0000320, secondary bytes255). The existing type selector
requires a Find Item destination bucket absent from every centered observation
population. Prior tests did not exercise centered requests or handler charging.

PROVEN: full-condition equality also rejects other character/terminal metadata
without evidence that those dimensions restrict retail destinations. The
original capture analysis distinguishes positive observations from unknown
eligibility; missing combinations do not prove exclusion.

Mike explicitly approved provisional expected-QL-only reuse: union exact
entrances observed at that QL, retain all other metadata as research provenance,
allow repeats, use no observation-frequency weighting, and fail without charging
when the QL has no observations. Cross-condition retail eligibility stays
UNPROVEN. No nearest-QL or unobserved-placement fallback is permitted.

The minimal catalog/generator repair and request/candidate diagnostics are
implemented. Approved build PASS; destination/fee/acceptance/QL regressions
PASS61/61; real disposable DAO identity checks PASS74 and full DAO checks PASS275.
The full DAO run used the identity route's existing compiler override because
the default wrapper's C#7.3 setting fails on current nullable source. Exact
identity, WorldPos, interaction range, keys, dungeon and existing persistence
remain unchanged. No schema or DAO edits.

OBSERVED: after rebuilding local runtime repair 73fc97a75, Mike confirmed five
offers for Shade at Borealis. The request was level 25 / QL25 / difficulty 6,
Neutral / breed 2 / profession 15, terminal 0xDAC1:0xC0020320, six raw bytes of 0.
The QL pool contained 124 destinations. Read-only persistence verification proves
five exact destination records and a 25-credit charge. Client rolling is verified;
manual mission acceptance/entry/completion remains unverified. Cross-condition
retail eligibility remains UNPROVEN.

Repair report: docs/evidence/MISSION_ROLL_ELIGIBILITY_REPAIR.md.

Original implementation history: docs/evidence/MISSION_DESTINATION_INTEGRATION.md.
This task does not extend objective/reward behavior or perform Linux deployment.
