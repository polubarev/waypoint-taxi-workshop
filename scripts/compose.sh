#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
# Explicit provider: never fall back to another engine or compose executable.
export PODMAN_COMPOSE_PROVIDER=podman-compose
exec podman-compose --env-file "$ROOT/.env" -p taxi-workshop -f "$ROOT/deploy/compose.yaml" "$@"
