"""Task scheduler and simulation engine."""
from typing import List, Dict, Optional
from datetime import datetime, timedelta
import math
import uuid

try:
    from models.task_v2 import Task, TaskStatus
    from models.satellite_v2 import Satellite
    from models.ground_station_v2 import GroundStation
    from algorithms.base import ScheduleResult
    from algorithms.fcfs import FCFSScheduler
    from algorithms.sjf import SJFScheduler
    from algorithms.edd import EDDScheduler
    from algorithms.max_visibility import MaxVisibilityScheduler
except ImportError:  # pragma: no cover
    from ..models.task_v2 import Task, TaskStatus
    from ..models.satellite_v2 import Satellite
    from ..models.ground_station_v2 import GroundStation
    from ..algorithms.base import ScheduleResult
    from ..algorithms.fcfs import FCFSScheduler
    from ..algorithms.sjf import SJFScheduler
    from ..algorithms.edd import EDDScheduler
    from ..algorithms.max_visibility import MaxVisibilityScheduler

try:
    from core.time_utils import ensure_utc, utc_now
except ImportError:  # pragma: no cover
    from .time_utils import ensure_utc, utc_now


class TaskScheduler:
    """任务调度器 - 协调调度算法执行"""

    def __init__(self, visibility_analyzer=None):
        self.visibility_analyzer = visibility_analyzer
        self.algorithms = {
            "fcfs": FCFSScheduler(),
            "sjf": SJFScheduler(),
            "edd": EDDScheduler(),
            "max_visibility": MaxVisibilityScheduler()
        }
        self.last_result: Optional[ScheduleResult] = None
        self.results_history: List[ScheduleResult] = []

    def get_available_algorithms(self) -> List[Dict]:
        """获取可用算法列表"""
        return [
            {
                "id": key,
                "name": alg.name,
                "description": self._get_algorithm_description(key)
            }
            for key, alg in self.algorithms.items()
        ]

    def _get_algorithm_description(self, alg_id: str) -> str:
        """获取算法描述"""
        descriptions = {
            "fcfs": "先来先服务 (First-Come, First-Served) - 简单公平的调度策略",
            "sjf": "最短作业优先 (Shortest Job First) - 最小化平均等待时间",
            "edd": "最早截止期优先 (Earliest Due Date) - 最小化任务超时",
            "max_visibility": "最大可见性优先 - 基于可见窗口优化调度",
        }
        return descriptions.get(alg_id, "未知算法")

    def run_scheduling(self, algorithm: str, tasks: List[Task],
                      satellites: List[Satellite],
                      ground_stations: List[GroundStation],
                      time_start: Optional[datetime] = None,
                      time_end: Optional[datetime] = None) -> Optional[ScheduleResult]:
        """
        执行调度

        Args:
            algorithm: 算法 ID
            tasks: 任务列表
            satellites: 卫星列表
            ground_stations: 地面站列表
            time_start: 开始时间（默认当前时间）
            time_end: 结束时间（默认开始后 24 小时）
        Returns:
            调度结果
        """
        if algorithm not in self.algorithms:
            return None

        scheduler = self.algorithms[algorithm]

        if time_start is None:
            time_start = utc_now()
        else:
            time_start = ensure_utc(time_start)
        if time_end is None:
            time_end = time_start + timedelta(hours=24)
        else:
            time_end = ensure_utc(time_end)

        # 执行调度
        result = scheduler.schedule(
            tasks=tasks,
            satellites=satellites,
            ground_stations=ground_stations,
            time_start=time_start,
            time_end=time_end,
            visibility_analyzer=self.visibility_analyzer
        )

        self.last_result = result
        self.results_history.append(result)

        return result

    def compare_algorithms(self, tasks: List[Task],
                          satellites: List[Satellite],
                          ground_stations: List[GroundStation],
                          time_start: Optional[datetime] = None,
                          time_end: Optional[datetime] = None) -> Dict:
        """
        比较所有算法的性能

        Returns:
            算法对比结果
        """
        if time_start is None:
            time_start = utc_now()
        else:
            time_start = ensure_utc(time_start)
        if time_end is None:
            time_end = time_start + timedelta(hours=24)
        else:
            time_end = ensure_utc(time_end)

        comparison = {
            "timestamp": utc_now().isoformat(),
            "time_range": {
                "start": time_start.isoformat(),
                "end": time_end.isoformat()
            },
            "tasks_count": len(tasks),
            "satellites_count": len(satellites),
            "ground_stations_count": len(ground_stations),
            "results": []
        }

        for alg_id, scheduler in self.algorithms.items():
            # 复制任务，避免状态污染
            import copy
            tasks_copy = copy.deepcopy(tasks)

            result = scheduler.schedule(
                tasks=tasks_copy,
                satellites=satellites,
                ground_stations=ground_stations,
                time_start=time_start,
                time_end=time_end,
                visibility_analyzer=self.visibility_analyzer
            )

            comparison["results"].append({
                "algorithm": alg_id,
                "name": scheduler.name,
                "metrics": {
                    "completion_rate": result.completion_rate,
                    "avg_response_time": result.avg_response_time,
                    "avg_turnaround_time": result.avg_turnaround_time,
                    "avg_waiting_time": result.avg_waiting_time,
                    "avg_delay": result.avg_delay,
                    "resource_utilization": result.resource_utilization,
                    "high_priority_completion_rate": result.high_priority_completion_rate
                },
                "task_summary": {
                    "total": result.total_tasks,
                    "completed": result.completed_tasks,
                    "failed": result.failed_tasks,
                    "timeout": result.timeout_tasks
                }
            })

        # 找出各项指标的最佳算法
        comparison["best_algorithms"] = self._find_best_algorithms(comparison["results"])

        return comparison

    def _find_best_algorithms(self, results: List[Dict]) -> Dict:
        """找出各项指标的最佳算法"""
        best = {}

        metrics = [
            "completion_rate",
            "avg_response_time",
            "avg_turnaround_time",
            "resource_utilization",
            "high_priority_completion_rate"
        ]

        for metric in metrics:
            if metric.startswith("avg_") and metric != "avg_delay":
                # 时间类指标越小越好
                best_result = min(results, key=lambda r: r["metrics"][metric])
            else:
                # 比率类指标越大越好
                best_result = max(results, key=lambda r: r["metrics"][metric])

            best[metric] = {
                "algorithm": best_result["algorithm"],
                "value": best_result["metrics"][metric]
            }

        return best

    def get_last_result(self) -> Optional[ScheduleResult]:
        """获取最后一次调度结果"""
        return self.last_result

    def get_results_history(self) -> List[ScheduleResult]:
        """获取历史调度结果"""
        return self.results_history

    def clear_history(self):
        """清除历史记录"""
        self.results_history = []
        self.last_result = None


class SimulationEngine:
    """仿真引擎 - 管理仿真状态和执行"""

    # Earth rotation angular velocity (rad/s) - 360 degrees per sidereal day
    EARTH_ANGULAR_VELOCITY = 7.292115e-5

    def __init__(self, scheduler: TaskScheduler,
                 orbit_calculator=None,
                 visibility_analyzer=None):
        self.scheduler = scheduler
        self.orbit_calc = orbit_calculator
        self.visibility_analyzer = visibility_analyzer

        # 仿真状态
        self.is_running = False
        self.is_paused = False
        self.current_time: Optional[datetime] = None
        self.start_time: Optional[datetime] = None
        self.end_time: Optional[datetime] = None
        self.time_speed = 1.0  # 时间流速倍数

        # Time acceleration
        self.sim_time: Optional[datetime] = None  # Simulated time (single source of truth)
        self.speed_factor: float = 60.0  # Default: 1 real second = 60 simulated seconds

        # 仿真数据
        self.satellites: Dict[str, Satellite] = {}
        self.ground_stations: Dict[str, GroundStation] = {}
        self.tasks: Dict[str, Task] = {}
        self.active_algorithm = "fcfs"

        # 统计信息
        self.stats = {
            "total_tasks_generated": 0,
            "total_tasks_completed": 0,
            "total_tasks_failed": 0,
            "simulation_duration": 0.0
        }

        # 后台仿真线程
        self._simulation_thread = None
        self._stop_thread = False
        self._thread_interval = 0.2  # 秒

    def initialize(self, satellites: List[Satellite],
                  ground_stations: List[GroundStation],
                  start_time: Optional[datetime] = None):
        """初始化仿真环境"""
        self.satellites = {s.id: s for s in satellites}
        self.ground_stations = {gs.id: gs for gs in ground_stations}

        # 添加卫星到轨道计算器
        if self.orbit_calc:
            for sat in satellites:
                self.orbit_calc.add_satellite(sat.id, sat.tle_line1, sat.tle_line2)

        if start_time is not None:
            self.start_time = ensure_utc(start_time)
        elif self.orbit_calc and hasattr(self.orbit_calc, "get_reference_time"):
            self.start_time = ensure_utc(self.orbit_calc.get_reference_time())
        else:
            self.start_time = utc_now()
        self.current_time = self.start_time
        self.sim_time = self.start_time  # Initialize simulated time
        self.stats["simulation_duration"] = 0.0

    def start(self, algorithm: str = "fcfs"):
        """开始仿真"""
        self.active_algorithm = algorithm
        self._align_time_to_pending_tasks()
        self.is_running = True
        self.is_paused = False

        # 启动后台仿真线程
        self._start_simulation_thread()

    def pause(self):
        """暂停仿真"""
        self.is_paused = True

    def resume(self):
        """恢复仿真"""
        self.is_paused = False

    def stop(self):
        """停止仿真"""
        self.is_running = False
        self.is_paused = False
        self._stop_simulation_thread()
        self.current_time = self.start_time

    def _align_time_to_pending_tasks(self):
        """Start near the first task instead of waiting days for stale TLE reference time."""
        if not self.tasks:
            return

        pending_arrivals = [
            task.arrival_time
            for task in self.tasks.values()
            if task.status == TaskStatus.PENDING and task.arrival_time is not None
        ]
        if not pending_arrivals:
            return

        first_arrival = min(pending_arrivals)
        if self.current_time is None or self.current_time < first_arrival:
            self.current_time = first_arrival
            self.sim_time = first_arrival
            if self.start_time is None or self.start_time > first_arrival:
                self.start_time = first_arrival

    def _start_simulation_thread(self):
        """启动后台仿真线程"""
        import threading

        if self._simulation_thread is not None and self._simulation_thread.is_alive():
            return

        self._stop_thread = False
        self._simulation_thread = threading.Thread(target=self._simulation_loop, daemon=True)
        self._simulation_thread.start()

    def _stop_simulation_thread(self):
        """停止后台仿真线程"""
        self._stop_thread = True
        if self._simulation_thread is not None:
            self._simulation_thread.join(timeout=2.0)
            self._simulation_thread = None

    def _simulation_loop(self):
        """仿真线程主循环"""
        import time

        while not self._stop_thread and self.is_running:
            if not self.is_paused:
                # Single source of time scaling: speed_factor.
                # Keep loop step fixed to real elapsed thread interval.
                self.step(delta_seconds=self._thread_interval)
            time.sleep(self._thread_interval)

    def step(self, delta_seconds: float = 60.0):
        """
        执行一个仿真步进

        Args:
            delta_seconds: 时间步长（秒）
        """
        if not self.is_running or self.is_paused:
            return

        # 更新仿真时间
        if self.current_time is None:
            self.current_time = self.start_time or utc_now()
        if self.sim_time is None:
            self.sim_time = self.current_time

        # Update simulated time with speed factor
        sim_delta = delta_seconds * self.speed_factor
        time_delta = timedelta(seconds=sim_delta)
        self.sim_time += time_delta
        self.current_time = self.sim_time  # Keep current_time in sync with sim_time
        self.stats["simulation_duration"] += sim_delta

        # 更新卫星位置
        self._update_satellite_positions()

        # 检查可见性
        self._update_visibility()

        # First settle currently active tasks, then assign new work,
        # then activate tasks whose execution window has started.
        self._update_task_status()
        self.run_scheduling()
        self._update_task_status()

    def _update_satellite_positions(self):
        """更新所有卫星位置"""
        if not self.orbit_calc:
            return

        # Use sim_time for position calculation
        calc_time = self.sim_time or self.current_time
        for sat_id, sat in self.satellites.items():
            pos = self.orbit_calc.calculate_position(sat_id, calc_time)
            if pos:
                sat.update_position(pos["lat"], pos["lon"], pos["alt"])

    def _update_visibility(self):
        """更新可见性状态"""
        for sat in self.satellites.values():
            sat.is_visible = False

        for gs in self.ground_stations.values():
            gs.connected_satellites = []

            for sat in self.satellites.values():
                if gs.is_satellite_visible(
                    sat.position["lat"],
                    sat.position["lon"],
                    sat.position["alt"]
                ):
                    sat.is_visible = True
                    gs.connected_satellites.append(sat.id)

    def _update_task_status(self):
        """更新任务状态"""
        for task in self.tasks.values():
            if task.status == TaskStatus.PENDING:
                # 检查是否超时
                if task.is_expired(self.current_time):
                    task.status = TaskStatus.TIMEOUT
                    self.stats["total_tasks_failed"] += 1

            elif task.status == TaskStatus.ASSIGNED:
                if task.is_expired(self.current_time):
                    task.status = TaskStatus.TIMEOUT
                    self.stats["total_tasks_failed"] += 1
                    continue

                if task.actual_start and self.current_time >= task.actual_start:
                    if task.assigned_satellite in self.satellites:
                        sat = self.satellites[task.assigned_satellite]
                        if not any(queue_task.id == task.id for queue_task in sat.task_queue):
                            sat.add_task(task)
                        task.start(task.assigned_satellite, task.actual_start)
                    else:
                        task.fail(self.current_time)
                        self.stats["total_tasks_failed"] += 1

            elif task.status == TaskStatus.RUNNING:
                if task.is_expired(self.current_time) and (task.actual_end is None or self.current_time < task.actual_end):
                    task.status = TaskStatus.TIMEOUT
                    self.stats["total_tasks_failed"] += 1
                    if task.assigned_satellite in self.satellites:
                        sat = self.satellites[task.assigned_satellite]
                        sat.failed_tasks += 1
                        sat.remove_task(task.id)
                    continue

                # 检查是否完成
                if task.actual_end and self.current_time >= task.actual_end:
                    task.complete(self.current_time)
                    self.stats["total_tasks_completed"] += 1

                    # 更新卫星统计
                    if task.assigned_satellite in self.satellites:
                        sat = self.satellites[task.assigned_satellite]
                        sat.completed_tasks += 1
                        sat.remove_task(task.id)

    def add_task(self, task: Task) -> bool:
        """添加任务"""
        self.tasks[task.id] = task
        self.stats["total_tasks_generated"] += 1
        return True

    def remove_task(self, task_id: str) -> bool:
        """移除任务"""
        if task_id in self.tasks:
            del self.tasks[task_id]
            return True
        return False

    def clear_tasks(self):
        """Clear all tasks and related counters."""
        self.tasks = {}
        self.stats["total_tasks_generated"] = 0
        self.stats["total_tasks_completed"] = 0
        self.stats["total_tasks_failed"] = 0
        for sat in self.satellites.values():
            sat.task_queue.clear()
            sat.current_load = 0.0
            sat.completed_tasks = 0
            sat.failed_tasks = 0

    def run_scheduling(self) -> Optional[ScheduleResult]:
        """执行调度"""
        if not self.is_running:
            return None

        pending_tasks = [t for t in self.tasks.values()
                        if t.status == TaskStatus.PENDING]

        if not pending_tasks:
            return None

        result = self.scheduler.run_scheduling(
            algorithm=self.active_algorithm,
            tasks=pending_tasks,
            satellites=list(self.satellites.values()),
            ground_stations=list(self.ground_stations.values()),
            time_start=self.current_time,
            time_end=self.current_time + timedelta(hours=24)
        )

        # 应用调度结果
        if result:
            for task in result.tasks:
                if task.id in self.tasks:
                    self.tasks[task.id] = task

        return result

    def get_status(self) -> Dict:
        """获取仿真状态"""
        return {
            "is_running": self.is_running,
            "is_paused": self.is_paused,
            "current_time": self.current_time.isoformat() if self.current_time else None,
            "sim_time": self.sim_time.isoformat() if self.sim_time else None,
            "start_time": self.start_time.isoformat() if self.start_time else None,
            "time_speed": self.time_speed,
            "speed_factor": self.speed_factor,
            "earth_rotation_angle": self.get_earth_rotation_angle(),
            "active_algorithm": self.active_algorithm,
            "stats": self.stats,
            "satellites_count": len(self.satellites),
            "ground_stations_count": len(self.ground_stations),
            "tasks_count": {
                "total": len(self.tasks),
                "pending": len([t for t in self.tasks.values() if t.status == TaskStatus.PENDING]),
                "assigned": len([t for t in self.tasks.values() if t.status == TaskStatus.ASSIGNED]),
                "running": len([t for t in self.tasks.values() if t.status == TaskStatus.RUNNING]),
                "completed": len([t for t in self.tasks.values() if t.status == TaskStatus.COMPLETED]),
                "failed": len([t for t in self.tasks.values() if t.status in [TaskStatus.FAILED, TaskStatus.TIMEOUT]])
            }
        }

    def get_earth_rotation_angle(self) -> float:
        """
        Calculate Earth rotation angle based on sim_time.
        Returns angle in radians since simulation start.
        """
        if self.sim_time is None or self.start_time is None:
            return 0.0
        elapsed_seconds = (self.sim_time - self.start_time).total_seconds()
        return elapsed_seconds * self.EARTH_ANGULAR_VELOCITY

    def set_speed_factor(self, speed_factor: float) -> Dict:
        """Set time acceleration speed factor."""
        self.speed_factor = max(1.0, min(3600.0, speed_factor))  # Clamp between 1x and 3600x
        return {
            "speed_factor": self.speed_factor,
            "sim_time": self.sim_time.isoformat() if self.sim_time else None
        }

    def get_time_info(self) -> Dict:
        """Get detailed time information."""
        return {
            "sim_time": self.sim_time.isoformat() if self.sim_time else None,
            "start_time": self.start_time.isoformat() if self.start_time else None,
            "speed_factor": self.speed_factor,
            "earth_rotation_angle_rad": self.get_earth_rotation_angle(),
            "earth_rotation_angle_deg": math.degrees(self.get_earth_rotation_angle()),
            "is_running": self.is_running,
            "is_paused": self.is_paused
        }

    def generate_random_tasks(self, count: int = 10,
                             size_range: tuple = (100, 1000),
                             priority_distribution: Optional[List[float]] = None) -> List[Task]:
        """Generate random tasks and add them into the engine task map."""
        import random

        if priority_distribution is None:
            priority_distribution = [0.1, 0.2, 0.4, 0.2, 0.1]

        if self.current_time is None:
            if self.orbit_calc and hasattr(self.orbit_calc, "get_reference_time"):
                self.current_time = ensure_utc(self.orbit_calc.get_reference_time())
            else:
                self.current_time = utc_now()

        tasks = []
        for i in range(count):
            # Random priority and task size.
            priority = random.choices([1, 2, 3, 4, 5], weights=priority_distribution)[0]
            size = random.uniform(size_range[0], size_range[1])

            # Arrival within next 60 minutes.
            arrival_offset = random.randint(0, 3600)
            arrival_time = self.current_time + timedelta(seconds=arrival_offset)

            # Deadline = arrival + estimated processing + slack.
            estimated_processing = size / 500
            slack = random.uniform(300, 1800)
            deadline = arrival_time + timedelta(seconds=estimated_processing + slack)

            task = Task(
                id=f"task_{uuid.uuid4().hex[:8]}",
                size=size,
                priority=priority,
                arrival_time=arrival_time,
                deadline=deadline
            )

            self.add_task(task)
            tasks.append(task)

        return tasks


