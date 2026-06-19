# Marcus Stone Quest Chain

Generated: 2026-06-18

## Scope

This chain map begins at `Mission:5514B18F` and uses captured Marcus Stone evidence only.

No implementation, SQL, runtime data, content packs, rewards, dialogue, quest logic, or packet behavior changed.

## Starting NPC

| Field | Value | Evidence | Confidence |
| --- | --- | --- | --- |
| NPC | Marcus Stone | `20260614-195107/events.log:100` | confirmed |
| Identity | `SimpleChar:782DE567` | `20260614-195107/events.log:100` | confirmed |
| Observed position | `3630.962, 40.985, 823.1738` | `20260614-195107/events.log:100` | confirmed observed position |
| Full-update visual packet | `HeadMesh=40667`, `MonsterScale=105`, `VisualFlags=31` | `20260614-195725/events.log:4712-4713` | confirmed full-update evidence |

## Main Chain

### 1. `Mission:5514B18F`

| Field | Value | Exact identity | Capture folder | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |
| Mission ID | `Mission:5514B18F` | `Mission:5514B18F` | `20260614-194454` | `packets.hex.log:5949`, packet `#5497` | confirmed |
| Title | `Talk to Marcus Stone` | `Mission:5514B18F` | `20260614-194454` | decoded QuestFullUpdate packet `#5497` | confirmed |
| Objective | `Talk to Marcus Stone.` | `Mission:5514B18F` | `20260614-194454` | decoded QuestFullUpdate packet `#5497` | confirmed |
| Target NPC | Marcus Stone | `SimpleChar:782DE567` | `20260614-195107` | `events.log:100`, `npc-interactions.log:39-42` | confirmed |

Captured flow:

- Rex handoff sends `Mission:5514B18F` as `Talk to Marcus Stone`.
- Player opens Marcus chat at `20260614-195107/npc-interactions.log:39-42`.
- Initial Marcus options appear at `chat-dialogue.log:7-8`.
- After the player selects option `0`, Marcus offers the fire-help branch through `chat-dialogue.log:11-16`.
- `Mission:5514B18F` is removed by `QuestMessage Action=Delete` at `events.log:1645-1646`.
- `Mission:5514B194` appears from QuestFullUpdate packet `#1323`.

`CharacterAction Action=59` appears at `events.log:1641-1644`; its meaning remains unresolved.

Next mission: `Mission:5514B194`.

### 2. `Mission:5514B194`

| Field | Value | Exact identity | Capture folder | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |
| Mission ID | `Mission:5514B194` | `Mission:5514B194` | `20260614-195107` | `packets.hex.log:1407`, packet `#1323` | confirmed |
| Title | `Extinguish the Gas Fire` | `Mission:5514B194` | `20260614-195107` | decoded QuestFullUpdate packet `#1323` | confirmed |
| Objective | `(Left Click) the Compact Fire Suppressant Container in your inventory to lift it up, then Left Click the Gas Fire to apply the fire suppressant.` | `Mission:5514B194` | `20260614-195107` | decoded QuestFullUpdate packet `#1323` | confirmed |
| Handout item packet | TemplateAction item `296780` to `OverflowWindow:0000` | item `296780` | `20260614-195107` | `events.log:1627-1628` | captured item identity; display name from quest text |
| Captured use target | `Terminal:57369E98`, Gas Fire | `Terminal:57369E98` | `20260614-195107` | `events.log:1961-1988` | confirmed interaction target for this completion |

Captured flow:

- Marcus handout packet gives item identity `296780` to overflow at `events.log:1627-1628`.
- Player uses `Inventory:004A` on `Terminal:57369E98` via `GenericCmd Action=UseItemOnItem` at `events.log:1961-1962`.
- The server acknowledges the same use at `events.log:1987-1988`.
- Feedback says `You extinguish the Gas Fire.` at `events.log:2005-2006`.
- `Mission:5514B194` is removed by `QuestMessage Action=Delete` at `events.log:2025-2026`.
- `Mission:5514B196` appears from QuestFullUpdate packet `#1677`.

Gas Fire identities observed nearby include `Terminal:57369E96`, `Terminal:57369E97`, `Terminal:57369E98`, `Terminal:572E9F09`, `Terminal:57330FB2`, `Terminal:57369CA9`, and `Terminal:57369E78`. The captured completion used `Terminal:57369E98`; static/runtime data for any Gas Fire must be extracted by exact identity in a separate task.

`CharacterAction Action=59` appears at `events.log:2021-2024`; its meaning remains unresolved.

Next mission: `Mission:5514B196`.

### 3. `Mission:5514B196`

| Field | Value | Exact identity | Capture folder | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |
| Mission ID | `Mission:5514B196` | `Mission:5514B196` | `20260614-195107` | `packets.hex.log:1773`, packet `#1677` | confirmed |
| Title | `Return to Marcus` | `Mission:5514B196` | `20260614-195107` | decoded QuestFullUpdate packet `#1677` | confirmed |
| Objective | `Talk to Marcus Stone and hand him the Compact Fire Suppressant Container.` | `Mission:5514B196` | `20260614-195107` | decoded QuestFullUpdate packet `#1677` | confirmed |
| Trade target | Marcus Stone | `SimpleChar:782DE567` | `20260614-195107` | `events.log:2507-2550` | confirmed |

Captured flow:

- Player returns to Marcus and gets visible options `Thanks.` and `Goodbye` at `chat-dialogue.log:23-24`.
- Marcus opens trade at `chat-dialogue.log:27-28`.
- Player trades `Inventory:004A` to Marcus at `events.log:2507-2508`.
- Player finishes trade at `events.log:2541-2542`.
- `KnuBotRejectedItems` reports zero rejected items at `events.log:2549-2550`.
- Completion-side packets show `Cash=2145`, `LastSaveXP=1450`, and `XP=1450` at `system-messages.log:627-642`.
- TemplateAction item `296569` goes to overflow at `events.log:2569-2570`.
- `Mission:5514B196` is removed by `QuestMessage Action=Delete` at `events.log:2581-2582`.
- `Mission:5514B197` appears from QuestFullUpdate packet `#2193`.

Reward deltas and item `296569` name are unresolved in this extraction. The packet stream proves the final stat values and item identity, not the complete before/after delta.

`CharacterAction Action=59` appears at `events.log:2577-2580`; its meaning remains unresolved.

Next mission: `Mission:5514B197`.

### 4. `Mission:5514B197`

| Field | Value | Exact identity | Capture folder | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |
| Mission ID | `Mission:5514B197` | `Mission:5514B197` | `20260614-195107` | `packets.hex.log:2311`, packet `#2193` | confirmed |
| Title | `Talk to Flint Novak` | `Mission:5514B197` | `20260614-195107` | decoded QuestFullUpdate packet `#2193` | confirmed |
| Objective | `Talk to Flint Novak.` | `Mission:5514B197` | `20260614-195107` | decoded QuestFullUpdate packet `#2193` | confirmed |
| Next NPC | Flint Novak | `SimpleChar:782DE569` | `20260614-195725` | `events.log:141` | confirmed |

Captured flow:

- Marcus sends `Mission:5514B197`, `Talk to Flint Novak`, after B196 completion.
- `20260614-195725/events.log:141` confirms Flint Novak identity `SimpleChar:782DE569`.
- `20260614-195725/events.log:5263-5266` later shows B197 delete plus another QuestFullUpdate, but that is Flint-chain context and should be decoded in a separate slice.

Next implementation target after Marcus B18F/B194 work: decode and implement Flint separately, not as part of Marcus B18F.

## Optional Marcus Branch

The capture also includes an optional Marcus branch after the main handoff has reached B197.

### A. `Mission:5514B199`

| Field | Value | Exact identity | Capture folder | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |
| Mission ID | `Mission:5514B199` | `Mission:5514B199` | `20260614-195107` | `packets.hex.log:2765`, packet `#2641` | confirmed |
| Title | `Use the Stim on a Wounded Dockworker` | `Mission:5514B199` | `20260614-195107` | decoded QuestFullUpdate packet `#2641` | confirmed |
| Objective | `Target a Wounded Dockworker and use the Health Regeneration Stim (Right-Click).` | `Mission:5514B199` | `20260614-195107` | decoded QuestFullUpdate packet `#2641` | confirmed |
| Handout item packet | TemplateAction item `297044` to overflow | item `297044` | `20260614-195107` | `events.log:3051-3052` | captured item identity; display name from quest text |
| Target family | Wounded Dockworker | multiple SimpleChar identities | `20260614-195107` | `events.log:92`, `101`, `867-914` | confirmed target family |

Captured flow:

- Marcus offers optional branch through visible option `Are those wounded workers your guys?` at `chat-dialogue.log:47-48`.
- Player selects that option and then `I'll do it, just tell me how.` at `chat-dialogue.log:49-56`.
- TemplateAction item `297044` goes to overflow at `events.log:3051-3052`.
- `Mission:5514B199` appears from QuestFullUpdate packet `#2641`.
- Later, TemplateAction item `297044` appears in `Inventory:004B` at `events.log:3752-3753`.
- `Mission:5514B199` is removed by `QuestMessage Action=Delete` at `events.log:3766-3767`.
- `Mission:5514B19A` appears from QuestFullUpdate packet `#3297`.

The exact use packet for the Wounded Dockworker interaction should be extracted in a separate focused task before implementation.

### B. `Mission:5514B19A`

| Field | Value | Exact identity | Capture folder | Packet/log source | Confidence |
| --- | --- | --- | --- | --- | --- |
| Mission ID | `Mission:5514B19A` | `Mission:5514B19A` | `20260614-195107` | `packets.hex.log:3451`, packet `#3297` | confirmed |
| Title | `Return to Marcus Stone` | `Mission:5514B19A` | `20260614-195107` | decoded QuestFullUpdate packet `#3297` | confirmed |
| Objective | `Return to Marcus Stone and hand him the Health Regeneration Stim.` | `Mission:5514B19A` | `20260614-195107` | decoded QuestFullUpdate packet `#3297` | confirmed |
| Trade target | Marcus Stone | `SimpleChar:782DE567` | `20260614-195107` | `events.log:6793-6844` | confirmed |

Captured flow:

- Player returns to Marcus and selects `I healed one of your workers with the Health Regeneration Stim.` at `chat-dialogue.log:95-98`.
- Marcus opens trade at `chat-dialogue.log:99-100`.
- Player trades `Inventory:004B` to Marcus at `events.log:6793-6794`.
- Player finishes trade at `events.log:6821-6822`.
- `KnuBotRejectedItems` reports zero rejected items at `events.log:6843-6844`.
- Completion-side packets show `Cash=3185` and `XP=2230` at `system-messages.log:1949-1952`.
- TemplateAction item `291082` with `Unknown1=50` goes to overflow at `events.log:6851-6852`.
- TemplateAction item `291043` with `Unknown1=25` goes to overflow at `events.log:6859-6860`.
- `Mission:5514B19A` is removed by `QuestMessage Action=Delete` at `events.log:6871-6872`.

No immediate next QuestFullUpdate after B19A delete was found in `20260614-195107`. Item names and reward deltas remain unresolved.

## Remaining Unknowns

- `CharacterAction Action=59` remains unresolved and must not be implemented by name or guessed semantics.
- `QuestMessage Action=Delete` is packet-level mission-window cleanup evidence only unless a future task proves broader gameplay meaning.
- B196 and B19A reward deltas are unresolved in this extraction.
- TemplateAction item names for `296569`, `291082`, and `291043` are unresolved.
- The exact Wounded Dockworker use packet is not mapped here.
- Flint Novak chain after `Mission:5514B197` is outside this Marcus extraction.

## Recommended Next Slice

Use the first implementation slice only:

`Marcus B18F gated dialogue handoff + B194 preview`.

Do not bundle in rewards, B196, B197/Flint, B199/B19A optional branch, action `59`, DB persistence, or item-name assumptions.
