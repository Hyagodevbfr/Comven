namespace Comven.Application.DTOs.Responses;

public record ClientResponse(Guid Id, string Name, decimal Balance, DateTime CreatedAt);