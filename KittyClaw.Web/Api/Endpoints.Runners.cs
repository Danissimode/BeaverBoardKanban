using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KittyClaw.Core.Automation;
using KittyClaw.Core.Automation.Runners;
using KittyClaw.Core.Services;

namespace KittyClaw.Web.Api;

public static partial class Endpoints
{
    /// <summary>
    /// Runner management endpoints
    /// </summary>
    private static void MapRunners(RouteGroupBuilder api)
    {
        // GET /api/runners - List all runners with availability
        api.MapGet("/runners", async (RunnerAvailabilityChecker checker) =>
        {
            var reports = await checker.CheckAllAsync();
            return Results.Ok(reports);
        }).WithTags("Runners");

        // GET /api/runners/health - Quick health check for all runners
        api.MapGet("/runners/health", async (RunnerAvailabilityChecker checker) =>
        {
            var reports = await checker.CheckAllAsync();
            var health = reports.Select(r => new
            {
                kind = r.RunnerKind,
                displayName = r.DisplayName,
                available = r.IsAvailable,
                version = r.Version,
                provider = r.Provider,
                model = r.Model,
                error = r.Error,
                isDefault = r.IsDefault,
                isRecommended = r.IsRecommended
            });
            return Results.Ok(health);
        }).WithTags("Runners");

        // GET /api/runners/default - Get current default runner
        api.MapGet("/runners/default", (RunnerRegistry registry) =>
        {
            var def = registry.GetDefaultRunner();
            return Results.Ok(new
            {
                kind = def.Kind,
                displayName = def.DisplayName,
                available = def.IsAvailable
            });
        }).WithTags("Runners");

        // POST /api/runners/default - Set default runner
        api.MapPost("/runners/default", async (
            SetDefaultRunnerRequest req,
            RunnerRegistry registry,
            SettingsService settings) =>
        {
            var runner = registry.GetRunner(req.Kind);
            if (runner is null)
            {
                return Results.BadRequest(new { error = $"Runner '{req.Kind}' not found" });
            }
            
            registry.SetDefaultRunner(req.Kind);
            
            // Persist preference
            var data = await settings.LoadAsync();
            data.PreferredRunner = req.Kind;
            await settings.SaveAsync(data);
            
            return Results.Ok(new { kind = runner.Kind, displayName = runner.DisplayName });
        }).WithTags("Runners");

        // GET /api/runners/{kind} - Get specific runner details
        api.MapGet("/runners/{kind}", async (string kind, RunnerRegistry registry, RunnerAvailabilityChecker checker) =>
        {
            var runner = registry.GetRunner(kind);
            if (runner is null)
            {
                return Results.NotFound(new { error = $"Runner '{kind}' not found" });
            }
            
            var report = await checker.CheckAsync(runner);
            return Results.Ok(report);
        }).WithTags("Runners");

        // GET /api/runners/recommended - Get the recommended runner for new projects
        api.MapGet("/runners/recommended", async (RunnerAvailabilityChecker checker) =>
        {
            var runner = await checker.GetRecommendedRunnerAsync();
            if (runner is null)
            {
                return Results.NotFound(new { error = "No available runners found" });
            }
            
            var report = await checker.CheckAsync(runner);
            return Results.Ok(report);
        }).WithTags("Runners");
    }
    
    /// <summary>
    /// Global run management endpoints (across all projects)
    /// </summary>
    private static void MapGlobalRuns(RouteGroupBuilder api)
    {
        // GET /api/runs - List all active runs across all projects
        api.MapGet("/runs", (AgentRunRegistry reg) =>
        {
            var active = reg.AllForProject("")
                .Where(r => r.Status == AgentRunStatus.Running)
                .ToList();
            // Also get from any registered run (in-memory)
            var allActive = reg.AllForProject("__all__")
                .Where(r => r.Status == AgentRunStatus.Running)
                .ToList();
            
            return Results.Ok(new
            {
                count = active.Count + allActive.Count,
                runs = active.Concat(allActive).Select(r => new
                {
                    r.RunId, r.ProjectSlug, r.AgentName, r.TicketId,
                    r.StartedAt, r.RunnerKind, status = r.Status.ToString()
                })
            });
        }).WithTags("GlobalRuns");

        // GET /api/runs/{runId} - Get run by ID (global, no project scope)
        api.MapGet("/runs/{runId}", (string runId, AgentRunRegistry reg) =>
        {
            var run = reg.Get(runId);
            if (run is null) return Results.NotFound(new { error = "Run not found" });
            return Results.Ok(new
            {
                run.RunId, run.ProjectSlug, run.AgentName, run.SkillFile,
                run.TicketId, run.ConcurrencyGroup, run.StartedAt, run.EndedAt,
                run.SessionId, run.ExitCode, run.RunnerKind,
                status = run.Status.ToString(),
                events = run.SnapshotBuffer()
            });
        }).WithTags("GlobalRuns");

        // GET /api/runs/{runId}/stream - SSE stream for run events (global)
        api.MapGet("/runs/{runId}/stream", async (string runId, string? since, HttpContext http, AgentRunRegistry reg, CancellationToken ct) =>
        {
            var run = reg.Get(runId);
            if (run is null) { http.Response.StatusCode = 404; return; }
            
            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers["X-Accel-Buffering"] = "no";

            DateTime? sinceUtc = null;
            if (!string.IsNullOrWhiteSpace(since)
                && DateTime.TryParse(since, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                sinceUtc = parsed.ToUniversalTime();
            }

            var queue = System.Threading.Channels.Channel.CreateUnbounded<StreamEvent>();
            void handler(StreamEvent ev) => queue.Writer.TryWrite(ev);
            run.OnEvent += handler;

            try
            {
                foreach (var ev in run.SnapshotBuffer())
                {
                    if (sinceUtc is not null && ev.At <= sinceUtc.Value) continue;
                    await WriteSseAsync(http.Response, ev, ct);
                }

                while (!ct.IsCancellationRequested && run.Status == AgentRunStatus.Running)
                {
                    while (queue.Reader.TryRead(out var ev))
                        await WriteSseAsync(http.Response, ev, ct);
                    try { await Task.Delay(200, ct); } catch { break; }
                }
                while (queue.Reader.TryRead(out var ev))
                    await WriteSseAsync(http.Response, ev, ct);
                await WriteSseRawAsync(http.Response, "event: end\ndata: {}\n\n", ct);
            }
            finally { run.OnEvent -= handler; }
        }).WithTags("GlobalRuns");

        // POST /api/runs/{runId}/stop - Stop a run (global)
        api.MapPost("/runs/{runId}/stop", async (string runId, StopRunRequest? req, AgentRunRegistry reg, RunnerRegistry runners) =>
        {
            var run = reg.Get(runId);
            if (run is null) return Results.NotFound(new { error = "Run not found" });
            
            if (run.Status != AgentRunStatus.Running)
                return Results.BadRequest(new { error = "Run is not active." });
            
            // Try to stop via runner
            var runner = runners.GetRunner(run.RunnerKind);
            if (runner is not null)
            {
                await runner.StopAsync(runId, CancellationToken.None);
            }
            
            run.Cancellation.Cancel();
            reg.Complete(runId, AgentRunStatus.Stopped, null);
            
            return Results.Ok(new { runId, status = "stopped", reason = req?.Reason });
        }).WithTags("GlobalRuns");

        // POST /api/runs/{runId}/steer - Send steering message (global)
        api.MapPost("/runs/{runId}/steer", async (string runId, SteerRunRequest req, AgentRunRegistry reg) =>
        {
            var run = reg.Get(runId);
            if (run is null) return Results.NotFound(new { error = "Run not found" });
            
            if (run.Status != AgentRunStatus.Running)
                return Results.BadRequest(new { error = "Run is not active." });
            
            await run.SteeringQueue.Writer.WriteAsync(req.Text);
            return Results.Ok(new { runId, messageSent = true });
        }).WithTags("GlobalRuns");
    }
}

/// <summary>
/// Request to set the default runner
/// </summary>
public sealed record SetDefaultRunnerRequest(string Kind);

/// <summary>
/// DTOs for runner responses
/// </summary>
public sealed record RunnerHealthDto(string Kind, string DisplayName, bool Available, string? Version, string? Error);
