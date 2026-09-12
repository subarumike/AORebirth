# ZoneEngine_New NPC/combat reconciliation — active work

This is an incomplete gameplay reconciliation record, not permission to switch
master. Baseline: `6dab287bd52590d7b9c0e7054bef95160a1594ba`.

## Inspected authority and runtime

| Files | Finding |
| --- | --- |
| `AORebirth/Server/ZoneEngine_New/Core/Playfield/SpawnService.cs`, `Core/Mobs/MobTemplates.cs` | A requested spawn level previously replaced only Level after copying the complete template stats. HP, AC and other attributes could therefore remain from another level. |
| `AORebirth/Server/ZoneEngine_New/Core/Playfield/OfficialHashSpawnAuthorization.cs`, `HashSpawnSystem.cs` | Native hash spawning requires exact governed placement identity, behavior readiness and explicit resolved template provenance. Raw extracted coordinates/hashes do not supply missing runtime identity authority. |
| `AORebirth/Server/ZoneEngine/Core/Playfields/OrdinaryEnemyCatalog.cs`, `OrdinaryEnemyProfile.cs`, `CapturedSubwayEncounterRuntimeService.cs` | Legacy has accepted profile/spawn/variant definitions and concrete activation consumers. These are existing supported functionality, not hypothetical AO behavior. |
| `AORebirth/Server/ZoneEngine/Core/Playfields/CapturedEnemyCombatContract.cs`, `CapturedEnemyCombatProfileCatalog.cs`, `CapturedEnemyCombatProfileCatalog.g.cs` | Accepted generated combat streams, timing, weapons and packet semantics exist, but their current consumer includes Legacy character/controller dependencies. They must be adapted, not regenerated from names or approximate formulas. |
| `AORebirth/Server/ZoneEngine_New/Core/Entities/NpcCharacter.cs`, `Character.cs`, `Core/Helpers/DamageCalculator.cs` | New NPC weapon full-update output is empty and attacks use the generic weapon damage calculation. This does not establish a connection to all accepted generated combat contracts. |

## Discrepancy matrix

| Area | Classification | Current disposition |
| --- | --- | --- |
| Level-only override retains other-level stats | BEHAVIORAL_REGRESSION | Repaired by `NpcTemplateLevelPolicy`: an explicit requested level must equal the template's actual Level stat before identity allocation or registration. A placement min/max range cannot authorize interpolation. |
| Accepted exact-level variants not yet represented by a selected New template | MISSING_IMPLEMENTATION | Requires an exact accepted profile/variant adapter. Rejecting a mismatch is a safety repair, not proof that variants have been ported. |
| Accepted Legacy NPC/profile activation has no New native-template bridge | MISSING_IMPLEMENTATION | Existing integration record reports all 199 authorized placement records lack this additional bridge. Keep the gate; use exact accepted profile materialization where available instead of inventing a hash/name match. No new NPC activation is claimed. |
| Generated combat contract consumers | PARTIAL_IMPLEMENTATION | The exact accepted generated-mission fixed/pistol consumer is connected. General accepted profile special attacks, parallel streams and production placement activation remain missing; the narrow mission runtime explicitly rejects unsupported contract shapes. |
| Accepted aggro, chase, leash, death, reward, loot and respawn policies | MISSING_IMPLEMENTATION | Need per-profile comparison and New consumers; generic engine behavior alone is not parity evidence. |
| Unresolved identity/behavior records | UNPROVEN_BEHAVIOR | Remain blocked. No nearest-name, nearby-location, template-hash or nearest-level fallback. |

## Implemented files and validation

- Added `Core/Mobs/NpcTemplateLevelPolicy.cs` and called it from New `SpawnService.Spawn`.
- Removed the separate Level-only mutation.
- Added `ZoneEngine_New.Tests/NpcTemplateLevelPolicyTests.cs`: exact level,
  unchanged no-override path, lower/higher levels, unsupported placement ranges,
  missing Level stat and nonpositive requested levels.
- Focused New test wrapper passed with these tests included. Whole-domain combat,
  exact-SHA cross-platform acceptance and client gameplay remain unclaimed.

No accepted generated data was edited. No capture/client was launched, no raw
capture dependency was introduced, and no existing blocked placement was promoted.

## Generated mission NPC and corpse checkpoint

The mission path now consumes the existing five selectable ACG bundles through
`GeneratedMissionNpcFactory`. The accepted BART production shell, frozen mission
difficulty level/health, exact source identity, decoded appearance and passive
FindPerson disposition are preserved. The mission policy is not presented as
capture-proven damage for arbitrary native NPCs.

- `MissionNpcCombatPolicy` and `MissionNpcCombatRuntime` adapt the existing
  mission-only fixed SIW1 / five-pistol policy, exact WIFU/SAW/Attack/AttackInfo,
  accepted timing, range, chase and death-stop behavior. No general profile
  selector is inferred from a name, level or nearby placement.
- `CapturedNpcCombatResolver` requires exact resource/name/MonsterData/level and
  original source membership in the accepted generated profile. It is a resolver,
  not proof of an ordinary-world activation consumer.
- Damage/death is committed through the mission object transaction before actor
  health/death publication. Restart preserves the durable damaged/dead object.
- The narrow `SpawnDeathCorpse` hook keeps New generic behavior unchanged outside
  generated missions. Mission corpses retain the exact NPC instance under the
  Corpse type, owner-only durable cash and accepted lifecycle rather than the
  generic loot pool or generic Biofreak visual packet.
- `GeneratedMissionCorpseWire` extracts the unchanged accepted L7 Tilda CFU and
  mission CATMesh map into one source used by Legacy and New. The combined test
  exposed that the shared typed CFU decoder cannot parse this accepted packet.
  `Dynel.BuildSpawnPacket` therefore provides a narrow exact-wire visibility path;
  ordinary typed spawns are unchanged. Byte-level tests preserve the entire
  accepted body, name terminator, material tail, current receiver and durable cash.
  Failed initial sends clear the visibility mark for retry. Unknown item loot
  remains explicitly unresolved-empty, as in the existing generated consumer.

The final focused checkpoint passed 308 tests and startup validation, including
exact corpse projection/visibility, mission NPC lifecycle and durable token credit.
The actual disposable SQL wrapper also passed corpse late-failure rollback,
exactly-once claims and restart checks. These source-level results do not establish
cross-platform acceptance or live gameplay validation. General accepted NPC/profile
activation, specials, loot, respawn and encounter parity remain substantive
master-switch work.
