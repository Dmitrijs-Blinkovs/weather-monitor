using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Web.Caching;

namespace WeatherMonitor.Web.Controllers;

[ApiController]
[Route("api/cities")]
public sealed class CitiesController(IFetchLogReader fetchLogReader) : ControllerBase
{
    /// <summary>Returns every city that has been fetched at least once.</summary>
    [HttpGet]
    [OutputCache(PolicyName = ApiCaching.Cities)]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken) =>
        fetchLogReader.GetCitiesAsync(cancellationToken);
}
