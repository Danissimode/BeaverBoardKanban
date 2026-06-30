# Changelog

All notable changes to Beaver Board will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Phase 6 — Token Economy: `TokenBudgetService` with per-role token budgets, context size estimation, and budget indicator in Execution tab
- Phase 7 — IDE/API Bridge: v1 API endpoints (`/api/v1/ide/projects`, plan import, evidence attachment, execution start, chat messages)
- Phase 8 — GitHub templates: bug report and feature request issue templates, SECURITY.md, CONTRIBUTING.md, CODE_OF_CONDUCT.md
- Phase 8 — Release workflow: GitHub Actions release pipeline for v1 tags
- Phase 1 — `TicketExecutionPersistenceService`: auto-saves ticket execution metadata on run completion; metadata survives app restarts
- Phase 1 — `ExecutionMetadata` added to `AgentRun` and persisted via `RunLogStore`
- Phase 0 — `scripts/audit-public-repo.sh`: local public-repo safety audit
- Phase 0 — CI step for public repo audit script

### Changed
- Runner retry UI in AgentRunDrawer: runner-specific buttons (OpenCode / Claude) instead of single retry
- Home page runner selection: "Use OpenCode" / "Use as default" buttons with active badge
- ProjectSettings Runners tab: radio-button default selector, health diagnostics, per-runner retry
- `petpals-*` model profile names → `default-*` (public repo safety)

### Fixed
- OpenCodeRunner: pre-existing `run.SteeringQueue` → `agentRun.SteeringQueue` typo (build error)

## [0.9.0] — 2025-06-XX

### Added
- Beaver Board Kanban — first named release as Beaver Board (fork of KittyClaw)
- OpenCode Runner: CLI mode + HTTP/SSE server mode
- Done Gate: enforced via `CheckWorktreeHasChangesAsync`; blocks Done if no git changes
- Security Banner: dismissable local-only warning on every page
- Team Chat Run Notifier: posts run start/complete/fail/stop events to team chat
- Execution Tab in card drawer: runner, status, worktree, Start/Stop/Steer buttons
- AgentRunDrawer: live SSE event stream, steering, retry
- Automation Engine: trigger/condition/action pipeline with `TicketExecutionMetadataStore`
- Claude Runner: full streaming, stop, steer, max_turns, ask_user_question
- Dark theme: CSS vars with system/light/dark toggle
