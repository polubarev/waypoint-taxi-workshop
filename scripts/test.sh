#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
case "${1:-all}" in
  auth) ./scripts/dotnet.sh test services/auth/Auth.Tests/Auth.Tests.csproj ;;
  gis) ./scripts/dotnet.sh test services/gis/Gis.Tests/Gis.Tests.csproj ;;
  booking) ./scripts/dotnet.sh test services/booking/Booking.Tests/Booking.Tests.csproj ;;
  contracts) ./scripts/dotnet.sh test tests/contract/Contract.Tests.csproj ;;
  api) python3 tests/e2e/api_flow.py ;;
  browser) npm run test:e2e ;;
  web) npm run test:web ;;
  e2e) python3 tests/e2e/api_flow.py && npm run test:e2e ;;
  acceptance) ./scripts/test.sh contracts && ./scripts/test.sh e2e ;;
  all) ./scripts/dotnet.sh test TaxiWorkshop.sln && npm run test:web ;;
  *) echo 'Usage: ./scripts/test.sh [all|auth|gis|booking|contracts|api|browser|web|e2e|acceptance]' >&2; exit 2 ;;
esac
