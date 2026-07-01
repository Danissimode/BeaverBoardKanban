# Release Readiness Matrix

> Canonical status of Beaver Board features. Updated for v0.1.0-preview.

## Implemented (works today)

| Feature | Status | Notes |
|---------|--------|-------|
| Kanban board | ✅ | Drag-drop, columns, tickets, labels, members, sub-tickets |
| Dashboard | ✅ | 15 tile types, free-drag, auto-refresh, AI chat creation |
| Automation engine | ✅ | Triggers, conditions, actions, in-flight chain guard |
| Agent run engine | ✅ | ClaudeRunner with streaming, stop, steer, SSE, quota fallback |
| Agent run persistence | ✅ | RunLogStore (JSON per run), registry survives restarts |
| Session registry | ✅ | Per-agent session IDs, resume support |
| Team chat | ✅ | @mention routing, signal filtering, run notifier |
| Evidence attachment | ✅ | Ticket-level evidence field, required evidence checklist |
| Done/Review gate | ✅ | Blocks Done without reviewer + riskLevel; enforces evidence |
| Token budget | ✅ | Role budgets, indicator in Execution tab |
| Sub-tickets | ✅ | Parent/child, progress tracking, API support |
| Image upload | ✅ | In descriptions, comments, chat |
| REST API | ✅ | OpenAPI + auto-generated Markdown docs, bearer token auth |
| macOS packaging | ✅ | Self-contained .app, DMG, launcher with lock/port/pid |
| Public baseline audit | ✅ | `scripts/audit/public-baseline.sh` — private path scanner |
| Upstream intake docs | ✅ | `docs/upstream-kittyclaw.md` with ADOPT/ADAPT/REWRITE/SKIP/BLOCK |
| Platform storage paths | ✅ | macOS `~/Library/...`, Linux XDG, Windows `%APPDATA%` |
| Runtime single-instance | ✅ | Lock file + port.json + backend.pid |
| Legacy migration | ✅ | Copies data from `.kittyclaw`, `TodoApp`, `KittyClaw` paths |

## Partially implemented (works, but incomplete or rough edges)

| Feature | Status | Notes |
|---------|--------|-------|
| OpenCode runner | 🟡 | CLI + SSE server mode exist; primary execution path still defaults to Claude |
| Provider/model routing | 🟡 | `AgentOrchestrationResolver` resolves runtime/role/model from ticket config; UI override is basic |
| IDE/API bridge | 🟡 | v1 endpoints (`/api/v1/ide/`) exist; docs and client examples are minimal |
| Homebrew Cask | 🟡 | Draft exists (`homebrew/Casks/beaver-board.rb`); not tested end-to-end |
| macOS signed/notarized | 🟡 | Unsigned preview only; Gatekeeper requires manual approval |
| Failure log persistence | 🟡 | `FailureLogStore` uses in-memory `ConcurrentDictionary`; survives restart only if logs are in `runs/` JSON |
| Auto-run idempotency | 🟡 | `TicketAutoRunService` has basic gate but no strict deduplication key across restarts |

## Experimental (may change or break)

| Feature | Status | Notes |
|---------|--------|-------|
| Execution slot resolver | 🔬 | `ExecutionResolver` with programmer/builder/reviewer slots; not wired to UI |
| Agent memory consolidation | 🔬 | `consolidateAgentMemory` action exists; quality varies by agent |
| AI chat tile creation | 🔬 | Agent writes `tile.yaml` + script from chat prompt; needs manual review |
| OpenCode SSE server | 🔬 | `OpenCodeRunner` server mode; not the default path |

## Planned (not yet started)

| Feature | Status | Notes |
|---------|--------|-------|
| Intel x64 macOS build | 📋 | Apple Silicon only for 0.1.x |
| Universal macOS binary | 📋 | `osx-universal` dotnet publish target |
| GitHub Pages landing | 📋 | README is sufficient for 0.1.x |
| Multi-provider marketplace UI | 📋 | Provider catalog UI; currently config-driven |
| Linux `.deb`/`.rpm` packaging | 📋 | No native Linux packaging yet |
| Windows `.msi` packaging | 📋 | No native Windows packaging yet |
| CLI formula (brew install) | 📋 | Cask only for 0.1.x; CLI later |

## Internal compatibility only (not user-facing)

| Feature | Status | Notes |
|---------|--------|-------|
| KittyClaw namespace | 🔒 | Preserved for upstream compatibility; all user-facing strings are Beaver Board |
| KittyClaw.ClaudeMock | 🔒 | Test-only mock binary; not shipped to end users |
| KittyClaw.QaRunner | 🔒 | Test harness; not shipped to end users |
| `KittyClaw.slnx` / `.csproj` names | 🔒 | Project file names; product identity is Beaver Board |

## Release blockers for v1.0.0

These must be resolved before a stable public release:

- [ ] FailureLogStore → persistent SQLite (not in-memory)
- [ ] TicketAutoRunService strict idempotency guard
- [ ] Done/Review gate enforcement verified with tests
- [ ] macOS release pipeline (GitHub Actions → DMG → Release asset)
- [ ] Homebrew tap tested end-to-end (`brew install --cask beaver-board`)
- [ ] Provider/model routing documented and UI-polished
- [ ] First-user onboarding flow verified (create project → assign agent → run)
- [ ] Demo board showcase quality

## Current version

**v0.1.0-preview** — packaging-ready, feature-complete for preview, not yet stable release.
