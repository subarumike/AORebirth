# AORebirth Dyna Architecture

## Evidence boundary

The repository imports 174 community-documented camp rows in `docs/generated/enemy_catalog/sources/dyna_boss_list_1.normalized.*`. These establish documented locations/names/approximate levels only. They are not proof of RDB stats, exact spawn coordinates, minion composition, timers, mechanics, or loot probabilities. Imported rows remain proposals until reviewed evidence activates them.

## DynaCamp

`DynaCampKey`, playfield, zone, coordinates, radius, boss/minion profile pools, approximate boss level, minion level range, boss/minion spawn policies, maximum alive counts, initial population, boss/minion delays, optional shared timer, reset policy, loot table key, evidence, confidence, and activation state.

The controller supports boss-only and boss-plus-minion camps, weighted variants, level-scaled variants, camp-wide or individual respawn, boss replacement, player-absence policy, activation/deactivation, restart recovery, and persistence where evidence requires it.

## DynaBossProfile

`DynaBossProfileKey`, base enemy profile, display-name policy, level/stat scaling policies, appearance overrides, combat/nano/special-attack profiles, loot table key, respawn policy, evidence, and confidence. Boss identity, profile, camp placement, loot, and live spawn state are separate. Random names are a policy with evidence, not string logic in a controller.

Ordinary mechanics use the shared enemy runtime. A custom module is allowed only for proven unique mechanics.

## Loot inheritance

Global ordinary -> family -> dyna global -> dyna level band -> dyna family -> specific boss/camp -> event override. More-specific named groups replace inherited groups only when declared; otherwise they append. Stable priority/key resolves assignments and ambiguity fails validation. The camp controller supplies boss profile, camp, playfield, level, family, event state, and killer/team context to the global loot service; it never contains item tables.

## Rollout

Start with evidence normalization and validation, then one inactive reference camp, deterministic simulation, capture review, bounded activation, lifecycle observation, and only then broader imports. Community levels remain approximate until stronger evidence promotes confidence.
