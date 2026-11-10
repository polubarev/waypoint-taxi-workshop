#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
./scripts/setup.sh
"${TAXI_CONTAINER_ENGINE:-podman}" info >/dev/null
./scripts/compose.sh build
# Recreate the whole local stack in dependency order, preserving named volumes.
# Partial recreation can retain an old image when Podman container dependencies exist.
./scripts/compose.sh down
./scripts/compose.sh up -d --no-build
./scripts/wait-ready.sh
