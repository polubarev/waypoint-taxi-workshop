#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
SERVER="$ROOT/.bob/workshop/node_modules/@playwright/mcp/cli.js"
if [[ ! -f "$SERVER" ]]; then
  echo 'Install the pinned Bob tools first: npm ci --prefix .bob/workshop' >&2
  exit 1
fi
mkdir -p "$ROOT/artifacts/playwright-mcp"
# Use the installed Chrome with an isolated in-memory profile. No browser or
# package downloads occur when Bob starts this server during the workshop.
exec node "$SERVER" --browser chrome --isolated --headless \
  --output-dir "$ROOT/artifacts/playwright-mcp" "$@"
