using Comven.Application.DTOs.Responses;

namespace Comven.Application.Interfaces.Repositories;

public interface IStatementRepository
{
    Task<StatementResponse> GetByPeriodAsync(Guid clientId, DateTime startUtc, DateTime endExclusiveUtc, int page, int pageSize, CancellationToken cancellationToken = default);
}