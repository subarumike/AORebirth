# First Ordinary Weapon-Hit Evidence Campaign

Outcome: **B — capture prepared, operator action required**.

No live observations have been collected in this repository state. No production formula is activated, and production damage remains legacy/fixed.

## Scope

This first campaign is limited to ordinary, normal, noncritical, single-hit weapon attacks below 1000 attack rating.

Excluded from this campaign: critical hits, Add All Off ordering, type-specific and universal add damage, post-1000 AR scaling, AMSCap behavior, special attacks, PvP, reflect, absorbs, damage shields, nanos, perks, procs, DoTs, and environmental damage.

## Selected fixtures

Primary weapon: QL1 Solar-Powered Pistol `121567`.

- Provenance: `items.dat`, starter loadout usage, and captured Subway Thief contract.
- Min/max: `2..18`.
- Legacy `DamageBonus`: `18`.
- Raw damage type: `90` / Projectile.
- Attack skill: `112=100`.
- AMSCap: absent in audited template; not part of this campaign.

Optional confirmation weapon: QL1 Worn Oak Bo `121565`.

- Provenance: `items.dat` and `itemnames`.
- Min/max: `6..24`.
- Legacy `DamageBonus`: `18`.
- Raw damage type: `91` / Melee.
- Attack skill: `100=100`.
- AMSCap: absent in audited template; not part of this campaign.

Target fixture: isolated `Malfunctioning Cleaning Robot` in the private-server low-level area. Acceptance depends on diagnostic proof, not the name alone.

## Normal-hit proof method

The current private-server proof method is server-side: diagnostic rows are accepted only when `AttackInfoHitType` equals `NormalAttackInfoHitType` and `hitKind` is `KnownNormal`. Low observed damage is not used as normal-hit proof.

Current blocker for stronger live parity: there is no independent live-client critical flag in this campaign tooling.

## Diagnostic capture

Runtime diagnostic output is disabled by default. It activates only when `AO_REBIRTH_WEAPON_DAMAGE_EVIDENCE_SESSION` is set for the engine process.

When enabled, player equipped-weapon auto-attacks write JSONL rows under:

```text
.local\weapon-damage-evidence\<SESSION_ID>\raw\server-weapon-damage-events.jsonl
```

The row records attacker identity, target identity, weapon template, min/max, raw damage type, attack skill definitions, attacker skill values, effective attack rating, Add All Off value, target matching AC, selected production strategy, base roll, observed damage, health before/after, normal-hit classification, and raw evidence reference.

## Session commands

```cmd
cmd /d /c tools\weapon_damage_evidence.cmd prepare --session-id first-normal-hit-001
cmd /d /c .local\weapon-damage-evidence\first-normal-hit-001\commands\start-session-engines.cmd
cmd /d /c tools\weapon_damage_evidence.cmd status --session-id first-normal-hit-001
cmd /d /c tools\weapon_damage_evidence.cmd finish --session-id first-normal-hit-001
cmd /d /c .local\weapon-damage-evidence\first-normal-hit-001\commands\disable-session-engines.cmd
cmd /d /c tools\weapon_damage_evidence.cmd analyze --session-id first-normal-hit-001
```

## Operator procedure

Codex preparation:

1. Prepare the session and start engines through the generated session command.
2. Confirm status shows the requested session id.

Mike client-side actions:

1. Use one low-level private-server character with no buffs, damage modifiers, reflect, absorb, damage shield, proc, nano damage, DoT, pet, or outside attacker.
2. Equip only QL1 Solar-Powered Pistol `121567` in the right hand.
3. Go to one isolated `Malfunctioning Cleaning Robot`.
4. Stand so no other character, NPC, or pet can hit the same target.
5. Use ordinary auto attack only. Do not use specials, perks, nanos, or item actions.
6. Collect 12 ordinary hit rows or stop when the target dies.
7. Stop immediately if a critical, heal, regeneration, reflect, absorb, shield, proc, DoT, environmental hit, second attacker, target switch, or wrong weapon appears.
8. Do not interpret packets manually; report only visible anomalies.

Codex import and analysis:

1. Finish the session.
2. Disable diagnostics by restarting engines without the session environment.
3. Run analyze.
4. Promote only sanitized valid observations into `docs/project/damage-evidence/observations/` in a later task if Mike approves.

## Observation matrix

| Matrix | Fixture | Weapon | Attacker stats | Target AC | Uncontrolled variable | Minimum observations | Required logs | Candidate formulas distinguished | Acceptance criteria |
| --- | --- | --- | --- | --- | --- | ---: | --- | --- | --- |
| A | same character and target | `121567` | same weapon skill below 1000, no buffs | same diagnostic `targetMatchingArmor` | base roll | 12 | JSONL diagnostics and ZoneEngine reference | base roll range, hidden fixed modifiers | all rows valid and health-continuous |
| B | same target | `121567` | one controlled below-1000 weapon-skill value change | same AC | base roll | 6 per AR value | `attackSkillValues`, `effectiveAttackRating` | AR-A, AR-B, AR-C | AR changes while other fields remain stable |
| C | same character | `121567` | same AR | two diagnostic AC values | base roll | 6 per target AC | `targetMatchingArmor` | AC-A, AC-B, AC-C, AC-D | target AC changes without overlap |
| D | high-AC target if available | `121567` | same AR | high enough to force floor candidates | base roll | 8 | health before/after and base roll | floor before/after AC | repeated floor-near rows |
| E | boundary values | `121567` | AR values near division boundaries | same AC | base roll | 6 | base roll and effective AR | truncation stage | rows near candidate divergence |

## Current result

Actual observations collected: none.

Current valid observation count: `0`.

Current incomplete observation count: `0`.

Current rejected observation count: `0`.

Candidate analysis is pending operator evidence.

## Next distinguishing observation

The smallest next observation is Matrix A: 12 isolated normal auto-attack hits with QL1 Solar-Powered Pistol `121567` against one isolated `Malfunctioning Cleaning Robot`, with diagnostics enabled and no target health discontinuity.
