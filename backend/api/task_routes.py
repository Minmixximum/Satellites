"""Task-related API routes."""

from datetime import datetime, timedelta
from typing import Dict, List
import uuid

from flask import Blueprint, current_app, jsonify, request

task_bp = Blueprint("task", __name__, url_prefix="/api")

try:
    from core.time_utils import ensure_utc, parse_iso_datetime, utc_now
    from database.runtime_sync import (
        db_task_to_api,
        load_engine_tasks_from_db,
        record_scheduling_history,
        sync_engine_tasks_to_db,
        task_runtime_updates,
    )
    from models.task_v2 import Task
    from models.satellite_v2 import Satellite
    from models.ground_station_v2 import GroundStation
except ImportError:  # pragma: no cover
    from ..core.time_utils import ensure_utc, parse_iso_datetime, utc_now
    from ..database.runtime_sync import (
        db_task_to_api,
        load_engine_tasks_from_db,
        record_scheduling_history,
        sync_engine_tasks_to_db,
        task_runtime_updates,
    )
    from ..models.task_v2 import Task
    from ..models.satellite_v2 import Satellite
    from ..models.ground_station_v2 import GroundStation


_tasks: Dict[str, dict] = {}


def _get_db():
    return getattr(current_app, "db_manager", None)


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


def _sync_runtime_before_read():
    db_manager = _get_db()
    engine = _get_engine()
    if db_manager and engine:
        sync_engine_tasks_to_db(db_manager, engine)


def _get_live_tasks() -> List[dict]:
    db_manager = _get_db()
    if not db_manager:
        return list(_tasks.values())

    _sync_runtime_before_read()
    return [db_task_to_api(task) for task in db_manager.get_all_tasks()]


@task_bp.route("/tasks/list", methods=["GET"])
def get_tasks():
    """Get task list."""
    status = request.args.get("status")
    db_manager = _get_db()

    if db_manager:
        _sync_runtime_before_read()
        if status:
            tasks = db_manager.get_tasks_by_status(status)
        else:
            tasks = db_manager.get_all_tasks()
        data = [db_task_to_api(task) for task in tasks]
        return jsonify({"success": True, "data": data, "count": len(data)})

    tasks = _get_live_tasks()
    if status:
        tasks = [task for task in tasks if task.get("status") == status]
    return jsonify({"success": True, "data": tasks, "count": len(tasks)})


@task_bp.route("/tasks/<task_id>", methods=["GET"])
def get_task(task_id: str):
    """Get a single task."""
    db_manager = _get_db()
    if db_manager:
        _sync_runtime_before_read()
        task = db_manager.get_task(task_id)
        if not task:
            return jsonify({"success": False, "error": f"Task {task_id} not found"}), 404
        return jsonify({"success": True, "data": db_task_to_api(task)})

    if task_id not in _tasks:
        return jsonify({"success": False, "error": f"Task {task_id} not found"}), 404
    return jsonify({"success": True, "data": _tasks[task_id]})


@task_bp.route("/tasks/create", methods=["POST"])
def create_task():
    """Create a task."""
    data = request.get_json()

    if not data or "size" not in data:
        return jsonify({"success": False, "error": "Missing required field: size"}), 400

    task_id = data.get("id") or f"task_{uuid.uuid4().hex[:8]}"
    now = _resolve_reference_time()
    dt_arrival = parse_iso_datetime(data.get("arrival_time"), default=now)
    dt_deadline = parse_iso_datetime(data.get("deadline"), default=now + timedelta(hours=1))

    task_data = {
        "id": task_id,
        "size": data["size"],
        "priority": data.get("priority", 3),
        "deadline": dt_deadline.isoformat(),
        "arrival_time": dt_arrival.isoformat(),
        "status": "pending",
        "assigned_satellite": None,
        "actual_start": None,
        "actual_end": None,
        "source_lat": data.get("source_lat"),
        "source_lon": data.get("source_lon"),
        "task_type": data.get("task_type", "computing"),
        "input_data_size": data.get("input_data_size", 0),
        "output_data_size": data.get("output_data_size", 0),
        "created_at": now.isoformat(),
    }

    db_manager = _get_db()
    if db_manager:
        db_manager.save_task(task_data)
        saved = db_task_to_api(db_manager.get_task(task_id))
        engine = _get_engine()
        if engine:
            engine.add_task(Task.from_dict(saved))
        return jsonify(
            {
                "success": True,
                "data": saved,
                "message": f"Task {task_id} created successfully",
            }
        )

    _tasks[task_id] = task_data
    engine = _get_engine()
    if engine:
        engine.add_task(Task.from_dict(task_data))

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
    db_manager = _get_db()
    if db_manager:
        if not db_manager.delete_task(task_id):
            return jsonify({"success": False, "error": f"Task {task_id} not found"}), 404

        engine = _get_engine()
        if engine:
            engine.remove_task(task_id)

        return jsonify({"success": True, "message": f"Task {task_id} deleted successfully"})

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
    db_manager = _get_db()
    if db_manager:
        db_manager.clear_all_tasks()
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
    """Run scheduler."""
    data = request.get_json() or {}
    algorithm = data.get("algorithm", "fcfs")

    scheduler = _get_scheduler()
    engine = _get_engine()
    db_manager = _get_db()

    if not scheduler or not engine:
        return jsonify({"success": False, "error": "Scheduler or simulation engine not available"}), 500

    if db_manager:
        pending_tasks = [db_task_to_api(task) for task in db_manager.get_pending_tasks()]
    else:
        pending_tasks = [task for task in _tasks.values() if task.get("status") == "pending"]

    if not pending_tasks:
        return jsonify({"success": False, "error": "No pending tasks to schedule"}), 400

    from .satellite_routes import get_stored_ground_stations, get_stored_satellites

    task_objects = [Task.from_dict(task) for task in pending_tasks]
    sat_data = get_stored_satellites()
    gs_data = get_stored_ground_stations()
    satellites = [Satellite.from_dict(sat) for sat in sat_data.values()]
    ground_stations = [GroundStation.from_dict(gs) for gs in gs_data.values()]

    dt_start = parse_iso_datetime(data.get("time_start"), default=_resolve_reference_time())
    dt_end = parse_iso_datetime(data.get("time_end"), default=dt_start + timedelta(hours=1))

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

    if db_manager:
        for task in result.tasks:
            db_manager.update_task(task.id, task_runtime_updates(task, dt_start))
            engine.tasks[task.id] = task
        record_scheduling_history(
            db_manager,
            result,
            getattr(engine, "current_session_id", None),
            algorithm,
        )
    else:
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
    """Compare all scheduling algorithms."""
    data = request.get_json() or {}
    scheduler = _get_scheduler()
    db_manager = _get_db()

    if not scheduler:
        return jsonify({"success": False, "error": "Scheduler not available"}), 500

    from .satellite_routes import get_stored_ground_stations, get_stored_satellites

    if db_manager:
        task_rows = [db_task_to_api(task) for task in db_manager.get_all_tasks()]
    else:
        task_rows = list(_tasks.values())

    task_objects = [Task.from_dict(task) for task in task_rows]
    sat_data = get_stored_satellites()
    gs_data = get_stored_ground_stations()
    satellites = [Satellite.from_dict(sat) for sat in sat_data.values()]
    ground_stations = [GroundStation.from_dict(gs) for gs in gs_data.values()]

    dt_start = parse_iso_datetime(data.get("time_start"), default=_resolve_reference_time())
    dt_end = parse_iso_datetime(data.get("time_end"), default=dt_start + timedelta(hours=1))

    comparison = scheduler.compare_algorithms(
        tasks=task_objects,
        satellites=satellites,
        ground_stations=ground_stations,
        time_start=dt_start,
        time_end=dt_end,
    )

    return jsonify({"success": True, "data": comparison})


def init_tasks(tasks: List[dict]):
    """Compatibility helper: persist task seed data instead of owning an in-memory store."""
    _tasks.clear()
    _tasks.update({task["id"]: task for task in tasks if isinstance(task, dict) and "id" in task})

    try:
        db_manager = _get_db()
    except RuntimeError:
        db_manager = None

    if db_manager:
        db_manager.save_tasks_batch(tasks)
        engine = _get_engine()
        if engine:
            load_engine_tasks_from_db(db_manager, engine)


def get_stored_tasks():
    """Compatibility helper returning the current database-backed task map."""
    try:
        db_manager = _get_db()
    except RuntimeError:
        db_manager = None

    if db_manager:
        return {
            task["id"]: db_task_to_api(task)
            for task in db_manager.get_all_tasks()
            if isinstance(task, dict) and task.get("id")
        }
    return _tasks
