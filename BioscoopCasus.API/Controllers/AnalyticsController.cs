using System.Threading.Tasks;
using BioscoopCasus.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly OccupancyAnalyticsService _occupancyService;
    private readonly RevenueAnalyticsService _revenueService;

    public AnalyticsController(OccupancyAnalyticsService occupancyService, RevenueAnalyticsService revenueService)
    {
        _occupancyService = occupancyService;
        _revenueService = revenueService;
    }

    [HttpGet("occupancy")]
    public async Task<ActionResult<OccupancyAnalyticsSummary>> GetOccupancy(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string scope = "both")
    {
        var result = await _occupancyService.GetOccupancyAsync(startDate, endDate, scope);
        return Ok(result);
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueAnalyticsSummary>> GetRevenue(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string scope = "movie")
    {
        var result = await _revenueService.GetRevenueAsync(startDate, endDate, scope);
        return Ok(result);
    }
}


