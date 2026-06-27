namespace KittyClaw.Core.Automation;

/// <summary>
/// Execution modes for AI agent runs.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Legacy ClaudeRunner path - existing behavior
    /// </summary>
    LegacyClaude,
    
    /// <summary>
    /// Direct OpenCode execution without CAO governance
    /// </summary>
    DirectOpenCode,
    
    /// <summary>
    /// CAO-governed execution (stub for future implementation)
    /// </summary>
    CaoGoverned,
    
    /// <summary>
    /// Team workflow execution (stub for future implementation)
    /// </summary>
    TeamWorkflow,
    
    /// <summary>
    /// Manual execution mode
    /// </summary>
    Manual
}
