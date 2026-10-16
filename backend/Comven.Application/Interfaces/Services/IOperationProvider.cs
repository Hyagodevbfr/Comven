using Comven.Domain.Enums;

namespace Comven.Application.Interfaces.Services;

public interface IOperationProvider
{
    Task<bool> ExecuteAsync(OperationType type, CancellationToken cancellationToken = default);
}
