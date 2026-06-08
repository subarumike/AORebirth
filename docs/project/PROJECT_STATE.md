# Current Status

CellAO NightPredator is a local C#/.NET Framework-era Anarchy Online server workspace. Current work is focused on making the server compatible with Mike's current AO client and local `cellao_codex_clean` MySQL database through evidence-backed packet, gameplay, and data repairs.

# Working Systems

- Login, chat, and zone engines build and run locally.
- Current-client `FullCharacter` version 26 and live-style login state are locked project decisions.
- Sit/stand behavior is repaired.
- Weapon and armor equipment visuals are repaired for the current test scope.
- Equipped items persist across relog in the documented test scope.
- Death/respawn white-screen behavior is repaired.
- Corpse use, item loot, credit loot, XP text, and corpse despawn have working documented paths.
- Player trade item and credit transfer have been repaired in the documented test scope.
- Vendor shop buy, sell, close, and current-client ICC shop stock coverage have been repaired for the captured Fair Trade areas.
- Surgery clinic and implant flows have documented repaired behavior.

# Partially Working Systems

- Inventory and container behavior work for several repaired flows but still need broader regression coverage.
- Combat works for basic weapon/NPC test scenarios, but packet semantics are not complete.
- Corpse visuals and `CorpseFullUpdate` remain areas for cleanup.
- Shop/vendor database coverage is improved but still has remaining static vendor coverage gaps.
- Playfield/interior mapping has repaired fixtures and remaining audit candidates.
- Enemy spawn testing has supported low-level families, but final spawn tables are not complete.
- DB-backed mob loot is modeled and partially wired, with limited reviewed data.
- Nanos, tradeskills, teams, organizations, pets, missions, quests, perks, research, bank, bags, stacks, and containers need separate focused work.

# Known Broken Systems

- NPC chase/movement is high risk and not gameplay-ready.
- `PlayfieldAnarchyF` is documented as a current-client structure mismatch.
- Some packet classes are missing, under-modeled, or awaiting capture-backed runtime use.
- Broad static vendor coverage remains incomplete.
- Full gameplay systems for missions, quests, perks, research, pets, PvP/towers, teams, and organizations are not complete.

# Current Development Focus

The latest completed work focused on captured current-client vendor/shop data for ICC/Fair Trade terminals and documentation reorganization. Future behavior changes should continue to use live capture, private-server capture, AO stripdown source, or local code facts as evidence.

# Last Completed Milestone

ICC/Fair Trade vendor stock repairs were pushed to `origin/master` in commit `cffc5da` after verification showed:

- vendor DB issues: 0
- shop inventory item-cache issues: 0
- tradeskill room captured rows: 3,101
- tradeskill vendor rows: 38

# Next Milestone

Keep documentation entry points small and current. For gameplay work, choose the next target from `docs/ai/CURRENT_TASK.md` and validate through focused capture, smoke/source assertions, and Mike's playtest when client behavior matters.
