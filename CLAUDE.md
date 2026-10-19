@AGENTS.md

## Claude Code specifics

- Project skills live in `.claude/skills/` (`run-locally`, `verify-changes`, `manage-cities`, `add-ef-migration`,
  `add-api-endpoint`, `troubleshoot-ingestion`). Use the matching skill instead of improvising the procedure.
- `.claude/launch.json` defines preview servers `azurite`, `functions` and `web` for the desktop app's preview tools.
- Never read, print or ask for the OpenWeatherMap API key. Check that it is set with
  `dotnet user-secrets list --project src/WeatherMonitor.Functions`, and report only whether
  `OpenWeatherMap:ApiKey` is present - never its value.
- Do not create or change Azure resources, commit or push unless the user explicitly asks for it.
