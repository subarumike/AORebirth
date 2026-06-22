# Current Task

## Active Task

Montroyal private-city movement-entry compatibility from AOSharp live capture `20260622-101935`.

## Scope

- Treat Montroyal private-city entry as movement-triggered teleport, not `GenericCmd Use`.
- Add the captured entry mapping from public PF `655` near `(3140.412, 51.54391, 799.8611)` to private-city instance `1196045`.
- Send the captured private-city ready block for private-city instance `1196045`, including empty `PlayfieldAllTowers` and empty `PlayfieldAllCities`.
- Use the captured Montroyal private-city generator identities for guest-key terminal `Terminal:574B84AB`, CityController `CityController:9C6010`, and shuttle door `Door:108ECA90` through the current `PlayfieldAnarchyF` data path.
- Do not implement guest-key terminal use, CityController use, CityAdvantages, OrgClient, city ownership, city-bank, or org failure text from this capture.

## Evidence

- `20260622-101935`: valid complete capture; entry from PF `655` into private-city instance `1196045`.
- Entry was movement-triggered: no `GenericCmd Use` was captured before teleport.
- Movement stopped near `(3140.412, 51.54391, 799.8611)`, followed by `SocialStatus=4`, `N3Teleport`, and `PLAYFIELD-INIT 1196045`.
- Spawned private-city dynels were `Terminal:574B84AB`, `CityController:9C6010`, and `Door:108ECA90`.
- Ready block after init included `PlayfieldAnarchyF`, `DoorStatusUpdate`, character/item full updates, `FullCharacter`, empty `PlayfieldAllTowers`, empty `PlayfieldAllCities`, and `CharInPlay`.
- `PlayfieldAnarchyF` for instance `1196045` uses private-city proxy `C79E:138A`; using `C79E:177A` makes the client load resource `6010` and label the instance as `Serenity Islands`.
- Local smoke with PF proxy resource `6010`, zone `1828`/`1829`, area `Serenity Islands` invalidated the earlier `(1155.1, 5.0, 1218.0)` spawn and `(1168.7, 7.9, 1221.5)` exit coordinates as Serenity-derived, not Montroyal-derived.

## Validation Plan

- `git diff --check`
- `cmd /d /c tools\build_aorebirth_debug.cmd`
- `cmd /d /c restart-engines.cmd`
- User gameplay testing required: move into the Montroyal entry area, confirm teleport to private-city instance `1196045`, confirm private-city init completes, confirm guest-key terminal, CityController, and shuttle door are visible, and confirm no client stall/crash.

## Validation Result

- `git diff --check`: PASS.
- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS after stopping running engines that locked `ZoneEngine.exe` during rebuild.
- `cmd /d /c restart-engines.cmd`: PASS.
- First user smoke reached the captured entry coordinate, then `ZoneEngine.out.log` showed `KeyNotFoundException` constructing dynamic private-city instance `1196045` because no checked-in `PFData` statels exist for that instance.
- Dynamic private-city playfields without `PFData` now initialize with empty statels/vendors instead of crashing.
- Post-fix `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- Post-fix `cmd /d /c restart-engines.cmd`: PASS.
- Entry spawn destination restored to the captured Montroyal landing `(530.0042, 163.2545, 580.9957)`.
- Second user smoke could not zone out; `ZoneEngine.out.log` showed repeated `KeyNotFoundException` in `WallCollision.CheckCollision` for dynamic private-city instance `1196045`.
- Dynamic instances without `PFData` now skip wall collision checks, and the provisional exit trigger was moved to the captured Montroyal shuttle-door area near `(530.4664, 160.6381, 590.7054)` returning to public PF `655`.
- Post-exit-fix `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- Post-exit-fix `cmd /d /c restart-engines.cmd`: PASS.
- Exit landing adjusted from the first PF `655` landing to user-confirmed safe public location `(3138.2, 51.4, 812.8)`.
- PF proxy for `1196045` corrected from the older private-city resource `C79E:177A` to the Montroyal capture value `C79E:138A`.
- Post-proxy-fix `git diff --check`: PASS.
- Post-proxy-fix `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS.
- Post-proxy-fix `cmd /d /c restart-engines.cmd`: PASS.
- User smoke confirmed the Montroyal door now lands in the correct private city.
