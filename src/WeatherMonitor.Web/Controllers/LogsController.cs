using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Web.Caching;
using WeatherMonitor.Web.Models;

namespace WeatherMonitor.Web.Controllers;

[ApiController]
[Route("api/logs")]
public sealed class LogsController(IFetchLogReader fetchLogReader) : ControllerBase
{
    /// <summary>Returns a page of fetch attempts (successful and failed), newest first.</summary>
    [HttpGet]
    [OutputCache(PolicyName = ApiCaching.FetchLog)]
    [ProducesResponseType<PagedResult<FetchLogEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<FetchLogEntryDto>> GetAsync(
        [FromQuery] FetchLogRequest request,
        CancellationToken cancellationToken) =>
        fetchLogReader.GetPageAsync(request.ToQuery(), cancellationToken);
}
