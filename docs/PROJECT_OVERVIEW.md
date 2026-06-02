# Project Overview

Generated: 2026-06-02

## Project Name

CellAO local server project.

## Purpose

CellAO is an open-source C# emulator/server stack for Anarchy Online. This local repo is being modernized and repaired so it works with Mike's current Anarchy Online client and local MySQL database.

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
- Visual Studio solution: `CellAO/CellAO.sln`.
- MySQL database configured for local use.
- Dapper, NLog, NBug, MemBus, DotNetZip, MathNet.Numerics, MySql.Data, Npgsql, and bundled AOtomation/msgpack dependencies.
- PowerShell scripts for build, smoke tests, engine start/stop, capture analysis, and tooling.

## Third-Party And External Reference Sources

- `CellAO/Libraries/Source/AOtomation/AOtomation.Messaging`
- `CellAO/Libraries/Source/msgpack-cli`
- `C:\Users\Mike\Documents\AO stripdown\Anarchy Online`
- `C:\Users\Mike\Documents\New project\external\never-knows-best`
- Public reference repos inspected from `https://gitlab.com/never-knows-best`, especially AOSharp, AODB, AOSharp.Clientless, and Anarchy Online NavMeshes.

## Repository Overview

- `CellAO/Server/ChatEngine`: chat server.
- `CellAO/Server/LoginEngine`: login/account/character-list server.
- `CellAO/Server/ZoneEngine`: zone server and most gameplay logic.
- `CellAO/Server/WebEngine`: web host.
- `CellAO/Libraries/Source/CellAO.Core`: core entities, inventory, items, playfields, requirements, vectors, functions, and supporting runtime models.
- `CellAO/Libraries/Source/CellAO.Database`: database access and entities.
- `CellAO/Libraries/Source/CellAO.Stats`: stat definitions and stat handling.
- `CellAO/Libraries/Source/AOtomation`: packet/message models.
- `CellAO/Documentation`: existing technical docs, packet notes, enum docs, and repair reports.
- `docs`: AI handoff documentation created for this project.
- `tools-temp`: active capture, smoke test, replay, DB backup, and experimental tooling.
- `Tools`: historical utility projects and launcher tooling.

## Current Status

Equipment, weapon visuals, dual wield, armor visuals, many equipment slots, basic combat, corpse creation, loot, death/respawn, and parts of trade have been repaired or partially repaired through playtesting and packet evidence. NPC movement and some inventory/trade/credit flows remain high-risk. The project is in active development with a dirty worktree, so future agents must inspect status before editing.

Unknowns:

- TODO: Requires human clarification for the intended long-term branch/release policy.
- TODO: Requires human clarification for production deployment expectations.
- TODO: Requires human clarification for how much historical CellAO behavior should be preserved versus current-client parity.

