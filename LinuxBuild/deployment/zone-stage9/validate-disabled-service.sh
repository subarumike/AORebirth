#!/usr/bin/env bash
set -euo pipefail

readonly SERVICE_NAME="ao-rebirth-zoneengine.service"
readonly RELEASE_ROOT="/opt/ao-rebirth/zoneengine/releases"
readonly CURRENT_LINK="/opt/ao-rebirth/zoneengine/current"
readonly ENVIRONMENT_DIRECTORY="/etc/ao-rebirth/zoneengine"
readonly ENVIRONMENT_FILE="${ENVIRONMENT_DIRECTORY}/zoneengine.env"
readonly CHAT_STAGE6_ENVIRONMENT="/etc/ao-rebirth/chatengine/stage6/chatengine.env"
readonly UNIT_PATH="/etc/systemd/system/${SERVICE_NAME}"
readonly FAILURE_DROP_IN_DIRECTORY="/run/systemd/system/${SERVICE_NAME}.d"
readonly FAILURE_DROP_IN_FILE="${FAILURE_DROP_IN_DIRECTORY}/stage9-invalid-listen.conf"

fail()
{
    echo "FAIL: $*" >&2
    exit 1
}

require_root()
{
    [[ "${EUID}" -eq 0 ]] || fail "run as root"
}

require_file()
{
    local path="$1"
    [[ -f "${path}" && ! -L "${path}" ]] || fail "required file is missing or unsafe: ${path}"
}

require_publish_tree()
{
    local publish_dir="$1"
    [[ -d "${publish_dir}" && ! -L "${publish_dir}" ]] || fail "publish directory is missing or unsafe"
    require_file "${publish_dir}/ZoneEngine"
    require_file "${publish_dir}/ZoneEngine.dll"
    require_file "${publish_dir}/Config.xml"
    require_file "${publish_dir}/items.dat"
    require_file "${publish_dir}/nanos.dat"
    require_file "${publish_dir}/playfields.dat"
    require_file "${publish_dir}/XML Data/Stats.xml"
    require_file "${publish_dir}/XML Data/Playfields.xml"
    require_file "${publish_dir}/Scripts/KnuBotFlappy.cs"
    require_file "${publish_dir}/Scripts/InfoBot.cs"
    require_file "${publish_dir}/Scripts/KnuBotItemGiver.cs"
    require_file "${publish_dir}/Scripts/PerkResetService.cs"
}

install_environment_file()
{
    install -d -o root -g root -m 0755 "${ENVIRONMENT_DIRECTORY}"
    if [[ ! -f "${ENVIRONMENT_FILE}" ]]; then
        require_file "${CHAT_STAGE6_ENVIRONMENT}"
        local mysql_line
        mysql_line="$(grep -m 1 '^AO_REBIRTH_MYSQL_CONNECTION=' "${CHAT_STAGE6_ENVIRONMENT}")" \
            || fail "Stage 6 MySQL connection assignment is unavailable"
        [[ -n "${mysql_line}" ]] || fail "Stage 6 MySQL connection assignment is empty"
        umask 077
        printf '%s\n' "${mysql_line}" > "${ENVIRONMENT_FILE}.tmp"
        chown root:root "${ENVIRONMENT_FILE}.tmp"
        chmod 0600 "${ENVIRONMENT_FILE}.tmp"
        mv -f -- "${ENVIRONMENT_FILE}.tmp" "${ENVIRONMENT_FILE}"
    fi

    [[ "$(stat -c '%U:%G:%a' "${ENVIRONMENT_FILE}")" == "root:root:600" ]] \
        || fail "ZoneEngine environment must be root-owned mode 0600"
    grep -q '^AO_REBIRTH_MYSQL_CONNECTION=Server=127.0.0.1;Port=33067;Database=aorebirth_chatengine_stage6;' "${ENVIRONMENT_FILE}" \
        || fail "ZoneEngine environment is not pinned to the Stage 6 loopback database"
}

install_release()
{
    local publish_dir="$1"
    local unit_file="$2"
    local release_id="$3"
    local release_path="${RELEASE_ROOT}/${release_id}"

    [[ "${release_id}" =~ ^stage9-[A-Za-z0-9._-]+$ ]] || fail "release id must start with stage9-"
    [[ ! -e "${release_path}" && ! -L "${release_path}" ]] || fail "release already exists"
    require_file "${unit_file}"
    require_publish_tree "${publish_dir}"

    if ! getent group aorebirth >/dev/null; then
        groupadd --system aorebirth
    fi
    if ! id -u aorebirth >/dev/null 2>&1; then
        useradd --system --gid aorebirth --home-dir /nonexistent --shell /usr/sbin/nologin aorebirth
    fi

    install -d -o root -g root -m 0755 "${RELEASE_ROOT}"
    install -d -o root -g root -m 0755 "${release_path}"
    cp -a -- "${publish_dir}/." "${release_path}/"
    chown -R root:root "${release_path}"
    find "${release_path}" -type d -exec chmod 0755 {} +
    find "${release_path}" -type f -exec chmod 0644 {} +
    chmod 0755 "${release_path}/ZoneEngine"
    if [[ -f "${release_path}/createdump" ]]; then
        chmod 0755 "${release_path}/createdump"
    fi

    ln -sTfn "${release_path}" "${CURRENT_LINK}"
    install_environment_file
    install -o root -g root -m 0644 "${unit_file}" "${UNIT_PATH}"
    systemctl daemon-reload
}

require_active()
{
    [[ "$(systemctl is-active "${SERVICE_NAME}")" == "active" ]] \
        || fail "service did not become active"
    local main_pid
    main_pid="$(systemctl show "${SERVICE_NAME}" --property=MainPID --value)"
    [[ "${main_pid}" =~ ^[0-9]+$ && "${main_pid}" != "0" ]] || fail "service has no main PID"
    local -a zone_pids
    local attempt
    for attempt in {1..25}; do
        zone_pids=()
        local proc_entry
        for proc_entry in /proc/[0-9]*; do
            [[ -r "${proc_entry}/cmdline" ]] || continue
            local command_line
            command_line="$(tr '\0' ' ' < "${proc_entry}/cmdline")"
            if [[ "${command_line}" == "${CURRENT_LINK}/ZoneEngine --validate-lifecycle --shutdown-file /run/ao-rebirth-zoneengine/shutdown "
                || "${command_line}" == "$(readlink -f "${CURRENT_LINK}/ZoneEngine") --validate-lifecycle --shutdown-file /run/ao-rebirth-zoneengine/shutdown " ]]; then
                zone_pids+=("${proc_entry##*/}")
            fi
        done

        [[ "${#zone_pids[@]}" -eq 1 ]] && break
        sleep 0.2
    done

    [[ "${#zone_pids[@]}" -eq 1 ]] || fail "expected exactly one ZoneEngine validation process"
    local command_path
    command_path="$(readlink -f "/proc/${zone_pids[0]}/exe" 2>/dev/null || true)"
    [[ "${command_path}" == "$(readlink -f "${CURRENT_LINK}/ZoneEngine")" ]] \
        || fail "ZoneEngine validation process is not the expected apphost"
}

require_inactive_success()
{
    [[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
        || fail "service is not inactive"
    [[ "$(systemctl show "${SERVICE_NAME}" --property=Result --value)" == "success" ]] \
        || fail "service result is not success"
}

validate_lifecycle()
{
    systemctl reset-failed "${SERVICE_NAME}" >/dev/null 2>&1 || true
    systemctl start "${SERVICE_NAME}"
    require_active
    systemctl status "${SERVICE_NAME}" --no-pager --lines=0 >/dev/null
    echo "ZONE_STAGE9_SYSTEMD_START_OK"
    echo "ZONE_STAGE9_SYSTEMD_STATUS_OK"

    systemctl stop "${SERVICE_NAME}"
    require_inactive_success
    echo "ZONE_STAGE9_SYSTEMD_STOP_OK"

    systemctl start "${SERVICE_NAME}"
    require_active
    systemctl restart "${SERVICE_NAME}"
    require_active
    echo "ZONE_STAGE9_SYSTEMD_RESTART_OK"

    systemctl stop "${SERVICE_NAME}"
    require_inactive_success
}

validate_controlled_failure()
{
    install -d -o root -g root -m 0755 "${FAILURE_DROP_IN_DIRECTORY}"
    printf '%s\n' \
        '[Service]' \
        'Environment=AO_REBIRTH_ZONE_LISTEN_IP=0.0.0.0' \
        'Restart=no' \
        > "${FAILURE_DROP_IN_FILE}"
    chown root:root "${FAILURE_DROP_IN_FILE}"
    chmod 0644 "${FAILURE_DROP_IN_FILE}"
    systemctl daemon-reload
    systemctl reset-failed "${SERVICE_NAME}" >/dev/null 2>&1 || true

    set +e
    systemctl start "${SERVICE_NAME}" >/dev/null 2>&1
    local start_status=$?
    set -e
    [[ "${start_status}" -ne 0 ]] || fail "controlled invalid configuration unexpectedly started"
    [[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "failed" ]] \
        || fail "controlled invalid configuration did not report failed"
    echo "ZONE_STAGE9_SYSTEMD_CONTROLLED_FAILURE_OK"

    rm -f -- "${FAILURE_DROP_IN_FILE}"
    rmdir -- "${FAILURE_DROP_IN_DIRECTORY}" >/dev/null 2>&1 || true
    systemctl daemon-reload
    systemctl reset-failed "${SERVICE_NAME}" >/dev/null 2>&1 || true
}

main()
{
    require_root
    [[ "$#" -eq 3 ]] || fail "usage: validate-disabled-service.sh <publish-dir> <unit-file> <stage9-release-id>"
    local publish_dir
    local unit_file
    publish_dir="$(realpath -e -- "$1")"
    unit_file="$(realpath -e -- "$2")"

    systemctl stop "${SERVICE_NAME}" >/dev/null 2>&1 || true
    [[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" != "enabled" ]] \
        || fail "ZoneEngine service must not be enabled during Stage 9 validation"

    trap 'systemctl stop "${SERVICE_NAME}" >/dev/null 2>&1 || true' EXIT
    install_release "${publish_dir}" "${unit_file}" "$3"
    validate_lifecycle
    validate_controlled_failure

    systemctl stop "${SERVICE_NAME}" >/dev/null 2>&1 || true
    [[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" != "enabled" ]] \
        || fail "ZoneEngine service was enabled unexpectedly"
    trap - EXIT
    echo "PASS: ZoneEngine Stage 9 disabled service validation completed; service=disabled/inactive."
}

main "$@"
