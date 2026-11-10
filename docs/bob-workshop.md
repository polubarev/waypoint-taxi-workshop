# Bob workshop setup

Open this repository as a workspace in Bob IDE. The application and tests run independently of Bob. See [development and checks](development.md) for the toolchain and test commands.

## Project setup

- For a normal implementation exercise, use Plan or Agent as directed in the task brief. For the specialist workflow, start a fresh task in Orchestrator. The repository supplies six modes, project rules, and the live verification skill.
- Review the permissions for your task before you start. The example in `.bob/workshop/approval.example.json` is a reference. Bob loads workspace modes and MCP settings from the `.bob` directory.
- Context7 provides documentation lookup. For browser inspection, install the pinned MCP tool with `npm ci --prefix .bob/workshop`. The browser MCP launcher uses Node 22+ and an installed Google Chrome. The Playwright test suite uses its own Chromium installation.
- Use a fresh Bob task after changing mode or MCP settings so the task sees the current configuration.

## Code review

For a hosted exercise, give Reviewer the workshop pull request link. The optional GitHub reader requires GitHub CLI signed in to GitHub.com and access to the participant repo. Its tools read the PR, its diff, and text files at the PR head. The reader is configured for this repository in `.bob/mcp.json`.

For a local exercise, compare `main` with the review branch as shown in the [README](../README.md) and provide the diff to Reviewer. When reviewing your own uncommitted changes, run `./scripts/bob-review.sh` from the repository root and point Reviewer to `artifacts/review/`.
