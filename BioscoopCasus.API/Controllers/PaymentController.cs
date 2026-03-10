using BioscoopCasus.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/payment")]
public class PaymentController : ControllerBase
{
    private readonly BioscoopDbContext _context;
    public PaymentController(BioscoopDbContext context)
    {
        _context = context;
    }

    [HttpPost("pin")]
    public async Task<ActionResult> ValidatePin([FromBody] string pinCode)
    {
        var isValid = await _context.PinCards.AnyAsync(p => p.PinCode == pinCode);
        if (!isValid)
            return BadRequest(new { message = "Ongeldige pincode" });
        return Ok(new {message = "Betaling voltooid" });
    }
}
