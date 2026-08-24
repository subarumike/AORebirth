#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C

readonly EXPECTED_OWNERSHIP_DIR="/var/lib/ao-rebirth/session-ownership"
readonly EXPECTED_SERVICE_USER="aorebirth"
readonly EXPECTED_SERVICE_GROUP="aorebirth"
readonly EXPECTED_DATABASE="aorebirth_chatengine_stage6"
readonly DATABASE_CONTAINER="aorebirth-chatengine-mysql-stage6"

manifest_path=""
expected_sha=""
dry_run=false
deploy_root="${AO_REBIRTH_DEPLOY_TEST_ROOT:-}"
test_mode="${AO_REBIRTH_DEPLOY_TEST_MODE:-0}"
failure_step="${AO_REBIRTH_DEPLOY_TEST_FAIL_STEP:-}"
mutation_started=false
deployment_committed=false
rolling_back=false
snapshot_dir=""
rollback_first_failure=""

fail() { echo "FAIL: $*" >&2; exit 1; }
usage() { echo "usage: upgrade-active-services.sh --manifest <release.manifest> --expected-sha <sha> [--dry-run]" >&2; }
root_path() { printf '%s%s' "${deploy_root}" "$1"; }

while [[ "$#" -gt 0 ]]; do
    case "$1" in
        --manifest) manifest_path="${2:-}"; shift 2 ;;
        --expected-sha) expected_sha="${2:-}"; shift 2 ;;
        --dry-run) dry_run=true; shift ;;
        --help) usage; exit 0 ;;
        *) usage; exit 2 ;;
    esac
done

[[ "${test_mode}" == "1" || "${EUID}" -eq 0 ]] || fail "run as root"
[[ "${expected_sha}" =~ ^[0-9a-f]{40}$ ]] || fail "invalid expected source SHA"
[[ -f "${manifest_path}" && ! -L "${manifest_path}" ]] || fail "release manifest is missing or unsafe"
manifest_path="$(realpath -e -- "${manifest_path}")"

manifest_value()
{
    local key="$1"
    local count line
    count="$(grep -Ec "^${key}=" "${manifest_path}" || true)"
    [[ "${count}" == "1" ]] || fail "manifest key ${key} is missing or duplicated"
    line="$(grep -E "^${key}=" "${manifest_path}")"
    printf '%s' "${line#*=}"
}

readonly FORMAT="$(manifest_value FORMAT)"
readonly SOURCE_SHA="$(manifest_value SOURCE_SHA)"
readonly BUILD_TIMESTAMP_UTC="$(manifest_value BUILD_TIMESTAMP_UTC)"
readonly LOGIN_ARTIFACT_DIR="$(manifest_value LOGINENGINE_ARTIFACT_DIR)"
readonly LOGIN_ARTIFACT_SHA="$(manifest_value LOGINENGINE_ARTIFACT_SHA256)"
readonly ZONE_ARTIFACT_DIR="$(manifest_value ZONEENGINE_ARTIFACT_DIR)"
readonly ZONE_ARTIFACT_SHA="$(manifest_value ZONEENGINE_ARTIFACT_SHA256)"
readonly LOGIN_UNIT_SOURCE="$(manifest_value LOGINENGINE_UNIT_PATH)"
readonly LOGIN_UNIT_SHA="$(manifest_value LOGINENGINE_UNIT_SHA256)"
readonly ZONE_UNIT_SOURCE="$(manifest_value ZONEENGINE_UNIT_PATH)"
readonly ZONE_UNIT_SHA="$(manifest_value ZONEENGINE_UNIT_SHA256)"
readonly LOGIN_SERVICE="$(manifest_value LOGINENGINE_SERVICE)"
readonly ZONE_SERVICE="$(manifest_value ZONEENGINE_SERVICE)"
readonly MANIFEST_PREVIOUS_LOGIN="$(manifest_value PREVIOUS_LOGINENGINE_RELEASE)"
readonly MANIFEST_PREVIOUS_ZONE="$(manifest_value PREVIOUS_ZONEENGINE_RELEASE)"
readonly LOGIN_INSTALL_ROOT="$(root_path /opt/ao-rebirth/loginengine)"
readonly ZONE_INSTALL_ROOT="$(root_path /opt/ao-rebirth/zoneengine)"
readonly LOGIN_RELEASES="${LOGIN_INSTALL_ROOT}/releases"
readonly ZONE_RELEASES="${ZONE_INSTALL_ROOT}/releases"
readonly LOGIN_CURRENT="${LOGIN_INSTALL_ROOT}/current"
readonly ZONE_CURRENT="${ZONE_INSTALL_ROOT}/current"
readonly LOGIN_UNIT_TARGET="$(root_path /etc/systemd/system/ao-rebirth-loginengine.service)"
readonly ZONE_UNIT_TARGET="$(root_path /etc/systemd/system/ao-rebirth-zoneengine.service)"
readonly LOGIN_ENV="$(root_path /etc/ao-rebirth/loginengine/loginengine.env)"
readonly ZONE_ENV="$(root_path /etc/ao-rebirth/zoneengine/zoneengine.env)"
readonly LOGIN_CONFIG="$(root_path /etc/ao-rebirth/loginengine/Config.xml)"
readonly ZONE_CONFIG="$(root_path /etc/ao-rebirth/zoneengine/Config.xml)"
readonly OWNERSHIP_DIR="$(root_path "${EXPECTED_OWNERSHIP_DIR}")"
readonly SNAPSHOT_ROOT="$(root_path /opt/ao-rebirth/deployment-snapshots)"
readonly DEPLOYED_MANIFEST="$(root_path /opt/ao-rebirth/deployed-release.env)"
readonly RELEASE_NAME="release-${SOURCE_SHA}"
readonly LOGIN_RELEASE_TARGET="${LOGIN_RELEASES}/${RELEASE_NAME}"
readonly ZONE_RELEASE_TARGET="${ZONE_RELEASES}/${RELEASE_NAME}"
readonly TEST_STATE="${deploy_root}/test-state"

validate_manifest_shape()
{
    [[ "${FORMAT}" == "1" ]] || fail "unsupported release manifest format"
    [[ "${SOURCE_SHA}" == "${expected_sha}" ]] || fail "manifest source SHA mismatch"
    [[ "${SOURCE_SHA}" =~ ^[0-9a-f]{40}$ ]] || fail "manifest source SHA is invalid"
    [[ "${LOGIN_SERVICE}" == "ao-rebirth-loginengine.service" ]] || fail "unexpected LoginEngine service"
    [[ "${ZONE_SERVICE}" == "ao-rebirth-zoneengine.service" ]] || fail "unexpected ZoneEngine service"
    local unknown
    unknown="$(cut -d= -f1 "${manifest_path}" | grep -Ev '^(FORMAT|SOURCE_SHA|BUILD_TIMESTAMP_UTC|LOGINENGINE_ARTIFACT_DIR|LOGINENGINE_ARTIFACT_SHA256|ZONEENGINE_ARTIFACT_DIR|ZONEENGINE_ARTIFACT_SHA256|LOGINENGINE_UNIT_PATH|LOGINENGINE_UNIT_SHA256|ZONEENGINE_UNIT_PATH|ZONEENGINE_UNIT_SHA256|LOGINENGINE_SERVICE|ZONEENGINE_SERVICE|PREVIOUS_LOGINENGINE_RELEASE|PREVIOUS_ZONEENGINE_RELEASE)$' | head -n 1 || true)"
    [[ -z "${unknown}" ]] || fail "unknown manifest key ${unknown}"
}

require_regular_file() { [[ -f "$1" && ! -L "$1" ]] || fail "required file is missing or unsafe: $1"; }

require_artifact()
{
    local artifact_dir="$1" apphost="$2" expected_hash="$3"
    [[ -d "${artifact_dir}" && ! -L "${artifact_dir}" ]] || fail "${apphost} artifact directory is unsafe"
    require_regular_file "${artifact_dir}/${apphost}"
    require_regular_file "${artifact_dir}/SOURCE_SHA"
    require_regular_file "${artifact_dir}/BUILD_PROVENANCE.env"
    require_regular_file "${artifact_dir}/LINUX_ACCEPTANCE.env"
    [[ "$(sha256sum "${artifact_dir}/${apphost}" | awk '{print $1}')" == "${expected_hash}" ]] || fail "${apphost} artifact hash mismatch"
    [[ "$(tr -d '\r\n\t ' < "${artifact_dir}/SOURCE_SHA")" == "${SOURCE_SHA}" ]] || fail "${apphost} SOURCE_SHA mismatch"
    grep -Fx "COMMIT_SHA=${SOURCE_SHA}" "${artifact_dir}/BUILD_PROVENANCE.env" >/dev/null || fail "${apphost} build provenance mismatch"
    grep -Fx "AO_REBIRTH_SOURCE_SHA=${SOURCE_SHA}" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null || fail "${apphost} acceptance source mismatch"
    grep -Fx "SOURCE_SHA_MATCH=PASS" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null || fail "${apphost} source SHA gate did not pass"
    grep -Fx "TRACKED_SOURCE_CLEAN=PASS" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null || fail "${apphost} tracked-source gate did not pass"
    grep -Fx "LINUX_ACCEPTANCE=PASS" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null || fail "${apphost} Linux acceptance did not pass"
    [[ -z "$(find "${artifact_dir}" -type l -print -quit)" ]] || fail "${apphost} artifact contains a symlink"
    [[ -z "$(find "${artifact_dir}" ! -type f ! -type d -print -quit)" ]] || fail "${apphost} artifact contains a special file"
}

require_exact_line() { [[ "$(grep -Fxc -- "$2" "$1" || true)" == "1" ]] || fail "$3"; }
line_number() { grep -nFx -- "$2" "$1" | cut -d: -f1; }
ownership_value()
{
    local line
    line="$(grep -F 'Environment=AO_REBIRTH_SESSION_OWNERSHIP_DIR=' "$1")"
    line="${line#*=}"
    printf '%s' "${line#*=}"
}

validate_units()
{
    require_regular_file "${LOGIN_UNIT_SOURCE}"
    require_regular_file "${ZONE_UNIT_SOURCE}"
    [[ "$(sha256sum "${LOGIN_UNIT_SOURCE}" | awk '{print $1}')" == "${LOGIN_UNIT_SHA}" ]] || fail "LoginEngine unit hash mismatch"
    [[ "$(sha256sum "${ZONE_UNIT_SOURCE}" | awk '{print $1}')" == "${ZONE_UNIT_SHA}" ]] || fail "ZoneEngine unit hash mismatch"
    local unit
    for unit in "${LOGIN_UNIT_SOURCE}" "${ZONE_UNIT_SOURCE}"; do
        require_exact_line "${unit}" "User=${EXPECTED_SERVICE_USER}" "service user contract failed"
        require_exact_line "${unit}" "Group=${EXPECTED_SERVICE_GROUP}" "service group contract failed"
        require_exact_line "${unit}" "PrivateTmp=true" "PrivateTmp contract failed"
        require_exact_line "${unit}" "StateDirectory=ao-rebirth" "StateDirectory contract failed"
        require_exact_line "${unit}" "StateDirectoryMode=0700" "StateDirectory mode contract failed"
        require_exact_line "${unit}" "Environment=AO_REBIRTH_SESSION_OWNERSHIP_DIR=${EXPECTED_OWNERSHIP_DIR}" "ownership directory contract failed"
        require_exact_line "${unit}" "ExecStartPre=/usr/bin/install -d -m 0700 ${EXPECTED_OWNERSHIP_DIR}" "ownership directory creation contract failed"
    done
    [[ "$(ownership_value "${LOGIN_UNIT_SOURCE}")" == "$(ownership_value "${ZONE_UNIT_SOURCE}")" ]] || fail "ownership directories differ"
    [[ "$(ownership_value "${LOGIN_UNIT_SOURCE}")" == "${EXPECTED_OWNERSHIP_DIR}" ]] || fail "ownership directory is not the governed path"
    [[ "$(ownership_value "${LOGIN_UNIT_SOURCE}")" != /tmp* ]] || fail "ownership directory must not be under /tmp"
    require_exact_line "${LOGIN_UNIT_SOURCE}" "Environment=AO_REBIRTH_BIND_MODE=Public" "LoginEngine Public bind contract failed"
    require_exact_line "${LOGIN_UNIT_SOURCE}" "ExecStart=/opt/ao-rebirth/loginengine/current/LoginEngine --headless" "LoginEngine executable contract failed"
    require_exact_line "${ZONE_UNIT_SOURCE}" "ExecStartPre=/opt/ao-rebirth/zoneengine/current/ZoneEngine --recover-stale-online --recovery-lock-file /run/ao-rebirth-zoneengine/stale-online-recovery.lock" "ZoneEngine stale-online recovery contract failed"
    require_exact_line "${ZONE_UNIT_SOURCE}" "ExecStartPre=/opt/ao-rebirth/zoneengine/current/ZoneEngine --validate-database" "ZoneEngine database validation contract failed"
    require_exact_line "${ZONE_UNIT_SOURCE}" "ExecStart=/opt/ao-rebirth/zoneengine/current/ZoneEngine --validate-lifecycle --shutdown-file /run/ao-rebirth-zoneengine/shutdown" "ZoneEngine executable contract failed"
    local ownership_line recovery_line database_line start_line
    ownership_line="$(line_number "${ZONE_UNIT_SOURCE}" "ExecStartPre=/usr/bin/install -d -m 0700 ${EXPECTED_OWNERSHIP_DIR}")"
    recovery_line="$(line_number "${ZONE_UNIT_SOURCE}" "ExecStartPre=/opt/ao-rebirth/zoneengine/current/ZoneEngine --recover-stale-online --recovery-lock-file /run/ao-rebirth-zoneengine/stale-online-recovery.lock")"
    database_line="$(line_number "${ZONE_UNIT_SOURCE}" "ExecStartPre=/opt/ao-rebirth/zoneengine/current/ZoneEngine --validate-database")"
    start_line="$(line_number "${ZONE_UNIT_SOURCE}" "ExecStart=/opt/ao-rebirth/zoneengine/current/ZoneEngine --validate-lifecycle --shutdown-file /run/ao-rebirth-zoneengine/shutdown")"
    (( ownership_line < recovery_line && database_line == recovery_line + 1 && database_line < start_line )) || fail "ZoneEngine ExecStartPre ordering contract failed"
}

service_active()
{
    if [[ "${test_mode}" == "1" ]]; then [[ "$(cat "${TEST_STATE}/$1.active")" == active ]]; else systemctl is-active --quiet "$2"; fi
}
service_restarts()
{
    if [[ "${test_mode}" == "1" ]]; then cat "${TEST_STATE}/$1.restarts"; else systemctl show "$2" -p NRestarts --value; fi
}
service_stop()
{
    if [[ "${test_mode}" == "1" ]]; then printf 'inactive\n' > "${TEST_STATE}/$1.active"; else systemctl stop "$2"; fi
}
service_start()
{
    local engine="$1"
    if [[ "${test_mode}" == "1" ]]; then
        [[ "${rolling_back}" == "true" || "${failure_step}" != "${engine}_start" ]] || return 1
        printf 'active\n' > "${TEST_STATE}/${engine}.active"
        local starts="$(cat "${TEST_STATE}/${engine}.starts")"
        printf '%s\n' "$((starts + 1))" > "${TEST_STATE}/${engine}.starts"
    else
        systemctl start "$2"
    fi
}
listener_present()
{
    local engine="$1" port="$2" service="$3"
    if [[ "${test_mode}" == "1" ]]; then
        [[ "${mutation_started}" != "true" || "${rolling_back}" == "true" || "${failure_step}" != listener ]] || return 1
        service_active "${engine}" "${service}" || return 1
        return 0
    fi
    local main_pid="$(systemctl show "${service}" -p MainPID --value)"
    [[ "${main_pid}" =~ ^[1-9][0-9]*$ ]] || return 1
    ss -H -ltnp "sport = :${port}" | grep -F "pid=${main_pid}," >/dev/null
}
online_count()
{
    if [[ "${test_mode}" == "1" ]]; then cat "${TEST_STATE}/online"; else docker exec -i "${DATABASE_CONTAINER}" sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" '"${EXPECTED_DATABASE}"' --batch --raw --skip-column-names -e "SELECT COUNT(*) FROM characters WHERE Online IS NOT NULL AND Online <> 0;"'; fi
}
daemon_reload() { [[ "${test_mode}" == "1" ]] || systemctl daemon-reload; }
verify_unit_static() { [[ "${test_mode}" == "1" ]] || systemd-analyze verify "${LOGIN_UNIT_SOURCE}" "${ZONE_UNIT_SOURCE}" >/dev/null; }

require_current_release()
{
    [[ -L "$1" ]] || fail "current release path is not a symlink: $1"
    local resolved="$(realpath -e -- "$1")"
    [[ "${resolved}" == "$2/"* ]] || fail "current release is outside immutable releases: $1"
    printf '%s' "${resolved}"
}

check_shared_directory()
{
    if [[ "${test_mode}" == "1" ]]; then
        local login_ownership zone_ownership fixture_record
        login_ownership="$(ownership_value "${LOGIN_UNIT_SOURCE}")"
        zone_ownership="$(ownership_value "${ZONE_UNIT_SOURCE}")"
        fixture_record="${TEST_STATE}/ownership-directory"
        [[ "${login_ownership}" == "${zone_ownership}" ]] || fail "fixture ownership directories differ"
        [[ "${login_ownership}" == "${EXPECTED_OWNERSHIP_DIR}" ]] || fail "fixture ownership directory is not the governed path"
        [[ "${login_ownership}" != /tmp* ]] || fail "fixture ownership directory must not be under /tmp"
        [[ "${OWNERSHIP_DIR}" == "$(root_path "${login_ownership}")" ]] || fail "fixture rooted ownership directory path mismatch"
        if [[ ! -e "${OWNERSHIP_DIR}" ]]; then
            [[ -d "$(root_path /var/lib)" && ! -L "$(root_path /var/lib)" ]] || fail "shared ownership parent is unavailable"
            echo "OWNERSHIP_DIR_PREDEPLOY=CREATABLE_BY_GOVERNED_TRANSACTION"
            return
        fi
        [[ -d "${OWNERSHIP_DIR}" && ! -L "${OWNERSHIP_DIR}" ]] || fail "shared ownership path is unsafe"
        if [[ -f "${fixture_record}" ]]; then
            [[ "$(cat "${fixture_record}")" == "${OWNERSHIP_DIR}" ]] || fail "fixture idempotent ownership directory diverged"
            echo "OWNERSHIP_DIR_FIXTURE_IDEMPOTENT=PASS"
        else
            printf '%s\n' "${OWNERSHIP_DIR}" > "${fixture_record}"
        fi
        echo "OWNERSHIP_DIR_PREDEPLOY=PASS"
        return
    fi
    if [[ ! -e "${OWNERSHIP_DIR}" ]]; then
        [[ -d "$(root_path /var/lib)" && ! -L "$(root_path /var/lib)" ]] || fail "shared ownership parent is unavailable"
        echo "OWNERSHIP_DIR_PREDEPLOY=CREATABLE_BY_GOVERNED_TRANSACTION"
        return
    fi
    [[ -d "${OWNERSHIP_DIR}" && ! -L "${OWNERSHIP_DIR}" ]] || fail "shared ownership path is unsafe"
    [[ "$(stat -c '%a' "${OWNERSHIP_DIR}")" == 700 ]] || fail "shared ownership directory mode must be 700"
    [[ "$(stat -c '%U:%G' "${OWNERSHIP_DIR}")" == "${EXPECTED_SERVICE_USER}:${EXPECTED_SERVICE_GROUP}" ]] || fail "shared ownership directory owner/group mismatch"
    runuser -u "${EXPECTED_SERVICE_USER}" -g "${EXPECTED_SERVICE_GROUP}" -- test -r "${OWNERSHIP_DIR}"
    runuser -u "${EXPECTED_SERVICE_USER}" -g "${EXPECTED_SERVICE_GROUP}" -- test -w "${OWNERSHIP_DIR}"
    runuser -u "${EXPECTED_SERVICE_USER}" -g "${EXPECTED_SERVICE_GROUP}" -- test -x "${OWNERSHIP_DIR}"
    echo "OWNERSHIP_DIR_PREDEPLOY=PASS"
}

require_rollback_material()
{
    PREVIOUS_LOGIN_RELEASE="$(require_current_release "${LOGIN_CURRENT}" "${LOGIN_RELEASES}")"
    PREVIOUS_ZONE_RELEASE="$(require_current_release "${ZONE_CURRENT}" "${ZONE_RELEASES}")"
    PREVIOUS_LOGIN_LINK_TARGET="$(readlink -- "${LOGIN_CURRENT}")"
    PREVIOUS_ZONE_LINK_TARGET="$(readlink -- "${ZONE_CURRENT}")"
    [[ -n "${PREVIOUS_LOGIN_LINK_TARGET}" ]] || fail "could not capture prior LoginEngine current symlink target"
    [[ -n "${PREVIOUS_ZONE_LINK_TARGET}" ]] || fail "could not capture prior ZoneEngine current symlink target"
    require_regular_file "${PREVIOUS_LOGIN_RELEASE}/LoginEngine"
    require_regular_file "${PREVIOUS_ZONE_RELEASE}/ZoneEngine"
    require_regular_file "${LOGIN_UNIT_TARGET}"
    require_regular_file "${ZONE_UNIT_TARGET}"
    require_regular_file "${LOGIN_ENV}"
    require_regular_file "${ZONE_ENV}"
    require_regular_file "${LOGIN_CONFIG}"
    PREVIOUS_LOGIN_ARTIFACT_SHA256="$(sha256sum "${PREVIOUS_LOGIN_RELEASE}/LoginEngine" | awk '{print $1}')"
    PREVIOUS_ZONE_ARTIFACT_SHA256="$(sha256sum "${PREVIOUS_ZONE_RELEASE}/ZoneEngine" | awk '{print $1}')"
    PREVIOUS_LOGIN_UNIT_SHA256="$(sha256sum "${LOGIN_UNIT_TARGET}" | awk '{print $1}')"
    PREVIOUS_ZONE_UNIT_SHA256="$(sha256sum "${ZONE_UNIT_TARGET}" | awk '{print $1}')"
    readonly PREVIOUS_LOGIN_RELEASE PREVIOUS_ZONE_RELEASE PREVIOUS_LOGIN_LINK_TARGET PREVIOUS_ZONE_LINK_TARGET
    readonly PREVIOUS_LOGIN_ARTIFACT_SHA256 PREVIOUS_ZONE_ARTIFACT_SHA256 PREVIOUS_LOGIN_UNIT_SHA256 PREVIOUS_ZONE_UNIT_SHA256
    [[ -z "${MANIFEST_PREVIOUS_LOGIN}" || "${MANIFEST_PREVIOUS_LOGIN}" == "${PREVIOUS_LOGIN_RELEASE}" ]] || fail "manifest previous LoginEngine release mismatch"
    [[ -z "${MANIFEST_PREVIOUS_ZONE}" || "${MANIFEST_PREVIOUS_ZONE}" == "${PREVIOUS_ZONE_RELEASE}" ]] || fail "manifest previous ZoneEngine release mismatch"
    echo "ROLLBACK_LOGINENGINE_RELEASE=${PREVIOUS_LOGIN_RELEASE}"
    echo "ROLLBACK_ZONEENGINE_RELEASE=${PREVIOUS_ZONE_RELEASE}"
    echo "ROLLBACK_READINESS=PASS"
}

preflight()
{
    validate_manifest_shape
    require_artifact "${LOGIN_ARTIFACT_DIR}" LoginEngine "${LOGIN_ARTIFACT_SHA}"
    require_artifact "${ZONE_ARTIFACT_DIR}" ZoneEngine "${ZONE_ARTIFACT_SHA}"
    validate_units
    verify_unit_static
    [[ "${test_mode}" == "1" ]] || id "${EXPECTED_SERVICE_USER}" >/dev/null 2>&1
    [[ "${test_mode}" == "1" ]] || [[ "$(id -gn "${EXPECTED_SERVICE_USER}")" == "${EXPECTED_SERVICE_GROUP}" ]]
    service_active login "${LOGIN_SERVICE}" || fail "LoginEngine is not active before deployment"
    service_active zone "${ZONE_SERVICE}" || fail "ZoneEngine is not active before deployment"
    LOGIN_RESTARTS_BEFORE="$(service_restarts login "${LOGIN_SERVICE}")"
    ZONE_RESTARTS_BEFORE="$(service_restarts zone "${ZONE_SERVICE}")"
    readonly LOGIN_RESTARTS_BEFORE ZONE_RESTARTS_BEFORE
    listener_present login 7500 "${LOGIN_SERVICE}" || fail "port 7500 predeploy listener is missing"
    listener_present zone 7501 "${ZONE_SERVICE}" || fail "port 7501 predeploy listener is missing"
    require_rollback_material
    check_shared_directory
    ONLINE_BEFORE="$(online_count)"
    readonly ONLINE_BEFORE
    [[ "${ONLINE_BEFORE}" =~ ^[0-9]+$ ]] || fail "could not determine online character count"
    echo "LOGINENGINE_PREDEPLOY_STATUS=active"
    echo "ZONEENGINE_PREDEPLOY_STATUS=active"
    echo "LOGINENGINE_PREDEPLOY_RESTARTS=${LOGIN_RESTARTS_BEFORE}"
    echo "ZONEENGINE_PREDEPLOY_RESTARTS=${ZONE_RESTARTS_BEFORE}"
    echo "ONLINE_NONZERO_ROWS_PREDEPLOY=${ONLINE_BEFORE}"
    echo "PREDEPLOY_HEALTH_CHECK=PASS"
}

current_release_matches()
{
    [[ -f "${LOGIN_CURRENT}/SOURCE_SHA" && -f "${ZONE_CURRENT}/SOURCE_SHA" ]] || return 1
    [[ "$(tr -d '\r\n\t ' < "${LOGIN_CURRENT}/SOURCE_SHA")" == "${SOURCE_SHA}" ]] || return 1
    [[ "$(tr -d '\r\n\t ' < "${ZONE_CURRENT}/SOURCE_SHA")" == "${SOURCE_SHA}" ]] || return 1
    [[ "$(sha256sum "${LOGIN_CURRENT}/LoginEngine" | awk '{print $1}')" == "${LOGIN_ARTIFACT_SHA}" ]] || return 1
    [[ "$(sha256sum "${ZONE_CURRENT}/ZoneEngine" | awk '{print $1}')" == "${ZONE_ARTIFACT_SHA}" ]] || return 1
    [[ "$(sha256sum "${LOGIN_UNIT_TARGET}" | awk '{print $1}')" == "${LOGIN_UNIT_SHA}" ]] || return 1
    [[ "$(sha256sum "${ZONE_UNIT_TARGET}" | awk '{print $1}')" == "${ZONE_UNIT_SHA}" ]] || return 1
    [[ -d "${OWNERSHIP_DIR}" ]] || return 1
    [[ "${test_mode}" == 1 || "$(stat -c '%a' "${OWNERSHIP_DIR}")" == 700 ]] || return 1
}

create_snapshot()
{
    snapshot_dir="${SNAPSHOT_ROOT}/${RELEASE_NAME}-$(date -u +%Y%m%dT%H%M%SZ)-$$"
    if [[ "${test_mode}" == "1" ]]; then mkdir -p -- "${snapshot_dir}"; else install -d -m 0700 "${snapshot_dir}"; fi
    cp -p -- "${LOGIN_UNIT_TARGET}" "${snapshot_dir}/loginengine.service"
    cp -p -- "${ZONE_UNIT_TARGET}" "${snapshot_dir}/zoneengine.service"
    cp -p -- "${LOGIN_ENV}" "${snapshot_dir}/loginengine.env"
    cp -p -- "${ZONE_ENV}" "${snapshot_dir}/zoneengine.env"
    cp -p -- "${LOGIN_CONFIG}" "${snapshot_dir}/loginengine.Config.xml"
    [[ ! -f "${ZONE_CONFIG}" ]] || cp -p -- "${ZONE_CONFIG}" "${snapshot_dir}/zoneengine.Config.xml"
    cat > "${snapshot_dir}/rollback.env" <<EOF
SOURCE_SHA=${SOURCE_SHA}
PREVIOUS_LOGINENGINE_RELEASE=${PREVIOUS_LOGIN_RELEASE}
PREVIOUS_ZONEENGINE_RELEASE=${PREVIOUS_ZONE_RELEASE}
PREVIOUS_LOGINENGINE_LINK_TARGET=${PREVIOUS_LOGIN_LINK_TARGET}
PREVIOUS_ZONEENGINE_LINK_TARGET=${PREVIOUS_ZONE_LINK_TARGET}
PREVIOUS_LOGINENGINE_ARTIFACT_SHA256=${PREVIOUS_LOGIN_ARTIFACT_SHA256}
PREVIOUS_ZONEENGINE_ARTIFACT_SHA256=${PREVIOUS_ZONE_ARTIFACT_SHA256}
PREVIOUS_LOGINENGINE_UNIT_SHA256=${PREVIOUS_LOGIN_UNIT_SHA256}
PREVIOUS_ZONEENGINE_UNIT_SHA256=${PREVIOUS_ZONE_UNIT_SHA256}
LOGINENGINE_WAS_ACTIVE=YES
ZONEENGINE_WAS_ACTIVE=YES
EOF
    [[ "${test_mode}" == "1" ]] || chmod 0600 "${snapshot_dir}"/*
    echo "ROLLBACK_SNAPSHOT_PATH=${snapshot_dir}"
    echo "ROLLBACK_SNAPSHOT=PASS"
}

inject_failure() { [[ "${test_mode}" != 1 || "${rolling_back}" == true || "${failure_step}" != "$1" ]] || fail "injected failure at $1"; }

install_release()
{
    local artifact_dir="$1" apphost="$2" expected_hash="$3" target="$4" releases="$5"
    if [[ -e "${target}" ]]; then
        [[ -d "${target}" && ! -L "${target}" ]] || fail "existing release target is unsafe"
        [[ "$(sha256sum "${target}/${apphost}" | awk '{print $1}')" == "${expected_hash}" ]] || fail "existing immutable release differs"
        [[ "$(tr -d '\r\n\t ' < "${target}/SOURCE_SHA")" == "${SOURCE_SHA}" ]] || fail "existing immutable release source differs"
        return
    fi
    local staging="${releases}/.${RELEASE_NAME}.staging.$$"
    [[ ! -e "${staging}" ]] || fail "release staging path already exists"
    if [[ "${test_mode}" == 1 ]]; then
        mkdir -p -- "${staging}"
        cp -R -- "${artifact_dir}/." "${staging}/"
    else
        install -d -m 0750 "${staging}"
        cp -a -- "${artifact_dir}/." "${staging}/"
        chown -R root:"${EXPECTED_SERVICE_GROUP}" "${staging}"
        find "${staging}" -type d -exec chmod 0750 {} +
        find "${staging}" -type f -exec chmod 0640 {} +
        chmod 0750 "${staging}/${apphost}"
        [[ ! -f "${staging}/createdump" ]] || chmod 0750 "${staging}/createdump"
    fi
    [[ "$(sha256sum "${staging}/${apphost}" | awk '{print $1}')" == "${expected_hash}" ]] || fail "staged ${apphost} hash mismatch"
    mv -T -- "${staging}" "${target}"
}

switch_link()
{
    local swap="$1.production-release.$$"
    ln -sT -- "$2" "${swap}"
    mv -fT -- "${swap}" "$1"
}

install_units()
{
    local login_swap="${LOGIN_UNIT_TARGET}.production-release.$$" zone_swap="${ZONE_UNIT_TARGET}.production-release.$$"
    install -m 0644 "${LOGIN_UNIT_SOURCE}" "${login_swap}"
    install -m 0644 "${ZONE_UNIT_SOURCE}" "${zone_swap}"
    mv -fT -- "${login_swap}" "${LOGIN_UNIT_TARGET}"
    mv -fT -- "${zone_swap}" "${ZONE_UNIT_TARGET}"
    [[ "$(sha256sum "${LOGIN_UNIT_TARGET}" | awk '{print $1}')" == "${LOGIN_UNIT_SHA}" ]] || fail "installed LoginEngine unit hash mismatch"
    [[ "$(sha256sum "${ZONE_UNIT_TARGET}" | awk '{print $1}')" == "${ZONE_UNIT_SHA}" ]] || fail "installed ZoneEngine unit hash mismatch"
}

create_shared_directory()
{
    if [[ "${test_mode}" == 1 ]]; then
        mkdir -p -- "${OWNERSHIP_DIR}"
    else
        install -d -o "${EXPECTED_SERVICE_USER}" -g "${EXPECTED_SERVICE_GROUP}" -m 0700 "$(root_path /var/lib/ao-rebirth)"
        install -d -o "${EXPECTED_SERVICE_USER}" -g "${EXPECTED_SERVICE_GROUP}" -m 0700 "${OWNERSHIP_DIR}"
    fi
    check_shared_directory
    [[ "${test_mode}" != 1 ]] || echo "OWNERSHIP_DIR_FIXTURE_SHARED_PATH=PASS"
}

write_deployed_manifest()
{
    local temporary="${DEPLOYED_MANIFEST}.tmp.$$"
    cat > "${temporary}" <<EOF
SOURCE_SHA=${SOURCE_SHA}
LOGINENGINE_RELEASE=${LOGIN_RELEASE_TARGET}
ZONEENGINE_RELEASE=${ZONE_RELEASE_TARGET}
LOGINENGINE_ARTIFACT_SHA256=${LOGIN_ARTIFACT_SHA}
ZONEENGINE_ARTIFACT_SHA256=${ZONE_ARTIFACT_SHA}
LOGINENGINE_UNIT_SHA256=${LOGIN_UNIT_SHA}
ZONEENGINE_UNIT_SHA256=${ZONE_UNIT_SHA}
DEPLOYED_AT_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)
EOF
    chmod 0600 "${temporary}"
    mv -f -- "${temporary}" "${DEPLOYED_MANIFEST}"
}

post_health()
{
    service_active login "${LOGIN_SERVICE}" && service_active zone "${ZONE_SERVICE}" \
        && listener_present login 7500 "${LOGIN_SERVICE}" && listener_present zone 7501 "${ZONE_SERVICE}" \
        && [[ "$(service_restarts login "${LOGIN_SERVICE}")" == "${LOGIN_RESTARTS_BEFORE}" ]] \
        && [[ "$(service_restarts zone "${ZONE_SERVICE}")" == "${ZONE_RESTARTS_BEFORE}" ]] \
        && [[ "$(online_count)" == 0 ]] && current_release_matches
}

verify_rollback_state()
{
    [[ -L "${LOGIN_CURRENT}" && -L "${ZONE_CURRENT}" ]] || return 1
    [[ "$(readlink -- "${LOGIN_CURRENT}")" == "${PREVIOUS_LOGIN_LINK_TARGET}" ]] || return 1
    [[ "$(readlink -- "${ZONE_CURRENT}")" == "${PREVIOUS_ZONE_LINK_TARGET}" ]] || return 1
    [[ -d "${PREVIOUS_LOGIN_RELEASE}" && -d "${PREVIOUS_ZONE_RELEASE}" ]] || return 1
    [[ "$(realpath -e -- "${LOGIN_CURRENT}")" == "${PREVIOUS_LOGIN_RELEASE}" ]] || return 1
    [[ "$(realpath -e -- "${ZONE_CURRENT}")" == "${PREVIOUS_ZONE_RELEASE}" ]] || return 1
    [[ "$(sha256sum "${LOGIN_CURRENT}/LoginEngine" | awk '{print $1}')" == "${PREVIOUS_LOGIN_ARTIFACT_SHA256}" ]] || return 1
    [[ "$(sha256sum "${ZONE_CURRENT}/ZoneEngine" | awk '{print $1}')" == "${PREVIOUS_ZONE_ARTIFACT_SHA256}" ]] || return 1
    [[ "$(sha256sum "${LOGIN_UNIT_TARGET}" | awk '{print $1}')" == "${PREVIOUS_LOGIN_UNIT_SHA256}" ]] || return 1
    [[ "$(sha256sum "${ZONE_UNIT_TARGET}" | awk '{print $1}')" == "${PREVIOUS_ZONE_UNIT_SHA256}" ]] || return 1
    echo "ROLLBACK_EXACT_PRIOR_TARGETS=PASS"
    echo "ROLLBACK_PRIOR_ARTIFACTS_AND_UNITS=PASS"
    echo "ROLLBACK_NO_MIXED_STATE=PASS"
}

rollback_operation()
{
    local operation="$1"
    shift
    if "$@"; then
        echo "ROLLBACK_STEP_${operation}=PASS"
        return 0
    fi
    echo "ROLLBACK_STEP_${operation}=FAIL" >&2
    if [[ -z "${rollback_first_failure}" ]]; then
        rollback_first_failure="${operation}"
        echo "ROLLBACK_FIRST_FAILURE=${rollback_first_failure}" >&2
    fi
    return 1
}

rollback()
{
    rolling_back=true
    local failed=false
    rollback_operation STOP_LOGIN service_stop login "${LOGIN_SERVICE}" || failed=true
    rollback_operation STOP_ZONE service_stop zone "${ZONE_SERVICE}" || failed=true
    rollback_operation RESTORE_LOGIN_CURRENT switch_link "${LOGIN_CURRENT}" "${PREVIOUS_LOGIN_LINK_TARGET}" || failed=true
    rollback_operation RESTORE_ZONE_CURRENT switch_link "${ZONE_CURRENT}" "${PREVIOUS_ZONE_LINK_TARGET}" || failed=true
    rollback_operation RESTORE_LOGIN_UNIT install -m 0644 "${snapshot_dir}/loginengine.service" "${LOGIN_UNIT_TARGET}" || failed=true
    rollback_operation RESTORE_ZONE_UNIT install -m 0644 "${snapshot_dir}/zoneengine.service" "${ZONE_UNIT_TARGET}" || failed=true
    rollback_operation DAEMON_RELOAD daemon_reload || failed=true
    rollback_operation VERIFY_EXACT_PRIOR_STATE verify_rollback_state || failed=true
    rollback_operation START_LOGIN service_start login "${LOGIN_SERVICE}" || failed=true
    rollback_operation START_ZONE service_start zone "${ZONE_SERVICE}" || failed=true
    rollback_operation LOGIN_ACTIVE service_active login "${LOGIN_SERVICE}" || failed=true
    rollback_operation ZONE_ACTIVE service_active zone "${ZONE_SERVICE}" || failed=true
    rollback_operation LOGIN_LISTENER listener_present login 7500 "${LOGIN_SERVICE}" || failed=true
    rollback_operation ZONE_LISTENER listener_present zone 7501 "${ZONE_SERVICE}" || failed=true
    [[ "${failed}" == false ]] || { echo "ROLLBACK_BOTH_SERVICES=FAIL" >&2; return 1; }
    echo "ROLLBACK_BOTH_SERVICES=PASS"
}

on_exit()
{
    local status=$?
    trap - EXIT
    if [[ "${mutation_started}" == true && "${deployment_committed}" != true ]]; then rollback || status=1; fi
    exit "${status}"
}

main()
{
    preflight
    if current_release_matches; then
        echo "ALREADY_DEPLOYED=YES"
        echo "IDEMPOTENT_REDEPLOY=PASS"
        echo "DEPLOYED_SHA=${SOURCE_SHA}"
        return
    fi
    [[ "${ONLINE_BEFORE}" == 0 ]] || fail "online characters present; deployment policy is fail closed"
    echo "ONLINE_PLAYER_DEPLOYMENT_POLICY=REQUIRE_ZERO_NONZERO_ONLINE_ROWS"
    if [[ "${dry_run}" == true ]]; then echo "DRY_RUN=PASS"; echo "PRODUCTION_MUTATION=NO"; return; fi

    create_snapshot
    mutation_started=true
    trap on_exit EXIT
    service_stop login "${LOGIN_SERVICE}"
    service_stop zone "${ZONE_SERVICE}"
    inject_failure artifact_install
    install_release "${LOGIN_ARTIFACT_DIR}" LoginEngine "${LOGIN_ARTIFACT_SHA}" "${LOGIN_RELEASE_TARGET}" "${LOGIN_RELEASES}"
    install_release "${ZONE_ARTIFACT_DIR}" ZoneEngine "${ZONE_ARTIFACT_SHA}" "${ZONE_RELEASE_TARGET}" "${ZONE_RELEASES}"
    inject_failure unit_install
    install_units
    switch_link "${LOGIN_CURRENT}" "${LOGIN_RELEASE_TARGET}"
    switch_link "${ZONE_CURRENT}" "${ZONE_RELEASE_TARGET}"
    create_shared_directory
    daemon_reload
    verify_unit_static
    service_start login "${LOGIN_SERVICE}" || fail "LoginEngine failed startup"
    service_active login "${LOGIN_SERVICE}" || fail "LoginEngine is not active after startup"
    listener_present login 7500 "${LOGIN_SERVICE}" || fail "LoginEngine listener is missing after startup"
    service_start zone "${ZONE_SERVICE}" || fail "ZoneEngine failed startup"
    service_active zone "${ZONE_SERVICE}" || fail "ZoneEngine is not active after startup"
    listener_present zone 7501 "${ZONE_SERVICE}" || fail "ZoneEngine listener is missing after startup"
    post_health || fail "post-deployment health check failed"
    write_deployed_manifest
    deployment_committed=true
    trap - EXIT
    echo "TRANSACTIONAL_DEPLOYMENT=PASS"
    echo "DEPLOYED_SHA=${SOURCE_SHA}"
    echo "ROLLBACK_REQUIRED=NO"
}

main
