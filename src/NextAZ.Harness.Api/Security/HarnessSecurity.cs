using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
namespace NextAZ.Harness.Api.Security;
public static class HarnessSecurity
{
    public static IServiceCollection AddHarnessSecurity(this IServiceCollection services, IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var authority = configuration["Authentication:Authority"];
        var audience = configuration["Authentication:Audience"];
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) ||
            string.IsNullOrWhiteSpace(audience) ||
            (issuer.Scheme != "https" && !(environment.IsDevelopment() && issuer.IsLoopback)))
            throw new InvalidOperationException("Configure a trusted Auth authority and a Harness-specific audience.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.Audience = audience;
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                ValidateIssuerSigningKey = true, RequireSignedTokens = true, RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "sub", RoleClaimType = "role"
            };
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("HarnessExecute", policy => HarnessPolicy(policy)
                .RequireRole("HarnessAdmin", "HarnessOperator"));
            options.AddPolicy("HarnessRead", policy => HarnessPolicy(policy)
                .RequireRole("HarnessAdmin", "HarnessOperator", "HarnessViewer"));
            options.AddPolicy("HarnessWrite", policy => HarnessPolicy(policy)
                .RequireRole("HarnessAdmin"));
            options.AddPolicy("ImobiliariaExecute", ImobiliariaPolicy);
            options.AddPolicy("ImobiliariaRead", ImobiliariaPolicy);
            options.AddPolicy("CartorioExecute", CartorioPolicy);
            options.AddPolicy("CartorioRead", CartorioPolicy);
            options.FallbackPolicy = TenantPolicy(new AuthorizationPolicyBuilder()).Build();
        });
        return services;
    }
    private static AuthorizationPolicyBuilder HarnessPolicy(AuthorizationPolicyBuilder policy) =>
        ProductPolicy(policy, "nextaz-harness", "nextaz-harness");
    private static void ImobiliariaPolicy(AuthorizationPolicyBuilder policy) =>
        TenantPolicy(policy).RequireAssertion(context => HasSingleClaim(context.User, "product", "nextaz-imobiliaria") &&
            HasImobiliariaServiceAccess(context.User));
    private static void CartorioPolicy(AuthorizationPolicyBuilder policy) =>
        TenantPolicy(policy).RequireAssertion(context => HasSingleClaim(context.User, "product", "nextaz-cartorio") &&
            HasCartorioServiceAccess(context.User));
    private static AuthorizationPolicyBuilder ProductPolicy(AuthorizationPolicyBuilder policy,
        string product, string scope) => TenantPolicy(policy)
            .RequireAssertion(context => HasSingleClaim(context.User, "product", product))
            .RequireAssertion(context => HasScope(context.User, scope));
    private static AuthorizationPolicyBuilder TenantPolicy(AuthorizationPolicyBuilder policy) =>
        policy.RequireAuthenticatedUser().RequireAssertion(context =>
        {
            var tenants = context.User.FindAll("tenant_id").ToArray();
            return tenants.Length == 1 && Guid.TryParse(tenants[0].Value, out var tenantId) && tenantId != Guid.Empty;
        });
    internal static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scope) =>
        user.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
    internal static bool HasImobiliariaServiceAccess(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole("Service") && HasScope(user, "imobiliaria.atendimento") &&
        HasSingleClaim(user, "sub", "nextaz-imobiliaria-harness") &&
        HasSingleClaim(user, "client_id", "nextaz-imobiliaria-harness") &&
        HasSingleClaim(user, "azp", "nextaz-imobiliaria-harness");
    internal static bool HasCartorioServiceAccess(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole("Service") && HasScope(user, "cartorio.atendimento") &&
        HasSingleClaim(user, "sub", "nextaz-cartorio-harness") &&
        HasSingleClaim(user, "client_id", "nextaz-cartorio-harness") &&
        HasSingleClaim(user, "azp", "nextaz-cartorio-harness");
    private static bool HasSingleClaim(System.Security.Claims.ClaimsPrincipal user, string type, string value)
    {
        var claims = user.FindAll(type).ToArray();
        return claims.Length == 1 && string.Equals(claims[0].Value, value, StringComparison.Ordinal);
    }
}
