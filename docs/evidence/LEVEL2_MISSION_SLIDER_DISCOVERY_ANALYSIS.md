# Level-2 Mission Slider Discovery Analysis

## Decision

The complete primary campaign is valid: **27 states, 54 requests, and 270 offers**. All seven sliders are distinct, deterministic protocol inputs. No planned semantic state is protocol-redundant.

**Level-2 character status: YES — it may safely advance for the slider-evidence objective.** No additional level-2 slider capture is required. The next assigned character level is **7**.

This does not claim reward-pool exhaustion or infer probabilities. QL1 statistical content saturation is a separate objective.

## Capture inventory

- Primary retained sessions: 13
- Explicitly retained surplus sessions: 1
- Primary requests: 54
- Primary offers: 270
- Requests per state: 2
- Offers per request: 5
- Character surrogate: `c018f43089319dad5f48a4cb49b49b0cda7889e08d457473181729fcd3fadf8a`
- Terminal: `{"instance": -1073741169, "type": 56001}` in playfield `{"instance": 655, "type": 51100}`
- Harvester versions: 1.3.0.0, 1.4.0.0
- AOSharp version: 1.0.0.0

One extra clean Order/Chaos -50 session was discovered and retained. It is excluded from the primary two-repeat matrix as `SURPLUS_EXACT_STATE_REPEAT_AFTER_PLANNED_QUOTA_FILLED`; it was not silently ignored.

## Exact 27-state matrix

| State | Label | Difficulty | Good/Bad | Order/Chaos | Open/Hidden | Physical/Mystical | Head On/Stealth | Money/XP | Expected QL | Requests | Offers |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | CENTERED_BASELINE_D1 | 1 | 255 | 255 | 255 | 255 | 255 | 255 | 1 | 2 | 10 |
| 2 | GOOD_BAD_FULL_LEFT | 1 | 156 | 255 | 255 | 255 | 255 | 255 | 1 | 2 | 10 |
| 3 | GOOD_BAD_FULL_RIGHT | 1 | 100 | 255 | 255 | 255 | 255 | 255 | 1 | 2 | 10 |
| 4 | GOOD_BAD_MINUS_50 | 1 | 206 | 255 | 255 | 255 | 255 | 255 | 1 | 2 | 10 |
| 5 | GOOD_BAD_PLUS_50 | 1 | 50 | 255 | 255 | 255 | 255 | 255 | 1 | 2 | 10 |
| 6 | ORDER_CHAOS_FULL_LEFT | 1 | 255 | 156 | 255 | 255 | 255 | 255 | 1 | 2 | 10 |
| 7 | ORDER_CHAOS_FULL_RIGHT | 1 | 255 | 100 | 255 | 255 | 255 | 255 | 1 | 2 | 10 |
| 8 | ORDER_CHAOS_MINUS_50 | 1 | 255 | 206 | 255 | 255 | 255 | 255 | 1 | 2 | 10 |
| 9 | ORDER_CHAOS_PLUS_50 | 1 | 255 | 50 | 255 | 255 | 255 | 255 | 1 | 2 | 10 |
| 10 | OPEN_HIDDEN_FULL_LEFT | 1 | 255 | 255 | 156 | 255 | 255 | 255 | 1 | 2 | 10 |
| 11 | OPEN_HIDDEN_FULL_RIGHT | 1 | 255 | 255 | 100 | 255 | 255 | 255 | 1 | 2 | 10 |
| 12 | OPEN_HIDDEN_MINUS_50 | 1 | 255 | 255 | 206 | 255 | 255 | 255 | 1 | 2 | 10 |
| 13 | OPEN_HIDDEN_PLUS_50 | 1 | 255 | 255 | 50 | 255 | 255 | 255 | 1 | 2 | 10 |
| 14 | PHYSICAL_MYSTICAL_FULL_LEFT | 1 | 255 | 255 | 255 | 156 | 255 | 255 | 1 | 2 | 10 |
| 15 | PHYSICAL_MYSTICAL_FULL_RIGHT | 1 | 255 | 255 | 255 | 100 | 255 | 255 | 1 | 2 | 10 |
| 16 | PHYSICAL_MYSTICAL_MINUS_50 | 1 | 255 | 255 | 255 | 206 | 255 | 255 | 1 | 2 | 10 |
| 17 | PHYSICAL_MYSTICAL_PLUS_50 | 1 | 255 | 255 | 255 | 50 | 255 | 255 | 1 | 2 | 10 |
| 18 | HEADON_STEALTH_FULL_LEFT | 1 | 255 | 255 | 255 | 255 | 156 | 255 | 1 | 2 | 10 |
| 19 | HEADON_STEALTH_FULL_RIGHT | 1 | 255 | 255 | 255 | 255 | 100 | 255 | 1 | 2 | 10 |
| 20 | HEADON_STEALTH_MINUS_50 | 1 | 255 | 255 | 255 | 255 | 206 | 255 | 1 | 2 | 10 |
| 21 | HEADON_STEALTH_PLUS_50 | 1 | 255 | 255 | 255 | 255 | 50 | 255 | 1 | 2 | 10 |
| 22 | MONEY_XP_FULL_LEFT | 1 | 255 | 255 | 255 | 255 | 255 | 156 | 1 | 2 | 10 |
| 23 | MONEY_XP_FULL_RIGHT | 1 | 255 | 255 | 255 | 255 | 255 | 100 | 1 | 2 | 10 |
| 24 | MONEY_XP_MINUS_50 | 1 | 255 | 255 | 255 | 255 | 255 | 206 | 1 | 2 | 10 |
| 25 | MONEY_XP_PLUS_50 | 1 | 255 | 255 | 255 | 255 | 255 | 50 | 1 | 2 | 10 |
| 26 | CENTERED_BASELINE_D6 | 6 | 255 | 255 | 255 | 255 | 255 | 255 | 2 | 2 | 10 |
| 27 | CENTERED_BASELINE_D10 | 10 | 255 | 255 | 255 | 255 | 255 | 255 | 3 | 2 | 10 |

## Outbound protocol findings

Every state repeated an identical 52-byte outbound packet across both requests. No volatile offsets or unexplained differences were present.

| Slider | Changed raw request offsets | Classification |
| --- | --- | --- |
| Easy/Hard | 0x1E | TRANSMITTED_PROVEN |
| Good/Bad | 0x1F | TRANSMITTED_PROVEN |
| Order/Chaos | 0x20 | TRANSMITTED_PROVEN |
| Open/Hidden | 0x21 | TRANSMITTED_PROVEN |
| Physical/Mystical | 0x22 | TRANSMITTED_PROVEN |
| Head On/Stealth | 0x23 | TRANSMITTED_PROVEN |
| Money/XP | 0x24 | TRANSMITTED_PROVEN |

Each slider changes one distinct byte. No shared packing was observed. Full-left, -50, center, +50, and full-right use the proven native bytes `156`, `206`, `255`, `50`, and `100`; semantic order is monotonic after signed-byte decoding. Easy/Hard is a separate one-based detent byte.

## Request determinism

All 27 states are `REQUEST_PAYLOAD_DETERMINISTIC`: the two complete outbound packets are byte-for-byte identical. The volatile-field mask is empty because no request identity, timestamp, or sequence field changed in the serialized request packets. There are no unexplained request differences.

## Inbound structure

There is exactly one raw response packet per request and every decoded response contains exactly five ordered offers. The seven-byte returned slider tuple is present in raw inbound data at the common offset recorded in `inbound-structure-diff.json`. Response lengths vary with mission text, rewards, and offer payloads. The current decoder exposes per-offer decoded boundaries and six raw unknown chunks, but not defensible absolute raw packet offsets for each offer.

## Decoded output findings

| Slider | Protocol classification | Output classification | Level-2 decision |
| --- | --- | --- | --- |
| easy_hard | DEFINITE_PROTOCOL_INPUT | DEFINITE_OBSERVABLE_OUTPUT_EFFECT | CAN_DEFER_TO_HIGHER_LEVEL |
| good_bad | DEFINITE_PROTOCOL_INPUT | POSSIBLE_OUTPUT_EFFECT | CAN_DEFER_TO_HIGHER_LEVEL |
| order_chaos | DEFINITE_PROTOCOL_INPUT | NO_EFFECT_DETECTED_AT_DISCOVERY_SAMPLE | CAN_DEFER_TO_HIGHER_LEVEL |
| open_hidden | DEFINITE_PROTOCOL_INPUT | NO_EFFECT_DETECTED_AT_DISCOVERY_SAMPLE | CAN_DEFER_TO_HIGHER_LEVEL |
| physical_mystical | DEFINITE_PROTOCOL_INPUT | NO_EFFECT_DETECTED_AT_DISCOVERY_SAMPLE | CAN_DEFER_TO_HIGHER_LEVEL |
| headon_stealth | DEFINITE_PROTOCOL_INPUT | NO_EFFECT_DETECTED_AT_DISCOVERY_SAMPLE | CAN_DEFER_TO_HIGHER_LEVEL |
| money_xp | DEFINITE_PROTOCOL_INPUT | DEFINITE_OBSERVABLE_OUTPUT_EFFECT | CAN_DEFER_TO_HIGHER_LEVEL |

- **Money/XP:** `DEFINITE_OBSERVABLE_OUTPUT_EFFECT`. Full-left and full-right credit ranges are disjoint in both repeated cohorts; XP ranges are disjoint in the opposite direction. Both credits and XP are affected. Reward identity and final formula remain inconclusive.
- **Good/Bad:** `POSSIBLE_OUTPUT_EFFECT`. Extreme states produced different mission-icon sets, but ten offers per state cannot establish availability or probability.
- **Easy/Hard:** `DEFINITE_OBSERVABLE_OUTPUT_EFFECT` for compensation scaling across detents 1, 6, and 10. Expected QL remains static request provenance, not server-confirmed mission QL.
- **Order/Chaos, Open/Hidden, Physical/Mystical, Head On/Stealth:** no deterministic offer-level effect was detected. Their historical claims mostly concern interiors, enemies, locks, traps, or behavior that offer packets do not expose.

### Money/XP special analysis

Both compensation fields change. Full-left yielded higher credit ranges and lower XP ranges than full-right, with disjoint extreme-state ranges across both repeated cohorts. Reward identities varied, but the discovery sample cannot attribute that variation to the slider. Reward QL showed no deterministic slider-linked effect. No final compensation formula or probability was inferred.

### Easy/Hard special analysis

Detents 1, 6, and 10 are separately encoded at the Easy/Hard byte and map statically to expected QL1, QL2, and QL3 for character level 2. Returned packet structure and compensation scale across these states. AOSharp still reports `mission_ql` as unavailable, so request provenance is not mislabeled as a server-confirmed QL.

## Unknown-field candidates

Mission-QL scan result: **STRONG_MISSION_QL_CANDIDATE**. `UnkChunk3Base64`, big-endian 32-bit value at decoded chunk offset `16`, equals expected QL 1/2/3 across all 30 Easy/Hard comparison offers. Overlapping byte/word views are recorded as aliases, not independent candidates. This remains a candidate pending broader multi-QL confirmation and is not promoted to runtime semantics.

No unknown-chunk field produced a deterministic all-offer mapping to decoded `MissionIcon`; the already decoded icon remains the authoritative mission-type observation. Objective identity/QL, token reward, entrance identity, and faction requirements remain unknown because no decoded ground truth exists.

## Redundancy and follow-up

- Protocol-redundant semantic states: **none**
- Exact state-repeat request determinism: **27/27 states**
- Additional level-2 slider capture: **not required**
- Minimal follow-up matrix: **empty**
- Money/XP formula work: defer to a higher-level controlled capture
- Interior-behavior slider work: requires accepted mission/interior instrumentation, not more QL1 offer rolling
- Mission-QL candidate work: use broader multi-QL correlations

## Evidence boundaries

These are discovery results, not probabilities. Differences in counts or frequencies are not treated as stable distributions. No mission runtime behavior was implemented or changed.

**RUNTIME MISSION LOGIC CHANGED: NO**
