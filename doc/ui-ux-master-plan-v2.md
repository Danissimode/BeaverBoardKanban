# BeaverBoard v2.0 — UI/UX Master Plan

**Context:** User provided a detailed UI/UX plan. This document refines it based on actual codebase analysis.

---

## Ключевой инсайт

**~60% уже реализовано.** Детальный анализ кодовой базы показал:

| Что | Статус | Комментарий |
|-----|--------|-------------|
| 4-tab drawer (Details/Plan/Execution/Run) | ✅ Готово | Board.razor:853-1219 |
| Runner API (`/api/runners/health`) | ✅ Готово | Endpoints.Runners.cs |
| Runner availability checker | ✅ Готово | `RunnerAvailabilityChecker.cs` |
| Live streaming console drawer | ✅ Готово | `AgentRunDrawer.razor` |
| Basic runner badges на карточках | ✅ Готово | `execution-badges`, `exec-badge--running` |
| Basic chat drawer | ✅ Готово | `ChatDrawer.razor` |
| Basic TeamChat dock | ✅ Готово | `TeamChatDock.razor` |
| Basic onboarding с runner detection | ✅ Готово | `Home.razor:82-162` |

**Основные пробелы — в UI/UX полировке:**
1. ❌ **Persistent Status Bar** в header (глобальный индикатор runner'ов)
2. ❌ **Enhanced Runner Badges** на карточках (нужна цветовая кодировка + длительность)
3. ❌ **Runner Switcher** в Chat drawer (сейчас жёстко к Claude)
4. ❌ **#ai-activity channel** в TeamChat (AI events не отображаются)
5. ❌ **Toast notifications** (нет системы нотификаций)
6. ❌ **Mobile responsive** (breakpoints нет)
7. ❌ **Live progress bar** в Execution tab (есть meta, нет визуального progress)
8. ❌ **Onboarding improvements** (авто-детекция, персистентный runner preference)

---

## PR #1 — Persistent Runner Status Bar (Sprint 1)

**Files:**
- `KittyClaw.Web/Components/Layout/MainLayout.razor` — добавить StatusBar
- `KittyClaw.Web/wwwroot/app.css` — стили StatusBar

**Implementation:**
```razor
@* В MainLayout.razor, после .page-header *@
<div class="runner-status-bar">
    @foreach (var r in _runners)
    {
        <span class="runner-status-badge runner-@r.Kind @(r.IsAvailable ? "available" : "unavailable")">
            @(r.Kind == "opencode" ? "🔵" : "🟠") @r.DisplayName
            @if (r.IsAvailable) { <span class="pulse-dot"></span> }
        </span>
    }
</div>
```

**API:** Уже есть `/api/runners/health` — просто добавить Blazor state management.

**Verification:** Header показывает 🔵 OpenCode Ready / 🟠 Claude Idle.

---

## PR #2 — Enhanced Runner Badges on Cards (Sprint 1)

**Files:**
- `KittyClaw.Web/Components/Pages/Board.razor` — карточки (Board.razor:168-232)
- `KittyClaw.Web/wwwroot/app.css` — стили badge'ов

**Changes:**
1. Цветовая кодировка badge'ов:
   - `opencode` → синий `#3B82F6`
   - `claude` → оранжевый `#F97316`
   - `manual` → серый `#6B7280`
   - `failed` → красный `#EF4444` + pulse анимация

2. Добавить длительность выполнения:
```razor
<span class="agent-run-badge badge-@run.Status.ToString().ToLower()"
      style="--badge-color: @GetRunnerColor(run.RunnerKind)">
    @GetRunnerIcon(run.RunnerKind) @FormatElapsed(run.StartedAt)
</span>
```

**Verification:** Карточки с активными run показывают цветной badge с длительностью.

---

## PR #3 — Chat Drawer Multi-Runner (Sprint 2)

**Files:**
- `KittyClaw.Web/Components/ClaudeChatDrawer.razor` → `ChatDrawer.razor` ✅ Done
- `KittyClaw.Web/Api/Endpoints.Chat.cs` — добавить runner selection в API

**Changes:**
1. Добавить dropdown для выбора runner'а в header чата:
```razor
<div class="chat-runner-selector">
    <select @bind="_selectedRunner" class="runner-select">
        <option value="opencode">🔵 OpenCode</option>
        <option value="claude">🟠 Claude Code</option>
        <option value="manual">⚪ Manual</option>
    </select>
</div>
```

2. API endpoint для получения списка runner'ов: GET `/api/chat/runners` — возвращает available runners.

3. Прокинуть runner selection в `/api/projects/{slug}/chat/start`.

**Verification:** Chat drawer работает с OpenCode и Claude, можно переключать mid-chat.

---

## PR #4 — #ai-activity Channel in TeamChat (Sprint 2)

**Files:**
- `KittyClaw.Web/Components/TeamChat/TeamChatDock.razor`
- `KittyClaw.Core/Services/ChatService.cs` или новый `ITeamChatService`

**Changes:**
1. Добавить новый filter/tab `#ai-activity`:
```razor
<button class="filter-btn @(ActiveFilter == "ai-activity" ? "active" : "")" 
        @onclick='() => SetFilter("ai-activity")'>
    🤖 AI Activity
</button>
```

2. Автоматическая публикация AI events из `AutomationEngine` → `TeamChatService`:
   - `run_started` event → публикуется в `#ai-activity`
   - `run_completed` event → публикуется в `#ai-activity`
   - `run_failed` event → публикуется в `#ai-activity` с priority high

3. Render AI events как special cards:
```razor
@if (msg.MessageType == "run_event")
{
    <div class="message-card message-card--ai-event">
        <span class="ai-event-icon">@(GetAiEventIcon(msg.Subtype))</span>
        <span class="ai-event-text">@msg.Body</span>
        <a href="/board/@Slug/ticket/@msg.TicketId" class="ai-event-link">View #@msg.TicketId</a>
    </div>
}
```

**Verification:** AI Activity tab показывает все runner events с возможностью перейти к тикету.

---

## PR #5 — Toast Notification System (Sprint 2)

**Files:**
- `KittyClaw.Web/Components/ToastContainer.razor` (новый)
- `KittyClaw.Web/wwwroot/app.css`
- `KittyClaw.Web/Services/ToastService.cs` (новый)
- `KittyClaw.Web/Components/Layout/MainLayout.razor` — добавить ToastContainer

**Implementation:**
```csharp
// ToastService.cs
public sealed record Toast(
    string Id,
    string Title,
    string Body,
    ToastKind Kind, // success | error | warning | info
    string? RunId = null,
    string? TicketId = null
);

public enum ToastKind { Success, Error, Warning, Info }

public interface IToastService
{
    event Action<Toast>? OnToast;
    void Show(Toast toast);
    void Dismiss(string id);
}
```

```razor
@* ToastContainer.razor *@
<div class="toast-container">
    @foreach (var toast in _toasts)
    {
        <div class="toast toast-@toast.Kind">
            <span class="toast-icon">@GetIcon(toast.Kind)</span>
            <div class="toast-content">
                <strong>@toast.Title</strong>
                <p>@toast.Body</p>
            </div>
            @if (toast.RunId is not null)
            {
                <button @onclick="() => OpenRun(toast.RunId)">Watch</button>
            }
            <button @onclick="() => Dismiss(toast.Id)">×</button>
        </div>
    }
</div>
```

**Hook into existing flows:**
- `AgentRunsState` уже имеет `OnChange` — добавить toast при переходах RunStatus
- Не нужно менять AutomationEngine, только Web layer

**Verification:** Toast появляется при start/stop/fail run.

---

## PR #6 — Mobile Responsive (Sprint 3)

**Files:**
- `KittyClaw.Web/wwwroot/app.css` — breakpoints
- `KittyClaw.Web/Components/Pages/Board.razor` — mobile layout
- `KittyClaw.Web/Components/Layout/MainLayout.razor` — mobile status bar

**Breakpoints:**
```css
/* Mobile-first, добавялем desktop overrides */
@media (min-width: 640px) {
    /* 2-column Kanban, collapsible chat */
}
@media (min-width: 1024px) {
    /* Full Kanban + persistent chat + TeamChat */
}
```

**Mobile-specific changes:**
1. Board: single column, swipe между column'ами, FAB для actions
2. Status bar: compact mode для mobile
3. Chat drawer: full-screen на mobile
4. Ticket panel: slide-in panel

---

## PR #7 — Onboarding Improvements (Sprint 3)

**Files:**
- `KittyClaw.Web/Components/Pages/Home.razor` — улучшить onboarding
- `KittyClaw.Core/Automation/Runners/RunnerAvailabilityChecker.cs` — добавить версию и provider info

**Changes:**
1. Авто-детекция runner'а при первом визите:
```csharp
// RunnerAvailabilityChecker — вернуть больше metadata
public record RunnerAvailabilityReport(
    string RunnerKind,
    string DisplayName,
    bool IsAvailable,
    string? Version,
    string? Provider,     // NEW: напр. "openrouter", "anthropic"
    string? Model,        // NEW: напр. "deepseek-v4-pro"
    bool IsDefault,
    bool IsRecommended,
    string? Error
);
```

2. Onboarding UI — показать provider/model вместо just "Installed":
```razor
<div class="connector-card connector-primary @(_opencodeInstalled ? "ok" : "missing")">
    <span class="connector-icon">🦫</span>
    <div class="connector-info">
        <span class="connector-name">@L["OpenCode"]</span>
        @if (_opencodeInstalled)
        {
            <span class="connector-status">
                ✅ @L["StatusInstalled"] · @_opencodeVersion · via @_opencodeProvider
            </span>
        }
        else
        {
            <span class="connector-status">@L["StatusNotInstalled"]</span>
        }
    </div>
</div>
```

3. Сохранение runner preference в project settings.

---

## PR #8 — Live Progress in Execution Tab (Sprint 2)

**Files:**
- `KittyClaw.Web/Components/Pages/Board.razor` — Execution tab (Board.razor:931-1033)
- `KittyClaw.Web/wwwroot/app.css`

**Changes:**
1. Добавить live progress bar когда run активен:
```razor
@if (_latestRunMeta?.Status == AgentRunStatus.Running)
{
    <div class="live-progress-section">
        <div class="live-progress-header">
            <span class="runner-badge" style="--badge-color: @GetRunnerColor(_latestRunMeta.RunnerKind)">
                @GetRunnerIcon(_latestRunMeta.RunnerKind) @_latestRunMeta.RunnerKind
            </span>
            <span class="live-progress-time">@FormatElapsed(_latestRunMeta.StartedAt)</span>
        </div>
        <div class="live-progress-bar">
            <div class="live-progress-fill" style="width: @(_liveProgress ?? 0)%"></div>
        </div>
        <div class="live-progress-actions">
            <button class="btn-warning" @onclick="StopLatestRun">⏹ Stop</button>
            <button class="btn-secondary" @onclick="OpenRunDrawerForLatest">Open Console</button>
        </div>
    </div>
}
```

2. WebSocket/SSE connection для real-time progress: использовать существующий `AgentRunsState.OnChange` + SSE из `AgentRunDrawer`.

**Verification:** Execution tab показывает анимированный progress bar для активных run'ов.

---

## Отложенные фичи (Sprint 4+)

| Фича | Причина отложки |
|------|-----------------|
| Dark mode toggle | CSS tokens уже есть, но UI toggle не приоритет |
| Accessibility audit | Нужна отдельная итерация с a11y-специалистом |
| Graceful degradation UI (Claude not configured) | Backend graceful — UI не критичен |

---

## Рекомендуемый порядок реализации

```
PR #1 → PR #2 (Sprint 1) — Status Bar + Enhanced Badges (простые, видимые)
PR #3 → PR #8 (Sprint 2) — Chat Multi-Runner + Live Progress (связаны через runner API)
PR #4 → PR #5 (Sprint 2) — AI Activity + Toasts (связаны через event system)
PR #7 (Sprint 3) — Onboarding Improvements
PR #6 (Sprint 3) — Mobile Responsive
```

**Общее время:** ~6-8 спринтов (ожидаемо меньше, т.к. ~60% уже реализовано).

---

## Проверка гипотез

1. **Chat drawer hardcoded к Claude?** → Нет, UI уже использует `/chat/start-v2` с runner selector ✅
2. **TeamChat не получает AI events?** → Да, `TeamChatDock` только poll'ит вручную. Нужен push из `AutomationEngine`.
3. **Нет toast system?** → Да, nowhere in codebase. Нужен новый сервис.
4. **Mobile responsive?** → Нет медиа-запросов в CSS. Board = single column на всех размерах.

---

## Verification Strategy

Для каждого PR:
1. **Manual test:** Запустить BeaverBoard, выполнить described user flow
2. **API test:** `curl http://localhost:5230/api/runners/health` — убедиться в正确ном JSON
3. **Build test:** `dotnet build KittyClaw.Web` — убедиться что нет compile errors
4. **CSS:** Browser DevTools — проверить что стили применяются
