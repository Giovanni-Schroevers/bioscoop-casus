using Microsoft.AspNetCore.Mvc;

namespace BioscoopCasus.API.Controllers;

public class TicketPricingController
{
    [HttpGet("ticketPricing")]
    public IActionResult GetTicketPricing()
    {
        var json = File.ReadAllText("config/ticketPricing.json");
        return new OkObjectResult(json);
    }
}