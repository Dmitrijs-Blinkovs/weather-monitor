"""Deployment smoke test: every configured city must be fetched after deployment completes.

/health alone is not enough: right after a deployment it can still be Healthy from fetches made by the previous
release. So this waits until /health reports Healthy AND every city in the shipped configuration has a successful fetch
started after deployment completed. The watermark must be captured after deploying Functions;
otherwise the previous worker can satisfy the check while the packages are being uploaded.
This is a post-deployment freshness check, not an attestation of the running binary.

Usage: python smoke_test.py <web-url> <deployment-completed-utc, e.g. 2026-10-21T09:00:00Z> <functions appsettings.json>
"""

import json
import os
import sys
import time
import urllib.error
import urllib.request

# 20 x 15 s = 5 minutes; overridable for a quick local run.
ATTEMPTS = int(os.environ.get("SMOKE_TEST_ATTEMPTS", "20"))
PAUSE_SECONDS = int(os.environ.get("SMOKE_TEST_PAUSE_SECONDS", "15"))


def get(url: str) -> str:
    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            return response.read().decode("utf-8")
    except (urllib.error.URLError, TimeoutError) as error:
        return f"<{error}>"


def without_fraction(timestamp: str) -> str:
    """'2026-10-21T09:02:00.123Z' -> '2026-10-21T09:02:00Z', so ISO strings compare correctly as text."""
    return timestamp.split(".")[0].rstrip("Z") + "Z"


def cities_without_new_success(web_url: str, since: str, cities: list[str]) -> list[str]:
    body = get(f"{web_url}/api/logs?status=Success&pageSize=200")
    try:
        items = json.loads(body).get("items", [])
    except json.JSONDecodeError:
        return cities
    fresh = {item["city"] for item in items if without_fraction(item["attemptedAtUtc"]) > since}
    return [city for city in cities if city not in fresh]


def main() -> int:
    web_url, since, config_path = sys.argv[1].rstrip("/"), without_fraction(sys.argv[2]), sys.argv[3]
    with open(config_path, encoding="utf-8") as config:
        cities = [city["Name"] for city in json.load(config)["Weather"]["Cities"]]

    for attempt in range(1, ATTEMPTS + 1):
        health = get(f"{web_url}/health").strip()
        missing = cities_without_new_success(web_url, since, cities)
        if health == "Healthy" and not missing:
            print(f"Healthy, and {', '.join(cities)} fetched after {since} (attempt {attempt}).")
            return 0
        print(f"Attempt {attempt}: health {health or '<no response>'}; no new success yet for {', '.join(missing) or '-'}")
        if attempt < ATTEMPTS:
            time.sleep(PAUSE_SECONDS)

    print(f"::error::The new release did not report Healthy with a new successful fetch for every city within "
          f"{ATTEMPTS * PAUSE_SECONDS} s")
    return 1


if __name__ == "__main__":
    sys.exit(main())
