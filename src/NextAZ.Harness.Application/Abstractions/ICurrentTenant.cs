namespace NextAZ.Harness.Application.Abstractions;
// Implemented at each trusted entry point, never populated from request metadata.
public interface ICurrentTenant
{
    Guid TenantId { get; }
    IReadOnlySet<string> Permissions { get; }
}
