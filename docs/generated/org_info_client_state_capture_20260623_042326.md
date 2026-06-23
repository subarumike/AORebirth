# Org Info Client State Capture 20260623-042326

## Scope

- Task: capture live org/client initialization before `/org info`.
- Server captured: live Anarchy Online.
- Live capture folder: `tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-042326`
- AORebirth/private comparison reference: `tools-temp\AOSharpLiveCapture\bin\Debug\captures\20260623-040642`
- Exact command typed during live capture: `/org info`
- No AORebirth code was changed for this report.

## Character And Org State

- Character: `SimpleChar:67341D9C`
- Name: `Valk2024`
- Public playfield after first zone: `Playfield2:028F` / PF `655`
- Private city playfield before `/org info`: `Playfield2:10E83C` / PF `1108028`
- Organization id: `1970177` (`0x001E1001`)
- Organization name: `Est. 2024`
- ClanLevel: `1`

## Live Zoning Sequence Before `/org info`

The capture starts while already logged in. The useful ready block is the second zoning event back into the private city:

- `09:24:25.3283042Z` `PLAYFIELD-INIT 1108028`
- `09:24:25.3313053Z` live ready block begins for `SimpleChar:67341D9C`
- `09:24:30.7018155Z` `/org info` sends `OrgClient`
- `09:24:30.7633900Z` live server responds with `OrgServer / OrgInfo`

### Org-Related Ready Block Packets

Decoded `events.log` sequence for the private city ready block:

| Events # | Packet | Identity | Decoded data |
| --- | --- | --- | --- |
| 160 | `OrgInfoPacket` | `SimpleChar:67341D9C` | org packet present |
| 161 | `Stat` | `SimpleChar:67341D9C` | `SocialStatus=4`, `Unknown=1` |
| 162 | `Stat` | `SimpleChar:67341D9C` | `Clan=1970177`, `Unknown=0` |
| 163 | `Stat` | `SimpleChar:67341D9C` | `ClanLevel=1`, `Unknown=0` |
| 164 | `Stat` | `SimpleChar:67341D9C` | `SocialStatus=4`, `Unknown=1` |
| 165 | `Stat` | `SimpleChar:67341D9C` | `SocialStatus=4`, `Unknown=1` |
| 166 | `Stat` | `SimpleChar:67341D9C` | `SocialStatus=4`, `Unknown=1` |
| 167 | `FullCharacter` | `SimpleChar:67341D9C` | full character payload |
| 168 | `PlayfieldAllTowers` | `Playfield2:10E83C` | present after `FullCharacter` |
| 169 | `PlayfieldAllCities` | `Playfield2:10E83C` | present after `FullCharacter` |

Raw `packets.hex.log` entries for the same relevant packet block:

- `IN #172 OrgInfoPacket`: contains `Organization:001E1001` and `Est. 2024`
- `IN #173 Stat`: `SocialStatus=4`
- `IN #174 Stat`: `Clan=1970177`
- `IN #175 Stat`: `ClanLevel=1`
- `IN #176 Stat`: `SocialStatus=4`
- `IN #179 FullCharacter`
- `IN #180 PlayfieldAllTowers`
- `IN #181 PlayfieldAllCities`

Raw org identity/name packet:

```text
0008000A0001002C00000DBA67341D9C2E2A4A6B0000C35067341D9C00001E100100094573742E2032303234
```

Decoded meaning:

- Target/identity: `SimpleChar:67341D9C`
- Organization id: `0x001E1001` / `1970177`
- Organization name: `Est. 2024`

## `/org info` Request

Raw live request:

```text
0000000A0000000067341D9C000000027F4B31080000C35067341D9C0005000000000000000000000001
```

Decoded fields:

- N3 type: `OrgClient`
- Command: `OrgClientCommand.Info = 0x05`
- Sender identity: `SimpleChar:67341D9C`
- Command target: `None:00000000`
- `Unknown1 = 1`
- No command argument string.

Note: the earlier live command capture `docs/generated/org_command_capture_20260623_084448.md` also observed `Unknown1=1`, but its command target was self. This capture shows that target identity is not required to be self for the client to send `Unknown1=1`.

## `/org info` Response

Raw live response:

```text
0024000A0001006500000DBA67341D9C64582A070000C35067341D9C000200000000000000000000DEAA001E100100094573742E2032303234000000000000000A4465706172746D656E74000B43656C6369757332303234000747656E6572616C000003F1
```

Decoded fields:

- N3 type: `OrgServer`
- OrgServer message type: `OrgInfo = 0x02`
- Organization: `Organization:001E1001`
- Organization name: `Est. 2024`
- String fields after org name:
  - empty string
  - empty string
  - `Department`
  - `Celcius2024`
  - `General`
- Trailer: `000003F1`

## Other Org-Related Packets

During public playfield init, live sent the same org setup for the local character:

- `OrgInfoPacket`
- `SocialStatus=4`
- `Clan=1970177`
- `ClanLevel=1`
- repeated `SocialStatus=4`
- `FullCharacter`
- `PlayfieldAllTowers`
- `PlayfieldAllCities`

The capture also observed unrelated org packets for another character:

- `SimpleChar:78CB986F`
- `OrgInfoPacket` with org id `2066435`
- `Clan=2066435`
- `ClanLevel=0`
- several `OrgServer` packets of type `0x06`

Those other-character packets are not part of the local `/org info` command path.

## Live Versus AORebirth Comparison

Known AORebirth/private reference: `20260623-040642`.

AORebirth/private command packet:

```text
0000000A0000000000000012000000027F4B31080000C3500000001200050000C3500000001200000007
```

Decoded private fields:

- N3 type: `OrgClient`
- Command: `OrgClientCommand.Info = 0x05`
- Sender identity: `SimpleChar:0012`
- Command target: `SimpleChar:0012`
- `Unknown1 = 7`

AORebirth/private response packet:

- N3 type: `OrgServer`
- OrgServer message type: `OrgInfo = 0x02`
- Response shape uses the same five post-org-name string fields and `000003F1` trailer.

Important limitation: the AORebirth/private reference capture did not include a zoning ready block before `/org info`. It only proves the private client sent `Unknown1=7` in that already-initialized state and that AORebirth returned an `OrgInfo` packet.

## Best-Supported Hypothesis

The live client sends `Unknown1=1` after receiving org initialization during zoning:

1. `OrgInfoPacket`
2. `SocialStatus=4`
3. `Clan=1970177`
4. `ClanLevel=1`
5. repeated `SocialStatus=4`
6. `FullCharacter`
7. `PlayfieldAllTowers`
8. `PlayfieldAllCities`

The best-supported hypothesis is that `Unknown1=7` in the AORebirth/private capture reflects different client org state before the command, not an `OrgInfoMessage` response-shape problem. The current live evidence points to the zoning ready-block org initialization and ordering as the area to compare against AORebirth.

## Implementation Recommendation

Do not change `OrgInfoMessage` shape and do not force outgoing `Unknown1`; that value is client-generated.

Next implementation step:

- Inspect AORebirth's private-city zoning ready block against the live sequence above.
- If AORebirth does not send the same org setup before `FullCharacter`, implement that exact ordering for the owned private city path:
  - `OrgInfoPacket`
  - `SocialStatus=4`, `Unknown=1`
  - `Clan=<org id>`, `Unknown=0`
  - `ClanLevel=<rank>`, `Unknown=0`
  - repeated `SocialStatus=4`, `Unknown=1` if the existing ready block omits those repeats
  - `FullCharacter`
  - `PlayfieldAllTowers`
  - `PlayfieldAllCities`
- If AORebirth already sends that sequence in current code, the private capture that showed `Unknown1=7` is insufficient because it did not include zoning initialization. In that case, the next action should be static verification of deployed ordering/logging, not another `OrgInfo` response patch.

Evidence is insufficient to prove any required change to `/org info` response fields.
