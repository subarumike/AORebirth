# Live Organization Command Capture 20260623-084448

## Source

- Capture folder: `tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-021643`
- Relevant org-command window:
  - `2026-06-23T08:44:48.1759662Z` capture restarted
  - `2026-06-23T08:45:17.6615601Z` capture stopped
- Mike-reported commands performed: `/org bank`, `/org info`
- No AORebirth org implementation code was changed for this report.

## Character And Organization State

- Character identity: `SimpleChar:67341D9C`
- Character was in an organization.
- Captured organization identity from `/org info`: `Organization:001E1001`
- Decimal organization id: `1970177`
- Organization name: `Est. 2024`
- Captured org info string sequence after org name:
  - empty string
  - empty string
  - `Department`
  - `Celcius2024`
  - `General`
- The captured `OrgInfo` trailer ended with `000003F1`.

The local message contract names these `OrgInfoMessage` string fields as description, objective, history, governing form, leader name, and rank. This packet only exposed five strings after organization name in the captured payload, so implementation should preserve packet shape and validate the exact field mapping before broad org-info work.

## Packet Findings

### Slash Command Input Path

Typing `/org bank` and `/org info` did not appear as outbound chat text in the capture. The live client sent `OrgClient` N3 packets directly.

Relevant `OrgClient` command ids:

- `/org bank`: `OrgClientCommand.Bank = 18` (`0x12`)
- `/org info`: `OrgClientCommand.Info = 5` (`0x05`)

Both captured outbound packets used:

- Identity: `SimpleChar:67341D9C`
- Target: `SimpleChar:67341D9C`
- Unknown1: `1`
- No command-argument string.

### `/org bank`

Command:

- Exact command typed: `/org bank`
- Time: `2026-06-23T08:45:02.9155636Z`
- Packet: `OUT-N3 OrgClient`
- Packet number: `#273`
- Length: `42`
- Raw N3 type: `OrgClient`
- Command id: `0x12`
- Decoded command: `Bank`
- Target: `SimpleChar:67341D9C`
- Unknown1: `1`
- Raw hex:
  - `0000000A0000000067341D9C000000027F4B31080000C35067341D9C00120000C35067341D9C00000001`

Response:

- Time: `2026-06-23T08:45:02.9863311Z`
- Packet: `IN-N3 ChatText`
- Packet number: `#663`
- Length: `56`
- Visible/decoded text payload from capture tooling:
  - `~&!!!&t"L&(Yu!!!!!~`
- Raw hex:
  - `018F000A0001003800000DB967341D9C5F4B442A0000C35067341D9C0000137E262121212674224C2628597521212121217E100000000001`

Observed behavior classification:

- Response type: `ChatText`
- No `FeedbackMessage` observed.
- No `OrgServer` packet observed for `/org bank`.
- No `OrgInfoPacket` update observed.
- No org stat update observed.
- The `ChatText` payload was not human-readable through the current capture decoder. Do not invent a user-facing string from this capture alone.

### `/org info`

Command:

- Exact command typed: `/org info`
- Time: `2026-06-23T08:45:07.6159234Z`
- Packet: `OUT-N3 OrgClient`
- Packet number: `#275`
- Length: `42`
- Raw N3 type: `OrgClient`
- Command id: `0x05`
- Decoded command: `Info`
- Target: `SimpleChar:67341D9C`
- Unknown1: `1`
- Raw hex:
  - `0000000A0000000067341D9C000000027F4B31080000C35067341D9C00050000C35067341D9C00000001`

Response:

- Time: `2026-06-23T08:45:07.7180210Z`
- Packet: `IN-N3 OrgServer`
- Packet number: `#666`
- Length: `101`
- Raw N3 type: `OrgServer`
- OrgServer message type: `0x02`
- Decoded message type: `OrgInfo`
- Organization: `Organization:001E1001`
- Organization name: `Est. 2024`
- Captured strings after org name:
  - empty string
  - empty string
  - `Department`
  - `Celcius2024`
  - `General`
- Raw hex:
  - `0192000A0001006500000DB967341D9C64582A070000C35067341D9C000200000000000000000000DEAA001E100100094573742E2032303234000000000000000A4465706172746D656E74000B43656C6369757332303234000747656E6572616C000003F1`

Observed behavior classification:

- Response type: `OrgServer / OrgInfo`
- No `FeedbackMessage` observed.
- No separate `OrgInfoPacket` observed in this narrow command response.
- No org stat update observed.
- No raw unknown N3 packet was associated with this command.

## FeedbackMessage IDs

No `FeedbackMessage` packets were observed for the captured `/org bank` or `/org info` commands.

## Org-Related Packets Observed

- `OUT-N3 OrgClient` command `Bank` (`0x12`)
- `IN-N3 ChatText` response to `Bank`
- `OUT-N3 OrgClient` command `Info` (`0x05`)
- `IN-N3 OrgServer` message type `OrgInfo` (`0x02`) response to `Info`

## Commands Captured

- `/org bank`
- `/org info`

## Commands Not Captured

The following commands were not captured in this run:

- `/org`
- `/org info <target>`
- `/org ranks`
- `/org tax`
- `/org debt`
- `/org leave`
- `/org invite`
- `/org promote`
- `/org demote`
- `/org kick`
- `/org description <text>`
- `/org objective <text>`
- `/org history <text>`
- `/org governingform`
- `/org governingform <form>`
- `/org create <name>`
- `/org disband`
- `/org contract`
- `/org vote info`
- `/org vote <voteentry id>`
- `/org startvote "text" <duration> <entries>`
- `/org stopvote <text>`

Not captured states:

- Character not in an organization.
- Character in org but not leader.
- Character in org as leader.
- Missing target.
- Invalid target.
- Same-org target.
- Target not in org.
- Different-org target.
- Same-side invite target.
- Wrong-side invite target.

Reason: this run only exercised `/org bank` and `/org info` on the current in-org character state.

## Implementation Planning

### Phase 1: Read-Only Org Commands

Recommended first implementation phase:

- Handle `OrgClientCommand.Info` by returning captured `OrgServer / OrgInfo` packet shape.
- Handle `OrgClientCommand.Bank` as a read-only command, but do not invent a user-facing bank string. The live response is `ChatText`, yet the captured text payload is not human-readable through the current decoder.
- Add narrow logging for decoded org command id, target, and command args before expanding command coverage.

Additional captures still needed for this phase:

- `/org`
- `/org ranks`
- `/org tax`
- `/org debt`
- `/org governingform`
- `/org bank` in a decoder-confirmed or visually confirmed state so the human-visible bank text is known.

### Phase 2: Basic Member Actions

- `/org leave`
- `/org invite`
- Invite accept/decline flow

Capture first; do not infer prompts, target validation, or feedback ids.

### Phase 3: Leader Text Updates

- `/org description`
- `/org objective`
- `/org history`
- `/org name`

Capture the mutation request packet, success response, permission failure, and resulting `/org info` packet.

### Phase 4: Rank And Member Management

- `/org promote`
- `/org demote`
- `/org kick`

Capture target-required failures, same-org target, wrong-org target, permission failures, and success packets.

### Phase 5: Bank And Tax Mutations

- `/org bank add`
- `/org bank remove`
- `/org tax <cash>`

Capture exact cash mutation response packets and validation failures before implementation.

### Phase 6: High-Risk Or Destructive Commands

- `/org create`
- `/org disband`
- `/org governingform <form>`

Treat as destructive/high-risk. Capture failures first and require explicit implementation approval.

### Phase 7: Voting And Contracts

- `/org vote`
- `/org vote info`
- `/org startvote`
- `/org stopvote`
- `/org contract`

Capture packet shape before implementation. Do not infer from help text.
