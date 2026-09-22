#!/usr/bin/env bash

# Shared fail-closed validation for the ZoneEngine official-placement artifact.
# Callers keep ownership of their user-facing failure marker and should invoke
# placement_provenance_load in an `if ! ...; then fail ...; fi` guard.

placement_provenance_error()
{
    echo "PLACEMENT_PROVENANCE_ERROR: $*" >&2
    return 1
}

placement_require_regular_file()
{
    [[ -f "$1" && ! -L "$1" ]] \
        || placement_provenance_error "required regular file is missing or unsafe: $1"
}

placement_env_value()
{
    local path="$1" key="$2" count line
    count="$(grep -Ec "^${key}=" "${path}" || true)"
    [[ "${count}" == "1" ]] \
        || placement_provenance_error "${path} must contain exactly one ${key} assignment" \
        || return 1
    line="$(grep -E "^${key}=" "${path}")"
    printf '%s' "${line#*=}"
}

placement_require_sha256()
{
    [[ "$1" =~ ^[0-9a-f]{64}$ ]] \
        || placement_provenance_error "$2 is not a lowercase SHA-256 digest"
}

placement_require_env_line()
{
    local path="$1" key="$2" value="$3"
    [[ "$(grep -Fxc -- "${key}=${value}" "${path}" || true)" == "1" ]] \
        || placement_provenance_error "${path} does not contain exact ${key} provenance"
}

placement_sha256()
{
    sha256sum "$1" | awk '{print $1}'
}

placement_require_manifest_string()
{
    local path="$1" key="$2" expected="$3" actual
    actual="$(
        awk -F'"' -v key="${key}" '
            $2 == key { count++; value = $4 }
            END { if (count != 1) exit 1; print value }
        ' "${path}"
    )" \
        || placement_provenance_error "${path} must contain exactly one ${key} string" \
        || return 1
    [[ "${actual}" == "${expected}" ]] \
        || placement_provenance_error "${path} ${key} does not match artifact provenance"
}

placement_require_build_manifest_string()
{
    local path="$1" key="$2" expected="$3"
    grep -Fq -- "\"${key}\":\"${expected}\"" "${path}" \
        || placement_provenance_error "${path} ${key} does not match artifact provenance"
}

placement_require_shard_digests()
{
    local manifest_path="$1" corpus_root="$2"
    local inventory entry_count unique_path_count digest relative_path
    inventory="$(
        awk -F'"' '
            $2 == "Path" {
                if (path != "") exit 2
                path = $4
                next
            }
            $2 == "ShardSha256" {
                if (path == "") exit 3
                print $4 "  " path
                path = ""
                count++
            }
            END {
                if (path != "" || count != 630) exit 4
            }
        ' "${manifest_path}"
    )" \
        || placement_provenance_error "official placement corpus manifest shard inventory is invalid" \
        || return 1

    entry_count="$(printf '%s\n' "${inventory}" | wc -l | tr -d '[:space:]')"
    [[ "${entry_count}" == "630" ]] \
        || placement_provenance_error "official placement corpus manifest shard inventory count is ${entry_count}, expected 630" \
        || return 1

    while read -r digest relative_path; do
        [[ "${digest}" =~ ^[0-9a-f]{64}$ ]] \
            || placement_provenance_error "official placement corpus manifest contains an invalid shard digest" \
            || return 1
        [[ "${relative_path}" =~ ^placements/pf_[0-9]+\.json$ ]] \
            || placement_provenance_error "official placement corpus manifest contains an unsafe shard path" \
            || return 1
    done <<< "${inventory}"

    unique_path_count="$(
        printf '%s\n' "${inventory}" | awk '{print $2}' | sort -u | wc -l | tr -d '[:space:]'
    )"
    [[ "${unique_path_count}" == "630" ]] \
        || placement_provenance_error "official placement corpus manifest shard paths are not unique" \
        || return 1

    (
        cd -- "${corpus_root}"
        printf '%s\n' "${inventory}" | sha256sum --check --strict --status
    ) \
        || placement_provenance_error "official placement shard digest mismatch"
}

placement_provenance_load()
{
    local artifact_dir="$1"
    local expected_source_sha="$2"
    local expected_build_platform="$3"
    local expected_build_manifest_sha="${4:-}"
    local corpus_root placements_root shard_count

    [[ "${expected_source_sha}" =~ ^[0-9a-f]{40}$ ]] \
        || placement_provenance_error "expected source SHA is invalid" \
        || return 1
    case "${expected_build_platform}" in
        linux|windows-hosted-linux-publish) ;;
        *) placement_provenance_error "expected build platform is invalid"; return 1 ;;
    esac
    if [[ -n "${expected_build_manifest_sha}" ]]; then
        placement_require_sha256 "${expected_build_manifest_sha}" \
            "expected placement build manifest SHA" || return 1
    fi

    corpus_root="${artifact_dir}/Content/Official/PlayfieldPlacements"
    placements_root="${corpus_root}/placements"
    [[ -d "${corpus_root}" && ! -L "${corpus_root}" ]] \
        || placement_provenance_error "official placement corpus directory is missing or unsafe" \
        || return 1
    [[ -d "${placements_root}" && ! -L "${placements_root}" ]] \
        || placement_provenance_error "official placement shard directory is missing or unsafe" \
        || return 1

    PLACEMENT_CORPUS_ROOT="${corpus_root}"
    PLACEMENT_CORPUS_MANIFEST_PATH="${corpus_root}/official-placement-corpus-manifest.json"
    PLACEMENT_CORPUS_SUMMARY_PATH="${corpus_root}/official-placement-summary.json"
    PLACEMENT_CORPUS_INDEX_PATH="${corpus_root}/official-placement-index.json"
    PLACEMENT_ACGHASH_INVENTORY_PATH="${corpus_root}/official-acghash-inventory.json"
    PLACEMENT_BUILD_MANIFEST_PATH="${corpus_root}/official-placement-build-manifest.json"
    PLACEMENT_PROVENANCE_PATH="${corpus_root}/PLACEMENT_PROVENANCE.env"

    placement_require_regular_file "${PLACEMENT_CORPUS_MANIFEST_PATH}" || return 1
    placement_require_regular_file "${PLACEMENT_CORPUS_SUMMARY_PATH}" || return 1
    placement_require_regular_file "${PLACEMENT_CORPUS_INDEX_PATH}" || return 1
    placement_require_regular_file "${PLACEMENT_ACGHASH_INVENTORY_PATH}" || return 1
    placement_require_regular_file "${PLACEMENT_BUILD_MANIFEST_PATH}" || return 1
    placement_require_regular_file "${PLACEMENT_PROVENANCE_PATH}" || return 1

    [[ -z "$(find "${corpus_root}" -type l -print -quit)" ]] \
        || placement_provenance_error "official placement corpus contains a symlink" \
        || return 1
    [[ -z "$(find "${corpus_root}" ! -type f ! -type d -print -quit)" ]] \
        || placement_provenance_error "official placement corpus contains a special file" \
        || return 1
    shard_count="$(find "${placements_root}" -maxdepth 1 -type f -name 'pf_*.json' | wc -l | tr -d '[:space:]')"
    [[ "${shard_count}" == "630" ]] \
        || placement_provenance_error "official placement shard count is ${shard_count}, expected 630" \
        || return 1
    [[ -z "$(find "${placements_root}" -maxdepth 1 -type f ! -name 'pf_*.json' -print -quit)" ]] \
        || placement_provenance_error "official placement shard directory contains an unexpected file" \
        || return 1

    PLACEMENT_SOURCE_SHA="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" SOURCE_SHA)" || return 1
    PLACEMENT_BUILD_PLATFORM="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" BUILD_PLATFORM)" || return 1
    PLACEMENT_CORPUS_VERSION="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_CORPUS_VERSION)" || return 1
    PLACEMENT_CORPUS_MANIFEST_SHA256="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_CORPUS_MANIFEST_SHA256)" || return 1
    PLACEMENT_CORPUS_SUMMARY_SHA256="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_CORPUS_SUMMARY_SHA256)" || return 1
    PLACEMENT_CORPUS_INDEX_SHA256="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_CORPUS_INDEX_SHA256)" || return 1
    PLACEMENT_ACGHASH_INVENTORY_SHA256="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_ACGHASH_INVENTORY_SHA256)" || return 1
    PLACEMENT_BUILD_MANIFEST_SHA256="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_BUILD_MANIFEST_SHA256)" || return 1
    PLACEMENT_RESOURCE_COUNT="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_RESOURCE_COUNT)" || return 1
    PLACEMENT_PARSED_RESOURCE_COUNT="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_PARSED_RESOURCE_COUNT)" || return 1
    PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT)" || return 1
    PLACEMENT_DISTRICT_COUNT="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_DISTRICT_COUNT)" || return 1
    PLACEMENT_RECORD_COUNT="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_RECORD_COUNT)" || return 1
    PLACEMENT_UNIQUE_ACGHASH_COUNT="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_UNIQUE_ACGHASH_COUNT)" || return 1
    PLACEMENT_RUNTIME_AUTHORIZED_COUNT="$(placement_env_value "${PLACEMENT_PROVENANCE_PATH}" PLACEMENT_RUNTIME_AUTHORIZED_COUNT)" || return 1

    [[ "${PLACEMENT_SOURCE_SHA}" == "${expected_source_sha}" ]] \
        || placement_provenance_error "placement source SHA does not match expected source SHA" \
        || return 1
    [[ "${PLACEMENT_BUILD_PLATFORM}" == "${expected_build_platform}" ]] \
        || placement_provenance_error "placement build platform does not match expected platform" \
        || return 1
    [[ -n "${PLACEMENT_CORPUS_VERSION}" ]] \
        || placement_provenance_error "placement corpus version is empty" \
        || return 1
    [[ "${PLACEMENT_RESOURCE_COUNT}" == "630" ]] \
        || placement_provenance_error "official playfield resource count drifted" \
        || return 1
    [[ "${PLACEMENT_PARSED_RESOURCE_COUNT}" == "627" ]] \
        || placement_provenance_error "parsed official playfield resource count drifted" \
        || return 1
    [[ "${PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT}" == "3" ]] \
        || placement_provenance_error "parser-limited official playfield resource count drifted" \
        || return 1
    [[ "${PLACEMENT_DISTRICT_COUNT}" == "4146" ]] \
        || placement_provenance_error "official district count drifted" \
        || return 1
    [[ "${PLACEMENT_RECORD_COUNT}" == "32805" ]] \
        || placement_provenance_error "official placement count drifted" \
        || return 1
    [[ "${PLACEMENT_UNIQUE_ACGHASH_COUNT}" == "4016" ]] \
        || placement_provenance_error "official ACGHash inventory count drifted" \
        || return 1
    [[ "${PLACEMENT_RUNTIME_AUTHORIZED_COUNT}" == "199" ]] \
        || placement_provenance_error "official placement runtime authorization count drifted" \
        || return 1

    placement_require_sha256 "${PLACEMENT_CORPUS_MANIFEST_SHA256}" \
        PLACEMENT_CORPUS_MANIFEST_SHA256 || return 1
    placement_require_sha256 "${PLACEMENT_CORPUS_SUMMARY_SHA256}" \
        PLACEMENT_CORPUS_SUMMARY_SHA256 || return 1
    placement_require_sha256 "${PLACEMENT_CORPUS_INDEX_SHA256}" \
        PLACEMENT_CORPUS_INDEX_SHA256 || return 1
    placement_require_sha256 "${PLACEMENT_ACGHASH_INVENTORY_SHA256}" \
        PLACEMENT_ACGHASH_INVENTORY_SHA256 || return 1
    placement_require_sha256 "${PLACEMENT_BUILD_MANIFEST_SHA256}" \
        PLACEMENT_BUILD_MANIFEST_SHA256 || return 1

    [[ "$(placement_sha256 "${PLACEMENT_CORPUS_MANIFEST_PATH}")" == "${PLACEMENT_CORPUS_MANIFEST_SHA256}" ]] \
        || placement_provenance_error "official placement corpus manifest digest mismatch" \
        || return 1
    [[ "$(placement_sha256 "${PLACEMENT_CORPUS_SUMMARY_PATH}")" == "${PLACEMENT_CORPUS_SUMMARY_SHA256}" ]] \
        || placement_provenance_error "official placement summary digest mismatch" \
        || return 1
    [[ "$(placement_sha256 "${PLACEMENT_CORPUS_INDEX_PATH}")" == "${PLACEMENT_CORPUS_INDEX_SHA256}" ]] \
        || placement_provenance_error "official placement index digest mismatch" \
        || return 1
    [[ "$(placement_sha256 "${PLACEMENT_ACGHASH_INVENTORY_PATH}")" == "${PLACEMENT_ACGHASH_INVENTORY_SHA256}" ]] \
        || placement_provenance_error "official placement ACGHash inventory digest mismatch" \
        || return 1
    [[ "$(placement_sha256 "${PLACEMENT_BUILD_MANIFEST_PATH}")" == "${PLACEMENT_BUILD_MANIFEST_SHA256}" ]] \
        || placement_provenance_error "official placement build manifest digest mismatch" \
        || return 1

    placement_require_manifest_string "${PLACEMENT_CORPUS_MANIFEST_PATH}" CorpusVersion "${PLACEMENT_CORPUS_VERSION}" || return 1
    placement_require_manifest_string "${PLACEMENT_CORPUS_MANIFEST_PATH}" SummarySha256 "${PLACEMENT_CORPUS_SUMMARY_SHA256}" || return 1
    placement_require_manifest_string "${PLACEMENT_CORPUS_MANIFEST_PATH}" IndexSha256 "${PLACEMENT_CORPUS_INDEX_SHA256}" || return 1
    placement_require_manifest_string "${PLACEMENT_CORPUS_MANIFEST_PATH}" AcgHashInventorySha256 "${PLACEMENT_ACGHASH_INVENTORY_SHA256}" || return 1
    placement_require_build_manifest_string "${PLACEMENT_BUILD_MANIFEST_PATH}" SourceSHA "${PLACEMENT_SOURCE_SHA}" || return 1
    placement_require_build_manifest_string "${PLACEMENT_BUILD_MANIFEST_PATH}" CorpusVersion "${PLACEMENT_CORPUS_VERSION}" || return 1
    placement_require_build_manifest_string "${PLACEMENT_BUILD_MANIFEST_PATH}" CorpusManifestSha256 "${PLACEMENT_CORPUS_MANIFEST_SHA256}" || return 1
    placement_require_build_manifest_string "${PLACEMENT_BUILD_MANIFEST_PATH}" SummarySha256 "${PLACEMENT_CORPUS_SUMMARY_SHA256}" || return 1
    placement_require_build_manifest_string "${PLACEMENT_BUILD_MANIFEST_PATH}" IndexSha256 "${PLACEMENT_CORPUS_INDEX_SHA256}" || return 1
    placement_require_build_manifest_string "${PLACEMENT_BUILD_MANIFEST_PATH}" AcgHashInventorySha256 "${PLACEMENT_ACGHASH_INVENTORY_SHA256}" || return 1
    placement_require_shard_digests "${PLACEMENT_CORPUS_MANIFEST_PATH}" "${PLACEMENT_CORPUS_ROOT}" || return 1

    [[ -z "${expected_build_manifest_sha}" \
        || "${PLACEMENT_BUILD_MANIFEST_SHA256}" == "${expected_build_manifest_sha}" ]] \
        || placement_provenance_error "official placement build manifest does not match the accepted Windows digest" \
        || return 1
}

placement_require_build_provenance()
{
    local build_provenance_path="$1"
    placement_require_regular_file "${build_provenance_path}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_CORPUS_VERSION "${PLACEMENT_CORPUS_VERSION}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_CORPUS_MANIFEST_SHA256 "${PLACEMENT_CORPUS_MANIFEST_SHA256}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_CORPUS_SUMMARY_SHA256 "${PLACEMENT_CORPUS_SUMMARY_SHA256}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_CORPUS_INDEX_SHA256 "${PLACEMENT_CORPUS_INDEX_SHA256}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_ACGHASH_INVENTORY_SHA256 "${PLACEMENT_ACGHASH_INVENTORY_SHA256}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_BUILD_MANIFEST_SHA256 "${PLACEMENT_BUILD_MANIFEST_SHA256}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_RESOURCE_COUNT "${PLACEMENT_RESOURCE_COUNT}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_PARSED_RESOURCE_COUNT "${PLACEMENT_PARSED_RESOURCE_COUNT}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT "${PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_DISTRICT_COUNT "${PLACEMENT_DISTRICT_COUNT}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_RECORD_COUNT "${PLACEMENT_RECORD_COUNT}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_UNIQUE_ACGHASH_COUNT "${PLACEMENT_UNIQUE_ACGHASH_COUNT}" || return 1
    placement_require_env_line "${build_provenance_path}" PLACEMENT_RUNTIME_AUTHORIZED_COUNT "${PLACEMENT_RUNTIME_AUTHORIZED_COUNT}" || return 1
}

placement_append_build_provenance()
{
    local path="$1"
    cat >> "${path}" <<EOF
PLACEMENT_CORPUS_VERSION=${PLACEMENT_CORPUS_VERSION}
PLACEMENT_CORPUS_MANIFEST_SHA256=${PLACEMENT_CORPUS_MANIFEST_SHA256}
PLACEMENT_CORPUS_SUMMARY_SHA256=${PLACEMENT_CORPUS_SUMMARY_SHA256}
PLACEMENT_CORPUS_INDEX_SHA256=${PLACEMENT_CORPUS_INDEX_SHA256}
PLACEMENT_ACGHASH_INVENTORY_SHA256=${PLACEMENT_ACGHASH_INVENTORY_SHA256}
PLACEMENT_BUILD_MANIFEST_SHA256=${PLACEMENT_BUILD_MANIFEST_SHA256}
PLACEMENT_RESOURCE_COUNT=${PLACEMENT_RESOURCE_COUNT}
PLACEMENT_PARSED_RESOURCE_COUNT=${PLACEMENT_PARSED_RESOURCE_COUNT}
PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT=${PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT}
PLACEMENT_DISTRICT_COUNT=${PLACEMENT_DISTRICT_COUNT}
PLACEMENT_RECORD_COUNT=${PLACEMENT_RECORD_COUNT}
PLACEMENT_UNIQUE_ACGHASH_COUNT=${PLACEMENT_UNIQUE_ACGHASH_COUNT}
PLACEMENT_RUNTIME_AUTHORIZED_COUNT=${PLACEMENT_RUNTIME_AUTHORIZED_COUNT}
EOF
}
