# Current Task

## Active

### RK terminal missions: Stage 2 durable instance binding

Generated terminal-mission acceptance now creates a distinct accepted quest
identity and atomically persists one version-2 ACG binding under
`mission-state/acg-bindings`. The binding owns the exact offer, owner/no-team
state, mission seed/key/exterior/terminal, selected bundle/hash/building,
isolated live PF2, expiry, and lifecycle. Startup validates and restores active
reservations before allocation. Exact key/marker resolution replaces the
newest-mission fallback for bound missions.

Allocated live PF2 values are bounded to `0x160000..0x16FFFF`, excluding every
captured PF2 and shared PF `1419349`. Bound production entry remains
fail-closed until Stage 3 can safely materialize doors, chests, NPCs,
objectives, collision, and navigation.

Evidence and architecture:

- `docs/evidence/RK_MISSION_ACG_INTERIOR_EVIDENCE_20260728.md`

### PF127 Subway combat completion

All `51` PF127 Filth Fleas now resolve capture-backed combat. The final nine
L7, L8, L14, and L15 actors use the bounded
`filth-flea-saw-bounded-level-piecewise-v1` numeric setup while retaining the
exact natural EPAH/AZUS special sequence, slots, instances, hit/damage wires,
terminal outcome, and shared packet path from semantic profile
`218eb3509f2be66b-12f99a4c2f732061`.

The formula is exact over the available stable L4..L21 observations:
`floor((21*L+28)/4)` for L4..L10 and `6*L-1` for L11..L21. Production retains
level, damage, range, cadence, Energy/ammunition, and ordered mutable SAW
state. The independent L19 `Unknown2=141` observation remains mutable
generation-local state rather than reusable identity.

The fixed PF127/PF1931 checkpoint is now `464/25` of `489`: PF127 improves
from `290/32` to `299/23`; PF1931 remains `165/2`.

Evidence:

- `docs/evidence/SUBWAY_FILTH_FLEA_COMBAT_RESTORATION_20260726.md`
- `docs/generated/enemy_combat_setup_formula_dataset.json`
- `docs/generated/capture_backed_npc_combat_active_coverage.json`
