# Weapon Damage Input Provenance

Outcome: **B — partial input provenance**.

This audit is evidence-only. It does not activate an AO weapon-damage formula and does not change production damage selection. Current production remains `LegacyFallback` or `FixedCapturedDamage`.

Follow-up parity work adds a schema and report-only evaluator, not formula activation. Observation schema version `1.0` records source metadata, weapon inputs, attacker stats, target stats, resolved hit type, health before/after, observed damage, packet/log references, and uncertainty annotations. The live observation template must not be populated with inferred values.

## Source model

| Input | Storage source | Database/resource source | Field/stat | Load path | Runtime owner | Lookup path | Type | Missing/default behavior | Evidence | Active caller availability |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Equipped weapon identity | inventory/equipment or captured contract | `items`, starter loadout code, captured NPC contracts | low/high/template id, QL, placement | DB inventory load or contract equip | player/NPC inventory owner | equipped slot lookup | int | missing weapon forces legacy/fixed path | `PROVEN_REPOSITORY_BEHAVIOR` / `PROVEN_CAPTURED_BEHAVIOR` | partial |
| Minimum damage | `ItemTemplate.Stats` or NPC stat | `items.dat`, character stats | `mindamage` / 286 | `ItemLoader.CacheAllItems` -> `Item.GetAttribute`; NPC `Stats` | attack-source builder | `weapon.GetAttribute(286)` or `Stats[286].Value` | signed int | `getItemAttribute` falls back to defaults; builder requires explicit provenance | `PROVEN_REPOSITORY_BEHAVIOR` | legacy callers use it |
| Maximum damage | `ItemTemplate.Stats` or NPC stat | `items.dat`, character stats | `maxdamage` / 285 | same as min | attack-source builder | `weapon.GetAttribute(285)` or `Stats[285].Value` | signed int | same as min | `PROVEN_REPOSITORY_BEHAVIOR` | legacy callers use it |
| Legacy damage bonus | item/NPC stat | `items.dat`, character stats | `damagebonus` / 284 | same as min | `CombatDamageRules` facade | `damageBonus` parameter | signed int then clamped | negative clamps to zero in legacy behavior | `PROVEN_REPOSITORY_BEHAVIOR` | active legacy only |
| Critical bonus | unresolved | none proven | none proven; `CriticalIncrease` / 379 has zero loaded template occurrences | none | none | none | unknown | missing critical bonus blocks formula request | `UNKNOWN` | unavailable |
| Damage type | `ItemTemplate.Stats` | `items.dat` | `damagetype` / 436 | `ItemLoader.CacheAllItems` -> `Item.GetAttribute` | attack-source builder/diagnostics | `weapon.GetAttribute(436)` | signed int | missing raw stat is not formula-ready | `PROVEN_REPOSITORY_BEHAVIOR` for raw field | partial |
| Attack skills | `ItemTemplate.Attack` dictionary | `items.dat` | skill stat id -> percentage | `ItemLoader.CacheAllItems` | diagnostic builder only | `template.Attack` | signed int pairs | missing dictionary blocks formula request | `PROVEN_DATABASE_CONTRACT` | not used by production damage |
| Attacker skill values | character stats | character stats table plus runtime modifiers | skill stat ids | `Stats.ReadStatsfromSql`; runtime modifiers | attacker stat owner | `Stats[stat].Value` | calculated signed int | missing/duplicate supplied snapshots are malformed/incomplete | `PROVEN_REPOSITORY_BEHAVIOR` for accessor | not wired to production damage |
| Add All Off | character stat | character stats table plus runtime modifiers | `AMSModifier` / 276 | `Stats.ReadStatsfromSql`; runtime modifiers | attacker stat owner | `Stats[276].Value` | calculated signed int | missing supplied snapshot blocks formula request | `PROVEN_REPOSITORY_BEHAVIOR` for accessor | not wired to production damage |
| AMS cap | `ItemTemplate.Stats` | `items.dat` | `AMSCap` / 538 | `ItemLoader.CacheAllItems` | diagnostic builder only | `weapon.GetAttribute(538)` | signed int | absence/zero semantics unresolved; negative malformed | `PROVEN_DATABASE_CONTRACT` for field presence | not used by production damage |
| Matching armor | target stats | character stats table plus runtime modifiers | damage-type AC stat | `Stats.ReadStatsfromSql`; runtime modifiers | target stat owner | `Stats[armorStat].Value` | calculated signed int | missing is not assumed zero | `PROVEN_DATABASE_CONTRACT` for mapping | not used by production damage |
| Type-specific add damage | attacker stats | character stats table plus runtime modifiers | damage-type add stat | `Stats.ReadStatsfromSql`; runtime modifiers | attacker stat owner | `Stats[addStat].Value` | calculated signed int | missing is not assumed zero | `PROVEN_DATABASE_CONTRACT` for mapping | not used by production damage |
| Universal add damage | none proven | none | none | none | none | none | unknown | builder reports missing source unless an explicit diagnostic source is supplied | `UNKNOWN` | unavailable |
| Normal/critical state | none resolved for ordinary callers | none | none | none | combat hit-resolution seam | not supplied | bool | missing state blocks formula request | `UNKNOWN` | unavailable |

`Stat.Value` is a calculated effective value: `(BaseValue + Modifier + Trickle) * PercentageModifier / 100`, floored. DB stat rows load into base values. The audit did not prove whether every damage-relevant caller has already-applied item, nano, perk, or owner-derived modifiers at the point a future formula builder would run.

## Representative records

Read-only sources used: `cellao_codex_clean.itemnames`, `cellao_codex_clean.items`, starter loadout code, captured NPC contracts, and `items.dat` via `ItemLoader`.

| Record | Source | QL | Min | Max | Legacy bonus | Raw damage type | Attack skills | AMSCap | Attack/recharge | Missing or ambiguous fields |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: | --- | --- |
| Solar-Powered Pistol `121567` | `items.dat`; starter loadouts; captured Thief contract | 1 | 2 | 18 | 18 | 90 | `112=100` | absent | `100/100` | critical bonus absent; AMS cap absent/semantics unresolved; universal add damage absent; critical state absent |
| Worn Oak Bo `121565` | `items.dat`; `itemnames` | 1 | 6 | 24 | 18 | 91 | `100=100` | absent | `50/150` | same critical/add/AMSCap blockers |
| Solar-Powered Rifle `121568` | `items.dat`; `itemnames` | 1 | 3 | 24 | 18 | 90 | `113=100` | absent | `100/150` | same critical/add/AMSCap blockers |
| DefaultDistance_001 `100240` | `items.dat`; `itemnames` | 1 | 6 | 12 | 6 | 92 | `100=100` | absent | `5/235` | identity is a template name, not proven active equipment; same critical/add/AMSCap blockers |
| Useless Triple-Blade `121572` | `items.dat`; `itemnames` | 1 | 1 | 10 | 10 | 91 | `103=67`, `106=33` | 30 | `100/129` | multi-skill representation proven; weighting semantics and zero-cap behavior not proven |
| Captured Subway Thief weapon `121567` | captured NPC contract | 1 | fixed 9 active contract | fixed 9 active contract | not used | projectile contract behavior | not used | not used | captured timing | remains `FixedCapturedDamage`; formula request not used |
| Violent Vagabond contract weapon `130590` | captured NPC contract plus `itemnames` | 1 | 1 | 1 | 1 | absent in explicit template stats | none | absent | `10/250` | contract proves equipped identity only; template is named `Red Wine`, so weapon semantics are ambiguous |

The sampled local `items` table had no current rows for these template IDs. A currently equipped player weapon is represented by starter loadout code using `121567`, not by a live inventory row in the audited database snapshot.

Read-only `items.dat` AMSCap audit for templates with both min/max weapon stats found `17,574` candidate weapon templates: `7,388` without stat `538`, `0` with zero, `10,186` positive, and `0` negative. This proves field presence distribution only; it does not prove formula semantics or active caller use.

## Attack-skill representation

Weapons can carry attack skills as an `ItemTemplate.Attack` dictionary of `statId -> percentage`. Single-skill examples use `100`. Multi-skill examples exist, e.g. `121572` has `103=67` and `106=33`.

The diagnostic builder enforces total weight `100` for formula readiness because no repository evidence proves behavior for missing, zero, negative, over-100, or non-total-100 weights. It does not calculate production AR. Add All Off is represented separately and is not applied to active damage.

## Armor mapping

Proven diagnostic mappings:

| Damage type | AC stat |
| --- | ---: |
| Projectile | 90 |
| Melee | 91 |
| Energy | 92 |
| Chemical | 93 |
| Radiation | 94 |
| Cold | 95 |
| Poison | 96 |
| Fire | 97 |
| Nano | 168 |

Disease and other internal/unknown values have no proven mapping. Missing armor is malformed/incomplete for formula request construction, not zero.

## Add-damage mapping

Proven type-specific diagnostic mappings:

| Damage type | Add-damage stat |
| --- | ---: |
| Projectile | 278 |
| Melee | 279 |
| Energy | 280 |
| Chemical | 281 |
| Radiation | 282 |
| Cold | 311 |
| Nano | 315 |
| Fire | 316 |
| Poison | 317 |

No universal weapon add-damage stat is proven. `DamageBonus` remains legacy damage bonus only.

## Request-builder behavior

`WeaponDamageRequestBuilder` is side-effect-free. It builds a diagnostic `DamageCalculationRequest` plus input provenance and issues, but it is not wired into active production damage selection.

`WeaponDamageDiagnosticSnapshotBuilder` is a separate opt-in diagnostic seam. It is disabled by default, does not call production formula selection, and evaluates candidate formulas only against supplied observation records.

The first ordinary-hit campaign adds an opt-in runtime JSONL evidence seam for player equipped-weapon auto-attacks. It is enabled only through `AO_REBIRTH_WEAPON_DAMAGE_EVIDENCE_SESSION` and writes under `.local\weapon-damage-evidence\<SESSION_ID>\raw\`. It records already-computed production result data plus weapon/template/stat evidence needed for import. The seam does not select a formula and is not active in normal engine startup.

Classifications:

- `FormulaInputComplete`: all supplied diagnostic inputs are complete.
- `FormulaInputIncomplete`: required inputs or semantics are missing.
- `LegacyRequired`: no trustworthy weapon template identity exists.
- `FixedCaptured`: fixed captured damage, such as Subway Thief.
- `MalformedData`: invalid or duplicate data, such as minimum greater than maximum, negative AMS cap, or duplicate attacker stat.

The builder never silently defaults missing data to zero. Known zero must be supplied explicitly.

## Caller readiness

| Caller | Readiness | Reason |
| --- | --- | --- |
| Player with equipped weapon | `PARTIAL_STAT_PROVENANCE` | weapon min/max/timing can be read, but active caller does not supply attack-skill provenance, matching armor, add-damage sources, or critical state |
| NPC with equipped weapon | `PARTIAL_STAT_PROVENANCE` | captured contracts can equip templates, but formula inputs are incomplete |
| NPC natural attack | `LEGACY_ONLY` | uses NPC min/max/damage bonus stats without weapon-template attack-skill provenance |
| Attack pet | `LEGACY_ONLY` | routes through legacy pet min/max/fallback damage, not formula inputs |
| Other pet | `LEGACY_ONLY` | no ordinary weapon formula input path proven |
| Captured fixed-damage NPC | `FIXED_CAPTURED` | fixed contracts bypass AR, AC, criticals, and add damage |

Target armor availability remains `AVAILABLE_BUT_SEMANTICS_PARTIAL` for player, NPC, and pet targets: matching AC stat mappings are known, but capture-level proof is still required for timing, formula ordering, and whether the active caller can provide the value at the exact hit boundary. Missing matching armor remains incomplete and is not zero.

## Remaining blockers before formula activation

- prove critical bonus source and semantics
- prove current normal/critical/miss/evade state seam for player, NPC, and pet ordinary hits
- prove attack-skill weighting semantics and behavior for invalid totals
- prove Add All Off ordering relative to weighting
- prove AMS cap absence, zero, and negative semantics
- prove AC divisor, ordering, and floor interaction
- prove add-damage ordering, eligibility, and universal stat source
- prove target armor availability for player, NPC, and pet targets
- prove caller integration can supply every input without changing packet order, timing, health mutation, death, corpse, loot, XP, or heartbeat behavior
- complete the parity observation matrix in `docs/project/damage-evidence/procedures/operator-observation-matrix.md`
