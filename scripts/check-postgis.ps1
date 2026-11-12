$ErrorActionPreference = 'Stop'
$engine = if ($env:TAXI_CONTAINER_ENGINE) { $env:TAXI_CONTAINER_ENGINE } else { 'podman' }
if ($engine -notin @('docker', 'podman')) { throw 'Set TAXI_CONTAINER_ENGINE to podman or docker.' }
$compose = @()
if ($engine -eq 'docker') { $compose += 'compose' }
$command = if ($engine -eq 'docker') { 'docker' } else { 'podman-compose' }
$root = Split-Path -Parent $PSScriptRoot
$arguments = @('--env-file', "$root/.env", '-p', 'taxi-workshop', '-f', "$root/deploy/compose.yaml", 'exec', '-T', 'gis-db', 'psql', '-U', 'taxi', '-d', 'gis', '-v', 'ON_ERROR_STOP=1', '-c')
& $command @compose @arguments "LOAD 'postgis-3'"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $command @compose @arguments 'SELECT 1 / (ST_Distance(ST_SetSRID(ST_MakePoint(0, 0), 4326)::geography, ST_SetSRID(ST_MakePoint(0, 1), 4326)::geography) > 100000)::int AS postgis_works'
exit $LASTEXITCODE
