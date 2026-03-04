using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BioscoopCasus.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDto loginDto)
    {
        // Simple hardcoded password check for the school project
        if (loginDto.Password != "BioscoopAdmin123")
        {
            return Unauthorized("Invalid password.");
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        // Fallback key if not in config
        var keyString = _configuration["JwtConfig:Secret"] ?? "SuperSecretKeyForBioscoopCasusApiWhichNeedsToBeAtLeast32BytesLong!";
        var key = Encoding.ASCII.GetBytes(keyString);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, "Admin"),
                new Claim(ClaimTypes.Role, "Admin")
            }),
            Expires = DateTime.UtcNow.AddDays(7), // Token valid for 7 days
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new AuthResponseDto(tokenString));
    }
}
