#!/usr/bin/env bash
set -euo pipefail

readonly CONTAINER_NAME="aorebirth-chatengine-mysql-stage6"
readonly NETWORK_NAME="aorebirth_chatengine_stage6_internal"
readonly VOLUME_NAME="aorebirth_chatengine_mysql_stage6_data"
readonly SECRET_DIRECTORY="/etc/ao-rebirth/chatengine/stage6"
readonly EXPECTED_LABEL="chatengine-stage6-disposable"
readonly CONFIRMATION="--confirm-remove-aorebirth-chatengine-stage6"
readonly SERVICE_NAME="ao-rebirth-chatengine.service"
readonly DROP_IN_FILE="/run/systemd/system/${SERVICE_NAME}.d/stage6-validation.conf"
readonly DROP_IN_TEMP="/run/systemd/system/${SERVICE_NAME}.d/stage6-validation.conf.tmp"

fail()
{
    echo "REFUSED: $*" >&2
    exit 1
}

if [[ "${EUID}" -ne 0 ]]; then
    fail "run as root"
fi

if [[ "$#" -ne 1 || "$1" != "${CONFIRMATION}" ]]; then
    fail "exact confirmation is required: ${CONFIRMATION}"
fi

[[ "$(systemctl is-enabled "${SERVICE_NAME}" 2>/dev/null || true)" == "disabled" ]] \
    || fail "the ChatEngine service must be disabled before database removal"
[[ "$(systemctl is-active "${SERVICE_NAME}" 2>/dev/null || true)" == "inactive" ]] \
    || fail "the ChatEngine service must be inactive before database removal"
[[ ! -e "${DROP_IN_FILE}" && ! -L "${DROP_IN_FILE}" \
    && ! -e "${DROP_IN_TEMP}" && ! -L "${DROP_IN_TEMP}" ]] \
    || fail "recover the exact Stage 6 validation state before database removal"
docker info >/dev/null 2>&1 || fail "Docker is unavailable; no removal was attempted"

container_exists=false
volume_exists=false
network_exists=false
secret_directory_exists=false

container_matches="$(docker container ls --all --filter "name=^${CONTAINER_NAME}$" --format '{{.Names}}')"
[[ -z "${container_matches}" || "${container_matches}" == "${CONTAINER_NAME}" ]] \
    || fail "unexpected container name-filter result"
if [[ "${container_matches}" == "${CONTAINER_NAME}" ]]; then
    container_label="$(docker inspect --format '{{index .Config.Labels "org.aorebirth.purpose"}}' "${CONTAINER_NAME}")"
    [[ "${container_label}" == "${EXPECTED_LABEL}" ]] || fail "container label mismatch"
    container_exists=true
fi

volume_matches="$(docker volume ls --filter "name=^${VOLUME_NAME}$" --format '{{.Name}}')"
[[ -z "${volume_matches}" || "${volume_matches}" == "${VOLUME_NAME}" ]] \
    || fail "unexpected volume name-filter result"
if [[ "${volume_matches}" == "${VOLUME_NAME}" ]]; then
    volume_label="$(docker volume inspect --format '{{index .Labels "org.aorebirth.purpose"}}' "${VOLUME_NAME}")"
    [[ "${volume_label}" == "${EXPECTED_LABEL}" ]] || fail "volume label mismatch"
    volume_exists=true
fi

network_matches="$(docker network ls --filter "name=^${NETWORK_NAME}$" --format '{{.Name}}')"
[[ -z "${network_matches}" || "${network_matches}" == "${NETWORK_NAME}" ]] \
    || fail "unexpected network name-filter result"
if [[ "${network_matches}" == "${NETWORK_NAME}" ]]; then
    network_label="$(docker network inspect --format '{{index .Labels "org.aorebirth.purpose"}}' "${NETWORK_NAME}")"
    [[ "${network_label}" == "${EXPECTED_LABEL}" ]] || fail "network label mismatch"
    network_exists=true
fi

if [[ -e "${SECRET_DIRECTORY}" || -L "${SECRET_DIRECTORY}" ]]; then
    [[ ! -L "${SECRET_DIRECTORY}" ]] \
        || fail "the exact Stage 6 secret path is a symbolic link"
    [[ -d "${SECRET_DIRECTORY}" && ! -L "${SECRET_DIRECTORY}" ]] \
        || fail "the Stage 6 secret path is not a regular directory"
    resolved_secret_directory="$(realpath -e -- "${SECRET_DIRECTORY}")"
    [[ "${resolved_secret_directory}" == "${SECRET_DIRECTORY}" ]] \
        || fail "secret directory resolved outside the exact Stage 6 path"

    mapfile -t secret_entries < <(find "${resolved_secret_directory}" -mindepth 1 -maxdepth 1 -printf '%f\n' | LC_ALL=C sort)
    for secret_entry in "${secret_entries[@]}"; do
        [[ "${secret_entry}" == "chatengine.env" || "${secret_entry}" == "mysql.env" ]] \
            || fail "unexpected Stage 6 secret filename"
    done
    secret_directory_exists=true
fi

if [[ "${container_exists}" == "true" ]]; then
    docker stop --time 15 "${CONTAINER_NAME}" >/dev/null
    docker rm "${CONTAINER_NAME}" >/dev/null
fi
if [[ "${volume_exists}" == "true" ]]; then
    docker volume rm "${VOLUME_NAME}" >/dev/null
fi
if [[ "${network_exists}" == "true" ]]; then
    docker network rm "${NETWORK_NAME}" >/dev/null
fi

docker info >/dev/null 2>&1 || fail "Docker became unavailable during removal"
remaining_containers="$(docker container ls --all --filter "name=^${CONTAINER_NAME}$" --format '{{.Names}}')"
remaining_volumes="$(docker volume ls --filter "name=^${VOLUME_NAME}$" --format '{{.Name}}')"
remaining_networks="$(docker network ls --filter "name=^${NETWORK_NAME}$" --format '{{.Name}}')"
[[ -z "${remaining_containers}" ]] \
    || fail "the disposable container remains after removal"
[[ -z "${remaining_volumes}" ]] \
    || fail "the disposable volume remains after removal"
[[ -z "${remaining_networks}" ]] \
    || fail "the disposable network remains after removal"

if [[ "${secret_directory_exists}" == "true" ]]; then
    rm -f -- \
        "${resolved_secret_directory}/chatengine.env" \
        "${resolved_secret_directory}/mysql.env"
    rmdir -- "${resolved_secret_directory}"
fi

echo "PASS: exact labeled disposable Stage 6 MySQL container, volume, network, and credentials were removed."
