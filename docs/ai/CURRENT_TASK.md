# Current Task

## Current Focus

Finalized Vergil capture `20260716-222007` is integrated as a boss-only
`40`-unit NPC travel leash while the shared PF127 and target-distance safety
boundaries remain `100`. The next active boundary is a bounded private Vergil
leash smoke. After that smoke, return to the bounded Bloodcreeper smoke and then
continue the next Subway enemy from the existing corpus through the shared
ordinary runtime.

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
- Finalized capture `20260716-034433` is indexed only for Vergil Aeneid. It adds
  exact L29/6796 HP/scale 131/RunSpeed 131 runtime data, exact-level fail-closed
  healing, the captured 420-byte corpse scale field, and a complete `563`-credit
  five-item corpse snapshot including 100 QL1 bullets.
- Vergil's three observed corpses now replay as indivisible item-plus-credit
  snapshots. No generated corpse can mix items or credits from different
  captures; wider pool membership and official selection probabilities remain
  unresolved.
- Vergil combat evidence now keeps five local-player hits at `22..23` separate
  from three Killer-pet hits at `23..28`. Retarget-heavy cadence remains
  unresolved, and weapon damage/recharge remains equipped-weapon-owned.
- No quarantined population rows were activated; the population remains 260
  catalog rows, 222 active rows, and 38 quarantined rows.
- Every born-empty or fully emptied corpse now starts cleanup at three seconds,
  including credit-only corpses whose final credit award is delayed.
- Abmouth and Vergil schedule a new generation exactly ten minutes from death,
  independently of dead-NPC despawn and the older loot corpse.
- Abmouth and Vergil loot-bearing corpses now retain their confirmed 30-minute
  lifetime. Capture `20260716-220400` adds a second atomic Abmouth
  item-plus-`587`-credit snapshot without mixing its slots with the older corpse.
- Abmouth capture evidence keeps four player-facing hits separate from ten hits
  against the player-owned Healer and Wrath Incarnation pets.
- Vergil capture `20260716-222007` contains two reset cycles with `40.52` and
  `40.30` unit homeward paths. Vergil now resets only after his own travel from
  home exceeds `40`; his target may remain beyond `40` without prematurely
  triggering that boss override, while the existing `100`-unit target safety
  boundary remains intact. Other PF127 NPCs retain the shared `100`-unit
  private policy.

## Remaining

1. Run the bounded private Vergil leash smoke: draw him beyond `40` units from
   his activation home while remaining within the shared `100`-unit target
   boundary; confirm combat clears, he routes home, and healing state resets.
2. Run the bounded private Bloodcreeper smoke: level `15..25`, Bite, Spit,
   chase, corpse, 150 credits, unresolved/empty item handling, close/reopen,
   cleanup, 240-second respawn, and no duplicate generation.
3. If later loot evidence is required, collect only the remaining bounded
   samples: eight strict complete Bloodcreeper outcomes and three strict complete
   Disobedient Bot outcomes. No new live capture is requested in this task, and
   combat, geometry, LOS, navigation, chase, leash, and respawn do not need to be
   recaptured for this loot boundary.
4. Continue the next whole-enemy slice from the existing corpus first. Keep
   fixed-level rows fixed until capture evidence or an approved design decision
   establishes a range.

No repeat named-boss capture is needed for the promoted data. The packet
captures do not independently bracket ten minutes; Mike's direct live timing
confirmation establishes that value.

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
- Do not merge player-owned-pet damage into player-facing enemy damage or infer
  Vergil cadence from a mixed-target fight.
- Keep Vergil loot snapshots atomic; independent item-slot or credit rolls would
  create corpse combinations that were never observed.
- Keep both Abmouth loot snapshots atomic. Capture `20260716-220400` reused
  corpse identity `F69001`; generation-aware offline reconstruction now rebinds
  the new six-item snapshot to Abmouth instead of the stale Vergil generation.
- Named-boss respawn starts at death and must not wait for a 30-minute corpse.
- Existing encounter, pet, vendor, static, quest, navigation, LOS, leash, corpse,
  loot, and combat ownership remains unchanged.
- Do not generalize Vergil's captured `40`-unit NPC travel limit to Abmouth,
  Infectors, or ordinary PF127 enemies. Do not invent a dynamic home anchor from
  a capture that began with Vergil already visible and fighting.
- Do not auto-attach or launch AO/capture tooling. Mike runs gameplay and supplies
  completed captures when requested.
