using System.ComponentModel.DataAnnotations;
using WeatherMonitor.Application.FetchLog;
using WeatherMonitor.Domain;

namespace WeatherMonitor.Web.Models;

/// <summary>Query string of <c>GET /api/logs</c>.</summary>
public sealed class FetchLogRequest
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    /// <summary>Largest page whose row offset ((page - 1) * pageSize) still fits in an <see cref="int"/>.</summary>
    public const int MaxPage = int.MaxValue / MaxPageSize;

    [Range(1, MaxPage)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;

    [StringLength(FetchAttempt.CityMaxLength)]
    public string? City { get; init; }

    public FetchStatus? Status { get; init; }

    public FetchLogQuery ToQuery() => new(Page, PageSize, City, Status);
}
