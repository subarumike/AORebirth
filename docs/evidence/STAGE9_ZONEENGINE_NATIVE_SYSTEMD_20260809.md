# Stage 9 ZoneEngine native Ubuntu/systemd validation

Date: 2026-08-09

Commit under test before Stage 9 changes: `505fc445`

Repository: `D:\AO_Rebirth_Linux_Build`

Branch: `codex/linux-parallel-build`

## Native Linux environment

- SSH target used for validation: `root@2.24.96.30`
- Kernel: `Linux mail 6.8.0-124-generic #124-Ubuntu SMP PREEMPT_DYNAMIC Tue May 26 13:00:45 UTC 2026 x86_64 x86_64 x86_64 GNU/Linux`
- Architecture: `x86_64`
- systemd: `systemd 255 (255.4-1ubuntu8.16)`
- Host .NET runtime: not installed/reported; Stage 9 therefore used a self-contained `linux-x64` publish.
- Local SDK used for publish: `.NET SDK 10.0.302`

## Native commands executed

The self-contained package was published locally with:

```cmd
LinuxBuild\publish-zoneengine.cmd linux-x64 true
```

The package was uploaded as an archive and unpacked under:

```text
/tmp/ao-rebirth-zoneengine-stage9-20260809-zone-001/publish
```

Native listener-free startup validation command:

```sh
cd /tmp/ao-rebirth-zoneengine-stage9-20260809-zone-001/publish
AO_REBIRTH_REQUIRED_SQL_TYPE=MySql \
AO_REBIRTH_EXPECTED_DATABASE=aorebirth_chatengine_stage6 \
AO_REBIRTH_ZONE_LISTEN_IP=127.0.0.1 \
AO_REBIRTH_CHAT_LISTEN_IP=127.0.0.1 \
AO_REBIRTH_CONFIG_PATH=/tmp/ao-rebirth-zoneengine-stage9-20260809-zone-001/publish/Config.xml \
AO_REBIRTH_MYSQL_CONNECTION='Server=127.0.0.1;Port=33067;Database=aorebirth_chatengine_stage6;Uid=aorebirth_stage8;Pwd=stage8-placeholder;SslMode=None' \
./ZoneEngine --validate-startup
```

Result:

```text
ZONEENGINE_VALIDATION_OK mode=startup provider=MySql listeners=0 assets=ok
```

Native listener-free lifecycle validation command:

```sh
cd /tmp/ao-rebirth-zoneengine-stage9-20260809-zone-001/publish
touch /tmp/ao-rebirth-zoneengine-stage9-20260809-zone-001/zone.shutdown
AO_REBIRTH_REQUIRED_SQL_TYPE=MySql \
AO_REBIRTH_EXPECTED_DATABASE=aorebirth_chatengine_stage6 \
AO_REBIRTH_ZONE_LISTEN_IP=127.0.0.1 \
AO_REBIRTH_CHAT_LISTEN_IP=127.0.0.1 \
AO_REBIRTH_CONFIG_PATH=/tmp/ao-rebirth-zoneengine-stage9-20260809-zone-001/publish/Config.xml \
AO_REBIRTH_MYSQL_CONNECTION='Server=127.0.0.1;Port=33067;Database=aorebirth_chatengine_stage6;Uid=aorebirth_stage8;Pwd=stage8-placeholder;SslMode=None' \
./ZoneEngine --validate-lifecycle --shutdown-file /tmp/ao-rebirth-zoneengine-stage9-20260809-zone-001/zone.shutdown
```

Result:

```text
ZONEENGINE_LIFECYCLE_READY listeners=0 database=closed
ZONEENGINE_LIFECYCLE_STOPPED status=clean
```

Native read-only database preflight ran through the systemd unit using the
root-owned Stage 6 environment file. Journal evidence:

```text
ZONEENGINE_DATABASE_OK provider=MySql database=aorebirth_chatengine_stage6 requiredTables=34 visibleTables=34 onlineCharacters=0 listeners=0
```

No database schema changes, DML fixture writes, gameplay changes, packet changes,
or generated-combat changes were performed.

## systemd validation

Repository unit:

```text
LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service
```

Installed unit:

```text
/etc/systemd/system/ao-rebirth-zoneengine.service
```

Installed release:

```text
/opt/ao-rebirth/zoneengine/releases/stage9-20260809-zone-006
```

Current symlink:

```text
/opt/ao-rebirth/zoneengine/current
```

Validator:

```sh
bash validate-disabled-service.sh \
  /tmp/ao-rebirth-zoneengine-stage9-20260809-zone-001/publish \
  /tmp/ao-rebirth-zoneengine-stage9-20260809-zone-001/deployment/ao-rebirth-zoneengine.service \
  stage9-20260809-zone-006
```

Result:

```text
ZONE_STAGE9_SYSTEMD_START_OK
ZONE_STAGE9_SYSTEMD_STATUS_OK
ZONE_STAGE9_SYSTEMD_STOP_OK
ZONE_STAGE9_SYSTEMD_RESTART_OK
ZONE_STAGE9_SYSTEMD_CONTROLLED_FAILURE_OK
PASS: ZoneEngine Stage 9 disabled service validation completed; service=disabled/inactive.
```

Post-validation state:

```text
Result=success
ActiveState=inactive
SubState=dead
systemctl is-enabled: disabled
TCP 7501: no listener
```

## Controlled failure

The validator installed a runtime drop-in setting:

```ini
Environment=AO_REBIRTH_ZONE_LISTEN_IP=0.0.0.0
Restart=no
```

The service failed deterministically during startup validation and was reset
after the assertion passed.

## Known independent Windows blocker

Windows Debug generated-combat validation remains blocked by the known
pre-existing provenance issue:

```text
captured realm 655 provenance lacks matching raw-derived SCFU metadata
```

Stage 9 did not inspect, regenerate, or modify generated-combat content.

## Credential incident

During manual direct shell validation, one failed command expansion printed the
Stage 6 disposable MySQL password in local tool output. The value is not stored
in the repository and is not repeated here. Rotate the Stage 6 disposable MySQL
credential externally before treating that database as sensitive.
