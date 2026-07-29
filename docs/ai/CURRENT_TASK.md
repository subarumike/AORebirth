# Current Task

## Active

### RK terminal missions: exact abandonment cleanup

Generated-mission abandonment now authorizes the exact accepted binding before
sending Quest Delete, advances only that objective and binding, removes only
the binding-owned key/tool/item artifacts, and retries durable cleanup without
resuming completion rewards. Unknown or authored quest deletes no longer enter
terminal-mission fallback cleanup, and legacy terminal cleanup no longer pops a
different mission's newest key or clears character-wide mission state.

PF2 release is now gated by successful spatial, operational, and materialized
runtime cleanup plus the objective journal's durable cleanup-complete state.
Stale binding transitions are rejected so completion and abandonment cannot
overwrite each other. Failed cleanup remains durable work instead of reaching
`Cleaned`. Focused and mission-filtered regressions plus the isolated Debug
build pass. Live expiry scheduling, occupant evacuation, and mission-corpse
retirement remain separate work.

### RK terminal missions: live level-4 integration repair

Private-server validation now proves roll, accept, exact key grant, isolated
entry, Find Item pickup, completion, credit/item reward, and exact key removal.
The same run exposed an ICC destination-filter bug and an unreachable bound-PF2
NPC spawn hook. Same-playfield PF655 markers now remain eligible for low-level
neutral ICC rolls, bound ACG instances reach Stage 5 NPC materialization, and
live NPC level/health reuse the existing deterministic mission-QL policy rather
than the source capture's higher values. Version-1 operational state is
validated and atomically migrated to version 2 so active missions preserve
mutable state while adopting safe difficulty. Build/regression and one live
roll/entry/combat recheck remain available, but Mike deferred further live
testing for this session.

### RK terminal missions: Stage 6 captured spatial authority

Each bound generated-mission PF2 now derives a deterministic finite
axis-aligned envelope from the exact selected bundle's captured spawn, exit,
dynels, NPC slots, and objective slots. A bounded `2.0` coordinate tolerance is
the only expansion. Player movement, doors, chests, objectives, repair,
Find Person, exit, player/NPC combat, aggro, and damage all require the exact
owner, live PF2, mapped runtime identity, finite coordinates, active lifecycle,
and the same bundle envelope.

Version-1 `acg-spatial` sidecars persist only the last valid mission-player
position and exact binding identity with SHA-256 and atomic replacement.
Invalid movement restores the last accepted position or captured spawn. The
existing production `8.0` interaction/combat distance remains authoritative.
No generated-PF collision geometry exists, so LOS that requires geometry is
explicitly unresolved and fail-closed; range-only operations do not claim clear
LOS. Mission NPC pursuit is stationary because no safe navigation graph exists.

Stage 1 payloads and hashes, Stage 2 bindings/PF2s, Stage 3 runtime identities,
Stage 4 completion/rewards, and Stage 5 combat/corpse/container state remain
unchanged. No procedural generation, room topology, collision mesh,
pathfinding, loot, reward, slider, authored-quest, or database-schema work is
included.

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
and PF1931 domain: 14 initial profiles, two successor stages, two owned-add
domains, and Murial's ordinary-owned named patrol. The ordinary baseline
remains `489/489`.

The shared encounter registry now owns registrations by playfield instance, so
PF127 and PF1931 retirement removes all encounter definitions without stale
combat, add, successor, patrol, respawn, corpse, or visibility workers.
Full-corpus combat generation is bounded-memory and reproducible: 375 sessions,
359 canonical sessions, 2,827 complete chains, 255 capture-certified profiles,
303 semantic definitions, and zero generator errors; the second generation
produces no diff.

The remaining dungeon-gameplay backlog is now explicitly owned. Aztur NPC
despawn schedules exactly one full-chain reset by rematerializing Uklesh after
the Temple named-policy delay; successor stages and owned adds never respawn
independently. All 19 named respawn domains are classified explicitly. Murial
retains one ordinary-population-owned patrol and an explicit 300-second
post-despawn policy reset. Eumenides retains its captured 30-minute
loot-bearing corpse and now uses the captured shared three-second empty-corpse
cleanup bound.

Strike Foreman is active under the PF127 named-encounter owner. Its level-19
runtime selects QL19 inside the exact `122767/122768` item range, retains the
captured equipped/slot/packet semantics, and uses the shared PF127 lifecycle
and level-bounded loot-quality policy.

Exact unresolved boundaries remain fail-closed: downstream gameplay effects
and scheduling for presentation-only Temple nanos, unknown loot probabilities
and wider pools, and Murial nano `70294` and loot. The Aztur-to-Uklesh
600-second interval remains an explicit Temple policy rather than a
capture-timed interval.

Evidence:

- `docs/evidence/DUNGEON_NAMED_ENCOUNTER_COMPLETION_20260728.md`
- `docs/evidence/DUNGEON_GAMEPLAY_COMPLETION_20260728.md`
