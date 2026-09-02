# Low-Level Rubi-Ka Mission Slider Capture Matrix

## Decision

`MissionOfferHarvester` 1.4.0 capture-contract version 3 now provides the
explicit-slider gate required before the level-2 campaign. It accepts an exact
Easy/Hard detent and either a named complete state or all six explicit secondary
values. It verifies the constructed, serialized, transmitted, and returned
state and fails closed on any mismatch. Run the documented one-request live
acceptance before beginning the matrix below.

Harvester 1.4.0 additionally automates the resumable 27-state matrix. After the
accepted states 1-7, run all remaining states with one command:

```text
/missionharvest matrix 8 27 2 2.0
```

The plugin changes state internally, reapplies and verifies all seven controls
before every request, and preserves each state separately in the same journal.

This is an evidence-planning gate only. It does not authorize or implement any
AORebirth runtime mission behavior.

## Evidence standard

Three different claims must remain separate:

1. **Structurally proven:** the dedicated harvester/AOSharp request path,
   serialized fields, and fields returned by AOSharp.
2. **Observed locally:** completed level-2 harvester journals.
3. **Historical player documentation:** descriptions of server effects. These
   are useful hypotheses, not server-code proof and not probability evidence.

No Funcom mission-generator source was available. Therefore a slider being sent
to the server does not by itself prove which returned fields it changes.

## Exact control and wire representation

The dedicated capture tool must expose Easy/Hard as an exact detent `1..11` and
the other six controls as canonical semantic values `-100..100`. The retained
AO wire convention encodes negative signed values by unchecked byte conversion
and encodes semantic center `0` as signed `-1`/raw byte `255`. AOSharp serializes
all seven fields as ordered bytes in `QuestAlternativeMessage.MissionSliders`.
Malis source corroborates this historical encoding but is not used to automate,
configure, observe, or capture the campaign.

| Semantic position | Capture-tool semantic value | AOSharp/wire byte |
| --- | ---: | ---: |
| Full left | -100 | 156 |
| Left interior representative | -50 | 206 |
| Center | 0 | 255 |
| Right interior representative | +50 | 50 |
| Full right | +100 | 100 |

The raw byte order is not semantic slider order. Byte `255` is the capture
tool's canonical center encoding; it is not the right-hand endpoint. Semantic
values `0` and `-1` collide at wire byte `255`, so record both the capture
tool's canonical semantic value and the transmitted byte.

Easy/Hard does not use this signed conversion. Its detent `1..11` is passed
unchanged as the request byte. For a level-2 character, the current
retained table maps detents `1..5` to QL 1, `6..9` to QL 2, and `10..11` to QL
3. The duplicate-QL detents are still different request bytes and must not be
assumed equivalent without controlled evidence.

## AOSharp and dedicated capture-tool behavior

- AOSharp's inspected `MissionTerminal` API accepts seven request bytes. Native
  AO UI slider state is irrelevant because the dedicated harvester generates
  requests directly.
- AOSharp deserializes the seven bytes on `QuestAlternativeMessage`; the
  harvester can preserve the returned `MissionSliders` bytes.
- Harvester 1.4.0 owns automation and capture. It directly controls the exact
  detent and all six secondary values and records semantic and native forms.
- It does not read, observe, invoke, or depend on Malis.
- Existing level-2 journals contain centered (`255`) samples for all eleven
  difficulty detents. No existing non-centered state is a controlled
  one-variable-at-a-time comparison.

## Slider assessment

| Slider | Known effect | Proven? | AOSharp-readable? | Capture-tool controlled? | Must test at level 2? | Recommended states |
| --- | --- | --- | --- | --- | --- | --- |
| Easy ↔ Hard | Selects one of 11 difficulty request bytes; retained level table maps it to intended mission QL. Historical guides also associate it with mob, chest, and reward level. | Request encoding and table mapping are proven from source. AOSharp offers do not expose authoritative response-side mission QL, so every downstream effect is not yet live-proven. | Request/response byte yes. | Yes, explicitly in 1.4.0. | Yes. Existing centered level-2 data already covers detents 1..11. | Preserve all detents. For the new matrix fix detent 1/QL1, then add centered bridges at detent 6/QL2 and detent 10/QL3. |
| Good ↔ Bad | Historical sources consistently associate it with mission type and degree of violence. This can change objective type and the five-icon mix. | Historical/player evidence only; controlled local causality is not proven. | Request/response byte yes. | Yes in 1.4.0. | **Yes.** Omitting it invalidates a claim that the observed five-type mix is complete or representative. | Semantic `-100,-50,0,+50,+100` / raw `156,206,255,50,100`. |
| Order ↔ Chaos | Historical sources associate it with human versus monster enemy families; full Chaos has a claimed special paired-mob selection behavior. | Historical/player evidence only. The offer harvester cannot observe interior enemy populations. | Request/response byte yes. | Yes in 1.4.0. | **Yes for discovery.** QL1 enemy pools could otherwise remain unobserved. | Semantic `-100,-50,0,+50,+100`; accept/inspect one QL1 mission at center and each endpoint only after interior capture is ready. |
| Open ↔ Hidden | Historical sources associate it with locked doors/chests and hidden/secret spaces. | Historical/player evidence only. Not visible in mission offers. | Request/response byte yes. | Yes in 1.4.0. | **Yes for discovery.** | Semantic `-100,-50,0,+50,+100`; inspect center and endpoint interiors later. |
| Physical ↔ Mystical | Historical sources associate it with weapon-oriented versus nano-using enemy populations/professions. | Historical/player evidence only. Not visible in mission offers. | Request/response byte yes. | Yes in 1.4.0. | **Yes for discovery.** | Semantic `-100,-50,0,+50,+100`; inspect center and endpoint interiors later. |
| Head On ↔ Stealth | Historical sources conflict: one associates Stealth with traps/cameras/turrets; another associates the slider with aggression and assist/sneak behavior. | Conflicting historical/player evidence; controlled local proof is required. | Request/response byte yes. | Yes in 1.4.0. | **Yes.** The disagreement makes omission unsafe. | Semantic `-100,-50,0,+50,+100`; inspect traps, security devices, aggression, and assists at center and endpoints. |
| Money ↔ XP | Historical sources consistently associate it with the credits/XP reward split. Both returned numeric fields are exposed by AOSharp. | Transport and returned fields are proven; causal magnitude and possible effects on item rewards are not yet controlled live evidence. | Request/response byte yes. | Yes in 1.4.0. | **Yes.** It is the most direct non-difficulty numeric-output test. | Semantic `-100,-50,0,+50,+100` / raw `156,206,255,50,100`. |

No historical source reviewed ties a non-difficulty slider directly to mission
destination or reward-item selection. That is absence of proof, not proof of no
effect. Destination and every reward descriptor must remain in every comparison.

### Plausible affected output fields

`Historical` below means a player-documented hypothesis to test. `Unknown`
means the available evidence does not justify either an effect or no effect.

| Slider | Mission QL | Type/objective | Reward item / reward QL | Credits / XP | Destination | Environment/layout | Enemy population/difficulty | Other returned fields |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Easy/Hard | Intended QL table mapping; authoritative returned QL unavailable | Unknown | Reward/chest QL historically associated; selection unknown | Scaling plausible but uncontrolled | Unknown | Lock difficulty historically associated | Mob level/difficulty historically associated | Preserve all |
| Good/Bad | No evidence | **Historical** mission-type/objective mix | Unknown / unknown | No evidence | Unknown | Mob-count claim is explicitly disputed in the forum guide | Violence/type mix historical | Title/description could change with objective |
| Order/Chaos | No evidence | Objective target identity could change indirectly | Unknown / no evidence | No evidence | Unknown | No evidence | **Historical** human/monster family and full-Chaos pairing | Preserve all |
| Open/Hidden | No evidence | No evidence | Mission reward unknown; chest contents are an interior channel | Unlock XP is interior, not offered XP | Unknown | **Historical** locks, chests, secret walls | No evidence | Not observable from offers alone |
| Physical/Mystical | No evidence | Target identity could change indirectly | Unknown / no evidence | No evidence | Unknown | No evidence | **Historical** weapon/nano profession or combat style | Not observable from offers alone |
| Head On/Stealth | No evidence | No evidence | Unknown / no evidence | No evidence | Unknown | **Conflicting historical** traps/security devices | **Conflicting historical** aggression/assist/sneak behavior | Not observable from offers alone |
| Money/XP | No evidence | No evidence | Selection and QL unknown | **Historical** direct reward split; exact causal curve unproven | Unknown | No evidence | No evidence | Preserve all to detect an unexpected coupling |

## Recommended level-2 offer matrix

Use one ordinary solo terminal, one level-2 character, unchanged faction and
inventory conditions, and no accepted/completed missions during the offer-only
matrix. Do not load or use Malis. Hold difficulty at detent `1` (QL1) and hold
all six capture-tool semantic values at `0` except the single slider named by
the test.

Run these exact states:

1. One all-centered baseline.
2. For each of Good/Bad, Order/Chaos, Open/Hidden, Physical/Mystical,
   Head On/Stealth, and Money/XP, independently test UI `-100`, `-50`, `+50`,
   and `+100`, returning every other control to UI `0` each time.
3. Add an all-centered detent-6/QL2 bridge.
4. Add an all-centered detent-10/QL3 bridge.

This is 27 states: 25 QL1 states (one baseline plus four non-center states for
each of six sliders) and two centered QL bridges. Use **two requests per state**
for discovery only: 54 requests and 270 offers total. That sample is intended
only to detect dimensions and verify recording; it must not be used to estimate
probabilities or exhaust a reward pool.

The historical guide describes five potentially meaningful position regions:
left endpoint, left interior, center, right interior, and right endpoint. The
`-50/+50` representatives preserve those interior regions without a large
campaign.

## Interior follow-up before the level-2 character is allowed to level

Offer data cannot prove layout, locks, traps, security devices, enemy family,
profession, or aggression. After an interior mission capture path is ready,
accept and inspect—without completing or killing—one QL1 mission at center and
at each endpoint for:

- Order/Chaos
- Open/Hidden
- Physical/Mystical
- Head On/Stealth

Run these sequentially and remove each inspected mission afterward. Do not let
the character gain XP. Intermediate interior states, interaction tests, and
repeat sampling can be deferred until a controlled endpoint effect is observed.

## Data required on every request

- session/request ID, UTC timestamp, client/AOSharp/harvester versions;
- character identity, level, side, profession, and relevant organization fields;
- solo/team scope;
- terminal identity, name, playfield, local/global coordinates, and rotation;
- Easy/Hard detent byte, intended QL, QL-table revision/hash, and whether the
  capture tool resolved or explicitly selected the detent;
- for every other slider: semantic name, signed capture-tool value, transmitted raw
  byte, and returned/echoed raw byte;
- cash before/after and request cost when available;
- the complete ordered five-offer cohort, including mission identity, icon/type,
  title, description, destination, credits, XP, every reward ID/QL/name, all
  AOSharp-exposed fields, and raw unknown chunks;
- accepted offer index and mission identity for any interior follow-up;
- for interior evidence: instance identity, layout/rooms, door/chest lock state,
  secret areas, traps, cameras/turrets, and enemy identity/family/profession,
  level, position, aggression, and assist behavior.

Omitting slider bytes invalidates causal comparison. Omitting the canonical
signed value loses the `0` versus `-1` distinction. Center-only Good/Bad data cannot
support mission-type coverage claims. Center-only Money/XP data cannot support
the credits/XP rule. Offer-only data cannot support any conclusion about the
four interior/environment sliders.

## Capture-tool gate

The dedicated harvester now accepts explicit arguments for difficulty detent
and all six canonical signed or raw slider values and records both semantic and
native representations. No Malis mode or dependency is part of this workflow.

It does not silently resolve duplicate QLs to the first detent. It preserves the
exact serialized/transmitted request and received response bytes and rejects a
request or stops further sends if the configured state cannot be verified.

Before the matrix, perform only this one-request acceptance:

```text
/missionharvest start 1 1 CENTERED_BASELINE 2.0
```

Require one transmitted raw request, one received raw response, one verified
cohort, and automatic completion in the schema-3 journal.

## Sources

- Governed AOSharp source archive at commit `b45b7a05...`, members
  `AOSharp.Core/Dynel/MissionTerminal.cs`,
  `MissionSliders.cs`, and `QuestAlternativeMessage.cs`, retained under
  `docs/reference/missions/malis/raw/`.
- `Tools/AOSharpMissionOfferHarvester/Main.cs` and completed local
  `%LOCALAPPDATA%/AOSharp/MissionOfferHarvester/sessions` journals.
- Governed Malis source at commit `3ac9943a...` is retained only as historical
  corroboration of the signed-byte convention; it is not an operational input.
- [Rubi-Ka Mission Settings 101](https://forums.funcom.com/t/rubi-ka-mission-settings-101/6664), explicitly phrased by its author as what they "think" the sliders do.
- [AO-Universe: How to pull a mission](https://www.ao-universe.com/guides/classic-ao/gameplay-guides-6/how-to-pull-a-mission), independent historical player documentation.
- [Anarchy Online Wiki: Mission Options](https://wiki.aodb.us/wiki/Quests), independent historical player documentation.
