using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("register")]
    public IActionResult Register() => StatusCode(501);

    [HttpPost("login")]
    public IActionResult Login() => StatusCode(501);
}
