#!/usr/bin/env python3
"""
Driver for running + browser-driving Agrumy.Api + Agrumy.Web locally.
See SKILL.md in this same directory for the full writeup - this file
is the harness, not documentation.

Requires: .NET 10 SDK, Python `playwright` package with Chromium
installed (`python -m playwright install chromium` if missing), and
the AGRUMY_TEST_PASSWORD env var set (see SKILL.md "Setup") before
`shot`/`eval`.

Usage:
    python driver.py check-config          # verify the two appsettings.json exist (see SKILL.md Setup)
    python driver.py up                    # start Api (5100) + Web (5101)
    python driver.py status                # check both are answering
    python driver.py shot <path> <out.png> # login, navigate, screenshot
    python driver.py eval <path> <js-file>  # login, navigate, page.evaluate(), print JSON result
    python driver.py down                  # stop both

<path> is anything after the host, e.g. "/FarmOpenfield/Parcel?idFarmParcelZone=7".
Login credentials and ports are documented in SKILL.md.
"""
import json
import os
import subprocess
import sys
import time
import urllib.request

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
API_PORT = 5100
WEB_PORT = 5101
API_BASE = f"http://localhost:{API_PORT}"
WEB_BASE = f"http://localhost:{WEB_PORT}"
LOGIN = os.environ.get("AGRUMY_TEST_LOGIN", "admin@agrumy.local")
PASSWORD = os.environ.get("AGRUMY_TEST_PASSWORD")
PIDFILE = os.path.join(os.environ.get("TEMP", "/tmp"), "agrumy_driver_pids.json")

API_APPSETTINGS_PATH = os.path.join(REPO_ROOT, "Agrumy.Api", "appsettings.json")
WEB_APPSETTINGS_PATH = os.path.join(REPO_ROOT, "Agrumy.Web", "appsettings.json")


def check_config():
    missing = [p for p in (API_APPSETTINGS_PATH, WEB_APPSETTINGS_PATH) if not os.path.exists(p)]
    if not missing:
        print("Both appsettings.json files exist - good to go.")
        return
    print("Missing local config (gitignored, not in git):")
    for p in missing:
        print(f"  {p}")
    print()
    print("See SKILL.md 'Setup' section: copy the known-good appsettings.json block from the")
    print("repo root CLAUDE.md ('Deploy na api.agrumy.com' -> 'appsettings.json' section) into")
    print(f"the file(s) above, then change \"Urls\" to http://localhost:{API_PORT} (Api) /")
    print(f"http://localhost:{WEB_PORT} (Web), and Agrumy.Web's WebView:ApiService to")
    print(f"http://localhost:{API_PORT} - do NOT reuse the real deploy's 5000/5001 ports.")
    sys.exit(1)


def _wait_up(url, timeout=60):
    deadline = time.time() + timeout
    while time.time() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=3) as resp:
                if resp.status == 200:
                    return True
        except Exception:
            pass
        time.sleep(2)
    return False


def up():
    check_config()
    pids = {}
    # CompressionEnabled=false works around a confirmed bug in this checkout's static-assets
    # pipeline (see SKILL.md Gotchas) - without it, every static asset silently serves as an
    # empty body to any real client that sends Accept-Encoding (i.e. every real browser).
    api_proc = subprocess.Popen(
        ["dotnet", "run", "--project", "Agrumy.Api/Agrumy.Api.csproj", "-c", "Debug",
         "--no-launch-profile", "-p:CompressionEnabled=false"],
        cwd=REPO_ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        creationflags=getattr(subprocess, "DETACHED_PROCESS", 0) | getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0),
    )
    pids["api"] = api_proc.pid
    web_proc = subprocess.Popen(
        ["dotnet", "run", "--project", "Agrumy.Web/Agrumy.Web.csproj", "-c", "Debug",
         "--no-launch-profile", "-p:CompressionEnabled=false"],
        cwd=REPO_ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        creationflags=getattr(subprocess, "DETACHED_PROCESS", 0) | getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0),
    )
    pids["web"] = web_proc.pid
    with open(PIDFILE, "w") as f:
        json.dump(pids, f)

    api_ok = _wait_up(f"{API_BASE}/api/User/BootstrapPending")
    web_ok = _wait_up(f"{WEB_BASE}/Login")
    print(f"Api  ({API_BASE}): {'up' if api_ok else 'NOT RESPONDING'}")
    print(f"Web  ({WEB_BASE}): {'up' if web_ok else 'NOT RESPONDING'}")
    if not (api_ok and web_ok):
        sys.exit(1)


def status():
    api_ok = _wait_up(f"{API_BASE}/api/User/BootstrapPending", timeout=3)
    web_ok = _wait_up(f"{WEB_BASE}/Login", timeout=3)
    print(f"Api  ({API_BASE}): {'up' if api_ok else 'down'}")
    print(f"Web  ({WEB_BASE}): {'up' if web_ok else 'down'}")


def down():
    if not os.path.exists(PIDFILE):
        print("No pidfile - nothing to stop (were they started outside this driver?).")
        return
    with open(PIDFILE) as f:
        pids = json.load(f)
    for name, pid in pids.items():
        try:
            subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], capture_output=True)
            print(f"Stopped {name} (pid {pid})")
        except Exception as e:
            print(f"Could not stop {name} (pid {pid}): {e}")
    os.remove(PIDFILE)


def _login(page):
    if not PASSWORD:
        print("AGRUMY_TEST_PASSWORD is not set - see SKILL.md 'Setup' for where to get it.")
        sys.exit(1)
    page.goto(f"{WEB_BASE}/Login", wait_until="load")
    page.fill('input[name="Login"]', LOGIN)
    page.fill('input[name="Password"]', PASSWORD)
    page.click('input[type="submit"]')
    page.wait_for_load_state("load")


def shot(path, out_path):
    from playwright.sync_api import sync_playwright
    with sync_playwright() as p:
        browser = p.chromium.launch(args=["--no-sandbox"])
        page = browser.new_page()
        errors = []
        page.on("pageerror", lambda e: errors.append(str(e)))
        _login(page)
        page.goto(f"{WEB_BASE}{path}", wait_until="load")
        page.wait_for_timeout(3000)
        page.screenshot(path=out_path, full_page=True)
        browser.close()
    print(f"Saved {out_path}")
    if errors:
        print("Page errors:", errors)


def eval_js(path, js_file):
    from playwright.sync_api import sync_playwright
    with open(js_file, encoding="utf-8") as f:
        script = f.read()
    with sync_playwright() as p:
        browser = p.chromium.launch(args=["--no-sandbox"])
        page = browser.new_page()
        _login(page)
        page.goto(f"{WEB_BASE}{path}", wait_until="load")
        page.wait_for_timeout(1000)
        result = page.evaluate(script)
        browser.close()
    print(json.dumps(result, indent=2, default=str))


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    cmd = sys.argv[1]
    if cmd == "check-config":
        check_config()
    elif cmd == "up":
        up()
    elif cmd == "status":
        status()
    elif cmd == "down":
        down()
    elif cmd == "shot":
        shot(sys.argv[2], sys.argv[3])
    elif cmd == "eval":
        eval_js(sys.argv[2], sys.argv[3])
    else:
        print(f"Unknown command: {cmd}")
        print(__doc__)
        sys.exit(1)


if __name__ == "__main__":
    main()
