# Private City Montroyal Entry Capture Result

Date: 2026-06-22

Capture source: `tools-temp/AOSharpLiveCapture/bin/Debug/captures/20260622-101935`

## Live Capture Evidence

- Capture was complete and valid.
- Public entry playfield was PF `655`; private destination was private-city instance `1196045`.
- Entry was movement-triggered. No `GenericCmd Use` was captured for the entry.
- Movement stopped near `(3140.412, 51.54391, 799.8611)`, followed by `SocialStatus=4`, `N3Teleport`, and `PLAYFIELD-INIT 1196045`.
- Spawned private-city dynels were `Terminal:574B84AB`, `CityController:9C6010`, and `Door:108ECA90`.
- The private-city instance `1196045` ready block included empty `PlayfieldAllTowers` and empty `PlayfieldAllCities`.
- `PlayfieldAnarchyF` for instance `1196045` uses private-city proxy `C79E:138A`; using `C79E:177A` makes the client load resource `6010` and label the instance as `Serenity Islands`.
- Local smoke with PF proxy resource `6010`, zone `1828`/`1829`, area `Serenity Islands` invalidated the earlier `(1155.1, 5.0, 1218.0)` spawn and `(1168.7, 7.9, 1221.5)` exit coordinates as Serenity-derived, not Montroyal-derived.

## Implemented Compatibility Layer

- Added a narrow movement-triggered Montroyal entry mapping from PF `655` near the captured coordinate to private-city instance `1196045`.
- Sent the captured `SocialStatus=4` stat update before the teleport.
- Preserved the existing dynamic private-city candidate handling and extended it to the internal `Playfield` identity type used by server teleports.
- Sent empty towers/cities for private-city instance `1196045` while leaving the previously captured non-empty city payload for the older private-city capture path.
- Added a Montroyal `PlayfieldAnarchyF` generator payload variant containing `Terminal:574B84AB`, `CityController:9C6010`, and `Door:108ECA90`.
- Allowed captured dynamic private-city instances without checked-in `PFData` to initialize with empty statels/vendors instead of throwing during zone creation.
- Restored the entry destination to the captured Montroyal landing `(530.0042, 163.2545, 580.9957)` using server coordinate order `x, y, z`.
- Skipped wall collision checks for dynamic instances with no `PFData`; without that, the heartbeat repeatedly threw in `WallCollision.CheckCollision` for instance `1196045`.
- Moved the provisional exit trigger to the captured Montroyal shuttle-door area near `(530.4664, 160.6381, 590.7054)` that returns to PF `655`.
- Adjusted the PF `655` exit landing to the user-confirmed safe public location `(3138.2, 51.4, 812.8)` using server coordinate order `x, y, z`.
- Corrected the PF proxy for private-city instance `1196045` from the older private-city resource `C79E:177A` to the Montroyal capture value `C79E:138A`.

## Not Implemented

- Guest-key terminal use.
- CityController use.
- CityAdvantages or OrgClient behavior.
- City ownership, organization gating, city-bank, guest-card validation, persistence, or item semantics.

## Validation Status

- `git diff --check`: PASS.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS after stopping running engines that locked `ZoneEngine.exe` during rebuild.
- `cmd /d /c restart-engines.cmd`: PASS.
- First user smoke reached the entry trigger, then `ZoneEngine.out.log` showed `KeyNotFoundException` in `Playfield` construction for private-city instance `1196045`; the empty dynamic private-city `PFData` fallback was added and rebuilt.
- Post-fix `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- Post-fix `cmd /d /c restart-engines.cmd`: PASS.
- Post-exit-fix `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- Post-exit-fix `cmd /d /c restart-engines.cmd`: PASS.
- Post-proxy-fix `git diff --check`: PASS.
- Post-proxy-fix `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- Post-proxy-fix `cmd /d /c restart-engines.cmd`: PASS.
- User smoke confirmed the Montroyal door now lands in the correct private city.
