using System.ComponentModel.DataAnnotations;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Web.Models;

/// <summary>Query string of <c>GET /api/logs</c>.</summary>
public sealed class FetchLogRequest
{
    public const int DefaultPageSize = 50;

    // The limits are the business rules of FetchLogQuery; validating them here turns a violation into a 400.
    [Range(1, FetchLogQuery.MaxPage)]
    public int Page { get; init; } = 1;

    [Range(1, FetchLogQuery.MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;

    [StringLength(FetchAttempt.CityMaxLength)]
    public string? City { get; init; }

    public FetchStatus? Status { get; init; }

    public FetchLogQuery ToQuery() => new(Page, PageSize, City, Status);
}
