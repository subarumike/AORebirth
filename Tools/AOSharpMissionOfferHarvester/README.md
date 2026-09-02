# AOSharp Mission Offer Harvester

This dedicated AOSharp plugin owns mission-terminal automation, explicit slider
control, and raw evidence capture. Malis is not loaded, called, observed, or
required. The plugin is evidence-only and does not change AORebirth mission
generation.

Build and run all offline gates with:

```text
cmd /d /c Tools\build_mission_offer_harvester.cmd
```

The build verifies the retained AOSharp SDK `1.0.106`, generates/checks the
governed mission-QL table, compiles the plugin, and runs the slider resolver and
fail-closed gate tests. The DLL is emitted at
`Tools\AOSharpMissionOfferHarvester\bin\Release\AOSharpMissionOfferHarvester.dll`.
Codex does not load the plugin, inject it, launch AO, or control the client.

## Explicit commands

Select/use one ordinary mission terminal, then use either a named preset or an
explicit custom state:

```text
/missionharvest start <difficulty-detent 1-11> <requests> <preset> [interval-seconds]
/missionharvest startcustom <difficulty-detent 1-11> <requests> <good-bad> <order-chaos> <open-hidden> <physical-mystical> <headon-stealth> <money-xp> [interval-seconds]
/missionharvest matrix <start-state 1-27> <end-state 1-27> <requests-per-state> [interval-seconds]
/missionharvest status
/missionharvest stop
```

There are no implicit secondary-slider defaults. A preset is mandatory for
`start`; all six secondary values are mandatory for `startcustom`. Invalid or
unrepresentable input sends no request. Secondary values accept `FULL_LEFT`,
`CENTER`, `FULL_RIGHT`, a signed semantic integer from `-100` through `100`, or
an exact native byte as `raw:0` through `raw:255`.

| Semantic position | Signed value | AOSharp/native byte |
| --- | ---: | ---: |
| Full left | -100 | 156 |
| Center | 0 | 255 |
| Full right | 100 | 100 |

Easy/Hard is the explicit one-based detent byte and does not use the signed
conversion. The plugin reports the complete resolved state and stable
SHA-256 slider-state ID before starting. Its expected mission QL is a static
character-level/detent table lookup, not a claimed response-side mission QL.

Named presets:

- `CENTERED_BASELINE`
- `GOOD_BAD_FULL_LEFT`, `GOOD_BAD_FULL_RIGHT`
- `ORDER_CHAOS_FULL_LEFT`, `ORDER_CHAOS_FULL_RIGHT`
- `OPEN_HIDDEN_FULL_LEFT`, `OPEN_HIDDEN_FULL_RIGHT`
- `PHYSICAL_MYSTICAL_FULL_LEFT`, `PHYSICAL_MYSTICAL_FULL_RIGHT`
- `HEADON_STEALTH_FULL_LEFT`, `HEADON_STEALTH_FULL_RIGHT`
- `MONEY_XP_FULL_LEFT`, `MONEY_XP_FULL_RIGHT`

Every non-baseline preset changes exactly one secondary slider and centers the
other five. The default interval is 2.0 seconds, the minimum is 1.5 seconds,
and only one request may be outstanding.

## Resumable level-2 matrix

Harvester 1.4 can run the complete 27-state discovery matrix without manually
pasting one command per state. The command is restricted to a level-2 character,
applies and verifies the complete state before every request, reports each state
transition in chat, and fails closed for the entire run on any evidence failure.

| Indices | States |
| --- | --- |
| 1 | Detent-1 centered baseline |
| 2-5 | Good/Bad full left, full right, -50, +50 |
| 6-9 | Order/Chaos full left, full right, -50, +50 |
| 10-13 | Open/Hidden full left, full right, -50, +50 |
| 14-17 | Physical/Mystical full left, full right, -50, +50 |
| 18-21 | Head On/Stealth full left, full right, -50, +50 |
| 22-25 | Money/XP full left, full right, -50, +50 |
| 26 | Detent-6 centered QL2 bridge |
| 27 | Detent-10 centered QL3 bridge |

Ranges make the campaign resumable. After states 1-7 are accepted, this one
command runs every remaining state with two requests each:

```text
/missionharvest matrix 8 27 2 2.0
```

The single schema-3 session retains a distinct matrix index, label, requested
state, native values, slider-state ID, raw packets, and cohort association on
every request.

## Capture contract version 3

Before every send, the plugin constructs all seven native slider fields, reads
them back from the request object, serializes the request, disassembles the
exact bytes, and verifies the seven fields and terminal identity. It then sends
those exact bytes. The raw outbound event must match the pre-send SHA-256 and
slider state. The raw inbound response packet, decoded returned sliders, cohort,
and each offer are tied to the same request ID, cohort ID, terminal, and slider
state ID.

The JSONL preserves:

- requested semantic state and resolved native bytes;
- native request-object readback and pre-send decoded values;
- exact serialized and observed transmitted packets as Base64, byte length,
  and SHA-256;
- exact received response packet with the same raw evidence;
- returned sliders and verification disposition;
- request-time terminal origin and complete mission destination;
- the complete ordered offer cohort, reward descriptors, credits, XP, known
  mission-icon type, every AOSharp `MissionInfo` public field, and raw unknown
  chunks.

Native AO UI slider state is not a dependency: the harvester constructs the
request packet directly. The journal explicitly records the pre-construction UI
state as not applicable and the constructed request object as the verifiable
native state.

Any missing API, readback mismatch, serialization mismatch, transmitted hash or
slider mismatch, response mismatch, terminal change, association failure, or
timeout writes a structured error and stops the session. No later request is
sent. Raw events are append-only and `Flush(true)` is performed after every
event.

Normalize completed or partial journals with:

```text
Tools\modern_mission_capture_planner.cmd --normalize-session "<events.jsonl>" --output-dir "<normalized-session-directory>"
```

The normalizer accepts schemas 1, 2, and 3. Schema 3 retains request/cohort
slider IDs, semantic/native values, outbound/inbound raw packets, verification,
and structured failures. A finite capture does not prove the complete reward,
destination, type, or probability distribution.

## Manual one-request acceptance gate

This is documentation only; Codex does not execute it. On the preserved level-2
character, use one ordinary solo terminal and run:

```text
/missionharvest start 1 1 CENTERED_BASELINE 2.0
```

Acceptance requires the chat start message to show detent `1`, the preset, and
slider-state ID; exactly one request; one raw transmitted request event; one raw
response event; one verified cohort (normally five offers); an automatic stop
with reason `requested_count_completed`; and a schema-3 journal that normalizes
successfully. Do not begin the wider low-level matrix until this single request
passes live.
