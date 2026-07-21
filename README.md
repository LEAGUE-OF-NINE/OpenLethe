# Lethe Server C# Port

A C# / ASP.NET Core port of the Lethe (Limbus Company private) game server.

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

All keys can go in `.env` (loaded on startup); real environment variables override `.env`.

## Tests

Tests use [Testcontainers](https://testcontainers.com/) to spin up a throwaway Postgres, so
**Docker must be running** for the database-backed tests to execute (they are skipped, not
failed, if Docker is unavailable).

```bash
dotnet test
```
