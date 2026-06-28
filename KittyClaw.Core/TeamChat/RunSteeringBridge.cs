using KittyClaw.Core.Automation;

namespace KittyClaw.Core.TeamChat;

public class RunSteeringBridge : IRunSteeringBridge
{
    private readonly AgentRunRegistry _runRegistry;

    public RunSteeringBridge(AgentRunRegistry runRegistry)
    {
        _runRegistry = runRegistry;
    }

    public async Task<SteerResult> SendToRunAsync(string runId, string message, CancellationToken ct = default)
    {
        var run = _runRegistry.Get(runId);
        if (run is null)
            return new SteerResult(false, "Run not found", null);

        if (run.Status != AgentRunStatus.Running)
            return new SteerResult(false, "Run is not active", null);

        try
        {
            await run.SteeringQueue.Writer.WriteAsync(message, ct);
            return new SteerResult(true, null, run.RunId);
        }
        catch (Exception ex)
        {
            return new SteerResult(false, ex.Message, null);
        }
    }

    public StopResult StopRun(string runId)
    {
        var run = _runRegistry.Get(runId);
        if (run is null)
            return new StopResult(false, "Run not found");

        if (run.Status != AgentRunStatus.Running)
            return new StopResult(false, "Run is not active");

        try
        {
            run.Cancellation.Cancel();
            return new StopResult(true, null);
        }
        catch (Exception ex)
        {
            return new StopResult(false, ex.Message);
        }
    }

    public bool IsSteeringSupported(string runId)
    {
        var run = _runRegistry.Get(runId);
        return run is not null && run.Status == AgentRunStatus.Running;
    }
}
