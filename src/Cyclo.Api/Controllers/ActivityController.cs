using Microsoft.AspNetCore.Mvc;

namespace Cyclo.Api.Controllers;

[ApiController]
[Route("activities")]
public class ActivityController : ControllerBase
{
    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id)
    {
        return NotFound();
    }
}