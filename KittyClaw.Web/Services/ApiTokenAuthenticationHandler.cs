using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace KittyClaw.Web.Services;

/// <summary>
/// Authenticates requests via the Authorization: Bearer <token> header.
/// Calls ApiTokenService.VerifyToken to validate the token against the stored hash.
/// On success, populates the ClaimsPrincipal with token scopes for downstream authorization.
/// </summary>
public sealed class ApiTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ApiTokenService _apiTokenService;

    public ApiTokenAuthenticationHandler(
        ApiTokenService apiTokenService,
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _apiTokenService = apiTokenService;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var result = _apiTokenService.VerifyToken(authHeader);
        if (result is null)
        {
            Logger.LogWarning("API token authentication failed: invalid or missing token");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API token"));
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "api"),
            new Claim("tokenHash", result.TokenHash),
        };

        foreach (ApiScopes scope in Enum.GetValues<ApiScopes>())
        {
            if (scope != ApiScopes.None && result.Scopes.HasFlag(scope))
            {
                claims.Add(new Claim("scope", scope.ToString().ToLowerInvariant()));
            }
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogDebug("API token authenticated successfully for token hash {HashPrefix}",
            result.TokenHash[..8]);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
