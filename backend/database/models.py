"""SQLAlchemy database models for the satellite scheduling backend."""

from datetime import datetime

from sqlalchemy import Boolean, Column, DateTime, Float, ForeignKey, Integer, String, Text
from sqlalchemy.orm import declarative_base, relationship

Base = declarative_base()


class SatelliteDB(Base):
    """Persisted satellite resource metadata."""

    __tablename__ = "satellites"

    id = Column(String(20), primary_key=True)
    name = Column(String(100), nullable=False)
    norad_id = Column(Integer)

    tle_line1 = Column(Text, nullable=False)
    tle_line2 = Column(Text, nullable=False)
    tle_epoch = Column(DateTime)

    inclination = Column(Float)
    altitude = Column(Float)
    mean_motion = Column(Float)

    capacity = Column(Float, default=30000.0)
    storage = Column(Float, default=512000.0)
    max_power = Column(Float, default=3000.0)

    current_power = Column(Float, default=3000.0)
    current_load = Column(Float, default=0.0)
    is_visible = Column(Boolean, default=False)

    completed_tasks = Column(Integer, default=0)
    failed_tasks = Column(Integer, default=0)

    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)

    def to_dict(self):
        return {
            "id": self.id,
            "name": self.name,
            "norad_id": self.norad_id,
            "tle_line1": self.tle_line1,
            "tle_line2": self.tle_line2,
            "tle_epoch": self.tle_epoch.isoformat() if self.tle_epoch else None,
            "inclination": self.inclination,
            "altitude": self.altitude,
            "mean_motion": self.mean_motion,
            "capacity": self.capacity,
            "storage": self.storage,
            "max_power": self.max_power,
            "current_power": self.current_power,
            "current_load": self.current_load,
            "is_visible": self.is_visible,
            "completed_tasks": self.completed_tasks,
            "failed_tasks": self.failed_tasks,
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "updated_at": self.updated_at.isoformat() if self.updated_at else None,
        }


class GroundStationDB(Base):
    """Persisted ground station metadata."""

    __tablename__ = "ground_stations"

    id = Column(String(20), primary_key=True)
    name = Column(String(100), nullable=False)

    latitude = Column(Float, nullable=False)
    longitude = Column(Float, nullable=False)
    altitude = Column(Float, default=0.0)

    min_elevation = Column(Float, default=10.0)
    max_range = Column(Float, default=3000.0)
    communication_speed = Column(Float, default=100.0)

    is_active = Column(Boolean, default=True)

    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)

    visibility_records = relationship("VisibilityRecordDB", back_populates="ground_station")

    def to_dict(self):
        return {
            "id": self.id,
            "name": self.name,
            "latitude": self.latitude,
            "longitude": self.longitude,
            "altitude": self.altitude,
            "min_elevation": self.min_elevation,
            "max_range": self.max_range,
            "communication_speed": self.communication_speed,
            "is_active": self.is_active,
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "updated_at": self.updated_at.isoformat() if self.updated_at else None,
        }


class TaskDB(Base):
    """Persisted task state."""

    __tablename__ = "tasks"

    id = Column(String(64), primary_key=True)

    size = Column(Float, nullable=False)
    priority = Column(Integer, default=3)
    task_type = Column(String(50), default="computing")

    input_data_size = Column(Float, default=0.0)
    output_data_size = Column(Float, default=0.0)

    arrival_time = Column(DateTime, nullable=False)
    deadline = Column(DateTime, nullable=False)

    status = Column(String(20), default="pending")

    assigned_satellite_id = Column(String(20))
    actual_start = Column(DateTime)
    actual_end = Column(DateTime)

    source_lat = Column(Float)
    source_lon = Column(Float)

    algorithm = Column(String(20))
    scheduled_start = Column(Float)
    scheduled_end = Column(Float)

    progress = Column(Float, default=0.0)
    wait_time = Column(Float)
    processing_time = Column(Float)

    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)

    def to_dict(self):
        return {
            "id": self.id,
            "size": self.size,
            "priority": self.priority,
            "task_type": self.task_type,
            "input_data_size": self.input_data_size,
            "output_data_size": self.output_data_size,
            "arrival_time": self.arrival_time.isoformat() if self.arrival_time else None,
            "deadline": self.deadline.isoformat() if self.deadline else None,
            "status": self.status,
            "assigned_satellite_id": self.assigned_satellite_id,
            "actual_start": self.actual_start.isoformat() if self.actual_start else None,
            "actual_end": self.actual_end.isoformat() if self.actual_end else None,
            "source_lat": self.source_lat,
            "source_lon": self.source_lon,
            "algorithm": self.algorithm,
            "scheduled_start": self.scheduled_start,
            "scheduled_end": self.scheduled_end,
            "progress": self.progress,
            "wait_time": self.wait_time,
            "processing_time": self.processing_time,
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "updated_at": self.updated_at.isoformat() if self.updated_at else None,
        }


class SimulationSessionDB(Base):
    """Persisted simulation run metadata."""

    __tablename__ = "simulation_sessions"

    id = Column(Integer, primary_key=True, autoincrement=True)

    session_name = Column(String(100))
    algorithm = Column(String(20), default="fcfs")

    start_time = Column(DateTime)
    end_time = Column(DateTime)
    sim_duration = Column(Float, default=0.0)

    speed_factor = Column(Float, default=60.0)
    max_tasks = Column(Integer, default=100)

    total_tasks = Column(Integer, default=0)
    completed_tasks = Column(Integer, default=0)
    failed_tasks = Column(Integer, default=0)
    timeout_tasks = Column(Integer, default=0)

    completion_rate = Column(Float)
    avg_response_time = Column(Float)
    avg_turnaround_time = Column(Float)
    resource_utilization = Column(Float)

    status = Column(String(20), default="running")

    created_at = Column(DateTime, default=datetime.utcnow)

    def to_dict(self):
        return {
            "id": self.id,
            "session_name": self.session_name,
            "algorithm": self.algorithm,
            "start_time": self.start_time.isoformat() if self.start_time else None,
            "end_time": self.end_time.isoformat() if self.end_time else None,
            "sim_duration": self.sim_duration,
            "speed_factor": self.speed_factor,
            "max_tasks": self.max_tasks,
            "total_tasks": self.total_tasks,
            "completed_tasks": self.completed_tasks,
            "failed_tasks": self.failed_tasks,
            "timeout_tasks": self.timeout_tasks,
            "completion_rate": self.completion_rate,
            "avg_response_time": self.avg_response_time,
            "avg_turnaround_time": self.avg_turnaround_time,
            "resource_utilization": self.resource_utilization,
            "status": self.status,
            "created_at": self.created_at.isoformat() if self.created_at else None,
        }


class VisibilityRecordDB(Base):
    """Persisted visibility window record."""

    __tablename__ = "visibility_records"

    id = Column(Integer, primary_key=True, autoincrement=True)

    satellite_id = Column(String(20), nullable=False)
    ground_station_id = Column(String(20), ForeignKey("ground_stations.id"), nullable=False)

    window_start = Column(DateTime, nullable=False)
    window_end = Column(DateTime, nullable=False)
    duration = Column(Float)

    max_elevation = Column(Float)
    min_distance = Column(Float)

    recorded_at = Column(DateTime, default=datetime.utcnow)

    ground_station = relationship("GroundStationDB", back_populates="visibility_records")

    def to_dict(self):
        return {
            "id": self.id,
            "satellite_id": self.satellite_id,
            "ground_station_id": self.ground_station_id,
            "window_start": self.window_start.isoformat() if self.window_start else None,
            "window_end": self.window_end.isoformat() if self.window_end else None,
            "duration": self.duration,
            "max_elevation": self.max_elevation,
            "min_distance": self.min_distance,
            "recorded_at": self.recorded_at.isoformat() if self.recorded_at else None,
        }


class SchedulingHistoryDB(Base):
    """Persisted scheduling decision history."""

    __tablename__ = "scheduling_history"

    id = Column(Integer, primary_key=True, autoincrement=True)

    session_id = Column(Integer, ForeignKey("simulation_sessions.id"))
    task_id = Column(String(64), ForeignKey("tasks.id"), nullable=False)
    satellite_id = Column(String(20))

    algorithm = Column(String(20), nullable=False)
    decision_time = Column(DateTime, nullable=False)

    success = Column(Boolean, default=True)
    reason = Column(String(100))

    created_at = Column(DateTime, default=datetime.utcnow)

    def to_dict(self):
        return {
            "id": self.id,
            "session_id": self.session_id,
            "task_id": self.task_id,
            "satellite_id": self.satellite_id,
            "algorithm": self.algorithm,
            "decision_time": self.decision_time.isoformat() if self.decision_time else None,
            "success": self.success,
            "reason": self.reason,
            "created_at": self.created_at.isoformat() if self.created_at else None,
        }
