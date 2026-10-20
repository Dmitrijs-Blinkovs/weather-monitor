using System.ComponentModel.DataAnnotations;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Web.Models;

/// <summary>Query string of <c>GET /api/logs</c>.</summary>
public sealed class FetchLogRequest
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    /// <summary>
    /// Deepest page that can be requested. Deep OFFSET paging costs the database more with every page; older entries
    /// are reached by filtering (city, result) instead. Must match MAX_LOG_PAGE in the client.
    /// </summary>
    public const int MaxPage = 1000;

    [Range(1, MaxPage)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;

    [StringLength(FetchAttempt.CityMaxLength)]
    public string? City { get; init; }

    public FetchStatus? Status { get; init; }

    public FetchLogQuery ToQuery() => new(Page, PageSize, City, Status);
}
