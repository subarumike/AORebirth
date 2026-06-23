# City Controller Non-Org Capture 20260623-081344

## Capture

- Folder: `C:\Users\Mike\Documents\AORebirth\tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-081344`
- AO window: `Anarchy Online - Valk2024`
- Character: `Valk2024`
- Playfield: `Playfield2:120000`

## Character State Visible In Capture

The short capture does not include org-stat packets. The initial snapshot shows:

- Character identity: `SimpleChar:67341D9C`
- Name: `Valk2024`
- Position: `(1002.177, 5.01, 1242.507)`
- Level: `200`

The limited menu text in the controller response is `Identifies As Clan`.

## Use Target

`events.log` records one City Controller use:

- OUT `GenericCmd Use`
- User/identity: `SimpleChar:67341D9C`
- Target: `CityController:9CA011`
- Fields: `Temp1=0 Count=2 Action=Use Temp4=1 Unknown=1`

## Response Sequence

`packets.hex.log` records the complete response sequence:

1. IN `AOTransportSignal`, signal `5`
2. IN `AOTransportSignal`, signal `10`
3. IN `AOTransportSignal`, signal `13`
4. IN `AOTransportSignal`, signal `14`
5. IN `AOTransportSignal`, signal `15`
6. IN `GenericCmd Use` success ack

`events.log` lists four `AOTransportSignal` rows, but `packets.hex.log` contains five signal packets before the ack. The packet hex is the source of truth for the count.

## Decoded AOTransportSignal Fields

All five signals use:

- Signal identity: `SimpleChar:67341D9C`
- Unknown: `1`

Signal `5` payload:

- Window/info identity: `C419:C000`
- Captured org/city value: `0014E80A` (`1370122`)
- Building identity: `C79E:177A`
- Character identity in payload: `C350:67341D9C`
- Mode fields: `2`, `1`, `-1`
- Text length: `18`
- Text: `Identifies As Clan`

Signal payloads:

- Signal `10`: `03938700`
- Signal `13`: `002632BF`
- Signal `14`: `0000000195C57953`
- Signal `15`: `3F800000`

## GenericCmd Ack

The success ack is:

- IN `GenericCmd Use`
- Identity/user: `SimpleChar:67341D9C`
- Target: `CityController:9CA011`
- Fields: `Temp1=1 Count=2 Action=Use Temp4=1 Unknown=0`

## Feedback And Chat

No `FeedbackMessage` was present in this capture. No `ChatText` or system text was present in `system-messages.log` or `chat-dialogue.log`.

## Difference From Owner/Member Menu

The owner/member capture `private_city_owned_entry_capture_20260623_021643.md` also uses five `AOTransportSignal` packets and a `GenericCmd` success ack, but its first signal contains owner org text `Est. 2024` and owner/member menu payload values.

The non-org capture instead uses:

- Target/controller: `CityController:9CA011`
- First signal text: `Identifies As Clan`
- Building identity: `C79E:177A`
- Signal `10`: `03938700`
- Signal `13`: `002632BF`
- Signal `14`: `0000000195C57953`
- No FeedbackMessage

## Implementation Recommendation

AORebirth must not choose the owner/member menu based on the character having any organization. It must compare the character's organization id against the organization that owns the current private-city playfield.

- If the character organization owns the current private-city playfield, preserve the existing owner/member menu.
- If the character organization does not own the current private-city playfield, send the captured limited non-org menu above.
- Keep the captured `GenericCmd` success ack after the menu response.
- Do not send `CityAdvantages`, `OrgClient`, ownership, purchase, or guest-key lifecycle behavior for this path.
