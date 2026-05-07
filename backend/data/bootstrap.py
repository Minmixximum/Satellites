"""Seed data and scenario selection helpers."""

from __future__ import annotations

from datetime import datetime, timedelta, timezone
from typing import Dict, List


MAX_GROUND_STATIONS = 10
MAX_TASKS = 20


GROUND_STATION_SEEDS: List[Dict] = [
    {
        "id": "gs_001",
        "name": "NASA DSN Goldstone",
        "latitude": 35.4267,
        "longitude": -116.89,
        "altitude": 1.0,
    },
    {
        "id": "gs_002",
        "name": "NASA DSN Madrid",
        "latitude": 40.4314,
        "longitude": -4.2481,
        "altitude": 0.865,
    },
    {
        "id": "gs_003",
        "name": "NASA DSN Canberra",
        "latitude": -35.4014,
        "longitude": 148.9817,
        "altitude": 0.688,
    },
    {
        "id": "gs_004",
        "name": "ESA Kiruna",
        "latitude": 67.8571,
        "longitude": 20.9643,
        "altitude": 0.4,
    },
    {
        "id": "gs_005",
        "name": "ESA Kourou",
        "latitude": 5.2514,
        "longitude": -52.805,
        "altitude": 0.02,
    },
    {
        "id": "gs_006",
        "name": "ESA New Norcia",
        "latitude": -31.0482,
        "longitude": 116.1915,
        "altitude": 0.252,
    },
    {
        "id": "gs_007",
        "name": "ESA Cebreros",
        "latitude": 40.4527,
        "longitude": -4.3676,
        "altitude": 0.794,
    },
    {
        "id": "gs_008",
        "name": "ESA Malargue",
        "latitude": -35.775,
        "longitude": -69.398,
        "altitude": 1.55,
    },
    {
        "id": "gs_009",
        "name": "KSAT Svalbard",
        "latitude": 78.2298,
        "longitude": 15.4078,
        "altitude": 0.45,
    },
    {
        "id": "gs_010",
        "name": "SSC Esrange",
        "latitude": 67.89,
        "longitude": 21.08,
        "altitude": 0.42,
    },
]


TASK_BLUEPRINTS: List[Dict] = [
    {"size": 200, "priority": 5, "input": 12, "output": 6, "lat": 39.9042, "lon": 116.4074},
    {"size": 350, "priority": 3, "input": 18, "output": 8, "lat": 31.2304, "lon": 121.4737},
    {"size": 500, "priority": 4, "input": 22, "output": 11, "lat": 22.3193, "lon": 114.1694},
    {"size": 650, "priority": 2, "input": 28, "output": 14, "lat": 35.6762, "lon": 139.6503},
    {"size": 800, "priority": 5, "input": 34, "output": 16, "lat": 37.5665, "lon": 126.978},
    {"size": 300, "priority": 1, "input": 15, "output": 7, "lat": 1.3521, "lon": 103.8198},
    {"size": 450, "priority": 3, "input": 20, "output": 10, "lat": -33.8688, "lon": 151.2093},
    {"size": 600, "priority": 4, "input": 26, "output": 13, "lat": -35.2809, "lon": 149.13},
    {"size": 750, "priority": 2, "input": 32, "output": 15, "lat": 51.5074, "lon": -0.1278},
    {"size": 900, "priority": 5, "input": 38, "output": 19, "lat": 48.8566, "lon": 2.3522},
    {"size": 250, "priority": 3, "input": 14, "output": 6, "lat": 52.52, "lon": 13.405},
    {"size": 400, "priority": 1, "input": 19, "output": 9, "lat": 40.4168, "lon": -3.7038},
    {"size": 550, "priority": 4, "input": 24, "output": 12, "lat": 41.9028, "lon": 12.4964},
    {"size": 700, "priority": 2, "input": 30, "output": 14, "lat": 55.7558, "lon": 37.6173},
    {"size": 850, "priority": 5, "input": 36, "output": 18, "lat": 25.2048, "lon": 55.2708},
    {"size": 320, "priority": 3, "input": 16, "output": 8, "lat": -1.2921, "lon": 36.8219},
    {"size": 480, "priority": 2, "input": 21, "output": 10, "lat": -23.5505, "lon": -46.6333},
    {"size": 620, "priority": 4, "input": 27, "output": 13, "lat": 19.4326, "lon": -99.1332},
    {"size": 780, "priority": 1, "input": 33, "output": 16, "lat": 40.7128, "lon": -74.006},
    {"size": 950, "priority": 5, "input": 40, "output": 20, "lat": 34.0522, "lon": -118.2437},
]


def _utc_now() -> datetime:
    return datetime.now(timezone.utc)


def _task_template(index: int, now: datetime) -> Dict:
    blueprint = TASK_BLUEPRINTS[index - 1]
    arrival = now + timedelta(seconds=index * 10)
    deadline = arrival + timedelta(minutes=90 + (index % 6) * 20)
    return {
        "id": f"task_{index:03d}",
        "size": float(blueprint["size"]),
        "priority": int(blueprint["priority"]),
        "task_type": "computing",
        "input_data_size": float(blueprint["input"]),
        "output_data_size": float(blueprint["output"]),
        "arrival_time": arrival.isoformat(timespec="seconds"),
        "deadline": deadline.isoformat(timespec="seconds"),
        "status": "template",
        "assigned_satellite": None,
        "actual_start": None,
        "actual_end": None,
        "source_lat": float(blueprint["lat"]),
        "source_lon": float(blueprint["lon"]),
        "progress": 0.0,
    }


def seed_reference_data(db_manager) -> Dict[str, int]:
    """Ensure the fixed ground-station and task pools exist."""
    ground_inserted = 0
    task_inserted = 0

    for station in GROUND_STATION_SEEDS:
        if db_manager.get_ground_station(station["id"]):
            continue
        data = {
            **station,
            "min_elevation": 10.0,
            "max_range": 3000.0,
            "communication_speed": 100.0,
            "is_active": True,
        }
        db_manager.save_ground_station(data)
        ground_inserted += 1

    now = _utc_now()
    for index in range(1, MAX_TASKS + 1):
        task_id = f"task_{index:03d}"
        if db_manager.get_task(task_id):
            continue
        db_manager.save_task(_task_template(index, now))
        task_inserted += 1

    return {"ground_stations_inserted": ground_inserted, "tasks_inserted": task_inserted}


def activate_task_selection(db_manager, task_count: int) -> List[Dict]:
    """Reset all seed tasks, then activate the first task_count tasks."""
    db_manager.reset_all_tasks_to_template()

    selected = db_manager.get_tasks(limit=task_count, include_templates=True)
    now = _utc_now()

    for index, task in enumerate(selected, start=1):
        arrival = now + timedelta(seconds=(index - 1) * 10)
        deadline = arrival + timedelta(minutes=90 + (index % 6) * 20)
        db_manager.update_task(
            task["id"],
            {
                "arrival_time": arrival.isoformat(timespec="seconds"),
                "deadline": deadline.isoformat(timespec="seconds"),
                "status": "pending",
                "assigned_satellite": None,
                "actual_start": None,
                "actual_end": None,
                "algorithm": None,
                "scheduled_start": None,
                "scheduled_end": None,
                "progress": 0.0,
                "wait_time": None,
                "processing_time": None,
            },
        )

    return db_manager.get_tasks(limit=task_count)
