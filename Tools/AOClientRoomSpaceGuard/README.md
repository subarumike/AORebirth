# AO Client RoomSpace Guard

This runtime guard prevents the Subway `std::bad_cast` crash in both supported AO client builds without modifying launcher-managed files.

The guard:

- selects a patch profile only when `N3.dll` has an approved SHA-256 hash;
- waits for `anarchyonline.exe` from the requested client directory;
- verifies that the loaded `N3.dll` came from the same directory;
- patches only four audited surface-collision callsites (`CalculateClosestPoint`, `GetTileTriangles`, and the two `GetLineIntersection` lookups) with a checked `Space_i` to `n3RoomSpace_t` preflight;
- uses the checked `n3RoomSpace_t*` directly for `GetInsideCell` and the original room lookup, avoiding a second throwing cast;
- returns `nullptr` only to that collision caller when the checked cast or cell lookup fails; that caller already handles the no-room result;
- leaves every other `PosToRoom` call unchanged;
- suspends the client briefly while replacing the single call instruction;
- verifies the in-memory bytes and flushes the instruction cache;
- keeps a bounded telemetry monitor alive until the client exits;
- records only the first guarded failure at each callsite plus one final counter summary;
- atomically preserves the first playfield pointer, exact cast-input room-space pointer,
  event-time vtable, failure reason, current field value, and field-match result before
  later failures can overwrite that evidence.

Run the matching installed launcher shortcut. Logs are written to:

`%LOCALAPPDATA%\AOClientRoomSpaceGuard\guard.log`

The patch exists only in process memory and must be applied on each client launch. Unknown or updated DLL hashes fail closed.

Telemetry messages:

- `PATCH PASS`: all four calls and the telemetry wrapper were installed and verified.
- `MONITOR START`: the minimized guard is watching the patched process.
- `GUARD HIT first`: the first failed cast or invalid-cell result for that callsite,
  including object identity evidence.
- `MONITOR END`: the client exited and the final per-callsite counts were recorded.

The monitor does not log every event, so a long-running client cannot create unbounded
per-hit output.

## Build and deploy

Close guarded AO clients, then run from the AORebirth repository root:

```cmd
cmd /d /c Tools\AOClientRoomSpaceGuard\Build-And-Deploy.cmd
```

The wrapper compiles an optimized x86 executable to a temporary path, runs the wrapper
self-test, validates both installed N3 hashes, and deploys the executable, launchers, and
README to `C:\Funcom\AOClientRoomSpaceGuard`. It never launches AO.

## Confirmed A/B validation

- Old client on live: guarded for more than 20 minutes in the failing room; the unguarded
  client crashed almost immediately. Crash return `N3+0x148BB` is the instruction after
  guarded call `N3+0x148B6`.
- New client on AORebirth: guarded in the same failing condition without a crash; the
  unguarded client crashed almost immediately. Crash return `N3+0x16149` is the instruction
  after guarded call `N3+0x16144`.

This validates the current mitigation. It does not yet identify the earlier lifecycle
operation that makes the room-space object incompatible.
