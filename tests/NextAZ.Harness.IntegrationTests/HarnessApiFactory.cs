using Microsoft.AspNetCore.DataProtection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
namespace NextAZ.Harness.IntegrationTests;
public sealed class HarnessApiFactory : WebApplicationFactory<Program>
{
    public static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public const string Issuer = "https://auth.harness.test";
    private readonly RSA _rsa = RSA.Create(2048);
    private RsaSecurityKey Key => new(_rsa) { KeyId = "test-only" };
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Harness:Simulation:Enabled", "true");
        builder.UseSetting("Harness:Simulation:TenantIds:0", TenantA.ToString());
        builder.UseSetting("Harness:Simulation:TenantIds:1", TenantB.ToString());
        builder.UseSetting("Harness:Imobiliaria:Enabled", "true");
        builder.UseSetting("Harness:Imobiliaria:TenantIds:0", TenantA.ToString());
        builder.UseSetting("Harness:Imobiliaria:TenantIds:1", TenantB.ToString());
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = Issuer,
            ["Authentication:Audience"] = "nextaz-harness-api",
            ["Harness:Simulation:Enabled"] = "true",
            ["Harness:Simulation:TenantIds:0"] = TenantA.ToString(),
            ["Harness:Simulation:TenantIds:1"] = TenantB.ToString(),
            ["Harness:Imobiliaria:Enabled"] = "true",
            ["Harness:Imobiliaria:TenantIds:0"] = TenantA.ToString(),
            ["Harness:Imobiliaria:TenantIds:1"] = TenantB.ToString()
        }));
        builder.ConfigureTestServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                options.Configuration.SigningKeys.Add(Key);
                options.ConfigurationManager = new Microsoft.IdentityModel.Protocols.StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
                options.TokenValidationParameters.ValidIssuer = Issuer;
                options.TokenValidationParameters.ValidAudience = "nextaz-harness-api";
            }));
    }
    public string Token(Guid? tenant = null, string role = "HarnessOperator", string product = "nextaz-harness",
        string audience = "nextaz-harness-api", string issuer = Issuer, string scope = "nextaz-harness",
        bool expired = false, bool wrongSignature = false, bool duplicateTenant = false,
        string? clientId = null, string? azp = null, string subject = "test-user")
    {
        var claims = new List<Claim> { new("sub", subject), new("role", role), new("product", product), new("scope", scope) };
        if (clientId is not null) claims.Add(new("client_id", clientId));
        if (azp is not null) claims.Add(new("azp", azp));
        if (tenant.HasValue) claims.Add(new("tenant_id", tenant.Value.ToString()));
        if (duplicateTenant) claims.Add(new("tenant_id", TenantB.ToString()));
        var credentials = new SigningCredentials(wrongSignature
            ? new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test-only" } : Key, SecurityAlgorithms.RsaSha256);
        var now = DateTime.UtcNow;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience, claims,
            now.AddMinutes(-10), expired ? now.AddMinutes(-5) : now.AddMinutes(5), credentials));
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _rsa.Dispose();
    }
}
