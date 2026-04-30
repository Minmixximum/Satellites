"""Application configuration."""

from __future__ import annotations

import os


class Config:
    """Base configuration."""

    SECRET_KEY = os.environ.get("SECRET_KEY") or "dev-secret-key-satellite-scheduler"
    DEBUG = os.environ.get("FLASK_DEBUG", "True").lower() == "true"

    API_PREFIX = "/api"
    JSON_AS_ASCII = False

    DATA_SOURCE_URL = "https://celestrak.org/NORAD/elements/gp.php?GROUP=last-30-days&FORMAT=tle"

    CORS_ORIGINS = ["http://localhost:3000", "http://localhost:8080", "*"]
    CORS_METHODS = ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
    CORS_HEADERS = ["Content-Type", "Authorization"]

    DEFAULT_SATELLITE_COUNT = 5
    DEFAULT_GROUND_STATION_COUNT = 3

    ORBIT_CACHE_TTL_SECONDS = 5
    ORBIT_PREDICTION_MINUTES = 90
    TIME_STEP_SECONDS = 60

    DEFAULT_SCHEDULING_ALGORITHM = "max_visibility"
    SIMULATION_TIME_SPEED = 1.0

    DEFAULT_TASK_SIZE_MIN = 100
    DEFAULT_TASK_SIZE_MAX = 1000
    MAX_TASK_QUEUE_LENGTH = 10

    DEFAULT_MIN_ELEVATION = 10.0
    DEFAULT_MAX_RANGE = 3000.0

    BASE_DIR = os.path.dirname(os.path.abspath(__file__))
    DATA_DIR = os.path.join(BASE_DIR, "data")
    TLE_DIR = os.path.join(DATA_DIR, "tle")
    SCENARIOS_DIR = os.path.join(DATA_DIR, "scenarios")


class DevelopmentConfig(Config):
    DEBUG = True
    TESTING = False


class TestingConfig(Config):
    DEBUG = False
    TESTING = True


class ProductionConfig(Config):
    DEBUG = False
    TESTING = False
    SECRET_KEY = os.environ.get("SECRET_KEY")


config_map = {
    "development": DevelopmentConfig,
    "testing": TestingConfig,
    "production": ProductionConfig,
    "default": DevelopmentConfig,
}
