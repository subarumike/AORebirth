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
output directory. Raw append-only JSONL is written below
`<AOSharp plugin local-data directory>\sessions\<session-id>\events.jsonl`.
Every event is flushed durably. Normalize a completed or partial journal offline
with:

```text
Tools\modern_mission_capture_planner.cmd --normalize-session "<events.jsonl>" --output-dir "<normalized-session-directory>"
```

The normalizer emits `capture_session.jsonl`, `mission_request.jsonl`, and `mission_offer.jsonl`. Add accepted normalized sessions to `docs\reference\missions\modern-capture\capture-session-index.json`; subsequent planner runs incorporate their offer counts.
