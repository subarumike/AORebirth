#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/../../../.." && pwd)"
upgrader="${repository_root}/LinuxBuild/deployment/production-release/upgrade-active-services.sh"
login_unit_source="${repository_root}/LinuxBuild/deployment/systemd/ao-rebirth-loginengine.service"
zone_unit_source="${repository_root}/LinuxBuild/deployment/systemd/ao-rebirth-zoneengine.service"
fake_sha="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
other_sha="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
tests_run=0

fail() { echo "FAIL: $*" >&2; exit 1; }
require() { "$@" || fail "assertion failed: $*"; }
set_manifest_value()
{
    awk -F= -v key="$2" -v value="$3" '$1 == key { print key "=" value; next } { print }' "$1" > "$1.tmp"
    mv -f -- "$1.tmp" "$1"
}
create_placement_artifact()
{
    local artifact_dir="$1"
    local corpus_dir="${artifact_dir}/Content/Official/PlayfieldPlacements"
    local shard_id shard_sha entry_suffix
    mkdir -p -- "${corpus_dir}/placements"
    printf '{"fixture":"summary"}\n' > "${corpus_dir}/official-placement-summary.json"
    printf '{"fixture":"index"}\n' > "${corpus_dir}/official-placement-index.json"
    printf '{"fixture":"acghash"}\n' > "${corpus_dir}/official-acghash-inventory.json"
    for shard_id in $(seq 1 630); do
        printf '{}\n' > "${corpus_dir}/placements/pf_${shard_id}.json"
    done

    fixture_placement_corpus_version="fixture-corpus-v1"
    fixture_placement_summary_sha="$(sha256sum "${corpus_dir}/official-placement-summary.json" | awk '{print $1}')"
    fixture_placement_index_sha="$(sha256sum "${corpus_dir}/official-placement-index.json" | awk '{print $1}')"
    fixture_placement_acghash_sha="$(sha256sum "${corpus_dir}/official-acghash-inventory.json" | awk '{print $1}')"
    shard_sha="$(sha256sum "${corpus_dir}/placements/pf_1.json" | awk '{print $1}')"
    {
        printf '{\n'
        printf '  "AcgHashInventorySha256": "%s",\n' "${fixture_placement_acghash_sha}"
        printf '  "CorpusVersion": "%s",\n' "${fixture_placement_corpus_version}"
        printf '  "IndexSha256": "%s",\n' "${fixture_placement_index_sha}"
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
        printf '  "SummarySha256": "%s"\n' "${fixture_placement_summary_sha}"
        printf '}\n'
    } > "${corpus_dir}/official-placement-corpus-manifest.json"
    fixture_placement_corpus_manifest_sha="$(sha256sum "${corpus_dir}/official-placement-corpus-manifest.json" | awk '{print $1}')"
    printf '{"SchemaVersion":1,"SourceSHA":"%s","CorpusVersion":"%s","CorpusManifestSha256":"%s","IndexSha256":"%s","SummarySha256":"%s","AcgHashInventorySha256":"%s"}\n' \
        "${fake_sha}" \
        "${fixture_placement_corpus_version}" \
        "${fixture_placement_corpus_manifest_sha}" \
        "${fixture_placement_index_sha}" \
        "${fixture_placement_summary_sha}" \
        "${fixture_placement_acghash_sha}" \
        > "${corpus_dir}/official-placement-build-manifest.json"
    fixture_placement_build_manifest_sha="$(sha256sum "${corpus_dir}/official-placement-build-manifest.json" | awk '{print $1}')"
    cat > "${corpus_dir}/PLACEMENT_PROVENANCE.env" <<EOF
SOURCE_SHA=${fake_sha}
BUILD_PLATFORM=linux
PLACEMENT_CORPUS_VERSION=${fixture_placement_corpus_version}
PLACEMENT_CORPUS_MANIFEST_SHA256=${fixture_placement_corpus_manifest_sha}
PLACEMENT_CORPUS_SUMMARY_SHA256=${fixture_placement_summary_sha}
PLACEMENT_CORPUS_INDEX_SHA256=${fixture_placement_index_sha}
PLACEMENT_ACGHASH_INVENTORY_SHA256=${fixture_placement_acghash_sha}
PLACEMENT_BUILD_MANIFEST_SHA256=${fixture_placement_build_manifest_sha}
PLACEMENT_RESOURCE_COUNT=630
PLACEMENT_PARSED_RESOURCE_COUNT=627
PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT=3
PLACEMENT_DISTRICT_COUNT=4146
PLACEMENT_RECORD_COUNT=32805
PLACEMENT_UNIQUE_ACGHASH_COUNT=4016
PLACEMENT_RUNTIME_AUTHORIZED_COUNT=199
EOF
    cat >> "${artifact_dir}/BUILD_PROVENANCE.env" <<EOF
PLACEMENT_CORPUS_VERSION=${fixture_placement_corpus_version}
PLACEMENT_CORPUS_MANIFEST_SHA256=${fixture_placement_corpus_manifest_sha}
PLACEMENT_CORPUS_SUMMARY_SHA256=${fixture_placement_summary_sha}
PLACEMENT_CORPUS_INDEX_SHA256=${fixture_placement_index_sha}
PLACEMENT_ACGHASH_INVENTORY_SHA256=${fixture_placement_acghash_sha}
PLACEMENT_BUILD_MANIFEST_SHA256=${fixture_placement_build_manifest_sha}
PLACEMENT_RESOURCE_COUNT=630
PLACEMENT_PARSED_RESOURCE_COUNT=627
PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT=3
PLACEMENT_DISTRICT_COUNT=4146
PLACEMENT_RECORD_COUNT=32805
PLACEMENT_UNIQUE_ACGHASH_COUNT=4016
PLACEMENT_RUNTIME_AUTHORIZED_COUNT=199
EOF
    cat >> "${artifact_dir}/LINUX_ACCEPTANCE.env" <<EOF
PLACEMENT_VALIDATION=PASS
EXPECTED_PLACEMENT_BUILD_MANIFEST_SHA256=${fixture_placement_build_manifest_sha}
PLACEMENT_BUILD_MANIFEST_SHA256=${fixture_placement_build_manifest_sha}
EOF
}
create_artifact()
{
    mkdir -p -- "$1"
    printf '#!/usr/bin/env bash\nexit 0\n' > "$1/$2"
    chmod 0755 "$1/$2"
    printf '%s\n' "${fake_sha}" > "$1/SOURCE_SHA"
    printf 'COMMIT_SHA=%s\n' "${fake_sha}" > "$1/BUILD_PROVENANCE.env"
    cat > "$1/LINUX_ACCEPTANCE.env" <<EOF
AO_REBIRTH_SOURCE_SHA=${fake_sha}
SOURCE_SHA_MATCH=PASS
TRACKED_SOURCE_CLEAN=PASS
LINUX_ACCEPTANCE=PASS
EOF
    if [[ "$2" == "ZoneEngine" ]]; then
        create_placement_artifact "$1"
    fi
}
create_fixture()
{
    fixture="$(mktemp -d)"
    root="${fixture}/root"
    input="${fixture}/input"
    state="${root}/test-state"
    mkdir -p "${root}/opt/ao-rebirth/loginengine/releases/old-login" "${root}/opt/ao-rebirth/zoneengine/releases/old-zone" \
        "${root}/opt/ao-rebirth/deployment-snapshots" "${root}/etc/systemd/system" \
        "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d" \
        "${root}/etc/ao-rebirth/loginengine" "${root}/etc/ao-rebirth/zoneengine" "${root}/var/lib" "${state}" "${input}"
    printf 'old-login\n' > "${root}/opt/ao-rebirth/loginengine/releases/old-login/LoginEngine"
    printf 'old-zone\n' > "${root}/opt/ao-rebirth/zoneengine/releases/old-zone/ZoneEngine"
    printf '%s\n' "${other_sha}" > "${root}/opt/ao-rebirth/loginengine/releases/old-login/SOURCE_SHA"
    printf '%s\n' "${other_sha}" > "${root}/opt/ao-rebirth/zoneengine/releases/old-zone/SOURCE_SHA"
    chmod 0755 "${root}/opt/ao-rebirth/loginengine/releases/old-login/LoginEngine" "${root}/opt/ao-rebirth/zoneengine/releases/old-zone/ZoneEngine"
    prior_login_link_target="releases/old-login"
    prior_zone_link_target="releases/old-zone"
    MSYS=winsymlinks:sys ln -s -- "${prior_login_link_target}" "${root}/opt/ao-rebirth/loginengine/current"
    MSYS=winsymlinks:sys ln -s -- "${prior_zone_link_target}" "${root}/opt/ao-rebirth/zoneengine/current"
    require test -L "${root}/opt/ao-rebirth/loginengine/current"
    require test -L "${root}/opt/ao-rebirth/zoneengine/current"
    require test "$(readlink -- "${root}/opt/ao-rebirth/loginengine/current")" = "${prior_login_link_target}"
    require test "$(readlink -- "${root}/opt/ao-rebirth/zoneengine/current")" = "${prior_zone_link_target}"
    prior_login_release="$(realpath -e -- "${root}/opt/ao-rebirth/loginengine/current")"
    prior_zone_release="$(realpath -e -- "${root}/opt/ao-rebirth/zoneengine/current")"
    printf 'old login unit\n' > "${root}/etc/systemd/system/ao-rebirth-loginengine.service"
    printf 'old zone unit\n' > "${root}/etc/systemd/system/ao-rebirth-zoneengine.service"
    printf '[Service]\nType=simple\nNotifyAccess=none\n' > "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf"
    printf '[Service]\nEnvironment=AO_REBIRTH_DAILY_LOGIN_REWARDS_JSON=/opt/ao-rebirth/website/src/uwg.daily.icc-rk/rewards.json\n' > "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/20-daily-login.conf"
    printf 'AO_REBIRTH_CONFIG_PATH=%s\n' "${root}/etc/ao-rebirth/loginengine/Config.xml" > "${root}/etc/ao-rebirth/loginengine/loginengine.env"
    printf 'AO_REBIRTH_CONFIG_PATH=%s\n' "${root}/etc/ao-rebirth/zoneengine/Config.xml" > "${root}/etc/ao-rebirth/zoneengine/zoneengine.env"
    printf '<Config><ZoneIP>2.24.96.30</ZoneIP></Config>\n' > "${root}/etc/ao-rebirth/loginengine/Config.xml"
    printf '<Config><ZoneIP>2.24.96.30</ZoneIP></Config>\n' > "${root}/etc/ao-rebirth/zoneengine/Config.xml"
    printf 'active\n' > "${state}/login.active"; printf 'active\n' > "${state}/zone.active"
    printf '0\n' > "${state}/login.restarts"; printf '0\n' > "${state}/zone.restarts"
    printf '0\n' > "${state}/login.starts"; printf '0\n' > "${state}/zone.starts"; printf '0\n' > "${state}/online"
    printf 'PASS\n' > "${state}/candidate-validation"
    printf 'NO\n' > "${state}/login.port-occupied"; printf 'NO\n' > "${state}/zone.port-occupied"
    printf 'PASS\n' > "${state}/login.port-inspection"; printf 'PASS\n' > "${state}/zone.port-inspection"
    printf 'simple\n' > "${state}/zone.effective-type"
    printf 'none\n' > "${state}/zone.notify-access"
    printf '%s %s\n' \
        "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf" \
        "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/20-daily-login.conf" \
        > "${state}/zone.dropin-paths"
    printf 'NO\n' > "${state}/zone.effective-mismatch-after-reload"
    printf 'NO\n' > "${state}/daily-login-dropin-tamper-after-reload"
    printf 'NO\n' > "${state}/zone.restart-on-start"
    printf 'NO\n' > "${state}/online-on-zone-start"
    printf 'NO\n' > "${state}/online-on-login-stop"
    printf 'NO\n' > "${state}/zone-change-on-login-stop"
    printf '0\n' > "${state}/login.listener-delay"; printf '0\n' > "${state}/zone.listener-delay"
    printf '0\n' > "${state}/login.listener-checks"; printf '0\n' > "${state}/zone.listener-checks"
    create_artifact "${input}/login" LoginEngine
    create_artifact "${input}/zone" ZoneEngine
    printf '<Config><ZoneIP>127.0.0.1</ZoneIP></Config>\n' > "${input}/login/Config.xml"
    printf '<Config><ZoneIP>127.0.0.1</ZoneIP></Config>\n' > "${input}/zone/Config.xml"
    cp -- "${login_unit_source}" "${input}/login.service"
    cp -- "${zone_unit_source}" "${input}/zone.service"
    manifest="${input}/release.manifest"
    cat > "${manifest}" <<EOF
FORMAT=2
SOURCE_SHA=${fake_sha}
BUILD_TIMESTAMP_UTC=2026-08-24T00:00:00Z
LOGINENGINE_ARTIFACT_DIR=${input}/login
LOGINENGINE_ARTIFACT_SHA256=$(sha256sum "${input}/login/LoginEngine" | awk '{print $1}')
ZONEENGINE_ARTIFACT_DIR=${input}/zone
ZONEENGINE_ARTIFACT_SHA256=$(sha256sum "${input}/zone/ZoneEngine" | awk '{print $1}')
PLACEMENT_CORPUS_VERSION=${fixture_placement_corpus_version}
PLACEMENT_CORPUS_MANIFEST_SHA256=${fixture_placement_corpus_manifest_sha}
PLACEMENT_CORPUS_SUMMARY_SHA256=${fixture_placement_summary_sha}
PLACEMENT_CORPUS_INDEX_SHA256=${fixture_placement_index_sha}
PLACEMENT_ACGHASH_INVENTORY_SHA256=${fixture_placement_acghash_sha}
PLACEMENT_BUILD_MANIFEST_SHA256=${fixture_placement_build_manifest_sha}
PLACEMENT_RESOURCE_COUNT=630
PLACEMENT_PARSED_RESOURCE_COUNT=627
PLACEMENT_PARSER_LIMITED_RESOURCE_COUNT=3
PLACEMENT_DISTRICT_COUNT=4146
PLACEMENT_RECORD_COUNT=32805
PLACEMENT_UNIQUE_ACGHASH_COUNT=4016
PLACEMENT_RUNTIME_AUTHORIZED_COUNT=199
LOGINENGINE_UNIT_PATH=${input}/login.service
LOGINENGINE_UNIT_SHA256=$(sha256sum "${input}/login.service" | awk '{print $1}')
ZONEENGINE_UNIT_PATH=${input}/zone.service
ZONEENGINE_UNIT_SHA256=$(sha256sum "${input}/zone.service" | awk '{print $1}')
LOGINENGINE_SERVICE=ao-rebirth-loginengine.service
ZONEENGINE_SERVICE=ao-rebirth-zoneengine.service
PREVIOUS_LOGINENGINE_RELEASE=
PREVIOUS_ZONEENGINE_RELEASE=
EOF
    old_login_unit_hash="$(sha256sum "${root}/etc/systemd/system/ao-rebirth-loginengine.service" | awk '{print $1}')"
    old_zone_unit_hash="$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service" | awk '{print $1}')"
    old_zone_dropin_hash="$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf" | awk '{print $1}')"
    daily_login_dropin_hash="$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/20-daily-login.conf" | awk '{print $1}')"
    old_login_artifact_hash="$(sha256sum "${prior_login_release}/LoginEngine" | awk '{print $1}')"
    old_zone_artifact_hash="$(sha256sum "${prior_zone_release}/ZoneEngine" | awk '{print $1}')"
    new_login_unit_hash="$(sha256sum "${input}/login.service" | awk '{print $1}')"
    new_zone_unit_hash="$(sha256sum "${input}/zone.service" | awk '{print $1}')"
    new_login_artifact_hash="$(sha256sum "${input}/login/LoginEngine" | awk '{print $1}')"
    new_zone_artifact_hash="$(sha256sum "${input}/zone/ZoneEngine" | awk '{print $1}')"
    require test "${old_login_unit_hash}" != "${new_login_unit_hash}"
    require test "${old_zone_unit_hash}" != "${new_zone_unit_hash}"
    require test "${old_login_artifact_hash}" != "${new_login_artifact_hash}"
    require test "${old_zone_artifact_hash}" != "${new_zone_artifact_hash}"
    require test "${old_zone_dropin_hash}" = 2d1ebd0ffd7534c6357830891a35d2343428b56c8093b05223abe7635f67b55f
    require test "${daily_login_dropin_hash}" = 4ea8e3ba780f564a17ba454fa46121a6618985da3ef449d792016a41f8ac0e29
    cat > "${root}/opt/ao-rebirth/deployed-release.env" <<EOF
SOURCE_SHA=${other_sha}
LOGINENGINE_RELEASE=${prior_login_release}
ZONEENGINE_RELEASE=${prior_zone_release}
LOGINENGINE_ARTIFACT_SHA256=${old_login_artifact_hash}
ZONEENGINE_ARTIFACT_SHA256=${old_zone_artifact_hash}
LOGINENGINE_UNIT_SHA256=${old_login_unit_hash}
ZONEENGINE_UNIT_SHA256=${old_zone_unit_hash}
EOF
}
run_upgrade()
{
    env MSYS=winsymlinks:sys AO_REBIRTH_DEPLOY_TEST_MODE=1 AO_REBIRTH_DEPLOY_TEST_ROOT="${root}" AO_REBIRTH_DEPLOY_TEST_FAIL_STEP="${2:-}" \
        bash "${upgrader}" --manifest "${manifest}" --expected-sha "${1:-${fake_sha}}"
}
run_recovery_upgrade()
{
    env MSYS=winsymlinks:sys AO_REBIRTH_DEPLOY_TEST_MODE=1 AO_REBIRTH_DEPLOY_TEST_ROOT="${root}" AO_REBIRTH_DEPLOY_TEST_FAIL_STEP="${2:-}" \
        bash "${upgrader}" --manifest "${manifest}" --expected-sha "${1:-${fake_sha}}" --recover-zone-outage
}
run_stopped_recovery_upgrade()
{
    env MSYS=winsymlinks:sys AO_REBIRTH_DEPLOY_TEST_MODE=1 AO_REBIRTH_DEPLOY_TEST_ROOT="${root}" AO_REBIRTH_DEPLOY_TEST_FAIL_STEP="${2:-}" \
        bash "${upgrader}" --manifest "${manifest}" --expected-sha "${1:-${fake_sha}}" --recover-zone-outage --resume-stopped-recovery
}
set_stopped_pair()
{
    printf 'inactive\n' > "${state}/login.active"
    printf 'inactive\n' > "${state}/zone.active"
}
remove_governed_dropin()
{
    rm -f -- "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf"
    printf 'notify\n' > "${state}/zone.effective-type"
    printf 'main\n' > "${state}/zone.notify-access"
    printf '%s\n' "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/20-daily-login.conf" > "${state}/zone.dropin-paths"
}
assert_governed_dropin_restored()
{
    require test -f "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf"
    require test ! -L "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf"
    require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf" | awk '{print $1}')" = "${old_zone_dropin_hash}"
    require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/20-daily-login.conf" | awk '{print $1}')" = "${daily_login_dropin_hash}"
    require test "$(cat "${state}/zone.effective-type")" = simple
    require test "$(cat "${state}/zone.notify-access")" = none
}
assert_old_targets()
{
    require test -L "${root}/opt/ao-rebirth/loginengine/current"
    require test -L "${root}/opt/ao-rebirth/zoneengine/current"
    require test "$(readlink -- "${root}/opt/ao-rebirth/loginengine/current")" = "${prior_login_link_target}"
    require test "$(readlink -- "${root}/opt/ao-rebirth/zoneengine/current")" = "${prior_zone_link_target}"
    require test -d "${prior_login_release}"
    require test -d "${prior_zone_release}"
    require test "$(realpath -e -- "${root}/opt/ao-rebirth/loginengine/current")" = "${prior_login_release}"
    require test "$(realpath -e -- "${root}/opt/ao-rebirth/zoneengine/current")" = "${prior_zone_release}"
    require test "$(sha256sum "${root}/opt/ao-rebirth/loginengine/current/LoginEngine" | awk '{print $1}')" = "${old_login_artifact_hash}"
    require test "$(sha256sum "${root}/opt/ao-rebirth/zoneengine/current/ZoneEngine" | awk '{print $1}')" = "${old_zone_artifact_hash}"
    require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-loginengine.service" | awk '{print $1}')" = "${old_login_unit_hash}"
    require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service" | awk '{print $1}')" = "${old_zone_unit_hash}"
    require test "$(sha256sum "${root}/opt/ao-rebirth/loginengine/current/LoginEngine" | awk '{print $1}')" != "${new_login_artifact_hash}"
    require test "$(sha256sum "${root}/opt/ao-rebirth/zoneengine/current/ZoneEngine" | awk '{print $1}')" != "${new_zone_artifact_hash}"
    require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-loginengine.service" | awk '{print $1}')" != "${new_login_unit_hash}"
    require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service" | awk '{print $1}')" != "${new_zone_unit_hash}"
}
assert_old_pair()
{
    assert_old_targets
    require test "$(cat "${state}/login.active")" = active
    require test "$(cat "${state}/zone.active")" = active
}
expect_preflight_failure()
{
    if run_upgrade "${1:-${fake_sha}}" > "${fixture}/output" 2>&1; then fail "expected preflight failure"; fi
    assert_old_pair
    tests_run=$((tests_run + 1))
}
expect_transaction_failure()
{
    if run_upgrade "${fake_sha}" "$1" > "${fixture}/output" 2>&1; then fail "expected transaction failure at $1"; fi
    grep -F 'ROLLBACK_BOTH_SERVICES=PASS' "${fixture}/output" >/dev/null || fail "paired rollback did not pass for $1"
    grep -F 'ROLLBACK_EXACT_PRIOR_TARGETS=PASS' "${fixture}/output" >/dev/null || fail "exact prior symlink targets were not restored for $1"
    grep -F 'ROLLBACK_PRIOR_ARTIFACTS_AND_UNITS=PASS' "${fixture}/output" >/dev/null || fail "prior artifacts and units were not restored for $1"
    grep -F 'ROLLBACK_NO_MIXED_STATE=PASS' "${fixture}/output" >/dev/null || fail "rollback left mixed deployment state for $1"
    grep -F 'ROLLBACK_STEP_LOGIN_READINESS=PASS' "${fixture}/output" >/dev/null || fail "LoginEngine rollback readiness did not pass for $1"
    grep -F 'ROLLBACK_STEP_ZONE_READINESS=PASS' "${fixture}/output" >/dev/null || fail "ZoneEngine rollback readiness did not pass for $1"
    assert_old_pair
    assert_governed_dropin_restored
    tests_run=$((tests_run + 1))
}

case "${AO_REBIRTH_DEPLOY_TEST_CASE:-all}" in
    all) ;;
    success)
        create_fixture
        printf '7\n' > "${state}/login.listener-delay"; printf '7\n' > "${state}/zone.listener-delay"
        run_upgrade > "${fixture}/success-output"
        require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/success-output"
        require grep -F 'READINESS_WAIT=PASS engine=login elapsedSeconds=7' "${fixture}/success-output"
        require grep -F 'READINESS_WAIT=PASS engine=zone elapsedSeconds=7' "${fixture}/success-output"
        require test ! -e "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf"
        require test "$(cat "${state}/zone.effective-type")" = notify
        require test "$(cat "${state}/zone.notify-access")" = main
        echo "PASS: production deployment workflow successful startup (1/1)"
        exit 0
        ;;
    artifact_install|unit_install|login_start|zone_start)
        selected_case="${AO_REBIRTH_DEPLOY_TEST_CASE}"
        create_fixture
        if [[ "${selected_case}" == artifact_install ]]; then
            printf '7\n' > "${state}/login.listener-delay"; printf '7\n' > "${state}/zone.listener-delay"
        fi
        expect_transaction_failure "${selected_case}"
        if [[ "${selected_case}" == artifact_install ]]; then
            require grep -F 'READINESS_WAIT=PASS engine=login elapsedSeconds=7' "${fixture}/output"
            require grep -F 'READINESS_WAIT=PASS engine=zone elapsedSeconds=7' "${fixture}/output"
        fi
        if [[ "${selected_case}" == login_start ]]; then
            require grep -F 'OWNERSHIP_DIR_FIXTURE_SHARED_PATH=PASS' "${fixture}/output"
            require test -d "${root}/var/lib/ao-rebirth/session-ownership"
        fi
        echo "PASS: production deployment workflow ${selected_case} rollback (1/1)"
        exit 0
        ;;
    idempotent)
        create_fixture
        run_upgrade > "${fixture}/success-output"
        require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/success-output"
        login_starts_before="$(cat "${state}/login.starts")"
        zone_starts_before="$(cat "${state}/zone.starts")"
        run_upgrade > "${fixture}/idempotent-output"
        require grep -F 'IDEMPOTENT_REDEPLOY=PASS' "${fixture}/idempotent-output"
        require grep -F 'OWNERSHIP_DIR_FIXTURE_IDEMPOTENT=PASS' "${fixture}/idempotent-output"
        require test "$(cat "${state}/ownership-directory")" = "${root}/var/lib/ao-rebirth/session-ownership"
        require test "$(cat "${state}/login.starts")" = "${login_starts_before}"
        require test "$(cat "${state}/zone.starts")" = "${zone_starts_before}"
        echo "PASS: production deployment workflow idempotent redeploy (1/1)"
        exit 0
        ;;
    *) fail "unknown deployment fixture case: ${AO_REBIRTH_DEPLOY_TEST_CASE}" ;;
esac

behavior_hash_before="$(sha256sum "${repository_root}/AORebirth/Server/LoginEngine/CoreClient/LoginHandoffLifecycle.cs" \
    "${repository_root}/AORebirth/Server/ZoneEngine/StaleOnlineRecovery.cs" \
    "${repository_root}/AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs")"

login_config_uses="$(grep -Fc 'AO_REBIRTH_CONFIG_PATH="${login_config_path}"' "${upgrader}" || true)"
zone_config_uses="$(grep -Fc 'AO_REBIRTH_CONFIG_PATH="${zone_config_path}"' "${upgrader}" || true)"
require test "${login_config_uses}" = 2
require test "${zone_config_uses}" = 2
if grep -F 'AO_REBIRTH_CONFIG_PATH="${LOGIN_ARTIFACT_DIR}/Config.xml"' "${upgrader}" >/dev/null; then
    fail "candidate LoginEngine validation reverted to the portable artifact config"
fi
if grep -F 'AO_REBIRTH_CONFIG_PATH="${ZONE_ARTIFACT_DIR}/Config.xml"' "${upgrader}" >/dev/null; then
    fail "candidate ZoneEngine validation reverted to a config that may differ from production"
fi
tests_run=$((tests_run + 1))

create_fixture; expect_preflight_failure "${other_sha}"
create_fixture; set_manifest_value "${manifest}" LOGINENGINE_ARTIFACT_SHA256 "$(printf bad | sha256sum | awk '{print $1}')"; expect_preflight_failure
create_fixture; set_manifest_value "${manifest}" ZONEENGINE_ARTIFACT_SHA256 "$(printf bad | sha256sum | awk '{print $1}')"; expect_preflight_failure
create_fixture; printf 'AO_REBIRTH_CONFIG_PATH=%s\n' "${input}/login/Config.xml" > "${root}/etc/ao-rebirth/loginengine/loginengine.env"; expect_preflight_failure; require grep -F 'candidate LoginEngine configuration path diverges from the governed production path' "${fixture}/output"
create_fixture; printf 'AO_REBIRTH_CONFIG_PATH=%s\n AO_REBIRTH_CONFIG_PATH=%s\n' "${root}/etc/ao-rebirth/zoneengine/Config.xml" "${input}/zone/Config.xml" > "${root}/etc/ao-rebirth/zoneengine/zoneengine.env"; expect_preflight_failure; require grep -F 'AO_REBIRTH_CONFIG_PATH is missing or duplicated' "${fixture}/output"
create_fixture; rm -f -- "${input}/zone/Content/Official/PlayfieldPlacements/official-placement-build-manifest.json"; expect_preflight_failure
create_fixture; printf 'tampered\n' >> "${input}/zone/Content/Official/PlayfieldPlacements/official-placement-summary.json"; expect_preflight_failure
create_fixture; set_manifest_value "${manifest}" PLACEMENT_BUILD_MANIFEST_SHA256 "$(printf bad | sha256sum | awk '{print $1}')"; expect_preflight_failure
create_fixture; rm -f -- "${input}/zone/Content/Official/PlayfieldPlacements/placements/pf_630.json"; expect_preflight_failure
create_fixture; printf 'tampered\n' >> "${input}/zone/Content/Official/PlayfieldPlacements/placements/pf_1.json"; expect_preflight_failure
create_fixture; sed -i '/^PLACEMENT_VALIDATION=PASS$/d' "${input}/zone/LINUX_ACCEPTANCE.env"; expect_preflight_failure
create_fixture; rm -f -- "${input}/login.service"; expect_preflight_failure
create_fixture; rm -f -- "${input}/zone.service"; expect_preflight_failure
create_fixture; sed -i 's|AO_REBIRTH_SESSION_OWNERSHIP_DIR=/var/lib/ao-rebirth/session-ownership|AO_REBIRTH_SESSION_OWNERSHIP_DIR=/var/lib/ao-rebirth/zone-private|' "${input}/zone.service"; set_manifest_value "${manifest}" ZONEENGINE_UNIT_SHA256 "$(sha256sum "${input}/zone.service" | awk '{print $1}')"; expect_preflight_failure
create_fixture; sed -i 's|/var/lib/ao-rebirth/session-ownership|/tmp/session-ownership|g' "${input}/login.service" "${input}/zone.service"; set_manifest_value "${manifest}" LOGINENGINE_UNIT_SHA256 "$(sha256sum "${input}/login.service" | awk '{print $1}')"; set_manifest_value "${manifest}" ZONEENGINE_UNIT_SHA256 "$(sha256sum "${input}/zone.service" | awk '{print $1}')"; expect_preflight_failure
create_fixture; sed -i '/^PrivateTmp=true$/d' "${input}/login.service"; set_manifest_value "${manifest}" LOGINENGINE_UNIT_SHA256 "$(sha256sum "${input}/login.service" | awk '{print $1}')"; expect_preflight_failure
create_fixture; sed -i '/ZoneEngine --recover-stale-online/d' "${input}/zone.service"; set_manifest_value "${manifest}" ZONEENGINE_UNIT_SHA256 "$(sha256sum "${input}/zone.service" | awk '{print $1}')"; expect_preflight_failure
create_fixture; sed -i 's|ZoneEngine --headless --shutdown-file|ZoneEngine --validate-lifecycle --shutdown-file|' "${input}/zone.service"; set_manifest_value "${manifest}" ZONEENGINE_UNIT_SHA256 "$(sha256sum "${input}/zone.service" | awk '{print $1}')"; expect_preflight_failure; require grep -F 'ZoneEngine production executable contract failed' "${fixture}/output"

create_fixture
if run_recovery_upgrade > "${fixture}/output" 2>&1; then fail "outage recovery accepted an active ZoneEngine"; fi
assert_old_pair
tests_run=$((tests_run + 1))

create_fixture
printf 'activating\n' > "${state}/zone.active"
if run_recovery_upgrade > "${fixture}/output" 2>&1; then fail "outage recovery accepted a non-stopped ZoneEngine state"; fi
assert_old_targets
require test "$(cat "${state}/login.active")" = active
require test "$(cat "${state}/zone.active")" = activating
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
printf 'YES\n' > "${state}/zone.port-occupied"
if run_recovery_upgrade > "${fixture}/output" 2>&1; then fail "outage recovery accepted an occupied ZoneEngine port"; fi
assert_old_targets
require test "$(cat "${state}/login.active")" = active
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
printf 'FAIL\n' > "${state}/zone.port-inspection"
if run_recovery_upgrade > "${fixture}/output" 2>&1; then fail "outage recovery accepted a failed ZoneEngine port inspection"; fi
require grep -F 'could not inspect port 7501' "${fixture}/output"
assert_old_targets
require test "$(cat "${state}/login.active")" = active
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
env MSYS=winsymlinks:sys AO_REBIRTH_DEPLOY_TEST_MODE=1 AO_REBIRTH_DEPLOY_TEST_ROOT="${root}" \
    bash "${upgrader}" --manifest "${manifest}" --expected-sha "${fake_sha}" --recover-zone-outage --dry-run > "${fixture}/output"
require grep -F 'ZONEENGINE_OUTAGE_RECOVERY_PRECONDITION=PASS' "${fixture}/output"
require grep -F 'CANDIDATE_DATABASE_COMPATIBILITY=PASS' "${fixture}/output"
require grep -F 'DRY_RUN=PASS' "${fixture}/output"
require test "$(cat "${state}/candidate-login-config")" = "${root}/etc/ao-rebirth/loginengine/Config.xml"
require test "$(cat "${state}/candidate-zone-config")" = "${root}/etc/ao-rebirth/zoneengine/Config.xml"
require grep -F '<ZoneIP>2.24.96.30</ZoneIP>' "$(cat "${state}/candidate-login-config")"
require grep -F '<ZoneIP>2.24.96.30</ZoneIP>' "$(cat "${state}/candidate-zone-config")"
require grep -F '<ZoneIP>127.0.0.1</ZoneIP>' "${input}/login/Config.xml"
require grep -F '<ZoneIP>127.0.0.1</ZoneIP>' "${input}/zone/Config.xml"
assert_old_targets
require test "$(cat "${state}/login.active")" = active
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
printf 'FAIL\n' > "${state}/candidate-validation"
if run_recovery_upgrade > "${fixture}/output" 2>&1; then fail "outage recovery accepted an incompatible candidate database contract"; fi
require grep -F 'candidate database compatibility validation failed' "${fixture}/output"
assert_old_targets
require test "$(cat "${state}/login.active")" = active
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
printf 'PASS_RESTART_ZONE\n' > "${state}/candidate-validation"
if run_recovery_upgrade > "${fixture}/output" 2>&1; then fail "outage recovery accepted a changing frozen restart count"; fi
require grep -F 'ZoneEngine restart count changed while outage recovery was frozen' "${fixture}/output"
assert_old_targets
require test "$(cat "${state}/login.active")" = active
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
run_recovery_upgrade > "${fixture}/output"
require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/output"
require grep -F 'ROLLBACK_POLICY=RESTORE_INCOMPATIBLE_PRIOR_PAIR_BUT_LEAVE_STOPPED' "${fixture}/output"
require test "$(cat "${state}/login.active")" = active
require test "$(cat "${state}/zone.active")" = active
require grep -F 'ZONEENGINE_WAS_ACTIVE=NO' "${root}/opt/ao-rebirth/deployment-snapshots/"*/rollback.env
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
printf 'YES\n' > "${state}/online-on-zone-start"
run_recovery_upgrade > "${fixture}/output"
require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/output"
require grep -F 'POST_START_STABILITY=PASS' "${fixture}/output"
require test "$(cat "${state}/login.active")" = active
require test "$(cat "${state}/zone.active")" = active
require test "$(cat "${state}/online")" = 1
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
printf 'YES\n' > "${state}/online-on-login-stop"
if run_recovery_upgrade > "${fixture}/output" 2>&1; then fail "outage recovery accepted an online character after admission closed"; fi
require grep -F 'online characters appeared after LoginEngine admission closed' "${fixture}/output"
require grep -F 'ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=PASS' "${fixture}/output"
assert_old_targets
require test "$(cat "${state}/login.active")" = inactive
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
printf 'YES\n' > "${state}/zone-change-on-login-stop"
if run_recovery_upgrade > "${fixture}/output" 2>&1; then fail "outage recovery accepted a ZoneEngine state change before mutation"; fi
require grep -F 'ZoneEngine recovery state changed before release mutation' "${fixture}/output"
require grep -F 'ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=PASS' "${fixture}/output"
assert_old_targets
require test "$(cat "${state}/login.active")" = inactive
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
run_upgrade > "${fixture}/first-output"
printf 'inactive\n' > "${state}/zone.active"
run_recovery_upgrade > "${fixture}/output"
require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/output"
if grep -Fq 'ALREADY_DEPLOYED=YES' "${fixture}/output"; then fail "outage recovery left an already-deployed ZoneEngine stopped"; fi
require test "$(cat "${state}/login.active")" = active
require test "$(cat "${state}/zone.active")" = active
require test "$(cat "${state}/zone.starts")" = 2
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
printf 'YES\n' > "${state}/zone.restart-on-start"
if run_recovery_upgrade > "${fixture}/output" 2>&1; then fail "outage recovery accepted a ZoneEngine auto-restart"; fi
require grep -F 'post-deployment stability check failed' "${fixture}/output"
require grep -F 'ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=PASS' "${fixture}/output"
assert_old_targets
require test "$(cat "${state}/login.active")" = inactive
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
printf 'inactive\n' > "${state}/zone.active"
if run_recovery_upgrade "${fake_sha}" login_start > "${fixture}/output" 2>&1; then fail "expected outage-recovery transaction failure"; fi
require grep -F 'ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=PASS' "${fixture}/output"
assert_old_targets
assert_governed_dropin_restored
require test "$(cat "${state}/login.active")" = inactive
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
if env MSYS=winsymlinks:sys AO_REBIRTH_DEPLOY_TEST_MODE=1 AO_REBIRTH_DEPLOY_TEST_ROOT="${root}" \
    bash "${upgrader}" --manifest "${manifest}" --expected-sha "${fake_sha}" --resume-stopped-recovery > "${fixture}/output" 2>&1; then
    fail "stopped recovery modifier was accepted without outage recovery mode"
fi
require grep -F -- '--resume-stopped-recovery requires --recover-zone-outage' "${fixture}/output"
assert_old_targets
tests_run=$((tests_run + 1))

create_fixture
printf '[Service]\nEnvironment=UNMANAGED=1\n' > "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/20-unmanaged.conf"
if run_upgrade > "${fixture}/output" 2>&1; then fail "deployment accepted an unmanaged ZoneEngine systemd drop-in"; fi
require grep -F 'unmanaged ZoneEngine systemd drop-in is present' "${fixture}/output"
assert_old_pair
tests_run=$((tests_run + 1))

create_fixture
printf '# changed\n' >> "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf"
if run_upgrade > "${fixture}/output" 2>&1; then fail "deployment accepted unknown stale readiness drop-in content"; fi
require grep -F 'stale readiness drop-in content is not the governed production override' "${fixture}/output"
assert_old_pair
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
printf 'YES\n' > "${state}/zone.effective-mismatch-after-reload"
if run_stopped_recovery_upgrade > "${fixture}/output" 2>&1; then fail "stopped recovery accepted an ineffective ZoneEngine readiness unit"; fi
require grep -F 'effective Type=notify readiness contract failed after installation' "${fixture}/output"
require grep -F 'ROLLBACK_ZONEENGINE_EFFECTIVE_UNIT=PASS type=simple notifyAccess=none' "${fixture}/output"
require grep -F 'ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=PASS' "${fixture}/output"
assert_old_targets
assert_governed_dropin_restored
require test "$(cat "${state}/login.active")" = inactive
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
printf 'YES\n' > "${state}/daily-login-dropin-tamper-after-reload"
if run_stopped_recovery_upgrade > "${fixture}/output" 2>&1; then fail "stopped recovery accepted concurrent daily-login drop-in drift"; fi
require grep -F 'effective Type=notify readiness contract failed after installation' "${fixture}/output"
require grep -F 'ROLLBACK_STEP_VERIFY_EXACT_PRIOR_STATE=FAIL' "${fixture}/output"
require grep -F 'ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=FAIL' "${fixture}/output"
assert_old_targets
require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf" | awk '{print $1}')" = "${old_zone_dropin_hash}"
require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/20-daily-login.conf" | awk '{print $1}')" != "${daily_login_dropin_hash}"
require test "$(cat "${state}/login.active")" = inactive
require test "$(cat "${state}/zone.active")" = inactive
require test "$(cat "${state}/login.starts")" = 0
require test "$(cat "${state}/zone.starts")" = 0
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
dropin_hash_before="$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf" | awk '{print $1}')"
env MSYS=winsymlinks:sys AO_REBIRTH_DEPLOY_TEST_MODE=1 AO_REBIRTH_DEPLOY_TEST_ROOT="${root}" \
    bash "${upgrader}" --manifest "${manifest}" --expected-sha "${fake_sha}" --recover-zone-outage --resume-stopped-recovery --dry-run > "${fixture}/output"
require grep -F 'STOPPED_PAIR_RECOVERY_PRECONDITION=PASS' "${fixture}/output"
require grep -F 'STOPPED_PAIR_ROLLBACK_PROVENANCE=PASS' "${fixture}/output"
require grep -F 'DRY_RUN=PASS' "${fixture}/output"
require grep -F 'PRODUCTION_MUTATION=NO' "${fixture}/output"
require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf" | awk '{print $1}')" = "${dropin_hash_before}"
assert_old_targets
require test "$(cat "${state}/login.active")" = inactive
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
run_stopped_recovery_upgrade > "${fixture}/output"
require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/output"
require grep -F 'ZONEENGINE_EFFECTIVE_READINESS_CONTRACT=PASS type=notify notifyAccess=main dropInPaths=governed-daily-login' "${fixture}/output"
require grep -F 'LOGINENGINE_WAS_ACTIVE=NO' "${root}/opt/ao-rebirth/deployment-snapshots/"*/rollback.env
require grep -F 'ZONEENGINE_WAS_ACTIVE=NO' "${root}/opt/ao-rebirth/deployment-snapshots/"*/rollback.env
require test "$(cat "${state}/login.active")" = active
require test "$(cat "${state}/zone.active")" = active
require test ! -e "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf"
require test "$(cat "${state}/zone.effective-type")" = notify
require test "$(cat "${state}/zone.notify-access")" = main
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
printf 'YES\n' > "${state}/login.port-occupied"
if run_stopped_recovery_upgrade > "${fixture}/output" 2>&1; then fail "stopped recovery accepted an occupied LoginEngine port"; fi
require grep -F 'port 7500 must be closed for stopped-pair recovery' "${fixture}/output"
assert_old_targets
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
printf 'FAIL\n' > "${state}/login.port-inspection"
if run_stopped_recovery_upgrade > "${fixture}/output" 2>&1; then fail "stopped recovery accepted failed LoginEngine port inspection"; fi
require grep -F 'could not inspect port 7500' "${fixture}/output"
assert_old_targets
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
printf 'PASS_ACTIVATE_LOGIN\n' > "${state}/candidate-validation"
if run_stopped_recovery_upgrade > "${fixture}/output" 2>&1; then fail "stopped recovery accepted LoginEngine state drift"; fi
require grep -F 'LoginEngine is not in an exact stopped state for stopped-pair recovery' "${fixture}/output"
assert_old_targets
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
printf 'PASS_RESTART_LOGIN\n' > "${state}/candidate-validation"
if run_stopped_recovery_upgrade > "${fixture}/output" 2>&1; then fail "stopped recovery accepted LoginEngine restart drift"; fi
require grep -F 'LoginEngine restart count changed while stopped-pair recovery was frozen' "${fixture}/output"
assert_old_targets
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
printf 'PASS_ONLINE\n' > "${state}/candidate-validation"
if run_stopped_recovery_upgrade > "${fixture}/output" 2>&1; then fail "stopped recovery accepted Online drift before mutation"; fi
require grep -F 'online characters appeared before stopped-pair recovery mutation' "${fixture}/output"
assert_old_targets
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
if run_stopped_recovery_upgrade "${fake_sha}" login_start > "${fixture}/output" 2>&1; then fail "expected stopped-recovery transaction failure"; fi
require grep -F 'ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=PASS' "${fixture}/output"
require grep -F 'ROLLBACK_ZONEENGINE_EFFECTIVE_UNIT=PASS type=simple notifyAccess=none' "${fixture}/output"
assert_old_targets
assert_governed_dropin_restored
require test "$(cat "${state}/login.active")" = inactive
require test "$(cat "${state}/zone.active")" = inactive
tests_run=$((tests_run + 1))

create_fixture
set_stopped_pair
remove_governed_dropin
if run_stopped_recovery_upgrade "${fake_sha}" login_start > "${fixture}/output" 2>&1; then fail "expected stopped-recovery failure without a prior drop-in"; fi
require grep -F 'ROLLBACK_INCOMPATIBLE_PAIR_LEFT_STOPPED=PASS' "${fixture}/output"
require grep -F 'ROLLBACK_ZONEENGINE_EFFECTIVE_UNIT=PASS type=notify notifyAccess=main' "${fixture}/output"
assert_old_targets
require test ! -e "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf"
require test "$(cat "${state}/zone.effective-type")" = notify
require test "$(cat "${state}/zone.notify-access")" = main
tests_run=$((tests_run + 1))

create_fixture
run_upgrade > "${fixture}/first-output"
set_stopped_pair
login_starts_before="$(cat "${state}/login.starts")"
zone_starts_before="$(cat "${state}/zone.starts")"
run_stopped_recovery_upgrade > "${fixture}/output"
require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/output"
if grep -Fq 'ALREADY_DEPLOYED=YES' "${fixture}/output"; then fail "stopped-pair recovery took an idempotent no-op path"; fi
require test "$(cat "${state}/login.starts")" = "$((login_starts_before + 1))"
require test "$(cat "${state}/zone.starts")" = "$((zone_starts_before + 1))"
tests_run=$((tests_run + 1))

create_fixture; printf '7\n' > "${state}/login.listener-delay"; printf '7\n' > "${state}/zone.listener-delay"; expect_transaction_failure artifact_install; require grep -F 'READINESS_WAIT=PASS engine=login elapsedSeconds=7' "${fixture}/output"; require grep -F 'READINESS_WAIT=PASS engine=zone elapsedSeconds=7' "${fixture}/output"
create_fixture; expect_transaction_failure unit_install
create_fixture; expect_transaction_failure login_start
create_fixture; expect_transaction_failure zone_start
create_fixture; expect_transaction_failure listener; require grep -F 'READINESS_WAIT=TIMEOUT engine=login elapsedSeconds=30' "${fixture}/output"; require grep -F 'READINESS_JOURNAL_BEGIN service=ao-rebirth-loginengine.service' "${fixture}/output"; require grep -F 'READINESS_JOURNAL_END service=ao-rebirth-loginengine.service' "${fixture}/output"

create_fixture
printf '7\n' > "${state}/login.listener-delay"; printf '7\n' > "${state}/zone.listener-delay"
run_upgrade > "${fixture}/success-output"
require grep -F 'TRANSACTIONAL_DEPLOYMENT=PASS' "${fixture}/success-output"
require grep -F 'READINESS_WAIT=PASS engine=login elapsedSeconds=7' "${fixture}/success-output"
require grep -F 'READINESS_WAIT=PASS engine=zone elapsedSeconds=7' "${fixture}/success-output"
require test "$(tr -d '\r\n\t ' < "${root}/opt/ao-rebirth/loginengine/current/SOURCE_SHA")" = "${fake_sha}"
require test "$(tr -d '\r\n\t ' < "${root}/opt/ao-rebirth/zoneengine/current/SOURCE_SHA")" = "${fake_sha}"
require test "$(sha256sum "${root}/opt/ao-rebirth/zoneengine/current/Content/Official/PlayfieldPlacements/official-placement-build-manifest.json" | awk '{print $1}')" = "${fixture_placement_build_manifest_sha}"
require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-loginengine.service" | awk '{print $1}')" = "$(sha256sum "${input}/login.service" | awk '{print $1}')"
require test "$(sha256sum "${root}/etc/systemd/system/ao-rebirth-zoneengine.service" | awk '{print $1}')" = "$(sha256sum "${input}/zone.service" | awk '{print $1}')"
require test -d "${root}/var/lib/ao-rebirth/session-ownership"
require test ! -e "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/10-type-simple.conf"
require test "$(cat "${state}/zone.effective-type")" = notify
require test "$(cat "${state}/zone.notify-access")" = main
require test "$(cat "${state}/zone.dropin-paths")" = "${root}/etc/systemd/system/ao-rebirth-zoneengine.service.d/20-daily-login.conf"
tests_run=$((tests_run + 1))

login_starts_before="$(cat "${state}/login.starts")"; zone_starts_before="$(cat "${state}/zone.starts")"
run_upgrade > "${fixture}/idempotent-output"
require grep -F 'IDEMPOTENT_REDEPLOY=PASS' "${fixture}/idempotent-output"
require grep -F 'OWNERSHIP_DIR_FIXTURE_IDEMPOTENT=PASS' "${fixture}/idempotent-output"
require test "$(cat "${state}/ownership-directory")" = "${root}/var/lib/ao-rebirth/session-ownership"
require test "$(cat "${state}/login.starts")" = "${login_starts_before}"
require test "$(cat "${state}/zone.starts")" = "${zone_starts_before}"
tests_run=$((tests_run + 1))

behavior_hash_after="$(sha256sum "${repository_root}/AORebirth/Server/LoginEngine/CoreClient/LoginHandoffLifecycle.cs" \
    "${repository_root}/AORebirth/Server/ZoneEngine/StaleOnlineRecovery.cs" \
    "${repository_root}/AORebirth/Server/ZoneEngine/Core/Playfields/Playfield.cs")"
require test "${behavior_hash_before}" = "${behavior_hash_after}"
tests_run=$((tests_run + 1))
require test "${tests_run}" = 56
echo "PASS: production deployment workflow tests (56/56)"
