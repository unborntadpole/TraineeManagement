using Microsoft.AspNetCore.Mvc;

namespace TrainingDirectory.Api.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class HealthController : ControllerBase
{

    [HttpGet(Name = "GetHealth")]
    public IActionResult GetHealth()
    {
        return Ok(
            new {
                
                status = "running", 
                application = "Trainee Management API", 
                timestamp = DateTime.UtcNow 
            }
        );
    }
}