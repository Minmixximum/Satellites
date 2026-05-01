"""Flask entrypoint for the satellite scheduling backend."""

from __future__ import annotations

import json
import os
import random
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Optional

from flask import Flask, jsonify
from flask_cors import CORS

try:
    from config import Config, config_map
    from models import GroundStation, Satellite, Task
    from core.orbit_calculator import OrbitCalculator
    from core.scheduler import SimulationEngine, TaskScheduler
    from core.visibility import VisibilityAnalyzer
    from api import initialize_bp, satellite_bp, simulation_bp, task_bp
    from api.satellite_routes import init_ground_stations, init_satellites
    from api.task_routes import init_tasks
    from data.tle_fetcher import fetch_and_save_tle
except ImportError:  # pragma: no cover
    from .config import Config, config_map
    from .models import GroundStation, Satellite, Task
    from .core.orbit_calculator import OrbitCalculator
    from .core.scheduler import SimulationEngine, TaskScheduler
    from .core.visibility import VisibilityAnalyzer
    from .api import initialize_bp, satellite_bp, simulation_bp, task_bp
    from .api.satellite_routes import init_ground_stations, init_satellites
    from .api.task_routes import init_tasks
    from .data.tle_fetcher import fetch_and_save_tle

try:
    from core.time_utils import utc_now
except ImportError:  # pragma: no cover
    from .core.time_utils import utc_now

try:
    from skyfield.api import EarthSatellite, load, wgs84

    SKYFIELD_AVAILABLE = True
except Exception:
    EarthSatellite = None
    load = None
    wgs84 = None
    SKYFIELD_AVAILABLE = False


config = Config()


def create_app(config_name: str = "default") -> Flask:
    """Create and configure the Flask app."""
    app = Flask(__name__)
    app.config.from_object(config_map.get(config_name, config_map["default"]))

    CORS(app, origins=app.config.get("CORS_ORIGINS", "*"))

    app.orbit_calculator = OrbitCalculator()
    app.visibility_analyzer = VisibilityAnalyzer(app.orbit_calculator)
    app.task_scheduler = TaskScheduler(app.visibility_analyzer)
    app.simulation_engine = SimulationEngine(
        app.task_scheduler,
        app.orbit_calculator,
        app.visibility_analyzer,
    )

    app.register_blueprint(satellite_bp)
    app.register_blueprint(task_bp)
    app.register_blueprint(simulation_bp)
    app.register_blueprint(initialize_bp)

    _initialize_data(app)
    return app


def _initialize_data(app: Flask) -> None:
    """Initialize in-memory stores and simulation engine state."""
    tle_dir = os.path.join(os.path.dirname(__file__), "data", "tle")
    tle_file_path = os.path.join(tle_dir, "tle.txt")

    try:
        os.makedirs(tle_dir, exist_ok=True)
        fetch_and_save_tle(url=config.DATA_SOURCE_URL, output_dir=tle_dir)
    except Exception as exc:
        print(f"warning: failed to refresh TLE data: {exc}")

    if os.path.exists(tle_file_path):
        print(f'load tle data from:{tle_file_path}')
        satellites_data = _parse_tle_file(tle_file_path, count=config.DEFAULT_SATELLITE_COUNT)
    else:
        satellites_data = _generate_test_satellites(config.DEFAULT_SATELLITE_COUNT)

    ground_stations_data = _generate_test_ground_stations(config.DEFAULT_GROUND_STATION_COUNT)
    tasks_data = generate_tasks(count=config.DEFAULT_GENERATE_TASK_NUM)

    init_satellites(satellites_data)
    init_ground_stations(ground_stations_data)
    init_tasks(tasks_data)

    for sat_data in satellites_data:
        try:
            app.orbit_calculator.add_satellite(
                sat_data["id"],
                sat_data["tle_line1"],
                sat_data["tle_line2"],
            )
        except Exception as exc:
            print(f"warning: failed to add satellite {sat_data.get('id')}: {exc}")

    satellites = [Satellite.from_dict(s) for s in satellites_data]
    ground_stations = [GroundStation.from_dict(gs) for gs in ground_stations_data]
    tasks = [Task.from_dict(ts) for ts in tasks_data]

    app.simulation_engine.initialize(satellites, ground_stations)
    app.simulation_engine.clear_tasks()
    for task in tasks:
        app.simulation_engine.add_task(task)


def _parse_tle_file(filepath: str, count: int = config.DEFAULT_SATELLITE_COUNT) -> List[dict]:
    """Parse a TLE file (name + line1 + line2 triplets) into satellite dicts."""
    if not SKYFIELD_AVAILABLE:
        print("warning: skyfield not installed, using generated satellite positions")
        return _generate_test_satellites(count)

    satellites: List[dict] = []
    with open(filepath, "r", encoding="utf-8") as f:
        lines = [line.strip() for line in f if line.strip()]

    ts = load.timescale()

    i = 0
    line_len = len(lines)
    while i + 2 < line_len and len(satellites) < count:
        name_line = lines[i]
        tle_line1 = lines[i + 1]
        tle_line2 = lines[i + 2]
        i += 3*(line_len // (count*4))

        lat = 0.0
        lon = 0.0
        alt = 550.0

        try:
            satellite = EarthSatellite(tle_line1, tle_line2, name_line, ts)
            geocentric = satellite.at(ts.now())
            lat = wgs84.latlon_of(geocentric)[0].degrees
            lon = wgs84.latlon_of(geocentric)[1].degrees
            alt = wgs84.height_of(geocentric).km
        except Exception:
            pass

        norad_id = tle_line1[2:7].strip() or f"{len(satellites) + 1:03d}"
        sat_id = f"sat_{norad_id}"

        satellites.append(
            {
                "id": sat_id,
                "name": name_line,
                "tle_line1": tle_line1,
                "tle_line2": tle_line2,
                "capacity": 800 + (len(satellites) % 5) * 100,
                "storage": 500 + (len(satellites) % 3) * 250,
                "max_power": 3000,
                "current_power": 3000,
                "position": {"lat": round(lat, 4), "lon": round(lon, 4), "alt": round(alt, 3)},
                "current_load": 0,
                "is_visible": False,
                "task_queue_length": 0,
                "completed_tasks": 0,
                "failed_tasks": 0,
                "last_update": utc_now().isoformat(),
            }
        )

    if not satellites:
        return _generate_test_satellites(count)

    return satellites


def _generate_test_satellites(count: int = 5) -> List[dict]:
    """Generate fallback satellites when TLE data is unavailable."""
    satellites: List[dict] = []
    for i in range(count):
        sat_id = f"sat_{i + 1:03d}"
        altitude = 520 + (i % 5) * 15
        satellites.append(
            {
                "id": sat_id,
                "name": f"Satellite-{i + 1}",
                "tle_line1": f"1 0000{i + 1:02d}U 24001A   26100.00000000  .00000000  00000-0  00000-0 0  0010",
                "tle_line2": f"2 0000{i + 1:02d}  53.0000 {i * 45:8.4f} 0001000 000.0000 000.0000 15.00000000    10",
                "capacity": 800 + (i % 5) * 100,
                "storage": 500 + (i % 3) * 250,
                "max_power": 3000,
                "current_power": 3000,
                "position": {
                    "lat": round(random.uniform(-60, 60), 4),
                    "lon": round(random.uniform(-180, 180), 4),
                    "alt": float(altitude),
                },
                "current_load": 0,
                "is_visible": False,
                "task_queue_length": 0,
                "completed_tasks": 0,
                "failed_tasks": 0,
                "last_update": utc_now().isoformat(),
            }
        )
    return satellites


def _generate_test_ground_stations(count: int = 3) -> List[dict]:
    """Generate test ground stations."""
    locations = [
        {"name": "China Miyun", "lat": 40.3833, "lon": 116.8667, "alt": 0.0},
        {"name": "China Kashi", "lat": 39.5, "lon": 76.0167, "alt": 0.0},
        {"name": "China Sanya", "lat": 18.3, "lon": 109.3167, "alt": 0.0},
        {"name": "USA Point Barrow", "lat": 71.393, "lon": -156.422, "alt": 0.0},
        {"name": "Norway Svalbard", "lat": 78.22876, "lon": 15.40157, "alt": 0.45},
    ]

    stations: List[dict] = []
    for i, loc in enumerate(locations[:count]):
        stations.append(
            {
                "id": f"gs_{i + 1:03d}",
                "name": f"Ground Station {loc['name']}",
                "latitude": loc["lat"],
                "longitude": loc["lon"],
                "altitude": loc["alt"],
                "min_elevation": 10.0,
                "max_range": 3000.0,
                "communication_speed": 100.0,
                "connected_satellites": [],
                "is_active": True,
            }
        )

    return stations


def _generate_default_tasks(count: Optional[int] = None) -> List[dict]:
    """Generate fallback pending tasks."""
    if count is None:
        count = max(1, config.MAX_TASK_QUEUE_LENGTH // 2)

    now = utc_now()
    tasks: List[dict] = []
    arrival_interval = [i*10 for i in range(1,count)]

    for i in range(count):
        
        arrival = now + timedelta(seconds=arrival_interval[i-1])
        deadline = arrival + timedelta(minutes=random.randint(60, 200))
        tasks.append(
            {
                "id": f"task_{i + 1:03d}",
                "size": random.choice([200, 400, 600, 800, 1000,2000,1500]),
                "priority": random.randint(1, 5),
                "deadline": deadline.isoformat(timespec="seconds"),
                "arrival_time": arrival.isoformat(timespec="seconds"),
                "status": "pending",
                "assigned_satellite": None,
                "actual_start": None,
                "actual_end": None,
                "source_lat": round(random.uniform(-60, 60), 4),
                "source_lon": round(random.uniform(-180, 180), 4),
                "task_type": "computing",
                "input_data_size": random.choice([10, 20, 30, 50]),
                "output_data_size": random.choice([5, 10, 15, 25]),
            }
        )

    return tasks


def generate_tasks(count: int=4) -> List[dict]:
    """Generate tasks and save to JSON file."""
    tasks = _generate_default_tasks(count)

    file_path = os.path.join(os.path.dirname(__file__), "data", "tasks", "tasks.json")
    path = Path(file_path)

    path.parent.mkdir(parents=True, exist_ok=True)

    with path.open("w", encoding="utf-8") as f:
        json.dump({"tasks": tasks}, f, indent=2, ensure_ascii=False)

    print(f"Generated {len(tasks)} tasks and saved to {file_path}")
    return tasks


def load_tasks_from_json(file_path: Optional[str] = None) -> List[dict]:
    """Load tasks from JSON file; fallback to generated tasks."""
    if file_path is None:
        file_path = os.path.join(os.path.dirname(__file__), "data", "tasks", "tasks.json")

    path = Path(file_path)
    if not path.exists():
        return _generate_default_tasks()

    try:
        with path.open("r", encoding="utf-8") as f:
            data = json.load(f)
        tasks = data.get("tasks", []) if isinstance(data, dict) else []
        if not tasks:
            return _generate_default_tasks()
        return tasks
    except Exception as exc:
        print(f"warning: failed to load tasks from {file_path}: {exc}")
        return _generate_default_tasks()


app = create_app(os.environ.get("FLASK_ENV", "development"))


@app.route("/")
def index():
    """Root endpoint."""
    return jsonify(
        {
            "name": "Low Earth Orbit Satellite Task Scheduling System",
            "version": "1.0.0",
            "description": "Satellite task scheduling backend with Unity integration",
            "api_prefix": "/api",
            "endpoints": {
                "satellite": "/api/satellite/all",
                "ground_station": "/api/groundstation/all",
                "tasks": "/api/tasks/list",
                "algorithms": "/api/scheduler/algorithms",
                "simulation": "/api/simulation/status",
            },
        }
    )


@app.route("/api")
def api_index():
    """API index endpoint."""
    return jsonify(
        {
            "message": "Low Earth Orbit Satellite Task Scheduling API",
            "version": "1.0.0",
            "status": "running",
        }
    )


if __name__ == "__main__":
    print("=" * 60)
    print("Low Earth Orbit Satellite Task Scheduling System")
    print("=" * 60)
    print("Starting Flask service...")
    print("API URL: http://localhost:5000")
    print("Health check: http://localhost:5000/api/health")
    print("=" * 60)

    app.run(
        host="0.0.0.0",
        port=5000,
        debug=app.config.get("DEBUG", True),
        threaded=True,
    )
