"""Base classes and result model for scheduling algorithms."""

from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from typing import Dict, List

try:
    from models.ground_station_v2 import GroundStation
    from models.satellite_v2 import Satellite
    from models.task_v2 import Task, TaskStatus
except ImportError:  # pragma: no cover
    from ..models.ground_station_v2 import GroundStation
    from ..models.satellite_v2 import Satellite
    from ..models.task_v2 import Task, TaskStatus

try:
    from core.time_utils import utc_now
except ImportError:  # pragma: no cover
    from ..core.time_utils import utc_now


@dataclass
class ScheduleResult:
    """Scheduling result summary and metrics."""

    algorithm_name: str
    tasks: List[Task] = field(default_factory=list)
    assignments: Dict[str, str] = field(default_factory=dict)  # task_id -> satellite_id
    schedule_time: datetime = field(default_factory=utc_now)

    # Counters
    total_tasks: int = 0
    completed_tasks: int = 0
    failed_tasks: int = 0
    timeout_tasks: int = 0

    # Time metrics (seconds)
    avg_response_time: float = 0.0
    avg_turnaround_time: float = 0.0
    avg_waiting_time: float = 0.0

    # Resource / quality metrics
    resource_utilization: float = 0.0
    completion_rate: float = 0.0
    avg_delay: float = 0.0
    high_priority_completion_rate: float = 0.0

    def calculate_metrics(self):
        """Calculate derived metrics from tasks."""
        if not self.tasks:
            return

        self.total_tasks = len(self.tasks)
        completed = [t for t in self.tasks if t.status == TaskStatus.COMPLETED]
        failed = [t for t in self.tasks if t.status == TaskStatus.FAILED]
        timeout = [t for t in self.tasks if t.status == TaskStatus.TIMEOUT]
        scheduled = [
            t for t in self.tasks
            if t.status in {TaskStatus.ASSIGNED, TaskStatus.RUNNING, TaskStatus.COMPLETED}
            and t.actual_end is not None
        ]

        # For scheduler-only results, ASSIGNED/RUNNING tasks with a planned end time
        # count as successfully scheduled work.
        self.completed_tasks = len(scheduled)
        self.failed_tasks = len(failed)
        self.timeout_tasks = len(timeout)

        self.completion_rate = self.completed_tasks / self.total_tasks if self.total_tasks > 0 else 0.0

        response_times = [t.get_response_time() for t in scheduled if t.get_response_time() is not None]
        turnaround_times = [t.get_turnaround_time() for t in scheduled if t.get_turnaround_time() is not None]
        waiting_times = [t.get_wait_time(t.deadline) for t in self.tasks]

        self.avg_response_time = sum(response_times) / len(response_times) if response_times else 0.0
        self.avg_turnaround_time = sum(turnaround_times) / len(turnaround_times) if turnaround_times else 0.0
        self.avg_waiting_time = sum(waiting_times) / len(waiting_times) if waiting_times else 0.0

        delays = []
        for t in scheduled:
            if t.actual_end and t.deadline:
                delays.append((t.actual_end - t.deadline).total_seconds())
        self.avg_delay = sum(delays) / len(delays) if delays else 0.0

        high_priority_tasks = [t for t in self.tasks if t.priority >= 4]
        high_priority_completed = [
            t for t in high_priority_tasks
            if t.status in {TaskStatus.ASSIGNED, TaskStatus.RUNNING, TaskStatus.COMPLETED}
            and t.actual_end is not None
        ]
        self.high_priority_completion_rate = (
            len(high_priority_completed) / len(high_priority_tasks) if high_priority_tasks else 0.0
        )

    def to_dict(self) -> Dict:
        """Serialize result for API responses."""
        return {
            "algorithm_name": self.algorithm_name,
            "schedule_time": self.schedule_time.isoformat(),
            "metrics": {
                "total_tasks": self.total_tasks,
                "completed_tasks": self.completed_tasks,
                "failed_tasks": self.failed_tasks,
                "timeout_tasks": self.timeout_tasks,
                "completion_rate": self.completion_rate,
                "avg_response_time": self.avg_response_time,
                "avg_turnaround_time": self.avg_turnaround_time,
                "avg_waiting_time": self.avg_waiting_time,
                "avg_delay": self.avg_delay,
                "resource_utilization": self.resource_utilization,
                "high_priority_completion_rate": self.high_priority_completion_rate,
            },
            "tasks": [t.to_dict() for t in self.tasks],
            "assignments": self.assignments,
        }


class BaseScheduler(ABC):
    """Abstract scheduler interface."""

    def __init__(self, name: str):
        self.name = name

    @abstractmethod
    def schedule(
        self,
        tasks: List[Task],
        satellites: List[Satellite],
        ground_stations: List[GroundStation],
        time_start: datetime,
        time_end: datetime,
        visibility_analyzer=None,
    ) -> ScheduleResult:
        """Run scheduling and return a schedule result."""
        raise NotImplementedError

    def get_satellite_capacity(self, satellite: Satellite) -> float:
        """Get currently available compute capacity of a satellite."""
        return satellite.get_available_capacity()

    def can_assign_task(self, task: Task, satellite: Satellite) -> bool:
        """Check whether a task can be assigned to a satellite."""
        if task.size > self.get_satellite_capacity(satellite):
            return False

        if len(satellite.task_queue) >= 10:
            return False

        return True

    def estimate_completion_time(self, task: Task, satellite: Satellite, current_time: datetime) -> datetime:
        """Estimate completion time for a task on a satellite."""
        processing_time = task.get_processing_time(satellite.capacity)
        return current_time + timedelta(seconds=processing_time)
