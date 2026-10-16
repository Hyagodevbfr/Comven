using Comven.Application.Interfaces.Services;
using Comven.Domain.Enums;
using Comven.Infrastructure.Providers;

namespace Comven.Api.Development;

/// <summary>
/// Registrado somente em Development. Mantém o comportamento aleatório do
/// fornecedor simulado, exceto quando a requisição envia o cabeçalho
/// <see cref="FailureHeader"/>, caso em que a falha é forçada.
/// </summary>
public class DevelopmentOperationProvider : IOperationProvider
{
    public const string FailureHeader = "X-Force-Provider-Failure";

    private readonly FakeOperationProvider _inner;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DevelopmentOperationProvider(FakeOperationProvider inner, IHttpContextAccessor httpContextAccessor)
    {
        _inner = inner;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> ExecuteAsync(OperationType type, CancellationToken cancellationToken = default)
    {
        var forceFailure = _httpContextAccessor.HttpContext?.Request.Headers[FailureHeader].ToString() == "true";

        if (!forceFailure)
            return await _inner.ExecuteAsync(type, cancellationToken);

        await Task.Delay(300, cancellationToken);
        return false;
    }
}
