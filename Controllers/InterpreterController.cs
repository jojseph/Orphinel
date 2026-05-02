using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using orphinel.LexorRuntime;

namespace orphinel.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class InterpreterController : ControllerBase
{
    private readonly LexorExecutionService _executionService;

    public InterpreterController(LexorExecutionService executionService)
    {
        _executionService = executionService;
    }

    [HttpPost("run")]
    public async Task<ActionResult<LexorExecutionResponse>> Run([FromBody] LexorExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Source))
        {
            return BadRequest(new LexorExecutionResponse(
                false,
                string.Empty,
                "Source code is required.",
                new Dictionary<string, LexorVariableSnapshot>()));
        }

        LexorExecutionResponse response = await _executionService.ExecuteAsync(request);
        return Ok(response);
    }
}
