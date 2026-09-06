# Mission-location catalog reconciliation

The supplied catalog is accepted as authoritative for location IDs and exact
names, with external provenance. Existing captures do not yet supply a proven
bridge from each destination to those IDs. No selected destination ID has been
invented or promoted. Reconciliation is complete as an audit; exact assignment
of the offers remains unresolved.

## Results

| Scope | Requests/cohorts | Returned offers | Exact destination IDs established |
| --- | ---: | ---: | ---: |
| Required level-2 slider discovery | 54 | 270 | 0 |
| All 77 existing MissionOfferHarvester sessions, including the primary scope | 18,638 | 93,185 | 0 |

The primary scope passes the existing strict level-2 validator: 27 states,
two requests per state, five offers per request, level 2, matching serialized
and transmitted outbound bytes, matching inbound response copies/hashes,
matching slider layers, successful stop reasons and no errors/timeouts.

The broader scope includes every recorded cohort, including the historical
surplus level-2 request. One cohort is empty; 18,637 contain five offers. There
are 71 older cohorts without a raw response attached, one session without a
stop marker and two sessions containing an error or timeout. These do not
invalidate the separate clean 270-offer primary scope. Historical sessions are
included for offer evidence, not falsely certified as complete campaigns.
Files were stable throughout their reads. This scope is the MissionOfferHarvester
corpus, not an assertion that unrelated capture products were inventoried.

## Catalog authority and preservation

See `docs/reference/missions/external-location-catalog/PROVENANCE.md` and the
byte-preserved `ACGEntrances.json`. The catalog has 370 names, 2,235 unique IDs
and no duplicate ID ownership. Its one final object comma is tolerated only
in the parser; the original file remains unchanged. The generated reverse
index retains unsigned decimal, signed decimal, hexadecimal and exact names.

SOURCE_ROLE: AUTHORITATIVE_EXTERNAL_GAME_CODE_EXTRACT

AUTHORITATIVE_FOR: Complete mission-location ID catalog; exact location ID
values; exact associated display names.

ORIGIN: Supplied by another project; reportedly extracted directly from AO
game code. No project revision, source function or extraction log was supplied.

AOREBIRTH_LOCAL_GHIDRA_EXTRACTION: NO
AOREBIRTH_INDEPENDENT_REPRODUCTION: NO

## Why the byte hits do not identify destinations

All available raw response bytes and all captured unknown chunks were scanned
at every possible byte offset for all exact catalog IDs, in both endian orders.
Unsigned and signed representations have identical bits; the reverse index
retains both numeric displays. No alternate encoded representation is asserted.

The only primary-capture match is `3221226127` (`0xC000028F`, catalog name
`Workers Flats`). It equals the request terminal instance `-1073741169`
as an unsigned 32-bit value. There are 362 occurrences across the 54 response
packets. Of these, 38 occur in offer `UnkChunk4Base64` at offset 28, all with the
same origin-terminal instance. This is not an offer-specific destination ID.
An instance-only catalog lookup would incorrectly label these destinations
Workers Flats despite their recorded destination playfields and coordinates.
The catalog does not supply an identity type tag to disambiguate typed AO
identities. A matching instance integer is not sufficient.

Across the broader corpus, exact hits comprise 108,318 instances of
`3221226127`, 19,327 of `3221226272` (catalog: `A building in Borealis`), and one
of `3221228553` (catalog: `Subway Entrance`). Every one equals its request's
terminal instance. There are zero nonterminal catalog-ID hits in raw responses
or unknown offer chunks. This excludes a direct four-byte catalog-ID field in
the available responses, but does not rule out an unproven alternate encoding.

## Description matching

106 of the 270 primary offers contain exact catalog-name text; 164 contain
none. These are recorded as text candidates only. Every name found in this
primary scope maps to multiple catalog IDs:

| Name | Offers containing the name | Catalog IDs with that name |
| --- | ---: | ---: |
| a building | 43 | 188 |
| a factory | 2 | 9 |
| a factory building | 1 | 13 |
| a house | 37 | 77 |
| a livingmodule | 4 | 11 |
| a ruined factory | 1 | 2 |
| a waterwell | 2 | 7 |
| building | 16 | 28 |

Matching is case-sensitive, word-bounded, longest-name first. Generic text can
occur outside the destination phrase; even one textual candidate would require
semantic confirmation. No probability, nearest coordinate, fabricated ID or
name-only assignment is used.

## Existing tools and location data inspected

- `Tools/analyze_level2_mission_slider_capture.py`: primary packet/slider
  integrity validator and previous `UNKNOWN_NOT_EXPOSED` entrance classification.
- `Tools/modern_mission_capture_planner.py`: normalized destination coordinates,
  names and retained raw fields; no decoded entrance-instance bridge.
- `Tools/analyze_mission_spectrum_capture.py`: existing campaign offer analysis.
- `Tools/AOSharpMissionOfferHarvester/Main.cs`: records `MissionInfo` chunks;
  destination entrance identity is explicitly unexposed.
- `Tools/arpa3_mission_evidence.py`: ClickSaver location name, playfield and XY
  observations; those are not exact catalog-ID mappings.
- `AORebirth/Server/ZoneEngine/Core/Missions/MissionLocationPool.cs`: existing
  capture-backed playfield/XYZ spots and legacy EntranceLow/EntranceHigh values.
- `AORebirth/Server/ZoneEngine/Core/Missions/MissionRollService.cs`: copies those
  two values into Unknown18/Unknown19, without a catalog-ID bridge.
- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging/src/SmokeLounge.AOtomation.Messaging/GameData/QuestActionList.cs`:
  calls the fields only "Probably low and high id of the entrance". This
  conjectural comment cannot prove their relation to the supplied 32-bit IDs.
- `docs/generated/playfields/placements/pf_695.json` and the official placement
  source manifest: static hash-spawn data with unresolved SourceNpcId, not
  entrance dynel IDs. No coordinate-to-entrance-ID mapping is supplied there.
- `docs/project/KNOWN_DECISIONS.md`: identity-first evidence, raw retention and
  explicit unresolved classifications; names/proximity do not establish identity.

The existing legacy x86 mission analyzer was invoked first for the primary
sessions and then all 77 local sessions. It reports missing `raw-packets.csv`
because these are MissionOfferHarvester JSONL journals. The established JSONL
validator and the new offline reconciliation handle their actual format.

## Reproduction and outputs

```cmd
cmd /d /c Tools\reconcile_mission_locations.cmd --catalog docs\reference\missions\external-location-catalog\ACGEntrances.json --sessions-root C:\Users\Mike\AppData\Local\AOSharp\MissionOfferHarvester\sessions --output-dir docs\generated\missions\location-reconciliation
cmd /d /c Tools\test_mission_location_reconciliation.cmd
```

`docs/generated/missions/location-reconciliation/` contains:

- `level2-offers.jsonl`: all 270 primary offers, individually reconciled.
- `all-offers.jsonl.gz`: all 93,185 existing harvester offers, individually
  reconciled; includes the primary offers rather than double-counting them.
- `source-manifest.json`: source file hashes, line/event counts and session IDs.
- `artifact-manifest.json`: output hashes and generator/request-index hashes.
- `catalog-index.json`: reverse catalog, exact original names and numeric views.
- `observed-destinations.json`: captured coordinates, associated name candidates
  and retained UnknownChunk5 word pairs, without guessed IDs.
- `summary.json`: primary and broader scope counts, field matches and provenance.

Each offer has its session/request/cohort/mission identity, source event line,
packet hashes, origin, destination, slider-state ID, description hash and exact
candidate-match offsets. Original descriptions and all other raw fields remain
in the hashed source journals. `selected_location_id` stays null with the explicit
status `UNRESOLVED_NO_PROVEN_DESTINATION_ID_BRIDGE`.

Starting SHAs, primary dirty status, every registered worktree and mission branch
are retained in `MISSION_LOCATION_RECONCILIATION_REPOSITORY_SNAPSHOT.md`.
The primary worktree and all unrelated worktrees remain unchanged.

## Remaining evidence needed

The useful next source is an entrance catalog that joins each location ID to
its destination playfield and exact marker coordinates, or the supplying
project's code that links these IDs to the fields in a mission offer. That
would let the existing captures be reconciled offline. Additional rolling
would not supply this missing interpretation and is not requested.

## Validation

PASS: four regression tests cover unaligned and boundary-offset matches in
both endian orders, near-match rejection, corrupt packet hashes/lengths,
unsigned/signed identity preservation, the exact retained source hash, all
generated artifact hashes, unique offer keys, terminal-collision exclusion,
54 primary requests with five offers each, and 93,185 total offer rows.

PASS: two complete offline runs produced the same artifact manifest SHA-256,
`527bfa2fe635f4e86436a705914774a381850e58a084c39d29b82b5eeab0cb10`.
PASS: whitespace validation and unchanged primary worktree status.
No runtime build was required for this offline evidence task.

LIVE_MISSION_CAPTURE_PERFORMED: NO
RUNTIME_MISSION_LOGIC_CHANGED: NO
SOURCE_INDEPENDENTLY_REPRODUCED: NO
