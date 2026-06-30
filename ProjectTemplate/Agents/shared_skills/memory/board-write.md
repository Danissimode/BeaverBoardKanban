---
title: "board-write — how to modify the Beaver Board"
role: all-agents
lastUpdated: 2026-06-30
---

## Key patterns

- Always read before writing (check current state to avoid redundant updates)
- `author` field is required on every write call
- Move ticket only if not already in target column
- Use `board-read` to verify state after writing
- Strict scope: never fix unrelated bugs — create a new ticket instead
