---
name: live-system-verification
description: Verify behavior against a running system, diagnose failed checks, and prepare repeatable acceptance evidence for review. Use in Test engineer for changes that affect runtime behavior.
groups:
  - test-engineer
---

# Live system verification

1. Check the handoff for the scope, running version and checks that already passed. Use the repository's documented verification and diagnostic commands. If the running version is stale, ask the implementing owner to refresh it. Test-only edits generally need no application rebuild.
2. Run the smallest check that reproduces a failure. Use bounded readiness checks and service logs to correlate symptoms with their cause. Preserve the actual command output and exit status.
3. For access changes, cover permitted and denied operations, actors and resource boundaries. Where relevant, test how changed grants affect existing sessions. Record the original state of fixtures before changing them, restore it during cleanup and verify the restoration.
4. Run independent checks separately while diagnosing. Run the documented integrated checks against the final version when required. Reuse valid fixtures and avoid duplicate runs against unchanged behavior. Report unavailable checks explicitly.
5. For visible changes, inspect the affected surface with available browser tools in an authorized environment. Use the automated suite for authenticated coverage if the browser lacks an authorized session. Keep credentials out of tool arguments. Report browser tool observations separately from automated test results.
6. Refresh review evidence after final edits. Hand off the actual results, evidence gaps and any reproduction. Switch to the implementing owner for a fix or to Reviewer when the required checks pass.
