#!/usr/bin/env bash
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
export DOTNET_CLI_HOME="$ROOT/.tools/cli-home"
export NUGET_PACKAGES="$ROOT/.tools/nuget"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
if [[ -x "$ROOT/.tools/dotnet/dotnet" ]]; then exec "$ROOT/.tools/dotnet/dotnet" "$@"; fi
exec dotnet "$@"
