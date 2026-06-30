---
title: "board-read — how to read the Beaver Board API"
role: all-agents
lastUpdated: 2026-06-30
---

## Key patterns

- Always define `api="${KITTYCLAW_API_URL}"` before making calls
- Filter with query params (`?columnId=Backlog`, `?assignee=programmer`) instead of fetching all
- Always check HTTP status codes — `curl -s` hides errors
- Fetch column list before moving tickets (names must match exactly)
