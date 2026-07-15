# Current Task

## Current Focus

Complete the Subway dungeon, resource/playfield `127`, through incremental capture-backed implementation and live validation.

## Done in this slice

- Synced and consolidated `origin/master` without discarding the existing Subway work.
- Preserved the confirmed PF127 entrance reverse exit while removing the unproven position-only proxy exit that force-zoned players from inside the Subway.
- Preserved ordinary PF127 interior door statels and canonicalized inbound proxy travel to entrance door `0xC006007F`.
- Preserved both the incoming inventory-use regressions and the local Subway door regressions in the shared test file.
- Added the completed Disobedient Bot combat packet byte-vector regression.
- Retained the incoming Alien XP, stim/recharger, combat, and inventory changes as merged work; Alien XP is not the active Subway priority.
- Captured two matching official-live client dumps for the `VetoPosition -> PosToRoom` crash, proved the exception is `std::bad_cast` (`Bad dynamic_cast!`), and extended the existing non-throwing RoomSpace wrapper to the fifth audited callsite in both client profiles. The rebuilt proxy package is installed in both approved client directories; in-game regression validation remains pending.

## Remaining

1. Re-test the official-live location that repeatedly produced `N3!n3Playfield_t::PosToRoom+0x44` and confirm the fifth RoomSpace callsite prevents the crash.
2. Live-validate Vergil Aeneid retaliation after the hostile-NPC suppression-gas fix.
3. Live-validate that only the confirmed Subway entrance door exits PF127 and that ordinary interior doors no longer force-zone the player.
4. Continue room-by-room Subway population, encounter, object, loot, and progression work from completed captures.

## Constraints

- Use official-live capture evidence for Subway behavior and content.
- Do not reintroduce a position-only PF127 proxy exit.
- Do not remove ordinary Subway interior door statels.
- Do not treat provisional Alien XP reward values as capture-confirmed.
