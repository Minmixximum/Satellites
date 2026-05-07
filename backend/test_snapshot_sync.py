"""Tests for satellite and ground-station snapshot persistence."""

from __future__ import annotations

import os

try:
    import app as app_module
    from app import create_app
    from database import DatabaseManager
except ImportError:  # pragma: no cover
    from backend import app as app_module
    from backend.app import create_app
    from backend.database import DatabaseManager


def _sample_satellite(**overrides):
    data = {
        "id": "sat_25544",
        "name": "ISS (ZARYA)",
        "norad_id": 25544,
        "tle_line1": "1 25544U 98067A   26120.50000000  .00000000  00000-0  00000-0 0  9990",
        "tle_line2": "2 25544  51.6400 100.0000 0005000 200.0000 160.0000 15.50000000 00010",
        "tle_epoch": "2026-04-30T12:00:00+00:00",
        "inclination": 51.64,
        "altitude": 420.5,
        "mean_motion": 15.5,
        "capacity": 900.0,
        "storage": 750.0,
        "max_power": 3000.0,
        "current_power": 3000.0,
        "current_load": 0.0,
        "is_visible": False,
        "completed_tasks": 0,
        "failed_tasks": 0,
    }
    data.update(overrides)
    return data


def _sample_ground_station(**overrides):
    data = {
        "id": "gs_001",
        "name": "Ground Station Beijing",
        "latitude": 39.9042,
        "longitude": 116.4074,
        "altitude": 0.044,
        "min_elevation": 10.0,
        "max_range": 3000.0,
        "communication_speed": 100.0,
        "is_active": True,
        "connected_satellites": ["sat_25544"],
    }
    data.update(overrides)
    return data


def test_satellite_snapshot_sync_is_incremental():
    db = DatabaseManager("sqlite:///:memory:")

    first = db.save_satellites_batch([_sample_satellite()])
    second = db.save_satellites_batch([_sample_satellite()])
    third = db.save_satellites_batch([_sample_satellite(capacity=1200.0)])

    assert first == {"inserted": 1, "updated": 0, "unchanged": 0, "total": 1}
    assert second == {"inserted": 0, "updated": 0, "unchanged": 1, "total": 1}
    assert third == {"inserted": 0, "updated": 1, "unchanged": 0, "total": 1}

    saved = db.get_satellite("sat_25544")
    assert saved is not None
    assert saved["capacity"] == 1200.0
    assert saved["norad_id"] == 25544
    assert saved["mean_motion"] == 15.5


def test_ground_station_snapshot_sync_filters_runtime_fields():
    db = DatabaseManager("sqlite:///:memory:")

    first = db.save_ground_stations_batch([_sample_ground_station()])
    second = db.save_ground_stations_batch([_sample_ground_station()])
    third = db.save_ground_stations_batch([_sample_ground_station(max_range=4500.0)])

    assert first == {"inserted": 1, "updated": 0, "unchanged": 0, "total": 1}
    assert second == {"inserted": 0, "updated": 0, "unchanged": 1, "total": 1}
    assert third == {"inserted": 0, "updated": 1, "unchanged": 0, "total": 1}

    saved = db.get_ground_station("gs_001")
    assert saved is not None
    assert saved["max_range"] == 4500.0
    assert "connected_satellites" not in saved


def test_app_initialization_seeds_ground_stations_without_satellite_snapshots(monkeypatch):
    sample_satellites = [
        {
            "id": "sat_25544",
            "name": "ISS (ZARYA)",
            "norad_id": 25544,
            "tle_line1": "1 25544U 98067A   26120.50000000  .00000000  00000-0  00000-0 0  9990",
            "tle_line2": "2 25544  51.6400 100.0000 0005000 200.0000 160.0000 15.50000000 00010",
            "position": {"lat": 12.3, "lon": 45.6, "alt": 421.2},
            "capacity": 800.0,
            "storage": 500.0,
            "max_power": 3000.0,
            "current_power": 3000.0,
            "current_load": 0.0,
            "is_visible": False,
            "task_queue_length": 0,
            "completed_tasks": 0,
            "failed_tasks": 0,
            "last_update": "2026-05-01T00:00:00+00:00",
        }
    ]
    sample_ground_stations = [
        {
            "id": "gs_001",
            "name": "Ground Station Beijing",
            "latitude": 39.9042,
            "longitude": 116.4074,
            "altitude": 0.044,
            "min_elevation": 10.0,
            "max_range": 3000.0,
            "communication_speed": 100.0,
            "connected_satellites": [],
            "is_active": True,
        }
    ]

    original_exists = app_module.os.path.exists

    monkeypatch.setattr(app_module, "fetch_and_save_tle", lambda **kwargs: None)
    monkeypatch.setattr(app_module, "_parse_tle_file", lambda filepath, count: sample_satellites)
    monkeypatch.setattr(
        app_module.os.path,
        "exists",
        lambda path: True if path.endswith(os.path.join("tle", "tle.txt")) else original_exists(path),
    )

    app = create_app("testing")
    saved_satellites = app.db_manager.get_all_satellites()
    saved_ground_stations = app.db_manager.get_all_ground_stations()

    assert saved_satellites == []
    assert len(saved_ground_stations) == 10
    assert saved_ground_stations[0]["id"] == "gs_001"
