# Specialist workflow

## Scope and decisions

- Read the repository's development guide and the relevant implementation before changing it. Derive requirements from the user's request and agreed decisions.
- Use specialist roles when their expertise or independent verification helps the task. Choose only the roles needed for the work.
- For a multi-step implementation, maintain one visible `update_todo_list` checklist with short, stable item labels and owners. Orchestrator opens it with one item per phase (plan, implement, verify, review). After reading the code, Architect replaces the implement item with owned work items and leaves the other phases in place. Keep one item in progress. Once an item is complete, preserve its exact text and completed state; add a separate repair item when needed. Put detailed evidence in the handoff. Carry decisions and the review-pass count forward across modes.
- A planning-only or explanation-only request ends with the answer. Mode auto-approval does not authorize implementation. If the user has requested implementation, routine role handoffs need no additional confirmation.
- Record reversible assumptions the user delegates and carry them through implementation and review. Bundle unresolved decisions that materially block progress into one question and continue independent work. Keep optional improvements in the backlog.

## Role handoffs

- Use `switch_mode` for authorized work outside the current role. A prose suggestion to select another role does not complete a handoff. Do not edit through a terminal or MCP to bypass `fileRegex`.
- Use one conversation and sequential role switches for this workflow.
- Architect defines acceptance criteria, impact, compatibility, owners and unresolved decisions. Backend owns service logic, shared contracts and deployment changes within its assigned scope. GIS owns geospatial implementation and its user-facing integration. Test engineer gathers verification evidence. Reviewer evaluates the result against the agreed scope.
- Route by the remaining work and its dependencies. An ordinary first pass is Architect → implementing owner(s) → Test engineer → Reviewer. Skip unaffected owners. After a failed test or review, return only to its owner, then verification, then review. Do not restart Architect or repeat unrelated roles.
- Concrete transitions use `switch_mode` with the destination `mode_id`: Orchestrator → `architect`; Architect → `backend-developer` (or `gis-specialist` for GIS-only work); Backend → `gis-specialist` if GIS work remains, otherwise `test-engineer`; GIS → another unfinished implementing owner, otherwise `test-engineer`; Test engineer → the defect owner on failure, otherwise `reviewer`; Reviewer → the blocker owner on CHANGES REQUIRED. A repaired change always returns through Test engineer to Reviewer. APPROVE ends the chain. Do not replace any of these calls with a request for manual role selection.
- Before a switch, give a handoff of at most 200 words: acceptance items completed/remaining; changed files; commands with actual results; unresolved blockers/decisions; next owner and its exact task; review pass (0, 1 or 2). Do not repeat the full plan, file contents or prior reviews.
- Keep context focused: search for symbols and read relevant ranges. Read independent files together when supported. Avoid recursive listings of generated dependencies and repeated full-repository exploration.
- If an edit fails or the tool output is inconclusive, reopen the current file and retry with a smaller edit. Read back critical edits and check the actual diff before declaring the stage complete or rebuilding. Use the edit tools for all source and test writes, even within your allowed paths; never use shell snippets to repair edits.

## Verification and runtime

- Implementers run their focused checks as they work. Run shared and integration checks after dependent owners have landed their changes. Do not repeat an unchanged full suite after every handoff; rerun checks affected by later changes and any previously failing checks.
- Run one documented command per terminal call and preserve its output and exit status. Use the repository's separate test and diagnostic commands when available. Record which checks actually ran when a combined command stopped early.
- Use the repository's documented configuration mechanism. Keep credential values out of prompts and output.
- The implementing owner prepares the runtime after the relevant changes are ready. Rebuild again only for changes that affect the running version. Use documented readiness checks or native process controls. Run tests that mutate shared state sequentially.
- Investigate unexpected fixture or environment state. Destructive resets require explicit authorization. Restore test fixtures to their original values and verify cleanup.
- For hosted pull requests, Reviewer uses the configured GitHub reader to retrieve the PR, diff and source at its head commit. For local work, the last executing owner generates the documented review snapshot or supplies the diff and status. Reviewer checks relevant source, including untracked changes for local work. Refresh the evidence after further edits and keep unrelated or sensitive material out of it.

## Review and completion

- Review pass 1 evaluates the agreed acceptance criteria and the complete proposed change. Give each blocker a stable ID, evidence, an owner, a concrete suggested fix and a concrete verification step. Optional findings also carry a suggested fix. Separate optional improvements and unapproved scope changes. Existing behavior outside the requested outcome does not automatically become another task.
- For access changes, identify the requested actors, operations and resource boundaries before editing. Preserve existing grants and require explicit scope for additional access. Verify the enforcement points and cover both allowed and denied behavior.
- Address real correctness, authorization, compatibility and regression problems. Do not dismiss a blocker to meet a time limit, and do not weaken a test to pass.
- CHANGES REQUIRED routes only the outstanding blocker IDs to their owners. After repairs, rerun affected tests and update the snapshot. Review pass 2 checks those fixes and affected behavior; it must still report a newly discovered serious regression.
- Maximum two review passes. After pass 2, unresolved blockers or unmade essential product decisions produce BLOCKED with the exact remaining work. Do not begin a third review or another full role tour.
- On APPROVE, close the visible checklist before writing the final verdict, preserving completed labels. Report verified outcomes and limits. Distinguish the reviewer's observations from checks run by earlier roles.

## Tools and boundaries

- Architect, Reviewer and Orchestrator use read, todo and mode-switch tools. Reviewer also has access to the repository's dedicated read-only GitHub tools. A mode switch grants the destination role's capabilities only after it occurs. An explanation-only request ends with the explanation.
- Implementing roles and Test engineer can use MCP. Resolve a Context7 library ID first, then query one documentation concept at a time. If it is unavailable, record that and use official sources; do not repeatedly retry an unavailable server.
- Test engineer activates `live-system-verification` once when preparing runtime acceptance. If activation fails, read `.bob/skills/live-system-verification/SKILL.md` directly and follow the guide within the role's permissions. Record that the file was read directly. If the file is unavailable too, use the repository's documented checks and report the missing guide.
- Use browser tools within the authorized environment and session. Apply the same approval and data-handling rules to browser interactions as to other state-changing actions.
- Tool groups control availability; task approvals control interruptions; command allowlists and MCP Always Allow select individual operations. None replaces the others. File regexes constrain file tools, not arbitrary shell commands or MCP code execution.
