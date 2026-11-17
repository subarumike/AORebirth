# PorkForAor

Read-only exporter that publishes PORK-compatible bio files (XML, JSON, HTML) and online lists for AO-Rebirth.
The specification is `docs/api/character-bio-api.md`. The exporter reads the database and writes files; it never
writes to the database and does not depend on the engines.

Status: the loop, change detection, sweep, quarantine, configuration and the three exporters (characters,
organization stats, online lists) are written. The exporters have been checked only against an in-memory database
and are waiting for the first run on the local database.

## Requirements

PHP 8.2 or later with `pdo_mysql`, `mbstring` and `json`.

## Setup

1. Copy `.env.example` to `.env` and fill it in (SQL access, export folder, GameData path).
2. `php pork.php check` validates the configuration, the database, the three meta tables, the GameData files and the folders.
3. `php pork.php run` starts the loop (Windows: `run-pork.cmd`). Stop it with Ctrl+C.

## Commands

| Command | Effect |
| --- | --- |
| `run` | Default. Loop every `PORK_TICK_SECONDS`: reads the meta tables, renders changed entities, touches online files. Runs the sweep every `PORK_SWEEP_HOURS`. |
| `sweep` | One reconciliation pass, then exit. |
| `check` | Validates the setup and exits with a non-zero code on failure. |

Set `PORK_DEBUG_HEARTBEAT=1` to log the cost of every pass.

## Files

- `export/` (see `PORK_EXPORT_PATH`): the folder served over HTTP by the web server.
- `state/`: watermarks (`state.json`) and quarantined files. Keep it outside the served folder.
- `web/apache.htaccess`: local test configuration. Copy it to `<PORK_EXPORT_PATH>\.htaccess` so that `bio.xml?data_type=json` serves `bio.json`. The production web server is configured by the server owner.
- `web/org.php`: organization directory modeled on the retail `orgs.html` page. Copy it to the root of `<PORK_EXPORT_PATH>` and serve it with PHP. `org.php?l=b&dim=10` lists the organizations starting with B, `l=others` the other names. It reads only the published `basicstats.json` files. The dimensions it lists are the `DIMENSIONS` constant at the top of the file.
- Linux service files are handled outside this repository.
