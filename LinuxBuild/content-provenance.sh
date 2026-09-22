#!/usr/bin/env bash
# Offline integrity of the operator-editable package. This is not a runtime
# authorization catalog: any structurally valid replacement data can be packaged.
content_provenance_tool="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/Tools/content_provenance.py"

content_provenance_evaluate()
{
    local mode="$1" root="$2" sha="$3" platform="$4" expected="${5:-}" output key value
    output="$(python3 "${content_provenance_tool}" "${mode}" "${root}" "${sha}" "${platform}" "${expected}")" || return 1
    while IFS='=' read -r key value; do
        value="${value%$'\r'}"
        case "${key}" in
            CONTENT_PROVENANCE_VERSION|CONTENT_MANIFEST_SHA256|CONTENT_FILE_COUNT|CONTENT_TOTAL_BYTES) printf -v "${key}" '%s' "${value}" ;;
            *) echo 'CONTENT_PROVENANCE_INVALID_OUTPUT' >&2; return 1 ;;
        esac
    done <<< "${output}"
}
content_provenance_write() { content_provenance_evaluate write "$@"; }
content_provenance_load() { content_provenance_evaluate check "$@"; }
content_append_build_provenance()
{
    cat >> "$1" <<EOF
CONTENT_PROVENANCE_VERSION=${CONTENT_PROVENANCE_VERSION}
CONTENT_MANIFEST_SHA256=${CONTENT_MANIFEST_SHA256}
CONTENT_FILE_COUNT=${CONTENT_FILE_COUNT}
CONTENT_TOTAL_BYTES=${CONTENT_TOTAL_BYTES}
EOF
}
content_require_build_provenance()
{
    local path="$1" key expected
    [[ -f "${path}" && ! -L "${path}" ]] || return 1
    for key in CONTENT_PROVENANCE_VERSION CONTENT_MANIFEST_SHA256 CONTENT_FILE_COUNT CONTENT_TOTAL_BYTES; do
        expected="${!key}"
        [[ "$(grep -c "^${key}=" "${path}" || true)" == 1 ]] || return 1
        grep -Fxq "${key}=${expected}" "${path}" || return 1
    done
}
