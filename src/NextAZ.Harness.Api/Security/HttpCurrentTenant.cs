using System.Collections.Frozen;
using NextAZ.Harness.Application.Abstractions;
namespace NextAZ.Harness.Api.Security;
public sealed class HttpCurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    public Guid TenantId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            var claims = user?.FindAll("tenant_id").ToArray();
            return user?.Identity?.IsAuthenticated == true && claims?.Length == 1 &&
                Guid.TryParse(claims[0].Value, out var id) ? id : Guid.Empty;
        }
    }
    public IReadOnlySet<string> Permissions
    {
        get
        {
            var user = accessor.HttpContext?.User;
            var permissions = new HashSet<string>();
            if (user?.Identity?.IsAuthenticated == true && HasHarnessAccess(user))
            {
                if (user.IsInRole("HarnessAdmin") || user.IsInRole("HarnessOperator"))
                    permissions.UnionWith(["harness.read", "harness.execute"]);
                if (user.IsInRole("HarnessViewer")) permissions.Add("harness.read");
            }
            if (user?.Identity?.IsAuthenticated == true && HasImobiliariaAccess(user))
                permissions.UnionWith(["harness.read", "harness.execute"]);
            if (user?.Identity?.IsAuthenticated == true && HasCartorioAccess(user))
                permissions.UnionWith(["harness.read", "harness.execute"]);
            return permissions.ToFrozenSet();
        }
    }
    private static bool HasHarnessAccess(System.Security.Claims.ClaimsPrincipal user) =>
        user.HasClaim("product", "nextaz-harness") && HarnessSecurity.HasScope(user, "nextaz-harness");
    private static bool HasImobiliariaAccess(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindAll("product").Select(c => c.Value).SequenceEqual(["nextaz-imobiliaria"]) &&
        HarnessSecurity.HasImobiliariaServiceAccess(user);
    private static bool HasCartorioAccess(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindAll("product").Select(c => c.Value).SequenceEqual(["nextaz-cartorio"]) &&
        HarnessSecurity.HasCartorioServiceAccess(user);
}
