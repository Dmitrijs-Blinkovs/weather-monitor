using Microsoft.AspNetCore.Mvc;
using WeatherMonitor.Application.FetchLog;

namespace WeatherMonitor.Web.Controllers;

[ApiController]
[Route("api/cities")]
public sealed class CitiesController(IFetchLogReader fetchLogReader) : ControllerBase
{
    /// <summary>Returns every city that has been fetched at least once.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken) =>
        fetchLogReader.GetCitiesAsync(cancellationToken);
}
