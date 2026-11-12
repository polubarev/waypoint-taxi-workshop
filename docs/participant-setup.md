# Participant setup

Complete this setup before the workshop. You need internet access for installers, the repository, package and container image downloads, and Bob model access. Background map tiles also need internet. Ask the organizer for the repository URL, access credentials, workshop brief, and any Bob account or license requirements.

The demo runs locally. You do not need to install PostgreSQL, PostGIS, RabbitMQ, or a .NET runtime separately: the containers supply them. Running the demo requires Git, Python, a Bash shell, and one container engine. For the implementation and testing exercises, also install the .NET SDK, Node.js, and Bob below.

## 1. Prepare your terminal and repository

- **Windows:** Install [WSL 2 and Ubuntu](https://learn.microsoft.com/en-us/windows/wsl/install). In an administrator PowerShell, run `wsl --install`, restart if prompted, open Ubuntu, and create its Linux user. Run the remaining shell commands in the Ubuntu terminal. Install Linux versions of the command-line tools inside WSL; Windows installations alone may not be available there.
- **macOS:** Use Terminal. Install Apple's command line tools when prompted by `git --version`.
- **Linux:** Use a Bash terminal and your distribution's package manager.

Install [Git](https://git-scm.com/install/) and [Python 3](https://www.python.org/downloads/) for your environment. On Ubuntu/WSL, for example:

```sh
sudo apt-get update
sudo apt-get install -y git python3
```

Clone the repository using the URL supplied by the organizer, then enter it. On WSL, a checkout in your Linux home directory is recommended. If access requires authentication, follow your organization's Git hosting instructions.

```sh
git clone <repository-url>
cd taxi_bob_workshop
git --version
python3 --version
```

Use the actual directory name if your clone is named differently. Commands in the rest of this guide run from the repository root. The scripts require LF line endings; see [development and checks](development.md) if an existing Windows checkout has CRLF scripts.

## 2. Install one container engine

Choose **Podman** (the project default) or **Docker**. Start the engine before running the workshop. Virtualization may need to be enabled and organizational installation or license policies may apply.

### Podman

Follow the [Podman installation instructions](https://podman.io/docs/installation) for your platform and install the separate [podman-compose](https://github.com/containers/podman-compose#installation) command in the same environment as the workshop shell. On macOS, create and start a Podman machine with `podman machine init` and `podman machine start`. On Windows, make Podman and podman-compose available in your WSL distribution; follow Podman's Windows guidance for its machine or use a Linux installation in WSL.

```sh
podman --version
podman-compose --version
podman info
```

### Docker

Install and start [Docker Desktop](https://docs.docker.com/get-started/get-docker/) or install [Docker Engine and the Compose plugin](https://docs.docker.com/engine/install/) on Linux. Docker Desktop includes Compose. On Windows, use its WSL 2 engine and enable integration for your Ubuntu distribution as described in the [Docker WSL guide](https://docs.docker.com/desktop/features/wsl/).

In each new workshop shell, select Docker before running any stack commands:

```sh
export TAXI_CONTAINER_ENGINE=docker
docker --version
docker compose version
docker info
```

Keep the same engine selection for starting, stopping, and inspecting the stack. Docker and Podman have separate containers and volumes.

## 3. Install development and workshop tools

- **.NET 8 SDK:** Install the SDK, not just the runtime, using the [.NET 8 download](https://dotnet.microsoft.com/download/dotnet/8.0) or [Linux installation instructions](https://learn.microsoft.com/en-us/dotnet/core/install/linux). The repository selects SDK 8.0.400 or a newer .NET 8 feature band. In WSL, install the Linux SDK inside the distribution. Verify with `dotnet --list-sdks`. The project also supports a local SDK; see [development and checks](development.md).
- **Node.js 22 or later and npm:** Install from [Node.js](https://nodejs.org/en/download) for your shell environment. Verify with `node --version` and `npm --version`.
- **Bob IDE:** Install and sign in using your organization's [Bob setup](https://bob.ibm.com/) and the organizer's instructions. Open this repository as a workspace. See [Bob workshop setup](bob-workshop.md) for the project modes and tools.
- **Browser tools:** The browser acceptance tests use Playwright's Chromium, installed below. Bob's optional browser inspection MCP also needs [Google Chrome](https://www.google.com/chrome/) and its separately pinned npm dependencies; see [Bob workshop setup](bob-workshop.md).
- **Hosted review only:** Install [GitHub CLI](https://cli.github.com/) and sign in with `gh auth login` if the organizer asks you to review a hosted GitHub pull request. Your account needs access to the participant repository.

Install the project dependencies and the browser used by the acceptance tests:

```sh
./scripts/dotnet.sh restore TaxiWorkshop.sln
npm ci
npm run vendor
PLAYWRIGHT_BROWSERS_PATH="$PWD/.tools/browsers" npx playwright install chromium
```

On Linux/WSL, Playwright may report missing system libraries; follow its instructions or the [Playwright Linux dependencies guide](https://playwright.dev/docs/intro#system-requirements). For Bob's optional browser MCP, also run `npm ci --prefix .bob/workshop`.

## 4. Check the demo

With your selected engine running, build and start the stack:

```sh
./scripts/up.sh
```

The first run downloads images and builds services. Once the readiness check succeeds, open [http://localhost:8080](http://localhost:8080) and sign in as `employee` with password `TaxiDemo!2026`. The first run creates a local `.env` with generated secrets; keep it private. The [README](../README.md) lists other demo accounts and stop commands. Contact the organizer if installation, engine access, or readiness fails.

For test commands, platform notes, and troubleshooting details, continue to [development and checks](development.md). The GIS image supports amd64; ARM machines need compatible x86-64 container emulation.
