#!/usr/bin/env bash
set -euo pipefail

readonly SERVICE_NAME="ao-rebirth-zoneengine.service"
readonly SERVICE_USER="aorebirth"
readonly SERVICE_GROUP="aorebirth"
readonly INSTALL_ROOT="/opt/ao-rebirth/zoneengine"
readonly RELEASES_DIRECTORY="${INSTALL_ROOT}/releases"
readonly CURRENT_LINK="${INSTALL_ROOT}/current"
readonly ENVIRONMENT_FILE="/etc/ao-rebirth/zoneengine/zoneengine.env"
readonly DATABASE_CONTAINER="aorebirth-chatengine-mysql-stage6"
readonly EXPECTED_DATABASE="aorebirth_chatengine_stage6"
readonly APPHOST_NAME="ZoneEngine"
readonly APPHOST_MODE="750"
readonly CREATEDUMP_MODE="750"
readonly DIRECTORY_MODE="750"
readonly FILE_MODE="640"
readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
source "${SCRIPT_DIR}/../../placement-provenance.sh"

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
    require_file "${publish_dir}/${APPHOST_NAME}"
    require_file "${publish_dir}/ZoneEngine.dll"
    require_file "${publish_dir}/Config.xml"
    require_file "${publish_dir}/items.dat"
    require_file "${publish_dir}/nanos.dat"
    require_file "${publish_dir}/playfields.dat"
    require_file "${publish_dir}/XML Data/Stats.xml"
    require_file "${publish_dir}/XML Data/Playfields.xml"
}

read_source_sha_file()
{
    local path="$1"
    tr -d '\r\n\t ' < "${path}"
}

require_artifact_provenance()
{
    local artifact_dir="$1"
    require_file "${artifact_dir}/SOURCE_SHA"
    require_file "${artifact_dir}/BUILD_PROVENANCE.env"
    require_file "${artifact_dir}/LINUX_ACCEPTANCE.env"
    local actual_source_sha
    actual_source_sha="$(read_source_sha_file "${artifact_dir}/SOURCE_SHA")"
    [[ "${actual_source_sha}" == "${expected_source_sha}" ]] \
        || fail "artifact source SHA mismatch: expected ${expected_source_sha}, actual ${actual_source_sha}"
    grep -Fx "COMMIT_SHA=${expected_source_sha}" "${artifact_dir}/BUILD_PROVENANCE.env" >/dev/null \
        || fail "build provenance commit does not match expected source SHA"
    grep -Fx "AO_REBIRTH_SOURCE_SHA=${expected_source_sha}" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "Linux acceptance source SHA does not match expected source SHA"
    grep -Fx "EXPECTED_SOURCE_SHA=${expected_source_sha}" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "Linux acceptance expected SHA does not match deployment SHA"
    grep -Fx "SOURCE_SHA_MATCH=PASS" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "Linux acceptance did not pass source SHA match"
    grep -Fx "TRACKED_SOURCE_CLEAN=PASS" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "Linux acceptance did not prove tracked source clean"
    grep -Fx "LINUX_ACCEPTANCE=PASS" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "Linux acceptance marker is missing or not PASS"
    placement_provenance_load "${artifact_dir}" "${expected_source_sha}" linux \
        || fail "official placement artifact provenance is invalid"
    placement_require_build_provenance "${artifact_dir}/BUILD_PROVENANCE.env" \
        || fail "build provenance lacks official placement evidence"
    grep -Fx "PLACEMENT_VALIDATION=PASS" "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "official placement Linux acceptance did not pass"
    grep -Fx "EXPECTED_PLACEMENT_BUILD_MANIFEST_SHA256=${PLACEMENT_BUILD_MANIFEST_SHA256}" \
        "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "accepted official placement manifest digest does not match"
    grep -Fx "PLACEMENT_BUILD_MANIFEST_SHA256=${PLACEMENT_BUILD_MANIFEST_SHA256}" \
        "${artifact_dir}/LINUX_ACCEPTANCE.env" >/dev/null \
        || fail "official placement manifest acceptance provenance is missing"
}

require_identity()
{
    getent group "${SERVICE_GROUP}" >/dev/null || fail "missing service group ${SERVICE_GROUP}"
    id -u "${SERVICE_USER}" >/dev/null 2>&1 || fail "missing service user ${SERVICE_USER}"
}

require_safe_release_name()
{
    local release_name="$1"
    [[ "${release_name}" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*$ ]] || fail "unsafe release name"
}

require_direct_release_path()
{
    local release_path="$1"
    local release_name
    release_name="$(basename -- "${release_path}")"
    [[ "${release_path}" == "${RELEASES_DIRECTORY}/${release_name}" ]] \
        || fail "release path is not a direct child of ${RELEASES_DIRECTORY}"
}

require_current_release()
{
    [[ -L "${CURRENT_LINK}" ]] || fail "current path must be a symlink"
    old_release_target="$(readlink -- "${CURRENT_LINK}")" || fail "could not read current symlink"
    require_direct_release_path "${old_release_target}"
    [[ "$(realpath -e -- "${CURRENT_LINK}")" == "${old_release_target}" ]] \
        || fail "current symlink does not resolve exactly"
    validate_release_permissions "${old_release_target}"
}

apply_release_permissions()
{
    local release_path="$1"
    chown -R root:"${SERVICE_GROUP}" "${release_path}"
    find "${release_path}" -type d -exec chmod "0${DIRECTORY_MODE}" {} +
    find "${release_path}" -type f -exec chmod "0${FILE_MODE}" {} +
    chmod "0${APPHOST_MODE}" "${release_path}/${APPHOST_NAME}"
    if [[ -f "${release_path}/createdump" ]]; then
        chmod "0${CREATEDUMP_MODE}" "${release_path}/createdump"
    fi
}

validate_release_permissions()
{
    local release_path="$1"
    local apphost="${release_path}/${APPHOST_NAME}"
    require_publish_tree "${release_path}"
    [[ "$(stat -c '%U:%G:%a' "${release_path}")" == "root:${SERVICE_GROUP}:${DIRECTORY_MODE}" ]] \
        || fail "release directory must be root:${SERVICE_GROUP} mode ${DIRECTORY_MODE}: ${release_path}"
    [[ "$(stat -c '%U:%G:%a' "${apphost}")" == "root:${SERVICE_GROUP}:${APPHOST_MODE}" ]] \
        || fail "${APPHOST_NAME} apphost must be root:${SERVICE_GROUP} mode ${APPHOST_MODE}: ${apphost}"
    [[ -x "${apphost}" ]] || fail "${APPHOST_NAME} apphost is not executable"
    runuser -u "${SERVICE_USER}" -g "${SERVICE_GROUP}" -- test -x "${apphost}" \
        || fail "${SERVICE_USER} cannot execute ${APPHOST_NAME} apphost"
}

mysql_connection_value()
{
    local mysql_line
    mysql_line="$(grep -m 1 '^AO_REBIRTH_MYSQL_CONNECTION=' "${ENVIRONMENT_FILE}")" \
        || fail "ZoneEngine MySQL connection assignment is unavailable"
    [[ -n "${mysql_line}" ]] || fail "ZoneEngine MySQL connection assignment is empty"
    printf '%s' "${mysql_line#*=}"
}

run_zone_validation()
{
    local release_path="$1"
    local validation_mode="$2"
    local mysql_connection
    mysql_connection="$(mysql_connection_value)"
    runuser -u "${SERVICE_USER}" -g "${SERVICE_GROUP}" -- env \
        AO_REBIRTH_REQUIRED_SQL_TYPE=MySql \
        AO_REBIRTH_EXPECTED_DATABASE="${EXPECTED_DATABASE}" \
        AO_REBIRTH_BIND_MODE=Public \
        AO_REBIRTH_STAGE10_PUBLIC_PLAYER_ACCESS=1 \
        AO_REBIRTH_ZONE_LISTEN_IP=0.0.0.0 \
        AO_REBIRTH_CHAT_LISTEN_IP=127.0.0.1 \
        AO_REBIRTH_CONFIG_PATH="${release_path}/Config.xml" \
        AO_REBIRTH_MYSQL_CONNECTION="${mysql_connection}" \
        "${release_path}/${APPHOST_NAME}" "${validation_mode}" >/dev/null
}

validate_release_runtime()
{
    local release_path="$1"
    validate_release_permissions "${release_path}"
    run_zone_validation "${release_path}" --validate-startup
    run_zone_validation "${release_path}" --validate-database
}

verify_no_online_characters()
{
    local online
    online="$(docker exec -i "${DATABASE_CONTAINER}" sh -c \
        'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" '"${EXPECTED_DATABASE}"' --batch --raw --skip-column-names -e "SELECT COUNT(*) FROM characters WHERE Online <> 0;"')" \
        || fail "could not inspect online character guard"
    [[ "${online}" == "0" ]] || fail "online characters present: ${online}"
}

stage_release()
{
    local publish_dir="$1"
    release_staging="${INSTALL_ROOT}/.staging-${release_name}-$$"
    [[ ! -e "${release_staging}" && ! -L "${release_staging}" ]] || fail "release staging target already exists"
    install -d -o root -g "${SERVICE_GROUP}" -m "0${DIRECTORY_MODE}" "${release_staging}"
    cp -a -- "${publish_dir}/." "${release_staging}/"
    apply_release_permissions "${release_staging}"
    require_artifact_provenance "${release_staging}"
    validate_release_runtime "${release_staging}"
}

promote_release()
{
    mv -T -- "${release_staging}" "${release_target}"
    require_artifact_provenance "${release_target}"
    validate_release_runtime "${release_target}"
    rollback_target="${old_release_target}"
    validate_release_runtime "${rollback_target}"

    systemctl stop "${SERVICE_NAME}"
    ln -sT -- "${release_target}" "${current_swap}"
    mv -fT -- "${current_swap}" "${CURRENT_LINK}"

    if ! systemctl start "${SERVICE_NAME}"; then
        ln -sT -- "${rollback_target}" "${current_swap}"
        mv -fT -- "${current_swap}" "${CURRENT_LINK}"
        systemctl start "${SERVICE_NAME}" >/dev/null 2>&1 || true
        fail "new ${SERVICE_NAME} release failed to start; rollback target restored"
    fi

    [[ "$(systemctl is-active "${SERVICE_NAME}")" == "active" ]] \
        || fail "${SERVICE_NAME} is not active after promotion"
}

validate_artifact_provenance_command()
{
    [[ "$#" -eq 2 ]] || fail "usage: upgrade-live-service.sh --validate-artifact-provenance <publish-dir> <expected-source-sha>"
    local publish_dir
    publish_dir="$(realpath -e -- "$1")"
    expected_source_sha="$2"
    [[ "${expected_source_sha}" =~ ^[0-9a-fA-F]{40}$ ]] || fail "invalid expected source SHA"
    require_publish_tree "${publish_dir}"
    require_artifact_provenance "${publish_dir}"
    echo "PASS: artifact provenance matches expected source SHA."
}

cleanup()
{
    if [[ -n "${release_staging:-}" && -e "${release_staging}" && ! -e "${release_target:-}" ]]; then
        rm -rf -- "${release_staging}"
    fi
    if [[ -n "${current_swap:-}" && ( -e "${current_swap}" || -L "${current_swap}" ) ]]; then
        rm -f -- "${current_swap}"
    fi
}

main()
{
    if [[ "${1:-}" == "--validate-artifact-provenance" ]]; then
        shift
        validate_artifact_provenance_command "$@"
        return
    fi

    require_root
    [[ "$#" -eq 3 ]] || fail "usage: upgrade-live-service.sh <publish-dir> <release-id> <expected-source-sha>"
    local publish_dir
    publish_dir="$(realpath -e -- "$1")"
    release_name="$2"
    expected_source_sha="$3"
    [[ "${expected_source_sha}" =~ ^[0-9a-fA-F]{40}$ ]] || fail "invalid expected source SHA"
    require_safe_release_name "${release_name}"
    require_identity
    require_publish_tree "${publish_dir}"
    require_artifact_provenance "${publish_dir}"

    release_target="${RELEASES_DIRECTORY}/${release_name}"
    current_swap="${INSTALL_ROOT}/.current-upgrade-${release_name}"
    require_direct_release_path "${release_target}"
    [[ ! -e "${release_target}" && ! -L "${release_target}" ]] || fail "release already exists"
    [[ ! -e "${current_swap}" && ! -L "${current_swap}" ]] || fail "current swap target already exists"

    trap cleanup EXIT
    require_current_release
    verify_no_online_characters
    install -d -o root -g "${SERVICE_GROUP}" -m "0${DIRECTORY_MODE}" "${RELEASES_DIRECTORY}"
    stage_release "${publish_dir}"
    promote_release
    trap - EXIT
    cleanup
    echo "PASS: ${SERVICE_NAME} promoted to ${release_name}; apphost=root:${SERVICE_GROUP}:${APPHOST_MODE}."
}

main "$@"
