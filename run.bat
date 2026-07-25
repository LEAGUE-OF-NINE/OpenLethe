@echo off
REM Build and start OpenLethe (server + Postgres) with Docker Compose.
where docker >nul 2>nul || (
  echo Docker not found. Install Docker Desktop: https://www.docker.com/products/docker-desktop/
  pause & exit /b 1
)
echo Starting OpenLethe... server will be at http://localhost:8080  (press Ctrl+C to stop)
docker compose up --build
pause
