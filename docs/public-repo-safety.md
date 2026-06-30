# Public Repository Safety Guide

This document defines the safety rules for maintaining Beaver Board Kanban as a public open-source repository.

## Purpose

Beaver Board Kanban is a public repository. To protect contributors and maintain security, certain types of information **must never** be committed to the repository.

---

## 🚫 Never Commit

### 1. Secrets and Credentials
- API keys (OpenAI, Anthropic, OpenRouter, etc.)
- Authentication tokens
- Passwords
- Database connection strings with credentials
- SSH keys or certificates

### 2. Private Project Names
- Names of private projects or workspaces
- Internal company/project identifiers
- Personal project names

### 3. Local Machine Paths
- Absolute paths (e.g., `/Users/username/...`)
- User-specific directories
- Home directory references
- Windows user paths (e.g., `C:\Users\username\...`)

### 4. Personal Provider/Model Preferences
- Specific API provider endpoints or account IDs
- Personal model defaults or quotas
- Account-specific configuration
- Custom model names tied to your account

### 5. Infrastructure Details
- Specific quotas or rate limits
- Personal billing information
- Custom bridge configurations
- Private API endpoints

---

## ✅ Safe to Commit

### Generic Defaults
```json
// ✅ Good - generic placeholder
"provider": "<your-provider>"
"model": "<your-model>"

// ❌ Bad - specific personal preference
"provider": "openrouter"
"model": "deepseek-v4-pro"
```

### CLI Commands
```bash
# ✅ Good - PATH lookup
opencode

# ❌ Bad - absolute personal path
/Users/username/.opencode/bin/opencode
```

### Workspace References
```bash
# ✅ Good - environment variable or app-relative
${BEAVERBOARD_DEFAULT_WORKSPACE}
./workspace

# ❌ Bad - absolute personal path
/Users/username/Documents/Projects/MyProject
```

---

## Configuration Guidelines

### appsettings.json
- Use generic defaults only
- CLI command should be `"opencode"` (PATH lookup)
- Provider/model should be empty or `<placeholder>`
- All secrets must come from environment variables or `appsettings.Development.json`

### Program.cs
- Never hardcode workspace paths
- Use `Environment.GetEnvironmentVariable()` for user-specific paths
- Default to app root or current directory

### README and Documentation
- Use `<your-provider>` and `<your-model>` as placeholders
- Don't list your actual preferred models/providers
- Don't include screenshots showing private data

---

## GitHub Secret Scanning

GitHub automatically scans repositories for committed secrets. If a secret is detected:

1. **Immediately revoke the secret** in the provider's dashboard
2. **Do not assume the repository is safe** just because you deleted the commit
3. Consider using [GitHub's Push Protection](https://docs.github.com/en/code-security/secret-scanning/about-secret-scanning#push-protection-to-prevent-secrets-from-being-pushed)

### Enabling Push Protection

Go to **Settings → Code security and analysis → Secret scanning** and enable:
- ✅ Secret scanning
- ✅ Push protection

---

## Local Override Pattern

For local development, use `appsettings.Development.json`:

```json
{
  "OpenCode": {
    "CliCommand": "/Users/myuser/.local/bin/opencode",
    "DefaultProvider": "my-provider",
    "DefaultModel": "my-model"
  }
}
```

This file is already in `.gitignore` and will not be committed.

---

## Review Checklist

Before committing any changes, verify:

- [ ] No API keys or tokens in the diff
- [ ] No absolute paths to personal directories
- [ ] No private project or workspace names
- [ ] No personal provider/model configurations
- [ ] All secrets use environment variables
- [ ] Documentation uses generic placeholders

---

## Reporting Security Issues

If you discover a security issue in this repository, please report it via:

1. GitHub's [Private vulnerability reporting](https://github.com/beaverboard/BeaverBoardKanban/security/advisories/new)
2. Or email (to be added)

Do NOT open a public issue for security vulnerabilities.
