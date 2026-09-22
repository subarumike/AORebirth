#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C

readonly SERVICE_NAME="ao-rebirth-loginengine.service"
readonly UPLOAD_DIRECTORY="/tmp/ao-rebirth-loginengine-publish"
readonly UPLOADED_UNIT="/tmp/ao-rebirth-loginengine.service"
readonly INSTALL_ROOT="/opt/ao-rebirth/loginengine"
readonly RELEASES_DIRECTORY="${INSTALL_ROOT}/releases"
readonly CURRENT_LINK="${INSTALL_ROOT}/current"
readonly CONFIGURATION_DIRECTORY="/etc/ao-rebirth/loginengine"
readonly CONFIGURATION_FILE="${CONFIGURATION_DIRECTORY}/Config.xml"
readonly ENVIRONMENT_FILE="${CONFIGURATION_DIRECTORY}/loginengine.env"
readonly UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}"
readonly DROP_IN_DIRECTORY="/run/systemd/system/${SERVICE_NAME}.d"
readonly RUNTIME_MASK_PATH="/run/systemd/system/${SERVICE_NAME}"
readonly OPERATION_LOCK="/run/aorebirth-loginengine-stage7-upgrade.lock"
readonly EXPECTED_DATABASE="aorebirth_chatengine_stage6"
readonly LOGIN_PORT="7500"

temporary_directory=""
unit_candidate=""
unit_backup=""
unit_canonical=""
unit_installed_normalized=""
unit_swap=""
current_swap=""
release_name=""
release_target=""
release_staging=""
old_release_target=""
old_release_name=""
environment_metadata_before=""
release_move_attempted=false
unit_update_attempted=false
current_switch_attempted=false
runtime_mask_attempted=false
runtime_mask_owned=false
upgrade_complete=false

fail()
{
    echo "FAIL: $*" >&2
    exit 1
}

require_exact_line()
{
    local count
    local expected_line="$2"
    local file_path="$1"

    count="$(grep -Fxc -- "${expected_line}" "${file_path}" || true)"
    [[ "${count}" == "1" ]] \
        || fail "required canonical deployment line is missing or duplicated"
}

require_safe_directory()
{
    local directory_mode
    local expected_owner="$2"
    local path="$1"

    [[ -d "${path}" && ! -L "${path}" ]] || fail "directory is missing or unsafe: ${path}"
    [[ "$(realpath -e -- "${path}")" == "${path}" ]] \
        || fail "directory does not resolve exactly: ${path}"
    [[ "$(stat -c '%U:%G' "${path}")" == "${expected_owner}" ]] \
        || fail "directory ownership is unsafe: ${path}"
    directory_mode="$(stat -c '%a' "${path}")"
    (( (8#${directory_mode} & 0022) == 0 )) \
        || fail "directory is group/world writable: ${path}"
}

require_safe_regular_file()
{
    local expected_metadata="$2"
    local path="$1"

    [[ -f "${path}" && ! -L "${path}" ]] || fail "file is missing or unsafe: ${path}"
    [[ "$(realpath -e -- "${path}")" == "${path}" ]] \
        || fail "file does not resolve exactly: ${path}"
    [[ "$(stat -c '%U:%G:%a' "${path}")" == "${expected_metadata}" ]] \
        || fail "file metadata is unsafe: ${path}"
    [[ "$(stat -c '%h' "${path}")" == "1" ]] \
        || fail "file must not be hard-linked: ${path}"
}

require_safe_uploaded_file()
{
    local file_mode
    local path="$1"

    [[ -f "${path}" && ! -L "${path}" ]] || fail "uploaded file is missing or unsafe: ${path}"
    [[ "$(stat -c '%U:%G' "${path}")" == "root:root" ]] \
        || fail "uploaded file must be root-owned: ${path}"
    [[ "$(stat -c '%h' "${path}")" == "1" ]] \
        || fail "uploaded file must not be hard-linked: ${path}"
    file_mode="$(stat -c '%a' "${path}")"
    (( (8#${file_mode} & 0022) == 0 )) \
        || fail "uploaded file is group/world writable: ${path}"
}

path_has_no_nested_mount()
{
    local mount_target
    local path="$1"
    local mount_targets

    mount_targets="$(findmnt -rn -o TARGET)" || return 1
    while IFS= read -r mount_target; do
        case "${mount_target}" in
            "${path}"|"${path}"/*)
                return 1
                ;;
        esac
    done <<< "${mount_targets}"

    return 0
}

require_no_nested_mount()
{
    local path="$1"

    path_has_no_nested_mount "${path}" \
        || fail "path contains a mount-point alias: ${path}"
}

require_safe_tree()
{
    local hard_link_output
    local special_output
    local symlink_output
    local tree_root="$1"
    local unsafe_mode_output
    local unsafe_owner_output

    require_safe_directory "${tree_root}" "root:root"
    require_no_nested_mount "${tree_root}"
    symlink_output="$(find "${tree_root}" -type l -print -quit)" \
        || fail "could not inspect tree for symlinks"
    [[ -z "${symlink_output}" ]] || fail "tree contains a symlink"
    special_output="$(find "${tree_root}" ! -type d ! -type f -print -quit)" \
        || fail "could not inspect tree for special files"
    [[ -z "${special_output}" ]] || fail "tree contains a special file"
    hard_link_output="$(find "${tree_root}" -type f -links +1 -print -quit)" \
        || fail "could not inspect tree for hard links"
    [[ -z "${hard_link_output}" ]] || fail "tree contains a hard-linked file"
    unsafe_owner_output="$(find "${tree_root}" \( ! -user root -o ! -group root \) -print -quit)" \
        || fail "could not inspect tree ownership"
    [[ -z "${unsafe_owner_output}" ]] || fail "tree contains a non-root-owned path"
    unsafe_mode_output="$(find "${tree_root}" -perm /022 -print -quit)" \
        || fail "could not inspect tree permissions"
    [[ -z "${unsafe_mode_output}" ]] || fail "tree contains a group/world-writable path"
}

require_package()
{
    local apphost_description
    local package_root="$1"
    local require_executable="${2:-true}"
    local required_name

    require_safe_tree "${package_root}"
    for required_name in \
        LoginEngine \
        LoginEngine.dll \
        LoginEngine.deps.json \
        LoginEngine.runtimeconfig.json \
        Config.xml \
        libcoreclr.so \
        libhostfxr.so \
        libhostpolicy.so \
        System.Private.CoreLib.dll; do
        require_safe_uploaded_file "${package_root}/${required_name}"
    done

    if [[ "${require_executable}" == "true" ]]; then
        [[ -x "${package_root}/LoginEngine" ]] \
            || fail "the LoginEngine apphost is not executable"
    fi
    apphost_description="$(file -b -- "${package_root}/LoginEngine")" \
        || fail "could not inspect the LoginEngine apphost"
    [[ "${apphost_description}" == *"ELF 64-bit"* && "${apphost_description}" == *"x86-64"* ]] \
        || fail "the LoginEngine apphost is not Linux x86_64"

    require_exact_line "${package_root}/Config.xml" \
        "  <MysqlConnection>Server=localhost;Database=cellao_codex_clean;Uid=cellaodbuser;Pwd=REPLACE_WITH_LOCAL_PASSWORD</MysqlConnection>"
    require_exact_line "${package_root}/Config.xml" "  <LoginPort>7500</LoginPort>"
    require_exact_line "${package_root}/Config.xml" "  <SQLType>MySql</SQLType>"
}

require_unit_contract()
{
    local execution_directive_count
    local unit_path="$1"

    require_exact_line "${unit_path}" "[Unit]"
    require_exact_line "${unit_path}" "Description=AORebirth LoginEngine"
    require_exact_line "${unit_path}" "Wants=network-online.target"
    require_exact_line "${unit_path}" "After=network-online.target"
    require_exact_line "${unit_path}" "[Service]"
    require_exact_line "${unit_path}" "Type=notify"
    require_exact_line "${unit_path}" "NotifyAccess=main"
    require_exact_line "${unit_path}" "User=aorebirth"
    require_exact_line "${unit_path}" "Group=aorebirth"
    require_exact_line "${unit_path}" "WorkingDirectory=/opt/ao-rebirth/loginengine/current"
    require_exact_line "${unit_path}" 'Environment=AO_REBIRTH_REQUIRED_SQL_TYPE=MySql'
    require_exact_line "${unit_path}" \
        "Environment=AO_REBIRTH_EXPECTED_DATABASE=${EXPECTED_DATABASE}"
    require_exact_line "${unit_path}" 'Environment=AO_REBIRTH_LOGIN_LISTEN_IP=127.0.0.1'
    require_exact_line "${unit_path}" \
        'EnvironmentFile=/etc/ao-rebirth/loginengine/loginengine.env'
    require_exact_line "${unit_path}" \
        'ExecStartPre=/usr/bin/test ${AO_REBIRTH_EXPECTED_DATABASE} = aorebirth_chatengine_stage6'
    require_exact_line "${unit_path}" \
        'ExecStartPre=/usr/bin/test ${AO_REBIRTH_LOGIN_LISTEN_IP} = 127.0.0.1'
    require_exact_line "${unit_path}" \
        'ExecStartPre=/opt/ao-rebirth/loginengine/current/LoginEngine --validate-startup'
    require_exact_line "${unit_path}" \
        'ExecStartPre=/opt/ao-rebirth/loginengine/current/LoginEngine --validate-database'
    require_exact_line "${unit_path}" \
        'ExecStart=/opt/ao-rebirth/loginengine/current/LoginEngine --headless'
    require_exact_line "${unit_path}" "Restart=on-failure"
    require_exact_line "${unit_path}" "RestartSec=5s"
    require_exact_line "${unit_path}" "TimeoutStartSec=30s"
    require_exact_line "${unit_path}" "TimeoutStopSec=45s"
    require_exact_line "${unit_path}" "KillSignal=SIGTERM"
    require_exact_line "${unit_path}" "SuccessExitStatus=0"
    require_exact_line "${unit_path}" "UMask=0077"
    require_exact_line "${unit_path}" "NoNewPrivileges=true"
    require_exact_line "${unit_path}" "PrivateTmp=true"
    require_exact_line "${unit_path}" "ProtectSystem=strict"
    require_exact_line "${unit_path}" "ProtectHome=true"
    require_exact_line "${unit_path}" "ProtectKernelTunables=true"
    require_exact_line "${unit_path}" "ProtectKernelModules=true"
    require_exact_line "${unit_path}" "ProtectKernelLogs=true"
    require_exact_line "${unit_path}" "ProtectControlGroups=true"
    require_exact_line "${unit_path}" "RestrictSUIDSGID=true"
    require_exact_line "${unit_path}" "RestrictRealtime=true"
    require_exact_line "${unit_path}" "RestrictNamespaces=true"
    require_exact_line "${unit_path}" "LockPersonality=true"
    require_exact_line "${unit_path}" "RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6"
    require_exact_line "${unit_path}" "[Install]"
    require_exact_line "${unit_path}" "WantedBy=multi-user.target"
    execution_directive_count="$(grep -Ec \
        '^[[:space:]]*Exec[A-Za-z]*[[:space:]]*=' "${unit_path}" || true)"
    [[ "${execution_directive_count}" == "5" ]] \
        || fail "the LoginEngine unit contains an unexpected execution directive"
    [[ "$(grep -Fc 'AO_REBIRTH_MYSQL_CONNECTION' "${unit_path}" || true)" == "0" ]] \
        || fail "the LoginEngine unit must not contain a database connection"
}

write_canonical_unit()
{
    local output_path="$1"

    umask 022
    printf '%s\n' \
        '[Unit]' \
        'Description=AORebirth LoginEngine' \
        'Wants=network-online.target' \
        'After=network-online.target' \
        '' \
        '[Service]' \
        'Type=notify' \
        'NotifyAccess=main' \
        'User=aorebirth' \
        'Group=aorebirth' \
        'WorkingDirectory=/opt/ao-rebirth/loginengine/current' \
        'Environment=AO_REBIRTH_REQUIRED_SQL_TYPE=MySql' \
        'Environment=AO_REBIRTH_EXPECTED_DATABASE=aorebirth_chatengine_stage6' \
        'Environment=AO_REBIRTH_LOGIN_LISTEN_IP=127.0.0.1' \
        'EnvironmentFile=/etc/ao-rebirth/loginengine/loginengine.env' \
        'ExecStartPre=/usr/bin/test ${AO_REBIRTH_EXPECTED_DATABASE} = aorebirth_chatengine_stage6' \
        'ExecStartPre=/usr/bin/test ${AO_REBIRTH_LOGIN_LISTEN_IP} = 127.0.0.1' \
        'ExecStartPre=/opt/ao-rebirth/loginengine/current/LoginEngine --validate-startup' \
        'ExecStartPre=/opt/ao-rebirth/loginengine/current/LoginEngine --validate-database' \
        'ExecStart=/opt/ao-rebirth/loginengine/current/LoginEngine --headless' \
        'Restart=on-failure' \
        'RestartSec=5s' \
        'TimeoutStartSec=30s' \
        'TimeoutStopSec=45s' \
        'KillSignal=SIGTERM' \
        'SuccessExitStatus=0' \
        'UMask=0077' \
        'NoNewPrivileges=true' \
        'PrivateTmp=true' \
        'ProtectSystem=strict' \
        'ProtectHome=true' \
        'ProtectKernelTunables=true' \
        'ProtectKernelModules=true' \
        'ProtectKernelLogs=true' \
        'ProtectControlGroups=true' \
        'RestrictSUIDSGID=true' \
        'RestrictRealtime=true' \
        'RestrictNamespaces=true' \
        'LockPersonality=true' \
        'RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6' \
        '' \
        '[Install]' \
        'WantedBy=multi-user.target' \
        > "${output_path}"
    chown root:root "${output_path}"
    chmod 0644 "${output_path}"
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
    require_effective_property FragmentPath "${UNIT_FILE}"
    require_effective_property DropInPaths ""
    require_effective_property User aorebirth
    require_effective_property Group aorebirth
    require_effective_property Type notify
    require_effective_property NotifyAccess main
    require_effective_property WorkingDirectory /opt/ao-rebirth/loginengine/current
    require_effective_property EnvironmentFiles \
        '/etc/ao-rebirth/loginengine/loginengine.env (ignore_errors=no)'
    require_effective_unit_environment
    require_effective_property Restart on-failure
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

assert_no_login_listener()
{
    local socket_output

    socket_output="$(ss -H -ltn "sport = :${LOGIN_PORT}")" \
        || fail "could not inspect TCP port ${LOGIN_PORT}"
    [[ -z "${socket_output}" ]] || fail "TCP port ${LOGIN_PORT} must remain closed"
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
    local connection_assignment_count
    local connection_assignment
    local connection_password
    local file_path="$1"
    local expected_connection_prefix
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
    connection_assignment_count="$(grep -Ec '^AO_REBIRTH_MYSQL_CONNECTION=' "${file_path}" || true)"
    [[ "${connection_assignment_count}" == "1" ]] \
        || fail "the LoginEngine environment must contain exactly one MySQL connection assignment"
    connection_assignment="${environment_lines[4]}"
    expected_connection_prefix="AO_REBIRTH_MYSQL_CONNECTION=Server=127.0.0.1;Port=33067;Database=${EXPECTED_DATABASE};Uid=aorebirth_stage6;Pwd="
    [[ "${connection_assignment}" == "${expected_connection_prefix}"*';SslMode=None' ]] \
        || fail "the LoginEngine environment has a noncanonical local database connection"
    connection_password="${connection_assignment#"${expected_connection_prefix}"}"
    connection_password="${connection_password%';SslMode=None'}"
    [[ -n "${connection_password}" \
        && "${connection_password}" != *';'* \
        && "${connection_password}" != *$'\r'* \
        && "${connection_password}" != *$'\n'* \
        && "${connection_assignment}" == "${expected_connection_prefix}${connection_password};SslMode=None" ]] \
        || fail "the LoginEngine environment has a noncanonical local database connection"
}

verify_no_extended_metadata()
{
    local acl_output
    local capability_output
    local tree_root="$1"
    local xattr_output

    acl_output="$(getfacl -R -p -c -s -- "${tree_root}" 2>/dev/null)" \
        || fail "could not verify release ACL metadata"
    [[ -z "${acl_output}" ]] || fail "release tree contains an extended ACL"
    capability_output="$(getcap -r "${tree_root}" 2>/dev/null)" \
        || fail "could not verify release file capabilities"
    [[ -z "${capability_output}" ]] || fail "release tree contains a file capability"
    xattr_output="$(getfattr -R -d -m- --absolute-names -- "${tree_root}" 2>/dev/null)" \
        || fail "could not verify release extended attributes"
    [[ -z "${xattr_output}" ]] || fail "release tree contains an extended attribute"
}

strip_and_verify_extended_metadata()
{
    local file_path
    local tree_root="$1"

    setfacl -R -b -- "${tree_root}" \
        || fail "could not strip release ACL metadata"
    find "${tree_root}" -type d -exec setfacl -k -- {} + \
        || fail "could not strip release default ACL metadata"
    while IFS= read -r -d '' file_path; do
        if ! setcap -r "${file_path}" 2>/dev/null; then
            [[ -z "$(getcap "${file_path}" 2>/dev/null)" ]] \
                || fail "could not strip a release file capability"
        fi
    done < <(find "${tree_root}" -type f -print0)
    verify_no_extended_metadata "${tree_root}"
}

acquire_operation_lock()
{
    if [[ ! -e "${OPERATION_LOCK}" && ! -L "${OPERATION_LOCK}" ]]; then
        (
            umask 077
            set -o noclobber
            : > "${OPERATION_LOCK}"
        ) 2>/dev/null || true
    fi

    require_safe_regular_file "${OPERATION_LOCK}" "root:root:600"
    exec 9<> "${OPERATION_LOCK}"
    flock -n 9 || fail "another LoginEngine upgrade operation holds the fixed lock"
}

acquire_runtime_mask()
{
    if [[ "${runtime_mask_owned}" == "true" ]]; then
        [[ -L "${RUNTIME_MASK_PATH}" \
            && "$(readlink -- "${RUNTIME_MASK_PATH}")" == "/dev/null" \
            && "$(stat -c '%U:%G' "${RUNTIME_MASK_PATH}")" == "root:root" ]]
        return
    fi

    [[ ! -e "${RUNTIME_MASK_PATH}" && ! -L "${RUNTIME_MASK_PATH}" ]] || return 1
    runtime_mask_attempted=true
    systemctl mask --runtime "${SERVICE_NAME}" >/dev/null || return 1
    [[ -L "${RUNTIME_MASK_PATH}" \
        && "$(readlink -- "${RUNTIME_MASK_PATH}")" == "/dev/null" \
        && "$(stat -c '%U:%G' "${RUNTIME_MASK_PATH}")" == "root:root" ]] || return 1
    systemctl daemon-reload >/dev/null || return 1
    runtime_mask_owned=true
}

release_runtime_mask()
{
    if [[ "${runtime_mask_attempted}" != "true" ]]; then
        return 0
    fi

    if [[ ! -e "${RUNTIME_MASK_PATH}" && ! -L "${RUNTIME_MASK_PATH}" ]]; then
        runtime_mask_owned=false
        return 0
    fi
    [[ -L "${RUNTIME_MASK_PATH}" \
        && "$(readlink -- "${RUNTIME_MASK_PATH}")" == "/dev/null" \
        && "$(stat -c '%U:%G' "${RUNTIME_MASK_PATH}")" == "root:root" ]] || return 1
    systemctl unmask --runtime "${SERVICE_NAME}" >/dev/null || return 1
    [[ ! -e "${RUNTIME_MASK_PATH}" && ! -L "${RUNTIME_MASK_PATH}" ]] || return 1
    runtime_mask_owned=false
}

prove_disabled_inactive_listener_free()
{
    [[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" == "disabled" ]] \
        && [[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
        && [[ -z "$(ss -H -ltn "sport = :${LOGIN_PORT}" 2>/dev/null || true)" ]] \
        && [[ ! -e "${RUNTIME_MASK_PATH}" && ! -L "${RUNTIME_MASK_PATH}" ]]
}

remove_guarded_release_directory()
{
    local path="$1"
    local releases_real
    local resolved

    if [[ ! -e "${path}" && ! -L "${path}" ]]; then
        return 0
    fi

    [[ -d "${path}" && ! -L "${path}" ]] || return 1
    [[ "$(stat -c '%U:%G' "${path}")" == "root:root" ]] || return 1
    releases_real="$(realpath -e -- "${RELEASES_DIRECTORY}")" || return 1
    resolved="$(realpath -e -- "${path}")" || return 1
    [[ "${resolved}" == "${releases_real}"/* && "${resolved}" != "${releases_real}" ]] || return 1
    path_has_no_nested_mount "${resolved}" || return 1
    rm -r --one-file-system -- "${resolved}"
}

remove_guarded_regular_file()
{
    local path="$1"

    if [[ ! -e "${path}" && ! -L "${path}" ]]; then
        return 0
    fi
    [[ -f "${path}" && ! -L "${path}" ]] || return 1
    [[ "$(stat -c '%U:%G' "${path}")" == "root:root" ]] || return 1
    rm -f -- "${path}"
}

remove_guarded_switch_link()
{
    local link_target
    local path="$1"

    if [[ ! -e "${path}" && ! -L "${path}" ]]; then
        return 0
    fi
    [[ -L "${path}" ]] || return 1
    [[ "$(stat -c '%U:%G' "${path}")" == "root:root" ]] || return 1
    link_target="$(readlink -- "${path}")" || return 1
    [[ "${link_target}" == "${release_target}" || "${link_target}" == "${old_release_target}" ]] \
        || return 1
    rm -- "${path}"
}

cleanup_temporary_directory()
{
    local temporary_real

    if [[ -z "${temporary_directory}" || (! -e "${temporary_directory}" && ! -L "${temporary_directory}") ]]; then
        return 0
    fi
    [[ -d "${temporary_directory}" && ! -L "${temporary_directory}" ]] || return 1
    temporary_real="$(realpath -e -- "${temporary_directory}")" || return 1
    [[ "${temporary_real}" == /run/aorebirth-login-stage7-upgrade.* ]] || return 1
    rm -f -- "${unit_candidate}" "${unit_backup}" "${unit_canonical}" "${unit_installed_normalized}"
    rmdir -- "${temporary_real}"
}

restore_current_link()
{
    local current_target

    [[ "${current_switch_attempted}" == "true" ]] || return 0
    if [[ -L "${CURRENT_LINK}" ]]; then
        current_target="$(readlink -- "${CURRENT_LINK}")" || return 1
        if [[ "${current_target}" == "${old_release_target}" ]]; then
            remove_guarded_switch_link "${current_swap}" || return 1
            current_is_proven_old
            return
        fi
        [[ "${current_target}" == "${release_target}" ]] || return 1
    elif [[ -e "${CURRENT_LINK}" ]]; then
        return 1
    fi

    remove_guarded_switch_link "${current_swap}" || return 1
    ln -sT -- "${old_release_target}" "${current_swap}" || return 1
    mv -fT -- "${current_swap}" "${CURRENT_LINK}" || return 1
    current_is_proven_old
}

current_is_proven_old()
{
    local current_resolved
    local new_resolved=""

    [[ -n "${old_release_target}" && -L "${CURRENT_LINK}" ]] || return 1
    [[ "$(stat -c '%U:%G' "${CURRENT_LINK}")" == "root:root" ]] || return 1
    [[ "$(readlink -- "${CURRENT_LINK}")" == "${old_release_target}" ]] || return 1
    current_resolved="$(realpath -e -- "${CURRENT_LINK}")" || return 1
    [[ "${current_resolved}" == "${old_release_target}" ]] || return 1
    if [[ -e "${release_target}" && ! -L "${release_target}" ]]; then
        new_resolved="$(realpath -e -- "${release_target}")" || return 1
        [[ "${current_resolved}" != "${new_resolved}" ]] || return 1
    fi

    return 0
}

restore_unit_file()
{
    [[ "${unit_update_attempted}" == "true" ]] || return 0
    [[ -f "${unit_backup}" && ! -L "${unit_backup}" ]] || return 1

    if [[ -f "${UNIT_FILE}" && ! -L "${UNIT_FILE}" ]] \
        && cmp -s -- "${UNIT_FILE}" "${unit_backup}"; then
        remove_guarded_regular_file "${unit_swap}" || return 1
        return 0
    fi
    if [[ -e "${UNIT_FILE}" || -L "${UNIT_FILE}" ]]; then
        [[ -f "${UNIT_FILE}" && ! -L "${UNIT_FILE}" ]] || return 1
        cmp -s -- "${UNIT_FILE}" "${unit_candidate}" || return 1
    fi

    remove_guarded_regular_file "${unit_swap}" || return 1
    install -o root -g root -m 0644 "${unit_backup}" "${unit_swap}" || return 1
    mv -fT -- "${unit_swap}" "${UNIT_FILE}" || return 1
    cmp -s -- "${UNIT_FILE}" "${unit_backup}"
}

rollback_upgrade()
{
    local cleanup_failed=false

    restore_current_link || cleanup_failed=true
    restore_unit_file || cleanup_failed=true

    if [[ "${current_switch_attempted}" == "true" || "${unit_update_attempted}" == "true" ]]; then
        systemctl daemon-reload >/dev/null 2>&1 || cleanup_failed=true
    fi

    remove_guarded_switch_link "${current_swap}" || cleanup_failed=true
    remove_guarded_regular_file "${unit_swap}" || cleanup_failed=true
    if [[ "${release_move_attempted}" == "true" ]]; then
        if current_is_proven_old; then
            remove_guarded_release_directory "${release_target}" || cleanup_failed=true
        else
            cleanup_failed=true
        fi
    fi
    remove_guarded_release_directory "${release_staging}" || cleanup_failed=true

    [[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
        || cleanup_failed=true
    if [[ -n "$(ss -H -ltn "sport = :${LOGIN_PORT}" 2>/dev/null || true)" ]]; then
        cleanup_failed=true
    fi

    [[ "${cleanup_failed}" == "false" ]]
}

cleanup_on_exit()
{
    local exit_status=$?
    local rollback_permitted=true

    trap - EXIT INT TERM
    if [[ "${upgrade_complete}" != "true" ]]; then
        if ! acquire_runtime_mask; then
            echo "FAIL: Stage 7.1 could not acquire the runtime activation barrier for rollback" >&2
            rollback_permitted=false
            exit_status=1
        fi
        if [[ "${rollback_permitted}" == "true" ]] && ! rollback_upgrade; then
            echo "FAIL: Stage 7.1 disabled-service upgrade rollback was incomplete" >&2
            exit_status=1
        fi
    fi
    if ! release_runtime_mask; then
        echo "FAIL: Stage 7.1 runtime activation barrier cleanup was incomplete" >&2
        exit_status=1
    elif ! systemctl daemon-reload >/dev/null 2>&1; then
        echo "FAIL: Stage 7.1 systemd reload after activation-barrier cleanup failed" >&2
        exit_status=1
    elif ! prove_disabled_inactive_listener_free; then
        echo "FAIL: Stage 7.1 did not restore the disabled inactive listener-free boundary" >&2
        exit_status=1
    fi
    if ! cleanup_temporary_directory; then
        echo "FAIL: Stage 7.1 temporary-file cleanup was incomplete" >&2
        exit_status=1
    fi
    exit "${exit_status}"
}

if [[ "${EUID}" -ne 0 ]]; then
    fail "run as root"
fi

[[ "$#" -eq 1 ]] || fail "usage: upgrade-disabled-service.sh <unique-stage7-release-name>"
release_name="$1"
[[ "${release_name}" =~ ^stage7-[a-z0-9][a-z0-9._-]{0,55}$ ]] \
    || fail "release name must be a unique stage7-* identifier"

release_target="${RELEASES_DIRECTORY}/${release_name}"
release_staging="${RELEASES_DIRECTORY}/.${release_name}.installing"
unit_swap="${UNIT_FILE}.stage7-upgrade-${release_name}"
current_swap="${INSTALL_ROOT}/.current.stage7-upgrade-${release_name}"

for required_command in \
    file \
    findmnt \
    flock \
    getcap \
    getfacl \
    getfattr \
    setcap \
    setfacl; do
    command -v "${required_command}" >/dev/null 2>&1 \
        || fail "required guarded-upgrade command is unavailable: ${required_command}"
done

require_safe_directory "/" "root:root"
require_safe_directory "/run" "root:root"
require_safe_directory "/run/systemd" "root:root"
require_safe_directory "/run/systemd/system" "root:root"
acquire_operation_lock
trap cleanup_on_exit EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

[[ "$(uname -m)" == "x86_64" ]] || fail "the uploaded Stage 7.1 package requires x86_64 Ubuntu"
[[ "$(id -gn aorebirth 2>/dev/null || true)" == "aorebirth" ]] \
    || fail "the existing aorebirth service account/group is required"
[[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" == "disabled" ]] \
    || fail "the LoginEngine service must be disabled"
[[ "$(systemctl show "${SERVICE_NAME}" --property=LoadState --value)" == "loaded" ]] \
    || fail "the existing LoginEngine unit must be loaded"
[[ "$(systemctl show "${SERVICE_NAME}" --property=NeedDaemonReload --value)" == "no" ]] \
    || fail "systemd has an unreviewed pending unit change"
[[ ! -e "${DROP_IN_DIRECTORY}" && ! -L "${DROP_IN_DIRECTORY}" ]] \
    || fail "the LoginEngine service has an unsafe runtime drop-in path"
[[ ! -e "${RUNTIME_MASK_PATH}" && ! -L "${RUNTIME_MASK_PATH}" ]] \
    || fail "the LoginEngine service already has a runtime mask"
require_safe_regular_file "${UNIT_FILE}" "root:root:644"
require_unit_contract "${UNIT_FILE}"
require_effective_installed_unit
acquire_runtime_mask || fail "could not acquire the trap-owned LoginEngine runtime activation barrier"
[[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
    || fail "the LoginEngine service must be inactive behind the activation barrier"
assert_no_login_listener

[[ ! -e "${release_target}" && ! -L "${release_target}" ]] \
    || fail "the immutable release target already exists"
[[ ! -e "${release_staging}" && ! -L "${release_staging}" ]] \
    || fail "the release staging target already exists"
[[ ! -e "${unit_swap}" && ! -L "${unit_swap}" ]] \
    || fail "the unit swap target already exists"
[[ ! -e "${current_swap}" && ! -L "${current_swap}" ]] \
    || fail "the current-link swap target already exists"

require_safe_directory "/opt" "root:root"
require_safe_directory "/opt/ao-rebirth" "root:root"
require_safe_directory "${INSTALL_ROOT}" "root:root"
require_safe_directory "${RELEASES_DIRECTORY}" "root:root"
require_safe_directory "/etc/ao-rebirth" "root:aorebirth"
require_safe_directory "${CONFIGURATION_DIRECTORY}" "root:aorebirth"
require_safe_directory "/etc/systemd/system" "root:root"

[[ -L "${CURRENT_LINK}" ]] || fail "the LoginEngine current path must be a symlink"
[[ "$(stat -c '%U:%G' "${CURRENT_LINK}")" == "root:root" ]] \
    || fail "the LoginEngine current symlink must be root-owned"
old_release_target="$(readlink -- "${CURRENT_LINK}")" \
    || fail "the LoginEngine current symlink could not be read"
old_release_name="$(basename -- "${old_release_target}")"
[[ "${old_release_name}" =~ ^stage7-[a-z0-9][a-z0-9._-]{0,55}$ ]] \
    || fail "the installed LoginEngine release identity is unsafe"
[[ "$(dirname -- "${old_release_target}")" == "${RELEASES_DIRECTORY}" \
    && "${old_release_target}" == "${RELEASES_DIRECTORY}/${old_release_name}" ]] \
    || fail "the installed LoginEngine release is not a direct child of the reviewed releases directory"
[[ "$(realpath -e -- "${CURRENT_LINK}")" == "${old_release_target}" ]] \
    || fail "the LoginEngine current symlink does not resolve exactly"
[[ "${old_release_target}" != "${release_target}" ]] \
    || fail "the new release identity must differ from the installed release"
require_package "${old_release_target}"
verify_no_extended_metadata "${old_release_target}"

require_safe_regular_file "${CONFIGURATION_FILE}" "root:aorebirth:640"
cmp -s -- "${old_release_target}/Config.xml" "${CONFIGURATION_FILE}" \
    || fail "the installed Config.xml differs from the active immutable release"
require_safe_regular_file "${ENVIRONMENT_FILE}" "root:root:600"
require_runtime_environment_file "${ENVIRONMENT_FILE}"
environment_metadata_before="$(stat -c '%d:%i:%s:%Y:%Z:%U:%G:%a' "${ENVIRONMENT_FILE}")"
require_safe_regular_file "${UNIT_FILE}" "root:root:644"
require_unit_contract "${UNIT_FILE}"

require_package "${UPLOAD_DIRECTORY}" false
cmp -s -- "${UPLOAD_DIRECTORY}/Config.xml" "${CONFIGURATION_FILE}" \
    || fail "the uploaded release attempts to change the reviewed LoginEngine configuration"
require_safe_uploaded_file "${UPLOADED_UNIT}"
[[ "$(realpath -e -- "${UPLOADED_UNIT}")" == "${UPLOADED_UNIT}" ]] \
    || fail "the uploaded unit path did not resolve exactly"

temporary_directory="$(mktemp -d -- /run/aorebirth-login-stage7-upgrade.XXXXXX)"
[[ -d "${temporary_directory}" && ! -L "${temporary_directory}" ]] \
    || fail "could not create a safe Stage 7.1 temporary directory"
[[ "$(stat -c '%U:%G:%a' "${temporary_directory}")" == "root:root:700" ]] \
    || fail "the Stage 7.1 temporary directory has unsafe permissions"
unit_candidate="${temporary_directory}/ao-rebirth-loginengine.service"
unit_backup="${temporary_directory}/ao-rebirth-loginengine.service.previous"
unit_canonical="${temporary_directory}/ao-rebirth-loginengine.service.canonical"
unit_installed_normalized="${temporary_directory}/ao-rebirth-loginengine.service.installed-normalized"

install -o root -g root -m 0644 "${UPLOADED_UNIT}" "${unit_candidate}"
sed -i 's/\r$//' "${unit_candidate}"
require_unit_contract "${unit_candidate}"
write_canonical_unit "${unit_canonical}"
cmp -s -- "${unit_candidate}" "${unit_canonical}" \
    || fail "the normalized uploaded unit is not byte-exact canonical deployment input"
install -o root -g root -m 0644 "${UNIT_FILE}" "${unit_installed_normalized}"
sed -i 's/\r$//' "${unit_installed_normalized}"
cmp -s -- "${unit_installed_normalized}" "${unit_canonical}" \
    || fail "the installed LoginEngine unit is outside the byte-exact reviewed canonical boundary"
systemd-analyze verify "${unit_candidate}"
install -o root -g root -m 0600 "${UNIT_FILE}" "${unit_backup}"

install -d -o root -g root -m 0755 "${release_staging}"
cp -R --no-preserve=mode,ownership,timestamps,links,xattr,context -- \
    "${UPLOAD_DIRECTORY}/." "${release_staging}/"
chown -R root:root "${release_staging}"
find "${release_staging}" -type d -exec chmod 0755 {} +
find "${release_staging}" -type f -exec chmod 0644 {} +
chmod 0755 "${release_staging}/LoginEngine"
if [[ -f "${release_staging}/createdump" ]]; then
    chmod 0755 "${release_staging}/createdump"
fi
strip_and_verify_extended_metadata "${release_staging}"
require_package "${release_staging}"
cmp -s -- "${release_staging}/Config.xml" "${CONFIGURATION_FILE}" \
    || fail "the staged release configuration changed unexpectedly"
release_move_attempted=true
mv -T -- "${release_staging}" "${release_target}"
require_package "${release_target}"
verify_no_extended_metadata "${release_target}"

if ! cmp -s -- "${unit_candidate}" "${UNIT_FILE}"; then
    install -o root -g root -m 0644 "${unit_candidate}" "${unit_swap}"
    unit_update_attempted=true
    mv -fT -- "${unit_swap}" "${UNIT_FILE}"
    require_safe_regular_file "${UNIT_FILE}" "root:root:644"
    cmp -s -- "${unit_candidate}" "${UNIT_FILE}" \
        || fail "the installed unit differs from the reviewed unit candidate"
fi

ln -sT -- "${release_target}" "${current_swap}"
current_switch_attempted=true
mv -fT -- "${current_swap}" "${CURRENT_LINK}"
[[ -L "${CURRENT_LINK}" && "$(readlink -- "${CURRENT_LINK}")" == "${release_target}" ]] \
    || fail "the LoginEngine current symlink was not atomically repointed"
[[ "$(realpath -e -- "${CURRENT_LINK}")" == "${release_target}" ]] \
    || fail "the LoginEngine current symlink does not resolve to the new immutable release"

systemctl daemon-reload
[[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
    || fail "the upgraded LoginEngine service became active behind the activation barrier"
[[ -L "${RUNTIME_MASK_PATH}" \
    && "$(readlink -- "${RUNTIME_MASK_PATH}")" == "/dev/null" \
    && "$(stat -c '%U:%G' "${RUNTIME_MASK_PATH}")" == "root:root" ]] \
    || fail "the trap-owned runtime activation barrier changed during the upgrade"
assert_no_login_listener
require_package "${release_target}"
verify_no_extended_metadata "${release_target}"
cmp -s -- "${release_target}/Config.xml" "${CONFIGURATION_FILE}" \
    || fail "the active release configuration differs from the reviewed configuration"
require_safe_regular_file "${ENVIRONMENT_FILE}" "root:root:600"
require_runtime_environment_file "${ENVIRONMENT_FILE}"
[[ "$(stat -c '%d:%i:%s:%Y:%Z:%U:%G:%a' "${ENVIRONMENT_FILE}")" == "${environment_metadata_before}" ]] \
    || fail "the existing LoginEngine environment changed during the upgrade"

release_runtime_mask || fail "could not release the trap-owned runtime activation barrier"
systemctl daemon-reload
prove_disabled_inactive_listener_free \
    || fail "the upgraded LoginEngine did not return to the disabled inactive listener-free boundary"
[[ "$(systemctl show "${SERVICE_NAME}" --property=NeedDaemonReload --value)" == "no" ]] \
    || fail "systemd did not load the reviewed LoginEngine unit"
require_effective_installed_unit
assert_no_login_listener

# The new release, unit, symlink, and disabled/inactive service state are now committed.
# Rollback material is removed only after this point.
upgrade_complete=true
remove_guarded_regular_file "${unit_swap}" \
    || fail "the unit swap cleanup failed"
remove_guarded_switch_link "${current_swap}" \
    || fail "the current-link swap cleanup failed"
cleanup_temporary_directory || fail "the Stage 7.1 temporary-file cleanup failed"
temporary_directory=""
trap - EXIT INT TERM

echo "PASS: unique self-contained LoginEngine release upgraded atomically; service=disabled/inactive listener=closed database=${EXPECTED_DATABASE}."
