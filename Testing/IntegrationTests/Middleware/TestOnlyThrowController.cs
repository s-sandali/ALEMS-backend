using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationTests.Middleware;

[ApiController]
[AllowAnonymous]
[Route("api/test-only/throw")]
public class TestOnlyThrowController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        throw new InvalidOperationException("intentional test-only uncaught exception");
    }
}