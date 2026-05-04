from __future__ import annotations

from typing import Literal
from uuid import UUID

from pydantic import BaseModel


class IdCardSuggestedValues(BaseModel):
    first_name: str
    last_name: str
    date_of_birth: str   # ISO date string
    address: str


class CCCDExtraction(BaseModel):
    id_number: str
    full_name: str
    date_of_birth: str           # ISO date "YYYY-MM-DD"
    gender: str                  # "Nam" | "Nữ"
    nationality: str
    place_of_origin: str
    place_of_residence: str
    expiry_date: str             # ISO date
    confidence: float            # 0.0–1.0
    raw_text: str
    suggested_form_values: IdCardSuggestedValues | None = None


class LicenseSuggestedValues(BaseModel):
    license_number: str
    license_grade: str
    license_expiry_date: str     # ISO date


class LicenseExtraction(BaseModel):
    license_number: str
    full_name: str
    date_of_birth: str
    address: str
    license_grade: str           # "B1" | "B2" | "C" | "D" | "E" | "FC" | "FD"
    issue_date: str
    expiry_date: str
    issuing_authority: str
    confidence: float
    raw_text: str
    suggested_form_values: LicenseSuggestedValues | None = None


class VehicleRegSuggestedValues(BaseModel):
    license_plate: str
    registration_number: str


class VehicleRegistrationExtraction(BaseModel):
    license_plate: str
    brand: str
    model: str
    year_of_manufacture: int | None
    chassis_number: str
    engine_number: str
    registration_number: str
    owner_name: str
    owner_id_number: str
    expiry_date: str
    confidence: float
    raw_text: str
    suggested_form_values: VehicleRegSuggestedValues | None = None


class FieldMatchResult(BaseModel):
    field: str
    ocr_value: str
    submitted_value: str
    match_score: float           # 0.0–1.0
    is_critical: bool


class DocumentMatchResult(BaseModel):
    document_type: str           # "cccd" | "license" | "vehicle_reg"
    confidence: float
    matched_fields: list[FieldMatchResult]
    critical_mismatch: bool


class OverallVerificationResult(BaseModel):
    driver_id: UUID
    cccd_match: DocumentMatchResult
    license_match: DocumentMatchResult
    vehicle_reg_match: DocumentMatchResult
    overall_confidence: float
    cross_checks_passed: bool
    status: Literal["ocr_verified", "manual_review", "rejected"]
    rejection_reasons: list[str]
