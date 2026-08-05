# PF1931 Temple post-login client crash reconciliation

Date: 2026-08-04
Starting SHA: `df02982366da8d7cddf4cdd39efe01fbc2d7fa9e`

## Failure boundary and crash timeline

Soldier (character 22) loaded from the database, authenticated, completed the
basic PF1931 world-entry batch, and then the official client crashed in
`BinaryStream.dll` through `DatabaseController.dll`. The preserved baseline
window starts with the PF647 Temple collision at approximately `16:41:49`, then
records a disconnect at `16:41:56` and PF1931 reconnects at `16:41:57`,
`16:42:21`, and `16:43:31`. Each reconnect reported four valid basic entry
objects and no invalid objects before the client disappeared.

Two controlled reproductions produced the same call stack and invariant stack
tuple `0000C77D 00000001 000F4241 0000078B`. The attempted-read address varied
(`0000FF00`, then `F800F800`), which is consistent with the client parsing the
wrong payload shape rather than a transport write failure.

## Correlated post-login sequence

The production-safe evidence boundary was recorded with a per-connection
correlation ID and packet ordinal, serialized length and SHA-256 prefix, queue
transition, write return, flush return, socket state, target, and playfield.
No raw unrestricted packet capture or credentials were retained.

The crashing full-door session was correlation
`9ce1c2bf1875406c83008348925c78a7`. Its initial order was:

1. `ChatServerInfo`, `PlayfieldAnarchyF`, `GameTime`.
2. Character stats/action and Soldier `SimpleCharFullUpdate`.
3. Soldier weapon, `FullCharacter`, level/stats, and special-attack weapon.
4. Three visible Cultists, each with `SimpleCharFullUpdate`, weapon, and
   `CharInPlay`.
5. Door ordinals 1-43, serialized as packet ordinals 26-68.
6. Soldier `CharInPlay`, appearance, and final stat packets.

Every door packet returned from write and flush while the socket still appeared
connected. The server observed the disconnect later; no serialization, write,
flush, or ZoneEngine fatal error identified a door boundary.

## Door evidence classification

The authoritative PF1931 statel inventory has 44 Door identities. The exterior
link `C024078B` is lifecycle-owned and excluded from initial dynamic replay. The
remaining 43 official internal statels are captured closed, unique, valid,
44-byte full envelopes, initial-entry eligible, and replayed once per external
arrival. No record was reclassified or removed by this repair.

| Ordinal | Runtime Door instance | Classification |
|---:|---:|---|
| 1 | -1071249525 | captured closed; internal; initial eligible |
| 2 | -1071183989 | captured closed; internal; initial eligible |
| 3 | -1071118453 | captured closed; internal; initial eligible |
| 4 | -1071511669 | captured closed; internal; initial eligible |
| 5 | -1071446133 | captured closed; internal; initial eligible |
| 6 | -1071773813 | captured closed; internal; initial eligible |
| 7 | -1071708277 | captured closed; internal; initial eligible |
| 8 | -1071642741 | captured closed; internal; initial eligible |
| 9 | -1071577205 | captured closed; internal; initial eligible |
| 10 | -1072232565 | captured closed; internal; initial eligible |
| 11 | -1072167029 | captured closed; internal; initial eligible |
| 12 | -1072101493 | captured closed; internal; initial eligible |
| 13 | -1072691317 | captured closed; internal; initial eligible |
| 14 | -1072625781 | captured closed; internal; initial eligible |
| 15 | -1072560245 | captured closed; internal; initial eligible |
| 16 | -1072494709 | captured closed; internal; initial eligible |
| 17 | -1072429173 | captured closed; internal; initial eligible |
| 18 | -1072363637 | captured closed; internal; initial eligible |
| 19 | -1072298101 | captured closed; internal; initial eligible |
| 20 | -1071970421 | captured closed; internal; initial eligible |
| 21 | -1071904885 | captured closed; internal; initial eligible |
| 22 | -1071839349 | captured closed; internal; initial eligible |
| 23 | -1072035957 | captured closed; internal; initial eligible |
| 24 | -1073150069 | captured closed; internal; initial eligible |
| 25 | -1073084533 | captured closed; internal; initial eligible |
| 26 | -1073018997 | captured closed; internal; initial eligible |
| 27 | -1072953461 | captured closed; internal; initial eligible |
| 28 | -1072887925 | captured closed; internal; initial eligible |
| 29 | -1072822389 | captured closed; internal; initial eligible |
| 30 | -1072756853 | captured closed; internal; initial eligible |
| 31 | -1071315061 | captured closed; internal; initial eligible |
| 32 | -1070921845 | captured closed; internal; initial eligible |
| 33 | -1073281141 | captured closed; internal; initial eligible |
| 34 | -1073215605 | captured closed; internal; initial eligible |
| 35 | -1073739893 | captured closed; internal; initial eligible |
| 36 | -1073674357 | captured closed; internal; initial eligible |
| 37 | -1073608821 | captured closed; internal; initial eligible |
| 38 | -1073543285 | captured closed; internal; initial eligible |
| 39 | -1073477749 | captured closed; internal; initial eligible |
| 40 | -1073412213 | captured closed; internal; initial eligible |
| 41 | -1073346677 | captured closed; internal; initial eligible |
| 42 | -1071052917 | captured closed; internal; initial eligible |
| 43 | -1070987381 | captured closed; internal; initial eligible |

Totals: captured internal records 43; initial-entry eligible 43;
interaction-only 0; visibility-scoped 0; unsupported 0. The exterior lifecycle
door remains separate from these totals.

## Binary isolation matrix

| Run | Door replay | PF1931 init | Result |
|---|---|---|---|
| Baseline | all 43 | malformed generated-playfield identity | client crash; attempted read `0000FF00` |
| Control | none | malformed generated-playfield identity | identical client crash; attempted read `F800F800` |
| Repair proof | none | ordinary static-playfield shape | stable for about 26 seconds; normal movement/combat; exited to PF647 |
| Full acceptance | all 43 | ordinary static-playfield shape | stable for about 31 seconds; all 43 flushed; exited to PF647 |

Control correlation: `d59bd60a4e4c44bd9f75d7ff787cc8ef`.  Fixed/no-door
correlation: `07085c8888084922bfc8923f9daf96b3`.  Fixed/full-door
correlation: `563c32a35d5c4cf89d572b16fe946446`.

## Exact defect and repair

Commit `f91d5ec6` added a PF1931-specific `PlayfieldAnarchyF` override that set
`PlayfieldId1.Type` to `0xC79E` (generated-building data) but left
`GeneratorPayload` null. The serializer consequently emitted the ordinary
static-playfield tail. The official client selected its generated-building
database parser from the advertised identity type and consumed the ordinary
tail as generated data, reaching the `0xC77D` generated-record boundary visible
in both crash stacks before dereferencing invalid data.

The earlier 43-door merge `9ca72e8a` was not causal. Its runtime and generated
door artifacts remain authoritative and unchanged.

The repair removes only the unsupported PF1931 override, restoring the normal
`Playfield1` static-resource identity and standard tail. A serializer guard now
rejects generated-playfield identities `0xC79E`/`0xC79F` when the exact generator
payload is absent. The malformed packet fingerprint was length 86,
SHA-256 prefix `F0D742D4AAC78208`; the repaired static packet is length 86,
SHA-256 prefix `517B86646697AE71`.

## Marcus PF6553 disposition

The separate log flood was unrelated to Soldier and PF1931. PF6553 heartbeat
ownership called `MarcusPadAmbientCombat.LinkFight`, which invoked the shared
attack-start packet factory even though the Marcus contract has no complete
captured attack-start/special-weapon context. The exception occurred before the
fight timers were installed, so the heartbeat retried the same invalid start.

No context was fabricated. Link ownership now fails closed before calling any
attack-start factory unless both Marcus and robot contracts have all contexts
required by the exact calls. Supported stationary mesh and burning-robot visual
behavior remains. Focused tests preserve the factory calls behind the guard,
and a clean engine restart produced no repeated Marcus exception.

The Marcus source correction is an auxiliary input to governed active-combat
coverage. The repository writer regenerated only the affected active-coverage
payload and generation manifest; all other generated cohort files remained
byte-identical. Current validation passes at cohort identity
`f8f3dcd2c3ff218e419e787ef0348770ab5a10074a9a9bb2c7c577afb5ff6efd`.

## Acceptance evidence

- Debug build: PASS.
- Focused N3 recovered contracts: 21/21 PASS.
- Focused Temple door/runtime contracts: 15/15 PASS.
- Focused captured combat factory contracts: 38/38 PASS.
- Live Soldier fixed/no-door login and PF1931-to-PF647 exit: PASS.
- Live Soldier full 43-door PF647-to-PF1931 entry, 31-second residency, and
  PF1931-to-PF647 exit: PASS.
- Mandatory integration gate: PASS twice on the unchanged final commit/tree.
