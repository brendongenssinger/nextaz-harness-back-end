using System.Security.Claims;
namespace NextAZ.Harness.Api.Security;
public static class HarnessServiceClaims
{
    public static bool CanDraft(ClaimsPrincipal user, IConfiguration configuration)
    {
        var tenants = user.FindAll("tenant_id").ToArray();
        var clients = user.FindAll("client_id").ToArray();
        var allowed = configuration.GetSection("Authentication:EmployeeDraftClientIds").Get<string[]>() ?? [];
        return user.Identity?.IsAuthenticated == true && tenants.Length == 1 &&
            Guid.TryParse(tenants[0].Value, out var tenant) && tenant != Guid.Empty &&
            clients.Length == 1 && allowed.Contains(clients[0].Value, StringComparer.Ordinal) &&
            user.HasClaim("product", "funcionario-digital") &&
            user.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains("harness.employee-drafts", StringComparer.Ordinal);
    }
}
