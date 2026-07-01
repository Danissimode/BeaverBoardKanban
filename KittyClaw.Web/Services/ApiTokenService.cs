using System;
using System.Security.Cryptography;
using System.Text;
using KittyClaw.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KittyClaw.Web.Services;

/// <summary>
/// Simple API token authentication for IDE/integration access.
///
/// Tokens are stored in settings.json as a SHA256 hash (never plain text).
/// Supports simple scopes: read, write, execute, admin.
///
/// Tokens are managed via the SettingsService and verified here by hash comparison.
/// </summary>
public sealed class ApiTokenService
{
    private readonly SettingsService _settings;
    private readonly ILogger<ApiTokenService>? _logger;

    public ApiTokenService(SettingsService settings, ILogger<ApiTokenService>? logger = null)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Verifies a bearer token and returns its scopes, or null if invalid.
    /// </summary>
    public ApiTokenResult? VerifyToken(string? bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
            return null;

        // Strip "Bearer " prefix
        var token = bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? bearerToken["Bearer ".Length..]
            : bearerToken;

        // Empty token = invalid
        if (string.IsNullOrWhiteSpace(token))
            return null;

        // Load settings synchronously (small JSON, cached)
        var settings = _settings.LoadSync();
        if (string.IsNullOrWhiteSpace(settings.ApiTokenHash))
            return null;

        var hash = HashToken(token);
        if (!string.Equals(hash, settings.ApiTokenHash, StringComparison.Ordinal))
            return null;

        _logger?.LogDebug("API token verified for scopes: {Scopes}", settings.ApiTokenScopes ?? "read");
        return new ApiTokenResult
        {
            TokenHash = hash,
            Scopes = ParseScopes(settings.ApiTokenScopes ?? "read"),
        };
    }

    /// <summary>
    /// Generates a new random API token and returns both the raw token (shown once) and the hash.
    /// Call this to provision a new token for an IDE.
    /// </summary>
    public (string Raw, string Hash) GenerateToken()
    {
        var raw = $"{Guid.NewGuid():N}{Guid.NewGuid():N}"[..48]; // 48-char random
        return (raw, HashToken(raw));
    }

    /// <summary>
    /// Computes SHA256 hash of a token for storage.
    /// </summary>
    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static ApiScopes ParseScopes(string scopes)
    {
        var result = ApiScopes.None;
        foreach (var s in scopes.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = s.Trim().ToLowerInvariant();
            result |= trimmed switch
            {
                "read" => ApiScopes.Read,
                "write" => ApiScopes.Write,
                "execute" => ApiScopes.Execute,
                "admin" => ApiScopes.Admin,
                _ => ApiScopes.None,
            };
        }
        return result;
    }
}

public sealed class ApiTokenResult : IBindableFromHttpContext<ApiTokenResult>
{
    public required string TokenHash { get; init; }
    public ApiScopes Scopes { get; init; }

    public static ValueTask<ApiTokenResult?> BindAsync(HttpContext context, System.Reflection.ParameterInfo parameter)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        var apiTokenService = context.RequestServices.GetRequiredService<ApiTokenService>();
        var result = apiTokenService.VerifyToken(authHeader);
        return ValueTask.FromResult(result);
    }
}

[Flags]
public enum ApiScopes
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    Admin = 8,
}
