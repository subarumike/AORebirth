# Windcaller Karrec Subway Entrance Quest Evidence

Status: capture-backed observation fixture; not a runtime specification.

Primary capture: `20260717-223626`
Gateway corroboration capture: `20260717-232249`

This document records only fields observed in those captures. Unknown numeric fields remain unknown, account eligibility is not assigned a guessed flag, and no packet structure is inferred from dialogue or UI text.

## Capture integrity and scope

| Capture | UTC window | Observed role | Validation |
|---|---|---|---|
| `20260717-223626` | `2026-07-18T03:36:26.0790608Z` to `03:41:48.7962050Z` | Karrec acceptance, Annoying Dude and Maddy Cardile objectives, two-item return, reward, mission deletion | Capture reports `3940` raw records (`3685` inbound, `255` outbound), zero raw write/projection/callback errors, `processingAllowed=true`, `recaptureRequired=false`; temporary-copy offline validation decoded `288/288` SCFU rows with zero failures/incomplete rows. |
| `20260717-232249` | `2026-07-18T04:22:49.8647701Z` to `04:23:02.5217167Z` | Completed-player use of the gateway and teleport to playfield `647` | Capture reports `220` raw records (`214` inbound, `6` outbound), zero raw write/projection/callback errors, `processingAllowed=true`, `recaptureRequired=false`; temporary-copy offline validation decoded `17/17` SCFU rows with zero errors. |

Primary-capture raw-sink SHA-256 values recorded during the audit:

- `packets.hex.log`: `12e765f3315835ab1f5a5e57693ff2fe5e68f32cd70dee2aa7e47318501037ab`
- `raw-packets.csv`: `14889e0bf5b06239f187b9dd134f8836b5c39b751feb0ce01c2d3fb82b519b0c`
- `capture-session.json`: `09e6b3710715916e159caeacc5e5cd5146dd4bfb4e90d7188bc8242a578572d2`

Capture-local player identity is `(SimpleChar:7944C065)` / `{type:50000, instance:2034548837}`. The character name is deliberately normalized to `{playerName}` in the one server line that interpolates it.

## Exact participants and positions

The following identities come from exact SCFU rows in playfield `655`:

| Role | Identity | Position | Exact fields |
|---|---|---|---|
| Windcaller Karrec | `(SimpleChar:796360BB)` / `{50000,2036555963}` | `(3212.37549,35.975,788.760132)` | version `58`, level `200`, health `51008`, MonsterData `40818`, scale `121`, family `136`, head mesh `40696`, run-speed base `515` |
| Annoying Dude | `(SimpleChar:796360BD)` / `{50000,2036555965}` | `(3185.87134,35.11,963.378967)` | level `45`, health `1958`, MonsterData `26103`, scale `104`, family `103`, head mesh `40117`, run-speed base `154` |
| Maddy Cardile | `(SimpleChar:796360BC)` / `{50000,2036555964}` | first `(3332.37524,35.11,931.1814)`, later `(3342.10986,35.11,919.9939)` | level `200`, health `365`, MonsterData `26090`, scale `121`, family `103`, head mesh `40647`, run-speed base `515` |

Karrec's additional observed SCFU fields are:

- flags `170552011`; character flags `277352961`; account flags `0`; expansions `0`; appearance `1576`
- textures `0:0:0|1:161710:0|2:161715:0|3:161705:0|4:161725:0`
- meshes `0:20108:161720:2|0:40696:0:4`
- active nano `(53019:3233F):0:29050327:29050327`
- visible title `1`; neutral; normal; Solitus; male

Provenance: primary `raw-packets.csv:3585` / global ordinal `3584` / sequence `3347`, decoded `scfu-appearance.csv:272`. `MonsterData` is retained under its observed field name and is not relabeled as an NPC template ID.

## Acceptance path

All selected answers below were index `0`; all observed append-text packets carried `Unknown1=2`.

1. Karrec: `Greetings, traveller. My name is Karrec.\nWould you spare a moment of your precious time?`
   Options: `You can have a moment, but no more.` / `Goodbye`
2. Karrec: `I am but a humble servant carrying out his duties. But even servants need food once in a while...`
   Options: `So, you want food?` / `A servant?` / `Goodbye`
3. Karrec: `Yes, and of course any spare change...`
   Options: `What will I get out of this?` / `Goodbye`
4. Karrec: `I shall grant you access to the Temple of the Three Winds.`
   Options: `Fine, tell me what to do.` / `Goodbye`
5. Karrec: `There is an annoying man that always eats his bronto burger in front of me.. You must find him and take his burger.\nSecondly, the rich old woman Maddy Cardile never donated any money to our cause. Force her to do so.`
   Options: `I'll do it...` / `You are a scum, I will never help you.` / `Goodbye`

The selected fifth answer is outbound `KnubotAnswer Answer=0` at `03:36:38.6516876Z` (`raw-packets.csv:57`). The server then sends the only acceptance-state packet, inbound `QuestFullUpdate`, at `03:36:40.1495857Z` (`raw-packets.csv:65`, global ordinal `64`, sequence `58`, packet length `733`). No outbound generic Quest-accept packet occurs in the capture.

After the full update, Karrec says `Good, good. You know where to find me when you are done.` with only `Goodbye` available.

## QuestFullUpdate DTO

The 733-byte packet parses exactly to its end. Its envelope is:

- N3 type `1180319841`
- identity `{type:50000, instance:2034548837}`
- `Unknown1=1`
- quest count `1`

The quest is:

```text
QuestId       = {type:56003, instance:1431802753} / (Mission:55579381)
Unknown1      = 15
Unknown2      = 0
Unknown3      = 0
Unknown4      = 2
ShortInfo     = The Windcaller's requests
LongInfo      = The Windcaller's requests<BR><BR>Windcaller Karrec told you to get him a hamburger from an annoying individual. He also told you to get a woman named Maddy Cardile to donate money to his temple.<BR><BR><font color="#FF0000">Mission Objective:<BR>Give Windcaller Karrec a Bronto Burger and Maddy's Credit Card.</font>
UnknownId1    = {type:50000, instance:2036555963}
Unknown5      = 6
Unknown6      = 0
Unknown7      = 0
Unknown8      = 0
Unknown9      = 1009
Unknown10     = 1009
MissionItem   = {lowId:285612, highId:285612, ql:1, unknown:0}
Unknown11     = 1110716998
Unknown12     = 0
Unknown13     = 0
UnknownHash1  = 00000000
Unknown14..18 = 0
UnknownId2    = {type:50000, instance:2034548837}
MissionIconId = 244818
Unknown20     = 60
Unknown21     = 60
PlayerIds     = [{type:50000, instance:2034548837}]
UnknownArray1 = [89266741]
UnknownArray2 = []
CharacterInfos = []
Unknown22     = 6
PlayerIds2    = [{type:50000, instance:2034548837}]
Unknown23     = 0
Unknown24     = 105201
UnknownId3    = {type:0, instance:0}
Unknown25     = 0
Unknown26     = 0
QuestIdentities = []
Unknown27     = 7
FactionInfos  = []
Unknown28     = 1
```

The single quest action is:

```text
Version       = 24
Action        = {type:0, instance:0}
UnknownId1    = {type:0, instance:0}
UnknownId2    = {type:70099, instance:105201}
UnknownId3    = {type:0, instance:0}
UnknownId4    = {type:0, instance:0}
Unknown1..4   = 0.0
UnknownId5    = {type:0, instance:0}
Unknown5..8   = 0.0
UnknownId6    = {type:0, instance:0}
UnknownHash1  = 6A5B02D9
Unknown9      = 0
UnknownId7    = {type:54001, instance:1297226293}
PlayfieldId   = {type:0, instance:0}
Unknown10     = 0
Unknown11     = 0
Position      = (0.0,0.0,0.0)
```

Item `285612` is named `Daily Mission XP Reward` by the repository item-name table. Types `70099` and `54001` remain numeric because this evidence does not prove semantic names for them.

## Objective dialogue and item delivery

### Annoying Dude

Selected index-0 path:

1. `Yo!` -> `I'm here to make sure you don't bother Karrec again.`
2. `I would never do such thing!` -> `Enough lies, hand me your burger.`
3. `You are nuts, come on! I'm hungry.` -> `I will count to three...`
4. `You will not have my burger!` -> `1...`
5. `I'm sure we can arrange some sort of agreement?` -> `2...`
6. `Come on, I'm skinny I need the calories...` -> `3!`
7. `Ok, ok, ok! Take it, it didn't taste that good anyway. Just leave me alone!`

The server then sends a TemplateAction for item `{lowId:297042, highId:297042, ql:1}` and a ContainerAddItem from `(OverflowWindow:0)` to the local player's overflow window, slot `111` (`raw-packets.csv:1581-1582`). Item `297042` is `Bronto Burger` in the item-name table.

### Maddy Cardile

An initial open produced only `Hello there, young one.` and a server close (`npc-interactions.log:112-115`). The capture does not prove why that attempt closed.

Successful index-0 path:

1. `Hello there, young one.` -> `I heard you have not donated any money to Karrec's cause.`
2. `You should not mingle with people like him, child.` -> `I am here to collect your donation. Who I mingle with is not your business.`
3. `Well, now. You are one of those. I don't have the time or patience to deal with you. Get lost before I tell one of the peacekeepers what you are up to.` -> `I kick peacekeeper butt for fun. Tell them and I will show you!`
4. `I'm disappointed in you, child. I will give you my credit card if you leave me alone. Money is not worth risking anyone's life for. Not even yours.` -> `I knew you would come to your senses, old woman.`
5. `Leave now, before I change my mind.`

The server then sends a TemplateAction for item `{lowId:297043, highId:297043, ql:1}` and a ContainerAddItem to the local player's overflow window, slot `111` (`raw-packets.csv:2901-2902`). Item `297043` is `Maddy's Credit Card` in the item-name table.

## Return, trade, reward, and completion order

The player selects index `0` for `I have the bronto burger and a donation to your temple` (`raw-packets.csv:3883-3885`). Exact observed order follows:

1. Karrec: `Give it to me!`
2. Inbound KnuBotStartTrade, target Karrec, two item slots, message: `Move the items you want to give to Windcaller Karrec into the available slots in the Give Item Tab on the right side of this window and press "Accept'.`
3. Outbound KnubotTrade, container `(Inventory:0042)` (`raw-packets.csv:3896`, sequence `251`).
4. Outbound KnubotTrade, container `(Inventory:0047)` (`raw-packets.csv:3899`, sequence `252`).
5. Outbound FinishTrade, `Decline=0`, `Amount=0` (`raw-packets.csv:3902`, sequence `253`).
6. Inbound RejectedItems with an empty item list (`raw-packets.csv:3903`).
7. Karrec append text (`Unknown2=1`): `Karrec hands you a note covered with strange words and symbols, none of which make any sense to you. You upload the information to your ncu and throw the paper away.`
8. Karrec append text (`Unknown2=0`): `Your devotion to the Cult of Three Winds gains you passage to the sacred Temple {playerName}. You may now use the gateway.`
9. Options: `Thank you, Karrec.` / `Goodbye`.
10. FormatFeedback: `Unknown=1`, `Unknown1=1107296284`, encoded message `~&!!!":!)90Fi!!![g~`, `Unknown2=0` (`raw-packets.csv:3908`, sequence `3654`). The capture contains no plaintext decoding of this value. The contemporaneous client UI displayed `5000 of your XP were allocated to your personal research.`; that sentence is user-observed UI evidence, not packet plaintext.
11. PerkUpdate: `Unknown=1`, `Unknown1=2680`, `Unknown2=2680`, `Unknown3=45000` (`raw-packets.csv:3909`, sequence `3655`). These fields remain semantically unresolved; `45000` is not labeled total/base XP here.
12. Stat numeric ID `75` changes `4026 -> 4028` (`raw-packets.csv:3910-3911`).
13. FormatFeedback literal: `Side tokens collected: 4028.` (`raw-packets.csv:3912`). This proves an observed side-token delta of `+2`; it does not resolve every use of numeric stat ID `75`.
14. Feedback: category `110`, message ID `108871108` (`raw-packets.csv:3913`).
15. CharacterAction: action `59`, target `(Mission:55579381)`, parameter 1 `56003`, parameter 2 `1431802753` (`raw-packets.csv:3914`).
16. Quest action `Delete`, mission `(Mission:55579381)`, remaining fields zero (`raw-packets.csv:3915`).
17. Stat SocialStatus `0` (`raw-packets.csv:3916`).

There is no captured Cash stat, directly labeled Experience stat, inventory-update reward packet, follow-on QuestFullUpdate, or next-mission handoff in this completion sequence.

## Gateway corroboration

The entrance object is repeatedly projected locally as `(Terminal:C004028F)`, name `Temple of Three Winds Gateway`, position `(3213.421,37.57098,787.9217)` in playfield `655`. No exact Terminal full-update/template packet was found for this identity.

In `20260717-232249`, the completed player performs:

- outbound GenericCmd at `04:22:51.6724647Z`: `Temp1=0`, `Count=2`, `Action=Use`, `Temp4=1`, user local player, target `(Terminal:C004028F)`, `Unknown=1` (`raw-packets.csv:14`, global ordinal `13`, sequence `1`)
- inbound acknowledgement at `04:22:51.7714568Z`: `Temp1=1`, `Count=2`, `Action=Use`, `Temp4=1`, same target, `Unknown=0` (`raw-packets.csv:15`, global ordinal `14`, sequence `13`)
- inbound N3Teleport at `04:22:51.992489Z` (`raw-packets.csv:17`, global ordinal `16`, sequence `15`)

The 114-byte teleport parses exactly:

```text
N3Type          = 1125743906
Identity        = {type:50000, instance:2034548837}
Unknown         = 0
Destination     = (3214.815185546875,35.51499938964844,791.053466796875)
Heading         = (0,-0.9576424956321716,0,0.2879597544670105)
Unknown1        = 97
Playfield       = {type:51100, instance:647}
GameServerId    = 0
SgId            = 0
ChangePlayfield = {type:40016, instance:647}
Unknown4        = 0
Unknown5        = 0
Playfield2      = {type:0, instance:0}
PayloadLength   = 12
PayloadBEFloats = (1814.0,29.0,2699.0)
```

Playfield initialization then reports `647`, and the local player appears at `(1814,28.81,2699)`. This is behavioral corroboration that this completed character can use the gateway; it does not expose the persistent eligibility flag or storage location.

## Explicit unresolved and excluded claims

- No packet identifies an account, character, or mission flag that stores permanent gateway eligibility. The post-zone player SCFU still reports `AccountFlags=0`.
- No access-denial capture for exact identity `(Terminal:C004028F)` was found in the searched capture corpus.
- No exact Terminal full update or template ID was found. Vending-machine identity `(VendingMachine:C004028F)` is a different identity type at a different position and is excluded despite its matching instance suffix.
- The exact reward XP total/base amount is unresolved. Only the user-observed `5000` personal-research allocation and the unknown numeric PerkUpdate fields are recorded.
- Numeric stat ID `75` is authoritative; a derived enum label is not treated as proof because the adjacent literal packet says `Side tokens collected: 4028.`
- The capture proves successful two-slot trade completion and no rejected items, but it does not label which inventory container is the burger versus the credit card.
- It does not prove failure branches, alternate dialogue choices, repeated-objective behavior, already-completed dialogue, delete/abandon recovery, reconnect persistence, or team sharing.
- It does not prove a next quest or any mission-state transition beyond server full update at acceptance and server delete at completion.

The deterministic companion fixture is `AORebirth/Server/ZoneEngine/Content/Captured/Quests/windcaller_karrec_subway_entrance_20260717_223626.json`.
