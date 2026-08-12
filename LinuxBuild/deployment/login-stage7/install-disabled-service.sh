#!/usr/bin/env bash
set -euo pipefail
export LC_ALL=C

readonly SERVICE_NAME="ao-rebirth-loginengine.service"
readonly UPLOAD_DIRECTORY="/tmp/ao-rebirth-loginengine-publish"
readonly UPLOADED_UNIT="/tmp/ao-rebirth-loginengine.service"
readonly SOURCE_ENVIRONMENT_FILE="/etc/ao-rebirth/chatengine/stage6/chatengine.env"
readonly INSTALL_ROOT="/opt/ao-rebirth/loginengine"
readonly RELEASES_DIRECTORY="${INSTALL_ROOT}/releases"
readonly CURRENT_LINK="${INSTALL_ROOT}/current"
readonly CONFIGURATION_DIRECTORY="/etc/ao-rebirth/loginengine"
readonly CONFIGURATION_FILE="${CONFIGURATION_DIRECTORY}/Config.xml"
readonly ENVIRONMENT_FILE="${CONFIGURATION_DIRECTORY}/loginengine.env"
readonly UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}"
readonly EXPECTED_DATABASE="aorebirth_chatengine_stage6"
readonly LOGIN_PORT="7500"

temporary_directory=""
release_staging=""
release_target=""
config_directory_created=false
current_created=false
environment_created=false
install_root_created=false
installation_complete=false
ao_rebirth_root_created=false
release_created=false
release_staging_created=false
releases_directory_created=false
configuration_created=false
unit_created=false

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
    [[ "$(stat -c '%U:%G' "${path}")" == "${expected_owner}" ]] \
        || fail "directory ownership is unsafe: ${path}"
    directory_mode="$(stat -c '%a' "${path}")"
    (( (8#${directory_mode} & 0022) == 0 )) \
        || fail "directory is group/world writable: ${path}"
}

require_safe_uploaded_file()
{
    local file_mode
    local path="$1"

    [[ -f "${path}" && ! -L "${path}" ]] || fail "uploaded file is missing or unsafe: ${path}"
    [[ "$(stat -c '%U:%G' "${path}")" == "root:root" ]] \
        || fail "uploaded file must be root-owned: ${path}"
    file_mode="$(stat -c '%a' "${path}")"
    (( (8#${file_mode} & 0022) == 0 )) \
        || fail "uploaded file is group/world writable: ${path}"
}

assert_no_login_listener()
{
    local socket_output

    socket_output="$(ss -H -ltn "sport = :${LOGIN_PORT}")" \
        || fail "could not inspect TCP port ${LOGIN_PORT}"
    [[ -z "${socket_output}" ]] \
        || fail "TCP port ${LOGIN_PORT} is already listening"
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
    releases_real="$(realpath -e -- "${RELEASES_DIRECTORY}")" || return 1
    resolved="$(realpath -e -- "${path}")" || return 1
    [[ "${resolved}" == "${releases_real}"/* && "${resolved}" != "${releases_real}" ]] || return 1
    rm -r -- "${resolved}"
}

remove_created_regular_file()
{
    local expected_owner="$2"
    local path="$1"

    if [[ ! -e "${path}" && ! -L "${path}" ]]; then
        return 0
    fi

    [[ -f "${path}" && ! -L "${path}" ]] || return 1
    [[ "$(stat -c '%U:%G' "${path}")" == "${expected_owner}" ]] || return 1
    rm -f -- "${path}"
}

cleanup_temporary_directory()
{
    local temporary_real

    if [[ -z "${temporary_directory}" || (! -e "${temporary_directory}" && ! -L "${temporary_directory}") ]]; then
        return 0
    fi

    [[ -d "${temporary_directory}" && ! -L "${temporary_directory}" ]] || return 1
    temporary_real="$(realpath -e -- "${temporary_directory}")" || return 1
    [[ "${temporary_real}" == /run/aorebirth-login-stage7-install.* ]] || return 1
    rm -f -- "${temporary_real}/ao-rebirth-loginengine.service" \
        "${temporary_real}/loginengine.env"
    rmdir -- "${temporary_real}"
}

rollback_installation()
{
    local cleanup_failed=false

    if [[ "${current_created}" == "true" ]]; then
        if [[ ! -e "${CURRENT_LINK}" && ! -L "${CURRENT_LINK}" ]]; then
            :
        elif [[ -L "${CURRENT_LINK}" && "$(readlink -- "${CURRENT_LINK}")" == "${release_target}" ]]; then
            rm -- "${CURRENT_LINK}" || cleanup_failed=true
        else
            cleanup_failed=true
        fi
    fi

    if [[ "${unit_created}" == "true" ]]; then
        remove_created_regular_file "${UNIT_FILE}" "root:root" || cleanup_failed=true
    fi
    if [[ "${environment_created}" == "true" ]]; then
        remove_created_regular_file "${ENVIRONMENT_FILE}" "root:root" || cleanup_failed=true
    fi
    if [[ "${configuration_created}" == "true" ]]; then
        remove_created_regular_file "${CONFIGURATION_FILE}" "root:aorebirth" || cleanup_failed=true
    fi
    if [[ "${release_created}" == "true" ]]; then
        remove_guarded_release_directory "${release_target}" || cleanup_failed=true
    fi
    if [[ "${release_staging_created}" == "true" ]]; then
        remove_guarded_release_directory "${release_staging}" || cleanup_failed=true
    fi

    if [[ "${unit_created}" == "true" ]]; then
        systemctl daemon-reload >/dev/null 2>&1 || cleanup_failed=true
    fi

    if [[ "${config_directory_created}" == "true" ]]; then
        rmdir -- "${CONFIGURATION_DIRECTORY}" >/dev/null 2>&1 || cleanup_failed=true
    fi
    if [[ "${releases_directory_created}" == "true" ]]; then
        rmdir -- "${RELEASES_DIRECTORY}" >/dev/null 2>&1 || cleanup_failed=true
    fi
    if [[ "${install_root_created}" == "true" ]]; then
        rmdir -- "${INSTALL_ROOT}" >/dev/null 2>&1 || cleanup_failed=true
    fi
    if [[ "${ao_rebirth_root_created}" == "true" ]]; then
        rmdir -- "/opt/ao-rebirth" >/dev/null 2>&1 || cleanup_failed=true
    fi

    [[ "${cleanup_failed}" == "false" ]]
}

cleanup_on_exit()
{
    local exit_status=$?

    trap - EXIT INT TERM
    if [[ "${installation_complete}" != "true" ]]; then
        if ! rollback_installation; then
            echo "FAIL: Stage 7 installation rollback was incomplete" >&2
            exit_status=1
        fi
    fi
    if ! cleanup_temporary_directory; then
        echo "FAIL: Stage 7 temporary-file cleanup was incomplete" >&2
        exit_status=1
    fi
    exit "${exit_status}"
}

if [[ "${EUID}" -ne 0 ]]; then
    fail "run as root"
fi

[[ "$#" -eq 1 ]] || fail "usage: install-disabled-service.sh <unique-stage7-release-name>"
release_name="$1"
[[ "${release_name}" =~ ^stage7-[a-z0-9][a-z0-9._-]{0,55}$ ]] \
    || fail "release name must be a unique stage7-* identifier"

release_target="${RELEASES_DIRECTORY}/${release_name}"
release_staging="${RELEASES_DIRECTORY}/.${release_name}.installing"

[[ "$(uname -m)" == "x86_64" ]] || fail "the uploaded Stage 7 package requires x86_64 Ubuntu"
[[ "$(id -gn aorebirth 2>/dev/null || true)" == "aorebirth" ]] \
    || fail "the existing aorebirth service account/group is required"

enabled_state="$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)"
[[ "${enabled_state}" == "disabled" || "${enabled_state}" == "not-found" ]] \
    || fail "the LoginEngine service must not be enabled"
[[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
    || fail "the LoginEngine service must be inactive"
assert_no_login_listener

[[ ! -e "${UNIT_FILE}" && ! -L "${UNIT_FILE}" ]] \
    || fail "the LoginEngine unit path already exists"
[[ ! -e "${CURRENT_LINK}" && ! -L "${CURRENT_LINK}" ]] \
    || fail "the LoginEngine current path already exists"
[[ ! -e "${CONFIGURATION_FILE}" && ! -L "${CONFIGURATION_FILE}" ]] \
    || fail "the LoginEngine configuration path already exists"
[[ ! -e "${ENVIRONMENT_FILE}" && ! -L "${ENVIRONMENT_FILE}" ]] \
    || fail "the LoginEngine environment path already exists"
[[ ! -e "${release_target}" && ! -L "${release_target}" ]] \
    || fail "the unique release target already exists"
[[ ! -e "${release_staging}" && ! -L "${release_staging}" ]] \
    || fail "the release staging target already exists"

require_safe_directory "/opt" "root:root"
if [[ -e "/opt/ao-rebirth" || -L "/opt/ao-rebirth" ]]; then
    require_safe_directory "/opt/ao-rebirth" "root:root"
fi
if [[ -e "${INSTALL_ROOT}" || -L "${INSTALL_ROOT}" ]]; then
    require_safe_directory "${INSTALL_ROOT}" "root:root"
fi
if [[ -e "${RELEASES_DIRECTORY}" || -L "${RELEASES_DIRECTORY}" ]]; then
    require_safe_directory "${RELEASES_DIRECTORY}" "root:root"
fi
require_safe_directory "/etc/ao-rebirth" "root:aorebirth"
require_safe_directory "/etc/systemd/system" "root:root"
if [[ -e "${CONFIGURATION_DIRECTORY}" || -L "${CONFIGURATION_DIRECTORY}" ]]; then
    require_safe_directory "${CONFIGURATION_DIRECTORY}" "root:aorebirth"
fi

require_safe_directory "${UPLOAD_DIRECTORY}" "root:root"
[[ "$(realpath -e -- "${UPLOAD_DIRECTORY}")" == "${UPLOAD_DIRECTORY}" ]] \
    || fail "the publish upload path did not resolve exactly"
symlink_output="$(find "${UPLOAD_DIRECTORY}" -type l -print -quit)" \
    || fail "could not inspect the publish upload for symlinks"
[[ -z "${symlink_output}" ]] || fail "the publish upload contains a symlink"
special_output="$(find "${UPLOAD_DIRECTORY}" ! -type d ! -type f -print -quit)" \
    || fail "could not inspect the publish upload for special files"
[[ -z "${special_output}" ]] || fail "the publish upload contains a special file"
unsafe_owner_output="$(find "${UPLOAD_DIRECTORY}" \( ! -user root -o ! -group root \) -print -quit)" \
    || fail "could not inspect publish upload ownership"
[[ -z "${unsafe_owner_output}" ]] || fail "the publish upload contains a non-root-owned path"
unsafe_mode_output="$(find "${UPLOAD_DIRECTORY}" -perm /022 -print -quit)" \
    || fail "could not inspect publish upload permissions"
[[ -z "${unsafe_mode_output}" ]] || fail "the publish upload contains a group/world-writable path"

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
    require_safe_uploaded_file "${UPLOAD_DIRECTORY}/${required_name}"
done

apphost_description="$(file -b -- "${UPLOAD_DIRECTORY}/LoginEngine")" \
    || fail "could not inspect the uploaded LoginEngine apphost"
[[ "${apphost_description}" == *"ELF 64-bit"* && "${apphost_description}" == *"x86-64"* ]] \
    || fail "the uploaded LoginEngine apphost is not Linux x86_64"

require_exact_line "${UPLOAD_DIRECTORY}/Config.xml" \
    "  <MysqlConnection>Server=localhost;Database=cellao_codex_clean;Uid=cellaodbuser;Pwd=REPLACE_WITH_LOCAL_PASSWORD</MysqlConnection>"
require_exact_line "${UPLOAD_DIRECTORY}/Config.xml" "  <LoginPort>7500</LoginPort>"
require_exact_line "${UPLOAD_DIRECTORY}/Config.xml" "  <SQLType>MySql</SQLType>"

require_safe_uploaded_file "${UPLOADED_UNIT}"
[[ "$(realpath -e -- "${UPLOADED_UNIT}")" == "${UPLOADED_UNIT}" ]] \
    || fail "the uploaded unit path did not resolve exactly"

[[ -f "${SOURCE_ENVIRONMENT_FILE}" && ! -L "${SOURCE_ENVIRONMENT_FILE}" ]] \
    || fail "the Stage 6 source environment is missing or unsafe"
[[ "$(realpath -e -- "${SOURCE_ENVIRONMENT_FILE}")" == "${SOURCE_ENVIRONMENT_FILE}" ]] \
    || fail "the Stage 6 source environment path did not resolve exactly"
[[ "$(stat -c '%U:%G:%a' "${SOURCE_ENVIRONMENT_FILE}")" == "root:root:600" ]] \
    || fail "the Stage 6 source environment must be root-owned mode 0600"
connection_count="$(grep -Ec '^[[:space:]]*AO_REBIRTH_MYSQL_CONNECTION[[:space:]]*=' \
    "${SOURCE_ENVIRONMENT_FILE}" || true)"
[[ "${connection_count}" == "1" ]] \
    || fail "the Stage 6 source environment must contain exactly one MySQL connection assignment"
connection_assignment="$(grep -E '^[[:space:]]*AO_REBIRTH_MYSQL_CONNECTION[[:space:]]*=' \
    "${SOURCE_ENVIRONMENT_FILE}")"
[[ "${connection_assignment}" == "AO_REBIRTH_MYSQL_CONNECTION=Server=127.0.0.1;Port=33067;Database=${EXPECTED_DATABASE};"* \
    && "${connection_assignment}" != *$'\r'* \
    && "${connection_assignment}" == *';SslMode=None' ]] \
    || fail "the Stage 6 MySQL connection assignment is not canonical"

temporary_directory="$(mktemp -d -- /run/aorebirth-login-stage7-install.XXXXXX)"
[[ -d "${temporary_directory}" && ! -L "${temporary_directory}" ]] \
    || fail "could not create a safe Stage 7 temporary directory"
[[ "$(stat -c '%U:%G:%a' "${temporary_directory}")" == "root:root:700" ]] \
    || fail "the Stage 7 temporary directory has unsafe permissions"
trap cleanup_on_exit EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

unit_candidate="${temporary_directory}/ao-rebirth-loginengine.service"
environment_candidate="${temporary_directory}/loginengine.env"
install -o root -g root -m 0644 "${UPLOADED_UNIT}" "${unit_candidate}"
sed -i 's/\r$//' "${unit_candidate}"

require_exact_line "${unit_candidate}" "Type=notify"
require_exact_line "${unit_candidate}" "User=aorebirth"
require_exact_line "${unit_candidate}" "Group=aorebirth"
require_exact_line "${unit_candidate}" \
    'EnvironmentFile=/etc/ao-rebirth/loginengine/loginengine.env'
require_exact_line "${unit_candidate}" \
    "Environment=AO_REBIRTH_EXPECTED_DATABASE=${EXPECTED_DATABASE}"
require_exact_line "${unit_candidate}" \
    'Environment=AO_REBIRTH_LOGIN_LISTEN_IP=127.0.0.1'
require_exact_line "${unit_candidate}" \
    'ExecStartPre=/usr/bin/test ${AO_REBIRTH_EXPECTED_DATABASE} = aorebirth_chatengine_stage6'
require_exact_line "${unit_candidate}" \
    'ExecStartPre=/usr/bin/test ${AO_REBIRTH_LOGIN_LISTEN_IP} = 127.0.0.1'
require_exact_line "${unit_candidate}" \
    "ExecStartPre=/opt/ao-rebirth/loginengine/current/LoginEngine --validate-startup"
require_exact_line "${unit_candidate}" \
    "ExecStartPre=/opt/ao-rebirth/loginengine/current/LoginEngine --validate-database"
require_exact_line "${unit_candidate}" \
    "ExecStart=/opt/ao-rebirth/loginengine/current/LoginEngine --headless"
[[ "$(grep -Fc 'AO_REBIRTH_MYSQL_CONNECTION' "${unit_candidate}" || true)" == "0" ]] \
    || fail "the uploaded unit must not contain a database connection"

umask 077
printf '%s\n' \
    "AO_REBIRTH_CONFIG_PATH=${CONFIGURATION_FILE}" \
    'AO_REBIRTH_LOGIN_LISTEN_IP=127.0.0.1' \
    'AO_REBIRTH_REQUIRED_SQL_TYPE=MySql' \
    "AO_REBIRTH_EXPECTED_DATABASE=${EXPECTED_DATABASE}" \
    "${connection_assignment}" \
    > "${environment_candidate}"
chown root:root "${environment_candidate}"
chmod 0600 "${environment_candidate}"

if [[ ! -e "/opt/ao-rebirth" ]]; then
    ao_rebirth_root_created=true
    install -d -o root -g root -m 0755 "/opt/ao-rebirth"
fi
if [[ ! -e "${INSTALL_ROOT}" ]]; then
    install_root_created=true
    install -d -o root -g root -m 0755 "${INSTALL_ROOT}"
fi
if [[ ! -e "${RELEASES_DIRECTORY}" ]]; then
    releases_directory_created=true
    install -d -o root -g root -m 0755 "${RELEASES_DIRECTORY}"
fi
if [[ ! -e "${CONFIGURATION_DIRECTORY}" ]]; then
    config_directory_created=true
    install -d -o root -g aorebirth -m 0750 "${CONFIGURATION_DIRECTORY}"
fi

release_staging_created=true
install -d -o root -g root -m 0755 "${release_staging}"
cp -a -- "${UPLOAD_DIRECTORY}/." "${release_staging}/"
chown -R root:root "${release_staging}"
find "${release_staging}" -type d -exec chmod 0755 {} +
find "${release_staging}" -type f -exec chmod 0644 {} +
chmod 0755 "${release_staging}/LoginEngine"
if [[ -f "${release_staging}/createdump" ]]; then
    chmod 0755 "${release_staging}/createdump"
fi
release_created=true
mv -T -- "${release_staging}" "${release_target}"
release_staging_created=false

current_created=true
ln -sT "${release_target}" "${CURRENT_LINK}"
configuration_created=true
install -o root -g aorebirth -m 0640 \
    "${release_target}/Config.xml" "${CONFIGURATION_FILE}"
environment_created=true
install -o root -g root -m 0600 "${environment_candidate}" "${ENVIRONMENT_FILE}"
unit_created=true
install -o root -g root -m 0644 "${unit_candidate}" "${UNIT_FILE}"

cmp -s -- "${release_target}/Config.xml" "${CONFIGURATION_FILE}" \
    || fail "installed Config.xml differs from the published artifact"
cmp -s -- "${environment_candidate}" "${ENVIRONMENT_FILE}" \
    || fail "installed LoginEngine environment differs from the guarded candidate"
cmp -s -- "${unit_candidate}" "${UNIT_FILE}" \
    || fail "installed LoginEngine unit differs from the guarded candidate"
[[ "$(realpath -e -- "${CURRENT_LINK}")" == "$(realpath -e -- "${release_target}")" ]] \
    || fail "the LoginEngine current link does not resolve to the unique release"

systemd-analyze verify "${UNIT_FILE}"
systemctl daemon-reload
[[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" == "disabled" ]] \
    || fail "the installed LoginEngine service is not disabled"
[[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
    || fail "the installed LoginEngine service is not inactive"
[[ "$(systemctl show "${SERVICE_NAME}" --property=FragmentPath --value)" == "${UNIT_FILE}" ]] \
    || fail "systemd did not load the exact installed LoginEngine unit"
assert_no_login_listener

installation_complete=true
cleanup_temporary_directory || fail "Stage 7 temporary-file cleanup failed"
temporary_directory=""
trap - EXIT INT TERM

echo "PASS: unique self-contained LoginEngine release installed; service=disabled/inactive listener=closed database=${EXPECTED_DATABASE}."
