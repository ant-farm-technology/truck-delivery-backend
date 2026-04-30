"""Kafka consumer for DriverDocumentsSubmittedEvent → runs full OCR verification."""

from __future__ import annotations

import asyncio
import json
import time
import uuid
from datetime import datetime

import structlog
from confluent_kafka import Consumer, KafkaError, KafkaException, Producer
from prometheus_client import Counter

from ocr.config import settings
from ocr.models.events import DriverDocumentsSubmittedEvent, DriverVerificationCompletedEvent
from ocr.services.id_card_ocr import IdCardOcrService
from ocr.services.image_loader import load_image_from_url, image_to_numpy
from ocr.services.license_ocr import LicenseOcrService
from ocr.services.vehicle_reg_ocr import VehicleRegistrationOcrService
from ocr.services.verification import compute_overall_verification

logger = structlog.get_logger(__name__)

_verification_counter = Counter(
    "ocr_verification_total",
    "Total async verifications processed",
    ["status"],
)


class DriverDocumentsConsumer:
    def __init__(self) -> None:
        self._consumer = Consumer({
            "bootstrap.servers": settings.kafka_bootstrap_servers,
            "group.id": settings.kafka_consumer_group,
            "auto.offset.reset": "earliest",
            "enable.auto.commit": False,
        })
        self._producer = Producer({
            "bootstrap.servers": settings.kafka_bootstrap_servers,
        })
        self._running = False
        self._loop = asyncio.new_event_loop()

    def start(self) -> None:
        self._running = True
        self._consumer.subscribe([settings.kafka_topic_documents_submitted])
        logger.info("kafka_consumer_subscribed", topic=settings.kafka_topic_documents_submitted)

        while self._running:
            msg = self._consumer.poll(timeout=1.0)
            if msg is None:
                continue
            if msg.error():
                if msg.error().code() == KafkaError._PARTITION_EOF:
                    continue
                logger.error("kafka_consumer_error", error=str(msg.error()))
                continue

            self._loop.run_until_complete(self._handle_message(msg))

        self._consumer.close()
        self._loop.close()

    def stop(self) -> None:
        self._running = False
        logger.info("kafka_consumer_stopping")

    async def _handle_message(self, msg) -> None:
        log = logger.bind(topic=msg.topic(), partition=msg.partition(), offset=msg.offset())

        try:
            payload = json.loads(msg.value().decode("utf-8"))
            event = DriverDocumentsSubmittedEvent(**payload)

            # Idempotency: skip duplicates via message_id check would go here
            # (using Redis store — omitted for brevity, same pattern as KafkaConsumerBase)

            log = log.bind(driver_id=str(event.driver_id), message_id=str(event.message_id))
            log.info("documents_submitted_received")

            result = await self._process_verification(event)
            self._publish_result(result)

            self._consumer.commit(msg)
            log.info("documents_verification_committed", status=result.status)

        except Exception as exc:
            log.error("documents_verification_failed", error=str(exc))
            # Route to DLQ
            self._route_to_dlq(msg, exc)
            self._consumer.commit(msg)

    async def _process_verification(self, event: DriverDocumentsSubmittedEvent) -> DriverVerificationCompletedEvent:
        id_card_svc = IdCardOcrService.get_instance()
        license_svc = LicenseOcrService.get_instance()
        vehicle_svc = VehicleRegistrationOcrService.get_instance()

        # Download all 6 document images concurrently
        (id_front, id_back, lic_front, lic_back, vreg_front, vreg_back) = await asyncio.gather(
            load_image_from_url(event.id_card_front_url),
            load_image_from_url(event.id_card_back_url),
            load_image_from_url(event.license_front_url),
            load_image_from_url(event.license_back_url),
            load_image_from_url(event.vehicle_reg_front_url),
            load_image_from_url(event.vehicle_reg_back_url),
        )

        # Convert to numpy arrays
        id_front_arr, id_back_arr = image_to_numpy(id_front), image_to_numpy(id_back)
        lic_front_arr, lic_back_arr = image_to_numpy(lic_front), image_to_numpy(lic_back)
        vr_front_arr, vr_back_arr = image_to_numpy(vreg_front), image_to_numpy(vreg_back)

        # Run OCR on all documents concurrently
        cccd, license_data, vehicle_reg = await asyncio.gather(
            id_card_svc.extract(id_front_arr, id_back_arr),
            license_svc.extract(lic_front_arr, lic_back_arr),
            vehicle_svc.extract(vr_front_arr, vr_back_arr),
        )

        submitted = {
            "full_name": event.submitted_full_name,
            "date_of_birth": event.submitted_date_of_birth,
            "id_card_number": "",  # not submitted directly — OCR extracts and verifies uniqueness
            "license_number": event.submitted_license_number,
            "license_grade": event.submitted_license_grade,
            "license_expiry": event.submitted_license_expiry,
            "license_plate": event.submitted_license_plate,
            "registration_number": event.submitted_registration_number,
        }

        overall = compute_overall_verification(
            driver_id=event.driver_id,
            cccd=cccd,
            license_data=license_data,
            vehicle_reg=vehicle_reg,
            submitted=submitted,
        )

        _verification_counter.labels(status=overall.status).inc()

        return DriverVerificationCompletedEvent(
            driver_id=event.driver_id,
            status=overall.status,
            overall_confidence=overall.overall_confidence,
            rejection_reasons=overall.rejection_reasons,
            ocr_extracted_data={
                "cccd": cccd.model_dump(exclude={"raw_text"}),
                "license": license_data.model_dump(exclude={"raw_text"}),
                "vehicle_reg": vehicle_reg.model_dump(exclude={"raw_text"}),
            },
        )

    def _publish_result(self, event: DriverVerificationCompletedEvent) -> None:
        payload = event.model_dump_json().encode("utf-8")
        self._producer.produce(
            topic=settings.kafka_topic_verification_completed,
            key=str(event.driver_id).encode("utf-8"),
            value=payload,
        )
        self._producer.flush()
        logger.info(
            "verification_result_published",
            driver_id=str(event.driver_id),
            status=event.status,
            topic=settings.kafka_topic_verification_completed,
        )

    def _route_to_dlq(self, msg, exc: Exception) -> None:
        dlq_topic = f"{settings.kafka_topic_documents_submitted}.dlq"
        try:
            self._producer.produce(
                topic=dlq_topic,
                key=msg.key(),
                value=msg.value(),
                headers={"error": str(exc).encode("utf-8")},
            )
            self._producer.flush()
            logger.warning("message_routed_to_dlq", dlq_topic=dlq_topic, error=str(exc))
        except Exception as dlq_exc:
            logger.error("dlq_publish_failed", error=str(dlq_exc))
