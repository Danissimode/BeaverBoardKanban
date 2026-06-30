# done-gate skill

Verify that a ticket is ready to move to `Done` before marking it as done. The Done Gate enforces two requirements: **evidence exists** and **git has changes** in the workspace. This prevents empty or unverified work from being marked complete.

## The two gates

A ticket may only move to `Done` if **both** conditions are true:

### Gate 1: Evidence attached

At least one evidence record exists on the ticket.

```bash
evidence=$(curl -s "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}/evidence")
count=$(echo "$evidence" | jq length)
if [[ "$count" -eq 0 ]]; then
  echo "BLOCKED: no evidence attached to ticket"
  exit 1
fi
```

### Gate 2: Git has changes

The workspace has uncommitted file changes that relate to the ticket.

```bash
# Check for any uncommitted changes
change_count=$(git status --porcelain | wc -l)
if [[ "$change_count" -eq 0 ]]; then
  echo "BLOCKED: no git changes in workspace"
  exit 1
fi

# List changed files
git status --porcelain
```

## Complete Done Gate check

```bash
#!/usr/bin/env bash
set -euo pipefail
api="${KITTYCLAW_API_URL}"
slug="{project-slug}"
tid="{ticket-id}"

# Gate 1: evidence
evidence=$(curl -s "$api/api/projects/$slug/tickets/$tid/evidence")
if [[ $(echo "$evidence" | jq length 2>/dev/null || echo 0) -eq 0 ]]; then
  echo "BLOCKED: no evidence — attach at least one evidence record before Done"
  exit 1
fi

# Gate 2: git changes
if ! git diff --quiet 2>/dev/null || ! git diff --cached --quiet 2>/dev/null; then
  echo "BLOCKED: uncommitted changes exist — commit or stash before Done"
  exit 1
fi
change_count=$(git status --porcelain | wc -l)
if [[ "$change_count" -eq 0 ]]; then
  echo "BLOCKED: no git changes — nothing to verify as done"
  exit 1
fi

echo "PASSED: evidence exists and git has changes"
```

## When to run the Done Gate

Run the Done Gate check **before** moving a ticket to `Done`. Do NOT skip this check.

If you are the agent completing work, run it yourself before requesting the ticket be closed:

```bash
# In your agent run, after finishing implementation:
./done-gate-check.sh
if [[ $? -eq 0 ]]; then
  # Safe to move to Done
  curl -X PATCH "$api/api/projects/{project-slug}/tickets/{ticket-id}/status" \
    -H "Content-Type: application/json" \
    -d '{"status": "Done", "author": "{your-handle}"}'
  curl -X POST "$api/api/projects/{project-slug}/chat/messages" \
    -H "Content-Type: application/json" \
    -d '{"content": "✅ BB-42 done. Evidence attached, git changes committed.", "author": "{your-handle}"}'
else
  # Blocked — post comment and stay in Review
  curl -X POST "$api/api/projects/{project-slug}/tickets/{ticket-id}/comments" \
    -H "Content-Type: application/json" \
    -d '{"content": "Done Gate blocked: no evidence attached or no git changes. Fix and retry.", "author": "system"}'
fi
```

## If both gates pass but git status is dirty

The Done Gate accepts a dirty working tree (uncommitted changes) — it only checks that changes **exist**, not that they've been committed. However:

- If the `committer` agent runs on Done, it will handle the commit
- If no `committer` runs, you should commit before moving to Done to keep the history clean

## Done Gate bypass

The human owner can manually move a ticket to `Done` regardless of the gate status. Agents must never bypass the gate.

## Common failures

| Failure | Cause | Fix |
|---|---|---|
| `BLOCKED: no evidence` | Evidence not attached yet | Attach evidence via `evidence` skill first |
| `BLOCKED: no git changes` | No files modified | Implement the ticket before Done |
| `BLOCKED: uncommitted changes` | Working tree dirty | Let committer handle, or commit explicitly |

## Gate order

Always check evidence **before** git changes. If evidence is missing, report it immediately — don't bother checking git.
