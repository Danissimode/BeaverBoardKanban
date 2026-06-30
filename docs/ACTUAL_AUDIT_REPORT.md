# Реальный аудит BeaverBoardKanban — Runner Integration

> Дата: 2026-06-30
> Проверено файлов: 40+ ключевых файлов
> Вывод: анализ пользователя был основан на **устаревшей версии** кодовой базы. Многие компоненты, описанные как «отсутствующие», уже реализованы. Ниже — честная сверка с реальностью.

---

## Что УЖЕ реализовано (вопреки анализу)

| Компонент | Что говорил анализ | Реальность | Статус |
|-----------|-------------------|------------|--------|
| `OpenCodeRunner` | «Второсортный, не регистрирует run, нет Stop/Steer» | Полная интеграция с `AgentRunRegistry`, `StopAsync` через `Process.Kill`, `SteerAsync` через temp file, SSE/HTTP server mode, event streaming, `DeepCheckAsync` | ✅ Done |
| `RunnerAvailabilityChecker` | «Нужно создать» | Уже существует, делает deep-check с версией, рекомендует OpenCode | ✅ Done |
| `Endpoints.Runners.cs` | «Нужно создать» | Уже существует: `/runners`, `/runners/health`, `/runners/default`, `/runners/{kind}`, `/runners/recommended` | ✅ Done |
| `AgentRun.RunnerKind` | «Нужно добавить» | Уже есть с default="claude" | ✅ Done |
| `SettingsData.PreferredRunner` | «Нужно добавить» | Уже есть: `"auto"`, `"opencode"`, `"claude"` | ✅ Done |
| `SettingsData.OpenCode` | «Нужно добавить» | Уже есть `OpenCodeConfigData` с provider/model/agent | ✅ Done |
| `Endpoints.Chat.cs` v2 | «Жёстко использует ClaudeRunner» | Использует `RunnerRegistry.ResolveRunner(req.Runner, ExecutionMode.DirectOpenCode)` | ✅ Done |
| `Endpoints.Runs.cs` /retry | «Жёстко вызывает ClaudeRunner» | Использует `RunnerRegistry`, разрешает по `run.RunnerKind` | ✅ Done |
| `Home.razor` onboarding | «Не проверяет OpenCodeRunner.IsAvailable» | Проверяет OpenCode первым, Claude — опционально, вызывает `/api/runners/health`, позволяет выбрать default | ✅ Done |
| `ProjectSettings.razor` | «Нет UI runner'ов» | Есть секция Runners: выбор default, health check, диагностика | ✅ Done |
| `TeamChatRunNotifier` | «Нужно создать» | Уже существует, подписан на `AgentRunRegistry.OnRunStarted/OnRunEnded` | ✅ Done |
| `AgentRunsState` | Не упоминался | Мост в Blazor: toast-уведомления, реактивные обновления UI | ✅ Done |
| `ActionExecutor.Runners.cs` | «Нужно расширить» | Уже расширен: `ResolveRunner`, `CheckExecutionPolicyAsync`, `EnsureWorktreeAsync`, `SaveExecutionMetadataAsync` | ✅ Done |
| `PendingSteerMessages` | «Нужно добавить» | Полностью реализовано в `AgentRun`, `ClaudeRunner`, `RunLogStore`, `AgentRunSnapshot` | ✅ Done |
| `ClaudeRunnerAdapter` | «Не существует» | Уже существует, оборачивает `ClaudeRunner` в `IAgentRunner` | ✅ Done |

---

## Оставшиеся реальные проблемы (P0-P2)

### P0 — Реальные баги

1. **TeamChatRunNotifier зарегистрирован в DI, но никогда не инстанциируется**
   - В `Program.cs`: `builder.Services.AddSingleton<TeamChatRunNotifier>();`
   - Но нигде нет `GetRequiredService<TeamChatRunNotifier>()`
   - ASP.NET Core создаёт singleton лениво. Если никто не запрашивает — конструктор не вызывается, события не подписываются.
   - **Результат:** команда не видит уведомления о старте/завершении run в TeamChat.

2. **RunnerRegistry не читает `PreferredRunner` из settings при старте**
   - `Program.cs` всегда ставит OpenCode дефолтом если он доступен, игнорируя выбор пользователя.
   - Пользователь выбирает Claude → перезапускает приложение → OpenCode снова дефолт.

3. **PendingSteerMessages утекают в chat v2**
   - `Endpoints.Chat.cs` v2 делает `DrainPendingSteerMessages()` на строке 127, но:
     - `AgentRunRequest` **не имеет** поля `PendingSteerMessages`
     - `ClaudeRunnerAdapter.StartAsync` всегда ставит `PendingSteerMessages = null`
     - `OpenCodeRunner` вообще не обрабатывает pending steers
   - **Результат:** steering сообщения, пришедшие между запусками, теряются в v2.

### P1 — Значительные недоработки

4. **OpenCodeRunner не линкует внешний CancellationToken**
   - `ExecuteViaCliAsync` не использует `CreateLinkedTokenSource` с внешним токеном.
   - Внешний `cancellationToken` переданный в `StartAsync` игнорируется во время выполнения.

5. **OpenCodeConfigData.DefaultModel — дефолт Claude-модели**
   - Сейчас: `"anthropic/claude-3-5-sonnet-20241022"`
   - Для OpenCode-ориентированного продукта должен быть OpenRouter-дефолт (например, `deepseek-v4-pro` или `anthropic/claude-3-5-sonnet` через OpenRouter).

6. **Chat target list жёстко показывает "Claude"**
   - `Endpoints.Chat.cs` строка 30: `new("owner-chat", "Claude", "claude")`
   - Должно быть адаптивно под текущий default runner.

7. **Нет автоматического обновления статуса тикета при завершении run**
   - `TeamChatRunNotifier` пишет сообщение в чат, но статус тикета не меняется.
   - Запрошено в анализе: Running → In Progress, Completed → Review, Failed → Failed.

### P2 — Мелочи

8. **ClaudeRunnerAdapter.StopAsync/SteerAsync/GetStatusAsync — TODO**
   - Не критично, т.к. endpoints обходят адаптер через `AgentRunRegistry`.
   - Но для полноты интерфейса `IAgentRunner` стоит доделать.

9. **README.OpenCode.md дублирует docs/OpenCode-Integration.md**
   - Известно (см. status-matrix.md).

---

## Архитектурная правда о rebase

В текущем репозитории **нет** стратегии `partial class` + `.Beaver.cs` для сохранения совместимости с upstream. Все BeaverBoard-изменения интегрированы напрямую в файлы `KittyClaw.*`. Форк уже сильно дивергировал. Предложенная стратегия rebase-friendly архитектуры — это хорошая идея для **будущего**, но не для текущего состояния.

---

## План реализации (только реальные пробелы)

### Фаза 1: P0 — Критические баги
- [ ] Fix `TeamChatRunNotifier` — вызвать `GetRequiredService` в `Program.cs` или использовать `IHostedService`
- [ ] Fix `RunnerRegistry` — читать `PreferredRunner` из `SettingsService` при инициализации
- [ ] Fix `PendingSteerMessages` — добавить в `AgentRunRequest`, прокинуть в chat v2, обработать в `OpenCodeRunner` и `ClaudeRunnerAdapter`

### Фаза 2: P1 — UX и надёжность
- [ ] Fix `OpenCodeRunner` cancellation linking
- [ ] Fix `OpenCodeConfigData.DefaultModel` → OpenRouter-дефолт
- [ ] Fix chat target list — показывать имя текущего default runner
- [ ] Auto-status update — `TeamChatRunNotifier` или `AgentRunRegistry.OnRunEnded` → `TicketService.MoveStatus`

### Фаза 3: P2 — Доделки
- [ ] Реализовать `ClaudeRunnerAdapter.StopAsync/SteerAsync/GetStatusAsync`
- [ ] Удалить/перенаправить `README.OpenCode.md`
