#!/usr/bin/env bash
set -euo pipefail

readonly SERVICE_NAME="ao-rebirth-chatengine.service"
readonly ENVIRONMENT_FILE="/etc/ao-rebirth/chatengine/chatengine.env"
readonly TEST_ENVIRONMENT_FILE="/etc/ao-rebirth/chatengine/stage6/chatengine.env"
readonly DROP_IN_DIRECTORY="/run/systemd/system/${SERVICE_NAME}.d"
readonly DROP_IN_FILE="${DROP_IN_DIRECTORY}/stage6-validation.conf"
readonly DROP_IN_TEMP="${DROP_IN_DIRECTORY}/stage6-validation.conf.tmp"
readonly RECOVERY_ARGUMENT="--recover-stage6-validation"

fail()
{
    echo "FAIL: $*" >&2
    exit 1
}

drop_in_paths_are_safe()
{
    local directory_mode
    local file_mode
    local file_path

    if [[ -e "${DROP_IN_DIRECTORY}" || -L "${DROP_IN_DIRECTORY}" ]]; then
        [[ -d "${DROP_IN_DIRECTORY}" && ! -L "${DROP_IN_DIRECTORY}" ]] || return 1
        [[ "$(stat -c '%U:%G' "${DROP_IN_DIRECTORY}")" == "root:root" ]] || return 1
        directory_mode="$(stat -c '%a' "${DROP_IN_DIRECTORY}")"
        (( (8#${directory_mode} & 0022) == 0 )) || return 1
    fi

    for file_path in "${DROP_IN_TEMP}" "${DROP_IN_FILE}"; do
        if [[ -e "${file_path}" || -L "${file_path}" ]]; then
            [[ -f "${file_path}" && ! -L "${file_path}" ]] || return 1
            [[ "$(stat -c '%U:%G' "${file_path}")" == "root:root" ]] || return 1
            file_mode="$(stat -c '%a' "${file_path}")"
            (( (8#${file_mode} & 0022) == 0 )) || return 1
        fi
    done

    if [[ -f "${DROP_IN_FILE}" ]]; then
        [[ "$(cat -- "${DROP_IN_FILE}")" == $'[Service]\nEnvironmentFile=/etc/ao-rebirth/chatengine/stage6/chatengine.env\nRuntimeMaxSec=90s\nRestart=no' ]] \
            || return 1
    fi

    return 0
}

restore_test_state()
{
    local cleanup_failed=false

    if ! systemctl stop "${SERVICE_NAME}" >/dev/null 2>&1; then
        cleanup_failed=true
    fi
    if [[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" != "inactive" ]]; then
        cleanup_failed=true
    fi

    if drop_in_paths_are_safe; then
        rm -f -- "${DROP_IN_TEMP}" "${DROP_IN_FILE}"
        rmdir -- "${DROP_IN_DIRECTORY}" >/dev/null 2>&1 || true
        if ! systemctl daemon-reload >/dev/null 2>&1; then
            cleanup_failed=true
        fi
    else
        cleanup_failed=true
    fi

    [[ "${cleanup_failed}" == "false" ]]
}

cleanup_on_exit()
{
    local exit_status=$?

    trap - EXIT INT TERM
    if ! restore_test_state; then
        echo "FAIL: Stage 6 validation cleanup did not restore an inactive service" >&2
        exit_status=1
    fi
    exit "${exit_status}"
}

require_exact_loopback_listener()
{
    local port="$1"
    local socket_state
    local receive_queue
    local send_queue
    local local_address
    local peer_address
    local -a socket_lines

    mapfile -t socket_lines < <(ss -H -ltn "sport = :${port}")
    [[ "${#socket_lines[@]}" -eq 1 ]] || fail "port ${port} does not have exactly one listener"
    read -r socket_state receive_queue send_queue local_address peer_address \
        <<< "${socket_lines[0]}"
    [[ "${socket_state}" == "LISTEN" && "${local_address}" == "127.0.0.1:${port}" ]] \
        || fail "port ${port} is not bound exactly to IPv4 loopback"
}

if [[ "${EUID}" -ne 0 ]]; then
    fail "run as root"
fi

[[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" == "disabled" ]] \
    || fail "the ChatEngine service must remain disabled during Stage 6"

if [[ "$#" -eq 1 && "$1" == "${RECOVERY_ARGUMENT}" ]]; then
    drop_in_paths_are_safe || fail "the runtime Stage 6 drop-in paths are unsafe"
    restore_test_state || fail "recovery did not restore an inactive service"
    [[ -z "$(ss -H -ltn "sport = :7012")" ]] || fail "the player Chat listener remained open"
    [[ -z "$(ss -H -ltn "sport = :6996")" ]] || fail "the ISCom listener remained open"
    echo "PASS: exact Stage 6 validation state was recovered; service=disabled/inactive listeners=0."
    exit 0
fi

[[ "$#" -eq 0 ]] || fail "usage: validate-disabled-service.sh [${RECOVERY_ARGUMENT}]"
[[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
    || fail "the ChatEngine service must be inactive before Stage 6 validation"
[[ -f "${ENVIRONMENT_FILE}" ]] || fail "the original ChatEngine environment is missing"
[[ -f "${TEST_ENVIRONMENT_FILE}" ]] || fail "the Stage 6 test environment is missing"
[[ "$(stat -c '%U:%G:%a' "${TEST_ENVIRONMENT_FILE}")" == "root:root:600" ]] \
    || fail "the Stage 6 test environment must be root-owned mode 0600"
[[ ! -e "${DROP_IN_FILE}" && ! -L "${DROP_IN_FILE}" \
    && ! -e "${DROP_IN_TEMP}" && ! -L "${DROP_IN_TEMP}" ]] \
    || fail "the exact runtime Stage 6 drop-in path already exists"

trap cleanup_on_exit EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
drop_in_paths_are_safe || fail "the runtime Stage 6 drop-in path is unsafe"
if [[ ! -e "${DROP_IN_DIRECTORY}" ]]; then
    install -d -o root -g root -m 0755 "${DROP_IN_DIRECTORY}"
fi
drop_in_paths_are_safe || fail "the runtime Stage 6 drop-in directory is unsafe"
umask 022
(
    set -o noclobber
    printf '%s\n' \
        '[Service]' \
        "EnvironmentFile=${TEST_ENVIRONMENT_FILE}" \
        'RuntimeMaxSec=90s' \
        'Restart=no' \
        > "${DROP_IN_TEMP}"
)
chown root:root "${DROP_IN_TEMP}"
chmod 0644 "${DROP_IN_TEMP}"
mv -T -- "${DROP_IN_TEMP}" "${DROP_IN_FILE}"
drop_in_paths_are_safe || fail "the runtime Stage 6 drop-in content is unsafe"

systemctl daemon-reload

systemctl start "${SERVICE_NAME}"
[[ "$(systemctl is-active "${SERVICE_NAME}")" == "active" ]] \
    || fail "ChatEngine did not reach Type=notify readiness"
[[ "$(systemctl show "${SERVICE_NAME}" --property=Type --value)" == "notify" ]] \
    || fail "ChatEngine is not running as Type=notify"

require_exact_loopback_listener 7012
require_exact_loopback_listener 6996

systemctl stop "${SERVICE_NAME}"
[[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
    || fail "ChatEngine did not stop cleanly"
[[ "$(systemctl show "${SERVICE_NAME}" --property=Result --value)" == "success" ]] \
    || fail "ChatEngine service result was not success"
[[ -z "$(ss -H -ltn "sport = :7012")" ]] || fail "the player Chat listener remained open"
[[ -z "$(ss -H -ltn "sport = :6996")" ]] || fail "the ISCom listener remained open"

restore_test_state
trap - EXIT INT TERM

[[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" == "disabled" ]] \
    || fail "the ChatEngine service was enabled unexpectedly"

echo "PASS: disabled ChatEngine passed live database preflight, Type=notify readiness, loopback listeners, and clean SIGTERM shutdown."
