using Claims.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ClaimsDbContext _dbContext;

    public HealthController(ClaimsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var canConnect = await _dbContext.Database.CanConnectAsync();

        return Ok(new
        {
            status = canConnect ? "Healthy" : "Degraded",
            database = canConnect ? "Connected" : "Disconnected",
            service = "Claims Management API",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            timestamp = DateTime.UtcNow
        });
    }
}
