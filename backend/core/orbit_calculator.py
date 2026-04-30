"""Lightweight orbit calculator abstraction used by the API and simulation engine."""

from __future__ import annotations

import math
from dataclasses import dataclass
from datetime import datetime, timezone, timedelta
from typing import Dict, List, Optional
try:
    from core.time_utils import ensure_utc, utc_now
except ImportError:  # pragma: no cover
    from .time_utils import ensure_utc, utc_now


@dataclass
class _SatelliteOrbitParams:
    sat_id: str
    tle_line1: str
    tle_line2: str
    inclination_deg: float
    altitude_km: float
    angular_velocity_rad_s: float
    phase_rad: float
    epoch_utc: datetime


class OrbitCalculator:
    """A lightweight orbit approximation for API-level simulation."""

    # Earth rotation angular velocity (rad/s) - 360 degrees per sidereal day
    EARTH_ANGULAR_VELOCITY = 7.292115e-5

    def __init__(self):
        self.satellites: Dict[str, _SatelliteOrbitParams] = {}
        self._fallback_epoch = datetime(2026, 1, 1, tzinfo=timezone.utc)

    def get_OrbitCalculator_config(self):
        """Compatibility API retained from previous implementation."""
        earth_radius_km = 6378.137
        flattening = 1.0 / 298.257223563
        return earth_radius_km, flattening

    def add_satellite(self, sat_id: str, tle_line1: str, tle_line2: str):
        """Register a satellite from TLE lines."""
        inclination = self._parse_inclination(tle_line2)
        mean_motion = self._parse_mean_motion(tle_line2)
        epoch_utc = self._parse_tle_epoch(tle_line1) or self._fallback_epoch

        # Approximate altitude from mean motion.
        altitude = self._approx_altitude_from_mean_motion(mean_motion)
        angular_velocity = (mean_motion * 2.0 * math.pi) / (24.0 * 3600.0)

        phase = (hash(sat_id) % 360) * math.pi / 180.0

        self.satellites[sat_id] = _SatelliteOrbitParams(
            sat_id=sat_id,
            tle_line1=tle_line1,
            tle_line2=tle_line2,
            inclination_deg=inclination,
            altitude_km=altitude,
            angular_velocity_rad_s=angular_velocity,
            phase_rad=phase,
            epoch_utc=epoch_utc,
        )

    def remove_satellite(self, sat_id: str):
        self.satellites.pop(sat_id, None)

    def calculate_position(self, sat_id: str, dt: datetime) -> Optional[Dict[str, float]]:
        """
        Calculate approximate geodetic position (lat/lon/alt).

        The longitude is calculated in Earth-fixed coordinates (ECEF-like),
        accounting for Earth's rotation relative to the satellite's orbital motion.
        """
        sat = self.satellites.get(sat_id)
        if sat is None:
            return None

        dt = ensure_utc(dt)

        elapsed = (dt - sat.epoch_utc).total_seconds()

        # Satellite orbital angle (in inertial frame)
        orbital_angle = sat.angular_velocity_rad_s * elapsed + sat.phase_rad

        # Earth rotation angle since epoch
        earth_rotation = self.EARTH_ANGULAR_VELOCITY * elapsed

        inclination_rad = math.radians(sat.inclination_deg)

        # Latitude depends only on orbital angle and inclination
        lat = math.degrees(math.asin(math.sin(inclination_rad) * math.sin(orbital_angle)))

        # Longitude in Earth-fixed frame: orbital position minus Earth rotation
        # This gives the ground track position
        lon_inertial = math.degrees(orbital_angle)
        lon_earth_fixed = lon_inertial - math.degrees(earth_rotation)
        lon = lon_earth_fixed % 360.0
        lon = lon - 360.0 if lon > 180.0 else lon

        # Add small periodic altitude variation.
        alt = sat.altitude_km + 10.0 * math.sin(orbital_angle * 0.5)

        return {"lat": lat, "lon": lon, "alt": alt}

    def calculate_positions_batch(self, sat_ids: List[str], dt: datetime) -> Dict[str, Dict]:
        return {sat_id: self.calculate_position(sat_id, dt) for sat_id in sat_ids}

    def predict_orbit(
        self,
        sat_id: str,
        start_time: datetime,
        duration_minutes: int = 90,
        time_step_seconds: int = 60,
    ) -> List[Dict]:
        """Predict sampled orbit positions for a time range."""
        positions: List[Dict] = []

        steps = max(1, int((duration_minutes * 60) / max(1, time_step_seconds)))
        current = ensure_utc(start_time)

        for _ in range(steps + 1):
            pos = self.calculate_position(sat_id, current)
            if pos is not None:
                positions.append(
                    {
                        "timestamp": current.isoformat(),
                        "lat": pos["lat"],
                        "lon": pos["lon"],
                        "alt": pos["alt"],
                    }
                )
            current += timedelta(seconds=time_step_seconds)

        return positions

    def get_satellite_info(self, sat_id: str) -> Optional[Dict]:
        sat = self.satellites.get(sat_id)
        if sat is None:
            return None
        return {
            "id": sat.sat_id,
            "inclination": sat.inclination_deg,
            "altitude": sat.altitude_km,
            "mean_motion_rad_s": sat.angular_velocity_rad_s,
        }

    def get_orbital_period(self, sat_id: str) -> float:
        sat = self.satellites.get(sat_id)
        if sat is None or sat.angular_velocity_rad_s <= 0:
            return 0.0
        return (2.0 * math.pi) / sat.angular_velocity_rad_s

    def get_reference_time(self) -> datetime:
        """Return a stable UTC reference time derived from loaded TLE epochs."""
        if not self.satellites:
            return utc_now()
        return min(s.epoch_utc for s in self.satellites.values())

    def clear_cache(self):
        # Kept for compatibility; nothing cached in this lightweight version.
        return

    @staticmethod
    def _parse_inclination(tle_line2: str) -> float:
        try:
            return float(tle_line2[8:16].strip())
        except Exception:
            return 53.0

    @staticmethod
    def _parse_mean_motion(tle_line2: str) -> float:
        try:
            value = tle_line2[52:63].strip()
            return float(value) if value else 15.0
        except Exception:
            return 15.0

    @staticmethod
    def _parse_tle_epoch(tle_line1: str) -> Optional[datetime]:
        """
        Parse TLE epoch (YYDDD.DDDDDDDD) from line 1.

        Returns UTC datetime when available.
        """
        try:
            epoch_str = tle_line1[18:32].strip()
            if len(epoch_str) < 5:
                return None

            yy = int(epoch_str[:2])
            day_of_year = float(epoch_str[2:])
            year = 1900 + yy if yy >= 57 else 2000 + yy
            year_start = datetime(year, 1, 1, tzinfo=timezone.utc)
            return year_start + timedelta(days=day_of_year - 1.0)
        except Exception:
            return None

    @staticmethod
    def _approx_altitude_from_mean_motion(mean_motion_rev_day: float) -> float:
        # Very rough mapping around LEO.
        if mean_motion_rev_day <= 0:
            return 550.0
        return max(350.0, min(1200.0, 850.0 - (mean_motion_rev_day - 13.0) * 120.0))
