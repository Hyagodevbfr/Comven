
using Comven.Application.DTOs.Requests;
using Comven.Application.DTOs.Responses;
using Comven.Application.Services;
using Comven.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Comven.Api.Controllers;

[ApiController]
[Route("api/operations")]
public class OperationsController : ControllerBase
{
    private readonly OperationService _operationService;

    public OperationsController(OperationService operationService)
    {
        _operationService = operationService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateOperationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _operationService.CreateAsync(
            request,
            cancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                "Saldo insuficiente." => StatusCodes.Status409Conflict,
                "A chave de idempotência já foi utilizada para outra operação."
                    => StatusCodes.Status409Conflict,
                "Cliente não encontrado." => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest
            };

            return StatusCode(statusCode, new ProblemDetails
            {
                Title = "Não foi possível criar a operação.",
                Detail = result.Error,
                Status = statusCode
            });
        }

        var operation = ToResponse(result.Value!);

        return CreatedAtAction(
            nameof(GetById),
            new { id = operation.Id },
            operation);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var operation = await _operationService.GetByIdAsync(
            id,
            cancellationToken);

        if (operation is null)
            return NotFound(new ProblemDetails
            {
                Title = "Operação não encontrada.",
                Status = StatusCodes.Status404NotFound
            });

        return Ok(operation);
    }

    private static OperationResponse ToResponse(Operation operation)
    {
        return new OperationResponse(
            operation.Id,
            operation.ClientId,
            operation.Type,
            operation.Amount,
            operation.Status,
            operation.CreatedAt,
            operation.FinishedAt);
    }
}
