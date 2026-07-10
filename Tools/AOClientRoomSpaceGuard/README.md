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
- exits after one successful patch.

Run the matching installed launcher shortcut. Logs are written to:

`%LOCALAPPDATA%\AOClientRoomSpaceGuard\guard.log`

The patch exists only in process memory and must be applied on each client launch. Unknown or updated DLL hashes fail closed.
