# Demo Board — "Hello, Beaver Board!"

This is a minimal example project to get familiar with Beaver Board.

## What's included

- **4 sample tickets** across Backlog → Done
- **1 agent config** (`architect`) using Claude Sonnet 4
- **1 automation** — notifies team chat on Done
- **Evidence attached** to one ticket

## How to use

1. Open Beaver Board (`dotnet run --project KittyClaw.Web`)
2. Click **"New Project"** → name it `demo` → set workspace to `examples/demo-board`
3. You'll see the pre-loaded tickets and agent
4. Try assigning a ticket to the `architect` agent and starting a run

## Customizing

- Edit `tickets.json` to add/remove/rename tickets
- Edit `.agents/agents.json` to add agents with different models
- Edit `.agents/automations.json` to change automation triggers

## Understanding the structure

```
examples/demo-board/
├── tickets.json          # Board state (columns, tickets, labels)
├── .agents/
│   ├── agents.json      # Agent definitions (name, model, system prompt)
│   ├── automations.json # Automation triggers and actions
│   └── memory/          # Agent memory files
└── CLAUDE.md            # Workspace context for agents
```
