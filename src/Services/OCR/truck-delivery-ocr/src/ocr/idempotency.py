"""Redis-backed idempotency store — prevents duplicate OCR processing of the same message."""

from __future__ import annotations

import redis

from ocr.config import settings


class RedisIdempotencyStore:
    def __init__(self) -> None:
        self._client = redis.from_url(settings.redis_url, decode_responses=True)

    def has_processed(self, message_id: str) -> bool:
        return self._client.exists(f"ocr:idempotency:{message_id}") == 1

    def mark_processed(self, message_id: str) -> None:
        self._client.set(
            f"ocr:idempotency:{message_id}",
            "1",
            ex=settings.idempotency_ttl_seconds,
        )
