#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
case "${TAXI_CONTAINER_ENGINE:-podman}" in
  podman)
    export PODMAN_COMPOSE_PROVIDER=podman-compose
    exec podman-compose --env-file "$ROOT/.env" -p taxi-workshop -f "$ROOT/deploy/compose.yaml" "$@"
    ;;
  docker)
    exec docker compose --env-file "$ROOT/.env" -p taxi-workshop -f "$ROOT/deploy/compose.yaml" "$@"
    ;;
  *) echo 'Set TAXI_CONTAINER_ENGINE to podman or docker.' >&2; exit 1 ;;
esac
