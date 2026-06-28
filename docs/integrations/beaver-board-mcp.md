# Beaver Board MCP Control Plane

External MCP gateway for ChatGPT, Jules, Codex, and other AI agents to manage Beaver Board safely.

## Architecture

```
ChatGPT / Jules / Codex
  ↓ MCP stdio
BeaverBoardKanban-mcp
  ↓ REST API (localhost:5230)
Beaver Board Kanban
  ↓ tickets / runs / team chat
Agents / OpenCode / worktrees
  ↓
Repo
```

## What the MCP Can Do

- List projects and tickets
- Create tickets with plans and acceptance criteria
- Set execution config (provider, model, agent)
- Move tickets through workflow stages
- Post to Team Chat (@team, @role, #ticket)
- Reply to Needs Human questions
- Check latest runs and failures
- Request completion reports
- Mark Verified (not Done — owner only)

## What the MCP Cannot Do

- Run shell commands
- Git commit/push
- Edit repository files
- Make arbitrary HTTP requests
- Directly execute agents
- Move tickets to Done (owner only)

## Setup

```bash
# Clone the MCP repo
git clone https://github.com/Danissimode/BeaverBoardKanban-mcp.git
cd BeaverBoardKanban-mcp
npm install
npm run build

# Configure policy
mkdir -p ~/.config/beaver-board-mcp
cp config/policy.example.yaml ~/.config/beaver-board-mcp/policy.yaml

# Edit policy
vim ~/.config/beaver-board-mcp/policy.yaml

# Run
node dist/index.js --config ~/.config/beaver-board-mcp/policy.yaml
```

## Policy Configuration

```yaml
beaver:
  base_url: "http://localhost:5230"
  author: "chatgpt"
  allowed_projects:
    - "petpals"
    - "petpalscursor"

tools:
  allow:
    - "beaver.projects.list"
    - "beaver.ticket.get"
    - "beaver.ticket.create"
    - "beaver.team_chat.post"
    - "beaver.failures.list"
  deny:
    - "run_shell_command"
    - "git_commit"
    - "edit_repo_files"
```

## See Also

- [MCP Security Boundary](mcp-security-boundary.md)
- [ChatGPT MCP Workflow](chatgpt-mcp-workflow.md)
- [BeaverBoardKanban-mcp Repository](https://github.com/Danissimode/BeaverBoardKanban-mcp)
