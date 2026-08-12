#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C

readonly SERVICE_NAME="ao-rebirth-loginengine.service"
readonly UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}"
readonly ENVIRONMENT_FILE="/etc/ao-rebirth/loginengine/loginengine.env"
readonly CONFIGURATION_FILE="/etc/ao-rebirth/loginengine/Config.xml"
readonly EXECUTABLE_PATH="/opt/ao-rebirth/loginengine/current/LoginEngine"
readonly RELEASES_DIRECTORY="/opt/ao-rebirth/loginengine/releases"
readonly DROP_IN_DIRECTORY="/run/systemd/system/${SERVICE_NAME}.d"
readonly DROP_IN_FILE="${DROP_IN_DIRECTORY}/stage7-validation.conf"
readonly DROP_IN_TEMP="${DROP_IN_DIRECTORY}/stage7-validation.conf.tmp"
readonly RECOVERY_ARGUMENT="--recover-stage7-validation"
readonly LOGIN_PORT="7500"
readonly EXPECTED_DATABASE="aorebirth_chatengine_stage6"

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
    local unexpected_path

    if [[ -e "${DROP_IN_DIRECTORY}" || -L "${DROP_IN_DIRECTORY}" ]]; then
        [[ -d "${DROP_IN_DIRECTORY}" && ! -L "${DROP_IN_DIRECTORY}" ]] || return 1
        [[ "$(stat -c '%U:%G' "${DROP_IN_DIRECTORY}")" == "root:root" ]] || return 1
        directory_mode="$(stat -c '%a' "${DROP_IN_DIRECTORY}")"
        (( (8#${directory_mode} & 0022) == 0 )) || return 1
        unexpected_path="$(find "${DROP_IN_DIRECTORY}" -mindepth 1 -maxdepth 1 \
            ! -path "${DROP_IN_TEMP}" ! -path "${DROP_IN_FILE}" -print -quit)" || return 1
        [[ -z "${unexpected_path}" ]] || return 1
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
        [[ "$(cat -- "${DROP_IN_FILE}")" == $'[Service]\nRuntimeMaxSec=90s\nRestart=no' ]] \
            || return 1
    fi

    return 0
}

assert_no_login_listener()
{
    local socket_output

    socket_output="$(ss -H -ltn "sport = :${LOGIN_PORT}")" \
        || fail "could not inspect TCP port ${LOGIN_PORT}"
    [[ -z "${socket_output}" ]] || fail "the LoginEngine listener remained open"
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

    if [[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" != "disabled" ]]; then
        cleanup_failed=true
    fi

    [[ "${cleanup_failed}" == "false" ]]
}

cleanup_on_exit()
{
    local exit_status=$?

    trap - EXIT INT TERM
    if ! restore_test_state; then
        echo "FAIL: Stage 7 validation cleanup did not restore the disabled inactive service" >&2
        exit_status=1
    fi
    exit "${exit_status}"
}

require_root_secret_file()
{
    local file_path="$1"

    [[ -f "${file_path}" && ! -L "${file_path}" ]] \
        || fail "required environment file is missing or unsafe: ${file_path}"
    [[ "$(stat -c '%U:%G:%a' "${file_path}")" == "root:root:600" ]] \
        || fail "environment files must be root-owned mode 0600"
}

require_runtime_environment_file()
{
    local connection_assignment
    local file_path="$1"
    local -a environment_lines

    require_root_secret_file "${file_path}"
    mapfile -t environment_lines < "${file_path}"
    [[ "${#environment_lines[@]}" -eq 5 ]] \
        || fail "the LoginEngine environment must contain exactly five canonical assignments"
    [[ "${environment_lines[0]}" == "AO_REBIRTH_CONFIG_PATH=${CONFIGURATION_FILE}" ]] \
        || fail "the LoginEngine environment has a noncanonical configuration path"
    [[ "${environment_lines[1]}" == "AO_REBIRTH_LOGIN_LISTEN_IP=127.0.0.1" ]] \
        || fail "the LoginEngine environment has a noncanonical listener address"
    [[ "${environment_lines[2]}" == "AO_REBIRTH_REQUIRED_SQL_TYPE=MySql" ]] \
        || fail "the LoginEngine environment has a noncanonical database provider"
    [[ "${environment_lines[3]}" == "AO_REBIRTH_EXPECTED_DATABASE=${EXPECTED_DATABASE}" ]] \
        || fail "the LoginEngine environment has a noncanonical expected database"
    connection_assignment="${environment_lines[4]}"
    [[ "${connection_assignment}" == "AO_REBIRTH_MYSQL_CONNECTION=Server=127.0.0.1;Port=33067;Database=${EXPECTED_DATABASE};"* \
        && "${connection_assignment}" == *';SslMode=None' ]] \
        || fail "the LoginEngine environment has a noncanonical local database connection"
}

require_installed_configuration()
{
    local login_port_count

    [[ -f "${CONFIGURATION_FILE}" && ! -L "${CONFIGURATION_FILE}" ]] \
        || fail "the LoginEngine configuration is missing or unsafe"
    [[ "$(stat -c '%U:%G:%a' "${CONFIGURATION_FILE}")" == "root:aorebirth:640" ]] \
        || fail "the LoginEngine configuration must be root:aorebirth mode 0640"
    login_port_count="$(grep -Fxc -- '  <LoginPort>7500</LoginPort>' "${CONFIGURATION_FILE}" || true)"
    [[ "${login_port_count}" == "1" ]] \
        || fail "the installed LoginEngine configuration must contain canonical TCP port 7500"
}

require_installed_unit()
{
    local database_guard_count
    local loopback_guard_count

    [[ -f "${UNIT_FILE}" && ! -L "${UNIT_FILE}" ]] \
        || fail "the LoginEngine systemd unit is missing or unsafe"
    [[ "$(stat -c '%U:%G:%a' "${UNIT_FILE}")" == "root:root:644" ]] \
        || fail "the LoginEngine systemd unit must be root-owned mode 0644"
    database_guard_count="$(grep -Fxc -- \
        'ExecStartPre=/usr/bin/test ${AO_REBIRTH_EXPECTED_DATABASE} = aorebirth_chatengine_stage6' \
        "${UNIT_FILE}" || true)"
    [[ "${database_guard_count}" == "1" ]] \
        || fail "the installed unit lacks the exact effective database guard"
    loopback_guard_count="$(grep -Fxc -- \
        'ExecStartPre=/usr/bin/test ${AO_REBIRTH_LOGIN_LISTEN_IP} = 127.0.0.1' \
        "${UNIT_FILE}" || true)"
    [[ "${loopback_guard_count}" == "1" ]] \
        || fail "the installed unit lacks the exact effective loopback guard"
    systemd-analyze verify "${UNIT_FILE}" \
        || fail "the LoginEngine systemd unit did not pass systemd-analyze verify"
}

require_effective_property()
{
    local actual_value
    local expected_value="$2"
    local property_name="$1"

    actual_value="$(systemctl show "${SERVICE_NAME}" --property="${property_name}" --value)" \
        || fail "could not read effective systemd property ${property_name}"
    [[ "${actual_value}" == "${expected_value}" ]] \
        || fail "effective systemd property ${property_name} is not canonical"
}

count_occurrences()
{
    local needle="$2"
    local remaining="$1"

    OCCURRENCE_COUNT=0
    while [[ "${remaining}" == *"${needle}"* ]]; do
        remaining="${remaining#*"${needle}"}"
        ((OCCURRENCE_COUNT += 1))
    done
}

require_effective_exec_commands()
{
    local database_command
    local database_guard
    local exec_start
    local exec_start_pre
    local loopback_guard
    local startup_command

    exec_start="$(systemctl show "${SERVICE_NAME}" --property=ExecStart --value)" \
        || fail "could not read the effective LoginEngine ExecStart"
    count_occurrences "${exec_start}" 'argv[]='
    [[ "${OCCURRENCE_COUNT}" -eq 1 \
        && "${exec_start}" == *'path=/opt/ao-rebirth/loginengine/current/LoginEngine ; argv[]=/opt/ao-rebirth/loginengine/current/LoginEngine --headless ; ignore_errors=no ;'* ]] \
        || fail "the effective LoginEngine ExecStart is not canonical"

    database_guard='path=/usr/bin/test ; argv[]=/usr/bin/test ${AO_REBIRTH_EXPECTED_DATABASE} = aorebirth_chatengine_stage6 ; ignore_errors=no ;'
    loopback_guard='path=/usr/bin/test ; argv[]=/usr/bin/test ${AO_REBIRTH_LOGIN_LISTEN_IP} = 127.0.0.1 ; ignore_errors=no ;'
    startup_command='path=/opt/ao-rebirth/loginengine/current/LoginEngine ; argv[]=/opt/ao-rebirth/loginengine/current/LoginEngine --validate-startup ; ignore_errors=no ;'
    database_command='path=/opt/ao-rebirth/loginengine/current/LoginEngine ; argv[]=/opt/ao-rebirth/loginengine/current/LoginEngine --validate-database ; ignore_errors=no ;'
    exec_start_pre="$(systemctl show "${SERVICE_NAME}" --property=ExecStartPre --value)" \
        || fail "could not read the effective LoginEngine ExecStartPre"
    count_occurrences "${exec_start_pre}" 'argv[]='
    [[ "${OCCURRENCE_COUNT}" -eq 4 \
        && "${exec_start_pre}" == *"${database_guard}"*"${loopback_guard}"*"${startup_command}"*"${database_command}"* ]] \
        || fail "the effective LoginEngine ExecStartPre sequence is not canonical"
}

require_effective_unit_environment()
{
    local actual_environment
    local assignment
    local found_database=false
    local found_listen=false
    local found_provider=false
    local -a assignments

    actual_environment="$(systemctl show "${SERVICE_NAME}" --property=Environment --value)" \
        || fail "could not read the effective LoginEngine Environment"
    read -r -a assignments <<< "${actual_environment}"
    [[ "${#assignments[@]}" -eq 3 ]] \
        || fail "the effective LoginEngine Environment is not canonical"
    for assignment in "${assignments[@]}"; do
        case "${assignment}" in
            AO_REBIRTH_REQUIRED_SQL_TYPE=MySql)
                found_provider=true
                ;;
            AO_REBIRTH_EXPECTED_DATABASE=aorebirth_chatengine_stage6)
                found_database=true
                ;;
            AO_REBIRTH_LOGIN_LISTEN_IP=127.0.0.1)
                found_listen=true
                ;;
            *)
                fail "the effective LoginEngine Environment contains an unexpected assignment"
                ;;
        esac
    done
    [[ "${found_provider}" == "true" \
        && "${found_database}" == "true" \
        && "${found_listen}" == "true" ]] \
        || fail "the effective LoginEngine Environment lacks a canonical assignment"
}

require_effective_installed_unit()
{
    local expected_drop_in="$1"
    local expected_restart="$2"

    require_effective_property FragmentPath "${UNIT_FILE}"
    require_effective_property DropInPaths "${expected_drop_in}"
    require_effective_property User aorebirth
    require_effective_property Group aorebirth
    require_effective_property Type notify
    require_effective_property NotifyAccess main
    require_effective_property WorkingDirectory /opt/ao-rebirth/loginengine/current
    require_effective_property EnvironmentFiles \
        '/etc/ao-rebirth/loginengine/loginengine.env (ignore_errors=no)'
    require_effective_unit_environment
    require_effective_property Restart "${expected_restart}"
    require_effective_property NoNewPrivileges yes
    require_effective_property PrivateTmp yes
    require_effective_property ProtectSystem strict
    require_effective_property ProtectHome yes
    require_effective_property ProtectKernelTunables yes
    require_effective_property ProtectKernelModules yes
    require_effective_property ProtectKernelLogs yes
    require_effective_property ProtectControlGroups yes
    require_effective_property RestrictSUIDSGID yes
    require_effective_property RestrictRealtime yes
    require_effective_property RestrictNamespaces yes
    require_effective_property LockPersonality yes
    require_effective_property RestrictAddressFamilies 'AF_INET AF_INET6 AF_UNIX'
    require_effective_exec_commands
}

resolve_expected_executable()
{
    local executable_mode
    local resolved_executable
    local resolved_releases

    resolved_executable="$(realpath -e -- "${EXECUTABLE_PATH}")" \
        || fail "the LoginEngine apphost could not be resolved"
    resolved_releases="$(realpath -e -- "${RELEASES_DIRECTORY}")" \
        || fail "the LoginEngine releases directory could not be resolved"
    [[ "${resolved_executable}" == "${resolved_releases}"/* ]] \
        || fail "the LoginEngine apphost is outside the guarded releases directory"
    [[ -f "${resolved_executable}" && -x "${resolved_executable}" ]] \
        || fail "the LoginEngine apphost is not a regular executable"
    [[ "$(stat -c '%U:%G' "${resolved_executable}")" == "root:root" ]] \
        || fail "the LoginEngine apphost must be root-owned"
    executable_mode="$(stat -c '%a' "${resolved_executable}")"
    (( (8#${executable_mode} & 0022) == 0 )) \
        || fail "the LoginEngine apphost must not be group/world writable"

    printf '%s\n' "${resolved_executable}"
}

require_exact_loopback_listener()
{
    local actual_executable
    local expected_executable="$1"
    local local_address
    local main_pid
    local peer_address
    local process_info
    local receive_queue
    local send_queue
    local socket_output
    local socket_state
    local -a socket_lines

    main_pid="$(systemctl show "${SERVICE_NAME}" --property=MainPID --value)" \
        || fail "could not read the LoginEngine MainPID"
    [[ "${main_pid}" =~ ^[1-9][0-9]*$ ]] \
        || fail "LoginEngine does not have a valid MainPID"

    actual_executable="$(readlink -f -- "/proc/${main_pid}/exe")" \
        || fail "could not resolve the LoginEngine MainPID executable"
    [[ "${actual_executable}" == "${expected_executable}" ]] \
        || fail "the LoginEngine MainPID is not the installed apphost"

    socket_output="$(ss -H -ltnp "sport = :${LOGIN_PORT}")" \
        || fail "could not inspect TCP port ${LOGIN_PORT}"
    [[ -n "${socket_output}" ]] || fail "the LoginEngine listener was not created"
    mapfile -t socket_lines <<< "${socket_output}"
    [[ "${#socket_lines[@]}" -eq 1 ]] \
        || fail "port ${LOGIN_PORT} does not have exactly one listener"
    read -r socket_state receive_queue send_queue local_address peer_address process_info \
        <<< "${socket_lines[0]}"
    [[ "${socket_state}" == "LISTEN" && "${local_address}" == "127.0.0.1:${LOGIN_PORT}" ]] \
        || fail "port ${LOGIN_PORT} is not bound exactly to IPv4 loopback"
    [[ "${process_info}" == *"pid=${main_pid},"* ]] \
        || fail "port ${LOGIN_PORT} is not owned by the LoginEngine MainPID"
}

if [[ "${EUID}" -ne 0 ]]; then
    fail "run as root"
fi

[[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" == "disabled" ]] \
    || fail "the LoginEngine service must remain disabled during Stage 7"

if [[ "$#" -eq 1 && "$1" == "${RECOVERY_ARGUMENT}" ]]; then
    drop_in_paths_are_safe || fail "the runtime Stage 7 drop-in paths are unsafe"
    restore_test_state || fail "recovery did not restore the disabled inactive service"
    assert_no_login_listener
    echo "PASS: exact Stage 7 validation state was recovered; service=disabled/inactive listeners=0."
    exit 0
fi

[[ "$#" -eq 0 ]] || fail "usage: validate-disabled-service.sh [${RECOVERY_ARGUMENT}]"
[[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
    || fail "the LoginEngine service must be inactive before Stage 7 validation"
require_runtime_environment_file "${ENVIRONMENT_FILE}"
require_installed_configuration
require_installed_unit
expected_executable="$(resolve_expected_executable)"
systemctl daemon-reload
require_effective_installed_unit "" on-failure
[[ ! -e "${DROP_IN_FILE}" && ! -L "${DROP_IN_FILE}" \
    && ! -e "${DROP_IN_TEMP}" && ! -L "${DROP_IN_TEMP}" ]] \
    || fail "the exact runtime Stage 7 drop-in path already exists"

trap cleanup_on_exit EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
drop_in_paths_are_safe || fail "the runtime Stage 7 drop-in path is unsafe"
if [[ ! -e "${DROP_IN_DIRECTORY}" ]]; then
    install -d -o root -g root -m 0755 "${DROP_IN_DIRECTORY}"
fi
drop_in_paths_are_safe || fail "the runtime Stage 7 drop-in directory is unsafe"
umask 022
(
    set -o noclobber
    printf '%s\n' \
        '[Service]' \
        'RuntimeMaxSec=90s' \
        'Restart=no' \
        > "${DROP_IN_TEMP}"
)
chown root:root "${DROP_IN_TEMP}"
chmod 0644 "${DROP_IN_TEMP}"
mv -T -- "${DROP_IN_TEMP}" "${DROP_IN_FILE}"
drop_in_paths_are_safe || fail "the runtime Stage 7 drop-in content is unsafe"

systemctl daemon-reload
require_effective_installed_unit "${DROP_IN_FILE}" no

systemctl start "${SERVICE_NAME}"
[[ "$(systemctl is-active "${SERVICE_NAME}")" == "active" ]] \
    || fail "LoginEngine did not reach Type=notify readiness"
[[ "$(systemctl show "${SERVICE_NAME}" --property=Type --value)" == "notify" ]] \
    || fail "LoginEngine is not running as Type=notify"

require_exact_loopback_listener "${expected_executable}"

systemctl stop "${SERVICE_NAME}"
[[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
    || fail "LoginEngine did not stop cleanly"
[[ "$(systemctl show "${SERVICE_NAME}" --property=Result --value)" == "success" ]] \
    || fail "LoginEngine service result was not success"
exec_main_code="$(systemctl show "${SERVICE_NAME}" --property=ExecMainCode --value)"
[[ "${exec_main_code}" == "exited" || "${exec_main_code}" == "1" || "${exec_main_code}" == "0" ]] \
    || fail "LoginEngine did not handle SIGTERM as a clean process exit"
[[ "$(systemctl show "${SERVICE_NAME}" --property=ExecMainStatus --value)" == "0" ]] \
    || fail "LoginEngine returned a nonzero status after SIGTERM"
assert_no_login_listener

restore_test_state
trap - EXIT INT TERM

[[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" == "disabled" ]] \
    || fail "the LoginEngine service was enabled unexpectedly"

echo "PASS: disabled LoginEngine passed live database preflight, Type=notify readiness, exact loopback listener ownership, and clean SIGTERM shutdown."
