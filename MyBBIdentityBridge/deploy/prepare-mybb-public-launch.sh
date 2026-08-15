#!/usr/bin/env bash
set -euo pipefail

forum_root="${FORUM_ROOT:-/opt/ao-rebirth/forum/current}"
forum_shared="${FORUM_SHARED:-/opt/ao-rebirth/forum/shared}"
backup_root="${BACKUP_ROOT:-/opt/ao-rebirth/database/backups}"
mysql_container="${MYSQL_CONTAINER:-ao-rebirth-mysql}"
forum_container="${FORUM_CONTAINER:-ao-rebirth-forum}"
css_source="${CSS_SOURCE:-$(dirname "$0")/aorebirth-forum-launch.css}"
css_target="$forum_root/aorebirth-forum-launch.css"
config_path="$forum_shared/config.php"
mysql_secret_path="/opt/ao-rebirth/database/secrets/mysql_mybb_password"

read_config() {
  python3 - "$config_path" "$1" <<'PY'
import re
import sys

path, key = sys.argv[1], sys.argv[2]
text = open(path, encoding="utf-8").read()
pattern = r"\$config\['database'\]\['" + re.escape(key) + r"'\]\s*=\s*'([^']*)';"
match = re.search(pattern, text)
if not match:
    raise SystemExit(f"missing MyBB config key: {key}")
print(match.group(1))
PY
}

db_name="$(read_config database)"
db_user="$(read_config username)"
db_pass="$(read_config password)"
db_host="$(read_config hostname)"
db_prefix="$(read_config table_prefix)"

if [ -f "$mysql_secret_path" ]; then
  printf '%s' "$db_pass" > "$mysql_secret_path"
  chmod 600 "$mysql_secret_path"
  chown root:root "$mysql_secret_path"
fi

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup_dir="$backup_root/mybb-launch-public-$stamp"
mkdir -p "$backup_dir"
chmod 700 "$backup_dir"

docker exec -i -e MYSQL_PWD="$db_pass" "$mysql_container" \
  mysqldump --no-tablespaces -h "$db_host" -u "$db_user" "$db_name" \
  > "$backup_dir/${db_name}.sql"
tar -C "$forum_shared" -czf "$backup_dir/forum-shared.tar.gz" \
  config.php settings.php aorebirth_identity_bridge_config.php uploads avatars attachments cache secrets
sha256sum "$backup_dir/${db_name}.sql" "$backup_dir/forum-shared.tar.gz" > "$backup_dir/SHA256SUMS"

install -m 0644 "$css_source" "$css_target"
chown root:www-data "$css_target" 2>/dev/null || true

sql_file="$(mktemp)"
python3 - "$db_prefix" <<'PY' > "$sql_file"
import time
import sys

prefix = sys.argv[1]
now = int(time.time())
admin_uid = 1
admin_username = "AORebirthAdmin"

def q(value):
    return "'" + str(value).replace("\\", "\\\\").replace("'", "''") + "'"

settings = {
    "bbname": "AORebirth Forums",
    "bbdescription": "Official AORebirth community forum for announcements, support, bug reports, development discussion, and player coordination.",
    "bburl": "https://forum.ao-rebirth.com",
    "homename": "AORebirth",
    "homeurl": "https://ao-rebirth.com",
    "adminemail": "forum@ao-rebirth.com",
    "contactemail": "forum@ao-rebirth.com",
    "returnemail": "forum@ao-rebirth.com",
    "mailingaddress": "forum@ao-rebirth.com",
    "enableemail": "0",
    "mail_handler": "mail",
    "mail_logging": "2",
    "postfloodcheck": "1",
    "postfloodsecs": "60",
    "pmfloodsecs": "60",
    "enablepms": "1",
    "enableattachments": "1",
    "maxattachments": "5",
    "attachthumbnails": "yes",
    "attachthumbh": "160",
    "attachthumbw": "160",
    "allowremoteavatars": "0",
    "useravatardims": "100|100",
    "maxavatardims": "100x100",
    "avatarsize": "100",
    "siglength": "255",
    "maxsigimages": "1",
    "sightml": "0",
    "pmsallowhtml": "0",
    "announcementshtml": "0",
    "cookiesecureflag": "1",
    "cookiesamesiteflag": "1",
    "disableregs": "1",
}

forums = {
    1: "Official AORebirth news, important notices, update information, server status, and community policies.",
    2: "Official AORebirth news, important notices, and community announcements.",
    3: "Release notes, fixes, changes, and update history.",
    4: "Maintenance notices, outages, service restoration, and availability updates.",
    5: "Forum rules, game rules, account policies, and community standards.",
    6: "General AORebirth community discussion and player coordination.",
    7: "General AORebirth and Anarchy Online discussion.",
    8: "New players, returning veterans, and community introductions.",
    9: "Screenshots, gameplay clips, community media, and visual content.",
    10: "Organization recruitment, events, coordination, and community groups.",
    11: "Discussion not directly related to AORebirth.",
    12: "Gameplay discussion, shared knowledge, and player help.",
    13: "Questions and assistance for new or returning players.",
    14: "Builds, strategies, equipment, and profession discussion.",
    15: "Missions, dungeons, encounters, raids, and general PvE discussion.",
    16: "PvP strategy, tower battles, duels, and competitive gameplay.",
    17: "Tradeskill processes, recipes, materials, and crafting help.",
    18: "Weapons, armor, implants, symbiants, loot, and equipment discussion.",
    19: "Community-created guides, references, and useful resources.",
    20: "Support for AORebirth accounts, installation, connection, launcher, and gameplay issues.",
    21: "Installation, crashes, graphics, compatibility, and technical troubleshooting.",
    22: "Account access, registration, login, and account-related help.",
    23: "Reproducible reports of broken game or website behavior.",
    24: "Login, connection, launcher, patching, and client-update problems.",
    25: "Development updates, implementation discussion, testing coordination, and community feedback.",
    26: "Official development updates and milestone reports.",
    27: "Server architecture, gameplay systems, protocol, database, and runtime work.",
    28: "Replacement client, rendering, UI, input, and client-side engineering.",
    29: "Quests, NPCs, missions, vendors, world systems, and restoration work.",
    30: "Player suggestions and proposed improvements.",
    31: "Test builds, QA coordination, test-server feedback, and validation.",
    32: "Player buying, selling, trading, and services.",
    33: "Player purchase requests.",
    34: "Items offered for sale.",
    35: "Item-for-item exchanges.",
    36: "Tradeskills and other in-game services.",
    37: "Read-only archive forums retained for reference.",
    38: "Closed and resolved historical bug reports.",
    39: "Archived discussions about older releases.",
    40: "Historical development discussions retained for reference.",
}

threads = [
    (2, "Welcome to AORebirth", True, "", """Welcome to the AORebirth forum.

AORebirth is a community restoration project for a private Anarchy Online server environment. The forum exists as the public place for official notices, support, bug reports, development updates, and player discussion.

Your forum identity is linked to your AORebirth account. Use the AORebirth website to register or log in, then use the forum login handoff. Do not use native MyBB registration or post account credentials publicly.

Useful places to start:
[list]
[*]Bugs: AORebirth Support -> Bug Reports
[*]Installation or crash help: AORebirth Support -> Technical Support
[*]Account login or access questions: AORebirth Support -> Account Support
[*]Development discussion: Development
[*]General community discussion: Community
[/list]"""),
    (5, "Forum Rules", True, "closed", """AORebirth forum rules are intended to keep the community useful and safe.

[list]
[*]Keep discussion respectful. Harassment, threats, and targeted abuse are not allowed.
[*]Do not post private information, doxxing material, passwords, session cookies, verification links, or account ownership details.
[*]Do not share credentials or ask another user for credentials.
[*]Do not spam, flood, impersonate staff, or misrepresent official project decisions.
[*]Do not post malware, credential-stealing files, or exploit instructions intended to harm the live service.
[*]Use Bug Reports for reproducible defects and Suggestions & Feature Requests for proposed changes.
[*]Marketplace posts must be clear and honest. Scams and deceptive listings are not allowed.
[*]Moderators may move, edit, hide, or close posts to protect users and keep the forum organized.
[*]Account enforcement may apply to forum and game access when conduct creates a real service or community risk.
[/list]"""),
    (5, "How AORebirth Accounts Work", True, "closed", """AORebirth accounts are created and managed through the AORebirth website and Account Broker.

The forum uses linked AORebirth identity for login. MyBB does not receive your AO password, and users should not try to register directly inside MyBB.

Use:
[list]
[*]Register: https://ao-rebirth.com/register
[*]Login / My Account: https://ao-rebirth.com/login
[*]Forum login handoff: https://ao-rebirth.com/forum-login
[/list]

Never post your password, email verification links, session cookies, or private account ownership information. If a support case requires private details, use the approved private support path instead of posting them in a public thread."""),
    (4, "Current Server Status", True, "", """Use this forum for official availability and maintenance notices.

If the game, website, or forum is unavailable, staff will post status updates here when practical. Player connection problems should go to AORebirth Support -> Connection / Launcher Issues unless an official incident thread already exists."""),
    (3, "Patch Notes / Update Index", True, "closed", """Future patch notes will be posted as one thread per release or deployment.

Preferred title format:

[code]AORebirth Update - YYYY-MM-DD[/code]

Each update should summarize player-visible changes, fixes, known issues, and any required client or launcher action."""),
    (23, "How to Report a Bug", True, "", """Use this template for reproducible game, website, or forum defects:

[code]Title:
Character:
Playfield:
Date/Time:
What happened:
What should have happened:
Steps to reproduce:
Client version:
Screenshots/logs:[/code]

Do not post passwords, email verification links, session cookies, private account data, or other credentials. If a log contains private data, redact it before posting."""),
    (21, "Technical Support - Read Before Posting", True, "", """When asking for technical help, include:

[list]
[*]Client version
[*]Windows version
[*]Launcher version
[*]Exact error text
[*]When the issue occurred
[*]Steps to reproduce it
[*]Relevant logs or screenshots
[/list]

Do not post passwords, private account details, verification links, session cookies, or other credentials."""),
    (22, "Account Support - Protect Your Account Information", True, "", """Account Support is for account access, registration, login, and identity-linking questions.

Do not post passwords, password-reset information, email verification links, private account ownership details, session cookies, or other sensitive information in public threads.

If a case requires private proof or private account details, use the approved private support path. Moderators should move or redact sensitive information if it is accidentally posted."""),
    (26, "Current Development Status", True, "", """AORebirth is currently focused on restoring server behavior, running the production stack on Linux, maintaining the unified account system, and preparing public community infrastructure.

The forum launch gives players a permanent place for announcements, patch notes, support, bug reports, development discussion, and testing coordination.

Ongoing work includes game/server behavior restoration, client and launcher work, content/world restoration, and capture-backed validation where needed."""),
    (30, "Suggestions / Feature Request Guidelines", True, "", """Use this forum for proposed improvements.

Good suggestions explain the problem, the proposed change, why it helps AORebirth, and any likely tradeoffs. Use Bug Reports instead when something is broken and reproducible."""),
    (8, "Introduce Yourself", False, "", """New to AORebirth or returning after a long break? Use this thread or this forum to say hello.

No fake activity is being seeded here; this is simply a place for real community introductions as players arrive."""),
]

print("START TRANSACTION;")
for name, value in settings.items():
    print(f"UPDATE {prefix}settings SET value={q(value)} WHERE name={q(name)};")

for fid, desc in forums.items():
    print(f"UPDATE {prefix}forums SET description={q(desc)}, allowhtml=0 WHERE fid={fid};")

for fid in (2, 3, 4, 5, 38, 39, 40):
    print(f"UPDATE {prefix}forums SET open=0 WHERE fid={fid};")

for fid in (2, 3, 4, 5, 38, 39, 40):
    for gid in (1, 2, 5, 7):
        vals = [fid, gid, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1]
        cols = "fid,gid,canview,canviewthreads,canonlyviewownthreads,candlattachments,canpostthreads,canpostreplys,canonlyreplyownthreads,canpostattachments,canratethreads,caneditposts,candeleteposts,candeletethreads,caneditattachments,canviewdeletionnotice,modposts,modthreads,mod_edit_posts,modattachments,canpostpolls,canvotepolls"
        updates = ",".join([f"{c}=VALUES({c})" for c in cols.split(",")[2:]])
        print(f"INSERT INTO {prefix}forumpermissions ({cols}) VALUES ({','.join(map(str, vals))}) ON DUPLICATE KEY UPDATE {updates};")

print(f"UPDATE {prefix}attachtypes SET enabled=0 WHERE extension IN ('exe','bat','cmd','com','scr','ps1','vbs','js','jar','msi','dll','php','phtml','sh','py','pl');")

print(f"UPDATE {prefix}templates SET template = REPLACE(template, '/cache/aorebirth-forum-launch.css?20260815', '/aorebirth-forum-launch.css?20260815') WHERE title='headerinclude';")
print(f"UPDATE {prefix}templates SET template = CONCAT(template, '\\n<link rel=\"stylesheet\" href=\"{{$mybb->settings[\\'bburl\\']}}/aorebirth-forum-launch.css?20260815\" type=\"text/css\" />') WHERE title='headerinclude' AND template NOT LIKE '%aorebirth-forum-launch.css%';")
nav = '<ul class="aor-launch-nav"><li><a href="https://ao-rebirth.com">AORebirth Home</a></li><li><a href="https://forum.ao-rebirth.com">Forum</a></li><li><a href="https://ao-rebirth.com/register">Register</a></li><li><a href="https://ao-rebirth.com/forum-login">Login / My Account</a></li><li><a href="https://ao-rebirth.com/client-patch-test.php">Downloads</a></li></ul>'
print(f"UPDATE {prefix}templates SET template = REPLACE(template, '<ul class=\"menu top_links\">', {q(nav + '<ul class=\"menu top_links\">')}) WHERE title='header' AND template NOT LIKE '%aor-launch-nav%';")
guest_template = """<!-- Continuation of div(class=\"upper\") as opened in the header template -->
<span class=\"welcome\">Hello There, Guest! <a href=\"https://ao-rebirth.com/forum-login\" class=\"login\">Login with AORebirth</a> <a href=\"https://ao-rebirth.com/register\" class=\"register\">Create Account</a></span>
</div>
</div>"""
print(f"UPDATE {prefix}templates SET template={q(guest_template)} WHERE title='header_welcomeblock_guest';")

for idx, (fid, subject, sticky, closed, message) in enumerate(threads, start=1):
    dateline = now + idx
    subject_q = q(subject)
    message_q = q(message)
    closed_q = q(closed)
    print(f"INSERT INTO {prefix}threads (fid,subject,prefix,icon,poll,uid,username,dateline,firstpost,lastpost,lastposter,lastposteruid,views,replies,closed,sticky,numratings,totalratings,notes,visible,unapprovedposts,deletedposts,attachmentcount,deletetime) SELECT {fid},{subject_q},0,0,0,{admin_uid},{q(admin_username)},{dateline},0,{dateline},{q(admin_username)},{admin_uid},0,0,{closed_q},{1 if sticky else 0},0,0,'',1,0,0,0,0 WHERE NOT EXISTS (SELECT 1 FROM {prefix}threads WHERE fid={fid} AND subject={subject_q});")
    print(f"SET @aor_tid := (SELECT tid FROM {prefix}threads WHERE fid={fid} AND subject={subject_q} ORDER BY tid LIMIT 1);")
    print(f"INSERT INTO {prefix}posts (tid,replyto,fid,subject,icon,uid,username,dateline,message,ipaddress,includesig,smilieoff,edituid,edittime,editreason,visible) SELECT @aor_tid,0,{fid},{subject_q},0,{admin_uid},{q(admin_username)},{dateline},{message_q},INET6_ATON('127.0.0.1'),0,0,0,0,'',1 WHERE NOT EXISTS (SELECT 1 FROM {prefix}posts WHERE tid=@aor_tid AND uid={admin_uid});")
    print(f"UPDATE {prefix}threads SET firstpost=(SELECT pid FROM {prefix}posts WHERE tid=@aor_tid ORDER BY pid LIMIT 1), lastpost={dateline}, lastposter={q(admin_username)}, lastposteruid={admin_uid}, sticky={1 if sticky else 0}, closed={closed_q}, visible=1 WHERE tid=@aor_tid;")

for fid in sorted({fid for fid, *_ in threads} | set(forums.keys())):
    print(f"UPDATE {prefix}forums f SET threads=(SELECT COUNT(*) FROM {prefix}threads t WHERE t.fid=f.fid AND t.visible=1), posts=(SELECT COUNT(*) FROM {prefix}posts p WHERE p.fid=f.fid AND p.visible=1), lastpost=COALESCE((SELECT MAX(t.lastpost) FROM {prefix}threads t WHERE t.fid=f.fid AND t.visible=1),0) WHERE f.fid={fid};")
    print(f"UPDATE {prefix}forums f LEFT JOIN {prefix}threads t ON t.tid=(SELECT tt.tid FROM {prefix}threads tt WHERE tt.fid=f.fid AND tt.visible=1 ORDER BY tt.lastpost DESC LIMIT 1) SET f.lastposttid=COALESCE(t.tid,0), f.lastpostsubject=COALESCE(t.subject,''), f.lastposter=COALESCE(t.lastposter,''), f.lastposteruid=COALESCE(t.lastposteruid,0) WHERE f.fid={fid};")

print(f"UPDATE {prefix}users u SET postnum=(SELECT COUNT(*) FROM {prefix}posts p WHERE p.uid=u.uid AND p.visible=1) WHERE u.uid={admin_uid};")
print(f"UPDATE {prefix}stats SET numthreads=(SELECT COUNT(*) FROM {prefix}threads WHERE visible=1), numposts=(SELECT COUNT(*) FROM {prefix}posts WHERE visible=1), numusers=(SELECT COUNT(*) FROM {prefix}users);")
print("COMMIT;")
PY

docker exec -i -e MYSQL_PWD="$db_pass" "$mysql_container" \
  mysql -h "$db_host" -u "$db_user" "$db_name" < "$sql_file"
rm -f "$sql_file"

rebuild_script="$forum_root/aorebirth_launch_rebuild.php"
cat > "$rebuild_script" <<'PHP'
<?php
define('IN_MYBB', 1);
define('NO_ONLINE', 1);
require __DIR__ . '/global.php';
require_once MYBB_ROOT . 'inc/functions_rebuild.php';

if (function_exists('rebuild_settings')) {
    rebuild_settings();
}

if (function_exists('rebuild_stats')) {
    rebuild_stats();
}

if (isset($cache) && is_object($cache)) {
    if (method_exists($cache, 'update_forums')) {
        $cache->update_forums();
    }
    if (method_exists($cache, 'update_stats')) {
        $cache->update_stats();
    }
}

echo "AOR_MYBB_REBUILD=PASS\n";
PHP

docker exec "$forum_container" php /var/www/html/aorebirth_launch_rebuild.php
rm -f "$rebuild_script"

curl -fsS -o /dev/null -w "forum_https=%{http_code}\n" https://forum.ao-rebirth.com/
echo "backup_dir=$backup_dir"
echo "AOR_MYBB_PUBLIC_LAUNCH_PREP=PASS"
