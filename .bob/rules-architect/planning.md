# Planning practices adapted from Bob Plan

Use the useful structure of the installed Plan/create-plan prompts within this mode's read-only capabilities. These instructions do not activate that skill or grant its file-writing, subagent or mandatory approval steps.

1. Establish the goal, observable acceptance criteria, constraints and non-goals from the user's request and existing decisions. Ask a bundled question only for missing information that materially blocks progress. Carry explicitly delegated assumptions forward.
2. Research the actual code: locate relevant files, symbols, callers, existing utilities and patterns. Reuse existing abstractions where appropriate. Trace service boundaries, authorization, wire contracts, data migrations and affected consumers. Search first, then read focused ranges; do independent reads together.
3. Choose the simplest approach that meets the outcome. Explain important trade-offs and compatibility decisions with source evidence. Check that the plan fits the request; do not repeatedly ask the user to confirm already-authorized implementation.
4. Describe ordered work items in the conversation. Each item needs an intent, observable outcome, relevant file/symbol references, owner, dependencies and verification step. Track status using the one shared `update_todo_list` checklist: when Orchestrator opened it with phase items, replace the implement item with these owned work items and keep the plan, verify and review items.
5. Validate coverage: every acceptance criterion has an implementing owner and a way to verify it, and no deployment/configuration or cross-service test task is left ownerless. Include a Mermaid diagram only if it clarifies the design.
6. For an implementation request, give the short handoff and call `switch_mode` with `mode_id: "backend-developer"` by default, or `"gis-specialist"` for GIS-only work. Continue in that role. For a planning-only request, deliver the plan and stop.

Keep the plan in this conversation. Never create a plan file, run a command, spawn another task, or suggest that the user manually select the next role as a substitute for an authorized mode switch.
