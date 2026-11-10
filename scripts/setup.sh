#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ ! -e .env ]]; then
  python3 - <<'PYSETUP'
from pathlib import Path
import secrets,os
values={'JWT_KEY':secrets.token_hex(32),'DB_PASSWORD':secrets.token_hex(20),'BROKER_USER':'taxi','BROKER_PASSWORD':secrets.token_hex(20),'DEMO_PASSWORD':'TaxiDemo!2026','APP_PORT':'8080'}
fd=os.open('.env',os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
with os.fdopen(fd,'w') as f:
    f.writelines(f'{k}={v}\n' for k,v in values.items())
PYSETUP
  echo 'Created local .env with generated signing, database and broker secrets.'
fi
case "${TAXI_CONTAINER_ENGINE:-podman}" in
  podman)
    command -v podman >/dev/null || { echo 'Install Podman first.' >&2; exit 1; }
    command -v podman-compose >/dev/null || { echo 'Install podman-compose first.' >&2; exit 1; }
    ;;
  docker)
    command -v docker >/dev/null || { echo 'Install Docker first.' >&2; exit 1; }
    docker compose version >/dev/null || { echo 'Install the Docker Compose plugin first.' >&2; exit 1; }
    ;;
  *) echo 'Set TAXI_CONTAINER_ENGINE to podman or docker.' >&2; exit 1 ;;
esac
echo 'Ready. Start with ./scripts/up.sh'
