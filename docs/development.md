# Development and checks

## Toolchain

.NET 8 SDK, Node 22+, Python 3, and either Podman with podman-compose or Docker with the Compose plugin. `global.json` selects the .NET 8.0.4xx feature band or a newer .NET 8 feature band. `scripts/dotnet.sh` uses a project-local SDK in `.tools/dotnet` when installed, otherwise the host SDK; CLI state and NuGet packages stay in `.tools`.

Use Podman with podman-compose by default, or select Docker with `export TAXI_CONTAINER_ENGINE=docker`. Use the same selection for every stack command. Each engine has separate containers and volumes, so stop the previous stack before switching; data is not migrated. The implementing owner starts or rebuilds the demo with `./scripts/up.sh`, which runs `./scripts/wait-ready.sh`. The readiness check can also be run on its own. A reset with `./scripts/reset.sh --yes` deletes local demo volumes and requires explicit authorization.

Install .NET 8 using Microsoft's official installer or SDK packages. For a project-local installation, pass `--channel 8.0 --install-dir "$PWD/.tools/dotnet" --no-path` to the official dotnet-install script.

```sh
./scripts/dotnet.sh restore TaxiWorkshop.sln
npm ci
npm run vendor
PLAYWRIGHT_BROWSERS_PATH="$PWD/.tools/browsers" npx playwright install chromium
```

Lockfiles pin NuGet and npm dependencies. Container builds use the same projects and frontend dependency lockfile. Base image tags are demo defaults; record image digests for a repeatable event and rebuild before publishing a new baseline.

## Test commands

| Scope | Command |
| --- | --- |
| Auth unit tests | `./scripts/test.sh auth` |
| GIS unit tests | `./scripts/test.sh gis` |
| Booking unit tests | `./scripts/test.sh booking` |
| Shared contracts + all service tests + frontend logic | `./scripts/test.sh all` |
| Consumer contracts | `./scripts/test.sh contracts` |
| Real API acceptance only | `./scripts/test.sh api` |
| Browser acceptance only | `./scripts/test.sh browser` |
| Frontend logic only | `./scripts/test.sh web` |
| Contract + real API + browser acceptance | `./scripts/test.sh acceptance` |
| Real API and browser e2e | `./scripts/test.sh e2e` |

Run the affected service's focused checks first. Shared contract changes require `all`. Service boundary changes require `contracts`. Behavior changes require `e2e` against the running local stack.

Unit suites run without containers and should finish in under a minute per service. The real API suite talks to the gateway, all services, PostgreSQL/PostGIS and RabbitMQ. Playwright covers desktop and mobile. End-to-end tests create trips and temporarily change manager roles, restoring them in `finally`; use this isolated demo rather than a shared live environment.

`e2e` stops if API acceptance fails. Use the `api` and `browser` scopes independently to collect both results during diagnosis. The API runner and Playwright configuration load `.env` internally; do not source it into Bob's terminal commands or print it. Run mutating acceptance checks sequentially.

The contract suite uses strict provider response/request fixtures at the HTTP-client boundary. The API acceptance suite checks actual provider serialization and behavior. It is not a Pact broker deployment.

For bounded workshop diagnostics, use `./scripts/bob-check.sh build`, `syntax`, `status` or `logs auth` (also `booking`, `gis` or `gateway`). Each is a standalone command that preserves its result. Test engineer may use `syntax`, `status` and `logs`; builds and stack rebuilds remain the implementing owner's responsibility.

For a bug fix, first add a failing regression and capture its output, implement the fix, then rerun the same test plus affected checks. Never weaken tests to pass.

## Service solutions

The root `TaxiWorkshop.sln` builds everything. Each service also has a solution containing its API, domain and tests plus required shared projects. The reporting utility reads an exported event JSON file:

```sh
./scripts/dotnet.sh run --project jobs/reporting/Reporting.csproj -- event.json
```

## Data and configuration

Database schema and idempotent seed data are in each API project's `schema.sql` and seed method. This is an initial baseline schema, not a full migration framework. Preserve data with additive migration scripts when implementing exercises. Changing `CREATE TABLE IF NOT EXISTS` alone does not migrate an existing volume.

`.env` is generated with mode 0600 and ignored. Do not paste it into prompts, logs, or issues. `deploy/compose.yaml` maps it into hierarchical .NET configuration. Service URLs, broker URI, exchange, queue prefix and consumer suffix come from configuration.

Rejected, cancelled and completed bookings are terminal. Assignment requires approval. Cancellation is allowed while pending, approved or assigned. Completion removes the trip from the live layer using the existing `BookingCancelled` removal message; the audit and booking state record `Completed`. This preserves the baseline's two trip-event types.

The map shows approved/assigned trip lines, not simulated GPS vehicle positions. Distance is a straight-line EPSG:2039 metric estimate, not a road-routing distance. Saved places are frontend seed data.

GeoJSON uses WGS84 with longitude first. Verify changes to distance, coordinate order or projections against the running PostGIS service.

The shared live-trip layer requires Dispatcher authorization in GIS. Other users can read zones and validate routes, and employees list their own bookings through Booking.

## Bob modes

`.bob/custom_modes.yaml` defines five specialist modes plus the coordinating Orchestrator. Architect and Reviewer have read, todo and mode-switch tools. Reviewer also has dedicated read-only GitHub MCP tools. Test engineer's edit tool is limited to test directories. Its execute tool is governed by project instructions and terminal approval: edit `fileRegex` is not an operating-system sandbox for shell commands or MCP tools. Use a restricted execution environment when hard command enforcement is required.

The shared workflow in `.bob/rules/workflow.md` uses one conversation, automatic mode switches and at most two review passes. Orchestrator opens the checklist with one item per phase; Architect adds the owned work items after reading the code. An approval ends the task; unresolved blockers after the second pass end in an explicit blocked result. Planning-only requests never authorize implementation. Ordinary feature work can use the built-in Plan and Agent modes; the custom sequence is for the specialist demo.

Before a read-only review, an executing owner runs `./scripts/bob-review.sh`. It writes the base revision, status and tracked diff under `artifacts/review/`. Reviewer must also open relevant untracked source listed in the status file. Refresh the snapshot after subsequent edits. The snapshot is written to an ignored directory.

See [Bob workshop setup](bob-workshop.md) for project tools, local checks and hosted review.

Official configuration references: [custom modes](https://bob.ibm.com/docs/ide/configuration/custom-modes), [project rules](https://bob.ibm.com/docs/ide/configuration/rules). Validate mode loading in the installed IDE before the live session; model/IDE-specific behavior is not established by static config validation.
