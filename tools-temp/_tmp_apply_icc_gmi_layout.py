"""Align daily/store with GMI market layout under htdocs + Host rewrite."""
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
RewriteCond %{HTTP_HOST} ^uwg\\.daily\\.icc-rk$ [NC]
RewriteRule ^index\\.app$ daily/index.app [L]
RewriteCond %{HTTP_HOST} ^uwg\\.store\\.icc-rk$ [NC]
RewriteRule ^index\\.app$ store/index.app [L]

<IfModule mod_headers.c>
  Header set Cache-Control "no-store, no-cache, must-revalidate, max-age=0"
  Header set Pragma "no-cache"
  Header set Expires "0"
</IfModule>
"""

VHOST_BLOCK = """
# Daily Login Rewards: vgtp://uwg.daily.icc-rk/index.app (assets in /daily/)
# Same pattern as GMI: DocumentRoot=htdocs, not a hostname folder.
<VirtualHost *:80>
    DocumentRoot "C:/xampp/htdocs"
    ServerName uwg.daily.icc-rk
    DirectoryIndex index.app index.php index.html
    AddType text/html .app
    <Directory "C:/xampp/htdocs">
        Options Indexes FollowSymLinks Includes ExecCGI
        AllowOverride All
        Require all granted
    </Directory>
</VirtualHost>

# Item Store: vgtp://uwg.store.icc-rk/index.app (assets in /store/)
<VirtualHost *:80>
    DocumentRoot "C:/xampp/htdocs"
    ServerName uwg.store.icc-rk
    DirectoryIndex index.app index.php index.html
    AddType text/html .app
    <Directory "C:/xampp/htdocs">
        Options Indexes FollowSymLinks Includes ExecCGI
        AllowOverride All
        Require all granted
    </Directory>
</VirtualHost>
"""


def sync_app(name: str) -> None:
    src = SRC / name
    dst = HTDOCS / name
    dst.mkdir(parents=True, exist_ok=True)
    for path in src.rglob("*"):
        if path.is_file():
            rel = path.relative_to(src)
            target = dst / rel
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    shutil.copy2(dst / "index.html", dst / "index.app")
    (dst / ".htaccess").write_text(HTACCESS_SUB, encoding="utf-8", newline="\n")
    print(f"synced {dst}")


def rewrite_vhosts() -> None:
    text = VHOSTS.read_text(encoding="utf-8", errors="replace")
    # Drop any prior daily/store VirtualHost blocks (old hostname DocumentRoots or ours).
    text = re.sub(
        r"\n?#\s*Daily Login Rewards:.*?</VirtualHost>\s*",
        "\n",
        text,
        flags=re.S | re.I,
    )
    text = re.sub(
        r"\n?#\s*Item Store:.*?</VirtualHost>\s*",
        "\n",
        text,
        flags=re.S | re.I,
    )
    # Also drop orphan hostname-folder vhosts if comments were missing.
    text = re.sub(
        r"\n?<VirtualHost \*:80>\s*DocumentRoot \"C:/xampp/htdocs/uwg\.(daily|store)\.icc-rk\".*?</VirtualHost>\s*",
        "\n",
        text,
        flags=re.S | re.I,
    )
    VHOSTS.write_text(text.rstrip() + "\n" + VHOST_BLOCK, encoding="utf-8", newline="\n")
    print("vhosts updated")


def probe(scheme: str, host: str) -> None:
    ctx = ssl._create_unverified_context() if scheme == "https" else None
    req = urllib.request.Request(
        f"{scheme}://127.0.0.1/index.app", headers={"Host": host}
    )
    with urllib.request.urlopen(req, context=ctx, timeout=5) as r:
        body = r.read(1200).decode("utf-8", "replace")
        low = body.lower()
        title = ""
        if "<title>" in low:
            i = low.index("<title>") + 7
            title = body[i : i + 80].split("<")[0]
        print(f"{scheme} {host} -> {r.status} {title!r}")


def main() -> None:
    sync_app("daily")
    sync_app("store")
    (HTDOCS / ".htaccess").write_text(HTACCESS_ROOT, encoding="utf-8", newline="\n")
    print("wrote htdocs/.htaccess rewrite")
    rewrite_vhosts()

    subprocess.check_call([str(HTTPD), "-t"])
    subprocess.call(["taskkill", "/F", "/IM", "httpd.exe"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.check_call(["ping", "-n", "3", "127.0.0.1"], stdout=subprocess.DEVNULL)
    subprocess.Popen([str(HTTPD)], cwd=r"C:\xampp")
    subprocess.check_call(["ping", "-n", "4", "127.0.0.1"], stdout=subprocess.DEVNULL)

    for scheme in ("http", "https"):
        for host in ("uwg.daily.icc-rk", "uwg.store.icc-rk", "uwg.trade.omni-rk"):
            try:
                probe(scheme, host)
            except Exception as e:
                print(f"{scheme} {host} -> FAIL {e}")


if __name__ == "__main__":
    main()
