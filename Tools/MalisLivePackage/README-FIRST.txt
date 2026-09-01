Malis Mission Roller 2.0 + MissionOfferHarvester live package
=============================================================

This package uses the audited Malis source commit
3ac9943a4943b8cb80eda9e40359729e656686b0 and is compiled directly against
Mike's installed AOSharp runtime assemblies.  It intentionally does not ship
private AOSharp, Newtonsoft.Json, Serilog, or other host dependencies.

Malis behavior
--------------

Malis is an AOSharp plugin, not a standalone program.  Its window opens during
plugin initialization.  There is no normal chat command for opening the UI.
The `/mmr` command is developer-only and does not start mission rolling.

In the header:

- Start toggles automatic rolling on or off.
- Request performs exactly one mission-terminal request.
- Settings opens slider, mission-type, location, item-list, and extra options.

The packaged `JSON\Settings.json` is a safe evidence configuration derived
from the audited defaults.  Auto Accept, Auto Adjust QL, Remove Roll, and all
five mission-type matches are disabled.  The audited Default_Settings.json is
also retained unchanged.  No Malis source or selection algorithm was changed.

First one-roll coexistence acceptance
-------------------------------------

1. Launch AO through the installed AOSharp environment.
2. Confirm the Malis window opens and the harvester load message appears.
3. Use a normal Rubi-Ka mission terminal.
4. In Malis Settings, confirm Auto Accept is off and select the intended
   Easy/Hard difficulty.
5. Enter `/missionharvest observe <planned-target-QL>` in AO chat.
6. Click Malis Request exactly once.  Do not click Accept.
7. Enter `/missionharvest status`; expect one request and one complete cohort.
8. Enter `/missionharvest stop` and stop.  Inspect the evidence before any
   automatic or bulk rolling.

For later controlled automatic rolling, keep Auto Accept and all mission types
disabled, keep Auto Adjust QL disabled, add one roll-list entry so Malis permits
auto mode, set the slider manually, start harvester observe mode, and toggle
Malis Start.  Toggle Start again to stop, then stop the harvester.

Evidence location
-----------------

The harvester writes raw durable JSONL under:

`%LOCALAPPDATA%\AOSharp\MissionOfferHarvester\sessions`

The target QL supplied to `observe` is an operator/planner input.  AOSharp
MissionInfo does not expose a direct server mission-QL field.

RUNTIME MISSION LOGIC CHANGED: NO
