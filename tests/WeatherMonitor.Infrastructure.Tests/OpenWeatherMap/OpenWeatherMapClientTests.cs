using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using WeatherMonitor.Infrastructure.OpenWeatherMap;

namespace WeatherMonitor.Infrastructure.Tests.OpenWeatherMap;

public sealed class OpenWeatherMapClientTests
{
    private const string ApiKey = "test-api-key-123";

    // Trimmed real response of GET /data/2.5/weather?q=Riga&units=metric
    private const string RigaResponse = """
        {"coord":{"lon":24.1059,"lat":56.946},"weather":[{"id":803,"main":"Clouds","description":"broken clouds","icon":"04d"}],
         "base":"stations","main":{"temp":8.42,"feels_like":6.1,"temp_min":7.84,"temp_max":9.06,"pressure":1012,"humidity":81},
         "visibility":10000,"wind":{"speed":4.12,"deg":230},"clouds":{"all":75},"dt":1792400400,
         "sys":{"type":2,"id":2021354,"country":"LV","sunrise":1792386000,"sunset":1792421000},
         "timezone":10800,"id":456172,"name":"Riga","cod":200}
        """;

    [Fact]
    public async Task Requests_current_weather_in_metric_units_with_the_api_key()
    {
        var handler = Respond(HttpStatusCode.OK, RigaResponse);

        await CreateClient(handler).GetCurrentWeatherAsync("Rome", null, TestContext.Current.CancellationToken);

        var uri = handler.RequestedUris.Single();
        uri.AbsolutePath.ShouldBe("/data/2.5/weather");
        uri.Query.ShouldContain("q=Rome");
        uri.Query.ShouldContain("units=metric");
        uri.Query.ShouldContain($"appid={ApiKey}");
    }

    [Fact]
    public async Task The_country_code_is_appended_to_disambiguate_the_city()
    {
        var handler = Respond(HttpStatusCode.OK, RigaResponse);

        await CreateClient(handler).GetCurrentWeatherAsync("Rome", "IT", TestContext.Current.CancellationToken);

        handler.RequestedUris.Single().Query.ShouldContain("q=Rome%2CIT");
    }

    [Fact]
    public async Task City_names_are_escaped_in_the_query_string()
    {
        var handler = Respond(HttpStatusCode.OK, RigaResponse);

        await CreateClient(handler).GetCurrentWeatherAsync("New York&x=1", null, TestContext.Current.CancellationToken);

        handler.RequestedUris.Single().Query.ShouldContain("q=New%20York%26x%3D1");
    }

    [Fact]
    public async Task A_successful_response_is_parsed_and_the_raw_payload_is_kept_verbatim()
    {
        var result = await CreateClient(Respond(HttpStatusCode.OK, RigaResponse))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.HttpStatusCode.ShouldBe(200);
        result.Weather.Country.ShouldBe("LV");
        result.Weather.Temperature.ShouldBe(8.42);
        result.Weather.TemperatureMin.ShouldBe(7.84);
        result.Weather.TemperatureMax.ShouldBe(9.06);
        result.Weather.ObservedAtUtc.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1792400400).UtcDateTime);
        result.Weather.ObservedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        result.Weather.RawPayload.ShouldBe(RigaResponse);
    }

    [Fact]
    public async Task An_error_status_is_returned_as_a_failure_with_the_providers_message()
    {
        const string body = """{"cod":401, "message": "Invalid API key. Please see https://openweathermap.org/faq#error401 for more info."}""";

        var result = await CreateClient(Respond(HttpStatusCode.Unauthorized, body))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.HttpStatusCode.ShouldBe(401);
        result.Error.ShouldStartWith("Invalid API key.");
    }

    [Fact]
    public async Task An_error_without_a_json_body_falls_back_to_the_reason_phrase()
    {
        var result = await CreateClient(Respond(HttpStatusCode.BadGateway, "<html>bad gateway</html>"))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.HttpStatusCode.ShouldBe(502);
        result.Error.ShouldBe("Bad Gateway");
    }

    [Fact]
    public async Task A_response_missing_required_fields_is_a_failure()
    {
        var result = await CreateClient(Respond(HttpStatusCode.OK, """{"main":{"temp":5},"dt":1792400400}"""))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.HttpStatusCode.ShouldBe(200);
        result.Error.ShouldContain("missing");
    }

    [Fact]
    public async Task A_response_that_is_not_json_is_a_failure()
    {
        var result = await CreateClient(Respond(HttpStatusCode.OK, "not json"))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldContain("not valid JSON");
    }

    [Fact]
    public async Task A_network_error_is_a_failure_and_never_leaks_the_api_key()
    {
        var handler = new StubHttpMessageHandler(request =>
            throw new HttpRequestException($"Connection refused ({request.RequestUri})"));

        var result = await CreateClient(handler).GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.HttpStatusCode.ShouldBeNull();
        result.Error.ShouldStartWith("Request failed: Connection refused");
        result.Error.ShouldNotContain(ApiKey);
        result.Error.ShouldContain("appid=***");
    }

    private static StubHttpMessageHandler Respond(HttpStatusCode statusCode, string body) =>
        new(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    private static OpenWeatherMapClient CreateClient(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.openweathermap.org/") },
            Options.Create(new OpenWeatherMapOptions { ApiKey = ApiKey }));
}
