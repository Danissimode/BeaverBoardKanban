using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using KittyClaw.Core.Automation.AI;
using KittyClaw.Core.Automation.Runtimes;
using KittyClaw.Core.Automation.Triggers;
using KittyClaw.Core.Services;

namespace KittyClaw.Core.Automation;

/// <summary>
/// Evaluates automation conditions and executes action sequences.
/// Owns the git semaphore and all Execute*ActionAsync helpers.
/// </summary>
internal sealed partial class ActionExecutor
{
    // AI Provider Service for new execution path
    private readonly AIProviderService? _aiProviderService;
    
    // Main constructor with AI Provider Service
    public ActionExecutor(
        TicketService tickets,
        MemberService members,
        LabelService labels,
        SessionRegistry sessions,
        AgentRunRegistry runs,
        IEnumerable<IAgentRuntime> runtimes,
        IAgentPromptBuilder promptBuilder,
        AgentRuntimeConfigLoader configLoader,
        CostTracker cost,
        LocalizationService loc,
        ProjectService projects,
        RunStateManager runState,
        ILogger logger,
        AIProviderService? aiProviderService = null)
    {
        _tickets = tickets;
        _members = members;
        _labels = labels;
        _sessions = sessions;
        _runs = runs;
        _runtimes = runtimes;
        _promptBuilder = promptBuilder;
        _configLoader = configLoader;
        _cost = cost;
        _loc = loc;
        _projects = projects;
        _runState = runState;
        _logger = logger;
        _aiProviderService = aiProviderService;
        
        // Initialize AI Provider integration
        InitializeAIProviderService(aiProviderService);
    }

    // Backward-compatible constructor for tests and legacy code paths.
    public ActionExecutor(
        TicketService tickets,
        MemberService members,
        LabelService labels,
        SessionRegistry sessions,
        AgentRunRegistry runs,
        ClaudeRunner runner,
        CostTracker cost,
        LocalizationService loc,
        ProjectService projects,
        RunStateManager runState,
        ILogger logger)
    {
        _tickets = tickets;
        _members = members;
        _labels = labels;
        _sessions = sessions;
        _runs = runs;
        _runtimes = new[] { new ClaudeCodeRuntime(runner) };
        _promptBuilder = new PromptBuilder();
        _configLoader = new BackwardCompatConfigLoader();
        _cost = cost;
        _loc = loc;
        _projects = projects;
        _runState = runState;
        _logger = logger;
        _aiProviderService = null;
    }
}
