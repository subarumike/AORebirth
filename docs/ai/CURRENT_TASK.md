# Current Task

## Current Focus

The shared ordinary-enemy level and respawn foundation is complete for PF127.
The next Subway task should use this foundation to finish the next ordinary
enemy from the existing capture corpus rather than adding another bespoke
runtime path.

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
- The generic foundation suite passes `24/24`. The full supported test assembly
  passes `322/335`; its 13 failures are the established unrelated baseline:
  three damage-evidence checks, one inventory-ownership guardrail, six
  session/lifecycle source guardrails, and three visibility-integration source
  guardrails. The focused lifecycle class remains `53/59` with exactly those six
  established session/lifecycle failures.
- The approved Debug build compiles AORebirth.Core and ZoneEngine source. Final
  ZoneEngine output copy remains blocked only because the running private server
  holds `Built\Debug\ZoneEngine.exe`; the engine was not stopped for this task.

## Remaining

1. Private-client validation of Bloodcreeper remains pending; do not describe it
   as accepted until gameplay confirms the existing spawn/combat/corpse/credit
   slice and unresolved item-loot handling is explicitly accepted or extended.
2. Continue whole-enemy acceptance work on the next ordinary Subway archetype
   using existing captures first. New respawn captures are for proven exceptions
   or disputed timing, not one capture per ordinary enemy.
3. Keep level ranges fixed unless existing evidence or an approved design
   decision establishes an inclusive band.

## Constraints

- The 240-second ordinary default is a private-project policy, not a claim that
  every official AO Subway enemy uses the same exact timer.
- Bloodcreeper is ordinary content, not a unique boss or scripted encounter.
- Do not turn two observed empty Bloodcreeper item snapshots into proof of an
  empty item pool.
- Do not activate the 38 quarantined rows or populate unresolved loot pools.
- Existing encounter, pet, vendor, static, quest, navigation, LOS, leash, corpse,
  loot, and combat ownership remains unchanged.
- Do not auto-attach or launch AO/capture tooling. Mike runs gameplay and supplies
  completed captures when requested.
