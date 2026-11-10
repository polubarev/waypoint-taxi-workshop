#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

usage() {
  echo 'Usage: ./scripts/bob-check.sh build|syntax|status|logs auth|booking|gis|gateway' >&2
  exit 2
}

case "${1:-}" in
  build)
    [[ $# -eq 1 ]] || usage
    exec ./scripts/dotnet.sh build TaxiWorkshop.sln
    ;;
  syntax)
    [[ $# -eq 1 ]] || usage
    node --check web/app.js
    exec node --check web/ui-logic.mjs
    ;;
  status)
    [[ $# -eq 1 ]] || usage
    exec ./scripts/compose.sh ps
    ;;
  logs)
    [[ $# -eq 2 ]] || usage
    case "$2" in
      auth|booking|gis|gateway) exec ./scripts/compose.sh logs --tail=80 "$2" ;;
      *) usage ;;
    esac
    ;;
  *) usage ;;
esac
