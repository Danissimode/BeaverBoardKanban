# team-chat skill

Send and read messages in the project's shared team chat. Use this to coordinate with other agents, ask questions, share status, or notify the human owner.

## Setup

```bash
api="${KITTYCLAW_API_URL}"
```

## Read recent messages

```bash
# Last 50 messages
curl -s "$api/api/projects/{project-slug}/chat/messages?limit=50"

# Messages linked to a specific ticket
curl -s "$api/api/projects/{project-slug}/chat/messages?ticketId={ticket-id}"

# Messages since a timestamp (ISO 8601)
curl -s "$api/api/projects/{project-slug}/chat/messages?since=2026-06-01T09:00:00Z&limit=100"
```

## Send a message

```bash
curl -X POST "$api/api/projects/{project-slug}/chat/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "Finished implementing BB-42. Tests pass, moved to Review.",
    "author": "{your-handle}"
  }'
```

## Link a message to a ticket

```bash
curl -X POST "$api/api/projects/{project-slug}/chat/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "Blocked: the auth library is not installed yet.",
    "author": "programmer",
    "ticketId": "{ticket-id}"
  }'
```

Linking a message to a ticket makes it visible in the ticket's activity timeline.

## @mention a member

```bash
curl -X POST "$api/api/projects/{project-slug}/chat/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "@owner The auth library dependency is missing. Please add it so I can continue.",
    "author": "programmer"
  }'
```

The board will notify the mentioned member. Mentions work in both chat messages and ticket comments.

## Broadcast (multi-agent coordination)

When coordinating across multiple agents, post to the shared channel and set `isBroadcast: true`:
```bash
curl -X POST "$api/api/projects/{project-slug}/chat/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "Breaking change in API v2: all agents should update their /api/v2/ calls by EOD.",
    "author": "programmer",
    "isBroadcast": true
  }'
```

## Slash commands

The chat supports these slash commands (post as message content):

| Command | Effect |
|---|---|
| `/status` | Lists all active agent runs |
| `/board {query}` | Returns matching tickets (shorthand for board-read) |
| `/help` | Posts a help message |

## Run event messages

The orchestrator automatically posts run start/complete/fail/stop events to chat. You can also post manual status updates:

```bash
# Run started
curl -X POST "$api/api/projects/{project-slug}/chat/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "🔨 Starting BB-42 implementation...",
    "author": "programmer",
    "ticketId": "{ticket-id}"
  }'

# Run completed
curl -X POST "$api/api/projects/{project-slug}/chat/messages" \
  -H "Content-Type: application/json" \
  -d '{
    "content": "✅ BB-42 done. 3 tests added, build green. Moved to Review.",
    "author": "programmer",
    "ticketId": "{ticket-id}"
  }'
```

## Guidelines

**Be concise**: team chat is for coordination, not essays. One clear sentence is better than three paragraphs.

**Notify at decision points**: post to chat when you're blocked, when you finish a subtask, or when you need input from the owner.

**Use ticket linking**: always link chat messages to a ticket (`ticketId`) when the message is about a specific task. Unlinked messages are harder to trace later.

**Don't spam**: if your agent runs every 30 s, don't post a chat message every time. Post on state transitions (start, blocked, done, question).

**Coordinate via chat, not git commits**: use chat for inter-agent communication; save git commits for code changes.
