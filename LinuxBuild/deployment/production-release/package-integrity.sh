#!/usr/bin/env bash
# Hash every payload file and enforce the declared executable dependencies.
package_verify()
{
    local root="$1" inventory="$1/LinuxBuild/PACKAGE_CONTENTS.tsv" digest mode relative
    [[ -f "${inventory}" && ! -L "${inventory}" ]] || { echo 'PACKAGE_INTEGRITY=FAIL missing inventory' >&2; return 1; }
    [[ -z "$(find "${root}/LinuxBuild" -type l -print -quit)" ]] || return 1
    awk -F '\t' 'NF != 3 || length($1) != 64 || $1 ~ /[^0-9a-f]/ || $2 !~ /^0[0-7][0-7][0-7]$/ || $3 !~ /^LinuxBuild\// || $3 ~ /(^|\/)\.\.(\/|$)|\\/ || seen[$3]++ { exit 1 } END { if (NR < 1) exit 1 }' "${inventory}" || return 1
    diff -u <( { cut -f3 "${inventory}"; printf '%s\n' LinuxBuild/PACKAGE_CONTENTS.tsv; } | sort) \
        <(cd -- "${root}" && find LinuxBuild -type f | sort) || return 1
    (cd -- "${root}" && awk -F '\t' '{print $1 "  " $3}' "${inventory}" | sha256sum --check --strict --status) || return 1
    while IFS=$'\t' read -r digest mode relative; do
        [[ -f "${root}/${relative}" && ! -L "${root}/${relative}" ]] || return 1
        [[ "$(stat -c '%a' "${root}/${relative}")" == "${mode#0}" ]] || { echo "PACKAGE_MODE=FAIL ${relative}" >&2; return 1; }
    done < "${inventory}"
    echo 'DEPLOYMENT_DEPENDENCY_COMPLETENESS_GUARD=PASS'
}

package_verify_installed()
{
    local root="$1" login="$2" zone="$3" inventory="$1/LinuxBuild/PACKAGE_CONTENTS.tsv"
    package_verify "${root}" || return 1
    awk -F '\t' -v login="${login}" -v zone="${zone}" '
        $3 ~ /^LinuxBuild\/artifacts\/loginengine\/linux-x64\/self-contained\// { sub(/^LinuxBuild\/artifacts\/loginengine\/linux-x64\/self-contained/,login,$3); print $1 "  " $3 }
        $3 ~ /^LinuxBuild\/artifacts\/zoneengine\/linux-x64\/self-contained\// { sub(/^LinuxBuild\/artifacts\/zoneengine\/linux-x64\/self-contained/,zone,$3); print $1 "  " $3 }
    ' "${inventory}" | sha256sum --check --strict --status || return 1
    echo 'INSTALLED_RELEASE_IDENTITY=PASS'
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    set -euo pipefail
    package_verify "$1"
fi
