"""SJF scheduling algorithm."""

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


class SJFScheduler(BaseScheduler):
    """Shortest Job First scheduler."""

    def __init__(self, preemptive: bool = False):
        super().__init__("SJF")
        self.preemptive = preemptive

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

        sorted_tasks = sorted(tasks, key=lambda t: (t.size, t.arrival_time))
        sat_queue_length = {sat.id: len(getattr(sat, "task_queue", [])) for sat in satellites}

        for task in sorted_tasks:
            if task.status != TaskStatus.PENDING:
                result.tasks.append(task)
                continue

            start_time = max(current_time, task.arrival_time)

            best_sat = None
            best_finish_time = None

            for sat in satellites:
                if task.size > self.get_satellite_capacity(sat):
                    continue
                if sat_queue_length[sat.id] >= 10:
                    continue

                processing_time = task.get_processing_time(sat.capacity)
                finish_time = start_time + timedelta(seconds=processing_time)

                if finish_time > task.deadline or finish_time > time_end:
                    continue

                if best_finish_time is None or finish_time < best_finish_time:
                    best_sat = sat
                    best_finish_time = finish_time

            if best_sat is None:
                result.tasks.append(task)
                continue

            task.assign(best_sat.id, start_time, best_finish_time)

            result.assignments[task.id] = best_sat.id
            sat_queue_length[best_sat.id] += 1
            current_time = best_finish_time
            result.tasks.append(task)

        result.calculate_metrics()

        if satellites:
            total_capacity = sum(sat.capacity for sat in satellites)
            used_capacity = sum(
                t.size
                for t in result.tasks
                if t.status in {TaskStatus.ASSIGNED, TaskStatus.RUNNING, TaskStatus.COMPLETED}
            )
            result.resource_utilization = used_capacity / total_capacity if total_capacity > 0 else 0.0

        return result
