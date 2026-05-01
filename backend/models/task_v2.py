"""Task model used by scheduling and API layers."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import Enum
from typing import Any, Dict, Optional


class TaskStatus(Enum):
    PENDING = "pending"
    ASSIGNED = "assigned"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    TIMEOUT = "timeout"


def _ensure_utc(dt: datetime) -> datetime:
    if dt.tzinfo is None:
        return dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc)


def _parse_dt(value: Any) -> datetime:
    if isinstance(value, datetime):
        return _ensure_utc(value)
    if isinstance(value, str) and value:
        return _ensure_utc(datetime.fromisoformat(value))
    return _ensure_utc(datetime.now(timezone.utc))


@dataclass
class Task:
    id: str
    size: float
    priority: int = 3
    deadline: datetime = field(default_factory=lambda: _ensure_utc(datetime.now(timezone.utc)))
    arrival_time: datetime = field(default_factory=lambda: _ensure_utc(datetime.now(timezone.utc)))

    status: TaskStatus = TaskStatus.PENDING
    assigned_satellite: Optional[str] = None

    actual_start: Optional[datetime] = None
    actual_end: Optional[datetime] = None

    source_lat: Optional[float] = None
    source_lon: Optional[float] = None

    task_type: str = "computing"
    input_data_size: float = 0.0
    output_data_size: float = 0.0

    def __post_init__(self):
        self.deadline = _ensure_utc(self.deadline)
        self.arrival_time = _ensure_utc(self.arrival_time)

        if self.actual_start:
            self.actual_start = _ensure_utc(self.actual_start)
        if self.actual_end:
            self.actual_end = _ensure_utc(self.actual_end)

        if self.size < 0:
            raise ValueError(f"Task size cannot be negative: {self.size}")
        if self.input_data_size < 0:
            raise ValueError(f"input_data_size cannot be negative: {self.input_data_size}")
        if self.output_data_size < 0:
            raise ValueError(f"output_data_size cannot be negative: {self.output_data_size}")

        self.priority = max(1, min(5, self.priority))

        if self.deadline < self.arrival_time:
            self.deadline = self.arrival_time

    def get_processing_time(self, satellite_capacity: float) -> float:
        if satellite_capacity <= 0:
            return float("inf")
        return self.size / satellite_capacity

    def get_slack_time(self, current_time: datetime) -> float:
        return (self.deadline - _ensure_utc(current_time)).total_seconds()

    def is_expired(self, current_time: datetime) -> bool:
        return _ensure_utc(current_time) > self.deadline

    def get_wait_time(self, current_time: datetime) -> float:
        return (_ensure_utc(current_time) - self.arrival_time).total_seconds()

    def get_response_time(self) -> Optional[float]:
        if self.actual_start is None:
            return None
        return (self.actual_start - self.arrival_time).total_seconds()

    def get_turnaround_time(self) -> Optional[float]:
        if self.actual_end is None:
            return None
        return (self.actual_end - self.arrival_time).total_seconds()

    def assign(self, satellite_id: str, start_time: datetime, end_time: Optional[datetime] = None):
        self.status = TaskStatus.ASSIGNED
        self.assigned_satellite = satellite_id
        self.actual_start = _ensure_utc(start_time)
        self.actual_end = _ensure_utc(end_time) if end_time else None

    def start(self, satellite_id: str, current_time: datetime):
        self.status = TaskStatus.RUNNING
        self.assigned_satellite = satellite_id
        if self.actual_start is None:
            self.actual_start = _ensure_utc(current_time)
        else:
            self.actual_start = _ensure_utc(self.actual_start)

    def get_progress(self, current_time: Optional[datetime] = None) -> float:
        now = _ensure_utc(current_time or datetime.now(timezone.utc))

        if self.status in (TaskStatus.COMPLETED, TaskStatus.FAILED, TaskStatus.TIMEOUT):
            return 1.0

        if self.status == TaskStatus.RUNNING:
            if self.actual_start is None or self.actual_end is None:
                return 0.0

            duration = (self.actual_end - self.actual_start).total_seconds()
            if duration <= 0:
                return 1.0

            elapsed = (now - self.actual_start).total_seconds()
            return max(0.0, min(1.0, elapsed / duration))

        return 0.0

    def complete(self, current_time: datetime):
        self.status = TaskStatus.COMPLETED
        self.actual_end = _ensure_utc(current_time)

    def fail(self, current_time: datetime):
        self.status = TaskStatus.FAILED
        self.actual_end = _ensure_utc(current_time)

    def to_dict(self, current_time: Optional[datetime] = None) -> Dict[str, Any]:
        return {
            "id": self.id,
            "size": self.size,
            "priority": self.priority,
            "deadline": self.deadline.isoformat(),
            "arrival_time": self.arrival_time.isoformat(),
            "status": self.status.value,
            "assigned_satellite": self.assigned_satellite,
            "actual_start": self.actual_start.isoformat() if self.actual_start else None,
            "actual_end": self.actual_end.isoformat() if self.actual_end else None,
            "source_lat": self.source_lat,
            "source_lon": self.source_lon,
            "task_type": self.task_type,
            "input_data_size": self.input_data_size,
            "output_data_size": self.output_data_size,
            "progress": self.get_progress(current_time),
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Task":
        status_value = data.get("status", "pending")
        try:
            status = TaskStatus(status_value)
        except ValueError:
            status = TaskStatus.PENDING

        task = cls(
            id=data["id"],
            size=float(data["size"]),
            priority=int(data.get("priority", 3)),
            deadline=_parse_dt(data.get("deadline")),
            arrival_time=_parse_dt(data.get("arrival_time")),
            status=status,
            assigned_satellite=data.get("assigned_satellite"),
            source_lat=data.get("source_lat"),
            source_lon=data.get("source_lon"),
            task_type=data.get("task_type", "computing"),
            input_data_size=float(data.get("input_data_size", 0.0)),
            output_data_size=float(data.get("output_data_size", 0.0)),
        )

        if data.get("actual_start"):
            task.actual_start = _parse_dt(data["actual_start"])
        if data.get("actual_end"):
            task.actual_end = _parse_dt(data["actual_end"])

        return task
