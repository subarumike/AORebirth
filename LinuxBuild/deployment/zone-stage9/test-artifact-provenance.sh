#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
gate="${script_dir}/upgrade-live-service.sh"
fixture="$(mktemp -d)"
fake_sha="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
other_sha="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
tests_run=0
artifact_index=0
trap 'rm -rf -- "${fixture}"' EXIT

fail() { echo "FAIL: $*" >&2; exit 1; }

create_artifact()
{
    local shard_id shard_sha entry_suffix
    artifact_index=$((artifact_index + 1))
    artifact="${fixture}/artifact-${artifact_index}"
    corpus="${artifact}/Content/Official/PlayfieldPlacements"
    mkdir -p -- "${artifact}/XML Data" "${corpus}/placements"
    printf 'apphost\n' > "${artifact}/ZoneEngine"
    printf 'assembly\n' > "${artifact}/ZoneEngine.dll"
    printf 'config\n' > "${artifact}/Config.xml"
    printf 'items\n' > "${artifact}/items.dat"
    printf 'nanos\n' > "${artifact}/nanos.dat"
    printf 'playfields\n' > "${artifact}/playfields.dat"
    printf 'stats\n' > "${artifact}/XML Data/Stats.xml"
    printf 'playfields xml\n' > "${artifact}/XML Data/Playfields.xml"
    printf '%s\n' "${fake_sha}" > "${artifact}/SOURCE_SHA"

    printf '{"fixture":"summary"}\n' > "${corpus}/official-placement-summary.json"
    printf '{"fixture":"index"}\n' > "${corpus}/official-placement-index.json"
    printf '{"fixture":"acghash"}\n' > "${corpus}/official-acghash-inventory.json"
    for shard_id in $(seq 1 630); do
        printf '{}\n' > "${corpus}/placements/pf_${shard_id}.json"
    done

    summary_sha="$(sha256sum "${corpus}/official-placement-summary.json" | awk '{print $1}')"
    index_sha="$(sha256sum "${corpus}/official-placement-index.json" | awk '{print $1}')"
    acghash_sha="$(sha256sum "${corpus}/official-acghash-inventory.json" | awk '{print $1}')"
    shard_sha="$(sha256sum "${corpus}/placements/pf_1.json" | awk '{print $1}')"
    {
        printf '{\n'
        printf '  "AcgHashInventorySha256": "%s",\n' "${acghash_sha}"
        printf '  "CorpusVersion": "fixture-corpus-v1",\n'
        printf '  "IndexSha256": "%s",\n' "${index_sha}"
        printf '  "Metrics": {"ResourceCount": 630},\n'
        printf '  "Playfields": [\n'
        for shard_id in $(seq 1 630); do
            entry_suffix=,
            [[ "${shard_id}" == "630" ]] && entry_suffix=
            printf '    {\n'
            printf '      "Path": "placements/pf_%s.json",\n' "${shard_id}"
            printf '      "ShardSha256": "%s"\n' "${shard_sha}"
            printf '    }%s\n' "${entry_suffix}"
        done
        printf '  ],\n'
        printf '  "SummarySha256": "%s"\n' "${summary_sha}"
        printf '}\n'
    } > "${corpus}/official-placement-corpus-manifest.json"
    corpus_manifest_sha="$(sha256sum "${corpus}/official-placement-corpus-manifest.json" | awk '{print $1}')"
    printf '{"SchemaVersion":1,"SourceSHA":"%s","CorpusVersion":"fixture-corpus-v1","CorpusManifestSha256":"%s","IndexSha256":"%s","SummarySha256":"%s","AcgHashInventorySha256":"%s"}\n' \
        "${fake_sha}" \
        "${corpus_manifest_sha}" \
        "${index_sha}" \
        "${summary_sha}" \
        "${acghash_sha}" \
        > "${corpus}/official-placement-build-manifest.json"
    build_manifest_sha="$(sha256sum "${corpus}/official-placement-build-manifest.json" | awk '{print $1}')"
    cat > "${corpus}/PLACEMENT_PROVENANCE.env" <<EOF
SOURCE_SHA=${fake_sha}
BUILD_PLATFORM=linux
PLACEMENT_CORPUS_VERSION=fixture-corpus-v1
PLACEMENT_CORPUS_MANIFEST_SHA256=${corpus_manifest_sha}
PLACEMENT_CORPUS_SUMMARY_SHA256=${summary_sha}
PLACEMENT_CORPUS_INDEX_SHA256=${index_sha}
PLACEMENT_ACGHASH_INVENTORY_SHA256=${acghash_sha}
PLACEMENT_BUILD_MANIFEST_SHA256=${build_manifest_sha}
PLACEMENT_RESOURCE_COUNT=630
PLACEMENT_PARSED_RESOURCE_COUNT=627
PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT=3
PLACEMENT_DISTRICT_COUNT=4146
PLACEMENT_RECORD_COUNT=32805
PLACEMENT_UNIQUE_ACGHASH_COUNT=4016
PLACEMENT_RUNTIME_AUTHORIZED_COUNT=25
EOF
    cat > "${artifact}/BUILD_PROVENANCE.env" <<EOF
COMMIT_SHA=${fake_sha}
PLACEMENT_CORPUS_VERSION=fixture-corpus-v1
PLACEMENT_CORPUS_MANIFEST_SHA256=${corpus_manifest_sha}
PLACEMENT_CORPUS_SUMMARY_SHA256=${summary_sha}
PLACEMENT_CORPUS_INDEX_SHA256=${index_sha}
PLACEMENT_ACGHASH_INVENTORY_SHA256=${acghash_sha}
PLACEMENT_BUILD_MANIFEST_SHA256=${build_manifest_sha}
PLACEMENT_RESOURCE_COUNT=630
PLACEMENT_PARSED_RESOURCE_COUNT=627
PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT=3
PLACEMENT_DISTRICT_COUNT=4146
PLACEMENT_RECORD_COUNT=32805
PLACEMENT_UNIQUE_ACGHASH_COUNT=4016
PLACEMENT_RUNTIME_AUTHORIZED_COUNT=25
EOF
    cat > "${artifact}/LINUX_ACCEPTANCE.env" <<EOF
AO_REBIRTH_SOURCE_SHA=${fake_sha}
EXPECTED_SOURCE_SHA=${fake_sha}
SOURCE_SHA_MATCH=PASS
TRACKED_SOURCE_CLEAN=PASS
LINUX_ACCEPTANCE=PASS
PLACEMENT_VALIDATION=PASS
EXPECTED_PLACEMENT_BUILD_MANIFEST_SHA256=${build_manifest_sha}
PLACEMENT_BUILD_MANIFEST_SHA256=${build_manifest_sha}
EOF
}

run_gate()
{
    bash "${gate}" --validate-artifact-provenance "${artifact}" "${1:-${fake_sha}}"
}

expect_failure()
{
    if run_gate "${1:-${fake_sha}}" > "${fixture}/failure-output" 2>&1; then
        fail "expected artifact provenance rejection"
    fi
    tests_run=$((tests_run + 1))
}

create_artifact
run_gate > "${fixture}/success-output"
grep -Fx "PASS: artifact provenance matches expected source SHA." "${fixture}/success-output" >/dev/null \
    || fail "valid placement artifact did not pass"
tests_run=$((tests_run + 1))

create_artifact; rm -f -- "${corpus}/official-placement-build-manifest.json"; expect_failure
create_artifact; printf 'tampered\n' >> "${corpus}/official-placement-index.json"; expect_failure
create_artifact; rm -f -- "${corpus}/placements/pf_630.json"; expect_failure
create_artifact; printf 'tampered\n' >> "${corpus}/placements/pf_1.json"; expect_failure
create_artifact; expect_failure "${other_sha}"
create_artifact; sed -i '/^PLACEMENT_CORPUS_SUMMARY_SHA256=/d' "${artifact}/BUILD_PROVENANCE.env"; expect_failure
create_artifact; sed -i 's/^EXPECTED_PLACEMENT_BUILD_MANIFEST_SHA256=.*/EXPECTED_PLACEMENT_BUILD_MANIFEST_SHA256=ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff/' "${artifact}/LINUX_ACCEPTANCE.env"; expect_failure

[[ "${tests_run}" == "8" ]] || fail "unexpected artifact provenance test count: ${tests_run}"
echo "PASS: ZoneEngine placement artifact provenance tests (8/8)"
