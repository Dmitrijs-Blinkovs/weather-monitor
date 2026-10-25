using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Tests.Domain;

public sealed class CurrentWeatherTests
{
    [Theory]
    [InlineData(250, 1, 2)]
    [InlineData(5, -150, 6)]
    [InlineData(5, 7, 3)]
    [InlineData(double.NaN, 1, 2)]
    public void Implausible_temperatures_cannot_form_an_observation(double temperature, double min, double max)
    {
        Should.Throw<ArgumentException>(() => new CurrentWeather("LV", temperature, min, max, TestData.Now, "{}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TOO-LONG-CODE")]
    public void A_country_code_is_required_and_never_silently_truncated(string country)
    {
        Should.Throw<ArgumentException>(() => new CurrentWeather(country, 5, 4, 6, TestData.Now, "{}"));
    }

    [Theory]
    [InlineData("L")]
    [InlineData("LVA")]
    [InlineData("L1")]
    [InlineData("ÉS")]
    public void The_country_must_be_an_iso_3166_alpha_2_code(string country)
    {
        Should.Throw<ArgumentException>(() => new CurrentWeather(country, 5, 4, 6, TestData.Now, "{}"));
    }

    [Fact]
    public void The_country_code_is_stored_in_upper_case()
    {
        new CurrentWeather("lv", 5, 4, 6, TestData.Now, "{}").Country.ShouldBe("LV");
    }

    [Fact]
    public void An_observation_time_without_a_time_zone_is_rejected()
    {
        var unspecified = DateTime.SpecifyKind(TestData.Now, DateTimeKind.Unspecified);

        Should.Throw<ArgumentException>(() => new CurrentWeather("LV", 5, 4, 6, unspecified, "{}"));
    }

    [Fact]
    public void A_payload_above_the_size_limit_is_rejected_rather_than_stored_partially()
    {
        var payload = new string('x', (64 * 1024) + 1);

        Should.Throw<ArgumentException>(() => new CurrentWeather("LV", 5, 4, 6, TestData.Now, payload));
    }

    [Fact]
    public void The_raw_payload_is_required()
    {
        Should.Throw<ArgumentException>(() => new CurrentWeather("LV", 5, 4, 6, TestData.Now, " "));
    }

    [Fact]
    public void A_provider_gets_the_reason_instead_of_an_exception()
    {
        var created = CurrentWeather.TryCreate("LV", 5, 7, 3, TestData.Now, "{}", out var weather, out var error);

        created.ShouldBeFalse();
        weather.ShouldBeNull();
        error.ShouldBe("Implausible temperatures: temp 5, temp_min 7, temp_max 3");
    }

    [Fact]
    public void Plausible_values_form_an_observation()
    {
        var weather = new CurrentWeather("LV", -3.5, -4, -3, TestData.Now, "{}");

        weather.Country.ShouldBe("LV");
        weather.Temperature.ShouldBe(-3.5);
        weather.ObservedAtUtc.ShouldBe(TestData.Now);
    }
}
