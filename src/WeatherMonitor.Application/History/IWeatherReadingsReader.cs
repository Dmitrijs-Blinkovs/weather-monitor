using WeatherMonitor.Application.Paging;

namespace WeatherMonitor.Application.History;

/// <summary>Read side of the individual weather readings, one per successful fetch.</summary>
public interface IWeatherReadingsReader
{
    /// <summary>Returns one page of the readings fetched within the range (inclusive), newest first.</summary>
    Task<PagedResult<WeatherReadingDto>> GetPageAsync(WeatherReadingsQuery query, CancellationToken cancellationToken);
}
