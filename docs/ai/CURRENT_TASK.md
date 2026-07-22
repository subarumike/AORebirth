# Current Task

## Active

### Capture-backed NPC combat repair

- One shared runtime now owns capture-backed WIFU, `SpecialAttackWeapon`,
  `Attack`, and `AttackInfo` construction and preserves the captured raw fields.
- Synthetic incoming-hit chat is removed; the serialized combat packet is the
  only incoming combat-line source.
- Unsupported contracts fail closed while their actors remain spawned and
  visible. The current initial-active audit covers `1,496` hostile/retaliatory
  actors: the proven
  level-5 Subway Thief plus `14` exact Temple Cultist identities are
  runtime-certified and `1,481` actors are passive and quarantined. Two later
  Subway Infector slots raise the configured maximum to `1,498`, with `1,483`
  quarantined.
- The certified Thief and fourteen Temple sources use their own owner-linked physical
  weapon definition and per-actor Energy sequence. Source `0x7984B379` preserves
  Energy/ammo `15 -> 14 -> 13` and exact captured packet bodies.
- Exact packet/audit tests and the Debug build pass. Chat, Login, and Zone were
  restarted successfully and ports `6996`, `7012`, `7500`, and `7501` listen.
  The official-client acceptance run remains unverified, so live client text is
  not claimed fixed.
- Full evidence and disposition ledger:
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
