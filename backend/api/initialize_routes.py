"""Initialization API routes for demo/scenario/task bootstrap."""

from __future__ import annotations

import json
import os
import random
from datetime import datetime, timedelta, timezone
from typing import List, Optional, Tuple

from flask import Blueprint, current_app, jsonify, request

try:
    from models.ground_station_v2 import GroundStation
    from models.satellite_v2 import Satellite
    from models.task_v2 import Task
    from data.bootstrap import MAX_GROUND_STATIONS, MAX_TASKS, activate_task_selection, seed_reference_data
    from data.tle_fetcher import fetch_and_save_tle
except ImportError:  # pragma: no cover
    from ..models.ground_station_v2 import GroundStation
    from ..models.satellite_v2 import Satellite
    from ..models.task_v2 import Task
    from ..data.bootstrap import MAX_GROUND_STATIONS, MAX_TASKS, activate_task_selection, seed_reference_data
    from ..data.tle_fetcher import fetch_and_save_tle

initialize_bp = Blueprint("initialize", __name__, url_prefix="/api/initialize")

try:
    from core.time_utils import ensure_utc
except ImportError:  # pragma: no cover
    from ..core.time_utils import ensure_utc


def _load_tle_from_file(filepath: str) -> List[dict]:
    """Load satellites from a 3-line-per-satellite TLE text file."""
    satellites: List[dict] = []
    with open(filepath, "r", encoding="utf-8") as f:
        lines = [line.strip() for line in f if line.strip()]

    for index, i in enumerate(range(0, len(lines), 3), start=1):
        if i + 2 >= len(lines):
            break
        tle_line1 = lines[i + 1]
        norad_str = tle_line1[2:7].strip() or f"{index:03d}"
        satellites.append(
            {
                "id": f"sat_{norad_str}",
                "name": lines[i],
                "tle_line1": tle_line1,
                "tle_line2": lines[i + 2],
                "capacity": 1000,
                "storage": 1000,
                "max_power": 3000,
                "current_power": 3000,
                "position": {"lat": 0.0, "lon": 0.0, "alt": 550.0},
                "current_load": 0,
                "is_visible": False,
                "task_queue_length": 0,
                "completed_tasks": 0,
                "failed_tasks": 0,
                "last_update": datetime.now(timezone.utc).isoformat(),
            }
        )

    return satellites


def _extract_epoch_from_tle(tle_line1: str) -> datetime:
    """Extract approximate epoch datetime from TLE line 1."""
    epoch_str = tle_line1[18:32].strip()  # YYDDD.DDDDDDDD
    days = float(epoch_str[2:])
    yy = int(epoch_str[:2])
    year = 1900 + yy if yy >= 57 else 2000 + yy
    base = datetime(year, 1, 1, tzinfo=timezone.utc)
    return base + timedelta(days=days - 1)


def _generate_tasks(num_tasks: int, epoch_start: datetime, time_window_hours: int = 24) -> List[dict]:
    """Generate random tasks around epoch_start."""
    epoch_start = ensure_utc(epoch_start)
    tasks: List[dict] = []
    for i in range(1, num_tasks + 1):
        offset_seconds = random.uniform(0, time_window_hours * 3600)
        arrival = epoch_start + timedelta(seconds=offset_seconds)
        deadline = arrival + timedelta(seconds=random.uniform(3600, 6 * 3600))

        tasks.append(
            {
                "id": f"task_{i:03d}",
                "size": random.choice([200, 500, 800, 1000]),
                "priority": random.randint(1, 5),
                "deadline": deadline.isoformat(timespec="seconds"),
                "arrival_time": arrival.isoformat(timespec="seconds"),
                "status": "pending",
            }
        )
    return tasks


def _generate_scenario_from_satellites(
    satellites: List[dict],
    num_sats: int,
    num_tasks: int,
    output_file: str,
) -> str:
    """Generate scenario JSON from TLE satellites."""
    if num_sats > len(satellites):
        raise ValueError(f"Requested {num_sats} satellites, only {len(satellites)} available")

    selected = satellites[:num_sats]
    sat_list = []
    for idx, sat in enumerate(selected, start=1):
        sat_list.append(
            {
                "id": f"sat_{idx:03d}",
                "name": sat["name"],
                "tle_line1": sat["tle_line1"],
                "tle_line2": sat["tle_line2"],
                "capacity": sat.get("capacity", 1000),
                "storage": sat.get("storage", 1000),
                "max_power": sat.get("max_power", 3000),
                "current_power": sat.get("current_power", 3000),
                "position": sat.get("position", {"lat": 0.0, "lon": 0.0, "alt": 550.0}),
                "current_load": sat.get("current_load", 0),
                "is_visible": sat.get("is_visible", False),
                "task_queue_length": sat.get("task_queue_length", 0),
                "completed_tasks": sat.get("completed_tasks", 0),
                "failed_tasks": sat.get("failed_tasks", 0),
                "last_update": sat.get("last_update", datetime.now(timezone.utc).isoformat()),
            }
        )

    epoch = _extract_epoch_from_tle(selected[0]["tle_line1"])
    tasks = _generate_tasks(num_tasks, epoch, time_window_hours=24)

    ground_stations = [
        {
            "id": "gs_001",
            "name": "Ground Station Beijing",
            "latitude": 39.9042,
            "longitude": 116.4074,
            "altitude": 0.044,
            "min_elevation": 10,
            "max_range": 3000,
            "communication_speed": 100.0,
            "connected_satellites": [],
            "is_active": True,
        }
    ]

    scenario = {
        "name": "Small Scenario",
        "description": f"{num_sats} satellites, 1 ground station, {num_tasks} tasks",
        "satellites": sat_list,
        "ground_stations": ground_stations,
        "tasks": tasks,
    }

    os.makedirs(os.path.dirname(output_file), exist_ok=True)
    with open(output_file, "w", encoding="utf-8") as f:
        json.dump(scenario, f, indent=2, ensure_ascii=False)

    return output_file


def _generate_scenario_file() -> Tuple[Optional[str], Optional[str]]:
    """Find or generate scenario.json."""
    try:
        backend_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

        possible_paths = [
            os.path.join(backend_dir, "scenarios", "scenario.json"),
            os.path.join(backend_dir, "data", "scenarios", "scenario.json"),
            os.path.join(backend_dir, "scenario.json"),
        ]

        for scenario_path in possible_paths:
            if os.path.exists(scenario_path):
                return scenario_path, None

        try:
            from config import Config
        except ImportError:  # pragma: no cover
            from ..config import Config

        cfg = Config()
        num_sats = cfg.DEFAULT_SATELLITE_COUNT
        num_tasks = max(1, cfg.MAX_TASK_QUEUE_LENGTH // 2)

        tle_dir = os.path.join(backend_dir, "data", "tle")
        if not os.path.exists(tle_dir):
            return None, "No TLE directory found in data/tle"

        tle_files = [f for f in os.listdir(tle_dir) if f.endswith(".txt")]
        if not tle_files:
            return None, "No TLE files found in data/tle"

        tle_files.sort(key=lambda x: os.path.getmtime(os.path.join(tle_dir, x)), reverse=True)
        tle_file = os.path.join(tle_dir, tle_files[0])

        satellites = _load_tle_from_file(tle_file)
        if not satellites:
            return None, "No satellites parsed from TLE file"

        num_sats = min(num_sats, len(satellites))

        output_dir = os.path.join(backend_dir, "scenarios")
        scenario_path = os.path.join(output_dir, "scenario.json")
        _generate_scenario_from_satellites(satellites, num_sats, num_tasks, scenario_path)

        if not os.path.exists(scenario_path):
            return None, f"Scenario file not generated: {scenario_path}"

        return scenario_path, None
    except Exception as exc:
        return None, f"Error generating scenario: {exc}"


def _load_scenario_data(scenario_path: str):
    """Load scenario JSON data."""
    try:
        with open(scenario_path, "r", encoding="utf-8") as f:
            scenario = json.load(f)

        satellites = scenario.get("satellites", [])
        ground_stations = scenario.get("ground_stations", [])
        tasks = scenario.get("tasks", [])

        return satellites, ground_stations, tasks, None
    except Exception as exc:
        return None, None, None, f"Error loading scenario: {exc}"


def _resolve_tle_file() -> Optional[str]:
    backend_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    tle_dir = os.path.join(backend_dir, "data", "tle")
    os.makedirs(tle_dir, exist_ok=True)

    try:
        fetch_and_save_tle(output_dir=tle_dir)
    except Exception as exc:
        print(f"warning: failed to refresh TLE data for scenario init: {exc}")

    preferred = os.path.join(tle_dir, "tle.txt")
    if os.path.exists(preferred):
        return preferred

    candidates = [
        os.path.join(tle_dir, name)
        for name in os.listdir(tle_dir)
        if name.endswith(".txt")
    ]
    if not candidates:
        return None

    candidates.sort(key=lambda path: os.path.getmtime(path), reverse=True)
    return candidates[0]


def _parse_positive_int(value, default: int) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


@initialize_bp.route("/demo", methods=["POST"])
def initialize_demo():
    """Initialize a demo task set (8 random tasks)."""
    try:
        engine = current_app.simulation_engine
        if not engine:
            return jsonify({"success": False, "error": "Simulation engine not available"}), 500

        db_manager = getattr(current_app, "db_manager", None)
        if db_manager:
            db_manager.clear_all_tasks()
        engine.clear_tasks()

        tasks = engine.generate_random_tasks(
            count=8,
            size_range=(100, 1000),
            priority_distribution=[0.1, 0.2, 0.4, 0.2, 0.1],
        )

        from .task_routes import init_tasks

        task_dicts = [task.to_dict() for task in tasks]
        init_tasks(task_dicts)

        return jsonify(
            {
                "success": True,
                "message": f"Demo initialized with {len(tasks)} tasks",
                "task_count": len(tasks),
                "tasks": task_dicts,
            }
        )
    except Exception as exc:
        return jsonify({"success": False, "error": f"Failed to initialize demo: {exc}"}), 500


@initialize_bp.route("/test_tasks", methods=["POST"])
def initialize_test_tasks():
    """Initialize a test task set (10 random tasks)."""
    try:
        engine = current_app.simulation_engine
        if not engine:
            return jsonify({"success": False, "error": "Simulation engine not available"}), 500

        db_manager = getattr(current_app, "db_manager", None)
        if db_manager:
            db_manager.clear_all_tasks()
        engine.clear_tasks()

        tasks = engine.generate_random_tasks(
            count=10,
            size_range=(200, 800),
            priority_distribution=[0.15, 0.25, 0.35, 0.15, 0.1],
        )

        from .task_routes import init_tasks

        task_dicts = [task.to_dict() for task in tasks]
        init_tasks(task_dicts)

        return jsonify(
            {
                "success": True,
                "message": f"Test tasks initialized with {len(tasks)} tasks",
                "task_count": len(tasks),
                "tasks": task_dicts,
            }
        )
    except Exception as exc:
        return jsonify({"success": False, "error": f"Failed to initialize test tasks: {exc}"}), 500


@initialize_bp.route("/clear", methods=["POST"])
def clear_tasks():
    """Clear all tasks in both task store and simulation engine."""
    try:
        from .task_routes import init_tasks

        db_manager = getattr(current_app, "db_manager", None)
        if db_manager:
            db_manager.clear_all_tasks()
        init_tasks([])

        engine = current_app.simulation_engine
        if engine:
            engine.clear_tasks()

        return jsonify({"success": True, "message": "All tasks cleared successfully"})
    except Exception as exc:
        return jsonify({"success": False, "error": f"Failed to clear tasks: {exc}"}), 500


@initialize_bp.route("/status", methods=["GET"])
def get_initialization_status():
    """Get initialization status."""
    try:
        from .task_routes import get_stored_tasks

        tasks = get_stored_tasks()
        engine = current_app.simulation_engine
        engine_task_count = len(engine.tasks) if engine and hasattr(engine, "tasks") else 0

        return jsonify(
            {
                "success": True,
                "data": {
                    "stored_task_count": len(tasks),
                    "engine_task_count": engine_task_count,
                    "has_simulation_engine": engine is not None,
                    "initialization_available": True,
                },
            }
        )
    except Exception as exc:
        return jsonify({"success": False, "error": f"Failed to get initialization status: {exc}"}), 500


@initialize_bp.route("/scenario", methods=["POST"])
def initialize_scenario():
    """Initialize a selectable scenario from TLE + database-backed seed data."""
    try:
        engine = current_app.simulation_engine
        if not engine:
            return jsonify({"success": False, "error": "Simulation engine not available"}), 500

        payload = request.get_json(silent=True) or {}
        requested_satellite_count = _parse_positive_int(payload.get("satellite_count"), 5)
        requested_ground_station_count = _parse_positive_int(payload.get("ground_station_count"), 3)
        requested_task_count = _parse_positive_int(payload.get("task_count"), 8)

        if requested_satellite_count < 1:
            return jsonify({"success": False, "error": "satellite_count must be at least 1"}), 400
        if requested_ground_station_count < 1:
            return jsonify({"success": False, "error": "ground_station_count must be at least 1"}), 400
        if requested_ground_station_count > MAX_GROUND_STATIONS:
            return jsonify({"success": False, "error": f"ground_station_count cannot exceed {MAX_GROUND_STATIONS}"}), 400
        if requested_task_count < 1:
            return jsonify({"success": False, "error": "task_count must be at least 1"}), 400
        if requested_task_count > MAX_TASKS:
            return jsonify({"success": False, "error": f"task_count cannot exceed {MAX_TASKS}"}), 400

        tle_file = _resolve_tle_file()
        if not tle_file:
            return jsonify({"success": False, "error": "No TLE file available"}), 500

        satellites = _load_tle_from_file(tle_file)
        if not satellites:
            return jsonify({"success": False, "error": "No satellites parsed from TLE file"}), 400

        satellite_count = min(requested_satellite_count, len(satellites))
        satellites = satellites[:satellite_count]

        db_manager = getattr(current_app, "db_manager", None)
        if not db_manager:
            return jsonify({"success": False, "error": "Database manager not available"}), 500

        seed_reference_data(db_manager)
        ground_stations = db_manager.get_ground_stations(limit=requested_ground_station_count)
        tasks = activate_task_selection(db_manager, requested_task_count)

        from .satellite_routes import init_ground_stations, init_satellites

        init_satellites(satellites)
        init_ground_stations(ground_stations)

        orbit_calc = getattr(current_app, "orbit_calculator", None)
        if orbit_calc:
            orbit_calc.satellites.clear()
            for sat in satellites:
                try:
                    orbit_calc.add_satellite(sat["id"], sat["tle_line1"], sat["tle_line2"])
                except Exception as exc:
                    print(f"warning: failed to add satellite {sat.get('id')}: {exc}")

        satellite_objects = [Satellite.from_dict(s) for s in satellites]
        ground_station_objects = [GroundStation.from_dict(gs) for gs in ground_stations]

        engine.initialize(satellite_objects, ground_station_objects)
        engine.clear_tasks()

        for task_dict in tasks:
            try:
                engine.add_task(Task.from_dict(task_dict))
            except Exception as exc:
                print(f"warning: failed to add task {task_dict.get('id')}: {exc}")

        return jsonify(
            {
                "success": True,
                "message": (
                    f"Scenario initialized with {len(satellites)} satellites, "
                    f"{len(ground_stations)} ground stations, {len(tasks)} tasks"
                ),
                "data": {
                    "satellite_count": len(satellites),
                    "ground_station_count": len(ground_stations),
                    "task_count": len(tasks),
                    "tle_file": tle_file,
                },
            }
        )
    except Exception as exc:
        return jsonify({"success": False, "error": f"Failed to initialize scenario: {exc}"}), 500
