using Microsoft.AspNetCore.Mvc;
using TrainingDirectory.Api.Models;

namespace TrainingDirectory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TraineeController : ControllerBase
{
    private readonly ILogger<TraineeController> _logger;
    public TraineeController(ILogger<TraineeController> logger)
    {
        _logger = logger;
    }
    private static readonly List<Trainee> trainees =
    [
        new Trainee(){FirstName="Yash",LastName="Sharma",Email="yash.sharma@zeuslearning.com",TechStack="HTML,CSS",Status="Active"}, 
        new Trainee(){FirstName="Rheetik",LastName="Sharma",Email="rheetik.sharma@zeuslearning.com",TechStack="HTML,CSS",Status="Active"}, 
        new Trainee(){FirstName="Jay Prakash",LastName="Yadav",Email="jayprakash.yadav@zeuslearning.com",TechStack="HTML,CSS",Status="Active"}
    ];

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        _logger.LogInformation("Api called at {timestamp}", DateTime.UtcNow);
        return Ok(trainees);
    }
}
