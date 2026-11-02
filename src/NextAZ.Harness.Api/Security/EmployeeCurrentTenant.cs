using System.Collections.Frozen;
using NextAZ.Harness.Application.Abstractions;
namespace NextAZ.Harness.Api.Security;
public sealed class EmployeeCurrentTenant(HttpCurrentTenant inner, IHttpContextAccessor accessor, IConfiguration configuration) : ICurrentTenant
{
    public Guid TenantId => inner.TenantId;
    public IReadOnlySet<string> Permissions
    {
        get
        {
            var permissions = inner.Permissions.ToHashSet(StringComparer.Ordinal);
            if (accessor.HttpContext?.User is { } user && HarnessServiceClaims.CanDraft(user, configuration))
                permissions.UnionWith(["harness.execute", "harness.employee-drafts"]);
            if (accessor.HttpContext?.User is { } templateUser &&
                HarnessServiceClaims.CanGenerateWhatsAppTemplate(templateUser, configuration))
                permissions.UnionWith(["harness.execute", "whatsapp.templates.generate"]);
            if (accessor.HttpContext?.User is { } receiptUser &&
                HarnessServiceClaims.CanClassifyReceipt(receiptUser, configuration))
                permissions.UnionWith(["harness.execute", "receipts.classify"]);
            return permissions.ToFrozenSet();
        }
    }
}
