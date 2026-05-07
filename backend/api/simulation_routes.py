"""
Simulation control API routes.
"""

from flask import Blueprint, current_app, jsonify, request

simulation_bp = Blueprint("simulation", __name__, url_prefix="/api")

try:
    from core.time_utils import utc_now
    from database.runtime_sync import load_engine_tasks_from_db, sync_engine_tasks_to_db, sync_engine_stats_to_session
except ImportError:  # pragma: no cover
    from ..core.time_utils import utc_now
    from ..database.runtime_sync import load_engine_tasks_from_db, sync_engine_tasks_to_db, sync_engine_stats_to_session


def _get_engine():
    return getattr(current_app, "simulation_engine", None)


def _get_scheduler():
    return getattr(current_app, "task_scheduler", None)


def _get_db():
    return getattr(current_app, "db_manager", None)


@simulation_bp.route("/simulation/start", methods=["POST"])
def start_simulation():
    """
    Start simulation.
    Request body examples:
        {
            "algorithm": "fcfs",
            "speed_factor": 60.0
        }
        {
            "algorithm": "fcfs",
            "timeScale": 60.0
        }
        {
            "algorithm": "fcfs",
            "time_scale": 60.0,
            "time_speed": 60.0
        }
    """
    data = request.get_json() or {}
    engine = _get_engine()

    if not engine:
        return jsonify({"success": False, "error": "Simulation engine not available"}), 500

    algorithm = data.get("algorithm", "fcfs")
    db_manager = _get_db()

    # Single source of truth: speed_factor.
    # Keep backward compatibility for legacy names.
    speed_factor = data.get("speed_factor")
    if speed_factor is None:
        speed_factor = data.get("timeScale")
    if speed_factor is None:
        speed_factor = data.get("time_scale")
    if speed_factor is None:
        speed_factor = data.get("time_speed")
    if speed_factor is None:
        speed_factor = getattr(engine, "speed_factor", 60.0)

    engine.active_algorithm = algorithm
    # Keep this field for compatibility but neutralize its effect.
    engine.time_speed = 1.0
    speed_result = engine.set_speed_factor(speed_factor)
    if db_manager:
        load_engine_tasks_from_db(db_manager, engine)
        engine.current_session_id = db_manager.create_session(
            algorithm=algorithm,
            speed_factor=speed_result["speed_factor"],
            max_tasks=len(getattr(engine, "tasks", {})),
        )
    engine.start(algorithm)
    engine.run_scheduling()
    if db_manager:
        sync_engine_tasks_to_db(db_manager, engine)
        sync_engine_stats_to_session(db_manager, engine, engine.current_session_id)

    return jsonify(
        {
            "success": True,
            "message": f"Simulation started with algorithm: {algorithm}",
            "data": {
                "algorithm": algorithm,
                "speed_factor": speed_result["speed_factor"],
                # Legacy response field retained for old clients.
                "timeScale": speed_result["speed_factor"],
                "current_time": engine.current_time.isoformat() if engine.current_time else None,
            },
        }
    )


@simulation_bp.route("/simulation/algorithm", methods=["POST"])
def set_simulation_algorithm():
    """
    Set scheduling algorithm without changing run/pause state.
    Request body:
        {
            "algorithm": "sjf"
        }
    """
    data = request.get_json() or {}
    engine = _get_engine()

    if not engine:
        return jsonify({"success": False, "error": "Simulation engine not available"}), 500

    algorithm = data.get("algorithm")
    if not algorithm:
        return jsonify({"success": False, "error": "Missing required field: algorithm"}), 400

    engine.active_algorithm = algorithm

    return jsonify(
        {
            "success": True,
            "message": f"Simulation algorithm set to: {algorithm}",
            "data": {"algorithm": algorithm},
        }
    )


@simulation_bp.route("/simulation/pause", methods=["POST"])
def pause_simulation():
    """Pause simulation."""
    engine = _get_engine()

    if not engine:
        return jsonify({"success": False, "error": "Simulation engine not available"}), 500

    engine.pause()
    db_manager = _get_db()
    if db_manager:
        sync_engine_tasks_to_db(db_manager, engine)
        sync_engine_stats_to_session(db_manager, engine, getattr(engine, "current_session_id", None))

    return jsonify(
        {
            "success": True,
            "message": "Simulation paused",
            "data": {"current_time": engine.current_time.isoformat() if engine.current_time else None},
        }
    )


@simulation_bp.route("/simulation/resume", methods=["POST"])
def resume_simulation():
    """Resume simulation."""
    engine = _get_engine()

    if not engine:
        return jsonify({"success": False, "error": "Simulation engine not available"}), 500

    engine.resume()

    return jsonify(
        {
            "success": True,
            "message": "Simulation resumed",
            "data": {"current_time": engine.current_time.isoformat() if engine.current_time else None},
        }
    )


@simulation_bp.route("/simulation/reset", methods=["POST"])
def reset_simulation():
    """Reset simulation."""
    engine = _get_engine()

    if not engine:
        return jsonify({"success": False, "error": "Simulation engine not available"}), 500

    engine.stop()
    db_manager = _get_db()
    session_id = getattr(engine, "current_session_id", None)
    if db_manager and session_id:
        sync_engine_tasks_to_db(db_manager, engine)
        sync_engine_stats_to_session(db_manager, engine, session_id)
        db_manager.update_session(session_id, {"status": "reset"})
    if db_manager:
        db_manager.clear_all_tasks()
    engine.current_session_id = None

    # Also clear task store.
    from .task_routes import init_tasks

    init_tasks([])

    # Clear engine tasks if supported.
    if hasattr(engine, "clear_tasks"):
        engine.clear_tasks()
    elif hasattr(engine, "tasks"):
        engine.tasks = {}

    return jsonify({"success": True, "message": "Simulation reset"})


@simulation_bp.route("/simulation/status", methods=["GET"])
def get_simulation_status():
    """Get simulation status."""
    engine = _get_engine()

    if not engine:
        return jsonify({"success": False, "error": "Simulation engine not available"}), 500

    return jsonify({"success": True, "data": engine.get_status()})


@simulation_bp.route("/simulation/step", methods=["POST"])
def simulation_step():
    """
    Run one simulation step.
    Request body:
        {
            "delta_seconds": 60
        }
    """
    data = request.get_json() or {}
    delta = data.get("delta_seconds", 60)

    engine = _get_engine()

    if not engine:
        return jsonify({"success": False, "error": "Simulation engine not available"}), 500

    engine.step(delta)
    db_manager = _get_db()
    if db_manager:
        sync_engine_tasks_to_db(db_manager, engine)
        sync_engine_stats_to_session(db_manager, engine, getattr(engine, "current_session_id", None))

    return jsonify({"success": True, "data": engine.get_status()})


@simulation_bp.route("/simulation/batch_tasks", methods=["POST"])
def generate_batch_tasks():
    """
    Generate random tasks in batch.
    Request body:
        {
            "count": 10,
            "size_range": [100, 1000],
            "priority_distribution": [0.1, 0.2, 0.4, 0.2, 0.1]
        }
    """
    data = request.get_json() or {}

    engine = _get_engine()

    if not engine:
        return jsonify({"success": False, "error": "Simulation engine not available"}), 500

    count = data.get("count", 10)
    size_range = data.get("size_range", [100, 1000])
    priority_dist = data.get("priority_distribution", [0.1, 0.2, 0.4, 0.2, 0.1])

    tasks = engine.generate_random_tasks(count, tuple(size_range), priority_dist)
    db_manager = _get_db()
    if db_manager:
        db_manager.save_tasks_batch([task.to_dict() for task in tasks])

    return jsonify(
        {
            "success": True,
            "message": f"Generated {len(tasks)} tasks",
            "data": {"count": len(tasks), "tasks": [t.to_dict() for t in tasks]},
        }
    )


@simulation_bp.route("/simulation/compare", methods=["GET", "POST"])
def get_simulation_compare():
    """Get algorithm comparison summary."""
    scheduler = _get_scheduler()

    if not scheduler:
        return jsonify({"success": False, "error": "Scheduler not available"}), 500

    return jsonify(
        {
            "success": True,
            "data": {"message": "Use /scheduler/compare endpoint for detailed comparison"},
        }
    )


@simulation_bp.route("/health", methods=["GET"])
def health_check():
    """Health check."""
    return jsonify(
        {
            "success": True,
            "message": "Service is healthy",
            "timestamp": utc_now().isoformat(),
        }
    )


@simulation_bp.route("/simulation/time", methods=["GET"])
def get_simulation_time():
    """Get current simulation time and Earth rotation angle."""
    engine = _get_engine()

    if not engine:
        return jsonify({"success": False, "error": "Simulation engine not available"}), 500

    db_manager = _get_db()
    if db_manager:
        sync_engine_tasks_to_db(db_manager, engine)
        sync_engine_stats_to_session(db_manager, engine, getattr(engine, "current_session_id", None))

    return jsonify({"success": True, "data": engine.get_time_info()})


@simulation_bp.route("/simulation/speed", methods=["POST"])
def set_simulation_speed():
    """
    Set time acceleration speed factor.
    Request body:
        {
            "speed_factor": 60.0
        }
    """
    data = request.get_json() or {}
    engine = _get_engine()

    if not engine:
        return jsonify({"success": False, "error": "Simulation engine not available"}), 500

    speed_factor = data.get("speed_factor", 60.0)

    result = engine.set_speed_factor(speed_factor)

    return jsonify(
        {
            "success": True,
            "message": f"Speed factor set to {result['speed_factor']}x",
            "data": result
        }
    )
