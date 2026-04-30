"""Shared UTC time helpers for backend modules."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Optional


def utc_now() -> datetime:
    """Return current UTC datetime (timezone-aware)."""
    return datetime.now(timezone.utc)


def ensure_utc(dt: datetime) -> datetime:
    """Normalize datetime into timezone-aware UTC."""
    if dt.tzinfo is None:
        return dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc)


def parse_iso_datetime(value: Optional[str], default: Optional[datetime] = None) -> datetime:
    """
    Parse an ISO datetime string as UTC.

    If parsing fails or value is empty, return `default` when provided,
    otherwise return current UTC time.
    """
    if not value:
        return ensure_utc(default) if default is not None else utc_now()

    try:
        return ensure_utc(datetime.fromisoformat(value))
    except (TypeError, ValueError):
        return ensure_utc(default) if default is not None else utc_now()
