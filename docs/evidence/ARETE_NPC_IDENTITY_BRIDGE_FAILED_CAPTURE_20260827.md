# Arete NPC Identity Bridge Failed Capture Audit

## Preserved source

- Capture ID: `20260827-213046`
- Folder: `Captures\Arete Landing [PF 6553] - 20260827-213046`
- Epoch: `20260827-213046-zone-0001`, inclusive ordinals `0..1259`, valid and finalized
- Start: `2026-08-28T02:30:46.6455070Z`
- Stop requested: `2026-08-28T02:33:01.3189149Z`
- End: `2026-08-28T02:33:10.0588154Z`
- Runtime playfield: type `40016`, instance `1810432`
- Live `Playfield.ModelIdentity`: type `51102`, instance `6553`

The capture folder is retained unchanged. Raw integrity is clean: 1,259 inbound
packets, zero outbound packets, 1,259 indexed/logged raw records, zero raw
write errors, zero projection errors, and zero drain timeouts.

## Packet and bridge totals

| Measure | Result |
| --- | ---: |
| SCFU packets | 17 |
| SCFU decode failures / incomplete | 0 / 0 |
| Unique raw SCFU identities | 13 |
| SCFU identities linked live | 7 |
| SCFU identities received but unlinked | 5 client-present + 1 raw-only |
| Ordinary Stat packets | 350 |
| Stat decode failures / incomplete | 0 / 0 |
| Unique raw Stat identities | 5 |
| Stat identities linked live | 5 |
| Bridge snapshots | 2,202 |
| Unique client NPC identities | 38 |
| Unmatched raw SCFU identity | `(50000:7A38C2DC)` |
| Unmatched raw Stat identities | none |

No capture-layer dropped-work or queue-depth metrics existed in this version.
The raw/index counts and error counters provide no evidence of packet loss.

## Per-identity classification

`packet-not-received` means the server did not transmit that packet class for
the identity during the preserved observation window. It is not a decode
failure.

| Runtime identity | Snapshots | SCFU status | Stat status |
| --- | ---: | --- | --- |
| `(50000:79F70628)` | 74 | packet-not-received | packet-received-decoded-linked |
| `(50000:79F70629)` | 74 | packet-not-received | packet-not-received |
| `(50000:79F70634)` | 74 | packet-not-received | packet-received-decoded-linked |
| `(50000:79F70635)` | 74 | packet-not-received | packet-received-decoded-linked |
| `(50000:79F7063C)` | 74 | packet-not-received | packet-not-received |
| `(50000:79F7063D)` | 74 | packet-not-received | packet-not-received |
| `(50000:79F70650)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A1F7DAA)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A1F7DB2)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A1F7DB5)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A2D1657)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A2D7512)` | 34 | packet-received-decoded-linked | packet-not-received |
| `(50000:7A35116B)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A35116D)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A351173)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A35117C)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A368664)` | 65 | packet-received-decoded-linked | packet-not-received |
| `(50000:7A371DEC)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A371E36)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A38C008)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A38C009)` | 16 | packet-received-before-snapshot | packet-not-received |
| `(50000:7A38C1D8)` | 27 | packet-received-before-snapshot | packet-not-received |
| `(50000:7A38C1DF)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A38C1E4)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A38C216)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A38C2C6)` | 46 | packet-not-received | packet-not-received |
| `(50000:7A38C2C8)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A38C2C9)` | 47 | packet-not-received | packet-not-received |
| `(50000:7A38C2CD)` | 74 | packet-not-received | packet-not-received |
| `(50000:7A38C2CE)` | 34 | packet-not-received | packet-received-decoded-linked |
| `(50000:7A38C2CF)` | 70 | packet-received-decoded-unlinked | packet-not-received |
| `(50000:7A38C2D0)` | 54 | packet-received-decoded-linked | packet-not-received |
| `(50000:7A38C2D1)` | 40 | packet-received-before-snapshot | packet-not-received |
| `(50000:7A38C2D4)` | 17 | packet-received-decoded-linked | packet-not-received |
| `(50000:7A38C2D5)` | 16 | packet-received-decoded-linked | packet-not-received |
| `(50000:7A38C2D6)` | 15 | packet-received-decoded-linked | packet-received-decoded-linked |
| `(50000:7A38C2D7)` | 14 | packet-received-decoded-linked | packet-not-received |
| `(50000:7A38C2DA)` | 5 | packet-received-before-snapshot | packet-not-received |

The four `before-snapshot` SCFUs and the same-ordinal decoded/unlinked SCFU for
`(50000:7A38C2CF)` were lost by the old first-discovery evidence floor. The
remaining 26 client identities never received an SCFU. All five identities
that received ordinary Stat packets linked correctly; the other 33 did not
receive an ordinary Stat packet.

## Performance finding

The old recorder performed a complete nearby scan about once per second and
emitted an unchanged snapshot for every NPC. Snapshot counts per identity were
5 to 74, with a median of 74. Every snapshot enumerated all 626 distinct
client Stat values, for an estimated 1,378,452 main-thread `GetStat` calls.
The live JSONL reached 170,243,941 bytes and every two-second flush rewrote the
entire growing artifact while holding the bridge lock. The final replay JSON
was 90,835,661 bytes.

This polling plus whole-file rewrite path explains both the unplayable client
and immediate recovery after capture stop. Raw packet writing occurred before
bridge projection and remained complete.

## Model identity finding

`Playfield.ModelIdentity` was not absent. The live native wrapper exposed
type `51102`, instance `6553`. The recorder serialized that raw identity but
correctly refused to promote it as `base_playfield_direct`, because only type
`1000014` has proven direct base-playfield semantics. Fixtures had not covered
the live non-`1000014` wrapper result, and the old epoch model did not record an
explicit state/retry/final reason.
