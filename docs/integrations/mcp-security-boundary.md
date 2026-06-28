# MCP Security Boundary

The Beaver Board MCP Control Plane is a security-sensitive gateway. It provides AI agents with controlled access to the board while maintaining strict boundaries.

## Security Principles

1. **No shell access** — MCP cannot execute shell commands
2. **No git access** — MCP cannot commit, push, or clone
3. **No repo editing** — MCP cannot modify source files
4. **No arbitrary HTTP** — MCP can only call typed Beaver Board API endpoints
5. **No direct agent execution** — MCP cannot run OpenCode/Claude directly
6. **Policy enforcement** — All mutations checked against policy
7. **Audit logging** — Every tool call recorded
8. **Owner-only Done** — AI agents cannot move tickets to Done

## Allowed Tools (Default)

| Tool | Description | Mutating |
|------|-------------|----------|
| `beaver.projects.list` | List projects | No |
| `beaver.board.summary` | Get board summary | No |
| `beaver.ticket.get` | Get ticket details | No |
| `beaver.ticket.create` | Create new ticket | Yes |
| `beaver.ticket.add_comment` | Add comment | Yes |
| `beaver.ticket.set_plan` | Set implementation plan | Yes |
| `beaver.ticket.approve_plan` | Approve plan | Yes |
| `beaver.ticket.set_execution` | Set provider/model | Yes |
| `beaver.ticket.move_status` | Move ticket | Yes |
| `beaver.runs.latest` | Get latest run | No |
| `beaver.failures.list` | List failures | No |
| `beaver.team_chat.post` | Post to team chat | Yes |
| `beaver.team_chat.inbox` | Get inbox | No |
| `beaver.team_chat.reply` | Reply to message | Yes |
| `audit.recent` | Get audit log | No |

## Denied Tools (Always)

| Tool | Reason |
|------|--------|
| `run_shell_command` | Shell access |
| `git_commit` | Git access |
| `git_push` | Git access |
| `edit_repo_files` | Repo editing |
| `apply_patch` | Repo editing |
| `execute_agent_directly` | Direct execution |
| `generic_http_request` | Arbitrary HTTP |
| `raw_openapi_call` | Raw API proxy |

## Policy Enforcement

Every mutating tool call goes through:

1. **Tool policy check** — Is tool allowed?
2. **Confirmation check** — Does tool require user confirmation?
3. **Project check** — Is project in allowed list?
4. **Transition check** — Is status transition allowed?
5. **Risk check** — Does ticket have high-risk labels?
6. **Execution** — Call Beaver Board API
7. **Audit log** — Record the call

## Audit Log

All tool calls are logged to:

```
~/.local/share/beaver-board-mcp/audit.jsonl
```

Each entry contains:
- Timestamp
- Tool name
- Arguments (redacted if secrets)
- Result status
- Author

## Redaction

Sensitive data is automatically redacted in logs:
- API keys
- Tokens
- Passwords
- Secrets in ticket descriptions
