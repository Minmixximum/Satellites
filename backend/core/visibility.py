"""Visibility analysis utilities."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta
import math
from typing import Dict, List, Optional


EARTH_RADIUS = 6371.0


@dataclass
class VisibilityWindow:
    start_time: datetime
    end_time: datetime
    max_elevation: float
    min_distance: float

    def to_dict(self) -> Dict:
        duration = (self.end_time - self.start_time).total_seconds()
        return {
            "start_time": self.start_time.isoformat(),
            "end_time": self.end_time.isoformat(),
            "duration_seconds": duration,
            "max_elevation": self.max_elevation,
            "min_distance": self.min_distance,
        }


class VisibilityAnalyzer:
    """Calculate visibility windows between satellites and ground stations."""

    def __init__(self, orbit_calculator):
        self.orbit_calculator = orbit_calculator

    def _geometry(
        self,
        sat_lat: float,
        sat_lon: float,
        sat_alt: float,
        gs_lat: float,
        gs_lon: float,
        gs_alt: float,
    ):
        lat1 = math.radians(gs_lat)
        lon1 = math.radians(gs_lon)
        lat2 = math.radians(sat_lat)
        lon2 = math.radians(sat_lon)

        dlon = lon2 - lon1
        dlat = lat2 - lat1

        a = math.sin(dlat / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin(dlon / 2) ** 2
        a = max(0.0, min(1.0, a))
        theta = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))

        r_gs = EARTH_RADIUS + gs_alt
        r_sat = EARTH_RADIUS + sat_alt

        distance = math.sqrt(r_gs**2 + r_sat**2 - 2 * r_gs * r_sat * math.cos(theta))

        numerator = r_sat * math.cos(theta) - r_gs
        denominator = r_sat * math.sin(theta)

        if denominator == 0 and numerator == 0:
            elevation = 90.0
        else:
            elevation = math.degrees(math.atan2(numerator, denominator))
            elevation = max(-90.0, min(90.0, elevation))

        return distance, elevation

    def calculate_visibility(
        self,
        sat_id: str,
        gs_lat: float,
        gs_lon: float,
        gs_alt: float,
        dt: datetime,
        min_elevation: float = 10.0,
        max_range: float = 3000.0,
    ) -> Optional[Dict[str, float]]:
        pos = self.orbit_calculator.calculate_position(sat_id, dt)
        if not pos:
            return None

        distance, elevation = self._geometry(
            pos["lat"], pos["lon"], pos["alt"], gs_lat, gs_lon, gs_alt
        )
        is_visible = elevation >= min_elevation and distance <= max_range

        return {
            "satellite_id": sat_id,
            "timestamp": dt.isoformat(),
            "distance": distance,
            "elevation": elevation,
            "is_visible": is_visible,
        }

    def find_visibility_windows(
        self,
        sat_id: str,
        gs_lat: float,
        gs_lon: float,
        gs_alt: float,
        time_start: datetime,
        time_end: datetime,
        min_elevation: float = 10.0,
        max_range: float = 3000.0,
        time_step_seconds: int = 60,
    ) -> List[VisibilityWindow]:
        windows: List[VisibilityWindow] = []

        current = time_start
        current_window_start = None
        window_max_elevation = -90.0
        window_min_distance = float("inf")

        while current <= time_end:
            vis = self.calculate_visibility(
                sat_id,
                gs_lat,
                gs_lon,
                gs_alt,
                current,
                min_elevation=min_elevation,
                max_range=max_range,
            )

            if vis and vis["is_visible"]:
                if current_window_start is None:
                    current_window_start = current
                    window_max_elevation = vis["elevation"]
                    window_min_distance = vis["distance"]
                else:
                    window_max_elevation = max(window_max_elevation, vis["elevation"])
                    window_min_distance = min(window_min_distance, vis["distance"])
            else:
                if current_window_start is not None:
                    windows.append(
                        VisibilityWindow(
                            start_time=current_window_start,
                            end_time=current,
                            max_elevation=window_max_elevation,
                            min_distance=window_min_distance,
                        )
                    )
                    current_window_start = None
                    window_max_elevation = -90.0
                    window_min_distance = float("inf")

            current += timedelta(seconds=max(1, time_step_seconds))

        if current_window_start is not None:
            windows.append(
                VisibilityWindow(
                    start_time=current_window_start,
                    end_time=time_end,
                    max_elevation=window_max_elevation,
                    min_distance=window_min_distance,
                )
            )

        return windows
