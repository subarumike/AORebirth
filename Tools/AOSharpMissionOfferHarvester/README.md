# AOSharp Mission Offer Harvester

This is evidence-only instrumentation for AOSharp SDK `1.0.106`. It records every offer in each returned cohort and does not filter for desired rewards. It does not change AORebirth mission generation.

The plugin has been compiled offline against the exact retained SDK package. It has not been loaded into or live-validated against Mike's installed AOSharp runtime, and Codex did not launch or control the AO client.

Build with:

```text
cmd /d /c Tools\build_mission_offer_harvester.cmd
```

The build verifies the retained NuGet SHA-256, extracts its two reference assemblies under `tools-temp`, and produces `Tools\AOSharpMissionOfferHarvester\bin\Release\AOSharpMissionOfferHarvester.dll`.

After Mike loads the plugin in a compatible AOSharp installation, manually select/use an ordinary mission terminal and control collection in AO chat:

```text
/missionharvest start <target-mission-QL> <request-count> [interval-seconds]
/missionharvest status
/missionharvest stop
```

The plugin reads the current character level, resolves the target QL through its
generated copy of AORebirth's governed 220×11 mission-level table, and sends the
first exact matching one-based difficulty slot. If the target QL is not an exact
member of the current character's row, it sends no request. It never substitutes
the nearest QL. The generated resolver records the canonical table SHA-256 and
is checked before every repository build.

The default interval is 2.0 seconds, the enforced minimum is 1.5 seconds, and
only one request may be outstanding. One request means one mission-terminal
refresh and records the complete returned cohort, normally five offers.
AOSharp `MissionInfo` does not expose a separate server mission-QL field, so the
target is proven at request time by the exact character-level/slot lookup rather
than inferred from reward QLs.

Start, automatic completion, manual stop, and status messages report the session
ID, current character level, target QL, resolved slot, request/cohort counts, and
output file. Raw append-only JSONL is written below
`<AOSharp plugin local-data directory>\sessions\<session-id>\events.jsonl`.
Every event is flushed durably. Normalize a completed or partial journal offline
with:

```text
Tools\modern_mission_capture_planner.cmd --normalize-session "<events.jsonl>" --output-dir "<normalized-session-directory>"
```

## Per-roll capture contract

Version 1.2 records the request-time roll origin on the request, returned cohort,
and every offer: terminal identity and name, current playfield, terminal local and
global coordinates, terminal rotation, player identity and coordinates, and the
capture timestamp. This is distinct from each mission destination, which records
the offered playfield identity and destination coordinates.

Every public field exposed by AOSharp 1.0.106 `MissionInfo` is retained. Each
offer includes mission identity, title, description, terminal identity, credits,
XP, reward descriptor version, every reward item's low/high IDs and QL, mission
icon, structured mission destination, all six raw unknown chunks, and the
request-time roll origin. The five proven Malis icon mappings are emitted as an
explicit mission-type record: Return Item, Kill Target, Find Target, Find Item,
and Use Item/Repair. An unknown icon remains `UNKNOWN_ICON_RAW_VALUE_PRESERVED`;
the plugin never guesses its type from prose.

The response envelope now preserves every public `QuestAlternativeMessage`
header field in addition to the returned sliders and full offer cohort. Fields
that AOSharp does not expose remain explicitly null under `not_exposed_fields`.
Complete capture of each returned roll does not prove that a finite sample has
exhausted AO's possible reward items, mission destinations, or probabilities.

The normalizer emits `capture_session.jsonl`, `mission_request.jsonl`, and `mission_offer.jsonl`. Add accepted normalized sessions to `docs\reference\missions\modern-capture\capture-session-index.json`; subsequent planner runs incorporate their offer counts.
