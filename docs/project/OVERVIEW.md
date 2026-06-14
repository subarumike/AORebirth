# Project Overview

Generated: 2026-06-02

## Project Name

AO Rebirth local server project.

## Purpose

AO Rebirth is an open-source C# emulator/server stack for Anarchy Online. This local repo is being modernized and repaired so it works with Mike's current Anarchy Online client and local MySQL database.

## Vision

Create a maintainable, evidence-backed local AO server that can support login, chat, zoning, inventory, equipment, combat, loot, death/respawn, trade, NPCs, quests, and playfield behavior with current-client packet compatibility.

## Goals

- Keep the local server playable for targeted manual testing.
- Repair packet and runtime behavior from source evidence, not intuition.
- Preserve verified fixes with smoke/source assertions.
- Build a documentation system that future AI agents can use after context loss.
- Keep the project safe for Mike's local database and client workflow.

## Target Audience

- Mike, as the project owner and live client tester.
- Future AI coding agents.
- Developers familiar with C#, packet serialization, MMO server systems, and Anarchy Online reverse engineering.

## Major Features

- Login, chat, zone, and web engine console applications.
- AOtomation-based N3 packet serialization models.
- Character, dynel, inventory, item, stat, playfield, NPC, and vendor core models.
- Equipment and visual mesh handling for weapons, armor, HUD, deck, util, and related slots.
- Combat, corpse, loot, and death/respawn systems under active repair.
- Player trade under active repair.
- Debug chat commands for item spawning, NPC spawning, teleporting, stat inspection, weather, and related workflows.
- Capture and smoke-test tooling under `tools-temp`.

## Technology Stack

- C# / .NET Framework-era projects.
- Visual Studio solution: `AORebirth/AORebirth.sln`.
- MySQL database configured for local use.
- Dapper, NLog, NBug, MemBus, DotNetZip, MathNet.Numerics, MySql.Data, Npgsql, and bundled AOtomation/msgpack dependencies.
- PowerShell scripts for build, smoke tests, engine start/stop, capture analysis, and tooling.

## Third-Party And External Reference Sources

- `AORebirth/Libraries/Source/AOtomation/AOtomation.Messaging`
- `AORebirth/Libraries/Source/msgpack-cli`
- `C:\Users\Mike\Documents\AO stripdown\Anarchy Online`
- `C:\Users\Mike\Documents\New project\external\never-knows-best`
- Public reference repos inspected from `https://gitlab.com/never-knows-best`, especially AOSharp, AODB, AOSharp.Clientless, and Anarchy Online NavMeshes.

## Repository Overview

- `AORebirth/Server/ChatEngine`: chat server.
- `AORebirth/Server/LoginEngine`: login/account/character-list server.
- `AORebirth/Server/ZoneEngine`: zone server and most gameplay logic.
- `AORebirth/Server/WebEngine`: web host.
- `AORebirth/Libraries/Source/AORebirth.Core`: core entities, inventory, items, playfields, requirements, vectors, functions, and supporting runtime models.
- `AORebirth/Libraries/Source/AORebirth.Database`: database access and entities.
- `AORebirth/Libraries/Source/AORebirth.Stats`: stat definitions and stat handling.
- `AORebirth/Libraries/Source/AOtomation`: packet/message models.
- `AORebirth/Documentation`: generated enum/stat documentation and original generated reference files.
- `docs/ai`: active AI workflow and task documentation.
- `docs/project`: active project overview, architecture, decisions, features, roadmap, and status.
- `docs/backlog`: current bugs and TODO lists.
- `docs/reports`: current audit reports.
- `docs/reference`: packet, gameplay, enemy, loot, and client-DLL reference reports and data.
- `docs/archive`: historical AI session notes and dated handoffs.
- `tools-temp`: active capture, smoke test, replay, DB backup, and experimental tooling.
- `Tools`: historical utility projects and launcher tooling.

## Current Status

Equipment, weapon visuals, dual wield, armor visuals, many equipment slots, basic combat, corpse creation, loot, death/respawn, and parts of trade have been repaired or partially repaired through playtesting and packet evidence. NPC movement and some inventory/trade/credit flows remain high-risk. The project is in active development with a dirty worktree, so future agents must inspect status before editing.

Unknowns:

- TODO: Requires human clarification for the intended long-term branch/release policy.
- TODO: Requires human clarification for production deployment expectations.
- TODO: Requires human clarification for how much historical AO Rebirth behavior should be preserved versus current-client parity.
