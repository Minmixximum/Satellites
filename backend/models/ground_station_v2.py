"""Ground station model and visibility utilities."""

from __future__ import annotations

from dataclasses import dataclass, field
import math
from typing import Dict, List

EARTH_RADIUS = 6371.0  # km


@dataclass
class GroundStation:
    id: str
    name: str

    latitude: float
    longitude: float
    altitude: float

    min_elevation: float = 10.0
    max_range: float = 3000.0
    communication_speed: float = 100.0

    connected_satellites: List[str] = field(default_factory=list)
    is_active: bool = True

    def __post_init__(self):
        self.latitude = max(-90.0, min(90.0, float(self.latitude)))
        self.longitude = ((float(self.longitude) + 180.0) % 360.0) - 180.0

        if not (0.0 <= self.min_elevation <= 90.0):
            raise ValueError(f"min_elevation must be in [0, 90], got {self.min_elevation}")
        if self.max_range <= 0:
            raise ValueError(f"max_range must be positive, got {self.max_range}")
        if self.communication_speed < 0:
            raise ValueError(f"communication_speed cannot be negative, got {self.communication_speed}")

    def _geometric_parameters(self, sat_lat: float, sat_lon: float, sat_alt: float):
        lat1 = math.radians(self.latitude)
        lon1 = math.radians(self.longitude)
        lat2 = math.radians(sat_lat)
        lon2 = math.radians(sat_lon)

        dlon = lon2 - lon1
        dlat = lat2 - lat1
        a = math.sin(dlat / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin(dlon / 2) ** 2
        a = max(0.0, min(1.0, a))
        theta = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))

        r_gs = EARTH_RADIUS + self.altitude
        r_sat = EARTH_RADIUS + sat_alt

        distance = math.sqrt(r_gs**2 + r_sat**2 - 2 * r_gs * r_sat * math.cos(theta))

        numerator = r_sat * math.cos(theta) - r_gs
        denominator = r_sat * math.sin(theta)

        if denominator == 0 and numerator == 0:
            elevation = 90.0
        else:
            elevation = math.degrees(math.atan2(numerator, denominator))
            elevation = max(-90.0, min(90.0, elevation))

        return theta, distance, elevation

    def is_satellite_visible(self, sat_lat: float, sat_lon: float, sat_alt: float) -> bool:
        _, distance, elevation = self._geometric_parameters(sat_lat, sat_lon, sat_alt)
        if distance > self.max_range:
            return False
        if elevation < self.min_elevation:
            return False
        return True

    def get_visibility_score(self, sat_lat: float, sat_lon: float, sat_alt: float) -> float:
        if not self.is_satellite_visible(sat_lat, sat_lon, sat_alt):
            return 0.0

        _, distance, elevation = self._geometric_parameters(sat_lat, sat_lon, sat_alt)

        clipped_elevation = min(elevation, 90.0)
        elevation_score = (clipped_elevation - self.min_elevation) / (90.0 - self.min_elevation)
        elevation_score = max(0.0, min(1.0, elevation_score))

        distance_score = 1.0 - (distance / self.max_range)
        distance_score = max(0.0, min(1.0, distance_score))

        score = elevation_score * 0.6 + distance_score * 0.4
        return max(0.0, min(1.0, score))

    def to_dict(self) -> Dict:
        return {
            "id": self.id,
            "name": self.name,
            "latitude": self.latitude,
            "longitude": self.longitude,
            "altitude": self.altitude,
            "min_elevation": self.min_elevation,
            "max_range": self.max_range,
            "communication_speed": self.communication_speed,
            "connected_satellites": self.connected_satellites.copy(),
            "is_active": self.is_active,
        }

    @classmethod
    def from_dict(cls, data: Dict) -> "GroundStation":
        gs = cls(
            id=data["id"],
            name=data["name"],
            latitude=float(data["latitude"]),
            longitude=float(data["longitude"]),
            altitude=float(data.get("altitude", 0.0)),
            min_elevation=float(data.get("min_elevation", 10.0)),
            max_range=float(data.get("max_range", 3000.0)),
            communication_speed=float(data.get("communication_speed", 100.0)),
        )
        gs.connected_satellites = list(data.get("connected_satellites", []))
        gs.is_active = bool(data.get("is_active", True))
        return gs
