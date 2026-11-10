# Waypoint Taxi Workshop

Waypoint is a local taxi booking application with synthetic demo data. Use it to practice cross-service changes, authorization, geospatial features, testing, and code review.

## Run on localhost with Podman

Prerequisites: Podman with a running machine on macOS/Windows, podman-compose, and Python 3. Containers include the application runtime; host .NET and Node are needed only for host-side development/tests.

```sh
./scripts/up.sh
```

Open [Waypoint](http://localhost:8080). Default demo password: `TaxiDemo!2026` (configurable in the generated, ignored `.env`). Usernames: `employee`, `manager`, `dispatcher`, `gisadmin`, `admin`, `employee-north`, `manager-north`.

The gateway binds to `127.0.0.1`; no cloud resources are needed. Each service owns a separate PostgreSQL container and volume, GIS has PostGIS, and RabbitMQ carries events. The databases and service ports are internal. The RabbitMQ management UI binds to [localhost:15672](http://localhost:15672); its generated credentials are in `.env`.

```sh
./scripts/down.sh                 # stop, preserve data
./scripts/up.sh                   # build/start, preserve data
./scripts/reset.sh --yes          # delete this demo's volumes, reseed
./scripts/compose.sh logs --tail=80
```

Use Podman exclusively. The scripts explicitly call `podman-compose`. Compose's `dockerfile` field and `docker.io` registry names are format/registry names, not another container engine.

Map libraries are served locally. Background tiles use OpenStreetMap and need internet. Zone geometry, route selection and polygon editing continue to work without background tiles. Initial package/image downloads and Bob model access also need network access.

## Develop and verify

See [development](docs/development.md), [architecture](docs/architecture.md), and [Bob setup](docs/bob-workshop.md).

```sh
npm ci
npm run vendor
./scripts/test.sh all
./scripts/test.sh contracts
./scripts/test.sh e2e             # requires running stack and Chromium
```

## Workshop exercises

Start implementation exercises on your own branch from `main`. Use the task brief supplied for your session.

For the code review exercise, stay on `main` and review the changes on `review/operations-reporting`. The exercise branch is for review practice and should stay separate from your local demo.

```sh
git diff main...origin/review/operations-reporting
```

You can also review the hosted pull request when your workshop provides a link.
