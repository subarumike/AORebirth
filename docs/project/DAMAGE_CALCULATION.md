# Damage Calculation Architecture

## Boundary

`ZoneEngine.Core.DamageCalculator` is the side-effect-free damage boundary. It accepts a `DamageCalculationRequest`, reads deterministic values through `IDamageRandomSource`, and returns a `DamageCalculationResult` with ordered `DamageCalculationTrace` stages. It does not mutate health, trigger death, spawn corpses, award loot or XP, send packets, change aggro, consume ammunition, or schedule timing.

Existing combat callers continue to enter through `CombatDamageRules.Calculate(...)`, which now delegates to the centralized calculator. This preserves the current player, NPC, pet, and captured Subway combat numbers while providing deterministic tests through `CombatDamageRules.CalculateDetailed(...)`.

## Current Outcome

Outcome B: architecture and evidence only.

The repository exposes some weapon input contracts, but it does not prove the AO ordinary weapon formula ordering or caller completeness needed to activate AR, AC, critical, or add-damage math in production. The calculator therefore keeps production on legacy/fixed strategies and records evidence-blocked formula readiness when the candidate weapon formula policy is requested.

## Current Production Formula

Evidence class: `PROVEN_REPOSITORY_BEHAVIOR`.

Current migrated normal-hit behavior is exactly the legacy `CombatDamageRules` behavior:

1. `normalizedMinDamage = max(0, minDamage)`
2. `normalizedMaxDamage = max(normalizedMinDamage, maxDamage)`
3. `normalizedDamageBonus = max(0, damageBonus)`
4. `fallbackDamage = 15` for players, `1` for NPCs and pets
5. if `normalizedMaxDamage > 0`, roll inclusive integer damage from `normalizedMinDamage..normalizedMaxDamage`
6. otherwise use attacker level
7. final damage is `max(fallbackDamage, baseDamage + normalizedDamageBonus)`

Rounding is integer-only. The current migrated formula has no floating point. Negative minimum, maximum, and damage bonus values are clamped before calculation. The random roll is inclusive at both ends.

## Weapon Formula Readiness

The active audit found these local input contracts:

- weapon minimum damage: item or NPC stat `StatIds.mindamage` / stat `286`
- weapon maximum damage: item or NPC stat `StatIds.maxdamage` / stat `285`
- legacy damage bonus: item or NPC stat `StatIds.damagebonus` / stat `284`
- weapon attack range and timing: item stats `attackrange`, `itemdelay`, and `rechargedelay`
- possible attack-rating stats: `AMS` / stat `22`, `AMSModifier` / stat `276`, `AMSCap` / stat `538`, and `NPCScriptAMSScale` / stat `581`
- critical-related stats: `CriticalIncrease` / stat `379`, `CriticalDecrease` / stat `391`
- type-specific add-damage stats: Projectile `278`, Melee `279`, Energy `280`, Chemical `281`, Radiation `282`, Cold `311`, Nano `315`, Fire `316`, Poison `317`

The following were not proven sufficiently for production formula activation:

- which attacker skill or weighted skill set defines effective AR for each weapon
- whether `DamageBonus` is AO flat add damage or only a legacy server field
- whether `AMSCap` is weapon cap data for active weapons and what zero means
- critical-hit state resolution for current callers
- critical bonus field semantics and scaling
- AR scaling numerator, denominator, truncation point, and post-1000 behavior
- AC divisor and whether AC applies before the minimum floor
- add-damage ordering and eligibility

`DamageCalculationPolicy.EvidenceBackedWeaponFormula(...)` is intentionally evidence-blocked. It validates the required request shape and traces the missing input, but still returns the preserved legacy result rather than enabling partial formula math.

## Strategy Selection

The calculator now records a strategy for every result:

- `FixedCapturedDamage`: selected when the policy or definition is fixed captured damage; AR, AC, critical, and add-damage formulas are bypassed.
- `LegacyFallback`: selected for current production requests because the AO weapon formula remains incomplete.
- `EvidenceBlocked`: selected when a caller explicitly requests the candidate formula policy but required evidence or inputs are missing.
- `EvidenceBackedWeaponDamage`: reserved for a future activation when the complete formula and caller inputs are proven.

Formula-backed ordinary weapon requests require weapon min/max, damage type, effective AR or weighted attack-skill contributions, explicit target matching AC, resolved critical state, and critical bonus when critical. Missing values must not be silently treated as zero.

## Fixed Captured Damage

Captured fixed damage is represented through `DamageCalculationPolicy.CapturedFixedDamage(...)` and `DamageDefinition.FixedDamage`. This is used for fixed-contract behavior such as the Subway Thief's captured `9` damage and bypasses unproven attack-rating and armor formulas.

The current runtime still supplies that fixed value through the existing captured combat contract path. The centralized calculator now has an explicit fixed-damage policy and regression tests proving that fixed `9` damage remains fixed even when high armor and attack rating inputs are present.

## Stage Order

The calculator records this explicit order:

1. Validate request
2. Resolve mode and policy
3. Resolve damage type
4. Resolve immunity or invulnerability
5. Resolve hit outcome
6. Resolve critical
7. Resolve effective attack rating
8. Apply attack-rating cap
9. Apply pre-1000 attack-rating scaling
10. Apply post-1000 attack-rating scaling
11. Roll or select base damage
12. Apply critical contribution
13. Apply armor mitigation
14. Apply flat damage modifiers
15. Apply minimum-damage floor
16. Resolve special sub-hits
17. Aggregate sub-hits
18. Apply special compression
19. Apply attack-specific cap
20. Apply PvP conversion
21. Apply PvP maximum-health cap
22. Apply reflect
23. Consume typed absorbs
24. Consume universal absorbs
25. Resolve reflected return damage
26. Resolve damage-shield return damage
27. Clamp final values
28. Return trace

This order is not claimed as original AO behavior. Stages with no repository or capture proof are trace-visible and evidence-blocked when their inputs are present.

## Implemented Mechanics

- Regular player and NPC auto-attack damage through the legacy repository formula.
- Player-owned attack pet damage where the existing NPC combat coordinator already routes pet attack sources through `CombatDamageRules`.
- Fixed captured damage representation for contracts such as Subway Thief.
- Deterministic random input for base damage rolls and future chance rolls.
- Attack-rating cap arithmetic as a traceable policy input. Scaling remains disabled because the profession/NPC/pet factors are not proven.
- Damage-type-to-AC stat mapping for proven repository stat contracts: Projectile `90`, Melee `91`, Energy `92`, Chemical `93`, Radiation `94`, Cold `95`, Poison `96`, Fire `97`, Nano `168`. Disease remains unmapped.
- Type-specific add-damage stat mapping for proven repository stat contracts. No universal add-damage stat was proven.
- Weighted attack-skill contribution modeling. It is represented with integer contribution `value * percentage / 100`, but no active caller uses it for formula damage.
- Side-effect-free trace output for unresolved armor, critical, AR scaling, special, PvP, reflect, absorb, returned damage, damage shield, percentage-health, nano, perk, and proc stages.

## Preserved Runtime Responsibilities

The runtime services still own:

- health subtraction
- death checks
- corpse lifecycle
- combat packet emission
- aggro and target state
- loot rights
- XP
- ammo and recharge
- movement and timing

No Subway Thief timing, packet order, StopFight/Death order, corpse behavior, loot, XP, heartbeat health handling, or runtime lifecycle behavior was moved into the calculator.

## Unknown Formula Policy

The following remain evidence-blocked and inactive for production formula changes:

- AO weapon AR and post-1000 AR scaling
- the exact current-client ordinary weapon formula and integer truncation points
- profession-specific AR factors
- armor mitigation ordering and integer division
- critical bonus scaling and floor participation
- add-damage eligibility by attack class
- Burst, Full Auto, Aimed Shot, Sneak Attack, Backstab, Brawl, Dimach, Sharp Objects, and martial-arts multipliers or compression
- nano direct damage, DoT, life drain, AoE, perk, and proc formulas
- PvP reduction and health-cap ordering
- reflect prevention, reflect return, reflect caps
- typed and universal absorb ordering and consumption
- damage-shield return and recursion rules

Required evidence for each unsupported mechanic is an identity-linked capture or repository/database contract that proves inputs, ordering, rounding, emitted packets, health mutation, and returned-damage behavior.
