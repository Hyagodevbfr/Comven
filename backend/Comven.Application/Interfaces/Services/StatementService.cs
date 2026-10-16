
using Comven.Application.DTOs.Responses;
using Comven.Application.Interfaces.Repositories;
using Comven.Domain.Common;

namespace Comven.Application.Services;

public class StatementService
{
    private readonly IStatementRepository _statements;
    private readonly IClientRepository _clients;

    public StatementService(IStatementRepository statements, IClientRepository clients)
    {
        _statements = statements;
        _clients = clients;
    }

    public async Task<DomainResult<StatementResponse>> GetByPeriodAsync(Guid clientId, DateOnly startDate, DateOnly endDate, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (clientId == Guid.Empty)
            return DomainResult<StatementResponse>.Failure(
                "Cliente inválido.");

        if (startDate > endDate)
            return DomainResult<StatementResponse>.Failure(
                "A data inicial não pode ser maior que a final.");

        if (endDate == DateOnly.MaxValue)
            return DomainResult<StatementResponse>.Failure(
                "Data final inválida.");

        if (page < 1 || pageSize < 1 || pageSize > 100)
            return DomainResult<StatementResponse>.Failure(
                "Parâmetros de paginação inválidos.");

        if ((long)(page - 1) * pageSize > int.MaxValue)
            return DomainResult<StatementResponse>.Failure(
                "Página inválida.");

        var client = await _clients.GetByIdAsync(
            clientId,
            cancellationToken);

        if (client is null)
            return DomainResult<StatementResponse>.Failure(
                "Cliente não encontrado.");

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
            "America/Sao_Paulo");

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            startDate.ToDateTime(TimeOnly.MinValue),
            timeZone);

        var endExclusiveUtc = TimeZoneInfo.ConvertTimeToUtc(
            endDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            timeZone);

        var statement = await _statements.GetByPeriodAsync(
            clientId,
            startUtc,
            endExclusiveUtc,
            page,
            pageSize,
            cancellationToken);

        return DomainResult<StatementResponse>.Success(statement);
    }
}
