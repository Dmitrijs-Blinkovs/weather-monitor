"""Regression tests for the deployment freshness check; no network or Azure required."""

import contextlib
import io
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

import smoke_test


class SmokeTestTests(unittest.TestCase):
    cities = ["London", "Rome", "Riga"]
    completed = "2026-10-22T10:02:00Z"

    def logs_at(self, timestamp, cities=None):
        return json.dumps({"items": [
            {"city": city, "attemptedAtUtc": timestamp}
            for city in (self.cities if cities is None else cities)
        ]})

    def test_old_release_fetches_during_deployment_do_not_pass(self):
        # Deployment started at 10:00; the old worker fetched at 10:01 while Web was being deployed.
        with patch.object(smoke_test, "get", return_value=self.logs_at("2026-10-22T10:01:00Z")):
            self.assertEqual(smoke_test.cities_without_new_success("unused", self.completed, self.cities), self.cities)

    def test_all_cities_fetched_after_completion_pass(self):
        with patch.object(smoke_test, "get", return_value=self.logs_at("2026-10-22T10:03:00.123Z")):
            self.assertEqual(smoke_test.cities_without_new_success("unused", self.completed, self.cities), [])

    def test_one_missing_city_does_not_pass(self):
        with patch.object(smoke_test, "get", return_value=self.logs_at("2026-10-22T10:03:00Z", ["London", "Riga"])):
            self.assertEqual(smoke_test.cities_without_new_success("unused", self.completed, self.cities), ["Rome"])

    def test_same_second_is_conservatively_rejected(self):
        with patch.object(smoke_test, "get", return_value=self.logs_at("2026-10-22T10:02:00.999Z")):
            self.assertEqual(smoke_test.cities_without_new_success("unused", self.completed, self.cities), self.cities)

    def test_unavailable_api_does_not_pass(self):
        with patch.object(smoke_test, "get", return_value="<connection refused>"):
            self.assertEqual(smoke_test.cities_without_new_success("unused", self.completed, self.cities), self.cities)

    def test_healthy_alone_does_not_pass_and_unhealthy_with_fresh_data_does_not_pass(self):
        config = Path(__file__).resolve().parents[2] / "src/WeatherMonitor.Functions/appsettings.json"
        for health, timestamp in [("Healthy", "2026-10-22T10:01:00Z"), ("Degraded", "2026-10-22T10:03:00Z")]:
            with self.subTest(health=health), patch.object(sys, "argv", ["smoke_test.py", "unused", self.completed, str(config)]), \
                    patch.object(smoke_test, "ATTEMPTS", 1), \
                    patch.object(smoke_test, "get", side_effect=[health, self.logs_at(timestamp)]), \
                    contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(smoke_test.main(), 1)

    def test_workflow_takes_the_watermark_after_both_deployments(self):
        # Guard the workflow wiring too: testing only the Python comparison misses an early watermark.
        workflow = (Path(__file__).resolve().parents[1] / "workflows/deploy.yml").read_text(encoding="utf-8")
        web = workflow.index("azd deploy web")
        functions = workflow.index("azd deploy functions")
        watermark = workflow.index('echo "DEPLOY_COMPLETED_AT=')
        smoke = workflow.index('python3 .github/scripts/smoke_test.py "$WEB_URL" "$DEPLOY_COMPLETED_AT"')
        self.assertLess(web, functions)
        self.assertLess(functions, watermark)
        self.assertLess(watermark, smoke)


if __name__ == "__main__":
    unittest.main()
