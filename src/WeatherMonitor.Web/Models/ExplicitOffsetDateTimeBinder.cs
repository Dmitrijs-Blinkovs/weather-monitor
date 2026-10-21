using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WeatherMonitor.Web.Models;

/// <summary>
/// Binds an ISO 8601 timestamp only when it states its offset (<c>Z</c> or <c>+hh:mm</c>). The default binder reads a
/// value without one as the server's local time, so the same request would mean different moments on different servers.
/// </summary>
internal sealed partial class ExplicitOffsetDateTimeBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.CompletedTask; // [Required] reports a missing value.
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value, value);
        if (!ExplicitOffset().IsMatch(value)
            || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"'{value}' must be an ISO 8601 date and time with a time zone, e.g. 2026-10-19T08:00:00Z or 2026-10-19T10:00:00+02:00.");
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(parsed);
        return Task.CompletedTask;
    }

    [GeneratedRegex(@"T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOffset();
}
