# Grid Terminal C0040320 Capture Result

## Scope

This note records the capture-backed behavior for using `Terminal:C0040320` in Borealis.

## Evidence

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-073837/events.log:203` identifies `Terminal:C0040320` as `Enter The Grid` at `(636.4026, 66.81002, 728.8094)`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260611-005202/events.log:11361` and `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260613-170220/events.log:3938` show `GenericCmd Use` targeting `Terminal:C0040320`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260611-005202/packets.hex.log:8489` shows the server response as `N3Teleport` to playfield proxy `0xC79E:00000098`, which is playfield `152`, with destination `(634.302, 66.810, 726.667)`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260613-170220/packets.hex.log:3143` shows the same flow to playfield proxy `0xC79E:00000098`, with destination `(636.604, 66.810, 726.744)`.
- The live captures did not show a separate successful `GenericCmd` acknowledgement packet between terminal use and teleport.
- `tools-temp/playfield-teleport-audit.csv:967` maps the same raw instance through older statel-door audit data to playfield `2063`; this conflicts with the live terminal-use captures and is not used for this terminal route.

## Result

Use of Borealis `Terminal:C0040320` should route through the existing playfield teleport flow to playfield `152`, preserving the character's current position and heading. The existing teleport flow emits the observed `N3Teleport` and despawn/handoff sequence.

## Unknowns

No captured failure branch was found for side, level, expansion, paid access, distance, or other access checks. No new checks are implemented from this capture.
