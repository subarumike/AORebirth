#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C

readonly EXPECTED_OWNERSHIP_DIR="/var/lib/ao-rebirth/session-ownership"
readonly EXPECTED_SERVICE_USER="aorebirth"
readonly EXPECTED_SERVICE_GROUP="aorebirth"
readonly EXPECTED_DATABASE="aorebirth_chatengine_stage6"
readonly DATABASE_CONTAINER="aorebirth-chatengine-mysql-stage6"
readonly READINESS_TIMEOUT_SECONDS=30
readonly READINESS_POLL_INTERVAL_SECONDS=1
readonly POST_START_STABILITY_SECONDS=10
readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/../../placement-provenance.sh"

manifest_path=""
expected_sha=""
dry_run=false
recover_zone_outage=false
resume_stopped_recovery=false
deploy_root="${AO_REBIRTH_DEPLOY_TEST_ROOT:-}"
test_mode="${AO_REBIRTH_DEPLOY_TEST_MODE:-0}"
failure_step="${AO_REBIRTH_DEPLOY_TEST_FAIL_STEP:-}"
mutation_started=false
deployment_committed=false
rolling_back=false
snapshot_dir=""
rollback_first_failure=""
ZONE_RESTARTS_START_BASELINE=""
ZONE_NOTIFY_DROPIN_WAS_PRESENT=NO
ZONE_EFFECTIVE_TYPE_BEFORE=""
ZONE_EFFECTIVE_NOTIFY_ACCESS_BEFORE=""
ZONE_EFFECTIVE_DROPIN_PATHS_BEFORE=""

fail() { echo "FAIL: $*" >&2; exit 1; }
usage() { echo "usage: upgrade-active-services.sh --manifest <release.manifest> --expected-sha <sha> [--dry-run] [--recover-zone-outage [--resume-stopped-recovery]]" >&2; }
root_path() { printf '%s%s' "${deploy_root}" "$1"; }

while [[ "$#" -gt 0 ]]; do
    case "$1" in
        --manifest) manifest_path="${2:-}"; shift 2 ;;
        --expected-sha) expected_sha="${2:-}"; shift 2 ;;
        --dry-run) dry_run=true; shift ;;
        --recover-zone-outage) recover_zone_outage=true; shift ;;
        --resume-stopped-recovery) resume_stopped_recovery=true; shift ;;
        --help) usage; exit 0 ;;
        *) usage; exit 2 ;;
    esac
done

[[ "${resume_stopped_recovery}" != true || "${recover_zone_outage}" == true ]] \
    || fail "--resume-stopped-recovery requires --recover-zone-outage"

recovery_requested()
{
    [[ "${recover_zone_outage}" == true ]]
}

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
readonly MANIFEST_PLACEMENT_CORPUS_VERSION="$(manifest_value PLACEMENT_CORPUS_VERSION)"
readonly MANIFEST_PLACEMENT_CORPUS_MANIFEST_SHA="$(manifest_value PLACEMENT_CORPUS_MANIFEST_SHA256)"
readonly MANIFEST_PLACEMENT_CORPUS_SUMMARY_SHA="$(manifest_value PLACEMENT_CORPUS_SUMMARY_SHA256)"
readonly MANIFEST_PLACEMENT_CORPUS_INDEX_SHA="$(manifest_value PLACEMENT_CORPUS_INDEX_SHA256)"
readonly MANIFEST_PLACEMENT_ACGHASH_INVENTORY_SHA="$(manifest_value PLACEMENT_ACGHASH_INVENTORY_SHA256)"
readonly MANIFEST_PLACEMENT_BUILD_MANIFEST_SHA="$(manifest_value PLACEMENT_BUILD_MANIFEST_SHA256)"
readonly MANIFEST_PLACEMENT_RESOURCE_COUNT="$(manifest_value PLACEMENT_RESOURCE_COUNT)"
readonly MANIFEST_PLACEMENT_PARSED_RESOURCE_COUNT="$(manifest_value PLACEMENT_PARSED_RESOURCE_COUNT)"
readonly MANIFEST_PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT="$(manifest_value PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT)"
readonly MANIFEST_PLACEMENT_DISTRICT_COUNT="$(manifest_value PLACEMENT_DISTRICT_COUNT)"
readonly MANIFEST_PLACEMENT_RECORD_COUNT="$(manifest_value PLACEMENT_RECORD_COUNT)"
readonly MANIFEST_PLACEMENT_UNIQUE_ACGHASH_COUNT="$(manifest_value PLACEMENT_UNIQUE_ACGHASH_COUNT)"
readonly MANIFEST_PLACEMENT_RUNTIME_AUTHORIZED_COUNT="$(manifest_value PLACEMENT_RUNTIME_AUTHORIZED_COUNT)"
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
readonly ZONE_DROPIN_DIR="$(root_path /etc/systemd/system/ao-rebirth-zoneengine.service.d)"
readonly ZONE_STALE_NOTIFY_DROPIN="${ZONE_DROPIN_DIR}/10-type-simple.conf"
readonly ZONE_STALE_NOTIFY_DROPIN_SHA256="2d1ebd0ffd7534c6357830891a35d2343428b56c8093b05223abe7635f67b55f"
readonly ZONE_DAILY_LOGIN_DROPIN="${ZONE_DROPIN_DIR}/20-daily-login.conf"
readonly ZONE_DAILY_LOGIN_DROPIN_SHA256="4ea8e3ba780f564a17ba454fa46121a6618985da3ef449d792016a41f8ac0e29"
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
    [[ "${FORMAT}" == "2" ]] || fail "unsupported release manifest format"
    [[ "${SOURCE_SHA}" == "${expected_sha}" ]] || fail "manifest source SHA mismatch"
    [[ "${SOURCE_SHA}" =~ ^[0-9a-f]{40}$ ]] || fail "manifest source SHA is invalid"
    [[ "${LOGIN_SERVICE}" == "ao-rebirth-loginengine.service" ]] || fail "unexpected LoginEngine service"
    [[ "${ZONE_SERVICE}" == "ao-rebirth-zoneengine.service" ]] || fail "unexpected ZoneEngine service"
    local unknown
    unknown="$(cut -d= -f1 "${manifest_path}" | grep -Ev '^(FORMAT|SOURCE_SHA|BUILD_TIMESTAMP_UTC|LOGINENGINE_ARTIFACT_DIR|LOGINENGINE_ARTIFACT_SHA256|ZONEENGINE_ARTIFACT_DIR|ZONEENGINE_ARTIFACT_SHA256|PLACEMENT_CORPUS_VERSION|PLACEMENT_CORPUS_MANIFEST_SHA256|PLACEMENT_CORPUS_SUMMARY_SHA256|PLACEMENT_CORPUS_INDEX_SHA256|PLACEMENT_ACGHASH_INVENTORY_SHA256|PLACEMENT_BUILD_MANIFEST_SHA256|PLACEMENT_RESOURCE_COUNT|PLACEMENT_PARSED_RESOURCE_COUNT|PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT|PLACEMENT_DISTRICT_COUNT|PLACEMENT_RECORD_COUNT|PLACEMENT_UNIQUE_ACGHASH_COUNT|PLACEMENT_RUNTIME_AUTHORIZED_COUNT|LOGINENGINE_UNIT_PATH|LOGINENGINE_UNIT_SHA256|ZONEENGINE_UNIT_PATH|ZONEENGINE_UNIT_SHA256|LOGINENGINE_SERVICE|ZONEENGINE_SERVICE|PREVIOUS_LOGINENGINE_RELEASE|PREVIOUS_ZONEENGINE_RELEASE)$' | head -n 1 || true)"
    [[ -z "${unknown}" ]] || fail "unknown manifest key ${unknown}"
}

require_zone_placement_artifact()
{
    local artifact_dir="$1"
    placement_provenance_load \
        "${artifact_dir}" \
        "${SOURCE_SHA}" \
        linux \
        "${MANIFEST_PLACEMENT_BUILD_MANIFEST_SHA}" \
        || fail "ZoneEngine official placement provenance is invalid"
    placement_require_build_provenance "${artifact_dir}/BUILD_PROVENANCE.env" \
        || fail "ZoneEngine build provenance lacks official placement evidence"
    [[ "${PLACEMENT_CORPUS_VERSION}" == "${MANIFEST_PLACEMENT_CORPUS_VERSION}" ]] \
        || fail "placement corpus version does not match release manifest"
    [[ "${PLACEMENT_CORPUS_MANIFEST_SHA256}" == "${MANIFEST_PLACEMENT_CORPUS_MANIFEST_SHA}" ]] \
        || fail "placement corpus manifest digest does not match release manifest"
    [[ "${PLACEMENT_CORPUS_SUMMARY_SHA256}" == "${MANIFEST_PLACEMENT_CORPUS_SUMMARY_SHA}" ]] \
        || fail "placement summary digest does not match release manifest"
    [[ "${PLACEMENT_CORPUS_INDEX_SHA256}" == "${MANIFEST_PLACEMENT_CORPUS_INDEX_SHA}" ]] \
        || fail "placement index digest does not match release manifest"
    [[ "${PLACEMENT_ACGHASH_INVENTORY_SHA256}" == "${MANIFEST_PLACEMENT_ACGHASH_INVENTORY_SHA}" ]] \
        || fail "placement ACGHash inventory digest does not match release manifest"
    [[ "${PLACEMENT_RESOURCE_COUNT}" == "${MANIFEST_PLACEMENT_RESOURCE_COUNT}" \
        && "${PLACEMENT_PARSED_RESOURCE_COUNT}" == "${MANIFEST_PLACEMENT_PARSED_RESOURCE_COUNT}" \
        && "${PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT}" == "${MANIFEST_PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT}" \
        && "${PLACEMENT_DISTRICT_COUNT}" == "${MANIFEST_PLACEMENT_DISTRICT_COUNT}" \
        && "${PLACEMENT_RECORD_COUNT}" == "${MANIFEST_PLACEMENT_RECORD_COUNT}" \
        && "${PLACEMENT_UNIQUE_ACGHASH_COUNT}" == "${MANIFEST_PLACEMENT_UNIQUE_ACGHASH_COUNT}" \
        && "${PLACEMENT_RUNTIME_AUTHORIZED_COUNT}" == "${MANIFEST_PLACEMENT_RUNTIME_AUTHORIZED_COUNT}" ]] \
        || fail "placement global counts do not match release manifest"
    grep -Fx "PLACEMENT_VALIDATION=PASS" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "ZoneEngine placement acceptance did not pass"
    grep -Fx "EXPECTED_PLACEMENT_BUILD_MANIFEST_SHA256=${MANIFEST_PLACEMENT_BUILD_MANIFEST_SHA}" \
        "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "ZoneEngine accepted placement manifest digest does not match"
    grep -Fx "PLACEMENT_BUILD_MANIFEST_SHA256=${MANIFEST_PLACEMENT_BUILD_MANIFEST_SHA}" \
        "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "ZoneEngine placement manifest acceptance provenance is missing"
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
    require_exact_line "${ZONE_UNIT_SOURCE}" "ExecStart=/opt/ao-rebirth/zoneengine/current/ZoneEngine --headless --shutdown-file /run/ao-rebirth-zoneengine/shutdown" "ZoneEngine production executable contract failed"
    [[ "$(grep -Fxc -- "ExecStart=/opt/ao-rebirth/zoneengine/current/ZoneEngine --validate-lifecycle --shutdown-file /run/ao-rebirth-zoneengine/shutdown" "${ZONE_UNIT_SOURCE}" || true)" == "0" ]] || fail "ZoneEngine validation lifecycle cannot be production ExecStart"
    local ownership_line recovery_line database_line start_line
    ownership_line="$(line_number "${ZONE_UNIT_SOURCE}" "ExecStartPre=/usr/bin/install -d -m 0700 ${EXPECTED_OWNERSHIP_DIR}")"
    recovery_line="$(line_number "${ZONE_UNIT_SOURCE}" "ExecStartPre=/opt/ao-rebirth/zoneengine/current/ZoneEngine --recover-stale-online --recovery-lock-file /run/ao-rebirth-zoneengine/stale-online-recovery.lock")"
    database_line="$(line_number "${ZONE_UNIT_SOURCE}" "ExecStartPre=/opt/ao-rebirth/zoneengine/current/ZoneEngine --validate-database")"
    start_line="$(line_number "${ZONE_UNIT_SOURCE}" "ExecStart=/opt/ao-rebirth/zoneengine/current/ZoneEngine --headless --shutdown-file /run/ao-rebirth-zoneengine/shutdown")"
    (( ownership_line < recovery_line && database_line == recovery_line + 1 && database_line < start_line )) || fail "ZoneEngine ExecStartPre ordering contract failed"
}

service_active()
{
    if [[ "${test_mode}" == "1" ]]; then [[ "$(cat "${TEST_STATE}/$1.active")" == active ]]; else systemctl is-active --quiet "$2"; fi
}
service_stopped()
{
    local engine="$1" service="$2" state substate main_pid
    if [[ "${test_mode}" == "1" ]]; then
        state="$(cat "${TEST_STATE}/${engine}.active")"
        [[ "${state}" == inactive || "${state}" == failed ]]
        return
    fi
    state="$(systemctl show "${service}" -p ActiveState --value)"
    substate="$(systemctl show "${service}" -p SubState --value)"
    main_pid="$(systemctl show "${service}" -p MainPID --value)"
    [[ "${main_pid}" == 0 ]] || return 1
    [[ "${state}/${substate}" == inactive/dead || "${state}/${substate}" == failed/failed ]]
}
listener_absent()
{
    local engine="$1" port="$2" service="$3" listener_output
    if [[ "${test_mode}" == "1" ]]; then
        [[ "$(cat "${TEST_STATE}/${engine}.port-inspection")" == PASS ]] \
            || fail "could not inspect port ${port}"
        [[ "$(cat "${TEST_STATE}/${engine}.port-occupied")" == NO ]]
    else
        listener_output="$(ss -H -ltn "sport = :${port}")" \
            || fail "could not inspect port ${port}"
        [[ -z "${listener_output}" ]]
    fi
}

zone_effective_property()
{
    local property="$1"
    if [[ "${test_mode}" == "1" ]]; then
        case "${property}" in
            Type) cat "${TEST_STATE}/zone.effective-type" ;;
            NotifyAccess) cat "${TEST_STATE}/zone.notify-access" ;;
            DropInPaths) cat "${TEST_STATE}/zone.dropin-paths" ;;
            *) return 1 ;;
        esac
        return
    fi
    systemctl show "${ZONE_SERVICE}" -p "${property}" --value
}

zone_daily_login_dropin_governed()
{
    [[ -f "${ZONE_DAILY_LOGIN_DROPIN}" && ! -L "${ZONE_DAILY_LOGIN_DROPIN}" ]] \
        && [[ "$(sha256sum "${ZONE_DAILY_LOGIN_DROPIN}" | awk '{print $1}')" == "${ZONE_DAILY_LOGIN_DROPIN_SHA256}" ]]
}

zone_effective_notify_contract()
{
    zone_daily_login_dropin_governed \
        && [[ "$(zone_effective_property Type)" == notify ]] \
        && [[ "$(zone_effective_property NotifyAccess)" == main ]] \
        && [[ "$(zone_effective_property DropInPaths)" == "${ZONE_DAILY_LOGIN_DROPIN}" ]]
}

validate_zone_dropin_preflight()
{
    local entry
    if [[ -e "${ZONE_DROPIN_DIR}" || -L "${ZONE_DROPIN_DIR}" ]]; then
        [[ -d "${ZONE_DROPIN_DIR}" && ! -L "${ZONE_DROPIN_DIR}" ]] \
            || fail "ZoneEngine drop-in directory is unsafe"
        shopt -s nullglob dotglob
        local entries=("${ZONE_DROPIN_DIR}"/*)
        shopt -u nullglob dotglob
        for entry in "${entries[@]}"; do
            [[ "${entry}" == "${ZONE_STALE_NOTIFY_DROPIN}" \
                || "${entry}" == "${ZONE_DAILY_LOGIN_DROPIN}" ]] \
                || fail "unmanaged ZoneEngine systemd drop-in is present: ${entry}"
            require_regular_file "${entry}"
        done
    fi

    zone_daily_login_dropin_governed \
        || fail "ZoneEngine daily-login drop-in content is not the governed production bridge"

    ZONE_EFFECTIVE_TYPE_BEFORE="$(zone_effective_property Type)"
    ZONE_EFFECTIVE_NOTIFY_ACCESS_BEFORE="$(zone_effective_property NotifyAccess)"
    ZONE_EFFECTIVE_DROPIN_PATHS_BEFORE="$(zone_effective_property DropInPaths)"
    if [[ -e "${ZONE_STALE_NOTIFY_DROPIN}" || -L "${ZONE_STALE_NOTIFY_DROPIN}" ]]; then
        require_regular_file "${ZONE_STALE_NOTIFY_DROPIN}"
        [[ "$(sha256sum "${ZONE_STALE_NOTIFY_DROPIN}" | awk '{print $1}')" == "${ZONE_STALE_NOTIFY_DROPIN_SHA256}" ]] \
            || fail "ZoneEngine stale readiness drop-in content is not the governed production override"
        [[ "${ZONE_EFFECTIVE_TYPE_BEFORE}" == simple \
            && "${ZONE_EFFECTIVE_NOTIFY_ACCESS_BEFORE}" == none \
            && "${ZONE_EFFECTIVE_DROPIN_PATHS_BEFORE}" == "${ZONE_STALE_NOTIFY_DROPIN} ${ZONE_DAILY_LOGIN_DROPIN}" ]] \
            || fail "ZoneEngine stale readiness drop-in does not match the effective unit state"
        ZONE_NOTIFY_DROPIN_WAS_PRESENT=YES
        echo "ZONEENGINE_STALE_READINESS_DROPIN=GOVERNED_REMOVAL_REQUIRED"
        return
    fi

    [[ "${ZONE_EFFECTIVE_TYPE_BEFORE}" == notify \
        && "${ZONE_EFFECTIVE_NOTIFY_ACCESS_BEFORE}" == main \
        && "${ZONE_EFFECTIVE_DROPIN_PATHS_BEFORE}" == "${ZONE_DAILY_LOGIN_DROPIN}" ]] \
        || fail "ZoneEngine effective readiness contract is unmanaged"
    ZONE_NOTIFY_DROPIN_WAS_PRESENT=NO
    echo "ZONEENGINE_STALE_READINESS_DROPIN=ABSENT"
}

remove_zone_stale_notify_dropin()
{
    if [[ "${ZONE_NOTIFY_DROPIN_WAS_PRESENT}" == YES ]]; then
        rm -f -- "${ZONE_STALE_NOTIFY_DROPIN}"
        [[ ! -e "${ZONE_STALE_NOTIFY_DROPIN}" && ! -L "${ZONE_STALE_NOTIFY_DROPIN}" ]] \
            || fail "ZoneEngine stale readiness drop-in removal failed"
        echo "ZONEENGINE_STALE_READINESS_DROPIN_REMOVED=PASS"
        return
    fi
    [[ ! -e "${ZONE_STALE_NOTIFY_DROPIN}" && ! -L "${ZONE_STALE_NOTIFY_DROPIN}" ]] \
        || fail "ZoneEngine readiness drop-in appeared during deployment"
}

restore_zone_notify_dropin()
{
    if [[ "${ZONE_NOTIFY_DROPIN_WAS_PRESENT}" == YES ]]; then
        [[ -f "${snapshot_dir}/zoneengine.10-type-simple.conf" \
            && ! -L "${snapshot_dir}/zoneengine.10-type-simple.conf" ]] || return 1
        [[ "$(sha256sum "${snapshot_dir}/zoneengine.10-type-simple.conf" | awk '{print $1}')" == "${ZONE_STALE_NOTIFY_DROPIN_SHA256}" ]] \
            || return 1
        [[ -d "${ZONE_DROPIN_DIR}" && ! -L "${ZONE_DROPIN_DIR}" ]] || return 1
        local swap="${ZONE_STALE_NOTIFY_DROPIN}.production-release.$$"
        install -m 0644 "${snapshot_dir}/zoneengine.10-type-simple.conf" "${swap}" || return 1
        mv -fT -- "${swap}" "${ZONE_STALE_NOTIFY_DROPIN}" || return 1
        return
    fi
    [[ ! -e "${ZONE_STALE_NOTIFY_DROPIN}" && ! -L "${ZONE_STALE_NOTIFY_DROPIN}" ]]
}

verify_zone_dropin_rollback()
{
    zone_daily_login_dropin_governed || return 1
    if [[ "${ZONE_NOTIFY_DROPIN_WAS_PRESENT}" == YES ]]; then
        [[ -f "${ZONE_STALE_NOTIFY_DROPIN}" && ! -L "${ZONE_STALE_NOTIFY_DROPIN}" ]] || return 1
        [[ "$(sha256sum "${ZONE_STALE_NOTIFY_DROPIN}" | awk '{print $1}')" == "${ZONE_STALE_NOTIFY_DROPIN_SHA256}" ]] \
            || return 1
    else
        [[ ! -e "${ZONE_STALE_NOTIFY_DROPIN}" && ! -L "${ZONE_STALE_NOTIFY_DROPIN}" ]] || return 1
    fi
    [[ "$(zone_effective_property Type)" == "${ZONE_EFFECTIVE_TYPE_BEFORE}" ]] || return 1
    [[ "$(zone_effective_property NotifyAccess)" == "${ZONE_EFFECTIVE_NOTIFY_ACCESS_BEFORE}" ]] || return 1
    [[ "$(zone_effective_property DropInPaths)" == "${ZONE_EFFECTIVE_DROPIN_PATHS_BEFORE}" ]] || return 1
    echo "ROLLBACK_ZONEENGINE_EFFECTIVE_UNIT=PASS type=${ZONE_EFFECTIVE_TYPE_BEFORE} notifyAccess=${ZONE_EFFECTIVE_NOTIFY_ACCESS_BEFORE}"
}

service_restarts()
{
    if [[ "${test_mode}" == "1" ]]; then cat "${TEST_STATE}/$1.restarts"; else systemctl show "$2" -p NRestarts --value; fi
}
service_reset_failed()
{
    if [[ "${test_mode}" == "1" ]]; then
        printf '0\n' > "${TEST_STATE}/$1.restarts"
        [[ "$(cat "${TEST_STATE}/$1.active")" != failed ]] \
            || printf 'inactive\n' > "${TEST_STATE}/$1.active"
    else
        systemctl reset-failed "$2"
    fi
}
service_stop()
{
    if [[ "${test_mode}" == "1" ]]; then
        printf 'inactive\n' > "${TEST_STATE}/$1.active"
        if [[ "$1" == login && "$(cat "${TEST_STATE}/online-on-login-stop")" == YES ]]; then
            printf '1\n' > "${TEST_STATE}/online"
            printf 'NO\n' > "${TEST_STATE}/online-on-login-stop"
        fi
        if [[ "$1" == login && "$(cat "${TEST_STATE}/zone-change-on-login-stop")" == YES ]]; then
            local zone_restarts
            zone_restarts="$(cat "${TEST_STATE}/zone.restarts")"
            printf '%s\n' "$((zone_restarts + 1))" > "${TEST_STATE}/zone.restarts"
            printf 'active\n' > "${TEST_STATE}/zone.active"
            printf 'NO\n' > "${TEST_STATE}/zone-change-on-login-stop"
        fi
    else
        systemctl stop "$2"
    fi
}
service_start()
{
    local engine="$1"
    if [[ "${test_mode}" == "1" ]]; then
        [[ "${rolling_back}" == "true" || "${failure_step}" != "${engine}_start" ]] || return 1
        printf 'active\n' > "${TEST_STATE}/${engine}.active"
        local starts="$(cat "${TEST_STATE}/${engine}.starts")"
        printf '%s\n' "$((starts + 1))" > "${TEST_STATE}/${engine}.starts"
        if [[ "${engine}" == zone && "$(cat "${TEST_STATE}/zone.restart-on-start")" == YES ]]; then
            local restarts="$(cat "${TEST_STATE}/zone.restarts")"
            printf '%s\n' "$((restarts + 1))" > "${TEST_STATE}/zone.restarts"
            printf 'NO\n' > "${TEST_STATE}/zone.restart-on-start"
        fi
        if [[ "${engine}" == zone && "$(cat "${TEST_STATE}/online-on-zone-start")" == YES ]]; then
            printf '1\n' > "${TEST_STATE}/online"
            printf 'NO\n' > "${TEST_STATE}/online-on-zone-start"
        fi
    else
        systemctl start "$2"
    fi
}
listener_present()
{
    local engine="$1" port="$2" service="$3"
    if [[ "${test_mode}" == "1" ]]; then
        local starts
        starts="$(cat "${TEST_STATE}/${engine}.starts")"
        [[ "${mutation_started}" != "true" || "${rolling_back}" == "true" || "${failure_step}" != listener || "${starts}" -eq 0 ]] || return 1
        service_active "${engine}" "${service}" || return 1
        if [[ "${mutation_started}" == "true" && "${starts}" -gt 0 ]]; then
            local delay checks
            delay="$(cat "${TEST_STATE}/${engine}.listener-delay")"
            checks="$(cat "${TEST_STATE}/${engine}.listener-checks")"
            if (( checks < delay )); then
                printf '%s\n' "$((checks + 1))" > "${TEST_STATE}/${engine}.listener-checks"
                return 1
            fi
        fi
        return 0
    fi
    local main_pid="$(systemctl show "${service}" -p MainPID --value)"
    [[ "${main_pid}" =~ ^[1-9][0-9]*$ ]] || return 1
    ss -H -ltnp "sport = :${port}" | grep -F "pid=${main_pid}," >/dev/null
}
service_state()
{
    if [[ "${test_mode}" == "1" ]]; then
        cat "${TEST_STATE}/$1.active"
    else
        systemctl show "$2" -p ActiveState -p SubState --value 2>/dev/null | tr '\n' '/' | sed 's|/$||'
    fi
}
service_journal()
{
    if [[ "${test_mode}" == "1" ]]; then
        printf 'fixture journal service=%s state=%s restarts=%s\n' "$2" "$(service_state "$1" "$2")" "$(service_restarts "$1" "$2")"
    else
        journalctl --unit="$2" --lines=50 --no-pager
    fi
}
readiness_pause()
{
    [[ "${test_mode}" == "1" ]] || sleep "${READINESS_POLL_INTERVAL_SECONDS}"
}
wait_for_readiness()
{
    local engine="$1" port="$2" service="$3" elapsed=0
    while true; do
        if service_active "${engine}" "${service}" && listener_present "${engine}" "${port}" "${service}"; then
            echo "READINESS_WAIT=PASS engine=${engine} elapsedSeconds=${elapsed} timeoutSeconds=${READINESS_TIMEOUT_SECONDS} pollIntervalSeconds=${READINESS_POLL_INTERVAL_SECONDS} service=${service} state=$(service_state "${engine}" "${service}") restarts=$(service_restarts "${engine}" "${service}") listenerPort=${port} listenerDetected=YES"
            return 0
        fi
        (( elapsed < READINESS_TIMEOUT_SECONDS )) || break
        readiness_pause
        elapsed=$((elapsed + READINESS_POLL_INTERVAL_SECONDS))
    done

    local listener_state=NO state_value restart_value
    listener_present "${engine}" "${port}" "${service}" && listener_state=YES
    state_value="$(service_state "${engine}" "${service}" 2>/dev/null || printf unknown)"
    restart_value="$(service_restarts "${engine}" "${service}" 2>/dev/null || printf unknown)"
    echo "READINESS_WAIT=TIMEOUT engine=${engine} elapsedSeconds=${elapsed} timeoutSeconds=${READINESS_TIMEOUT_SECONDS} pollIntervalSeconds=${READINESS_POLL_INTERVAL_SECONDS} service=${service} state=${state_value} restarts=${restart_value} listenerPort=${port} listenerDetected=${listener_state}" >&2
    echo "READINESS_JOURNAL_BEGIN service=${service}" >&2
    service_journal "${engine}" "${service}" >&2 || true
    echo "READINESS_JOURNAL_END service=${service}" >&2
    return 1
}
online_count()
{
    if [[ "${test_mode}" == "1" ]]; then cat "${TEST_STATE}/online"; else docker exec -i "${DATABASE_CONTAINER}" sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" '"${EXPECTED_DATABASE}"' --batch --raw --skip-column-names -e "SELECT COUNT(*) FROM characters WHERE Online IS NOT NULL AND Online <> 0;"'; fi
}
daemon_reload()
{
    if [[ "${test_mode}" == "1" ]]; then
        if [[ "${rolling_back}" != true \
            && -f "${TEST_STATE}/daily-login-dropin-tamper-after-reload" \
            && "$(cat "${TEST_STATE}/daily-login-dropin-tamper-after-reload")" == YES ]]; then
            printf '# concurrent fixture drift\n' >> "${ZONE_DAILY_LOGIN_DROPIN}"
            printf 'NO\n' > "${TEST_STATE}/daily-login-dropin-tamper-after-reload"
        fi
        if [[ -f "${ZONE_STALE_NOTIFY_DROPIN}" && ! -L "${ZONE_STALE_NOTIFY_DROPIN}" ]]; then
            printf 'simple\n' > "${TEST_STATE}/zone.effective-type"
            printf 'none\n' > "${TEST_STATE}/zone.notify-access"
            printf '%s %s\n' "${ZONE_STALE_NOTIFY_DROPIN}" "${ZONE_DAILY_LOGIN_DROPIN}" > "${TEST_STATE}/zone.dropin-paths"
        elif [[ "${rolling_back}" != true \
            && -f "${TEST_STATE}/zone.effective-mismatch-after-reload" \
            && "$(cat "${TEST_STATE}/zone.effective-mismatch-after-reload")" == YES ]]; then
            printf 'simple\n' > "${TEST_STATE}/zone.effective-type"
            printf 'none\n' > "${TEST_STATE}/zone.notify-access"
            printf '/run/systemd/system/ao-rebirth-zoneengine.service.d/99-fixture.conf\n' > "${TEST_STATE}/zone.dropin-paths"
        else
            printf 'notify\n' > "${TEST_STATE}/zone.effective-type"
            printf 'main\n' > "${TEST_STATE}/zone.notify-access"
            printf '%s\n' "${ZONE_DAILY_LOGIN_DROPIN}" > "${TEST_STATE}/zone.dropin-paths"
        fi
        return
    fi
    systemctl daemon-reload
}
verify_unit_static() { [[ "${test_mode}" == "1" ]] || systemd-analyze verify "${LOGIN_UNIT_SOURCE}" "${ZONE_UNIT_SOURCE}" >/dev/null; }

environment_value()
{
    local environment_file="$1" key="$2" count line
    count="$(grep -Ec "^[[:space:]]*${key}[[:space:]]*=" "${environment_file}" || true)"
    [[ "${count}" == "1" ]] \
        || fail "${key} is missing or duplicated in ${environment_file}"
    line="$(grep -E "^[[:space:]]*${key}[[:space:]]*=" "${environment_file}")"
    [[ "${line}" == "${key}="* ]] \
        || fail "${key} must use canonical KEY=value formatting in ${environment_file}"
    [[ -n "${line#*=}" ]] || fail "${key} is empty in ${environment_file}"
    printf '%s' "${line#*=}"
}

validate_candidate_database_contract()
{
    local login_config_path zone_config_path
    login_config_path="$(environment_value "${LOGIN_ENV}" AO_REBIRTH_CONFIG_PATH)"
    zone_config_path="$(environment_value "${ZONE_ENV}" AO_REBIRTH_CONFIG_PATH)"
    [[ "${login_config_path}" == "${LOGIN_CONFIG}" ]] \
        || fail "candidate LoginEngine configuration path diverges from the governed production path"
    [[ "${zone_config_path}" == "${ZONE_CONFIG}" ]] \
        || fail "candidate ZoneEngine configuration path diverges from the governed production path"
    require_regular_file "${login_config_path}"
    require_regular_file "${zone_config_path}"
    if [[ "${test_mode}" == "1" ]]; then
        local fixture_validation
        printf '%s\n' "${login_config_path}" > "${TEST_STATE}/candidate-login-config"
        printf '%s\n' "${zone_config_path}" > "${TEST_STATE}/candidate-zone-config"
        fixture_validation="$(cat "${TEST_STATE}/candidate-validation")"
        case "${fixture_validation}" in
            PASS_RESTART_ZONE)
                local fixture_restarts
                fixture_restarts="$(cat "${TEST_STATE}/zone.restarts")"
                printf '%s\n' "$((fixture_restarts + 1))" > "${TEST_STATE}/zone.restarts"
                fixture_validation=PASS
                ;;
            PASS_RESTART_LOGIN)
                local fixture_login_restarts
                fixture_login_restarts="$(cat "${TEST_STATE}/login.restarts")"
                printf '%s\n' "$((fixture_login_restarts + 1))" > "${TEST_STATE}/login.restarts"
                fixture_validation=PASS
                ;;
            PASS_ACTIVATE_LOGIN)
                printf 'active\n' > "${TEST_STATE}/login.active"
                fixture_validation=PASS
                ;;
            PASS_ONLINE)
                printf '1\n' > "${TEST_STATE}/online"
                fixture_validation=PASS
                ;;
        esac
        [[ "${fixture_validation}" == PASS ]] \
            || fail "candidate database compatibility validation failed"
        echo "CANDIDATE_DATABASE_COMPATIBILITY=PASS"
        return
    fi

    require_regular_file "${LOGIN_ARTIFACT_DIR}/Config.xml"
    require_regular_file "${ZONE_ARTIFACT_DIR}/Config.xml"
    local login_connection zone_connection
    login_connection="$(environment_value "${LOGIN_ENV}" AO_REBIRTH_MYSQL_CONNECTION)"
    zone_connection="$(environment_value "${ZONE_ENV}" AO_REBIRTH_MYSQL_CONNECTION)"

    if ! runuser -u "${EXPECTED_SERVICE_USER}" -g "${EXPECTED_SERVICE_GROUP}" -- env \
        AO_REBIRTH_REQUIRED_SQL_TYPE=MySql \
        AO_REBIRTH_EXPECTED_DATABASE="${EXPECTED_DATABASE}" \
        AO_REBIRTH_BIND_MODE=Public \
        AO_REBIRTH_CONFIG_PATH="${login_config_path}" \
        AO_REBIRTH_MYSQL_CONNECTION="${login_connection}" \
        "${LOGIN_ARTIFACT_DIR}/LoginEngine" --validate-startup >/dev/null; then
        fail "candidate LoginEngine startup contract validation failed"
    fi
    if ! runuser -u "${EXPECTED_SERVICE_USER}" -g "${EXPECTED_SERVICE_GROUP}" -- env \
        AO_REBIRTH_REQUIRED_SQL_TYPE=MySql \
        AO_REBIRTH_EXPECTED_DATABASE="${EXPECTED_DATABASE}" \
        AO_REBIRTH_BIND_MODE=Public \
        AO_REBIRTH_CONFIG_PATH="${login_config_path}" \
        AO_REBIRTH_MYSQL_CONNECTION="${login_connection}" \
        "${LOGIN_ARTIFACT_DIR}/LoginEngine" --validate-database >/dev/null; then
        fail "candidate LoginEngine database contract validation failed"
    fi
    if ! runuser -u "${EXPECTED_SERVICE_USER}" -g "${EXPECTED_SERVICE_GROUP}" -- env \
        AO_REBIRTH_REQUIRED_SQL_TYPE=MySql \
        AO_REBIRTH_EXPECTED_DATABASE="${EXPECTED_DATABASE}" \
        AO_REBIRTH_BIND_MODE=Public \
        AO_REBIRTH_STAGE10_PUBLIC_PLAYER_ACCESS=1 \
        AO_REBIRTH_ZONE_LISTEN_IP=0.0.0.0 \
        AO_REBIRTH_CHAT_LISTEN_IP=127.0.0.1 \
        AO_REBIRTH_CONFIG_PATH="${zone_config_path}" \
        AO_REBIRTH_MYSQL_CONNECTION="${zone_connection}" \
        "${ZONE_ARTIFACT_DIR}/ZoneEngine" --validate-startup >/dev/null; then
        fail "candidate ZoneEngine startup contract validation failed"
    fi
    if ! runuser -u "${EXPECTED_SERVICE_USER}" -g "${EXPECTED_SERVICE_GROUP}" -- env \
        AO_REBIRTH_REQUIRED_SQL_TYPE=MySql \
        AO_REBIRTH_EXPECTED_DATABASE="${EXPECTED_DATABASE}" \
        AO_REBIRTH_BIND_MODE=Public \
        AO_REBIRTH_STAGE10_PUBLIC_PLAYER_ACCESS=1 \
        AO_REBIRTH_ZONE_LISTEN_IP=0.0.0.0 \
        AO_REBIRTH_CHAT_LISTEN_IP=127.0.0.1 \
        AO_REBIRTH_CONFIG_PATH="${zone_config_path}" \
        AO_REBIRTH_MYSQL_CONNECTION="${zone_connection}" \
        "${ZONE_ARTIFACT_DIR}/ZoneEngine" --validate-database >/dev/null; then
        fail "candidate ZoneEngine database contract validation failed"
    fi
    echo "CANDIDATE_DATABASE_COMPATIBILITY=PASS"
}

verify_recovery_frozen()
{
    recovery_requested || return 0
    service_stopped zone "${ZONE_SERVICE}" \
        || fail "ZoneEngine is not in an exact stopped state for outage recovery"
    listener_absent zone 7501 "${ZONE_SERVICE}" \
        || fail "port 7501 must remain closed for outage recovery"
    [[ "$(service_restarts zone "${ZONE_SERVICE}")" == "${ZONE_RESTARTS_BEFORE}" ]] \
        || fail "ZoneEngine restart count changed while outage recovery was frozen"
    echo "ZONEENGINE_OUTAGE_FROZEN=PASS"
    if [[ "${resume_stopped_recovery}" == true ]]; then
        service_stopped login "${LOGIN_SERVICE}" \
            || fail "LoginEngine is not in an exact stopped state for stopped-pair recovery"
        listener_absent login 7500 "${LOGIN_SERVICE}" \
            || fail "port 7500 reopened during stopped-pair recovery"
        [[ "$(service_restarts login "${LOGIN_SERVICE}")" == "${LOGIN_RESTARTS_BEFORE}" ]] \
            || fail "LoginEngine restart count changed while stopped-pair recovery was frozen"
        echo "STOPPED_PAIR_RECOVERY_FROZEN=PASS"
    fi
}

verify_pre_stop_boundary()
{
    if [[ "${resume_stopped_recovery}" == true ]]; then
        verify_recovery_frozen
        [[ "$(online_count)" == 0 ]] \
            || fail "online characters appeared before stopped-pair recovery mutation"
        echo "PRESTOP_BOUNDARY=PASS onlineCharacters=0 mode=stopped-pair"
        return
    fi
    service_active login "${LOGIN_SERVICE}" \
        || fail "LoginEngine stopped before deployment mutation"
    listener_present login 7500 "${LOGIN_SERVICE}" \
        || fail "port 7500 listener disappeared before deployment mutation"
    [[ "$(online_count)" == 0 ]] \
        || fail "online characters appeared before deployment mutation"
    verify_recovery_frozen
    echo "PRESTOP_BOUNDARY=PASS onlineCharacters=0"
}

verify_zone_pre_stop_invariant()
{
    if recovery_requested; then
        service_stopped zone "${ZONE_SERVICE}" \
            || fail "ZoneEngine recovery state changed before release mutation"
        listener_absent zone 7501 "${ZONE_SERVICE}" \
            || fail "port 7501 reopened before release mutation"
    else
        service_active zone "${ZONE_SERVICE}" \
            || fail "ZoneEngine stopped before its controlled deployment stop"
        listener_present zone 7501 "${ZONE_SERVICE}" \
            || fail "port 7501 listener changed before its controlled deployment stop"
    fi
    [[ "$(service_restarts zone "${ZONE_SERVICE}")" == "${ZONE_RESTARTS_BEFORE}" ]] \
        || fail "ZoneEngine restart count changed before its controlled deployment stop"
    echo "ZONE_PRESTOP_INVARIANT=PASS mode=$(recovery_requested && printf stopped || printf active)"
}

verify_login_admission_closed_boundary()
{
    service_stopped login "${LOGIN_SERVICE}" \
        || fail "LoginEngine did not reach an exact stopped state before deployment mutation"
    listener_absent login 7500 "${LOGIN_SERVICE}" \
        || fail "port 7500 remained open after LoginEngine admission closed"
    verify_zone_pre_stop_invariant
    [[ "$(online_count)" == 0 ]] \
        || fail "online characters appeared after LoginEngine admission closed"
    verify_zone_pre_stop_invariant
    echo "LOGIN_ADMISSION_CLOSED_BOUNDARY=PASS onlineCharacters=0"
}

verify_closed_engine_boundary()
{
    service_stopped login "${LOGIN_SERVICE}" \
        || fail "LoginEngine did not remain stopped before deployment mutation"
    service_stopped zone "${ZONE_SERVICE}" \
        || fail "ZoneEngine did not reach an exact stopped state before deployment mutation"
    listener_absent login 7500 "${LOGIN_SERVICE}" \
        || fail "port 7500 reopened before deployment mutation"
    listener_absent zone 7501 "${ZONE_SERVICE}" \
        || fail "port 7501 remained open after ZoneEngine admission closed"
    [[ "$(online_count)" == 0 ]] \
        || fail "online characters appeared before the closed-engine mutation boundary"
    echo "CLOSED_ENGINE_MUTATION_BOUNDARY=PASS onlineCharacters=0"
}

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
    require_regular_file "${ZONE_CONFIG}"
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

deployed_manifest_value()
{
    local key="$1" count line
    count="$(grep -Ec "^${key}=" "${DEPLOYED_MANIFEST}" || true)"
    [[ "${count}" == 1 ]] || fail "deployed release key ${key} is missing or duplicated"
    line="$(grep -E "^${key}=" "${DEPLOYED_MANIFEST}")"
    printf '%s' "${line#*=}"
}

require_stopped_recovery_provenance()
{
    [[ "${resume_stopped_recovery}" == true ]] || return 0
    require_regular_file "${PREVIOUS_LOGIN_RELEASE}/SOURCE_SHA"
    require_regular_file "${PREVIOUS_ZONE_RELEASE}/SOURCE_SHA"
    require_regular_file "${DEPLOYED_MANIFEST}"
    local prior_login_sha prior_zone_sha deployed_sha
    prior_login_sha="$(tr -d '\r\n\t ' < "${PREVIOUS_LOGIN_RELEASE}/SOURCE_SHA")"
    prior_zone_sha="$(tr -d '\r\n\t ' < "${PREVIOUS_ZONE_RELEASE}/SOURCE_SHA")"
    deployed_sha="$(deployed_manifest_value SOURCE_SHA)"
    [[ "${prior_login_sha}" =~ ^[0-9a-f]{40}$ \
        && "${prior_login_sha}" == "${prior_zone_sha}" \
        && "${prior_login_sha}" == "${deployed_sha}" ]] \
        || fail "stopped recovery prior release SHA provenance is incoherent"
    [[ "$(deployed_manifest_value LOGINENGINE_RELEASE)" == "${PREVIOUS_LOGIN_RELEASE}" \
        && "$(deployed_manifest_value ZONEENGINE_RELEASE)" == "${PREVIOUS_ZONE_RELEASE}" ]] \
        || fail "stopped recovery prior release paths are incoherent"
    [[ "$(deployed_manifest_value LOGINENGINE_ARTIFACT_SHA256)" == "${PREVIOUS_LOGIN_ARTIFACT_SHA256}" \
        && "$(deployed_manifest_value ZONEENGINE_ARTIFACT_SHA256)" == "${PREVIOUS_ZONE_ARTIFACT_SHA256}" \
        && "$(deployed_manifest_value LOGINENGINE_UNIT_SHA256)" == "${PREVIOUS_LOGIN_UNIT_SHA256}" \
        && "$(deployed_manifest_value ZONEENGINE_UNIT_SHA256)" == "${PREVIOUS_ZONE_UNIT_SHA256}" ]] \
        || fail "stopped recovery prior artifact or unit provenance is incoherent"
    echo "STOPPED_PAIR_ROLLBACK_PROVENANCE=PASS sourceSha=${prior_login_sha}"
}

preflight()
{
    validate_manifest_shape
    require_artifact "${LOGIN_ARTIFACT_DIR}" LoginEngine "${LOGIN_ARTIFACT_SHA}"
    require_artifact "${ZONE_ARTIFACT_DIR}" ZoneEngine "${ZONE_ARTIFACT_SHA}"
    require_zone_placement_artifact "${ZONE_ARTIFACT_DIR}"
    validate_units
    verify_unit_static
    validate_zone_dropin_preflight
    [[ "${test_mode}" == "1" ]] || id "${EXPECTED_SERVICE_USER}" >/dev/null 2>&1
    [[ "${test_mode}" == "1" ]] || [[ "$(id -gn "${EXPECTED_SERVICE_USER}")" == "${EXPECTED_SERVICE_GROUP}" ]]
    LOGIN_RESTARTS_BEFORE="$(service_restarts login "${LOGIN_SERVICE}")"
    ZONE_RESTARTS_BEFORE="$(service_restarts zone "${ZONE_SERVICE}")"
    readonly LOGIN_RESTARTS_BEFORE ZONE_RESTARTS_BEFORE
    if [[ "${resume_stopped_recovery}" == true ]]; then
        service_stopped login "${LOGIN_SERVICE}" || fail "LoginEngine must be exactly stopped for stopped-pair recovery"
        listener_absent login 7500 "${LOGIN_SERVICE}" || fail "port 7500 must be closed for stopped-pair recovery"
        service_stopped zone "${ZONE_SERVICE}" || fail "ZoneEngine must be exactly stopped for stopped-pair recovery"
        listener_absent zone 7501 "${ZONE_SERVICE}" || fail "port 7501 must be closed for stopped-pair recovery"
        echo "STOPPED_PAIR_RECOVERY_PRECONDITION=PASS"
    elif [[ "${recover_zone_outage}" == true ]]; then
        service_active login "${LOGIN_SERVICE}" || fail "LoginEngine is not active before outage recovery"
        listener_present login 7500 "${LOGIN_SERVICE}" || fail "port 7500 predeploy listener is missing"
        service_stopped zone "${ZONE_SERVICE}" || fail "ZoneEngine must already be stopped for outage recovery"
        listener_absent zone 7501 "${ZONE_SERVICE}" || fail "port 7501 must be closed for outage recovery"
        echo "ZONEENGINE_OUTAGE_RECOVERY_PRECONDITION=PASS"
    else
        service_active login "${LOGIN_SERVICE}" || fail "LoginEngine is not active before deployment"
        listener_present login 7500 "${LOGIN_SERVICE}" || fail "port 7500 predeploy listener is missing"
        service_active zone "${ZONE_SERVICE}" || fail "ZoneEngine is not active before deployment"
        listener_present zone 7501 "${ZONE_SERVICE}" || fail "port 7501 predeploy listener is missing"
    fi
    require_rollback_material
    require_stopped_recovery_provenance
    check_shared_directory
    ONLINE_BEFORE="$(online_count)"
    readonly ONLINE_BEFORE
    [[ "${ONLINE_BEFORE}" =~ ^[0-9]+$ ]] || fail "could not determine online character count"
    if [[ "${resume_stopped_recovery}" == true ]]; then
        echo "LOGINENGINE_PREDEPLOY_STATUS=stopped-recovery"
        echo "ZONEENGINE_PREDEPLOY_STATUS=stopped-recovery"
    else
        echo "LOGINENGINE_PREDEPLOY_STATUS=active"
    fi
    if [[ "${recover_zone_outage}" == true && "${resume_stopped_recovery}" != true ]]; then
        echo "ZONEENGINE_PREDEPLOY_STATUS=stopped-outage"
    elif [[ "${resume_stopped_recovery}" != true ]]; then
        echo "ZONEENGINE_PREDEPLOY_STATUS=active"
    fi
    echo "LOGINENGINE_PREDEPLOY_RESTARTS=${LOGIN_RESTARTS_BEFORE}"
    echo "ZONEENGINE_PREDEPLOY_RESTARTS=${ZONE_RESTARTS_BEFORE}"
    echo "ONLINE_NONZERO_ROWS_PREDEPLOY=${ONLINE_BEFORE}"
    [[ "${ONLINE_BEFORE}" == 0 ]] || fail "online characters present; deployment policy is fail closed"
    validate_candidate_database_contract
    verify_recovery_frozen
    echo "PREDEPLOY_HEALTH_CHECK=PASS"
}

current_release_matches()
{
    [[ -f "${LOGIN_CURRENT}/SOURCE_SHA" && -f "${ZONE_CURRENT}/SOURCE_SHA" ]] || return 1
    [[ "$(tr -d '\r\n\t ' < "${LOGIN_CURRENT}/SOURCE_SHA")" == "${SOURCE_SHA}" ]] || return 1
    [[ "$(tr -d '\r\n\t ' < "${ZONE_CURRENT}/SOURCE_SHA")" == "${SOURCE_SHA}" ]] || return 1
    [[ "$(sha256sum "${LOGIN_CURRENT}/LoginEngine" | awk '{print $1}')" == "${LOGIN_ARTIFACT_SHA}" ]] || return 1
    [[ "$(sha256sum "${ZONE_CURRENT}/ZoneEngine" | awk '{print $1}')" == "${ZONE_ARTIFACT_SHA}" ]] || return 1
    placement_provenance_load \
        "${ZONE_CURRENT}" \
        "${SOURCE_SHA}" \
        linux \
        "${MANIFEST_PLACEMENT_BUILD_MANIFEST_SHA}" >/dev/null 2>&1 || return 1
    placement_require_build_provenance "${ZONE_CURRENT}/BUILD_PROVENANCE.env" >/dev/null 2>&1 || return 1
    [[ "$(sha256sum "${LOGIN_UNIT_TARGET}" | awk '{print $1}')" == "${LOGIN_UNIT_SHA}" ]] || return 1
    [[ "$(sha256sum "${ZONE_UNIT_TARGET}" | awk '{print $1}')" == "${ZONE_UNIT_SHA}" ]] || return 1
    [[ ! -e "${ZONE_STALE_NOTIFY_DROPIN}" && ! -L "${ZONE_STALE_NOTIFY_DROPIN}" ]] || return 1
    zone_effective_notify_contract || return 1
    [[ -d "${OWNERSHIP_DIR}" ]] || return 1
    [[ "${test_mode}" == 1 || "$(stat -c '%a' "${OWNERSHIP_DIR}")" == 700 ]] || return 1
}

create_snapshot()
{
    local login_was_active=YES
    local zone_was_active=YES
    [[ "${resume_stopped_recovery}" != true ]] || login_was_active=NO
    if recovery_requested; then zone_was_active=NO; fi
    snapshot_dir="${SNAPSHOT_ROOT}/${RELEASE_NAME}-$(date -u +%Y%m%dT%H%M%SZ)-$$"
    if [[ "${test_mode}" == "1" ]]; then mkdir -p -- "${snapshot_dir}"; else install -d -m 0700 "${snapshot_dir}"; fi
    cp -p -- "${LOGIN_UNIT_TARGET}" "${snapshot_dir}/loginengine.service"
    cp -p -- "${ZONE_UNIT_TARGET}" "${snapshot_dir}/zoneengine.service"
    cp -p -- "${LOGIN_ENV}" "${snapshot_dir}/loginengine.env"
    cp -p -- "${ZONE_ENV}" "${snapshot_dir}/zoneengine.env"
    cp -p -- "${LOGIN_CONFIG}" "${snapshot_dir}/loginengine.Config.xml"
    [[ ! -f "${ZONE_CONFIG}" ]] || cp -p -- "${ZONE_CONFIG}" "${snapshot_dir}/zoneengine.Config.xml"
    if [[ "${ZONE_NOTIFY_DROPIN_WAS_PRESENT}" == YES ]]; then
        cp -p -- "${ZONE_STALE_NOTIFY_DROPIN}" "${snapshot_dir}/zoneengine.10-type-simple.conf"
    fi
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
LOGINENGINE_WAS_ACTIVE=${login_was_active}
ZONEENGINE_WAS_ACTIVE=${zone_was_active}
ZONEENGINE_STALE_NOTIFY_DROPIN_WAS_PRESENT=${ZONE_NOTIFY_DROPIN_WAS_PRESENT}
ZONEENGINE_EFFECTIVE_TYPE_BEFORE=${ZONE_EFFECTIVE_TYPE_BEFORE}
ZONEENGINE_EFFECTIVE_NOTIFY_ACCESS_BEFORE=${ZONE_EFFECTIVE_NOTIFY_ACCESS_BEFORE}
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
        if [[ "${apphost}" == "ZoneEngine" ]]; then
            require_zone_placement_artifact "${target}"
        fi
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
    if [[ "${apphost}" == "ZoneEngine" ]]; then
        require_zone_placement_artifact "${staging}"
    fi
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
PLACEMENT_CORPUS_VERSION=${MANIFEST_PLACEMENT_CORPUS_VERSION}
PLACEMENT_CORPUS_MANIFEST_SHA256=${MANIFEST_PLACEMENT_CORPUS_MANIFEST_SHA}
PLACEMENT_CORPUS_SUMMARY_SHA256=${MANIFEST_PLACEMENT_CORPUS_SUMMARY_SHA}
PLACEMENT_CORPUS_INDEX_SHA256=${MANIFEST_PLACEMENT_CORPUS_INDEX_SHA}
PLACEMENT_ACGHASH_INVENTORY_SHA256=${MANIFEST_PLACEMENT_ACGHASH_INVENTORY_SHA}
PLACEMENT_BUILD_MANIFEST_SHA256=${MANIFEST_PLACEMENT_BUILD_MANIFEST_SHA}
PLACEMENT_RESOURCE_COUNT=${MANIFEST_PLACEMENT_RESOURCE_COUNT}
PLACEMENT_PARSED_RESOURCE_COUNT=${MANIFEST_PLACEMENT_PARSED_RESOURCE_COUNT}
PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT=${MANIFEST_PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT}
PLACEMENT_DISTRICT_COUNT=${MANIFEST_PLACEMENT_DISTRICT_COUNT}
PLACEMENT_RECORD_COUNT=${MANIFEST_PLACEMENT_RECORD_COUNT}
PLACEMENT_UNIQUE_ACGHASH_COUNT=${MANIFEST_PLACEMENT_UNIQUE_ACGHASH_COUNT}
PLACEMENT_RUNTIME_AUTHORIZED_COUNT=${MANIFEST_PLACEMENT_RUNTIME_AUTHORIZED_COUNT}
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
        && zone_effective_notify_contract \
        && [[ "$(service_restarts login "${LOGIN_SERVICE}")" == "${LOGIN_RESTARTS_BEFORE}" ]] \
        && [[ -n "${ZONE_RESTARTS_START_BASELINE}" ]] \
        && [[ "$(service_restarts zone "${ZONE_SERVICE}")" == "${ZONE_RESTARTS_START_BASELINE}" ]] \
        && current_release_matches
}

post_start_stability()
{
    local elapsed=0
    while true; do
        post_health || return 1
        (( elapsed >= POST_START_STABILITY_SECONDS )) && break
        readiness_pause
        elapsed=$((elapsed + READINESS_POLL_INTERVAL_SECONDS))
    done
    echo "POST_START_STABILITY=PASS seconds=${POST_START_STABILITY_SECONDS} zoneRestarts=${ZONE_RESTARTS_START_BASELINE}"
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
    verify_zone_dropin_rollback || return 1
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
    rollback_operation RESTORE_ZONE_NOTIFY_DROPIN restore_zone_notify_dropin || failed=true
    rollback_operation DAEMON_RELOAD daemon_reload || failed=true
    rollback_operation VERIFY_EXACT_PRIOR_STATE verify_rollback_state || failed=true
    if [[ "${recover_zone_outage}" == true ]]; then
        [[ "${failed}" == false ]] || { echo "ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=FAIL" >&2; return 1; }
        echo "ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=PASS"
        return
    fi
    [[ "${failed}" == false ]] \
        || { echo "ROLLBACK_BOTH_SERVICES=FAIL" >&2; return 1; }
    rollback_operation START_LOGIN service_start login "${LOGIN_SERVICE}" || failed=true
    rollback_operation LOGIN_READINESS wait_for_readiness login 7500 "${LOGIN_SERVICE}" || failed=true
    rollback_operation START_ZONE service_start zone "${ZONE_SERVICE}" || failed=true
    rollback_operation ZONE_READINESS wait_for_readiness zone 7501 "${ZONE_SERVICE}" || failed=true
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
    if [[ "${recover_zone_outage}" != true ]] && current_release_matches; then
        echo "ALREADY_DEPLOYED=YES"
        echo "IDEMPOTENT_REDEPLOY=PASS"
        echo "DEPLOYED_SHA=${SOURCE_SHA}"
        return
    fi
    echo "ONLINE_PLAYER_DEPLOYMENT_POLICY=REQUIRE_ZERO_NONZERO_ONLINE_ROWS"
    if [[ "${recover_zone_outage}" == true ]]; then
        echo "ROLLBACK_POLICY=RESTORE_INCOMPATIBLE_PRIOR_PAIR_BUT_LEAVE_STOPPED"
    fi
    verify_pre_stop_boundary
    if [[ "${dry_run}" == true ]]; then echo "DRY_RUN=PASS"; echo "PRODUCTION_MUTATION=NO"; return; fi

    create_snapshot
    mutation_started=true
    trap on_exit EXIT
    if [[ "${resume_stopped_recovery}" != true ]]; then
        service_stop login "${LOGIN_SERVICE}"
    fi
    verify_login_admission_closed_boundary
    if ! recovery_requested; then
        service_stop zone "${ZONE_SERVICE}"
    fi
    verify_closed_engine_boundary
    inject_failure artifact_install
    install_release "${LOGIN_ARTIFACT_DIR}" LoginEngine "${LOGIN_ARTIFACT_SHA}" "${LOGIN_RELEASE_TARGET}" "${LOGIN_RELEASES}"
    install_release "${ZONE_ARTIFACT_DIR}" ZoneEngine "${ZONE_ARTIFACT_SHA}" "${ZONE_RELEASE_TARGET}" "${ZONE_RELEASES}"
    inject_failure unit_install
    install_units
    switch_link "${LOGIN_CURRENT}" "${LOGIN_RELEASE_TARGET}"
    switch_link "${ZONE_CURRENT}" "${ZONE_RELEASE_TARGET}"
    create_shared_directory
    remove_zone_stale_notify_dropin
    daemon_reload
    verify_unit_static
    zone_effective_notify_contract \
        || fail "ZoneEngine effective Type=notify readiness contract failed after installation"
    echo "ZONEENGINE_EFFECTIVE_READINESS_CONTRACT=PASS type=notify notifyAccess=main dropInPaths=governed-daily-login"
    service_start login "${LOGIN_SERVICE}" || fail "LoginEngine failed startup"
    wait_for_readiness login 7500 "${LOGIN_SERVICE}" || fail "LoginEngine readiness timed out after startup"
    if [[ "${recover_zone_outage}" == true ]]; then
        service_reset_failed zone "${ZONE_SERVICE}"
        [[ "$(service_restarts zone "${ZONE_SERVICE}")" == 0 ]] \
            || fail "ZoneEngine restart counter did not reset before controlled startup"
        echo "ZONEENGINE_RESTART_COUNTER_RESET=PASS previous=${ZONE_RESTARTS_BEFORE} baseline=0"
    fi
    ZONE_RESTARTS_START_BASELINE="$(service_restarts zone "${ZONE_SERVICE}")"
    service_start zone "${ZONE_SERVICE}" || fail "ZoneEngine failed startup"
    wait_for_readiness zone 7501 "${ZONE_SERVICE}" || fail "ZoneEngine readiness timed out after startup"
    post_start_stability || fail "post-deployment stability check failed"
    write_deployed_manifest
    deployment_committed=true
    trap - EXIT
    echo "TRANSACTIONAL_DEPLOYMENT=PASS"
    echo "DEPLOYED_SHA=${SOURCE_SHA}"
    echo "ROLLBACK_REQUIRED=NO"
}

main
