using Comven.Application.Interfaces.Services;
using Comven.Domain.Enums;

namespace Comven.Infrastructure.Providers;

internal class FakeOperationProvider : IOperationProvider
{
    public async Task<bool> ExecuteAsync(OperationType type, CancellationToken cancellationToken = default)
    {
        await Task.Delay(300, cancellationToken);

        return Random.Shared.Next(100) >= 20;
    }
}
