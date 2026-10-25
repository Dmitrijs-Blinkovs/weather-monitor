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
    public async Task An_error_keeps_its_response_body_with_the_api_key_redacted()
    {
        var body = $"<html>Proxy error for /data/2.5/weather?q=Riga&appid={ApiKey}</html>";

        var result = await CreateClient(Respond(HttpStatusCode.BadGateway, body))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.ResponseBody.ShouldBe("<html>Proxy error for /data/2.5/weather?q=Riga&appid=***</html>");
    }

    [Fact]
    public async Task A_successful_status_with_unusable_data_keeps_the_body_that_could_not_be_used()
    {
        const string body = """{"dt":1792400400,"main":{"temp":5,"temp_min":7,"temp_max":3},"sys":{"country":"LV"}}""";

        var result = await CreateClient(Respond(HttpStatusCode.OK, body))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.ResponseBody.ShouldBe(body);
    }

    [Fact]
    public async Task A_successful_fetch_carries_no_failure_body()
    {
        var result = await CreateClient(Respond(HttpStatusCode.OK, RigaResponse))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.ResponseBody.ShouldBeNull();
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
    public async Task An_error_with_an_empty_message_keeps_its_status_and_falls_back_to_the_reason_phrase()
    {
        var result = await CreateClient(Respond(HttpStatusCode.InternalServerError, """{"cod":500,"message":" "}"""))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.HttpStatusCode.ShouldBe(500);
        result.Error.ShouldBe("Internal Server Error");
    }

    [Fact]
    public async Task An_error_without_any_text_is_described_by_its_status_code()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            ReasonPhrase = string.Empty,
            Content = new StringContent(string.Empty),
        });

        var result = await CreateClient(handler).GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.HttpStatusCode.ShouldBe(503);
        result.Error.ShouldBe("HTTP 503");
    }

    [Fact]
    public async Task A_timeout_from_the_resilience_pipeline_is_a_failure_without_status()
    {
        var handler = new StubHttpMessageHandler(_ =>
            throw new Polly.Timeout.TimeoutRejectedException("The operation didn't complete within the allowed timeout of '00:00:10'."));

        var result = await CreateClient(handler).GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.HttpStatusCode.ShouldBeNull();
        result.Error.ShouldContain("allowed timeout");
    }

    [Fact]
    public async Task An_http_client_timeout_is_a_failure_without_status()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."));

        var result = await CreateClient(handler).GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("Request timed out");
    }

    [Fact]
    public async Task Cancellation_by_the_caller_is_not_turned_into_a_failure()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(() =>
            CreateClient(handler).GetCurrentWeatherAsync("Riga", null, cancellation.Token));
    }

    [Fact]
    public async Task A_successful_response_that_is_not_json_is_a_failure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>captive portal</html>", Encoding.UTF8, "text/html"),
        });

        var result = await CreateClient(handler).GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("Unexpected content type 'text/html'");
    }

    [Theory]
    [InlineData("""{"dt":1792400400,"main":{"temp":250,"temp_min":1,"temp_max":2},"sys":{"country":"LV"}}""")]
    [InlineData("""{"dt":1792400400,"main":{"temp":5,"temp_min":7,"temp_max":3},"sys":{"country":"LV"}}""")]
    public async Task Implausible_temperatures_are_a_failure_instead_of_data(string body)
    {
        var result = await CreateClient(Respond(HttpStatusCode.OK, body))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldStartWith("Implausible temperatures");
    }

    [Theory]
    [InlineData("http://api.openweathermap.org/", false)]
    [InlineData("not a url", false)]
    [InlineData("https://api.openweathermap.org/", true)]
    public void The_base_url_must_use_https_because_the_key_is_in_the_query_string(string baseUrl, bool valid)
    {
        var options = new OpenWeatherMapOptions { BaseUrl = baseUrl, ApiKey = ApiKey };

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            options, new System.ComponentModel.DataAnnotations.ValidationContext(options), results, validateAllProperties: true);

        isValid.ShouldBe(valid);
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
    public async Task An_observation_time_outside_the_date_range_is_a_failure_not_an_exception()
    {
        const string body = """{"dt":999999999999999,"main":{"temp":5,"temp_min":4,"temp_max":6},"sys":{"country":"LV"}}""";

        var result = await CreateClient(Respond(HttpStatusCode.OK, body))
            .GetCurrentWeatherAsync("Riga", null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe("Observation time out of range: dt 999999999999999");
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
        result.ResponseBody.ShouldBeNull();
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
