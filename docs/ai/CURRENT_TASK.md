# Current Task

## Current Focus

The complete existing Bloodcreeper and Disobedient Bot corpse-loot corpus has
been audited and every strictly supported item behavior is implemented. The
next active boundary is the bounded private Bloodcreeper smoke; no new capture
is requested by this completed offline slice. After that smoke, continue the
next Subway enemy from the existing corpus through the shared ordinary runtime.

## Done in this slice

- All 260 represented Subway population rows now carry a non-null generic level
  definition. The same 222 rows remain active and the same 38 remain
  quarantined.
- Fixed captured rows stay fixed across respawns. Bloodcreeper is the only
  current inclusive range (`L15..L25`) and selects once per new population
  generation through the generic data path.
- Level selection is injectable for deterministic tests. The selected variant
  and generation are stored in the ordinary runtime definition and are resolved
  before level-dependent stats and combat preparation.
- Visibility loss/re-entry, combat reset, corpse transitions, runtime ticks, and
  navigation cannot reroll a level. Failed materialization retries reuse the
  selection for the same generation; stale generations fail closed.
- Eligible ordinary enemies now inherit the centralized 240-second PF127
  post-NPC-despawn policy unless explicit data overrides it.
- Explicit policies retain precedence: Thief remains 60 seconds; Filth Flea and
  Bloodcreeper remain 240 seconds.
- Named enemies, bosses, scripted encounters, summons, pets, temporary encounter
  adds, vendors, static objects, containers, quest-owned entities, explicit
  no-respawn rows, and unsupported classifications cannot inherit the ordinary
  default.
- The existing `WorldPopulationController` and `WorldRespawnScheduler` retain
  generation and scheduling ownership; no second scheduler was added.
- Deterministic level, respawn-resolution, exclusion, validation, scheduler,
  population-count, Bloodcreeper, and ordinary-profile tests pass.
- The generic foundation suite passes `24/24`. The focused loot-evidence suite
  passes `10/10`, and the full supported test assembly passes `332/345`; its 13
  failures are the established unrelated baseline:
  three damage-evidence checks, one inventory-ownership guardrail, six
  session/lifecycle source guardrails, and three visibility-integration source
  guardrails. The focused lifecycle class remains `53/59` with exactly those six
  established session/lifecycle failures.
- The approved Debug build compiles AORebirth.Core and ZoneEngine source. Final
  ZoneEngine output copy remains blocked only because the running private server
  holds `Built\Debug\ZoneEngine.exe`; the engine was not stopped for this task.
- The completed corpus audit is recorded in
  `docs/evidence/SUBWAY_BLOODCREEPER_DISOBEDIENT_BOT_LOOT_AUDIT.md`.
- Disobedient Bot has seven strict complete loot outcomes: one QL1 Small Power
  Supply (`234877/234877`), one QL10 Eye Implant: Pharma Tech, Bright
  (`104683/104684`), and five item-empty inventories. Runtime item selection uses
  a provisional weighted-one policy with relative weights `1 + 1 + 5 empty`.
  The two memberships are capture-proven, but the weights are private-server
  policy and the broader pool remains incomplete. Burnt Out Memory Chip
  (`234876/234876`) remains inactive because its corpse linkage is incomplete.
- Bloodcreeper has four exact corpse generations and two complete item
  inventories, both empty. This does not prove an empty item pool; Bloodcreeper
  item loot remains explicitly unresolved and inactive while its proven corpse,
  150-credit, level, combat, and 240-second respawn behavior remains in scope.
- Capture tooling now canonicalizes padded and unpadded numeric corpse identities
  before joins and permits offline reconstruction when projection status is
  incomplete but `recaptureRequired=false`.
- Finalized capture `20260716-034559` is indexed only for Melded Patterns combat
  evidence. Player-target rows now establish four captured hits at `21..34`;
  Healer-pet hits are excluded, repeat cadence remains unresolved, and this
  evidence report does not activate a Melded Patterns runtime contract.
- No quarantined population rows were activated; the population remains 260
  catalog rows, 222 active rows, and 38 quarantined rows.

## Remaining

1. Run the bounded private Bloodcreeper smoke: level `15..25`, Bite, Spit,
   chase, corpse, 150 credits, unresolved/empty item handling, close/reopen,
   cleanup, 240-second respawn, and no duplicate generation.
2. If later loot evidence is required, collect only the remaining bounded
   samples: eight strict complete Bloodcreeper outcomes and three strict complete
   Disobedient Bot outcomes. No new live capture is requested in this task, and
   combat, geometry, LOS, navigation, chase, leash, and respawn do not need to be
   recaptured for this loot boundary.
3. Continue the next whole-enemy slice from the existing corpus first. Keep
   fixed-level rows fixed until capture evidence or an approved design decision
   establishes a range.

## Constraints

- The 240-second ordinary default is a private-project policy, not a claim that
  every official AO Subway enemy uses the same exact timer.
- Bloodcreeper is ordinary content, not a unique boss or scripted encounter.
- Do not turn two observed empty Bloodcreeper item snapshots into proof of an
  empty item pool.
- Do not describe the Bot `1 + 1 + 5 empty` policy as official weighting or a
  complete pool, and do not activate `234876/234876` without a strict corpse
  identity chain.
- Do not activate the 38 quarantined rows or populate unresolved loot pools.
- Existing encounter, pet, vendor, static, quest, navigation, LOS, leash, corpse,
  loot, and combat ownership remains unchanged.
- Do not auto-attach or launch AO/capture tooling. Mike runs gameplay and supplies
  completed captures when requested.
