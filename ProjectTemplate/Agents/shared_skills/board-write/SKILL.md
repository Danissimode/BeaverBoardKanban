# board-write skill

Write access to the Beaver Board: creating tickets, moving them between columns, updating fields, posting comments, and setting labels. Always read the board first (see `board-read` skill) before making changes.

## Always read before writing

Before any write operation:
1. Fetch the current ticket state to avoid redundant updates
2. Fetch the column list to ensure the target column name is correct
3. Skip the write if the desired state is already reached

## Move a ticket

```bash
curl -X PATCH "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}/status" \
  -H "Content-Type: application/json" \
  -d '{"status": "Review", "author": "{your-handle}"}'
```

Valid statuses: match your project's column names exactly. Common: `Backlog`, `Todo`, `InProgress`, `Review`, `Done`, `Blocked`.

Always pass `author` — the board records who made the change.

## Create a ticket

```bash
curl -X POST "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Fix login redirect loop",
    "description": "Users are redirected to /login indefinitely after logout.",
    "status": "Backlog",
    "priority": "Required",
    "labels": ["bug"],
    "author": "{your-handle}"
  }'
```

Valid priorities: `Idea`, `NiceToHave`, `Required`, `Critical`.

## Update ticket fields

```bash
curl -X PATCH "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Updated title",
    "description": "New description",
    "assignedTo": "programmer",
    "priority": "Critical",
    "author": "{your-handle}"
  }'
```

All fields are optional. Only include what you want to change.

## Post a comment

```bash
curl -X POST "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}/comments" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "Applied fix in commit abc123. Ready for review.",
    "author": "{your-handle}"
  }'
```

Format tips:
- Use `@{handle}` to notify a member
- Use `#{ticket-id}` to reference a ticket in the same project
- Use `#{project-slug}:{ticket-id}` for cross-project references
- Keep comments concise: 1–3 sentences. Longer explanations belong in the ticket description or evidence, not comments.

## Set labels

```bash
curl -X PUT "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}/labels" \
  -H "Content-Type: application/json" \
  -d '{"labels": ["bug", "security"], "author": "{your-handle}"}'
```

## Add/remove sub-ticket

```bash
# Create as child of parent ticket
curl -X POST "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets" \
  -H "Content-Type: application/json" \
  -d '{"title": "Sub-task: write unit tests", "parentId": "{parent-ticket-id}", "status": "Backlog", "author": "{your-handle}"}'

# Reparent
curl -X PUT "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}/parent" \
  -H "Content-Type: application/json" \
  -d '{"parentId": "{new-parent-id}", "author": "{your-handle}"}'

# Detach
curl -X DELETE "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}/parent?author={your-handle}"
```

## Create a column

```bash
curl -X POST "${KITTYCLAW_API_URL}/api/projects/{project-slug}/columns" \
  -H "Content-Type: application/json" \
  -d '{"name": "QA", "order": 4, "author": "{your-handle}"}'
```

## Create a label

```bash
curl -X POST "${KITTYCLAW_API_URL}/api/projects/{project-slug}/labels" \
  -H "Content-Type: application/json" \
  -d '{"name": "security", "color": "#e63946", "author": "{your-handle}"}'
```

## Patterns

**Conditional moves**: Before moving, fetch the ticket's current status. Skip if already there:
```bash
current=$(curl -s "$api/api/projects/{project-slug}/tickets/{id}" | jq -r '.columnId')
if [[ "$current" == "Review" ]]; then
  echo "Already in Review, skipping"
else
  curl -X PATCH "$api/api/projects/{project-slug}/tickets/{id}/status" \
    -H "Content-Type: application/json" \
    -d '{"status": "Review", "author": "programmer"}'
fi
```

**Blocking flow**: Move to `Blocked` + post a comment explaining the blocker — don't just leave it in `InProgress`.

**Strict scope**: Create a new ticket for any work you discover outside your assigned ticket. Never silently fix unrelated bugs.

## Error handling

Always check the HTTP status of write calls. A 4xx/5xx means the change did NOT happen:
```bash
code=$(curl -s -o /dev/null -w "%{http_code}" -X PATCH ...)
if [[ "$code" != "2"* ]]; then
  echo "Failed to move ticket: HTTP $code"
  exit 1
fi
```
