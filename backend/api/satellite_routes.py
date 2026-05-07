"""
Satellite and ground-station related API routes.
"""

from datetime import datetime, timedelta
from typing import Dict, List

from flask import Blueprint, current_app, jsonify, request

satellite_bp = Blueprint("satellite", __name__, url_prefix="/api")

try:
    from core.time_utils import ensure_utc, parse_iso_datetime, utc_now
except ImportError:  # pragma: no cover
    from ..core.time_utils import ensure_utc, parse_iso_datetime, utc_now

# In-memory stores
_satellites: Dict[str, dict] = {}
_ground_stations: Dict[str, dict] = {}


def _get_orbit_calculator():
    return getattr(current_app, "orbit_calculator", None)


def _get_engine():
    return getattr(current_app, "simulation_engine", None)


def _get_visibility_analyzer():
    return getattr(current_app, "visibility_analyzer", None)


def _get_db():
    return getattr(current_app, "db_manager", None)


def _resolve_reference_time() -> datetime:
    engine = _get_engine()
    if engine and getattr(engine, "current_time", None):
        return ensure_utc(engine.current_time)

    orbit_calc = _get_orbit_calculator()
    if orbit_calc and hasattr(orbit_calc, "get_reference_time"):
        return ensure_utc(orbit_calc.get_reference_time())

    return utc_now()


@satellite_bp.route("/satellite/all", methods=["GET"])
def get_all_satellites():
    """Get all satellites with real-time positions from simulation engine."""
    engine = _get_engine()
    orbit_calc = _get_orbit_calculator()

    # 如果仿真引擎有卫星数据，使用实时位置
    if engine and engine.satellites:
        satellites_list = []
        for sat_id, sat in engine.satellites.items():
            sat_dict = _satellites.get(sat_id, {})
            satellites_list.append({
                "id": sat_id,
                "name": sat_dict.get("name", sat_id),
                "position": {
                    "lat": sat.position.get("lat", 0),
                    "lon": sat.position.get("lon", 0),
                    "alt": sat.position.get("alt", 550),
                },
                "capacity": sat_dict.get("capacity", 1000),
                "storage": sat_dict.get("storage", 1000),
                "max_power": sat_dict.get("max_power", 100),
                "current_power": sat.current_power,
                "current_load": sat.current_load,
                "is_visible": sat.is_visible,
                "task_queue_length": len(sat.task_queue),
                "completed_tasks": sat.completed_tasks,
                "failed_tasks": sat.failed_tasks,
                "status": "idle" if sat.current_load < 0.3 else ("busy" if sat.current_load < 0.7 else "overloaded"),
                "last_update": utc_now().isoformat(),
            })
        return jsonify({"success": True, "data": satellites_list, "count": len(satellites_list)})

    # 否则返回静态数据
    return jsonify({"success": True, "data": list(_satellites.values()), "count": len(_satellites)})


@satellite_bp.route("/satellite/<sat_id>", methods=["GET"])
def get_satellite(sat_id: str):
    """Get one satellite."""
    if sat_id not in _satellites:
        return jsonify({"success": False, "error": f"Satellite {sat_id} not found"}), 404

    return jsonify({"success": True, "data": _satellites[sat_id]})


@satellite_bp.route("/satellite/position", methods=["POST"])
def get_satellite_position():
    """
    Get real-time position.
    Request body:
        {
            "satellite_id": "sat_001",
            "timestamp": "2024-01-01T00:00:00"  # optional
        }
    """
    data = request.get_json()

    if not data or "satellite_id" not in data:
        return jsonify({"success": False, "error": "Missing required field: satellite_id"}), 400

    sat_id = data["satellite_id"]
    orbit_calc = _get_orbit_calculator()

    if orbit_calc and sat_id in orbit_calc.satellites:
        timestamp = data.get("timestamp")
        dt = parse_iso_datetime(timestamp, default=_resolve_reference_time())

        position = orbit_calc.calculate_position(sat_id, dt)

        if position:
            return jsonify({"success": True, "data": position})

        return jsonify({"success": False, "error": "Failed to calculate position"}), 500

    if sat_id in _satellites:
        return jsonify({"success": True, "data": _satellites[sat_id].get("position", {})})

    return jsonify({"success": False, "error": f"Satellite {sat_id} not found"}), 404


@satellite_bp.route("/satellite/orbit", methods=["POST"])
def get_satellite_orbit():
    """
    Predict satellite orbit.
    Request body:
        {
            "satellite_id": "sat_001",
            "start_time": "2024-01-01T00:00:00",
            "duration_minutes": 90,
            "time_step_seconds": 60
        }
    """
    data = request.get_json()

    if not data or "satellite_id" not in data:
        return jsonify({"success": False, "error": "Missing required field: satellite_id"}), 400

    sat_id = data["satellite_id"]
    orbit_calc = _get_orbit_calculator()

    if not orbit_calc or sat_id not in orbit_calc.satellites:
        return jsonify({"success": False, "error": f"Satellite {sat_id} not found"}), 404

    start_time = data.get("start_time")
    dt = parse_iso_datetime(start_time, default=_resolve_reference_time())
    duration = data.get("duration_minutes", 90)
    step = data.get("time_step_seconds", 60)

    positions = orbit_calc.predict_orbit(sat_id, dt, duration, step)

    return jsonify(
        {
            "success": True,
            "data": {
                "satellite_id": sat_id,
                "start_time": dt.isoformat(),
                "positions": positions,
                "count": len(positions),
            },
        }
    )


@satellite_bp.route("/satellite/create", methods=["POST"])
def create_satellite():
    """
    Create a satellite.
    Request body:
        {
            "id": "sat_001",
            "name": "Satellite 1",
            "tle_line1": "...",
            "tle_line2": "...",
            "capacity": 1000,
            "storage": 1000
        }
    """
    data = request.get_json()

    required_fields = ["id", "name", "tle_line1", "tle_line2"]
    for field in required_fields:
        if field not in data:
            return jsonify({"success": False, "error": f"Missing required field: {field}"}), 400

    sat_id = data["id"]

    orbit_calc = _get_orbit_calculator()
    if orbit_calc:
        try:
            orbit_calc.add_satellite(sat_id, data["tle_line1"], data["tle_line2"])
        except Exception as e:
            return jsonify({"success": False, "error": f"Invalid TLE data: {str(e)}"}), 400

    _satellites[sat_id] = {
        "id": sat_id,
        "name": data["name"],
        "tle_line1": data["tle_line1"],
        "tle_line2": data["tle_line2"],
        "capacity": data.get("capacity", 1000),
        "storage": data.get("storage", 1000),
        "max_power": data.get("max_power", 100),
        "position": {"lat": 0, "lon": 0, "alt": 0},
        "current_power": data.get("current_power", 100),
        "current_load": 0,
        "is_visible": False,
        "task_queue_length": 0,
        "completed_tasks": 0,
        "failed_tasks": 0,
        "last_update": utc_now().isoformat(),
        "created_at": utc_now().isoformat(),
    }
    db_manager = _get_db()
    if db_manager:
        db_manager.save_satellite(_satellites[sat_id])

    return jsonify(
        {
            "success": True,
            "data": _satellites[sat_id],
            "message": f"Satellite {sat_id} created successfully",
        }
    )


@satellite_bp.route("/satellite/<sat_id>", methods=["DELETE"])
def delete_satellite(sat_id: str):
    """Delete satellite."""
    if sat_id not in _satellites:
        return jsonify({"success": False, "error": f"Satellite {sat_id} not found"}), 404

    orbit_calc = _get_orbit_calculator()
    if orbit_calc:
        orbit_calc.remove_satellite(sat_id)

    del _satellites[sat_id]
    db_manager = _get_db()
    if db_manager:
        db_manager.delete_satellite(sat_id)

    return jsonify({"success": True, "message": f"Satellite {sat_id} deleted successfully"})


@satellite_bp.route("/groundstation/all", methods=["GET"])
def get_all_ground_stations():
    """Get all ground stations."""
    return jsonify({"success": True, "data": list(_ground_stations.values()), "count": len(_ground_stations)})


@satellite_bp.route("/groundstation/<gs_id>", methods=["GET"])
def get_ground_station(gs_id: str):
    """Get one ground station."""
    if gs_id not in _ground_stations:
        return jsonify({"success": False, "error": f"Ground station {gs_id} not found"}), 404

    return jsonify({"success": True, "data": _ground_stations[gs_id]})


@satellite_bp.route("/groundstation/create", methods=["POST"])
def create_ground_station():
    """
    Create a ground station.
    Request body:
        {
            "id": "gs_001",
            "name": "Ground Station 1",
            "latitude": 39.9,
            "longitude": 116.4,
            "altitude": 0.1,
            "min_elevation": 10,
            "max_range": 3000
        }
    """
    data = request.get_json()

    required_fields = ["id", "name", "latitude", "longitude"]
    for field in required_fields:
        if field not in data:
            return jsonify({"success": False, "error": f"Missing required field: {field}"}), 400

    gs_id = data["id"]

    _ground_stations[gs_id] = {
        "id": gs_id,
        "name": data["name"],
        "latitude": data["latitude"],
        "longitude": data["longitude"],
        "altitude": data.get("altitude", 0),
        "min_elevation": data.get("min_elevation", 10),
        "max_range": data.get("max_range", 3000),
        "connected_satellites": [],
        "is_active": True,
        "created_at": utc_now().isoformat(),
    }
    db_manager = _get_db()
    if db_manager:
        db_manager.save_ground_station(_ground_stations[gs_id])

    return jsonify(
        {
            "success": True,
            "data": _ground_stations[gs_id],
            "message": f"Ground station {gs_id} created successfully",
        }
    )


@satellite_bp.route("/groundstation/<gs_id>", methods=["DELETE"])
def delete_ground_station(gs_id: str):
    """Delete ground station."""
    if gs_id not in _ground_stations:
        return jsonify({"success": False, "error": f"Ground station {gs_id} not found"}), 404

    del _ground_stations[gs_id]
    db_manager = _get_db()
    if db_manager:
        db_manager.update_ground_station(gs_id, {"is_active": False})

    return jsonify({"success": True, "message": f"Ground station {gs_id} deleted successfully"})


@satellite_bp.route("/visibility/calculate", methods=["POST"])
def calculate_visibility():
    """
    Calculate visibility windows.
    Request body:
        {
            "satellite_id": "sat_001",
            "ground_station_id": "gs_001",
            "start_time": "2024-01-01T00:00:00",
            "end_time": "2024-01-01T01:00:00"
        }
    """
    data = request.get_json()

    if not data or "satellite_id" not in data or "ground_station_id" not in data:
        return jsonify({"success": False, "error": "Missing required fields: satellite_id, ground_station_id"}), 400

    sat_id = data["satellite_id"]
    gs_id = data["ground_station_id"]

    visibility_analyzer = _get_visibility_analyzer()

    if not visibility_analyzer:
        return jsonify({"success": False, "error": "Visibility analyzer not available"}), 500

    if gs_id not in _ground_stations:
        return jsonify({"success": False, "error": f"Ground station {gs_id} not found"}), 404

    gs = _ground_stations[gs_id]

    start_time = data.get("start_time")
    end_time = data.get("end_time")
    dt_start = parse_iso_datetime(start_time, default=_resolve_reference_time())
    dt_end = parse_iso_datetime(end_time, default=dt_start + timedelta(hours=1))

    windows = visibility_analyzer.find_visibility_windows(
        sat_id,
        gs["latitude"],
        gs["longitude"],
        gs["altitude"],
        dt_start,
        dt_end,
    )

    return jsonify(
        {
            "success": True,
            "data": {
                "satellite_id": sat_id,
                "ground_station_id": gs_id,
                "windows": [w.to_dict() for w in windows],
                "count": len(windows),
            },
        }
    )


def init_satellites(satellites: List[dict]):
    """Initialize satellite store."""
    _satellites.clear()
    _satellites.update({s["id"]: s for s in satellites if isinstance(s, dict) and "id" in s})


def init_ground_stations(ground_stations: List[dict]):
    """Initialize ground station store."""
    _ground_stations.clear()
    _ground_stations.update(
        {gs["id"]: gs for gs in ground_stations if isinstance(gs, dict) and "id" in gs}
    )


def get_stored_satellites():
    """Get satellite store."""
    return _satellites


def get_stored_ground_stations():
    """Get ground station store."""
    return _ground_stations
