"""Helpers for keeping persisted tasks and the simulation runtime in sync."""

from __future__ import annotations

from datetime import datetime
from typing import Iterable, List, Optional

try:
    from models.task_v2 import Task
except ImportError:  # pragma: no cover
    from ..models.task_v2 import Task

try:
    from core.time_utils import ensure_utc, utc_now
except ImportError:  # pragma: no cover
    from ..core.time_utils import ensure_utc, utc_now


ACTIVE_TASK_STATUSES = ["pending", "assigned", "running"]


def db_task_to_api(task: dict) -> dict:
    """Expose the legacy task response shape while storing DB-native names."""
    data = task.copy()
    data["assigned_satellite"] = data.pop("assigned_satellite_id", None)
    return data


def api_task_to_db(task: dict) -> dict:
    data = task.copy()
    if "assigned_satellite" in data:
        data["assigned_satellite_id"] = data.pop("assigned_satellite")
    return data


def db_task_to_runtime(task: dict) -> Task:
    return Task.from_dict(db_task_to_api(task))


def load_engine_tasks_from_db(db_manager, engine, statuses: Optional[Iterable[str]] = None) -> List[Task]:
    """Rebuild engine task cache from persisted active task state."""
    if not engine:
        return []

    selected_statuses = list(statuses or ACTIVE_TASK_STATUSES)
    rows = db_manager.get_tasks_by_statuses(selected_statuses)
    tasks = []

    if hasattr(engine, "clear_tasks"):
        engine.clear_tasks()
    else:
        engine.tasks = {}

    for row in rows:
        try:
            task = db_task_to_runtime(row)
        except Exception as exc:
            print(f"warning: failed to load task {row.get('id')} from database: {exc}")
            continue
        engine.add_task(task)
        tasks.append(task)

    return tasks


def task_runtime_updates(task: Task, current_time: Optional[datetime] = None) -> dict:
    now = ensure_utc(current_time) if current_time else utc_now()
    task_dict = task.to_dict(current_time=now)

    processing_time = None
    if task.actual_start and task.actual_end:
        processing_time = (task.actual_end - task.actual_start).total_seconds()

    return {
        "status": task_dict.get("status"),
        "assigned_satellite_id": task_dict.get("assigned_satellite"),
        "actual_start": task_dict.get("actual_start"),
        "actual_end": task_dict.get("actual_end"),
        "progress": task_dict.get("progress"),
        "wait_time": task.get_wait_time(now),
        "processing_time": processing_time,
    }


def sync_engine_tasks_to_db(db_manager, engine) -> int:
    """Persist current runtime task state back to the database."""
    if not db_manager or not engine or not getattr(engine, "tasks", None):
        return 0

    current_time = getattr(engine, "current_time", None) or utc_now()
    count = 0
    for task in engine.tasks.values():
        if db_manager.update_task(task.id, task_runtime_updates(task, current_time)):
            count += 1
    return count


def sync_engine_stats_to_session(db_manager, engine, session_id: Optional[int]) -> bool:
    if not db_manager or not engine or not session_id:
        return False

    status = engine.get_status()
    task_counts = status.get("tasks_count", {})
    total = task_counts.get("total", 0)
    completed = task_counts.get("completed", 0)
    failed = task_counts.get("failed", 0)

    return db_manager.update_session(
        session_id,
        {
            "sim_duration": status.get("stats", {}).get("simulation_duration", 0.0),
            "total_tasks": total,
            "completed_tasks": completed,
            "failed_tasks": failed,
            "timeout_tasks": task_counts.get("failed", 0),
            "completion_rate": completed / total if total else 0.0,
        },
    )


def record_scheduling_history(db_manager, result, session_id: Optional[int], algorithm: str) -> int:
    if not db_manager or not result:
        return 0

    decision_time = utc_now()
    count = 0
    for task in getattr(result, "tasks", []):
        satellite_id = getattr(task, "assigned_satellite", None)
        status = getattr(getattr(task, "status", None), "value", getattr(task, "status", None))
        db_manager.save_scheduling_history(
            {
                "session_id": session_id,
                "task_id": task.id,
                "satellite_id": satellite_id,
                "algorithm": algorithm,
                "decision_time": decision_time,
                "success": satellite_id is not None,
                "reason": "assigned" if satellite_id else str(status or "unassigned"),
            }
        )
        count += 1
    return count
