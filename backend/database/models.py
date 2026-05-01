"""SQLAlchemy database models for satellite scheduling system."""

from datetime import datetime
from sqlalchemy import (
    Column, String, Float, Integer, Boolean, DateTime,
    ForeignKey, Text, create_engine
)
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import relationship

Base = declarative_base()


class SatelliteDB(Base):
    """卫星数据表"""
    __tablename__ = 'satellites'

    id = Column(String(20), primary_key=True)
    name = Column(String(100), nullable=False)
    norad_id = Column(Integer)

    # TLE 数据
    tle_line1 = Column(Text, nullable=False)
    tle_line2 = Column(Text, nullable=False)
    tle_epoch = Column(DateTime)

    # 轨道参数
    inclination = Column(Float)
    altitude = Column(Float)
    mean_motion = Column(Float)

    # 资源属性
    capacity = Column(Float, default=30000.0)
    storage = Column(Float, default=512000.0)
    max_power = Column(Float, default=3000.0)

    # 运行状态
    current_power = Column(Float, default=3000.0)
    current_load = Column(Float, default=0.0)
    is_visible = Column(Boolean, default=False)

    # 统计信息
    completed_tasks = Column(Integer, default=0)
    failed_tasks = Column(Integer, default=0)

    # 时间戳
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)

    # 关系
    tasks = relationship("TaskDB", back_populates="satellite")
    visibility_records = relationship("VisibilityRecordDB", back_populates="satellite")

    def to_dict(self):
        """转换为字典"""
        return {
            'id': self.id,
            'name': self.name,
            'norad_id': self.norad_id,
            'tle_line1': self.tle_line1,
            'tle_line2': self.tle_line2,
            'tle_epoch': self.tle_epoch.isoformat() if self.tle_epoch else None,
            'inclination': self.inclination,
            'altitude': self.altitude,
            'mean_motion': self.mean_motion,
            'capacity': self.capacity,
            'storage': self.storage,
            'max_power': self.max_power,
            'current_power': self.current_power,
            'current_load': self.current_load,
            'is_visible': self.is_visible,
            'completed_tasks': self.completed_tasks,
            'failed_tasks': self.failed_tasks,
            'created_at': self.created_at.isoformat() if self.created_at else None,
            'updated_at': self.updated_at.isoformat() if self.updated_at else None,
        }


class GroundStationDB(Base):
    """地面站数据表"""
    __tablename__ = 'ground_stations'

    id = Column(String(20), primary_key=True)
    name = Column(String(100), nullable=False)

    # 地理位置
    latitude = Column(Float, nullable=False)
    longitude = Column(Float, nullable=False)
    altitude = Column(Float, default=0.0)

    # 通信参数
    min_elevation = Column(Float, default=10.0)
    max_range = Column(Float, default=3000.0)
    communication_speed = Column(Float, default=100.0)

    # 状态
    is_active = Column(Boolean, default=True)

    # 时间戳
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)

    # 关系
    visibility_records = relationship("VisibilityRecordDB", back_populates="ground_station")

    def to_dict(self):
        """转换为字典"""
        return {
            'id': self.id,
            'name': self.name,
            'latitude': self.latitude,
            'longitude': self.longitude,
            'altitude': self.altitude,
            'min_elevation': self.min_elevation,
            'max_range': self.max_range,
            'communication_speed': self.communication_speed,
            'is_active': self.is_active,
            'created_at': self.created_at.isoformat() if self.created_at else None,
            'updated_at': self.updated_at.isoformat() if self.updated_at else None,
        }


class TaskDB(Base):
    """任务数据表"""
    __tablename__ = 'tasks'

    id = Column(String(20), primary_key=True)

    # 任务属性
    size = Column(Float, nullable=False)
    priority = Column(Integer, default=3)
    task_type = Column(String(50), default='computing')

    # 数据大小
    input_data_size = Column(Float, default=0.0)
    output_data_size = Column(Float, default=0.0)

    # 时间约束
    arrival_time = Column(DateTime, nullable=False)
    deadline = Column(DateTime, nullable=False)

    # 状态
    status = Column(String(20), default='pending')

    # 分配信息
    assigned_satellite_id = Column(String(20), ForeignKey('satellites.id'))
    actual_start = Column(DateTime)
    actual_end = Column(DateTime)

    # 位置信息
    source_lat = Column(Float)
    source_lon = Column(Float)

    # 调度信息
    algorithm = Column(String(20))
    scheduled_start = Column(Float)
    scheduled_end = Column(Float)

    # 统计
    progress = Column(Float, default=0.0)
    wait_time = Column(Float)
    processing_time = Column(Float)

    # 时间戳
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)

    # 关系
    satellite = relationship("SatelliteDB", back_populates="tasks")

    def to_dict(self):
        """转换为字典"""
        return {
            'id': self.id,
            'size': self.size,
            'priority': self.priority,
            'task_type': self.task_type,
            'input_data_size': self.input_data_size,
            'output_data_size': self.output_data_size,
            'arrival_time': self.arrival_time.isoformat() if self.arrival_time else None,
            'deadline': self.deadline.isoformat() if self.deadline else None,
            'status': self.status,
            'assigned_satellite_id': self.assigned_satellite_id,
            'actual_start': self.actual_start.isoformat() if self.actual_start else None,
            'actual_end': self.actual_end.isoformat() if self.actual_end else None,
            'source_lat': self.source_lat,
            'source_lon': self.source_lon,
            'algorithm': self.algorithm,
            'scheduled_start': self.scheduled_start,
            'scheduled_end': self.scheduled_end,
            'progress': self.progress,
            'wait_time': self.wait_time,
            'processing_time': self.processing_time,
            'created_at': self.created_at.isoformat() if self.created_at else None,
            'updated_at': self.updated_at.isoformat() if self.updated_at else None,
        }


class SimulationSessionDB(Base):
    """仿真会话表"""
    __tablename__ = 'simulation_sessions'

    id = Column(Integer, primary_key=True, autoincrement=True)

    # 会话信息
    session_name = Column(String(100))
    algorithm = Column(String(20), default='fcfs')

    # 时间信息
    start_time = Column(DateTime)
    end_time = Column(DateTime)
    sim_duration = Column(Float, default=0.0)

    # 配置
    speed_factor = Column(Float, default=60.0)
    max_tasks = Column(Integer, default=100)

    # 统计结果
    total_tasks = Column(Integer, default=0)
    completed_tasks = Column(Integer, default=0)
    failed_tasks = Column(Integer, default=0)
    timeout_tasks = Column(Integer, default=0)

    # 性能指标
    completion_rate = Column(Float)
    avg_response_time = Column(Float)
    avg_turnaround_time = Column(Float)
    resource_utilization = Column(Float)

    # 状态
    status = Column(String(20), default='running')

    # 时间戳
    created_at = Column(DateTime, default=datetime.utcnow)

    def to_dict(self):
        """转换为字典"""
        return {
            'id': self.id,
            'session_name': self.session_name,
            'algorithm': self.algorithm,
            'start_time': self.start_time.isoformat() if self.start_time else None,
            'end_time': self.end_time.isoformat() if self.end_time else None,
            'sim_duration': self.sim_duration,
            'speed_factor': self.speed_factor,
            'max_tasks': self.max_tasks,
            'total_tasks': self.total_tasks,
            'completed_tasks': self.completed_tasks,
            'failed_tasks': self.failed_tasks,
            'timeout_tasks': self.timeout_tasks,
            'completion_rate': self.completion_rate,
            'avg_response_time': self.avg_response_time,
            'avg_turnaround_time': self.avg_turnaround_time,
            'resource_utilization': self.resource_utilization,
            'status': self.status,
            'created_at': self.created_at.isoformat() if self.created_at else None,
        }


class VisibilityRecordDB(Base):
    """可见性记录表"""
    __tablename__ = 'visibility_records'

    id = Column(Integer, primary_key=True, autoincrement=True)

    satellite_id = Column(String(20), ForeignKey('satellites.id'), nullable=False)
    ground_station_id = Column(String(20), ForeignKey('ground_stations.id'), nullable=False)

    # 可见窗口
    window_start = Column(DateTime, nullable=False)
    window_end = Column(DateTime, nullable=False)
    duration = Column(Float)

    # 可见性参数
    max_elevation = Column(Float)
    min_distance = Column(Float)

    # 时间戳
    recorded_at = Column(DateTime, default=datetime.utcnow)

    # 关系
    satellite = relationship("SatelliteDB", back_populates="visibility_records")
    ground_station = relationship("GroundStationDB", back_populates="visibility_records")

    def to_dict(self):
        """转换为字典"""
        return {
            'id': self.id,
            'satellite_id': self.satellite_id,
            'ground_station_id': self.ground_station_id,
            'window_start': self.window_start.isoformat() if self.window_start else None,
            'window_end': self.window_end.isoformat() if self.window_end else None,
            'duration': self.duration,
            'max_elevation': self.max_elevation,
            'min_distance': self.min_distance,
            'recorded_at': self.recorded_at.isoformat() if self.recorded_at else None,
        }


class SchedulingHistoryDB(Base):
    """调度历史表"""
    __tablename__ = 'scheduling_history'

    id = Column(Integer, primary_key=True, autoincrement=True)

    session_id = Column(Integer, ForeignKey('simulation_sessions.id'))
    task_id = Column(String(20), ForeignKey('tasks.id'), nullable=False)
    satellite_id = Column(String(20), ForeignKey('satellites.id'))

    # 调度决策
    algorithm = Column(String(20), nullable=False)
    decision_time = Column(DateTime, nullable=False)

    # 调度结果
    success = Column(Boolean, default=True)
    reason = Column(String(100))

    # 时间戳
    created_at = Column(DateTime, default=datetime.utcnow)

    def to_dict(self):
        """转换为字典"""
        return {
            'id': self.id,
            'session_id': self.session_id,
            'task_id': self.task_id,
            'satellite_id': self.satellite_id,
            'algorithm': self.algorithm,
            'decision_time': self.decision_time.isoformat() if self.decision_time else None,
            'success': self.success,
            'reason': self.reason,
            'created_at': self.created_at.isoformat() if self.created_at else None,
        }
