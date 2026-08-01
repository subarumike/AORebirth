# PF1931 Temple dynamic-door completion (2026-07-31)

> **PF1931 status authority (2026-08-01):** Historical evidence/provenance only. Current PF1931 status is the [Temple full-corpus completion matrix](TEMPLE_FULL_CORPUS_COMPLETION_20260801.md); any PF1931 completion, blocker, or test-count statement below is superseded by that matrix.

## Scope and sources

- Starting repository SHA: `9ca72e8a4c3102322f6d9060691dd8a32c83019d`.
- Complete `tools-temp/AOSharpLiveCapture/bin/Debug/captures` raw packet corpus.
- `263` current-realm PF1931 `DoorStatusUpdate` records over `43` runtime
  identities, `68` older records with a different allocation, and all three
  current-realm open snapshots.
- Client `18.8.62_EP1` `Gamecode.dll` and `N3.dll`, using the checked-in Ghidra
  function map plus targeted disassembly of the mapped routines.
- Official `AORebirth/Datafiles/playfields.dat` and
  `pf1931-dungeon-geometry.json` room graph.

The movement projection had omitted outbound `CharDCMove` (`0x54111123`). The
shared decoder now reads the authoritative 70-byte shape (identity, move type,
quaternion, coordinates, tick, and auxiliary values). Regenerating the three
open sessions produced `1,730` complete movement rows and zero decode errors.

## Recovered contract

There is no client door-use or `GenericCmd` request adjacent to any PF1931 open
snapshot. Each open follows an outbound movement sample crossing the official
door statel:

| Session | Door | Open UTC | Player position | Official door | Distance |
| --- | ---: | --- | --- | --- | ---: |
| `20260721-230426` | `277657507` | `04:05:04.7846872` | `244.687,13.011,318.195` | `244.999,13.011,317.985` | `0.376m` |
| `20260721-041439` | `277657500` | `09:16:05.1772122` | `158.056,31.011,267.219` | `158.013,31.009,266.992` | `0.231m` |
| `20260721-042139` | `277657491` | `09:25:04.5109558` | `91.074,13.011,273.746` | `91.012,13.010,273.992` | `0.253m` |

The last pre-contact sample in the tightest trace is `0.522m` away. Together
the crossings select the official half-meter contact cell: three-dimensional
distance `<= 0.5m`, with no actor-name or captured-identity mapping.

The complete raw corpus has four same-identity open-to-close pairs (`5.293s`,
`40.972s`, `148.526s`, and `204.701s`). The three long pairs span visibility or
room re-entry. The continuously observed doorway lifecycle closes at `5.293s`,
selecting the server-owned integral five-second hold. A door remains open
through the hold and closes on the heartbeat after its triggering recipient has
left contact. The triggering character is the packet recipient in every raw
envelope. State is therefore tracked per recipient: two players each receive
one transition, while repeated movement inside the contact cell produces no
duplicate packet.

Client `DoorStatusUpdateIIR_t::PollStatus` at `Gamecode.dll:0x100A015A`
dispatches the second serialized subclass byte (`Unknown3`) to the open path
(`0x10080319`, then imported `n3RoomMonitor_t::DoorOpened`) or close path
(`0x100803B4`, then `DoorClosed`). `Unknown2` instead selects a separate visual
parameter path. The emitted packet therefore keeps the captured constants and
changes only `Unknown3` between `1` and `0`.

## Official identities and ownership

Direct `playfields.dat` validation finds `44` PF1931 `Door` statels. The
official room graph marks `C024078B` as EntryHall's `roomIndex=-1` exterior
link. Excluding it produces exactly `43` unique internal automatic doors. The
runtime uses those official identities and positions and rejects any count
other than `43`.

One playfield-owned proximity runtime runs from the existing heartbeat; it
creates no timer or worker. Entry, re-entry, and death-respawn clear the
recipient state and send one exact closed snapshot for each internal door.
Characters removed from the playfield are pruned, and playfield disposal clears
all definitions and recipient state.

## Validation

- `TempleDoorStatusRuntimeTests`: `6/6` pass.
- PF1931 official resource validation: `44` door statels, `1` exterior,
  `43/43` unique internal doors.
- Temple ordinary, named, lifecycle, combat packet/profile, collision, and
  navigation focused regression suites: pass.
- Debug build: pass.

The repository-wide test run is not a clean gate in the concurrent worktree:
`872/917` passed and `45` unrelated Arete/generated-content/formula tests failed.
The focused Temple gates above all pass.
