using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WeatherMonitor.Application.History;
using WeatherMonitor.Web.Caching;
using WeatherMonitor.Web.Models;

namespace WeatherMonitor.Web.Controllers;

[ApiController]
[Route("api/weather")]
public sealed class WeatherController(IWeatherHistoryService historyService) : ControllerBase
{
    /// <summary>
    /// Returns the temperature series of every city for the given period, together with
    /// each city's country, latest temperature, min/max temperature and last update time.
    /// </summary>
    [HttpGet("history")]
    [OutputCache(PolicyName = ApiCaching.History)]
    [ProducesResponseType<WeatherHistoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<WeatherHistoryDto> GetHistoryAsync(
        [FromQuery] WeatherHistoryRequest request,
        CancellationToken cancellationToken) =>
        historyService.GetHistoryAsync(request.FromUtc, request.ToUtc, cancellationToken);
}
