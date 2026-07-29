"""Serve Item Store + Daily like GMI: Funcom HTTP hosts under htdocs."""
from pathlib import Path
import re
import shutil
import subprocess
import ssl
import urllib.request

SRC = Path(r"C:\Users\nermi\source\repos\AORebirth\tools-temp\icc-rk-local-web")
HTDOCS = Path(r"C:\xampp\htdocs")
VHOSTS = Path(r"C:\xampp\apache\conf\extra\httpd-vhosts.conf")
HTTPD = Path(r"C:\xampp\apache\bin\httpd.exe")
HOSTS = Path(r"C:\Windows\System32\drivers\etc\hosts")

HTACCESS_SUB = """DirectoryIndex index.app index.php index.html
AddType text/html .app

<IfModule mod_headers.c>
  Header set Cache-Control "no-store, no-cache, must-revalidate, max-age=0"
  Header set Pragma "no-cache"
  Header set Expires "0"
</IfModule>
"""

HTACCESS_ROOT = """DirectoryIndex index.app index.php index.html
AddType text/html .app

RewriteEngine On
# VGTP hostnames (address bar) -> app folders
RewriteCond %{HTTP_HOST} ^uwg\\.daily\\.icc-rk$ [NC]
RewriteRule ^index\\.app$ daily/index.app [L]
RewriteCond %{HTTP_HOST} ^uwg\\.store\\.icc-rk$ [NC]
RewriteRule ^index\\.app$ store/index.app [L]
# Funcom Item Shop host (what Awesomium actually fetches) -> /shop/
RewriteCond %{HTTP_HOST} ^aoshop\\.funcom\\.com$ [NC]
RewriteRule ^$ shop/index.app [L]
RewriteCond %{HTTP_HOST} ^aoshop\\.funcom\\.com$ [NC]
RewriteRule ^shop/?$ shop/index.app [L]

<IfModule mod_headers.c>
  Header set Cache-Control "no-store, no-cache, must-revalidate, max-age=0"
  Header set Pragma "no-cache"
  Header set Expires "0"
</IfModule>
"""

DAILY_VHOST = """
# Daily Login Rewards (Awesomium loads http://dailyrewards.anarchy-online.com/)
<VirtualHost *:80>
    DocumentRoot "C:/xampp/htdocs/daily"
    ServerName dailyrewards.anarchy-online.com
    DirectoryIndex index.app index.php index.html
    AddType text/html .app
    <Directory "C:/xampp/htdocs/daily">
        Options Indexes FollowSymLinks Includes ExecCGI
        AllowOverride All
        Require all granted
    </Directory>
</VirtualHost>
"""

STORE_MARK = "LOCAL AORebirth Item Store"
DAILY_MARK = "LOCAL AORebirth Daily Rewards"


def ensure_hosts(entries):
    text = HOSTS.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()
    existing = "\n".join(lines).lower()
    changed = False
    for host in entries:
        if host.lower() in existing:
            # ensure no broken concat; keep as-is if present
            continue
        lines.append(f"127.0.0.1    {host}")
        changed = True
        print("hosts add", host)
    if changed:
        HOSTS.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
        subprocess.call(["ipconfig", "/flushdns"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    else:
        print("hosts already has aoshop + dailyrewards")


def sync_folder(src_name: str, dst_name: str, base_href: str, title_mark: str) -> Path:
    src = SRC / src_name
    dst = HTDOCS / dst_name
    dst.mkdir(parents=True, exist_ok=True)
    for path in src.rglob("*"):
        if path.is_file():
            rel = path.relative_to(src)
            target = dst / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    html = (dst / "index.html").read_text(encoding="utf-8")
    if "<base " in html:
        html = re.sub(r'<base href="[^"]*">', f'<base href="{base_href}">', html)
    else:
        html = html.replace("<head>", f'<head>\n  <base href="{base_href}">', 1)
    (dst / "index.html").write_text(html, encoding="utf-8", newline="\n")
    shutil.copy2(dst / "index.html", dst / "index.app")
    (dst / ".htaccess").write_text(HTACCESS_SUB, encoding="utf-8", newline="\n")
    print("synced", dst, "base", base_href, "mark", title_mark in html)
    return dst


def patch_vhosts():
    text = VHOSTS.read_text(encoding="utf-8", errors="replace")
    # Ensure trade vhost aliases aoshop (same DocumentRoot as aomarket/GMI)
    if "aoshop.funcom.com" not in text:
        text = text.replace(
            "ServerAlias aomarket.funcom.com",
            "ServerAlias aomarket.funcom.com\n    ServerAlias aoshop.funcom.com",
            1,
        )
        print("added ServerAlias aoshop.funcom.com")
    # Replace prior dailyrewards vhost if any
    text = re.sub(
        r"\n?#\s*Daily Login Rewards \(Awesomium.*?</VirtualHost>\s*",
        "\n",
        text,
        flags=re.S | re.I,
    )
    if "dailyrewards.anarchy-online.com" not in text:
        text = text.rstrip() + "\n" + DAILY_VHOST
        print("added dailyrewards vhost")
    VHOSTS.write_text(text, encoding="utf-8", newline="\n")


def probe(scheme, host, path="/"):
    ctx = ssl._create_unverified_context() if scheme == "https" else None
    req = urllib.request.Request(f"{scheme}://127.0.0.1{path}", headers={"Host": host})
    with urllib.request.urlopen(req, context=ctx, timeout=5) as r:
        body = r.read(2000).decode("utf-8", "replace")
        low = body.lower()
        title = ""
        if "<title>" in low:
            i = low.index("<title>") + 7
            title = body[i : i + 60].split("<")[0]
        mark = ""
        if "local aorebirth" in low:
            mark = " LOCAL_MARK"
        if "temporarily unavailable" in low or "temporily unavailable" in low:
            mark += " FUNCOM_ERR"
        print(f"{scheme} Host={host} {path} -> {r.status} {title!r}{mark}")


def main():
    ensure_hosts(["aoshop.funcom.com", "dailyrewards.anarchy-online.com"])
    # Keep /store and /daily for VGTP hosts; GMI-style paths for Funcom hosts
    sync_folder("store", "store", "/store/", STORE_MARK)
    sync_folder("store", "shop", "/shop/", STORE_MARK)
    # dailyrewards vhost DocumentRoot is htdocs/daily, so assets are at /css /js
    sync_folder("daily", "daily", "/", DAILY_MARK)
    (HTDOCS / ".htaccess").write_text(HTACCESS_ROOT, encoding="utf-8", newline="\n")
    patch_vhosts()

    subprocess.check_call([str(HTTPD), "-t"])
    subprocess.call(["taskkill", "/F", "/IM", "httpd.exe"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.check_call(["ping", "-n", "3", "127.0.0.1"], stdout=subprocess.DEVNULL)
    subprocess.Popen([str(HTTPD)], cwd=r"C:\xampp")
    subprocess.check_call(["ping", "-n", "4", "127.0.0.1"], stdout=subprocess.DEVNULL)

    checks = [
        ("http", "aoshop.funcom.com", "/shop/"),
        ("http", "aoshop.funcom.com", "/shop/index.app"),
        ("http", "aomarket.funcom.com", "/market/"),
        ("http", "dailyrewards.anarchy-online.com", "/"),
        ("http", "dailyrewards.anarchy-online.com", "/index.app"),
        ("http", "uwg.store.icc-rk", "/index.app"),
        ("http", "uwg.daily.icc-rk", "/index.app"),
    ]
    for args in checks:
        try:
            probe(*args)
        except Exception as e:
            print("FAIL", args, e)


if __name__ == "__main__":
    main()
