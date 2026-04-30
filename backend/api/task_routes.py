"""
Task-related API routes.
"""

from datetime import datetime, timedelta
from typing import Dict, List
import uuid

from flask import Blueprint, current_app, jsonify, request

task_bp = Blueprint("task", __name__, url_prefix="/api")

try:
    from core.time_utils import ensure_utc, parse_iso_datetime, utc_now
except ImportError:  # pragma: no cover
    from ..core.time_utils import ensure_utc, parse_iso_datetime, utc_now

# In-memory task store
_tasks: Dict[str, dict] = {}


def _get_engine():
    return getattr(current_app, "simulation_engine", None)


def _get_scheduler():
    return getattr(current_app, "task_scheduler", None)


def _resolve_reference_time() -> datetime:
    engine = _get_engine()
    if engine and getattr(engine, "current_time", None):
        return ensure_utc(engine.current_time)

    orbit_calc = getattr(current_app, "orbit_calculator", None)
    if orbit_calc and hasattr(orbit_calc, "get_reference_time"):
        return ensure_utc(orbit_calc.get_reference_time())

    return utc_now()


@task_bp.route("/tasks/list", methods=["GET"])
def get_tasks():
    """Get task list."""
    status = request.args.get("status")
    tasks = list(_tasks.values())

    if status:
        tasks = [t for t in tasks if t.get("status") == status]

    return jsonify({"success": True, "data": tasks, "count": len(tasks)})


@task_bp.route("/tasks/<task_id>", methods=["GET"])
def get_task(task_id: str):
    """Get a single task."""
    if task_id not in _tasks:
        return jsonify({"success": False, "error": f"Task {task_id} not found"}), 404

    return jsonify({"success": True, "data": _tasks[task_id]})


@task_bp.route("/tasks/create", methods=["POST"])
def create_task():
    """
    Create a task.
    Request body:
        {
            "size": 500,
            "priority": 3,
            "deadline": "2024-01-01T02:00:00",
            "arrival_time": "2024-01-01T00:00:00",
            "task_type": "computing",
            "input_data_size": 100,
            "output_data_size": 50
        }
    """
    data = request.get_json()

    if not data or "size" not in data:
        return jsonify({"success": False, "error": "Missing required field: size"}), 400

    task_id = data.get("id") or f"task_{uuid.uuid4().hex[:8]}"

    arrival_time = data.get("arrival_time")
    deadline = data.get("deadline")

    now = _resolve_reference_time()
    dt_arrival = parse_iso_datetime(arrival_time, default=now)
    dt_deadline = parse_iso_datetime(deadline, default=now + timedelta(hours=1))

    _tasks[task_id] = {
        "id": task_id,
        "size": data["size"],
        "priority": data.get("priority", 3),
        "deadline": dt_deadline.isoformat(),
        "arrival_time": dt_arrival.isoformat(),
        "status": "pending",
        "assigned_satellite": None,
        "actual_start": None,
        "actual_end": None,
        "task_type": data.get("task_type", "computing"),
        "input_data_size": data.get("input_data_size", 0),
        "output_data_size": data.get("output_data_size", 0),
        "created_at": now.isoformat(),
    }

    engine = _get_engine()
    if engine:
        from ..models.task_v2 import Task

        task = Task.from_dict(_tasks[task_id])
        engine.add_task(task)

    return jsonify(
        {
            "success": True,
            "data": _tasks[task_id],
            "message": f"Task {task_id} created successfully",
        }
    )


@task_bp.route("/tasks/<task_id>", methods=["DELETE"])
def delete_task(task_id: str):
    """Delete a task."""
    if task_id not in _tasks:
        return jsonify({"success": False, "error": f"Task {task_id} not found"}), 404

    engine = _get_engine()
    if engine:
        engine.remove_task(task_id)

    del _tasks[task_id]

    return jsonify({"success": True, "message": f"Task {task_id} deleted successfully"})


@task_bp.route("/tasks/clear", methods=["POST"])
def clear_tasks():
    """Clear all tasks."""
    _tasks.clear()

    engine = _get_engine()
    if engine:
        if hasattr(engine, "clear_tasks"):
            engine.clear_tasks()
        elif hasattr(engine, "tasks"):
            engine.tasks = {}

    return jsonify({"success": True, "message": "All tasks cleared"})


@task_bp.route("/scheduler/algorithms", methods=["GET"])
def get_algorithms():
    """Get available scheduling algorithms."""
    scheduler = _get_scheduler()

    if scheduler:
        algorithms = scheduler.get_available_algorithms()
    else:
        algorithms = [
            {"id": "fcfs", "name": "FCFS", "description": "First-Come, First-Served"},
            {"id": "sjf", "name": "SJF", "description": "Shortest Job First"},
            {"id": "edd", "name": "EDD", "description": "Earliest Due Date"},
            {"id": "max_visibility", "name": "Max-Visibility", "description": "Max Visibility First"},
        ]

    return jsonify({"success": True, "data": algorithms})


@task_bp.route("/scheduler/run", methods=["POST"])
def run_scheduler():
    """
    Run scheduler.
    Request body:
        {
            "algorithm": "fcfs",
            "time_start": "2024-01-01T00:00:00",
            "time_end": "2024-01-01T01:00:00"
        }
    """
    data = request.get_json() or {}
    algorithm = data.get("algorithm", "fcfs")

    scheduler = _get_scheduler()
    engine = _get_engine()

    if not scheduler or not engine:
        return jsonify({"success": False, "error": "Scheduler or simulation engine not available"}), 500

    pending_tasks = [t for t in _tasks.values() if t.get("status") == "pending"]

    if not pending_tasks:
        return jsonify({"success": False, "error": "No pending tasks to schedule"}), 400

    from ..models.task_v2 import Task
    from ..models.satellite_v2 import Satellite
    from ..models.ground_station_v2 import GroundStation
    from .satellite_routes import get_stored_ground_stations, get_stored_satellites

    task_objects = [Task.from_dict(t) for t in pending_tasks]

    sat_data = get_stored_satellites()
    gs_data = get_stored_ground_stations()

    satellites = [Satellite.from_dict(s) for s in sat_data.values()]
    ground_stations = [GroundStation.from_dict(gs) for gs in gs_data.values()]

    time_start = data.get("time_start")
    time_end = data.get("time_end")
    dt_start = parse_iso_datetime(time_start, default=_resolve_reference_time())
    dt_end = parse_iso_datetime(time_end, default=dt_start + timedelta(hours=1))

    result = scheduler.run_scheduling(
        algorithm=algorithm,
        tasks=task_objects,
        satellites=satellites,
        ground_stations=ground_stations,
        time_start=dt_start,
        time_end=dt_end,
    )

    if not result:
        return jsonify({"success": False, "error": f"Failed to run scheduler with algorithm: {algorithm}"}), 400

    for task in result.tasks:
        if task.id in _tasks:
            _tasks[task.id].update(task.to_dict())

    return jsonify({"success": True, "data": result.to_dict()})


@task_bp.route("/scheduler/result/<result_id>", methods=["GET"])
def get_scheduler_result(result_id: str):
    """Get scheduler result (simplified)."""
    scheduler = _get_scheduler()

    if not scheduler:
        return jsonify({"success": False, "error": "Scheduler not available"}), 500

    result = scheduler.get_last_result()

    if not result:
        return jsonify({"success": False, "error": "No scheduling result available"}), 404

    return jsonify({"success": True, "data": result.to_dict()})


@task_bp.route("/scheduler/compare", methods=["POST"])
def compare_algorithms():
    """
    Compare all scheduling algorithms.
    Request body:
        {
            "time_start": "2024-01-01T00:00:00",
            "time_end": "2024-01-01T01:00:00"
        }
    """
    data = request.get_json() or {}

    scheduler = _get_scheduler()

    if not scheduler:
        return jsonify({"success": False, "error": "Scheduler not available"}), 500

    from ..models.task_v2 import Task
    from ..models.satellite_v2 import Satellite
    from ..models.ground_station_v2 import GroundStation
    from .satellite_routes import get_stored_ground_stations, get_stored_satellites

    task_objects = [Task.from_dict(t) for t in _tasks.values()]

    sat_data = get_stored_satellites()
    gs_data = get_stored_ground_stations()

    satellites = [Satellite.from_dict(s) for s in sat_data.values()]
    ground_stations = [GroundStation.from_dict(gs) for gs in gs_data.values()]

    time_start = data.get("time_start")
    time_end = data.get("time_end")
    dt_start = parse_iso_datetime(time_start, default=_resolve_reference_time())
    dt_end = parse_iso_datetime(time_end, default=dt_start + timedelta(hours=1))

    comparison = scheduler.compare_algorithms(
        tasks=task_objects,
        satellites=satellites,
        ground_stations=ground_stations,
        time_start=dt_start,
        time_end=dt_end,
    )

    return jsonify({"success": True, "data": comparison})


def init_tasks(tasks: List[dict]):
    """Initialize task store."""
    _tasks.clear()
    _tasks.update({t["id"]: t for t in tasks if isinstance(t, dict) and "id" in t})


def get_stored_tasks():
    """Get current task store."""
    return _tasks
