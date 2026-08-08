# ChatEngine Ubuntu service package

This is the first deployable Linux server slice. It runs ChatEngine only;
LoginEngine and ZoneEngine remain on the later porting stages.

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

AO_RELEASE_PATH=/opt/ao-rebirth/chatengine/releases/stage5-test
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

Copy `chatengine.env.example` on the VPS, set its mode to `0600`, and add the
real `AO_REBIRTH_MYSQL_CONNECTION` value there. Do not put the database secret
in Git, `Config.xml`, command-line arguments, or chat output. The service's
`ExecStartPre` validates configuration and constructs a closed provider
connection object; it never calls `Open`.

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

Then reload systemd and start the service:

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
- UDP is disabled.

The first VPS pass must run `--validate-startup` and `--validate-lifecycle`
before starting either listener. A live database open, schema check, public
firewall change, or player test requires separate authorization and credentials.
