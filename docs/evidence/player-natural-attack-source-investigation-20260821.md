# Player Natural Attack Source Investigation

Date: 2026-08-21

## Accepted evidence boundary

| Field | Value |
| --- | --- |
| `STARTING_SHA` | `5339673955e7781b15ff594061e683143303f0d6` |
| `ENDING_SHA` | `5339673955e7781b15ff594061e683143303f0d6` |
| Fresh capture | `Captures/ICC Shuttleport [PF 4582] - 20260821-034134` |
| Capture state | `PLAYER_CAPTURE_V2_ACCEPTED=YES` |
| Active weapons | `[]` |
| Natural mode | `natural-unarmed-runtime-no-equipped-weapons` |
| Player | level `3`, breed `4`, profession `9` |
| Observed normal damage | `5..10` |
| Observed Brawl damage | `6`, `10`, `13` |
| Critical sample | none |

The capture is accepted for observed behavior and state correlation. It is not treated as proof of unobserved natural-template or mitigation fields.

## Source conclusion

| Required field | Result | Authoritative source or reason |
| --- | --- | --- |
| `NATURAL_ATTACK_MODEL` | `UNRESOLVED` | The official client has an implicit runtime dummy-weapon path, but no accessible source proves whether its damage values are template-backed, computed, or hybrid. |
| `NATURAL_ATTACK_AUTHORITATIVE_SOURCE` | `Official Gamecode runtime dummy weapon selected by `WeaponHolder_t.GetDummyWeapon(pWeaponHolder, Stat.MartialArts)` | `AOSharp.Core/Dynel/SimpleChar.cs` and `AOSharp.Common/Unmanaged/Imports/Gamecode/WeaponHolder_t.cs` prove the call and its use for unarmed range checks. |
| `NATURAL_MIN_DAMAGE` | `UNRESOLVED` | No public dummy-template stat accessor or authoritative natural damage field was found. The capture value is the default/sentinel `1234567890`, not a usable value. |
| `NATURAL_MAX_DAMAGE` | `UNRESOLVED` | Same evidence boundary as minimum damage. |
| `NATURAL_CRIT_BONUS` | `UNRESOLVED` | No natural critical-bonus source or critical formula was exposed. |
| `NATURAL_PRIMARY_DAMAGE_TYPE` | `0 (raw only; semantics unresolved)` | Capture `DamageType1=0` is observed through `GetStat`; no authoritative natural-template mapping proves the human-readable type. |
| `NATURAL_SECONDARY_DAMAGE_TYPE` | `UNRESOLVED` | No natural dummy definition proves whether a secondary damage type exists. |
| `NATURAL_ATTACK_DELAY` | `UNRESOLVED` | The public `DummyItem.AttackDelay` path is for an accessible item object; the unarmed dummy pointer is not exposed as a safe plugin object. Observed hit intervals are not authoritative timing fields. |
| `NATURAL_RECHARGE_DELAY` | `UNRESOLVED` | Same evidence boundary as attack delay; capture value is sentinel/default. |
| `NATURAL_ATTACK_RANGE` | `UNRESOLVED` | The client uses the dummy pointer with native `IsDynelInWeaponRange`; no numeric natural range field was exposed. |
| `NATURAL_ATTACK_SKILL` | `UNRESOLVED` | `Stat` exposes `MartialArts`, but no `AttackSkill` enum/source for the natural dummy was found. `MartialArts=20` is a player skill value, not proof of the complete attack-source definition. |
| `NATURAL_DEFENSE_SKILL` | `UNRESOLVED` | No natural target-defense selection source was exposed. |
| `TARGET_AC_AVAILABLE` | `NO` | Fresh target updates contain `HasKnubotData`, level, health, `MonsterData`, and `MonsterScale`, but no AC fields. The shipped `mobtemplate.sql` schema has no AC columns. |
| `TARGET_AC_SOURCE` | `UNRESOLVED` | The repository documents AC-stat mappings for diagnostic weapon formulas, but this capture does not prove the active target value or the exact hit-boundary source. Missing AC is not assumed to be zero. |
| `CRITICAL_FORMULA_SOURCE` | `UNRESOLVED` | No authoritative client formula or complete natural critical sample was found. |

## What was inspected

### AOSharp runtime path

- `tools-temp/external/aosharp-github/AOSharp.Core/Dynel/SimpleChar.cs`: no-equipped-weapon range checks call `WeaponHolder_t.GetDummyWeapon` with `Stat.MartialArts`; unarmed specials are Brawl and Dimach; the dummy pointer is internal.
- `tools-temp/external/aosharp-github/AOSharp.Core/SpecialAttack.cs`: confirms the same dummy lookup for unarmed special range checks; it does not expose natural damage or timing fields.
- `tools-temp/external/aosharp-github/AOSharp.Common/Unmanaged/Imports/Gamecode/WeaponHolder_t.cs`: exposes native delegates for `GetDummyWeapon`, weapon lookup, and range checks, but no dummy structure or template-stat layout.
- `tools-temp/external/aosharp-github/AOSharp.Core/Game.cs`: binds the native dummy/range functions and `DummyItem_t.GetStat`; no natural-template resolver is bound.
- `tools-temp/external/aosharp-github/AOSharp.Common/GameData/Stat.cs`: contains `MartialArts`, `MinDamage`, `MaxDamage`, `CriticalBonus`, `AttackRange`, `AttackDelay`, `RechargeDelay`, damage-type stats, and `UnarmedTemplateInstance`; it does not provide a natural attack definition or `AttackSkill` enum.

### Official client and repository data

- `docs/reference/client-dll-function-map/ao_client_dll_combat_corpse_loot_readable_functions.csv`: records official client functions including `N3Msg_GetInterpolatedItem`, `N3Msg_CreateDummyItemID`, `GetAttackRange`, `N3Msg_GetSkillToAttackString`, `N3Msg_GetSkillToDefendString`, and damage display helpers. The map does not provide their natural-attack field values or formula implementation.
- `docs/reference/client-dll-function-map/cellao_cross_reference_combat_corpse_loot.csv`: marks the relevant dummy-item and skill-display functions as missing in the reconstruction; no implementation was promoted.
- `docs/reference/client-dll-function-map/ao_client_dll_symbol_hints.csv`: contains `Feedback_NeedUnarmedCombatWeapon` and `Feedback_RHNeedUnarmedCombatWeapon`, confirming an official unarmed-combat state, but not its numeric damage model.
- `E:/Anarchy Online/Gamecode.dll`: static binary strings confirm official symbols and `DummyItemBase_t` RTTI, but static strings do not establish the dummy object layout or formula inputs.
- `LinuxBuild/artifacts/zoneengine/linux-x64/framework-dependent/XML Data/Stats.xml`: confirms combat stat IDs/defaults and `UnarmedTemplateInstance` default `0`; it contains no natural dummy template record.
- `LinuxBuild/artifacts/zoneengine/linux-x64/framework-dependent/SqlTables/mobtemplate.sql`: Beach Leet data provides level/health/`MonsterData`/scale, but the schema contains no AC or natural attack fields.
- `docs/project/WEAPON_DAMAGE_INPUT_PROVENANCE.md`: defines proven equipped-weapon damage inputs and AC-stat mappings. It explicitly keeps natural NPC attacks legacy-only and treats missing matching armor as incomplete, not zero.

## Rejected candidates

- `UnarmedTemplateInstance=0` is a runtime state marker in the accepted capture, not a proven complete template identity.
- `MartialArts=20` is the player skill snapshot, not the natural damage range, timing, crit bonus, or full attack/defense model.
- Flimsy Hammer, Baseball Bat, and Light Bar are inventory candidates only. They were not active weapons and cannot supply the unarmed baseline.
- `N3Msg_GetInterpolatedItem` and `N3Msg_CreateDummyItemID` are official-client candidates, but no repository evidence maps either function to the natural attack dummy or proves the returned fields.
- Observed hit intervals and positional separation are capture diagnostics only. They are not promoted to `AttackDelay`, `RechargeDelay`, or `AttackRange`.
- The Beach Leet `MonsterData` value is not treated as AC or as a natural damage template identifier.

## Governance result

| Field | Value |
| --- | --- |
| `PLAYER_DAMAGE_BASELINE_COMPLETE` | `NO` |
| `REMAINING_GAPS` | Natural min/max damage; crit bonus and formula; primary/secondary damage semantics; attack/recharge timing; numeric range; attack skill; defense skill; target AC at hit boundary; critical sample. |
| `CAPTURE_TOOLING_CHANGE_REQUIRED` | `NO` |
| `RECAPTURE_REQUIRED` | `NO` |
| `GOVERNANCE_STATUS` | `BLOCKED_UNPROVEN_PLAYER_COMBAT_FIELDS` |
| `PROMOTION_STATUS` | No natural damage or mitigation fields promoted. |

The accepted capture already contains the required player-state, equipment, event, snapshot-correlation, and observed-hit evidence. The remaining gaps are authoritative client/runtime source gaps, not missing capture columns. A future source breakthrough may justify a narrowly scoped accessor investigation; this record does not authorize dereferencing guessed offsets or changing `AOSharpLiveCapture`.

## Tests and changes

- `FILES_CHANGED`: `docs/evidence/player-natural-attack-source-investigation-20260821.md`
- `TESTS_ADDED`: `NO`
- `TEST_RESULTS`: `Evidence-only change; no parser, runtime, or capture-tool code was changed. Existing accepted player-combat v2 validation remains `43 tests passed`.
- `AOSharpLiveCapture_CHANGED`: `NO`
