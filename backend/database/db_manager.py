"""Database manager for the satellite scheduling backend."""

from __future__ import annotations

import os
import threading
from contextlib import contextmanager
from datetime import datetime, timezone
from typing import Any, Dict, Iterable, List, Optional

from sqlalchemy import create_engine, desc
from sqlalchemy.orm import Session, sessionmaker
from sqlalchemy.pool import StaticPool

from .models import (
    Base,
    GroundStationDB,
    SatelliteDB,
    SchedulingHistoryDB,
    SimulationSessionDB,
    TaskDB,
    VisibilityRecordDB,
)


class DatabaseManager:
    """Small repository layer around SQLAlchemy sessions."""

    def __init__(self, db_url: str = None):
        if db_url is None:
            db_path = os.path.join(os.path.dirname(__file__), "..", "data", "satellite.db")
            os.makedirs(os.path.dirname(db_path), exist_ok=True)
            db_url = f"sqlite:///{db_path}"

        if db_url.startswith("sqlite") and db_url != "sqlite:///:memory:":
            db_path = db_url.replace("sqlite:///", "", 1)
            if db_path:
                os.makedirs(os.path.dirname(os.path.abspath(db_path)), exist_ok=True)

        if db_url == "sqlite:///:memory:":
            self.engine = create_engine(
                db_url,
                echo=False,
                connect_args={"check_same_thread": False},
                poolclass=StaticPool,
            )
        elif db_url.startswith("sqlite"):
            self.engine = create_engine(
                db_url,
                echo=False,
                connect_args={"check_same_thread": False},
            )
        else:
            self.engine = create_engine(db_url, echo=False)

        self.SessionLocal = sessionmaker(bind=self.engine, expire_on_commit=False)
        self._lock = threading.RLock()
        Base.metadata.create_all(self.engine)

    @contextmanager
    def session_scope(self) -> Iterable[Session]:
        with self._lock:
            session = self.SessionLocal()
            try:
                yield session
                session.commit()
            except Exception:
                session.rollback()
                raise
            finally:
                session.close()

    @staticmethod
    def _column_data(model, data: dict) -> dict:
        allowed = {column.name for column in model.__table__.columns}
        return {key: value for key, value in data.items() if key in allowed}

    @staticmethod
    def _parse_datetime(value):
        if isinstance(value, str) and value:
            return datetime.fromisoformat(value.replace("Z", "+00:00"))
        return value

    def _process_task_dict(self, task_dict: dict) -> dict:
        task_data = task_dict.copy()
        if "assigned_satellite" in task_data:
            task_data["assigned_satellite_id"] = task_data.pop("assigned_satellite")

        for field in (
            "arrival_time",
            "deadline",
            "actual_start",
            "actual_end",
            "created_at",
            "updated_at",
        ):
            if field in task_data:
                task_data[field] = self._parse_datetime(task_data[field])

        return self._column_data(TaskDB, task_data)

    def _process_satellite_dict(self, satellite_dict: dict) -> dict:
        data = satellite_dict.copy()
        for field in ("tle_epoch", "created_at", "updated_at"):
            if field in data:
                data[field] = self._parse_datetime(data[field])
        return self._column_data(SatelliteDB, data)

    def _process_ground_station_dict(self, ground_station_dict: dict) -> dict:
        data = ground_station_dict.copy()
        for field in ("created_at", "updated_at"):
            if field in data:
                data[field] = self._parse_datetime(data[field])
        return self._column_data(GroundStationDB, data)

    def _process_session_updates(self, updates: dict) -> dict:
        data = updates.copy()
        for field in ("start_time", "end_time", "created_at"):
            if field in data:
                data[field] = self._parse_datetime(data[field])
        return self._column_data(SimulationSessionDB, data)

    def _process_history_dict(self, history: dict) -> dict:
        data = history.copy()
        for field in ("decision_time", "created_at"):
            if field in data:
                data[field] = self._parse_datetime(data[field])
        return self._column_data(SchedulingHistoryDB, data)

    def _upsert(self, session: Session, model, data: dict):
        existing = session.get(model, data["id"])
        if existing:
            for key, value in data.items():
                setattr(existing, key, value)
            return existing, False

        row = model(**data)
        session.add(row)
        return row, True

    @staticmethod
    def _normalize_compare_value(value):
        if isinstance(value, datetime):
            if value.tzinfo is not None:
                return value.astimezone(timezone.utc).replace(tzinfo=None)
            return value
        return value

    def _sync_snapshot_batch(self, session: Session, model, records: List[dict]) -> Dict[str, int]:
        prepared: List[dict] = []
        record_ids: List[str] = []

        for record in records:
            if not isinstance(record, dict):
                continue
            data = self._column_data(model, record)
            if not data.get("id"):
                continue
            prepared.append(data)
            record_ids.append(data["id"])

        if not prepared:
            return {"inserted": 0, "updated": 0, "unchanged": 0, "total": 0}

        existing_rows = (
            session.query(model)
            .filter(model.id.in_(record_ids))
            .all()
        )
        existing_by_id = {row.id: row for row in existing_rows}

        inserted = 0
        updated = 0
        unchanged = 0
        to_insert = []

        for data in prepared:
            existing = existing_by_id.get(data["id"])
            if existing is None:
                to_insert.append(model(**data))
                inserted += 1
                continue

            changed = False
            for key, value in data.items():
                current = getattr(existing, key)
                if self._normalize_compare_value(current) != self._normalize_compare_value(value):
                    setattr(existing, key, value)
                    changed = True

            if changed:
                if hasattr(existing, "updated_at"):
                    existing.updated_at = datetime.utcnow()
                updated += 1
            else:
                unchanged += 1

        if to_insert:
            session.add_all(to_insert)

        return {
            "inserted": inserted,
            "updated": updated,
            "unchanged": unchanged,
            "total": len(prepared),
        }

    # Satellite operations

    def save_satellite(self, satellite_dict: dict) -> SatelliteDB:
        data = self._process_satellite_dict(satellite_dict)
        with self.session_scope() as session:
            sat, _ = self._upsert(session, SatelliteDB, data)
            return sat

    def save_satellites_batch(self, satellites: List[dict]) -> Dict[str, int]:
        with self.session_scope() as session:
            prepared = [self._process_satellite_dict(satellite) for satellite in satellites]
            return self._sync_snapshot_batch(session, SatelliteDB, prepared)

    def get_satellite(self, sat_id: str) -> Optional[Dict]:
        with self.session_scope() as session:
            sat = session.get(SatelliteDB, sat_id)
            return sat.to_dict() if sat else None

    def get_all_satellites(self) -> List[Dict]:
        with self.session_scope() as session:
            return [sat.to_dict() for sat in session.query(SatelliteDB).all()]

    def update_satellite(self, sat_id: str, updates: dict) -> bool:
        data = self._process_satellite_dict(updates)
        with self.session_scope() as session:
            sat = session.get(SatelliteDB, sat_id)
            if not sat:
                return False
            for key, value in data.items():
                setattr(sat, key, value)
            sat.updated_at = datetime.utcnow()
            return True

    def delete_satellite(self, sat_id: str) -> bool:
        with self.session_scope() as session:
            sat = session.get(SatelliteDB, sat_id)
            if not sat:
                return False
            session.delete(sat)
            return True

    # Ground station operations

    def save_ground_station(self, gs_dict: dict) -> GroundStationDB:
        data = self._process_ground_station_dict(gs_dict)
        with self.session_scope() as session:
            gs, _ = self._upsert(session, GroundStationDB, data)
            return gs

    def save_ground_stations_batch(self, ground_stations: List[dict]) -> Dict[str, int]:
        with self.session_scope() as session:
            prepared = [self._process_ground_station_dict(ground_station) for ground_station in ground_stations]
            return self._sync_snapshot_batch(session, GroundStationDB, prepared)

    def get_ground_station(self, gs_id: str) -> Optional[Dict]:
        with self.session_scope() as session:
            gs = session.get(GroundStationDB, gs_id)
            return gs.to_dict() if gs else None

    def get_all_ground_stations(self) -> List[Dict]:
        return self.get_ground_stations(active_only=False)

    def get_ground_stations(self, limit: Optional[int] = None, active_only: bool = True) -> List[Dict]:
        with self.session_scope() as session:
            query = session.query(GroundStationDB)
            if active_only:
                query = query.filter_by(is_active=True)
            query = query.order_by(GroundStationDB.id.asc())
            if limit is not None:
                query = query.limit(limit)
            return [gs.to_dict() for gs in query.all()]

    def update_ground_station(self, gs_id: str, updates: dict) -> bool:
        data = self._process_ground_station_dict(updates)
        with self.session_scope() as session:
            gs = session.get(GroundStationDB, gs_id)
            if not gs:
                return False
            for key, value in data.items():
                setattr(gs, key, value)
            gs.updated_at = datetime.utcnow()
            return True

    # Task operations

    def save_task(self, task_dict: dict) -> TaskDB:
        data = self._process_task_dict(task_dict)
        with self.session_scope() as session:
            task, _ = self._upsert(session, TaskDB, data)
            return task

    def save_tasks_batch(self, tasks: List[dict]) -> int:
        created = 0
        with self.session_scope() as session:
            for task_dict in tasks:
                data = self._process_task_dict(task_dict)
                if not data.get("id"):
                    continue
                _, was_created = self._upsert(session, TaskDB, data)
                created += int(was_created)
        return created

    def get_task(self, task_id: str) -> Optional[Dict]:
        with self.session_scope() as session:
            task = session.get(TaskDB, task_id)
            return task.to_dict() if task else None

    def get_all_tasks(self, include_templates: bool = False) -> List[Dict]:
        return self.get_tasks(include_templates=include_templates)

    def get_tasks(self, limit: Optional[int] = None, include_templates: bool = False) -> List[Dict]:
        with self.session_scope() as session:
            query = session.query(TaskDB)
            if not include_templates:
                query = query.filter(TaskDB.status != "template")
            query = query.order_by(TaskDB.id.asc())
            if limit is not None:
                query = query.limit(limit)
            return [task.to_dict() for task in query.all()]

    def get_tasks_by_status(self, status: str) -> List[Dict]:
        with self.session_scope() as session:
            query = session.query(TaskDB).filter_by(status=status)
            if status != "template":
                query = query.filter(TaskDB.status != "template")
            tasks = query.order_by(TaskDB.id.asc()).all()
            return [task.to_dict() for task in tasks]

    def get_tasks_by_statuses(self, statuses: List[str]) -> List[Dict]:
        with self.session_scope() as session:
            query = session.query(TaskDB).filter(TaskDB.status.in_(statuses))
            if "template" not in statuses:
                query = query.filter(TaskDB.status != "template")
            tasks = query.order_by(TaskDB.id.asc()).all()
            return [task.to_dict() for task in tasks]

    def get_pending_tasks(self) -> List[Dict]:
        return self.get_tasks_by_status("pending")

    def update_task(self, task_id: str, updates: dict) -> bool:
        data = self._process_task_dict(updates)
        with self.session_scope() as session:
            task = session.get(TaskDB, task_id)
            if not task:
                return False
            for key, value in data.items():
                setattr(task, key, value)
            task.updated_at = datetime.utcnow()
            return True

    def update_task_status(self, task_id: str, status: str, **kwargs) -> bool:
        return self.update_task(task_id, {"status": status, **kwargs})

    def delete_task(self, task_id: str) -> bool:
        with self.session_scope() as session:
            task = session.get(TaskDB, task_id)
            if not task:
                return False
            session.delete(task)
            return True

    def clear_all_tasks(self) -> int:
        with self.session_scope() as session:
            count = session.query(TaskDB).filter(TaskDB.status != "template").count()
            session.query(SchedulingHistoryDB).delete()
            active_tasks = session.query(TaskDB).filter(TaskDB.status != "template").all()
            now = datetime.utcnow()
            for task in active_tasks:
                task.status = "template"
                task.assigned_satellite_id = None
                task.actual_start = None
                task.actual_end = None
                task.algorithm = None
                task.scheduled_start = None
                task.scheduled_end = None
                task.progress = 0.0
                task.wait_time = None
                task.processing_time = None
                task.updated_at = now
            return count

    def reset_all_tasks_to_template(self) -> int:
        with self.session_scope() as session:
            tasks = session.query(TaskDB).all()
            now = datetime.utcnow()
            for task in tasks:
                task.status = "template"
                task.assigned_satellite_id = None
                task.actual_start = None
                task.actual_end = None
                task.algorithm = None
                task.scheduled_start = None
                task.scheduled_end = None
                task.progress = 0.0
                task.wait_time = None
                task.processing_time = None
                task.updated_at = now
            return len(tasks)

    def get_task_statistics(self) -> Dict[str, Any]:
        with self.session_scope() as session:
            active_query = session.query(TaskDB).filter(TaskDB.status != "template")
            total = active_query.count()
            pending = active_query.filter_by(status="pending").count()
            assigned = active_query.filter_by(status="assigned").count()
            running = active_query.filter_by(status="running").count()
            completed = active_query.filter_by(status="completed").count()
            failed = active_query.filter_by(status="failed").count()
            timeout = active_query.filter_by(status="timeout").count()

            return {
                "total": total,
                "pending": pending,
                "assigned": assigned,
                "running": running,
                "completed": completed,
                "failed": failed,
                "timeout": timeout,
                "completion_rate": completed / total if total > 0 else 0.0,
            }

    # Simulation session operations

    def create_session(
        self,
        algorithm: str = "fcfs",
        speed_factor: float = 60.0,
        session_name: str = None,
        max_tasks: int = 100,
    ) -> int:
        with self.session_scope() as session:
            sim_session = SimulationSessionDB(
                session_name=session_name,
                algorithm=algorithm,
                speed_factor=speed_factor,
                max_tasks=max_tasks,
                start_time=datetime.utcnow(),
                status="running",
            )
            session.add(sim_session)
            session.flush()
            return sim_session.id

    def get_simulation_session(self, session_id: int) -> Optional[Dict]:
        with self.session_scope() as session:
            sim_session = session.get(SimulationSessionDB, session_id)
            return sim_session.to_dict() if sim_session else None

    def get_recent_sessions(self, limit: int = 10) -> List[Dict]:
        with self.session_scope() as session:
            sessions = (
                session.query(SimulationSessionDB)
                .order_by(desc(SimulationSessionDB.created_at))
                .limit(limit)
                .all()
            )
            return [sim_session.to_dict() for sim_session in sessions]

    def update_session(self, session_id: int, updates: dict) -> bool:
        data = self._process_session_updates(updates)
        with self.session_scope() as session:
            sim_session = session.get(SimulationSessionDB, session_id)
            if not sim_session:
                return False
            for key, value in data.items():
                setattr(sim_session, key, value)
            return True

    def complete_session(self, session_id: int, results: dict = None) -> bool:
        updates = {"status": "completed", "end_time": datetime.utcnow()}
        if results:
            updates.update(results)
        return self.update_session(session_id, updates)

    def get_session_statistics(self, session_id: int) -> Optional[Dict]:
        session_data = self.get_simulation_session(session_id)
        if not session_data:
            return None

        with self.session_scope() as session:
            history = session.query(SchedulingHistoryDB).filter_by(session_id=session_id).all()
            total_decisions = len(history)
            successful = sum(1 for row in history if row.success)

            return {
                **session_data,
                "scheduling_decisions": total_decisions,
                "successful_assignments": successful,
                "failed_assignments": total_decisions - successful,
            }

    # Visibility and scheduling history operations

    def save_visibility_record(self, record: dict) -> VisibilityRecordDB:
        data = self._column_data(VisibilityRecordDB, record)
        with self.session_scope() as session:
            rec = VisibilityRecordDB(**data)
            session.add(rec)
            return rec

    def get_visibility_records(
        self,
        satellite_id: str = None,
        ground_station_id: str = None,
    ) -> List[Dict]:
        with self.session_scope() as session:
            query = session.query(VisibilityRecordDB)
            if satellite_id:
                query = query.filter_by(satellite_id=satellite_id)
            if ground_station_id:
                query = query.filter_by(ground_station_id=ground_station_id)
            return [record.to_dict() for record in query.all()]

    def save_scheduling_history(self, history: dict) -> SchedulingHistoryDB:
        data = self._process_history_dict(history)
        with self.session_scope() as session:
            row = SchedulingHistoryDB(**data)
            session.add(row)
            return row

    def get_scheduling_history(
        self,
        session_id: int = None,
        algorithm: str = None,
    ) -> List[Dict]:
        with self.session_scope() as session:
            query = session.query(SchedulingHistoryDB)
            if session_id:
                query = query.filter_by(session_id=session_id)
            if algorithm:
                query = query.filter_by(algorithm=algorithm)
            return [row.to_dict() for row in query.all()]
