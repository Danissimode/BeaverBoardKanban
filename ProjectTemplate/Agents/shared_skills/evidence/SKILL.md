# evidence skill

Attach proof of work to tickets: test results, screenshots, code diffs, logs, reports, or a structured summary. Evidence appears in the ticket's Execution tab and satisfies the Done Gate requirements.

## What counts as evidence

Any of these is valid evidence:
- **Test output** — `dotnet test` / `npm test` / `cargo test` stdout/stderr
- **Build log** — clean compile output showing 0 errors
- **Code diff** — `git diff` output for the changes made
- **Screenshot** — image URL from a visual test or UI demo
- **Structured summary** — written in the evidence `summary` field
- **File list** — all modified file paths listed in `checks`
- **Performance numbers** — benchmarks, before/after metrics
- **Static analysis** — linter output, type-check output

## Attach evidence to a ticket

```bash
curl -X POST "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}/evidence" \
  -H "Content-Type: application/json" \
  -d '{
    "type": "summary",
    "summary": "Added 3 unit tests for AuthService. All pass. No regressions in existing suite.",
    "checks": [
      "dotnet test --filter FullyQualifiedName~AuthService → 3 tests, 0 failures",
      "Build → 0 errors"
    ],
    "risks": [],
    "author": "{your-handle}"
  }'
```

## Attach file-based evidence

For test output files, logs, or diffs, save the file first, then attach:

```bash
# Run tests and capture output
dotnet test --logger "trx;LogFileName=test-results.trx" > test.log 2>&1

# Attach with file reference
curl -X POST "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}/evidence" \
  -H "Content-Type: application/json" \
  -d '{
    "type": "test-results",
    "summary": "AuthService tests: 3 passed, 0 failed. Total suite: 47 passed.",
    "fileUrls": ["/absolute/path/to/test-results.trx"],
    "checks": [
      "AuthServiceTests: 3 passed",
      "Total test suite: 47 passed, 0 failed"
    ],
    "risks": [],
    "author": "qa-tester"
  }'
```

## Evidence types

| Type | Use case |
|---|---|
| `summary` | Written explanation of what was done and verified |
| `test-results` | Test framework output (xUnit, Jest, RSpec, etc.) |
| `build-log` | Compiler output |
| `diff` | Code changes (attach via fileUrl or inline) |
| `screenshot` | Visual proof (attach image URL) |
| `benchmark` | Performance before/after metrics |
| `security-scan` | Linter, SAST, or dependency audit output |
| `other` | Anything else |

## The `checks` and `risks` fields

Both are arrays of strings. Be specific:

**Good checks:**
```
- "dotnet test --filter ~AuthService → 5 tests, 0 failures, 0 skipped"
- "Build → 0 errors, 0 warnings"
- "git diff --stat → 3 files changed, +47 lines"
```

**Good risks:**
```
- "Changed shared BaseService.cs — review other call sites"
- "Added new dependency on System.Text.Json — verify compatibility"
- "No test coverage for error path in ParseConfig"
```

**Empty is fine** if there's nothing noteworthy:
```json
"checks": ["Build → 0 errors"],
"risks": []
```

## Evidence and Done Gate

The Done Gate blocks `InProgress → Done` unless:
1. At least one evidence record exists on the ticket
2. `git diff` shows at least one file change in the workspace

If evidence is missing, the owner (or the `done-gate` skill) will reject the move.

## Read existing evidence

```bash
curl -s "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}/evidence"
```

## Delete evidence (if you made a mistake)

```bash
curl -X DELETE "${KITTYCLAW_API_URL}/api/projects/{project-slug}/tickets/{ticket-id}/evidence/{evidence-id}?author={your-handle}"
```

## When to attach evidence

Always attach evidence when:
- Moving a ticket from `InProgress` to `Review`
- Finishing a ticket and asking the owner to close it
- Completing any work that should be verifiable

Always include:
1. What you changed (`checks`)
2. What risks or trade-offs were made (`risks`)
3. A plain-language summary of the result

Skip evidence only when the ticket is purely a question, discussion, or design artifact — not actual code work.
