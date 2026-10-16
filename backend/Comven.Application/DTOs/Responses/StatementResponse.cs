namespace Comven.Application.DTOs.Responses;

public record StatementResponse(IReadOnlyList<StatementItemResponse> Items, int TotalItems, int Page, int PageSize);