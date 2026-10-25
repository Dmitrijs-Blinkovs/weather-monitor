using System.ComponentModel.DataAnnotations;
using WeatherMonitor.Application.Ingestion;

namespace WeatherMonitor.Application.Tests.Ingestion;

public sealed class WeatherIngestionOptionsTests
{
    [Fact]
    public void The_three_default_cities_are_valid()
    {
        Validate(Options(("London", "GB"), ("Rome", "IT"), ("Riga", "LV"))).ShouldBeEmpty();
    }

    [Fact]
    public void The_country_code_is_optional()
    {
        Validate(Options(("London", null))).ShouldBeEmpty();
    }

    [Fact]
    public void At_least_one_city_is_required()
    {
        Validate(new WeatherIngestionOptions()).ShouldNotBeEmpty();
    }

    [Fact]
    public void Blank_city_names_are_rejected()
    {
        Validate(Options(("London", "GB"), (" ", "IT")))
            .ShouldContain(result => result.ErrorMessage!.Contains("must not be empty", StringComparison.Ordinal));
    }

    [Fact]
    public void Duplicate_city_names_are_rejected_regardless_of_case()
    {
        Validate(Options(("Riga", "LV"), ("riga", "LV")))
            .ShouldContain(result => result.ErrorMessage!.Contains("unique", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ITA")]
    [InlineData("I")]
    [InlineData("1T")]
    public void Country_codes_must_be_two_letters(string countryCode)
    {
        Validate(Options(("Rome", countryCode)))
            .ShouldContain(result => result.ErrorMessage!.Contains("two-letter", StringComparison.Ordinal));
    }

    private static WeatherIngestionOptions Options(params (string Name, string? CountryCode)[] cities) =>
        new() { Cities = [.. cities.Select(city => new CityOptions { Name = city.Name, CountryCode = city.CountryCode })] };

    private static List<ValidationResult> Validate(WeatherIngestionOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
