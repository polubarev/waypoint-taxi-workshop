#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "${1:-}" != "--yes" ]]; then echo 'This deletes only taxi-workshop volumes and reseeds this demo. Run ./scripts/reset.sh --yes'; exit 1; fi
./scripts/compose.sh down -v
./scripts/up.sh
