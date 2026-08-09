using Microsoft.AspNetCore.Mvc;

namespace KotoDibo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BillSplitController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => StatusCode(501);

    [HttpGet("{id}")]
    public IActionResult GetById(string id) => StatusCode(501);

    [HttpPost]
    public IActionResult Create() => StatusCode(501);
}
