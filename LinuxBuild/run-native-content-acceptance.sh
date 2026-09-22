#!/usr/bin/env bash
set -euo pipefail
# Invoke inside an isolated native Linux SDK environment, never production.
expected_sha="${1:?exact public master SHA required}"
workspace="${2:?controlled acceptance workspace required}"
[[ "$#" == 2 && "${expected_sha}" =~ ^[0-9a-f]{40}$ ]] || exit 2
for prerequisite in dotnet git git-lfs python3 zip unzip; do
    command -v "${prerequisite}" >/dev/null || { echo "ACCEPTANCE_PREREQUISITE_MISSING=${prerequisite}" >&2; exit 2; }
done
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
bash "${script_dir}/accept-linux-sha.sh" \
    --expected-sha "${expected_sha}" \
    --repo-url https://github.com/subarumike/AORebirth.git \
    --workspace "${workspace}" --runtime linux-x64 --self-contained true
