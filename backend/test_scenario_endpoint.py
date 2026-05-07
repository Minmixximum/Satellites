"""Tests for /api/initialize/scenario endpoint."""

from __future__ import annotations

import json
import os

try:
    from app import create_app
    from data.bootstrap import MAX_GROUND_STATIONS, MAX_TASKS
except ImportError:  # pragma: no cover
    from backend.app import create_app
    from backend.data.bootstrap import MAX_GROUND_STATIONS, MAX_TASKS


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


def test_reference_seed_counts():
    app = create_app("testing")

    ground_stations = app.db_manager.get_ground_stations(active_only=False)
    tasks = app.db_manager.get_all_tasks(include_templates=True)

    assert len(ground_stations) == MAX_GROUND_STATIONS
    assert len(tasks) == MAX_TASKS


def test_scenario_endpoint_uses_requested_counts():
    app = create_app("testing")
    app.config["TESTING"] = True

    with app.test_client() as client:
        response = client.post(
            "/api/initialize/scenario",
            json={"satellite_count": 5, "ground_station_count": 3, "task_count": 8},
        )
        assert response.status_code == 200

        payload = response.get_json()
        data = payload.get("data", {})
        assert data.get("satellite_count") == 5
        assert data.get("ground_station_count") == 3
        assert data.get("task_count") == 8

        assert client.get("/api/satellite/all").get_json()["count"] == 5
        assert client.get("/api/groundstation/all").get_json()["count"] == 3
        assert client.get("/api/tasks/list").get_json()["count"] == 8

        assert app.db_manager.get_all_satellites() == []


def test_scenario_endpoint_rejects_oversized_counts():
    app = create_app("testing")
    app.config["TESTING"] = True

    with app.test_client() as client:
        ground_response = client.post(
            "/api/initialize/scenario",
            json={"satellite_count": 5, "ground_station_count": 11, "task_count": 8},
        )
        assert ground_response.status_code == 400

        task_response = client.post(
            "/api/initialize/scenario",
            json={"satellite_count": 5, "ground_station_count": 3, "task_count": 21},
        )
        assert task_response.status_code == 400



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
