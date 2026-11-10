# Editing scope

Use edit tools for the test paths allowed by this mode. Run the documented `./scripts/test.sh` scopes, `./scripts/wait-ready.sh`, `./scripts/bob-review.sh`, and `./scripts/bob-check.sh` with `syntax`, `status` or `logs SERVICE`. Run one command per call and keep credentials out of output.

The execute group and optional browser MCP are for verification, not permission to edit implementation files through a shell or browser code. Route source fixes to their owner using `switch_mode`, retaining the failing test. A rebuild belongs to an implementing mode; a destructive reset needs explicit approval. Refresh the review snapshot after test edits, then switch to Reviewer when the required evidence is complete.

Activate `live-system-verification` for runtime acceptance. Save the original state of any fixture the tests change, restore it during cleanup and verify the restoration. Use the edit tools for test changes.

Use the standard terminal and MCP approval controls. FileRegex restricts file tools, not arbitrary shell execution or MCP tools; a restricted operating environment is needed for adversarial enforcement.
