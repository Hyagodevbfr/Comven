
using Comven.Application.DTOs.Requests;
using Comven.Application.DTOs.Responses;
using Comven.Application.Interfaces;
using Comven.Application.Interfaces.Repositories;
using Comven.Domain.Common;
using Comven.Domain.Entities;

namespace Comven.Application.Services;

public class ClientService
{
    private readonly IClientRepository _clients;
    private readonly IUnitOfWork _unitOfWork;

    public ClientService(
        IClientRepository clients,
        IUnitOfWork unitOfWork)
    {
        _clients = clients;
        _unitOfWork = unitOfWork;
    }

    public async Task<DomainResult<ClientResponse>> CreateAsync(
        CreateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var clientResult = Client.Create(
            request.Name,
            request.InitialBalance);

        if (clientResult.IsFailure)
            return DomainResult<ClientResponse>.Failure(
                clientResult.Error!);

        var client = clientResult.Value!;

        await _clients.AddAsync(client, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return DomainResult<ClientResponse>.Success(
            ToResponse(client));
    }

    public async Task<ClientResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var client = await _clients.GetByIdAsync(
            id,
            cancellationToken);

        return client is null
            ? null
            : ToResponse(client);
    }

    private static ClientResponse ToResponse(Client client)
    {
        return new ClientResponse(
            client.Id,
            client.Name,
            client.Balance,
            client.CreatedAt);
    }
}
