#!/usr/bin/env bash
set -euo pipefail

target="/opt/ao-rebirth/forum/current/inc/functions.php"
needle='	if($samesite != "" && $mybb->settings['\''cookiesamesiteflag'\''])
	{'
replacement='	if($samesite == "" && $mybb->settings['\''cookiesamesiteflag'\''])
	{
		$samesite = "lax";
	}

	if($samesite != "" && $mybb->settings['\''cookiesamesiteflag'\''])
	{'

python3 - "$target" "$needle" "$replacement" <<'PY'
from pathlib import Path
import sys

path = Path(sys.argv[1])
needle = sys.argv[2]
replacement = sys.argv[3]
text = path.read_text(encoding="utf-8")
if replacement in text:
    print("MYBB_SAMESITE_PATCH=ALREADY_PRESENT")
    raise SystemExit(0)
if needle not in text:
    print("MYBB_SAMESITE_PATCH=NEEDLE_NOT_FOUND")
    raise SystemExit(1)
path.write_text(text.replace(needle, replacement, 1), encoding="utf-8", newline="\n")
print("MYBB_SAMESITE_PATCH=APPLIED")
PY
