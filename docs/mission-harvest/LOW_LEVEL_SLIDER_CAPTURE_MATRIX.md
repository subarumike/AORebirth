# Low-Level Rubi-Ka Mission Slider Capture Matrix

## Decision

Do not begin the large level-2 mission harvest with the current active-roller
command. `MissionOfferHarvester` capture-contract version 2 records the seven
returned slider bytes, but version 1.2.1 actively sends the selected difficulty
and hard-codes all six other fields to byte `255`. It also resolves a target QL
to the first matching difficulty detent. That is sufficient for a centered
baseline, but it cannot execute the controlled slider matrix below.

This is an evidence-planning gate only. It does not authorize or implement any
AORebirth runtime mission behavior.

## Evidence standard

Three different claims must remain separate:

1. **Structurally proven:** exact Malis/AOSharp source, serialized request
   fields, value conversion, and fields returned by AOSharp.
2. **Observed locally:** completed level-2 harvester journals and the installed
   Malis settings file.
3. **Historical player documentation:** descriptions of server effects. These
   are useful hypotheses, not server-code proof and not probability evidence.

No Funcom mission-generator source was available. Therefore a slider being sent
to the server does not by itself prove which returned fields it changes.

## Exact control and wire representation

Malis commit `3ac9943a4943b8cb80eda9e40359729e656686b0` defines Easy/Hard as an
integer UI range `1..11`. The other six controls use UI range `-100..100`.
Immediately before calling `MissionTerminal.RequestMissions`, Malis changes an
exact UI value of `0` to signed `-1` and casts each signed value to `byte`.
AOSharp serializes all seven fields as ordered bytes in
`QuestAlternativeMessage.MissionSliders`.

| Semantic position | Malis UI value | AOSharp/wire byte |
| --- | ---: | ---: |
| Full left | -100 | 156 |
| Left interior representative | -50 | 206 |
| Center | 0 | 255 |
| Right interior representative | +50 | 50 |
| Full right | +100 | 100 |

The raw byte order is not semantic slider order. Byte `255` means Malis center
because Malis deliberately encodes center as signed `-1`; it is not the
right-hand endpoint. UI values `0` and `-1` collide at wire byte `255`, so a
capture that stores only the byte cannot distinguish those two Malis control
positions. Record both the signed Malis UI value and the transmitted byte.

Easy/Hard does not use this signed conversion. Its Malis UI value `1..11` is
passed unchanged as the request byte. For a level-2 character, the current
retained table maps detents `1..5` to QL 1, `6..9` to QL 2, and `10..11` to QL
3. The duplicate-QL detents are still different request bytes and must not be
assumed equivalent without controlled evidence.

## AOSharp and Malis behavior

- AOSharp's inspected `MissionTerminal` API has no property that exposes the
  current native AO mission-window control positions. It accepts seven request
  bytes.
- AOSharp deserializes the seven bytes on `QuestAlternativeMessage`; the
  harvester can preserve the returned `MissionSliders` bytes.
- Malis can read its own seven UI controls. It passes every control on every
  request.
- Malis changes only Easy/Hard automatically, and only when `AutoAdjustQl` is
  enabled while rolling a configured item. The other six controls remain the
  saved/user-selected values.
- The currently installed Malis settings have `AutoAdjustQl=false`, Easy/Hard
  `6`, and all six other controls at UI `0`.
- Existing level-2 journals contain centered (`255`) samples for all eleven
  difficulty detents. One earlier Malis-observe journal contains a non-centered
  returned state (`156,47,187,187,230,44`), but it was not a controlled
  one-variable-at-a-time test and cannot establish an effect.

## Slider assessment

| Slider | Known effect | Proven? | AOSharp-readable? | Malis-controlled? | Must test at level 2? | Recommended states |
| --- | --- | --- | --- | --- | --- | --- |
| Easy ↔ Hard | Selects one of 11 difficulty request bytes; retained level table maps it to intended mission QL. Historical guides also associate it with mob, chest, and reward level. | Request encoding and table mapping are proven from source. AOSharp offers do not expose authoritative response-side mission QL, so every downstream effect is not yet live-proven. | Request/response byte yes; native AO UI position no. | Yes; Malis may auto-change it when `AutoAdjustQl=true`. | Yes. Existing centered level-2 data already covers detents 1..11. | Preserve all detents. For the new matrix fix detent 1/QL1, then add centered bridges at detent 6/QL2 and detent 10/QL3. |
| Good ↔ Bad | Historical sources consistently associate it with mission type and degree of violence. This can change objective type and the five-icon mix. | Historical/player evidence only; controlled local causality is not proven. | Returned byte yes; native AO UI position no. | Manual/saved value; not auto-changed. | **Yes.** Omitting it invalidates a claim that the observed five-type mix is complete or representative. | UI `-100,-50,0,+50,+100` / raw `156,206,255,50,100`. |
| Order ↔ Chaos | Historical sources associate it with human versus monster enemy families; full Chaos has a claimed special paired-mob selection behavior. | Historical/player evidence only. The offer harvester cannot observe interior enemy populations. | Returned byte yes; native AO UI position no. | Manual/saved value; not auto-changed. | **Yes for discovery.** QL1 enemy pools could otherwise remain unobserved. | UI `-100,-50,0,+50,+100`; accept/inspect one QL1 mission at center and each endpoint only after interior capture is ready. |
| Open ↔ Hidden | Historical sources associate it with locked doors/chests and hidden/secret spaces. | Historical/player evidence only. Not visible in mission offers. | Returned byte yes; native AO UI position no. | Manual/saved value; not auto-changed. | **Yes for discovery.** | UI `-100,-50,0,+50,+100`; inspect center and endpoint interiors later. |
| Physical ↔ Mystical | Historical sources associate it with weapon-oriented versus nano-using enemy populations/professions. | Historical/player evidence only. Not visible in mission offers. | Returned byte yes; native AO UI position no. | Manual/saved value; not auto-changed. | **Yes for discovery.** | UI `-100,-50,0,+50,+100`; inspect center and endpoint interiors later. |
| Head On ↔ Stealth | Historical sources conflict: one associates Stealth with traps/cameras/turrets; another associates the slider with aggression and assist/sneak behavior. | Conflicting historical/player evidence; controlled local proof is required. | Returned byte yes; native AO UI position no. | Manual/saved value; not auto-changed. | **Yes.** The disagreement makes omission unsafe. | UI `-100,-50,0,+50,+100`; inspect traps, security devices, aggression, and assists at center and endpoints. |
| Money ↔ XP | Historical sources consistently associate it with the credits/XP reward split. Both returned numeric fields are exposed by AOSharp. | Transport and returned fields are proven; causal magnitude and possible effects on item rewards are not yet controlled live evidence. | Returned byte yes; native AO UI position no. | Manual/saved value; not auto-changed. | **Yes.** It is the most direct non-difficulty numeric-output test. | UI `-100,-50,0,+50,+100` / raw `156,206,255,50,100`. |

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
matrix. Disable Malis `AutoAdjustQl`. Hold difficulty at detent `1` (QL1) and
hold all six signed sliders at UI `0` except the single slider named by the test.

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
- Easy/Hard detent byte, intended QL, QL-table revision/hash, and whether Malis
  auto-adjusted it;
- for every other slider: semantic name, signed Malis UI value, transmitted raw
  byte, and returned/echoed raw byte;
- cash before/after and request cost when available;
- the complete ordered five-offer cohort, including mission identity, icon/type,
  title, description, destination, credits, XP, every reward ID/QL/name, all
  AOSharp-exposed fields, and raw unknown chunks;
- accepted offer index and mission identity for any interior follow-up;
- for interior evidence: instance identity, layout/rooms, door/chest lock state,
  secret areas, traps, cameras/turrets, and enemy identity/family/profession,
  level, position, aggression, and assist behavior.

Omitting slider bytes invalidates causal comparison. Omitting signed Malis UI
values loses the `0` versus `-1` distinction. Center-only Good/Bad data cannot
support mission-type coverage claims. Center-only Money/XP data cannot support
the credits/XP rule. Offer-only data cannot support any conclusion about the
four interior/environment sliders.

## Capture-tool gate

Before running this matrix, the tooling must provide one of these evidence-safe
paths:

- explicit arguments for difficulty detent and all six signed slider values,
  recording signed and raw representations; or
- a restored Malis-observe mode that correlates each response with an exact
  snapshot of Malis's seven UI controls.

The tool must not silently resolve duplicate QLs to the first detent during a
detent-equivalence test. It must preserve the response slider bytes and reject a
request if the configured state cannot be represented exactly.

## Sources

- Governed Malis source archive at commit `3ac9943a...`, members
  `UI/Views/SliderView.xml`, `Views/SliderView.cs`, and `MainWindow.cs`, retained
  under `docs/reference/missions/malis/raw/`.
- Governed AOSharp source archive at commit `b45b7a05...`, members
  `AOSharp.Core/Dynel/MissionTerminal.cs`,
  `MissionSliders.cs`, and `QuestAlternativeMessage.cs`, retained under
  `docs/reference/missions/malis/raw/`.
- `Tools/AOSharpMissionOfferHarvester/Main.cs` and completed local
  `%LOCALAPPDATA%/AOSharp/MissionOfferHarvester/sessions` journals.
- [Rubi-Ka Mission Settings 101](https://forums.funcom.com/t/rubi-ka-mission-settings-101/6664), explicitly phrased by its author as what they "think" the sliders do.
- [AO-Universe: How to pull a mission](https://www.ao-universe.com/guides/classic-ao/gameplay-guides-6/how-to-pull-a-mission), independent historical player documentation.
- [Anarchy Online Wiki: Mission Options](https://wiki.aodb.us/wiki/Quests), independent historical player documentation.
