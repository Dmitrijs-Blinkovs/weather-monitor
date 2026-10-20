using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using WeatherMonitor.Domain;
using WeatherMonitor.Infrastructure.Persistence;

namespace WeatherMonitor.Infrastructure.Tests.Persistence;

/// <summary>
/// The columns and the domain's validation must describe the same data: a value the domain accepts always fits its
/// column, so the database never truncates or rejects what the business rules let through. A new text column fails
/// here until its length is decided together with the rule that guards it.
/// </summary>
public sealed class DataModelContractTests
{
    private static readonly IModel Model = CreateSqlServerModel();

    [Fact]
    public void Every_text_column_has_the_length_the_domain_validates()
    {
        var expected = new Dictionary<string, int?>
        {
            ["FetchAttempt.City"] = FetchAttempt.CityMaxLength,
            ["FetchAttempt.ErrorMessage"] = FetchAttempt.ErrorMessageMaxLength,
            ["FetchAttempt.ResponseBody"] = null, // nvarchar(max); the domain cuts it at ResponseBodyMaxLength
            ["WeatherSnapshot.City"] = FetchAttempt.CityMaxLength,
            ["WeatherSnapshot.Country"] = CurrentWeather.CountryCodeLength,
            ["WeatherSnapshot.RawPayload"] = null, // nvarchar(max); the domain rejects more than RawPayloadMaxLength
        };

        var actual = Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties()
                .Where(property => property.ClrType == typeof(string))
                .Select(property => (Key: $"{entity.ClrType.Name}.{property.Name}", Length: property.GetMaxLength())))
            .ToDictionary(column => column.Key, column => column.Length);

        actual.ShouldBe(expected, ignoreOrder: true);
    }

    [Fact]
    public void Times_keep_the_full_date_time_range_and_temperatures_keep_double_precision()
    {
        // datetime would start in 1753 and round to 3 ms; real would lose digits of a double.
        var properties = Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()).ToList();

        properties.Where(property => property.ClrType == typeof(DateTime))
            .ShouldAllBe(property => property.GetColumnType() == "datetime2");
        properties.Where(property => property.ClrType == typeof(double))
            .ShouldAllBe(property => property.GetColumnType() == "float");
    }

    [Fact]
    public void The_country_code_is_stored_as_exactly_two_ascii_characters()
    {
        var country = Model.FindEntityType(typeof(WeatherSnapshot))!.FindProperty(nameof(WeatherSnapshot.Country))!;

        country.GetColumnType().ShouldBe("char(2)");
    }

    private static IModel CreateSqlServerModel()
    {
        var options = new DbContextOptionsBuilder<WeatherDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;
        using var context = new WeatherDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
