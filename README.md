# Lethe Server C# Port

A C# / ASP.NET Core port of the Lethe (Limbus Company private) game server.

## Quickstart

The fastest way — **Docker** (no .NET SDK needed). This builds the server and starts it
alongside a Postgres database:

```bash
docker compose up --build
```

On Windows you can just double-click **`run.bat`**.

That's it. The server is at **http://localhost:8080** and migrates the database on startup.
Verify it's up:

```bash
curl http://localhost:8080/health   # -> ok
```

Stop it with `Ctrl+C` (add `-d` to run in the background). Data persists in a Docker volume
between runs.

> Prefer running from source (hot reload, no containers)? See
> [Running the server](#running-the-server) below.

## Running the server

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download) (`dotnet --version` ≥ 9.0)
- PostgreSQL — for real gameplay (account/dungeon state lives in Postgres). The quickest way:

  ```bash
  docker run --name openlethe-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=openlethe -p 5432:5432 -d postgres
  ```

### 1. Configure the database connection

The server reads the connection string from configuration key `ConnectionStrings:Postgres`.

**Recommended: a `.env` file.** On startup the server loads a `.env` (searching up from the
working directory) into the environment. Copy the template and edit it:

```bash
cp .env.example .env
```

```dotenv
# .env
ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=openlethe;Username=postgres;Password=postgres
```

`.env` is gitignored. Real environment variables take precedence over it, so you can still
override any value from the shell:

```bash
# bash / Git Bash
export ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=openlethe;Username=postgres;Password=postgres"
```

```powershell
# PowerShell
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5432;Database=openlethe;Username=postgres;Password=postgres"
```

(`__` in an env var maps to the `:` in the config key.) On startup the server runs EF Core
migrations automatically, so an empty database is created and schema-migrated for you — no
manual migration step needed.

> Running **without** a connection string is supported: the server boots and serves
> DB-free routes (e.g. `GET /health`), but any account/dungeon endpoint will fail. This is
> mainly useful for a quick smoke test.

### 2. Run

```bash
dotnet run --project src/OpenLethe.Server
```

The server listens on **http://localhost:5055** (HTTPS on https://localhost:7293). This comes
from the launch profile in `Properties/launchSettings.json`. To bind a different host/port set
`ASPNETCORE_URLS` (e.g. `ASPNETCORE_URLS=http://0.0.0.0:8080`) and run without a launch profile:

```bash
ASPNETCORE_URLS=http://0.0.0.0:8080 dotnet run --project src/OpenLethe.Server --no-launch-profile
```

> **Launch-profile precedence:** `dotnet run` (with the default profile) sets `ASPNETCORE_URLS`
> and `ASPNETCORE_ENVIRONMENT=Development` itself, and those win over `.env`. So `ASPNETCORE_URLS`
> / `ASPNETCORE_ENVIRONMENT` placed in `.env` only take effect for the published DLL or when you
> pass `--no-launch-profile`. `ConnectionStrings__Postgres` and other keys are unaffected — they
> load from `.env` in all cases.

### 3. Verify

```bash
curl http://localhost:5055/health   # -> ok
```

### Configuration reference

| Key | Env var | Default | Purpose |
| --- | --- | --- | --- |
| `ConnectionStrings:Postgres` | `ConnectionStrings__Postgres` | *(none)* | Postgres connection string; migrations run on startup when set |
| `Auth:JwtSecret` | `Auth__JwtSecret` | ephemeral random (per boot) | HS256 signing secret; leave unset for localhost (tokens just don't survive a restart) |
| — | `ASPNETCORE_URLS` | `http://localhost:5055` (launch profile) | Host/port to bind. `.env` value applies only without a launch profile (see note above) |
| — | `ASPNETCORE_ENVIRONMENT` | `Development` (launch profile) | `Development` or `Production`. `.env` value applies only without a launch profile |

#### Optional: Discord login and skill translation

Neither group is needed to play - the game routes and the `/auth/login` dev login work
without them. Leave them unset and the features stay dormant.

| Env var | Default | Purpose |
| --- | --- | --- |
| `CLIENT_ID`, `CLIENT_SECRET` | *(none)* | Discord OAuth app credentials; required by `/auth/discord` and `/auth/authorized` |
| `FRONTEND_URL` | *(none)* | Where the non-launcher flow redirects back to; required by every `/auth` route |
| `REDIRECT_URL` | `http://localhost:8080/auth/authorized` | OAuth callback registered with Discord. Must match the portal entry exactly and point at the port you actually reach the server on (8080 via Docker, 5055 under `dotnet run`) |
| `AUTH_URL`, `TOKEN_URL` | Discord's endpoints | Override to point at a different OAuth provider |
| `DISCORD_GUILD_ID` | *(none)* | Set to require membership of that server to log in. **Unset means no whitelist** - upstream always gates on a guild |
| `DISCORD_TOKEN` | *(none)* | Bot token used for the membership check and avatar lookup |
| `DISCORD_WHITELIST_IDS` | *(none)* | Comma-separated user snowflakes that bypass the guild check (upstream hard-codes these in source) |
| `CAPTCHA_SECRET_KEY` | *(none)* | Cloudflare Turnstile secret for `/auth/captcha`, which issues the cookie `/misc/locale` requires |
| `MOD_FILES_DIR` | `modfiles` | Directory served at the server root as `/Lethe.dll`, `/ModularSkillScripts.dll`, `/motions.dll`, `/limbus-manifest.txt`, `/noticeMeta.json` — what LetheLauncher downloads before launching. Docker mounts `./modfiles` here |
| `RELEASE_CHANNEL_ID` | *(none)* | Discord channel to source `Lethe.dll` from when there is no local file (needs `DISCORD_TOKEN`) |
| `MODULAR_RELEASE_CHANNEL_ID` | *(none)* | Same, for `ModularSkillScripts.dll` |
| `MOTIONS_RELEASE_CHANNEL_ID` | falls back to `RELEASE_CHANNEL_ID` | Same, for `motions.dll` — set it to publish motions separately |
| `LIMBUS_MANIFEST_URL` | `https://files.lethelc.site/limbus-manifest.txt` | Where `/limbus-manifest.txt` redirects when no local file is present |
| `OPENAI_API_KEY` | *(none)* | Required by `/misc/locale*`; any OpenAI-compatible chat-completions endpoint |
| `OPENAI_BASE_URL` | `https://api.openai.com/v1` | Point at a proxy or a local model server |
| `OPENAI_MODEL` | `gpt-3.5-turbo` | Model used for skill-text generation |

All keys can go in `.env` (loaded on startup); real environment variables override `.env`.

## Tests

Tests use [Testcontainers](https://testcontainers.com/) to spin up a throwaway Postgres, so
**Docker must be running** for the database-backed tests to execute (they are skipped, not
failed, if Docker is unavailable).

```bash
dotnet test
```
