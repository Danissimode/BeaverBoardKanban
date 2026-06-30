# Workspace: Demo Board

This is a sample Beaver Board workspace used to demonstrate and explore the application's features.

## Purpose

This board is for learning and experimentation. Feel free to create, modify, and delete tickets and agents here.

## Agents

- **Architect** (`architect`) — Uses Claude Sonnet 4 for planning and design. Assigned the `planner` role with a 200K token budget.

## Automations

- **Notify on Done** — Posts to team chat whenever a ticket is moved to the Done column.

## Conventions

- Ticket IDs follow the pattern `DEMO-NNN`
- Agents use the `planner` role (see `RoleBudgetConfig`)
- Automations are defined in `.agents/automations.json`

## Next steps

1. Create your own project workspace and start tracking real work
2. Add more agents with different roles (builder, reviewer, qa)
3. Explore automations — e.g. auto-assign on certain labels, escalate on blockers
4. Try the IDE/API bridge: `POST /api/v1/ide/projects/demo/execution/start`
