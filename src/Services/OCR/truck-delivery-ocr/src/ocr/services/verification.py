"""Cross-verification: compare OCR extracted data vs driver-submitted data."""

from __future__ import annotations

from datetime import date
from uuid import UUID

import structlog
from fuzzywuzzy import fuzz

from ocr.config import settings
from ocr.models.response import (
    CCCDExtraction,
    DocumentMatchResult,
    FieldMatchResult,
    LicenseExtraction,
    OverallVerificationResult,
    VehicleRegistrationExtraction,
)

logger = structlog.get_logger(__name__)

# License grades that are valid for truck driving (excludes B1, A-series)
_TRUCK_VALID_GRADES = {"B2", "C", "D", "E", "FC", "FD"}


def _fuzzy_score(a: str, b: str) -> float:
    if not a or not b:
        return 0.0
    return fuzz.ratio(a.upper().strip(), b.upper().strip()) / 100.0


def _exact_score(a: str, b: str) -> float:
    if not a or not b:
        return 0.0
    return 1.0 if a.strip() == b.strip() else 0.0


def _match_cccd(ocr: CCCDExtraction, submitted: dict) -> DocumentMatchResult:
    fields: list[FieldMatchResult] = [
        FieldMatchResult(
            field="id_number",
            ocr_value=ocr.id_number,
            submitted_value=submitted.get("id_card_number", ""),
            match_score=_exact_score(ocr.id_number, submitted.get("id_card_number", "")),
            is_critical=True,
        ),
        FieldMatchResult(
            field="full_name",
            ocr_value=ocr.full_name,
            submitted_value=submitted.get("full_name", ""),
            match_score=_fuzzy_score(ocr.full_name, submitted.get("full_name", "")),
            is_critical=True,
        ),
        FieldMatchResult(
            field="date_of_birth",
            ocr_value=ocr.date_of_birth,
            submitted_value=submitted.get("date_of_birth", ""),
            match_score=_exact_score(ocr.date_of_birth, submitted.get("date_of_birth", "")),
            is_critical=True,
        ),
    ]

    critical_mismatch = any(f.is_critical and f.match_score < 0.9 for f in fields)
    confidence = sum(f.match_score for f in fields) / len(fields) if fields else 0.0

    return DocumentMatchResult(
        document_type="cccd",
        confidence=round(confidence, 3),
        matched_fields=fields,
        critical_mismatch=critical_mismatch,
    )


def _match_license(ocr: LicenseExtraction, submitted: dict) -> DocumentMatchResult:
    fields: list[FieldMatchResult] = [
        FieldMatchResult(
            field="license_number",
            ocr_value=ocr.license_number,
            submitted_value=submitted.get("license_number", ""),
            match_score=_exact_score(ocr.license_number, submitted.get("license_number", "")),
            is_critical=True,
        ),
        FieldMatchResult(
            field="license_grade",
            ocr_value=ocr.license_grade,
            submitted_value=submitted.get("license_grade", ""),
            match_score=_exact_score(ocr.license_grade, submitted.get("license_grade", "")),
            is_critical=True,
        ),
        FieldMatchResult(
            field="expiry_date",
            ocr_value=ocr.expiry_date,
            submitted_value=submitted.get("license_expiry", ""),
            match_score=_exact_score(ocr.expiry_date, submitted.get("license_expiry", "")),
            is_critical=False,
        ),
        FieldMatchResult(
            field="full_name",
            ocr_value=ocr.full_name,
            submitted_value=submitted.get("full_name", ""),
            match_score=_fuzzy_score(ocr.full_name, submitted.get("full_name", "")),
            is_critical=False,
        ),
    ]

    critical_mismatch = any(f.is_critical and f.match_score < 0.9 for f in fields)
    confidence = sum(f.match_score for f in fields) / len(fields) if fields else 0.0

    return DocumentMatchResult(
        document_type="license",
        confidence=round(confidence, 3),
        matched_fields=fields,
        critical_mismatch=critical_mismatch,
    )


def _match_vehicle_reg(ocr: VehicleRegistrationExtraction, submitted: dict) -> DocumentMatchResult:
    fields: list[FieldMatchResult] = [
        FieldMatchResult(
            field="license_plate",
            ocr_value=ocr.license_plate,
            submitted_value=submitted.get("license_plate", ""),
            match_score=_exact_score(ocr.license_plate, submitted.get("license_plate", "")),
            is_critical=True,
        ),
        FieldMatchResult(
            field="registration_number",
            ocr_value=ocr.registration_number,
            submitted_value=submitted.get("registration_number", ""),
            match_score=_fuzzy_score(ocr.registration_number, submitted.get("registration_number", "")),
            is_critical=False,
        ),
    ]

    critical_mismatch = any(f.is_critical and f.match_score < 0.9 for f in fields)
    confidence = sum(f.match_score for f in fields) / len(fields) if fields else 0.0

    return DocumentMatchResult(
        document_type="vehicle_reg",
        confidence=round(confidence, 3),
        matched_fields=fields,
        critical_mismatch=critical_mismatch,
    )


def _cross_checks(
    cccd: CCCDExtraction,
    license_data: LicenseExtraction,
    vehicle_reg: VehicleRegistrationExtraction,
) -> tuple[bool, list[str]]:
    """Run cross-document consistency checks. Returns (passed, rejection_reasons)."""
    reasons: list[str] = []

    # CCCD name must fuzzy-match license name
    name_score = _fuzzy_score(cccd.full_name, license_data.full_name)
    if name_score < 0.85:
        reasons.append(f"Tên trên CCCD và GPLX không khớp (score={name_score:.2f})")

    # CCCD DOB must match license DOB
    if cccd.date_of_birth and license_data.date_of_birth:
        if cccd.date_of_birth != license_data.date_of_birth:
            reasons.append("Ngày sinh trên CCCD và GPLX không khớp")

    # Vehicle owner ID must match CCCD id_number (if present)
    if vehicle_reg.owner_id_number and cccd.id_number:
        if vehicle_reg.owner_id_number != cccd.id_number:
            reasons.append("Số CCCD trên đăng ký xe không khớp với CCCD của tài xế")

    # License grade must be valid for truck driving
    if license_data.license_grade and license_data.license_grade.upper() not in _TRUCK_VALID_GRADES:
        reasons.append(f"Hạng GPLX {license_data.license_grade} không đủ điều kiện lái xe tải")

    # License must not be expired
    if license_data.expiry_date:
        try:
            expiry = date.fromisoformat(license_data.expiry_date)
            if expiry < date.today():
                reasons.append(f"GPLX đã hết hạn ngày {license_data.expiry_date}")
        except ValueError:
            pass

    # Vehicle registration must not be expired
    if vehicle_reg.expiry_date:
        try:
            expiry = date.fromisoformat(vehicle_reg.expiry_date)
            if expiry < date.today():
                reasons.append(f"Đăng ký xe đã hết hạn ngày {vehicle_reg.expiry_date}")
        except ValueError:
            pass

    return len(reasons) == 0, reasons


def compute_overall_verification(
    driver_id: UUID,
    cccd: CCCDExtraction,
    license_data: LicenseExtraction,
    vehicle_reg: VehicleRegistrationExtraction,
    submitted: dict,
) -> OverallVerificationResult:
    cccd_match = _match_cccd(cccd, submitted)
    license_match = _match_license(license_data, submitted)
    vehicle_match = _match_vehicle_reg(vehicle_reg, submitted)

    # Weighted confidence: CCCD 40% + license 40% + vehicle 20%
    overall_confidence = round(
        cccd_match.confidence * 0.4
        + license_match.confidence * 0.4
        + vehicle_match.confidence * 0.2,
        3,
    )

    cross_passed, cross_reasons = _cross_checks(cccd, license_data, vehicle_reg)

    rejection_reasons: list[str] = list(cross_reasons)
    if cccd_match.critical_mismatch:
        rejection_reasons.append("CCCD có trường quan trọng không khớp")
    if license_match.critical_mismatch:
        rejection_reasons.append("GPLX có trường quan trọng không khớp")
    if vehicle_match.critical_mismatch:
        rejection_reasons.append("Đăng ký xe có trường quan trọng không khớp")

    threshold_verified = settings.ocr_confidence_threshold_verified
    threshold_manual = settings.ocr_confidence_threshold_manual_review

    if overall_confidence < threshold_manual or (not cross_passed and any("hết hạn" in r or "không đủ" in r for r in cross_reasons)):
        status = "rejected"
    elif overall_confidence >= threshold_verified and cross_passed:
        status = "ocr_verified"
    else:
        status = "manual_review"

    logger.info(
        "verification_computed",
        driver_id=str(driver_id),
        overall_confidence=overall_confidence,
        status=status,
        cross_passed=cross_passed,
    )

    return OverallVerificationResult(
        driver_id=driver_id,
        cccd_match=cccd_match,
        license_match=license_match,
        vehicle_reg_match=vehicle_match,
        overall_confidence=overall_confidence,
        cross_checks_passed=cross_passed,
        status=status,
        rejection_reasons=rejection_reasons,
    )
