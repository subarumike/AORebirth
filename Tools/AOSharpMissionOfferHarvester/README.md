# AOSharp Mission Offer Harvester

The plugin rolls missions and captures evidence. It does not contain or enforce
a character roster. The current character level is only recorded as evidence.

Select an ordinary mission terminal and run:

```text
/missionharvest campaign <requests-per-actual-QL> [interval-seconds]
```

Example:

```text
/missionharvest campaign 250 1.5
```

The plugin first sends one request at each of the 11 Easy/Hard
positions and records the mission QL actually returned by each position. It
then continues rolling each distinct observed mission QL until that QL has the
requested number of verified five-offer responses. If multiple Difficulty
positions return the same QL, their mappings are retained but the repeated
capture work is grouped by the observed QL.

The campaign is a small supplement to the completed centered captures. It uses
the `FIND_ITEM_PERSON_SUPPLEMENT` preset: Good/Bad full Bad, Order/Chaos full
Order, Open/Hidden full Hidden, Physical/Mystical full Physical, Head On/Stealth
centered, and Money/XP full Money. The preserved mission-settings guide reports
this combination as three Find Item, one Return Item, and one Find Person. The
existing centered captures already contain Repair, Return Item, and Kill Person,
so the combined evidence covers all five mission types without repeating the
full 250-request baseline. There is one semantic state, not a 13-state or
27-state item-capture matrix.

```text
/missionharvest status
/missionharvest stop
```

Progress is stored per observed character identity and resumes automatically:

```text
MissionOfferHarvester\campaigns\MISSION_QL_SPECTRUM_V4\level-NNN-character-ID\progress.jsonl
```

Only verified requests with matching request/response linkage, exactly five
offers, and a uniform response-side mission-QL value receive a durable
completion marker. Raw outbound and inbound packets, hashes, all offers,
mission text/type/location, credits, XP, item IDs and observed item QLs, unknown
fields, slider bytes, timestamps, and provenance remain in the session journal.

The response-side QL value is decoded from the capture-backed
`MissionInfo.UnkChunk3` candidate at big-endian bytes 16-19. If a later request
at a previously mapped Difficulty position returns a different value, capture
stops after retaining the contrary raw evidence.

The obsolete `/missionharvest items` command is disabled because it multiplied
each QL by unrelated semantic-slider states. Targeted `start`, `startcustom`,
and the old level-2 discovery `matrix` remain available for compatibility but
are not used for the normal mission-item capture.

Build and test:

```text
cmd /d /c Tools\build_mission_offer_harvester.cmd
```

Output DLL:

```text
Tools\AOSharpMissionOfferHarvester\bin\Release\AOSharpMissionOfferHarvester.dll
```

Codex builds and installs the DLL but does not inject it, launch AO, or control
the client.
