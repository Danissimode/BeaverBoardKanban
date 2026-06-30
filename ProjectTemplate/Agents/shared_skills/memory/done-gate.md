---
title: "done-gate — blocking Done until evidence + git changes exist"
role: all-agents
lastUpdated: 2026-06-30
---

## Key patterns

- Run Done Gate before every `InProgress → Done` move
- Both gates must pass: (1) evidence attached, (2) git has changes
- If blocked: post comment and stay in Review, do not bypass
- Owner can manually bypass the gate — agents cannot
