# AORebirth Test Gap Analysis

| Area | Current evidence | Gap | Required contract |
| --- | --- | --- | --- |
| Ordinary profiles/spawns | source and lifecycle tests | cross-playfield manifests | duplicate/reference/quarantine/boss/summon rejection |
| Visibility | policy/index/lifecycle/metrics tests | pacing/static objects/formal capacity | bounded recipients and packet budget |
| Loot | narrow corpse and captured tests | normalized inheritance, groups, rights | seeded golden vectors and evidence validation |
| Corpse | captured open/close/reopen/credit paths | concurrent access, disconnect, restart | state-machine tests with packet fixtures |
| Respawn | accepted enemy timing tests | shared timers, cancellation, restart | fake clock scheduler tests |
| Dyna | import parser test | no runtime simulation | camp population/replacement/recovery tests |
| Mission/dungeon | Arete content validators | shared population integration | instance isolation/determinism/cleanup |
| Encounters | one-off handlers | no framework boundary | data-only boss rejection and module capability tests |
| Persistence | character/mission DAOs | world state recovery | versioned snapshot/restart tests |
| Pets | dedicated runtime/catalog tests | global scans, restore/cleanup integration | owner disconnect/zoning/restart and visibility tests |
| Weapons/nanos | damage evidence framework, nano services | incomplete formulas and broad integration | evidence-gated formula vectors and effect lifecycle |
| Content pipeline | several deterministic generators | no common schema/dependency graph | all-source validation and inactive unresolved rows |

Guardrails should scan first-party runtime source for enemy-name/MonsterData branching outside approved adapters, embedded item tables in enemy/controllers, spawn-owned timers, ordinary catalog boss/summon rows, whole-playfield dynamic fanout, duplicate identities, and observed-only guaranteed loot.

Replace timing-dependent tests with injected clocks; replace production randomness with deterministic test sources; prefer executable state transitions over source-string assertions. Preserve exact packet byte/order fixtures where protocol evidence exists.
