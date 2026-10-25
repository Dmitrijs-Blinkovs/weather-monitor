using System.ComponentModel.DataAnnotations;
using WeatherMonitor.Application.History;
using WeatherMonitor.Application.Paging;

namespace WeatherMonitor.Web.Models;

/// <summary>Query string of <c>GET /api/weather/readings</c>.</summary>
public sealed class WeatherReadingsRequest : TimeRangeRequest
{
    public const int DefaultPageSize = 50;

    // The limits are the business rules of PageLimits; validating them here turns a violation into a 400.
    [Range(1, PageLimits.MaxPage)]
    public int Page { get; init; } = 1;

    [Range(1, PageLimits.MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;

    public WeatherReadingsQuery ToQuery() => new(FromUtc, ToUtc, Page, PageSize);
}
