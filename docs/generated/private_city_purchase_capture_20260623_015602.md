# Private City Purchase Capture 20260623-015602

Capture folder:

`tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-015602`

This was intended as the one-time owned-city purchase capture. The packet evidence below records what the client and live server actually exchanged during the captured action. It does not infer owned-city behavior that was not present in the capture.

## Capture Readiness

- Wrapper created fresh capture folder `20260623-015602`.
- `AOSharpLiveCapture loaded` row count: `1`.
- `packets.hex.log` and `events.log` were actively growing before Mike performed the action.
- `AOSharpLiveInjector.exe` exited after injection.

## Purchase Trigger

Captured interaction target:

- `CityController:9C182E`

The capture folder did not include a fresh dynel spawn/full-update row naming this controller. It captured the interaction target identity directly in the `GenericCmd` packets.

Player identity:

- `SimpleChar:67341D9C`

Initial captured location context:

- Initial snapshot: playfield `(Playfield2:124002)`.
- Character position at start: `(579.0417, 160.6381, 586.5974)`.
- Stop before interaction: `(584.876, 160.6381, 593.3539)`.

## Timeline

### 2026-06-23T06:56:41.0223974Z

`IN-N3 CharDCMove`

- `MoveType=ForwardStop`
- Position: `(584.876, 160.6381, 593.3539)`

### 2026-06-23T06:56:41.2639037Z

`OUT-N3 GenericCmd`

- User: `SimpleChar:67341D9C`
- Target: `CityController:9C182E`
- Fields: `Temp1=0 Count=4 Action=Use Temp4=1`
- Raw packet: `OUT #63 len=61 n3=GenericCmd`

### 2026-06-23T06:56:41.3849036Z

Inbound response block:

- `IN-N3 AOTransportSignal` x4
- First transport signal contains raw ASCII `no organization`.
- `IN-N3 GenericCmd`
  - User: `SimpleChar:67341D9C`
  - Target: `CityController:9C182E`
  - Fields: `Temp1=1 Count=4 Action=Use Temp4=1`

### 2026-06-23T06:56:41.7719800Z

Inbound response block:

- `IN-N3 AOTransportSignal`
- `IN-N3 Feedback`
  - `CategoryId=110`
  - `MessageId=8208531`
  - Decoded text field was empty in `system-messages.log`.

### 2026-06-23T06:56:41.9619787Z - 2026-06-23T06:56:42.0419784Z

Raw packet activity:

- `IN n3=0x00000001 len=40`
- `OUT n3=0x00000002 len=40`
- `IN n3=0x0000000B len=548`

These packets were not decoded by the capture logger.

### 2026-06-23T06:56:44.7014081Z

Raw outbound packet:

- `OUT n3=0x55796602 len=33`

This packet was not decoded by the capture logger.

### 2026-06-23T06:56:44.8654084Z - 2026-06-23T06:56:44.8664099Z

Inbound response block:

- `IN-N3 AOTransportSignal` x6
- First transport signal contains raw ASCII `no organization`.
- Final transport signal contains raw ASCII `Est. 2024`.
- `IN-N3 Stat`
  - `Cash=175232992`

## Packet Findings

Outbound interaction packets:

- `GenericCmd Use` to `CityController:9C182E`.
- Raw outbound `n3=0x55796602` occurred about 3.44 seconds later.

Inbound response packets:

- `AOTransportSignal` response blocks.
- `GenericCmd Use` success-ack shape: `Temp1=1 Count=4 Action=Use Temp4=1`.
- `FeedbackMessage CategoryId=110 MessageId=8208531`.
- `StatMessage Cash=175232992`.
- Undecoded raw packets: `n3=0x00000001`, `n3=0x0000000B`.

New dynels created:

- None decoded in this capture window.

Organization/city messages:

- No decoded `OrgClient` packet was present.
- No decoded `OrgInfoPacket` update was present during the interaction.
- Raw transport signals included `no organization`.
- Raw transport signal included `Est. 2024`.

CityAdvantages:

- No decoded `CityAdvantages` packet was present.

Playfield updates:

- No decoded playfield transition, teleport, `PlayfieldAllCities`, or `PlayfieldAllTowers` packet was present during the interaction.
- Capture remained in initial playfield context `(Playfield2:124002)`.

Inventory/credit state:

- `inventory-updates.csv` contained only the header; no inventory movement was captured.
- One cash stat update was captured: `Cash=175232992`.
- The capture does not include an earlier cash stat in this folder, so the exact credit delta is not proven by this capture alone.

## Proven Missing Feature

The capture does not prove owned-city transition behavior, owned CityAdvantages payloads, or city ownership state updates. It proves that AORebirth still needs a capture-backed CityController response path for the observed live-server transport/feedback/stat behavior before owned-city behavior can be implemented safely.

## Recommended Next Implementation Target

Implement only the captured CityController response plumbing that is now proven:

- `GenericCmd Use` to `IdentityType.CityController`.
- `GenericCmd` success acknowledgement matching the live shape.
- Safe handling/logging for the observed `AOTransportSignal` response shape, including the no-organization response.
- Do not implement owned CityAdvantages or ownership transition until a capture contains decoded or otherwise confirmed owned-city transition packets.
