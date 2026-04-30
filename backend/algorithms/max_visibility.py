"""Max-Visibility scheduling algorithm."""

from __future__ import annotations

from datetime import datetime, timedelta
from typing import List

try:
    from algorithms.base import BaseScheduler, ScheduleResult
    from models.ground_station_v2 import GroundStation
    from models.satellite_v2 import Satellite
    from models.task_v2 import Task, TaskStatus
except ImportError:  # pragma: no cover
    from .base import BaseScheduler, ScheduleResult
    from ..models.ground_station_v2 import GroundStation
    from ..models.satellite_v2 import Satellite
    from ..models.task_v2 import Task, TaskStatus


class MaxVisibilityScheduler(BaseScheduler):
    """Prioritize assignments with better visibility and available capacity."""

    def __init__(self, visibility_weight: float = 0.4, capacity_weight: float = 0.4, load_weight: float = 0.2):
        super().__init__("Max-Visibility")
        self.visibility_weight = visibility_weight
        self.capacity_weight = capacity_weight
        self.load_weight = load_weight

    def schedule(
        self,
        tasks: List[Task],
        satellites: List[Satellite],
        ground_stations: List[GroundStation],
        time_start: datetime,
        time_end: datetime,
        visibility_analyzer=None,
    ) -> ScheduleResult:
        result = ScheduleResult(algorithm_name=self.name)
        current_time = time_start

        sorted_tasks = sorted(tasks, key=lambda t: (-t.priority, t.deadline, t.arrival_time))
        sat_queue_length = {sat.id: len(getattr(sat, "task_queue", [])) for sat in satellites}

        max_capacity = max((sat.capacity for sat in satellites), default=1.0)

        for task in sorted_tasks:
            if task.status != TaskStatus.PENDING:
                result.tasks.append(task)
                continue

            start_time = max(current_time, task.arrival_time)

            best_sat = None
            best_finish_time = None
            best_score = None

            for sat in satellites:
                if task.size > self.get_satellite_capacity(sat):
                    continue
                if sat_queue_length[sat.id] >= 10:
                    continue

                processing_time = task.get_processing_time(sat.capacity)
                finish_time = start_time + timedelta(seconds=processing_time)
                if finish_time > task.deadline or finish_time > time_end:
                    continue

                visibility_score = self._visibility_score(sat, ground_stations)
                capacity_score = max(0.0, min(1.0, self.get_satellite_capacity(sat) / max_capacity))
                load_score = max(0.0, min(1.0, 1.0 - sat.current_load / 100.0))

                score = (
                    self.visibility_weight * visibility_score
                    + self.capacity_weight * capacity_score
                    + self.load_weight * load_score
                )

                if best_score is None or score > best_score:
                    best_score = score
                    best_sat = sat
                    best_finish_time = finish_time

            if best_sat is None:
                task.fail(start_time)
                result.tasks.append(task)
                continue

            task.start(best_sat.id, start_time)
            task.complete(best_finish_time)

            result.assignments[task.id] = best_sat.id
            sat_queue_length[best_sat.id] += 1
            current_time = best_finish_time
            result.tasks.append(task)

        result.calculate_metrics()

        if satellites:
            total_capacity = sum(sat.capacity for sat in satellites)
            used_capacity = sum(t.size for t in result.tasks if t.status == TaskStatus.COMPLETED)
            result.resource_utilization = used_capacity / total_capacity if total_capacity > 0 else 0.0

        return result

    def _visibility_score(self, satellite: Satellite, ground_stations: List[GroundStation]) -> float:
        if not ground_stations:
            return 0.0

        sat_pos = satellite.position or {"lat": 0.0, "lon": 0.0, "alt": 0.0}
        scores = [
            gs.get_visibility_score(sat_pos["lat"], sat_pos["lon"], sat_pos["alt"])
            for gs in ground_stations
        ]
        return max(scores) if scores else 0.0
