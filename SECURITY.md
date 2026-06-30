# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| v0.x    | :warning: Latest development — use caution |

## Reporting a Vulnerability

If you discover a security vulnerability in Beaver Board, please report it responsibly.

**Please do NOT open a public GitHub issue for security vulnerabilities.**

Instead, email the maintainer directly or contact them through the repository's security tab.

When reporting, please include:
- A clear description of the vulnerability
- Steps to reproduce it
- Potential impact
- Any suggested fixes (if applicable)

## Security Model

Beaver Board is designed for **local-first, single-user** usage. Key security properties:

### Local-only by default
- Beaver Board binds to `localhost` only by default.
- No `AllowAnyOrigin` CORS policy.
- Do NOT expose Beaver Board to the public internet without additional authentication.

### Data storage
- All data is stored locally in `%APPDATA%/BeaverBoard/` (Windows) or `~/.beaverboard/` (macOS/Linux).
- You can change the data directory with the `BEAVERBOARD_DATA_DIR` environment variable.

### Agent execution
- Agents execute arbitrary code on your machine. Only install agents you trust.
- The `executePowerShell` / `executeScript` automation action is **disabled by default**.
- Review agent SKILL.md files before adding agents to your workspace.

### Secrets and API keys
- API keys for AI providers are configured per-project and stored locally.
- Never commit provider API keys to source control.
- Use environment variables or a secrets manager for CI/CD integration.

### Public repository safety
- The `scripts/audit-public-repo.sh` script scans for leaked secrets, hardcoded paths, and private project references before commits.
- Run it locally before pushing to the public repository.
- The CI pipeline includes Gitleaks for secrets scanning on every pull request.
