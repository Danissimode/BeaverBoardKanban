# Feature Status Matrix

This document is the source of truth for what is actually implemented vs. what is planned/stub/future. Updated after each release.

Last updated: 2026-06-30-5

## Status Legend

| Status | Meaning |
|--------|---------|
| **Done** | Fully implemented, user-facing, tested |
| **Partial** | Implemented but incomplete UX or known gaps |
| **Stub** | Interface/class exists but no functional implementation |
| **Future** | Described in docs but no code yet |
| **N/A** | Not applicable |

---

## Core Board

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| Kanban board (columns, cards, drag-drop) | **Done** | ✅ | Full CRUD, reorder, custom columns |
| Labels | **Done** | ✅ | Color-coded, multi-select |
| Members / agents | **Done** | ✅ | With avatar and status |
| Comments | **Done** | ✅ | @mentions, #ticket refs |
| Sub-tickets | **Done** | ✅ | parentId, hierarchy |
| Priority | **Done** | ✅ | Idea → Critical |
| Blocker / approval flags | **Done** | ✅ | Blocking tickets |
| Card drawer | **Done** | ✅ | Tabs: Details, Evidence, Chat, Execution |
| Dashboard tiles | **Partial** | ✅ | markdown, kpi, table, progress, charts, heatmap, timeline, mermaid; AI chat tile creation works; refresh via LLM prompt |
| Advanced search | **Partial** | ✅ | Full-text across tickets; UI complete |
| Image upload | **Done** | ✅ | Via chat description, stored in data dir |

---

## Automation Engine

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| Triggers: interval | **Done** | ✅ | |
| Triggers: ticketInColumn | **Done** | ✅ | |
| Triggers: statusChange | **Done** | ✅ | |
| Triggers: gitCommit | **Done** | ✅ | |
| Triggers: boardIdle | **Done** | ✅ | |
| Triggers: agentInactivity | **Done** | ✅ | |
| Conditions (all types) | **Done** | ✅ | ticketLabel, ticketPriority, columnCardCount, timeRange, random, ... |
| Actions: runAgent | **Done** | ✅ | |
| Actions: moveTicketStatus | **Done** | ✅ | |
| Actions: addComment | **Done** | ✅ | |
| Actions: consolidateAgentMemory | **Done** | ✅ | |
| Actions: commitAgentMemory | **Done** | ✅ | |
| Actions: executePowerShell | **Done** | ✅ | **Off by default** (security) |
| Actions: createTicket | **Done** | ✅ | |
| Automation editor UI | **Partial** | ✅ | TriggerEditor, ConditionEditor, ActionEditor all exist |
| In-memory chain guard (prevents duplicate runs) | **Partial** | ⚠️ | Not serialized to disk; lost on app restart |

---

## Agent Runners

| Runner | Status | User-facing | Notes |
|--------|--------|-------------|-------|
| ClaudeRunner (claude CLI) | **Done** | ✅ | Full streaming, stop, steer, max_turns, ask_user_question |
| OpenCodeRunner | **Partial** | ✅ | CLI mode + HTTP/SSE server mode (POST /api/runs + SSE stream); steering via temp file; prompt via `--prompt-file`; context-{runId}.md; respects explicit runner override |
| Chat v2 runner override | **Done** | ✅ | `req.Runner` field now respected by v2; falls back to default if unavailable |
| RunnerRegistry | **Done** | ✅ | Abstraction over runners; default selection |
| RunnerAvailabilityChecker | **Done** | ✅ | Detects which runners are installed |
| ClaudeCodeRuntime | **Done** | ⚠️ | Registered but internal |
| MimoCodeRuntime | **Stub** | ❌ | Commented out in Program.cs; no real implementation |
| ScriptRuntime | **Stub** | ❌ | Commented out in Program.cs; no real implementation |
| CodexRuntime | **Stub** | ❌ | Commented out in Program.cs; no real implementation |
| GitHubCopilotRuntime | **Stub** | ❌ | Commented out in Program.cs; no real implementation |
| AntigravityRuntime | **Stub** | ❌ | Commented out in Program.cs; no real implementation |
| VibeRuntime | **Stub** | ❌ | Commented out in Program.cs; no real implementation |
| KimiCodeRuntime | **Stub** | ❌ | Commented out in Program.cs; no real implementation |

---

## Execution / OpenCode Integration

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| Execution Tab in ticket drawer | **Partial** | ✅ | Shows runner, status, worktree; buttons (Start/Stop/Steer) present |
| Worktree per card | **Partial** | ✅ | WorktreeService exists; branch per ticket; cleanup not automatic |
| Provider/model catalog | **Done** | ✅ | OpenCodeProviderModelCatalog with OpenAI, Anthropic, OpenRouter, Ollama, Mistral, Gemini, DeepSeek |
| Execution metadata storage | **Partial** | ⚠️ | Stored in AgentRun; SQLite persistence not wired |
| Steering (Claude) | **Done** | ✅ | Via temp file; ResumeSteerMessages pattern works |
| Steering (OpenCode CLI) | **Partial** | ⚠️ | Steering via temp file (`.agents/tmp/steer-{runId}.txt`); requires OpenCode to poll this file |
| Done Gate | **Done** | ✅ | Enforced via CheckWorktreeHasChangesAsync; git diff --stat blocks Done if no changes |
| CAO Governance | **Future** | ❌ | CaoGoverned execution mode in docs; no runner implementation |
| TeamWorkflow (multi-agent) | **Future** | ❌ | Documented in OpenCode-Integration.md; no implementation |
| Manual execution mode | **Done** | ✅ | Available in execution mode selector |
| Quota/cost tracking | **Done** | ✅ | CostTracker persists to RunLogStore |
| Failure logbook | **Done** | ✅ | FailureLogStore backed by SQLite |

---

## Team Chat

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| Shared command chat (TeamChatDock) | **Done** | ✅ | Dockable panel; agent attribution |
| Agent ↔ agent messaging | **Done** | ✅ | Via @mentions and steering bridge |
| TeamCommandRouter | **Done** | ✅ | Routes commands to agents |
| AgentChatPolicyService | **Partial** | ⚠️ | Registered; policy enforcement not fully wired |
| Persistent chat across runs | **Done** | ✅ | ChatService backed by SQLite |
| TeamChatRunNotifier | **Done** | ✅ | Subscribes to AgentRunRegistry events; posts ai-activity messages (start/complete/fail/stop) to team chat |
| /chat/start v1 deprecated | **Done** | ✅ | v1 proxies to v2; returns X-Chat-Deprecation header |

---

## Security

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| CORS: LocalOnly whitelist | **Done** | ✅ | localhost + 127.0.0.1 only; AllowAll policy removed |
| executePowerShell off-by-default | **Done** | ✅ | `EnabledByDefault: false` in ActionSpec |
| Security banner (local-only warning) | **Done** | ✅ | `SecurityBanner.razor`; dismissable; persisted in settings |
| Public repo safety guide | **Done** | ✅ | `docs/public-repo-safety.md` |
| Health endpoint | **Done** | ✅ | `GET /api/health` — paths redacted; returns `{writable, pathKind}` |
| Secrets scanning | **N/A** | — | No CI configured yet |
| Dashboard script execution | **Partial** | ⚠️ | DashboardScriptRunner exists; no policy gate |

---

## Data / Storage

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| SQLite per-project DB | **Done** | ✅ | |
| %APPDATA%/BeaverBoard/ | **Done** | ✅ | Default path as of v0.9 |
| %APPDATA%/KittyClaw/ fallback | **Done** | ✅ | KITTYCLAW_DATA_DIR env var |
| Agent memory in workspace | **Done** | ✅ | `<workspace>/.agents/` |
| Run snapshots (JSON) | **Done** | ✅ | runs/{runId}.json |
| Onboarding detection | **Done** | ✅ | Claude CLI + Git detection on first launch |

---

## UI / UX

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| Blazor Server (SSR) | **Done** | ✅ | Interactive components |
| Dark theme | **Done** | ✅ | CSS vars, system/light/dark |
| Drag-and-drop | **Done** | ✅ | |
| Live run drawer (AgentRunDrawer) | **Done** | ✅ | SSE streaming events |
| RunnerStatusBar | **Done** | ✅ | Shows running agents |
| Toast notifications | **Done** | ✅ | |
| Escape key stack | **Done** | ✅ | |
| Reconnect modal | **Done** | ✅ | |
| Onboarding popup | **Done** | ✅ | |
| Project creation with workspace picker | **Done** | ✅ | |
| Settings: language, theme, preferred runner | **Done** | ✅ | |
| OpenCode project settings | **Partial** | ✅ | Provider, model, agent, auth status shown |
| CI pass/fail badge (from update check) | **Done** | ✅ | UpdateBanner shows latest release |
| Board filter / sort state | **Done** | ✅ | Per-session, persisted in component state |

---

## API / OpenAPI

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| Full REST API | **Done** | ✅ | All CRUD for projects, tickets, columns, labels, members, chats |
| OpenAPI JSON spec | **Done** | ✅ | `/openapi/v1.json` |
| Markdown API docs | **Done** | ✅ | `/api/docs` |
| SSE board events | **Done** | ✅ | `/api/projects/{slug}/events` |
| Auth: `author` required on mutating endpoints | **Done** | ✅ | HTTP 400 if missing |
| Per-project automation API | **Done** | ✅ | |

---

## Docs

| Document | Status | Notes |
|----------|--------|-------|
| README.md | **Done** | Clear value prop; honest about OpenCode status |
| README.OpenCode.md (root) | **Partial** | Duplicate of docs/OpenCode-Integration.md — candidate for removal |
| doc/index.md | **Done** | Architecture map |
| doc/agent-dispatch.md | **Done** | Claude-centric; accurate |
| doc/automation-engine.md | **Done** | Covers triggers, conditions, actions |
| doc/dashboard.md | **Done** | Tile types, creation flow |
| doc/kanban-ui.md | **Partial** | Needs updates post UI redesign |
| doc/storage.md | **Done** | Updated with BeaverBoard path; mentions BEAVERBOARD_DATA_DIR |
| doc/project-template.md | **Done** | Accurate |
| doc/worktree-workflow.md | **Done** | |
| doc/rest-api.md | **Done** | Accurate |
| doc/update-check.md | **Done** | |
| doc/ui-ux-master-plan-v2.md | **Future** | Historical, superceded |
| doc/graphic-charter.md | **Done** | Branding guide |
| docs/OpenCode-Integration.md | **Done** | 758 lines; comprehensive but some future sections |
| docs/public-repo-safety.md | **Done** | |
| docs/Runner-API.md | **Partial** | |
| docs/integrations/mcp-security-boundary.md | **Future** | MCP docs |

---

## Token Economy

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| TokenBudgetService | **Done** | ✅ | Context size estimation, per-role budgets, fallback model suggestion |
| Role budgets (planner/builder/reviewer/qa/docs) | **Done** | ✅ | Defined in `RoleBudgetConfig`; visible in Execution tab |
| Budget indicator in Execution tab | **Done** | ✅ | Shows role, token estimate, context level, fallback model |
| Broadcast fanout warning | **Done** | ⚠️ | Infrastructure ready; multi-target chat UI not yet implemented |
| CostTracker integration (daily cap) | **Done** | ✅ | Already existed; integrated with TokenBudgetService |

---

## IDE / API Bridge

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| API v1 base endpoints | **Done** | ✅ | All CRUD endpoints under `/api/` |
| `/api/v1/ide/projects` | **Done** | ✅ | List accessible projects |
| `/api/v1/ide/projects/{slug}/board` | **Done** | ✅ | Full board state (columns, tickets, labels, members) |
| Plan import `/api/v1/ide/.../plans/import` | **Done** | ✅ | Structured plan → tickets with optional agent assignment |
| Evidence attachment `/api/v1/ide/.../evidence` | **Done** | ✅ | Posts evidence as comment with summary/files/checks/risks |
| Execution start `/api/v1/ide/.../execution/start` | **Done** | ✅ | Triggers agent run via RunnerRegistry |
| Chat from IDE `/api/v1/ide/.../chat/messages` | **Done** | ✅ | Posts message to team chat |
| API token auth (SHA256, scopes: read/write/execute/admin) | **Done** | ✅ | `ApiTokenService`; tokens managed via settings.json |
| Local API token generation endpoint | **Done** | ✅ | `POST /api/v1/ide/.../api-token/generate` (admin scope) |
| Example IDE config | **Future** | ❌ | Not yet included |

---

## Infrastructure

| Feature | Status | Notes |
|---------|--------|-------|
| .NET 10 build | **Done** | `dotnet build` passes, 0 errors |
| Tests: core automation | **Partial** | Some tests exist; timeout issues in CI |
| Tests: OpenCode runner | **Partial** | OpenCodeRunnerTests.cs exists |
| Release: run.sh / run.bat | **Done** | dotnet watch wrapper |
| tools/publish-stable.ps1 | **Partial** | Exists; not tested end-to-end |
| GitHub Actions CI | **Done** | build + test + public audit + Gitleaks secrets scan |
| Public repo audit script | **Done** | `scripts/audit-public-repo.sh` |
| GitHub release workflow | **Done** | `.github/workflows/release.yml` |
| PR template | **Done** | `.github/pull_request_template.md` |
| Issue templates | **Done** | bug_report.yml, feature_request.yml |
| SECURITY.md | **Done** | |
| CONTRIBUTING.md | **Done** | |
| CODE_OF_CONDUCT.md | **Done** | |
| CHANGELOG.md | **Done** | |

---

## Agent Skills Pack

| Feature | Status | User-facing | Notes |
|---------|--------|-------------|-------|
| `board-read` skill | **Done** | ⚠️ | SKILL.md + memory index; injected via preamble |
| `board-write` skill | **Done** | ⚠️ | SKILL.md + memory index; referenced in programmer/qa/committer |
| `team-chat` skill | **Done** | ⚠️ | SKILL.md + memory index |
| `evidence` skill | **Done** | ⚠️ | SKILL.md + memory index; referenced in qa-tester |
| `done-gate` skill | **Done** | ⚠️ | SKILL.md + memory index; referenced in programmer/qa-tester |
| Shared skills discovery | **Done** | ✅ | Preamble references all 5 skills; per-agent SKILL.md includes table |

### Skill contents

- **`board-read`** — full curl reference for all GET endpoints (tickets, columns, labels, members, comments, evidence, runs, chat, mentions), filtering patterns, error handling
- **`board-write`** — full curl reference for all write endpoints (move, create, update, comment, labels, sub-tickets), conditional-update patterns, strict-scope rules
- **`team-chat`** — read/write messages, @mentions, ticket linking, broadcast, slash commands, run event patterns, coordination guidelines
- **`evidence`** — attach test results/build logs/diffs/screenshots; `checks`/`risks` field patterns; Done Gate integration
- **`done-gate`** — two-gate check (evidence exists + git has changes); script template; common failure table; bypass rules

All skills embedded as `KittyClaw.Core.AgentsTemplate/shared_skills/*/SKILL.md` — auto-included via existing `**\SKILL.md` glob in the csproj.

---

## Known Issues

1. ~~**Board._liveProgress warning**~~ — **Fixed** (removed unused field)
2. **OpenCode CLI steering** — temp file approach works but OpenCode must actively poll the file; no SSE/websocket path confirmed
3. **Automation chain guard** — in-memory only; duplicate runs possible after app restart
4. **docs/ vs doc/** — two doc directories; `docs/` is KittyClaw-style, `doc/` is BeaverBoard-style; creates confusion
5. ~~**README.OpenCode.md**~~ — **Fixed** (merged into docs/OpenCode-Integration.md or removed)
6. **7 stub runtimes commented out** — Mimo, Script, Codex, GitHubCopilot, Antigravity, Vibe, Kimi — kept as commented candidates in Program.cs; not registered in DI
7. **TicketExecutionMetadataStore.GetAsync** — path resolution is simplified; may not find metadata for projects with non-standard workspace layouts
8. **Multi-target broadcast chat** — fanout warning infrastructure exists but multi-target UI not implemented yet

---

## Next Milestone: v1.0 (MVP Complete)

See `doc/roadmap-v1.md` for the full v1.0 plan.
