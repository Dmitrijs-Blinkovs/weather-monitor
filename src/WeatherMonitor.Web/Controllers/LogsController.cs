using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Application.Paging;
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

    /// <summary>
    /// Returns one attempt with the provider's full response body (the payload of a success, the body of a failure),
    /// loaded only when the UI asks for it.
    /// </summary>
    [HttpGet("{id:long:min(1)}")]
    [OutputCache(PolicyName = ApiCaching.AttemptDetails)]
    [ProducesResponseType<FetchAttemptDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FetchAttemptDetailsDto>> GetDetailsAsync(long id, CancellationToken cancellationToken) =>
        await fetchLogReader.GetDetailsAsync(id, cancellationToken) is { } details ? details : NotFound();
}
