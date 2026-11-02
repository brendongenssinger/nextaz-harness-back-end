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

    public static bool CanGenerateWhatsAppTemplate(ClaimsPrincipal user, IConfiguration configuration)
    {
        var tenants = user.FindAll("tenant_id").ToArray();
        var clients = user.FindAll("client_id").ToArray();
        var subjects = user.FindAll("sub").ToArray();
        var authorizedParties = user.FindAll("azp").ToArray();
        var allowed = configuration.GetSection("Authentication:WhatsAppTemplateClientIds").Get<string[]>() ?? [];
        return user.Identity?.IsAuthenticated == true && tenants.Length == 1 &&
            Guid.TryParse(tenants[0].Value, out var tenant) && tenant != Guid.Empty &&
            clients.Length == 1 && subjects.Length == 1 && authorizedParties.Length == 1 &&
            allowed.Contains(clients[0].Value, StringComparer.Ordinal) &&
            string.Equals(subjects[0].Value, clients[0].Value, StringComparison.Ordinal) &&
            string.Equals(authorizedParties[0].Value, clients[0].Value, StringComparison.Ordinal) &&
            user.HasClaim("product", "funcionario-digital") &&
            user.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains("whatsapp.templates.generate", StringComparer.Ordinal);
    }

    public static bool CanClassifyReceipt(ClaimsPrincipal user, IConfiguration configuration)
    {
        var tenants = user.FindAll("tenant_id").ToArray();
        var clients = user.FindAll("client_id").ToArray();
        var subjects = user.FindAll("sub").ToArray();
        var authorizedParties = user.FindAll("azp").ToArray();
        var allowed = configuration.GetSection("Authentication:ReceiptClassifierClientIds").Get<string[]>() ?? [];
        return user.Identity?.IsAuthenticated == true && user.IsInRole("Service") && tenants.Length == 1 &&
            Guid.TryParse(tenants[0].Value, out var tenant) && tenant != Guid.Empty &&
            clients.Length == 1 && subjects.Length == 1 && authorizedParties.Length == 1 &&
            allowed.Contains(clients[0].Value, StringComparer.Ordinal) &&
            string.Equals(subjects[0].Value, clients[0].Value, StringComparison.Ordinal) &&
            string.Equals(authorizedParties[0].Value, clients[0].Value, StringComparison.Ordinal) &&
            user.HasClaim("product", "funcionario-digital") &&
            user.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Contains("receipts.classify", StringComparer.Ordinal);
    }
}
