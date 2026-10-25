<#
.SYNOPSIS
    Measures the read API against a seeded load-test database and prints a Markdown report.

.DESCRIPTION
    Every endpoint and range the UI uses is requested -Runs times with a unique extra query parameter, so the output
    cache never answers and each number is a real database round trip (a cached answer is measured separately).
    Reports the median and the slowest time, the bytes on the wire (gzip) and uncompressed, and the rows returned.
    Then fires -Clients concurrent requests of the heavier kinds -Rounds times to check behaviour under load.

.EXAMPLE
    ./tools/load-test/Measure-Api.ps1                       # against Start-LoadTestApi.ps1 on port 5280
    ./tools/load-test/Measure-Api.ps1 -Runs 20 -Clients 64
#>
param(
    [string]$BaseUrl = 'http://127.0.0.1:5280',
    [ValidateRange(1, 100)]
    [int]$Runs = 10,
    [ValidateRange(1, 512)]
    [int]$Clients = 32,
    [ValidateRange(1, 50)]
    [int]$Rounds = 5
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
# Windows PowerShell (.NET Framework) allows two connections per host by default, which would serialise the burst.
[Net.ServicePointManager]::DefaultConnectionLimit = 1024

$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AutomaticDecompression = [Net.DecompressionMethods]::None
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromMinutes(2)
[void]$client.DefaultRequestHeaders.AcceptEncoding.TryParseAdd('gzip')

function Get-Uncached([string]$path) {
    $separator = if ($path.Contains('?')) { '&' } else { '?' }
    return "$BaseUrl$path${separator}probe=$([Guid]::NewGuid().ToString('N'))"
}

function Read-Response($response) {
    $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    $text = if ($response.Content.Headers.ContentEncoding -contains 'gzip') {
        $stream = New-Object IO.Compression.GZipStream((New-Object IO.MemoryStream(, $bytes)), [IO.Compression.CompressionMode]::Decompress)
        (New-Object IO.StreamReader($stream)).ReadToEnd()
    } else {
        [Text.Encoding]::UTF8.GetString($bytes)
    }
    return @{ Status = [int]$response.StatusCode; Wire = $bytes.Length; Text = $text }
}

function Invoke-Timed([string]$url) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $response = $client.GetAsync($url).GetAwaiter().GetResult()
    $result = Read-Response $response
    $clock.Stop()
    $result.Ms = $clock.Elapsed.TotalMilliseconds
    return $result
}

function Get-Rows([string]$text) {
    if (-not $text.StartsWith('{') -and -not $text.StartsWith('[')) { return $text }  # health: Healthy / Degraded
    $json = $text | ConvertFrom-Json
    if ($null -ne $json.cities -and $null -ne $json.cities[0].points) {
        return '{0:n0} points' -f (($json.cities | ForEach-Object { $_.points.Count }) | Measure-Object -Sum).Sum
    }
    if ($null -ne $json.items) { return '{0:n0} of {1:n0}' -f $json.items.Count, $json.totalCount }
    if ($json -is [array]) { return '{0:n0}' -f $json.Count }
    return ''
}

$now = [DateTime]::UtcNow
$to = New-Object DateTime($now.Year, $now.Month, $now.Day, $now.Hour, $now.Minute, 0, [DateTimeKind]::Utc)
$to = $to.AddMinutes(1)
function Range([TimeSpan]$span) { 'from={0:yyyy-MM-ddTHH:mm:ssZ}&to={1:yyyy-MM-ddTHH:mm:ssZ}' -f $to.Subtract($span), $to }

$latestId = ((Invoke-Timed "$BaseUrl/api/logs?page=1&pageSize=1").Text | ConvertFrom-Json).items[0].id
$cases = [ordered]@{
    'Liveness /health/live'                 = '/health/live'
    'Readiness /health (database)'          = '/health'
    'Cities'                                = '/api/cities'
    'History, last hour'                    = "/api/weather/history?$(Range ([TimeSpan]::FromHours(1)))"
    'History, last 6 hours'                 = "/api/weather/history?$(Range ([TimeSpan]::FromHours(6)))"
    'History, last 24 hours'                = "/api/weather/history?$(Range ([TimeSpan]::FromDays(1)))"
    'History, last 7 days'                  = "/api/weather/history?$(Range ([TimeSpan]::FromDays(7)))"
    'History, 31 days (maximum)'            = "/api/weather/history?$(Range ([TimeSpan]::FromDays(31)))"
    'Readings, 24 hours, page 1'            = "/api/weather/readings?$(Range ([TimeSpan]::FromDays(1)))&page=1&pageSize=60"
    'Readings, 31 days, page 1'             = "/api/weather/readings?$(Range ([TimeSpan]::FromDays(31)))&page=1&pageSize=60"
    'Readings, 31 days, page 1,000'         = "/api/weather/readings?$(Range ([TimeSpan]::FromDays(31)))&page=1000&pageSize=60"
    'Fetch log, page 1'                     = '/api/logs?page=1&pageSize=25'
    'Fetch log, page 1,000 x 200'           = '/api/logs?page=1000&pageSize=200'
    'Fetch log, failures of one city'       = '/api/logs?page=1&pageSize=25&city=Rome&status=Failure'
    'Attempt details (payload)'             = "/api/logs/$latestId"
}

Write-Host "Measuring $BaseUrl, $Runs uncached runs per request..."
$report = foreach ($name in $cases.Keys) {
    $warmUp = Invoke-Timed (Get-Uncached $cases[$name])
    if ($warmUp.Status -ne 200) { throw "$name answered HTTP $($warmUp.Status): $($warmUp.Text)" }
    $times = 1..$Runs | ForEach-Object { (Invoke-Timed (Get-Uncached $cases[$name])).Ms } | Sort-Object
    [pscustomobject]@{
        Request = $name
        Median  = $times[[int][Math]::Floor(($times.Count - 1) / 2)]
        Slowest = $times[-1]
        Wire    = $warmUp.Wire
        Raw     = [Text.Encoding]::UTF8.GetByteCount($warmUp.Text)
        Rows    = Get-Rows $warmUp.Text
    }
}

$cachedUrl = "$BaseUrl$($cases['History, 31 days (maximum)'])"
[void](Invoke-Timed $cachedUrl)
$cached = (1..$Runs | ForEach-Object { (Invoke-Timed $cachedUrl).Ms } | Sort-Object)[[int][Math]::Floor(($Runs - 1) / 2)]

$burstPaths = @($cases['History, last 7 days'], $cases['History, 31 days (maximum)'], $cases['Readings, 31 days, page 1'],
    $cases['Fetch log, page 1'], $cases['Fetch log, page 1,000 x 200'])
$statuses = @{}
$burstClock = [Diagnostics.Stopwatch]::StartNew()
for ($round = 0; $round -lt $Rounds; $round++) {
    $tasks = 1..$Clients | ForEach-Object { $client.GetAsync((Get-Uncached $burstPaths[$_ % $burstPaths.Count])) }
    [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks)
    foreach ($task in $tasks) {
        $status = [int]$task.Result.StatusCode
        $statuses[$status] = 1 + [int]$statuses[$status]
        $task.Result.Dispose()
    }
}
$burstClock.Stop()
$total = $Clients * $Rounds

"| Request | Median ms | Slowest ms | On the wire (gzip) | Uncompressed | Rows |"
"|---|---:|---:|---:|---:|---|"
foreach ($row in $report) {
    "| {0} | {1:n0} | {2:n0} | {3:n1} KB | {4:n1} KB | {5} |" -f $row.Request, $row.Median, $row.Slowest, ($row.Wire / 1KB), ($row.Raw / 1KB), $row.Rows
}
""
"Output cache: the same 31-day history request answered from the cache in {0:n1} ms (median)." -f $cached
"Burst: {0} rounds of {1} concurrent uncached requests ({2:n0} in total) took {3:n1} s, {4:n0} requests/s; status codes: {5}." -f `
    $Rounds, $Clients, $total, $burstClock.Elapsed.TotalSeconds, ($total / $burstClock.Elapsed.TotalSeconds), `
    (($statuses.Keys | Sort-Object | ForEach-Object { "$_ x $($statuses[$_])" }) -join ', ')
