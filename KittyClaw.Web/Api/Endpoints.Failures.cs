using KittyClaw.Core.Automation;

namespace KittyClaw.Web.Api;

public static partial class Endpoints
{
    private static void MapFailureLogbook(RouteGroupBuilder api)
    {
        // Get all failures for a project
        api.MapGet("/projects/{slug}/failures", async (string slug, FailureLogStore store, bool? unresolved) =>
        {
            var entries = unresolved == true
                ? await store.UnresolvedForProjectAsync(slug)
                : await store.ForProjectAsync(slug);
            return Results.Ok(entries.Select(e => new
            {
                e.Id,
                e.TicketId,
                e.Kind,
                e.Message,
                e.RequiredAction,
                e.RunId,
                e.CreatedAt,
                e.Resolved,
                e.ResolvedAt,
            }));
        }).WithTags("Failures");

        // Get failures for a specific ticket
        api.MapGet("/projects/{slug}/tickets/{id:int}/failures", async (string slug, int id, FailureLogStore store) =>
        {
            var entries = await store.ForTicketAsync(slug, id);
            return Results.Ok(entries.Select(e => new
            {
                e.Id,
                e.Kind,
                e.Message,
                e.RequiredAction,
                e.RunId,
                e.CreatedAt,
                e.Resolved,
                e.ResolvedAt,
            }));
        }).WithTags("Failures");

        // Get latest unresolved failure for a ticket
        api.MapGet("/projects/{slug}/tickets/{id:int}/failures/latest", async (string slug, int id, FailureLogStore store) =>
        {
            var entry = await store.LatestUnresolvedAsync(slug, id);
            return entry is null ? Results.NotFound() : Results.Ok(new
            {
                entry.Id,
                entry.Kind,
                entry.Message,
                entry.RequiredAction,
                entry.RunId,
                entry.CreatedAt,
            });
        }).WithTags("Failures");

        // Mark a failure as resolved
        api.MapPost("/projects/{slug}/failures/{failureId}/resolve", async (string slug, string failureId, FailureLogStore store) =>
        {
            var ok = await store.ResolveAsync(slug, failureId);
            return ok ? Results.Ok(new { resolved = true }) : Results.NotFound();
        }).WithTags("Failures");

        // Clear all failures for a ticket
        api.MapDelete("/projects/{slug}/tickets/{id:int}/failures", async (string slug, int id, FailureLogStore store) =>
        {
            await store.ClearForTicketAsync(slug, id);
            return Results.NoContent();
        }).WithTags("Failures");
    }
}