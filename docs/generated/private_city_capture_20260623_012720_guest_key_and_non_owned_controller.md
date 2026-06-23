# Private City Capture 20260623-012720

Capture folder:

`tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-012720`

This note records only packet evidence from the guest key generator and the non-owned / no-organization CityController path. The capture logger duplicated adjacent entries, so repeated adjacent packet rows with identical content are treated as one logical packet for the sequence summaries.

## Guest Key Generator

Terminal:

- Identity: `Terminal:5751538B`
- Name: `Private City Guest Key Generator`
- Position: `(534, 160.6381, 578)`

Client action:

- Time: `2026-06-23T06:33:05.1306568Z`
- Packet: `OUT-N3 GenericCmd`
- User: `SimpleChar:67341D9C`
- Target: `Terminal:5751538B`
- Fields: `Temp1=0 Count=1 Action=Use Temp4=1`

Item creation:

- Time: `2026-06-23T06:33:05.2016570Z`
- Packet: `IN-N3 SimpleItemFullUpdate`
- Item identity: `51056:6D780D`
- `StaticInstance=280642`
- `ACGItemLevel=1`
- `ACGItemTemplateID=280642`
- `ACGItemTemplateID2=280642`
- `MultipleCount=1`
- `PlayfieldId=1196034`

Inventory placement:

- Packet: `IN-N3 ContainerAddItem`
- Identity: `SimpleChar:67341D9C`
- Source: `OverflowWindow:0000`
- Target: `OverflowWindow:67341D9C`
- Slot: `111`

Success acknowledgement:

- Packet: `IN-N3 GenericCmd`
- Target: `Terminal:5751538B`
- Fields: `Temp1=1 Count=1 Action=Use Temp4=1`

De-duplicated sequence:

`GenericCmd Use OUT -> SimpleItemFullUpdate IN -> ContainerAddItem IN slot 111 -> GenericCmd Use success ack IN`

No matching system-message text was captured at the action timestamp.

## Non-Owned / No-Organization CityController

CityController:

- Identity: `CityController:9C182E`
- Name: `City Controller`
- Position: `(586, 160.638, 598.4673)`

Client action:

- Time: `2026-06-23T06:35:30.0037133Z`
- Packet: `OUT-N3 GenericCmd`
- User: `SimpleChar:67341D9C`
- Target: `CityController:9C182E`
- Fields: `Temp1=0 Count=2 Action=Use Temp4=1`

Response packets:

- Multiple `IN-N3 AOTransportSignal` packets followed the use action.
- The first `AOTransportSignal` packet payload includes raw ASCII text: `no organization`.

Success acknowledgement:

- Time: `2026-06-23T06:35:30.1336318Z`
- Packet: `IN-N3 GenericCmd`
- User: `SimpleChar:67341D9C`
- Target: `CityController:9C182E`
- Fields: `Temp1=1 Count=2 Action=Use Temp4=1`

Feedback response:

- Time: `2026-06-23T06:35:30.5008942Z`
- Packet: `IN-N3 Feedback`
- `CategoryId=110`
- `MessageId=8208531`
- The decoded text field was empty in `system-messages.log`.

Negative evidence:

- No decoded `OrgClient` command was present for this CityController interaction.
- No decoded `CityAdvantages` packet was present for this CityController interaction.

De-duplicated sequence:

`GenericCmd Use OUT -> AOTransportSignal IN ("no organization") -> AOTransportSignal IN x4 -> GenericCmd Use success ack IN -> AOTransportSignal IN -> Feedback IN`

## Implementation Guidance From This Capture

This capture supports only the non-owned / no-organization CityController path and the guest key item creation path. It does not support implementing owned CityAdvantages behavior.
