using FinCore.BuildingBlocks.Application.Security;
using FinCore.BuildingBlocks.Web.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FinCore.BuildingBlocks.Web.Authorization;

/// <summary>
/// Bearer authentication for the API (spec 002). Staff and customers sign in at two different identity pools
/// (Keycloak realms locally, Cognito user pools on AWS), so the API runs one JWT validator per pool and a policy
/// scheme picks the validator from the token's issuer. A customer token can therefore never be validated as staff.
/// </summary>
public static class FinCoreAuthentication
{
    public const string Scheme = "FinCore";
    public const string StaffScheme = "Staff";
    public const string CustomerScheme = "Customers";

    /// <summary>Policy for endpoints any signed-in staff member may call (e.g. business date).</summary>
    public const string StaffPolicy = "fincore:staff";

    public static IServiceCollection AddFinCoreAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var staff = AuthPool.Read(configuration, StaffScheme);
        var customers = AuthPool.Read(configuration, CustomerScheme);
        var requireHttps = configuration.GetValue("Auth:RequireHttpsMetadata", true);

        services.AddAuthentication(Scheme)
            .AddPolicyScheme(Scheme, "Staff or customer bearer token", o =>
                o.ForwardDefaultSelector = ctx => SelectScheme(ctx, customers.Issuer))
            .AddJwtBearer(StaffScheme, o => Configure(o, staff, requireHttps))
            .AddJwtBearer(CustomerScheme, o => Configure(o, customers, requireHttps));

        services.AddAuthorizationBuilder()
            .AddPolicy(StaffPolicy, p => p.RequireAuthenticatedUser().RequireClaim(FinCoreClaimTypes.Kind, nameof(UserKind.Staff)));

        return services;
    }

    /// <summary>Requires a signed-in staff member without naming a specific permission.</summary>
    public static TBuilder RequireStaff<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(StaffPolicy);

    // Reads the issuer without validating the token; the selected scheme then performs the full validation.
    // Unknown or missing issuers fall through to the staff validator, which rejects them.
    private static string SelectScheme(HttpContext context, string? customerIssuer)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (customerIssuer is not null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var handler = new JsonWebTokenHandler();
            var raw = header["Bearer ".Length..].Trim();
            if (handler.CanReadToken(raw)
                && string.Equals(handler.ReadJsonWebToken(raw).Issuer.TrimEnd('/'), customerIssuer, StringComparison.Ordinal))
            {
                return CustomerScheme;
            }
        }

        return StaffScheme;
    }

    private static void Configure(JwtBearerOptions options, AuthPool pool, bool requireHttps)
    {
        options.Authority = pool.Authority;
        options.Audience = pool.Audience;
        options.RequireHttpsMetadata = requireHttps;
        // Keep JWT claim names as issued ("sub", "email"); the legacy mapping renames them to long SOAP URIs.
        options.MapInboundClaims = false;
        if (pool.Issuer is not null)
        {
            options.TokenValidationParameters.ValidIssuer = pool.Issuer;
        }
    }

    /// <summary>Settings of one identity pool, from <c>Auth:Staff</c> or <c>Auth:Customers</c>.</summary>
    public sealed record AuthPool(string? Authority, string? Audience, string? Issuer)
    {
        public static AuthPool Read(IConfiguration configuration, string name)
        {
            var section = configuration.GetSection($"Auth:{name}");
            var authority = section["Authority"];
            // The issuer is the authority URL for Keycloak and Cognito; tests override it explicitly.
            var issuer = (section["Issuer"] ?? authority)?.TrimEnd('/');
            return new AuthPool(authority, section["Audience"], string.IsNullOrEmpty(issuer) ? null : issuer);
        }
    }
}
