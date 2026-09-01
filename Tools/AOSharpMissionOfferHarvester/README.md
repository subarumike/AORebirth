# AOSharp Mission Offer Harvester

This is evidence-only instrumentation for AOSharp. It records every offer in each returned cohort and does not filter for desired rewards. It does not change AORebirth mission generation.

The standard build remains pinned to the retained SDK package. The Malis live
package workflow additionally compiles this plugin against Mike's exact
installed AOSharp runtime. It has not been loaded into or live-validated with
the AO client by Codex.

Build with:

```text
cmd /d /c Tools\build_mission_offer_harvester.cmd
```

The build verifies the retained NuGet SHA-256, extracts its two reference assemblies under `tools-temp`, and produces `Tools\AOSharpMissionOfferHarvester\bin\Release\AOSharpMissionOfferHarvester.dll`.

For passive capture of requests produced by Malis, manually select/use an ordinary mission terminal, start observation, then use Malis:

```text
/missionharvest observe <planned-target-mission-QL>
/missionharvest status
/missionharvest stop
```

`observe` mode does not issue missions. It records outbound `QuestAlternativeMessage` requests from Malis or another AOSharp plugin and correlates each returned cohort. The target QL is the operator's planner input because AOSharp `MissionInfo` does not expose server mission QL.

The original active-driver mode remains available independently:

```text
/missionharvest start <difficulty-slot-1-through-11> <planned-target-mission-QL> <request-count> [interval-seconds]
/missionharvest status
/missionharvest stop
```

Active-driver mode defaults to 2.0 seconds and enforces a 1.5-second minimum.
Observe mode preserves Malis's original 1.5-second pacing and does not alter
it. The harvester correlates at most one outstanding request in either mode.

Raw append-only JSONL is written below the plugin's AOSharp local-data directory, one folder per session. Every event is flushed durably. Normalize a completed or partial journal offline with:

```text
Tools\modern_mission_capture_planner.cmd --normalize-session "<events.jsonl>" --output-dir "<normalized-session-directory>"
```

The normalizer emits `capture_session.jsonl`, `mission_request.jsonl`, and `mission_offer.jsonl`. Add accepted normalized sessions to `docs\reference\missions\modern-capture\capture-session-index.json`; subsequent planner runs incorporate their offer counts.
