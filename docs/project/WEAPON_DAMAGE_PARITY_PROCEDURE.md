# Weapon Damage Parity Procedure

This is the evidence-only process for ordinary weapon-hit parity. It does not change production damage.

Primary artifacts:

- Schema: `docs/project/damage-evidence/schema/weapon-damage-observation.schema.json`
- Operator matrix: `docs/project/damage-evidence/procedures/operator-observation-matrix.md`
- Synthetic controls: `docs/project/damage-evidence/observations/repository-synthetic-fixtures.json`
- Captured fixed Thief record: `docs/project/damage-evidence/observations/fixed-captured-thief.json`
- Live template: `docs/project/damage-evidence/observations/live-observation-template.json`
- Initial report: `docs/project/damage-evidence/reports/initial-parity-report.md`

Validation rules:

- Reject observations when health before/after does not reconcile to observed damage.
- Mark observations incomplete when weapon identity, mapped damage type, matching armor, Add All Off, AMSCap semantics, packet order, critical-state evidence, or single-source evidence is missing.
- Do not default missing armor, Add All Off, add damage, or AMSCap to zero.
- Treat possible reflect, absorb, shield, proc, nano, DoT, or environmental damage as an ambiguity until ruled out.
- Keep known critical hits incomplete unless the capture proves critical state independently.

Candidate evaluation is report-only. `WeaponDamageCandidateEvaluator` and `WeaponDamageParityReporter` compare complete observations against candidate formula orderings and report exact matches, multiple matches, no matches, rounding boundaries, and possible hidden modifiers. They do not call production damage paths and do not consume random numbers.

The diagnostic seam is opt-in through `WeaponDamageDiagnosticSnapshotBuilder`. It returns `null` when disabled and only records request-builder, production-result, and candidate-evaluation data when explicitly enabled by test or future diagnostic tooling.

Promotion requires exactly one candidate matching every complete observation, no contradictory observations, no unresolved required matrix rows, and an explicit implementation task authorizing production formula activation.
