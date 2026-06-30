# Runner API Documentation

Complete reference for BeaverBoard Kanban Runner Management API.

## Base URL

```
http://localhost:5230/api
```

---

## Runners Endpoints

### List All Runners

Check availability of all registered runners.

```bash
curl http://localhost:5230/api/runners
```

**Response:**
```json
[
  {
    "runnerKind": "opencode",
    "displayName": "OpenCode",
    "isAvailable": true,
    "isDefault": true,
    "version": "2.1.4",
    "isRecommended": true
  },
  {
    "runnerKind": "claude",
    "displayName": "Claude (Legacy)",
    "isAvailable": true,
    "isDefault": false,
    "version": null,
    "isRecommended": false
  }
]
```

---

### Runner Health Check

Quick health check for all runners (simplified response).

```bash
curl http://localhost:5230/api/runners/health
```

**Response:**
```json
[
  {
    "kind": "opencode",
    "displayName": "OpenCode",
    "available": true,
    "version": "2.1.4",
    "error": null
  },
  {
    "kind": "claude",
    "displayName": "Claude (Legacy)",
    "available": true,
    "version": null,
    "error": null
  }
]
```

---

### Get Default Runner

Get the current default runner.

```bash
curl http://localhost:5230/api/runners/default
```

**Response:**
```json
{
  "kind": "opencode",
  "displayName": "OpenCode",
  "available": true
}
```

---

### Set Default Runner

Change the default runner.

```bash
curl -X POST http://localhost:5230/api/runners/default \
  -H "Content-Type: application/json" \
  -d '{"kind": "opencode"}'
```

**Response:**
```json
{
  "kind": "opencode",
  "displayName": "OpenCode"
}
```

---

### Get Runner Details

Get detailed information about a specific runner.

```bash
curl http://localhost:5230/api/runners/opencode
```

---

### Get Recommended Runner

Get the recommended runner for new projects (OpenCode when available).

```bash
curl http://localhost:5230/api/runners/recommended
```

---

## Global Run Endpoints

These endpoints work across all projects (no slug required).

### List All Active Runs

Get all active runs across all projects.

```bash
curl http://localhost:5230/api/runs
```

**Response:**
```json
{
  "count": 2,
  "runs": [
    {
      "runId": "a1b2c3d4",
      "projectSlug": "petpals",
      "agentName": "programmer",
      "ticketId": 42,
      "startedAt": "2024-01-15T10:30:00Z",
      "runnerKind": "opencode",
      "status": "Running"
    }
  ]
}
```

---

### Get Run By ID

Get detailed information about a specific run.

```bash
curl http://localhost:5230/api/runs/a1b2c3d4
```

**Response:**
```json
{
  "runId": "a1b2c3d4",
  "projectSlug": "petpals",
  "agentName": "programmer",
  "skillFile": "programmer/SKILL.md",
  "ticketId": 42,
  "concurrencyGroup": "ticket-42",
  "startedAt": "2024-01-15T10:30:00Z",
  "endedAt": null,
  "sessionId": "session-123",
  "exitCode": null,
  "runnerKind": "opencode",
  "status": "Running",
  "events": [...]
}
```

---

### SSE Stream (Global)

Subscribe to real-time events for a run.

```bash
curl -N http://localhost:5230/api/runs/a1b2c3d4/stream
```

**Event Format:**
```
data: {"at":"2024-01-15T10:31:00Z","kind":"stdout","text":"> Analyzing file..."}

data: {"at":"2024-01-15T10:31:05Z","kind":"assistant","text":"Found issue..."}

event: end
data: {}
```

---

### Stop Run (Global)

Stop an active run.

```bash
curl -X POST http://localhost:5230/api/runs/a1b2c3d4/stop \
  -H "Content-Type: application/json" \
  -d '{"reason": "User requested"}'
```

**Response:**
```json
{
  "runId": "a1b2c3d4",
  "status": "stopped",
  "reason": "User requested"
}
```

---

### Steer Run (Global)

Send a steering message to an active run.

```bash
curl -X POST http://localhost:5230/api/runs/a1b2c3d4/steer \
  -H "Content-Type: application/json" \
  -d '{"text": "Stop what you are doing and check the tests first."}'
```

**Response:**
```json
{
  "runId": "a1b2c3d4",
  "messageSent": true
}
```

---

## Project-Scoped Run Endpoints

These endpoints are scoped to a specific project.

### List Project Runs

```bash
curl http://localhost:5230/api/projects/petpals/runs
```

---

### Start Run for Ticket

Start a run for a specific ticket.

```bash
curl -X POST http://localhost:5230/api/projects/petpals/tickets/42/run \
  -H "Content-Type: application/json" \
  -d '{"author": "owner"}'
```

**Response:**
```json
{
  "runId": "new-run-id",
  "status": "Running",
  "runner": "opencode"
}
```

---

### Get Run Details (Project)

```bash
curl http://localhost:5230/api/projects/petpals/runs/a1b2c3d4
```

---

### SSE Stream (Project)

```bash
curl -N http://localhost:5230/api/projects/petpals/runs/a1b2c3d4/stream
```

---

### Stop Run (Project)

```bash
curl -X POST http://localhost:5230/api/projects/petpals/runs/a1b2c3d4/stop \
  -H "Content-Type: application/json" \
  -d '{"reason": "Taking too long"}'
```

---

### Steer Run (Project)

```bash
curl -X POST http://localhost:5230/api/projects/petpals/runs/a1b2c3d4/steer \
  -H "Content-Type: application/json" \
  -d '{"text": "Focus on the backend only."}'
```

---

### Retry Run (Project)

Retry a failed or completed run. By default, uses the same runner as the original run.

```bash
# Retry with same runner
curl -X POST http://localhost:5230/api/projects/petpals/runs/a1b2c3d4/retry

# Retry with specific runner
curl -X POST http://localhost:5230/api/projects/petpals/runs/a1b2c3d4/retry \
  -H "Content-Type: application/json" \
  -d '{"runnerKind": "opencode"}'
```

**Response:**
```json
{
  "runId": "new-run-id",
  "runner": "opencode",
  "previousRunner": "claude"
}
```

---

### Get Latest Run for Ticket

Get metadata about the most recent run for a ticket.

```bash
curl http://localhost:5230/api/projects/petpals/tickets/42/runs/latest
```

---

### Get Run History for Ticket

Get all run history for a ticket.

```bash
curl http://localhost:5230/api/projects/petpals/tickets/42/runs
```

---

### OpenCode Health Check (Project)

```bash
curl http://localhost:5230/api/projects/petpals/opencode/health
```

**Response:**
```json
{
  "available": true,
  "kind": "opencode"
}
```

---

## Chat Endpoints

### Start Chat (v2 - Multi-Runner)

Start a chat session using the runner registry (supports OpenCode).

```bash
curl -X POST http://localhost:5230/api/projects/petpals/chat/start-v2 \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Implement JWT refresh tokens",
    "target": "owner-chat",
    "forceNew": false
  }'
```

**Response:**
```json
{
  "runId": "chat-run-id",
  "runner": "opencode"
}
```

---

## Response Formats

### Run Status Values

| Status | Description |
|--------|-------------|
| `Running` | Run is actively executing |
| `Completed` | Run finished successfully (exit code 0) |
| `Failed` | Run finished with non-zero exit code |
| `Stopped` | Run was manually stopped |
| `Queued` | Waiting for an available slot |

### Event Kinds

| Kind | Description |
|------|-------------|
| `stdout` | Standard output from the agent process |
| `stderr` | Standard error output |
| `assistant` | Assistant message |
| `tool_use` | Tool usage |
| `steer-sent` | Steering message sent to agent |
| `error` | Error occurred |
| `reset` | Session reset |

---

## Error Responses

### 400 Bad Request

```json
{
  "error": "Run is not active."
}
```

### 404 Not Found

```json
{
  "error": "Run not found"
}
```

### Runner Not Available

```json
{
  "error": "No runner available."
}
```

---

## Usage Examples

### Full Workflow: Start, Monitor, Steer, Stop

```bash
# 1. Start a run for ticket #42
RUN_ID=$(curl -s -X POST http://localhost:5230/api/projects/petpals/tickets/42/run \
  -H "Content-Type: application/json" \
  -d '{"author": "owner"}' | jq -r '.runId')
echo "Started run: $RUN_ID"

# 2. Monitor with SSE
curl -N "http://localhost:5230/api/runs/$RUN_ID/stream" &

# 3. After some time, send steering message
curl -X POST "http://localhost:5230/api/runs/$RUN_ID/steer" \
  -H "Content-Type: application/json" \
  -d '{"text": "Good progress! Now write tests."}'

# 4. Check status
curl "http://localhost:5230/api/runs/$RUN_ID" | jq '.status'

# 5. If needed, stop
curl -X POST "http://localhost:5230/api/runs/$RUN_ID/stop" \
  -H "Content-Type: application/json" \
  -d '{"reason": "User cancelled"}'
```

### Retry Failed Run with Different Runner

```bash
# Get the previous run ID
PREV_RUN="a1b2c3d4"

# Retry with OpenCode instead of Claude
curl -X POST "http://localhost:5230/api/projects/petpals/runs/$PREV_RUN/retry" \
  -H "Content-Type: application/json" \
  -d '{"runnerKind": "opencode"}'
```

### Check Runner Availability Before Starting

```bash
# Get recommended runner
REC=$(curl -s http://localhost:5230/api/runners/recommended | jq -r '.runnerKind')
echo "Recommended: $REC"

# Check health of all runners
curl -s http://localhost:5230/api/runners/health | jq '.'
```
