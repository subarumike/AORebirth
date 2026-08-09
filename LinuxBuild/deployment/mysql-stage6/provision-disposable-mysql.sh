#!/usr/bin/env bash
set -euo pipefail

readonly CONTAINER_NAME="aorebirth-chatengine-mysql-stage6"
readonly DATABASE_NAME="aorebirth_chatengine_stage6"
readonly DATABASE_USER="aorebirth_stage6"
readonly HOST_PORT="33067"
readonly IMAGE="mysql@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d"
readonly NETWORK_NAME="aorebirth_chatengine_stage6_internal"
readonly VOLUME_NAME="aorebirth_chatengine_mysql_stage6_data"
readonly SECRET_DIRECTORY="/etc/ao-rebirth/chatengine/stage6"
readonly MYSQL_ENVIRONMENT="${SECRET_DIRECTORY}/mysql.env"
readonly CHATENGINE_ENVIRONMENT="${SECRET_DIRECTORY}/chatengine.env"
readonly DISPOSABLE_LABEL="org.aorebirth.purpose=chatengine-stage6-disposable"
readonly ATTEMPT_LABEL_KEY="org.aorebirth.stage6-attempt"

fail()
{
    echo "REFUSED: $*" >&2
    exit 1
}

provision_succeeded=false
created_secret_directory=false
created_network_id=""
created_volume_name=""
created_volume_identity=""
created_container_id=""
provision_token=""

cleanup_failed_provision()
{
    local exit_status=$?
    local attempt_label
    local current_resource_id
    local current_volume_identity
    local docker_available=true
    local resource_label
    local resolved_secret_directory
    local rollback_incomplete=false

    trap - EXIT INT TERM
    if [[ "${provision_succeeded}" == "true" ]]; then
        exit "${exit_status}"
    fi

    set +e
    if ! docker info >/dev/null 2>&1; then
        docker_available=false
        rollback_incomplete=true
    fi

    if [[ "${docker_available}" == "true" ]]; then
        resource_label="$(docker inspect --format '{{index .Config.Labels "org.aorebirth.purpose"}}' "${CONTAINER_NAME}" 2>/dev/null)"
        attempt_label="$(docker inspect --format '{{index .Config.Labels "org.aorebirth.stage6-attempt"}}' "${CONTAINER_NAME}" 2>/dev/null)"
        current_resource_id="$(docker inspect --format '{{.Id}}' "${CONTAINER_NAME}" 2>/dev/null)"
        if [[ "${resource_label}" == "chatengine-stage6-disposable" \
            && "${attempt_label}" == "${provision_token}" \
            && ( -z "${created_container_id}" || "${current_resource_id}" == "${created_container_id}" ) ]]; then
            docker rm --force "${CONTAINER_NAME}" >/dev/null 2>&1 \
                || rollback_incomplete=true
        fi

        resource_label="$(docker volume inspect --format '{{index .Labels "org.aorebirth.purpose"}}' "${VOLUME_NAME}" 2>/dev/null)"
        attempt_label="$(docker volume inspect --format '{{index .Labels "org.aorebirth.stage6-attempt"}}' "${VOLUME_NAME}" 2>/dev/null)"
        current_volume_identity="$(docker volume inspect --format '{{.Name}}|{{.CreatedAt}}|{{.Mountpoint}}' "${VOLUME_NAME}" 2>/dev/null)"
        if [[ "${resource_label}" == "chatengine-stage6-disposable" \
            && "${attempt_label}" == "${provision_token}" \
            && ( -z "${created_volume_identity}" || "${current_volume_identity}" == "${created_volume_identity}" ) ]]; then
            docker volume rm "${VOLUME_NAME}" >/dev/null 2>&1 \
                || rollback_incomplete=true
        fi

        resource_label="$(docker network inspect --format '{{index .Labels "org.aorebirth.purpose"}}' "${NETWORK_NAME}" 2>/dev/null)"
        attempt_label="$(docker network inspect --format '{{index .Labels "org.aorebirth.stage6-attempt"}}' "${NETWORK_NAME}" 2>/dev/null)"
        current_resource_id="$(docker network inspect --format '{{.Id}}' "${NETWORK_NAME}" 2>/dev/null)"
        if [[ "${resource_label}" == "chatengine-stage6-disposable" \
            && "${attempt_label}" == "${provision_token}" \
            && ( -z "${created_network_id}" || "${current_resource_id}" == "${created_network_id}" ) ]]; then
            docker network rm "${NETWORK_NAME}" >/dev/null 2>&1 \
                || rollback_incomplete=true
        fi
    fi

    if [[ "${created_secret_directory}" == "true" \
        && -d "${SECRET_DIRECTORY}" \
        && ! -L "${SECRET_DIRECTORY}" ]]; then
        resolved_secret_directory="$(realpath -e -- "${SECRET_DIRECTORY}" 2>/dev/null)"
        if [[ "${resolved_secret_directory}" == "${SECRET_DIRECTORY}" ]]; then
            rm -f -- "${MYSQL_ENVIRONMENT}" "${CHATENGINE_ENVIRONMENT}"
            rmdir -- "${SECRET_DIRECTORY}" >/dev/null 2>&1
        fi
    fi

    if [[ "${docker_available}" == "true" ]]; then
        if docker inspect "${CONTAINER_NAME}" >/dev/null 2>&1; then
            rollback_incomplete=true
        fi
        if docker volume inspect "${VOLUME_NAME}" >/dev/null 2>&1; then
            rollback_incomplete=true
        fi
        if docker network inspect "${NETWORK_NAME}" >/dev/null 2>&1; then
            rollback_incomplete=true
        fi
        if ! docker info >/dev/null 2>&1; then
            rollback_incomplete=true
        fi
    fi
    if [[ "${created_secret_directory}" == "true" && ( -e "${SECRET_DIRECTORY}" || -L "${SECRET_DIRECTORY}" ) ]]; then
        rollback_incomplete=true
    fi

    if [[ "${rollback_incomplete}" == "true" ]]; then
        echo "REFUSED: provisioning failed and guarded rollback is incomplete; use the exact removal command" >&2
    else
        echo "REFUSED: provisioning failed; exact resources created by this attempt were rolled back" >&2
    fi
    exit "${exit_status}"
}

if [[ "${EUID}" -ne 0 ]]; then
    fail "run as root"
fi

for required_command in docker openssl ss; do
    command -v "${required_command}" >/dev/null 2>&1 \
        || fail "required command is unavailable: ${required_command}"
done

docker image inspect "${IMAGE}" >/dev/null 2>&1 \
    || fail "the pinned MySQL 8.4 image is not present locally"

if docker container inspect "${CONTAINER_NAME}" >/dev/null 2>&1; then
    fail "container already exists: ${CONTAINER_NAME}"
fi

if docker network inspect "${NETWORK_NAME}" >/dev/null 2>&1; then
    fail "network already exists: ${NETWORK_NAME}"
fi

if docker volume inspect "${VOLUME_NAME}" >/dev/null 2>&1; then
    fail "volume already exists: ${VOLUME_NAME}"
fi

if [[ -e "${SECRET_DIRECTORY}" || -L "${SECRET_DIRECTORY}" ]]; then
    fail "secret directory already exists: ${SECRET_DIRECTORY}"
fi

if ss -H -ltn "sport = :${HOST_PORT}" | grep -q .; then
    fail "loopback test port is already in use: ${HOST_PORT}"
fi

provision_token="$(openssl rand -hex 16)"
[[ -n "${provision_token}" ]] || fail "provisioning token generation failed"

trap cleanup_failed_provision EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

install -d -o root -g root -m 0700 "${SECRET_DIRECTORY}"
created_secret_directory=true
umask 077

mysql_root_password="$(openssl rand -base64 36 | tr -d '\r\n')"
mysql_app_password="$(openssl rand -base64 36 | tr -d '\r\n')"
[[ -n "${mysql_root_password}" && -n "${mysql_app_password}" ]] \
    || fail "password generation failed"

printf '%s\n' \
    "MYSQL_ROOT_PASSWORD=${mysql_root_password}" \
    "MYSQL_DATABASE=${DATABASE_NAME}" \
    "MYSQL_USER=${DATABASE_USER}" \
    "MYSQL_PASSWORD=${mysql_app_password}" \
    > "${MYSQL_ENVIRONMENT}"

printf '%s\n' \
    "AO_REBIRTH_CONFIG_PATH=/etc/ao-rebirth/chatengine/Config.xml" \
    "AO_REBIRTH_REQUIRED_SQL_TYPE=MySql" \
    "AO_REBIRTH_MYSQL_CONNECTION=Server=127.0.0.1;Port=${HOST_PORT};Database=${DATABASE_NAME};Uid=${DATABASE_USER};Pwd=${mysql_app_password};SslMode=None" \
    "AO_REBIRTH_STAGE6_DISPOSABLE_ACK=AO_REBIRTH_STAGE6_DISPOSABLE_ONLY" \
    > "${CHATENGINE_ENVIRONMENT}"

chown root:root "${MYSQL_ENVIRONMENT}" "${CHATENGINE_ENVIRONMENT}"
chmod 0600 "${MYSQL_ENVIRONMENT}" "${CHATENGINE_ENVIRONMENT}"

created_network_id="$(docker network create \
    --label "${DISPOSABLE_LABEL}" \
    --label "${ATTEMPT_LABEL_KEY}=${provision_token}" \
    "${NETWORK_NAME}")"
[[ -n "${created_network_id}" ]] || fail "network creation did not return an identity"
[[ "$(docker network inspect --format '{{index .Labels "org.aorebirth.stage6-attempt"}}' "${NETWORK_NAME}")" == "${provision_token}" ]] \
    || fail "network provisioning identity mismatch"

created_volume_name="$(docker volume create \
    --label "${DISPOSABLE_LABEL}" \
    --label "${ATTEMPT_LABEL_KEY}=${provision_token}" \
    "${VOLUME_NAME}")"
[[ "${created_volume_name}" == "${VOLUME_NAME}" ]] || fail "volume creation did not return the exact name"
[[ "$(docker volume inspect --format '{{index .Labels "org.aorebirth.stage6-attempt"}}' "${VOLUME_NAME}")" == "${provision_token}" ]] \
    || fail "volume provisioning identity mismatch"
created_volume_identity="$(docker volume inspect --format '{{.Name}}|{{.CreatedAt}}|{{.Mountpoint}}' "${VOLUME_NAME}")"
[[ -n "${created_volume_identity}" ]] || fail "volume creation did not return an identity"

created_container_id="$(docker run --detach \
    --name "${CONTAINER_NAME}" \
    --label "${DISPOSABLE_LABEL}" \
    --label "${ATTEMPT_LABEL_KEY}=${provision_token}" \
    --restart no \
    --network "${NETWORK_NAME}" \
    --publish "127.0.0.1:${HOST_PORT}:3306" \
    --env-file "${MYSQL_ENVIRONMENT}" \
    --volume "${VOLUME_NAME}:/var/lib/mysql" \
    --health-cmd='MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysqladmin ping -h 127.0.0.1 -uroot --silent' \
    --health-interval=2s \
    --health-timeout=2s \
    --health-retries=60 \
    "${IMAGE}")"
[[ -n "${created_container_id}" ]] || fail "container creation did not return an identity"
[[ "$(docker inspect --format '{{index .Config.Labels "org.aorebirth.stage6-attempt"}}' "${CONTAINER_NAME}")" == "${provision_token}" ]] \
    || fail "container provisioning identity mismatch"

health_status="starting"
for _ in $(seq 1 90); do
    health_status="$(docker inspect --format '{{.State.Health.Status}}' "${CONTAINER_NAME}")"
    if [[ "${health_status}" == "healthy" ]]; then
        break
    fi

    if [[ "${health_status}" == "unhealthy" ]]; then
        fail "the disposable MySQL container became unhealthy"
    fi

    sleep 2
done

[[ "${health_status}" == "healthy" ]] \
    || fail "timed out waiting for disposable MySQL readiness"

published_binding="$(docker port "${CONTAINER_NAME}" 3306/tcp)"
[[ "${published_binding}" == "127.0.0.1:${HOST_PORT}" ]] \
    || fail "unexpected database port binding"

attached_networks="$(docker inspect --format '{{range $name, $_ := .NetworkSettings.Networks}}{{$name}}{{"\n"}}{{end}}' "${CONTAINER_NAME}")"
[[ "${attached_networks}" == "${NETWORK_NAME}" ]] \
    || fail "unexpected disposable database network attachment"

provision_succeeded=true
echo "PASS: isolated disposable MySQL is healthy on loopback port ${HOST_PORT}; credentials were stored root-only."
