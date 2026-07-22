# Temple of Three Winds: Guardian, Gartua, and Main Room

## Scope

- Resource/playfield: PF1931 (`Temple of Three Winds`)
- Finalized official-live captures:
  - `20260721-230426`: Guardian of Tomorrow fight, corpse, loot, and complete nearby main-room SCFUs
  - `20260721-230824`: Gartua the Doorkeeper fight, corpse, and loot
  - `20260721-231151`: main-room fight and respawn slice
  - `20260721-232051`: additional main-room respawns, five hallway Cultists, and Gartua waypoints
- PF647 remains only the Temple transfer/gateway. All actors in this document run in PF1931.

## Guardian of Tomorrow

- Exact generation: L68, 26,500 health, MonsterData `22798`, scale `108`, run-speed base `263`.
- Exact anchor: `(274.823364, 13.01125, 388.980774)`.
- Combat was player-initiated. The captured actor emitted parallel slot-1 and slot-0 weapon streams. Normal local-player outcomes were one `36` opener followed by `75` outcomes; two `173` critical outcomes remain report-only.
- Exact corpse: CATMesh `21082`, 2,830 credits.
- Exact available-loot snapshot: `287143` QL200, `204596` QL1, `204756` QL1, and `204601` QL1, all quantity one.
- Mike's measured lifecycle annotation is ten-minute respawn and 30-minute unlooted loot-bearing corpse lifetime. Runtime normalizes the respawn to the existing named-Temple policy of 600 seconds after NPC despawn.

## Gartua the Doorkeeper

- Exact generation: L65, 14,130 health, MonsterData `159085`, scale `107`, run-speed base `228`.
- Exact anchor: `(274.99, 14.2112513, 426.642548)`.
- Gartua initiated combat. Eight captured normal local-player outcomes span `76..114`, slot `6`, ammo `-1`, weapon instance `0`.
- Nano `205590` is a self-targeted cast. The runtime preserves its captured self-target, initial combat delay, cast time, and repeat interval but does not invent the unresolved downstream stat effect.
- Exact corpse: CATMesh `23366`, 1,592 credits.
- Exact available-loot snapshot: `204650` QL1 and `204598` QL1, both quantity one.
- Mike's measured lifecycle annotation is ten-minute respawn and 120-second unlooted loot-bearing corpse lifetime.
- `20260721-232051` adds an exact identity-linked three-point path for Gartua
  `(SimpleChar:7987F148)`: `(275.379242,13.0112476,417.979675)`,
  `(274.75,14.0012474,408.15)`, and `(271.116425,14.0112476,409.686)`.
  The runtime adds this path to Gartua's earlier clean spawn generation instead
  of replacing that generation with the new damaged/in-motion SCFU.

## Main-room population and lifecycle

- `20260721-230426` contains 22 complete, unique Cultist SCFUs from the room band `z=419.313..462.421`. Those exact anchors are active in the existing Temple ordinary provider and reuse only the already capture-backed Cultist appearance, combat, aggro, corpse, loot, and credit profiles for their MonsterData values.
- `20260721-231151` contains complete replacement SCFUs for Acolyte Kalen, Acolyte Verona, Cyth the Faithful, Reverend Saxx, and Reverend Dashell.
- Three strict chains correlate death, corpse removal, and replacement: Kalen `309.825` seconds death-to-replacement, Verona `309.699`, and Dashell `310.066`. Their replacement occurs about 125 seconds after corpse removal, corroborating the existing ordinary Temple policy of 300 seconds after the engine's ten-second dead-NPC despawn boundary.
- `20260721-232051` adds three more strict chains: Acolyte Kalen
  `(7987F0AC -> 7987F125)` at `310.104` seconds, Acolyte Verona
  `(7987F0B2 -> 7987F12A)` at `309.974`, and Windcaller Donnel
  `(7987F0CC -> 7987F12E)` at `310.044`. These replacements appear
  `125.725..126.685` seconds after corpse removal and independently corroborate
  the same 300-second post-NPC-despawn policy.
- Five complete static Cultist SCFUs from the `z=404.832..409.260` hallway band
  are newly active: identities `7987F143`, `7987F145`, `7987F146`, `7987F147`,
  and `7987F149`. Identity `7987F107` overlaps the already promoted
  `x=315/z=455` anchor and is intentionally deduplicated.
- Uklesh the Frozen and the remaining named main-room actors are not activated by this slice. Their visible generations are preserved as evidence, but Uklesh has no completed fight/death capture and the wider named-actor loot/respawn set is incomplete.

## Unresolved

- Guardian and Gartua loot probabilities beyond their exact observed snapshots.
- Guardian and Gartua social aggro and exact reset/leash boundaries beyond the captured Gartua path.
- Gartua nano `205590` downstream stat ownership/effect.
- Remaining named main-room actor promotion and Uklesh combat.
- PF1931 collision/line-of-sight geometry.
