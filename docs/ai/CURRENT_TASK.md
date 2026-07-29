# Current Task

## Active

### RK terminal missions: Stage 4 exact objective completion

All five generated mission types now resolve through accepted quest, owner,
isolated PF2, exact runtime objective identity, and captured objective slot.
Version-1 objective sidecars persist the immutable objective contract,
mission-item identity, frozen rewards, grant states, completion packets, and
exact cleanup. Separate structured accepted-QFU builders preserve the captured
Find Person version `16`/flag `64`, Find Item version `15`, Return Item version
`8`, and Repair component `100348` to machine `100358` relationship.

Completion is durably resumable and never repeats a reward already marked
granted. A legacy cash/XP/inventory grant left in `Pending` fails closed for
operator reconciliation because that persistence cannot atomically commit with
the journal. NPC combat simulation, generic loot, collision, navigation,
procedural generation, durable team rewards, and private-client validation
remain deferred. No database schema changed.

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
