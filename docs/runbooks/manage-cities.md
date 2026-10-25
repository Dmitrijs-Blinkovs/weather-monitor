# Add, remove or change a city

Cities are configuration, not code (`Weather:Cities`, bound to `WeatherIngestionOptions`).

## Steps

1. Find the right query for the provider (with the user's key, or ask the user to open it):
   `https://api.openweathermap.org/data/2.5/weather?q=<City>,<CC>&units=metric&appid=<key>`
   Check `name`, `sys.country` and `coord` in the response - the city must be the intended one.
   Never use a bare name: `q=Rome` resolves to Rome, Georgia, USA (ADR 0005).
2. Edit `src/WeatherMonitor.Functions/appsettings.json`:
   ```json
   "Weather": {
     "Cities": [
       { "Name": "London", "CountryCode": "GB" },
       { "Name": "Rome", "CountryCode": "IT" },
       { "Name": "Riga", "CountryCode": "LV" }
     ]
   }
   ```
   `Name` is what the log, API and UI show; it must be unique (case-insensitive) and at most 100 characters.
   `CountryCode` is ISO 3166 alpha-2. In Azure the same list can be set with app settings
   `Weather__Cities__0__Name`, `Weather__Cities__0__CountryCode`, ...
3. Restart the Functions host. Invalid configuration stops it at startup with the validation message.
4. Trigger a run (`POST http://localhost:7071/admin/functions/FetchWeather`, body `{"input":""}`) and check the fetch
   log and `WeatherSnapshots.Country` for the new city.

## Notes

- Colours: the UI assigns `--series-N` by the alphabetical position in `/api/cities`; up to 8 cities get distinct,
  validated colours, more fall back to a neutral colour (ADR 0010). Adding a city can shift other cities' colours
  only if it sorts before them.
- Removing a city stops new fetches; its history stays in the database and in the chart for past ranges until the
  retention purge removes it (`Retention:Days`, 90).
- Renaming `Name` starts a new series; old rows keep the old name.
- More cities = more calls: free plan limit is 60/minute; each city costs 1 call per minute.
