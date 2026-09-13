namespace NextAZ.Harness.Infrastructure.Imobiliaria;
public interface IImobiliariaAccessTokenProvider
{
    Task<string> GetAsync(Guid tenantId, CancellationToken cancellationToken);
}
