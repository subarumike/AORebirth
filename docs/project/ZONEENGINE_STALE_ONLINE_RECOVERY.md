# ZoneEngine stale Online recovery

ZoneEngine startup remains fail-closed: `--validate-database` rejects every nonzero
`characters.Online` value. The systemd unit runs `--recover-stale-online` immediately
before that validation so stale state cannot permanently deadlock startup.

Recovery is allowed only while the command holds the service runtime lock, no other
ZoneEngine process exists, and port `7501` has no listener. The command reserves port
`7501` for the duration of a serializable database transaction, records affected IDs
and old values in the systemd journal, performs a bounded update against those IDs,
and verifies the nonzero count is zero before committing. Any guard, query, update,
or verification failure exits nonzero and prevents normal startup.

The automatic and manual paths use the same command:

```text
/opt/ao-rebirth/zoneengine/current/ZoneEngine --recover-stale-online \
  --recovery-lock-file /run/ao-rebirth-zoneengine/stale-online-recovery.lock
```

The command requires the same root-readable environment and systemd runtime directory
as the ZoneEngine service. Credentials are never written to recovery output.

The remaining root-cause fix belongs to LoginEngine and is intentionally separate:
clear `Online=1` only when the pre-Zone handoff client/session has conclusively lost
ownership. That change requires lifecycle coverage proving it cannot clear a legitimate
session after a successful handoff.
