#!/usr/bin/env bash
set -euo pipefail

readonly SERVICE_NAME="ao-rebirth-zoneengine.service"
readonly SERVICE_USER="aorebirth"
readonly SERVICE_GROUP="aorebirth"
readonly WEB_USER="www-data"
readonly WEBSITE_CONTAINER="ao-rebirth-website"
readonly CLAIMS_DIRECTORY="/var/lib/ao-rebirth/zoneengine/mission-state/daily-login/claims"
readonly WEB_CLAIMS_DIRECTORY="/run/ao-rebirth-daily-login/claims"
readonly REWARDS_JSON="/opt/ao-rebirth/website/src/uwg.daily.icc-rk/rewards.json"
readonly DROP_IN_DIRECTORY="/etc/systemd/system/ao-rebirth-zoneengine.service.d"
readonly DROP_IN_TARGET="${DROP_IN_DIRECTORY}/20-daily-login.conf"

fail()
{
    echo "FAIL: $*" >&2
    exit 1
}

main()
{
    [[ "${EUID}" -eq 0 ]] || fail "run as root"
    [[ "$#" -eq 1 ]] || fail "usage: configure-daily-login-bridge.sh <systemd-drop-in>"

    local drop_in_source
    drop_in_source="$(realpath -e -- "$1")"
    [[ -f "${drop_in_source}" && ! -L "${drop_in_source}" ]] \
        || fail "systemd drop-in is missing or unsafe"
    [[ -d "${CLAIMS_DIRECTORY}" && ! -L "${CLAIMS_DIRECTORY}" ]] \
        || fail "claims directory is missing or unsafe"
    [[ -f "${REWARDS_JSON}" && ! -L "${REWARDS_JSON}" ]] \
        || fail "rewards JSON is missing or unsafe"
    id -u "${SERVICE_USER}" >/dev/null 2>&1 || fail "missing service user"
    getent group "${SERVICE_GROUP}" >/dev/null || fail "missing service group"
    id -u "${WEB_USER}" >/dev/null 2>&1 || fail "missing web user"
    command -v getfacl >/dev/null || fail "getfacl is unavailable"
    command -v setfacl >/dev/null || fail "setfacl is unavailable"
    command -v docker >/dev/null || fail "docker is unavailable"

    local pending_claim
    pending_claim="$(find "${CLAIMS_DIRECTORY}" -maxdepth 1 -type f -name 'pending-*.json' -print -quit)"
    [[ -z "${pending_claim}" ]] || fail "pending Daily Login claim must be resolved first"

    chown "${SERVICE_USER}:${SERVICE_GROUP}" "${CLAIMS_DIRECTORY}"
    chmod 02770 "${CLAIMS_DIRECTORY}"
    setfacl -m u:"${WEB_USER}":rwx,m::rwx "${CLAIMS_DIRECTORY}"
    setfacl -m d:u:"${WEB_USER}":rwX,d:g::rwx,d:m::rwx,d:o::--- "${CLAIMS_DIRECTORY}"

    runuser -u "${SERVICE_USER}" -g "${SERVICE_GROUP}" -- test -w "${CLAIMS_DIRECTORY}" \
        || fail "ZoneEngine cannot write the claims directory"
    runuser -u "${SERVICE_USER}" -g "${SERVICE_GROUP}" -- test -r "${REWARDS_JSON}" \
        || fail "ZoneEngine cannot read rewards JSON"
    docker exec -u "${WEB_USER}" "${WEBSITE_CONTAINER}" test -w "${WEB_CLAIMS_DIRECTORY}" \
        || fail "web process cannot write the claims directory"

    install -d -o root -g root -m 0755 "${DROP_IN_DIRECTORY}"
    install -o root -g root -m 0644 "${drop_in_source}" "${DROP_IN_TARGET}"
    systemctl daemon-reload
    systemd-analyze verify "${SERVICE_NAME}"

    echo "PASS: Daily Login claims and rewards bridge configured; restart ${SERVICE_NAME} to activate."
}

main "$@"
