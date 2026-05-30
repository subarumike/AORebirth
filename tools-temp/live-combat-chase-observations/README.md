# Official Live Combat Chase Notes

Hard rule for future work:

- Do not guess protocol, combat, movement, equipment, loot, logout, or visual behavior.
- Use official live capture, known-good source code, or captured private-server reference data before changing behavior.
- Keep enemy movement behavior profile-specific; do not apply one mob's movement packet flow globally unless capture proves it is shared.
- If capture coverage is missing, collect the missing capture before coding the behavior.

Capture source: official live AO server, player `Mindfracture` / `SimpleChar:77E458F5`.

Capture folder:

`tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260528-191120`

Additional official-live capture:

`tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260528-192819`

Focused official-live chase capture:

`tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260529-205920`

Focused official-live moving-target chase capture:

`tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260529-212034`

## Player Attack Flow

- Client sends `Attack` from player to mob.
- Server echoes `Attack` from player to mob.
- Mob retaliation is a separate server `Attack` from mob to player.
- Normal weapon and mob melee damage is sent as `AttackInfo`, not `HealthDamage`.
- When the player stops attacking, live echoes `StopFight` for the player only; the mob can keep attacking.
- On mob death, live sends `StopFight` for the player, `StopFight` for the mob, then `CharacterAction Death`.

## Mob Chase Flow

Observed combat mobs:

- `SimpleChar:7888E378`
- `SimpleChar:7888E568`
- `SimpleChar:788DA3B8`

Live sends NPC chase movement with coordinate `FollowTarget` packets. `SetPos` appears as a correction/settle packet in some captures; it is not sent before every moving-target repath.

Example, mob `7888E378` at `2026-05-29T00:11:47.9467311Z`:

- `SetPos` for `7888E378`
- `FollowTarget` for `7888E378`, packet length 56
- Follow coordinate header after identity is `00 01 19 02`

Example, mob `788DA3B8` from capture `20260529-212034` while the player moved:

- Repeated coordinate `FollowTarget` packets for `788DA3B8`, packet length 56
- Follow coordinate header after identity is `00 01 19 02`
- Occasional separate `SetPos` corrections, not a `SetPos` before every repath

Correction sequence observed for `788DA3B8` at `2026-05-30T02:21:01.5794475Z`:

- `FollowTarget`, length 56, type-2 position-stop body: `00 02 19 ...`
- `StopMovingCmd`, length 41, body after identity: `00 00000001 C700000D 00000001`
- `SetPos`, length 47, body after identity: `00 <Vector3> 01 00000000 00`
- `FollowTarget`, length 68, type-2 settle body: `00 02 15 <mob identity> 00000000 <Vector3> 01 <Vector3>`

CellAO must not send a bare `SetPos` as a chase repair. If a correction is needed, send the captured correction sequence above.

That means:

- N3 unknown byte: `0`
- `FollowInfoType`: `1`
- `MoveMode`: `25` / run
- `CoordinateCount`: `2`

CellAO should not send the coordinate-follow N3 unknown byte as `1` for this chase case.

## Caster Mob Chase/Nano Fight

Capture `20260528-192819` included a useful fight against `SimpleChar:7888E5A4`.

Important sequence:

- `00:30:44.310` mob sends `SetWantedDirection`, `SpecialAttackWeapon`, `Attack -> player`, then `SetPos`.
- `00:30:50.834` mob finishes nano casting and damages player with `HealthDamage`.
- `00:30:50.834` mob sends a long `FollowTarget` packet, length 104, with `FollowInfoType=2`.
- `00:30:52.185` mob sends a `FollowTarget` packet, length 68, with `FollowInfoType=2`.
- `00:30:52.954` mob sends a simple coordinate `FollowTarget` packet, length 56, header `00 01 19 02`.
- `00:30:53.773` player info-requests target.
- `00:30:54.328` player sends `Attack`.
- `00:30:54.450` server echoes player `Attack`.
- `00:30:55.770` mob missed player with `MissedAttackInfo` (`Unknown1=14`, `Unknown2=6`).
- Player weapon hits continue as `AttackInfo`.
- Mob regular hit at `00:31:06.600` is `AttackInfo`, amount 28, ammo 13, weapon slot 6.
- Mob nano damage at `00:31:11.937` is `HealthDamage`, amount -135, `Stat=FireAC`.
- Death sends player `StopFight`, mob `StopFight`, `NumFightingOpponents=0`, then mob `CharacterAction Death`.
- Corpse follows at `00:31:13.831`.

This confirms normal mob attacks should stay `AttackInfo`, while nano/caster damage needs targeted `HealthDamage` handling.

## Movement Packet Shape Truth

- Official full-duplex captures are authoritative for the mobs listed above.
- Official coordinate `FollowTarget` chase packets use `N3Unknown=0`, `FollowInfoType=1`, `MoveMode=24/25`, and `CoordinateCount=2`.
- Official `SetPos` chase corrections use `N3Unknown=0`, coordinates, `Unknown1=1`, `Unknown2=0`, and `Unknown3=0`.
- Focused mob `SimpleChar:788DA3B9` chased for 78 seconds with 43 unique `FollowTarget` packets, all length 56 and all `N3Unknown=0`, `FollowInfoType=1`, `MoveMode=25`, `CoordinateCount=2`.
- That same focused mob had 5 `SetPos` repaths with `N3Unknown=0`, `Unknown1=1`, `Unknown2=0`, and `Unknown3=0`.
- Focused mob `SimpleChar:788DA3B8` in `20260529-212034` sent repeated coordinate `FollowTarget` packets while the player moved. It did not send `SetPos` before every moving-target repath.
- Focused mob `SimpleChar:788DA3B8` initial chase after player attack sent coordinate `FollowTarget` packets with `MoveMode=24`; later chase updates used `MoveMode=25`.
- Focused mob `SimpleChar:788DA3B8` correction used `FollowTarget` type-2 position-stop, `StopMovingCmd`, `SetPos`, then `FollowTarget` type-2 settle. Local code should use that packet sequence instead of isolated `SetPos`.
- The focused chase did not show mob-owned `CharDCMove`; player `CharDCMove` echo packets were separate from mob movement.
- The focused chase did not show a full-stop `FollowTarget` before each repath. Local runtime should not inject a stop packet into normal chase repaths.
- Local playtest rejected persisting predicted NPC coordinates before each new repath: it made chase warping worse. Keep this path out until a local packet/log trace proves the server coordinate model is correct.
- Private-server coordinate `FollowTarget` packets that use `N3Unknown=1` are private-server evidence only and should not be copied into official-live parity changes without a matching official capture.
- Official type-2 `FollowTarget` packets exist for longer path/settle updates, but the exact local runtime trigger is still pending targeted capture/playtest.
- N3 decompile evidence in `C:\Users\Mike\Documents\AO stripdown\Anarchy Online\decompile_report\n3_enemy_movement_refs.txt` marks `SetWantedDirection` as a fixed three-float direction vector packet. Official capture `20260528-192819` shows S2C `SetWantedDirection` packets for `SimpleChar` mobs, including combat-adjacent packets. Use this packet to publish mob facing/direction changes; do not treat it as a replacement for `FollowTarget` path movement.
- N3 decompile evidence in `C:\Users\Mike\Documents\AO stripdown\Anarchy Online\decompile_report\n3_dynel_enemy_refs.txt` identifies `RelocateDynelsIIR_t` and `DropDynelIIR_t`, but the recovered notes still mark their relocation/lifecycle side effects as unresolved. Do not emit these from CellAO NPC chase until a targeted live trace proves exact semantics.

## Enemy Behavior Evidence Map

Current useful local/source-backed signals:

- Enemy NPCs are dynel identity objects. Movement, combat, death, correction, and despawn work must key off stable `Identity_t` type/instance pairs, not display names or transient target state.
- `CharDCMoveIIR_t` has recovered local-binary fixture coverage in the AO stripdown rebuild: key `0x54111123`, wire size `54`, identity, quaternion, position, action, and tick/delta-style fields. This is evidence for server-authoritative movement playback and replay analysis, not proof that CellAO should synthesize mob-owned `CharDCMove` chase packets.
- Recovered movement action IDs and normalization/remap tests exist in the AO stripdown fixture harness. Use those labels to interpret captured movement states; do not invent action semantics for unknown values.
- `RelocateDynelsIIR_t` has a recovered metadata decoder: key `0x264B514B`, scaled identity-list count, leading identity, identity list, and payload/hash metadata. Its scene-graph side effects and exact live trigger remain unresolved, so it is evidence for bulk reposition/correction research only.
- `DropDynelIIR_t` has a recovered metadata decoder: key `0x47483633`, one identity, and three float fields. Its exact despawn/lifecycle side effects remain unresolved, so local NPC death should keep using the already validated despawn/corpse packet path until a targeted trace proves the `DropDynel` send path.
- `SetWantedDirectionIIR_t`, `n3LocalityUpdateIIR_t`, and `n3TeleportIIR_t` are side-channel evidence around direction, locality, and hard position changes. Use them as context when mining pursuit transitions; do not substitute them for normal chase movement packets.
- Combat event families (`Attack`, `AttackInfo`, `MissedAttackInfo`, `SpecialAttackInfo`, `HealthDamage`, `StopFight`/battle-over style messages) are valid enemy state-transition signals. Normal weapon and melee auto-attacks stay `AttackInfo` only unless a targeted capture proves a different damage family for a specific case.
- Transport traces and live playtests point to server-authoritative world-frame application. Local client simulation, distance leashes, and guessed path corrections must not drive enemy behavior.
- Live observation and captures support sticky aggro/long pursuit. Do not clear threat/fighting only because distance increased or the player moved.
- Existing AO stripdown fixture tests can be used to turn captured movement windows into replay tests. The next useful test layer is a CellAO enemy replay fixture that consumes a decoded movement/combat transcript and asserts state transitions by dynel identity.
- CellAO now has a compiled `EnemyBehaviorContract` that centralizes this evidence as source-backed labels and state transitions. Treat it as the bridge between decoded packet/capture facts and runtime behavior; do not bypass it with new guessed chase helpers.

## Current Code Implications

- Do not clear NPC retaliation just because the player sends `StopFight`; official live keeps the mob attacking.
- Keep normal weapon/mob hits as `AttackInfo` only.
- NPC continuous chase repair should use coordinate `FollowTarget` for normal moving-target repaths. `SetPos` correction behavior must use the full captured correction sequence, not an isolated `SetPos`.
- NPC facing updates should send `SetWantedDirection` with the normalized direction vector when CellAO changes NPC chase heading.
- Local combat chase should not send target-follow packets or short endpoints that rotate around the player center. The current contract is: out-of-range NPCs start a coordinate `FollowTarget`, server-side position advances toward the live target for range math, coordinate repaths are throttled by time and target movement, melee NPCs keep follow state and attack while moving, and ranged/nano-style NPCs stop only when their ranged attack source is in range.
- NPC coordinate chase should not also call generic forward-start/forward-stop movement prediction. The live-style `FollowTarget` packet is the movement instruction; CellAO's generic `UpdateMoveType(1/2)` prediction path double-applies movement and showed up locally as snapping/spiral jitter.
- Melee attacks do not require stopping. NPCs and players can attack while moving in melee range. Only ranged/nano-style attack sources should stop movement when they are in attack range.
- Runtime chase constants for official `FollowTarget` unknown byte, follow info type, run/walk move modes, point count, and coordinate repath throttle are anchored in `EnemyBehaviorContract` and covered by the CellAO combat smoke test.
- Local evidence from `2026-05-30 00:05` showed jitter from mismatched stop/attack thresholds and repeated follow resets. Keep the combat contract simple: out of range follows, in range stops and attacks; do not add separate hold/pending states without a targeted capture proving them.

## Private-Server Mob Capture Notes

Current logged-in capture:

`tools-temp/live-pcaps/private-server-mob-interaction/2026-05-28_21-51-25`

- Started after the private client was already logged in.
- Decoded 43 C2S frame candidates and 0 S2C frames.
- This is valid evidence for player-side mob engage flow only: `CharDCMove`, `CharacterAction`, `LookAt`, `Attack`, and `StopFight`.
- Do not treat this capture as proof that S2C chase packets are absent; the zlib stream was already established before capture started.

Useful full S2C private reference capture:

`tools-temp/live-pcaps/private-server-loot/2026-05-10_22-35-30`

- Started before login and decoded 6839 S2C frames.
- Important S2C counts: `CharDCMove` 2479, `FollowTarget` 583, `SimpleCharFullUpdate` 563, `CharacterAction` 267, `AttackInfo` 145, `StopFight` 129, `Attack` 94, `MissedAttackInfo` 39, `HealthDamage` 47.
- This confirms mob movement/chase data exists in existing private captures and should be mined before changing CellAO chase behavior.
- Observed `FollowTarget` packet sizes include 56, 68, 104, and 164 bytes. The 56-byte chase packets commonly begin after identity with `01 01 19 02`; 68-byte packets use `00 02 15 ...`.
- Example close combat sequence from the private S2C capture around `t=65.777`:
  - `CharacterAction` for the combatant.
  - `Attack` from mob/player actor to target.
  - `FollowTarget` for the mob.
  - More `FollowTarget` updates at short intervals while combat continues.
  - Damage result as `AttackInfo`.

Detailed mining report:

`tools-temp/live-combat-chase-observations/private-server-loot-mob-movement-mining.md`

Packet-body facts from the mining pass:

- `FollowTarget` coordinate payloads are `N3Unknown=1`, `FollowInfoType=1`, then `MoveMode`, `CoordinateCount`, followed by that many `Vector3` coordinates.
- Coordinate `MoveMode=24` is the dominant walking/pathing mode. Coordinate `MoveMode=25` appears for run/chase bursts near combat.
- Coordinate count is not always two. The private capture has counts from 2 through 11:
  - 56-byte packets: 2 coordinates.
  - 68-byte coordinate packets: 3 coordinates.
  - 80, 92, 104, 116, 128, 140, 152, 164-byte packets: 4 through 11 coordinates.
- Type-2 `FollowTarget` stop/settle packets are a different body shape: `N3Unknown=0`, `FollowInfoType=2`, `MoveType=21`, twelve zero bytes, one `Vector3`, flag byte `1`, then the same `Vector3` again.
- The current AOtomation `FollowCoordinateInfo` model only carries current/end coordinates, so it cannot represent captured multi-point paths.
- The current AOtomation `FollowTargetInfo` model does not match the captured type-2 stop/settle body.
- `CharDCMove` S2C packets consistently use `N3Unknown=0`; move types include `1`, `2`, `9`, `11`, `12`, `14`, `22`, and others from normal player movement.
- Normal combat result packets in this private capture use `AttackInfo` fields as: damage, ammo count, weapon slot, target identity, unknown4, hit type, unknown6. Player weapon hits commonly show ammo `-1`, weapon slot `6`, hit type `3` or `4`. Mob hits commonly show ammo `0`, slot `0` or `1`, hit type `3`.

Implemented packet-model repair:

- `FollowCoordinateInfo` can now carry all captured coordinates in a path, while keeping the legacy current/end properties populated.
- `FollowInfoSerializer` reads and writes the real coordinate count.
- `FollowStopInfo` models the captured type-2 stop/settle payload.
- Runtime `FillerFullStopAt` intentionally stays on the older stable target-follow payload shape until a type-2 stop/settle send is validated in-game.
