---
name: run-agrumy
description: Build, run, and screenshot Agrumy.Api + Agrumy.Web (the agrumy.sln solution) locally against the invent.hr alfa/test DB, and drive the running MVC app in a real browser. Use when asked to start Agrumy, run it locally, build agrumy.sln, take a screenshot of the Agrumy.Web UI, verify a change renders correctly, or log in and click through a page.
---

Two ASP.NET Core apps that talk to each other over HTTP: Agrumy.Api (JSON API,
port 5100 here) and Agrumy.Web (server-rendered MVC admin UI, cookie auth,
port 5101 here, calls Agrumy.Api via Refit). Drive it via
`.claude/skills/run-agrumy/driver.py` (Python + Playwright) - there is no
`chromium-cli` or Node on this machine. All paths below are relative to the
repo root (`AgrumyService-513openfield/`).

## Prerequisites

- .NET 10 SDK (`dotnet --version`).
- Python 3 with `playwright` installed and Chromium fetched:
  ```
  pip show playwright   # confirm it's there; this box already has 1.62.0
  python -m playwright install chromium   # only if the above says "not found"
  ```
- Optional, only if you need to find test data yourself: `pip show pymysql`
  (used to query invent.hr directly - see Gotchas).
- No `node`, `chromium-cli`, or `mysql` CLI on this machine - don't reach for
  them, use the Python equivalents above.

## Setup

`Agrumy.Api/appsettings.json` and `Agrumy.Web/appsettings.json` are gitignored
and do not exist on a fresh checkout - `dotnet run` will start but every DB
call will fail without them.

```
python .claude/skills/run-agrumy/driver.py check-config
```

If it reports them missing: copy the "Poznat-dobar sadržaj appsettings.json"
JSON blocks from the repo root `CLAUDE.md` (section "Deploy na
api.agrumy.com") into the two files above - that's the real invent.hr
connection string / JWT key, already committed there since this is a
standing alfa/test environment, not production. Then change:

- Agrumy.Api's `"Urls"` -> `"http://localhost:5100"`
- Agrumy.Web's `"Urls"` -> `"http://localhost:5101"`, and its
  `"WebView": { "ApiService": ... }` -> `"http://localhost:5100"`

Use 5100/5101, not the real deploy's 5000/5001 - another checkout's instance
is often already running on 5000/5001 (`netstat -ano | grep LISTEN` or
`Get-NetTCPConnection -LocalPort 5000,5001` to check first; never kill what
you didn't start).

The driver logs in as `admin@agrumy.local` before every `shot`/`eval` (every
invent.hr user has the same standing default test password - not committed
here or in CLAUDE.md; ask the user, or check this session's own memory if
you're a returning Claude Code session that has it recorded). Set it once:

```
export AGRUMY_TEST_PASSWORD='<the default test password>'
```

`AGRUMY_TEST_LOGIN` overrides the login (defaults to `admin@agrumy.local`) if
you need a different tenant/role - see the Gotchas section for how to find
one via direct DB query.

## Build

```
dotnet build agrumy.sln -c Debug
```

## Run (agent path)

```
python .claude/skills/run-agrumy/driver.py up       # starts both, waits until healthy
python .claude/skills/run-agrumy/driver.py status   # re-check without restarting
python .claude/skills/run-agrumy/driver.py shot "/FarmOpenfield/Parcel?idFarmParcelZone=7" out.png
python .claude/skills/run-agrumy/driver.py down      # stops both (reads the pidfile `up` wrote)
```

`shot`/`eval` log in as `admin@agrumy.local` / `Pa55w.rd1234!` (the standing
invent.hr default-password test admin - see Gotchas) before navigating, so
any authenticated page works. `<path>` is anything after the host, e.g.
`/FarmOpenfield/Parcel?idFarmParcelZone=7` (a real zone with synced NDVI/NDMI
satellite data - "sat-test-1ha" - useful for anything satellite-related).

| command | what it does |
|---|---|
| `check-config` | verifies both appsettings.json exist, prints setup guidance if not |
| `up` | starts Agrumy.Api (5100) + Agrumy.Web (5101) as detached background processes, polls until both answer |
| `status` | one-shot health check, no side effects |
| `shot <path> <out.png>` | logs in, navigates to `<path>`, full-page screenshot to `<out.png>`, prints any `pageerror`s |
| `eval <path> <js-file>` | logs in, navigates to `<path>`, runs the JS in `<js-file>` (must be a single expression/arrow function) via `page.evaluate`, prints the JSON result - use this to check `typeof X` on page globals, read live DOM state, or hit `fetch()` from an authenticated session without needing the UI to render |
| `down` | stops both by PID (from the pidfile `up` wrote to `%TEMP%\agrumy_driver_pids.json`) |

## Run (human path)

`dotnet run --project Agrumy.Api/Agrumy.Api.csproj -c Debug --no-launch-profile -p:CompressionEnabled=false`
and the same for `Agrumy.Web`, each in its own terminal, Ctrl-C to stop. The
`-p:CompressionEnabled=false` is not optional - see the first Gotcha.

## Test

```
dotnet test agrumy.sln -c Release
```
~1500 pass, ~185 skip (integration tests need `AGRUMY_TEST_MYSQL`/`AGRUMY_TEST_POSTGRES` env vars pointing at real DB containers - see `.github/workflows/build.yml` for the exact connection strings it uses in CI).

## Gotchas

- **Every static asset (JS/CSS) silently serves as an empty body to any real browser unless you build/run with `-p:CompressionEnabled=false`.** This is the single biggest trap here. Symptom: the page loads, every request is 200, `Content-Type`/`Content-Length` on a plain `curl` look fine - but in an actual browser the page is completely unstyled and `typeof L` (Leaflet), `typeof jQuery`, and every project `<script src>`-defined function all come back `"undefined"`, with zero console errors or CSP violations logged anywhere (checked CDP `Log`/`Security` domains directly - silent). Root cause, confirmed by direct testing: `dotnet run`'s static-web-assets precompression (`MapStaticAssets()` serving from `obj/Debug/net10.0/compressed/*.gz`) returns `Content-Length: 0` for a plain **GET** with `Accept-Encoding: gzip` (which every real browser sends) - `curl -I` (HEAD) on the same URL comes back with a perfectly normal header set including `Content-Encoding: gzip`, which is why checking with `curl -I` alone hides the bug. The precompressed `.gz` sidecar files on disk are themselves valid (manually gzip-decompressed one and it was byte-correct) - the bug is in how the endpoint serves them on a real GET. `driver.py up` already passes `-p:CompressionEnabled=false`; if you launch some other way, add it yourself.
- **No node/chromium-cli on this box.** Python's `playwright` package (1.62.0) is installed with Chromium already fetched to `~/AppData/Local/ms-playwright/` - use that instead of trying to install Node.
- **Manual `nohup cmd &` inside a single shell command does not survive** once that shell/tool-call exits (git-bash-on-Windows job-control quirk) - `dotnet run` started that way silently dies the moment the wrapping command returns, even though it printed a PID. Use a real subprocess launch (`subprocess.Popen` with `DETACHED_PROCESS`/`CREATE_NEW_PROCESS_GROUP`, which is what `driver.py up` does) or the harness's own background-process flag - never bare `&`.
- **Logging in via raw `curl -X POST /Login`** gets a `400 Bad Request` - the form requires a matching antiforgery cookie+token pair that only a real browser session produces. Always log in through Playwright (`_login()` in the driver), never by hand-rolling the POST.
- **No known login exists upfront on a given invent.hr checkout.** `admin@agrumy.local` with the standing default test password (see Setup - `AGRUMY_TEST_PASSWORD`) works today, but if it ever doesn't: `pymysql.connect(host='invent.hr', user='agrumy', password=<see CLAUDE.md>, database='agrumyapi')` and query the `user` table directly - table/column names are PascalCase (`user`, `farmParcelZone`, `farmParcelZoneSatelliteScene`, not the C# model names verbatim) and `TenantID` on `farmParcelZone` tells you which tenant's users can see a given zone.
- **`GET /health` 404s** in this run (route may be gated or different) - use `GET /api/User/BootstrapPending` (Api) and `GET /Login` (Web) as up-checks instead; both are what `driver.py`'s `up`/`status` poll.
