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

The decoded chase windows include coordinate `FollowTarget` packets. `SetPos` appears as a correction/settle packet in some captures; it is not sent before every moving-target update. Current CellAO runtime does not treat these rows as a license to stream coordinate repaths, because local playtests showed that model as visible hopping instead of smooth follow.

Example, mob `7888E378` at `2026-05-29T00:11:47.9467311Z`:

- `SetPos` for `7888E378`
- `FollowTarget` for `7888E378`, packet length 56
- Follow coordinate header after identity is `00 01 19 02`

Example, mob `788DA3B8` from capture `20260529-212034` while the player moved:

- Repeated coordinate `FollowTarget` packets for `788DA3B8`, packet length 56
- Follow coordinate header after identity is `00 01 19 02`
- Earlier broad capture notes showed occasional `SetPos` corrections on other mobs; do not apply those to this focused target unless the same target window shows them.

Correction sequence observed for `788DA3B8` at `2026-05-30T02:21:01.5794475Z`:

- `FollowTarget`, length 56, type-2 position-stop body: `00 02 19 ...`
- `StopMovingCmd`, length 41, body after identity: `00 00000001 C700000D 00000001`
- `SetPos`, length 47, body after identity: `00 <Vector3> 01 00000000 00`
- `FollowTarget`, length 68, type-2 settle body: `00 02 15 <mob identity> 00000000 <Vector3> 01 <Vector3>`

That sequence is from a different official window and remains evidence, but the later focused killed-target window (`SimpleChar:788DA3BD`, `2026-05-30T21:42:23Z` to `21:43:19Z`) showed only short type-2 `FollowTarget` position correction before later coordinate rows. CellAO must not send a bare `SetPos` as a chase repair, and should not default local NPC chase to the full correction sequence without target-specific evidence.

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
- Official `SetPos` chase corrections in broad captures use `N3Unknown=0`, coordinates, `Unknown1=1`, `Unknown2=0`, and `Unknown3=0`; focused target samples should decide whether local runtime emits them.
- Focused mob `SimpleChar:788DA3B9` chased for 78 seconds with 43 unique `FollowTarget` packets, all length 56 and all `N3Unknown=0`, `FollowInfoType=1`, `MoveMode=25`, `CoordinateCount=2`.
- That same focused mob had 5 `SetPos` correction rows with `N3Unknown=0`, `Unknown1=1`, `Unknown2=0`, and `Unknown3=0`.
- Focused mob `SimpleChar:788DA3B8` in `20260529-212034` sent repeated coordinate `FollowTarget` packets while the player moved. It did not send `SetPos` before every moving-target row.
- Existing chase fixtures include sub-second coordinate `FollowTarget` rows: official melee chase re-entered chase roughly 330ms after an in-range sample, and the private loot melee window has adjacent `FollowTarget` samples about 311ms and 412ms apart. These remain replay evidence, not a local repath-loop contract.
- Focused mob `SimpleChar:788DA3B8` initial chase after player attack sent coordinate `FollowTarget` packets with `MoveMode=24`; later chase updates used `MoveMode=25`.
- Focused killed target `SimpleChar:788DA3BD` correction used short type-2 `FollowTarget` position frames (`00 02 19`, `0,0,0x40000000`, one coordinate, trailing `00`) before later coordinate rows. Keep that smaller shape as correction evidence first; keep the larger `StopMovingCmd`/`SetPos`/type-21 settle sequence as separate evidence until a matching target window requires it.
- Current local normal chase does not inject the short type-2 position frame. Local circle tests showed that using a sharp-turn correction gate as a default chase repair caused visible snap/jitter.
- Local test at `2026-05-31 00:45` showed SimpleChar NPC target-follow broke visible chase after one `target-follow` row. Treat target-follow as unproven for NPC chase unless a future official/source trace proves that exact SimpleChar use.
- Current local melee chase is back on coordinate `FollowTarget` rows with `N3Unknown=0`, `FollowInfoType=1`, `MoveMode=25`, and `CoordinateCount=2`; the unresolved problem is how often and from which authoritative start/destination to send them.
- The focused chase did not show mob-owned `CharDCMove`; player `CharDCMove` echo packets were separate from mob movement.
- The focused chase did not show a full-stop `FollowTarget` before each moving-target row. Local runtime should not inject a stop packet into normal chase.
- Local playtest rejected persisting predicted NPC coordinates before each new coordinate update: it made chase warping worse. Keep this path out until a local packet/log trace proves the server coordinate model is correct.
- Local playtest on 2026-05-30 showed `NPCCHASE dest` drifting away from the latest logged player `CharDCMove` when using full generic prediction. Raw-only sampling then showed the opposite failure: NPCs waited for the next client movement update before chasing a continuously moving player. NPC chase/range decisions should use a small bounded projection anchored to player `RawCoordinates`, not an unbounded `Character.Coordinates()` target.
- Local playtest on 2026-05-30 also showed visible jitter from tiny melee repaths at about `1.50m` to `1.83m`, right on top of the melee follow stop distance. This is part of why normal chase moved away from coordinate repath packets.
- Private-server coordinate `FollowTarget` packets that use `N3Unknown=1` are private-server evidence only and should not be copied into official-live parity changes without a matching official capture.
- Official type-2 `FollowTarget` packets exist for longer path/settle updates, but the exact local runtime trigger is still pending targeted capture/playtest.
- N3 decompile evidence in `C:\Users\Mike\Documents\AO stripdown\Anarchy Online\decompile_report\n3_enemy_movement_refs.txt` marks `SetWantedDirection` as a fixed three-float direction vector packet. Official capture `20260528-192819` shows S2C `SetWantedDirection` packets for `SimpleChar` mobs, including combat-adjacent packets, but the focused normal melee chase did not show this packet. Keep it out of normal melee chase until a target-specific capture proves the trigger.
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
- Coordinate chase evidence uses the captured coordinate `FollowTarget` shape: N3 unknown byte `0`, `FollowInfoType=1`, `MoveMode=25`, `CoordinateCount=2`, current NPC coordinate, and destination coordinate. Local melee chase no longer treats that as a license to redirect the visible path every tick.
- Correction behavior must stay capture-specific until a targeted runtime trigger is proved; never send an isolated `SetPos` as a chase repair.
- Normal melee chase currently uses coordinate `FollowTarget`; `SetWantedDirection` remains a decoded helper for capture-specific use, not a default normal chase side-channel.
- Local combat chase should not send short endpoints that rotate around the player center. The current contract is: melee NPCs follow the player position without stop-distance destination shaping, keep attacking while moving, and ranged/nano-style NPCs stop only when their ranged attack source is in range.
- NPC coordinate chase should not also call generic forward-start/forward-stop movement prediction. The `FollowTarget` packet is the visible movement instruction; CellAO's generic `UpdateMoveType(1/2)` prediction path double-applies movement and showed up locally as snapping/spiral jitter.
- Player target sampling for NPC chase/range should stay anchored to the raw coordinate stored by incoming `CharDCMove`, with only a capped projection from the existing movement predictor. Raw-only sampling lagged until the player stopped; unbounded prediction made local NPCs chase projected points instead of the last authoritative client position.
- Melee attacks do not require stopping. NPCs and players can attack while moving in melee range. Only ranged/nano-style attack sources should stop movement when they are in attack range.
- Runtime chase constants for official `FollowTarget` unknown byte, follow info type, run/walk move modes, point count, and server-side chase speed are anchored in `EnemyBehaviorContract` and covered by the CellAO combat smoke test.
- The previous local throttle/visible-segment model is retired. Local `2026-05-30` logs showed that even slower coordinate repaths still hopped if packet starts came from a separate guessed-visible position instead of the same authoritative motion segment used by combat range.
- Local `ao test 3` on `2026-05-30 23:31-23:32` showed the next concrete failure: while already inside the `8m` melee gate, CellAO still emitted about one `coordinate-update` every `0.36s`; `79/113` sampled updates reversed direction by more than `90` degrees as the player circled the mob. In-range melee now suppresses fresh motion-segment packets; out-of-range chase re-enables them.
- Local `2026-05-31 00:04-00:05` playtest then showed the opposite edge: chase updates resumed mostly at `8m+`, so the mob waited too long before following. Melee attack validity and close visual follow hold are now split: `8m` remains the conservative hit gate, while `3m` is the local close-hold distance that suppresses tiny circling updates.
- Local `ao test 4` / `2026-05-31 00:09-00:10` showed the `3m` hold by itself was too narrow: `70` chase rows, `62` under `8m`, `50` under `4m`, and `38/69` direction deltas over `90` degrees. The next local test at `00:37-00:38` still showed repeated `3m` coordinate redirects and heartbeat exceptions on stale follow-target lookup. The target-follow experiment after that evidence failed locally, so the remaining repair must compare official coordinate rows against local coordinate rows rather than switching packet families.
- Nearby path reversals no longer use a default short type-2 `FollowTarget` correction gate. The captured correction packets remain replay evidence, but local video/logs showed the default gate made close circular movement visibly snap.
- Combat test mobs explicitly advertise `Run` movement state and `runspeed=400`, matching CellAO's `6.0 m/s` server chase speed through the existing `Character` runspeed formula. This keeps the client's FollowTarget interpolation speed aligned with the server-authoritative chase position.
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
- Runtime melee combat chase currently uses coordinate `FollowTarget` with hidden server-authority position updates; type-2 stop/settle sends stay capture evidence until validated in-game.
