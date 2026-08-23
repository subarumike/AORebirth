# AORebirth Ubuntu service packages

The Linux deployment lane now packages ChatEngine and a separate LoginEngine
slice. ZoneEngine remains on a later porting stage. Both installed test services
stay disabled and loopback-only.

## Build the package

From the repository root, publish a framework-dependent `linux-x64` package:

```bat
LinuxBuild\publish-chatengine.cmd linux-x64 false
```

Or publish a self-contained package that does not require a matching .NET
runtime on the VPS:

```bat
LinuxBuild\publish-chatengine.cmd linux-x64 true
```

Use `linux-arm64` instead only when the VPS architecture is ARM64. Publishing
is deliberately untrimmed, non-single-file, and non-AOT because the messaging
and serialization paths use runtime type discovery.

Build the Stage 7 LoginEngine package with:

```bat
LinuxBuild\publish-loginengine.cmd linux-x64 true
```

The output is
`LinuxBuild/artifacts/loginengine/linux-x64/self-contained`. The helper starts
from a guarded empty target, restores for the requested RID, and validates the
ELF apphost, architecture, assemblies, MemBus asset, configuration, and 34 SQL
files before it reports success.

## Server layout

The checked-in systemd unit expects:

- binaries in `/opt/ao-rebirth/chatengine/current`;
- configuration in `/etc/ao-rebirth/chatengine/Config.xml`;
- secrets in `/etc/ao-rebirth/chatengine/chatengine.env`;
- an unprivileged `aorebirth` user and group.

For the first Ubuntu 24.04 installation, upload the selected publish directory
as `/tmp/ao-rebirth-chatengine-publish`, the checked-in unit as
`/tmp/ao-rebirth-chatengine.service`, and `chatengine.env.example` as
`/tmp/chatengine.env.example`. Then install them with these exact commands:

```sh
sudo groupadd --system aorebirth
sudo useradd --system --gid aorebirth --home-dir /nonexistent \
  --shell /usr/sbin/nologin aorebirth

AO_RELEASE_PATH=/opt/ao-rebirth/chatengine/releases/stage6-test-001
sudo install -d -o root -g root -m 0755 /opt/ao-rebirth/chatengine/releases
sudo install -d -o root -g root -m 0755 "$AO_RELEASE_PATH"
sudo cp -a /tmp/ao-rebirth-chatengine-publish/. "$AO_RELEASE_PATH"/
sudo chown -R root:root "$AO_RELEASE_PATH"
sudo find "$AO_RELEASE_PATH" -type d -exec chmod 0755 {} +
sudo find "$AO_RELEASE_PATH" -type f -exec chmod 0644 {} +
sudo chmod 0755 "$AO_RELEASE_PATH/ChatEngine"
if [ -f "$AO_RELEASE_PATH/createdump" ]; then
  sudo chmod 0755 "$AO_RELEASE_PATH/createdump"
fi
sudo ln -sT "$AO_RELEASE_PATH" /opt/ao-rebirth/chatengine/current

sudo install -d -o root -g aorebirth -m 0750 /etc/ao-rebirth
sudo install -d -o root -g aorebirth -m 0750 /etc/ao-rebirth/chatengine
sudo install -o root -g aorebirth -m 0640 \
  "$AO_RELEASE_PATH/Config.xml" /etc/ao-rebirth/chatengine/Config.xml
sudo install -o root -g root -m 0600 \
  /tmp/chatengine.env.example /etc/ao-rebirth/chatengine/chatengine.env
sudo install -o root -g root -m 0644 \
  /tmp/ao-rebirth-chatengine.service \
  /etc/systemd/system/ao-rebirth-chatengine.service
sudo sed -i 's/\r$//' \
  /etc/ao-rebirth/chatengine/chatengine.env \
  /etc/systemd/system/ao-rebirth-chatengine.service
sudoedit /etc/ao-rebirth/chatengine/chatengine.env
```

These are first-install commands: `groupadd`, `useradd`, and the `current`
symlink deliberately fail instead of replacing an existing installation.

For an update, stop the still-disabled service, install into a new immutable
versioned release directory, remember the prior target, and replace `current`
atomically. Never copy new files over an existing release:

```sh
sudo systemctl stop ao-rebirth-chatengine.service
AO_PREVIOUS_RELEASE="$(readlink -f -- /opt/ao-rebirth/chatengine/current)"
AO_RELEASE_PATH=/opt/ao-rebirth/chatengine/releases/stage6-test-002
AO_NEXT_LINK=/opt/ao-rebirth/chatengine/.current-stage6-next
AO_ROLLBACK_LINK=/opt/ao-rebirth/chatengine/.current-stage6-rollback
test -d "$AO_PREVIOUS_RELEASE"
test ! -e "$AO_RELEASE_PATH"
test ! -e "$AO_NEXT_LINK"
sudo install -d -o root -g root -m 0755 "$AO_RELEASE_PATH"
sudo cp -a /tmp/ao-rebirth-chatengine-publish/. "$AO_RELEASE_PATH"/
sudo chown -R root:root "$AO_RELEASE_PATH"
sudo find "$AO_RELEASE_PATH" -type d -exec chmod 0755 {} +
sudo find "$AO_RELEASE_PATH" -type f -exec chmod 0644 {} +
sudo chmod 0755 "$AO_RELEASE_PATH/ChatEngine"
sudo ln -sT "$AO_RELEASE_PATH" "$AO_NEXT_LINK"
sudo mv -Tf "$AO_NEXT_LINK" /opt/ao-rebirth/chatengine/current
AO_UNIT_PATH=/etc/systemd/system/ao-rebirth-chatengine.service
AO_UNIT_BACKUP=/etc/systemd/system/ao-rebirth-chatengine.service.stage6-test-002-previous
test -f "$AO_UNIT_PATH"
test ! -e "$AO_UNIT_BACKUP"
sudo cp --preserve=mode,ownership,timestamps "$AO_UNIT_PATH" "$AO_UNIT_BACKUP"
sudo install -o root -g root -m 0644 \
  /tmp/ao-rebirth-chatengine.service "$AO_UNIT_PATH"
sudo systemctl daemon-reload
```

If validation fails, roll back both artifacts while the service remains
inactive:

```sh
test ! -e "$AO_ROLLBACK_LINK"
sudo ln -sT "$AO_PREVIOUS_RELEASE" "$AO_ROLLBACK_LINK"
sudo mv -Tf "$AO_ROLLBACK_LINK" /opt/ao-rebirth/chatengine/current
sudo install -o root -g root -m 0644 "$AO_UNIT_BACKUP" "$AO_UNIT_PATH"
sudo systemctl daemon-reload
```

Change `current` or the unit only while the service is inactive; the unit
resolves `current` separately for its preflight and runtime commands.

Copy `chatengine.env.example` on the VPS, set its mode to `0600`, and add the
real `AO_REBIRTH_MYSQL_CONNECTION` value there. Do not put the database secret
in Git, `Config.xml`, command-line arguments, or chat output. The service's
first `ExecStartPre` validates configuration and constructs a closed provider
connection object. Its second `ExecStartPre` opens the configured database
read-only, requires all 34 governed ChatEngine tables plus
`characters.Online`, and verifies that the runtime account can read every
required table before either listener starts.

Keep `/etc/ao-rebirth` and `/etc/ao-rebirth/chatengine` as `root:aorebirth`
mode `0750`: exact-case validation enumerates the latter, so the service needs
both group read and execute access. Install `Config.xml` as `root:aorebirth`
mode `0640`. Keep `chatengine.env` root-owned mode `0600`; systemd reads that
file before starting the service.

This first deployment is operationally MySQL-only. SQL Server and PostgreSQL
remain compile-covered compatibility surfaces; do not select them for this
service package. The systemd unit sets `AO_REBIRTH_REQUIRED_SQL_TYPE=MySql`
directly, so omitting the copied environment setting cannot weaken that
boundary. Under this profile, startup requires `AO_REBIRTH_MYSQL_CONNECTION`
and rejects an operational MySQL connection string in `Config.xml`; leave the
checked-in `REPLACE_WITH_` placeholder there.
`LogChat` must remain `false` for this milestone because the legacy per-channel
writer is not safe under concurrent player traffic; journald server logging is
still enabled.

Install the published `ChatEngine` apphost with executable mode (`0755`). A
framework-dependent package also requires the matching .NET 10 runtime on the
VPS; a self-contained package does not.

Before enabling the service, validate the unit and the offline startup path:

```sh
sudo systemd-analyze verify /etc/systemd/system/ao-rebirth-chatengine.service
sudo systemd-run --quiet --wait --pipe --collect \
  --uid=aorebirth \
  --working-directory=/opt/ao-rebirth/chatengine/current \
  --property=EnvironmentFile=/etc/ao-rebirth/chatengine/chatengine.env \
  /opt/ao-rebirth/chatengine/current/ChatEngine --validate-startup
sudo systemd-run --quiet --wait --pipe --collect \
  --uid=aorebirth \
  --working-directory=/opt/ao-rebirth/chatengine/current \
  --property=EnvironmentFile=/etc/ao-rebirth/chatengine/chatengine.env \
  /opt/ao-rebirth/chatengine/current/ChatEngine --validate-database
```

Then prove real Linux SIGTERM delivery through the listener-free lifecycle mode:

```sh
sudo systemd-run \
  --unit=ao-rebirth-chatengine-lifecycle \
  --uid=aorebirth \
  --working-directory=/opt/ao-rebirth/chatengine/current \
  /opt/ao-rebirth/chatengine/current/ChatEngine --validate-lifecycle
sudo timeout 10 sh -c \
  'until journalctl -u ao-rebirth-chatengine-lifecycle.service --no-pager -n 20 | grep -Fq CHATENGINE_LIFECYCLE_READY; do sleep 0.1; done'
sudo systemctl stop ao-rebirth-chatengine-lifecycle.service
sudo journalctl -u ao-rebirth-chatengine-lifecycle.service --no-pager -n 20 | \
  grep -F CHATENGINE_LIFECYCLE_STOPPED
sudo systemctl reset-failed ao-rebirth-chatengine-lifecycle.service
```

The final grep must report `status=clean`. This mode opens no database or
listener. Cross-RID packages are structure-validated on the build host; the
target apphost must still run these modes on matching Ubuntu architecture.

## Isolated Stage 6 MySQL acceptance

The checked-in `mysql-stage6` scripts provision a deliberately disposable
MySQL 8.4 target without touching an existing AORebirth, website, or mail
database. The exact boundary is:

- container `aorebirth-chatengine-mysql-stage6`;
- database `aorebirth_chatengine_stage6`;
- runtime user `aorebirth_stage6`;
- dedicated network and volume with the Stage 6 disposable label;
- host binding `127.0.0.1:33067` only;
- `--restart=no`, so it is not enabled across a VPS reboot;
- root-owned credentials below `/etc/ao-rebirth/chatengine/stage6`, mode `0600`.

Upload the four scripts in `LinuxBuild/deployment/mysql-stage6` to a unique
root-only temporary directory. From Windows, with `YOUR_VPS` replaced by the
configured host:

```bat
ssh.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 root@YOUR_VPS "test ! -e /root/aorebirth-stage6-mysql-001 && install -d -o root -g root -m 0700 /root/aorebirth-stage6-mysql-001"
scp.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 LinuxBuild\deployment\mysql-stage6\provision-disposable-mysql.sh LinuxBuild\deployment\mysql-stage6\apply-governed-schema.sh LinuxBuild\deployment\mysql-stage6\remove-disposable-mysql.sh LinuxBuild\deployment\mysql-stage6\validate-disabled-service.sh root@YOUR_VPS:/root/aorebirth-stage6-mysql-001/
```

On Ubuntu, pull the exact immutable MySQL 8.4 image, lock the script modes,
provision the empty container, load the governed publish inventory, and run the
live database preflight without opening a listener:

```sh
sudo docker pull mysql@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d
sudo chmod 0700 \
  /root/aorebirth-stage6-mysql-001/provision-disposable-mysql.sh \
  /root/aorebirth-stage6-mysql-001/apply-governed-schema.sh \
  /root/aorebirth-stage6-mysql-001/remove-disposable-mysql.sh \
  /root/aorebirth-stage6-mysql-001/validate-disabled-service.sh
sudo bash /root/aorebirth-stage6-mysql-001/provision-disposable-mysql.sh
sudo bash /root/aorebirth-stage6-mysql-001/apply-governed-schema.sh \
  /opt/ao-rebirth/chatengine/current/SqlTables
sudo systemd-run --quiet --wait --pipe --collect \
  --uid=aorebirth \
  --working-directory=/opt/ao-rebirth/chatengine/current \
  --property=EnvironmentFile=/etc/ao-rebirth/chatengine/stage6/chatengine.env \
  /opt/ao-rebirth/chatengine/current/ChatEngine --validate-database
```

The schema loader refuses an existing table, missing/extra/case-mismatched SQL
file, wrong container label/network/database, or non-root credential mode. It
does not use wildcards or `mysql --force`. After import it verifies the exact
34-table set, fresh mutable-table state, and `characters.Online`, then reduces
the application account to `SELECT`, `INSERT`, `UPDATE`, and `DELETE` on only
the disposable database.

Build the listener-free integration harness locally with:

```bat
LinuxBuild\verify-stage6-mysql.cmd
```

Publish it self-contained for the VPS architecture from the repository root:

```bat
set STAGE6_TOOL_VERSION=stage6-tool-001
if exist LinuxBuild\artifacts\stage6-mysql\%STAGE6_TOOL_VERSION% exit /b 1
dotnet restore LinuxBuild\Tools\Stage6MySqlIntegrationTests\Stage6MySqlIntegrationTests.csproj --runtime linux-x64
dotnet publish LinuxBuild\Tools\Stage6MySqlIntegrationTests\Stage6MySqlIntegrationTests.csproj --configuration Release --runtime linux-x64 --self-contained true --no-restore --output LinuxBuild\artifacts\stage6-mysql\%STAGE6_TOOL_VERSION%
ssh.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 root@YOUR_VPS "test ! -e /tmp/%STAGE6_TOOL_VERSION%"
scp.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 -r LinuxBuild\artifacts\stage6-mysql\%STAGE6_TOOL_VERSION% root@YOUR_VPS:/tmp/%STAGE6_TOOL_VERSION%
```

Then make the upload root-controlled but executable by the service account and
run only the exact disposable mode through the root-read environment file:

```sh
STAGE6_TOOL_PATH=/tmp/stage6-tool-001
sudo chown -R root:aorebirth "$STAGE6_TOOL_PATH"
sudo chmod 0750 "$STAGE6_TOOL_PATH"
sudo find "$STAGE6_TOOL_PATH" -type f -exec chmod 0640 {} +
sudo chmod 0750 "$STAGE6_TOOL_PATH/Stage6MySqlIntegrationTests"
sudo systemd-run --quiet --wait --pipe --collect \
  --uid=aorebirth \
  --working-directory="$STAGE6_TOOL_PATH" \
  --property=EnvironmentFile=/etc/ao-rebirth/chatengine/stage6/chatengine.env \
  "$STAGE6_TOOL_PATH/Stage6MySqlIntegrationTests" --run-disposable
```

The live gate
uses the production configuration, connector, DAOs, password hashing, and
encrypted login-key path. It commits one uniquely named login/character pair
so the production DAOs can see it, exercises positive and negative login and
ownership cases, deletes character then login in `finally`, and requires zero
fixture residue before passing. It never opens a listener or prints a secret.

After installing a package that includes `--validate-database`, prove the
disabled systemd unit end to end. The validator adds a root-owned runtime
systemd drop-in below `/run`, removes it on exit, and never replaces the normal
environment file:

```sh
sudo bash /root/aorebirth-stage6-mysql-001/validate-disabled-service.sh
```

This transiently starts the still-disabled unit, requires both configuration
and live database `ExecStartPre` gates, waits for `Type=notify`, proves both
listeners are loopback-only, delivers SIGTERM, requires a clean service result,
and removes its runtime drop-in even on failure. The normal
`/etc/ao-rebirth/chatengine/chatengine.env` is never replaced.
If the validator is killed untrappably, its drop-in forces the service to stop
after 90 seconds. Recover the exact disabled state with:

```sh
sudo bash /root/aorebirth-stage6-mysql-001/validate-disabled-service.sh \
  --recover-stage6-validation
```

When the disposable target is no longer needed, remove only its exact labeled
resources and root-only test credentials with:

```sh
sudo bash /root/aorebirth-stage6-mysql-001/remove-disposable-mysql.sh \
  --confirm-remove-aorebirth-chatengine-stage6
```

The removal script fails closed on any name, label, or secret-directory
mismatch and can safely resume after a partial prior removal. Provisioning
attempts to roll back only exact resources created by a failed invocation.

After removal and recovery have passed, delete only the two verified upload
directories:

```sh
test "$(realpath -e -- /root/aorebirth-stage6-mysql-001)" = \
  /root/aorebirth-stage6-mysql-001 || exit 1
test "$(realpath -e -- /tmp/stage6-tool-001)" = /tmp/stage6-tool-001 \
  || exit 1
sudo rm -r -- /root/aorebirth-stage6-mysql-001 /tmp/stage6-tool-001
```

Stage 6 ChatEngine acceptance ends here. Keep the service disabled and inactive.
Retain the disposable database only when proceeding directly to Stage 7;
otherwise remove it with the guarded Stage 6 workflow above.

## Isolated Stage 7 LoginEngine acceptance

Stage 7 reuses the isolated Stage 6 database on `127.0.0.1:33067`; it does not
touch the website or mail database containers. Publish `linux-x64` self-contained
and upload the package plus the exact unit/installer/validator paths expected by
the first-install guard:

```bat
scp.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 -r LinuxBuild\artifacts\loginengine\linux-x64\self-contained root@YOUR_VPS:/tmp/ao-rebirth-loginengine-publish
scp.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 LinuxBuild\deployment\systemd\ao-rebirth-loginengine.service root@YOUR_VPS:/tmp/ao-rebirth-loginengine.service
scp.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 LinuxBuild\deployment\login-stage7\install-disabled-service.sh root@YOUR_VPS:/tmp/ao-rebirth-loginengine-install.sh
scp.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 LinuxBuild\deployment\login-stage7\validate-disabled-service.sh root@YOUR_VPS:/tmp/ao-rebirth-loginengine-validate.sh
ssh.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 root@YOUR_VPS chmod -R go-w -- /tmp/ao-rebirth-loginengine-publish
ssh.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 root@YOUR_VPS bash /tmp/ao-rebirth-loginengine-install.sh stage7-YYYYMMDD-login-001
ssh.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 root@YOUR_VPS bash /tmp/ao-rebirth-loginengine-validate.sh
```

The installer is root-only and first-install-only. It imports exactly one
Stage 6 connection assignment without printing it, installs a unique immutable
release below `/opt/ao-rebirth/loginengine/releases`, pins the expected database,
and leaves `ao-rebirth-loginengine.service` disabled and inactive. The validator
rejects unreviewed systemd drop-ins, transiently starts the still-disabled unit,
requires live database preflight and `Type=notify`, proves that the exact main PID
alone owns `127.0.0.1:7500`, delivers SIGTERM, and restores disabled/inactive
state. A successful run prints:

```text
PASS: disabled LoginEngine passed live database preflight, Type=notify readiness, exact loopback listener ownership, and clean SIGTERM shutdown.
```

## Stage 7.1 security acceptance and disabled-service upgrade

Stage 7.1 adds fail-closed per-client authenticated state, CSPRNG server salt,
canonical authenticated identity and character-ownership guards, same-client
FIFO dispatch with a bounded shutdown drain, and transactional cleanup of the
governed character-owned data graph. Run both checked-in offline gates before
building or uploading a test package:

```bat
LinuxBuild\verify-stage7-contracts.cmd
LinuxBuild\verify-stage7-security-mysql.cmd
```

The second wrapper builds and runs
`LinuxBuild/Tools/Stage7MySqlSecurityIntegrationTests`. Live disposable mode is
deliberately guarded: it requires the exact Stage 6 database/user/loopback target
and the acknowledgement below, opens no listener, fingerprints the governed
tables before and after, and fails unless the baseline is restored exactly.
After publishing and uploading the self-contained tool to an exact reviewed
directory, run it through a transient unit:

```sh
sudo systemd-run --quiet --wait --pipe --collect \
  --uid=aorebirth \
  --working-directory=/tmp/stage7-security-tool-003 \
  --property=EnvironmentFile=/etc/ao-rebirth/chatengine/stage6/chatengine.env \
  --property=RuntimeMaxSec=120s \
  --setenv=AO_REBIRTH_STAGE7_SECURITY_DISPOSABLE_ACK=AO_REBIRTH_STAGE7_SECURITY_DISPOSABLE_ONLY \
  /tmp/stage7-security-tool-003/Stage7MySqlSecurityIntegrationTests --run-disposable
```

The accepted MySQL 8.4 run prints `residue=0` and leaves both engine listeners
closed. Never reuse the acknowledgement against a persistent or differently
named database.

The guarded release upgrade requires Ubuntu's `acl` and `attr` packages so it
can validate and canonicalize ACLs and extended attributes instead of inheriting
upload metadata:

```sh
sudo apt-get install --no-install-recommends acl attr file libcap2-bin util-linux
command -v file findmnt flock getcap getfacl getfattr realpath setcap setfacl
```

Publish LoginEngine, then upload the publish directory, reviewed unit, guarded
upgrade, and existing live validator to their exact input paths:

```bat
ssh.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 root@YOUR_VPS "set -eu; test ! -e /tmp/ao-rebirth-loginengine-publish; test ! -L /tmp/ao-rebirth-loginengine-publish; test ! -e /tmp/ao-rebirth-loginengine.service; test ! -L /tmp/ao-rebirth-loginengine.service; test ! -e /tmp/ao-rebirth-loginengine-upgrade.sh; test ! -L /tmp/ao-rebirth-loginengine-upgrade.sh; test ! -e /tmp/ao-rebirth-loginengine-validate.sh; test ! -L /tmp/ao-rebirth-loginengine-validate.sh"
scp.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 -r LinuxBuild\artifacts\loginengine\linux-x64\self-contained root@YOUR_VPS:/tmp/ao-rebirth-loginengine-publish
scp.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 LinuxBuild\deployment\systemd\ao-rebirth-loginengine.service root@YOUR_VPS:/tmp/ao-rebirth-loginengine.service
scp.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 LinuxBuild\deployment\login-stage7\upgrade-disabled-service.sh root@YOUR_VPS:/tmp/ao-rebirth-loginengine-upgrade.sh
scp.exe -i C:\Users\YOUR_USER\.ssh\id_ed25519 LinuxBuild\deployment\login-stage7\validate-disabled-service.sh root@YOUR_VPS:/tmp/ao-rebirth-loginengine-validate.sh
```

Windows-to-Linux copies can retain permissive directory modes. Before changing
them recursively, require the upload to resolve to the exact expected directory
and reject links or special files. Then remove group/world write access, lock the
script modes, perform the atomic disabled-service upgrade, and rerun live
validation:

```sh
(
set -euo pipefail
login_upload=/tmp/ao-rebirth-loginengine-publish
test "$(realpath -e -- "$login_upload")" = "$login_upload"
test -d "$login_upload"
test ! -L "$login_upload"
link_path="$(find -P "$login_upload" -xdev -type l -print -quit)"
special_path="$(find -P "$login_upload" -xdev ! -type d ! -type f -print -quit)"
hardlink_path="$(find -P "$login_upload" -xdev -type f -links +1 -print -quit)"
foreign_owner_path="$(find -P "$login_upload" -xdev ! -user root -print -quit)"
foreign_group_path="$(find -P "$login_upload" -xdev ! -group root -print -quit)"
mount_path="$(findmnt -rn -o TARGET | awk -v root="$login_upload" '$0 == root || index($0, root "/") == 1 { print; exit }')"
test -z "$link_path"
test -z "$special_path"
test -z "$hardlink_path"
test -z "$foreign_owner_path"
test -z "$foreign_group_path"
test -z "$mount_path"
sudo chmod -R go-w -- "$login_upload"
sudo chmod 0700 /tmp/ao-rebirth-loginengine-upgrade.sh /tmp/ao-rebirth-loginengine-validate.sh
sudo bash /tmp/ao-rebirth-loginengine-upgrade.sh stage7-20260809-login-003
sudo bash /tmp/ao-rebirth-loginengine-validate.sh
)
```

The pre-upload absence check is mandatory: never let `scp -r` merge a new
package into an old directory. After a successful upgrade and validation, this
exact root-only cleanup removes the disposable upload without following links or
crossing a mounted subtree:

```sh
(
set -euo pipefail
login_upload=/tmp/ao-rebirth-loginengine-publish
test "$(realpath -e -- "$login_upload")" = "$login_upload"
test -d "$login_upload"
test ! -L "$login_upload"
link_path="$(find -P "$login_upload" -xdev -type l -print -quit)"
special_path="$(find -P "$login_upload" -xdev ! -type d ! -type f -print -quit)"
hardlink_path="$(find -P "$login_upload" -xdev -type f -links +1 -print -quit)"
foreign_owner_path="$(find -P "$login_upload" -xdev ! -user root -print -quit)"
mount_path="$(findmnt -rn -o TARGET | awk -v root="$login_upload" '$0 == root || index($0, root "/") == 1 { print; exit }')"
test -z "$link_path"
test -z "$special_path"
test -z "$hardlink_path"
test -z "$foreign_owner_path"
test -z "$mount_path"
sudo rm -r --one-file-system -- "$login_upload"
)
```

The upgrade uses a fixed lock, validates package/unit/environment boundaries,
installs a new immutable release, switches `current` atomically, and rolls back
without deleting an active target if validation fails. The accepted release is
`stage7-20260809-login-003`; database preflight, `Type=notify`, exact main-PID
ownership of `127.0.0.1:7500`, and clean SIGTERM pass. The unit remains
disabled/inactive, TCP 7500 is closed, and the MySQL 8.4 target is healthy and
bound only to loopback.

Do not expose or enable LoginEngine yet. ZoneEngine is not present at
`127.0.0.1:7501`, official-client end-to-end login and retry/error UX have not
been proven, account character-count semantics remain unresolved, and no
sustained multiplayer soak has passed.

## ChatEngine production activation (separate approval)

Do not run this step against the disposable Stage 6 database. Activation
requires separate approval, a persistent production database that passes both
preflight modes, a populated root-only normal environment file, and confirmed
TCP 7012 firewall policy. Only then reload systemd and enable the service:

```sh
sudo systemctl daemon-reload
sudo systemctl enable --now ao-rebirth-chatengine.service
sudo systemctl status ao-rebirth-chatengine.service
```

The unit uses `Type=notify`; systemd does not report it active until both the
player Chat listener and loopback ISCom listener have started successfully.

Logs go to journald:

```sh
journalctl -u ao-rebirth-chatengine.service
```

## Network boundary

- TCP 7012 is the player-facing Chat port and may be exposed only after the
  public address and firewall policy are confirmed. The example service binds
  it to `127.0.0.1`; change `AO_REBIRTH_CHAT_LISTEN_IP` only after that approval.
- TCP 6996 is unauthenticated internal ISCom traffic. The Linux default and
  example environment bind it to `127.0.0.1`; never expose it publicly.
- TCP 7500 is the player-facing LoginEngine endpoint. Production clients connect
  directly, so the production unit uses explicit `AO_REBIRTH_BIND_MODE=Public`;
  `Loopback` remains the local/rollback mode.
- TCP 7501 is the player-facing ZoneEngine endpoint advertised by LoginEngine's
  configured `ZoneIP`. There is no reverse proxy or tunnel terminating either
  game-engine connection.
- UDP is disabled.

The first VPS pass must run `--validate-startup`, `--validate-database`, and
`--validate-lifecycle` before production listener activation. A public firewall
change or player test still requires separate authorization.
