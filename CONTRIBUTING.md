# Contributing to Beaver Board

Thank you for your interest in contributing to Beaver Board!

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Git](https://git-scm.com/)
- For full agent features: [OpenCode CLI](https://github.com/sao-image搜索/opencode) or [Claude CLI](https://docs.anthropic.com/en/docs/claude-code)

### Running locally

```bash
git clone https://github.com/Danissimode/BeaverBoardKanban.git
cd BeaverBoardKanban
dotnet restore
dotnet build
dotnet watch --project KittyClaw.Web
# → http://localhost:5230
```

### Running tests

```bash
dotnet test --filter "FullyQualifiedName~Automation"     # automation tests
dotnet test --filter "FullyQualifiedName~OpenCode"       # OpenCode runner tests
```

## Development Workflow

### Branch naming
- `feat/your-feature-name` — new features
- `fix/your-fix-name` — bug fixes
- `docs/your-docs-name` — documentation
- `chore/your-task-name` — maintenance, refactoring

### Before submitting a PR

1. `dotnet build` passes with 0 errors
2. `dotnet test --filter "<relevant area>"` passes
3. `scripts/audit-public-repo.sh` passes (no secrets, no private paths)
4. `doc/status-matrix.md` updated if you changed any feature status
5. PR checklist completed (see `.github/pull_request_template.md`)

### Code style

- C#: follow the existing conventions in the codebase (no formatter config committed yet — clean, readable code is the standard)
- Blazor/Razor: keep components focused; move complex logic to code-behind or service classes
- Commit messages: use imperative mood ("add feature" not "added feature")

## Architecture Overview

- `KittyClaw.Core/` — domain models, services, automation engine, runners
- `KittyClaw.Web/` — Blazor Server UI, REST API endpoints
- `KittyClaw.Core.Tests/` — xUnit tests
- `KittyClaw.QaRunner/` — isolated Playwright test runner
- `doc/` — architecture documentation
- `docs/` — user-facing documentation and integration guides
- `scripts/` — repo maintenance scripts
- `branding/` — logo, icons, brand assets

## Key Concepts

- **Runner** — abstraction over an agent runtime (OpenCode CLI, Claude CLI, etc.)
- **AgentRun** — a single execution of an agent with SSE event streaming
- **TicketExecutionMetadata** — persisted execution record per ticket (provider, model, worktree, etc.)
- **Automation** — trigger + conditions + actions pipeline
- **TeamChat** — shared chat with @mention routing to agents

## Areas to Contribute

Looking for ideas? Check the [open issues](https://github.com/Danissimode/BeaverBoardKanban/issues) labeled `good-first-issue` or `help-wanted`.

Current roadmap priorities (see `doc/roadmap-v1.md`):
- Phase 5: Agent Skills Pack (board-read, board-write, team-chat, evidence, done-gate skills)
- Phase 7: IDE/API Bridge improvements
- Phase 9: Release automation (doctor CLI, demo board)

## License

By contributing, you agree that your contributions will be licensed under the same license as Beaver Board.
