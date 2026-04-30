"""Tests for /api/initialize/scenario endpoint."""

from __future__ import annotations

import json
import os

try:
    from app import create_app
except ImportError:  # pragma: no cover
    from backend.app import create_app


def test_scenario_endpoint():
    app = create_app("testing")
    app.config["TESTING"] = True

    with app.test_client() as client:
        response = client.post("/api/initialize/scenario", json={})
        assert response.status_code == 200

        payload = response.get_json()
        assert payload is not None
        assert payload.get("success") is True

        data = payload.get("data", {})
        assert data.get("satellite_count", 0) > 0
        assert data.get("ground_station_count", 0) > 0
        assert data.get("task_count", 0) > 0



def test_scenario_file_exists():
    backend_dir = os.path.dirname(os.path.abspath(__file__))
    possible_paths = [
        os.path.join(backend_dir, "scenarios", "scenario.json"),
        os.path.join(backend_dir, "data", "scenarios", "scenario.json"),
        os.path.join(backend_dir, "scenario.json"),
    ]

    existing = next((p for p in possible_paths if os.path.exists(p)), None)
    assert existing is not None

    with open(existing, "r", encoding="utf-8") as f:
        scenario = json.load(f)

    assert isinstance(scenario, dict)
    assert len(scenario.get("satellites", [])) > 0
    assert len(scenario.get("ground_stations", [])) > 0
    assert len(scenario.get("tasks", [])) > 0
