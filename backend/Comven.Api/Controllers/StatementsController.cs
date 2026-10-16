
using Comven.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Comven.Api.Controllers;

[ApiController]
[Route("api/clients/{clientId:guid}/statement")]
public class StatementsController : ControllerBase
{
    private readonly StatementService _statementService;

    public StatementsController(StatementService statementService)
    {
        _statementService = statementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetByPeriod(
        Guid clientId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _statementService.GetByPeriodAsync(
            clientId,
            startDate,
            endDate,
            page,
            pageSize,
            cancellationToken);

        if (result.IsFailure)
        {
            var statusCode = result.Error == "Cliente não encontrado."
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return StatusCode(statusCode, new ProblemDetails
            {
                Title = "Não foi possível consultar o extrato.",
                Detail = result.Error,
                Status = statusCode
            });
        }

        return Ok(result.Value);
    }
}
