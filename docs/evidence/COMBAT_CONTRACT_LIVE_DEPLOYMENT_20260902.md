# Shared Combat Contract Live Deployment - 2026-09-02

## Scope

The repair restores shared combat packet fallbacks without changing the PR 22
locality implementation and without adding enemy, template, or playfield
special cases.

- unarmed player attacks receive a generic, deterministic strike context when
  no equipped weapon context exists;
- every armed NPC sends its existing owner-linked weapon definition to its
  current player target before combat scheduling begins.

No database schema or game-data content changed.

## Source and acceptance

- source commit: `530c381d1caeaacfa5b8100727ed47c95d5d70b7`;
- commit subject: `Restore shared player and NPC combat packet fallbacks`;
- Windows source acceptance: `PASS`;
- Windows placement manifest SHA-256:
  `add16292d006d288ae38e22c5948d0b4eb7c3c9ab581c17c41b3a6e2b82d4df5`;
- AOtomation messaging tests: `1127/1127 PASS`;
- focused shared combat contract test: `1/1 PASS`;
- Linux source acceptance: `PASS` for the same source and placement manifest.

## Production promotion

- release: `/opt/ao-rebirth/zoneengine/releases/combat-contract-530c381d`;
- active link: `/opt/ao-rebirth/zoneengine/current`;
- deployed `SOURCE_SHA`:
  `530c381d1caeaacfa5b8100727ed47c95d5d70b7`;
- `ao-rebirth-zoneengine.service`: `active/running`;
- restart count after promotion: `0`;
- public ZoneEngine listener: `0.0.0.0:7501`.

The governed upgrade retained the prior immutable release as the rollback
target. Client-facing combat text remains a live gameplay acceptance item;
Codex did not launch or control the AO client.
