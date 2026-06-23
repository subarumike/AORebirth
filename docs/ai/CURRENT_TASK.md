# Current Task

## Active Task

Implement captured CityController no-organization compatibility response.

## Scope

- Work only in AORebirth.
- Implement only the captured no-organization CityController response from `20260623-015602`.
- Do not implement owned-city transition.
- Do not implement CityAdvantages.
- Do not implement OrgClient 31.
- Do not implement city purchase or ownership persistence.
- Do not touch unrelated GM/chat/grid-inspection dirty files.

## Evidence

Capture folder:

`tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-015602`

Report:

`docs\generated\private_city_purchase_capture_20260623_015602.md`

Captured behavior:

- Target: `CityController:9C182E`.
- OUT `GenericCmd Use`: `Temp1=0 Count=4 Action=Use Temp4=1`.
- IN `AOTransportSignal` packets contain raw ASCII `no organization`.
- IN `GenericCmd Use` success ack: `Temp1=1 Count=4 Action=Use Temp4=1`.
- IN `FeedbackMessage`: `CategoryId=110 MessageId=8208531`.
- Later IN `StatMessage`: `Cash=175232992`.
- No decoded `OrgClient`.
- No decoded `CityAdvantages`.
- No teleport/playfield transition.
- No inventory movement.

## Implementation

- Add a dedicated CityController use handler.
- Send the captured-compatible `GenericCmd` success ack.
- Send `FeedbackMessage CategoryId=110 MessageId=8208531` when the character is not in an organization.
- Do not synthesize `AOTransportSignal`; no outbound model exists in the current server packet layer.
- Do not send or mutate the later cash stat because the capture does not prove it is part of the immediate CityController response path.
- Log unsupported captured packet pieces explicitly.

## Validation Plan

- `cmd /d /c tools\build_aorebirth_debug.cmd`
- `cmd /d /c restart-engines.cmd`
- `git diff --check`
- Live smoke if available:
  - Use CityController while not in an organization / not owning a city.
  - Confirm no crash/stall.
  - Confirm `GenericCmd` success ack behavior.
  - Confirm no `CityAdvantages`.
  - Confirm no ownership transition.

## Validation Result

- `cmd /d /c tools\build_aorebirth_debug.cmd`: PASS after stopping running `ZoneEngine.exe` processes that locked the build output.
- `cmd /d /c restart-engines.cmd`: PASS.
- `git diff --check`: PASS.
- Live smoke: Not run in this pass.
