# Grid Terminal C0040320 Capture Result

## Scope

This note records the capture-backed behavior for using `Terminal:C0040320` in Borealis.

## Evidence

- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-073837/events.log:203` identifies `Terminal:C0040320` as `Enter The Grid` at `(636.4026, 66.81002, 728.8094)`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260611-005202/events.log:11361` and `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260613-170220/events.log:3938` show `GenericCmd Use` targeting `Terminal:C0040320`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260611-005202/packets.hex.log:8489` shows the server response as `N3Teleport` to playfield proxy `0xC79E:00000098`, which is playfield `152`, with destination `(634.302, 66.810, 726.667)`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260613-170220/packets.hex.log:3143` shows the same flow to playfield proxy `0xC79E:00000098`, with destination `(636.604, 66.810, 726.744)`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-091447/packets.hex.log:223` shows the grid handoff `PlayfieldAnarchyF` with `CharacterCoordinates=(234.3062, 3.7750, 212.8138)`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-091447/events.log:645` shows the first local `SimpleCharFullUpdate` in playfield instance `1077254` at `Position=(234.3062, 3.775, 212.8138)` and `Heading=X: 0 | Y: 1 | Z: 0 | W: -4.371139E-08`.
- `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260621-091447/events.log:785` shows the first local post-load `CharDCMove FullStop` at the same position and heading.
- The live captures did not show a separate successful `GenericCmd` acknowledgement packet between terminal use and teleport.
- The destination carried in `N3Teleport` is source-side terminal context and is not the grid landing transform.
- No post-transfer `SetPos` correction was captured.
- `tools-temp/playfield-teleport-audit.csv:967` maps the same raw instance through older statel-door audit data to playfield `2063`; this conflicts with the live terminal-use captures and is not used for this terminal route.

## Result

Use of Borealis `Terminal:C0040320` should route through the existing playfield teleport flow to playfield `152` and land at `(234.3062, 3.7750, 212.8138)` with heading `(0, 1, 0, -4.371139E-08)`. The source-side terminal position must not be reused as the grid spawn position.

## Unknowns

No captured failure branch was found for side, level, expansion, paid access, distance, or other access checks. No new checks are implemented from this capture.
