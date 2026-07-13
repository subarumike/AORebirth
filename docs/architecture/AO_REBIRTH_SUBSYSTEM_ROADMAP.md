# AORebirth Subsystem Roadmap

## Phase 0 — Guardrails and seams

Objective: make ownership enforceable. Add static/source guards, deterministic clock/random interfaces, schema validation, and fixture contracts. Dependencies: none. Likely files: AOtomation tests, new domain-only ZoneEngine files, validation tools. Completion: violations fail tests without runtime changes.

## Phase 1 — Global loot domain

Objective: define tables/groups/entries/assignments and deterministic resolution. Dependencies: Phase 0. Migrate adapters for DB, captured ordinary evidence, and narrow captured special outcomes without switching production first. Tests: inheritance, weighted/independent/exclusive rolls, guaranteed evidence, QL/quantity, unique constraints. Removal enabled: debug and special-case loot branches after parity.

## Phase 2 — Corpse inventory and rights

Objective: move remaining-items, credits, ownership, access, transfer, close/reopen, and despawn policy out of `Playfield`. Dependencies: Phase 1 and packet fixtures. Preserve captured packets exactly. Completion: `Playfield` delegates all corpse state transitions and multi-access rules are explicit; unknown team/personal behavior remains blocked.

## Phase 3 — Population and respawn

Objective: introduce spawn definitions/groups/controllers, population state, and keyed scheduling. Dependencies: Phase 0. Adapt ordinary runtime first, then DB mobs and Arete robots. Tests: independent/shared timers, cancellation, reset, duplicate identity, cleanup, deterministic selection. Removal enabled: family orchestrators and timer duplication.

## Phase 4 — Static world manifests

Objective: versioned playfield manifests for static world and dungeon-static placement. Dependencies: Phase 3. Validate coordinates/playfields/profile references/quarantine. Coverage: scalable population without per-enemy code.

## Phase 5 — Dyna camps and bosses

Objective: normalize the 174 evidence rows into reviewed camp proposals and activate only proven slices. Dependencies: Phases 1, 3, 4. Tests: boss/minion pools, level policy, shared/individual respawn, replacement, absence, restart proposal, loot inheritance. Risk: community evidence uncertainty.

## Phase 6 — Mission and dungeon integration

Objective: let instance/dungeon/mission controllers instantiate shared profiles under distinct population policies. Dependencies: Phases 1-4. Preserve Arete mission state behavior behind adapters. Tests: generation determinism, cleanup, instance isolation, shared loot/visibility.

## Phase 7 — Encounter framework

Objective: narrow scripted modules for unique mechanics. Dependencies: shared population, combat, loot, and persistence contracts. Completion: data-only bosses need no module; modules cannot duplicate global services.

## Phase 8 — Persistence and recovery

Objective: durable state for camps, events, lockouts, selected dynamic variants, and recoverable instances. Dependencies: stable controllers. Do not persist all ordinary spawns. Tests use controlled clocks and restart snapshots.

## Phase 9 — Bulk content and operations

Objective: import/review/version broad world content, expose diagnostics, and activate in bounded playfield slices. Dependencies: all preceding phases. Completion: deterministic validation, operational rollback, performance budgets, no silent unresolved evidence.
