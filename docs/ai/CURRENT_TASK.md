# Current Task

## Active

### Capture-backed NPC combat repair

- One shared runtime now owns capture-backed WIFU, `SpecialAttackWeapon`,
  `Attack`, and `AttackInfo` construction and preserves the captured raw fields.
- Synthetic incoming-hit chat is removed; the serialized combat packet is the
  only incoming combat-line source.
- Unsupported contracts fail closed while their actors remain spawned and
  visible. The fixed initial-active audit covers `1,496` hostile/retaliatory
  actors and `755` merged profile rows: `85` actors/profiles are
  runtime-certified, while `1,411` actors across `670` profiles remain
  passive and quarantined with exact missing-evidence records.
- The deterministic corpus extractor audited `364` sessions (`348` canonical),
  recovered `2,647` complete attack chains, and generated `243`
  capture-certified profiles / `290` semantic definitions. Runtime safety gates
  admit `92` profiles / `100` definitions and report zero recoverable-evidence
  blockers. Per-actor, per-stream observation cursors preserve independent
  captured damage and ammunition sequences.
- Exact-byte replay, shared factory/catalog, fixed coverage, scripted-hostile
  coverage, secondary-evidence, range, Temple, and Subway checks pass. The full
  messaging suite is `447/480`; its remaining `33` failures are established
  unrelated damage/visibility/population work and were not changed here. The
  Debug build passes. Chat, Login, and Zone restarted successfully, with ports
  `6996`, `7012`, `7500`, and `7501` listening. Official-client acceptance
  remains unverified.
- Generated evidence and disposition artifacts:
  `docs/generated/capture_backed_npc_combat_inventory.json`,
  `docs/generated/capture_backed_npc_combat_active_coverage.json`,
  `docs/generated/capture_backed_npc_secondary_evidence_audit.json`, and
  `docs/generated/capture_backed_npc_attack_range_audit.json`. Narrative trace:
  `docs/evidence/CAPTURE_BACKED_NPC_COMBAT_AUDIT_20260722.md`.

### Prior pushed Arete main quest + implant crafting work

#### Arete main quest
- Mason / Vernon / Lorelei / Vaughn / Sarah / Stan / Shipping Manifest / ICC exit path
- Deliver tip → Stan trade factory → reward + Sarah / Buy Nano tips
- Bill FinishTrade no longer steals Stan Accept

#### Implant crafting
- Any-QL `IsImplant` recipes via robust resolve (reverse drag + Low/High/relations)
- Dapper `DBTradeSkill` column map (`Id1`/`Id2`/`ResultIds`/`QlRangePercent`)
- Tradeskill window accepts inventory slot 0
- UseItemOnItem derives result QL from implant (+ NanoProg bump)
- Mason Arete tip still QL1 Overflow

#### Retest (restart engines)
1. Zone console: large `Cached N trade skill entries` (~100k)
2. Cluster + Basic Implant both drag orders
3. Mason tip QL1 Overflow
4. Stan factory deliver with active tip

### Upstream (merged from origin)
Subway PF127 + Temple of Three Winds continue on master. PF647 is the Temple transfer/gateway; PF1931 owns the dungeon rooms, population, and loot. Combat certification is now capture-owned and fail-closed: the live-proven level-5 Subway Thief and fourteen exact Temple Cultist identities pass, and no unsupported actor may fall through to generic combat. The official-client text result is not yet verified. See `docs/project/PROJECT_STATE.md`, `docs/evidence/CAPTURE_BACKED_NPC_COMBAT_AUDIT_20260722.md`, and the TOTW evidence documents.
