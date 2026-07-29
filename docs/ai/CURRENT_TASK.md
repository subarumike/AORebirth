# Current Task

## Active

### RK terminal missions: Stage 5 operational isolated interiors

Bound generated-mission PF2 instances now bypass the legacy mission spawner and
materialize exact captured NPC slots as real server combat actors with Stage 3
runtime identities. Version-1 operational sidecars persist exact NPC health,
life/combat/corpse state and explicit unresolved-empty chest state. Kill death
is durable before Stage 4 completion, Find Person remains passive and exact
interaction-only, and all combat/container lookups require owner, live PF2, and
runtime identity.

Captured position, rotation, template, MonsterData, level, health, name, scale,
textures, and meshes remain authoritative. Existing production mission combat
owns damage and weapon behavior; no new formula is introduced. The finalized
captures do not prove corpse or chest contents, so generic loot is suppressed
and no item or credit outcome is fabricated. Distance and finite-coordinate
validation are active, but server collision, line-of-sight, room topology,
waypoint navigation, procedural generation, durable team rewards, and
private-client validation remain deferred. No database schema changed.

Evidence and architecture:

- `docs/evidence/RK_MISSION_ACG_INTERIOR_EVIDENCE_20260728.md`

### PF127 Subway combat completion

The cross-dungeon ordinary-combat checkpoint is complete: `489/0`.
PF127 is `322/0`; PF1931 is `167/0`.

The final 25 actors were 22 Violent Vagabonds, the L9 Stim Fiend
`0x7957E415`, and L18 Eternal Sentinels `0x7983FA22` and `0x7983FBC2`.
Vagabond setup uses bounded exact affine-floor equations over L6..L10 and the
capture-proven equipped-melee result domain. Stim Fiend extends its exact SIW1
formula only to the runtime-selected L9..L17 domain. Eternal Sentinel uses the
exact 123381..123384 loadout partitions and bounded L18..L20 setup formula.
Production still owns damage, range, cadence, QL, ammunition/Energy, and
mutable ordered state. Categorical weapon, mode, slot, instance, action,
hit/damage wires, stream identity, and packet order remain exact and
fail-closed.

Evidence:

- `docs/evidence/FINAL_ORDINARY_DUNGEON_COMBAT_COMPLETION_20260728.md`
- `docs/generated/enemy_combat_setup_formula_dataset.json`
- `docs/generated/capture_backed_npc_combat_active_coverage.json`

### PF127/PF1931 named encounter completion

The full named/scripted runtime inventory is complete for every active PF127
and PF1931 domain: 13 initial profiles, two successor stages, two owned-add
domains, and Murial's ordinary-owned named patrol. The ordinary baseline
remains `489/489`.

The shared encounter registry now owns registrations by playfield instance, so
PF127 and PF1931 retirement removes all encounter definitions without stale
combat, add, successor, patrol, respawn, corpse, or visibility workers.
Full-corpus combat generation is bounded-memory and reproducible: 374 sessions,
358 canonical sessions, 2,827 complete chains, 255 capture-certified profiles,
303 semantic definitions, and zero generator errors; the second generation
produces no diff.

Exact unresolved boundaries remain fail-closed: the post-Aztur full-chain
reset/respawn condition, downstream gameplay effects for presentation-only
Temple nanos, unknown loot probabilities, Murial-specific nano/respawn
behavior, and inactive Strike Foreman generation/lifecycle policy.

Evidence:

- `docs/evidence/DUNGEON_NAMED_ENCOUNTER_COMPLETION_20260728.md`
