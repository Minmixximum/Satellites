"""Satellite model used by simulation and APIs."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Dict, List, Optional

try:
    from core.time_utils import ensure_utc, utc_now
except ImportError:  # pragma: no cover
    from ..core.time_utils import ensure_utc, utc_now


@dataclass
class QueueTask:
    id: str
    size: float


@dataclass
class Satellite:
    id: str
    name: str
    tle_line1: str
    tle_line2: str

    capacity: float = 30000.0
    storage: float = 500 * 1024
    max_power: float = 3000.0

    position: Dict[str, float] = field(default_factory=dict)
    current_power: float = 3000.0
    current_load: float = 0.0
    is_visible: bool = False

    task_queue: List[QueueTask] = field(default_factory=list)
    max_queue_size: int = 10
    completed_tasks: int = 0
    failed_tasks: int = 0

    last_update: Optional[datetime] = None

    def __post_init__(self):
        if self.capacity <= 0:
            self.capacity = 1e-6
        if self.max_power <= 0:
            self.max_power = 1e-6

        self.current_power = max(0.0, min(self.current_power, self.max_power))

        default_pos = {"lat": 0.0, "lon": 0.0, "alt": 0.0}
        self.position = {**default_pos, **(self.position or {})}

        self.current_load = max(0.0, min(100.0, self.current_load))

        if self.last_update is None:
            self.last_update = utc_now()
        else:
            self.last_update = ensure_utc(self.last_update)

    def update_position(self, lat: float, lon: float, alt: float):
        self.position = {"lat": float(lat), "lon": float(lon), "alt": float(alt)}
        self.last_update = utc_now()

    def get_available_capacity(self) -> float:
        return self.capacity * (1.0 - self.current_load / 100.0)

    def add_task(self, task) -> bool:
        if len(self.task_queue) >= self.max_queue_size:
            return False

        task_id = getattr(task, "id", None)
        task_size = getattr(task, "size", None)
        if task_id is None or task_size is None:
            return False

        self.task_queue.append(QueueTask(id=str(task_id), size=float(task_size)))
        self._update_load()
        return True

    def remove_task(self, task_id: str) -> bool:
        for i, task in enumerate(self.task_queue):
            if task.id == task_id:
                self.task_queue.pop(i)
                self._update_load()
                return True
        return False

    def _update_load(self):
        if not self.task_queue:
            self.current_load = 0.0
            return

        total_size = sum(t.size for t in self.task_queue)
        raw_load = (total_size / self.capacity) * 100.0
        self.current_load = max(0.0, min(100.0, raw_load))

    def to_dict(self) -> Dict[str, Any]:
        return {
            "id": self.id,
            "name": self.name,
            "tle_line1": self.tle_line1,
            "tle_line2": self.tle_line2,
            "capacity": self.capacity,
            "storage": self.storage,
            "max_power": self.max_power,
            "current_power": self.current_power,
            "position": self.position,
            "current_load": self.current_load,
            "is_visible": self.is_visible,
            "task_queue_length": len(self.task_queue),
            "completed_tasks": self.completed_tasks,
            "failed_tasks": self.failed_tasks,
            "last_update": self.last_update.isoformat() if self.last_update else None,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "Satellite":
        sat = cls(
            id=data["id"],
            name=data.get("name", data["id"]),
            tle_line1=data.get("tle_line1", ""),
            tle_line2=data.get("tle_line2", ""),
            capacity=float(data.get("capacity", 30000.0)),
            storage=float(data.get("storage", 500 * 1024)),
            max_power=float(data.get("max_power", 3000.0)),
            position=data.get("position", {"lat": 0.0, "lon": 0.0, "alt": 0.0}),
        )

        sat.current_power = float(data.get("current_power", sat.current_power))
        sat.current_load = float(data.get("current_load", sat.current_load))
        sat.is_visible = bool(data.get("is_visible", sat.is_visible))
        sat.completed_tasks = int(data.get("completed_tasks", sat.completed_tasks))
        sat.failed_tasks = int(data.get("failed_tasks", sat.failed_tasks))

        last_update = data.get("last_update")
        if last_update:
            try:
                sat.last_update = ensure_utc(datetime.fromisoformat(last_update))
            except (TypeError, ValueError):
                sat.last_update = utc_now()

        sat.__post_init__()
        return sat
