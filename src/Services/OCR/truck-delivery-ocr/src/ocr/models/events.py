from __future__ import annotations

from datetime import datetime
from uuid import UUID, uuid4

from pydantic import BaseModel, Field


class DriverDocumentsSubmittedEvent(BaseModel):
    message_id: UUID = Field(default_factory=uuid4)
    occurred_at: datetime = Field(default_factory=datetime.utcnow)
    schema_version: int = 1

    driver_id: UUID
    portrait_photo_url: str
    id_card_front_url: str
    id_card_back_url: str
    license_front_url: str
    license_back_url: str
    vehicle_reg_front_url: str
    vehicle_reg_back_url: str

    submitted_full_name: str
    submitted_date_of_birth: str       # ISO date string "YYYY-MM-DD"
    submitted_license_number: str
    submitted_license_grade: str
    submitted_license_expiry: str      # ISO date string
    submitted_license_plate: str
    submitted_registration_number: str


class DriverVerificationCompletedEvent(BaseModel):
    message_id: UUID = Field(default_factory=uuid4)
    occurred_at: datetime = Field(default_factory=datetime.utcnow)
    schema_version: int = 1

    driver_id: UUID
    status: str                        # "ocr_verified" | "manual_review" | "rejected"
    overall_confidence: float
    rejection_reasons: list[str]
    ocr_extracted_data: dict           # full extraction for audit log
