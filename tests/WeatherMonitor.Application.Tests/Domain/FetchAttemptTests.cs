using WeatherMonitor.Domain;

namespace WeatherMonitor.Application.Tests.Domain;

public sealed class FetchAttemptTests
{
    [Fact]
    public void Succeeded_attempt_owns_a_snapshot_with_the_extracted_fields_and_the_full_payload()
    {
        var weather = TestData.Weather(temperature: 9.3, country: "LV");

        var attempt = FetchAttempt.Succeeded("Riga", TestData.Now, TimeSpan.FromMilliseconds(123.7), 200, weather);

        attempt.IsSuccess.ShouldBeTrue();
        attempt.HttpStatusCode.ShouldBe(200);
        attempt.DurationMs.ShouldBe(123);
        attempt.ErrorMessage.ShouldBeNull();

        var snapshot = attempt.Snapshot.ShouldNotBeNull();
        snapshot.City.ShouldBe("Riga");
        snapshot.Country.ShouldBe("LV");
        snapshot.Temperature.ShouldBe(9.3);
        snapshot.TemperatureMin.ShouldBe(weather.TemperatureMin);
        snapshot.TemperatureMax.ShouldBe(weather.TemperatureMax);
        snapshot.ObservedAtUtc.ShouldBe(weather.ObservedAtUtc);
        snapshot.FetchedAtUtc.ShouldBe(TestData.Now);
        snapshot.RawPayload.ShouldBe(weather.RawPayload);
    }

    [Fact]
    public void Failed_attempt_has_no_snapshot_and_keeps_the_error()
    {
        var attempt = FetchAttempt.Failed("Rome", TestData.Now, TimeSpan.FromSeconds(1), 401, "Invalid API key");

        attempt.IsSuccess.ShouldBeFalse();
        attempt.HttpStatusCode.ShouldBe(401);
        attempt.ErrorMessage.ShouldBe("Invalid API key");
        attempt.Snapshot.ShouldBeNull();
    }

    [Fact]
    public void Failed_attempt_truncates_an_overly_long_error_message()
    {
        var longMessage = new string('x', FetchAttempt.ErrorMessageMaxLength + 50);

        var attempt = FetchAttempt.Failed("Rome", TestData.Now, TimeSpan.Zero, null, longMessage);

        attempt.ErrorMessage.ShouldNotBeNull().Length.ShouldBe(FetchAttempt.ErrorMessageMaxLength);
    }

    [Fact]
    public void Control_characters_in_an_error_are_replaced_so_they_cannot_forge_log_lines()
    {
        var attempt = FetchAttempt.Failed("Rome", TestData.Now, TimeSpan.Zero, 500, "bad\r\nFAKE LOG LINE\u0007 ");

        attempt.ErrorMessage.ShouldBe("bad  FAKE LOG LINE");
    }

    [Fact]
    public void An_error_of_only_control_characters_is_stored_as_unknown()
    {
        FetchAttempt.Failed("Rome", TestData.Now, TimeSpan.Zero, 500, "\u0001\u0002 \u0007")
            .ErrorMessage.ShouldBe("Unknown error");
    }

    [Fact]
    public void Timestamps_without_a_kind_are_rejected_because_their_time_zone_is_unknown()
    {
        var unspecified = DateTime.SpecifyKind(TestData.Now, DateTimeKind.Unspecified);

        Should.Throw<ArgumentException>(() =>
            FetchAttempt.Failed("Rome", unspecified, TimeSpan.Zero, null, "error"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_city_is_required(string city)
    {
        Should.Throw<ArgumentException>(() =>
            FetchAttempt.Failed(city, TestData.Now, TimeSpan.Zero, null, "error"));
    }

    [Fact]
    public void A_negative_duration_is_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            FetchAttempt.Failed("Rome", TestData.Now, TimeSpan.FromSeconds(-1), null, "error"));
    }
}
