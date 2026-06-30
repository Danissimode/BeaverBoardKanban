# board-read skill

Read-only access to the Beaver Board: tickets, columns, labels, members, comments, and evidence. Use this skill whenever you need to understand the current state of the project before acting.

## Prerequisites

Before reading, infer from your working directory:
- **Project slug** = the folder name that contains `.agents/` (your working directory)
- **API base** = `${KITTYCLAW_API_URL}` (injected by the orchestrator; never hardcode `http://localhost:5230`)

Define a convenience variable:
```bash
api="${KITTYCLAW_API_URL}"
```

## Endpoints

### List all tickets

```bash
curl -s "$api/api/projects/{project-slug}/tickets"
```

Returns all tickets with id, title, columnId, labels, assignee, priority, parentId.

### Filter tickets

```
GET /api/projects/{project-slug}/tickets?columnId={columnId}
GET /api/projects/{project-slug}/tickets?assignee={handle}
GET /api/projects/{project-slug}/tickets?priority=Critical
GET /api/projects/{project-slug}/tickets?parentId={ticket-id}   # sub-tickets
GET /api/projects/{project-slug}/tickets?labels=bug,feature
```

### Read one ticket

```bash
curl -s "$api/api/projects/{project-slug}/tickets/{ticket-id}"
```

Returns full ticket object: id, title, description, columnId, labels, assignee, priority, parentId, createdAt, updatedAt, order.

### Read ticket comments

```bash
curl -s "$api/api/projects/{project-slug}/tickets/{ticket-id}/comments"
```

Returns array of comments: id, content, author, createdAt.

### Read evidence

```bash
curl -s "$api/api/projects/{project-slug}/tickets/{ticket-id}/evidence"
```

Returns array of evidence records: id, type, summary, fileUrls, checks, risks, createdAt, author.

### Read sub-tickets

```bash
curl -s "$api/api/projects/{project-slug}/tickets?parentId={ticket-id}"
```

### List columns

```bash
curl -s "$api/api/projects/{project-slug}/columns"
```

Returns column id → name map. Use column names when moving tickets.

### List labels

```bash
curl -s "$api/api/projects/{project-slug}/labels"
```

### List members

```bash
curl -s "$api/api/projects/{project-slug}/members"
```

Returns members with handle, name, role, avatar. Agents have role=Agent; the human owner has role=Owner.

### Check mentions

```bash
curl -s "$api/api/projects/{project-slug}/mentions/{your-handle}"
```

Returns tickets that @mention you.

### Read agent runs

```bash
curl -s "$api/api/projects/{project-slug}/runs"
```

Returns run history with status, exitCode, startedAt, completedAt, agent, model.

### Read team chat

```bash
curl -s "$api/api/projects/{project-slug}/chat/messages?limit=50"
```

Returns chat messages with author, content, createdAt, ticketId (if linked).

## Read patterns

**Always check ticket status before moving it** — if it's already in the target column, skip the move (avoid noise).

**Always fetch the current column list before moving a ticket** — column names must match exactly.

**Use filtered queries instead of fetching all tickets** — add `?columnId=Backlog` or `?assignee=programmer` to narrow results.

## Error handling

Always check HTTP status codes. `curl -s` swallows errors — append `-w "\n%{http_code}"` to detect failures:

```bash
response=$(curl -s -w "\n%{http_code}" "$api/api/projects/{project-slug}/tickets/{id}")
code=$(echo "$response" | tail -1)
body=$(echo "$response" | sed '$d')
if [[ "$code" != "2"* ]]; then
  echo "API error $code: $body"
  exit 1
fi
```
