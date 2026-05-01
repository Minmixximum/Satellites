"""Database manager for satellite scheduling system."""

import os
from contextlib import contextmanager
from datetime import datetime
from typing import List, Optional, Dict, Any

from sqlalchemy import create_engine, desc
from sqlalchemy.orm import sessionmaker, Session
from sqlalchemy.pool import StaticPool

from .models import (
    Base,
    SatelliteDB,
    GroundStationDB,
    TaskDB,
    SimulationSessionDB,
    VisibilityRecordDB,
    SchedulingHistoryDB,
)


class DatabaseManager:
    """数据库管理器"""

    def __init__(self, db_url: str = None):
        """
        初始化数据库管理器

        Args:
            db_url: 数据库连接字符串
                - SQLite: 'sqlite:///path/to/database.db'
                - PostgreSQL: 'postgresql://user:password@localhost/dbname'
                - MySQL: 'mysql+pymysql://user:password@localhost/dbname'
        """
        if db_url is None:
            # 默认使用 SQLite
            db_path = os.path.join(os.path.dirname(__file__), '..', 'data', 'satellite.db')
            os.makedirs(os.path.dirname(db_path), exist_ok=True)
            db_url = f'sqlite:///{db_path}'

        # SQLite 特殊配置
        if db_url.startswith('sqlite'):
            self.engine = create_engine(
                db_url,
                echo=False,
                connect_args={'check_same_thread': False},
                poolclass=StaticPool
            )
        else:
            self.engine = create_engine(db_url, echo=False)

        self.SessionLocal = sessionmaker(bind=self.engine)

        # 创建表
        Base.metadata.create_all(self.engine)

    @contextmanager
    def get_session(self) -> Session:
        """获取数据库会话（上下文管理器）"""
        session = self.SessionLocal()
        try:
            yield session
            session.commit()
        except Exception:
            session.rollback()
            raise
        finally:
            session.close()

    # ========== 卫星操作 ==========

    def save_satellite(self, satellite_dict: dict) -> SatelliteDB:
        """保存卫星数据"""
        with self.get_session() as session:
            # 检查是否已存在
            existing = session.query(SatelliteDB).filter_by(id=satellite_dict['id']).first()
            if existing:
                for key, value in satellite_dict.items():
                    setattr(existing, key, value)
                return existing
            else:
                sat = SatelliteDB(**satellite_dict)
                session.add(sat)
                return sat

    def save_satellites_batch(self, satellites: List[dict]) -> int:
        """批量保存卫星数据"""
        count = 0
        with self.get_session() as session:
            for sat_dict in satellites:
                existing = session.query(SatelliteDB).filter_by(id=sat_dict['id']).first()
                if existing:
                    for key, value in sat_dict.items():
                        setattr(existing, key, value)
                else:
                    sat = SatelliteDB(**sat_dict)
                    session.add(sat)
                    count += 1
        return count

    def get_satellite(self, sat_id: str) -> Optional[Dict]:
        """获取单个卫星"""
        with self.get_session() as session:
            sat = session.query(SatelliteDB).filter_by(id=sat_id).first()
            return sat.to_dict() if sat else None

    def get_all_satellites(self) -> List[Dict]:
        """获取所有卫星"""
        with self.get_session() as session:
            satellites = session.query(SatelliteDB).all()
            return [sat.to_dict() for sat in satellites]

    def update_satellite(self, sat_id: str, updates: dict) -> bool:
        """更新卫星数据"""
        with self.get_session() as session:
            sat = session.query(SatelliteDB).filter_by(id=sat_id).first()
            if sat:
                for key, value in updates.items():
                    if hasattr(sat, key):
                        setattr(sat, key, value)
                sat.updated_at = datetime.utcnow()
                return True
            return False

    def delete_satellite(self, sat_id: str) -> bool:
        """删除卫星"""
        with self.get_session() as session:
            sat = session.query(SatelliteDB).filter_by(id=sat_id).first()
            if sat:
                session.delete(sat)
                return True
            return False

    # ========== 地面站操作 ==========

    def save_ground_station(self, gs_dict: dict) -> GroundStationDB:
        """保存地面站数据"""
        with self.get_session() as session:
            existing = session.query(GroundStationDB).filter_by(id=gs_dict['id']).first()
            if existing:
                for key, value in gs_dict.items():
                    setattr(existing, key, value)
                return existing
            else:
                gs = GroundStationDB(**gs_dict)
                session.add(gs)
                return gs

    def save_ground_stations_batch(self, ground_stations: List[dict]) -> int:
        """批量保存地面站数据"""
        count = 0
        with self.get_session() as session:
            for gs_dict in ground_stations:
                existing = session.query(GroundStationDB).filter_by(id=gs_dict['id']).first()
                if existing:
                    for key, value in gs_dict.items():
                        setattr(existing, key, value)
                else:
                    gs = GroundStationDB(**gs_dict)
                    session.add(gs)
                    count += 1
        return count

    def get_ground_station(self, gs_id: str) -> Optional[Dict]:
        """获取单个地面站"""
        with self.get_session() as session:
            gs = session.query(GroundStationDB).filter_by(id=gs_id).first()
            return gs.to_dict() if gs else None

    def get_all_ground_stations(self) -> List[Dict]:
        """获取所有地面站"""
        with self.get_session() as session:
            stations = session.query(GroundStationDB).all()
            return [gs.to_dict() for gs in stations]

    def update_ground_station(self, gs_id: str, updates: dict) -> bool:
        """更新地面站数据"""
        with self.get_session() as session:
            gs = session.query(GroundStationDB).filter_by(id=gs_id).first()
            if gs:
                for key, value in updates.items():
                    if hasattr(gs, key):
                        setattr(gs, key, value)
                gs.updated_at = datetime.utcnow()
                return True
            return False

    # ========== 任务操作 ==========

    def save_task(self, task_dict: dict) -> TaskDB:
        """保存任务"""
        with self.get_session() as session:
            # 处理时间字段
            task_data = self._process_task_dict(task_dict)

            existing = session.query(TaskDB).filter_by(id=task_data['id']).first()
            if existing:
                for key, value in task_data.items():
                    setattr(existing, key, value)
                return existing
            else:
                task = TaskDB(**task_data)
                session.add(task)
                return task

    def save_tasks_batch(self, tasks: List[dict]) -> int:
        """批量保存任务"""
        count = 0
        with self.get_session() as session:
            for task_dict in tasks:
                task_data = self._process_task_dict(task_dict)

                existing = session.query(TaskDB).filter_by(id=task_data['id']).first()
                if existing:
                    for key, value in task_data.items():
                        setattr(existing, key, value)
                else:
                    task = TaskDB(**task_data)
                    session.add(task)
                    count += 1
        return count

    def _process_task_dict(self, task_dict: dict) -> dict:
        """处理任务字典，转换时间字段"""
        task_data = task_dict.copy()

        # 转换时间字符串为 datetime
        time_fields = ['arrival_time', 'deadline', 'actual_start', 'actual_end']
        for field in time_fields:
            if field in task_data and isinstance(task_data[field], str):
                try:
                    task_data[field] = datetime.fromisoformat(task_data[field].replace('Z', '+00:00'))
                except (ValueError, TypeError):
                    pass

        # 重命名字段以匹配数据库模型
        if 'assigned_satellite' in task_data:
            task_data['assigned_satellite_id'] = task_data.pop('assigned_satellite')

        return task_data

    def get_task(self, task_id: str) -> Optional[Dict]:
        """获取单个任务"""
        with self.get_session() as session:
            task = session.query(TaskDB).filter_by(id=task_id).first()
            return task.to_dict() if task else None

    def get_all_tasks(self) -> List[Dict]:
        """获取所有任务"""
        with self.get_session() as session:
            tasks = session.query(TaskDB).all()
            return [task.to_dict() for task in tasks]

    def get_tasks_by_status(self, status: str) -> List[Dict]:
        """按状态获取任务"""
        with self.get_session() as session:
            tasks = session.query(TaskDB).filter_by(status=status).all()
            return [task.to_dict() for task in tasks]

    def get_pending_tasks(self) -> List[Dict]:
        """获取所有待处理任务"""
        return self.get_tasks_by_status('pending')

    def update_task(self, task_id: str, updates: dict) -> bool:
        """更新任务"""
        with self.get_session() as session:
            task = session.query(TaskDB).filter_by(id=task_id).first()
            if task:
                # 处理时间字段
                time_fields = ['arrival_time', 'deadline', 'actual_start', 'actual_end']
                for field in time_fields:
                    if field in updates and isinstance(updates[field], str):
                        try:
                            updates[field] = datetime.fromisoformat(updates[field].replace('Z', '+00:00'))
                        except (ValueError, TypeError):
                            pass

                # 处理字段重命名
                if 'assigned_satellite' in updates:
                    updates['assigned_satellite_id'] = updates.pop('assigned_satellite')

                for key, value in updates.items():
                    if hasattr(task, key):
                        setattr(task, key, value)
                task.updated_at = datetime.utcnow()
                return True
            return False

    def update_task_status(self, task_id: str, status: str, **kwargs) -> bool:
        """更新任务状态"""
        updates = {'status': status, **kwargs}
        return self.update_task(task_id, updates)

    def delete_task(self, task_id: str) -> bool:
        """删除任务"""
        with self.get_session() as session:
            task = session.query(TaskDB).filter_by(id=task_id).first()
            if task:
                session.delete(task)
                return True
            return False

    def clear_all_tasks(self) -> int:
        """清空所有任务"""
        with self.get_session() as session:
            count = session.query(TaskDB).count()
            session.query(TaskDB).delete()
            return count

    # ========== 仿真会话操作 ==========

    def create_session(self, algorithm: str = 'fcfs', speed_factor: float = 60.0,
                       session_name: str = None) -> int:
        """
        创建仿真会话

        Returns:
            会话ID
        """
        with self.get_session() as session:
            sim_session = SimulationSessionDB(
                session_name=session_name,
                algorithm=algorithm,
                speed_factor=speed_factor,
                start_time=datetime.utcnow(),
                status='running'
            )
            session.add(sim_session)
            session.flush()
            return sim_session.id

    def get_session(self, session_id: int) -> Optional[Dict]:
        """获取仿真会话"""
        with self.get_session() as session:
            sim_session = session.query(SimulationSessionDB).filter_by(id=session_id).first()
            return sim_session.to_dict() if sim_session else None

    def get_recent_sessions(self, limit: int = 10) -> List[Dict]:
        """获取最近的仿真会话"""
        with self.get_session() as session:
            sessions = session.query(SimulationSessionDB)\
                .order_by(desc(SimulationSessionDB.created_at))\
                .limit(limit).all()
            return [s.to_dict() for s in sessions]

    def update_session(self, session_id: int, updates: dict) -> bool:
        """更新仿真会话"""
        with self.get_session() as session:
            sim_session = session.query(SimulationSessionDB).filter_by(id=session_id).first()
            if sim_session:
                for key, value in updates.items():
                    if hasattr(sim_session, key):
                        setattr(sim_session, key, value)
                return True
            return False

    def complete_session(self, session_id: int, results: dict = None) -> bool:
        """完成仿真会话"""
        updates = {
            'status': 'completed',
            'end_time': datetime.utcnow()
        }
        if results:
            updates.update(results)
        return self.update_session(session_id, updates)

    # ========== 可见性记录操作 ==========

    def save_visibility_record(self, record: dict) -> VisibilityRecordDB:
        """保存可见性记录"""
        with self.get_session() as session:
            rec = VisibilityRecordDB(**record)
            session.add(rec)
            return rec

    def get_visibility_records(self, satellite_id: str = None,
                                ground_station_id: str = None) -> List[Dict]:
        """获取可见性记录"""
        with self.get_session() as session:
            query = session.query(VisibilityRecordDB)
            if satellite_id:
                query = query.filter_by(satellite_id=satellite_id)
            if ground_station_id:
                query = query.filter_by(ground_station_id=ground_station_id)
            records = query.all()
            return [r.to_dict() for r in records]

    # ========== 调度历史操作 ==========

    def save_scheduling_history(self, history: dict) -> SchedulingHistoryDB:
        """保存调度历史"""
        with self.get_session() as session:
            h = SchedulingHistoryDB(**history)
            session.add(h)
            return h

    def get_scheduling_history(self, session_id: int = None,
                                algorithm: str = None) -> List[Dict]:
        """获取调度历史"""
        with self.get_session() as session:
            query = session.query(SchedulingHistoryDB)
            if session_id:
                query = query.filter_by(session_id=session_id)
            if algorithm:
                query = query.filter_by(algorithm=algorithm)
            history = query.all()
            return [h.to_dict() for h in history]

    # ========== 统计操作 ==========

    def get_task_statistics(self) -> Dict[str, Any]:
        """获取任务统计信息"""
        with self.get_session() as session:
            total = session.query(TaskDB).count()
            pending = session.query(TaskDB).filter_by(status='pending').count()
            assigned = session.query(TaskDB).filter_by(status='assigned').count()
            running = session.query(TaskDB).filter_by(status='running').count()
            completed = session.query(TaskDB).filter_by(status='completed').count()
            failed = session.query(TaskDB).filter_by(status='failed').count()
            timeout = session.query(TaskDB).filter_by(status='timeout').count()

            return {
                'total': total,
                'pending': pending,
                'assigned': assigned,
                'running': running,
                'completed': completed,
                'failed': failed,
                'timeout': timeout,
                'completion_rate': completed / total if total > 0 else 0.0
            }

    def get_session_statistics(self, session_id: int) -> Optional[Dict]:
        """获取仿真会话统计信息"""
        session_data = self.get_session(session_id)
        if not session_data:
            return None

        with self.get_session() as session:
            # 获取该会话的调度历史
            history = session.query(SchedulingHistoryDB)\
                .filter_by(session_id=session_id).all()

            total_decisions = len(history)
            successful = sum(1 for h in history if h.success)

            return {
                **session_data,
                'scheduling_decisions': total_decisions,
                'successful_assignments': successful,
                'failed_assignments': total_decisions - successful
            }
