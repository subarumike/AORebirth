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
/missionharvest start <difficulty-slot-1-through-11> <planned-target-mission-QL> <request-count> [interval-seconds]
/missionharvest status
/missionharvest stop
```

The default interval is 2.0 seconds, the enforced minimum is 1.5 seconds, and only one request may be outstanding. The planned target QL is retained as an experimental input; AOSharp `MissionInfo` does not expose the server mission QL.

Raw append-only JSONL is written below the plugin's AOSharp local-data directory, one folder per session. Every event is flushed durably. Normalize a completed or partial journal offline with:

```text
Tools\modern_mission_capture_planner.cmd --normalize-session "<events.jsonl>" --output-dir "<normalized-session-directory>"
```

The normalizer emits `capture_session.jsonl`, `mission_request.jsonl`, and `mission_offer.jsonl`. Add accepted normalized sessions to `docs\reference\missions\modern-capture\capture-session-index.json`; subsequent planner runs incorporate their offer counts.
