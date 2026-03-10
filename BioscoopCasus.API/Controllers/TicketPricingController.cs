using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi.Generated;

namespace BioscoopCasus.API.Controllers;

[Route("api/")]
public class TicketPricingController(IWebHostEnvironment hostingEnvironment)
{
    private readonly IWebHostEnvironment _hostingEnvironment = hostingEnvironment;

   
    [HttpGet("ticketPricing")]
    public IActionResult GetTicketPricing()
    {
        var jsonFilePath = Path.Combine(_hostingEnvironment.WebRootPath, "config", "ticketPricing.json");
        var json = File.ReadAllText(jsonFilePath);
        return new OkObjectResult(json);
    }
}