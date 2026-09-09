#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_dir="$(cd -- "$script_dir/.." && pwd)"
compose_project="${E2E_COMPOSE_PROJECT:-nuclear-evaluation-e2e}"

cleanup() {
  docker compose --project-name "$compose_project" --profile e2e down --volumes --remove-orphans
}

trap cleanup EXIT INT TERM

cd "$repo_dir"
mkdir -p tests/e2e/test-results tests/e2e/playwright-report
export E2E_UID="$(id -u)"
export E2E_GID="$(id -g)"
export NUCLEAR_APP_PORT="${E2E_HOST_APP_PORT:-0}"
export NUCLEAR_DB_PORT="${E2E_HOST_DB_PORT:-0}"

docker compose \
  --project-name "$compose_project" \
  --profile e2e \
  up \
  --build \
  --abort-on-container-exit \
  --exit-code-from e2e \
  e2e
