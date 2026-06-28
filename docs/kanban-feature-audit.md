# Kanban Feature Audit

_Generated: 2026-06-28_

## What Exists (Implemented)

### Core Kanban
- Drag-and-drop columns (board.js)
- Status transition confirmation (Ready→InProgress requires plan)
- Sub-tickets with parent/child + progress tracking
- Advanced search: #id, @owner, >date, priority:*, label:*, by:*
- Column management: create, reorder, colors
- Label management: CRUD
- Member management: CRUD
- Image upload in descriptions/comments
- Markdown with @mention, #id, cross-project refs
- Sort: name, created, updated
- Pause/resume project

### Ticket System
- Full CRUD
- Priority: Idea, NiceToHave, Required, Critical
- AssignedTo, ParentId, Labels
- Comments + Activity timeline
- Execution overrides: provider, model, agent, worktree, forbiddenPaths
- Plan workflow: PlanStatus, PlanBody, RequiresPlan, PlanApprovedBy
- Evidence tracking: RequiredEvidence, EvidenceCompleted
- RiskLevel, Reviewer fields

### Automation Engine
- 8 triggers: interval, ticketInColumn, statusChange, subTicketStatus, ticketCommentAdded, gitCommit, boardIdle, agentInactivity
- 10 conditions: ticketInColumn, fieldLength, priority, labels, assignedTo, hasParent, allSubTicketsInStatus, ticketAge, ticketCountInColumn, minDescriptionLength
- 9 actions: runAgent, moveTicketStatus, setLabels, assignTicket, addComment, consolidateAgentMemory, commitAgentMemory, executePowerShell, createTicket
- Post-run chain: runAgent → consolidateMemory → commitMemory

### Agent Runtimes (11)
- ClaudeCodeRuntime
- MimoCodeRuntime
- OpenCodeRuntime
- CodexRuntime
- GitHubCopilotRuntime
- AntigravityRuntime
- VibeRuntime
- KimiCodeRuntime
- ScriptRuntime
- ProcessRunner (base)
- AgentRuntimeRouter (dispatch)

### OpenCode Integration
- Zone A: IAgentRunner, RunnerRegistry, ITicketExecutionMetadataStore
- Zone B: OpenCodeRunner, WorktreeService, OpenCodeConfig
- Provider/model catalog
- Execution policy service
- Worktree per card

### Dashboard
- 15 tile types: markdown, table, kpi, kpi-grid, progress, sparkline, bar-chart, donut, gauge, status-grid, heatmap, leaderboard, timeline, image, mermaid
- Free-drag layout
- Auto-refresh via scripts
- AI chat-based tile creation
- Folder-based tile storage (.dashboard/)

### Team Chat (Beaver Board)
- Fixed bottom-right dock
- Filters: All, Needs Human, Failures, Mentions, Blockers
- Target picker: Team, Role (10 roles), Agent, Ticket
- Mention parser: @team, @role, @agent, #ticket, run:id
- Signal filter: important vs noisy events
- Agent profiles + role policies
- SQLite persistence
- 28 passing tests

### Other
- SSE real-time board updates
- Agent run drawer (live output, steer, stop)
- Claude chat drawer
- Automations page (list, enable/disable, edit)
- Project settings
- Onboarding popup
- Localization (EN, FR)
- Image upload
- Cross-project ticket references
- Git repository watcher

## What's Missing (Gaps)

### P0 Critical
| Gap | Impact | Effort |
|-----|--------|--------|
| FailureLogStore in-memory | Data loss on restart | Small |
| No Done/Review gate enforcement | Cards move to Done without review | Medium |
| No idempotency guard on auto-run | Duplicate runs possible | Small |

### P1 High
| Gap | Impact | Effort |
|-----|--------|--------|
| No ticket templates | Each ticket starts from scratch | Medium |
| No batch operations | Can't select multiple tickets | Medium |
| No ticket dependencies (blocked-by) | Only parent/child, no cross-ticket deps | Large |
| No time tracking | No estimation/tracking per ticket | Large |
| No SLA/breach detection | No time-based alerts | Large |
| No recurring tickets | createTicket exists but no recurring trigger | Small |
| No ticket cloning | Must recreate manually | Small |

### P2 Medium
| Gap | Impact | Effort |
|-----|--------|--------|
| No keyboard shortcuts | Power users slowed down | Medium |
| No ticket templates from existing | Can't save ticket as template | Small |
| No custom fields | Only predefined fields | Large |
| No ticket linking (related/follows/blocks) | Only parent/child | Large |
| No notification system | No email/webhook notifications | Large |
| No export/import | No CSV/JSON export | Medium |
| No ticket archiving | Done tickets stay forever | Small |
| No bulk status change | One at a time only | Small |
| No swimlanes | No assignee/label grouping | Large |
| No WIP limits | No per-column capacity limits | Medium |

### P3 Low
| Gap | Impact | Effort |
|-----|--------|--------|
| No Gantt/timeline view | No timeline visualization | Large |
| No burndown chart | No sprint progress view | Large |
| No velocity tracking | No team metrics | Large |
| No custom workflows | Fixed column set | Large |
| No ticket numbering customization | Sequential only | Small |
| No rich text in descriptions | Markdown only | Small |
| No image cropping/resizing | Upload only | Medium |
| No dark/light theme toggle | Dark only | Small |
| No mobile responsive | Desktop only | Large |
| No offline mode | Requires server | Large |

## Growth Zones

### 1. Orchestration Center (Next Big Feature)
Unified page for role routing, provider config, agent profiles, failure logbook, team chat settings.

### 2. Done/Review Gates
Enforce review + evidence before Done. Critical for quality.

### 3. Ticket Dependencies
blocked-by, relates-to, follows. Enables complex workflows.

### 4. Time Tracking + SLA
Estimates, tracking, breach alerts. Enterprise feature.

### 5. Notification System
Email, webhook, Slack integration. Keeps team informed.

### 6. Custom Fields
User-defined fields per project. Maximum flexibility.

### 7. Swimlanes + WIP Limits
Visual grouping + capacity management. Lean/kanban best practice.

### 8. Mobile + Offline
Responsive UI + service worker. Field accessibility.

### 9. Metrics + Reporting
Burndown, velocity, cycle time. Data-driven decisions.

### 10. Plugin System
Extensible triggers, conditions, actions. Community ecosystem.
