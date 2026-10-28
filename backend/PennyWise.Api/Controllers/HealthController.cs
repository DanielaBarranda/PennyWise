using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.Data;

namespace PennyWise.Api.Controllers;

// GET /api/health: a quick check that the API is running and can open the database.
// On AWS, calling it from EC2 #1 proves the two instances can talk to each other.
[ApiController]
[AllowAnonymous] // Monitoring and the EC2 #1 -> EC2 #2 check work without signing in.
[Route("api/health")]
public class HealthController(PennyWiseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var databaseOk = await db.Database.CanConnectAsync();
        var body = new { status = databaseOk ? "ok" : "degraded", database = databaseOk ? "ok" : "unreachable" };
        return databaseOk ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
