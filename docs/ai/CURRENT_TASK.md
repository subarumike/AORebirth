# Current Task

PR31 / PR32 reconciled integration candidate (2026-10-09).

Branch: `codex/integrate-perk-aura-taunt`, starting public master
`bddea56ca2d47c5eb64ed53f6e9efd279ff4f694`.

PROVEN: all six PR31 runtime files are retained exactly. PR32 contributes only
the positive TauntNpc handler and its original three tests; its persistent
requirement-selector changes are excluded. Additional focused tests cover
target stats/state/feedback, related requirement groups, event LastRnd,
Mongo bands, player/pet taunts, Skill bonuses, auras and DoT/HoT ticks.

Approved Windows build PASS. Focused cases PASS67. Related regression run:
205 passed / 7 failed / 212 total, with the exact same seven failure names and
assertion messages reproduced on untouched starting master (152/7/159).
Full-project compilation has the same six missing-mission-type diagnostics on
both trees. No new failure was observed in the executed selection.

The Opportunity Knocks negative control on exact PR32 fails both event-roll
cases; the reconciled candidate passes both. See
`docs/evidence/PR31_PR32_INTEGRATION.md` for provenance, commands and limits.

PROVEN: periodic team/area fanout, cancellation, count exhaustion, recipient
movement/range and caster attribution passed end-to-end runtime fixtures.
UNVERIFIED: live AO client acceptance.
No database, schema, GameData or Linux changes. No master merge or deployment.
The original checkout's mission branch and dirty work remain untouched.
