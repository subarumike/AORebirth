# Current Task

## Active

### RK terminal missions: Stage 3 captured interior materialization

Accepted generated missions now enter their exact isolated live PF2 and
materialize only the selected bundle's captured PAF payload, building, spawn,
exit, doors, chests, terminals, objective objects, and NPC placeholders.
Deterministic PF2-local runtime identities are persisted separately under
`mission-state/acg-runtime`; owner + live PF2 + runtime identity is required
for every lookup. Door/chest mutable state survives restart, shared replay is
blocked for bound missions, and abandoned/expired/cleaned bindings remove only
their own runtime registry and state.

Completion, rewards, loot outcomes, NPC combat, collision, navigation, and
procedural generation remain deferred. No database schema changed.

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
