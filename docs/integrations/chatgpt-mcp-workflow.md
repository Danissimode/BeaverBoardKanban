# ChatGPT MCP Workflow

How ChatGPT uses the Beaver Board MCP Control Plane to manage projects.

## Roles

- **ChatGPT** — architect, planner, reviewer
- **Beaver Board** — control plane, evidence hub
- **Agents** — executors (OpenCode, Claude, etc.)
- **Human** — owner, final approver

## Typical Workflow

### 1. Planning

ChatGPT creates a ticket with a plan:

```
beaver.ticket.create({
  projectSlug: "petpals",
  title: "Fix auth session restore",
  description: "Session restore fails after reload"
})

beaver.ticket.set_plan({
  projectSlug: "petpals",
  ticketId: 42,
  planBody: "1. Fix session restore flow\n2. Add regression test\n3. Request review"
})

beaver.ticket.set_execution({
  projectSlug: "petpals",
  ticketId: 42,
  executionMode: "DirectOpenCode",
  provider: "openrouter",
  model: "qwen/qwen3.5-coder",
  agent: "build"
})
```

### 2. Execution

ChatGPT moves ticket to trigger auto-run:

```
beaver.ticket.move_status({
  projectSlug: "petpals",
  ticketId: 42,
  fromStatus: "Ready",
  toStatus: "InProgress"
})
```

Beaver Board auto-runs the ticket with configured execution.

### 3. Monitoring

ChatGPT checks run status:

```
beaver.runs.latest({
  projectSlug: "petpals",
  ticketId: 42
})
```

### 4. Team Communication

ChatGPT posts to team chat:

```
beaver.team_chat.post({
  projectSlug: "petpals",
  body: "@reviewer please review KC-42 auth fix",
  targetType: "role",
  targetId: "reviewer"
})
```

### 5. Completion

After agent completes, ChatGPT requests review:

```
beaver.ticket.move_status({
  projectSlug: "petpals",
  ticketId: 42,
  fromStatus: "InProgress",
  toStatus: "Review"
})
```

### 6. Verification

ChatGPT marks verified (not Done — owner only):

```
beaver.ticket.move_status({
  projectSlug: "petpals",
  ticketId: 42,
  fromStatus: "Review",
  toStatus: "Verified"
})
```

## Error Handling

If a run fails:

```
beaver.failures.list({
  projectSlug: "petpals",
  ticketId: 42
})

beaver.team_chat.post({
  projectSlug: "petpals",
  body: "@team KC-42 run failed. Checking failures.",
  targetType: "team"
})
```

## Needs Human

If agent asks a question:

```
beaver.team_chat.inbox({
  projectSlug: "petpals"
})
# Returns: Needs Human items

beaver.team_chat.reply({
  projectSlug: "petpals",
  messageId: "msg_123",
  body: "No public API changes. Internal fix only."
})
```
