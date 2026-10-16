using Comven.Application.DTOs.Requests;
using Comven.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Comven.Api.Controllers;

[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly ClientService _clientService;

    public ClientsController(ClientService clientService)
    {
        _clientService = clientService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest request, CancellationToken cancellationToken
    )
    {
        var result = await _clientService.CreateAsync(request, cancellationToken
        );

        if(result.IsFailure)
            return BadRequest(new ProblemDetails
            {
                Title = "Falha ao criar cliente.",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest
            });

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken
    )
    {
        var client = await _clientService.GetByIdAsync(id, cancellationToken
        );

        if(client == null)
            return NotFound(new ProblemDetails
            {
                Title = "Cliente não encontrado.",
                Status = StatusCodes.Status404NotFound
            });

        return Ok(client);
    }
}